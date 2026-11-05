using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Unit;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using Source.Player;
using Source.SpaceShip;
using UnityEngine;

namespace IgiQoL.Features.Separation;

// In combat the game steers each ship straight at a ring around its target and switches its obstacle-avoiding
// navigator off, so ships stack on top of each other. This nudges a fighting ship's destination away from any ship
// closer than both hulls plus a gap: your ships by their own setting, enemy and allied NPC ships with OtherShips.
public sealed class Separation : Feature
{
    public static Separation Instance;

    public Separation() { Instance = this; }

    public override string Id => "Separation";
    public override string Title => "Keep Distance in Combat";
    public override string Description => "In a fight the game flies every ship straight at its target and turns its collision avoidance off, so ships pile on top of each other. With this on, your ships keep a gap from any ship near them while they fight, and with \"Other ships keep distance too\" enemy and allied ships do the same. A ship never keeps distance from the ship it is attacking. It only works while a ship is chasing its target, not for ships the game flies in formation (Escort, Hold Fire, a Hold or Move order, Defensive until hit). Set it per ship of yours with the last button on its row in the fleet panel.";
    public override Type[] PatchClasses => new[] { typeof(SeparationPatches) };

    public override void OnActivated()
    {
        GameEvents.SaveLoaded += SeparationPatches.Reset;
        GameEvents.MainMenuShown += SeparationPatches.Reset;
    }

