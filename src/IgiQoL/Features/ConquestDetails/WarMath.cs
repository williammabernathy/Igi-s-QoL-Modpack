using System.Collections.Generic;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.MissionSystem.Objectives;
using Source.Player;
using Source.Simulation.Story;
using Source.Simulation.World.System;
using Source.Spacestation;
using UnityEngine;

namespace IgiQoL.Features.ConquestDetails;

// The parts of a war-zone system's Defense number and the figures NPC fleets plan with. Every total comes from the
// game's own function (ConquestCombat, StationDefense), so the card never disagrees with the game; the parts are
// read the same way the game sums them.
internal sealed class DefenseParts
{
    public ConquestSystem System;
    // Station firepower before settling, after the sector multiplier: the hub and the owner's Defense stations.
    public float HubFirepower, StationsFirepower;
    public int DefenseStations;
    // A station whose contents aren't generated yet scores a flat estimate by its size.
    public bool Estimated;
    // The profile the Defense number uses (settling applied): firepower, and the hull a hub siege has to break.
    public float Firepower, Hull;
    public float Multiplier, Flat, Structure;
    public float StationTerm;
    public float Garrison;
    public int GarrisonShips, GarrisonWanted;
    public float Fleets;
    public int FleetCount;
    public ConquestFleet FirstFleet;
    public float Patrols;
    public int PatrolCount;
    public float Total;
    public float NextBattle, Relief;
    public FactionStation Weakest;

    public bool Capped => Firepower > WarMath.FirepowerCap * Multiplier;
    public bool AtFloor => Mathf.Min(Firepower, WarMath.FirepowerCap * Multiplier) * Structure < WarMath.FirepowerFloor * Multiplier;
    // What the Defense number drops to once every defender is dead and the hub's structure is broken: the 250 floor
    // (ConquestCombat.BoundStationFirepower) with the sector's flat bonus. A siege captures when the attackers still
    // have 1.25 times this.
    public float BrokenDefense => WarMath.FirepowerFloor * Multiplier + Flat;

    public void Read(ConquestSystem cs)
    {
        System = cs;
        HubFirepower = StationsFirepower = 0f;
        DefenseStations = 0;
        Estimated = false;
        Garrison = Fleets = Patrols = 0f;
        GarrisonShips = GarrisonWanted = FleetCount = PatrolCount = 0;
        FirstFleet = null;
        Weakest = null;
        NextBattle = Relief = 0f;

        Multiplier = StationDefense.GetSectorDefenseMultiplier(cs.system);
        Flat = StationDefense.GetSectorDefenseFlat(cs.system);
        Structure = ConquestCombat.GetSiegeStructureFraction(cs);
        foreach (var station in StationDefense.GetDefendingStations(cs))
        {
            float fp = StationDefense.GetStationProfile(station).firepower;
            if (!station.isContentGenerated) Estimated = true;
            if (station == cs.station) HubFirepower += fp;
            else
            {
                StationsFirepower += fp;
                DefenseStations++;
            }
        }
        var profile = StationDefense.GetProfile(cs);
        Firepower = profile.firepower;
        Hull = profile.hull;
        StationTerm = ConquestCombat.GetStationFirepower(cs);
        foreach (var station in ConquestCombat.GetDefenseStations(cs))
        {
            Garrison += FactionGarrison.GetStrength(station);
            GarrisonShips += station.garrisonShips.Count;
            GarrisonWanted += FactionGarrison.GetWantedCount(station);
        }
        if (GamePlayer.current?.GetStoryteller<LivingUniverse>() != null)
        {
            foreach (var fleet in ConquestCombat.GetDefendingFleets(cs))
            {
                Fleets += FleetMixBonus.GetBattleStrength(fleet);
                FleetCount++;
                if (FirstFleet == null) FirstFleet = fleet;
            }
        }
        foreach (var patrol in ConquestCombat.GetPatrolShips(cs))
        {
            Patrols += PatrolFleet.GetShipPoints(patrol.ship);
            PatrolCount++;
        }
        Total = ConquestCombat.GetDefenseStrength(cs);
        if (cs.isUnowned) return;
        NextBattle = ConquestCombat.GetNextBattleStrength(cs);
        Relief = ConquestCombat.GetIncomingRelief(cs);
        Weakest = ConquestCombat.ChooseNextDefenseStation(cs);
    }
}

internal enum SiegeResult { Captured, Skirmish, Repelled, Destroyed }

// One hub siege played out with the average damage roll (5% of a side's strength each 60 s round) and no hull edge.
internal struct SiegeEstimate
{
    public SiegeResult Result;
    public int Rounds;
    public float PointsLeft;
}

