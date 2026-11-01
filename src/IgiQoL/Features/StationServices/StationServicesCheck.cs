using System.Collections;
using System.Linq;
using Behaviour.Item.Builder;
using Behaviour.Item.Usable;
using Behaviour.Item;
using Behaviour.Unit;
using Behaviour.Equipment.Turret;
using Behaviour.UI.Spacestation;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.Rules;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.Galaxy.POI.Station;
using Source.Item;
using Source.Player;
using Source.SpaceShip;
using Source.Util;
using UnityEngine;
using CrewType = Behaviour.Crew.CrewType;
using StationHangar = Source.Spacestation.PersonalHangar;

namespace IgiQoL.Features.StationServices;

internal static class StationServicesCheck
{
    public static IEnumerator Run(SelfCheckContext ctx)
    {
        if (SpaceStationInterior.instance == null && Behaviour.Managers.BasePoiManager.current is SpacestationExteriorManager exterior)
        {
            exterior.EnterSpacestation();
            float wait = Time.realtimeSinceStartup + 30f;
            while (SpaceStationInterior.instance == null && Time.realtimeSinceStartup < wait) yield return null;
            yield return new WaitForSeconds(3f);
            if (SpaceStationInterior.instance != null) ctx.Cleanup(() => { if (SpaceStationInterior.instance != null) SpaceStationInterior.instance.ExitSpacestation(); });
        }
        if (Services.Docked() == null)
        {
            ctx.Skip("docked", "could not dock at " + MapPointOfInterest.current?.name);
            yield break;
        }
        var player = GamePlayer.current;
        long credits0 = player.credits;
        ctx.Cleanup(() => player.credits = credits0);
        // Cleanups run in the order they were added, so crew and brigs are snapshotted once, before any check touches them.
        foreach (var s in Services.FleetShips()) KeepCrew(ctx, s);
        KeepBrigs(ctx);

        yield return CheckRepair(ctx);
        CheckRestock(ctx);
        CheckFuel(ctx);
        CheckSell(ctx);
        CheckSellData(ctx);
        CheckSellStorage(ctx);
        CheckCrewTrack(ctx);
        CheckCrew(ctx);
        CheckBrig(ctx);
        yield return CheckOnDock(ctx);
        yield return CheckAirlock(ctx);
    }

    private static IEnumerator CheckRepair(SelfCheckContext ctx)
    {
        var ss = Services.Docked();
        string reason = Services.RepairUnavailable(ss);
        if (reason != null)
        {
            ctx.Skip("repair", reason);
            yield break;
        }
        var player = GamePlayer.current;
        var ship = player.currentSpaceShip;
        ctx.Cleanup(() => { ship.RepairHullHp(ship.maxHullHP); ship.RepairArmorHP(ship.maxArmorHP); });
        foreach (var job in StationHangar.current.jobs.ToArray())
            if (job.spaceshipGuid == ship.guid) StationHangar.current.CancelJob(job);
        ship.currentHullHP = ship.maxHullHP * 0.6f;
        int amount = Services.RepairAmount(ship);
        int cost = Services.RepairCost(amount);

        var quote = Services.Repair(false);
        long before = player.credits;
        var run = Services.Repair(true);
        RepairJobInfo(ship, out bool paid, out float perUnit);
        string detail = $"amount={amount} cost={cost} quote={quote.count}/{quote.credits} run={run.count}/{run.credits} credits {before}->{player.credits} paidJob={paid} costPerUnit={perUnit:F2} lines=[{string.Join("; ", run.lines)}] problems=[{string.Join("; ", run.problems)}]";
        if (quote.credits == run.credits && run.count >= 1 && paid && before - player.credits == run.credits && run.lines.Exists(l => l.Contains(amount + " damage"))) ctx.Pass("repair", detail);
        else ctx.Fail("repair", detail);

        var again = Services.Repair(false);
        if (!again.lines.Exists(l => l.StartsWith(ship.GetShipName() + ":"))) ctx.Pass("repair-once", $"second quote count={again.count}");
        else ctx.Fail("repair-once", "flagship quoted again while its paid repair runs");

        float hull0 = ship.currentHullHP;
        yield return new WaitForSeconds(3f);
        detail = $"hull {hull0:F0}->{ship.currentHullHP:F0} of {ship.maxHullHP:F0}";
        if (ship.currentHullHP > hull0) ctx.Pass("repair-progress", detail);
        else ctx.Fail("repair-progress", detail);
    }

    private static void RepairJobInfo(SpaceShipData ship, out bool paid, out float perUnit)
    {
        paid = false;
        perUnit = 0f;
        foreach (var job in StationHangar.current.jobs)
            if (job.spaceshipGuid == ship.guid && !job.autoRepair) { paid = true; perUnit = job.costPerUnit; }
    }

    private static InventoryItemType AmmoOf(SpaceShip unit)
    {
        if (unit.torpedoBayModule != null && unit.torpedoBayModule.ammoType != null) return unit.torpedoBayModule.ammoType;
        foreach (var t in unit.GetComponentsInChildren<AbstractTurret>())
            if (t.ammoType != null) return t.ammoType;
        return null;
    }