    public ConfigEntry<bool> Default, OtherShips;
    public ConfigEntry<float> Gap;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        Default = cfg.Bind(Id, "Default", false, SettingInfo.Describe("Keep distance from other ships",
            "When on, every ship of yours keeps a gap from other ships (yours and enemies) while it fights, instead of parking on top of them. A ship never keeps distance from the ship it is attacking. Each ship can override this in its own settings (the last button on its row in the fleet panel)."));
        Gap = cfg.Bind(Id, "Gap", 2f, SettingInfo.Describe("Gap between hulls (units)",
            "How much empty space a ship that keeps distance tries to leave between its hull and another ship's hull in combat.",
            new AcceptableValueRange<float>(0.5f, 10f)));
        OtherShips = cfg.Bind(Id, "OtherShips", false, SettingInfo.Describe("Other ships keep distance too",
            "Enemy and allied ships that aren't yours keep the same gap from every ship near them while they fight, so a big battle doesn't end up as one heap of hulls on top of your fleet. This changes how enemies move, so it's off by default."));
    }

    public bool IsOn(SpaceShipData ship) => ShipSettings.Get(ship.guid).separation ?? Default.Value;

    // Your ships follow their own setting; everyone else's follow OtherShips.
    public bool IsOn(SpaceShip ship) => Fleet.IsPlayerShip(ship) ? IsOn(ship.spaceShipData) : OtherShips.Value && !ship.isSalvage;

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        var flagship = Fleet.Flagship;
        if (flagship == null) { ctx.Skip("settings-roundtrip", "not in space"); yield break; }
        var g = flagship.spaceShipData.guid;
        var before = ShipSettings.Get(g).separation;
        ctx.Cleanup(() =>
        {
            ShipSettings.Edit(g).separation = before;
            ShipSettings.Save(g);
        });
        var e = ShipSettings.Edit(g);
        e.separation = true;
        ShipSettings.Save(g);
        ShipSettings.Invalidate();
        var record = SaveData.Ship(g, false);
        string detail = $"separation={ShipSettings.Get(g).separation} saved={(record != null && record["separation"].IsBoolean ? record["separation"].AsBoolean.ToString() : "none")}";
        if (ShipSettings.Get(g).separation == true && record != null && record["separation"].IsBoolean && record["separation"].AsBoolean) ctx.Pass("settings-roundtrip", detail);
        else ctx.Fail("settings-roundtrip", detail);

        // Push direction: a fleet ship placed half a hull from the flagship is pushed straight away from it. Any other
        // ship near the pair adds a push of its own, so the check picks a side where none is close and says which ships
        // crowd it otherwise.
        SpaceShip wing = null;
        foreach (var d in GamePlayer.current.activeFleet)
            if (d != null && d.unit is SpaceShip s && s != flagship && s.rigidbody != null) { wing = s; break; }
        if (wing == null)
        {
            foreach (var n in new[] { "push", "apart", "live", "other-ships" }) ctx.Skip(n, "no fleet ship in space");
            yield break;
        }
        var we = ShipSettings.Edit(wing.spaceShipData.guid);
        var wingBefore = we.separation;
        Vector2 home = flagship.rigidbody.position;
        Vector2 pushDir = Vector2.zero;
        string crowd = "";
        foreach (var dir in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
        {
            crowd = SeparationPatches.Nearby(wing, flagship, home + dir * 0.5f, Gap.Value);
            if (crowd.Length == 0) { pushDir = dir; break; }
        }
        if (pushDir == Vector2.zero) ctx.Skip("push", "other ships crowd the flagship: " + crowd);
        else
        {
            Vector2 start = home + pushDir * 0.5f;
            wing.transform.position = start;
            wing.rigidbody.position = start;
            Physics2D.SyncTransforms();
            var push = SeparationPatches.ComputePush(wing, Gap.Value);
            float along = Vector2.Dot(push, pushDir), across = Mathf.Abs(Vector2.Dot(push, new Vector2(-pushDir.y, pushDir.x)));
            string pushDetail = $"push=({push.x:0.00},{push.y:0.00}) wing at +0.5u {pushDir} of the flagship, along {along:0.00} across {across:0.00}, other ships near: {SeparationPatches.NearbyText(wing, flagship, start, Gap.Value)}";
            if (along > 0.5f && across < along * 0.5f) ctx.Pass("push", pushDetail);
            else ctx.Fail("push", pushDetail);
        }

        // Apart: lined up side by side with a gap between the hulls, the bounding circles still overlap but the
        // outlines don't, so there is no push.
        wing.rigidbody.rotation = flagship.rigidbody.rotation;
        wing.transform.rotation = flagship.transform.rotation;
        float side = flagship.height + wing.height + Gap.Value + 1f;
        Vector2 up = flagship.transform.up;
        Vector2 beside = home + up * side;
        string apartCrowd = SeparationPatches.Nearby(wing, flagship, beside, Gap.Value);
        if (apartCrowd.Length > 0)
        {
            beside = home - up * side;
            string otherSide = SeparationPatches.Nearby(wing, flagship, beside, Gap.Value);
            if (otherSide.Length > 0) apartCrowd = apartCrowd + " / " + otherSide;
            else apartCrowd = "";
        }
        if (apartCrowd.Length > 0) ctx.Skip("apart", "other ships crowd the flagship's sides: " + apartCrowd);
        else
        {
            wing.transform.position = beside;
            wing.rigidbody.position = beside;
            Physics2D.SyncTransforms();
            var hullGap = flagship.surfaceCollider.Distance(wing.surfaceCollider);
            float circles = Mathf.Max(flagship.radius, flagship.height) + Mathf.Max(wing.radius, wing.height) + Gap.Value;
            var apart = SeparationPatches.ComputePush(wing, Gap.Value);
            string apartDetail = $"centres {side:0.0} apart (circles reach {circles:0.0}), hull gap {hullGap.distance:0.00}, push=({apart.x:0.00},{apart.y:0.00}), other ships near: {SeparationPatches.NearbyText(wing, flagship, beside, Gap.Value)}";
            if (!hullGap.isValid || hullGap.distance < Gap.Value || side >= circles) ctx.Skip("apart", "couldn't line the hulls up: " + apartDetail);
            else if (apart == Vector2.zero) ctx.Pass("apart", apartDetail);
            else ctx.Fail("apart", apartDetail);

            // Settle: a hull 0.1 inside the gap is the end of a push, not a collision. The game counts a ship as arrived
            // only at the pushed point, so following it would keep a ship on its ring turning forever.
            if (hullGap.isValid && hullGap.distance >= Gap.Value && apart == Vector2.zero)
            {
                Vector2 inward = (home - beside).normalized;
                Vector2 near = beside + inward * (hullGap.distance - (Gap.Value - 0.1f));
                wing.transform.position = near;
                wing.rigidbody.position = near;
                Physics2D.SyncTransforms();
                var close = flagship.surfaceCollider.Distance(wing.surfaceCollider);
                var raw = SeparationPatches.ComputePush(wing, Gap.Value);
                var settled = SeparationPatches.Settle(raw);
                string settleDetail = $"hull gap {close.distance:0.00} of {Gap.Value:0.0}, raw push {raw.magnitude:0.00}, applied {settled.magnitude:0.00} (ignored below {SeparationPatches.MinPush:0.0}), other ships near: {SeparationPatches.NearbyText(wing, flagship, near, Gap.Value)}";
                if (!close.isValid || close.distance >= Gap.Value || close.distance < Gap.Value - 0.25f) ctx.Skip("settle", "couldn't put the hulls 0.1 inside the gap: " + settleDetail);
                else if (raw.sqrMagnitude > 0f && settled == Vector2.zero) ctx.Pass("settle", settleDetail);
                else ctx.Fail("settle", settleDetail);
            }
            else ctx.Skip("settle", "no clean side-by-side placement: " + apartDetail);
        }

        // Live: in the game's own physics step, the fleet ship's destination is its normal one plus the push.
        var targetModule = wing.targetProvider;
        var enemy = NearestEnemy(wing);
        if (enemy == null && SelfCheckContext.SpawnEnemies(1))
        {
            // The fleet may have killed the earlier checks' enemies by now.
            float until = Time.realtimeSinceStartup + 30f;
            while ((enemy = NearestEnemy(wing)) == null && Time.realtimeSinceStartup < until) yield return new WaitForSeconds(1f);
        }
        if (enemy == null || targetModule == null)
        {
            ctx.Skip("live", "no enemy for the fleet ship to fight");
            ctx.Skip("other-ships", "no enemy for the fleet ship to fight");
            yield break;
        }
        var route = new List<Source.Galaxy.MapPointOfInterest>(GamePlayer.current.waypoints);
        GamePlayer.current.waypoints.Clear();
        we.separation = true;
        string liveDetail;
        bool applied;
        try
        {
            targetModule.SetManualTarget(enemy);
            // The flagship may have moved toward its own target during the wait for enemies: place the wing now.
            Vector2 start = flagship.rigidbody.position + Vector2.right * 0.5f;
            SeparationPatches.Reset();
            wing.transform.position = start;
            wing.rigidbody.position = start;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            SeparationPatches.TryGetState(wing, out var st);
            // Steered at the pushed point during ApplyThrust, with the game's own destination restored afterwards.
            applied = st != null && st.push.sqrMagnitude > 0.01f && (st.applied - (st.baseDestination + st.push)).sqrMagnitude < 0.0001f
                && (wing.currentDestination - wing.ClampDestination(st.baseDestination)).sqrMagnitude < 0.0001f;
            liveDetail = st == null
                ? $"no state (override={wing.overrideTarget.HasValue} hold={wing.holdPosition} target={(targetModule.priorityTarget != null)})"
                : $"{wing.spaceShipData.GetShipName()}: push=({st.push.x:0.0},{st.push.y:0.0}) steered at {st.applied}, game destination {wing.currentDestination} kept";
        }
        finally
        {
            targetModule.ResetCurrentTargets();
            we.separation = wingBefore;
            GamePlayer.current.waypoints.AddRange(route);
        }
        if (applied) ctx.Pass("live", liveDetail);
        else ctx.Fail("live", liveDetail);

        // Own target: the game parks the ship on a ring around its target, so that target must not push it off the ring.
        // Wing placed half a unit from the enemy it is fighting, on a side where nothing else crowds it: the old
        // code pushed it straight away from the enemy, the new code leaves it alone.
        if (enemy == null) ctx.Skip("own-target", "the enemy died before the check");
        else
        {
            Vector2 enemyAt = enemy.transform.position;
            Vector2 spot = Vector2.zero;
            string ownCrowd = "";
            foreach (var dir in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
            {
                ownCrowd = SeparationPatches.Nearby(wing, enemy, enemyAt + dir * 0.5f, Gap.Value);
                if (ownCrowd.Length == 0) { spot = enemyAt + dir * 0.5f; break; }
            }
            if (spot == Vector2.zero) ctx.Skip("own-target", "other ships crowd the enemy: " + ownCrowd);
            else
            {
                var routeBefore = new List<Source.Galaxy.MapPointOfInterest>(GamePlayer.current.waypoints);
                GamePlayer.current.waypoints.Clear();
                try
                {
                    targetModule.SetManualTarget(enemy);
                    wing.transform.position = spot;
                    wing.rigidbody.position = spot;
                    Physics2D.SyncTransforms();
                    bool isTarget = wing.targetProvider != null && wing.targetProvider.priorityTarget == enemy;
                    var skipped = SeparationPatches.ComputePush(wing, Gap.Value);
                    var unskipped = SeparationPatches.ComputePush(wing, Gap.Value, false);
                    string ownDetail = $"{wing.spaceShipData.GetShipName()} 0.5 u from its target {enemy.name}: push with the target left out {skipped.magnitude:0.00}, counting it {unskipped.magnitude:0.00}, target is its priority target {isTarget}";
                    if (!isTarget) ctx.Skip("own-target", "the ship's target provider doesn't hold the enemy: " + ownDetail);
                    else if (skipped == Vector2.zero && unskipped.magnitude > 0.5f) ctx.Pass("own-target", ownDetail);
                    else ctx.Fail("own-target", ownDetail);
                }
                finally
                {
                    targetModule.ResetCurrentTargets();
                    GamePlayer.current.waypoints.AddRange(routeBefore);
                }
            }
        }

        // Other ships: an enemy dropped onto the flagship gets a push of its own only with OtherShips on.
        var other = enemy as SpaceShip;
        float wait = Time.realtimeSinceStartup + 20f;
        while (other != null && (other.targetProvider == null || other.targetProvider.priorityTarget == null) && Time.realtimeSinceStartup < wait) yield return new WaitForSeconds(0.5f);
        if (other == null || other.rigidbody == null || other.targetProvider == null || other.targetProvider.priorityTarget == null || other.overrideTarget.HasValue || other.holdPosition)
        {
            ctx.Skip("other-ships", "no enemy ship with a target of its own");
            yield break;
        }
        bool otherBefore = OtherShips.Value;
        ctx.Cleanup(() => OtherShips.Value = otherBefore);
        OtherShips.Value = false;
        bool offOk = !IsOn(other) && IsOn(flagship);
        Vector2 onTop = flagship.rigidbody.position + Vector2.up * 0.5f;
        other.transform.position = onTop;
        other.rigidbody.position = onTop;
        for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
        offOk &= !SeparationPatches.TryGetState(other, out _);
        OtherShips.Value = true;
        bool onOk = IsOn(other) && IsOn(flagship);
        other.transform.position = onTop;
        other.rigidbody.position = onTop;
        for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
        SeparationPatches.TryGetState(other, out var os);
        onOk &= os != null && os.push.sqrMagnitude > 0.01f && (other.currentDestination - os.baseDestination).sqrMagnitude < 0.0001f;
        OtherShips.Value = otherBefore;
        string otherDetail = $"{other.name}: off -> no push {offOk}, on -> push {(os != null ? $"({os.push.x:0.0},{os.push.y:0.0})" : "none")}, game destination kept {onOk}";
        if (offOk && onOk) ctx.Pass("other-ships", otherDetail);
        else ctx.Fail("other-ships", otherDetail);
    }

    private static AbstractUnit NearestEnemy(SpaceShip ship)
    {
        AbstractUnit enemy = null;
        var module = ship.targetProvider;
        if (module == null) return null;
        float bestDist = float.MaxValue;
        foreach (var u in UnityEngine.Object.FindObjectsByType<AbstractUnit>(FindObjectsInactive.Exclude))
        {
            if (u == null || !module.IsValidTarget(u)) continue;
            float d = Vector2.Distance(u.transform.position, ship.transform.position);
            if (d < bestDist)
            {
                enemy = u;
                bestDist = d;
            }
        }
        return enemy;
    }
}

