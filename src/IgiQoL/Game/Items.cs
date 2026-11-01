using Behaviour.Equipment.Module;
using Behaviour.Equipment.Turret;
using Behaviour.Item;
using Source.Item;
using Source.SpaceShip;

namespace IgiQoL.Game;

public static class Items
{
    public static string RuleKey(InventoryItemType item) =>
        item.itemLevel > 0 ? $"{item.identifier}@{item.itemLevel}" : item.identifier;

    public static bool GoesToDataInventory(InventoryItemType item) => item.CanGoInDataInventory();

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
