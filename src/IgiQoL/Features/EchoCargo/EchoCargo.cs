using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Gameplay;
using Behaviour.Item;
using Behaviour.UI;
using Behaviour.UI.NotificationAlert;
using Behaviour.UI.Spacestation;
using Behaviour.Util;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.Rules;
using Source.Galaxy.POI;
using Source.Item;
using Source.Player;
using Source.SpaceShip;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.EchoCargo;

public sealed class EchoCargo : Feature
{
    public static EchoCargo Instance;

    public EchoCargo() { Instance = this; }

    public override string Id => "EchoCargo";
    public override string Title => "ECHO cargo rules";
    public override string Description => "When ECHO unloads at a station, the dock action from the Item rules tab decides what happens to each stack: deposit as usual, keep it aboard, sell it, or deposit up to an amount and sell the rest. Favourite stacks, ammo the flagship uses and the usable items the game keeps aboard are never touched.";
    public override string SettingsTab => "Item rules";
    public override Type[] PatchClasses => new[] { typeof(EchoCargoPatches) };

    public ConfigEntry<bool> ApplyToAutoDeposit, SellFromData;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        ApplyToAutoDeposit = cfg.Bind(Id, "ApplyToAutoDeposit", true, SettingInfo.Describe("Apply when fleet cargo auto-deposits on dock",
            "With the game's fleet auto-deposit on, fleet holds are emptied the moment you dock. On: your rules apply there too: Keep aboard stays aboard, Sell stays aboard until ECHO unloads it, Sell above N deposits up to N. When you dock yourself nothing is sold, unless Station services' \"Sell by item rules when you dock\" is on or you press the airlock's Sell by rules button. Off: the game deposits everything as usual, so fleet-ship rules only matter with auto-deposit off."));
        SellFromData = cfg.Bind(Id, "SellFromData", true, SettingInfo.Describe("Sell from the data inventory",
            "Data items (intel and records, blueprint pieces, jumpgate passes, claims) never sit in cargo: the game puts them straight into your data inventory. On: a Sell or Sell above N rule that names the item or its category sells them from there when ECHO unloads, when you dock with Station services' \"Sell by item rules when you dock\" on, and with the airlock's Sell by rules button. Sell above N keeps N there. The default rule and the any-category \"rarity and below\" rules never sell from the data inventory. Favourites and items your missions need stay. Off: data items are never sold."));
    }

    private static InventoryItemType FirstOre()
    {
        foreach (var t in InventoryItemType.all)
            if (t.itemCategory == ItemCategory.Ore) return t;
        return null;
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        CheckPlan(ctx);
        CheckMoveCap(ctx);
        CheckLiveKeep(ctx);
        CheckDataPlan(ctx);
        CheckDataEcho(ctx);
        yield break;
    }

    private static string Show(CargoRules.Step s) => $"{s.action}/d{s.deposit}/s{s.sell}";

    private static void CheckPlan(SelfCheckContext ctx)
    {
        var sellAbove = new ItemRule { dockAction = DockAction.SellAbove, keepAmount = 10 };
        var sell = new ItemRule { dockAction = DockAction.Sell };
        var keep = new ItemRule { dockAction = DockAction.Keep };
        var def = new ItemRule();
        var a = CargoRules.Plan(sellAbove, 8, 7, 0, true);
        var b = CargoRules.Plan(sellAbove, 5, 10, 0, true);
        var c = CargoRules.Plan(sellAbove, 5, 10, 0, false);
        var d = CargoRules.Plan(sell, 5, 0, 2, true);
        var e = CargoRules.Plan(sell, 2, 0, 2, true);
        var f = CargoRules.Plan(sell, 5, 0, 0, false);
        var g = CargoRules.Plan(keep, 5, 0, 0, true);
        var h = CargoRules.Plan(def, 5, 0, 0, true);
        bool ok = a.action == DockAction.SellAbove && a.deposit == 3 && a.sell == 0
            && b.action == DockAction.SellAbove && b.sell == 5 && b.deposit == 0
            && c.action == DockAction.Default
            && d.action == DockAction.Sell && d.sell == 3
            && e.action == DockAction.Keep
            && f.action == DockAction.Default
            && g.action == DockAction.Keep
            && h.action == DockAction.Default;
        string detail = $"above(8,7)={Show(a)} above(5,10)={Show(b)} above-unsellable={Show(c)} sell(5,res2)={Show(d)} sell(2,res2)={Show(e)} sell-unsellable={Show(f)} keep={Show(g)} default={Show(h)}";
        if (ok) ctx.Pass("plan", detail);
        else ctx.Fail("plan", detail);
    }

    private static void CheckMoveCap(SelfCheckContext ctx)
    {
        var ore = FirstOre();
        if (ore == null)
        {
            ctx.Skip("move-cap", "no ore item type");
            return;
        }
        var from = new Inventory(false, false, () => 1000f);
        var to = new Inventory(false, false, () => 1000f);
        from.Add(ore, 50);
        int moved = Cargo.Move(from, to, ore, 7);
        string detail = $"moved={moved} from={from.GetCount(ore)} to={to.GetCount(ore)}";
        if (moved == 7 && from.GetCount(ore) == 43 && to.GetCount(ore) == 7) ctx.Pass("move-cap", detail);
        else ctx.Fail("move-cap", detail);
    }

    private static void CheckLiveKeep(SelfCheckContext ctx)
    {
        var ss = SpaceStation.current;
        if (ss == null || SpaceStationInterior.instance == null || !Singleton<IdleManager>.HasInstance)
        {
            ctx.Skip("live-keep", "not docked");
            return;
        }
        var flagship = GamePlayer.current.currentSpaceShip;
        Inventory.InventoryItem stack = null;
        foreach (var s in flagship.cargo.items)
            if (!s.favourite && (s.item.CanGoInMaterials() || s.item.CanGoInArmory())) { stack = s; break; }
        if (stack == null)
        {
            ctx.Skip("live-keep", "no droppable cargo");
            return;
        }
        var item = stack.item;
        int before = flagship.cargo.GetCount(item);
        bool result;
        try
        {
            ItemRules.UseForTest("{\"default\":{\"dockAction\":\"Keep\"}}");
            result = Singleton<IdleManager>.Instance.TryDropCargoOf(ss, flagship);
        }
        finally { ItemRules.Load(); }
        int after = flagship.cargo.GetCount(item);
        string detail = $"{Items.RuleKey(item)} result={result} count {before}->{after}";
        if (!result && after == before) ctx.Pass("live-keep", detail);
        else ctx.Fail("live-keep", detail);
    }

    // A sellable Data-category type, preferring one the player holds none of so the check leaves real stacks alone.
    internal static InventoryItemType TestDataItem()
    {
        InventoryItemType any = null;
        foreach (var t in InventoryItemType.all)
        {
            if (t.itemCategory != ItemCategory.Data || t.identifier == null || !t.canSell || t.sellValue <= 0) continue;
            if (GamePlayer.current.dataInventory.GetCount(t) == 0) return t;
            any ??= t;
        }
        return any;
    }

    private static void CheckDataPlan(SelfCheckContext ctx)
    {
        var sell = new ItemRule { dockAction = DockAction.Sell };
        var above = new ItemRule { dockAction = DockAction.SellAbove, keepAmount = 3 };
        int a = CargoRules.DataSellCount(sell, 5, 2), b = CargoRules.DataSellCount(sell, 2, 2), c = CargoRules.DataSellCount(above, 5, 0),
            d = CargoRules.DataSellCount(above, 5, 4), e = CargoRules.DataSellCount(above, 2, 0),
            f = CargoRules.DataSellCount(new ItemRule { dockAction = DockAction.Keep }, 5, 0), g = CargoRules.DataSellCount(new ItemRule(), 5, 0);
        string detail = $"sell(5,res2)={a} sell(2,res2)={b} above3(5)={c} above3(5,res4)={d} above3(2)={e} keep={f} default={g}";
        if (a == 3 && b == 0 && c == 2 && d == 1 && e == 0 && f == 0 && g == 0) ctx.Pass("data-plan", detail);
        else ctx.Fail("data-plan", detail);
    }

    // ECHO's unload sells from the data inventory: one stack per tick, Sell above N keeps N, an any-category rule sells nothing.
    private static void CheckDataEcho(SelfCheckContext ctx)
    {
        var ss = SpaceStation.current;
        if (ss == null || SpaceStationInterior.instance == null || !Singleton<IdleManager>.HasInstance)
        {
            ctx.Skip("data-echo", "not docked");
            return;
        }
        var item = TestDataItem();
        if (item == null || !CargoRules.CanSellHere(ss, item))
        {
            ctx.Skip("data-echo", item == null ? "no sellable Data item type" : "station has no shop");
            return;
        }
        var player = GamePlayer.current;
        var data = player.dataInventory;
        int count0 = data.GetCount(item);
        long credits0 = player.credits;
        try
        {
            data.Add(item, 5);
            int held = data.GetCount(item);
            int keep = Math.Max(held - 3, player.RequiredItemCountForMissions(item));
            int expect = held - keep;
            ItemRules.UseForTest("{\"items\":{\"" + Items.RuleKey(item) + "\":{\"dockAction\":\"SellAbove\",\"keepAmount\":" + keep + "}}}");
            long before = player.credits;
            bool result = Singleton<IdleManager>.Instance.TryDropCargo(ss);
            int after = data.GetCount(item);
            int left = 0;
            foreach (var s in data.items)
                if (s.item == item) left += CargoRules.DataSell(ss, s, 0);
            // An any-category rule resolves to Sell for the item but never reaches the data inventory.
            ItemRules.UseForTest("{\"rarityUpTo\":{\"Legendary\":{\"dockAction\":\"Sell\"}}}");
            int catchAll = 0;
            foreach (var s in data.items)
                if (s.item == item) catchAll += CargoRules.DataSell(ss, s, 0);
            bool resolves = ItemRules.Resolve(item).dockAction == DockAction.Sell;
            string detail = $"{Items.RuleKey(item)} keep={keep} result={result} held {held}->{after} credits +{player.credits - before} (expected {item.sellValue * expect}) left={left} catchAll={catchAll} resolvesSell={resolves}";
            if (result && expect > 0 && after == keep && player.credits - before == item.sellValue * expect && left == 0 && catchAll == 0 && resolves) ctx.Pass("data-echo", detail);
            else ctx.Fail("data-echo", detail);
        }
        finally
        {
            ItemRules.Load();
            data.Remove(item, data.GetCount(item));
            if (count0 > 0) data.Add(item, count0);
            player.credits = credits0;
        }
    }
}

