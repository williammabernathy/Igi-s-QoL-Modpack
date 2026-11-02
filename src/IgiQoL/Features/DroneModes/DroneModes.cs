using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Equipment.Module;
using Behaviour.Mining;
using Behaviour.Salvage;
using Behaviour.UI;
using Behaviour.Unit;
using Behaviour.Weapons;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.UI;
using Source.Data.Persistable;
using Source.Drone;
using Source.Galaxy;
using Source.Player;
using Source.SpaceShip;
using TMPro;
using UnityEngine;

namespace IgiQoL.Features.DroneModes;

// Extra drone behaviours on top of the game's Free roam / Follow ship toggle. Stored per ship (ShipSettings.droneMode);
// the game's own saved mode stays Free roam or Follow ship, so a save without the mod keeps working.
public sealed class DroneModes : Feature
{
    public static DroneModes Instance;

    public DroneModes() { Instance = this; }

    public override string Id => "DroneModes";
    public override string Title => "Drone modes";
    public override string Description => "More drone behaviours than the game's Free roam and Follow ship: follow at your own distance, guard the ship (only work on targets near it), or take the ship's target. Drones can also stay with another ship of your fleet instead of their own. Combat, painter, mining, salvage and repair drones each get their own version of a mode. The drone button on the HUD cycles through them for the ship you fly; other ships set theirs with the last button on their row in the fleet panel.";
    public override Type[] PatchClasses => new[] { typeof(DroneModePatches) };

    public static readonly string[] ModeNames = { "Game toggle", "Follow the ship", "Guard the ship", "Ship's target only" };

    // Mode, radius and anchor (the ship the drones stay with) for drones whose commander is one of your ships with a
    // mod mode set. The anchor is the commander unless another of your ships is picked and is here, not travelling.
    public static bool TryGet(AbstractUnit commander, out DroneMode mode, out float radius, out AbstractUnit anchor)
    {
        mode = DroneMode.Game;
        radius = 10f;
        anchor = commander;
        if (commander == null || !Fleet.IsPlayerShip(commander) || !(commander.unitData is SpaceShipData data)) return false;
        var s = ShipSettings.Get(data.guid);
        mode = s.droneMode;
        radius = s.droneRadius;
        if (s.droneAnchor != null && s.droneAnchor != data.guid)
        {
            var found = FindShip(s.droneAnchor);
            anchor = found != null ? found : commander;
        }
        return mode != DroneMode.Game;
    }

    // Your flagship or a fleet ship with that guid, if it is in space here.
    public static SpaceShip FindShip(string guid)
    {
        var player = GamePlayer.current;
        if (player == null) return null;
        SpaceShipData data = null;
        if (player.currentSpaceShip != null && player.currentSpaceShip.guid == guid) data = player.currentSpaceShip;
        else
            foreach (var d in player.activeFleet)
                if (d != null && d.guid == guid) { data = d; break; }
        var found = data != null ? Fleet.Unit(data) : null;
        return found != null && !found.isDestroyed && !found.travelling && found.gameObject.activeInHierarchy ? found : null;
    }

    // What the anchor works on, from its module of the drone's kind: combat (also for painter drones), mining or salvage.
    public static TargetableUnit AnchorTarget(AbstractUnit anchor, AbstractTargetingModule droneModule)
    {
        AbstractTargetingModule m = anchor.targetProvider;
        switch (droneModule)
        {
            case MiningModule:
                if (!(m is MiningModule)) m = anchor.GetModule<MiningModule>();
                break;
            case SalvageModule:
                if (!(m is SalvageModule)) m = anchor.GetModule<SalvageModule>();
                break;
            default:
                if (!(m is CombatModule))
                {
                    var combat = anchor.GetModule<CombatModule>();
                    if (combat != null) m = combat;
                }
                break;
        }
        return m != null ? m.priorityTarget : null;
    }

