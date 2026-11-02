using System;
using System.Collections.Generic;
using Behavior.UI.GalaxyMap;
using Behaviour.UI.HUD;
using Behaviour.UI.Shared;
using Behaviour.UI.Side_Menu.SideTabs.Captain;
using IgiQoL.Core;
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
// You size it with the corner handle; tabs that don't fit the header go into a "More" dropdown at its end.
public static class ModWindow
{
    public sealed class Tab
    {
        public Feature Owner;
        public string Id, Title;
        public int Order;
        public Action<RectTransform> Build;
        internal float width;
        internal Button button;
    }

    private sealed class WindowRoot : MonoBehaviour
    {
        // The frame after a tab is shown its rows built in their own Start or first Update exist, so it's measured then.
        private void LateUpdate()
        {
            if (measureFrame < 0 || Time.frameCount < measureFrame || root != (RectTransform)transform) return;
            measureFrame = -1;
            try { FitTab(); }
            catch (Exception e) { WindowFeature.Instance.Fail(e, "WindowRoot.LateUpdate"); }
        }

        private void OnDestroy()
        {
            if (root == (RectTransform)transform)
            {
                root = null;
                body = null;
                scroll = null;
                header = null;
                more = null;
            }
        }
    }

    private const string PrefX = "IgiQoL.Window.X", PrefY = "IgiQoL.Window.Y", PrefTab = "IgiQoL.Window.Tab";
    private const string PrefW = "IgiQoL.Window.W", PrefH = "IgiQoL.Window.H";
    private const float DefaultWidth = 760f, DefaultHeight = 520f, FloorWidth = 480f, MinHeight = 300f;
    // Window padding (6 + 6) around the header and body.
    private const float Padding = 12f;
    private const float MoreWidth = 120f;

    private static readonly List<Tab> tabs = new();
    // The tabs of the open window: registered tabs whose owner is active when it was built.
    private static readonly List<Tab> shown = new();
    private static RectTransform root, body, scroll, header;
    private static Kit.Choice more;
    // Tabs shown in the header; the rest are in the More dropdown.
    private static int fitting = -1;
    private static float grow, titleWidth, chosenWidth, chosenHeight, tabMinWidth;
    private static int measureFrame = -1;

    public static bool IsOpen => root != null;
    public static string CurrentTab { get; private set; }
    public static RectTransform Content { get; private set; }

    // Tabs register from any feature's OnActivated, in any registry order. Whether the window may open is decided in
    // Open, and a tab whose owner is off is left out of the window (owner null: always shown).
    public static void AddTab(Feature owner, string id, string title, int order, Action<RectTransform> build)
    {
        tabs.Add(new Tab { Owner = owner, Id = id, Title = title, Order = order, Build = build });
        tabs.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    private static bool Shown(Tab t) => t.Owner == null || t.Owner.Active;

    public static bool HasTab(string title) => tabs.Exists(t => t.Title == title && Shown(t));

    // For the self-check: the window, its tabs, how many fit the header (the rest are in More) and More's caption.
    internal static RectTransform Root => root;
    internal static IReadOnlyList<Tab> Tabs => shown;
    internal static int HeaderTabCount => fitting;
    internal static string MoreCaption => more == null ? null : more.root.TryGetComponent<TMP_Dropdown>(out var dd) ? dd.captionText.text : more.root.GetComponentInChildren<TMP_Text>().text;
    internal static float MinWidth => Mathf.Max(FloorWidth * grow, tabMinWidth);

    public static void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public static void Open(string tabId = null)
    {
        if (WindowFeature.Instance == null || !WindowFeature.Instance.Active || GamePlayer.current == null || HudManager.Instance == null) return;
        if (!IsOpen) Build();
        if (shown.Count == 0)
        {
            Close();
            return;
        }
        Show(tabId ?? PlayerPrefs.GetString(PrefTab, shown[0].Id));
    }

    public static void Close()
    {
        if (root != null) Object.Destroy(root.gameObject);
        root = null;
    }

    public static void Show(string id)
    {
        if (!IsOpen) return;
        var tab = Find(id);
        // The owner failed or was switched off while the window was open: rebuild the window without its tab.
        if (tab != null && !Shown(tab))
        {
            Close();
            Open();
            return;
        }
        if (tab == null) tab = shown[0];
        // Tabs rebuild themselves from their own button handlers, so the old content is destroyed at end of frame.
        if (scroll != null)
        {
            scroll.gameObject.SetActive(false);
            Object.Destroy(scroll.gameObject);
        }
        Content = Kit.Scroll(body);
        scroll = (RectTransform)Content.GetComponentInParent<ScrollRect>().transform;
        foreach (var t in shown)
            ((Image)t.button.targetGraphic).color = t == tab ? ColorHelper.mapDockButtonActive : ColorHelper.panelButton;
        CurrentTab = tab.Id;
        PlayerPrefs.SetString(PrefTab, tab.Id);
        SyncMore();
        try { tab.Build(Content); }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[Window] tab '{tab.Id}' failed to build: {e}");
            Kit.Muted(Content, "This tab failed to build; see BepInEx/LogOutput.log.");
        }
        FitTab();
        measureFrame = Time.frameCount + 1;
    }

