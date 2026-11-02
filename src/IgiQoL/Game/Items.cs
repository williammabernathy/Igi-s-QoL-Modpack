using Behaviour.Equipment.Module;
using Behaviour.Equipment.Turret;
using Behaviour.Item;
using Behaviour.Item.Usable;
using Source.Item;
using Source.Player;
using Source.SpaceShip;

namespace IgiQoL.Game;

public static class Items
{
    public static string RuleKey(InventoryItemType item) =>
        item.itemLevel > 0 ? $"{item.identifier}@{item.itemLevel}" : item.identifier;

    // What every mover and seller leaves aboard a ship: favourites, ammo the ship's fit fires (read from the fit, so it
    // holds for ships that aren't spawned), the usables the game keeps aboard (IdleManager.ShouldKeepInCargo), warp fuel
    // and items a mission still needs.
    public static bool KeepAboard(SpaceShipData ship, Inventory.InventoryItem stack)
    {
        if (stack.favourite) return true;
        var item = stack.item;
        if (item.itemCategory == ItemCategory.Ammo) return FiredBy(ship, item);
        if (item.itemCategory == ItemCategory.Usable) return item.storageOverride == StorageOverride.None;
        if (item.TryGetState<WarpFuelState>(out _)) return true;
        return GamePlayer.current.RequiredItemCountForMissions(item) > 0;
    }

    // Whether the ship's fitted turrets or torpedo bay fire this ammo. Read from the fit, as the game's private
    // SpaceShipData.GetAmmoUsedByTurrets does, so it works for ships that aren't spawned; AbstractUnit.DoWeUseAmmo
    // makes the same comparison on a spawned unit.
    public static bool FiredBy(SpaceShipData ship, InventoryItemType ammo)
    {
        var definition = ammo.definition;
        if (definition == null) return false;
        if (ship.hardpoints != null)
            foreach (var fitted in ship.hardpoints)
                if (fitted != null && fitted.TryGetComponent<AbstractTurret>(out var turret) && turret.ammoType == definition) return true;
        var bay = ship.GetEquippedItem(EquipmentSlot.TopedoBay);
        return bay != null && bay.TryGetComponent<TorpedoBayModule>(out var torpedoes) && torpedoes.ammoType == definition;
    }
}