internal static class WarMath
{
    // A fleet, or the fleets of an assault together, start an attack only with this many times the target's
    // next-battle strength (ConquestFleetOrders.CanBeat, PlanAssault).
    internal const float AttackMargin = ConquestFleetPlanner.EngageAdvantageMargin;
    internal const float CaptureMargin = ConquestWarTuning.SiegeCaptureMargin;
    internal const float FullFleet = ConquestWarTuning.FleetTargetPoints;
    internal const float FirepowerCap = ConquestWarTuning.MaxStationFirepower;
    internal const float FirepowerFloor = ConquestWarTuning.MinimumSiegeStrength;
    internal const float RoundSeconds = 60f;
    internal const float AverageDamageShare = 0.05f;
    internal const float Mission30 = 0.3f;

    internal static Conquest Conquest => GamePlayer.current?.GetStoryteller<Conquest>();
    internal static LivingUniverse Universe => GamePlayer.current?.GetStoryteller<LivingUniverse>();

    // The war-zone system behind a map system, or null outside the war zone.
    internal static ConquestSystem Of(SystemMapData system) =>
        system != null && system.sector != null && system.sector.isWarZone ? system.storyteller as ConquestSystem : null;

    // Judged strength an attack needs before it starts: 1.15 times the target's next battle
    // (ConquestFleetOrders.CanBeat, the gathering test in PlanAssault, TryAttack).
    internal static float ToStart(float nextBattle) => nextBattle * AttackMargin;

    // The attacker's war footing as the game applies it to a fleet's strength (WarFooting.GetMultiplier): 1 + the
    // faction's footing bonus, which is 0 for no faction, the player and any faction outside the Conquest war.
    internal static float Footing(Faction attacker) => WarFooting.GetMultiplier(attacker);

    // What one full fleet counts for when the game judges it: points x footing (WarFooting.GetJudgedStrength). The siege
    // tests sum the same judged strength (BattleManager.GetAttackerStrength); a round's damage uses
    // FleetMixBonus.GetBattleStrength, which equals it for an NPC fleet (the mix bonus only applies to Empire fleets).
    internal static float FullFleetStrength(Faction attacker) => FullFleet * Footing(attacker);

    // Raw fleet points a group needs before it starts an attack: the judged threshold divided by the footing.
    internal static float PointsToStart(float nextBattle, Faction attacker) => ToStart(nextBattle) / Footing(attacker);

    // Full 2000-point fleets an attack needs: 1 is a lone attack, 2 or more an assault.
    internal static int FleetsToStart(float nextBattle, Faction attacker) =>
        Mathf.Max(1, Mathf.CeilToInt(ToStart(nextBattle) / FullFleetStrength(attacker) - 0.0001f));

    // Only a hub siege with no defenders to kill first is simple enough to play out: no Defense stations (their sieges
    // come first) and no defending fleet or patrol (a fleet clash comes first).
    internal static bool CanEstimate(DefenseParts d) =>
        !d.System.isUnowned && !d.System.cannotBeTaken && d.DefenseStations == 0 && d.FleetCount == 0 && d.PatrolCount == 0 && d.Hull > 0f;

    // BattleSimulation.RunSiegeRound with the average roll: the attackers hit the structure, the station fires back with
    // the Defense it had at the start of the round. From 600 s on, a broken structure captures once the attackers hold
    // 1.25 times the Defense left (BattleManager.CanCaptureEarly, judged with WarFooting); at 1800 s the cap judge runs
    // (JudgeSiegeAtCap). attackers is the side's strength as the game sums it, so a full fleet is FullFleetStrength.
    internal static SiegeEstimate EstimateHubSiege(DefenseParts d, float attackers)
    {
        float m = d.Multiplier, cap = Mathf.Min(d.Firepower, FirepowerCap * m);
        float hull = d.Hull, a = attackers;
        int maxRounds = Mathf.RoundToInt(ConquestWarTuning.StationSiegeSeconds / RoundSeconds) - 1;
        int earliest = Mathf.RoundToInt(ConquestWarTuning.MinStationSiegeSeconds / RoundSeconds);
        float broken = d.BrokenDefense;
        for (int round = 1; round <= maxRounds; round++)
        {
            float defense = Mathf.Max(FirepowerFloor * m, cap * (hull / d.Hull)) + d.Flat;
            hull = Mathf.Max(0f, hull - a * AverageDamageShare);
            a -= defense * AverageDamageShare;
            if (a <= 0f) return new SiegeEstimate { Result = SiegeResult.Destroyed, Rounds = round, PointsLeft = 0f };
            if (round >= earliest && hull <= 0f && a >= broken * CaptureMargin)
                return new SiegeEstimate { Result = SiegeResult.Captured, Rounds = round, PointsLeft = a };
        }
        float end = Mathf.Max(FirepowerFloor * m, cap * (hull / d.Hull)) + d.Flat;
        var result = hull <= 0f && a >= end * CaptureMargin ? SiegeResult.Captured : a >= end ? SiegeResult.Skirmish : SiegeResult.Repelled;
        return new SiegeEstimate { Result = result, Rounds = maxRounds, PointsLeft = a };
    }