    private static void CheckRestock(SelfCheckContext ctx)
    {
        SpaceShipData ship = null;
        SpaceShip unit = null;
        InventoryItemType ammo = null;
        string seen = "";
        // Prefer a fleet ship over the flagship: vanilla ECHO only restocks the flagship.
        foreach (var s in Services.FleetShips())
        {
            var u = Fleet.Unit(s);
            var a = u != null ? AmmoOf(u) : null;
            seen += $"{s.GetShipName()}(unit={(u != null)}, ammo={(a != null ? Translation.Translate(a.displayName) : "none")}) ";
            if (a == null || ship != null && ship != GamePlayer.current.currentSpaceShip) continue;
            ship = s; unit = u; ammo = a;
        }
        if (ammo == null)
        {
            ctx.Skip("restock", "no fleet ship uses ammo: " + seen);
            return;
        }
        var armory = GamePlayer.current.globalInventory;
        int cargo0 = ship.cargo.GetCount(ammo), armory0 = armory.GetCount(ammo);
        ctx.Cleanup(() =>
        {
            ship.cargo.Remove(ammo, ship.cargo.GetCount(ammo));
            if (cargo0 > 0) ship.cargo.Add(ammo, cargo0);
            armory.Remove(ammo, armory.GetCount(ammo));
            if (armory0 > 0) armory.Add(ammo, armory0);
        });
        ship.cargo.Remove(ammo, cargo0);
        armory.Remove(ammo, armory0);
        armory.Add(ammo, 10);
        unit.GetAmmoTypesRequired().TryGetValue(ammo, out int need);
        var quote = Services.Restock(false);
        var run = Services.Restock(true);
        int loaded = ship.cargo.GetCount(ammo);
        int armoryLeft = armory.GetCount(ammo);
        string detail = $"{ship.GetShipName()} {ammo.displayName}: need={need} quote={quote.count}/{quote.credits} run={run.count}/{run.credits} cargo 0->{loaded} armory 10->{armoryLeft} lines=[{string.Join("; ", run.lines)}] problems=[{string.Join("; ", run.problems)}] fleet: {seen}";
        bool armoryFirst = armoryLeft == Mathf.Max(0, 10 - need);
        bool full = loaded == need && quote.credits == run.credits;
        bool shortButExplained = loaded == Mathf.Min(10, need) && run.problems.Count > 0;
        if (need > 0 && armoryFirst && (full || shortButExplained)) ctx.Pass("restock", detail);
        else ctx.Fail("restock", detail);
    }

    // Takes every warp fuel stack out of `inv` and returns a cleanup that puts them back.
    private static System.Action StripFuel(Inventory inv)
    {
        var taken = new System.Collections.Generic.List<(InventoryItemType item, int count)>();
        foreach (var stack in inv.items.ToArray())
            if (stack != null && stack.item.TryGetState<WarpFuelState>(out _)) taken.Add((stack.item, stack.count));
        foreach (var (item, count) in taken) inv.Remove(item, count);
        return () =>
        {
            foreach (var stack in inv.items.ToArray())
                if (stack != null && stack.item.TryGetState<WarpFuelState>(out _)) inv.Remove(stack.item, stack.count);
            foreach (var (item, count) in taken) inv.Add(item, count);
        };
    }

    private static float Multiplier(InventoryItemType item) => item.GetState<WarpFuelState>().multiplier;

    private static void CheckFuel(SelfCheckContext ctx)
    {
        var f = StationServices.Instance;
        var player = GamePlayer.current;
        var ss = Services.Docked();
        var ship = player.currentSpaceShip;
        int cfg0 = f.FuelCells.Value;
        ctx.Cleanup(() => f.FuelCells.Value = cfg0);
        ctx.Cleanup(StripFuel(ship.cargo));
        ctx.Cleanup(StripFuel(player.globalInventory));

        // Every shop's fuel, and the best kind on sale; a station without any gets 5 Ion cells in its first shop.
        ShopInventory firstShop = null;
        float best = 0f;
        int onSale = 0;
        foreach (var shop in new ShopInventory[] { ss.generalShopInventory, ss.miningShopInventory, ss.salvageShopInventory, ss.bountyShopInventory, ss.patrolShopInventory, ss.industryShopInventory, ss.conquestShopInventory })
        {
            if (shop == null) continue;
            firstShop ??= shop;
            foreach (var stack in shop.items)
                if (stack != null && stack.costItem == null && stack.item.TryGetState<WarpFuelState>(out var st) && st.remaining > 0f)
                {
                    onSale += stack.count;
                    best = Mathf.Max(best, st.multiplier);
                }
        }
        if (onSale < 3 && firstShop != null)
        {
            var ion = ItemBuilder.Get("WarpFuel").CreateWarpFuel(WarpFuelItem.WarpFuelType.IonCell);
            firstShop.Add(ion, 5);
            ctx.Cleanup(() => firstShop.Remove(ion, firstShop.GetCount(ion)));
            onSale += 5;
            best = Mathf.Max(best, Multiplier(ion));
        }
        if (onSale < 3)
        {
            ctx.Skip("fuel", $"no shop at {ss.name}");
            return;
        }

        f.FuelCells.Value = 0;
        var off = Services.Restock(false);
        var plasma = ItemBuilder.Get("WarpFuel").CreateWarpFuel(WarpFuelItem.WarpFuelType.PlasmaCell);
        player.globalInventory.Add(plasma, 1);
        f.FuelCells.Value = 4;
        var quote = Services.Restock(false);
        var run = Services.Restock(true);
        int aboard = Services.FuelCells(ship.cargo), armoryLeft = Services.FuelCells(player.globalInventory);
        float bestAboard = 0f;
        foreach (var stack in ship.cargo.items)
            if (stack != null && stack.item.TryGetState<WarpFuelState>(out var st)) bestAboard = Mathf.Max(bestAboard, st.multiplier);
        string summary = StationServices.Summary(null, run, null) ?? "";
        string detail = $"off cells={off.cells}; quote cells={quote.cells} run cells={run.cells} aboard={aboard} armory 1->{armoryLeft} best on sale x{best} best bought x{bestAboard} onSale={onSale} summary='{summary}' lines=[{string.Join("; ", run.lines)}] problems=[{string.Join("; ", run.problems)}]";
        if (off.cells == 0 && quote.cells == 4 && run.cells == 4 && aboard == 4 && armoryLeft == 0 && Mathf.Approximately(bestAboard, best) && summary.Contains("4 warp fuel cells")) ctx.Pass("fuel", detail);
        else ctx.Fail("fuel", detail);

        f.FuelCells.Value = 50;
        var shortQuote = Services.Restock(false);
        detail = $"cells={shortQuote.cells} problems=[{string.Join("; ", shortQuote.problems)}]";
        if (shortQuote.problems.Exists(p => p.Contains("warp fuel cell") && p.Contains("short"))) ctx.Pass("fuel-short", detail);
        else ctx.Fail("fuel-short", detail);
        f.FuelCells.Value = cfg0;
    }

