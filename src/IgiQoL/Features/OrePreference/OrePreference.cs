using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Crew;
using Behaviour.Equipment.Module;
using Behaviour.Gameplay;
using Behaviour.Managers;
using Behaviour.Mining;
using Behaviour.Unit;
using Behaviour.Util;
using Behaviour.Weapons;
using HarmonyLib;
using IgiQoL.Core;
using LightJson;
using Source.Data.Persistable;
using Source.Galaxy;
using UnityEngine;
using MiningPoi = Source.Galaxy.POI.Mining;

namespace IgiQoL.Features.OrePreference;

public enum OreMode { Off, Prefer, Only }

public sealed class OrePreference : Feature
{
    public static OrePreference Instance;

    public OrePreference() { Instance = this; }

    public override string Id => "OrePreference";
    public override string Title => "Preferred Ores";
    public override string Description => "Pick the ores you want, and ECHO and your ships mine asteroids with those ores first instead of the nearest asteroid. ECHO also flies to a mining location in the system that still has them. Set the ores on the ECHO tab.";
    public override string SettingsTab => "ECHO";
    public override Type[] PatchClasses => new[] { typeof(OrePreferencePatches) };

    public static readonly string[] ModeNames = { "Off (nearest asteroid, game default)", "Prefer these ores", "Only these ores" };

    public static OreMode Mode;
    // Ore item identifiers in the order they were added.
    public static readonly List<string> Ores = new();

    public override void OnActivated()
    {
        Load();
        GameEvents.SaveLoaded += OnSaveLoaded;
        EchoPriority.EchoPriority.EnsureTab();
    }

    private void OnSaveLoaded()
    {
        if (!Active) return;
        try { Load(); }
        catch (Exception e) { Fail(e, nameof(OnSaveLoaded)); }
    }

    internal static void Load()
    {
        nextElsewhereCheck = 0f;
        nextSceneScan = 0f;
        sceneAsteroids.Clear();
        scannedPoi = null;
        OrePreferencePatches.ResetLayer();
        Mode = OreMode.Off;
        Ores.Clear();
        var rec = SaveData.Feature("OrePreference", false);
        if (rec == null) return;
        if (rec["mode"].IsString && Enum.TryParse<OreMode>(rec["mode"].AsString, out var m) && Enum.IsDefined(typeof(OreMode), m)) Mode = m;
        var arr = rec["ores"];
        if (!arr.IsJsonArray) return;
        foreach (var v in arr.AsJsonArray)
            if (v.IsString && !Ores.Contains(v.AsString)) Ores.Add(v.AsString);
    }

    internal static void Write()
    {
        var rec = SaveData.Feature("OrePreference", true);
        rec["mode"] = Mode.ToString();
        var arr = new JsonArray();
        foreach (var id in Ores) arr.Add(id);
        rec["ores"] = arr;
        nextElsewhereCheck = 0f;
    }

    public static void Set(OreMode mode, IList<string> ores)
    {
        Mode = mode;
        Ores.Clear();
        foreach (var id in ores)
            if (!Ores.Contains(id)) Ores.Add(id);
        Write();
    }

    public static bool On => Instance.Active && Mode != OreMode.Off && Ores.Count > 0;

    // The asteroid tooltip only names the core ore with the Basic Scanning mining skill, so the core counts from then on.
    public static bool CoreKnown => SkilltreeNode.miningBasicScanning != null && SkilltreeNode.miningBasicScanning.isActive;

    public static bool IsPreferred(OreItemData ore) => ore != null && Ores.Contains(ore.item.identifier);

    public static bool HasPreferred(AsteroidData a, bool surface, bool core) =>
        a != null && (surface && a.surfaceAmount > 0 && IsPreferred(a.surfaceItem)
                      || core && CoreKnown && a.innerCoreAmount > 0 && IsPreferred(a.innerCoreItem));

