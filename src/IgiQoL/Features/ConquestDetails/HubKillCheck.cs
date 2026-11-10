using System.Collections.Generic;
using IgiQoL.Core;
using Source.Galaxy;
using Source.Simulation.Story;
using Source.Simulation.World.System;
using UnityEngine;

namespace IgiQoL.Features.ConquestDetails;

// A hub kill as the game runs it: the system is abandoned the way a destroyed hub abandons it, every faction gets the
// planning pass the 600 s clock would give it, and the faction the game hands the system to, or sends first, has to be
// the one the prediction named. The war stays changed for the rest of the run (the harness never saves), so these run
// last.
internal static partial class ConquestDetailsCheck
{
    private static void CheckHubKills(SelfCheckContext ctx, Conquest conquest)
    {
        var player = Source.Player.GamePlayer.current;
        bool unlocked = player.systemClaimUnlocked;
        // With claiming unlocked the game would offer you the system in a dialog first; declining it leads to the same
        // outcome, so the kills run as they do with claiming locked.
        try
        {
            player.systemClaimUnlocked = false;
            HubKill.Forget();
            CheckHubKillSiege(ctx, conquest);
            HubKill.Forget();
            CheckHubKillRace(ctx, conquest);
        }
        finally { player.systemClaimUnlocked = unlocked; }
    }

    // A siege at the hub: the kill hands the system to the siege's lead attacker on the spot.
    private static void CheckHubKillSiege(SelfCheckContext ctx, Conquest conquest)
    {
        foreach (var cs in conquest.systems)
        {
            var h = HubKill.Predict(cs);
            if (!h.Siege) continue;
            string owner = cs.faction.identifier;
            ConquestOwnership.Abandon(cs);
            string got = cs.isUnowned ? "nobody" : cs.faction.identifier;
            string detail = $"{cs.system.name} ({owner}): predicted {h.Claimer.identifier} by its hub siege, game handed it to {got}";
            if (cs.faction == h.Claimer) ctx.Pass("hub-kill-siege", detail);
            else ctx.Fail("hub-kill-siege", detail);
            return;
        }
        ctx.Skip("hub-kill-siege", "no siege at a hub stage in the war zone");
    }

    // No siege at the hub: the first fleet to arrive claims it.
    private static void CheckHubKillRace(SelfCheckContext ctx, Conquest conquest)
    {
        var tracked = WarMath.AutoTracked();
        ConquestSystem pick = null;
        HubKill predicted = default;
        var counts = new Dictionary<string, int>();
        // The cost of a prediction, which the War overview pays for each listed system once a second.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int predictions = 0;
        foreach (var cs in conquest.systems)
        {
            // A fight there makes its factions plan at once, which one pass for everyone doesn't stage.
            if (BattleAt(cs)) continue;
            var h = HubKill.Predict(cs);
            predictions++;
            counts[h.Result.ToString()] = counts.TryGetValue(h.Result.ToString(), out int n) ? n + 1 : 1;
            // A fleet already flying in only has to land; the planning pass is what the check is after.
            if (h.Result != HubKillResult.Claims || h.Jumps < 0) continue;
            if (pick == null || h.Claimer == tracked && predicted.Claimer != tracked) (pick, predicted) = (cs, h);
        }
        string cost = $"{predictions} predictions in {clock.Elapsed.TotalMilliseconds:0} ms";
        string staged = null;
        if (pick == null && !StageFreeFleet(conquest, out pick, out predicted, out staged))
        {
            var seen = new List<string>();
            foreach (var pair in counts) seen.Add(pair.Key + " " + pair.Value);
            ctx.Skip("hub-kill-race", $"no system where a hub kill has one first claimer ({string.Join(", ", seen)}), and no NPC fleet to stage next to one: {staged}; {cost}");
            return;
        }

        var universe = WarMath.Universe;
        string owner = pick.faction.identifier;
        // The pass runs now, so what's left is the flight (a fleet already flying in keeps its own clock).
        float travel = predicted.Jumps < 0 ? predicted.Seconds : predicted.Jumps == 0 ? 0f : ConquestFleetPlanner.TravelDuration(predicted.Jumps);
        ConquestOwnership.Abandon(pick);
        foreach (var economy in new List<FactionEconomy>(universe.factionEconomies.Values)) ConquestFleetPlanner.PlanBetweenTicks(economy);

        // First there: a fleet that claimed it on the spot, else the shortest flight still to go.
        Faction first = pick.isUnowned ? null : pick.faction;
        float firstTime = first != null ? 0f : float.MaxValue;
        var sent = new List<string>();
        foreach (var economy in universe.factionEconomies.Values)
            foreach (var fleet in economy.fleets)
            {
                if (fleet.state != ConquestFleetState.Traveling || fleet.destinationSystemGuid != pick.system.guid) continue;
                float left = fleet.travelDuration * (1f - fleet.routeProgress);
                sent.Add(fleet.faction.identifier + " " + left.ToString("0") + " s");
                if (left < firstTime) (first, firstTime) = (fleet.faction, left);
            }
        string detail = $"{pick.system.name} ({owner}{(staged != null ? ", staged: " + staged : "")}): predicted {predicted.Claimer.identifier}, "
            + $"{predicted.Jumps} jumps, {travel:0} s flight" + (predicted.Rival != null ? $", next {predicted.Rival.identifier} at {predicted.RivalJumps} jumps" : "")
            + $" | game: {(first != null ? first.identifier : "nobody")} first" + (firstTime < float.MaxValue ? $" in {firstTime:0} s" : "")
            + (pick.isUnowned ? "" : ", claimed at once") + $"; sent [{string.Join(", ", sent)}]; {cost}";
        if (first == predicted.Claimer && Mathf.Abs(firstTime - travel) < 1f) ctx.Pass("hub-kill-race", detail);
        else ctx.Fail("hub-kill-race", detail);
    }

