using System;
using System.Collections;
using Behaviour.Equipment.Module;
using Behaviour.Item;
using Behaviour.Tractoring;
using Behaviour.UI;
using Behaviour.UI.Tooltip;
using Behaviour.Unit;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.Rules;
using Source.Item;
using Source.Player;
using Source.SpaceShip;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.TractorCaps;

public sealed class TractorCaps : Feature
{
    public static TractorCaps Instance;

    public TractorCaps() { Instance = this; }

    public override string Id => "TractorCaps";
    public override string Title => "Loot pickup limits";
    public override string Description => "Your ships stop pulling in an item automatically once they carry the limit set for it on the Item rules tab (or never pull it in, with -1), and can leave cheap bulky loot alone. Loot they leave says why on its tooltip and on its Scanner row. Clicking the item yourself still picks it up.";
    public override string SettingsTab => "Item rules";
    public override Type[] PatchClasses => new[] { typeof(TractorCapsPatch), typeof(TractorCapsTooltipPatch) };

    public ConfigEntry<int> MinCreditsPerM3;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        MinCreditsPerM3 = cfg.Bind(Id, "MinCreditsPerM3", 0, SettingInfo.Describe("Skip loot worth less per m3 than",
            "Your ships (and ECHO's loot-before-leaving) don't pull in loot whose sell value per m3 of hold space is below this many credits, so cheap bulky items don't fill the hold. Items your missions need and ammo a ship uses are still picked up, and clicking an item yourself always works. 0 turns this off.",
            new AcceptableValueRange<int>(0, 1000000)));
    }

    // Why the filters hold an item back from a ship's auto-tractor. None = they let it through.
    public enum Skip { None, Limit, Never, Cheap }

    public static bool Allows(SpaceShipData ship, InventoryItemType item, AbstractUnit unit = null) => Check(ship, item, unit) == Skip.None;

    // Runs inside the tractor gate on every scan, so it returns an enum and builds no text.
    public static Skip Check(SpaceShipData ship, InventoryItemType item, AbstractUnit unit = null)
    {
        int cap = ItemRules.Resolve(item).tractorCap;
        if (cap == ItemRule.Never) return Exempt(unit, item) ? Skip.None : Skip.Never;
        if (cap > 0 && Cargo.Count(ship, item) >= cap) return Skip.Limit;
        return TooCheap(unit, item) ? Skip.Cheap : Skip.None;
    }

    public static bool TooCheap(AbstractUnit unit, InventoryItemType item)
    {
        int min = Instance.MinCreditsPerM3.Value;
        if (min <= 0 || item.m3 <= 0f || item.sellValue >= min * item.m3) return false;
        return !Exempt(unit, item);
    }

    public static string Describe(Skip skip, InventoryItemType item) => skip switch
    {
        Skip.Limit => "limit " + ItemRules.Resolve(item).tractorCap + " reached",
        Skip.Never => "never pick up",
        Skip.Cheap => "below " + Instance.MinCreditsPerM3.Value + " cr/m3",
        _ => null,
    };

    // Why none of your ships here auto-tractors the item, or null when one of them would, or none has a tractor beam.
    // With several ships held back, the first one's reason (the ship you fly comes first). Called when a Scanner row
    // refreshes or a loot tooltip opens, not from the gate.
    public static string WhySkipped(InventoryItemType item)
    {
        if (Instance == null || !Instance.Active || item == null) return null;
        string why = null;
        foreach (var data in Fleet.Ships(withMercenaries: true))
        {
            var unit = Fleet.Unit(data);
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.GetModule<TractorModule>() == null) continue;
            var skip = Check(data, item, unit);
            if (skip == Skip.None) return null;
            why ??= Describe(skip, item);
        }
        return why;
    }

    // Loot the filters never hold back: items your missions need and ammo the ship fires.
    private static bool Exempt(AbstractUnit unit, InventoryItemType item) =>
        GamePlayer.current.RequiredItemCountForMissions(item) > 0
        || item.itemCategory == ItemCategory.Ammo && unit != null && unit.DoWeUseAmmo(item);

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        InventoryItemType ammo = null;
        foreach (var t in InventoryItemType.all)
            if (t.itemCategory == ItemCategory.Ammo) { ammo = t; break; }
        if (ammo == null) ctx.Skip("resolve-ammo", "no ammo item type");
        else ctx.Pass("resolve-ammo", $"{Items.RuleKey(ammo)} tractorCap={ItemRules.Resolve(ammo).tractorCap}");

        var ship = GamePlayer.current.currentSpaceShip;
        Inventory.InventoryItem stack = null;
        foreach (var s in ship.cargo.items) { stack = s; break; }
        if (stack == null)
        {
            ctx.Skip("gate", "cargo empty");
            yield break;
        }
        bool capped, uncapped;
        string cappedWhy;
        try
        {
            ItemRules.UseForTest("{\"default\":{\"tractorCap\":1}}");
            capped = Allows(ship, stack.item);
            cappedWhy = Describe(Check(ship, stack.item), stack.item);
            ItemRules.UseForTest("{\"default\":{\"tractorCap\":0}}");
            uncapped = Allows(ship, stack.item);
        }
        finally { ItemRules.Load(); }
        string detail = $"{Items.RuleKey(stack.item)} count={Cargo.Count(ship, stack.item)} cap1 allows={capped} ('{cappedWhy}') cap0 allows={uncapped}";
        if (!capped && cappedWhy == "limit 1 reached" && uncapped) ctx.Pass("gate", detail);
        else ctx.Fail("gate", detail);

        // Value per m3: a stack type below the line is skipped, the same type with the line at 0 or under its value isn't.
        InventoryItemType bulky = null;
        foreach (var t in InventoryItemType.all)
            if (t.m3 > 0f && t.sellValue > 0 && t.itemCategory == ItemCategory.Ore && GamePlayer.current.RequiredItemCountForMissions(t) == 0) { bulky = t; break; }
        if (bulky == null)
        {
            ctx.Skip("value", "no ore item type with a value");
            yield break;
        }
        int min0 = Instance.MinCreditsPerM3.Value;
        float perM3 = bulky.sellValue / bulky.m3;
        try
        {
            int line = Mathf.CeilToInt(perM3) + 1;
            Instance.MinCreditsPerM3.Value = line;
            bool above = Allows(ship, bulky);
            string aboveWhy = Describe(Check(ship, bulky), bulky);
            Instance.MinCreditsPerM3.Value = Mathf.Max(1, Mathf.FloorToInt(perM3));
            bool below = Allows(ship, bulky);
            Instance.MinCreditsPerM3.Value = 0;
            bool off = Allows(ship, bulky);
            string vd = $"{bulky.identifier}: {bulky.sellValue} cr per {bulky.m3} m3 = {perM3:0.##}/m3; line above it allows={above} ('{aboveWhy}'), at or under it allows={below}, off allows={off}";
            if (!above && aboveWhy == "below " + line + " cr/m3" && below && off) ctx.Pass("value", vd);
            else ctx.Fail("value", vd);
        }
        finally { Instance.MinCreditsPerM3.Value = min0; }

        // Never (-1): the ore is left alone; ammo the flagship fires is still picked up.
        var unit = ship.unit;
        InventoryItemType usedAmmo = null;
        if (unit != null)
            foreach (var t in InventoryItemType.all)
                if (t.itemCategory == ItemCategory.Ammo && unit.DoWeUseAmmo(t)) { usedAmmo = t; break; }
        bool neverOre, neverAmmo;
        string neverWhy;
        try
        {
            ItemRules.UseForTest("{\"default\":{\"tractorCap\":-1}}");
            neverOre = Allows(ship, bulky, unit);
            neverWhy = Describe(Check(ship, bulky, unit), bulky);
            neverAmmo = usedAmmo == null || Allows(ship, usedAmmo, unit);
        }
        finally { ItemRules.Load(); }
        string nd = $"cap -1: {bulky.identifier} allows={neverOre} ('{neverWhy}'), used ammo {usedAmmo?.identifier ?? "(none fitted)"} allows={neverAmmo}";
        if (!neverOre && neverWhy == "never pick up" && neverAmmo) ctx.Pass("never", nd);
        else ctx.Fail("never", nd);
    }
}