    private static void CheckSell(SelfCheckContext ctx)
    {
        var ss = Services.Docked();
        var ore = TestOre();
        if (ore == null || ss.shopInventory == null)
        {
            ctx.Skip("sell", ore == null ? "no sellable ore type" : "station has no shop");
            return;
        }
        var player = GamePlayer.current;
        var ship = player.currentSpaceShip;
        var storage = Cargo.DepositTarget(ss, ore);
        int cargo0 = ship.cargo.GetCount(ore), stored0 = storage.GetCount(ore), shop0 = ss.shopInventory.GetCount(ore);
        ctx.Cleanup(() =>
        {
            ship.cargo.Remove(ore, ship.cargo.GetCount(ore));
            if (cargo0 > 0) ship.cargo.Add(ore, cargo0);
            storage.Remove(ore, storage.GetCount(ore));
            if (stored0 > 0) storage.Add(ore, stored0);
            ItemRules.Load();
        });
        string key = Items.RuleKey(ore);
        try
        {
            ship.cargo.Add(ore, 7);
            int inCargo = ship.cargo.GetCount(ore);
            ItemRules.UseForTest("{\"items\":{\"" + key + "\":{\"dockAction\":\"Sell\"}}}");
            long before = player.credits;
            var quote = Services.Sell(false);
            var run = Services.Sell(true);
            long expected = ore.sellValue * inCargo;
            string detail = $"{key}: cargo {inCargo}->{ship.cargo.GetCount(ore)} credits +{player.credits - before} (expected {expected}) quote={quote.credits} shop {shop0}->{ss.shopInventory.GetCount(ore)} lines=[{string.Join("; ", run.lines)}]";
            if (ship.cargo.GetCount(ore) == 0 && player.credits - before == expected && quote.credits == expected && ss.shopInventory.GetCount(ore) >= shop0 + inCargo) ctx.Pass("sell", detail);
            else ctx.Fail("sell", detail);

            // Sell above N: store up to N, sell the rest.
            ship.cargo.Add(ore, 9);
            int keep = storage.GetCount(ore) + 4;
            ItemRules.UseForTest("{\"items\":{\"" + key + "\":{\"dockAction\":\"SellAbove\",\"keepAmount\":" + keep + "}}}");
            int stored = storage.GetCount(ore);
            before = player.credits;
            Services.Sell(true);
            detail = $"keep={keep} stored {stored}->{storage.GetCount(ore)} cargo 9->{ship.cargo.GetCount(ore)} credits +{player.credits - before} (expected {ore.sellValue * 5})";
            if (storage.GetCount(ore) == keep && ship.cargo.GetCount(ore) == 0 && player.credits - before == ore.sellValue * 5) ctx.Pass("sell-above", detail);
            else ctx.Fail("sell-above", detail);

            // Sell above N over two ships: the quote books the first ship's deposit, so it matches what the run earns.
            SpaceShipData wing = null;
            foreach (var s in Services.FleetShips())
                if (s != ship && s.cargo != null) { wing = s; break; }
            if (wing == null) ctx.Skip("sell-above-fleet", "no fleet ship docked with you");
            else
            {
                int wing0 = wing.cargo.GetCount(ore);
                ctx.Cleanup(() => { wing.cargo.Remove(ore, wing.cargo.GetCount(ore)); if (wing0 > 0) wing.cargo.Add(ore, wing0); });
                if (wing0 > 0) wing.cargo.Remove(ore, wing0);
                ship.cargo.Add(ore, 9);
                wing.cargo.Add(ore, 9);
                keep = storage.GetCount(ore) + 4;
                ItemRules.UseForTest("{\"items\":{\"" + key + "\":{\"dockAction\":\"SellAbove\",\"keepAmount\":" + keep + "}}}");
                before = player.credits;
                var fleetQuote = Services.Sell(false);
                var fleetRun = Services.Sell(true);
                long expectedFleet = ore.sellValue * 14;
                detail = $"keep={keep} quote={fleetQuote.credits} run={fleetRun.credits} credits +{player.credits - before} (expected {expectedFleet}) stored={storage.GetCount(ore)} cargo {ship.cargo.GetCount(ore)}/{wing.cargo.GetCount(ore)}";
                if (fleetQuote.credits == expectedFleet && fleetRun.credits == expectedFleet && player.credits - before == expectedFleet && storage.GetCount(ore) == keep && ship.cargo.GetCount(ore) == 0 && wing.cargo.GetCount(ore) == 0) ctx.Pass("sell-above-fleet", detail);
                else ctx.Fail("sell-above-fleet", detail);
            }

            // Favourite stacks stay.
            ship.cargo.Add(ore, 3);
            foreach (var s in ship.cargo.items)
                if (s.item == ore) s.favourite = true;
            ItemRules.UseForTest("{\"items\":{\"" + key + "\":{\"dockAction\":\"Sell\"}}}");
            Services.Sell(true);
            detail = $"favourite cargo 3->{ship.cargo.GetCount(ore)}";
            foreach (var s in ship.cargo.items)
                if (s.item == ore) s.favourite = false;
            if (ship.cargo.GetCount(ore) == 3) ctx.Pass("sell-favourite", detail);
            else ctx.Fail("sell-favourite", detail);
        }
        finally { ItemRules.Load(); }
    }

