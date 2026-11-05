using IgiQoL.UI;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.Simulation.World.System;
using Source.Spacestation;
using Source.UI.Tooltip;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.ConquestDetails;

// The sections the feature adds to the galaxy map's hover cards: a war-zone system, an NPC or Empire fleet, a warship
// delivery, and a station in the war zone.
//
// The game lays a card row out as one 300-unit line: the badge first, then the label (at most 180 units), then the
// value in what is left, and it cuts text that doesn't fit with no ellipsis. So a row here keeps its label, value and
// badge to about 30 characters together; anything longer is a section description or a card note, which wrap.
internal static class WarCards
{
    internal const string SummaryTitle = "War";
    internal const string DefenseTitle = "Defense Breakdown";
    internal const string TakingTitle = "Taking This System";
    internal const string BattleTitle = "Battle Here";
    internal const string FleetTitle = "War Status";
    internal const string StationTitle = "In the War";
    internal const string VerdictLabel = "What to do";

    private static readonly DefenseParts Parts = new();
    private static readonly Reach Targets = new();

    private static Color Muted => ColorHelper.poiTooltipMuted;

    // The system's own war block right below the game's conquest section: one short section by default, the whole
    // breakdown when `full` is set (the detail key is held, or the setting asks for it, or the War overview shows it).
    internal static void AddSystem(TooltipCard card, ConquestSystem cs, bool full)
    {
        Parts.Read(cs);
        var d = Parts;
        int at = card.sections.Count;
        string conquestTitle = Translation.Translate(Behaviour.UI.Tooltip.MapTooltipCardBuilder.GetConquestSectionTitleKey(cs));
        for (int i = 0; i < card.sections.Count; i++)
            if (card.sections[i].title == conquestTitle) { at = i + 1; break; }

        var tracked = WarMath.AutoTracked();
        Targets.Build(tracked);
        var advice = tracked != null ? WarAdvice.Compute(d, tracked, Targets) : default;
        var war = WarSection(d, advice, full);
        if (!war.isEmpty) card.sections.Insert(at++, war);
        if (full && !cs.isUnowned && !cs.cannotBeTaken)
        {
            card.sections.Insert(at++, DefenseSection(d));
            var taking = TakingSection(d);
            if (taking != null) card.sections.Insert(at++, taking);
            var battle = BattleSection(cs, d);
            if (battle != null) card.sections.Insert(at, battle);
        }
        if (!full) card.AddHint("Hold " + Popout.KeyName(ConquestDetails.Instance.DetailKey.Value), "for the full breakdown");
    }

    // What to do, the numbers behind it, and a running fight. The card's header shows whose attack the advice is for.
    private static TooltipSection WarSection(DefenseParts d, Advice advice, bool full)
    {
        var cs = d.System;
        var s = new TooltipSection(SummaryTitle);
        bool has = advice.Tracked != null && advice.Kind != AdviceKind.Own;
        if (has)
        {
            s.count = Translation.Translate(advice.Tracked.name);
            s.countColor = advice.Tracked.color;
            s.rows.Add(new TooltipRow(VerdictLabel, advice.Text, advice.Color));
        }
        if (cs.isUnowned)
        {
            if (cs.abandonedBy != null) s.rows.Add(new TooltipRow("Abandoned by", Translation.Translate(cs.abandonedBy.name), cs.abandonedBy.color));
            if (full && has) s.description = WarAdvice.Detail(advice, d);
            return s;
        }
        if (cs.cannotBeTaken)
        {
            if (!has) s.rows.Add(new TooltipRow(VerdictLabel, advice.Text ?? (cs.isPlayerCapital ? "Your capital" : "Can't be taken"), Muted));
            if (full) s.description = WarAdvice.Detail(new Advice { Kind = AdviceKind.CannotTake, Tracked = advice.Tracked }, d);
            return s;
        }

        s.rows.Add(new TooltipRow("Next battle", WarMath.Points(d.NextBattle), Color.white));
        int fleets = WarMath.FleetsToStart(d.NextBattle);
        s.rows.Add(new TooltipRow("Attack starts at", WarMath.Points(WarMath.ToStart(d.NextBattle)) + " (" + fleets + (fleets == 1 ? " fleet)" : " fleets)"),
            fleets == 1 ? ColorHelper.greenish : ColorHelper.orange75));
        s.rows.Add(new TooltipRow("Points left to capture", WarMath.Points(d.BrokenDefense * WarMath.CaptureMargin), Color.white));
        var conquest = WarMath.Conquest;
        if (cs.isSettling && conquest != null)
            s.rows.Add(new TooltipRow("Firepower halved", "for " + WarMath.Timer(conquest.conquestTickTime + (cs.settlingTicks - 1) * 3600f), ColorHelper.reddish));
        if (full)
        {
            if (has) s.description = WarAdvice.Detail(advice, d);
        }
        else
        {
            var battle = FirstBattle(cs);
            if (battle != null) s.rows.Add(new TooltipRow(StageName(battle.stage), WarMath.Clock(battle.timeRemaining) + " left", ColorHelper.reddish));
        }
        return s;
    }

