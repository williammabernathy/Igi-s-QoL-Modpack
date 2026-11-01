using System;
using System.Collections.Generic;
using Behaviour.Item;
using Behaviour.Item.Usable;
using Behaviour.UI;
using Behaviour.UI.Spacestation;
using Behaviour.Unit;
using IgiQoL.Features.EchoCargo;
using IgiQoL.Game;
using IgiQoL.Rules;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.Galaxy.POI.Station;
using Source.Item;
using Source.Personnel;
using Source.Player;
using Source.SpaceShip;
using Source.Util;
using UnityEngine;
using CrewType = Behaviour.Crew.CrewType;
using StationHangar = Source.Spacestation.PersonalHangar;

namespace IgiQoL.Features.StationServices;

// What one service did or would do. Quotes and runs share it so the airlock buttons show exactly what a click does.
public sealed class ServiceReport
{
    public readonly List<string> lines = new();
    public readonly List<string> problems = new();
    public long credits;   // spent (repair, restock) or earned (sell)
    public int count;      // ships repaired, rounds or fuel cells loaded, stacks sold
    public int cells;      // restock only: warp fuel cells among `count`
}

// Repair, restock and sell for the ship you fly and your active fleet, at the station you're docked at.
public static class Services
{
    public static List<SpaceShipData> FleetShips() => Fleet.Ships(withMercenaries: false);

    public static SpaceStation Docked()
    {
        var interior = SpaceStationInterior.instance;
        return interior != null ? interior.spacestation : null;
    }

    // ---- Repair: the hangar's paid repair (PersonalHangar.RepairShip) for every damaged ship at once.

    public static string RepairUnavailable(SpaceStation ss)
    {
        if (ss == null) return "Dock at a station first.";
        if (!ss.HasFacility(SpaceStationFacility.PersonalHangar) || StationHangar.current == null) return "This station has no hangar, so it can't repair ships.";
        return null;
    }

    public static int RepairAmount(SpaceShipData s) => Mathf.CeilToInt(Mathf.Max(s.HullDamageTaken(), s.ArmorDamageTaken()));

    // Same formula as the hangar's repair cost label (UI PersonalHangar.GetRepairCost), including its "no faction = free" case.
    public static int RepairCost(int amount)
    {
        float discount = 1f;
        var faction = MapPointOfInterest.current?.faction;
        if (faction != null) discount = faction.GetReputationLevel(Faction.player).GetRepairCostDiscount();
        return Mathf.Max(0, Mathf.CeilToInt(amount * (1f - discount)));
    }

    private static bool PaidRepairRunning(SpaceShipData s)
    {
        foreach (var job in StationHangar.current.jobs)
            if (job.spaceshipGuid == s.guid && !job.autoRepair) return true;
        return false;
    }

    public static ServiceReport Repair(bool run)
    {
        var r = new ServiceReport();
        var ss = Docked();
        string reason = RepairUnavailable(ss);
        var player = GamePlayer.current;
        foreach (var s in FleetShips())
        {
            // The game only brings fleet ships back from repairs; the flown ship can keep a stale flag from its fleet days.
            if (s.IsWorkBayAssigned || (s.awayForRepairs && s != player.currentSpaceShip)) continue;
            int amount = RepairAmount(s);
            if (amount <= 0) continue;
            if (reason != null)
            {
                r.problems.Add(reason);
                return r;
            }
            if (PaidRepairRunning(s)) continue;
            int cost = RepairCost(amount);
            if (!player.CanAfford(cost + (run ? 0 : r.credits)))
            {
                r.problems.Add($"{s.GetShipName()}: not enough credits ({Money(cost)} needed).");
                continue;
            }
            if (run) StationHangar.current.StartRepair(amount, s.guid, autoRepair: false, cost);
            r.credits += cost;
            r.count++;
            r.lines.Add($"{s.GetShipName()}: {amount} damage, {Money(cost)}");
        }
        return r;
    }

    // ---- Restock: ECHO's ammo top-up (IdleManager.TryRestockAmmo) for every ship, armory first, then the station's shops.

