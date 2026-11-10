using System.Collections.Generic;
using Source.Galaxy;
using Source.Simulation.Story;
using Source.Simulation.World.System;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.ConquestDetails;

internal enum AdviceKind
{
    Own, Free, Battle, CannotTake, NotOnFront, Skipped, OnTheWay,
    LeaveIt, LeaveStart, FallsShort, KillFleet, Skip, KillHub,
}

// What the player's time is worth at one target system of a faction, from the numbers the game's own functions give.
// Every figure is exact for what it names: the fleets an attack needs come from the next-battle strength judged against
// the attacker's war footing (WarFooting.GetJudgedStrength), and the fleet lever removes the defending fleets' strength.
// The game has no per-system lever beyond those: war footing and war fleets belong to the whole faction. A war fleet
// also attacks the weakest target it can reach when it can't beat any; Skip says so while the faction has one out.
internal struct Advice
{
    public AdviceKind Kind;
    public Faction Tracked;
    public string Text;
    public Color Color;
    // Full fleets the attack needs now, and after the lever the advice names.
    public int Fleets, FleetsAfter;
    public float NextAfter;
    public SiegeEstimate Siege;
    public BattleStage Stage;
    public float TimeLeft;
    // What destroying the hub would do, read for the verdicts a hub kill can beat.
    public HubKill Hub;

    // Orange: the player's help changes the outcome.
    public bool Helps => Kind is AdviceKind.KillFleet or AdviceKind.FallsShort or AdviceKind.KillHub;
    // Help first, then what the faction handles alone, then what isn't worth a visit.
    public int Rank => Helps ? 0 : Kind is AdviceKind.Skip or AdviceKind.Skipped or AdviceKind.NotOnFront or AdviceKind.CannotTake ? 2 : 1;
    // Among the help: a lever that leaves one fleet first, then a hub kill, then the levers that leave more fleets, a
    // lone fleet that falls short last.
    public int Order => Kind switch
    {
        AdviceKind.FallsShort => int.MaxValue,
        AdviceKind.KillHub => 3,
        _ => FleetsAfter * 2,
    };
}

// The attack targets of one faction: the list its fleets pick from, and which of them the under-10% rule leaves.
internal sealed class Reach
{
    public Faction Faction;
    public readonly List<ConquestSystem> List = new();
    public readonly HashSet<ConquestSystem> Allowed = new();
    private readonly HashSet<ConquestSystem> all = new();

    public void Build(Faction faction)
    {
        Faction = faction;
        all.Clear();
        List.Clear();
        if (faction == null)
        {
            Allowed.Clear();
            return;
        }
        List.AddRange(WarMath.AttackCandidates(faction, Allowed));
        foreach (var cs in List) all.Add(cs);
    }

    public bool Contains(ConquestSystem cs) => all.Contains(cs);
}