    // Data items live in the data inventory, never in cargo: Sell above N keeps N there and the quote matches the run;
    // favourite stacks stay, and the setting turns it off.
    private static void CheckSellData(SelfCheckContext ctx)
    {
        var ss = Services.Docked();
        var item = EchoCargo.EchoCargo.TestDataItem();
        if (item == null || !EchoCargo.CargoRules.CanSellHere(ss, item))
        {
            ctx.Skip("sell-data", item == null ? "no sellable Data item type" : "station has no shop");
            return;
        }
        var player = GamePlayer.current;
        var data = player.dataInventory;
        var setting = EchoCargo.EchoCargo.Instance.SellFromData;
        bool setting0 = setting.Value;
        int count0 = data.GetCount(item);
        string key = Items.RuleKey(item);
        try
        {
            setting.Value = true;
            data.Add(item, 6);
            int held = data.GetCount(item);
            int keep = Mathf.Max(held - 4, player.RequiredItemCountForMissions(item));
            long expected = item.sellValue * (held - keep);
            ItemRules.UseForTest("{\"items\":{\"" + key + "\":{\"dockAction\":\"SellAbove\",\"keepAmount\":" + keep + "}}}");
            long before = player.credits;
            var quote = Services.Sell(false);
            var run = Services.Sell(true);
            string detail = $"{key}: keep={keep} held {held}->{data.GetCount(item)} credits +{player.credits - before} (expected {expected}) quote={quote.credits} run={run.credits} lines=[{string.Join("; ", run.lines)}]";
            if (expected > 0 && quote.credits == expected && run.credits == expected && player.credits - before == expected && data.GetCount(item) == keep
                && run.lines.Exists(l => l.StartsWith("Data inventory: "))) ctx.Pass("sell-data", detail);
            else ctx.Fail("sell-data", detail);

            foreach (var s in data.items)
                if (s.item == item) s.favourite = true;
            ItemRules.UseForTest("{\"items\":{\"" + key + "\":{\"dockAction\":\"Sell\"}}}");
            var fav = Services.Sell(true);
            int afterFav = data.GetCount(item);
            foreach (var s in data.items)
                if (s.item == item) s.favourite = false;
            setting.Value = false;
            var off = Services.Sell(true);
            detail = $"favourite: sold {fav.credits} cr, held {keep}->{afterFav}; setting off: sold {off.credits} cr, held {data.GetCount(item)}";
            if (fav.credits == 0 && afterFav == keep && off.credits == 0 && data.GetCount(item) == keep) ctx.Pass("sell-data-kept", detail);
            else ctx.Fail("sell-data-kept", detail);
        }
        finally
        {
            setting.Value = setting0;
            ItemRules.Load();
            data.Remove(item, data.GetCount(item));
            if (count0 > 0) data.Add(item, count0);
        }
    }

