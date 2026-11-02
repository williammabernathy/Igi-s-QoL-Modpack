using System.Collections;
using Behavior.UI.GalaxyMap;
using Behaviour.UI;
using Behaviour.UI.HUD;
using Behaviour.UI.Spacestation;
using Behaviour.UI.Tooltip;
using Behaviour.Util;
using HarmonyLib;
using Behaviour.UI.Side_Menu;
using IgiQoL.UI;
using Source.Galaxy.POI;
using Source.Player;
using Source.UI.Tooltip;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.Window;

public sealed class WindowHotkey : MonoBehaviour
{
    private void Update()
    {
        var feature = WindowFeature.Instance;
        if (!feature.Active) return;
        var key = feature.Hotkey.Value;
        var kb = Keyboard.current;
        if (kb == null || key <= Key.None || (int)key > Keyboard.KeyCount) return;
        try
        {
            if (!kb[key].wasPressedThisFrame) return;
            if (UIHelper.IsTypingInInputField() || AlertPopup.IsOpen || GamePlayer.current == null || HudManager.Instance == null) return;
            ModWindow.Toggle();
        }
        catch (System.Exception e) { feature.Fail(e, nameof(WindowHotkey)); }
    }
}

// The "QoL" buttons that toggle the mod window. In space: a copy of the HUD dock button, shown while the game shows
// its HUD, on top of the side menu column or above the Dock button (WindowFeature.Place), draggable from there.
// Docked: a station tab like the game's Fleet button, floating above the rightmost tab.
// Both live in the game's hierarchy and die with it.
public static class HudButton
{
    public static GameObject Instance;
    public static GameObject StationInstance;

    // Where the button sits in its place before any drag, in its parent's anchored coordinates.
    private static Vector2 home;
    private static readonly Vector3[] corners = new Vector3[4], bounds = new Vector3[4];

    private static string Tooltip(bool draggable) =>
        "Igi's QoL Modpack (" + WindowFeature.Instance.Hotkey.Value + ")" + (draggable ? ". Drag to move it." : "");

    // Shows the hotkey as it is now after it changed on the Settings tab.
    internal static void RefreshTooltips()
    {
        if (Instance != null)
        {
            var t = Instance.GetComponentInChildren<TooltipSource>(true);
            if (t != null) t.BodyText = Tooltip(true);
        }
        if (StationInstance != null)
        {
            var t = StationInstance.GetComponentInChildren<TooltipSource>(true);
            if (t != null) t.BodyText = Tooltip(false);
        }
    }

    // The drag offset from home, kept per place.
    private static string Pref(string axis) => "IgiQoL.HudButton." + WindowFeature.Instance.Place.Value + "." + axis;

    internal static Vector2 Offset
    {
        get => new(PlayerPrefs.GetFloat(Pref("X"), 0f), PlayerPrefs.GetFloat(Pref("Y"), 0f));
        set
        {
            PlayerPrefs.SetFloat(Pref("X"), value.x);
            PlayerPrefs.SetFloat(Pref("Y"), value.y);
        }
    }

    public static IEnumerator CreateWhenReady()
    {
        float deadline = Time.realtimeSinceStartup + 60f;
        // The side menu scene loads with the HUD but asynchronously; without it the button goes above the Dock button.
        while (SidePanel.instance == null && Time.realtimeSinceStartup < deadline) yield return null;
        if (Instance != null || HudManager.Instance == null || !WindowFeature.Instance.Active) yield break;
        var dock = HudManager.Instance.dockButton;
        if (dock == null)
        {
            Plugin.Log.LogInfo("[Window] no dock button to clone; hotkey only");
            yield break;
        }
        try { Create(dock); }
        catch (System.Exception e) { WindowFeature.Instance.Fail(e, nameof(HudButton)); }
    }

    private static void Create(Button src)
    {
        var go = Object.Instantiate(src.gameObject, src.transform.parent);
        go.name = "IgiQoL_HudButton";
        go.SetActive(HudManager.Instance.showHud);
        // Destroyed immediately so SetTooltip below does not reuse a component that is about to go away.
        foreach (var t in go.GetComponentsInChildren<TooltipSource>(true)) Object.DestroyImmediate(t);
        foreach (var c in go.GetComponentsInChildren<ITooltipCardSource>(true))
            if (c is Component comp) Object.DestroyImmediate(comp);
        foreach (var t in go.GetComponentsInChildren<Translatable>(true)) Object.DestroyImmediate(t);

        var b = go.GetComponent<Button>();
        b.onClick = new Button.ButtonClickedEvent();
        b.onClick.AddListener(ModWindow.Toggle);
        var label = go.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "QoL";
        else
        {
            foreach (var img in go.GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject == go) continue;
                img.sprite = PoiIconLibrary.Instance.Get(Kit.Icon("Info", PoiIconKeys.Captain("Command")));
                break;
            }
        }
        FleetBarParts.SetTooltip(b, null, Tooltip(true));