    // Whether this drone module can work on t: mining and salvage check the ore or wreck is still there (and a
    // driller has room to attach), repair checks the unit is missing HP the drone can restore.
    public static bool CanWork(AbstractTargetingModule module, TargetableUnit t)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;
        return module switch
        {
            MiningModule mining => t is Asteroid a && mining.IsTargetMineable(a),
            SalvageModule salvage => salvage.IsTargetSalvagable(t.GetComponent<SalvageContainer>()),
            _ => module.IsValidTarget(t),
        };
    }

    // The hull edge of t from p, the distance Guard measures.
    public static float Edge(Vector2 p, TargetableUnit t) => Vector2.Distance(p, t.transform.position) - t.radius;

    // Combat and painter drones: Guard and Ship's target are enforced every frame. Mining, salvage and repair drones
    // get them at their next pick, so a driller or grinder isn't pulled off a job it is attached to.
    public static bool FightsEnemies(AbstractTargetingModule module) => module is CombatModule || module is PainterModule;

    public static string ShortName(DroneMode mode, float radius) => mode switch
    {
        DroneMode.Follow => "Follow " + radius.ToString("0") + "u",
        DroneMode.Guard => "Guard " + radius.ToString("0") + "u",
        DroneMode.ShipTarget => "Ship's target",
        _ => "",
    };

    // Mining and salvage drones in Ship's target join the ship's asteroid or wreck. The flagship's last drone slot is
    // swapped for a mining drone (asteroids here) or a salvage drone (a wreck is spawned if there is none) for the
    // check, and everything is put back afterwards.
    private static IEnumerator CheckGatherer(SelfCheckContext ctx, SpaceShip ship, DroneBayModule bay)
    {
        var slots = ship.spaceShipData.droneSlots;
        var poi = GamePlayer.current.currentPointOfInterest;
        if (slots.Count == 0 || poi == null)
        {
            ctx.Skip("gatherer", slots.Count == 0 ? "flagship has no drone slots" : "no current location");
            yield break;
        }
        bool mining = UnityEngine.Object.FindAnyObjectByType<Asteroid>() != null;
        string kind = mining ? "Mining Drone Laser" : "Salvage Drone Laser";
        SalvageData spawned = null;
        GameObject spawnedObject = null;
        int idx = slots.Count - 1;
        var old = slots[idx];
        var g = ship.spaceShipData.guid;
        var s = ShipSettings.Get(g);
        var saved = (s.droneMode, s.droneRadius, s.droneAnchor);
        AbstractTargetingModule shipModule = null;
        TargetableUnit heldManual = null, heldAuto = null;
        try
        {
            bool wreckHere = false;
            foreach (var w in SalvageContainer.all)
                if (w != null && w.gameObject.activeInHierarchy) { wreckHere = true; break; }
            if (!mining && !wreckHere)
            {
                int level = Mathf.Max(1, ship.spaceShipData.level);
                spawned = new SalvageData
                {
                    position = (Vector2)ship.transform.position + Vector2.right * 12f,
                    angle = 0,
                    shipTemplate = poi.FindSalvageShipTemplate(Faction.marauders),
                };
                spawned.AddScrapContent(level, 0.5f);
                spawned.AddStructuralContent(level);
                spawnedObject = poi.AddPersistable(spawned);
                yield return null;
            }
            slots[idx] = Drone.Get(kind);
            bay.ForceResetDrones();
            bay.DeployDrones();
            Drone gatherer = null;
            float until = Time.realtimeSinceStartup + 12f;
            while (gatherer == null && Time.realtimeSinceStartup < until)
            {
                foreach (var d in bay.drones)
                    if (d != null && d.droneIndex == idx && d.isDeployed && !d.docking && d.targetProvider != null) { gatherer = d; break; }
                if (gatherer == null) yield return new WaitForSeconds(0.5f);
            }
            if (gatherer == null)
            {
                ctx.Skip("gatherer", kind + " didn't deploy");
                yield break;
            }
            var gm = gatherer.targetProvider;
            TargetableUnit job = null;
            foreach (var t in UnityEngine.Object.FindObjectsByType<TargetableUnit>(FindObjectsInactive.Exclude))
                if (t != null && DroneModes.CanWork(gm, t)) { job = t; break; }
            if (job == null)
            {
                ctx.Skip("gatherer", "nothing " + gatherer.name + " can work on here");
                yield break;
            }
            // Guard: the drone's own list, just outside and just inside the radius around the ship.
            var e = ShipSettings.Edit(g);
            float edge = DroneModes.Edge(ship.transform.position, job);
            (e.droneMode, e.droneRadius, e.droneAnchor) = (DroneMode.Guard, Mathf.Max(0.5f, edge - 1f), null);
            // The ship whose job the drone joins: the flagship, or a fleet ship with turrets of that kind as the anchor.
            SpaceShip worker = null;
            var candidates = new List<SpaceShip> { ship };
            foreach (var d in GamePlayer.current.activeFleet)
                if (d?.unit is SpaceShip w && DroneModes.FindShip(d.guid) != null) candidates.Add(w);
            foreach (var c in candidates)
            {
                shipModule = gm is MiningModule ? c.GetModule<MiningModule>() : c.GetModule<SalvageModule>();
                if (shipModule != null) { worker = c; break; }
            }
            if (shipModule != null)
            {
                heldManual = shipModule.manualTarget;
                heldAuto = shipModule.autoTarget;
                shipModule.ResetCurrentTargets();
            }
            gm.filteredTargets.Clear();
            gm.filteredTargets.Add(job);
            var outside = gm.FindClosestFilteredTarget();
            e.droneRadius = edge + 1f;
            var inside = gm.FindClosestFilteredTarget();
            string guardDetail = $"{gatherer.name}, {job.name} at {edge:0.0}u: radius {edge - 1f:0.0} -> {(outside == null ? "none" : outside.name)}, radius {edge + 1f:0.0} -> {(inside == null ? "none" : inside.name)}";
            if (outside == null && inside == job) ctx.Pass("gatherer-guard", guardDetail);
            else ctx.Fail("gatherer-guard", guardDetail);

            // The ship's job: taken in Ship's target, and in Guard when nothing free is in reach.
            if (shipModule == null) ctx.Skip("gatherer-assist", "none of your ships here has a " + gm.GetType().Name + " (turrets of that kind), so there is no job to join");
            else
            {
                shipModule.SetAutoTarget(job);
                (e.droneMode, e.droneRadius, e.droneAnchor) = (DroneMode.ShipTarget, 0.5f, worker == ship ? null : worker.spaceShipData.guid);
                gm.filteredTargets.Clear();
                var pick = gm.FindClosestFilteredTarget();
                e.droneMode = DroneMode.Guard;
                var guardPick = gm.FindClosestFilteredTarget();
                string detail = $"{gatherer.name}: {worker.spaceShipData.GetShipName()} works {job.name} ({edge:0.0}u from the flagship), ship's target -> {(pick == null ? "none" : pick.name)}, guard 0.5u with an empty list -> {(guardPick == null ? "none" : guardPick.name)}";
                if (pick == job && guardPick == job) ctx.Pass("gatherer-assist", detail);
                else ctx.Fail("gatherer-assist", detail);
            }
        }
        finally
        {
            if (shipModule != null)
            {
                shipModule.ResetCurrentTargets();
                if (heldManual != null) shipModule.SetManualTarget(heldManual);
                if (heldAuto != null) shipModule.SetAutoTarget(heldAuto);
            }
            var c = ShipSettings.Edit(g);
            (c.droneMode, c.droneRadius, c.droneAnchor) = saved;
            ShipSettings.Save(g);
            slots[idx] = old;
            bay.ForceResetDrones();
            if (spawned != null)
            {
                poi.RemovePersistable(spawned);
                if (spawnedObject != null) UnityEngine.Object.Destroy(spawnedObject);
            }
        }
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        var ship = Fleet.Flagship;
        if (ship == null) { ctx.Skip("settings-roundtrip", "not in space"); yield break; }
        var data = ship.spaceShipData;
        var g = data.guid;
        var before = ShipSettings.Get(g);
        var saved = (before.droneMode, before.droneRadius, before.droneAnchor);
        ctx.Cleanup(() =>
        {
            var c = ShipSettings.Edit(g);
            (c.droneMode, c.droneRadius, c.droneAnchor) = saved;
            ShipSettings.Save(g);
        });

        var e = ShipSettings.Edit(g);
        (e.droneMode, e.droneRadius) = (DroneMode.Guard, 14f);
        ShipSettings.Save(g);
        ShipSettings.Invalidate();
        var r = ShipSettings.Get(g);
        var record = SaveData.Ship(g, false);
        string savedMode = record != null && record["droneMode"].IsString ? record["droneMode"].AsString : "none";
        string detail = $"mode={r.droneMode} radius={r.droneRadius} saved={savedMode}";
        if (r.droneMode == DroneMode.Guard && r.droneRadius == 14f && savedMode == "Guard") ctx.Pass("settings-roundtrip", detail);
        else ctx.Fail("settings-roundtrip", detail);

        // The popup shows "Drones stay with" once a mode is set, listing this ship and the others in the fleet.
        ShipPopup.Open(data);
        TMP_Dropdown anchorDropdown = null;
        if (ShipPopup.IsOpen)
            foreach (var dd in ShipPopup.Body.GetComponentsInChildren<TMP_Dropdown>(false))
                if (dd.options.Count > 0 && dd.options[0].text == "This ship") { anchorDropdown = dd; break; }
        int others = 0;
        foreach (var d in GamePlayer.current.activeFleet)
            if (d != null && d.guid != g) others++;
        string popupDetail = anchorDropdown == null ? "no visible 'Drones stay with' dropdown" : $"options: {string.Join(", ", anchorDropdown.options.ConvertAll(o => o.text))}; fleet ships besides this one: {others}";
        if (anchorDropdown != null && anchorDropdown.options.Count == 1 + others) ctx.Pass("popup-anchor", popupDetail);
        else ctx.Fail("popup-anchor", popupDetail);
        yield return ctx.Screenshot("ship-popup-drones");
        ShipPopup.Close();

        // The HUD drone button: Follow ship -> Follow -> Guard -> Ship's target -> Free roam, with the label following.
        var controls = UnityEngine.Object.FindObjectsByType<DroneControls>(FindObjectsInactive.Include);
        if (controls.Length == 0 || controls[0].droneBay == null) ctx.Skip("hud-toggle", "no drone controls on the HUD");
        else
        {
            var c = controls[0];
            var gameMode = c.droneBay.behaviorMode;
            e = ShipSettings.Edit(g);
            (e.droneMode, e.droneRadius) = (DroneMode.Game, 12f);
            c.droneBay.SetBehaviorMode(DroneBehaviorMode.FollowShip);
            var seen = new System.Text.StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                c.ToggleBehavior();
                c.UpdateDisplay();
                seen.Append($"{ShipSettings.Get(g).droneMode}/{c.droneBay.behaviorMode} '{(c.behaviorLabel != null ? c.behaviorLabel.text : "")}'; ");
            }
            bool ok = ShipSettings.Get(g).droneMode == DroneMode.Game && c.droneBay.behaviorMode == DroneBehaviorMode.FreeRoam
                && c.behaviorLabel != null && seen.ToString().Contains("'Follow 12u'") && seen.ToString().Contains("'Ship's target'");
            if (ok) ctx.Pass("hud-toggle", seen.ToString());
            else ctx.Fail("hud-toggle", seen.ToString());
            c.droneBay.SetBehaviorMode(gameMode);
        }

        var bay = ship.droneBayModule;
        if (bay == null)
        {
            ctx.Skip("follow", "flagship has no drone bay");
            yield break;
        }
        yield return CheckGatherer(ctx, ship, bay);
        if (bay.AreDronesDocked()) bay.DeployDrones();
        // The gatherer check rebuilt the flagship's drones; the repair check below needs all of them out again.
        float until = Time.realtimeSinceStartup + 15f;
        while ((bay.AreDronesDocked() || bay.deploying) && Time.realtimeSinceStartup < until)
        {
            if (!bay.deploying && bay.AreDronesDocked()) bay.DeployDrones();
            yield return new WaitForSeconds(0.5f);
        }
        Drone drone = null;
        foreach (var d in bay.drones)
            if (d != null && d.isDeployed && !d.isReturning && !d.docking && d.targetProvider != null) { drone = d; break; }
        if (drone == null)
        {
            ctx.Skip("follow", "no deployed drone on the flagship");
            yield break;
        }

        // Follow radius: with no target the drone heads home only when it is further out than the radius.
        drone.targetProvider.ResetCurrentTargets();
        e = ShipSettings.Edit(g);
        (e.droneMode, e.droneRadius) = (DroneMode.Follow, 4f);
        Vector2 home = ship.transform.position;
        drone.rigidbody.position = home + Vector2.right * 12f;
        drone.ClearOverrideDestination();
        drone.ApplyBehaviorMode();
        var near = drone.unitData.overrideTarget;
        e.droneRadius = 20f;
        drone.ApplyBehaviorMode();
        var far = drone.unitData.overrideTarget;
        string followDetail = $"{drone.name} at 12u: radius 4 -> destination {(near.HasValue ? Vector2.Distance(near.Value, home).ToString("0.0") + "u from ship" : "none")}, radius 20 -> {(far.HasValue ? "destination set" : "none")}";
        if (near.HasValue && Vector2.Distance(near.Value, home) < 1f && !far.HasValue) ctx.Pass("follow", followDetail);
        else ctx.Fail("follow", followDetail);

        // Another ship as the anchor: the drone heads for that ship, and an unknown or absent ship falls back to its own.
        SpaceShip wing = null;
        foreach (var d in GamePlayer.current.activeFleet)
            if (d?.unit is SpaceShip w && w != ship && DroneModes.FindShip(d.guid) != null) { wing = w; break; }
        if (wing == null) ctx.Skip("anchor-follow", "no fleet ship here to stay with");
        else
        {
            drone.targetProvider.ResetCurrentTargets();
            e = ShipSettings.Edit(g);
            (e.droneMode, e.droneRadius, e.droneAnchor) = (DroneMode.Follow, 4f, wing.spaceShipData.guid);
            ShipSettings.Save(g);
            ShipSettings.Invalidate();
            var rec = SaveData.Ship(g, false);
            string savedAnchor = rec != null && rec["droneAnchor"].IsString ? rec["droneAnchor"].AsString : "none";
            Vector2 wingPos = wing.transform.position;
            drone.rigidbody.position = wingPos + Vector2.up * 12f;
            drone.ClearOverrideDestination();
            drone.ApplyBehaviorMode();
            var toWing = drone.unitData.overrideTarget;
            string anchorDetail = $"anchor {wing.spaceShipData.GetShipName()} ({Vector2.Distance(wingPos, home):0.0}u from the flagship), saved={(savedAnchor == wing.spaceShipData.guid ? "yes" : savedAnchor)}, destination {(toWing.HasValue ? Vector2.Distance(toWing.Value, wingPos).ToString("0.0") + "u from it" : "none")}";
            if (savedAnchor == wing.spaceShipData.guid && toWing.HasValue && Vector2.Distance(toWing.Value, wingPos) < 1f) ctx.Pass("anchor-follow", anchorDetail);
            else ctx.Fail("anchor-follow", anchorDetail);

            e = ShipSettings.Edit(g);
            e.droneAnchor = "no-such-ship";
            DroneModes.TryGet(ship, out _, out _, out var fallback);
            if (fallback == ship) ctx.Pass("anchor-fallback", "unknown anchor -> own ship");
            else ctx.Fail("anchor-fallback", "unknown anchor -> " + (fallback == null ? "null" : fallback.name));
            e.droneAnchor = null;
        }

        // Repair drones fix the anchor first, in Guard and in Ship's target, wherever it is in the list.
        Drone fixer = null;
        foreach (var u in UnityEngine.Object.FindObjectsByType<Drone>(FindObjectsInactive.Exclude))
            if (u != null && u.isDeployed && !u.isReturning && u.targetProvider is RepairModule && Fleet.IsPlayerShip(u.droneCommander)) { fixer = u; break; }
        if (fixer == null) ctx.Skip("repair-anchor", "no deployed repair drone on any of your ships");
        else
        {
            var boss = (SpaceShip)fixer.droneCommander;
            var bg = boss.spaceShipData.guid;
            var bs = ShipSettings.Get(bg);
            var bsaved = (bs.droneMode, bs.droneRadius, bs.droneAnchor);
            SpaceShip patient = wing != null && wing != boss ? wing : ship != boss ? ship : boss;
            float hull = patient.currentHullHP, shield = patient.currentShieldHP;
            var be = ShipSettings.Edit(bg);
            var rm = fixer.targetProvider;
            try
            {
                patient.currentHullHP = patient.maxHullHP * 0.5f;
                if (patient.maxShieldHP > 0f) patient.currentShieldHP = patient.maxShieldHP * 0.5f;
                (be.droneMode, be.droneRadius, be.droneAnchor) = (DroneMode.Guard, 2f, patient == boss ? null : patient.spaceShipData.guid);
                rm.filteredTargets.Clear();
                var guardPick = rm.FindClosestFilteredTarget();
                be.droneMode = DroneMode.ShipTarget;
                var stPick = rm.FindClosestFilteredTarget();
                string repairDetail = $"{boss.spaceShipData.GetShipName()}'s {fixer.name} guarding {patient.spaceShipData.GetShipName()} at 50%: guard -> {(guardPick == null ? "none" : guardPick.name)}, ship's target -> {(stPick == null ? "none" : stPick.name)}";
                if (guardPick == patient && stPick == patient) ctx.Pass("repair-anchor", repairDetail);
                else ctx.Fail("repair-anchor", repairDetail);
            }
            finally
            {
                patient.currentHullHP = hull;
                patient.currentShieldHP = shield;
                rm.ResetCurrentTargets();
                (be.droneMode, be.droneRadius, be.droneAnchor) = bsaved;
                ShipSettings.Save(bg);
            }
        }

        // Guard and ship's target need a combat drone; look on every ship of yours in space.
        Drone fighter = null;
        foreach (var u in UnityEngine.Object.FindObjectsByType<Drone>(FindObjectsInactive.Exclude))
            if (u != null && u.isDeployed && !u.isReturning && DroneModes.FightsEnemies(u.targetProvider) && Fleet.IsPlayerShip(u.droneCommander)) { fighter = u; break; }
        if (fighter == null)
        {
            ctx.Skip("guard", "no deployed combat or painter drone on any of your ships");
            yield break;
        }
        var commander = (SpaceShip)fighter.droneCommander;
        var cg = commander.spaceShipData.guid;
        var cb = ShipSettings.Get(cg);
        var csaved = (cb.droneMode, cb.droneRadius);
        var ce = ShipSettings.Edit(cg);
        var module = fighter.targetProvider;
        home = commander.transform.position;
        var enemies = new List<TargetableUnit>();
        void Collect()
        {
            enemies.Clear();
            foreach (var u in UnityEngine.Object.FindObjectsByType<AbstractUnit>(FindObjectsInactive.Exclude))
                if (u != null && module.IsValidTarget(u)) enemies.Add(u);
        }
        Collect();
        // The fleet may have killed the TargetPriority check's enemies by now.
        if (enemies.Count < 2 && SelfCheckContext.SpawnEnemies(2))
        {
            float spawnUntil = Time.realtimeSinceStartup + 30f;
            while (enemies.Count < 2 && Time.realtimeSinceStartup < spawnUntil)
            {
                yield return new WaitForSeconds(1f);
                Collect();
            }
            home = commander.transform.position;
        }
        if (enemies.Count == 0)
        {
            ctx.Skip("guard", "no enemies at this location");
            yield break;
        }
        // Sorted by hull edge distance from the ship, the measure Guard uses.
        float Edge(TargetableUnit t) => Vector2.Distance(home, t.transform.position) - t.radius;
        enemies.Sort((a, b) => Edge(a).CompareTo(Edge(b)));
        var closest = enemies[0];
        float closestDist = Edge(closest);
        AbstractTargetingModule shipModule = commander.GetModule<CombatModule>();
        if (shipModule == null) shipModule = commander.targetProvider;
        try
        {
            // Guard: the pick through the game's own FindClosestFilteredTarget, just inside and just outside the radius.
            module.filteredTargets.Clear();
            module.filteredTargets.AddRange(enemies);
            ce.droneMode = DroneMode.Guard;
            ce.droneRadius = Mathf.Max(1f, closestDist - 1f);
            var outside = module.FindClosestFilteredTarget();
            ce.droneRadius = closestDist + 1f;
            var inside = module.FindClosestFilteredTarget();
            string guardDetail = $"{commander.spaceShipData.GetShipName()}'s {fighter.name}: closest enemy edge {closestDist:0.0}u, radius {closestDist - 1f:0.0} -> {(outside == null ? "none" : outside.targetName)}, radius {closestDist + 1f:0.0} -> {(inside == null ? "none" : inside.targetName + " at " + Edge(inside).ToString("0.0") + "u")}";
            if (outside == null && inside != null && Edge(inside) <= closestDist + 1f) ctx.Pass("guard", guardDetail);
            else ctx.Fail("guard", guardDetail);

            // Ship's target: the pick and the per-frame correction both follow the ship.
            var shipTarget = enemies[enemies.Count - 1];
            shipModule.SetAutoTarget(shipTarget);
            ce.droneMode = DroneMode.ShipTarget;
            var pick = module.FindClosestFilteredTarget();
            module.ResetCurrentTargets();
            module.SetAutoTarget(closest);
            fighter.ApplyBehaviorMode();
            string stDetail = $"ship -> {shipTarget.targetName}, drone pick -> {(pick == null ? "none" : pick.targetName)}, drone after frame -> {(module.autoTarget == null ? "none" : module.autoTarget.targetName)}";
            if (pick == shipTarget && module.autoTarget == shipTarget) ctx.Pass("ship-target", stDetail);
            else ctx.Fail("ship-target", stDetail);
        }
        finally
        {
            module.filteredTargets.Clear();
            module.ResetCurrentTargets();
            shipModule.ResetAutoTarget();
            (ce.droneMode, ce.droneRadius) = csaved;
            ShipSettings.Save(cg);
        }
    }
}