    // Station storage with SellFromStorage on: Sell above N trims the storage to N in the same run that sells the cargo
    // stack, the quote matches the run, and the setting turns it off.
    private static void CheckSellStorage(SelfCheckContext ctx)
    {
        var ss = Services.Docked();
        var ore = TestOre();
        if (ore == null || !EchoCargo.CargoRules.CanSellHere(ss, ore) || Cargo.DepositTarget(ss, ore) != ss.materialStorage)
        {
            ctx.Skip("sell-storage", ore == null ? "no sellable ore type" : "station has no shop, or ore doesn't go to material storage");
            return;
        }
        var player = GamePlayer.current;
        var ship = player.currentSpaceShip;
        var storage = ss.materialStorage;
        var setting = EchoCargo.EchoCargo.Instance.SellFromStorage;
        bool setting0 = setting.Value;
        int cargo0 = ship.cargo.GetCount(ore), stored0 = storage.GetCount(ore);
        string key = Items.RuleKey(ore);
        try
        {
            setting.Value = true;
            if (cargo0 > 0) ship.cargo.Remove(ore, cargo0);
            ship.cargo.Add(ore, 5);
            storage.Add(ore, 6);
            int held = storage.GetCount(ore);
            int keep = Mathf.Max(held - 3, player.RequiredItemCountForMissions(ore));
            long expected = ore.sellValue * (ship.cargo.GetCount(ore) + held - keep);
            ItemRules.UseForTest("{\"items\":{\"" + key + "\":{\"dockAction\":\"SellAbove\",\"keepAmount\":" + keep + "}}}");
            long before = player.credits;
            var quote = Services.Sell(false);
            var run = Services.Sell(true);
            string detail = $"{key}: keep={keep} stored {held}->{storage.GetCount(ore)} cargo 5->{ship.cargo.GetCount(ore)} credits +{player.credits - before} (expected {expected}) quote={quote.credits} run={run.credits} lines=[{string.Join("; ", run.lines)}]";
            if (held > keep && quote.credits == expected && run.credits == expected && player.credits - before == expected && storage.GetCount(ore) == keep
                && ship.cargo.GetCount(ore) == 0 && run.lines.Exists(l => l.StartsWith("Station storage: "))) ctx.Pass("sell-storage", detail);
            else ctx.Fail("sell-storage", detail);

            setting.Value = false;
            ItemRules.UseForTest("{\"items\":{\"" + key + "\":{\"dockAction\":\"Sell\"}}}");
            var off = Services.Sell(true);
            detail = $"setting off: sold {off.credits} cr, stored {keep}->{storage.GetCount(ore)}";
            if (off.credits == 0 && storage.GetCount(ore) == keep) ctx.Pass("sell-storage-off", detail);
            else ctx.Fail("sell-storage-off", detail);
        }
        finally
        {
            setting.Value = setting0;
            ItemRules.Load();
            ship.cargo.Remove(ore, ship.cargo.GetCount(ore));
            if (cargo0 > 0) ship.cargo.Add(ore, cargo0);
            storage.Remove(ore, storage.GetCount(ore));
            if (stored0 > 0) storage.Add(ore, stored0);
        }
    }

    // A fleet ship (the flagship if there is none) and a crew type it has at least `min` of.
    private static SpaceShipData CrewShip(int min, out string type)
    {
        type = null;
        SpaceShipData pick = null;
        foreach (var s in Services.FleetShips())
        {
            if (s.GetMaxGrunts() <= 0) continue;
            foreach (var kv in s.crewData.crew)
                if (kv.Value >= min && CrewType.TryGet(kv.Key, out var t) && t.availableForPlayer) { pick = s; type = kv.Key; break; }
            if (pick != null && pick != GamePlayer.current.currentSpaceShip) break;
        }
        return pick;
    }

    // Puts a ship's crew, the reserve and the loss record back as they were.
    private static void KeepCrew(SelfCheckContext ctx, SpaceShipData ship)
    {
        var crew0 = new System.Collections.Generic.Dictionary<string, int>(ship.crewData.crew);
        var reserve0 = new System.Collections.Generic.Dictionary<string, int>(GamePlayer.current.unassignedCrew.crew);
        ctx.Cleanup(() =>
        {
            ship.crewData.crew.Clear();
            foreach (var kv in crew0) ship.crewData.crew[kv.Key] = kv.Value;
            var reserve = GamePlayer.current.unassignedCrew.crew;
            reserve.Clear();
            foreach (var kv in reserve0) reserve[kv.Key] = kv.Value;
            CrewLosses.Clear(ship);
            Source.Personnel.CrewData.NotifyCrewChanged();
        });
    }

    // Puts every fleet ship's brig and the station brig back as they were.
    private static void KeepBrigs(SelfCheckContext ctx)
    {
        var brigs = new System.Collections.Generic.List<Source.Personnel.PrisonerData> { GamePlayer.current.stationPrisoners };
        foreach (var s in Services.FleetShips())
            if (s.prisonerData != null) brigs.Add(s.prisonerData);
        foreach (var b in brigs)
        {
            var brig = b;
            var prisoners0 = new System.Collections.Generic.Dictionary<string, int>(brig.prisoners);
            var willing0 = new System.Collections.Generic.Dictionary<string, int>(brig.willing);
            ctx.Cleanup(() =>
            {
                brig.prisoners.Clear();
                foreach (var kv in prisoners0) brig.prisoners[kv.Key] = kv.Value;
                brig.willing.Clear();
                foreach (var kv in willing0) brig.willing[kv.Key] = kv.Value;
            });
        }
    }

    private static void ClearWilling(string type)
    {
        GamePlayer.current.stationPrisoners.willing.Remove(type);
        foreach (var s in Services.FleetShips()) s.prisonerData?.willing.Remove(type);
    }