public static class CargoRules
{
    // action Default = run the vanilla branch.
    public struct Step
    {
        public DockAction action;
        public int deposit;
        public int sell;
    }

    // Pure. count: stack size; stored: amount already in the destination storage; reserved: GamePlayer.RequiredItemCountForMissions; sellable: CanSellHere.
    public static Step Plan(ItemRule rule, int count, int stored, int reserved, bool sellable)
    {
        switch (rule.dockAction)
        {
            case DockAction.Keep:
                return new Step { action = DockAction.Keep };
            case DockAction.Sell:
            {
                if (!sellable) return default;   // unsellable here: deposit as the game would
                int sell = count - reserved;
                return sell > 0 ? new Step { action = DockAction.Sell, sell = sell } : new Step { action = DockAction.Keep };
            }
            case DockAction.SellAbove:
            {
                int room = Math.Max(0, rule.keepAmount - stored);
                if (room > 0) return new Step { action = DockAction.SellAbove, deposit = Math.Min(room, count) };
                if (!sellable) return default;
                int sell = count - reserved;
                return sell > 0 ? new Step { action = DockAction.SellAbove, sell = sell } : new Step { action = DockAction.Keep };
            }
            default:
                return default;
        }
    }

    // Pure. Data-inventory items are already in their storage, so Sell above N keeps N there. held: everything of the type
    // in the data inventory, favourites included; reserved: GamePlayer.RequiredItemCountForMissions.
    public static int DataSellCount(ItemRule rule, int held, int reserved)
    {
        int keep = rule.dockAction == DockAction.Sell ? reserved
            : rule.dockAction == DockAction.SellAbove ? Math.Max(rule.keepAmount, reserved)
            : held;
        return Math.Max(0, held - keep);
    }