[HarmonyPatch]
internal static class DroneModePatches
{
    // Runs every frame for each deployed drone that isn't returning. Replaces the game's follow-ship check.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Drone), "ApplyBehaviorMode")]
    private static bool ApplyPrefix(Drone __instance)
    {
        var feature = DroneModes.Instance;
        if (!feature.Active) return true;
        try
        {
            if (!DroneModes.TryGet(__instance.droneCommander, out var mode, out var radius, out var anchor)) return true;
            var module = __instance.targetProvider;
            Vector2 home = anchor.transform.position;
            if (module != null && module.manualTarget == null && DroneModes.FightsEnemies(module))
            {
                if (mode == DroneMode.ShipTarget)
                {
                    var shipTarget = DroneModes.AnchorTarget(anchor, module);
                    if (shipTarget != null && module.IsValidTarget(shipTarget))
                    {
                        if (module.autoTarget != shipTarget) module.SetAutoTarget(shipTarget);
                    }
                    else if (module.autoTarget != null) module.ResetAutoTarget();
                }
                else if (mode == DroneMode.Guard)
                {
                    // A quarter of slack so a target on the edge doesn't flicker in and out.
                    var t = module.autoTarget;
                    if (t != null && DroneModes.Edge(home, t) > radius * 1.25f) module.ResetAutoTarget();
                }
            }
            var target = module != null ? module.priorityTarget : null;
            if (target == null)
            {
                if (Vector2.Distance(__instance.rigidbody.position, home) > radius) __instance.SetOverrideDestination(home, clear: true);
                else __instance.ClearOverrideDestination();
            }
            return false;
        }
        catch (Exception e)
        {
            feature.Fail(e, nameof(DroneModePatches));
            return true;
        }
    }

