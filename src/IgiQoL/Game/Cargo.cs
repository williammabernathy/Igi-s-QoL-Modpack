using System;
using Behaviour.Item;
using Source.Galaxy.POI;
using Source.Item;
using Source.Player;
using Source.SpaceShip;
using UnityEngine;

namespace IgiQoL.Game;

public static class Cargo
{
    public static int Count(SpaceShipData ship, InventoryItemType item)
    {
        int count = ship.cargo.GetCount(item);
        if (Items.GoesToDataInventory(item)) count += GamePlayer.current.dataInventory.GetCount(item);
        return count;
    }

    // Moves up to `max` of `item`, never more than `to` has room for. Returns the amount moved.
    public static int Move(Inventory from, Inventory to, InventoryItemType item, int max)
    {
        int have = from.GetCount(item);
        int fit = item.m3 > 0f ? Mathf.FloorToInt(to.GetSpaceAvailable() / item.m3) : have;
        int want = Math.Min(Math.Min(have, fit), max);
        if (want <= 0) return 0;
        int removed = from.Remove(item, want);
        if (removed > 0) to.Add(item, removed);
        return removed;
    }

    // Where ECHO's DropFoundItem puts the item at this station; null when it fits neither storage.
    public static Inventory DepositTarget(SpaceStation station, InventoryItemType item) =>
        item.CanGoInArmory() ? GamePlayer.current.globalInventory : item.CanGoInMaterials() ? station.materialStorage : null;
}