    // How many of this data-inventory stack the rules sell here. Only a rule that names the item or its category counts.
    // Favourite stacks are never sold but count toward N. quoted: amount of the type earlier stacks were already quoted for.
    public static int DataSell(SpaceStation ss, Inventory.InventoryItem stack, int quoted)
    {
        if (stack.favourite) return 0;
        var item = stack.item;
        var rule = ItemRules.Explicit(item);
        if (rule == null || !CanSellHere(ss, item)) return 0;
        var player = GamePlayer.current;
        return Math.Min(stack.count, DataSellCount(rule, player.dataInventory.GetCount(item) - quoted, player.RequiredItemCountForMissions(item)));
    }

    public static bool CanSellHere(SpaceStation ss, InventoryItemType item) =>
        ss.shopInventory != null && SpaceStationInterior.instance != null
        && item.canSell && item.sellValue > 0 && !RefinedMaterialExtensions.IsCanister(item);

    // DropFoundItem's chunk, or the whole stack with EchoGates' fast unloading.
    public static int ChunkSize(SpaceShipData ship, InventoryItemType item)
    {
        if (EchoGates.EchoGates.FastUnloadOn) return int.MaxValue;
        if (item.itemCategory == ItemCategory.Ammo) return Mathf.Max(20, InventoryItemType.GetMagSizeForAmmo(item));
        if (item.itemCategory == ItemCategory.Currency) return 20;
        return 1 + Mathf.FloorToInt(ship.cargoCapacity / 200f);
    }

