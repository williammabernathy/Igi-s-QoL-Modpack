using System;
using System.Collections.Generic;
using Behavior.UI.GalaxyMap;
using Behaviour.UI.HUD;
using Behaviour.UI.Shared;
using Behaviour.UI.Side_Menu.SideTabs.Captain;
using IgiQoL.Features.Window;
using Source.Player;
using Source.UI.Tooltip;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.UI;

// The mod's tabbed window. Tabs register from their feature's OnActivated; content is rebuilt on every tab switch.
public static class ModWindow
{
    public sealed class Tab
    {
        public string Id, Title;
        public int Order;
        public Action<RectTransform> Build;
    }

    private sealed class WindowRoot : MonoBehaviour
    {
        private void OnDestroy()
        {
            if (root == (RectTransform)transform)
            {
                root = null;
                body = null;
                scroll = null;
                tabButtons.Clear();
            }
        }
    }

    private const string PrefX = "IgiQoL.Window.X", PrefY = "IgiQoL.Window.Y", PrefTab = "IgiQoL.Window.Tab";

    private static readonly List<Tab> tabs = new();
    private static readonly Dictionary<string, Button> tabButtons = new();
    private static RectTransform root, body, scroll;

    public static bool IsOpen => root != null;
    public static string CurrentTab { get; private set; }
    public static RectTransform Content { get; private set; }

    public static void AddTab(string id, string title, int order, Action<RectTransform> build)
    {
        if (WindowFeature.Instance == null || !WindowFeature.Instance.Active)
        {
            Plugin.Log.LogInfo($"[Window] tab '{id}' not added: Window feature inactive");
            return;
        }
        Register(id, title, order, build);
    }

    // Used by WindowFeature.OnActivated, which runs before the registry marks the feature Active.
    internal static void Register(string id, string title, int order, Action<RectTransform> build)
    {
        tabs.Add(new Tab { Id = id, Title = title, Order = order, Build = build });
        tabs.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public static bool HasTab(string title) => tabs.Exists(t => t.Title == title);

    public static void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public static void Open(string tabId = null)
    {
        if (GamePlayer.current == null || HudManager.Instance == null || tabs.Count == 0) return;
        if (!IsOpen) Build();
        Show(tabId ?? PlayerPrefs.GetString(PrefTab, tabs[0].Id));
    }

    public static void Close()
    {
        if (root != null) Object.Destroy(root.gameObject);
        root = null;
    }

    public static void Show(string id)
    {
        if (!IsOpen) return;
        var tab = Find(id) ?? tabs[0];
        // Tabs rebuild themselves from their own button handlers, so the old content is destroyed at end of frame.
        if (scroll != null)
        {
            scroll.gameObject.SetActive(false);
            Object.Destroy(scroll.gameObject);
        }
        Content = Kit.Scroll(body);
        scroll = (RectTransform)Content.GetComponentInParent<ScrollRect>().transform;
        foreach (var kv in tabButtons)
            ((Image)kv.Value.targetGraphic).color = kv.Key == tab.Id ? ColorHelper.mapDockButtonActive : ColorHelper.panelButton;
        CurrentTab = tab.Id;
        PlayerPrefs.SetString(PrefTab, tab.Id);
        try { tab.Build(Content); }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[Window] tab '{tab.Id}' failed to build: {e}");
            Kit.Muted(Content, "This tab failed to build; see BepInEx/LogOutput.log.");
        }
    }

    private static Tab Find(string id)
    {
        foreach (var t in tabs)
            if (t.Id == id) return t;
        return null;
    }

    private static void Build()
    {
        var position = new Vector2(PlayerPrefs.GetFloat(PrefX, 0f), PlayerPrefs.GetFloat(PrefY, 0f));
        // Tab captions grow with the Text size setting; each tab is at least 90 px and wide enough for its caption,
        // and the window widens so they keep fitting.
        float grow = Mathf.Max(1f, Features.TextScale.TextScale.Factor);
        root = Kit.Window("IgiQoL_Window", 760f * grow, 520f, position);
        root.gameObject.AddComponent<WindowRoot>();

        var header = Kit.Row(root, 6f, 26f);
        header.name = "Header";
        header.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 2, 0, 0);
        UiCards.AddBackground(header.gameObject, ColorHelper.panelHeaderBlue);
        FleetBarParts.AddIcon(header, Kit.Icon("Info", PoiIconKeys.Captain("Command")), ColorHelper.stationCardTitle, 16f);
        FleetBarParts.AddLine(header, "Igi's QoL", 12f, ColorHelper.stationCardTitle, TextAlignmentOptions.MidlineLeft);
        tabButtons.Clear();
        float tabsWidth = 0f;
        foreach (var tab in tabs)
        {
            string id = tab.Id;
            var button = Kit.TextButton(header, tab.Title, () => Show(id), 90f * grow);
            var caption = button.GetComponentInChildren<TMP_Text>();
            float width = Mathf.Max(90f * grow, caption != null ? Mathf.Ceil(caption.GetPreferredValues(tab.Title).x) + 12f : 0f);
            CaptainCards.SetFixedSize((RectTransform)button.transform, width, 22f);
            tabsWidth += width + 6f;
            tabButtons[id] = button;
        }
        root.sizeDelta = new Vector2(Mathf.Max(760f * grow, 150f * grow + tabsWidth), root.sizeDelta.y);
        CaptainCards.AddSpacer(header);
        Kit.Tile(header, PoiIconKeys.Fleet("Close"), ColorHelper.reddish, 22f, Close, "Close");
        var drag = header.gameObject.AddComponent<DragHandle>();
        drag.target = root;
        drag.onEndDrag = p =>
        {
            PlayerPrefs.SetFloat(PrefX, p.x);
            PlayerPrefs.SetFloat(PrefY, p.y);
        };

        body = UiCards.NewChild("Body", root);
        body.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        UiCards.SetColumn(body.gameObject.AddComponent<VerticalLayoutGroup>());
        PopoutStrip.Add(root);
        scroll = null;
    }
}
