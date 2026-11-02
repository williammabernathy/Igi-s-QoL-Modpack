using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Unit;
using Source.Galaxy;
using Source.Player;
using Source.SpaceShip;
using UnityEngine;

namespace IgiQoL.Core;

public sealed class SelfCheckContext
{
    public string FeatureId;
    public int PassCount, FailCount, SkipCount;
    public readonly List<Action> Cleanups = new();

    public void Pass(string name, string detail) { PassCount++; Write(name, "PASS", detail); }
    public void Fail(string name, string detail) { FailCount++; Write(name, "FAIL", detail); }
    public void Skip(string name, string reason) { SkipCount++; Write(name, "SKIP", reason); }
    public void Cleanup(Action action) => Cleanups.Add(action);

    public IEnumerator Screenshot(string name)
    {
        if (!DevFeature.Instance.Screenshot.Value) yield break;
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        string path = Application.persistentDataPath + "/igiqol-" + name + ".png";
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForSecondsRealtime(2f); // realtime: a check may capture while an AlertPopup pauses the game
        Plugin.Log.LogInfo($"[Dev] screenshot -> {path}");
    }

    // Spawns small combat hulls of a faction hostile to you at the current location, the spawn the console's
    // SpawnEnemy uses. For checks that need a fight after the fleet has killed the earlier checks' enemies.
    public static bool SpawnEnemies(int count)
    {
        var poi = MapPointOfInterest.current;
        if (poi == null) return false;
        string hull = null;
        foreach (var kv in SpaceShip.allShips)
            if (kv.Value.shipRoleType != null && kv.Value.shipRoleType.GetRole() == SpaceShipRole.Combat && kv.Value.shipRoleType.GetTypeSize() == 2) { hull = kv.Key; break; }
        if (hull == null) return false;
        var foe = Faction.player.IsEnemy(Faction.marauders) ? Faction.marauders : Faction.fanatics;
        poi.AddTriggeredSpawnFromFixedPayload(0f, hull, count, foe, null, UnitRank.Rookie, GamePlayer.current.level);
        return true;
    }

    // A run limited by [Dev] OnlyChecks starts docked; checks that need space undock first.
    public static IEnumerator EnsureInSpace()
    {
        if (Behaviour.UI.Spacestation.SpaceStationInterior.instance == null) yield break;
        Behaviour.UI.Spacestation.SpaceStationInterior.instance.ExitSpacestation();
        float wait = Time.realtimeSinceStartup + 30f;
        while ((Behaviour.UI.HUD.HudManager.Instance == null || !Behaviour.UI.HUD.HudManager.Instance.showHud) && Time.realtimeSinceStartup < wait) yield return null;
        yield return new WaitForSecondsRealtime(2f);
    }

    // An asteroid with the given surface ore and no core, built from the first asteroid field in this system and added
    // to the current location (AddPersistable spawns it). Remove it again with poi.RemovePersistable.
    public static Source.Data.Persistable.AsteroidData SpawnAsteroid(Vector2 position, Behaviour.Mining.OreItemData ore)
    {
        var poi = MapPointOfInterest.current;
        Source.Mining.AsteroidFieldData field = null;
        foreach (var p in SystemMapData.current.allPointsOfInterest)
            if (p.hasAsteroids && p.asteroidFieldData != null) { field = p.asteroidFieldData; break; }
        if (poi == null || field == null) return null;
        var d = new Source.Data.Persistable.AsteroidData("");
        d.InitAsteroid(poi, field, position, SeededRandom.Global);
        d.surfaceItem = ore;
        if (d.surfaceAmount <= 0) d.surfaceAmount = d.originalSurfaceAmount = 50;
        d.innerCoreAmount = 0;
        poi.AddPersistable(d);
        return d;
    }

    public static void RemoveAsteroid(Source.Data.Persistable.AsteroidData d)
    {
        if (d == null) return;
        MapPointOfInterest.current?.RemovePersistable(d);
        foreach (var a in UnityEngine.Object.FindObjectsByType<Behaviour.Mining.Asteroid>(FindObjectsInactive.Include))
            if (a.asteroidData == d) UnityEngine.Object.Destroy(a.gameObject);
    }

    private void Write(string name, string result, string detail) =>
        Plugin.Log.LogInfo($"CHECK {FeatureId}/{name} {result} - {detail}");
}
