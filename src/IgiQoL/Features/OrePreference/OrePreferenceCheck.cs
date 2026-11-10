using System.Collections;
using System.Collections.Generic;
using Behaviour.Equipment.Module;
using Behaviour.Gameplay;
using Behaviour.Mining;
using Behaviour.UI;
using Behaviour.Unit;
using Behaviour.Util;
using Behaviour.Weapons;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.Features.DroneModes;
using IgiQoL.UI;
using Source.Data.Persistable;
using Source.Galaxy;
using TMPro;
using UnityEngine;
using MiningPoi = Source.Galaxy.POI.Mining;

namespace IgiQoL.Features.OrePreference;

internal static class OrePreferenceCheck
{
    public static IEnumerator Run(SelfCheckContext ctx)
    {
        var mode0 = OrePreference.Mode;
        var ores0 = new List<string>(OrePreference.Ores);
        ctx.Cleanup(() => OrePreference.Set(mode0, ores0));

        var known = OrePreference.KnownOres();
        var names = new List<string>();
        foreach (var o in known) names.Add($"{o.item.identifier}={OrePreferenceSection.Name(o.item)}");
        if (known.Count >= 3) ctx.Pass("ores", $"{known.Count} ores: {string.Join(", ", names)}");
        else
        {
            ctx.Fail("ores", $"{known.Count} ores: {string.Join(", ", names)}");
            yield break;
        }

        CheckRoundtrip(ctx, known);
        CheckLocations(ctx, known);
        yield return CheckPicks(ctx, known);
        yield return CheckTab(ctx);
    }

    private static void CheckRoundtrip(SelfCheckContext ctx, List<OreItemData> known)
    {
        string a = known[0].item.identifier, b = known[1].item.identifier;
        OrePreference.Set(OreMode.Only, new[] { a, b, a });
        OrePreference.Load();
        var rec = SaveData.Feature("OrePreference", false);
        string detail = $"mode={OrePreference.Mode} ores=[{string.Join(", ", OrePreference.Ores)}] record={(rec != null ? rec.ToString() : "none")}";
        if (OrePreference.Mode == OreMode.Only && OrePreference.Ores.Count == 2 && OrePreference.Ores[0] == a && OrePreference.Ores[1] == b) ctx.Pass("roundtrip", detail);
        else ctx.Fail("roundtrip", detail);
    }

    // Surface ores per mining location in this system, as ECHO's own location search sees them.
    private static Dictionary<MapPointOfInterest, HashSet<string>> SystemOres()
    {
        var map = new Dictionary<MapPointOfInterest, HashSet<string>>();
        foreach (var poi in SystemMapData.current.allPointsOfInterest)
        {
            if (poi.hidden || !(poi is MiningPoi mining)) continue;
            mining.InitializeAsteroids();
            var set = new HashSet<string>();
            foreach (var p in poi.GetPersistables())
                if (p is AsteroidData d && d.surfaceAmount > 0 && d.surfaceItem != null) set.Add(d.surfaceItem.item.identifier);
            map[poi] = set;
        }
        return map;
    }

    // ECHO's location search through the game's own FindSuitableMiningPoi / IsSuitableForMining with the strict flag
    // the FindMiningActivity prefix sets.
    private static void CheckLocations(SelfCheckContext ctx, List<OreItemData> known)
    {
        var idle = Singleton<IdleManager>.Instance;
        var map = SystemOres();
        // An ore some locations have and others don't, so the filter has something to tell apart.
        string ore = null;
        MapPointOfInterest with = null, without = null;
        foreach (var kv in map)
            foreach (var id in kv.Value)
            {
                MapPointOfInterest other = null;
                foreach (var kv2 in map)
                    if (!kv2.Value.Contains(id)) { other = kv2.Key; break; }
                if (other != null && ore == null) { ore = id; with = kv.Key; without = other; }
            }
        string summary = $"{map.Count} mining locations in {SystemMapData.current.name}";
        if (idle == null || ore == null)
        {
            ctx.Skip("locations", summary + (idle == null ? ", no IdleManager" : ", no ore that only some of them have"));
            return;
        }
        string missing = null;
        foreach (var o in known)
        {
            bool anywhere = false;
            foreach (var kv in map) anywhere |= kv.Value.Contains(o.item.identifier);
            if (!anywhere) { missing = o.item.identifier; break; }
        }

        OrePreference.Set(OreMode.Only, new[] { ore });
        bool withOk, withoutOk;
        MapPointOfInterest found;
        OrePreferencePatches.strict = true;
        try
        {
            withOk = idle.IsSuitableForMining(with, TargetLayer.Surface);
            withoutOk = idle.IsSuitableForMining(without, TargetLayer.Surface);
            found = idle.FindSuitableMiningPoi(TargetLayer.Surface);
        }
        finally { OrePreferencePatches.strict = false; }
        bool vanillaWithout = idle.IsSuitableForMining(without, TargetLayer.Surface);
        bool onlyWants = OrePreference.WantsPreferredPoi(TargetLayer.Surface);
        OrePreference.Set(OreMode.Prefer, new[] { ore });
        bool preferWants = OrePreference.WantsPreferredPoi(TargetLayer.Surface);
        bool preferMissingWants = true;
        if (missing != null)
        {
            OrePreference.Set(OreMode.Prefer, new[] { missing });
            preferMissingWants = OrePreference.WantsPreferredPoi(TargetLayer.Surface);
        }
        string detail = $"{summary}; ore {ore}: '{with.name}' suitable={withOk}, '{without.name}' suitable={withoutOk} (game alone {vanillaWithout}), search -> {(found != null ? "'" + found.name + "' has it=" + map.ContainsKey(found) + "/" + (map.TryGetValue(found, out var s) && s.Contains(ore)) : "none")}; strict Only={onlyWants} Prefer={preferWants} Prefer {missing ?? "(every ore is somewhere)"}={preferMissingWants}";
        bool ok = withOk && !withoutOk && vanillaWithout && onlyWants && preferWants && (missing == null || !preferMissingWants)
                  && (found == null || map.TryGetValue(found, out var fs) && fs.Contains(ore));
        if (ok) ctx.Pass("locations", detail);
        else ctx.Fail("locations", detail);
    }

