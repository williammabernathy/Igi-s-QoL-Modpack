using System;
using System.Collections;
using Behaviour.Equipment.Module;
using Behaviour.Unit;
using Behaviour.Equipment.Turret;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using Source.Player;
using Source.SpaceShip;
using UnityEngine;

namespace IgiQoL.Features.EngagementDistance;

public sealed class EngagementDistance : Feature
{
    public static EngagementDistance Instance;

    public EngagementDistance() { Instance = this; }

    public override string Id => "EngagementDistance";
    public override string Title => "Engagement distance";
    public override string Description => "Lets each of your ships keep its own distance from its target in combat. Set it per ship with the last button on the ship's row in the fleet panel.";
    public override Type[] PatchClasses => new[] { typeof(ApproachPatch) };

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        var flagship = GamePlayer.current.currentSpaceShip;
        var g = flagship.guid;
        var before = ShipSettings.Get(g);
        var kept = (before.engagement, before.engagementDistance);
        ctx.Cleanup(() =>
        {
            var c = ShipSettings.Edit(g);
            (c.engagement, c.engagementDistance) = kept;
            ShipSettings.Save(g);
            ShipSettings.Invalidate();
        });

        var e = ShipSettings.Edit(g);
        e.engagement = EngagementMode.Fixed;
        e.engagementDistance = 12f;
        ShipSettings.Save(g);
        ShipSettings.Invalidate();
        var r = ShipSettings.Get(g);
        var record = SaveData.Ship(g, false);
        string saved = record == null ? null : (record["engagement"].IsString ? record["engagement"].AsString : null);
        string detail = $"engagement={r.engagement} distance={r.engagementDistance} saved={saved ?? "none"}";
        if (r.engagement == EngagementMode.Fixed && r.engagementDistance == 12f && saved == "Fixed") ctx.Pass("settings-roundtrip", detail);
        else ctx.Fail("settings-roundtrip", detail);

        // Needs the flagship in space and an enemy to target: undock first, and fetch an enemy if none is about.
        yield return SelfCheckContext.EnsureInSpace();
        var ship = Fleet.Flagship;
        var module = ship != null ? ship.GetModule<CombatModule>() : null;
        if (module == null)
        {
            const string noModule = "the flagship is not in space or has no combat module";
            ctx.Skip("live", noModule);
            ctx.Skip("live-maxrange", noModule);
            yield break;
        }
        var enemy = NearestEnemy(module);
        if (enemy == null && SelfCheckContext.SpawnEnemies(1))
        {
            float until = Time.realtimeSinceStartup + 30f;
            while ((enemy = NearestEnemy(module)) == null && Time.realtimeSinceStartup < until) yield return new WaitForSecondsRealtime(1f);
        }
        if (enemy == null)
        {
            ctx.Skip("live", "no enemy at this location to target");
            ctx.Skip("live-maxrange", "no enemy at this location to target");
            yield break;
        }

        // The check picks the target itself (a manual one), then puts the flagship's own targets back.
        var heldManual = module.manualTarget;
        var heldAuto = module.autoTarget;
        try
        {
            module.ResetCurrentTargets();
            module.SetManualTarget(enemy);
            int eligible = 0;
            float reach = float.MaxValue;
            Vector2 unitPos = ship.transform.position;
            foreach (var t in module.turrets)
            {
                if (t == null || !t.CanTarget(enemy)) continue;
                if (!(t.HasAmmo() || t.ammoType == null || t.AmmoAmountAvailable() > 0)) continue;
                eligible++;
                float own = t.customApproachDistance > 0f
                    ? t.customApproachDistance
                    : Mathf.Max(t.range - Vector2.Distance((Vector2)t.transform.position, unitPos) - 0.5f, 1f);
                if (own < reach) reach = own;
            }
            if (eligible == 0)
            {
                const string noWeapon = "no weapon of the flagship can hit the enemy with the ammo it has";
                ctx.Skip("live", noWeapon);
                ctx.Skip("live-maxrange", noWeapon);
                yield break;
            }

            module.UpdateApproachDistance();
            float expected = Mathf.Clamp(12f, module.GetMinDistanceToTarget(), 30f);
            string live = $"fixed 12: approachDistance={module.approachDistance} expected={expected}";
            if (module.approachDistance == expected) ctx.Pass("live", live);
            else ctx.Fail("live", live);

            ShipSettings.Edit(g).engagement = EngagementMode.MaxRange;
            module.UpdateApproachDistance();
            float expectedMax = Mathf.Clamp(reach, module.GetMinDistanceToTarget(), 30f);
            string liveMax = $"weapons' max range ({eligible} weapons, shortest reach {reach:0.0}): approachDistance={module.approachDistance} expected={expectedMax}";
            if (Mathf.Abs(module.approachDistance - expectedMax) < 0.01f) ctx.Pass("live-maxrange", liveMax);
            else ctx.Fail("live-maxrange", liveMax);
        }
        finally
        {
            module.ResetCurrentTargets();
            if (heldManual != null) module.SetManualTarget(heldManual);
            if (heldAuto != null) module.SetAutoTarget(heldAuto);
        }
    }

    private static AbstractUnit NearestEnemy(CombatModule module)
    {
        AbstractUnit best = null;
        float bestDist = float.MaxValue;
        foreach (var u in UnityEngine.Object.FindObjectsByType<AbstractUnit>(FindObjectsInactive.Exclude))
        {
            if (u == null || !module.IsValidTarget(u)) continue;
            float d = Vector2.Distance(u.transform.position, module.transform.position);
            if (d < bestDist) { best = u; bestDist = d; }
        }
        return best;
    }
}

[HarmonyPatch]
internal static class ApproachPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AbstractTargetingModule), "UpdateApproachDistance")]
    private static void Postfix(AbstractTargetingModule __instance)
    {
        var feature = EngagementDistance.Instance;
        if (!feature.Active) return;
        try
        {
            if (!(__instance is CombatModule)) return;
            var unit = __instance.parent;
            if (unit == null || !Fleet.IsPlayerShip(unit)) return;
            var data = unit.unitData as SpaceShipData;
            if (data == null) return;
            var s = ShipSettings.Get(data.guid);
            if (s.engagement == EngagementMode.Auto) return;
            var target = __instance.priorityTarget;
            if (target == null) return;
            var turrets = __instance.turrets;
            if (turrets == null) return;
            float d = float.MaxValue;
            bool any = false, reached = false;
            Vector2 unitPos = unit.transform.position;
            for (int i = 0; i < turrets.Length; i++)
            {
                var t = turrets[i];
                if (t == null || !t.CanTarget(target)) continue;
                any = true;
                // The game skips launchers with no ammo when it works out the distance (EvaluateTurretApproach).
                if (!(t.HasAmmo() || t.ammoType == null || t.AmmoAmountAvailable() > 0)) continue;
                reached = true;
                if (s.engagement == EngagementMode.Fixed) { d = s.engagementDistance; break; }
                float reach = t.customApproachDistance > 0f
                    ? t.customApproachDistance
                    : Mathf.Max(t.range - Vector2.Distance((Vector2)t.transform.position, unitPos) - 0.5f, 1f);
                if (reach < d) d = reach;
            }
            if (!any || !reached) return;
            __instance.approachDistance = Mathf.Clamp(d, __instance.GetMinDistanceToTarget(), 30f);
        }
        catch (Exception e) { feature.Fail(e, nameof(ApproachPatch)); }
    }
}
