using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Equipment.Module;
using Behaviour.Mining;
using Behaviour.Salvage;
using Behaviour.UI;
using Behaviour.Unit;
using Behaviour.Weapons;
using BepInEx.Configuration;
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
    public override string Title => "Drone Modes";
    public override string Description => "More drone behaviours than the game's Free Roam and Follow Ship: follow at your own distance, guard the ship (only work on targets near it), or take the ship's target. Drones can also stay with another ship of your fleet instead of their own. Combat, painter, mining, salvage and repair drones each get their own version of a mode. The drone button on the HUD cycles through them for the ship you fly; other ships set theirs with the last button on their row in the fleet panel.";
    public override Type[] PatchClasses => new[] { typeof(DroneModePatches) };

    public static readonly string[] ModeNames = { "Game toggle", "Follow the ship", "Guard the ship", "Ship's target only" };

    public ConfigEntry<bool> ShipTargetFallback;
    public ConfigEntry<bool> StayOnHold;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        ShipTargetFallback = cfg.Bind(Id, "ShipTargetFallback", false, SettingInfo.Describe("Ship's target only: guard when the ship has none",
            "When on, drones in the Ship's target only mode act like Guard the ship while the ship has no target, instead of waiting. A ship you fly yourself seldom has a target unless you click one or something shoots at it. Off by default: they wait for the ship's target."));
        StayOnHold = cfg.Bind(Id, "StayOnHold", false, SettingInfo.Describe("Drones stay near the ship on Hold Position",
            "When on, drones in one of the mod's modes act like Guard the ship while the ship they stay with is on Hold Position, so they don't chase targets far from it. Off by default: Hold Position doesn't reach drones."));
    }

    public override void OnActivated() => GameEvents.SaveLoaded += DroneModePatches.ResetState;

    // The mode a drone's rules follow: Follow the ship and Ship's target only turn into Guard the ship while the anchor
    // holds position and that setting is on.
    public static DroneMode Effective(DroneMode mode, AbstractUnit anchor)
    {
        if (mode != DroneMode.Game && mode != DroneMode.Guard && Instance.StayOnHold.Value && anchor != null && anchor.holdPosition) return DroneMode.Guard;
        return mode;
    }

    // The target the commanding ship has picked by hand, read the way the game's drones copy it (Drone.PropagateCommanderManualTarget).
    public static TargetableUnit CommanderManualTarget(AbstractUnit commander)
    {
        TargetableUnit t = null;
        if (commander.targetProvider != null) t = commander.targetProvider.manualTarget;
        if (t == null) { var c = commander.GetModule<CombatModule>(); if (c != null) t = c.manualTarget; }
        if (t == null) { var m = commander.GetModule<MiningModule>(); if (m != null) t = m.manualTarget; }
        if (t == null) { var s = commander.GetModule<SalvageModule>(); if (s != null) t = s.manualTarget; }
        return t;
    }

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

    // The radius drones are held to: never inside the anchor's own hull, so a big ship's drones don't sit pressed against it.
    public static float Reach(float radius, TargetableUnit anchor) => Mathf.Max(radius, anchor.radius + 2f);

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

    private static void SkipGatherer(SelfCheckContext ctx, string reason) => SkipAfter(ctx, reason, "gatherer", "gatherer-guard", "gatherer-assist");

    // An early exit leaves the later checks without a line; the harness counts lines, so each reports why it didn't run.
    private static void SkipAfter(SelfCheckContext ctx, string reason, params string[] names)
    {
        foreach (var n in names) ctx.Skip(n, reason);
    }

    // Mining and salvage drones in Ship's target join the ship's asteroid or wreck. The flagship's last drone slot is
    // swapped for a mining drone (asteroids here) or a salvage drone (a wreck is spawned if there is none) for the
    // check, and everything is put back afterwards.
    private static IEnumerator CheckGatherer(SelfCheckContext ctx, SpaceShip ship, DroneBayModule bay)
    {
        var slots = ship.spaceShipData.droneSlots;
        var poi = GamePlayer.current.currentPointOfInterest;
        if (slots.Count == 0 || bay.droneAmount == 0 || poi == null)
        {
            SkipGatherer(ctx, slots.Count == 0 || bay.droneAmount == 0 ? "flagship has no drone slots" : "no current location");
            yield break;
        }
        bool mining = UnityEngine.Object.FindAnyObjectByType<Asteroid>() != null;
        string kind = mining ? "Mining Drone Laser" : "Salvage Drone Laser";
        SalvageData spawned = null;
        GameObject spawnedObject = null;
        Source.Data.Persistable.AsteroidData spawnedRock = null;
        // The bay builds drones only for its first droneAmount slots (DroneBayModule.AddNewDrone); the saved slot list
        // can be longer, so the swap goes into the last slot the bay really uses.
        int idx = Mathf.Min(slots.Count, bay.droneAmount) - 1;
        var old = slots[idx];
        var g = ship.spaceShipData.guid;
        var s = ShipSettings.Get(g);
        var saved = (s.droneMode, s.droneRadius, s.droneAnchor);
        AbstractTargetingModule shipModule = null;
        TargetableUnit heldManual = null, heldAuto = null;
        // Guard's radius is at least the popup's 2 and the hull plus 2, so "just outside" (edge - 1) needs a job that far clear of the hull.
        float minEdge = Mathf.Max(3f, ship.radius + 3f);
        Vector2 clear = (Vector2)ship.transform.position + Vector2.right * (ship.radius + 14f);
        try
        {
            // A job of the drone's kind far enough out, spawned when the location has none.
            bool jobClear = false;
            if (mining)
            {
                foreach (var a in UnityEngine.Object.FindObjectsByType<Asteroid>(FindObjectsInactive.Exclude))
                    if (DroneModes.Edge(ship.transform.position, a) >= minEdge) { jobClear = true; break; }
                var ores = OrePreference.OrePreference.KnownOres();
                if (!jobClear && ores.Count > 0) spawnedRock = SelfCheckContext.SpawnAsteroid(clear, ores[0]);
            }
            else
            {
                foreach (var w in SalvageContainer.all)
                    if (w != null && w.gameObject.activeInHierarchy && DroneModes.Edge(ship.transform.position, w) >= minEdge) { jobClear = true; break; }
                if (!jobClear)
                {
                    int level = Mathf.Max(1, ship.spaceShipData.level);
                    spawned = new SalvageData
                    {
                        position = clear,
                        angle = 0,
                        shipTemplate = poi.FindSalvageShipTemplate(Faction.marauders),
                    };
                    spawned.AddScrapContent(level, 0.5f);
                    spawned.AddStructuralContent(level);
                    spawnedObject = poi.AddPersistable(spawned);
                }
            }
            if (!jobClear) yield return null;
            slots[idx] = Drone.Get(kind);
            bay.ForceResetDrones();
            bay.DeployDrones();
            Drone gatherer = null;
            // The bay launches its drones one after another (DroneBayModule.CoroutineDeployDrones) and the swapped slot is
            // the last, so a six-drone bay needs longer than a three-drone one.
            float until = Time.realtimeSinceStartup + 12f + 3f * slots.Count;
            while (gatherer == null && Time.realtimeSinceStartup < until)
            {
                foreach (var d in bay.drones)
                    if (d != null && d.droneIndex == idx && d.isDeployed && !d.docking && d.targetProvider != null) { gatherer = d; break; }
                if (gatherer == null) yield return new WaitForSeconds(0.5f);
            }
            if (gatherer == null)
            {
                int deployed = 0;
                foreach (var d in bay.drones) if (d != null && d.isDeployed) deployed++;
                SkipGatherer(ctx, $"{kind} didn't deploy in {12f + 3f * slots.Count:0} s ({deployed} of {bay.drones.Count} drones out, bay deploying {bay.deploying})");
                yield break;
            }
            var gm = gatherer.targetProvider;
            TargetableUnit job = null, nearest = null;
            float nearestEdge = float.MaxValue;
            foreach (var t in UnityEngine.Object.FindObjectsByType<TargetableUnit>(FindObjectsInactive.Exclude))
            {
                if (t == null || !DroneModes.CanWork(gm, t)) continue;
                float te = DroneModes.Edge(ship.transform.position, t);
                if (te < nearestEdge) { nearest = t; nearestEdge = te; }
                if (te >= minEdge && (job == null || te < DroneModes.Edge(ship.transform.position, job))) job = t;
            }
            if (job == null)
            {
                SkipGatherer(ctx, nearest == null ? "nothing " + gatherer.name + " can work on here"
                    : $"nearest job {nearest.name} is {nearestEdge:0.0}u from the ship's centre, a Guard probe needs {minEdge:0.0}u");
                yield break;
            }
            // Guard: the drone's own list, just outside and just inside the radius around the ship.
            var e = ShipSettings.Edit(g);
            float edge = DroneModes.Edge(ship.transform.position, job);
            (e.droneMode, e.droneRadius, e.droneAnchor) = (DroneMode.Guard, edge - 1f, null);
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
            SelfCheckContext.RemoveAsteroid(spawnedRock);
        }
    }

    // Drones with nothing to do inside their radius stop instead of drifting and turning at a stale point. Ship's target
    // only with no target on the flagship and the fallback off is the idle state; the drone is thrown in at speed and
    // spin and has to settle. Switching back to Game toggle must hand the game's own movement back.
    private IEnumerator CheckIdle(SelfCheckContext ctx, SpaceShip ship, DroneBayModule bay)
    {
        Drone d = null;
        foreach (var u in bay.drones)
            if (u != null && u.isDeployed && !u.isReturning && !u.docking && FightsEnemies(u.targetProvider)) { d = u; break; }
        if (d == null) { SkipAfter(ctx, "no deployed combat or painter drone on the flagship", "idle-stops", "idle-release"); yield break; }
        var g = ship.spaceShipData.guid;
        var s = ShipSettings.Get(g);
        var saved = (s.droneMode, s.droneRadius, s.droneAnchor);
        var module = d.targetProvider;
        AbstractTargetingModule shipModule = ship.GetModule<CombatModule>();
        if (shipModule == null) shipModule = ship.targetProvider;
        TargetableUnit heldManual = shipModule.manualTarget, heldAuto = shipModule.autoTarget;
        bool fallbackBefore = ShipTargetFallback.Value;
        try
        {
            ShipTargetFallback.Value = false;
            shipModule.ResetCurrentTargets();
            module.ResetCurrentTargets();
            var e = ShipSettings.Edit(g);
            (e.droneMode, e.droneRadius, e.droneAnchor) = (DroneMode.ShipTarget, 40f, null);
            // Clear of the hull, so the ship can't bump it while it settles.
            d.rigidbody.position = (Vector2)ship.transform.position + Vector2.right * (ship.radius + 10f);
            d.rigidbody.linearVelocity = new Vector2(4f, 3f);
            d.rigidbody.angularVelocity = 180f;
            // As if it had just gone idle while moving: the hold starts again from here.
            DroneModePatches.ReleaseIdle(d);
            bool hadTarget = false;
            for (int i = 0; i < 12 && d != null; i++)
            {
                yield return new WaitForSeconds(0.25f);
                if (d != null) hadTarget |= d.targetProvider != null && d.targetProvider.priorityTarget != null;
            }
            float maxSpeed = 0f, maxSpin = 0f;
            Vector2 from = d != null ? d.rigidbody.position : Vector2.zero;
            for (int i = 0; i < 8 && d != null; i++)
            {
                yield return new WaitForSeconds(0.25f);
                if (d == null) break;
                maxSpeed = Mathf.Max(maxSpeed, d.rigidbody.linearVelocity.magnitude);
                maxSpin = Mathf.Max(maxSpin, Mathf.Abs(d.rigidbody.angularVelocity));
                hadTarget |= d.targetProvider != null && d.targetProvider.priorityTarget != null;
            }
            if (d == null) { SkipAfter(ctx, "the drone was destroyed during the check", "idle-stops", "idle-release"); yield break; }
            // Drifting is the drone getting somewhere: its travel over the 2 s, not a small wobble on its stopping point.
            float drift = Vector2.Distance(from, d.rigidbody.position);
            float away = Vector2.Distance(d.rigidbody.position, ship.transform.position);
            var hold = d.unitData.overrideTarget;
            string idleDetail = $"{d.name} idle in Ship's target only, 2 s after 3 s settling: moved {drift:0.00}u, top speed {maxSpeed:0.00} (now {d.rigidbody.linearVelocity.magnitude:0.00}, ship {ship.rigidbody.linearVelocity.magnitude:0.00}), top spin {maxSpin:0.0} deg/s, keepMoving={d.keepMoving}, {away:0.0}u from the ship, hold point {(hold.HasValue ? Vector2.Distance(hold.Value, d.rigidbody.position).ToString("0.00") + "u away" : "none")}, destination {Vector2.Distance(d.currentDestination, d.rigidbody.position):0.00}u away, target {(d.targetProvider != null && d.targetProvider.priorityTarget != null ? d.targetProvider.priorityTarget.targetName : "none")}";
            // Ship's target only follows the flagship's target, and the flagship fights back when something hits it.
            if (hadTarget) ctx.Skip("idle-stops", "the drone had a target while measured, so it wasn't idle (something is fighting the flagship): " + idleDetail);
            else if (!d.keepMoving && drift < 0.5f && maxSpeed < 1f && maxSpin < 15f) ctx.Pass("idle-stops", idleDetail);
            else ctx.Fail("idle-stops", idleDetail);

            e.droneMode = DroneMode.Game;
            yield return new WaitForSeconds(0.5f);
            string releaseDetail = $"back on Game toggle: keepMoving={(d != null ? d.keepMoving.ToString() : "drone gone")}";
            if (d != null && d.keepMoving) ctx.Pass("idle-release", releaseDetail);
            else ctx.Fail("idle-release", releaseDetail);
        }
        finally
        {
            ShipTargetFallback.Value = fallbackBefore;
            module.ResetCurrentTargets();
            shipModule.ResetCurrentTargets();
            if (heldManual != null) shipModule.SetManualTarget(heldManual);
            if (heldAuto != null) shipModule.SetAutoTarget(heldAuto);
            var c = ShipSettings.Edit(g);
            (c.droneMode, c.droneRadius, c.droneAnchor) = saved;
            ShipSettings.Save(g);
        }
    }

    // With a route set the game's scanner skips all of your units, drones too. A drone in Guard the ship still has to
    // find an enemy near the ship; one on Game toggle stays as the game has it, with nothing scanned for it.
    private IEnumerator CheckRoute(SelfCheckContext ctx, Drone fighter, AbstractTargetingModule module, SpaceShip commander, List<TargetableUnit> enemies)
    {
        var player = GamePlayer.current;
        MapPointOfInterest open = null;
        foreach (var p in SystemMapData.current.allPointsOfInterest)
            if (!(p is Source.Galaxy.POI.SpaceStation)) { open = p; break; }
        var scanner = fighter.GetComponentInChildren<ScannerModule>();
        Vector2 home = commander.transform.position;
        TargetableUnit near = null;
        float nearEdge = float.MaxValue;
        foreach (var t in enemies)
        {
            if (t == null) continue;
            float edge = Edge(home, t);
            if (edge < nearEdge) { near = t; nearEdge = edge; }
        }
        // The earlier checks leave enemies wherever the fight took them; when the nearest is out of the drone's scanner
        // reach or more than 40u from the ship, it's moved 5u from the drone, on the ship's side.
        string movedNear = "";
        if (near is AbstractUnit unit && scanner != null
            && (nearEdge + 1f > 40f || Vector2.Distance(fighter.transform.position, near.transform.position) > scanner.range))
        {
            Vector2 drone = fighter.transform.position;
            Vector2 toShip = home - drone;
            Vector2 to = drone + (toShip.sqrMagnitude > 0.01f ? toShip.normalized : Vector2.right) * Mathf.Min(5f, toShip.magnitude * 0.5f);
            unit.transform.position = to;
            if (unit.rigidbody != null) unit.rigidbody.position = to;
            Physics2D.SyncTransforms();
            nearEdge = Edge(home, near);
            movedNear = $" ({near.targetName} moved beside the drone)";
        }
        string skip = open == null ? "no location in this system that isn't a station to route to"
            : scanner == null ? "the drone has no scanner"
            : near == null || nearEdge + 1f > 40f ? "no enemy within 40u of the ship" + movedNear
            : Vector2.Distance(fighter.transform.position, near.transform.position) > scanner.range ? $"nearest enemy is {Vector2.Distance(fighter.transform.position, near.transform.position):0}u from the drone, its scanner reaches {scanner.range:0}u{movedNear}"
            : null;
        if (skip != null) { SkipAfter(ctx, skip, "route-guard", "route-game"); yield break; }

        var g = commander.spaceShipData.guid;
        var s = ShipSettings.Get(g);
        var saved = (s.droneMode, s.droneRadius, s.droneAnchor);
        var route = new List<MapPointOfInterest>(player.waypoints);
        try
        {
            var e = ShipSettings.Edit(g);
            float radius = Mathf.Clamp(nearEdge + 1f, 2f, 40f);
            (e.droneMode, e.droneRadius, e.droneAnchor) = (DroneMode.Guard, radius, null);
            module.ResetCurrentTargets();
            module.filteredTargets.Clear();
            player.waypoints.Clear();
            player.waypoints.Add(open);
            float until = Time.realtimeSinceStartup + 4f;
            while (module.autoTarget == null && Time.realtimeSinceStartup < until) yield return new WaitForSeconds(0.25f);
            var got = module.autoTarget;
            string guardDetail = $"route set, Guard radius {radius:0.0}, nearest enemy edge {nearEdge:0.0}u{movedNear}: drone target -> {(got == null ? "none" : got.targetName + " at " + Edge(home, got).ToString("0.0") + "u")}";
            if (got != null && Edge(home, got) <= radius * 1.25f) ctx.Pass("route-guard", guardDetail);
            else ctx.Fail("route-guard", guardDetail);

            module.ResetCurrentTargets();
            module.filteredTargets.Clear();
            e.droneMode = DroneMode.Game;
            yield return new WaitForSeconds(2f);
            var gameGot = module.autoTarget;
            string gameDetail = $"route set, Game toggle, 2 s: drone target -> {(gameGot == null ? "none" : gameGot.targetName)}";
            if (gameGot == null) ctx.Pass("route-game", gameDetail);
            else ctx.Fail("route-game", gameDetail);
        }
        finally
        {
            player.waypoints.Clear();
            player.waypoints.AddRange(route);
            module.filteredTargets.Clear();
            module.ResetCurrentTargets();
            var c = ShipSettings.Edit(g);
            (c.droneMode, c.droneRadius, c.droneAnchor) = saved;
            ShipSettings.Save(g);
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
        // Clear the anchor: the saved one may point at a ship that isn't in the fleet, which the popup lists as an extra entry.
        (e.droneMode, e.droneRadius, e.droneAnchor) = (DroneMode.Guard, 14f, null);
        ShipSettings.Save(g);
        ShipSettings.Invalidate();
        var r = ShipSettings.Get(g);
        var record = SaveData.Ship(g, false);
        string savedMode = record != null && record["droneMode"].IsString ? record["droneMode"].AsString : "none";
        string detail = $"mode={r.droneMode} radius={r.droneRadius} saved={savedMode}";
        if (r.droneMode == DroneMode.Guard && r.droneRadius == 14f && savedMode == "Guard") ctx.Pass("settings-roundtrip", detail);
        else ctx.Fail("settings-roundtrip", detail);

        // The popup shows "Drones stay with" once a mode is set, listing this ship and the others in the fleet.
        TMP_Dropdown OpenAnchorDropdown()
        {
            ShipPopup.Close();
            ShipPopup.Open(data);
            if (!ShipPopup.IsOpen) return null;
            foreach (var dd in ShipPopup.Body.GetComponentsInChildren<TMP_Dropdown>(false))
                if (dd.options.Count > 0 && dd.options[0].text == "This ship") return dd;
            return null;
        }
        string OptionList(TMP_Dropdown dd) => dd == null ? "no visible 'Drones stay with' dropdown" : $"options: {string.Join(", ", dd.options.ConvertAll(o => o.text))}, selected {dd.value}";
        int others = 0;
        foreach (var d in GamePlayer.current.activeFleet)
            if (d != null && d.guid != g) others++;
        var anchorDropdown = OpenAnchorDropdown();
        string popupDetail = $"{OptionList(anchorDropdown)}; fleet ships besides this one: {others}";
        if (anchorDropdown != null && anchorDropdown.options.Count == 1 + others && anchorDropdown.value == 0) ctx.Pass("popup-anchor", popupDetail);
        else ctx.Fail("popup-anchor", popupDetail);
        yield return ctx.Screenshot("ship-popup-drones");

        // A saved anchor that is the ship itself means "This ship", not a stranger.
        ShipSettings.Edit(g).droneAnchor = g;
        ShipSettings.Save(g);
        anchorDropdown = OpenAnchorDropdown();
        string selfDetail = $"anchor = own guid: {OptionList(anchorDropdown)}; fleet ships besides this one: {others}";
        if (anchorDropdown != null && anchorDropdown.options.Count == 1 + others && anchorDropdown.value == 0) ctx.Pass("popup-anchor-self", selfDetail);
        else ctx.Fail("popup-anchor-self", selfDetail);

        // An anchor outside the active fleet stays listed, with the ship's name when it is one of yours.
        SpaceShipData parked = null;
        foreach (var owned in GamePlayer.current.spaceShips)
            if (owned != null && owned.guid != g && !GamePlayer.current.activeFleet.Contains(owned)) { parked = owned; break; }
        string staleGuid = parked != null ? parked.guid : "no-such-ship";
        string staleLabel = parked != null ? parked.GetShipName() + " (not in your fleet)" : "(a ship not in your fleet)";
        ShipSettings.Edit(g).droneAnchor = staleGuid;
        ShipSettings.Save(g);
        anchorDropdown = OpenAnchorDropdown();
        string staleDetail = $"anchor {(parked != null ? "a parked ship of yours" : "an unknown ship")}, expected last entry '{staleLabel}': {OptionList(anchorDropdown)}; fleet ships besides this one: {others}";
        if (anchorDropdown != null && anchorDropdown.options.Count == 2 + others && anchorDropdown.options[anchorDropdown.options.Count - 1].text == staleLabel
            && anchorDropdown.value == anchorDropdown.options.Count - 1) ctx.Pass("popup-anchor-stale", staleDetail);
        else ctx.Fail("popup-anchor-stale", staleDetail);
        ShipPopup.Close();
        ShipSettings.Edit(g).droneAnchor = null;
        ShipSettings.Save(g);

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
            SkipAfter(ctx, "flagship has no drone bay", "follow", "idle-stops", "idle-release", "anchor-follow", "anchor-fallback", "repair-anchor", "guard", "ship-target", "ship-target-fallback", "manual-target", "hold-position", "route-guard", "route-game", "gatherer", "gatherer-guard", "gatherer-assist");
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
            SkipAfter(ctx, "no deployed drone on the flagship", "follow", "idle-stops", "idle-release", "anchor-follow", "anchor-fallback", "repair-anchor", "guard", "ship-target", "ship-target-fallback", "manual-target", "hold-position", "route-guard", "route-game");
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
        // A drone with nothing to do inside the radius stops: no destination, or a stopping point at most a second of
        // its drift ahead of it.
        Vector2 dronePos = drone.rigidbody.position;
        float drift = drone.rigidbody.linearVelocity.magnitude;
        string followDetail = $"{drone.name} at 12u: radius 4 -> destination {(near.HasValue ? Vector2.Distance(near.Value, home).ToString("0.0") + "u from ship" : "none")}, radius 20 -> {(far.HasValue ? "stopping point " + Vector2.Distance(far.Value, dronePos).ToString("0.0") + "u ahead (drifting " + drift.ToString("0.0") + " u/s)" : "none")}";
        if (near.HasValue && Vector2.Distance(near.Value, home) < 1f && (!far.HasValue || Vector2.Distance(far.Value, dronePos) <= drift + 1f)) ctx.Pass("follow", followDetail);
        else ctx.Fail("follow", followDetail);
        yield return CheckIdle(ctx, ship, bay);
        // Enemies about can destroy the drone during the idle test; the anchor test takes another one.
        if (drone == null || !drone.isDeployed)
        {
            drone = null;
            foreach (var d in bay.drones)
                if (d != null && d.isDeployed && !d.isReturning && !d.docking && d.targetProvider != null) { drone = d; break; }
        }

        // Another ship as the anchor: the drone heads for that ship, and an unknown or absent ship falls back to its own.
        SpaceShip wing = null;
        foreach (var d in GamePlayer.current.activeFleet)
            if (d?.unit is SpaceShip w && w != ship && DroneModes.FindShip(d.guid) != null) { wing = w; break; }
        if (wing == null) ctx.Skip("anchor-follow", "no fleet ship here to stay with");
        else if (drone == null)
        {
            ctx.Skip("anchor-follow", "no deployed drone left on the flagship");
            ctx.Skip("anchor-fallback", "no deployed drone left on the flagship");
        }
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
            SkipAfter(ctx, "no deployed combat or painter drone on any of your ships", "guard", "ship-target", "ship-target-fallback", "manual-target", "hold-position", "route-guard", "route-game");
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
            SkipAfter(ctx, "no enemies at this location", "guard", "ship-target", "ship-target-fallback", "manual-target", "hold-position", "route-guard", "route-game");
            yield break;
        }
        // Sorted by hull edge distance from the ship, the measure Guard uses.
        float Edge(TargetableUnit t) => Vector2.Distance(home, t.transform.position) - t.radius;
        enemies.Sort((a, b) => Edge(a).CompareTo(Edge(b)));
        var closest = enemies[0];
        float closestDist = Edge(closest);
        AbstractTargetingModule shipModule = commander.GetModule<CombatModule>();
        if (shipModule == null) shipModule = commander.targetProvider;
        // A manual target on the commander outranks the auto target the check sets, so set it aside and put it back.
        TargetableUnit heldManual = shipModule.manualTarget, heldAuto = shipModule.autoTarget;
        try
        {
            // Guard: the pick through the game's own FindClosestFilteredTarget, just inside and just outside the radius.
            // The radius never goes inside the hull plus 2 units, so the probe uses the enemies beyond that.
            float reach = DroneModes.Reach(0f, commander);
            var probe = enemies.FindAll(t => Edge(t) - 1f >= reach);
            float probeDist = probe.Count > 0 ? Edge(probe[0]) : closestDist;
            module.filteredTargets.Clear();
            module.filteredTargets.AddRange(probe);
            ce.droneMode = DroneMode.Guard;
            ce.droneRadius = probeDist - 1f;
            var outside = module.FindClosestFilteredTarget();
            ce.droneRadius = probeDist + 1f;
            var inside = module.FindClosestFilteredTarget();
            string guardDetail = $"{commander.spaceShipData.GetShipName()}'s {fighter.name}: nearest enemy past the hull reach ({reach:0.0}u) at edge {probeDist:0.0}u, radius {probeDist - 1f:0.0} -> {(outside == null ? "none" : outside.targetName)}, radius {probeDist + 1f:0.0} -> {(inside == null ? "none" : inside.targetName + " at " + Edge(inside).ToString("0.0") + "u")}";
            if (probe.Count == 0) ctx.Skip("guard", $"every enemy is within the hull plus 2u reach ({reach:0.0}u) + 1u; the closest is at {closestDist:0.0}u");
            else if (outside == null && inside != null && Edge(inside) <= probeDist + 1f) ctx.Pass("guard", guardDetail);
            else ctx.Fail("guard", guardDetail);
            module.filteredTargets.Clear();
            module.filteredTargets.AddRange(enemies);

            // Ship's target: the pick and the per-frame correction both follow the ship.
            var shipTarget = enemies[enemies.Count - 1];
            shipModule.ResetCurrentTargets();
            shipModule.SetAutoTarget(shipTarget);
            ce.droneMode = DroneMode.ShipTarget;
            var pick = module.FindClosestFilteredTarget();
            module.ResetCurrentTargets();
            module.SetAutoTarget(closest);
            fighter.ApplyBehaviorMode();
            string stDetail = $"ship -> {shipTarget.targetName}, drone pick -> {(pick == null ? "none" : pick.targetName)}, drone after frame -> {(module.autoTarget == null ? "none" : module.autoTarget.targetName)}";
            if (pick == shipTarget && module.autoTarget == shipTarget) ctx.Pass("ship-target", stDetail);
            else ctx.Fail("ship-target", stDetail);

            // Ship's target only while the ship has nothing picked: the drone waits by default and guards with the setting on.
            shipModule.ResetCurrentTargets();
            module.ResetCurrentTargets();
            module.filteredTargets.Clear();
            module.filteredTargets.AddRange(enemies);
            ce.droneRadius = closestDist + 1f;
            var fallbackBefore = ShipTargetFallback.Value;
            TargetableUnit offPick, onPick;
            try
            {
                ShipTargetFallback.Value = false;
                offPick = module.FindClosestFilteredTarget();
                ShipTargetFallback.Value = true;
                onPick = module.FindClosestFilteredTarget();
            }
            finally { ShipTargetFallback.Value = fallbackBefore; }
            string fbDetail = $"ship has no target, closest enemy edge {closestDist:0.0}u, radius {ce.droneRadius:0.0}: setting off -> {(offPick == null ? "none" : offPick.targetName)}, setting on -> {(onPick == null ? "none" : onPick.targetName + " at " + Edge(onPick).ToString("0.0") + "u")}";
            if (offPick == null && onPick != null && Edge(onPick) <= ce.droneRadius) ctx.Pass("ship-target-fallback", fbDetail);
            else ctx.Fail("ship-target-fallback", fbDetail);

            // Manual targets: one the game handed the drone (a scramble) is dropped once it breaks the mode's rule,
            // one the commanding ship has by hand stays.
            var farthest = enemies[enemies.Count - 1];
            ce.droneMode = DroneMode.Guard;
            ce.droneRadius = 2f;
            reach = DroneModes.Reach(2f, commander);
            if (Edge(farthest) <= reach + 0.5f) SkipAfter(ctx, $"every enemy is within {reach:0.#}u of the ship (its hull plus 2u)", "manual-target", "hold-position");
            else
            {
                shipModule.ResetCurrentTargets();
                module.ResetCurrentTargets();
                module.SetManualTarget(farthest);
                bool accepted = module.manualTarget == farthest;
                fighter.ApplyBehaviorMode();
                var afterScramble = module.manualTarget;
                shipModule.SetManualTarget(farthest);
                module.SetManualTarget(farthest);
                fighter.ApplyBehaviorMode();
                var afterClick = module.manualTarget;
                string manualDetail = $"Guard radius 2, enemy edge {Edge(farthest):0.0}u: handed by the game -> {(afterScramble == null ? "dropped" : "kept")}, also the ship's manual target -> {(afterClick == farthest ? "kept" : "dropped")}";
                if (!accepted) ctx.Skip("manual-target", "the drone's module refused a manual target");
                else if (afterScramble == null && afterClick == farthest) ctx.Pass("manual-target", manualDetail);
                else ctx.Fail("manual-target", manualDetail);

                // Hold position: with the setting on, Follow the ship picks like Guard does, so a far enemy is left alone.
                shipModule.ResetCurrentTargets();
                module.ResetCurrentTargets();
                module.filteredTargets.Clear();
                module.filteredTargets.Add(farthest);
                ce.droneMode = DroneMode.Follow;
                bool holdBefore = commander.holdPosition, playerHoldBefore = GamePlayer.current.holdPosition, stayBefore = StayOnHold.Value;
                TargetableUnit holdOff, holdOn;
                try
                {
                    commander.SetHoldPosition(true);
                    StayOnHold.Value = false;
                    holdOff = module.FindClosestFilteredTarget();
                    StayOnHold.Value = true;
                    holdOn = module.FindClosestFilteredTarget();
                }
                finally
                {
                    commander.SetHoldPosition(holdBefore);
                    GamePlayer.current.holdPosition = playerHoldBefore;
                    StayOnHold.Value = stayBefore;
                }
                string holdDetail = $"Follow radius 2 on Hold position, enemy edge {Edge(farthest):0.0}u: setting off -> {(holdOff == null ? "none" : holdOff.targetName)}, setting on -> {(holdOn == null ? "none" : holdOn.targetName)}";
                if (holdOff == farthest && holdOn == null) ctx.Pass("hold-position", holdDetail);
                else ctx.Fail("hold-position", holdDetail);
            }
        }
        finally
        {
            module.filteredTargets.Clear();
            module.ResetCurrentTargets();
            shipModule.ResetCurrentTargets();
            if (heldManual != null) shipModule.SetManualTarget(heldManual);
            if (heldAuto != null) shipModule.SetAutoTarget(heldAuto);
            (ce.droneMode, ce.droneRadius) = csaved;
            ShipSettings.Save(cg);
        }
        yield return CheckRoute(ctx, fighter, module, commander, enemies);
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
            if (!DroneModes.TryGet(__instance.droneCommander, out var mode, out var radius, out var anchor))
            {
                ReleaseIdle(__instance);
                return true;
            }
            var state = StateOf(__instance);
            ScanDuringRoute(__instance, state);
            var module = __instance.targetProvider;
            var eff = DroneModes.Effective(mode, anchor);
            radius = DroneModes.Reach(radius, anchor);
            Vector2 home = anchor.transform.position;
            TargetableUnit shipTarget = null;
            if (eff == DroneMode.ShipTarget && module != null && DroneModes.FightsEnemies(module))
            {
                var st = DroneModes.AnchorTarget(anchor, module);
                if (st != null && module.IsValidTarget(st)) shipTarget = st;
                else if (feature.ShipTargetFallback.Value) eff = DroneMode.Guard;
            }
            if (module != null && eff != DroneMode.Follow && DroneModes.FightsEnemies(module))
            {
                // A target the game handed the drone (a scramble when the ship is hit) isn't one you picked. Yours, the
                // one the commanding ship has by hand, always stays.
                var held = module.manualTarget;
                if (held != null && held != DroneModes.CommanderManualTarget(__instance.droneCommander))
                {
                    bool drop = eff == DroneMode.ShipTarget ? held != shipTarget : DroneModes.Edge(home, held) > radius * 1.25f;
                    if (drop) module.ResetCurrentTargets();
                }
                if (module.manualTarget == null)
                {
                    if (eff == DroneMode.ShipTarget)
                    {
                        if (shipTarget != null)
                        {
                            if (module.autoTarget != shipTarget) module.SetAutoTarget(shipTarget);
                        }
                        else if (module.autoTarget != null) module.ResetAutoTarget();
                    }
                    else if (eff == DroneMode.Guard)
                    {
                        // A quarter of slack so a target on the edge doesn't flicker in and out.
                        var t = module.autoTarget;
                        if (t != null && DroneModes.Edge(home, t) > radius * 1.25f) module.ResetAutoTarget();
                    }
                }
            }
            var target = module != null ? module.priorityTarget : null;
            if (target != null) ReleaseIdle(__instance);
            else if (Vector2.Distance(__instance.rigidbody.position, home) > radius)
            {
                ReleaseIdle(__instance);
                __instance.SetOverrideDestination(home, clear: true);
            }
            else if (!state.holding)
            {
                // Idle inside the radius: stop where it is. The game keeps steering at the last point it was given and
                // nudges a stopped drone forward when keepMoving is on, which shows as drifting and turning in place.
                state.holding = true;
                __instance.keepMoving = false;
                Hold(__instance);
            }
            return false;
        }
        catch (Exception e)
        {
            feature.Fail(e, nameof(DroneModePatches));
            return true;
        }
    }

    private sealed class DroneState
    {
        public bool holding;
        public float nextScan;
        public ScannerModule scanner;
    }

    private static readonly Dictionary<Drone, DroneState> states = new Dictionary<Drone, DroneState>();
    private static readonly List<Drone> deadDrones = new List<Drone>();

    internal static void ResetState() => states.Clear();

    private static DroneState StateOf(Drone d)
    {
        if (!states.TryGetValue(d, out var s))
        {
            // Drones die and are rebuilt with every launch; drop the destroyed ones before the table grows.
            if (states.Count >= 64)
            {
                deadDrones.Clear();
                foreach (var k in states.Keys)
                    if (k == null) deadDrones.Add(k);
                foreach (var k in deadDrones) states.Remove(k);
            }
            s = new DroneState();
            states[d] = s;
        }
        return s;
    }

    // Gives the drone back to the game's own idle handling: movement on, and no leftover stop point.
    internal static void ReleaseIdle(Drone d)
    {
        if (!states.TryGetValue(d, out var s) || !s.holding) return;
        s.holding = false;
        d.keepMoving = true;
        d.ClearOverrideDestination();
    }

    // Stops the drone: its destination becomes a point a second ahead along its drift, so the game's own thrust control
    // brakes it to a halt there instead of turning it round to fly back. The game copies an override into the point it
    // steers at only on its own movement step, which skips some drones, so that point is set as well.
    private static void Hold(Drone d)
    {
        var at = d.rigidbody.position + d.rigidbody.linearVelocity;
        d.SetOverrideDestination(at);
        d.currentDestination = at;
    }

    // A drone steers its nose at its destination every physics step (Drone.FreeRotation). Held on its own position,
    // that point sits under it and the angle to it flips each step, which spins the drone in place. While it is held
    // the facing is taken over: brake any spin, then keep the heading it has.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Drone), "GetFacingOverrideAngle")]
    private static void FacingPostfix(Drone __instance, ref float? __result)
    {
        if (__result.HasValue || states.Count == 0 || !states.TryGetValue(__instance, out var s) || !s.holding) return;
        var feature = DroneModes.Instance;
        if (!feature.Active) return;
        try
        {
            float spin = __instance.rigidbody.angularVelocity;
            // RotateShip brakes when the spin runs against the asked turn and ignores turns under half a degree.
            __result = Mathf.Abs(spin) < 1f ? 0f : 0.6f * Mathf.Sign(spin);
        }
        catch (Exception e) { feature.Fail(e, nameof(DroneModePatches)); }
    }

    // The game's scanner skips every unit of yours while a route is set, so drones in one of the mod's modes would
    // never find anything to work on during travel. Their own scanner runs twice a second instead. Your ship's scanner
    // is left alone.
    private static void ScanDuringRoute(Drone d, DroneState s)
    {
        var player = GamePlayer.current;
        if (player == null || player.waypoints.Count == 0 || Time.time < s.nextScan) return;
        s.nextScan = Time.time + 0.5f;
        if (s.scanner == null) s.scanner = d.GetComponentInChildren<ScannerModule>();
        if (s.scanner != null && s.scanner.active) s.scanner.SelectClosestTargetInRange(s.scanner.range);
    }

    // A drone's own pick. Guard: nearest target within the radius of the anchor. Ship's target: combat and painter
    // drones take only the anchor's target; mining and salvage drones take the anchor's asteroid or wreck when they can
    // work it, else Guard's pick; repair drones take the anchor when it is damaged (in Guard too), else Guard's pick.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(AbstractTargetingModule), "FindClosestFilteredTarget")]
    private static bool PickPrefix(AbstractTargetingModule __instance, ref TargetableUnit __result)
    {
        var feature = DroneModes.Instance;
        if (!feature.Active) return true;
        try
        {
            if (!(__instance.parent is Drone drone)) return true;
            if (!DroneModes.TryGet(drone.droneCommander, out var mode, out var radius, out var anchor)) return true;
            mode = DroneModes.Effective(mode, anchor);
            if (mode == DroneMode.Follow) return true;
            bool fighter = DroneModes.FightsEnemies(__instance);
            if (fighter && mode == DroneMode.ShipTarget)
            {
                var shipTarget = DroneModes.AnchorTarget(anchor, __instance);
                if (shipTarget != null && __instance.IsValidTarget(shipTarget)) { __result = shipTarget; return false; }
                if (!feature.ShipTargetFallback.Value) { __result = null; return false; }
                // No target on the ship and the fallback is on: pick like Guard the ship does.
                mode = DroneMode.Guard;
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
            radius = DroneModes.Reach(radius, anchor);
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
