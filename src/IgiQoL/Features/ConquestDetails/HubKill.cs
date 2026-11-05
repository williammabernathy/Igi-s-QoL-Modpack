using System.Collections.Generic;
using Source.Galaxy;
using Source.Player;
using Source.Simulation.Story;
using Source.Simulation.World.System;
using UnityEngine;

namespace IgiQoL.Features.ConquestDetails;

internal enum HubKillResult
{
    // No prediction: no hub to kill, or the system goes back on sale.
    None,
    // One faction's fleet gets there first.
    Claims,
    // Two factions' fleets get there at the same time.
    TossUp,
    // No faction that would go for it has a free fleet now: it stays unowned until one has.
    NoFleet,
}

// What happens to a war-zone system if you destroy its hub now, by the game's own rules. The hub kill abandons the system
// (ConquestOwnership.Abandon). Once claiming is unlocked for you, the game first offers you the system; what follows is
// what happens with claiming locked, or after you decline. A siege in its hub stage hands the system to its lead
// attacker at once (BattleManager.EndBattlesAt, SystemClaim.Decline). Otherwise every other fight there ends, the system
// is unowned, and it goes to the first fleet that lands: one already flying in to attack it, or the one a bordering
// faction's next planning pass sends (every 600 s, all factions together, or at once for the factions of a fight that
// just ended; see FirstToGo). A fleet flies 60 s plus 150 s a jump. A faction with no frontline at all looks up to 60
// jumps away.
//
// Not modelled: what the same pass does before it gets to the attacks beyond refits, help for a system under attack and
// assaults (a retreat roll, a raid), and fleets whose orders change between now and that pass.
internal struct HubKill
{
    public HubKillResult Result;
    public Faction Claimer, Rival;
    public int Jumps, RivalJumps;
    // Seconds until the claimer's fleet arrives, counting the wait for the planning pass.
    public float Seconds;
    // The lead attacker of a hub siege takes it the moment the hub dies.
    public bool Siege;
    // You're offered the claim first (SystemClaim.GetRefusal passes for an unowned system you're in).
    public bool Offer;

    internal static HubKill Predict(ConquestSystem cs)
    {
        var r = new HubKill();
        var player = GamePlayer.current;
        var universe = WarMath.Universe;
        if (player == null || universe == null || cs.isUnowned || cs.cannotBeTaken || cs.isHubless || !ConquestScope.IsWarZone(cs.system)
            || ConquestScope.IsPlayerSector(cs.system))
            return r;
        r.Offer = player.systemClaimUnlocked && ConquestScope.IsOpenTo(cs.system, Faction.player);

        var battles = ConquestFleetOrders.GetActiveBattles();
        var replanNow = new HashSet<string>();
        foreach (var battle in battles)
        {
            if (battle.targetSystemGuid != cs.system.guid) continue;
            if (battle.stage == BattleStage.StationSiege)
            {
                var lead = BattleManager.GetLeadAttacker(battle);
                if (BattleManager.CanConquer(lead))
                {
                    r.Result = HubKillResult.Claims;
                    r.Claimer = lead.faction;
                    r.Siege = true;
                    return r;
                }
            }
            // The fight ends with the kill and its factions plan again straight away (BattleManager.RemoveBattle).
            replanNow.Add(battle.attackerFactionId);
            replanNow.Add(battle.defenderFactionId);
            replanNow.Add(battle.targetOwnerFactionId);
            foreach (var supporter in battle.supportFleets) replanNow.Add(supporter.factionId);
        }

        float pass = universe.militaryPlanningTime;
        var after = new List<BattleInstance>();
        foreach (var b in battles)
            if (b.targetSystemGuid != cs.system.guid) after.Add(b);
        float best = float.MaxValue, second = float.MaxValue;
        cs.EnsureConnections();
        foreach (var economy in universe.factionEconomies.Values)
        {
            var f = economy.faction;
            if (f == null || f == Faction.player || economy.fleets.Count == 0 || ConquestJoin.IsRaider(f) || ConquestJoin.IsDormant(f) || !ConquestScope.IsOpenTo(cs.system, f)) continue;
            float seconds = float.MaxValue;
            int jumps = -1;
            // A fleet already flying in to attack claims it the moment it lands (BattleManager.OnFleetArrived).
            foreach (var fleet in economy.fleets)
                if (fleet.state == ConquestFleetState.Traveling && fleet.orderReason == ConquestFleetOrderReason.Attacking && fleet.destinationSystemGuid == cs.system.guid)
                {
                    float left = fleet.travelDuration * (1f - fleet.routeProgress);
                    if (left < seconds) (seconds, jumps) = (left, -1);
                }
            var sent = FirstToGo(economy, cs, battles, after, out int hops);
            if (sent != null)
            {
                float t = (replanNow.Contains(f.identifier) ? 0f : pass) + (hops == 0 ? 0f : ConquestFleetPlanner.TravelDuration(hops));
                if (t < seconds) (seconds, jumps) = (t, hops);
            }
            if (seconds == float.MaxValue) continue;
            if (seconds < best)
            {
                (second, r.Rival, r.RivalJumps) = (best, r.Claimer, r.Jumps);
                (best, r.Claimer, r.Jumps) = (seconds, f, jumps);
            }
            else if (seconds < second)
            {
                (second, r.Rival, r.RivalJumps) = (seconds, f, jumps);
            }
        }
        if (r.Claimer == null)
        {
            r.Result = HubKillResult.NoFleet;
            return r;
        }
        r.Seconds = best;
        r.Result = r.Rival != null && second - best < 1f ? HubKillResult.TossUp : HubKillResult.Claims;
        return r;
    }