    private static BattleInstance FirstBattle(ConquestSystem cs)
    {
        var universe = WarMath.Universe;
        if (universe == null) return null;
        foreach (var battle in universe.battleManager.activeBattles)
            if (battle.targetSystemGuid == cs.system.guid) return battle;
        return null;
    }

    private static TooltipSection DefenseSection(DefenseParts d)
    {
        var cs = d.System;
        var s = new TooltipSection(DefenseTitle) { count = "= " + WarMath.Points(d.Total), countColor = Muted };
        s.rows.Add(new TooltipRow("Station firepower", WarMath.Points(d.StationTerm), Color.white));
        s.rows.Add(Sub("Hub", WarMath.Points(d.HubFirepower) + (d.Estimated ? " (estimate)" : "")));
        if (d.DefenseStations > 0) s.rows.Add(Sub("Defense stations (" + d.DefenseStations + ")", WarMath.Points(d.StationsFirepower)));
        if (cs.isSettling) s.rows.Add(Sub("Just changed hands", "x0.5", ColorHelper.reddish));
        if (d.Capped) s.rows.Add(Sub("Station cap " + WarMath.Points(WarMath.FirepowerCap * d.Multiplier), "has " + WarMath.Points(d.Firepower), Muted));
        if (d.Structure < 1f) s.rows.Add(Sub("Structure left", Mathf.RoundToInt(d.Structure * 100f) + "%", ColorHelper.reddish));
        if (d.AtFloor) s.rows.Add(Sub("Station minimum", WarMath.Points(WarMath.FirepowerFloor * d.Multiplier), Muted));
        if (!Mathf.Approximately(d.WarEffort, 1f))
            s.rows.Add(Sub("War effort", "x" + d.WarEffort.ToString("0.00"), d.WarEffort > 1f ? ColorHelper.greenish : ColorHelper.reddish));
        if (!Mathf.Approximately(d.Multiplier, 1f) || d.Flat != 0f)
            s.rows.Add(Sub("Sector bonus", "x" + d.Multiplier.ToString("0.00") + (d.Flat != 0f ? " +" + WarMath.Points(d.Flat) : ""), ColorHelper.greenishLight));
        if (d.DefenseStations > 0)
            s.rows.Add(new TooltipRow("Garrisons", WarMath.Points(d.Garrison), Color.white)
                .WithBadge(d.GarrisonShips + " of " + d.GarrisonWanted, Muted));
        if (d.FleetCount > 0)
            s.rows.Add(new TooltipRow("Defending fleets", WarMath.Points(d.Fleets), Color.white).WithBadge(d.FleetCount.ToString(), Muted));
        if (d.PatrolCount > 0)
            s.rows.Add(new TooltipRow("Patrol ships", WarMath.Points(d.Patrols), Color.white).WithBadge(d.PatrolCount.ToString(), Muted));
        return s;
    }

