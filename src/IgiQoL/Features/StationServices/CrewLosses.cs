using System;
using System.Collections.Generic;
using HarmonyLib;
using IgiQoL.Core;
using LightJson;
using Source.Personnel;
using Source.Player;
using Source.SpaceShip;

namespace IgiQoL.Features.StationServices;

// Crew your ships lost to hull hits (SpaceShipData.TryKillCrewFromHullDamage) and nobody has replaced yet, per ship and
// crew type, kept in the save. Any crew of that type added to the ship later (by you, ECHO, a loadout or the Replace crew
// service) counts as a replacement, so a death is never replaced twice.
public static class CrewLosses
{
    private const string Key = "crewLost";

    private static JsonObject Ships(bool create)
    {
        var record = SaveData.Feature(StationServices.Instance.Id, create);
        if (record == null) return null;
        if (record[Key].IsJsonObject) return record[Key].AsJsonObject;
        if (!create) return null;
        var obj = new JsonObject();
        record[Key] = obj;
        return obj;
    }

    public static int Pending(SpaceShipData ship, string type)
    {
        var ships = Ships(false);
        if (ships == null || ship == null || !ships[ship.guid].IsJsonObject) return 0;
        var v = ships[ship.guid].AsJsonObject[type];
        return v.IsNumber ? (int)v.AsNumber : 0;
    }

    // Crew types with losses still to replace on this ship, in a stable order.
    public static List<KeyValuePair<string, int>> PendingOf(SpaceShipData ship)
    {
        var list = new List<KeyValuePair<string, int>>();
        var ships = Ships(false);
        if (ships == null || ship == null || !ships[ship.guid].IsJsonObject) return list;
        foreach (var kv in ships[ship.guid].AsJsonObject)
        {
            int n = kv.Value.IsNumber ? (int)kv.Value.AsNumber : 0;
            if (n > 0) list.Add(new KeyValuePair<string, int>(kv.Key, n));
        }
        list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        return list;
    }

    public static void Record(SpaceShipData ship, string type, int amount)
    {
        if (ship == null || type == null || amount == 0) return;
        var ships = Ships(amount > 0);
        if (ships == null) return;
        if (!ships[ship.guid].IsJsonObject)
        {
            if (amount < 0) return;
            ships[ship.guid] = new JsonObject();
        }
        var types = ships[ship.guid].AsJsonObject;
        var v = types[type];
        int n = (v.IsNumber ? (int)v.AsNumber : 0) + amount;
        if (n > 0) types[type] = n;
        else types.Remove(type);
        if (types.Count == 0) ships.Remove(ship.guid);
    }

    public static void Clear(SpaceShipData ship)
    {
        var ships = Ships(false);
        if (ships != null && ship != null) ships.Remove(ship.guid);
    }

    // Your ship that owns this crew list (flagship, fleet or parked), or null for the reserve, stations and enemies.
    internal static SpaceShipData Owner(CrewData crew)
    {
        var player = GamePlayer.current;
        if (player == null) return null;
        foreach (var s in player.spaceShips)
            if (s != null && s.crewData == crew) return s;
        return null;
    }

    internal static bool Mine(SpaceShipData ship)
    {
        var player = GamePlayer.current;
        return player != null && ship != null && player.spaceShips.Contains(ship);
    }
}

[HarmonyPatch]
internal static class CrewLossPatches
{
    private static StationServices F => StationServices.Instance;

    // Returns the crew type that died, or null.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpaceShipData), nameof(SpaceShipData.TryKillCrewFromHullDamage))]
    private static void KillPostfix(SpaceShipData __instance, string __result)
    {
        if (__result == null || !F.Active) return;
        try
        {
            if (CrewLosses.Mine(__instance)) CrewLosses.Record(__instance, __result, 1);
        }
        catch (Exception e) { F.Fail(e, nameof(CrewLossPatches)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CrewData), nameof(CrewData.AddCrew))]
    private static void AddPostfix(CrewData __instance, string type, int amount)
    {
        if (amount <= 0 || !F.Active) return;
        try
        {
            var ship = CrewLosses.Owner(__instance);
            if (ship == null) return;
            int pending = CrewLosses.Pending(ship, type);
            if (pending > 0) CrewLosses.Record(ship, type, -Math.Min(pending, amount));
        }
        catch (Exception e) { F.Fail(e, nameof(CrewLossPatches)); }
    }
}