    // Systems a faction must own for the 30% missions: the smallest count whose share passes the game's own test. The
    // game stores count / total in a float field before SystemsConquered.IsComplete compares it against 0.3, so the
    // share is rounded to a float here too (21 / 70 passes only after that rounding).
    internal static int NeededFor30(int total)
    {
        int n = 0;
        while (n < total && Share(n, total) < Mission30) n++;
        return n;
    }

    internal static float Share(int count, int total) => (float)((double)count / total);

    internal static int CountOwned(Conquest conquest, Faction faction)
    {
        int n = 0;
        foreach (var cs in conquest.systems)
            if (cs.faction == faction) n++;
        return n;
    }

    // The faction of an accepted 30% mission ("Hostile Takeover", "The People's Sector", "Harmony"), or null.
    internal static Faction MissionFaction()
    {
        var player = GamePlayer.current;
        if (player?.missions == null) return null;
        foreach (var mission in player.missions)
            foreach (var step in mission.steps)
                foreach (var objective in step.objectives)
                    if (objective is SystemsConquered sc && sc.faction != null) return sc.faction;
        return null;
    }

    // The systems a faction's idle fleets pick their attacks from, as the planner builds them: the contested
    // neighbours of its frontline, or with no frontline any open war-zone system (ConquestFleetOrders.GatherCandidates),
    // cut to the homeland reach for the factions that raid from home. Peaceful lists the ones the under-10% rule leaves
    // (ConquestFleetOrders.ApplyPeace).
    internal static List<ConquestSystem> AttackCandidates(Faction faction, HashSet<ConquestSystem> peaceful)
    {
        var snapshot = ConquestFrontline.Build(faction);
        var list = snapshot.systems.Count > 0 ? ConquestFrontline.GetAttackCandidates(snapshot) : ConquestFrontline.GetBeachheadCandidates(faction);
        var reach = ConquestFleetOrders.GetAttackReach(faction);
        peaceful.Clear();
        foreach (var cs in ConquestFleetOrders.ApplyPeace(faction, list))
            if (reach == null || reach.Contains(cs)) peaceful.Add(cs);
        return reach == null ? list : list.FindAll(reach.Contains);
    }

    internal static string Pct(float share) => (share * 100f).ToString("0.0") + "%";

    // "3:05" or "1:02:05", from seconds.
    internal static string Clock(float seconds) => Source.Util.GameMath.FormatClock(Mathf.CeilToInt(seconds));

    // The game's own timer format, H:MM:SS ("0:42:37"), for every countdown that sits next to the Conquest Update timer.
    internal static string Timer(float seconds) => Source.Util.GameMath.FormatTime(Mathf.RoundToInt(seconds), shortFormat: true);

    // The faction the verdicts are for when nothing else is chosen: the one of an accepted 30% mission, else the major
    // faction holding the most systems.
    internal static Faction AutoTracked()
    {
        var mission = MissionFaction();
        if (mission != null) return mission;
        var conquest = Conquest;
        Faction best = null;
        int most = -1;
        foreach (var f in ConquestWarTuning.GreatGateFactions)
        {
            int n = conquest != null ? CountOwned(conquest, f) : 0;
            if (n > most) { most = n; best = f; }
        }
        return best;
    }

    // Whether a fleet of the faction is already attacking the system or gathering for an assault on it.
    internal static bool Incoming(Faction faction, ConquestSystem cs)
    {
        var economy = Universe?.PeekFactionEconomy(faction);
        if (economy == null) return false;
        foreach (var fleet in economy.fleets)
            if (fleet.assaultTargetGuid == cs.system.guid
                || fleet.state == ConquestFleetState.Traveling && fleet.orderReason == ConquestFleetOrderReason.Attacking && fleet.destinationSystemGuid == cs.system.guid)
                return true;
        return false;
    }

    internal static string Points(float points) => Mathf.RoundToInt(points).ToString("N0");
}