    // The faction owns a war-zone system next to it, so the unowned system is one of its attack candidates.
    private static bool Borders(ConquestSystem cs, Faction f)
    {
        if (cs.connectedSystems == null) return false;
        foreach (var n in cs.connectedSystems)
            if (n.faction == f && ConquestScope.IsWarZone(n.system)) return true;
        return false;
    }

    // The fleet the faction's next planning pass sends to the system once it's unowned, picked the way
    // ConquestFleetOrders.AssignOrders picks: its free fleets in list order, less those it sends to a refit or to a
    // system of its own under attack and those gathering for an assault; each takes its best target in turn
    // (AttackTarget.IsBetterThan puts an unowned system first, then the nearest), and the first whose best target is
    // this system goes. Jumps 0 is a fleet already in the system.
    private static ConquestFleet FirstToGo(FactionEconomy economy, ConquestSystem cs, List<BattleInstance> battles, List<BattleInstance> after, out int jumps)
    {
        jumps = -1;
        var f = economy.faction;
        var view = View(f);
        bool borders = Borders(cs, f), front = view.Snapshot.systems.Count > 0 || borders;
        // A faction with a frontline picks from its frontline's neighbours; one with none from every open war-zone
        // system (ConquestFrontline.GetBeachheadCandidates).
        if (front && !borders) return null;
        if (!front && (!ConquestWarTuning.FleetFieldingFactions.Contains(f) || WarMath.Conquest?.IsSittingOut(f) == true)) return null;
        int maxHops = front ? 4 : 60;
        var candidates = new List<ConquestSystem>(view.Candidates);
        if (!candidates.Contains(cs)) candidates.Add(cs);
        var claimed = ConquestFleetOrders.GetClaimedTargets(economy, after);
        var territory = view.Territory;

        var available = new List<ConquestFleet>();
        foreach (var fleet in economy.fleets)
        {
            if (fleet.isEmpireFleet || fleet.interceptHeld || ConquestFleetOrders.NeedsToRetreat(fleet)) continue;
            // An attacker here is set idle when its fight ends with the kill.
            bool attacksHere = false;
            foreach (var b in battles)
                if (b.targetSystemGuid == cs.system.guid && b.HasAttacker(fleet.guid)) attacksHere = true;
            if (!attacksHere && (fleet.state != ConquestFleetState.Idle || ConquestFleetOrders.IsInBattle(fleet, after))) continue;
            if (fleet.currentSystem != cs.system && ConquestFleetOrders.IsDefendingOnSite(fleet)) continue;
            available.Add(fleet);
        }
        int slots = 3;
        foreach (var fleet in economy.fleets)
            if (ConquestFleetOrders.IsRefitBusy(fleet)) slots--;
        for (int i = 0; i < available.Count && slots > 0;)
        {
            if (ConquestFleetOrders.IsDueRefit(available[i]) && ConquestFleetOrders.ChooseRefitYard(available[i]) != null)
            {
                available.RemoveAt(i);
                slots--;
            }
            else i++;
        }
        foreach (var b in after)
        {
            if (b.stage == BattleStage.Raid) continue;
            var target = BattleManager.GetTargetSystem(b);
            if (target == null || target.faction != f || ConquestFleetOrders.HasLiveResponder(economy, b, target)) continue;
            var responder = ConquestFleetOrders.ChooseResponder(available, target.system, b.timeRemaining);
            if (responder != null) available.Remove(responder);
        }
        available.RemoveAll(fleet => fleet.isInAssault && ConquestFleetOrders.IsGatheringForAssault(fleet) && candidates.Exists(c => c.system.guid == fleet.assaultTargetGuid));

        foreach (var fleet in available)
        {
            if (fleet.currentSystem == null) continue;
            var distances = Distances(fleet.currentSystem, maxHops);
            ConquestFleetOrders.AttackTarget? pick = null;
            foreach (var c in candidates)
            {
                if (claimed.Contains(c.system.guid) || !distances.TryGetValue(c.system, out int hops) || hops < 1 && c.system != fleet.currentSystem
                    || fleet.IsReattackBlocked(c.system.guid))
                    continue;
                bool unowned = c == cs || c.isUnowned;
                float strength = c == cs ? 0f : NextBattle(c);
                if (fleet.totalPoints < strength * WarMath.AttackMargin) continue;
                int size = unowned ? int.MaxValue : territory.TryGetValue(c.faction, out int n) ? n : 0;
                var t = new ConquestFleetOrders.AttackTarget(c, size, strength, hops);
                if (!pick.HasValue || t.IsBetterThan(pick.Value)) pick = t;
            }
            if (!pick.HasValue) continue;
            if (pick.Value.system == cs)
            {
                jumps = pick.Value.hops;
                return fleet;
            }
            claimed.Add(pick.Value.system.system.guid);
        }
        return null;
    }