[HarmonyPatch(typeof(TractorableItem), nameof(TractorableItem.CanBeAutoTractoredBy))]
internal static class TractorCapsPatch
{
    private static void Postfix(TractorableItem __instance, AbstractUnit unit, ref bool __result)
    {
        if (!TractorCaps.Instance.Active) return;
        try
        {
            if (__result && Fleet.IsPlayerShip(unit) && !TractorCaps.Allows((SpaceShipData)unit.unitData, __instance.data.itemType, unit))
                __result = false;
        }
        catch (Exception e) { TractorCaps.Instance.Fail(e, nameof(TractorCapsPatch)); }
    }
}

// Tooltips of loot in space (and of the Scanner's loot rows) say why your ships leave the item alone.
[HarmonyPatch(typeof(CompareTooltip), "AddContextualFooterHints")]
internal static class TractorCapsTooltipPatch
{
    private static void Postfix(CompareTooltip __instance, InventoryItemType item, ItemTooltipContext context)
    {
        if (!TractorCaps.Instance.Active || context != ItemTooltipContext.InSpace) return;
        try
        {
            string why = TractorCaps.WhySkipped(item);
            if (why != null) __instance.AddTextLine("Not picked up automatically: " + why).Text.color = ColorHelper.reddish;
        }
        catch (Exception e) { TractorCaps.Instance.Fail(e, nameof(TractorCapsTooltipPatch)); }
    }
}
