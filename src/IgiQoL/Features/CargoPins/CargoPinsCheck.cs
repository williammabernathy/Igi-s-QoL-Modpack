using System.Collections;
using System.Collections.Generic;
using Behaviour.Item;
using Behaviour.UI;
using Behaviour.UI.HUD;
using Behaviour.UI.Shared;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.UI;
using Source.Item;
using Source.Player;
using Source.SpaceShip;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.CargoPins;

internal static class CargoPinsCheck
{
    public static IEnumerator Run(SelfCheckContext ctx)
    {
        CheckAmmoData(ctx);

        var player = GamePlayer.current;
        SpaceShipData fleetShip = null;
        foreach (var s in Fleet.Ships(withMercenaries: false))
            if (s != player.currentSpaceShip) { fleetShip = s; break; }
        if (fleetShip == null)
        {
            ctx.Skip("move-all", "no fleet ship");
            yield break;
        }
        string guid = fleetShip.guid;
        var before = ShipSettings.Snapshot(guid);
        try
        {
            CheckRoundTrip(ctx, guid);
            CheckMoveAll(ctx, fleetShip);
        }
        finally { ShipSettings.Restore(guid, before); }

        yield return CheckSlot(ctx, fleetShip, before);
        yield return CheckPopup(ctx, fleetShip, before);
    }

    // The fit read from data names the same ammo as the game's check on the spawned ship.
    private static void CheckAmmoData(SelfCheckContext ctx)
    {
        int ships = 0, fired = 0, mismatches = 0;
        string first = null;
        foreach (var data in Fleet.Ships(withMercenaries: true))
        {
            var unit = Fleet.Unit(data);
            if (unit == null) continue;
            ships++;
            foreach (var t in InventoryItemType.all)
            {
                if (t.itemCategory != ItemCategory.Ammo && t.itemCategory != ItemCategory.Torpedo) continue;
                bool fromData = Items.FiredBy(data, t), fromUnit = unit.DoWeUseAmmo(t);
                if (fromData) fired++;
                if (fromData == fromUnit) continue;
                mismatches++;
                first ??= $"{data.GetShipName()} {t.identifier}: data={fromData} unit={fromUnit}";
            }
        }
        string detail = $"{ships} spawned ships, {fired} ammo types fired, {mismatches} mismatches{(first != null ? " (" + first + ")" : "")}";
        if (ships == 0) ctx.Skip("ammo-data", "no spawned ship");
        else if (mismatches == 0 && fired > 0) ctx.Pass("ammo-data", detail);
        else ctx.Fail("ammo-data", detail);
    }

    // Pins and the ammo switch go through Snapshot / Restore, the path loadouts use.
    private static void CheckRoundTrip(SelfCheckContext ctx, string guid)
    {
        var e = ShipSettings.Edit(guid);
        e.pins.Clear();
        e.pins.Add("SelfCheckPin");
        e.keepAmmo = true;
        ShipSettings.Save(guid);
        var snap = ShipSettings.Snapshot(guid);
        e = ShipSettings.Edit(guid);
        e.pins.Clear();
        e.keepAmmo = false;
        ShipSettings.Save(guid);
        var record = SaveData.Ship(guid, false);
        bool cleared = !snap["pins"].IsNull && (record == null || record["pins"].IsNull && record["keepAmmo"].IsNull);
        ShipSettings.Restore(guid, snap);
        var back = ShipSettings.Get(guid);
        string detail = $"snapshot={snap} restored pins=[{string.Join(",", back.pins)}] keepAmmo={back.keepAmmo} empty record dropped={cleared}";
        if (back.pins.Count == 1 && back.pins.Contains("SelfCheckPin") && back.keepAmmo && cleared) ctx.Pass("roundtrip", detail);
        else ctx.Fail("roundtrip", detail);
    }