        // The side menu column has a layout group; the button sits outside its flow.
        var layout = go.GetComponent<LayoutElement>();
        if (layout == null) layout = go.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
        var drag = go.AddComponent<DragHandle>();
        drag.target = (RectTransform)go.transform;
        drag.onEndDrag = _ => EndDrag();
        Instance = go;
        Place();
        Plugin.Log.LogInfo($"[Window] HUD button created under {go.transform.parent.name} (label={(label != null ? "text" : "icon")})");
    }

    internal static void Place()
    {
        if (Instance == null) return;
        var rt = (RectTransform)Instance.transform;
        var dock = HudManager.Instance != null ? HudManager.Instance.dockButton : null;
        var menu = SidePanel.instance != null ? SidePanel.instance.transform.Find("SidePanelMenu") as RectTransform : null;
        if (WindowFeature.Instance.Place.Value == ButtonPlace.SideMenu && menu != null)
        {
            // A square as wide as the column, just above its top button. The side panel slides as a whole, so the
            // button follows the column when the panel opens and closes.
            rt.SetParent(menu, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = Vector2.zero;
            float side = menu.rect.width;
            rt.sizeDelta = new Vector2(side, side);
            home = new Vector2(0f, 4f);
        }
        else
        {
            if (dock == null) return;
            var dockRt = (RectTransform)dock.transform;
            rt.SetParent(dockRt.parent, false);
            rt.anchorMin = dockRt.anchorMin;
            rt.anchorMax = dockRt.anchorMax;
            rt.pivot = dockRt.pivot;
            rt.sizeDelta = dockRt.sizeDelta;
            home = dockRt.anchoredPosition + new Vector2(0f, dockRt.rect.height + 4f);
        }
        rt.anchoredPosition = home + Offset;
    }

    public static void ResetOffset()
    {
        Offset = Vector2.zero;
        Place();
    }

    // Keeps the button on screen, then remembers where it was dropped relative to its place.
    private static void EndDrag()
    {
        var rt = (RectTransform)Instance.transform;
        rt.GetWorldCorners(corners);
        ((RectTransform)rt.GetComponentInParent<Canvas>().rootCanvas.transform).GetWorldCorners(bounds);
        var shift = Vector3.zero;
        if (corners[0].x < bounds[0].x) shift.x = bounds[0].x - corners[0].x;
        else if (corners[2].x > bounds[2].x) shift.x = bounds[2].x - corners[2].x;
        if (corners[0].y < bounds[0].y) shift.y = bounds[0].y - corners[0].y;
        else if (corners[2].y > bounds[2].y) shift.y = bounds[2].y - corners[2].y;
        rt.position += shift;
        Offset = rt.anchoredPosition - home;
    }

    internal static void SetHudVisible(bool visible)
    {
        if (Instance != null && Instance.activeSelf != visible) Instance.SetActive(visible);
    }

    internal static void CreateStationButton(SpaceStationInterior interior)
    {
        // Awake returns early (no buttons) when it runs without a station or player.
        if (interior.buttons == null) return;
        SpaceStationButton below = null;
        var tabs = interior.tabParent;
        for (int i = tabs.childCount - 1; i >= 0 && below == null; i--)
        {
            var tab = tabs.GetChild(i).GetComponent<SpaceStationButton>();
            // The game's own Fleet button floats above the Personal Hangar tab.
            if (tab != null && tab.gameObject.activeSelf && tab.spaceStationFacility != SpaceStationFacility.PersonalHangar) below = tab;
        }
        if (below == null)
        {
            Plugin.Log.LogInfo("[Window] no station tab to put the QoL button on; hotkey only while docked");
            return;
        }
        var icon = PoiIconLibrary.Instance.Get(Kit.Icon("Info", PoiIconKeys.Captain("Command")));
        var spec = new FleetButtonSpec("IgiQoL_StationButton", "QoL", Tooltip(false), ColorHelper.offWhite, null, null);
        var placed = PersonalFleetButton.Create(interior.tabButtonPrefab, (RectTransform)below.transform, interior.transform, icon, null, spec);
        placed.GetComponent<SpaceStationButton>().clickOverride = ModWindow.Toggle;
        StationInstance = placed.gameObject;
    }
}

[HarmonyPatch]
internal static class HudButtonPatches
{
    // Docking, dialogues and the ECHO idle screen hide the HUD through this; the dock button follows it too.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.ToggleHudElements))]
    private static void ToggleHudPostfix(bool visible)
    {
        if (!WindowFeature.Instance.Active) return;
        try { HudButton.SetHudVisible(visible); }
        catch (System.Exception e) { WindowFeature.Instance.Fail(e, nameof(HudButtonPatches)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpaceStationInterior), nameof(SpaceStationInterior.Awake))]
    private static void StationAwakePostfix(SpaceStationInterior __instance)
    {
        if (!WindowFeature.Instance.Active) return;
        try { HudButton.CreateStationButton(__instance); }
        catch (System.Exception e) { WindowFeature.Instance.Fail(e, nameof(HudButtonPatches)); }
    }
}
