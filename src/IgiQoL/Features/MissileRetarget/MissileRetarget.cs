using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Equipment.Module;
using Behaviour.Managers;
using Behaviour.Equipment.Turret.Projectile;
using Behaviour.Weapons;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.Features.TargetPriority;
using Source.Player;
using Source.SpaceShip;
using UnityEngine;
using Behaviour.Equipment.Turret;
using Behaviour.Unit;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.MissileRetarget;

public sealed class MissileRetarget : Feature
{
    public static MissileRetarget Instance;

    public MissileRetarget() { Instance = this; }

    public override string Id => "MissileRetarget";
    public override string Title => "Missile Retargeting";
    public override string Description => "Lets your ships' missiles find a new target when the enemy they were chasing is destroyed, instead of flying on and going to waste.";
    public override Type[] PatchClasses => new[] { typeof(MissilePatches) };

    public ConfigEntry<RetargetMode> Mode;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        Mode = cfg.Bind(Id, "Mode", RetargetMode.Off, SettingInfo.Describe("When a missile's target dies",
            "What your ships' missiles do when their target is destroyed before they hit. Fly on: the game's normal behaviour, so the missile is wasted. Weapon's target: steer to whatever the launcher aims at next. Nearest enemy: steer to the enemy closest to the missile that the launcher could hit; a target you give the launcher yourself afterwards still wins. With either of those, missiles in flight also follow a new target when the launcher switches to it, which the game does only for some launchers. Each ship can override this in its own settings (the last button on its row in the fleet panel).",
            "Fly on (game default)", "Weapon's target", "Nearest enemy"));
    }

    public override void OnActivated()
    {
        GameEvents.SaveLoaded += MissilePatches.Reset;
        GameEvents.MainMenuShown += MissilePatches.Reset;
    }

    // Per-ship override (ShipSettings.missileRetarget) wins over the config value.
    public RetargetMode Resolve(SpaceShipData ship) => ShipSettings.Get(ship.guid).missileRetarget ?? Mode.Value;

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        var flagship = GamePlayer.current.currentSpaceShip;
        var g = flagship.guid;

        // The per-ship choice is saved, wins over the config value, and null falls back to it.
        var kept = ShipSettings.Get(g).missileRetarget;
        var configKept = Mode.Value;
        string modeDetail;
        bool modeOk;
        try
        {
            Mode.Value = RetargetMode.Off;
            ShipSettings.Edit(g).missileRetarget = RetargetMode.Nearest;
            ShipSettings.Save(g);
            ShipSettings.Invalidate();
            var record = SaveData.Ship(g, false);
            string saved = record != null && record["missileRetarget"].IsString ? record["missileRetarget"].AsString : "none";
            var withOverride = Resolve(flagship);
            ShipSettings.Edit(g).missileRetarget = null;
            ShipSettings.Save(g);
            ShipSettings.Invalidate();
            var withoutOverride = Resolve(flagship);
            modeDetail = $"{flagship.GetShipName()}: override Nearest over config Off -> {withOverride} (saved {saved}), no override -> {withoutOverride}";
            modeOk = withOverride == RetargetMode.Nearest && saved == "Nearest" && withoutOverride == RetargetMode.Off;
        }
        finally
        {
            Mode.Value = configKept;
            ShipSettings.Edit(g).missileRetarget = kept;
            ShipSettings.Save(g);
            ShipSettings.Invalidate();
        }
        if (modeOk) ctx.Pass("mode", modeDetail);
        else ctx.Fail("mode", modeDetail);
        yield return CheckLive(ctx);
    }

    private static readonly string[] LiveChecks = { "switch", "off-switch", "nearest-kill", "nearest-click" };

    // Real missiles, fired by the check from one of the flagship's guns (a missile follows its launcher, so any gun
    // works) at enemies A and B, as non-seeking missiles so the game itself never re-acquires a target:
    // - Nearest enemy: fired at A, the launcher switches to B while A lives: the missile turns to B, not to the enemy
    //   nearest it (a switch isn't a kill);
    // - Fly on: the same switch leaves the missile without a target, as the game does;
    // - Nearest enemy: fired at an enemy that is then destroyed: the missile picks the enemy nearest it; then you pick
    //   another one: the missile drops its pick and turns to yours.
    private IEnumerator CheckLive(SelfCheckContext ctx)
    {
        yield return SelfCheckContext.EnsureInSpace();
        var ship = Fleet.Flagship;
        var module = ship != null ? ship.GetModule<CombatModule>() : null;
        var prefab = MissilePrefab();
        if (module == null || prefab == null) { SkipLive(ctx, $"combat module {module != null}, missile prefab {prefab != null}"); yield break; }
        var enemies = new List<AbstractUnit>();
        int found = Enemies(module, enemies);
        // The fleet kills arrivals quickly, so a few more than needed are brought in.
        bool called = found < 3 && SelfCheckContext.SpawnEnemies(6 - found);
        if (called)
        {
            float until = Time.realtimeSinceStartup + 45f;
            while (Enemies(module, enemies) < 3 && Time.realtimeSinceStartup < until) yield return new WaitForSecondsRealtime(0.25f);
        }
        AbstractTurret gun = null;
        if (enemies.Count >= 3)
            foreach (var t in module.turrets)
                if (t != null && t.CanTarget(enemies[0]) && t.CanTarget(enemies[1]) && t.CanTarget(enemies[2])) { gun = t; break; }
        if (gun == null) { SkipLive(ctx, $"needs three enemies one gun can target ({enemies.Count} enemies; {found} here at first, more called in {called})"); yield break; }

        var data = ship.spaceShipData;
        var g = data.guid;
        var kept = ShipSettings.Get(g).missileRetarget;
        var heldManual = module.manualTarget;
        var heldAuto = module.autoTarget;
        var fired = new List<Missile>();
        var (a, b) = Pair(gun, enemies);
        try
        {
            // The fleet keeps fighting, so an enemy or the missile can die inside the second each attempt takes. That
            // says nothing about the mode: the two test enemies get their hull topped up while an attempt runs, an
            // attempt that still loses one is repeated with a fresh pair, and fresh enemies are called in when fewer
            // than two are left.
            SetMode(g, RetargetMode.Nearest);
            string sd = null;
            bool judged = false;
            for (int attempt = 1; attempt <= 4 && !judged; attempt++)
            {
                yield return EnsurePair(module, gun, enemies, a, b, p => (a, b) = p);
                if (!Alive(a) || !Alive(b)) break;
                yield return Aim(module, gun, a);
                var m = Fire(prefab, gun, fired);
                yield return HoldAlive(a, b, m.ignitionDelay + 0.3f);
                var state = m.GetComponent<MissileState>();
                yield return Aim(module, gun, b);
                yield return HoldAlive(a, b, 5 * Time.fixedDeltaTime);
                sd = $"attempt {attempt}: origin {Name(state != null ? state.origin : null)} (fired at {Name(a)}), then {Name(b)}: missile target {(m != null && m.target != null ? m.target.name : "none")}";
                if (!Flying(m) || !Alive(a) || !Alive(b)) continue;
                // The missile follows what the launcher aims at now: B, or since 0.8.3.5 another enemy when B moved out of
                // the launcher's reach (AbstractTurret.UpdateFallbackTarget). Either way it must have left A. When B is
                // out of reach and A is the closest enemy it can work, the game itself keeps the launcher on A, and the
                // missile staying on A is right; that attempt tests nothing and is repeated.
                var now = gun.currentTarget as AbstractUnit;
                sd += $", aims {Vector2.Distance(m.targetPosition, Lead(m, b)):0.0} u from B's lead point, {Vector2.Distance(m.targetPosition, Lead(m, a)):0.0} u from A's (A and B {Vector2.Distance(a.transform.position, b.transform.position):0.0} u apart), launcher aims at {Name(now)}"
                    + (Alive(now) && now != b ? $" ({Vector2.Distance(m.targetPosition, Lead(m, now)):0.0} u from its lead point)" : "");
                if (now == a && !gun.CanWork(b)) { sd += " (B out of the launcher's reach, so the game kept it on A)"; continue; }
                judged = true;
                if (state != null && state.origin == a && m.target != null && Alive(now) && now != a && Closer(m, now, a)) ctx.Pass("switch", sd);
                else ctx.Fail("switch", sd);
            }
            if (!judged) ctx.Skip("switch", "no attempt got the launcher off A with both enemies and the missile alive: " + (sd ?? "fewer than two enemies left"));

            SetMode(g, RetargetMode.Off);
            string od = null;
            judged = false;
            for (int attempt = 1; attempt <= 4 && !judged; attempt++)
            {
                yield return EnsurePair(module, gun, enemies, a, b, p => (a, b) = p);
                if (!Alive(a) || !Alive(b)) break;
                yield return Aim(module, gun, a);
                var off = Fire(prefab, gun, fired);
                yield return HoldAlive(a, b, off.ignitionDelay + 0.3f);
                yield return Aim(module, gun, b);
                yield return HoldAlive(a, b, 5 * Time.fixedDeltaTime);
                od = $"attempt {attempt}: Fly on, switched from A to B: missile target {(off != null && off.target != null ? off.target.name : "none")}";
                if (!Flying(off) || !Alive(a) || !Alive(b)) continue;
                // As above: a launcher the game keeps on A (B out of its reach) never drops the missile's target.
                if (gun.currentTarget as AbstractUnit == a && !gun.CanWork(b)) { od += " (B out of the launcher's reach, so the game kept it on A)"; continue; }
                judged = true;
                if (off.target == null) ctx.Pass("off-switch", od);
                else ctx.Fail("off-switch", od);
            }
            if (!judged) ctx.Skip("off-switch", "no attempt got the launcher off A with both enemies and the missile alive: " + (od ?? "fewer than two enemies left"));

            // The fleet keeps fighting; take enemies that are alive now, and bring a few more first.
            int before = Enemies(module, enemies);
            bool spawned = before < 5 && SelfCheckContext.SpawnEnemies(6 - before);
            if (spawned)
            {
                float until = Time.realtimeSinceStartup + 30f;
                while (Enemies(module, enemies) < 3 && Time.realtimeSinceStartup < until) yield return new WaitForSecondsRealtime(0.25f);
            }
            if (enemies.Count < 3)
            {
                string few = $"fewer than three enemies: {before} before, spawned {spawned}, {enemies.Count} after";
                ctx.Skip("nearest-kill", few);
                ctx.Skip("nearest-click", few);
                yield break;
            }
            a = enemies[0];
            SetMode(g, RetargetMode.Nearest);
            yield return Aim(module, gun, a);
            var near = Fire(prefab, gun, fired);
            yield return new WaitForSeconds(near.ignitionDelay + 0.3f);
            var ns = near != null ? near.GetComponent<MissileState>() : null;
            if (Alive(a)) a.TakeDamage(new DamageData(ship) { power = 5e8f, hitTransform = a.transform, hitCoordinates = a.transform.position });
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Alive(a) && Time.realtimeSinceStartup < deadline) yield return null;
            // Your target died; nothing of yours is picked until you pick again.
            module.ResetCurrentTargets();
            // The pick is made once, at the first fixed step without a target; judge from that step's positions, since
            // the missile flies on and every later step moves the distances.
            int steps = 0;
            while (steps < 50 && Flying(near) && ns != null && ns.picked == null)
            {
                yield return new WaitForFixedUpdate();
                steps++;
            }
            if (!Flying(near) || ns == null || Alive(a) || Enemies(module, enemies) < 1)
            {
                string why = $"missile {(Flying(near) ? "flying" : "gone")}, A alive {Alive(a)}, {enemies.Count} other enemies";
                ctx.Skip("nearest-kill", why);
                ctx.Skip("nearest-click", why);
                yield break;
            }
            Vector2 at = near.transform.position;
            AbstractUnit nearest = null;
            foreach (var e in enemies)
                if (gun.CanTarget(e) && (nearest == null || Vector2.Distance(at, e.transform.position) < Vector2.Distance(at, nearest.transform.position))) nearest = e;
            var picked = ns.picked as AbstractUnit;
            float pickedDist = Alive(picked) ? Vector2.Distance(at, picked.transform.position) : -1f;
            float nearestDist = nearest != null ? Vector2.Distance(at, nearest.transform.position) : -1f;
            var scanner = ship.GetModule<ScannerModule>();
            string kd = $"A destroyed: picked {Name(picked)} at {pickedDist:0.0} u from the missile after {steps} steps, nearest enemy {Name(nearest)} at {nearestDist:0.0} u, missile target {(near.target != null ? near.target.name : "none")}";
            if (nearest != null && nearest != picked)
                kd += $"; nearest is {Vector2.Distance(ship.transform.position, nearest.transform.position):0.0} u from the ship (scanner {(scanner != null ? scanner.range : -1f):0.0}), fleet pick {module.TakesFleetPick()}, a candidate {MissilePatches.Candidate(module, nearest)}";
            // The pick comes from the last scan, so an enemy that moved since may be up to a little nearer.
            bool pickOk = Alive(picked) && module.IsValidTarget(picked) && near.target == picked.transform && pickedDist <= nearestDist + 2f;
            if (pickOk) ctx.Pass("nearest-kill", kd);
            else ctx.Fail("nearest-kill", kd);

            // An enemy inside the launcher's range: since 0.8.3.5 a launcher that can't reach the ship's target fires at
            // a nearer enemy instead (AbstractTurret.UpdateFallbackTarget), and its missiles follow that one. Fresh
            // enemies often wait outside that range, so when none is inside, the nearest other one is moved in.
            AbstractUnit other = null;
            foreach (var e in enemies)
                if (Alive(e) && e != picked && gun.CanWork(e)) { other = e; break; }
            string movedIn = "";
            if (pickOk && other == null)
                foreach (var e in enemies)
                {
                    if (!Alive(e) || e == picked || !gun.CanTarget(e)) continue;
                    Vector2 from = gun.transform.position;
                    Vector2 dir = ((Vector2)e.transform.position - from).normalized;
                    Vector2 to = from + (dir == Vector2.zero ? Vector2.right : dir) * gun.range * 0.6f;
                    e.transform.position = to;
                    if (e.rigidbody != null) e.rigidbody.position = to;
                    Physics2D.SyncTransforms();
                    if (gun.CanWork(e)) { other = e; movedIn = $" ({Name(e)} moved to {gun.range * 0.6f:0} u from the launcher)"; }
                    break;
                }
            if (!pickOk || other == null) { ctx.Skip("nearest-click", "no pick, or no second enemy the launcher can reach: " + kd); yield break; }
            module.SetManualTarget(other);
            // The launcher takes the new target at its own next aim step; the missile is judged once it has.
            float aimBy = Time.realtimeSinceStartup + 2f;
            while (gun.currentTarget != other && Time.realtimeSinceStartup < aimBy && Flying(near) && Alive(other)) yield return new WaitForFixedUpdate();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            if (!Flying(near) || !Alive(other) || !Alive(picked)) { ctx.Skip("nearest-click", "the missile or an enemy died during the check"); yield break; }
            string cd = $"pick {Name(ns.picked)} after you picked {Name(other)}{movedIn} (launcher aims at {Name(gun.currentTarget)}): aims {Vector2.Distance(near.targetPosition, Lead(near, other)):0.0} u from your target's lead point, {Vector2.Distance(near.targetPosition, Lead(near, picked)):0.0} u from the old pick's";
            if (ns.picked == null && Closer(near, other, picked)) ctx.Pass("nearest-click", cd);
            else ctx.Fail("nearest-click", cd);
        }
        finally
        {
            foreach (var m in fired)
                if (Flying(m)) m.Release();
            ShipSettings.Edit(g).missileRetarget = kept;
            ShipSettings.Save(g);
            ShipSettings.Invalidate();
            module.ResetCurrentTargets();
            if (heldManual != null && !heldManual.isDestroyed) module.SetManualTarget(heldManual);
            if (heldAuto != null && !heldAuto.isDestroyed) module.SetAutoTarget(heldAuto);
        }
    }

    private static void SkipLive(SelfCheckContext ctx, string reason)
    {
        foreach (var name in LiveChecks) ctx.Skip(name, reason);
    }

    private static void SetMode(string guid, RetargetMode mode)
    {
        ShipSettings.Edit(guid).missileRetarget = mode;
        ShipSettings.Save(guid);
        ShipSettings.Invalidate();
    }

    // The launcher aims at the target once its tracking point sits on it.
    private static IEnumerator Aim(CombatModule module, AbstractTurret gun, AbstractUnit target)
    {
        module.ResetCurrentTargets();
        // The fleet keeps fighting while the check runs; an enemy that died meanwhile leaves the launcher without a target.
        if (!Alive(target)) yield break;
        module.SetManualTarget(target);
        float until = Time.realtimeSinceStartup + 3f;
        while (Alive(target) && (gun.trackingTarget == null || gun.trackingTarget.transform.parent != target.transform) && Time.realtimeSinceStartup < until)
            yield return null;
    }

    // Fired the way a launcher fires (MissileTurret.Fire): from the game's projectile pool, which puts it in flight.
    // No damage, which also tells the check's missiles from the ones the pool hands out again after they land.
    private static Missile Fire(Missile prefab, AbstractTurret gun, List<Missile> fired)
    {
        var m = ProjectilePool.Spawn(prefab, gun.transform.position, gun.transform.rotation);
        m.Initialize(gun.trackingTarget != null ? gun.trackingTarget.transform : null, new DamageData(gun) { power = 0f }, 6f, false, Vector2.zero);
        fired.Add(m);
        return m;
    }

    private static bool Flying(Missile m) => m != null && m.isFlying && m.damageData != null && m.damageData.power == 0f;

    // A missile aims ahead of its target: where the target will be when the missile gets there at full speed
    // (Missile.UpdateTrackingTarget). Comparing against those points, not the targets' positions, keeps a fast enemy's
    // lead from deciding which one the missile is after.
    private static Vector2 Lead(Missile m, AbstractUnit t) =>
        (Vector2)t.transform.position + t.targetableVelocity * (Vector2.Distance(m.transform.position, t.transform.position) / m.maxSpeed);

    private static bool Closer(Missile m, AbstractUnit to, AbstractUnit than) =>
        Vector2.Distance(m.targetPosition, Lead(m, to)) < Vector2.Distance(m.targetPosition, Lead(m, than));

    private static bool Alive(AbstractUnit u) => u != null && !u.isDestroyed;

    // Keeps the two test enemies' hull full for this long (fixed steps), so the fleet's fire can't end an attempt.
    private static IEnumerator HoldAlive(AbstractUnit a, AbstractUnit b, float seconds)
    {
        float until = Time.time + seconds;
        do
        {
            if (Alive(a)) a.currentHullHP = a.maxHullHP;
            if (Alive(b)) b.currentHullHP = b.maxHullHP;
            yield return new WaitForFixedUpdate();
        } while (Time.time < until);
    }

    // The current pair when both live; else a fresh pair from the enemies here, calling in more when fewer than two are left.
    private static IEnumerator EnsurePair(CombatModule module, AbstractTurret gun, List<AbstractUnit> enemies, AbstractUnit a, AbstractUnit b, Action<(AbstractUnit, AbstractUnit)> set)
    {
        if (Alive(a) && Alive(b)) yield break;
        if (Enemies(module, enemies) < 2 && SelfCheckContext.SpawnEnemies(4))
        {
            float until = Time.realtimeSinceStartup + 30f;
            while (Enemies(module, enemies) < 2 && Time.realtimeSinceStartup < until) yield return new WaitForSecondsRealtime(0.25f);
        }
        if (Enemies(module, enemies) >= 2) set(Pair(gun, enemies));
    }

    // The first two enemies inside the launcher's range, else the first two. Since 0.8.3.5 a launcher fires at an enemy
    // it can reach instead of a target out of its range (AbstractTurret.UpdateFallbackTarget), so aiming it at one that
    // far away doesn't switch it.
    private static (AbstractUnit, AbstractUnit) Pair(AbstractTurret gun, List<AbstractUnit> enemies)
    {
        AbstractUnit first = null;
        foreach (var e in enemies)
        {
            if (!Alive(e) || !gun.CanWork(e)) continue;
            if (first == null) first = e;
            else return (first, e);
        }
        return (enemies[0], enemies[1]);
    }

    private static int Enemies(CombatModule module, List<AbstractUnit> into)
    {
        into.Clear();
        foreach (var u in Object.FindObjectsByType<AbstractUnit>(FindObjectsInactive.Exclude))
            if (u != null && !u.isDestroyed && module.IsValidTarget(u)) into.Add(u);
        Vector2 at = module.transform.position;
        into.Sort((x, y) => Vector2.Distance(at, x.transform.position).CompareTo(Vector2.Distance(at, y.transform.position)));
        return into.Count;
    }

    // A plain missile from any launcher the game knows; some items throw when asked for components, as the missile dump found.
    private static Missile MissilePrefab()
    {
        foreach (var item in Behaviour.Item.InventoryItemType.all)
        {
            Behaviour.Equipment.Turret.CombatTurrets.MissileTurret turret;
            try
            {
                if (item == null || !item.TryGetComponent(out turret) || turret.missilePrefab == null) continue;
            }
            catch (Exception) { continue; }
            if (turret.missilePrefab.GetType() == typeof(Missile)) return turret.missilePrefab;
        }
        return null;
    }

    private static string Name(TargetableUnit t) => t == null ? "none" : t.targetName + "#" + t.GetHashCode();
}

