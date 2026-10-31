using System;
using System.Collections;
using Behaviour.Equipment.Turret.Projectile;
using Behaviour.Weapons;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using Source.Player;
using Source.SpaceShip;
using UnityEngine;

namespace IgiQoL.Features.MissileRetarget;

public sealed class MissileRetarget : Feature
{
    public static MissileRetarget Instance;

    public MissileRetarget() { Instance = this; }

    public override string Id => "MissileRetarget";
    public override string Title => "Missile retargeting";
    public override string Description => "Lets your ships' missiles find a new target when the enemy they were chasing is destroyed, instead of flying on and going to waste.";
    public override Type[] PatchClasses => new[] { typeof(MissilePatches) };

    public ConfigEntry<RetargetMode> Mode;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        Mode = cfg.Bind(Id, "Mode", RetargetMode.Off, SettingInfo.Describe("When a missile's target dies",
            "What your ships' missiles do when their target is destroyed before they hit. Fly on: the game's normal behaviour, the missile is wasted. Weapon's target: steer to whatever the launcher is aiming at now. Nearest enemy: steer to the closest enemy the launcher could hit. Each ship can override this in its own settings (the last button on its row in the fleet panel).",
            "Fly on (game default)", "Weapon's target", "Nearest enemy"));
    }

    // Per-ship override (ShipSettings.missileRetarget) wins over the config value.
    public RetargetMode Resolve(SpaceShipData ship) => ShipSettings.Get(ship.guid).missileRetarget ?? Mode.Value;

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        var flagship = GamePlayer.current.currentSpaceShip;
        ctx.Pass("mode", $"flagship {flagship.GetShipName()}: {Resolve(flagship)} (config {Mode.Value})");
        yield break;
    }
}

public sealed class MissileOverride : MonoBehaviour
{
    public TargetableUnit target;
}

[HarmonyPatch]
internal static class MissilePatches
{
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
            if (turret == null || !Fleet.IsPlayerShip(turret.parent)) return;
            var mode = feature.Resolve((SpaceShipData)turret.parent.unitData);
            if (mode == RetargetMode.Off) return;

            if (mode == RetargetMode.Nearest)
            {
                var module = turret.targetingModule;
                TargetableUnit best = null;
                if (module != null)
                {
                    Vector3 pos = __instance.transform.position;
                    float bestSqr = float.MaxValue;
                    var list = module.filteredTargets;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var t = list[i];
                        if (t == null || t.isDestroyed || !turret.CanTarget(t)) continue;
                        float sqr = (t.transform.position - pos).sqrMagnitude;
                        if (sqr < bestSqr)
                        {
                            bestSqr = sqr;
                            best = t;
                        }
                    }
                }
                if (best != null)
                {
                    __instance.target = best.transform;
                    var o = __instance.GetComponent<MissileOverride>();
                    if (o == null) o = __instance.gameObject.AddComponent<MissileOverride>();
                    o.target = best;
                    return;
                }
            }

            var current = turret.currentTarget;
            if (current != null && !current.isDestroyed) __instance.target = current.transform;
        }
        catch (Exception e) { feature.Fail(e, nameof(MissilePatches)); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Missile), "UpdateTrackingTarget")]
    private static bool UpdateTrackingTargetPrefix(Missile __instance)
    {
        var feature = MissileRetarget.Instance;
        if (!feature.Active) return true;
        try
        {
            var o = __instance.GetComponent<MissileOverride>();
            if (o == null || o.target == null || o.target.isDestroyed) return true;
            Vector2 pos = __instance.transform.position;
            Vector2 targetPos = o.target.transform.position;
            float dt = Vector2.Distance(pos, targetPos) / __instance.maxSpeed;
            __instance.targetPosition = targetPos + o.target.targetableVelocity * dt + __instance.GetSwarmOffset();
            return false;
        }
        catch (Exception e)
        {
            feature.Fail(e, nameof(MissilePatches));
            return true;
        }
    }
}
