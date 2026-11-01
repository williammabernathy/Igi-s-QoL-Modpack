using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Ability;
using Behaviour.UI;
using Behaviour.UI.Tooltip;
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
    public override string Description => "A ship's hover card lists how much of each damage type it resists right now, buffs and debuffs included. Ships with armour also show what a hit on the armour loses in total, armour's own resistance or weakness included. Below that, every buff and debuff on the ship with its stacks and time left. The card updates twice a second while you hover it.";
    public override Type[] PatchClasses => new[] { typeof(ShipStatsPatches) };

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => ShipStatsCheck.Run(ctx);

    internal const string SectionTitle = "Resistances";
    internal const string EffectsTitle = "Buffs and debuffs";

    private static readonly List<TemporaryEffect> CardEffects = new();
    private static readonly List<ActivatedAbility> CardAbilities = new();

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

    // One row per buff or debuff the game's buff HUD would show: name and stacks, then time left over a bar in the
    // game's buff (blue) or debuff (red) colour. Abilities without an effect of their own have no timer. Null when
    // the ship has none.
    internal static TooltipSection EffectsSection(AbstractUnit unit)
    {
        Effects.Collect(unit, CardEffects, CardAbilities);
        if (CardEffects.Count + CardAbilities.Count == 0) return null;
        var section = new TooltipSection(EffectsTitle);
        foreach (var e in CardEffects)
        {
            var color = e.isDebuff ? ColorHelper.debuffBorder : ColorHelper.buffBorder;
            string name = Translation.Translate(e.displayName);
            if (e.stackSize > 1) name += " x" + e.stackSize;
            var row = new TooltipRow(name, TimeLeft(e.durationRemaining), null) { dotColor = color };
            section.rows.Add(row.WithFill(e.duration > 0f ? e.durationRemaining / e.duration : 0f, color));
        }
        foreach (var a in CardAbilities)
            section.rows.Add(new TooltipRow(Translation.Translate(a.displayName), a.toggleActive ? "on" : "active", null)
                { dotColor = a.toggledAbility ? ColorHelper.orange75 : ColorHelper.buffBorder });
        int debuffs = Effects.DebuffCount(unit);
        section.count = Count(section.rows.Count - debuffs, "buff") + " | " + Count(debuffs, "debuff");
        section.countColor = debuffs > 0 ? ColorHelper.red90 : ColorHelper.poiTooltipMuted;
        CardEffects.Clear();
        CardAbilities.Clear();
        return section;
    }

    private static string Count(int n, string noun) => n + " " + noun + (n == 1 ? "" : "s");

    // "45s" under a minute, else "2:05". The game's own "0 minutes and 45 seconds." is too wide for a card row.
    private static string TimeLeft(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return s < 60 ? s + "s" : s / 60 + ":" + (s % 60).ToString("00");
    }
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
            var effects = ShipStats.EffectsSection(__instance);
            if (effects != null) __result.sections.Add(effects);
            ShipStatsHover.Built(__instance);
        }
        catch (Exception e) { F.Fail(e, nameof(BuildTooltipCardPostfix)); }
    }
}

// Rebuilds a ship's hover card every 0.5 s while it shows, so effect timers run and resistances follow debuffs that
// land or expire. The game's buff icons refresh their own card at the same rate (BuffIcon).
internal sealed class ShipStatsHover : MonoBehaviour
{
    private const float RefreshSeconds = 0.5f;

    private TooltipSource source;
    private float timer;

    internal static void Built(SpaceShip ship)
    {
        var hover = ship.GetComponent<ShipStatsHover>();
        if (hover == null)
        {
            hover = ship.gameObject.AddComponent<ShipStatsHover>();
            hover.source = ship.GetComponent<TooltipSource>();
        }
        hover.timer = RefreshSeconds;
    }

    private void Update()
    {
        if (!ShipStats.Instance.Active || source == null || !UITooltip.IsShowing(source)) return;
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = RefreshSeconds;
        UITooltip.Refresh(source);
    }
}
