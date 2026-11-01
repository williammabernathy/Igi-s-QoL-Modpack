using System;
using System.Collections.Generic;
using Behavior.UI.GalaxyMap;
using Behaviour.UI;
using Behaviour.UI.HUD;
using Behaviour.UI.Shared;
using Behaviour.UI.Side_Menu.SideTabs.Captain;
using IgiQoL.Core;
using IgiQoL.Features.Window;
using LightJson;
using Source.UI.Tooltip;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.UI;

public enum PopoutShow { Always, WhenRelevant }

// One kind of floating window, registered once by the feature that owns it.
public sealed class PopoutKind
{
    public string Id, Title;
    public Feature Owner;
    public Vector2 DefaultSize = new(480f, 320f);
    public Vector2 MinSize = new(260f, 120f);
    public PopoutShow DefaultShow = PopoutShow.Always;
    // What "When relevant" means, as the options list shows it ("in combat"), and its test.
    public string RelevantLabel;
    public Func<bool> Relevant;
    // Fills the body; the kind's own rows in the options panel (may be null).
    public Action<Popout, RectTransform> Build;
    public Action<Popout, RectTransform> BuildOptions;
}

// Layout and options of one pop-out kind. Layout belongs to the machine, not the save: one PlayerPrefs JSON string
// per kind, beside the mod window's own position.
public sealed class PopoutState
{
    public bool open, placed, collapsed, clickThrough = true;
    public float x, y, w, h;
    public int opacity = 100;
    public PopoutShow show;
    // The kind's own options.
    public JsonObject options = new();

    internal static string Pref(string id) => "IgiQoL.Popout." + id;

    internal static PopoutState Load(PopoutKind kind)
    {
        var s = new PopoutState { show = kind.DefaultShow, w = kind.DefaultSize.x, h = kind.DefaultSize.y };
        string text = PlayerPrefs.GetString(Pref(kind.Id), "");
        if (text.Length == 0) return s;
        JsonObject o;
        try { o = JsonValue.Parse(text).AsJsonObject; }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Window] pop-out '{kind.Id}' layout unreadable, using defaults: {e.Message}");
            return s;
        }
        if (o == null) return s;
        s.open = o["open"].AsBoolean;
        s.placed = o["placed"].AsBoolean;
        s.collapsed = o["collapsed"].AsBoolean;
        if (o["clickThrough"].IsBoolean) s.clickThrough = o["clickThrough"].AsBoolean;
        s.x = (float)o["x"].AsNumber;
        s.y = (float)o["y"].AsNumber;
        if (o["w"].IsNumber) s.w = (float)o["w"].AsNumber;
        if (o["h"].IsNumber) s.h = (float)o["h"].AsNumber;
        if (o["opacity"].IsNumber) s.opacity = Mathf.Clamp((int)o["opacity"].AsNumber, Popout.MinOpacity, 100);
        if (o["show"].IsString && Enum.TryParse(o["show"].AsString, out PopoutShow show)) s.show = show;
        if (o["options"].IsJsonObject) s.options = o["options"].AsJsonObject;
        return s;
    }

    internal void Save(string id)
    {
        var o = new JsonObject();
        o["open"] = open;
        o["placed"] = placed;
        o["collapsed"] = collapsed;
        o["clickThrough"] = clickThrough;
        o["x"] = x;
        o["y"] = y;
        o["w"] = w;
        o["h"] = h;
        o["opacity"] = opacity;
        o["show"] = show.ToString();
        o["options"] = options;
        PlayerPrefs.SetString(Pref(id), o.ToString());
    }
}

// The open pop-outs. They live under the HUD canvas and die with it on every save load, so the ones left open come
// back on HudReady.
public static class Popouts
{
    private static readonly List<PopoutKind> kinds = new();
    private static readonly Dictionary<string, PopoutState> states = new();
    private static readonly Dictionary<string, Popout> open = new();

    // Goes up when a pop-out opens, closes, hides or shows, so the mod window's strip knows to redraw.
    public static int Version { get; internal set; }

    public static IReadOnlyList<PopoutKind> Kinds => kinds;

    public static void Register(PopoutKind kind)
    {
        if (kinds.Count == 0) GameEvents.HudReady += RestoreOpen;
        kinds.Add(kind);
    }