internal static class WarAdvice
{
    internal static Advice Compute(DefenseParts d, Faction tracked, Reach reach)
    {
        var cs = d.System;
        var a = new Advice { Tracked = tracked, Color = ColorHelper.poiTooltipMuted };
        if (cs.cannotBeTaken)
        {
            a.Kind = AdviceKind.CannotTake;
            a.Text = cs.isPlayerCapital ? "Your capital" : "Can't be taken";
            return a;
        }
        if (tracked != null && cs.faction == tracked)
        {
            a.Kind = AdviceKind.Own;
            return a;
        }
        var universe = WarMath.Universe;
        if (universe != null)
            foreach (var battle in universe.battleManager.activeBattles)
                if (battle.targetSystemGuid == cs.system.guid)
                {
                    a.Kind = AdviceKind.Battle;
                    a.Stage = battle.stage;
                    a.TimeLeft = battle.timeRemaining;
                    a.Text = "Battle running";
                    a.Hub = HubKill.Predict(cs);
                    if (a.Hub.Siege && CanKillHub(cs, tracked, a.Hub)) SetKillHub(ref a);
                    return a;
                }
        if (cs.isUnowned)
        {
            a.Kind = AdviceKind.Free;
            a.Text = "Free: claimed on arrival";
            a.Color = ColorHelper.greenish;
            return a;
        }
        if (reach != null && tracked != null)
        {
            if (!reach.Contains(cs))
            {
                a.Kind = AdviceKind.NotOnFront;
                a.Text = "Not on the front";
                return a;
            }
            if (!reach.Allowed.Contains(cs))
            {
                a.Kind = AdviceKind.Skipped;
                a.Text = "Skipped: under-10% rule";
                return WithHubKill(a, cs, tracked);
            }
        }
        if (tracked != null && WarMath.Incoming(tracked, cs))
        {
            a.Kind = AdviceKind.OnTheWay;
            a.Text = "Fleet already on the way";
            a.Color = ColorHelper.greenish;
            return a;
        }

        a.Fleets = WarMath.FleetsToStart(d.NextBattle, tracked);
        if (a.Fleets == 1)
        {
            if (WarMath.CanEstimate(d))
            {
                a.Siege = WarMath.EstimateHubSiege(d, WarMath.FullFleetStrength(tracked));
                if (a.Siege.Result == SiegeResult.Captured)
                {
                    a.Kind = AdviceKind.LeaveIt;
                    a.Text = "Leave it: 1 fleet takes it";
                    a.Color = ColorHelper.greenish;
                    return a;
                }
                a.Kind = AdviceKind.FallsShort;
                a.Text = "Help: 1 fleet falls short";
                a.Color = ColorHelper.orange75;
                return WithHubKill(a, cs, tracked);
            }
            a.Kind = AdviceKind.LeaveStart;
            a.Text = "Leave it: 1 fleet starts";
            a.Color = ColorHelper.greenish;
            return a;
        }

        // An assault. One lever could bring it down: kill the fleets defending the system.
        int afterKill = d.Fleets > 0f ? WarMath.FleetsToStart(d.NextBattle - d.Fleets, tracked) : a.Fleets;
        if (afterKill < a.Fleets)
        {
            a.Kind = AdviceKind.KillFleet;
            a.FleetsAfter = afterKill;
            a.NextAfter = d.NextBattle - d.Fleets;
            a.Text = "Help: kill its fleet";
            a.Color = ColorHelper.orange75;
            return a;
        }
        a.Kind = AdviceKind.Skip;
        a.FleetsAfter = afterKill;
        a.Text = "Skip: needs " + a.Fleets + " fleets";
        return WithHubKill(a, cs, tracked);
    }

    // A system no lever brings within one fleet's reach: destroying its hub may still hand it to the faction.
    private static Advice WithHubKill(Advice a, ConquestSystem cs, Faction tracked)
    {
        if (tracked == null) return a;
        a.Hub = HubKill.Predict(cs);
        if (CanKillHub(cs, tracked, a.Hub)) SetKillHub(ref a);
        return a;
    }

    // The faction gets the system, and you can shoot the hub: a plain click only targets an enemy's station.
    private static bool CanKillHub(ConquestSystem cs, Faction tracked, in HubKill hub) =>
        tracked != null && hub.Result == HubKillResult.Claims && hub.Claimer == tracked && cs.faction != null && cs.faction.IsEnemy(Faction.player);

    private static void SetKillHub(ref Advice a)
    {
        a.Kind = AdviceKind.KillHub;
        a.Text = "Help: kill the hub";
        a.Color = ColorHelper.orange75;
    }

    // The sentence under the verdict on the full card.
    internal static string Detail(in Advice a, DefenseParts d)
    {
        var cs = d.System;
        string who = a.Tracked != null ? Translation.Translate(a.Tracked.name) : "The faction";
        switch (a.Kind)
        {
            case AdviceKind.CannotTake:
                return cs.isPlayerCapital
                    ? "No fleet can take your capital."
                    : "No fleet can take a headquarters, and NPC fleets leave it off their target lists. Its hub has x30 hit points when you fight it.";
            case AdviceKind.Battle:
                return "A " + WarCards.StageName(a.Stage).ToLowerInvariant() + " is running here with " + WarMath.Clock(a.TimeLeft) + " left. Nothing to add unless you want to join it."
                    + (a.Hub.Siege ? " " + HubOutcome(a, cs, who) : "");
            case AdviceKind.Free:
                return "No owner. The first fleet to arrive claims it with no fight; fleets of factions next door go for unowned systems first.";
            case AdviceKind.NotOnFront:
                return "None of " + who + "'s systems border it, so its fleets can't pick it as a target yet.";
            case AdviceKind.Skipped:
                return who + " holds under 10% of the war zone, so its fleets only attack unowned systems and factions holding 10% or more. " + HubOutcome(a, cs, who);
            case AdviceKind.OnTheWay:
                return "A " + who + " fleet is already attacking or gathering for it. Leave it.";
            case AdviceKind.LeaveIt:
                return "One full fleet takes it in " + a.Siege.Rounds + " min with " + WarMath.Points(a.Siege.PointsLeft) + " points left, so " + who + " will do it on its own.";
            case AdviceKind.LeaveStart:
                return "One full fleet is enough to start the attack, so " + who + " will do it on its own. Defenders to kill first make the finish uncertain.";
            case AdviceKind.FallsShort:
                return "One full fleet can start the attack but " + FallsShortWhy(a.Siege.Result) + " and nothing you can change here brings it within reach. " + HubOutcome(a, cs, who);
            case AdviceKind.KillFleet:
                return "Killing the " + WarMath.Points(d.Fleets) + " points of fleets defending it takes the attack from " + a.Fleets + " full fleets to " + a.FleetsAfter + ".";
            case AdviceKind.Skip:
                return "It needs " + a.Fleets + " full fleets. Killing its defending fleets would leave " + FleetsAfterKill(d, a.Tracked) + ". "
                    + HubOutcome(a, cs, who) + WarFleetNote(a.Tracked, who);
            case AdviceKind.KillHub:
                return HubOutcome(a, cs, who);
        }
        return null;
    }

