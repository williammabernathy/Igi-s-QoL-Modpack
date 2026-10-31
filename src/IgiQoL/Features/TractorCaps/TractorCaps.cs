using System;
using System.Collections;
using Behaviour.Item;
using Behaviour.Tractoring;
using Behaviour.Unit;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.Rules;
using Source.Item;
using Source.Player;
using Source.SpaceShip;
using UnityEngine;

namespace IgiQoL.Features.TractorCaps;

public sealed class TractorCaps : Feature
{
    public static TractorCaps Instance;

    public TractorCaps() { Instance = this; }

    public override string Id => "TractorCaps";
    public override string Title => "Loot pickup limits";
    public override string Description => "Your ships stop pulling in an item automatically once they carry the limit set for it on the Item rules tab (or never pull it in, with -1), and can leave cheap bulky loot alone. Clicking the item yourself still picks it up.";
    public override string SettingsTab => "Item rules";
    public override Type[] PatchClasses => new[] { typeof(TractorCapsPatch) };

    public ConfigEntry<int> MinCreditsPerM3;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        MinCreditsPerM3 = cfg.Bind(Id, "MinCreditsPerM3", 0, SettingInfo.Describe("Skip loot worth less per m3 than",
            "Your ships (and ECHO's loot-before-leaving) don't pull in loot whose sell value per m3 of hold space is below this many credits, so cheap bulky items don't fill the hold. Items your missions need and ammo a ship uses are still picked up, and clicking an item yourself always works. 0 turns this off.",
            new AcceptableValueRange<int>(0, 1000000)));
    }

    public static bool Allows(SpaceShipData ship, InventoryItemType item, AbstractUnit unit = null)
    {
        int cap = ItemRules.Resolve(item).tractorCap;
        if (cap == ItemRule.Never) return Exempt(unit, item);
        if (cap > 0 && Cargo.Count(ship, item) >= cap) return false;
        return !TooCheap(unit, item);
    }

    public static bool TooCheap(AbstractUnit unit, InventoryItemType item)
    {
        int min = Instance.MinCreditsPerM3.Value;
        if (min <= 0 || item.m3 <= 0f || item.sellValue >= min * item.m3) return false;
        return !Exempt(unit, item);
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
        try
        {
            ItemRules.UseForTest("{\"default\":{\"tractorCap\":1}}");
            capped = Allows(ship, stack.item);
            ItemRules.UseForTest("{\"default\":{\"tractorCap\":0}}");
            uncapped = Allows(ship, stack.item);
        }
        finally { ItemRules.Load(); }
        string detail = $"{Items.RuleKey(stack.item)} count={Cargo.Count(ship, stack.item)} cap1 allows={capped} cap0 allows={uncapped}";
        if (!capped && uncapped) ctx.Pass("gate", detail);
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
            Instance.MinCreditsPerM3.Value = Mathf.CeilToInt(perM3) + 1;
            bool above = Allows(ship, bulky);
            Instance.MinCreditsPerM3.Value = Mathf.Max(1, Mathf.FloorToInt(perM3));
            bool below = Allows(ship, bulky);
            Instance.MinCreditsPerM3.Value = 0;
            bool off = Allows(ship, bulky);
            string vd = $"{bulky.identifier}: {bulky.sellValue} cr per {bulky.m3} m3 = {perM3:0.##}/m3; line above it allows={above}, at or under it allows={below}, off allows={off}";
            if (!above && below && off) ctx.Pass("value", vd);
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
        try
        {
            ItemRules.UseForTest("{\"default\":{\"tractorCap\":-1}}");
            neverOre = Allows(ship, bulky, unit);
            neverAmmo = usedAmmo == null || Allows(ship, usedAmmo, unit);
        }
        finally { ItemRules.Load(); }
        string nd = $"cap -1: {bulky.identifier} allows={neverOre}, used ammo {usedAmmo?.identifier ?? "(none fitted)"} allows={neverAmmo}";
        if (!neverOre && neverAmmo) ctx.Pass("never", nd);
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