    public static int DepositUpTo(IdleManager im, SpaceStation ss, SpaceShipData ship, InventoryItemType item, int max)
    {
        var dest = Cargo.DepositTarget(ss, item);
        if (dest == null) return 0;
        int moved = Cargo.Move(ship.cargo, dest, item, Math.Min(max, ChunkSize(ship, item)));
        if (moved > 0)
        {
            im.inventoryFull = false;
            im.SetQuickerUpdateTimer();
            IdleManager.ReportDeposit(ship, item);
        }
        return moved;
    }

    private static bool shopWarned;

    // Mirrors IdleManager.SellItemFromMaterials: open the shop screen (registers it with InventoryInteractionManager), sell, report, count the stat.
    public static bool Sell(IdleManager im, SpaceStation ss, Inventory.InventoryItem stack, int amount)
    {
        SpaceStationInterior.instance.GoToLocation(ss.shopInventory.facility);
        var iim = InventoryInteractionManager.Instance;
        if (iim == null || !iim.isShopOpen)
        {
            if (!shopWarned)
            {
                shopWarned = true;
                Plugin.Log.LogWarning("[EchoCargo] shop screen did not open; nothing sold");
            }
            return false;
        }
        var item = stack.item;
        long value = item.sellValue;
        if (!iim.SellAmount(stack, amount)) return false;
        IdleManager.UpdateActivity("@IdleSellItem", amount, item.displayName);
        im.SetQuickerUpdateTimer();
        GamePlayer.current.AddAutopilotStat(IdleStat.MaterialsSoldValue, value * amount);
        return true;
    }

    // Dock path for one fleet ship; returns false when a stack the rules wanted deposited did not fit (vanilla's "storage full" condition).
    public static bool DepositFleetShip(SpaceStation ss, SpaceShipData ship)
    {
        var materials = ss.materialStorage;
        var armory = GamePlayer.current.globalInventory;
        bool clear = true;
        int moved = 0;
        // Same in-place walk as SpaceShipData.TransferCargo; Set(slot, null) is safe mid-walk.
        foreach (var stack in ship.cargo.items)
        {
            if (stack.favourite) continue;
            var item = stack.item;
            var rule = ItemRules.Resolve(item);
            if (rule.dockAction == DockAction.Keep || rule.dockAction == DockAction.Sell) continue;   // stays aboard; ECHO's loop sells later
            if (rule.dockAction == DockAction.SellAbove)
            {
                var dest = Cargo.DepositTarget(ss, item);
                if (dest != null)
                {
                    int room = Math.Max(0, rule.keepAmount - dest.GetCount(item));
                    if (room > 0 && Cargo.Move(ship.cargo, dest, item, room) > 0) moved++;
                }
                continue;
            }
            if (item.CanGoInMaterials() && materials.GetSpaceAvailable() >= stack.spaceRequired)
            {
                materials.Add(item, stack.count);
                ship.cargo.Set(stack.slot, null);
                moved++;
            }
            else if (item.CanGoInArmory() && !armory.IsFull(stack.spaceRequired))
            {
                armory.Add(item, stack.count);
                ship.cargo.Set(stack.slot, null);
                moved++;
            }
            else clear = false;
        }
        if (moved > 0)
            Singleton<NotificationManager>.Instance.CreateNotification(Translation.Translate("@SSTransferredCargo")).WithColor(ColorHelper.orange75).Show();
        return clear;
    }
}

