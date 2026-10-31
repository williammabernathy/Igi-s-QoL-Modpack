using System.Collections;
using Behavior.UI.GalaxyMap;
using Behaviour.UI;
using Behaviour.UI.HUD;
using Behaviour.UI.Spacestation;
using Behaviour.UI.Tooltip;
using Behaviour.Util;
using HarmonyLib;
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
        if (kb == null || key == Key.None || !kb[key].wasPressedThisFrame) return;
        if (UIHelper.IsTypingInInputField() || AlertPopup.IsOpen || GamePlayer.current == null || HudManager.Instance == null) return;
        try { ModWindow.Toggle(); }
        catch (System.Exception e) { feature.Fail(e, nameof(WindowHotkey)); }
    }
}

// The "QoL" buttons that toggle the mod window. In space: a copy of the HUD dock button above it, shown while
// the game shows its HUD. Docked: a station tab like the game's Fleet button, floating above the rightmost tab.
// Both live in the game's hierarchy and die with it.
public static class HudButton
{
    public static GameObject Instance;
    public static GameObject StationInstance;

    private static string Tooltip => "Igi's QoL Modpack (" + WindowFeature.Instance.Hotkey.Value + ")";

    public static IEnumerator CreateWhenReady()
    {
        float deadline = Time.realtimeSinceStartup + 60f;
        while (HudManager.Instance == null || HudManager.Instance.dockButton == null)
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Plugin.Log.LogInfo("[Window] no dock button to clone; hotkey only");
                yield break;
            }
            yield return null;
        }
        if (Instance != null) yield break;
        try { Create(HudManager.Instance.dockButton); }
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

        var parent = src.transform.parent;
        if (parent.GetComponent<LayoutGroup>() != null)
            go.transform.SetSiblingIndex(src.transform.GetSiblingIndex());
        else
        {
            var srcRt = (RectTransform)src.transform;
            ((RectTransform)go.transform).anchoredPosition = srcRt.anchoredPosition + new Vector2(0f, srcRt.rect.height + 4f);
        }
        FleetBarParts.SetTooltip(b, null, Tooltip);
        Instance = go;

        var sb = new System.Text.StringBuilder();
        foreach (var c in go.GetComponents<Component>()) sb.Append(c.GetType().Name).Append(' ');
        Plugin.Log.LogInfo($"[Window] HUD button created under {parent.name} (layout={parent.GetComponent<LayoutGroup>() != null}, label={(label != null ? "text" : "icon")}, comps: {sb})");
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
        var spec = new FleetButtonSpec("IgiQoL_StationButton", "QoL", Tooltip, ColorHelper.offWhite, null, null);
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