    // A death from a hull hit is counted for that ship and type; crew of that type added later cancels it.
    private static void CheckCrewTrack(SelfCheckContext ctx)
    {
        var ship = CrewShip(1, out _);
        if (ship == null)
        {
            ctx.Skip("crew-track", "no ship of yours has crew");
            return;
        }
        int total0 = ship.crewData.totalCrew;
        string died = null;
        for (int i = 0; i < 1000 && died == null; i++) died = ship.TryKillCrewFromHullDamage();
        if (died == null)
        {
            ctx.Fail("crew-track", $"{ship.GetShipName()}: no crew died in 1000 tries");
            return;
        }
        int pending = CrewLosses.Pending(ship, died);
        ship.AddCrew(died, 1, false);
        int after = CrewLosses.Pending(ship, died);
        string detail = $"{ship.GetShipName()}: {died} died, crew {total0}->{total0 - 1}, pending {pending}; after adding one back pending {after}";
        if (pending == 1 && after == 0 && ship.crewData.totalCrew == total0) ctx.Pass("crew-track", detail);
        else ctx.Fail("crew-track", detail);
    }

    // Losses come back from the reserve first, then willing prisoners, then hired at the personnel center for its price.
    private static void CheckCrew(SelfCheckContext ctx)
    {
        var ss = Services.Docked();
        var ship = CrewShip(5, out string type);
        if (ship == null)
        {
            ctx.Skip("crew-reserve", "no ship of yours has 5 crew of one type");
            return;
        }
        var player = GamePlayer.current;
        CrewLosses.Clear(ship);
        int had = ship.crewData.GetCrewCount(type);
        var reserve = player.unassignedCrew;
        if (reserve.GetCrewCount(type) < 3) reserve.AddCrew(type, 3 - reserve.GetCrewCount(type), false);
        int reserve0 = reserve.GetCrewCount(type);
        ship.RemoveCrew(type, 3);
        CrewLosses.Record(ship, type, 3);
        long credits0 = player.credits;
        var quote = Services.Crew(false);
        var run = Services.Crew(true);
        string detail = $"{ship.GetShipName()} {type}: {had}->{had - 3}->{ship.crewData.GetCrewCount(type)}, reserve {reserve0}->{reserve.GetCrewCount(type)}, pending {CrewLosses.Pending(ship, type)}, credits {credits0}->{player.credits}, quote {quote.count}/{quote.credits} run {run.count}/{run.credits} lines=[{string.Join("; ", run.lines)}] problems=[{string.Join("; ", run.problems)}]";
        if (quote.count == 3 && run.count == 3 && ship.crewData.GetCrewCount(type) == had && reserve.GetCrewCount(type) == reserve0 - 3
            && CrewLosses.Pending(ship, type) == 0 && player.credits == credits0 && run.lines.Exists(l => l.Contains("3 from the reserve")))
            ctx.Pass("crew-reserve", detail);
        else ctx.Fail("crew-reserve", detail);

        // Reserve empty: two willing prisoners of that type in the ship's own brig join for 40% of the hire price each.
        reserve.RemoveCrew(type, reserve.GetCrewCount(type), false);
        ClearWilling(type);
        ship.prisonerData.AddPrisoners(type, 2);
        ship.prisonerData.AddWilling(type, 2);
        ship.RemoveCrew(type, 2);
        CrewLosses.Record(ship, type, 2);
        int recruitCost = ship.CalculateRecruitCost(type);
        credits0 = player.credits;
        quote = Services.Crew(false);
        run = Services.Crew(true);
        detail = $"{ship.GetShipName()} {type}: {ship.crewData.GetCrewCount(type)} of {had}, brig willing now {ship.prisonerData.GetWilling(type)}, recruit cost {recruitCost}, credits {credits0}->{player.credits}, quote {quote.count}/{quote.credits} run {run.count}/{run.credits} lines=[{string.Join("; ", run.lines)}]";
        if (quote.count == 2 && quote.credits == 2L * recruitCost && run.count == 2 && ship.crewData.GetCrewCount(type) == had && ship.prisonerData.GetWilling(type) == 0
            && credits0 - player.credits == 2L * recruitCost && run.lines.Exists(l => l.Contains("2 recruited from the brig")))
            ctx.Pass("crew-brig", detail);
        else ctx.Fail("crew-brig", detail);

        var center = ss.personnelCenter;
        center?.EnsureCrewPopulated();
        if (center == null || !center.Available.TryGetValue(type, out int stock) || stock < 2)
        {
            ctx.Skip("crew-hire", center == null ? "no personnel center at " + ss.name : $"{ss.name} has fewer than 2 {type} for hire");
            return;
        }
        reserve.RemoveCrew(type, reserve.GetCrewCount(type), false);
        ClearWilling(type);
        ship.RemoveCrew(type, 2);
        CrewLosses.Record(ship, type, 2);
        int price = PersonnelCenter.GetBuyPrice(CrewType.Get(type), ss.level);
        credits0 = player.credits;
        quote = Services.Crew(false);
        run = Services.Crew(true);
        center.Available.TryGetValue(type, out int stock1);
        detail = $"{ship.GetShipName()} {type}: {ship.crewData.GetCrewCount(type)} of {had}, stock {stock}->{stock1}, price {price}, credits {credits0}->{player.credits}, pending {CrewLosses.Pending(ship, type)}, quote {quote.count}/{quote.credits} run {run.count}/{run.credits} lines=[{string.Join("; ", run.lines)}]";
        if (quote.count == 2 && quote.credits == 2L * price && run.count == 2 && ship.crewData.GetCrewCount(type) == had
            && credits0 - player.credits == 2L * price && stock1 == stock - 2 && CrewLosses.Pending(ship, type) == 0)
            ctx.Pass("crew-hire", detail);
        else ctx.Fail("crew-hire", detail);
    }

