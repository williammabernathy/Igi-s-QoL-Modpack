using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Gameplay;
using Behaviour.Item;
using Behaviour.Item.Usable;
using Behaviour.UI;
using Behaviour.UI.NotificationAlert;
using Behaviour.UI.Spacestation;
using Behaviour.Unit;
using Behaviour.Util;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Features.Loadouts;
using IgiQoL.Game;
using IgiQoL.Rules;
using LightJson;
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

    public ConfigEntry<bool> ApplyToAutoDeposit, SellFromData, SellFromStorage, SellFromArmory;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        ApplyToAutoDeposit = cfg.Bind(Id, "ApplyToAutoDeposit", true, SettingInfo.Describe("Apply when fleet cargo auto-deposits on dock",
            "With the game's fleet auto-deposit on, fleet holds are emptied the moment you dock. On: your rules apply there too: Keep aboard stays aboard, Sell stays aboard until ECHO unloads it, Sell above N deposits up to N. When you dock yourself nothing is sold, unless Station services' \"Sell by item rules when you dock\" is on or you press the airlock's Sell by rules button. Off: the game deposits everything as usual, so fleet-ship rules only matter with auto-deposit off."));
        SellFromData = cfg.Bind(Id, "SellFromData", true, SettingInfo.Describe("Sell from the data inventory",
            "Data items (intel and records, blueprint pieces, jumpgate passes, claims) never sit in cargo: the game puts them straight into your data inventory. On: a Sell or Sell above N rule that names the item or its category sells them from there when ECHO unloads, when you dock with Station services' \"Sell by item rules when you dock\" on, and with the airlock's Sell by rules button. Sell above N keeps N there. The default rule and the any-category \"rarity and below\" rules never sell from the data inventory. Favourites and items your missions need stay. Off: data items are never sold."));
        SellFromStorage = cfg.Bind(Id, "SellFromStorage", false, SettingInfo.Describe("Sell from station storage",
            "Material storage is kept per station. On: a Sell or Sell above N rule that names the item or its category also sells what is already stored at the station you're docked at: when ECHO unloads (after the cargo and data inventory sales, before any deposit), when you dock with Station services' \"Sell by item rules when you dock\" on, and with the airlock's Sell by rules button. Sell above N keeps N in that station's storage. The default rule and the any-category \"rarity and below\" rules never sell from storage. Favourites and items your missions need stay. Off: stored materials are never sold by rules."));
        SellFromArmory = cfg.Bind(Id, "SellFromArmory", false, SettingInfo.Describe("Sell from the armory",
            "The armory holds the weapons, modules, boosters, ammo and usables you or ECHO stored. On: a Sell or Sell above N rule that names the item or its category also sells from the armory: when ECHO unloads (last, after cargo, the data inventory and station storage), when you dock with Station services' \"Sell by item rules when you dock\" on, and with the airlock's Sell by rules button. Sell above N keeps N in the armory. The default rule and the any-category \"rarity and below\" rules never sell from the armory. Kept: favourites, items your missions need, the usables the game keeps aboard, warp fuel, every ammo type a ship of your fleet fires, and the exact pieces any saved loadout uses. Off: nothing is sold from the armory."));
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
        CheckSellFirst(ctx);
        CheckDataPlan(ctx);
        CheckDataEcho(ctx);
        CheckStorageEcho(ctx);
        CheckArmoryKeeps(ctx);
        CheckArmoryEcho(ctx);
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
        var i = CargoRules.Plan(sellAbove, 8, 7, 2, true);
        var j = CargoRules.Plan(sellAbove, 2, 7, 0, true);
        var k = CargoRules.Plan(sellAbove, 8, 7, 0, false);
        bool ok = a.action == DockAction.SellAbove && a.deposit == 3 && a.sell == 5
            && b.action == DockAction.SellAbove && b.sell == 5 && b.deposit == 0
            && c.action == DockAction.Default
            && d.action == DockAction.Sell && d.sell == 3
            && e.action == DockAction.Keep
            && f.action == DockAction.Default
            && g.action == DockAction.Keep
            && h.action == DockAction.Default
            && i.deposit == 3 && i.sell == 3
            && j.deposit == 2 && j.sell == 0
            && k.deposit == 3 && k.sell == 0;
        string detail = $"above(8,7)={Show(a)} above(5,10)={Show(b)} above-unsellable={Show(c)} sell(5,res2)={Show(d)} sell(2,res2)={Show(e)} sell-unsellable={Show(f)} keep={Show(g)} default={Show(h)} above(8,7,res2)={Show(i)} above(2,7)={Show(j)} above(8,7)-unsellable={Show(k)}";
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

    // Two sellable ores no cargo ship carries, so the check's stacks are the only ones its rules touch.
    private static void TwoTestOres(out InventoryItemType first, out InventoryItemType second)
    {
        first = second = null;
        var player = GamePlayer.current;
        foreach (var t in InventoryItemType.all)
        {
            if (t.itemCategory != ItemCategory.Ore || !t.canSell || t.sellValue <= 0 || player.RequiredItemCountForMissions(t) > 0) continue;
            bool carried = false;
            foreach (var s in player.GetCargoShips()) carried |= s.cargo.GetCount(t) > 0;
            if (carried) continue;
            if (first == null) first = t;
            else { second = t; return; }
        }
    }

    // Sell before deposit: with a "Game default" stack ahead of it, ECHO's first unload step sells the part of a Sell above N
    // stack above N instead of depositing anything.
    private static void CheckSellFirst(SelfCheckContext ctx)
    {
        var ss = SpaceStation.current;
        if (ss == null || SpaceStationInterior.instance == null || !Singleton<IdleManager>.HasInstance)
        {
            ctx.Skip("sell-first", "not docked");
            return;
        }
        TwoTestOres(out var deposit, out var sold);
        if (sold == null || !CargoRules.CanSellHere(ss, sold))
        {
            ctx.Skip("sell-first", sold == null ? "fewer than two sellable ore types the fleet doesn't carry" : "station has no shop");
            return;
        }
        var player = GamePlayer.current;
        var cargo = player.currentSpaceShip.cargo;
        var storeA = Cargo.DepositTarget(ss, deposit);
        var storeB = Cargo.DepositTarget(ss, sold);
        int storedA0 = storeA.GetCount(deposit), storedB0 = storeB.GetCount(sold);
        long credits0 = player.credits;
        try
        {
            cargo.Add(deposit, 5);
            cargo.Add(sold, 5);
            int slotA = -1, slotB = -1;
            foreach (var s in cargo.items)
            {
                if (s.item == deposit) slotA = s.slot;
                if (s.item == sold) slotB = s.slot;
            }
            if (cargo.GetCount(deposit) != 5 || cargo.GetCount(sold) != 5 || slotA >= slotB)
            {
                ctx.Skip("sell-first", $"test stacks didn't land in order (slots {slotA}, {slotB}; flagship hold full?)");
                return;
            }
            ItemRules.UseForTest("{\"items\":{\"" + Items.RuleKey(sold) + "\":{\"dockAction\":\"SellAbove\",\"keepAmount\":" + (storedB0 + 2) + "}}}");
            bool result = Singleton<IdleManager>.Instance.TryDropCargo(ss);
            string detail = $"default {Items.RuleKey(deposit)} slot {slotA}: cargo 5->{cargo.GetCount(deposit)} stored {storedA0}->{storeA.GetCount(deposit)}; "
                + $"above N {Items.RuleKey(sold)} slot {slotB}: cargo 5->{cargo.GetCount(sold)} stored {storedB0}->{storeB.GetCount(sold)} "
                + $"credits +{player.credits - credits0} (expected {sold.sellValue * 3}) result={result}";
            if (result && cargo.GetCount(deposit) == 5 && storeA.GetCount(deposit) == storedA0 && cargo.GetCount(sold) == 2
                && storeB.GetCount(sold) == storedB0 && player.credits - credits0 == sold.sellValue * 3) ctx.Pass("sell-first", detail);
            else ctx.Fail("sell-first", detail);
        }
        finally
        {
            ItemRules.Load();
            cargo.Remove(deposit, cargo.GetCount(deposit));
            cargo.Remove(sold, cargo.GetCount(sold));
            storeA.Remove(deposit, storeA.GetCount(deposit) - storedA0);
            storeB.Remove(sold, storeB.GetCount(sold) - storedB0);
            player.credits = credits0;
        }
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
        int a = CargoRules.StoredSellCount(sell, 5, 2), b = CargoRules.StoredSellCount(sell, 2, 2), c = CargoRules.StoredSellCount(above, 5, 0),
            d = CargoRules.StoredSellCount(above, 5, 4), e = CargoRules.StoredSellCount(above, 2, 0),
            f = CargoRules.StoredSellCount(new ItemRule { dockAction = DockAction.Keep }, 5, 0), g = CargoRules.StoredSellCount(new ItemRule(), 5, 0);
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
                if (s.item == item) left += CargoRules.StoredSell(ss, s, 0);
            // An any-category rule resolves to Sell for the item but never reaches the data inventory.
            ItemRules.UseForTest("{\"rarityUpTo\":{\"Legendary\":{\"dockAction\":\"Sell\"}}}");
            int catchAll = 0;
            foreach (var s in data.items)
                if (s.item == item) catchAll += CargoRules.StoredSell(ss, s, 0);
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

    // ECHO's unload sells from this station's material storage with SellFromStorage on: Sell above N keeps N there, a
    // favourite stack stays, an any-category rule sells nothing.
    private static void CheckStorageEcho(SelfCheckContext ctx)
    {
        var ss = SpaceStation.current;
        if (ss == null || SpaceStationInterior.instance == null || !Singleton<IdleManager>.HasInstance)
        {
            ctx.Skip("storage-echo", "not docked");
            return;
        }
        TwoTestOres(out var ore, out _);
        if (ore == null || !CargoRules.CanSellHere(ss, ore) || Cargo.DepositTarget(ss, ore) != ss.materialStorage)
        {
            ctx.Skip("storage-echo", ore == null ? "no sellable ore type the fleet doesn't carry" : "station has no shop, or ore doesn't go to material storage");
            return;
        }
        var player = GamePlayer.current;
        var storage = ss.materialStorage;
        var setting = Instance.SellFromStorage;
        bool setting0 = setting.Value;
        int count0 = storage.GetCount(ore);
        long credits0 = player.credits;
        try
        {
            setting.Value = true;
            storage.Add(ore, 5);
            int held = storage.GetCount(ore);
            int keep = Math.Max(held - 3, player.RequiredItemCountForMissions(ore));
            int expect = held - keep;
            string key = Items.RuleKey(ore);
            ItemRules.UseForTest("{\"items\":{\"" + key + "\":{\"dockAction\":\"SellAbove\",\"keepAmount\":" + keep + "}}}");
            long before = player.credits;
            bool result = Singleton<IdleManager>.Instance.TryDropCargo(ss);
            int after = storage.GetCount(ore);
            long earned = player.credits - before;
            int left = 0, favLeft = 0, catchAll = 0;
            foreach (var s in storage.items)
                if (s.item == ore) left += CargoRules.StoredSell(ss, s, 0);
            // A favourite stack stays even under a plain Sell rule.
            ItemRules.UseForTest("{\"items\":{\"" + key + "\":{\"dockAction\":\"Sell\"}}}");
            var marked = new List<Inventory.InventoryItem>();
            foreach (var s in storage.items)
                if (s.item == ore && !s.favourite) { s.favourite = true; marked.Add(s); }
            foreach (var s in storage.items)
                if (s.item == ore) favLeft += CargoRules.StoredSell(ss, s, 0);
            foreach (var s in marked) s.favourite = false;
            // An any-category rule resolves to Sell for the ore but never reaches station storage.
            ItemRules.UseForTest("{\"rarityUpTo\":{\"Legendary\":{\"dockAction\":\"Sell\"}}}");
            foreach (var s in storage.items)
                if (s.item == ore) catchAll += CargoRules.StoredSell(ss, s, 0);
            bool resolves = ItemRules.Resolve(ore).dockAction == DockAction.Sell;
            string detail = $"{key} keep={keep} result={result} stored {held}->{after} credits +{earned} (expected {ore.sellValue * expect}) left={left} favourite={favLeft} catchAll={catchAll} resolvesSell={resolves}";
            if (result && expect > 0 && after == keep && earned == ore.sellValue * expect && left == 0 && favLeft == 0 && catchAll == 0 && resolves) ctx.Pass("storage-echo", detail);
            else ctx.Fail("storage-echo", detail);
        }
        finally
        {
            setting.Value = setting0;
            ItemRules.Load();
            storage.Remove(ore, storage.GetCount(ore) - count0);
            player.credits = credits0;
        }
    }

    internal const string TestLoadout = "igiqol-selfcheck";

    private static bool Carried(InventoryItemType t)
    {
        foreach (var s in Fleet.Ships(withMercenaries: true))
            if (s.cargo.GetCount(t) > 0) return true;
        return false;
    }

    internal static void RestoreCount(Inventory inv, InventoryItemType t, int count0)
    {
        int d = inv.GetCount(t) - count0;
        if (d > 0) inv.Remove(t, d);
        else if (d < 0) inv.Add(t, -d);
    }

    // Sellable ammo no ship of the fleet fires or carries, preferring a type the armory doesn't hold. Null while a fleet
    // ship isn't spawned, because ArmoryKeeps then keeps every ammo type.
    internal static InventoryItemType UnfiredAmmo()
    {
        var player = GamePlayer.current;
        var units = new List<SpaceShip>();
        foreach (var s in Fleet.Ships(withMercenaries: false))
        {
            var u = Fleet.Unit(s);
            if (u == null) return null;
            units.Add(u);
        }
        InventoryItemType any = null;
        foreach (var t in InventoryItemType.all)
        {
            if (t.itemCategory != ItemCategory.Ammo || t.identifier == null || !t.canSell || t.sellValue <= 0 || player.RequiredItemCountForMissions(t) > 0 || Carried(t)) continue;
            bool fired = false;
            foreach (var u in units) fired |= u.DoWeUseAmmo(t);
            if (fired) continue;
            if (player.globalInventory.GetCount(t) == 0) return t;
            any ??= t;
        }
        return any;
    }

    // Sellable ammo the flagship fires that no other ship carries, so a Sell rule on it can only reach the armory.
    internal static InventoryItemType FiredAmmo()
    {
        var player = GamePlayer.current;
        var unit = Fleet.Flagship;
        if (unit == null) return null;
        foreach (var t in InventoryItemType.all)
        {
            if (t.itemCategory != ItemCategory.Ammo || t.identifier == null || !t.canSell || t.sellValue <= 0 || !unit.DoWeUseAmmo(t)) continue;
            bool elsewhere = false;
            foreach (var s in Fleet.Ships(withMercenaries: true))
                elsewhere |= s != player.currentSpaceShip && s.cargo.GetCount(t) > 0;
            if (!elsewhere) return t;
        }
        return null;
    }

    // A sellable equipment type (sellable at ss when given) that no saved loadout names, the armory doesn't hold and no ship carries.
    internal static InventoryItemType TestPiece(SpaceStation ss, InventoryItemType besides)
    {
        var player = GamePlayer.current;
        HashSet<string> pieces = null;
        foreach (var t in InventoryItemType.all)
        {
            if (t == besides || t.equipmentDefinition == null || t.identifier == null || !t.canSell || t.sellValue <= 0 || !t.CanGoInArmory()
                || ss != null && !CargoRules.CanSellHere(ss, t) || player.RequiredItemCountForMissions(t) > 0 || player.globalInventory.GetCount(t) > 0
                || Carried(t) || CargoRules.ArmoryKeeps(t, ref pieces)) continue;
            return t;
        }
        return null;
    }

    internal static void PutTestLoadout(InventoryItemType piece)
    {
        var ship = GamePlayer.current.currentSpaceShip;
        LoadoutStore.Put(ship.guid, new Loadout { name = TestLoadout, hull = ship.shipClass.identifier, hardpoints = new JsonValue[] { piece.ToJson() } });
    }

    internal static void DeleteTestLoadout() => LoadoutStore.Delete(GamePlayer.current.currentSpaceShip.guid, TestLoadout);

    // The armory guard on its own: a usable the game keeps aboard, ammo the flagship fires and a piece a saved loadout
    // names stay; ammo the fleet doesn't fire and gear no loadout names don't.
    private static void CheckArmoryKeeps(SelfCheckContext ctx)
    {
        InventoryItemType usable = null;
        foreach (var t in InventoryItemType.all)
            if (t.itemCategory == ItemCategory.Usable && t.storageOverride == StorageOverride.None) { usable = t; break; }
        var fired = FiredAmmo();
        var unfired = UnfiredAmmo();
        var piece = TestPiece(null, null);
        var other = piece != null ? TestPiece(null, piece) : null;
        if (usable == null || fired == null || unfired == null || other == null)
        {
            ctx.Skip("armory-keeps", $"test items missing: usable={usable != null} fired={fired != null} unfired={unfired != null} pieces={(piece != null ? 1 : 0) + (other != null ? 1 : 0)}");
            return;
        }
        try
        {
            PutTestLoadout(piece);
            HashSet<string> pieces = null;
            bool u = CargoRules.ArmoryKeeps(usable, ref pieces), f = CargoRules.ArmoryKeeps(fired, ref pieces), n = CargoRules.ArmoryKeeps(unfired, ref pieces),
                p = CargoRules.ArmoryKeeps(piece, ref pieces), o = CargoRules.ArmoryKeeps(other, ref pieces);
            string detail = $"usable {Items.RuleKey(usable)}={u} fired {Items.RuleKey(fired)}={f} unfired {Items.RuleKey(unfired)}={n} loadout piece {Items.RuleKey(piece)}={p} other {Items.RuleKey(other)}={o}";
            if (u && f && !n && p && !o) ctx.Pass("armory-keeps", detail);
            else ctx.Fail("armory-keeps", detail);
        }
        finally { DeleteTestLoadout(); }
    }

    // ECHO's unload sells from the armory with SellFromArmory on: Sell above N keeps N there, and a Sell rule on ammo the
    // flagship fires sells none of it, though that stack sits ahead in the armory.
    private static void CheckArmoryEcho(SelfCheckContext ctx)
    {
        var ss = SpaceStation.current;
        if (ss == null || SpaceStationInterior.instance == null || !Singleton<IdleManager>.HasInstance)
        {
            ctx.Skip("armory-echo", "not docked");
            return;
        }
        var ammo = UnfiredAmmo();
        if (ammo == null || !CargoRules.CanSellHere(ss, ammo))
        {
            ctx.Skip("armory-echo", ammo == null ? "no sellable ammo the fleet doesn't fire or carry (or a fleet ship isn't spawned)" : "station has no shop");
            return;
        }
        var fired = FiredAmmo();
        var player = GamePlayer.current;
        var armory = player.globalInventory;
        var setting = Instance.SellFromArmory;
        bool setting0 = setting.Value;
        int ammo0 = armory.GetCount(ammo), fired0 = fired != null ? armory.GetCount(fired) : 0;
        long credits0 = player.credits;
        try
        {
            setting.Value = true;
            if (fired != null) armory.Add(fired, 5);
            armory.Add(ammo, 5);
            int held = armory.GetCount(ammo), keep = held - 3, firedHeld = fired != null ? armory.GetCount(fired) : 0;
            string key = Items.RuleKey(ammo);
            string rules = "\"" + key + "\":{\"dockAction\":\"SellAbove\",\"keepAmount\":" + keep + "}";
            if (fired != null) rules += ",\"" + Items.RuleKey(fired) + "\":{\"dockAction\":\"Sell\"}";
            ItemRules.UseForTest("{\"items\":{" + rules + "}}");
            long before = player.credits;
            bool result = Singleton<IdleManager>.Instance.TryDropCargo(ss);
            int after = armory.GetCount(ammo), firedAfter = fired != null ? armory.GetCount(fired) : 0;
            long earned = player.credits - before;
            string detail = $"{key} keep={keep} result={result} held {held}->{after} credits +{earned} (expected {ammo.sellValue * 3}) "
                + (fired != null ? $"fired {Items.RuleKey(fired)} {firedHeld}->{firedAfter}" : "no fired ammo to test");
            if (result && after == keep && earned == ammo.sellValue * 3 && firedAfter == firedHeld) ctx.Pass("armory-echo", detail);
            else ctx.Fail("armory-echo", detail);
        }
        finally
        {
            setting.Value = setting0;
            ItemRules.Load();
            RestoreCount(armory, ammo, ammo0);
            if (fired != null) RestoreCount(armory, fired, fired0);
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
                // The part above N (and above the missions' reserve) sells at once, beside the deposit up to N, so a full
                // storage only ever holds up the deposit.
                int room = Math.Max(0, rule.keepAmount - stored);
                int sell = sellable ? count - room - reserved : 0;
                if (room > 0) return new Step { action = DockAction.SellAbove, deposit = Math.Min(room, count), sell = Math.Max(0, sell) };
                if (!sellable) return default;
                return sell > 0 ? new Step { action = DockAction.SellAbove, sell = sell } : new Step { action = DockAction.Keep };
            }
            default:
                return default;
        }
    }

    // Pure. Stored items (data inventory, station storage, armory) are already in their storage, so Sell above N keeps N there.
    // held: everything of the type in that storage, favourites included; reserved: GamePlayer.RequiredItemCountForMissions.
    public static int StoredSellCount(ItemRule rule, int held, int reserved)
    {
        int keep = rule.dockAction == DockAction.Sell ? reserved
            : rule.dockAction == DockAction.SellAbove ? Math.Max(rule.keepAmount, reserved)
            : held;
        return Math.Max(0, held - keep);
    }

    // How many of this stored stack (data inventory, station storage or armory) the rules sell here. Only a rule that names
    // the item or its category counts. Favourite stacks are never sold but count toward N. quoted: amount of the type
    // earlier stacks of the same storage were already quoted for. The armory adds ArmoryKeeps.
    public static int StoredSell(SpaceStation ss, Inventory.InventoryItem stack, int quoted)
    {
        if (stack.favourite) return 0;
        var item = stack.item;
        var rule = ItemRules.Explicit(item);
        if (rule == null || !CanSellHere(ss, item)) return 0;
        return Math.Min(stack.count, StoredSellCount(rule, stack.inventory.GetCount(item) - quoted, GamePlayer.current.RequiredItemCountForMissions(item)));
    }

    public static bool CanSellHere(SpaceStation ss, InventoryItemType item) =>
        ss.shopInventory != null && SpaceStationInterior.instance != null
        && item.canSell && item.sellValue > 0 && !RefinedMaterialExtensions.IsCanister(item);

    // Armory items the rules never sell, on top of StoredSell's guards: the usables the game keeps aboard, warp fuel, ammo a
    // ship of your fleet fires (every ammo type while one of them isn't spawned) and the exact pieces a saved loadout names
    // (LoadoutApply.Identity). pieces: those identities, built on first need and reused for the rest of one pass.
    public static bool ArmoryKeeps(InventoryItemType item, ref HashSet<string> pieces)
    {
        if (item.itemCategory == ItemCategory.Usable && item.storageOverride == StorageOverride.None || item.TryGetState<WarpFuelState>(out _)) return true;
        if (item.itemCategory == ItemCategory.Ammo)
        {
            foreach (var s in Fleet.Ships(withMercenaries: false))
            {
                var unit = Fleet.Unit(s);
                if (unit == null || unit.DoWeUseAmmo(item)) return true;
            }
            return false;
        }
        if (item.equipmentDefinition == null) return false;
        pieces ??= LoadoutPieces();
        return pieces.Contains(LoadoutApply.Identity(item.ToJson()));
    }

    private static HashSet<string> LoadoutPieces()
    {
        var set = new HashSet<string>();
        foreach (var ship in GamePlayer.current.spaceShips)
            foreach (var l in LoadoutStore.For(ship.guid))
            {
                foreach (var v in l.hardpoints) if (!v.IsNull) set.Add(LoadoutApply.Identity(v));
                foreach (var v in l.modules.Values) if (!v.IsNull) set.Add(LoadoutApply.Identity(v));
                foreach (var v in l.boosters) if (!v.IsNull) set.Add(LoadoutApply.Identity(v));
            }
        return set;
    }

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

    // The rule's step for one cargo stack. Stacks vanilla keeps aboard come back as Keep, "Game default" as Default.
    private static CargoRules.Step StepFor(IdleManager im, SpaceStation ss, SpaceShipData ship, Inventory.InventoryItem stack)
    {
        if (im.ShouldKeepInCargo(ship, stack)) return new CargoRules.Step { action = DockAction.Keep };   // vanilla protections always win
        var item = stack.item;
        var rule = ItemRules.Resolve(item);
        if (rule.dockAction == DockAction.Default) return default;
        var dest = Cargo.DepositTarget(ss, item);
        return CargoRules.Plan(rule, stack.count, dest != null ? dest.GetCount(item) : 0,
            GamePlayer.current.RequiredItemCountForMissions(item), CargoRules.CanSellHere(ss, item));
    }

    // Sell before deposit: one sale per tick, from every cargo ship's hold, then the data inventory, then this station's
    // storage, then the armory, ahead of the vanilla unload walk. That walk stops at the first stack a full storage can't
    // take, so a full storage only ever holds up deposits.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.TryDropCargo))]
    private static bool TryDropCargoPrefix(IdleManager __instance, SpaceStation ss, ref bool __result)
    {
        if (!F.Active) return true;
        try
        {
            if (!ItemRules.HasDockRules) return true;
            foreach (var ship in GamePlayer.current.GetCargoShips())
                foreach (var stack in ship.cargo.items)
                {
                    int n = StepFor(__instance, ss, ship, stack).sell;
                    if (n > 0 && CargoRules.Sell(__instance, ss, stack, n)) { __result = true; return false; }
                }
            // Data items never reach cargo, and stored materials and gear are already off the ships, so they get their own walks.
            if (F.SellFromData.Value && SellOneStored(__instance, ss, GamePlayer.current.dataInventory)
                || F.SellFromStorage.Value && SellOneStored(__instance, ss, ss.materialStorage)
                || F.SellFromArmory.Value && SellOneStored(__instance, ss, GamePlayer.current.globalInventory))
            {
                __result = true;
                return false;
            }
            return true;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoCargoPatches)); return true; }
    }

    private static bool SellOneStored(IdleManager im, SpaceStation ss, Inventory storage)
    {
        bool armory = storage == GamePlayer.current.globalInventory;
        HashSet<string> pieces = null;
        foreach (var stack in storage.items)
        {
            int n = CargoRules.StoredSell(ss, stack, 0);
            if (n > 0 && !(armory && CargoRules.ArmoryKeeps(stack.item, ref pieces)) && CargoRules.Sell(im, ss, stack, n)) return true;
        }
        return false;
    }

    // Vanilla body (IdleManager.TryDropCargoOf) with the dock action of each stack's rule applied before the deposit branch.
    // Sales already ran in TryDropCargoPrefix.
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
                var step = StepFor(__instance, ss, cargoShip, stack);
                if (step.action == DockAction.Keep) continue;
                var item = stack.item;
                if (step.deposit > 0)
                {
                    if (CargoRules.DepositUpTo(__instance, ss, cargoShip, item, step.deposit) > 0) { __result = true; return false; }
                    // Storage full: the vanilla branch below reports it (or sells the cheapest stored material when auto-sell is on).
                }
                else if (step.sell > 0) continue;   // the sell pass was refused this tick; the stack stays aboard
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
