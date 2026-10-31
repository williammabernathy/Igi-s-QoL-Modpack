using System.Collections;
using System.Collections.Generic;
using Behaviour.Equipment.Module;
using Behaviour.Item;
using Behaviour.Managers;
using Behaviour.Mining;
using Behaviour.Tractoring;
using Behaviour.Unit;
using Behaviour.Util;
using IgiQoL.Core;
using Source.Data.Persistable;
using Source.Galaxy;
using Source.Item;
using Source.Player;
using UnityEngine;

namespace IgiQoL.Features.FleetHolds;

internal static class FleetHoldsCheck
{
    public static IEnumerator Run(SelfCheckContext ctx)
    {
        yield return SelfCheckContext.EnsureInSpace();
        var f = FleetHolds.Instance;
        var flagship = GameplayManager.Instance != null ? GameplayManager.Instance.spaceShip : null;
        SpaceShip wing = null;
        TractorModule tractor = null;
        foreach (var d in GamePlayer.current.activeFleet)
            if (d?.unit is SpaceShip u && u != flagship && u.gameObject.activeInHierarchy && (tractor = u.GetModule<TractorModule>()) != null) { wing = u; break; }
        if (flagship == null || wing == null || !Singleton<LootManager>.HasInstance)
        {
            ctx.Skip("overflow", "no fleet ship with a tractor beam here");
            yield break;
        }
        var wingCargo = wing.spaceShipData.cargo;
        var flagCargo = flagship.spaceShipData.cargo;
        var before = Counts(wingCargo);
        var flagBefore = Counts(flagCargo);
        bool overflow0 = f.Overflow.Value;

        InventoryItemType ore = null, favItem = null;
        foreach (var o in OreItemData.regularOres)
        {
            if (ore == null) ore = o.item;
            else if (favItem == null) { favItem = o.item; break; }
        }
        InventoryItemType ammo = null;
        foreach (var t in wing.GetComponentsInChildren<Behaviour.Equipment.Turret.AbstractTurret>())
            if (t.ammoType != null) { ammo = t.ammoType; break; }
        if (wing.torpedoBayModule != null && wing.torpedoBayModule.ammoType != null) ammo = wing.torpedoBayModule.ammoType;

        TractorableItem loot = null;
        try
        {
            // The wing's hold: a favourite stack, some of its own ammo, then ore until less than one unit of room is left.
            float flagUsed0 = flagship.spaceShipData.cargoUsed;
            var fav = wingCargo.Add(favItem, 2);
            if (fav != null) fav.favourite = true;
            if (ammo != null) wingCargo.Add(ammo, 10);
            int fill = Mathf.FloorToInt(wing.spaceShipData.GetCargoAvailable() / ore.m3);
            if (fill > 0) wingCargo.Add(ore, fill);
            float room0 = wing.spaceShipData.GetCargoAvailable();
            loot = Singleton<LootManager>.Instance.SpawnLootItem(new TractorableItemData
            {
                itemType = ore,
                itemAmount = 5,
                jettisoned = true,
                ownerFaction = Faction.player,
                position = (Vector2)wing.transform.position + new Vector2(3f, 0f),
            }, null);

            f.Overflow.Value = false;
            bool vanilla = tractor.CanFitInCargo(loot);
            f.Overflow.Value = true;
            FleetHolds.Forget(wing.spaceShipData);
            bool fits = tractor.CanFitInCargo(loot);
            float room1 = wing.spaceShipData.GetCargoAvailable();
            int favLeft = wingCargo.GetCount(favItem) - (before.TryGetValue(favItem, out int fv) ? fv : 0);
            int ammoLeft = ammo != null ? wingCargo.GetCount(ammo) : -1;
            float gained = flagship.spaceShipData.cargoUsed - flagUsed0;
            float want = Mathf.Min(5 * ore.m3 + wing.spaceShipData.cargoCapacity * FleetHolds.Headroom, wing.spaceShipData.cargoCapacity);
            string detail = $"{wing.spaceShipData.GetShipName()} hold {wing.spaceShipData.cargoCapacity:0} m3, room {room0:0.0} -> {room1:0.0} (wanted {want:0.0}); game alone fits={vanilla}, with overflow fits={fits}; flagship took {gained:0} m3; favourite {favItem.identifier} stack {favLeft}/2 and own ammo {ammoLeft} stayed";
            if (!vanilla && fits && room1 >= want - ore.m3 && gained >= room1 - room0 - 0.01f && favLeft == 2 && (ammo == null || ammoLeft >= 10)) ctx.Pass("overflow", detail);
            else ctx.Fail("overflow", detail);

            // Throttled: right after a hand-over, a ship doesn't try again for 2 s.
            int fill2 = Mathf.FloorToInt(wing.spaceShipData.GetCargoAvailable() / ore.m3);
            if (fill2 > 0) wingCargo.Add(ore, fill2);
            bool again = tractor.CanFitInCargo(loot);
            if (!again) ctx.Pass("throttle", $"refilled, second try within 2 s fits={again}");
            else ctx.Fail("throttle", "handed cargo over again within 2 s");
        }
        finally
        {
            f.Overflow.Value = overflow0;
            if (loot != null)
            {
                Singleton<LootManager>.Instance.RemoveLootItem(loot.data);
                Object.Destroy(loot.gameObject);
            }
            Restore(wingCargo, before);
            Restore(flagCargo, flagBefore);
        }
    }

    private static Dictionary<InventoryItemType, int> Counts(Inventory inv)
    {
        var d = new Dictionary<InventoryItemType, int>();
        foreach (var s in inv.items)
            if (s != null)
            {
                d.TryGetValue(s.item, out int n);
                d[s.item] = n + s.count;
            }
        return d;
    }

    // Puts each item type's count back; the check only added ore and ammo and moved stacks between the two ships.
    private static void Restore(Inventory inv, Dictionary<InventoryItemType, int> counts)
    {
        var now = Counts(inv);
        foreach (var kv in now)
        {
            counts.TryGetValue(kv.Key, out int want);
            if (kv.Value > want) inv.Remove(kv.Key, kv.Value - want);
        }
        foreach (var kv in counts)
        {
            now.TryGetValue(kv.Key, out int have);
            if (have < kv.Value) inv.Add(kv.Key, kv.Value - have);
        }
        foreach (var s in inv.items)
            if (s != null && s.favourite && !counts.ContainsKey(s.item)) s.favourite = false;
    }
}