    // Prisoners aboard go to the station brig, the willing ones still willing.
    private static void CheckBrig(SelfCheckContext ctx)
    {
        var player = GamePlayer.current;
        var ship = CrewShip(1, out string type);
        if (ship?.prisonerData == null || player.maxBrig < 4)
        {
            ctx.Skip("brig", ship == null ? "no ship of yours has crew" : $"station brig holds {player.maxBrig}");
            return;
        }
        var station = player.stationPrisoners;
        station.prisoners.Clear();
        station.willing.Clear();
        foreach (var s in Services.FleetShips())
        {
            s.prisonerData?.prisoners.Clear();
            s.prisonerData?.willing.Clear();
        }
        ship.prisonerData.AddPrisoners(type, 4);
        ship.prisonerData.AddWilling(type, 3);
        var quote = Services.Brig(false);
        var run = Services.Brig(true);
        station.prisoners.TryGetValue(type, out int inStation);
        string detail = $"{ship.GetShipName()} {type}: station brig {inStation} ({station.GetWilling(type)} willing), aboard {ship.prisonerData.totalPrisoners}, quote {quote.count} run {run.count} lines=[{string.Join("; ", run.lines)}] problems=[{string.Join("; ", run.problems)}]";
        if (quote.count == 4 && run.count == 4 && inStation == 4 && station.GetWilling(type) == 3 && ship.prisonerData.totalPrisoners == 0
            && run.lines.Exists(l => l.Contains("4 prisoners (3 willing to join)")))
            ctx.Pass("brig", detail);
        else ctx.Fail("brig", detail);
    }

    // The dock hook: AutoDepositOnDock runs our postfix, which waits for the interior and runs the enabled services.
    private static IEnumerator CheckOnDock(SelfCheckContext ctx)
    {
        var f = StationServices.Instance;
        var ss = Services.Docked();
        var ship = GamePlayer.current.currentSpaceShip;
        bool r0 = f.RepairOnDock.Value, s0 = f.RestockOnDock.Value, l0 = f.SellOnDock.Value, c0 = f.CrewOnDock.Value;
        foreach (var job in StationHangar.current?.jobs.ToArray() ?? new Source.Spacestation.RepairJob[0])
            if (job.spaceshipGuid == ship.guid) StationHangar.current.CancelJob(job);
        ship.currentHullHP = ship.maxHullHP * 0.7f;
        f.RepairOnDock.Value = true;
        f.RestockOnDock.Value = false;
        f.SellOnDock.Value = false;
        f.CrewOnDock.Value = true;
        var lost = SimulateLoss(out string lostType);
        StationServices.lastDockSummary = null;
        Behaviour.UI.PlayerCargo.AutoDepositOnDock();
        float until = Time.realtimeSinceStartup + 5f;
        while (StationServices.lastDockSummary == null && Time.realtimeSinceStartup < until) yield return null;
        f.RepairOnDock.Value = r0;
        f.RestockOnDock.Value = s0;
        f.SellOnDock.Value = l0;
        f.CrewOnDock.Value = c0;
        if (lost != null)
        {
            string crewDetail = $"{lost.GetShipName()} lost 1 {lostType}: pending now {CrewLosses.Pending(lost, lostType)}, summary='{StationServices.lastDockSummary}'";
            if (CrewLosses.Pending(lost, lostType) == 0 && (StationServices.lastDockSummary ?? "").Contains("replaced 1 crew")) ctx.Pass("crew-on-dock", crewDetail);
            else ctx.Fail("crew-on-dock", crewDetail);
        }
        else ctx.Skip("crew-on-dock", "no ship of yours has 2 crew of one type");
        RepairJobInfo(ship, out bool paid, out _);
        string detail = $"summary='{StationServices.lastDockSummary}' paidJob={paid} at {ss?.name}";
        if (paid && !string.IsNullOrEmpty(StationServices.lastDockSummary)) ctx.Pass("on-dock", detail);
        else if (Services.RepairUnavailable(ss) != null) ctx.Skip("on-dock", Services.RepairUnavailable(ss));
        else ctx.Fail("on-dock", detail);
    }

