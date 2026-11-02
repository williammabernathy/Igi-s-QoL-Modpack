using System;
using System.Collections.Generic;
using System.Text;
using Behaviour.Gameplay;
using Behaviour.Managers;
using Behaviour.UI.Missions;
using Behaviour.UI.Spacestation;
using Behaviour.Util;
using HarmonyLib;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.MissionSystem;
using Source.Player;
using IgiQoL.Game;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.EchoMissions;

// ECHO takes one mission, flies out, flies back to turn it in, and only then takes the next, so most of a mission
// session is the trip home and the docking in between. With MissionsAtOnce above 1 it takes several missions from the
// board in one visit, flies from one mission location to the nearest next one, and turns them all in on one trip home.
public static class MissionBatch
{
    public static int Limit
    {
        get
        {
            var f = EchoMissions.Instance;
            return f != null && f.Active ? Mathf.Clamp(f.MissionsAtOnce.Value, 1, 5) : 1;
        }
    }

    // Missions ECHO would work on (IdleManager.GetIdleMission's test), complete or not.
    public static int Held()
    {
        int n = 0;
        foreach (var m in GamePlayer.current.missions)
            if (m.CanBeIdled() && m.CanBeIdleCompleted() && !IdleManager.MissionIsUnreachable(m)) n++;
        return n;
    }

    // The nearest mission in this system that still has work at a location other than here and isn't a station visit,
    // passing the same tests ECHO applies before it flies to a mission. Null when there is none.
    public static Mission NextOpen()
    {
        var here = MapPointOfInterest.current;
        var system = GamePlayer.current.currentSystem;
        Vector2 from = here != null ? here.GetWorldPosition() : (Vector2)Fleet.Flagship.transform.position;
        bool courier = GamePlayer.current.autopilotSettings.allowCourierMissions;
        Mission best = null;
        float bestDistance = float.MaxValue;
        foreach (var m in GamePlayer.current.missions)
        {
            if (m.isComplete || m.failed || !m.CanBeIdled() || !m.CanBeIdleCompleted()) continue;
            if (m.gameplayType == GameplayType.Cargo && !courier) continue;
            var poi = m.GetActivePoi(forEcho: true);
            if (poi == null || poi == here || poi is SpaceStation || poi.hidden || poi.system != system) continue;
            if (IdleManager.MissionIsUnreachable(m) || IdleManager.MissionDungeonExceedsRisk(m) || !IdleManager.ShipHasLoadoutForMission(m)) continue;
            float d = Vector2.Distance(from, poi.GetWorldPosition());
            if (d < bestDistance)
            {
                best = m;
                bestDistance = d;
            }
        }
        return best;
    }

    // Whether ECHO has a reason of its own to go to a station first: a full hold, no ammo it could restock, crew to
    // hire, or a flagship under half hull or armour (the station repairs it).
    public static bool NeedsStation(IdleManager idle)
    {
        if (idle.inventoryFull) return true;
        var ship = Fleet.Flagship;
        if (ship == null) return true;
        var retreat = EchoRetreat.EchoRetreat.Instance;
        if (retreat != null && retreat.Active && retreat.LowShip() != null) return true;
        if (!ship.AmmoInCargoForTurrets() && IdleManager.CanRestockAmmoSomewhere()) return true;
        if (!GamePlayer.current.IsPrologueActive() && idle.IsCrewBelowThreshold()) return true;
        if (ship.currentHullHP < ship.maxHullHP * 0.5f) return true;
        return ship.maxArmorHP > 0f && ship.currentArmorHP < ship.maxArmorHP * 0.5f;
    }

    // Takes missions from the board until ECHO holds Limit of them; returns how many it took and their names.
    public static int TakeMore(SpaceStationInterior interior, StringBuilder names)
    {
        int taken = 0;
        int held = Held();
        int limit = Limit;
        while (held < limit && !GamePlayer.current.IsMissionsLimitExceeded())
        {
            var m = interior.GetMissionAvailableForAutopilot();
            if (m == null || IdleManager.MissionDungeonExceedsRisk(m) || !IdleManager.ShipHasLoadoutForMission(m)) break;
            int before = GamePlayer.current.missions.Count;
            GamePlayer.current.AcceptMission(m);
            if (GamePlayer.current.missions.Count == before) break;
            m.idle = true;
            if (names.Length > 0) names.Append(", ");
            names.Append(m.name);
            held++;
            taken++;
        }
        return taken;
    }