    public static PopoutState State(string id)
    {
        if (states.TryGetValue(id, out var s)) return s;
        var kind = Find(id);
        if (kind == null) return null;
        s = PopoutState.Load(kind);
        states[id] = s;
        return s;
    }

    public static Popout Get(string id) => open.TryGetValue(id, out var p) && p != null ? p : null;

    public static bool IsOpen(string id) => Get(id) != null;

    public static void Toggle(string id)
    {
        if (IsOpen(id)) Close(id);
        else Open(id);
    }

    public static void Open(string id)
    {
        var kind = Find(id);
        if (kind == null || IsOpen(id) || HudManager.Instance == null) return;
        var s = State(id);
        s.open = true;
        s.Save(id);
        open[id] = Popout.Create(kind, s);
        Version++;
    }

    public static void Close(string id)
    {
        var s = State(id);
        if (s != null)
        {
            s.open = false;
            s.Save(id);
        }
        var p = Get(id);
        if (p != null) Object.Destroy(p.gameObject);
        open.Remove(id);
        Version++;
    }

    // Builds an open pop-out again, keeping its state (after an option or the text size changed).
    public static void Rebuild(string id)
    {
        var p = Get(id);
        if (p == null) return;
        Object.Destroy(p.gameObject);
        open[id] = Popout.Create(p.Kind, p.State);
    }

    public static void RebuildAll()
    {
        foreach (var kind in kinds) Rebuild(kind.Id);
    }

    internal static void Forget(Popout p)
    {
        if (open.TryGetValue(p.Kind.Id, out var current) && current == p)
        {
            open.Remove(p.Kind.Id);
            Version++;
        }
    }

    // Drops the cached state, so the next State() reads PlayerPrefs again.
    internal static void ForgetState(string id) => states.Remove(id);

    internal static PopoutKind Find(string id)
    {
        foreach (var k in kinds)
            if (k.Id == id) return k;
        return null;
    }

    private static void RestoreOpen()
    {
        foreach (var kind in kinds)
        {
            if (kind.Owner != null && !kind.Owner.Active) continue;
            var s = State(kind.Id);
            if (!s.open || IsOpen(kind.Id)) continue;
            try
            {
                open[kind.Id] = Popout.Create(kind, s);
                Version++;
            }
            catch (Exception e)
            {
                if (kind.Owner != null) kind.Owner.Fail(e, "Popout " + kind.Id);
                else Plugin.Log.LogError($"[Window] pop-out '{kind.Id}' failed to build: {e}");
            }
        }
    }
}

// A floating window modelled on the game's fleet panel: a header that drags (kept on screen), collapses and opens the
// options, a body that can let clicks through to the game, a corner resize handle and an opacity setting.
public sealed class Popout : MonoBehaviour
{
    public const float HeaderHeight = 26f;
    public const int MinOpacity = 20;
    private const float ShowCheckSeconds = 0.25f;
    private const float FlashSeconds = 0.8f;

    private static readonly string[] OpacityNames = { "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%" };

    public PopoutKind Kind { get; private set; }
    public PopoutState State { get; private set; }
    // Under the show condition the frame hides but the pop-out stays open and keeps its place.
    public bool Shown => frame != null && frame.activeSelf;
    public bool ClicksPass => bodyGroup != null && !bodyGroup.blocksRaycasts;
    public RectTransform Body { get; private set; }

    private RectTransform root;
    private GameObject frame, options, resize, body;
    private CanvasGroup rootGroup, bodyGroup;
    private Image headerBackground;
    private TMP_Text collapseText;
    private float nextShowCheck, flashUntil;