    // Credit shops only, in the order SpaceStation.shopInventory prefers them; shops that take another currency are left alone.
    private static IEnumerable<ShopInventory> Shops(SpaceStation ss)
    {
        if (ss.generalShopInventory != null) yield return ss.generalShopInventory;
        if (ss.miningShopInventory != null) yield return ss.miningShopInventory;
        if (ss.salvageShopInventory != null) yield return ss.salvageShopInventory;
        if (ss.bountyShopInventory != null) yield return ss.bountyShopInventory;
        if (ss.patrolShopInventory != null) yield return ss.patrolShopInventory;
        if (ss.industryShopInventory != null) yield return ss.industryShopInventory;
        if (ss.conquestShopInventory != null) yield return ss.conquestShopInventory;
    }

    private static Inventory.InventoryItem ShopStack(SpaceStation ss, InventoryItemType type)
    {
        foreach (var shop in Shops(ss))
        {
            var stack = shop.Get(type);
            if (stack != null && stack.count > 0 && stack.costItem == null) return stack;
        }
        return null;
    }

    public static ServiceReport Restock(bool run)
    {
        var r = new ServiceReport();
        var ss = Docked();
        if (ss == null) return r;
        var player = GamePlayer.current;
        var armory = player.globalInventory;
        var iim = InventoryInteractionManager.Instance;
        var faction = MapPointOfInterest.current?.faction;
        var claimed = new Dictionary<InventoryItemType, int>();   // quotes only: armory rounds already promised to an earlier ship
        var booked = new Dictionary<SpaceShipData, float>();   // quotes only: m3 earlier picks would take in each ship's hold
        float Free(SpaceShipData ship) { booked.TryGetValue(ship, out float b); return ship.cargo.GetSpaceAvailable() - b; }
        void Book(SpaceShipData ship, float m3) { booked.TryGetValue(ship, out float b); booked[ship] = b + m3; }
        foreach (var s in FleetShips())
        {
            var unit = Fleet.Unit(s);
            if (unit == null) continue;
            foreach (var kv in unit.GetAmmoTypesRequired())
            {
                var type = kv.Key;
                int want = kv.Value;
                if (want <= 0) continue;
                claimed.TryGetValue(type, out int promised);
                int fromArmory = Math.Min(want, armory.GetCount(type) - promised);
                if (!run && type.m3 > 0f) fromArmory = Math.Min(fromArmory, Mathf.FloorToInt(Free(s) / type.m3));
                if (fromArmory > 0 && run) fromArmory = Cargo.Move(armory, s.cargo, type, fromArmory);
                if (fromArmory > 0 && !run)
                {
                    claimed[type] = promised + fromArmory;
                    Book(s, fromArmory * type.m3);
                }
                fromArmory = Math.Max(0, fromArmory);
                want -= fromArmory;

                int bought = 0;
                long spent = 0;
                string shortReason = null;
                // A shop may split one item over several stacks; a handful of passes covers that.
                for (int pass = 0; want > 0 && pass < 8; pass++)
                {
                    var stack = ShopStack(ss, type);
                    if (stack == null) { shortReason = "not sold here"; break; }
                    if (faction != null && !stack.item.EnoughRepWithFaction(faction)) { shortReason = "reputation too low to buy it"; break; }
                    if (player.level < stack.item.shopItemData.levelRequirement) { shortReason = "your level is too low to buy it"; break; }
                    int n = Math.Min(want, stack.count);
                    long price = stack.cost;
                    long budget = run ? player.credits : player.credits - r.credits - spent;
                    if (price > 0) n = (int)Math.Min(n, budget / price);
                    if (type.m3 > 0f) n = Math.Min(n, Mathf.FloorToInt((run ? s.cargo.GetSpaceAvailable() : Free(s)) / type.m3));
                    if (n <= 0) { shortReason = price > 0 && budget < price ? "not enough credits" : "no cargo space"; break; }
                    if (run && (iim == null || !iim.BuyAmount(stack, n, null, s.cargo, showStoredInventory: false))) { shortReason = "the shop refused the purchase"; break; }
                    bought += n;
                    spent += price * n;
                    want -= n;
                    if (!run) Book(s, n * type.m3);
                    if (!run) break;   // a quote stops at the first stack; the shop stack doesn't shrink without a purchase
                }
                int loaded = fromArmory + bought;
                if (loaded > 0)
                {
                    r.count += loaded;
                    r.credits += spent;
                    string from = fromArmory > 0 && bought > 0 ? $"{fromArmory} from the armory, {bought} bought for {Money(spent)}"
                        : fromArmory > 0 ? "from the armory" : $"bought for {Money(spent)}";
                    r.lines.Add($"{s.GetShipName()}: {loaded} {Name(type)} ({from})");
                }
                if (want > 0 && shortReason != null) r.problems.Add($"{s.GetShipName()}: {want} {Name(type)} short, {shortReason}.");
            }
        }
        RestockFuel(r, run, ss, booked);
        return r;
    }