    // Docked at a station with a mission board: with MissionsAtOnce 3, ECHO's mission decision fills up to three
    // missions in one call and points the HUD tracker at the nearest open one. The missions it took are abandoned again.
    public static void Check(IgiQoL.Core.SelfCheckContext ctx)
    {
        var interior = SpaceStationInterior.instance;
        var idle = Singleton<IdleManager>.Instance;
        if (interior == null || !interior.HasMissionBoard(missionCheck: true) || idle == null || !GamePlayer.current.autopilotSettings.runMissions)
        {
            ctx.Skip("missions-at-once", "not docked at a station with a mission board, or ECHO's missions are off");
            return;
        }
        var f = EchoMissions.Instance;
        int saved = f.MissionsAtOnce.Value;
        var focus = Singleton<FocusedMissionHandler>.Instance;
        var focused = focus.focusedMission;
        var before = new HashSet<Mission>(GamePlayer.current.missions);
        try
        {
            int held0 = Held();
            f.MissionsAtOnce.Value = Mathf.Max(3, held0 + 2);
            bool result = false;
            // The prefix alone: the game's own decision would undock and fly off. Handled = the prefix took the decision;
            // true means it handed over to the game, which this setup (limit above 1, open board) must not do.
            bool handled = !MissionBatchPatches.DecideMissionActivityPrefix(ref result);
            var taken = new List<string>();
            foreach (var m in GamePlayer.current.missions)
                if (!before.Contains(m)) taken.Add($"{m.name} @ {m.GetActivePoi(forEcho: true)?.name} idle={m.idle}");
            var next = NextOpen();
            string detail = $"held {held0} -> {Held()} (limit {f.MissionsAtOnce.Value}), handled {handled}, returned {result}, took [{string.Join("; ", taken)}], tracker on {focus.focusedMission?.name}, nearest open {next?.name}";
            if (handled && result && Held() == f.MissionsAtOnce.Value && taken.Count == f.MissionsAtOnce.Value - held0 && next != null && focus.focusedMission == next)
                ctx.Pass("missions-at-once", detail);
            else ctx.Fail("missions-at-once", detail);

            // Limit 1 is the game's own decision: the prefix hands over and takes nothing. (Calling the game's decision
            // here would undock and fly to the mission, which the checks after this one don't expect.)
            f.MissionsAtOnce.Value = 1;
            int count = GamePlayer.current.missions.Count;
            bool unused = false;
            bool handsOver = MissionBatchPatches.DecideMissionActivityPrefix(ref unused);
            string vanilla = $"missions {count} -> {GamePlayer.current.missions.Count}, game decides {handsOver}";
            if (handsOver && GamePlayer.current.missions.Count == count) ctx.Pass("missions-at-once-off", vanilla);
            else ctx.Fail("missions-at-once-off", vanilla);
        }
        finally
        {
            f.MissionsAtOnce.Value = saved;
            foreach (var m in new List<Mission>(GamePlayer.current.missions))
                if (!before.Contains(m)) GamePlayer.current.RemoveMission(m, completed: false);
            focus.SetMission(focused);
        }
    }
}

[HarmonyPatch]
internal static class MissionBatchPatches
{
    private static EchoMissions F => EchoMissions.Instance;

