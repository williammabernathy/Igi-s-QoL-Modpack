using System;
using System.Collections.Generic;
using HarmonyLib;
using LightJson;
using Source.Player;

namespace IgiQoL.Core;

public static class SaveData
{
    public static JsonObject Root = NewRoot();
    private static JsonObject pending;

    private static JsonObject NewRoot()
    {
        var root = new JsonObject();
        root["schema"] = 1;
        root["ships"] = new JsonObject();
        root["features"] = new JsonObject();
        return root;
    }

    public static void Capture(JsonValue playerJson)
    {
        var value = playerJson["igiQol"];
        pending = value.IsJsonObject ? value.AsJsonObject : null;
    }

    public static void Attach()
    {
        Root = pending ?? new JsonObject();
        pending = null;
        Root["schema"] = 1;
        if (!Root["ships"].IsJsonObject) Root["ships"] = new JsonObject();
        if (!Root["features"].IsJsonObject) Root["features"] = new JsonObject();
    }

    public static JsonObject Ship(string guid, bool create) => Child(Root["ships"].AsJsonObject, guid, create);

    public static JsonObject Feature(string id, bool create) => Child(Root["features"].AsJsonObject, id, create);

    private static JsonObject Child(JsonObject parent, string key, bool create)
    {
        var value = parent[key];
        if (value.IsJsonObject) return value.AsJsonObject;
        if (!create) return null;
        var obj = new JsonObject();
        parent[key] = obj;
        return obj;
    }

    public static JsonObject Export()
    {
        var player = GamePlayer.current;
        if (player == null) return Root;
        var live = new HashSet<string>();
        foreach (var ship in player.spaceShips) live.Add(ship.guid);
        foreach (var ship in player.activeFleet) live.Add(ship.guid);
        var ships = Root["ships"].AsJsonObject;
        var stale = new List<string>();
        foreach (var kv in ships)
            if (!live.Contains(kv.Key)) stale.Add(kv.Key);
        foreach (var key in stale) ships.Remove(key);
        return Root;
    }
}

[HarmonyPatch]
internal static class SaveDataPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GamePlayer), nameof(GamePlayer.FromJsonStaged))]
    private static void CapturePrefix(JsonValue val)
    {
        if (!CoreFeature.Instance.Active) return;
        try { SaveData.Capture(val); }
        catch (Exception e) { CoreFeature.Instance.Fail(e, nameof(SaveDataPatches)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GamePlayer), nameof(GamePlayer.ToJson))]
    private static void ExportPostfix(ref JsonValue __result)
    {
        if (!CoreFeature.Instance.Active) return;
        try { __result.AsJsonObject["igiQol"] = SaveData.Export(); }
        catch (Exception e) { CoreFeature.Instance.Fail(e, nameof(SaveDataPatches)); }
    }
}