    // Every ore the game generates, lowest level first.
    public static List<OreItemData> KnownOres()
    {
        var list = new List<OreItemData>(OreItemData.regularOres);
        list.Sort((a, b) =>
        {
            int c = a.item.itemLevel.CompareTo(b.item.itemLevel);
            return c != 0 ? c : a.item.rarity.CompareTo(b.item.rarity);
        });
        return list;
    }

    public static bool HasPreferred(MapPointOfInterest poi, TargetLayer layer)
    {
        bool surface = layer != TargetLayer.Core, core = layer != TargetLayer.Surface;
        foreach (var p in poi.GetPersistables())
            if (p is AsteroidData a && HasPreferred(a, surface, core)) return true;
        return false;
    }

    // The nearest asteroid with a preferred ore in a layer the module mines; null when none is left.
    // Takes the list and the module itself, not an IEnumerable and a delegate: this runs on the scanner tick and must not allocate.
    public static Asteroid NearestPreferred(List<TargetableUnit> candidates, Vector2 from, bool surface, bool core, MiningModule mineable)
    {
        Asteroid best = null;
        float bestDist = float.MaxValue;
        foreach (var t in candidates)
        {
            if (!(t is Asteroid a) || a == null || !HasPreferred(a.asteroidData, surface, core)) continue;
            if (mineable != null && !mineable.IsTargetMineable(a)) continue;
            float d = Vector2.Distance(from, a.transform.position);
            if (d < bestDist)
            {
                best = a;
                bestDist = d;
            }
        }
        return best;
    }

    // Every asteroid at this location, refreshed at most once a second (fleet ships ask on every scan while idle).
    private static readonly List<TargetableUnit> sceneAsteroids = new();
    private static float nextSceneScan;
    private static BasePoiManager scannedPoi;

    public static List<TargetableUnit> SceneAsteroids()
    {
        var poi = BasePoiManager.current;
        if (poi != scannedPoi || Time.unscaledTime >= nextSceneScan)
        {
            scannedPoi = poi;
            nextSceneScan = Time.unscaledTime + 1f;
            sceneAsteroids.Clear();
            if (poi != null) sceneAsteroids.AddRange(poi.GetComponentsInChildren<Asteroid>());
        }
        return sceneAsteroids;
    }

    internal static void ForgetScene() => nextSceneScan = 0f;

    // Prefer mode: whether another mining location in this system that ECHO can fly to still has a preferred ore.
    private static bool elsewhere;
    private static TargetLayer elsewhereLayer;
    private static float nextElsewhereCheck;

    public static bool PreferredElsewhere(TargetLayer layer)
    {
        if (layer == elsewhereLayer && Time.unscaledTime < nextElsewhereCheck) return elsewhere;
        elsewhereLayer = layer;
        nextElsewhereCheck = Time.unscaledTime + 10f;
        elsewhere = FindPoi(layer) != null;
        return elsewhere;
    }

    // The same walk as IdleManager.FindSuitableMiningPoi, with the preferred-ore test.
    public static MapPointOfInterest FindPoi(TargetLayer layer)
    {
        var system = SystemMapData.current;
        if (system == null || !Singleton<TravelManager>.HasInstance) return null;
        foreach (var poi in system.allPointsOfInterest)
        {
            if (poi.hidden || !(poi is MiningPoi mining) || poi == MapPointOfInterest.current || !Singleton<TravelManager>.Instance.CanWeTravel(poi)) continue;
            mining.InitializeAsteroids();
            if (HasPreferred(poi, layer)) return poi;
        }
        return null;
    }

    public static TargetLayer LayerOf(MiningModule m) =>
        m.canMineSurface && m.canMineCore ? TargetLayer.Both : m.canMineCore ? TargetLayer.Core : TargetLayer.Surface;