    // ---- Warp fuel: only the ship you fly burns it (TravelManager.UseWarpFuel reads currentSpaceShip.cargo).

    private static float FuelMultiplier(InventoryItemType item) =>
        item != null && item.TryGetState<WarpFuelState>(out var st) && st.remaining > 0f ? st.multiplier : 0f;

    // Cells as the game counts them for ECHO (Inventory.HasEnoughWarpFuel): a part-used cell is one cell.
    public static int FuelCells(Inventory inv)
    {
        int n = 0;
        foreach (var stack in inv.items)
            if (stack != null && FuelMultiplier(stack.item) > 0f) n += stack.count;
        return n;
    }

    // Fastest cells first; the price only decides between stacks of the same kind.
    private static List<Inventory.InventoryItem> FuelStacks(IEnumerable<Inventory.InventoryItem> items)
    {
        var list = new List<Inventory.InventoryItem>();
        foreach (var stack in items)
            if (stack != null && stack.count > 0 && FuelMultiplier(stack.item) > 0f) list.Add(stack);
        list.Sort((a, b) =>
        {
            int c = FuelMultiplier(b.item).CompareTo(FuelMultiplier(a.item));
            return c != 0 ? c : a.cost.CompareTo(b.cost);
        });
        return list;
    }

    private static void RestockFuel(ServiceReport r, bool run, SpaceStation ss, Dictionary<SpaceShipData, float> booked)
    {
        var player = GamePlayer.current;
        var s = player.currentSpaceShip;
        int target = StationServices.Instance.FuelCells.Value;
        if (target <= 0 || s == null) return;
        int want = target - FuelCells(s.cargo);
        if (want <= 0) return;

        // A quote moves nothing, so it books the space the ammo picks and its own earlier picks would take.
        float bookedM3 = 0f;
        if (!run) booked.TryGetValue(s, out bookedM3);
        int Fit(InventoryItemType item, int n) =>
            item.m3 > 0f ? Math.Min(n, Mathf.FloorToInt((s.cargo.GetSpaceAvailable() - bookedM3) / item.m3)) : n;

        int fromArmory = 0;
        foreach (var stack in FuelStacks(player.globalInventory.items))
        {
            if (want <= 0) break;
            var item = stack.item;
            int n = Fit(item, Math.Min(want, stack.count));
            if (n <= 0) break;
            if (run) n = Cargo.Move(player.globalInventory, s.cargo, item, n);
            else bookedM3 += n * item.m3;
            fromArmory += n;
            want -= n;
        }

        var shopStacks = new List<Inventory.InventoryItem>();
        foreach (var shop in Shops(ss)) shopStacks.AddRange(shop.items);
        var faction = MapPointOfInterest.current?.faction;
        var iim = InventoryInteractionManager.Instance;
        int bought = 0;
        long spent = 0;
        string shortReason = null;
        bool anyOnSale = false;
        foreach (var stack in FuelStacks(shopStacks))
        {
            if (want <= 0) break;
            if (stack.costItem != null) continue;
            anyOnSale = true;
            if (faction != null && !stack.item.EnoughRepWithFaction(faction)) { shortReason = "reputation too low to buy it"; continue; }
            if (player.level < stack.item.shopItemData.levelRequirement) { shortReason = "your level is too low to buy it"; continue; }
            long left = run ? player.credits : player.credits - r.credits - spent;
            int n = Math.Min(want, stack.count);
            if (stack.cost > 0) n = (int)Math.Min(n, left / stack.cost);
            n = Fit(stack.item, n);
            if (n <= 0) { shortReason = stack.cost > 0 && left < stack.cost ? "not enough credits" : "no cargo space"; break; }
            if (run && (iim == null || !iim.BuyAmount(stack, n, null, s.cargo, showStoredInventory: false))) { shortReason = "the shop refused the purchase"; break; }
            if (!run) bookedM3 += n * stack.item.m3;
            bought += n;
            spent += stack.cost * n;
            want -= n;
        }
        shortReason ??= anyOnSale ? "the shops here have no more" : "none for sale here";

        int loaded = fromArmory + bought;
        if (loaded > 0)
        {
            r.count += loaded;
            r.cells += loaded;
            r.credits += spent;
            string from = fromArmory > 0 && bought > 0 ? $"{fromArmory} from the armory, {bought} bought for {Money(spent)}"
                : fromArmory > 0 ? "from the armory" : $"bought for {Money(spent)}";
            r.lines.Add($"{s.GetShipName()}: {loaded} warp fuel cell{(loaded == 1 ? "" : "s")} ({from})");
        }
        if (want > 0) r.problems.Add($"{s.GetShipName()}: {want} warp fuel cell{(want == 1 ? "" : "s")} short, {shortReason}.");
    }