// What the feature knows about one of your missiles: the unit it was fired at, and the enemy "Nearest enemy" sent
// it to after that unit died. The game reuses missiles (ProjectilePool), so one component lives on each missile
// object and is cleared when the missile ends its flight.
public sealed class MissileState : MonoBehaviour
{
    // Missiles steering to a pick of their own; lets the per-step prefix skip GetComponent when none are.
    public static int Picks;
    public TargetableUnit origin;
    public TargetableUnit picked;
    private bool counted;

    public void Pick(TargetableUnit unit)
    {
        picked = unit;
        if (counted == (unit != null)) return;
        counted = unit != null;
        Picks += counted ? 1 : -1;
    }

    public void Clear()
    {
        origin = null;
        Pick(null);
    }

    private void OnDestroy()
    {
        if (counted) Picks--;
    }
}

// A missile homes on its launcher's current target for as long as it holds a target at all (Missile.UpdateTrackingTarget
// reads the launcher each step). It loses its hold whenever the launcher's target changes, because the game recreates
// the launcher's tracking point and the missile was aimed at the old one, and also when its enemy dies. So the
// feature remembers which unit each missile was fired at:
// - that unit still alive: the launcher switched targets, and the missile follows the new one in every mode but Off;
// - that unit destroyed: Weapon's target follows the launcher's next target, and Nearest enemy picks the enemy
//   nearest the missile, until you give the launcher a target yourself (a click, or an attack order to a fleet ship).
// Off leaves every missile to the game.
[HarmonyPatch]
internal static class MissilePatches
{
    // The scanner's last list per module of your ships: CombatModule.filteredTargets is empty while the module holds a
    // target, so a missile whose target just died found nothing there.
    private static readonly Dictionary<CombatModule, List<TargetableUnit>> lastScan = new();
    private static readonly List<AbstractUnit> roster = new();

