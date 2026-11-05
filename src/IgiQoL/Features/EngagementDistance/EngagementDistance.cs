using System;
using System.Collections;
using Behaviour.Equipment.Module;
using Behaviour.Unit;
using Behaviour.Equipment.Turret;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using Behaviour.Weapons;
using Source.Player;
using Source.SpaceShip;
using UnityEngine;

namespace IgiQoL.Features.EngagementDistance;

public sealed class EngagementDistance : Feature
{
    public static EngagementDistance Instance;

    public EngagementDistance() { Instance = this; }

    public override string Id => "EngagementDistance";
    public override string Title => "Engagement Distance";
    public override string Description => "Lets each of your ships keep its own distance from its target in combat. Set it per ship with the last button on the ship's row in the fleet panel. It only matters while a ship is chasing its target. The game flies ships in formation, and ignores this setting, on Escort or Hold Fire, with a Hold or Move order, and on Defensive until something hits them. The ship you fly stays put while Hold Position is switched on in the HUD.";
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
            ctx.Skip("live-fixed-cap", noModule);
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
            ctx.Skip("live-fixed-cap", "no enemy at this location to target");
            yield break;
        }

        // The check picks the target itself (a manual one), then puts the flagship's own targets back.
        var heldManual = module.manualTarget;
        var heldAuto = module.autoTarget;
        try
        {
            module.ResetCurrentTargets();
            module.SetManualTarget(enemy);
            // Each weapon's reach to the enemy's hull, measured from the flagship's centre: the farthest the flagship
            // can be from the enemy's centre and still hit it is that plus the hull between the enemy's centre and it.
            int eligible = 0;
            float edge = float.MaxValue;
            Vector2 unitPos = ship.transform.position;
            float hull = 0f;
            if (enemy.surfaceCollider != null && enemy.surfaceCollider.enabled)
                hull = Vector2.Distance(enemy.transform.position, enemy.surfaceCollider.ClosestPoint(unitPos));
            foreach (var t in module.turrets)
            {
                if (t == null || !t.CanTarget(enemy)) continue;
                if (!(t.HasAmmo() || t.ammoType == null || t.AmmoAmountAvailable() > 0)) continue;
                eligible++;
                float own = t.customApproachDistance > 0f
                    ? t.customApproachDistance * 1.2f
                    : t.range - Vector2.Distance((Vector2)t.transform.position, unitPos) - 0.5f + hull;
                if (own < edge) edge = own;
            }
            if (eligible == 0)
            {
                const string noWeapon = "no weapon of the flagship can hit the enemy with the ammo it has";
                ctx.Skip("live", noWeapon);
                ctx.Skip("live-maxrange", noWeapon);
                ctx.Skip("live-fixed-cap", noWeapon);
                yield break;
            }
            float min = module.GetMinDistanceToTarget();

            ShipSettings.Edit(g).engagement = EngagementMode.Auto;
            module.UpdateApproachDistance();
            float auto = module.approachDistance;

            // The game only moves a ship in once it's more than 1.2 times the distance away, so 1.2 times the
            // distance is where the ship may sit: that has to be within every weapon's reach, and as far back as
            // that allows (unless the game's 6 to 30 limits apply), and never closer than the game's own pick.
            ShipSettings.Edit(g).engagement = EngagementMode.MaxRange;
            module.UpdateApproachDistance();
            float max = module.approachDistance;
            float want = Mathf.Clamp(edge / 1.2f, min, 30f);
            string liveMax = $"weapons' max range ({eligible} weapons, enemy hull {hull:0.0}, nearest hull reach {edge:0.0}): approachDistance={max:0.00} sits out to {max * 1.2f:0.00}, expected {want:0.00}; game's own {auto:0.00}";
            if (Mathf.Abs(max - want) < 0.05f && (max * 1.2f <= edge + 0.05f || max <= min) && max >= auto - 0.05f) ctx.Pass("live-maxrange", liveMax);
            else ctx.Fail("live-maxrange", liveMax);

            ShipSettings.Edit(g).engagement = EngagementMode.Fixed;
            module.UpdateApproachDistance();
            float expected = Mathf.Clamp(Mathf.Min(12f, max), min, 30f);
            string live = $"fixed 12: approachDistance={module.approachDistance:0.00} expected={expected:0.00}";
            if (Mathf.Abs(module.approachDistance - expected) < 0.01f) ctx.Pass("live", live);
            else ctx.Fail("live", live);

            // A typed distance beyond the weapons' reach stops at the same place as Max range.
            ShipSettings.Edit(g).engagementDistance = 30f;
            module.UpdateApproachDistance();
            string cap = $"fixed 30: approachDistance={module.approachDistance:0.00}, max range {max:0.00}";
            if (Mathf.Abs(module.approachDistance - max) < 0.01f) ctx.Pass("live-fixed-cap", cap);
            else ctx.Fail("live-fixed-cap", cap);
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
            if (target == null || __instance.turrets == null) return;
            if (!MaxReach(__instance, target, out float reach)) return;
            float d = s.engagement == EngagementMode.Fixed ? Mathf.Min(s.engagementDistance, reach) : reach;
            __instance.approachDistance = Mathf.Clamp(d, __instance.GetMinDistanceToTarget(), 30f);
        }
        catch (Exception e) { feature.Fail(e, nameof(ApproachPatch)); }
    }

    // The game moves a ship in only once it's more than 1.2 times its approach distance from the target's centre,
    // and a weapon is in range when the target's hull, not its centre, is within reach. So the farthest a ship can
    // hold and still hit is 5/6 of (the weapon's reach from the ship's centre + the target's hull between its centre
    // and the ship). The game's own distance leaves the hull out, which keeps big targets closer than they need to be.
    // The shortest of those over the weapons that can hit the target with the ammo they have; false when none can.
    internal static bool MaxReach(AbstractTargetingModule module, TargetableUnit target, out float reach)
    {
        reach = float.MaxValue;
        var unit = module.parent;
        Vector2 unitPos = unit.transform.position;
        float hull = 0f;
        var surface = target.surfaceCollider;
        if (surface != null && surface.enabled && surface.gameObject.activeInHierarchy)
            hull = Vector2.Distance(target.transform.position, surface.ClosestPoint(unitPos));
        bool any = false, reached = false;
        var turrets = module.turrets;
        for (int i = 0; i < turrets.Length; i++)
        {
            var t = turrets[i];
            if (t == null || !t.CanTarget(target)) continue;
            any = true;
            // The game skips launchers with no ammo when it works out the distance (EvaluateTurretApproach).
            if (!(t.HasAmmo() || t.ammoType == null || t.AmmoAmountAvailable() > 0)) continue;
            reached = true;
            float own = t.customApproachDistance > 0f
                ? t.customApproachDistance
                : (Mathf.Max(t.range - Vector2.Distance((Vector2)t.transform.position, unitPos) - 0.5f, 1f) + hull) * (5f / 6f);
            if (own < reach) reach = own;
        }
        return any && reached;
    }
}