    // What a siege needs, beyond the summary: fleets on the way, the one-fleet estimate, and the rules as sentences.
    private static TooltipSection TakingSection(DefenseParts d)
    {
        var cs = d.System;
        var s = new TooltipSection(TakingTitle);
        if (d.Relief > 0f) s.rows.Add(new TooltipRow("Fleets on the way", WarMath.Points(d.Relief), Color.white));
        if (WarMath.CanEstimate(d)) AddEstimate(s, WarMath.EstimateHubSiege(d, WarMath.FullFleet));
        var conquest = WarMath.Conquest;
        if (cs.isInUnrest && conquest != null)
            s.rows.Add(new TooltipRow("No taxes for", WarMath.Timer(conquest.conquestTickTime + (cs.settlingTicks - 5) * 3600f), ColorHelper.reddish));
        s.description = "Fights in order: " + Stages(d) + ". A siege captures after at least 10 minutes, once every defender is dead and the hub is broken, "
            + "with the attackers still holding " + WarMath.Points(d.BrokenDefense * WarMath.CaptureMargin) + " points. "
            + "A fleet, or an assault, attacks at 1.15 times the next battle; a full fleet is " + WarMath.Points(WarMath.FullFleet) + " points.";
        return s;
    }

    // The siege stages in the order they run: a clash with the first defending fleet, one siege per Defense station
    // (weakest first), then the hub.
    private static string Stages(DefenseParts d)
    {
        string text = "";
        if (d.FleetCount > 0) text = "fleet clash, ";
        if (d.DefenseStations > 0) text += d.DefenseStations == 1 ? "Defense station, " : d.DefenseStations + " Defense stations, ";
        return text + "hub";
    }

    private static void AddEstimate(TooltipSection s, SiegeEstimate e)
    {
        switch (e.Result)
        {
            case SiegeResult.Captured:
                s.rows.Add(new TooltipRow("A full fleet", "captures in " + e.Rounds + " min", ColorHelper.greenish));
                s.rows.Add(Sub("keeps", WarMath.Points(e.PointsLeft) + " points", Muted));
                break;
            case SiegeResult.Skirmish:
                s.rows.Add(new TooltipRow("A full fleet", "ends in a skirmish", ColorHelper.orange75));
                break;
            case SiegeResult.Repelled:
                s.rows.Add(new TooltipRow("A full fleet", "is repelled", ColorHelper.reddish));
                break;
            default:
                s.rows.Add(new TooltipRow("A full fleet", "is destroyed", ColorHelper.reddish));
                break;
        }
    }

    // Every fight at the system: the stage, the clock, and the attackers' points against what the stage's end needs.
    private static TooltipSection BattleSection(ConquestSystem cs, DefenseParts d)
    {
        var universe = WarMath.Universe;
        if (universe == null) return null;
        TooltipSection s = null;
        foreach (var battle in universe.battleManager.activeBattles)
        {
            if (battle.targetSystemGuid != cs.system.guid) continue;
            s ??= new TooltipSection(BattleTitle);
            var attacker = Faction.Get(battle.attackerFactionId);
            string who = attacker != null ? Translation.Translate(attacker.name) : "?";
            s.rows.Add(new TooltipRow(StageName(battle.stage), WarMath.Clock(battle.timeRemaining) + " left", ColorHelper.reddish)
                { dotColor = attacker != null ? attacker.color : (Color?)null });
            s.rows.Add(Sub("Attacker", who));
            int points = BattleManager.GetAttackerPoints(battle);
            switch (battle.stage)
            {
                case BattleStage.FleetClash:
                    s.rows.Add(Sub("Attackers", WarMath.Points(points)));
                    s.rows.Add(Sub("Defenders", WarMath.Points(DefenderFleetPoints(battle))));
                    s.description ??= "More points wins the fleet clash when it ends at 10 minutes.";
                    break;
                case BattleStage.StationSiege:
                    s.rows.Add(Sub("Structure", Mathf.RoundToInt(battle.stationHullFraction * 100f) + "%"));
                    if (d.Garrison + d.Fleets + d.Patrols > 0f) s.rows.Add(Sub("Defenders left", WarMath.Points(d.Garrison + d.Fleets + d.Patrols)));
                    s.rows.Add(Sub("Attackers hold", WarMath.Points(points) + " of " + WarMath.Points(d.Total * WarMath.CaptureMargin)));
                    break;
                case BattleStage.DefenseStationSiege:
                    s.rows.Add(Sub("Structure", Mathf.RoundToInt(battle.stationHullFraction * 100f) + "%"));
                    if (GalaxyMapData.current?.GetPointOfInterest(battle.targetStationGuid) is FactionStation station)
                        s.rows.Add(Sub("Attackers hold", WarMath.Points(points) + " of " + WarMath.Points(ConquestCombat.GetDefenseStationStrength(cs, station, battle.stationHullFraction) * WarMath.CaptureMargin)));
                    break;
                case BattleStage.Raid:
                    s.rows.Add(Sub("Attackers hold", WarMath.Points(points)));
                    break;
            }
        }
        return s;
    }