    // A save whose fleets are all busy has no race to watch. An NPC fleet in no fight is made free, as the game leaves a
    // fleet after a battle (idle, loitering), in a system of its faction next to another faction's system; the first
    // such system where the prediction then names that faction is the target. A fleet that finds none is put back.
    private static bool StageFreeFleet(Conquest conquest, out ConquestSystem target, out HubKill predicted, out string staged)
    {
        target = null;
        predicted = default;
        staged = "no NPC fleet in no fight";
        int tried = 0;
        var battles = ConquestFleetOrders.GetActiveBattles();
        foreach (var economy in WarMath.Universe.factionEconomies.Values)
        {
            var f = economy.faction;
            if (f == null || f == Faction.player || ConquestJoin.IsRaider(f) || ConquestJoin.IsDormant(f)) continue;
            foreach (var fleet in economy.fleets)
            {
                if (fleet.isEmpireFleet || fleet.interceptHeld || ConquestFleetOrders.IsInBattle(fleet, battles) || ConquestFleetOrders.NeedsToRetreat(fleet)
                    || fleet.isWarFleet && fleet.lifetimeHoursLeft <= 0f) continue;
                tried++;
                var saved = new FleetPlace(fleet);
                foreach (var home in conquest.systems)
                {
                    if (home.faction != f || !ConquestScope.IsWarZone(home.system)) continue;
                    home.EnsureConnections();
                    Place(fleet, home);
                    HubKill.Forget();
                    foreach (var cs in home.connectedSystems)
                    {
                        if (cs.faction == null || cs.faction == f || cs.faction == Faction.player || BattleAt(cs)) continue;
                        var h = HubKill.Predict(cs);
                        if (h.Result != HubKillResult.Claims || h.Claimer != f || h.Jumps < 0) continue;
                        (target, predicted) = (cs, h);
                        staged = $"{f.identifier} fleet {fleet.displayName} placed at {home.system.name}";
                        return true;
                    }
                }
                saved.Restore(fleet);
                HubKill.Forget();
                staged = tried + " fleets tried, none next to a system its faction would claim first";
            }
        }
        return false;
    }

    private static void Place(ConquestFleet fleet, ConquestSystem at)
    {
        fleet.LeaveAssault();
        fleet.ClearRaidTarget();
        fleet.AbortTravel();
        fleet.currentSystemGuid = at.system.guid;
        fleet.state = ConquestFleetState.Idle;
        fleet.orderReason = ConquestFleetOrderReason.Loitering;
    }

    // Where a fleet is and what it's doing, to put back a fleet the staging moved for nothing.
    private readonly struct FleetPlace
    {
        private readonly string current, destination, assaultTarget, assaultStaging, raidTarget, raidOwner;
        private readonly float progress, duration, restSince;
        private readonly Vector2? start, rest;
        private readonly ConquestFleetState state;
        private readonly ConquestFleetOrderReason reason;

        public FleetPlace(ConquestFleet f)
        {
            (current, destination, progress, duration, start, rest, restSince) = (f.currentSystemGuid, f.destinationSystemGuid, f.routeProgress, f.travelDuration, f.travelStartPosition, f.restPosition, f.restSince);
            (state, reason, assaultTarget, assaultStaging, raidTarget, raidOwner) = (f.state, f.orderReason, f.assaultTargetGuid, f.assaultStagingGuid, f.raidTargetGuid, f.raidTargetOwnerId);
        }

        public void Restore(ConquestFleet f)
        {
            (f.currentSystemGuid, f.destinationSystemGuid, f.routeProgress, f.travelDuration, f.travelStartPosition, f.restPosition, f.restSince) = (current, destination, progress, duration, start, rest, restSince);
            (f.state, f.orderReason, f.assaultTargetGuid, f.assaultStagingGuid, f.raidTargetGuid, f.raidTargetOwnerId) = (state, reason, assaultTarget, assaultStaging, raidTarget, raidOwner);
        }
    }
}