    private static IEnumerator CheckAirlock(SelfCheckContext ctx)
    {
        // GoToLocation replaces the airlock; the old one (and its buttons) is only destroyed at the end of the frame.
        SpaceStationInterior.instance.GoToLocation(SpaceStationFacility.Airlock);
        float until = Time.realtimeSinceStartup + 10f;
        while ((AirlockServices.Instance == null || SpaceStationInterior.instance.currentAirlock == null
                || AirlockServices.Instance.gameObject != SpaceStationInterior.instance.currentAirlock.gameObject)
               && Time.realtimeSinceStartup < until) yield return null;
        var a = AirlockServices.Instance;
        if (a == null)
        {
            ctx.Fail("airlock", "no airlock buttons after opening the airlock");
            yield break;
        }
        // Something for each button: a damaged fleet ship (or flagship), ammo missing, a stack with a Sell rule.
        var player = GamePlayer.current;
        var ship = player.currentSpaceShip;
        foreach (var job in StationHangar.current?.jobs.ToArray() ?? new Source.Spacestation.RepairJob[0])
            if (job.spaceshipGuid == ship.guid) StationHangar.current.CancelJob(job);
        ship.currentHullHP = ship.maxHullHP * 0.5f;
        var ore = TestOre();
        if (ore != null)
        {
            ship.cargo.Add(ore, 5);
            ItemRules.UseForTest("{\"items\":{\"" + Items.RuleKey(ore) + "\":{\"dockAction\":\"Sell\"}}}");
        }
        var lost = SimulateLoss(out string lostType);
        a.Refresh();
        yield return new WaitForSeconds(1.5f);
        string Label(GameObject go) => go.activeSelf ? "'" + go.GetComponentInChildren<TMPro.TMP_Text>().text + "'" : "hidden";
        var rt = (RectTransform)a.repair.transform;
        string detail = $"repair={Label(a.repair)} restock={Label(a.restock)} sell={Label(a.sell)} crew={Label(a.crew)} brig={Label(a.brig)} repairPos={rt.anchoredPosition} size={rt.sizeDelta}";
        bool ok = a.repair.activeSelf && Label(a.repair).Contains("cr") && (ore == null || a.sell.activeSelf) && (lost == null || a.crew.activeSelf);
        yield return ctx.Screenshot("airlock-services");

        // Clicking Replace crew puts the crew member back and hides the button.
        if (lost != null && a.crew.activeSelf)
        {
            int before = lost.crewData.GetCrewCount(lostType);
            a.crew.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return new WaitForSeconds(1.5f);
            detail += $" afterCrewClick {lostType} {before}->{lost.crewData.GetCrewCount(lostType)} crew={Label(a.crew)}";
            ok &= lost.crewData.GetCrewCount(lostType) == before + 1 && !a.crew.activeSelf;
        }

        // Clicking Sell sells the stack and hides the button.
        if (ore != null && a.sell.activeSelf)
        {
            a.sell.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return new WaitForSeconds(1.5f);
            detail += $" afterSellClick cargo={ship.cargo.GetCount(ore)} sell={Label(a.sell)}";
            ok &= ship.cargo.GetCount(ore) == 0 && !a.sell.activeSelf;
        }
        if (ore != null) ship.cargo.Remove(ore, ship.cargo.GetCount(ore));
        ItemRules.Load();
        if (ok) ctx.Pass("airlock", detail);
        else ctx.Fail("airlock", detail);

        // All five buttons at once: with the game's own ones the row must still stop short of the Undock button.
        if (ore != null)
        {
            ship.cargo.Add(ore, 5);
            ItemRules.UseForTest("{\"items\":{\"" + Items.RuleKey(ore) + "\":{\"dockAction\":\"Sell\"}}}");
        }
        lost = SimulateLoss(out _);
        var brigShip = lost ?? ship;
        brigShip.prisonerData.AddPrisoners("Gunner", 2);
        SpaceShipData ammoShip = null;
        InventoryItemType ammo = null;
        int ammo0 = 0;
        foreach (var s in Services.FleetShips())
        {
            var u = Fleet.Unit(s);
            ammo = u != null ? AmmoOf(u) : null;
            if (ammo == null) continue;
            ammoShip = s;
            ammo0 = s.cargo.GetCount(ammo);
            s.cargo.Remove(ammo, ammo0);
            break;
        }
        a.Refresh();
        yield return new WaitForSeconds(2.5f);
        bool overflow = a.RowOverflows();
        string full = $"compact={a.Compact} overflow={overflow} repair={Label(a.repair)} restock={Label(a.restock)} sell={Label(a.sell)} crew={Label(a.crew)} brig={Label(a.brig)}";
        yield return ctx.Screenshot("airlock-services-all");
        if (ammoShip != null)
        {
            ammoShip.cargo.Remove(ammo, ammoShip.cargo.GetCount(ammo));
            if (ammo0 > 0) ammoShip.cargo.Add(ammo, ammo0);
        }
        if (ore != null) ship.cargo.Remove(ore, ship.cargo.GetCount(ore));
        ItemRules.Load();
        if (!overflow && a.repair.activeSelf && a.crew.activeSelf && a.brig.activeSelf && (ammoShip == null || a.restock.activeSelf) && (ore == null || a.sell.activeSelf))
            ctx.Pass("airlock-full", full);
        else ctx.Fail("airlock-full", full);
    }

    // One crew member of the check's crew ship counted as killed, with one of that type in the reserve to replace it.
    private static SpaceShipData SimulateLoss(out string type)
    {
        var ship = CrewShip(2, out type);
        if (ship == null) return null;
        CrewLosses.Clear(ship);
        var reserve = GamePlayer.current.unassignedCrew;
        if (reserve.GetCrewCount(type) < 1) reserve.AddCrew(type, 1, false);
        ship.RemoveCrew(type, 1);
        CrewLosses.Record(ship, type, 1);
        return ship;
    }

    // A sellable ore type none of your fleet carries, so a Sell rule on it touches only what the check adds.
    private static InventoryItemType TestOre()
    {
        var ships = Services.FleetShips();
        foreach (var t in InventoryItemType.all)
        {
            if (t.itemCategory != ItemCategory.Ore || !t.canSell || t.sellValue <= 0 || GamePlayer.current.RequiredItemCountForMissions(t) > 0) continue;
            bool carried = false;
            foreach (var s in ships) carried |= s.cargo.GetCount(t) > 0;
            if (!carried) return t;
        }
        return null;
    }
}