    private static int DefenderFleetPoints(BattleInstance battle)
    {
        var faction = Faction.Get(battle.defenderFactionId);
        var economy = faction != null ? WarMath.Universe?.PeekFactionEconomy(faction) : null;
        if (economy == null) return 0;
        foreach (var fleet in economy.fleets)
            if (fleet.guid == battle.defenderFleetGuid) return fleet.totalPoints;
        return 0;
    }

    internal static string StageName(BattleStage stage) => stage switch
    {
        BattleStage.FleetClash => "Fleet clash",
        BattleStage.DefenseStationSiege => "Defense station siege",
        BattleStage.StationSiege => "Hub siege",
        BattleStage.Raid => "Raid",
        _ => stage.ToString(),
    };

    // An NPC or Empire fleet: how far it is from retreating, what its order really is, and what its target asks of it.
    internal static TooltipSection FleetSection(ConquestFleet fleet)
    {
        var s = new TooltipSection(FleetTitle);
        float target = fleet.targetPoints;
        int points = fleet.totalPoints;
        if (target > 0f)
        {
            s.rows.Add(new TooltipRow("Of full strength", WarMath.Points(points) + " of " + WarMath.Points(target), Color.white)
                .WithFill(points / target, points < target * ConquestWarTuning.RetreatThreshold ? ColorHelper.reddish : ColorHelper.greenish));
            s.rows.Add(Sub("Retreats below", WarMath.Points(target * ConquestWarTuning.RetreatThreshold) + " (35%)"));
            s.rows.Add(Sub("Back on duty at", WarMath.Points(target * ConquestWarTuning.RetreatRecoveredThreshold) + " (75%)"));
        }
        // The meaning of the map's label can run long, so it's the section's wrapping description.
        s.description = Join(OrderMeaning(fleet), DefenderLever(fleet));
        AddTargetRows(s, fleet);
        if (fleet.reattackCooldownSeconds > 0f && fleet.reattackBlockedSystemGuid != null)
        {
            var blocked = GalaxyMapData.current?.GetSystem(fleet.reattackBlockedSystemGuid);
            if (blocked != null)
            {
                s.rows.Add(new TooltipRow("Blocked from", blocked.name, ColorHelper.orange75));
                s.rows.Add(Sub("for", WarMath.Clock(fleet.reattackCooldownSeconds), Muted));
            }
        }
        if (fleet.carriedDamage >= 1f)
        {
            s.rows.Add(new TooltipRow("Unrepaired damage", WarMath.Points(fleet.carriedDamage), Muted));
            s.rows.Add(Sub("repairs at home", "100 an hour", Muted));
        }
        if (!fleet.isEmpireFleet)
        {
            int level = FleetLevels.GetFleetLevel(fleet), npc = FleetLevels.GetNpcFleetLevel();
            if (FleetLevels.IsDueRefit(level, npc)) s.rows.Add(new TooltipRow("Due for a refit", "to level " + npc, ColorHelper.orange75));
        }
        return s;
    }