    // The verdicts judge the faction's regular fleets. A war fleet (ConquestFleet.isWarFleet) is the exception: when it
    // can't beat any target it raids a nearby outpost or attacks the weakest system it can reach (TryOrderRaid, TryAttackWeakest).
    private static string WarFleetNote(Faction tracked, string who)
    {
        var economy = tracked != null ? WarMath.Universe?.PeekFactionEconomy(tracked) : null;
        if (economy == null || WarFleets.CountActive(economy) == 0) return "";
        return " " + who + " has a war fleet out, though, and when it can't win it raids a nearby outpost or attacks the weakest system it can reach.";
    }

    // What destroying the hub would do for the faction, as one or two sentences.
    internal static string HubOutcome(in Advice a, ConquestSystem cs, string who)
    {
        var h = a.Hub;
        string text = HubResult(h, a.Tracked, cs, who);
        if (text.Length > 0 && h.Offer) text += " The game offers the claim to you first; this is what happens if you decline.";
        return text;
    }

    private static string HubResult(in HubKill h, Faction tracked, ConquestSystem cs, string who)
    {
        string owner = cs.faction != null ? Translation.Translate(cs.faction.name) : "its owner";
        bool forUs = h.Claimer == tracked && tracked != null;
        string claimer = h.Claimer != null ? Translation.Translate(h.Claimer.name) : "";
        switch (h.Result)
        {
            case HubKillResult.NoFleet:
                return "A hub kill wouldn't help yet: no faction next to it has a free fleet, so it would stay unowned until one has.";
            case HubKillResult.TossUp:
                return "A hub kill is a toss-up: fleets of " + claimer + " and " + Translation.Translate(h.Rival.name) + " would arrive at the same time, and the first to land claims it.";
            case HubKillResult.Claims:
                if (!forUs)
                {
                    if (h.Siege) return "Destroying the hub now hands the system to " + claimer + ", whose siege is at the hub.";
                    if (h.Claimer == cs.faction && h.Jumps == 0) return "A hub kill wouldn't help: " + owner + "'s own free fleet in the system would claim it straight back.";
                    return "A hub kill wouldn't help: a " + claimer + " fleet, " + Jumps(h.Jumps) + ", would claim it first.";
                }
                if (cs.faction == null || !cs.faction.IsEnemy(Faction.player))
                    return "A hub kill would hand it to " + who + ", but you can target the hub only once " + owner + " is your enemy.";
                if (h.Siege) return who + "'s siege is at the hub. Destroy the hub yourself and " + who + " takes the system at once.";
                return "Destroy its hub and the system has no owner. "
                    + (h.Jumps < 0
                        ? "A " + who + " fleet already on its way to attack it claims it when it lands, in about " + WarMath.Clock(h.Seconds) + ". "
                        : "The " + who + " fleet its next fleet orders send, " + Jumps(h.Jumps) + ", claims it in about " + WarMath.Clock(h.Seconds) + ". ")
                    + (h.Rival != null ? "The next faction to get there is " + Translation.Translate(h.Rival.name) + ". " : "No other faction has a free fleet that would go for it. ")
                    + "Leave its Defense stations: the claim razes them.";
        }
        return "";
    }

    // -1 is a fleet already flying in to attack it.
    private static string Jumps(int n) => n < 0 ? "already on its way to attack it" : n == 0 ? "already in the system" : n == 1 ? "1 jump away" : n + " jumps away";

    private static string FallsShortWhy(SiegeResult r) => r switch
    {
        SiegeResult.Skirmish => "ends in a skirmish with the hub standing",
        SiegeResult.Repelled => "is repelled",
        _ => "is destroyed",
    };

    private static int FleetsAfterKill(DefenseParts d, Faction attacker) =>
        d.Fleets > 0f ? WarMath.FleetsToStart(d.NextBattle - d.Fleets, attacker) : WarMath.FleetsToStart(d.NextBattle, attacker);
}