    // ---- Sell: the Item rules tab's dock actions (Sell, Sell above N) for your own docking, like EchoCargo does for ECHO.

    // Vanilla IdleManager.ShouldKeepInCargo, widened to the ammo each fleet ship uses.
    private static bool Protected(SpaceShipData s, Inventory.InventoryItem stack)
    {
        if (stack.favourite) return true;
        var item = stack.item;
        if (item.itemCategory == ItemCategory.Ammo)
        {
            var unit = Fleet.Unit(s);
            return unit == null || unit.DoWeUseAmmo(item);
        }
        return item.itemCategory == ItemCategory.Usable && item.storageOverride == StorageOverride.None;
    }

    public static ServiceReport Sell(bool run)
    {
        var r = new ServiceReport();
        var ss = Docked();
        if (ss == null || !ItemRules.HasDockRules) return r;
        var player = GamePlayer.current;
        // Quotes move nothing, so they book what earlier stacks would deposit: count per storage and item, m3 per storage.
        var planned = new Dictionary<(Inventory, InventoryItemType), int>();
        var plannedM3 = new Dictionary<Inventory, float>();
        foreach (var s in FleetShips())
        {
            foreach (var stack in new List<Inventory.InventoryItem>(s.cargo.items))
            {
                var item = stack.item;
                var rule = ItemRules.Resolve(item);
                if (rule.dockAction != DockAction.Sell && rule.dockAction != DockAction.SellAbove) continue;
                if (Protected(s, stack)) continue;
                var dest = Cargo.DepositTarget(ss, item);
                int stored = 0;
                if (dest != null)
                {
                    stored = dest.GetCount(item);
                    if (!run && planned.TryGetValue((dest, item), out int p)) stored += p;
                }
                int reserved = player.RequiredItemCountForMissions(item);
                bool sellable = CargoRules.CanSellHere(ss, item);
                int count = stack.count;
                var step = CargoRules.Plan(rule, count, stored, reserved, sellable);
                if (step.deposit > 0 && dest != null)
                {
                    float free = dest.GetSpaceAvailable();
                    if (!run && plannedM3.TryGetValue(dest, out float used)) free -= used;
                    int fit = item.m3 > 0f ? Mathf.FloorToInt(free / item.m3) : step.deposit;
                    int deposit = Math.Min(step.deposit, fit);
                    if (deposit > 0 && run)
                    {
                        dest.Add(item, deposit);
                        s.cargo.Remove(stack, deposit);
                    }
                    else if (deposit > 0)
                    {
                        planned.TryGetValue((dest, item), out int p2);
                        planned[(dest, item)] = p2 + deposit;
                        plannedM3.TryGetValue(dest, out float m);
                        plannedM3[dest] = m + deposit * item.m3;
                    }
                    if (deposit < step.deposit)
                        r.problems.Add($"{Name(item)}: storage is full, kept {count - deposit - step.sell} aboard {s.GetShipName()}.");
                }
                // Sell above N's part above N sells whether or not the deposit fit.
                if (step.sell <= 0) continue;
                int n = step.sell;
                long value = item.sellValue * n;
                if (run)
                {
                    // InventoryInteractionManager.SellAmount without the open shop screen it needs: pay, put the items in the shop, take them out of cargo.
                    player.credits += value;
                    if (item.itemCategory != ItemCategory.Data) ss.shopInventory.Add(item, n, item.buyBack);
                    s.cargo.Remove(stack, n);
                }
                r.credits += value;
                r.count++;
                r.lines.Add($"{s.GetShipName()}: {n} {Name(item)}, {Money(value)}");
            }
        }
        // Data items never sit in cargo: the game keeps them in the player-wide data inventory. Station storage holds what
        // earlier visits deposited here.
        var echo = EchoCargo.EchoCargo.Instance;
        if (echo.SellFromData.Value) SellStored(r, run, ss, player.dataInventory, "Data inventory");
        if (echo.SellFromStorage.Value) SellStored(r, run, ss, ss.materialStorage, "Station storage");
        return r;
    }