    internal static Popout Create(PopoutKind kind, PopoutState state)
    {
        var root = UiCards.NewChild("IgiQoL_Popout_" + kind.Id, HudManager.Instance.rootCanvasTransform);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0f, 1f);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2; // under the mod window (3), so an open pop-out never covers a tab
        root.gameObject.AddComponent<GraphicRaycaster>();
        var p = root.gameObject.AddComponent<Popout>();
        p.Kind = kind;
        p.State = state;
        p.root = root;
        p.rootGroup = root.gameObject.AddComponent<CanvasGroup>();
        p.Build();
        return p;
    }

    private void Build()
    {
        var frameRt = UiCards.NewChild("Frame", root);
        UiCards.Stretch(frameRt);
        frame = frameRt.gameObject;
        UiCards.SetColumn(frame.AddComponent<VerticalLayoutGroup>());
        WindowBorder.Add(frameRt);

        // The header carries the controls and stays clickable whatever the body does.
        var header = Kit.Row(frameRt, 6f, HeaderHeight);
        header.name = "Header";
        header.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 2, 0, 0);
        headerBackground = UiCards.AddBackground(header.gameObject, ColorHelper.panelHeaderBlue);
        FleetBarParts.AddLine(header, Kind.Title, 12f, ColorHelper.stationCardTitle, TextAlignmentOptions.MidlineLeft);
        CaptainCards.AddSpacer(header);
        Kit.Tooltip(Kit.TextButton(header, "Options", ToggleOptions, 64f).GetComponentInChildren<TMP_Text>(), null,
            "Show condition, opacity, click-through and position of this window.");
        var collapse = Kit.TextTile(header, State.collapsed ? "+" : "-", 22f, ToggleCollapsed);
        collapseText = collapse.GetComponentInChildren<TMP_Text>();
        Kit.Tile(header, PoiIconKeys.Fleet("Close"), ColorHelper.reddish, 22f, () => Popouts.Close(Kind.Id), "Close");
        var drag = header.gameObject.AddComponent<DragHandle>();
        drag.target = root;
        drag.onEndDrag = _ =>
        {
            Clamp();
            Persist();
        };

        // Outside the body, so it stays usable while clicks pass through the body.
        var optionsRt = Kit.Column(frameRt, 4f);
        optionsRt.name = "Options";
        UiCards.AddBackground(optionsRt.gameObject, ColorHelper.panelHeaderBlue);
        optionsRt.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(6, 6, 4, 4);
        options = optionsRt.gameObject;
        BuildOptions(optionsRt);
        options.SetActive(false);

        var bodyRt = UiCards.NewChild("Body", frameRt);
        body = bodyRt.gameObject;
        body.AddComponent<LayoutElement>().flexibleHeight = 1f;
        var layout = body.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 4f;
        UiCards.SetColumn(layout);
        // The body's own background, so with click-through on nothing of the window catches a click there.
        UiCards.AddBackground(body, ColorHelper.panelBlue);
        bodyGroup = body.AddComponent<CanvasGroup>();
        Body = bodyRt;
        Kind.Build(this, bodyRt);

        var resizeRt = UiCards.NewChild("Resize", root);
        resizeRt.anchorMin = resizeRt.anchorMax = resizeRt.pivot = new Vector2(1f, 0f);
        resizeRt.sizeDelta = new Vector2(14f, 14f);
        resizeRt.anchoredPosition = Vector2.zero;
        UiCards.AddBackground(resizeRt.gameObject, ColorHelper.resizeHandleIdle);
        resizeRt.gameObject.AddComponent<ResizeHandle>().owner = this;
        resize = resizeRt.gameObject;

        ApplyLayout();
        ApplyCollapsed();
        rootGroup.alpha = State.opacity / 100f;
        bodyGroup.blocksRaycasts = !State.clickThrough;
        nextShowCheck = 0f;
        UpdateShown();
    }

    private void BuildOptions(RectTransform column)
    {
        var row = Kit.Row(column, 8f, 26f);
        Kit.Label(row, "Show");
        Kit.Dropdown(row, new[] { "Always", "Only when " + (Kind.RelevantLabel ?? "relevant") }, (int)State.show, i =>
        {
            State.show = (PopoutShow)i;
            State.Save(Kind.Id);
            nextShowCheck = 0f;
        }, 200f);

        row = Kit.Row(column, 8f, 26f);
        Kit.Label(row, "Opacity");
        Kit.Dropdown(row, OpacityNames, Mathf.Clamp((State.opacity - MinOpacity) / 10, 0, OpacityNames.Length - 1), i =>
        {
            State.opacity = MinOpacity + i * 10;
            rootGroup.alpha = State.opacity / 100f;
            State.Save(Kind.Id);
        }, 90f);

        string key = WindowFeature.Instance != null ? KeyName(WindowFeature.Instance.PopoutInteractKey.Value) : "Ctrl";
        Kit.Toggle(column, "Clicks pass through to the game (hold " + key + " to use the window)", State.clickThrough, on =>
        {
            State.clickThrough = on;
            bodyGroup.blocksRaycasts = !on;
            State.Save(Kind.Id);
        });

        row = Kit.Row(column, 8f, 26f);
        Kit.Label(row, "Position and size");
        Kit.TextButton(row, "Reset", ResetLayout, 70f);

        Kind.BuildOptions?.Invoke(this, column);
    }

    private void ToggleOptions() => options.SetActive(!options.activeSelf);

    private void ToggleCollapsed()
    {
        State.collapsed = !State.collapsed;
        ApplyCollapsed();
        Clamp();
        Persist();
    }

    // As the fleet panel: the header stays, everything under it hides.
    private void ApplyCollapsed()
    {
        bool open = !State.collapsed;
        body.SetActive(open);
        if (!open) options.SetActive(false);
        resize.SetActive(open && Shown);
        collapseText.text = open ? "-" : "+";
        root.sizeDelta = new Vector2(State.w, open ? State.h : HeaderHeight);
    }

    private void ApplyLayout()
    {
        var canvas = CanvasSize();
        State.w = Mathf.Clamp(State.w, Kind.MinSize.x, Mathf.Max(Kind.MinSize.x, canvas.x));
        State.h = Mathf.Clamp(State.h, Kind.MinSize.y, Mathf.Max(Kind.MinSize.y, canvas.y));
        root.sizeDelta = new Vector2(State.w, State.collapsed ? HeaderHeight : State.h);
        // First open: the middle of the screen.
        root.anchoredPosition = State.placed ? new Vector2(State.x, State.y) : new Vector2(-State.w / 2f, State.h / 2f);
        Clamp();
    }

    private void ResetLayout()
    {
        State.placed = false;
        State.w = Kind.DefaultSize.x;
        State.h = Kind.DefaultSize.y;
        ApplyLayout();
        ApplyCollapsed();
        Persist();
    }

    private Vector2 CanvasSize() => ((RectTransform)root.parent).rect.size;

    // Keeps the whole window on the screen; a layout saved at a larger resolution or text size comes back inside it.
    internal void Clamp()
    {
        var canvas = CanvasSize();
        var size = root.sizeDelta;
        var pos = root.anchoredPosition;
        pos.x = Mathf.Clamp(pos.x, -canvas.x / 2f, Mathf.Max(-canvas.x / 2f, canvas.x / 2f - size.x));
        pos.y = Mathf.Clamp(pos.y, Mathf.Min(canvas.y / 2f, -canvas.y / 2f + size.y), canvas.y / 2f);
        root.anchoredPosition = pos;
    }

    internal void ResizeBy(Vector2 delta)
    {
        var canvas = CanvasSize();
        var pos = root.anchoredPosition;
        State.w = Mathf.Clamp(State.w + delta.x, Kind.MinSize.x, Mathf.Max(Kind.MinSize.x, canvas.x / 2f - pos.x));
        State.h = Mathf.Clamp(State.h - delta.y, Kind.MinSize.y, Mathf.Max(Kind.MinSize.y, pos.y + canvas.y / 2f));
        root.sizeDelta = new Vector2(State.w, State.h);
    }

    internal void Persist()
    {
        var pos = root.anchoredPosition;
        State.x = pos.x;
        State.y = pos.y;
        State.placed = true;
        State.Save(Kind.Id);
    }

    // Brings the window back on screen and lights its header up for a moment, so you can find it.
    public void Flash()
    {
        Clamp();
        Persist();
        flashUntil = Time.unscaledTime + FlashSeconds;
        headerBackground.color = ColorHelper.mapDockButtonActive;
        rootGroup.alpha = 1f;
    }

    private void Update()
    {
        try
        {
            if (Time.unscaledTime >= nextShowCheck) UpdateShown();
            if (flashUntil > 0f && Time.unscaledTime >= flashUntil)
            {
                flashUntil = 0f;
                headerBackground.color = ColorHelper.panelHeaderBlue;
                rootGroup.alpha = State.opacity / 100f;
            }
            // Click-through lets go while the interact key is held, so the body can be used.
            bool block = !State.clickThrough || InteractHeld();
            if (bodyGroup.blocksRaycasts != block) bodyGroup.blocksRaycasts = block;
        }
        catch (Exception e)
        {
            if (Kind.Owner != null) Kind.Owner.Fail(e, "Popout " + Kind.Id);
            else Plugin.Log.LogError($"[Window] pop-out '{Kind.Id}': {e}");
            Popouts.Close(Kind.Id);
        }
    }

    private void UpdateShown()
    {
        nextShowCheck = Time.unscaledTime + ShowCheckSeconds;
        bool want = State.show == PopoutShow.Always || Kind.Relevant == null || Kind.Relevant();
        if (want == frame.activeSelf) return;
        frame.SetActive(want);
        resize.SetActive(want && !State.collapsed);
        Popouts.Version++;
    }

    private static bool InteractHeld()
    {
        var feature = WindowFeature.Instance;
        var kb = Keyboard.current;
        if (feature == null || kb == null || UIHelper.IsTypingInInputField()) return false;
        // The Input System has no single Ctrl, Shift or Alt key; either side counts.
        switch (feature.PopoutInteractKey.Value)
        {
            case Key.LeftCtrl or Key.RightCtrl: return kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            case Key.LeftShift or Key.RightShift: return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            case Key.LeftAlt or Key.RightAlt: return kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
            case var key when key > Key.None && (int)key <= Keyboard.KeyCount: return kb[key].isPressed;
            default: return false;
        }
    }

    internal static string KeyName(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl => "Ctrl",
        Key.LeftShift or Key.RightShift => "Shift",
        Key.LeftAlt or Key.RightAlt => "Alt",
        _ => key.ToString(),
    };

    private void OnDestroy() => Popouts.Forget(this);
}

