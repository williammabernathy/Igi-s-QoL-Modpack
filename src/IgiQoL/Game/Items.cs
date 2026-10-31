using Behaviour.Item;

namespace IgiQoL.Game;

public static class Items
{
    public static string RuleKey(InventoryItemType item) =>
        item.itemLevel > 0 ? $"{item.identifier}@{item.itemLevel}" : item.identifier;

    public static bool GoesToDataInventory(InventoryItemType item) => item.CanGoInDataInventory();
}
