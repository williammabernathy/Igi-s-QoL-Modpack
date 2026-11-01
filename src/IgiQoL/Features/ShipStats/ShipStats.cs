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
    public override string Description => "A ship's hover card lists how much of each damage type it resists right now, buffs and debuffs included. Ships with armour also show what a hit on the armour loses in total, armour's own resistance or weakness included. Below that, every buff and debuff on the ship with its stacks and time left, what the ship is targeting, an enemy's threat list and which units are targeting the ship. The card updates twice a second while you hover it.";
    public override Type[] PatchClasses => new[] { typeof(ShipStatsPatches) };

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => ShipStatsCheck.Run(ctx);

    internal const string SectionTitle = "Resistances";
    internal const string EffectsTitle = "Buffs and debuffs";
    internal const string TargetingLabel = "Targeting";
    internal const string ThreatTitle = "Threat";
    internal const string AttackersTitle = "Attacked by";
    internal const int AggroRows = 5;

    private static readonly List<TemporaryEffect> CardEffects = new();
    private static readonly List<ActivatedAbility> CardAbilities = new();
    private static readonly List<KeyValuePair<AbstractUnit, float>> CardThreat = new();
    private static readonly List<AbstractUnit> CardAttackers = new();
    private static readonly List<string> AttackerNames = new();
    private static readonly List<int> AttackerCounts = new();

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

    // What the ship is targeting, as a card row next to the game's Captain row.
    internal static TooltipRow TargetingRow(AbstractUnit unit)
    {
        var target = Aggro.Target(unit);
        return new TooltipRow(TargetingLabel, target != null ? Aggro.Name(target) : "nothing", null);
    }

    // An NPC combat ship's threat table, highest first, each as a share of the top entry, so you can see how close
    // another ship is to pulling it away. The unit it's attacking gets a "target" badge: the game skips entries out of
    // its weapon or chase range first, so that's not always the top one. Null when it has no table or no entries.
    internal static TooltipSection ThreatSection(AbstractUnit unit)
    {
        if (!Aggro.Threat(unit, CardThreat) || CardThreat.Count == 0) return null;
        var target = Aggro.Target(unit);
        float top = CardThreat[0].Value;
        var section = new TooltipSection(ThreatTitle) { count = "% of top", countColor = ColorHelper.poiTooltipMuted };
        for (int i = 0; i < CardThreat.Count && i < AggroRows; i++)
        {
            var row = new TooltipRow(Aggro.Name(CardThreat[i].Key), Mathf.RoundToInt(CardThreat[i].Value / top * 100f) + "%", null);
            if (CardThreat[i].Key == target) row.WithBadge("target", ColorHelper.red90);
            section.rows.Add(row);
        }
        AddMore(section, CardThreat.Count - section.rows.Count);
        CardThreat.Clear();
        return section;
    }

    // Units whose current target is this ship, one row per name with a count ("Defense Platform x6"), most first.
    // Null when there are none.
    internal static TooltipSection AttackersSection(AbstractUnit unit)
    {
        Aggro.Attackers(unit, CardAttackers);
        if (CardAttackers.Count == 0) return null;
        foreach (var a in CardAttackers)
        {
            string name = Aggro.Name(a);
            int i = AttackerNames.IndexOf(name);
            if (i < 0)
            {
                AttackerNames.Add(name);
                AttackerCounts.Add(1);
                continue;
            }
            AttackerCounts[i]++;
            for (; i > 0 && AttackerCounts[i - 1] < AttackerCounts[i]; i--)
            {
                (AttackerNames[i - 1], AttackerNames[i]) = (AttackerNames[i], AttackerNames[i - 1]);
                (AttackerCounts[i - 1], AttackerCounts[i]) = (AttackerCounts[i], AttackerCounts[i - 1]);
            }
        }
        var section = new TooltipSection(AttackersTitle) { count = CardAttackers.Count.ToString(), countColor = ColorHelper.poiTooltipMuted };
        int hidden = 0;
        for (int i = 0; i < AttackerNames.Count; i++)
        {
            if (i < AggroRows) section.rows.Add(new TooltipRow(AttackerNames[i] + (AttackerCounts[i] > 1 ? " x" + AttackerCounts[i] : ""), "", null));
            else hidden += AttackerCounts[i];
        }
        AddMore(section, hidden);
        CardAttackers.Clear();
        AttackerNames.Clear();
        AttackerCounts.Clear();
        return section;
    }

    private static void AddMore(TooltipSection section, int hidden)
    {
        if (hidden > 0) section.rows.Add(new TooltipRow("+" + hidden + " more", "", null));
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
            __result.rows.Add(ShipStats.TargetingRow(__instance));
            var threat = ShipStats.ThreatSection(__instance);
            if (threat != null) __result.sections.Add(threat);
            var attackers = ShipStats.AttackersSection(__instance);
            if (attackers != null) __result.sections.Add(attackers);
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