    // At a station with a mission board: top up to MissionsAtOnce missions in one go, then point ECHO at the nearest.
    // With every mission kind switched off and nothing held, ECHO skips missions and goes on to its activities;
    // failed missions are still abandoned.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.DecideMissionActivity))]
    internal static bool DecideMissionActivityPrefix(ref bool __result)
    {
        if (!F.Active) return true;
        try
        {
            if (!GamePlayer.current.autopilotSettings.runMissions) return true;
            if (MissionKinds.NoneAllowed() && MissionBatch.Held() == 0)
            {
                __result = IdleManager.TryAbandonFailedMission();
                return false;
            }
            if (MissionBatch.Limit <= 1) return true;
            var interior = SpaceStationInterior.instance;
            if (interior == null || !interior.HasMissionBoard(missionCheck: true)) return true;
            foreach (var m in GamePlayer.current.missions)
                if (m.storyId == null && m.failed) return true;   // the game abandons it first
            var names = new StringBuilder();
            int taken = MissionBatch.TakeMore(interior, names);
            if (taken == 0) return true;
            var next = MissionBatch.NextOpen();
            if (next != null) Singleton<FocusedMissionHandler>.Instance.SetMission(next);
            IdleManager.UpdateActivity(taken == 1 ? $"Picking up mission {names}" : $"Picking up {taken} missions: {names}");
            Plugin.Log.LogInfo($"[EchoMissions] took {taken} missions ({MissionBatch.Held()} held): {names}");
            __result = true;
            return false;
        }
        catch (Exception e) { F.Fail(e, nameof(MissionBatchPatches)); return true; }
    }

    // Docked where finished missions are turned in, those come first, so the trip's rewards are all claimed before
    // ECHO picks a mission that is still open (the game prefers the mission on the HUD tracker).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.GetIdleMission))]
    private static void GetIdleMissionPostfix(ref Mission __result)
    {
        if (!F.Active) return;
        try
        {
            if (MissionBatch.Limit <= 1 || SpaceStationInterior.instance == null || !GamePlayer.current.autopilotSettings.runMissions) return;
            var here = MapPointOfInterest.current;
            if (here == null || (__result != null && __result.isComplete && __result.GetActivePoi(forEcho: true) == here)) return;
            foreach (var m in GamePlayer.current.missions)
                if (m.isComplete && m.CanBeIdled() && m.CanBeIdleCompleted() && m.GetActivePoi(forEcho: true) == here)
                {
                    __result = m;
                    return;
                }
        }
        catch (Exception e) { F.Fail(e, nameof(MissionBatchPatches)); }
    }

    private static bool claiming;

    // The game turns in one mission per decision (about 10 s each); with several finished missions aboard, the rest
    // that are turned in here go in the same decision.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.TryClaimCompletedMission))]
    private static void TryClaimCompletedMissionPostfix(IdleManager __instance, bool __result)
    {
        if (!F.Active || !__result || claiming) return;
        try
        {
            if (MissionBatch.Limit <= 1) return;
            claiming = true;
            int claimed = 1;
            foreach (var m in new List<Mission>(GamePlayer.current.missions))
                if (m.isComplete && m.CanBeIdled() && m.CanBeIdleCompleted() && __instance.TryClaimCompletedMission(m)) claimed++;
            if (claimed > 1) IdleManager.UpdateActivity($"Turning in {claimed} missions");
        }
        catch (Exception e) { F.Fail(e, nameof(MissionBatchPatches)); }
        finally { claiming = false; }
    }

    // ECHO heads for a station when a mission is done (to turn it in) or a location has nothing left to do. With
    // another mission still open in this system it flies there instead, unless it needs the station for itself.
    // Trips for ammo or crew pass a message or overrideClosests and stay as they are.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.IdleTravelToSpaceStation))]
    private static bool IdleTravelToSpaceStationPrefix(IdleManager __instance, string messageOverride, bool overrideClosests)
    {
        if (!F.Active) return true;
        try
        {
            if (MissionBatch.Limit <= 1 || messageOverride != null || overrideClosests || !GamePlayer.current.autopilotSettings.runMissions) return true;
            if (GamePlayer.current.autopilotSettings.noTravel || MissionBatch.NeedsStation(__instance)) return true;
            var next = MissionBatch.NextOpen();
            if (next == null) return true;
            __instance.SetIdleCombatMode(Fleet.Flagship, enable: false);
            if (__instance.TryLootBeforeLeaving()) return false;
            if (!Singleton<TravelManager>.Instance.CanWeTravel())
            {
                IdleManager.UpdateActivity("@IdleTravelNotPossible");
                return false;
            }
            __instance.TryTravelToActiveMission(next);
            Plugin.Log.LogInfo($"[EchoMissions] from {MapPointOfInterest.current?.name} on to {next.GetActivePoi(forEcho: true)?.name} for {next.name} instead of a station");
            return false;
        }
        catch (Exception e) { F.Fail(e, nameof(MissionBatchPatches)); return true; }
    }
}
