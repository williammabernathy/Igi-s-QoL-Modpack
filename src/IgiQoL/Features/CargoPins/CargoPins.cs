using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Item;
using Behaviour.UI;
using Behaviour.UI.Tooltip;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.UI;
using Source.Item;
using Source.Player;
using Source.SpaceShip;
using Source.Util;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.CargoPins;

// Pinned item types stay aboard a fleet ship when the fleet cargo's "move all to flagship" button (the gold crown)
// empties it. That button is the only caller of PlayerCargo.MoveAll, so ECHO's unload, the fleet auto-deposit and the
// Deposit button are untouched.
public sealed class CargoPins : Feature
{
    public static CargoPins Instance;

    public CargoPins() { Instance = this; }

    public override string Id => "CargoPins";
    public override string Title => "Pinned cargo";
    public override string Description => "Pin item types to a fleet ship so the fleet cargo's move-all-to-flagship button (the gold crown) leaves them aboard; everything else still moves in one click. Hover over a stack in a fleet ship's cargo and press the pin key, or use the ship's settings popup, which also has a switch to keep the ammo the ship fires.";
    public override Type[] PatchClasses => new[] { typeof(CargoPinsPatches) };

    public ConfigEntry<Key> PinKey;

    internal const string MarkName = "IgiQoL_PinMark";

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        PinKey = cfg.Bind(Id, "PinKey", Key.P, SettingInfo.Describe("Pin key",
            "Press it while the pointer is over a stack in a fleet ship's cargo to pin that item type to the ship, or unpin it. Type a key name such as P or K. Ignored while you type in a text field."));
    }

    public static bool HasPins(ShipSettings s) => s.pins.Count > 0 || s.keepAmmo;

    // Whether this item stays aboard the ship on "move all to flagship": pinned by hand, or ammo its fit fires.
    public static bool Pinned(SpaceShipData ship, InventoryItemType item)
    {
        var s = ShipSettings.Get(ship.guid);
        return s.pins.Contains(item.identifier) || s.keepAmmo && Items.FiredBy(ship, item);
    }

    // The ship whose cargo this is, without PlayerCargo.OwnerOf's closures: it runs on every cargo slot refresh.
    public static SpaceShipData Owner(Inventory inventory)
    {
        var player = GamePlayer.current;
        if (inventory == null || player == null) return null;
        if (player.currentSpaceShip != null && player.currentSpaceShip.cargo == inventory) return player.currentSpaceShip;
        foreach (var s in player.spaceShips) if (s != null && s.cargo == inventory) return s;
        foreach (var s in player.activeFleet) if (s != null && s.cargo == inventory) return s;
        return null;
    }

    // A ship whose cargo the crown button empties: one of yours, not the one you fly, not a hired mercenary.
    public static SpaceShipData PinnableOwner(Inventory inventory)
    {
        var ship = Owner(inventory);
        return ship != null && ship != GamePlayer.current.currentSpaceShip && PlayerCargo.CanManage(ship) ? ship : null;
    }

    public static void Toggle(SpaceShipData ship, InventoryItemType item)
    {
        var s = ShipSettings.Edit(ship.guid);
        if (!s.pins.Remove(item.identifier)) s.pins.Add(item.identifier);
        ShipSettings.Save(ship.guid);
    }

    // Shows or hides the lock mark on a cargo slot. The mark is created the first time a slot needs it.
    public static void Mark(InventoryItemSlot slot)
    {
        var stack = slot.contained;
        SpaceShipData ship;
        bool show = stack != null && (ship = PinnableOwner(slot.inventory)) != null && Pinned(ship, stack.item);
        var mark = slot.transform.Find(MarkName);
        if (mark == null)
        {
            if (!show) return;
            mark = CreateMark(slot.transform);
        }
        if (mark.gameObject.activeSelf != show) mark.gameObject.SetActive(show);
    }

    // Bottom left; the game uses the top left for the owner dot of the fleet cargo's "all ships" view.
    private static Transform CreateMark(Transform slot)
    {
        var rt = new GameObject(MarkName, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(slot, false);
        rt.pivot = rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.anchoredPosition = new Vector2(4f, 4f);
        rt.sizeDelta = new Vector2(14f, 14f);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = PoiIconLibrary.Instance.Get(Kit.Icon("Ok", "Lock"));
        image.color = ColorHelper.gold;
        image.raycastTarget = false;
        rt.gameObject.SetActive(false);
        return rt;
    }

    // Redraws the marks of every slot showing this ship's cargo, after a pin changed.
    public static void RefreshMarks(SpaceShipData ship)
    {
        foreach (var slot in Object.FindObjectsByType<InventoryItemSlot>(FindObjectsInactive.Exclude))
            if (slot.inventory == ship.cargo) Mark(slot);
        UITooltip.Refresh();
    }

    // The tooltip line for a stack in a fleet ship's cargo, or null for other slots.
    public static string Hint(InventoryItemSlot slot)
    {
        var stack = slot.contained;
        var ship = stack != null ? PinnableOwner(slot.inventory) : null;
        if (ship == null) return null;
        string key = Instance.PinKey.Value.ToString();
        if (ShipSettings.Get(ship.guid).pins.Contains(stack.item.identifier)) return "Pinned: the crown button leaves it aboard. " + key + " to unpin";
        if (Pinned(ship, stack.item)) return "The crown button leaves it aboard: ammo this ship fires";
        return key + ": pin to this ship, so the crown button leaves it aboard";
    }

    // Item types for the popup's pin list: what's aboard, then pins for items no longer aboard. Sorted by name.
    public static List<InventoryItemType> PinChoices(SpaceShipData ship, out List<string> orphanPins)
    {
        var list = new List<InventoryItemType>();
        var seen = new HashSet<string>();
        foreach (var stack in ship.cargo.items)
            if (stack?.item != null && seen.Add(stack.item.identifier)) list.Add(stack.item);
        list.Sort((a, b) => string.Compare(Translation.Translate(a.displayName), Translation.Translate(b.displayName), StringComparison.CurrentCulture));
        orphanPins = new List<string>();
        foreach (var id in ShipSettings.Get(ship.guid).pins)
            if (!seen.Contains(id)) orphanPins.Add(id);
        orphanPins.Sort(StringComparer.Ordinal);
        return list;
    }

    public static string PinName(string identifier) =>
        InventoryItemType.TryGet(identifier, out var item) && item != null ? Translation.Translate(item.displayName) : identifier;

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => CargoPinsCheck.Run(ctx);
}