    // Two test stacks on the fleet ship, one pinned: move-all into a test hold takes only the other, and a full hold
    // reports "didn't fit" only for stacks it tried to move. The ship's own stacks are pinned for the test so they stay.
    private static void CheckMoveAll(SelfCheckContext ctx, SpaceShipData ship)
    {
        var cargo = ship.cargo;
        var aboard = new HashSet<string>();
        foreach (var s in cargo.items) aboard.Add(s.item.identifier);
        InventoryItemType pinned = null, loose = null;
        foreach (var t in InventoryItemType.all)
        {
            if (t.itemCategory != ItemCategory.Ore || t.m3 <= 0f || aboard.Contains(t.identifier) || !t.CanGoInMaterials()) continue;
            if (pinned == null) pinned = t;
            else if (t.identifier != pinned.identifier) { loose = t; break; }
        }
        if (loose == null)
        {
            ctx.Skip("move-all", "no two ore types the ship doesn't carry");
            return;
        }
        var moveAll = AccessTools.Method(typeof(PlayerCargo), "MoveAll");
        var e = ShipSettings.Edit(ship.guid);
        e.pins.Clear();
        e.pins.UnionWith(aboard);
        e.pins.Add(pinned.identifier);
        e.keepAmmo = false;
        ShipSettings.Save(ship.guid);
        cargo.Add(pinned, 2);
        cargo.Add(loose, 2);
        try
        {
            var hold = new Inventory(false, false, () => 1000f);
            bool ok = (bool)moveAll.Invoke(null, new object[] { cargo, hold });
            string detail = $"pinned {pinned.identifier}: ship {cargo.GetCount(pinned)} hold {hold.GetCount(pinned)}; loose {loose.identifier}: ship {cargo.GetCount(loose)} hold {hold.GetCount(loose)}; result={ok}; own stacks kept={Kept(cargo, aboard)}";
            if (ok && cargo.GetCount(pinned) == 2 && hold.GetCount(pinned) == 0 && cargo.GetCount(loose) == 0 && hold.GetCount(loose) == 2 && Kept(cargo, aboard)) ctx.Pass("move-all", detail);
            else ctx.Fail("move-all", detail);
            cargo.Add(loose, hold.Remove(loose, 2));

            var full = new Inventory(false, false, () => 0f);
            bool tried = (bool)moveAll.Invoke(null, new object[] { cargo, full });
            ShipSettings.Edit(ship.guid).pins.Add(loose.identifier);
            ShipSettings.Save(ship.guid);
            bool none = (bool)moveAll.Invoke(null, new object[] { cargo, full });
            string fd = $"full hold: with a loose stack result={tried}, all pinned result={none}, hold empty={full.GetCount(loose) == 0}";
            if (!tried && none && full.GetCount(loose) == 0) ctx.Pass("move-all-full", fd);
            else ctx.Fail("move-all-full", fd);
        }
        finally
        {
            cargo.Remove(pinned, 2);
            cargo.Remove(loose, 2);
        }
    }

    private static bool Kept(Inventory cargo, HashSet<string> identifiers)
    {
        var left = new HashSet<string>();
        foreach (var s in cargo.items) left.Add(s.item.identifier);
        return left.IsSupersetOf(identifiers);
    }

    private static InventoryItemType FreeOre(SpaceShipData ship)
    {
        foreach (var t in InventoryItemType.all)
            if (t.itemCategory == ItemCategory.Ore && t.m3 > 0f && ship.cargo.GetCount(t) == 0) return t;
        return null;
    }