    // A drone's own pick. Guard: nearest target within the radius of the anchor. Ship's target: combat and painter
    // drones take only the anchor's target; mining and salvage drones take the anchor's asteroid or wreck when they can
    // work it, else Guard's pick; repair drones take the anchor when it is damaged (in Guard too), else Guard's pick.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AbstractTargetingModule), "FindClosestFilteredTarget")]
    private static bool PickPrefix(AbstractTargetingModule __instance, ref TargetableUnit __result)
    {
        var feature = DroneModes.Instance;
        if (!feature.Active) return true;
        try
        {
            if (!(__instance.parent is Drone drone)) return true;
            if (!DroneModes.TryGet(drone.droneCommander, out var mode, out var radius, out var anchor) || mode == DroneMode.Follow) return true;
            bool fighter = DroneModes.FightsEnemies(__instance);
            if (fighter && mode == DroneMode.ShipTarget)
            {
                var shipTarget = DroneModes.AnchorTarget(anchor, __instance);
                __result = shipTarget != null && __instance.IsValidTarget(shipTarget) ? shipTarget : null;
                return false;
            }
            TargetableUnit assist = null;
            if (__instance is RepairModule)
            {
                if (DroneModes.CanWork(__instance, anchor)) assist = anchor;
            }
            else if (__instance is MiningModule || __instance is SalvageModule)
            {
                var work = DroneModes.AnchorTarget(anchor, __instance);
                if (DroneModes.CanWork(__instance, work)) assist = work;
            }
            if (assist != null && (mode == DroneMode.ShipTarget || __instance is RepairModule))
            {
                __result = assist;
                return false;
            }
            Vector2 home = anchor.transform.position;
            Vector2 from = __instance.transform.position;
            TargetableUnit best = null;
            float bestDist = float.MaxValue;
            foreach (var t in __instance.filteredTargets)
            {
                if (t == null || DroneModes.Edge(home, t) > radius) continue;
                float d = Vector2.Distance(from, t.transform.position);
                if (d < bestDist)
                {
                    best = t;
                    bestDist = d;
                }
            }
            // The game's mining and salvage lists leave out what someone already works on while anything else is
            // left anywhere; a guarding gatherer with nothing free near the anchor joins the anchor's job instead.
            __result = best != null ? best : assist;
            return false;
        }
        catch (Exception e)
        {
            feature.Fail(e, nameof(DroneModePatches));
            return true;
        }
    }