    // Asteroids spawned next to the flagship and a mining drone swapped into its last drone slot, so ECHO's pick
    // (IdleManager.FindClosestMineableAsteroid) and a drone's own pick run with a real mining module.
    private static IEnumerator CheckPicks(SelfCheckContext ctx, List<OreItemData> known)
    {
        yield return SelfCheckContext.EnsureInSpace();
        var ship = Fleet.Flagship;
        var poi = MapPointOfInterest.current;
        var bay = ship != null ? ship.droneBayModule : null;
        if (ship == null || poi == null || bay == null || ship.spaceShipData.droneSlots.Count == 0 || bay.droneAmount == 0)
        {
            ctx.Skip("picks", ship == null ? "not in space" : bay == null ? "flagship has no drone bay" : poi == null ? "no current location" : "the drone bay has no drone slots");
            yield break;
        }

        // Near: an ore nobody prefers. Far: the preferred one.
        OreItemData plain = known[0], wanted = known[1], absent = known[2];
        Vector2 at = ship.transform.position;
        var near = SelfCheckContext.SpawnAsteroid(at + new Vector2(0f, 9f), plain);
        var far = SelfCheckContext.SpawnAsteroid(at + new Vector2(0f, -16f), wanted);
        if (near == null || far == null)
        {
            ctx.Skip("picks", "no asteroid field in this system to copy");
            yield break;
        }
        var spawned = new[] { near, far };

        var slots = ship.spaceShipData.droneSlots;
        // The bay builds drones only for its first droneAmount slots; the saved slot list can be longer.
        int idx = Mathf.Min(slots.Count, bay.droneAmount) - 1;
        var oldDrone = slots[idx];
        var g = ship.spaceShipData.guid;
        var s = ShipSettings.Get(g);
        var savedDrones = (s.droneMode, s.droneRadius, s.droneAnchor);
        try
        {
            var e = ShipSettings.Edit(g);
            e.droneMode = DroneMode.Game;   // the drone modes' own pick stays out of the way
            slots[idx] = Drone.Get("Mining Drone Laser");
            bay.ForceResetDrones();
            bay.DeployDrones();
            MiningModule gm = null;
            // The bay launches its drones one after another and the swapped slot is the last, so allow for the bay's size.
            float until = Time.realtimeSinceStartup + 12f + 3f * slots.Count;
            while (gm == null && Time.realtimeSinceStartup < until)
            {
                foreach (var d in bay.drones)
                    if (d != null && d.droneIndex == idx && d.isDeployed && d.targetProvider is MiningModule m) { gm = m; break; }
                if (gm == null) yield return new WaitForSeconds(0.5f);
            }
            Asteroid nearA = null, farA = null;
            foreach (var t in Object.FindObjectsByType<Asteroid>(FindObjectsInactive.Exclude))
            {
                if (t.asteroidData == near) nearA = t;
                if (t.asteroidData == far) farA = t;
            }
            if (gm == null || nearA == null || farA == null)
            {
                ctx.Skip("picks", $"miningDrone={(gm != null)} asteroids near={(nearA != null)} far={(farA != null)}");
                yield break;
            }
            // Every other asteroid here (a real mining location) would compete; only the two spawned ones count.
            int others = Object.FindObjectsByType<Asteroid>(FindObjectsInactive.Exclude).Length - 2;
            OrePreference.ForgetScene();
            string Name(TargetableUnit t) => t == null ? "none" : t == nearA ? "near(" + plain.item.identifier + ")" : t == farA ? "far(" + wanted.item.identifier + ")" : t.name;

            OrePreference.Set(OreMode.Off, new[] { wanted.item.identifier });
            var echoOff = IdleManager.FindClosestMineableAsteroid(gm);
            OrePreference.Set(OreMode.Prefer, new[] { wanted.item.identifier });
            var echoPrefer = IdleManager.FindClosestMineableAsteroid(gm);
            OrePreference.Set(OreMode.Only, new[] { absent.item.identifier });
            var echoOnlyAbsent = IdleManager.FindClosestMineableAsteroid(gm);
            string echoDetail = $"drone module surface={gm.canMineSurface} core={gm.canMineCore}, other asteroids here {others}: Off -> {Name(echoOff)}, Prefer {wanted.item.identifier} -> {Name(echoPrefer)}, Only {absent.item.identifier} -> {Name(echoOnlyAbsent)}";
            if ((others > 0 || echoOff == nearA) && echoPrefer == farA && echoOnlyAbsent == null) ctx.Pass("echo-pick", echoDetail);
            else ctx.Fail("echo-pick", echoDetail);

            // The drone's own pick from its list (the game's FindClosestFilteredTarget with the auto-pick prefix).
            gm.ResetCurrentTargets();
            gm.filteredTargets.Clear();
            gm.filteredTargets.Add(nearA);
            gm.filteredTargets.Add(farA);
            OrePreference.Set(OreMode.Off, new[] { wanted.item.identifier });
            var autoOff = gm.FindClosestFilteredTarget();
            OrePreference.Set(OreMode.Prefer, new[] { wanted.item.identifier });
            var autoPrefer = gm.FindClosestFilteredTarget();
            gm.filteredTargets.Remove(farA);   // someone else works the preferred asteroid: join it rather than take the other
            var autoJoin = gm.FindClosestFilteredTarget();
            OrePreference.Set(OreMode.Only, new[] { absent.item.identifier });
            var autoOnly = gm.FindClosestFilteredTarget();
            OrePreference.Set(OreMode.Prefer, new[] { absent.item.identifier });
            var autoPreferAbsent = gm.FindClosestFilteredTarget();
            string autoDetail = $"list near+far: Off -> {Name(autoOff)}, Prefer -> {Name(autoPrefer)}; list near only: Prefer -> {Name(autoJoin)}, Only {absent.item.identifier} -> {Name(autoOnly)}, Prefer {absent.item.identifier} -> {Name(autoPreferAbsent)}";
            if (autoOff == nearA && autoPrefer == farA && autoJoin == farA && autoOnly == null && autoPreferAbsent == nearA) ctx.Pass("auto-pick", autoDetail);
            else ctx.Fail("auto-pick", autoDetail);
            gm.filteredTargets.Clear();
        }
        finally
        {
            var c = ShipSettings.Edit(g);
            (c.droneMode, c.droneRadius, c.droneAnchor) = savedDrones;
            ShipSettings.Save(g);
            slots[idx] = oldDrone;
            bay.ForceResetDrones();
            foreach (var d in spawned) SelfCheckContext.RemoveAsteroid(d);
        }
    }

    private static IEnumerator CheckTab(SelfCheckContext ctx)
    {
        var known = OrePreference.KnownOres();
        OrePreference.Set(OreMode.Prefer, new[] { known[1].item.identifier, known[known.Count - 1].item.identifier });
        ModWindow.Open("echo");
        ctx.Cleanup(ModWindow.Close);
        yield return null;
        bool heading = false, modeRow = false, oreRow = false, addRow = false;
        if (ModWindow.IsOpen)
            foreach (var t in ModWindow.Content.GetComponentsInChildren<TMP_Text>())
            {
                heading |= t.text == OrePreference.Instance.Title;
                modeRow |= t.text == "Preferred ores";
                oreRow |= t.text == OrePreferenceSection.Name(known[1].item);
                addRow |= t.text == "Add an ore";
            }
        string detail = $"heading={heading} mode={modeRow} ore={oreRow} add={addRow}";
        if (heading && modeRow && oreRow && addRow) ctx.Pass("tab", detail);
        else ctx.Fail("tab", detail);
        // Scroll to the bottom, where the section sits, for the screenshot.
        var scroll = ModWindow.Content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }
        yield return ctx.Screenshot("window-echo-ores");
        ModWindow.Close();
    }
}