    // A real cargo slot showing a pinned stack gets the lock mark and the hint; unpinned, the mark goes away.
    private static IEnumerator CheckSlot(SelfCheckContext ctx, SpaceShipData ship, LightJson.JsonObject before)
    {
        var manager = InventoryInteractionManager.Instance;
        if (manager == null || manager.inventorySlotPrefab == null || HudManager.Instance == null)
        {
            ctx.Skip("slot", "no inventory slot prefab or HUD");
            yield break;
        }
        var ore = FreeOre(ship);
        if (ore == null)
        {
            ctx.Skip("slot", "no ore type the ship doesn't carry");
            yield break;
        }
        var stack = ship.cargo.Add(ore, 3);
        var holder = UiCards.NewChild("IgiQoL_PinCheck", HudManager.Instance.rootCanvasTransform);
        holder.sizeDelta = new Vector2(76f, 76f);
        var canvas = holder.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5;
        InventoryItemSlot slot = null;
        try
        {
            slot = Object.Instantiate(manager.inventorySlotPrefab, holder);
            ((RectTransform)slot.transform).sizeDelta = new Vector2(76f, 76f);
            slot.SetEntry(stack);
            string unpinnedHint = CargoPins.Hint(slot);
            CargoPins.Toggle(ship, ore);
            CargoPins.RefreshMarks(ship);
            var mark = slot.transform.Find(CargoPins.MarkName);
            bool shown = mark != null && mark.gameObject.activeSelf;
            string pinnedHint = CargoPins.Hint(slot);
            yield return ctx.Screenshot("cargo-pin-mark");
            CargoPins.Toggle(ship, ore);
            CargoPins.RefreshMarks(ship);
            bool hidden = mark != null && !mark.gameObject.activeSelf;
            string key = CargoPins.Instance.PinKey.Value.ToString();
            string detail = $"mark shown={shown} then hidden={hidden}; hints '{unpinnedHint}' / '{pinnedHint}'";
            if (shown && hidden && unpinnedHint != null && unpinnedHint.StartsWith(key + ": pin") && pinnedHint != null && pinnedHint.StartsWith("Pinned:")) ctx.Pass("slot", detail);
            else ctx.Fail("slot", detail);
        }
        finally
        {
            Object.Destroy(holder.gameObject);
            ship.cargo.Remove(ore, 3);
            ShipSettings.Restore(ship.guid, before);
        }
    }

    // The popup lists what's aboard, pins and unpins from its own row, and still fits on screen.
    private static IEnumerator CheckPopup(SelfCheckContext ctx, SpaceShipData ship, LightJson.JsonObject before)
    {
        // A stack aboard, so the pin row has an item to pin.
        var ore = FreeOre(ship);
        if (ore != null) ship.cargo.Add(ore, 3);
        try
        {
            ShipPopup.Open(ship);
            yield return null;
            if (!ShipPopup.IsOpen)
            {
                ctx.Fail("popup", "popup didn't open");
                yield break;
            }
            bool row = false, toggle = false;
            foreach (var t in ShipPopup.Body.GetComponentsInChildren<TMP_Text>(true))
            {
                row |= t.text == "Pin an item";
                toggle |= t.text == "Keep the ammo this ship fires on board";
            }
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            ((RectTransform)ShipPopup.Body.parent).GetWorldCorners(corners);
            var root = ShipPopup.Body.GetComponentInParent<Canvas>().rootCanvas;
            var cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            Vector2 low = RectTransformUtility.WorldToScreenPoint(cam, corners[0]), high = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            bool fits = screen.Contains(low) && screen.Contains(high);
            string detail = $"pin row={row} ammo switch={toggle} frame {low.y:0}..{high.y:0} of {Screen.height} fits={fits}";
            if (row && toggle && fits) ctx.Pass("popup", detail);
            else ctx.Fail("popup", detail);
            yield return ctx.Screenshot("ship-popup-pins");

            UnityEngine.UI.Button pin = null;
            foreach (var b in ShipPopup.Body.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                var label = b.GetComponentInChildren<TMP_Text>();
                if (label != null && (label.text == "Pin" || label.text == "Unpin")) { pin = b; break; }
            }
            if (pin == null || ore == null) ctx.Skip("popup-pin", pin == null ? "no Pin button" : "no ore type the ship doesn't carry");
            else
            {
                int n0 = ShipSettings.Get(ship.guid).pins.Count;
                pin.onClick.Invoke();
                yield return null;
                int n1 = ShipSettings.Get(ship.guid).pins.Count;
                string pd = $"pins {n0} -> {n1}, popup reopened={ShipPopup.IsOpen}";
                if (System.Math.Abs(n1 - n0) == 1 && ShipPopup.IsOpen) ctx.Pass("popup-pin", pd);
                else ctx.Fail("popup-pin", pd);
            }
        }
        finally
        {
            ShipPopup.Close();
            if (ore != null) ship.cargo.Remove(ore, 3);
            ShipSettings.Restore(ship.guid, before);
        }
    }
}