    // The HUD drone button cycles Free roam -> Follow ship (the game's two) -> Follow -> Guard -> Ship's target -> Free roam.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DroneControls), "ToggleBehavior")]
    private static bool TogglePrefix(DroneControls __instance)
    {
        var feature = DroneModes.Instance;
        if (!feature.Active) return true;
        try
        {
            var bay = __instance.droneBay;
            var ship = Fleet.Flagship;
            if (bay == null || ship == null) return true;
            string guid = ship.spaceShipData.guid;
            var s = ShipSettings.Edit(guid);
            if (s.droneMode == DroneMode.Game)
            {
                if (bay.behaviorMode == DroneBehaviorMode.FreeRoam) return true;
                s.droneMode = DroneMode.Follow;
            }
            else if (s.droneMode == DroneMode.ShipTarget)
            {
                s.droneMode = DroneMode.Game;
                bay.SetBehaviorMode(DroneBehaviorMode.FreeRoam);
            }
            else s.droneMode++;
            ShipSettings.Save(guid);
            __instance.UpdateDisplay();
            return false;
        }
        catch (Exception e)
        {
            feature.Fail(e, nameof(DroneModePatches));
            return true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DroneControls), "UpdateDisplay")]
    private static void LabelPostfix(DroneControls __instance)
    {
        var feature = DroneModes.Instance;
        if (!feature.Active) return;
        try
        {
            var label = __instance.behaviorLabel;
            var ship = Fleet.Flagship;
            if (label == null || ship == null || !DroneModes.TryGet(ship, out var mode, out var radius, out _)) return;
            label.text = DroneModes.ShortName(mode, radius);
        }
        catch (Exception e) { feature.Fail(e, nameof(DroneModePatches)); }
    }
}
