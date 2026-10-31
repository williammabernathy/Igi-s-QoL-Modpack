using System;
using System.Collections;
using Behaviour.Equipment.Module;
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
        ctx.Cleanup(() =>
        {
            ShipSettings.Edit(g).engagement = EngagementMode.Auto;
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

        var ship = GameplayManager.Instance != null ? GameplayManager.Instance.spaceShip : null;
        var module = ship != null ? ship.targetProvider : null;
        if (module == null || module.priorityTarget == null)
        {
            ctx.Skip("live", "flagship has no target at a station; behaviour is confirmed in combat");
            yield break;
        }
        module.UpdateApproachDistance();
        float expected = Mathf.Clamp(12f, module.GetMinDistanceToTarget(), 30f);
        string live = $"approachDistance={module.approachDistance} expected={expected}";
        if (module.approachDistance == expected) ctx.Pass("live", live);
        else ctx.Fail("live", live);
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
            bool any = false;
            Vector2 unitPos = unit.transform.position;
            for (int i = 0; i < turrets.Length; i++)
            {
                var t = turrets[i];
                if (t == null || !t.CanTarget(target)) continue;
                any = true;
                if (s.engagement == EngagementMode.Fixed) { d = s.engagementDistance; break; }
                float reach = t.customApproachDistance > 0f
                    ? t.customApproachDistance
                    : Mathf.Max(t.range - Vector2.Distance((Vector2)t.transform.position, unitPos) - 0.5f, 1f);
                if (reach < d) d = reach;
            }
            if (!any) return;
            __instance.approachDistance = Mathf.Clamp(d, __instance.GetMinDistanceToTarget(), 30f);
        }
        catch (Exception e) { feature.Fail(e, nameof(ApproachPatch)); }
    }
}