    // What a prediction reads for every system and faction alike, kept for the frame: the War overview predicts every
    // listed system in one refresh, and each prediction would otherwise rebuild every faction's frontline and walk the
    // jump graph from every free fleet. A check that changes the war between predictions calls Forget.
    private sealed class FactionView
    {
        public FrontlineSnapshot Snapshot;
        public List<ConquestSystem> Candidates;
        public Dictionary<Faction, int> Territory;
    }

    private static int frame = -1;
    private static readonly Dictionary<Faction, FactionView> Views = new();
    private static readonly Dictionary<(SystemMapData, int), Dictionary<SystemMapData, int>> Hops = new();
    private static readonly Dictionary<ConquestSystem, float> Strengths = new();

    internal static void Forget()
    {
        Views.Clear();
        Hops.Clear();
        Strengths.Clear();
    }

    private static void Fresh()
    {
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;
        Forget();
    }

    private static FactionView View(Faction f)
    {
        Fresh();
        if (Views.TryGetValue(f, out var v)) return v;
        var snapshot = ConquestFrontline.Build(f);
        var candidates = ConquestFleetOrders.GatherCandidates(f, snapshot, snapshot.systems.Count > 0, null);
        v = new FactionView { Snapshot = snapshot, Candidates = candidates, Territory = ConquestFleetOrders.CountOwnerTerritory(candidates) };
        Views[f] = v;
        return v;
    }

    private static Dictionary<SystemMapData, int> Distances(SystemMapData from, int maxHops)
    {
        Fresh();
        if (!Hops.TryGetValue((from, maxHops), out var d)) Hops[(from, maxHops)] = d = GalaxyMapData.current.GetJumpDistances(from, maxHops, npcTraffic: true);
        return d;
    }

    private static float NextBattle(ConquestSystem c)
    {
        Fresh();
        if (!Strengths.TryGetValue(c, out float s)) Strengths[c] = s = ConquestCombat.GetNextBattleStrength(c);
        return s;
    }
}