    private static Tab Find(string id)
    {
        foreach (var t in shown)
            if (t.Id == id) return t;
        return null;
    }

    private static void Build()
    {
        shown.Clear();
        foreach (var t in tabs)
            if (Shown(t)) shown.Add(t);
        var position = new Vector2(PlayerPrefs.GetFloat(PrefX, 0f), PlayerPrefs.GetFloat(PrefY, 0f));
        // Tab captions grow with the Text size setting; each tab is at least 90 px and wide enough for its caption.
        grow = Mathf.Max(1f, Features.TextScale.TextScale.Factor);
        chosenWidth = PlayerPrefs.GetFloat(PrefW, DefaultWidth * grow);
        chosenHeight = PlayerPrefs.GetFloat(PrefH, DefaultHeight);
        tabMinWidth = 0f;
        root = Kit.Window("IgiQoL_Window", chosenWidth, chosenHeight, position);
        root.gameObject.AddComponent<WindowRoot>();

        header = Kit.Row(root, 6f, 26f);
        header.name = "Header";
        header.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 2, 0, 0);
        UiCards.AddBackground(header.gameObject, ColorHelper.panelHeaderBlue);
        FleetBarParts.AddIcon(header, Kit.Icon("Info", PoiIconKeys.Captain("Command")), ColorHelper.stationCardTitle, 16f);
        var title = FleetBarParts.AddLine(header, "Igi's QoL", 12f, ColorHelper.stationCardTitle, TextAlignmentOptions.MidlineLeft);
        titleWidth = Mathf.Ceil(title.GetPreferredValues(title.text).x);
        foreach (var tab in shown)
        {
            string id = tab.Id;
            var button = Kit.TextButton(header, tab.Title, () => Show(id), 90f * grow);
            var caption = button.GetComponentInChildren<TMP_Text>();
            tab.width = Mathf.Max(90f * grow, caption != null ? Mathf.Ceil(caption.GetPreferredValues(tab.Title).x) + 12f : 0f);
            CaptainCards.SetFixedSize((RectTransform)button.transform, tab.width, 22f);
            tab.button = button;
        }
        more = null;
        fitting = -1;
        CaptainCards.AddSpacer(header);
        Kit.Tile(header, PoiIconKeys.Fleet("Close"), ColorHelper.reddish, 22f, Close, "Close");
        var drag = header.gameObject.AddComponent<DragHandle>();
        drag.target = root;
        drag.onEndDrag = _ =>
        {
            ClampToScreen();
            SaveLayout();
        };

