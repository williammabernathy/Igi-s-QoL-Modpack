using System;
using System.Collections;
using Behaviour.UI.HUD;
using Behaviour.UI.Side_Menu;
using Behaviour.UI.Spacestation;
using BepInEx.Configuration;
using IgiQoL.Core;
using IgiQoL.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IgiQoL.Features.Window;

public enum ButtonPlace { SideMenu, DockButton }

public sealed class WindowFeature : Feature
{
    public static WindowFeature Instance;

    public WindowFeature() { Instance = this; }

    public override string Id => "Window";
    public override string Title => "Mod window";
    public override string Description => "This window. Open and close it with the hotkey below, the QoL button in space (on top of the side menu or above the Dock button, see below), or the QoL tab above the station tabs while docked.";
    public override Type[] PatchClasses => new[] { typeof(HudButtonPatches) };

    public ConfigEntry<Key> Hotkey;
    public ConfigEntry<ButtonPlace> Place;
    public ConfigEntry<Key> PopoutInteractKey;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        Hotkey = cfg.Bind(Id, "Hotkey", Key.F6, SettingInfo.Describe("Hotkey",
            "Key that opens and closes this window. Type a key name such as F6, F7 or Backquote. Ignored while you type in a text field."));
        Place = cfg.Bind(Id, "ButtonPlace", ButtonPlace.SideMenu, SettingInfo.Describe("QoL button in space",
            "Where the QoL button sits while you fly. On top of the side menu: above the column of side menu buttons at the right, moving with them when the side panel opens and closes. Above the Dock button: the bottom middle of the HUD, where it stays put. " +
            "You can also drag the button; it keeps that distance from its place until you press Reset below.",
            "On top of the side menu", "Above the Dock button"));
        PopoutInteractKey = cfg.Bind(Id, "PopoutInteractKey", Key.LeftCtrl, SettingInfo.Describe("Pop-out windows: key to use a click-through window",
            "A pop-out window with click-through on lets your clicks on its body reach the game. Hold this key to use the window itself instead; its header always works. Type a key name such as LeftCtrl, LeftShift or LeftAlt; either Ctrl, Shift or Alt key counts. Ignored while you type in a text field."));
    }

    public override void OnActivated()
    {
        Plugin.Instance.gameObject.AddComponent<WindowHotkey>();
        GameEvents.HudReady += OnHudReady;
        Place.SettingChanged += (_, _) =>
        {
            try { HudButton.Place(); }
            catch (Exception e) { Fail(e, nameof(Place)); }
        };
        Hotkey.SettingChanged += (_, _) =>
        {
            try { HudButton.RefreshTooltips(); }
            catch (Exception e) { Fail(e, nameof(Hotkey)); }
        };
        ModWindow.Register("settings", "Settings", 90, BuildSettings);
    }

    private void OnHudReady()
    {
        if (!Active) return;
        Plugin.Instance.StartCoroutine(HudButton.CreateWhenReady());
    }

    private static void BuildSettings(RectTransform content)
    {
        Kit.Muted(content, "Every change is saved at once (to BepInEx/config/igi.qol.cfg). Hover over a setting's name for details. Features with their own tab (ECHO, Operations, Item rules) keep their settings there; this tab only has their on/off switch.");
        foreach (var feature in FeatureRegistry.All)
        {
            if (!feature.HasEnabledEntry || feature.Id == "Dev") continue;
            SettingRows.AddSection(content, feature);
            // A feature whose tab isn't there (its owner switched off) shows its settings here instead.
            if (feature.SettingsTab == null || !ModWindow.HasTab(feature.SettingsTab))
            {
                SettingRows.AddEntries(content, feature.Id);
                if (feature == Instance) AddButtonReset(content);
                continue;
            }
            SettingRows.AddEntries(content, feature.Id, SettingRows.Rows.EnabledOnly);
            Kit.Muted(content, "Its settings are on the " + feature.SettingsTab + " tab.");
        }
    }

    // Not a config entry, so the row is named apart from the entry rows the self-check counts.
    private static void AddButtonReset(RectTransform content)
    {
        var row = Kit.Row(content);
        row.name = "ResetRow";
        var label = Kit.Label(row, "QoL button position");
        Kit.Tooltip(label, "QoL button position", "Puts the QoL button back on its place after you dragged it.");
        Kit.TextButton(row, "Reset", HudButton.ResetOffset, 90f);
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        ModWindow.Open("settings");
        ctx.Cleanup(ModWindow.Close);
        // Settings shows every entry outside [Dev], except those of features whose settings live on another tab (only their Enabled).
        int entries = 0;
        foreach (var kv in Plugin.Instance.Config)
        {
            if (kv.Key.Section == "Dev") continue;
            var owner = Array.Find(FeatureRegistry.All, f => f.Id == kv.Key.Section);
            if (kv.Key.Key == "Enabled" || owner == null || owner.SettingsTab == null) entries++;
        }
        int rows = 0;
        if (ModWindow.IsOpen)
        {
            var content = ModWindow.Content;
            for (int i = 0; i < content.childCount; i++)
                if (content.GetChild(i).name == "Row") rows++;
        }
        string detail = $"rows={rows} entries={entries} tab={ModWindow.CurrentTab}";
        if (ModWindow.IsOpen && rows == entries) ctx.Pass("window-open", detail);
        else ctx.Fail("window-open", detail + $" open={ModWindow.IsOpen}");
        yield return ctx.Screenshot("window-settings");
        yield return WindowResizeCheck.Run(ctx);

        if (HudButton.Instance == null) ctx.Skip("hud-button", "no dock button in this scene");
        else
        {
            // Hidden with the HUD (docked, dialogues), so it never covers station screens.
            bool shown = HudButton.Instance.activeSelf, hud = HudManager.Instance.showHud;
            string hudDetail = $"parent={HudButton.Instance.transform.parent.name} active={shown} showHud={hud}";
            if (shown == hud) ctx.Pass("hud-button", hudDetail);
            else ctx.Fail("hud-button", hudDetail);
        }

        if (SpaceStationInterior.instance == null) ctx.Skip("station-button", "not docked");
        else if (HudButton.StationInstance == null) ctx.Fail("station-button", "docked but no QoL station tab");
        else
        {
            var placed = HudButton.StationInstance.GetComponent<PersonalFleetButton>();
            string stationDetail = $"above {(placed.target != null ? placed.target.name : "-")} rect={placed.rectTransform.rect.width:0.#}x{placed.rectTransform.rect.height:0.#}";
            if (placed.target != null) ctx.Pass("station-button", stationDetail);
            else ctx.Fail("station-button", stationDetail);
        }

        // Undock and check the HUD button comes back with the HUD (the station tab dies with the station screen).
        if (SpaceStationInterior.instance != null && HudButton.StationInstance != null)
        {
            ModWindow.Close();
            yield return ctx.Screenshot("docked");
            SpaceStationInterior.instance.ExitSpacestation();
            float until = Time.realtimeSinceStartup + 20f;
            while ((SpaceStationInterior.instance != null || !HudManager.Instance.showHud) && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSeconds(2f);
            bool back = HudButton.Instance != null && HudButton.Instance.activeSelf;
            string undockDetail = $"docked={SpaceStationInterior.instance != null} showHud={HudManager.Instance.showHud} hudButton={back} stationTab={HudButton.StationInstance != null}";
            if (SpaceStationInterior.instance == null && back && HudButton.StationInstance == null) ctx.Pass("undock", undockDetail);
            else ctx.Fail("undock", undockDetail);
            yield return ctx.Screenshot("undocked");
        }

        if (HudButton.Instance == null || !HudButton.Instance.activeSelf || SidePanel.instance == null)
        {
            ctx.Skip("button-place", "no QoL button in space");
            yield break;
        }
        yield return ButtonChecks(ctx);
    }

    private IEnumerator ButtonChecks(SelfCheckContext ctx)
    {
        ModWindow.Close();
        var rt = (RectTransform)HudButton.Instance.transform;
        var menu = (RectTransform)SidePanel.instance.transform.Find("SidePanelMenu");
        var corners = new Vector3[4];
        var menuCorners = new Vector3[4];
        // Measured in world space: left edges line up and the button's bottom is 4 units above the column's top.
        string Measure(out bool onTop)
        {
            rt.GetWorldCorners(corners);
            menu.GetWorldCorners(menuCorners);
            float scale = rt.lossyScale.x;
            float dx = (corners[0].x - menuCorners[0].x) / scale, dy = (corners[0].y - menuCorners[1].y) / scale;
            onTop = rt.parent == menu && Mathf.Abs(dx) < 0.5f && Mathf.Abs(dy - 4f) < 0.5f;
            return $"parent={rt.parent.name} dx={dx:0.#} dy={dy:0.#} size={rt.rect.width:0.#}x{rt.rect.height:0.#} menuX={menuCorners[0].x:0}";
        }

        // The check moves the button; the player's own place and drag offset come back afterwards.
        var savedPlace = Place.Value;
        Place.Value = ButtonPlace.SideMenu;
        var savedOffset = HudButton.Offset;
        ctx.Cleanup(() =>
        {
            Place.Value = ButtonPlace.SideMenu;
            HudButton.Offset = savedOffset;
            Place.Value = savedPlace;
            HudButton.Place();
        });
        HudButton.ResetOffset();
        yield return null;
        string first = Measure(out bool placed);
        if (placed) ctx.Pass("button-place", first);
        else ctx.Fail("button-place", first);
        bool wasOpen = SidePanel.instance.open;
        yield return ctx.Screenshot(wasOpen ? "qol-button-panel-open" : "qol-button-panel-closed");

        // Opening or closing the side panel slides the column 730 units; the button has to come along.
        SidePanel.instance.ToggleTab(SidePanel.SideTabType.Inventory);
        yield return new WaitForSecondsRealtime(1f);
        string second = Measure(out bool follows);
        string followDetail = $"open {wasOpen}->{SidePanel.instance.open}: {second}";
        if (follows && SidePanel.instance.open != wasOpen) ctx.Pass("button-follows", followDetail);
        else ctx.Fail("button-follows", followDetail);
        yield return ctx.Screenshot(wasOpen ? "qol-button-panel-closed" : "qol-button-panel-open");
        SidePanel.instance.ToggleTab(SidePanel.SideTabType.Inventory);
        yield return new WaitForSecondsRealtime(1f);

        // A drag moves the button, is no click, and survives re-placing; a drag off screen is pulled back.
        var drag = HudButton.Instance.GetComponent<DragHandle>();
        float canvasScale = rt.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
        var ev = new PointerEventData(EventSystem.current) { delta = new Vector2(-200f, 50f) * canvasScale, eligibleForClick = true };
        var start = rt.anchoredPosition;
        drag.OnBeginDrag(ev);
        drag.OnDrag(ev);
        drag.OnEndDrag(ev);
        HudButton.Place();
        var moved = rt.anchoredPosition - start;
        string dragDetail = $"moved={moved} click={ev.eligibleForClick} offset={HudButton.Offset}";
        if (!ev.eligibleForClick && Vector2.Distance(moved, new Vector2(-200f, 50f)) < 0.5f) ctx.Pass("button-drag", dragDetail);
        else ctx.Fail("button-drag", dragDetail);

        ev.delta = new Vector2(100000f, 100000f);
        drag.OnBeginDrag(ev);
        drag.OnDrag(ev);
        drag.OnEndDrag(ev);
        var screen = (RectTransform)rt.GetComponentInParent<Canvas>().rootCanvas.transform;
        rt.GetWorldCorners(corners);
        screen.GetWorldCorners(menuCorners);
        bool inside = corners[0].x >= menuCorners[0].x - 0.5f && corners[2].x <= menuCorners[2].x + 0.5f && corners[0].y >= menuCorners[0].y - 0.5f && corners[2].y <= menuCorners[2].y + 0.5f;
        string clampDetail = $"button x {corners[0].x:0}-{corners[2].x:0} y {corners[0].y:0}-{corners[2].y:0}, screen {menuCorners[2].x:0}x{menuCorners[2].y:0}";
        if (inside) ctx.Pass("button-clamp", clampDetail);
        else ctx.Fail("button-clamp", clampDetail);
        yield return ctx.Screenshot("qol-button-dragged");
        HudButton.ResetOffset();
        string reset = Measure(out bool back);
        if (back) ctx.Pass("button-reset", reset);
        else ctx.Fail("button-reset", reset);

        // The other place: above the Dock button, in the dock button's parent.
        Place.Value = ButtonPlace.DockButton;
        yield return null;
        var dock = (RectTransform)HudManager.Instance.dockButton.transform;
        var expected = dock.anchoredPosition + new Vector2(0f, dock.rect.height + 4f) + HudButton.Offset;
        string dockDetail = $"parent={rt.parent.name} pos={rt.anchoredPosition} expected={expected}";
        if (rt.parent == dock.parent && Vector2.Distance(rt.anchoredPosition, expected) < 0.5f) ctx.Pass("button-dock", dockDetail);
        else ctx.Fail("button-dock", dockDetail);
        yield return ctx.Screenshot("qol-button-dock");
    }
}