internal sealed class SeparationState
{
    public Vector2 baseDestination, applied, push;
    public float next;
}

[HarmonyPatch]
internal static class SeparationPatches
{
    private const float Refresh = 0.25f;

    private static readonly Dictionary<AbstractUnit, SeparationState> states = new();
    private static readonly List<AbstractUnit> dead = new();

    // Sum of "move away" vectors (centre to centre) from every ship whose hull outline is closer than the gap, each as
    // long as the gap still missing, capped at one hull diameter plus the gap. The bounding circles only pre-filter:
    // they are the hull's half-length, so long hulls side by side would count as overlapping when they aren't. A first
    // version pushed on circle overlap and kept nudging ships that weren't touching anyone. The ship's own target is
    // left out: the game parks the ship on a ring around it, and pushing off it too would make the two fight over the
    // ring (a close Fixed distance puts the ring inside a big hull). skipTarget is false only in the self-check.
    internal static Vector2 ComputePush(AbstractUnit unit, float gap, bool skipTarget = true)
    {
        Vector2 p = unit.rigidbody.position;
        float r = Mathf.Max(unit.radius, unit.height, 0.5f);
        var own = unit.surfaceCollider;
        var target = skipTarget && unit.targetProvider != null ? unit.targetProvider.priorityTarget : null;
        Vector2 push = Vector2.zero;
        foreach (var nav in ShipNavigator.navigators)
        {
            if (nav == null || nav.unit == null || nav.unit == unit || nav.rb == null || !nav.isActiveAndEnabled) continue;
            if (target != null && nav.unit == target) continue;
            if (nav.unit.travelling || nav.unit.isCarrierFighter) continue;
            float reach = r + nav.ownRadius + gap;
            Vector2 away = p - nav.rb.position;
            float d2 = away.sqrMagnitude;
            if (d2 >= reach * reach) continue;
            float d = Mathf.Sqrt(d2);
            float overlap = reach - d;
            if (own != null && nav.unit.surfaceCollider != null)
            {
                var cd = own.Distance(nav.unit.surfaceCollider);
                if (cd.isValid)
                {
                    if (cd.distance >= gap) continue;
                    overlap = gap - cd.distance;
                }
            }
            // Exactly on top of each other: split by instance id so the two ships go opposite ways.
            if (d < 0.01f)
            {
                away = unit.GetHashCode() < nav.unit.GetHashCode() ? Vector2.up : Vector2.down;
                d = 1f;
            }
            push += away / d * overlap;
        }
        float cap = 2f * r + gap;
        return push.sqrMagnitude > cap * cap ? push.normalized * cap : push;
    }