    private static void SellStored(ServiceReport r, bool run, SpaceStation ss, Inventory storage, string label)
    {
        var player = GamePlayer.current;
        var quoted = new Dictionary<InventoryItemType, int>();   // quotes only: what earlier stacks of the type would sell
        foreach (var stack in new List<Inventory.InventoryItem>(storage.items))
        {
            var item = stack.item;
            quoted.TryGetValue(item, out int q);
            int n = CargoRules.StoredSell(ss, stack, q);
            if (n <= 0) continue;
            long value = item.sellValue * n;
            if (run)
            {
                player.credits += value;
                if (item.itemCategory != ItemCategory.Data) ss.shopInventory.Add(item, n, item.buyBack);
                storage.Remove(stack, n);
            }
            else quoted[item] = q + n;
            r.credits += value;
            r.count++;
            r.lines.Add($"{label}: {n} {Name(item)}, {Money(value)}");
        }
    }

    // ---- Crew: replace the crew your ships lost to hull hits (CrewLosses): from the crew reserve (free), then willing
    // prisoners of that type in the ship's own brig and then the station brig (recruited at 40% of the hire price, as
    // SpaceShipData.RecruitWillingByType does), then hired at this station's personnel center (PersonnelCenterCrewPanel.
    // ExecuteBuy: its stock, its price). Vanilla ECHO only tops up the ship you fly, and the personnel center only hires
    // into the ship you fly or the reserve.

    public static ServiceReport Crew(bool run)
    {
        var r = new ServiceReport();
        var ss = Docked();
        if (ss == null) return r;
        var player = GamePlayer.current;
        var center = ss.personnelCenter;
        center?.EnsureCrewPopulated();
        var reserveUsed = new Dictionary<string, int>();
        var stationBrigUsed = new Dictionary<string, int>();
        var stockUsed = new Dictionary<string, int>();
        bool changed = false;
        foreach (var s in FleetShips())
        {
            var pending = CrewLosses.PendingOf(s);
            if (pending.Count == 0) continue;
            int room = s.GetFreeCrewSpace();
            int fromReserve = 0, recruited = 0, hired = 0;
            long spent = 0;
            var parts = new List<string>();
            foreach (var kv in pending)
            {
                string type = kv.Key;
                int want = Math.Min(kv.Value, room);
                if (want <= 0) break;
                int added = 0;
                int usedR = 0;
                if (!run) reserveUsed.TryGetValue(type, out usedR);
                int take = Math.Min(want, Math.Max(0, player.unassignedCrew.GetCrewCount(type) - usedR));
                if (take > 0)
                {
                    if (run)
                    {
                        player.unassignedCrew.RemoveCrew(type, take, false);
                        s.AddCrew(type, take, false);
                    }
                    reserveUsed[type] = usedR + take;
                    fromReserve += take;
                    added += take;
                }
                if (want > added && CrewType.TryGet(type, out var recruitType))
                {
                    int cost = Mathf.RoundToInt(recruitType.cost * 0.4f);
                    int usedB = 0;
                    if (!run) stationBrigUsed.TryGetValue(type, out usedB);
                    int shipWilling = s.prisonerData != null ? s.prisonerData.GetWilling(type) : 0;
                    int stationWilling = player.stationPrisoners.GetWilling(type) - usedB;
                    foreach (bool own in new[] { true, false })
                    {
                        long budget = run ? player.credits : player.credits - r.credits - spent;
                        int n = Math.Min(want - added, own ? shipWilling : stationWilling);
                        if (cost > 0) n = (int)Math.Min(n, Math.Max(0L, budget / cost));
                        if (n <= 0) continue;
                        if (run)
                        {
                            n = (own ? s.prisonerData : player.stationPrisoners).RecruitWilling(type, n);
                            player.credits -= (long)n * cost;
                            s.AddCrew(type, n, false);
                        }
                        if (!own) stationBrigUsed[type] = usedB + n;
                        recruited += n;
                        spent += (long)n * cost;
                        added += n;
                    }
                }
                if (want > added && center != null && CrewType.TryGet(type, out var crewType))
                {
                    center.Available.TryGetValue(type, out int stock);
                    int usedS = 0;
                    if (!run) stockUsed.TryGetValue(type, out usedS);
                    int price = PersonnelCenter.GetBuyPrice(crewType, ss.level);
                    int buy = Math.Min(want - added, stock - usedS);
                    long budget = run ? player.credits : player.credits - r.credits - spent;
                    if (price > 0) buy = (int)Math.Min(buy, Math.Max(0L, budget / price));
                    if (buy > 0 && (!run || center.TryPurchaseCrew(type, buy)))
                    {
                        long cost = (long)buy * price;
                        if (run)
                        {
                            player.RemoveCreditsForCrew(cost, type);
                            s.AddCrew(type, buy, false);
                        }
                        stockUsed[type] = usedS + buy;
                        hired += buy;
                        spent += cost;
                        added += buy;
                    }
                }
                room -= added;
                if (added > 0) parts.Add(CrewCount(added, type));
                if (added < want)
                    r.problems.Add($"{s.GetShipName()}: {CrewCount(want - added, type)} short, "
                                   + (center == null ? "none in your reserve or brigs and no personnel center here." : "not in your reserve or brigs or for hire here."));
            }
            // A full ship has nothing left to replace, whatever the count says (crew of another type took the places).
            if (run && s.GetFreeCrewSpace() == 0) CrewLosses.Clear(s);
            if (parts.Count == 0) continue;
            changed |= run;
            var how = new List<string>();
            if (fromReserve > 0) how.Add(fromReserve + " from the reserve");
            if (recruited > 0) how.Add(recruited + " recruited from the brig");
            if (hired > 0) how.Add(hired + " hired");
            if (spent > 0) how.Add(Money(spent));
            r.lines.Add($"{s.GetShipName()}: {string.Join(", ", parts)} ({string.Join("; ", how)})");
            r.count += fromReserve + recruited + hired;
            r.credits += spent;
        }
        if (changed) CrewData.NotifyCrewChanged();
        return r;
    }