    // Whether ECHO's choice of mining location should only accept locations with a preferred ore. Prefer: when the last
    // mining location or another one in the system still has one; with none left anywhere the game picks as usual.
    public static bool WantsPreferredPoi(TargetLayer layer)
    {
        if (!On) return false;
        if (Mode == OreMode.Only || PreferredElsewhere(layer)) return true;
        var last = Source.Player.GamePlayer.current?.lastVisitedMiningPOI;
        return last != null && HasPreferred(last, layer);
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => OrePreferenceCheck.Run(ctx);
}

[HarmonyPatch]
internal static class OrePreferencePatches
{
    // True while FindMiningActivity runs and only locations with a preferred ore count as suitable.
    internal static bool strict;

    // The layer of ECHO's latest FindMiningActivity call.
    private static TargetLayer? echoLayer;
    internal static void ResetLayer() => echoLayer = null;

    // ECHO's own asteroid pick inside a mining location.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), "FindClosestMineableAsteroid")]
    private static bool EchoPickPrefix(MiningModule miningModule, ref Asteroid __result)
    {
        var f = OrePreference.Instance;
        if (!OrePreference.On) return true;
        try
        {
            var pick = OrePreference.NearestPreferred(OrePreference.SceneAsteroids(), miningModule.transform.position,
                miningModule.canMineSurface, miningModule.canMineCore, miningModule);
            if (pick != null)
            {
                __result = pick;
                return false;
            }
            // Nothing preferred here: Only leaves; Prefer leaves too when another location still has some.
            // The layer ECHO searched locations with, so this pick and the location search agree (EchoPriority searches
            // with each list entry's own layer).
            if (OrePreference.Mode == OreMode.Only || OrePreference.PreferredElsewhere(echoLayer ?? OrePreference.LayerOf(miningModule)))
            {
                __result = null;
                return false;
            }
            return true;
        }
        catch (Exception e)
        {
            f.Fail(e, nameof(EchoPickPrefix));
            return true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), "FindMiningActivity")]
    private static void MiningActivityPrefix(TargetLayer targetLayer)
    {
        echoLayer = targetLayer;
        strict = false;
        if (!OrePreference.On) return;
        try { strict = OrePreference.WantsPreferredPoi(targetLayer); }
        catch (Exception e) { OrePreference.Instance.Fail(e, nameof(MiningActivityPrefix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(IdleManager), "FindMiningActivity")]
    private static void MiningActivityPostfix() => strict = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), "IsSuitableForMining")]
    private static bool SuitablePrefix(MapPointOfInterest poi, TargetLayer targetLayer, ref bool __result)
    {
        if (!strict || !OrePreference.On) return true;
        try
        {
            __result = OrePreference.HasPreferred(poi, targetLayer);
            return false;
        }
        catch (Exception e)
        {
            OrePreference.Instance.Fail(e, nameof(SuitablePrefix));
            return true;
        }
    }

    // Your ships' and their drones' own pick when they have no asteroid: preferred ores first.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AbstractTargetingModule), "FindClosestFilteredTarget")]
    private static bool AutoPickPrefix(AbstractTargetingModule __instance, ref TargetableUnit __result, bool __runOriginal)
    {
        if (!__runOriginal) return false;   // an earlier prefix (drone modes) already picked
        if (!OrePreference.On || !(__instance is MiningModule m)) return true;
        try
        {
            var parent = m.parent;
            if (parent == null || !parent.IsPlayer()) return true;
            Vector2 from = m.transform.position;
            var pick = OrePreference.NearestPreferred(m.filteredTargets, from, m.canMineSurface, m.canMineCore, null);
            if (pick == null)
                pick = OrePreference.NearestPreferred(OrePreference.SceneAsteroids(), from, m.canMineSurface, m.canMineCore, m);
            if (pick != null)
            {
                __result = pick;
                return false;
            }
            if (OrePreference.Mode == OreMode.Only)
            {
                __result = null;
                return false;
            }
            return true;
        }
        catch (Exception e)
        {
            OrePreference.Instance.Fail(e, nameof(AutoPickPrefix));
            return true;
        }
    }
}
