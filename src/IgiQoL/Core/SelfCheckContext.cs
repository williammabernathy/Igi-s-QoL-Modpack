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

    // Docks for a check that needs a station. Inside or outside a station that suits, the flagship stays and enters it.
    // Elsewhere it flies to a friendly station in this system, one that suits if there is one (the home station first,
    // else the first in the system's own order, as SystemMapData.GetNearestSpaceStationWithFacility picks), and enters
    // it. A trip abandons the player's running operations like any trip does (TravelManager.SetRouteToPOI). Undocks
    // again in a cleanup when it docked. The caller reads SpaceStationInterior.instance afterwards and skips with
    // DockNote, which also says when no station in the system suits (the caller's own facility test then skips).
    public IEnumerator EnsureDocked(Func<Source.Galaxy.POI.SpaceStation, bool> suits = null)
    {
        DockNote = null;
        var here = MapPointOfInterest.current as Source.Galaxy.POI.SpaceStation;
        var target = PickStation(suits);
        if (target == null && suits != null)
        {
            // Docked anyway, for the checks that need only a station; the caller's own facility test skips with this.
            DockNote = $"no friendly station in {SystemMapData.current?.name ?? "this system"} has what the check needs";
            target = PickStation(null);
        }
        bool stay = here != null && here.PlayerIsFriendly() && (target == null || target == here || suits == null || suits(here));
        if (Behaviour.UI.Spacestation.SpaceStationInterior.instance != null)
        {
            if (stay) yield break;
            yield return EnsureInSpace();
        }
        if (!stay)
        {
            var travel = Behaviour.Util.Singleton<Behaviour.Managers.TravelManager>.Instance;
            if (target == null || travel == null || !travel.CanWeTravel(target) || !travel.SetRouteToPOI(target))
            {
                DockNote = target == null ? "no friendly station in this system" : "can't travel to " + target.name;
                yield break;
            }
            float arrive = Time.realtimeSinceStartup + 180f;
            while (Time.realtimeSinceStartup < arrive && (MapPointOfInterest.current != target || travel.TravelActive() || !(Behaviour.Managers.BasePoiManager.current is SpacestationExteriorManager)))
                yield return null;
            Plugin.Log.LogInfo($"[Dev] {FeatureId}: flew to {target.name} to dock, arrived={MapPointOfInterest.current == target}");
            yield return new WaitForSecondsRealtime(2f);
        }
        if (!(Behaviour.Managers.BasePoiManager.current is SpacestationExteriorManager exterior))
        {
            DockNote = "not outside a station after the trip: " + MapPointOfInterest.current?.name;
            yield break;
        }
        exterior.EnterSpacestation();
        float wait = Time.realtimeSinceStartup + 30f;
        while (Behaviour.UI.Spacestation.SpaceStationInterior.instance == null && Time.realtimeSinceStartup < wait) yield return null;
        yield return new WaitForSecondsRealtime(3f);
        if (Behaviour.UI.Spacestation.SpaceStationInterior.instance == null) DockNote = "could not dock at " + MapPointOfInterest.current?.name;
        else Cleanup(() => { if (Behaviour.UI.Spacestation.SpaceStationInterior.instance != null) Behaviour.UI.Spacestation.SpaceStationInterior.instance.ExitSpacestation(); });
    }

    private static Source.Galaxy.POI.SpaceStation PickStation(Func<Source.Galaxy.POI.SpaceStation, bool> suits)
    {
        bool Ok(Source.Galaxy.POI.SpaceStation s) => s != null && !s.hidden && s.PlayerIsFriendly() && (suits == null || suits(s));
        var home = GamePlayer.current?.homeStation;
        if (home != null && home.IsCurrentSystem() && Ok(home)) return home;
        if (SystemMapData.current != null)
            foreach (var p in SystemMapData.current.pointsOfInterest)
                if (p is Source.Galaxy.POI.SpaceStation s && Ok(s)) return s;
        return null;
    }

    // Why the last EnsureDocked call isn't docked, or that no station in the system suits; null otherwise.
    public string DockNote;

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