// The corner handle: drags the bottom-right corner, the top-left stays put.
internal sealed class ResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Popout owner;

    public void OnBeginDrag(PointerEventData eventData) => eventData.eligibleForClick = false;

    public void OnDrag(PointerEventData eventData) =>
        owner.ResizeBy(eventData.delta / GetComponentInParent<Canvas>().rootCanvas.scaleFactor);

    public void OnEndDrag(PointerEventData eventData) => owner.Persist();

    public void OnPointerEnter(PointerEventData eventData) => GetComponent<Image>().color = ColorHelper.resizeHandleHover;

    public void OnPointerExit(PointerEventData eventData) => GetComponent<Image>().color = ColorHelper.resizeHandleIdle;
}

// A strip along the bottom of the mod window, shown while a pop-out is open: one chip per pop-out (greyed while its
// show condition hides it). A chip brings its window back on screen and flashes it; its x closes it.
internal sealed class PopoutStrip : MonoBehaviour
{
    private int version = -1;
    private RectTransform row;

    internal static void Add(RectTransform window)
    {
        var row = Kit.Row(window, 6f, 24f);
        row.name = "PopoutStrip";
        row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 6, 0, 0);
        UiCards.AddBackground(row.gameObject, ColorHelper.panelHeaderBlue);
        // On the window, not the row: the row hides while nothing is popped out.
        window.gameObject.AddComponent<PopoutStrip>().row = row;
        row.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (version == Popouts.Version) return;
        version = Popouts.Version;
        for (int i = row.childCount - 1; i >= 0; i--)
        {
            var child = row.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        bool any = false;
        Kit.Muted(row, "Pop-out windows");
        foreach (var kind in Popouts.Kinds)
        {
            var p = Popouts.Get(kind.Id);
            if (p == null) continue;
            any = true;
            string id = kind.Id;
            var chip = Kit.TextButton(row, kind.Title, () => Popouts.Get(id)?.Flash(), 130f);
            var text = chip.GetComponentInChildren<TMP_Text>();
            if (!p.Shown) text.color = ColorHelper.poiTooltipMuted;
            Kit.Tooltip(text, kind.Title, p.Shown ? "Click to bring this window back on screen and flash it." : "Hidden until " + (kind.RelevantLabel ?? "it's relevant") + " (its Show option).");
            Kit.TextTile(row, "x", 22f, () => Popouts.Close(id));
        }
        row.gameObject.SetActive(any);
    }
}