    // The map's "Reinforcing" label covers three different orders; this says which one. Two other labels get a line.
    internal static string OrderMeaning(ConquestFleet fleet)
    {
        var dest = fleet.destinationSystem?.name;
        if (fleet.state == ConquestFleetState.Retreating)
            return "Resting after a retreat; back on duty at 75% of full strength.";
        if (fleet.state == ConquestFleetState.Traveling && fleet.orderReason == ConquestFleetOrderReason.Reinforcing)
            return fleet.totalPoints < fleet.targetPoints * ConquestWarTuning.RetreatThreshold
                ? "Retreating to " + dest + " to recover, under 35% of full strength."
                : "Moving up to the frontline at " + dest + ".";
        if (fleet.state == ConquestFleetState.Idle && fleet.orderReason == ConquestFleetOrderReason.Loitering)
            return "Idle; it gets a new order at the next Fleet orders countdown (every 10 minutes, or after a battle).";
        return null;
    }

    private static string Join(string a, string b) => a == null ? b : b == null ? a : a + " " + b;

    // A fleet defending a system the tracked faction wants: what destroying it does to that faction's attack.
    internal static string DefenderLever(ConquestFleet fleet)
    {
        var cs = WarMath.Of(fleet.currentSystem);
        var tracked = WarMath.AutoTracked();
        if (cs == null || tracked == null || cs.isUnowned || cs.faction == tracked || fleet.faction == tracked) return null;
        bool defends = false;
        foreach (var f in ConquestCombat.GetDefendingFleets(cs))
            if (f == fleet) { defends = true; break; }
        if (!defends) return null;
        Targets.Build(tracked);
        if (!Targets.Allowed.Contains(cs)) return null;
        float next = ConquestCombat.GetNextBattleStrength(cs);
        int before = WarMath.FleetsToStart(next), after = WarMath.FleetsToStart(next - FleetMixBonus.GetBattleStrength(fleet));
        if (after >= before) return null;
        return "Destroying it takes " + Translation.Translate(tracked.name) + "'s attack on " + cs.system.name + " from " + before + " full fleets to " + after + ".";
    }

    private static void AddTargetRows(TooltipSection s, ConquestFleet fleet)
    {
        ConquestSystem target = null;
        float points = fleet.totalPoints;
        string label = null;
        if (fleet.isInAssault)
        {
            target = WarMath.Of(GalaxyMapData.current?.GetSystem(fleet.assaultTargetGuid));
            var staging = GalaxyMapData.current?.GetSystem(fleet.assaultStagingGuid);
            points = AssaultPoints(fleet);
            if (staging != null) s.rows.Add(new TooltipRow("Assault gathers at", staging.name, Color.white));
            label = "Assault strength";
        }
        else if (fleet.state == ConquestFleetState.Traveling && fleet.orderReason == ConquestFleetOrderReason.Attacking)
        {
            target = WarMath.Of(fleet.destinationSystem);
            label = "Strength";
        }
        if (target == null || target.isUnowned) return;
        Parts.Read(target);
        float need = WarMath.ToStart(Parts.NextBattle);
        s.rows.Add(new TooltipRow("Target", target.system.name, Color.white));
        s.rows.Add(Sub("Next battle", WarMath.Points(Parts.NextBattle)));
        s.rows.Add(Sub(label, WarMath.Points(points) + " of " + WarMath.Points(need), points >= need ? ColorHelper.greenish : ColorHelper.reddish));
    }

    private static float AssaultPoints(ConquestFleet fleet)
    {
        var economy = WarMath.Universe?.PeekFactionEconomy(fleet.faction);
        if (economy == null) return fleet.totalPoints;
        float sum = 0f;
        foreach (var f in economy.fleets)
            if (f.assaultTargetGuid == fleet.assaultTargetGuid) sum += f.totalPoints;
        return sum;
    }

    // A warship delivery also shows "Reinforcing" on the map; the card says what it is in a wrapping note.
    internal const string DeliveryNote = "A new warship on its way to its fleet or garrison.";