    // A push this short is the last bit of a gap that is almost met. Following it keeps a ship that has reached its
    // ring turning and thrusting sideways (the game only counts it as arrived once it is at the pushed point), so the
    // ships of a clump never settle. Ignoring it leaves them a few tenths of a unit inside the gap, which nobody sees.
    internal const float MinPush = 0.3f;

    internal static Vector2 Settle(Vector2 push) => push.sqrMagnitude < MinPush * MinPush ? Vector2.zero : push;

    // Self-check only: the ships other than the unit and its partner whose circles reach this spot, "" when none.
    // They push as well, so a check that expects only the partner's push can't run there.
    internal static string Nearby(AbstractUnit unit, AbstractUnit partner, Vector2 at, float gap)
    {
        var sb = new System.Text.StringBuilder();
        float r = Mathf.Max(unit.radius, unit.height, 0.5f);
        foreach (var nav in ShipNavigator.navigators)
        {
            if (nav == null || nav.unit == null || nav.unit == unit || nav.unit == partner || nav.rb == null || !nav.isActiveAndEnabled) continue;
            if (nav.unit.travelling || nav.unit.isCarrierFighter) continue;
            float d = Vector2.Distance(at, nav.rb.position);
            if (d >= r + nav.ownRadius + gap) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(nav.unit.name).Append(' ').Append(d.ToString("0.0")).Append(" u");
        }
        return sb.ToString();
    }