        body = UiCards.NewChild("Body", root);
        body.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        UiCards.SetColumn(body.gameObject.AddComponent<VerticalLayoutGroup>());
        PopoutStrip.Add(root);
        var handle = ResizeHandle.Add(root);
        handle.onResize = ResizeBy;
        handle.onEnd = SaveLayout;
        scroll = null;
        ApplySize(chosenWidth, chosenHeight);
    }

    // The window is as wide as you made it, or wider while the open tab needs more.
    private static void FitTab()
    {
        if (root == null) return;
        tabMinWidth = Mathf.Ceil(MinRowWidth(body)) + Padding;
        ApplySize(chosenWidth, chosenHeight);
    }

    // Resizes keeping the top-left corner put (the window's pivot is its centre), inside the screen.
    private static void ApplySize(float width, float height)
    {
        var canvas = CanvasSize();
        var size = root.sizeDelta;
        var pos = root.anchoredPosition;
        float left = pos.x - size.x / 2f, top = pos.y + size.y / 2f;
        float w = Mathf.Min(Mathf.Max(width, MinWidth), canvas.x);
        float h = Mathf.Clamp(height, Mathf.Min(MinHeight, canvas.y), canvas.y);
        root.sizeDelta = new Vector2(w, h);
        root.anchoredPosition = new Vector2(left + w / 2f, top - h / 2f);
        ClampToScreen();
        LayoutHeader();
    }

    private static void ResizeBy(Vector2 delta)
    {
        var canvas = CanvasSize();
        var size = root.sizeDelta;
        var pos = root.anchoredPosition;
        // The bottom-right corner follows the mouse up to the screen's edge.
        float maxW = canvas.x / 2f - (pos.x - size.x / 2f), maxH = (pos.y + size.y / 2f) + canvas.y / 2f;
        chosenWidth = Mathf.Clamp(size.x + delta.x, MinWidth, Mathf.Max(MinWidth, maxW));
        chosenHeight = Mathf.Clamp(size.y - delta.y, MinHeight, Mathf.Max(MinHeight, maxH));
        ApplySize(chosenWidth, chosenHeight);
    }

    private static void SaveLayout()
    {
        if (root == null) return;
        PlayerPrefs.SetFloat(PrefX, root.anchoredPosition.x);
        PlayerPrefs.SetFloat(PrefY, root.anchoredPosition.y);
        PlayerPrefs.SetFloat(PrefW, chosenWidth);
        PlayerPrefs.SetFloat(PrefH, chosenHeight);
    }

    private static Vector2 CanvasSize() => ((RectTransform)root.parent).rect.size;

    // A position or size saved at a larger resolution or text size comes back inside the screen.
    private static void ClampToScreen()
    {
        var canvas = CanvasSize();
        var size = root.sizeDelta;
        var pos = root.anchoredPosition;
        float hx = Mathf.Max(0f, (canvas.x - size.x) / 2f), hy = Mathf.Max(0f, (canvas.y - size.y) / 2f);
        root.anchoredPosition = new Vector2(Mathf.Clamp(pos.x, -hx, hx), Mathf.Clamp(pos.y, -hy, hy));
    }

    // Header: padding, icon, title, the tabs that fit, More, spacer, close; 6 px between neighbours.
    private static void LayoutHeader()
    {
        float room = root.sizeDelta.x - Padding - (8f + 16f + titleWidth + 22f + 3 * 6f);
        float all = 0f;
        foreach (var t in shown) all += t.width + 6f;
        int fit = shown.Count;
        if (all > room)
        {
            float left = room - (MoreWidth * grow + 6f);
            fit = 0;
            while (fit < shown.Count && shown[fit].width + 6f <= left)
            {
                left -= shown[fit].width + 6f;
                fit++;
            }
        }
        if (fit == fitting) return;
        fitting = fit;
        for (int i = 0; i < shown.Count; i++) shown[i].button.gameObject.SetActive(i < fit);
        if (more != null)
        {
            more.root.SetActive(false);
            Object.Destroy(more.root);
            more = null;
        }
        if (fit == shown.Count) return;
        var names = new List<string> { "More" };
        var ids = new List<string>();
        for (int i = fit; i < shown.Count; i++)
        {
            names.Add(shown[i].Title);
            ids.Add(shown[i].Id);
        }
        more = Kit.Dropdown(header, names, 0, i =>
        {
            if (i > 0) Show(ids[i - 1]);
        }, MoreWidth * grow);
        more.root.transform.SetSiblingIndex(shown[shown.Count - 1].button.transform.GetSiblingIndex() + 1);
        SyncMore();
    }

    // The dropdown names the open tab when it's one of its own, and reads "More" otherwise.
    private static void SyncMore()
    {
        if (more == null) return;
        int index = 0;
        for (int i = fitting; i < shown.Count; i++)
            if (shown[i].Id == CurrentTab) index = i - fitting + 1;
        more.SetIndex(index);
    }

    // The narrowest width rows fit in: a row adds up its fixed-width parts (anything with a minimum width, or a preferred
    // width and no flexible width, as the game's text buttons have), a column takes its widest row, a scroll view adds
    // its scrollbar, and flexible labels count as nothing (they shrink and cut off with "...").
    private static float MinRowWidth(RectTransform t)
    {
        var le = t.GetComponent<LayoutElement>();
        if (le != null && (le.minWidth > 0f || le.flexibleWidth <= 0f && le.preferredWidth > 0f)) return Mathf.Max(le.minWidth, le.preferredWidth);
        var scrollRect = t.GetComponent<ScrollRect>();
        if (scrollRect != null) return scrollRect.content != null ? MinRowWidth(scrollRect.content) + UiScrollView.ScrollbarWidth : 0f;
        var group = t.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (group == null) return 0f;
        bool row = group is HorizontalLayoutGroup;
        float sum = 0f, widest = 0f;
        int count = 0;
        for (int i = 0; i < t.childCount; i++)
        {
            var child = t.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf) continue;
            var childLe = child.GetComponent<LayoutElement>();
            if (childLe != null && childLe.ignoreLayout) continue;
            float w = MinRowWidth(child);
            sum += w;
            widest = Mathf.Max(widest, w);
            count++;
        }
        return group.padding.horizontal + (row ? sum + group.spacing * Mathf.Max(0, count - 1) : widest);
    }
}