[HarmonyPatch]
internal static class CargoPinsPatches
{
    private static CargoPins F => CargoPins.Instance;

    // Vanilla MoveAll with pinned stacks skipped. The result ("everything fitted", else @CargoFlagshipFull) counts only
    // the stacks it tried to move. Ships without pins run the vanilla body.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerCargo), "MoveAll")]
    private static bool MoveAllPrefix(Inventory from, Inventory to, ref bool __result)
    {
        if (!F.Active) return true;
        try
        {
            var ship = CargoPins.PinnableOwner(from);
            if (ship == null || !CargoPins.HasPins(ShipSettings.Get(ship.guid))) return true;
            bool all = true;
            foreach (var stack in new List<Inventory.InventoryItem>(from.items))
            {
                if (CargoPins.Pinned(ship, stack.item)) continue;
                int count = stack.count;
                all &= PlayerCargo.MoveStack(from, stack, count, to) == count;
            }
            __result = all;
            return false;
        }
        catch (Exception e)
        {
            F.Fail(e, nameof(MoveAllPrefix));
            return true;
        }
    }

    // The pin key over a fleet ship's cargo slot, as the game's F toggles a favourite.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryItemSlot), "Update")]
    private static void SlotUpdatePostfix(InventoryItemSlot __instance)
    {
        if (!__instance.mouseOver || !F.Active) return;
        var key = F.PinKey.Value;
        var kb = Keyboard.current;
        if (kb == null || key <= Key.None || (int)key > Keyboard.KeyCount) return;
        try
        {
            if (!kb[key].wasPressedThisFrame || __instance.contained == null || UIHelper.IsTypingInInputField()) return;
            var ship = CargoPins.PinnableOwner(__instance.inventory);
            if (ship == null) return;
            CargoPins.Toggle(ship, __instance.contained.item);
            CargoPins.RefreshMarks(ship);
        }
        catch (Exception e) { F.Fail(e, nameof(SlotUpdatePostfix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryItemSlot), nameof(InventoryItemSlot.UpdateItem))]
    private static void UpdateItemPostfix(InventoryItemSlot __instance)
    {
        if (!F.Active) return;
        try { CargoPins.Mark(__instance); }
        catch (Exception e) { F.Fail(e, nameof(UpdateItemPostfix)); }
    }

    // Under the game's own click hints, which this runs after; only for interactive slot tooltips.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CompareTooltip), "AddMoveAcrossHint")]
    private static void AddMoveAcrossHintPostfix(CompareTooltip __instance, TooltipSource tt)
    {
        if (!F.Active) return;
        try
        {
            var slot = tt != null ? tt.GetComponent<InventoryItemSlot>() : null;
            if (slot == null) return;
            string hint = CargoPins.Hint(slot);
            if (hint != null) __instance.AddTextLine(hint);
        }
        catch (Exception e) { F.Fail(e, nameof(AddMoveAcrossHintPostfix)); }
    }
}