[HarmonyPatch]
internal static class EchoCargoPatches
{
    private static EchoCargo F => EchoCargo.Instance;

    // Data items never reach cargo, so the cargo walk below never sees them. Sell one data-inventory stack per tick before
    // any cargo is unloaded, so a full storage can't hold up a sale.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.TryDropCargo))]
    private static bool TryDropCargoPrefix(IdleManager __instance, SpaceStation ss, ref bool __result)
    {
        if (!F.Active) return true;
        try
        {
            if (!F.SellFromData.Value || !ItemRules.HasDockRules) return true;
            foreach (var stack in GamePlayer.current.dataInventory.items)
            {
                int n = CargoRules.DataSell(ss, stack, 0);
                if (n > 0 && CargoRules.Sell(__instance, ss, stack, n)) { __result = true; return false; }
            }
            return true;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoCargoPatches)); return true; }
    }

    // Vanilla body (IdleManager.TryDropCargoOf) with the dock action of each stack's rule applied before the deposit branch.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.TryDropCargoOf))]
    private static bool TryDropCargoOfPrefix(IdleManager __instance, SpaceStation ss, SpaceShipData cargoShip, ref bool __result)
    {
        if (!F.Active) return true;
        try
        {
            if (!ItemRules.HasDockRules) return true;
            var player = GamePlayer.current;
            foreach (var stack in cargoShip.cargo.items)
            {
                if (__instance.ShouldKeepInCargo(cargoShip, stack)) continue;   // vanilla protections always win
                var item = stack.item;
                var rule = ItemRules.Resolve(item);
                if (rule.dockAction != DockAction.Default)
                {
                    var dest = Cargo.DepositTarget(ss, item);
                    var step = CargoRules.Plan(rule, stack.count, dest != null ? dest.GetCount(item) : 0,
                        player.RequiredItemCountForMissions(item), CargoRules.CanSellHere(ss, item));
                    if (step.action == DockAction.Keep) continue;
                    if (step.deposit > 0)
                    {
                        if (CargoRules.DepositUpTo(__instance, ss, cargoShip, item, step.deposit) > 0) { __result = true; return false; }
                        // Storage full: the vanilla branch below reports it (or sells the cheapest stored material when auto-sell is on).
                    }
                    else if (step.sell > 0)
                    {
                        if (CargoRules.Sell(__instance, ss, stack, step.sell)) { __result = true; return false; }
                        continue;   // refused this tick; the stack stays aboard
                    }
                }
                if (item.CanGoInMaterials())
                {
                    if (ss.materialStorage.IsFull(item.m3))
                    {
                        if (player.autopilotSettings.autoSell && __instance.SellItemFromMaterials(ss, item.m3)) { __result = true; return false; }
                        IdleManager.UpdateActivity("@IdleMaterialStorageFull");
                        __result = true;
                        return false;
                    }
                    __instance.DropFoundItem(ss, cargoShip, item);
                    __result = true;
                    return false;
                }
                if (item.CanGoInArmory())
                {
                    if (player.globalInventory.IsFull(item.m3))
                    {
                        IdleManager.UpdateActivity("@IdleArmoryStorageFull");
                        __result = true;
                        return false;
                    }
                    __instance.DropFoundItem(ss, cargoShip, item);
                    __result = true;
                    return false;
                }
            }
            __result = false;
            return false;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoCargoPatches)); return true; }
    }

    // Fleet auto-deposit on dock (not the manual Deposit button, which calls DepositFleetCargo directly).
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerCargo), nameof(PlayerCargo.AutoDepositOnDock))]
    private static bool AutoDepositOnDockPrefix()
    {
        if (!F.Active) return true;
        try
        {
            if (!F.ApplyToAutoDeposit.Value || !ItemRules.HasDockRules) return true;
            var player = GamePlayer.current;
            if (!player.autoDepositFleetCargo) return false;
            if (!(player.currentPointOfInterest is SpaceStation ss)) return false;
            bool clear = true;
            foreach (var ship in new List<SpaceShipData>(PlayerCargo.GetManagedFleetShips())) clear &= CargoRules.DepositFleetShip(ss, ship);
            if (!clear) InventoryInteractionManager.ShowRedNotification("@FleetCargoStorageFull");
            return false;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoCargoPatches)); return true; }
    }
}
