using System;
using System.Collections;
using Behaviour.Unit;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using Source.Combat;
using Source.UI.Tooltip;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.ShipStats;

public sealed class ShipStats : Feature
{
    public static ShipStats Instance;

    public ShipStats() { Instance = this; }

    public override string Id => "ShipStats";
    public override string Title => "Ship stats on hover";
    public override string Description => "A ship's hover card lists how much of each damage type it resists right now, buffs and debuffs included. Ships with armour also show what a hit on the armour loses in total, armour's own resistance or weakness included.";
    public override Type[] PatchClasses => new[] { typeof(ShipStatsPatches) };

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => ShipStatsCheck.Run(ctx);

    internal const string SectionTitle = "Resistances";

    // One row per damage type. The first figure is what shields and hull resist, the second what a hit on the
    // armour loses in total; negative when debuffs or an armour weakness make the type hit harder than normal.
    internal static TooltipSection Section(AbstractUnit unit)
    {
        bool armor = Resists.HasArmor(unit);
        var section = new TooltipSection(SectionTitle);
        if (armor)
        {
            section.count = "shield, hull | armour";
            section.countColor = ColorHelper.poiTooltipMuted;
        }
        foreach (var type in Resists.Types)
        {
            float hit = Resists.Multiplier(unit, type);
            float armorHit = hit * Resists.ArmorMultiplier(unit, type);
            string value = armor ? Pct(hit) + " | " + Pct(armorHit) : Pct(hit);
            bool weak = hit > 1f || (armor && armorHit > 1f);
            section.rows.Add(new TooltipRow(type.GetDisplayName(), value, weak ? ColorHelper.red90 : null) { dotColor = type.GetColor() });
        }
        return section;
    }

    // Resisted share of a hit, from the share that gets through.
    internal static string Pct(float taken) => Mathf.RoundToInt((1f - taken) * 100f) + "%";
}

[HarmonyPatch]
internal static class ShipStatsPatches
{
    private static ShipStats F => ShipStats.Instance;

    // Every ship's hover card, enemy, neutral or yours. Fleet ships rebuild it when their status changes.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpaceShip), "BuildTooltipCard")]
    private static void BuildTooltipCardPostfix(SpaceShip __instance, TooltipCard __result)
    {
        if (!F.Active || __result == null) return;
        try
        {
            __result.sections.Add(ShipStats.Section(__instance));
        }
        catch (Exception e) { F.Fail(e, nameof(BuildTooltipCardPostfix)); }
    }
}
