using System;
using System.Collections;
using Behaviour.Gameplay;
using Behaviour.Managers;
using Behaviour.Unit;
using Behaviour.Util;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.Player;
using UnityEngine;

namespace IgiQoL.Features.EchoRetreat;

// ECHO never leaves a fight on its own: the flagship fights until its hull hits zero and the game's emergency jump
// throws it to the nearest station (SpaceShip.TryEmergencyJump), and fleet ships leave for repairs at 10% hull
// (WingmanActions.OnDamageTaken). Every heavy hull hit can also kill a crew member (SpaceShip.TryNotifyCrewKilled).
public sealed class EchoRetreat : Feature
{
    public static EchoRetreat Instance;

    public EchoRetreat() { Instance = this; }

    public override string Id => "EchoRetreat";
    public override string Title => "ECHO retreat";
    public override string Description => "Lets ECHO break off and fly to the nearest friendly station for repairs when a ship's hull gets low, instead of fighting on until the emergency jump. Every heavy hit to the hull can kill a crew member, so leaving earlier also saves crew. Starting values (0) keep the game's behaviour.";
    public override string SettingsTab => "ECHO";
    public override Type[] PatchClasses => new[] { typeof(EchoRetreatPatches) };

    public ConfigEntry<int> FlagshipHullPercent;
    public ConfigEntry<int> FleetHullPercent;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        FlagshipHullPercent = cfg.Bind(Id, "FlagshipHullPercent", 0, SettingInfo.Describe("Retreat when the flagship's hull is below (%)",
            "While ECHO is on and your flagship's hull drops below this share, ECHO stops fighting and flies the fleet to the nearest friendly station, where the hangar repairs it before ECHO carries on. 0 = never (the game fights on until the emergency jump at 0 hull). It doesn't leave while an operation or boarding it runs is still going.",
            new AcceptableValueRange<int>(0, 90)));
        FleetHullPercent = cfg.Bind(Id, "FleetHullPercent", 0, SettingInfo.Describe("Retreat when a fleet ship's hull is below (%)",
            "The same for the other ships of your fleet at this location. The game sends a fleet ship away for two minutes of repairs at 10% hull, so only values above 10 change anything. 0 = never.",
            new AcceptableValueRange<int>(0, 90)));
    }

    public override void OnActivated() => EchoPriority.EchoPriority.EnsureTab();

    private float nextTick, nextTravelTry;

    // The first ship of yours here whose hull is below its threshold, as text for the status line; null when none is.
    public string LowShip()
    {
        int flagPct = FlagshipHullPercent.Value, fleetPct = FleetHullPercent.Value;
        var gm = GameplayManager.Instance;
        if (gm == null) return null;
        var flagship = gm.spaceShip;
        if (flagPct > 0 && flagship != null && Below(flagship, flagPct, out float f))
            return $"{flagship.spaceShipData.GetShipName()} hull at {f * 100f:0}%";
        if (fleetPct > 0)
            foreach (var s in gm.fleetSpaceShips)
                if (s != null && s != flagship && !s.travelling && s.gameObject.activeInHierarchy && s.spaceShipData != null && !s.spaceShipData.awayForRepairs && Below(s, fleetPct, out f))
                    return $"{s.spaceShipData.GetShipName()} hull at {f * 100f:0}%";
        return null;
    }

    private static bool Below(SpaceShip s, int percent, out float fraction)
    {
        fraction = s.maxHullHP > 0f ? s.currentHullHP / s.maxHullHP : 1f;
        return !s.isDestroyed && fraction * 100f < percent;
    }

    // Whether ECHO is somewhere it would retreat from: in space at a location that isn't a station, not already
    // travelling or emergency-jumping, and no operation it runs is still going.
    public static bool CanLeaveHere()
    {
        var poi = MapPointOfInterest.current;
        if (poi == null || poi is SpaceStation) return false;
        var travel = Singleton<TravelManager>.Instance;
        if (travel == null || travel.TravelActive() || travel.emergencyJumpActive || GamePlayer.current.waypoints.Count > 0) return false;
        var dungeons = Singleton<DungeonManager>.Instance;
        return dungeons == null || !dungeons.HasAnyActiveOperation;
    }

    internal void Tick(IdleManager idle)
    {
        if (Time.time < nextTick) return;
        nextTick = Time.time + 0.5f;
        if (FlagshipHullPercent.Value <= 0 && FleetHullPercent.Value <= 0) return;
        if (!CanLeaveHere()) return;
        string low = LowShip();
        if (low == null) return;
        Retreat(idle, low);
    }

    // IdleManager.IdleTravelToSpaceStation without its loot pickup and mission or home-station preference.
    internal void Retreat(IdleManager idle, string low)
    {
        var player = GamePlayer.current;
        if (player.autopilotSettings.noTravel)
        {
            if (Time.time < nextTravelTry) return;
            nextTravelTry = Time.time + 30f;
            IdleManager.UpdateActivity($"{low}; travel is off in the Autopilot settings, so ECHO stays");
            return;
        }
        // CanWeTravel shows a notification each time it says no, so don't ask every tick.
        if (Time.time < nextTravelTry) return;
        nextTravelTry = Time.time + 10f;
        var ship = Fleet.Flagship;
        idle.SetIdleCombatMode(ship, enable: false);
        var travel = Singleton<TravelManager>.Instance;
        if (!travel.CanWeTravel()) return;
        travel.TravelToClosestSpacestation();
        idle.idleTravelTarget = travel.targetPoi;
        string where = travel.targetPoi != null ? travel.targetPoi.name : "the nearest station";
        IdleManager.UpdateActivity($"Retreating to {where} for repairs: {low}");
        Plugin.Log.LogInfo($"[EchoRetreat] {low}, leaving {MapPointOfInterest.current?.name} for {where}");
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => EchoRetreatCheck.Run(ctx);
}

[HarmonyPatch]
internal static class EchoRetreatPatches
{
    private static EchoRetreat F => EchoRetreat.Instance;

    // Runs every frame; the feature itself only looks every half second.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(IdleManager), "Update")]
    private static void UpdatePostfix(IdleManager __instance)
    {
        if (!F.Active) return;
        var player = GamePlayer.current;
        if (player == null || !player.autoPlay) return;
        try { F.Tick(__instance); }
        catch (Exception e) { F.Fail(e, nameof(EchoRetreatPatches)); }
    }
}