    private static string CrewCount(int n, string type)
    {
        if (!CrewType.TryGet(type, out var t)) return n + " " + type;
        string one = Translation.Translate(t.displayName);
        return n + " " + (n == 1 ? one : !string.IsNullOrEmpty(t.displayNamePlural) ? Translation.Translate(t.displayNamePlural) : one + "s");
    }

    // ---- Brig: prisoners in your ships' brigs go to the station brig (CrewReserve.TransferShipBrigPrisoners: the
    // willing ones stay willing), so the next boarding's captives fit aboard. The station brig holds GamePlayer.maxBrig.

    public static ServiceReport Brig(bool run)
    {
        var r = new ServiceReport();
        if (Docked() == null) return r;
        var player = GamePlayer.current;
        var station = player.stationPrisoners;
        int room = player.maxBrig - station.totalPrisoners;
        foreach (var s in FleetShips())
        {
            var brig = s.prisonerData;
            if (brig == null || brig.totalPrisoners <= 0) continue;
            int moved = 0, willing = 0, left = brig.totalPrisoners;
            foreach (var kv in new List<KeyValuePair<string, int>>(brig.prisoners))
            {
                int n = Math.Min(kv.Value, room);
                if (n <= 0) break;
                int w = brig.GetWilling(kv.Key);
                int movedWilling = w - Math.Min(w, kv.Value - n);
                if (run)
                {
                    brig.RemovePrisoners(kv.Key, n);
                    station.AddPrisoners(kv.Key, n);
                    station.AddWilling(kv.Key, movedWilling);
                }
                room -= n;
                moved += n;
                willing += movedWilling;
            }
            left -= moved;
            if (moved > 0)
            {
                r.lines.Add($"{s.GetShipName()}: {moved} prisoner{(moved == 1 ? "" : "s")}" + (willing > 0 ? $" ({willing} willing to join)" : ""));
                r.count += moved;
            }
            if (left > 0) r.problems.Add($"The station brig is full ({player.maxBrig}): {left} prisoner{(left == 1 ? "" : "s")} stay aboard {s.GetShipName()}.");
        }
        if (run && r.count > 0) CrewData.NotifyCrewChanged();
        return r;
    }

    public static string Money(long credits) => GameMath.FormatNumber(credits) + " cr";

    private static string Name(InventoryItemType item) => Translation.Translate(item.displayName);
}