    internal static void Reset() => lastScan.Clear();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatModule), nameof(CombatModule.UpdateAvailableTargets))]
    private static void ScanPostfix(CombatModule __instance, IEnumerable<TargetableUnit> targets)
    {
        var feature = MissileRetarget.Instance;
        if (!feature.Active) return;
        try
        {
            if (!(targets is List<TargetableUnit> list) || __instance.parent == null || !Fleet.IsPlayerShip(__instance.parent)) return;
            lastScan[__instance] = list;
        }
        catch (Exception e) { feature.Fail(e, nameof(MissilePatches)); }
    }

    // The missile is aimed at the launcher's tracking point, a child of the unit the launcher is aiming at. A reused
    // missile keeps its component from the last flight, which ended with Clear.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Missile), nameof(Missile.Initialize), new[] { typeof(Transform), typeof(DamageData), typeof(float), typeof(bool), typeof(Vector2) })]
    private static void InitializePostfix(Missile __instance, Transform target)
    {
        var feature = MissileRetarget.Instance;
        if (!feature.Active) return;
        try
        {
            var turret = __instance.parentTurret;
            if (turret == null || !Fleet.IsPlayerShip(turret.parent)) return;
            var parent = target != null ? target.parent : null;
            var state = __instance.GetComponent<MissileState>();
            if (state == null) state = __instance.gameObject.AddComponent<MissileState>();
            state.origin = parent != null ? parent.GetComponent<TargetableUnit>() : null;
        }
        catch (Exception e) { feature.Fail(e, nameof(MissilePatches)); }
    }

    // The flight ended (hit, lifetime over); the pool may hand the missile to any launcher next, an enemy's too.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Missile), nameof(Missile.OnReleased))]
    private static void OnReleasedPostfix(Missile __instance)
    {
        try
        {
            var state = __instance.GetComponent<MissileState>();
            if (state != null) state.Clear();
        }
        catch (Exception e) { MissileRetarget.Instance.Fail(e, nameof(MissilePatches)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Missile), "FixedUpdate")]
    private static void FixedUpdatePostfix(Missile __instance)
    {
        var feature = MissileRetarget.Instance;
        if (!feature.Active) return;
        try
        {
            if (!__instance.startTracking || __instance.target != null) return;
            var turret = __instance.parentTurret;
            if (turret == null || !Fleet.IsPlayerShip(turret.parent) || !(turret.parent.unitData is SpaceShipData data)) return;
            var mode = feature.Resolve(data);
            if (mode == RetargetMode.Off) return;
            var state = __instance.GetComponent<MissileState>();
            if (state == null) return;

            var current = turret.currentTarget;
            bool hasCurrent = current != null && !current.isDestroyed;
            bool originAlive = state.origin != null && !state.origin.isDestroyed;
            var module = turret.targetingModule as CombatModule;
            if (mode == RetargetMode.Nearest && !originAlive && module != null && !TargetPriorityPatches.ManualWins(module))
            {
                var best = NearestEnemy(module, turret, __instance.transform.position);
                if (best != null)
                {
                    __instance.target = best.transform;
                    state.Pick(best);
                    return;
                }
            }
            if (hasCurrent) __instance.target = current.transform;
        }
        catch (Exception e) { feature.Fail(e, nameof(MissilePatches)); }
    }

    // The enemy nearest the missile that the launcher could hit: the module's own enemy test, so allies, neutrals,
    // wrecks and your own ships never count. A ship that takes fleet picks skips the scan above
    // (ScannerModule.UpdateCombatFromFleet), so its candidates are the location's enemies.
    internal static TargetableUnit NearestEnemy(CombatModule module, Behaviour.Equipment.Turret.AbstractTurret turret, Vector3 pos)
    {
        TargetableUnit best = null;
        float bestSqr = float.MaxValue;
        if (module.TakesFleetPick())
        {
            roster.Clear();
            PoiFleets.For(module.parent)?.GetEnemiesOf(module.parent, roster);
            for (int i = 0; i < roster.Count; i++) Closest(module, turret, pos, roster[i], ref best, ref bestSqr);
            roster.Clear();
            return best;
        }
        if (!lastScan.TryGetValue(module, out var list)) return null;
        for (int i = 0; i < list.Count; i++) Closest(module, turret, pos, list[i], ref best, ref bestSqr);
        return best;
    }

    // Whether the unit is among the enemies NearestEnemy looks at for this module (for the self-check's report).
    internal static bool Candidate(CombatModule module, TargetableUnit unit)
    {
        if (module.TakesFleetPick())
        {
            roster.Clear();
            PoiFleets.For(module.parent)?.GetEnemiesOf(module.parent, roster);
            bool found = roster.Contains(unit as AbstractUnit);
            roster.Clear();
            return found;
        }
        return lastScan.TryGetValue(module, out var list) && list.Contains(unit);
    }

    private static void Closest(CombatModule module, Behaviour.Equipment.Turret.AbstractTurret turret, Vector3 pos, TargetableUnit t, ref TargetableUnit best, ref float bestSqr)
    {
        if (t == null || t.isDestroyed || t == turret.parent || !module.IsValidTarget(t) || !turret.CanTarget(t)) return;
        float sqr = (t.transform.position - pos).sqrMagnitude;
        if (sqr < bestSqr)
        {
            bestSqr = sqr;
            best = t;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Missile), "UpdateTrackingTarget")]
    private static bool UpdateTrackingTargetPrefix(Missile __instance)
    {
        if (MissileState.Picks <= 0) return true;
        var feature = MissileRetarget.Instance;
        if (!feature.Active) return true;
        try
        {
            var state = __instance.GetComponent<MissileState>();
            if (state == null || state.picked == null) return true;
            var module = __instance.parentTurret != null ? __instance.parentTurret.targetingModule : null;
            // A target you gave the launcher since the pick wins; so does the pick's death.
            if (state.picked.isDestroyed || module != null && TargetPriorityPatches.ManualWins(module))
            {
                state.Pick(null);
                var current = __instance.parentTurret != null ? __instance.parentTurret.currentTarget : null;
                if (current != null && !current.isDestroyed) __instance.target = current.transform;
                return true;
            }
            Vector2 pos = __instance.transform.position;
            Vector2 targetPos = state.picked.transform.position;
            float dt = Vector2.Distance(pos, targetPos) / __instance.maxSpeed;
            __instance.targetPosition = targetPos + state.picked.targetableVelocity * dt + __instance.GetSwarmOffset();
            return false;
        }
        catch (Exception e)
        {
            feature.Fail(e, nameof(MissilePatches));
            return true;
        }
    }
}