    internal static string NearbyText(AbstractUnit unit, AbstractUnit partner, Vector2 at, float gap)
    {
        string s = Nearby(unit, partner, at, gap);
        return s.Length == 0 ? "none" : s;
    }

    // ApplyThrust is where the game steers toward currentDestination each physics step. The push is added for that
    // call only and the game's own destination is put back afterwards, so nothing the game stores is shifted.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AbstractUnit), "ApplyThrust")]
    private static void ApplyThrustPrefix(AbstractUnit __instance, out Vector2? __state)
    {
        __state = null;
        var feature = Separation.Instance;
        if (!feature.Active) return;
        try
        {
            if (!(__instance is SpaceShip ship) || ship.isCarrierFighter) return;
            var module = ship.targetProvider;
            if (!feature.IsOn(ship) || ship.travelling || ship.holdPosition || ship.overrideTarget.HasValue || module == null
                || module.priorityTarget == null)
            {
                if (states.Count > 0) states.Remove(ship);
                return;
            }
            if (!states.TryGetValue(ship, out var s))
            {
                s = new SeparationState();
                states[ship] = s;
                if (states.Count > 32) Prune();
            }
            float now = Time.time;
            if (now >= s.next)
            {
                s.push = Settle(ComputePush(ship, feature.Gap.Value));
                s.next = now + Refresh;
            }
            if (s.push == Vector2.zero) return;
            __state = ship.currentDestination;
            s.baseDestination = ship.currentDestination;
            s.applied = s.baseDestination + s.push;
            ship.currentDestination = s.applied;
        }
        catch (Exception e) { feature.Fail(e, nameof(SeparationPatches)); }
    }

    // The game clamps currentDestination to the map at the top of ApplyThrust, so the pushed point was clamped and the
    // stored one must be the clamped game destination, as if there had been no push.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AbstractUnit), "ApplyThrust")]
    private static void ApplyThrustPostfix(AbstractUnit __instance, Vector2? __state)
    {
        if (__state.HasValue) __instance.currentDestination = __instance.ClampDestination(__state.Value);
    }

    internal static bool TryGetState(AbstractUnit unit, out SeparationState state) => states.TryGetValue(unit, out state);

    // Keys are Unity objects that die with the save; drop them on load and at the main menu.
    internal static void Reset() => states.Clear();

    private static void Prune()
    {
        dead.Clear();
        foreach (var kv in states)
            if (kv.Key == null) dead.Add(kv.Key);
        foreach (var k in dead) states.Remove(k);
    }
}