    // A hub, Defense station or economic outpost in the war zone.
    internal static TooltipSection StationSection(SpaceStation station, ConquestSystem cs)
    {
        var s = new TooltipSection(StationTitle);
        var fs = station as FactionStation;
        bool hub = station == cs.station;
        bool defense = fs != null && fs.stationType == PlayerStationType.Defense;
        if (hub || defense)
        {
            var profile = StationDefense.GetStationProfile(station);
            string est = station.isContentGenerated ? "" : " (estimate)";
            s.rows.Add(new TooltipRow("Firepower", WarMath.Points(profile.firepower) + est, Color.white));
            s.rows.Add(new TooltipRow("Structure", WarMath.Points(profile.hull) + est, Color.white));
            float hp = ConquestWarTuning.LiveStationHpMultiplier + (hub && cs.headquarters ? ConquestWarTuning.LiveHeadquartersHpBonus : 0f);
            s.rows.Add(new TooltipRow(HitPointsLabel, "x" + hp.ToString("0"), Color.white));
        }
        if (defense)
        {
            s.rows.Add(new TooltipRow("Garrison", fs.garrisonShips.Count + " of " + FactionGarrison.GetWantedCount(fs) + " ships", Color.white));
            s.rows.Add(Sub("Points", WarMath.Points(FactionGarrison.GetStrength(fs)), Muted));
            if (fs.garrisonShips.Count > 0)
            {
                int level = FleetLevels.GetGarrisonLevel(fs), npc = FleetLevels.GetNpcFleetLevel();
                s.rows.Add(Sub("Ship level", level + (FleetLevels.IsDueRefit(level, npc) ? ", refit due" : ""), Muted));
            }
            s.rows.Add(new TooltipRow("Destroying it", "-5,000 reputation", ColorHelper.reddish));
            s.rows.Add(Sub("if already Hostile", "-1,000", Muted));
            s.description = "Killing the hub abandons the system. When another faction then claims it, its Defense stations are razed, so you can leave this one.";
        }
        else if (hub)
        {
            s.rows.Add(new TooltipRow("Destroying it", "abandons the system", ColorHelper.orange75));
            s.rows.Add(Sub("Each part", "-100 reputation", Muted));
            s.rows.Add(Sub("  at -500 or lower", "-5", Muted));
            AddClaimRows(s, HubKill.Predict(cs));
        }
        else if (fs != null)
        {
            s.rows.Add(station.raidCooldown > 0f
                ? new TooltipRow("Raid cooldown", WarMath.Clock(station.raidCooldown), ColorHelper.orange75)
                : new TooltipRow("Raids", "can be raided now", Muted));
        }
        return s.rows.Count > 0 ? s : null;
    }

    internal const string HitPointsLabel = "Hit points in a fight";
    internal const string ClaimLabel = "Claimed next by";

    // Who gets the system once the hub is gone, and how.
    private static void AddClaimRows(TooltipSection s, in HubKill h)
    {
        if (h.Result == HubKillResult.None) return;
        if (h.Offer)
        {
            s.rows.Add(new TooltipRow("Claim offer", "to you first", ColorHelper.greenish));
            s.description = "\"" + ClaimLabel + "\" is who takes the system if you decline the claim offer.";
        }
        switch (h.Result)
        {
            case HubKillResult.NoFleet:
                s.rows.Add(new TooltipRow(ClaimLabel, "no one yet", Muted));
                break;
            case HubKillResult.TossUp:
                s.rows.Add(new TooltipRow(ClaimLabel, "toss-up", ColorHelper.orange75));
                s.rows.Add(Sub(Translation.Translate(h.Claimer.name), ShortJumps(h.Jumps), Muted));
                s.rows.Add(Sub(Translation.Translate(h.Rival.name), ShortJumps(h.RivalJumps), Muted));
                break;
            case HubKillResult.Claims:
                s.rows.Add(new TooltipRow(ClaimLabel, Translation.Translate(h.Claimer.name), h.Claimer.color));
                s.rows.Add(h.Siege ? Sub("its hub siege", "at once", Muted) : Sub("its fleet, " + ShortJumps(h.Jumps), "in " + WarMath.Clock(h.Seconds), Muted));
                break;
        }
    }

    private static string ShortJumps(int n) => n < 0 ? "on the way" : n == 0 ? "in the system" : n == 1 ? "1 jump" : n + " jumps";

    private static TooltipRow Sub(string label, string value, Color? color = null) => new("  " + label, value, color ?? Color.white);
}
