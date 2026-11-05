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
    // Several windows of this kind at once, each with its own id ("<Id>-<n>") and its own state.
    public bool Multiple;
    // The mod window tab this view comes from: its header gets a Dock button, and its strip chip's x docks it back.
    public string DockTab;
    // A window's own title (for example a meter's view); null means Title.
    public Func<PopoutState, string> TitleOf;
    // Canvas order of the window. 2 keeps a pop-out under the mod window (3); a view meant for the galaxy map needs 4,
    // because the map's window draws at 3 and would cover it.
    public int SortingOrder = 2;

    internal string TitleFor(PopoutState s) => TitleOf != null ? TitleOf(s) : Title;
}

// Layout and options of one pop-out window. Layout belongs to the machine, not the save: one PlayerPrefs JSON string
// per window, beside the mod window's own position.
public sealed class PopoutState
{
    public bool open, placed, collapsed, clickThrough = true;
    public float x, y, w, h;
    public int opacity = 100;
    public PopoutShow show;
    // The kind's own options.
    public JsonObject options = new();

    internal static string Pref(string id) => "IgiQoL.Popout." + id;

    internal static PopoutState Load(PopoutKind kind, string id)
    {
        var s = new PopoutState { show = kind.DefaultShow, w = kind.DefaultSize.x, h = kind.DefaultSize.y };
        string text = PlayerPrefs.GetString(Pref(id), "");
        if (text.Length == 0) return s;
        JsonObject o;
        try { o = JsonValue.Parse(text).AsJsonObject; }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Window] pop-out '{id}' layout unreadable, using defaults: {e.Message}");
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
// back on HudReady. A window's id is its kind's id, or "<kind>-<n>" for a kind with several windows; those kinds keep
// the list of their windows (open or closed) in PlayerPrefs as a JSON array of ids.
public static class Popouts
{
    private static readonly List<PopoutKind> kinds = new();
    private static readonly Dictionary<string, PopoutState> states = new();
    private static readonly Dictionary<string, Popout> open = new();
    private static readonly Dictionary<string, List<string>> lists = new();
    private static readonly List<string> scratch = new();

    // Goes up when a pop-out opens, closes, hides, shows or is rebuilt, so the mod window's strip and the tabs'
    // pop-out buttons know to redraw.
    public static int Version { get; internal set; }

    public static IReadOnlyList<PopoutKind> Kinds => kinds;

    public static void Register(PopoutKind kind)
    {
        if (kinds.Count == 0)
        {
            GameEvents.HudReady += RestoreOpen;
            InputSystem.onAfterUpdate += ClaimWheel;
        }
        kinds.Add(kind);
    }

    // Click-through passes clicks to the game but not the wheel. The galaxy map and the event system read the wheel
    // in Update and LateUpdate in no fixed order with the pop-outs' own Update, so the body starts blocking here,
    // right after the input update and before any of them. Popout.Update keeps it that way for the rest of the frame.
    private static void ClaimWheel()
    {
        if (open.Count == 0 || !Popout.WheelMoved()) return;
        try
        {
            Vector2 point = Input.mousePosition;
            foreach (var p in open.Values)
                if (p != null && p.ClaimsWheelAt(point)) p.BlockBody();
        }
        catch (Exception e)
        {
            InputSystem.onAfterUpdate -= ClaimWheel;
            Plugin.Log.LogError($"[Window] pop-out wheel handling stopped: {e}");
        }
    }

    public static PopoutState State(string id)
    {
        if (states.TryGetValue(id, out var s)) return s;
        var kind = KindOf(id);
        if (kind == null) return null;
        s = PopoutState.Load(kind, id);
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
        var kind = KindOf(id);
        if (kind == null || IsOpen(id) || HudManager.Instance == null || kind.Owner != null && !kind.Owner.Active) return;
        var s = State(id);
        s.open = true;
        s.Save(id);
        try { open[id] = Popout.Create(kind, id, s); }
        catch (Exception e)
        {
            s.open = false;
            s.Save(id);
            if (kind.Owner != null) kind.Owner.Fail(e, "Popout " + id);
            else Plugin.Log.LogError($"[Window] pop-out '{id}' failed to build: {e}");
            return;
        }
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

    // Closes the window and opens the mod window on the tab it came from.
    public static void DockBack(string id)
    {
        var kind = KindOf(id);
        Close(id);
        if (kind?.DockTab != null) ModWindow.Open(kind.DockTab);
    }

    // The windows of a kind, open or closed, oldest first; a kind with one window has just its own id.
    public static IReadOnlyList<string> Windows(string kindId)
    {
        var kind = Find(kindId);
        return kind != null ? List(kind) : Array.Empty<string>();
    }

    public static bool AnyOpen(string kindId)
    {
        foreach (var id in Windows(kindId))
            if (IsOpen(id)) return true;
        return false;
    }

    public static void CloseAll(string kindId)
    {
        scratch.Clear();
        scratch.AddRange(Windows(kindId));
        foreach (var id in scratch)
            if (IsOpen(id)) Close(id);
    }

    // A new window of a kind with several windows; init fills its options before it's built. Returns its id.
    public static string OpenNew(string kindId, Action<PopoutState> init = null)
    {
        var kind = Find(kindId);
        if (kind == null || !kind.Multiple || HudManager.Instance == null || kind.Owner != null && !kind.Owner.Active) return null;
        var list = List(kind);
        int n = 1;
        while (list.Contains(kind.Id + "-" + n)) n++;
        string id = kind.Id + "-" + n;
        // A removed window's layout is gone already, but a killed game may have left one behind.
        PlayerPrefs.DeleteKey(PopoutState.Pref(id));
        states.Remove(id);
        var s = State(id);
        init?.Invoke(s);
        list.Add(id);
        SaveList(kind);
        Open(id);
        return id;
    }

    // Closes a window of a kind with several windows and forgets it, layout and options included.
    public static void Remove(string id)
    {
        var kind = KindOf(id);
        if (kind == null || !kind.Multiple) return;
        Close(id);
        PlayerPrefs.DeleteKey(PopoutState.Pref(id));
        states.Remove(id);
        if (List(kind).Remove(id)) SaveList(kind);
        Version++;
    }

    // Builds an open pop-out again, keeping its state (after an option or the text size changed).
    public static void Rebuild(string id)
    {
        var p = Get(id);
        if (p == null) return;
        Object.Destroy(p.gameObject);
        open[id] = Popout.Create(p.Kind, id, p.State);
        Version++;
    }

    public static void RebuildAll()
    {
        scratch.Clear();
        scratch.AddRange(open.Keys);
        foreach (var id in scratch) Rebuild(id);
    }

    internal static void Forget(Popout p)
    {
        if (open.TryGetValue(p.Id, out var current) && current == p)
        {
            open.Remove(p.Id);
            Version++;
        }
    }

    // Drops the cached state and window list, so the next read goes to PlayerPrefs again.
    internal static void ForgetState(string id)
    {
        states.Remove(id);
        lists.Remove(id);
    }

    internal static PopoutKind Find(string kindId)
    {
        foreach (var k in kinds)
            if (k.Id == kindId) return k;
        return null;
    }

    // The kind a window id belongs to: the kind's own id, or "<kind>-<n>" for a kind with several windows.
    internal static PopoutKind KindOf(string id)
    {
        foreach (var k in kinds)
        {
            if (!k.Multiple)
            {
                if (k.Id == id) return k;
                continue;
            }
            int n = k.Id.Length;
            if (id.Length > n + 1 && id[n] == '-' && id.StartsWith(k.Id, StringComparison.Ordinal) && int.TryParse(id.Substring(n + 1), out _)) return k;
        }
        return null;
    }

    private static string ListPref(string kindId) => PopoutState.Pref(kindId) + ".list";

    private static List<string> List(PopoutKind kind)
    {
        if (lists.TryGetValue(kind.Id, out var list)) return list;
        list = new List<string>();
        if (!kind.Multiple) list.Add(kind.Id);
        else
        {
            string text = PlayerPrefs.GetString(ListPref(kind.Id), "");
            try
            {
                if (text.Length > 0)
                    foreach (var v in JsonValue.Parse(text).AsJsonArray)
                        if (v.IsString && KindOf(v.AsString) == kind && !list.Contains(v.AsString)) list.Add(v.AsString);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[Window] pop-out list '{kind.Id}' unreadable, starting empty: {e.Message}"); }
        }
        lists[kind.Id] = list;
        return list;
    }

    private static void SaveList(PopoutKind kind)
    {
        var array = new JsonArray();
        foreach (var id in List(kind)) array.Add(id);
        PlayerPrefs.SetString(ListPref(kind.Id), array.ToString());
    }

    private static void RestoreOpen()
    {
        foreach (var kind in kinds)
        {
            if (kind.Owner != null && !kind.Owner.Active) continue;
            foreach (var id in List(kind))
            {
                var s = State(id);
                if (!s.open || IsOpen(id)) continue;
                try
                {
                    open[id] = Popout.Create(kind, id, s);
                    Version++;
                }
                catch (Exception e)
                {
                    if (kind.Owner != null) kind.Owner.Fail(e, "Popout " + id);
                    else Plugin.Log.LogError($"[Window] pop-out '{id}' failed to build: {e}");
                }
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
    // The window's id: its kind's id, or "<kind>-<n>" for a kind with several windows.
    public string Id { get; private set; }
    public PopoutState State { get; private set; }
    // Under the show condition the frame hides but the pop-out stays open and keeps its place.
    public bool Shown => frame != null && frame.activeSelf;
    public bool ClicksPass => bodyGroup != null && !bodyGroup.blocksRaycasts;
    public RectTransform Body { get; private set; }

    // True when a wheel turn at this screen point belongs to this window: click-through is on (otherwise the body
    // blocks by itself) and the point is on the shown body.
    internal bool ClaimsWheelAt(Vector2 screenPoint)
    {
        if (!State.clickThrough || body == null || !body.activeInHierarchy) return false;
        var canvas = root.GetComponent<Canvas>().rootCanvas;
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(Body, screenPoint, cam);
    }

    internal void BlockBody()
    {
        if (!bodyGroup.blocksRaycasts) bodyGroup.blocksRaycasts = true;
    }

    // The game reads the wheel both ways: the map's zoom through the Input System, its sector and system steps
    // through the old Input class.
    internal static bool WheelMoved() =>
        Mouse.current != null && Mouse.current.scroll.ReadValue().y != 0f || Input.mouseScrollDelta.y != 0f;

    private RectTransform root;
    private GameObject frame, options, resize, body;
    private CanvasGroup rootGroup, bodyGroup;
    private Image headerBackground;
    private TMP_Text collapseText;
    private float nextShowCheck, flashUntil;

    internal static Popout Create(PopoutKind kind, string id, PopoutState state)
    {
        var root = UiCards.NewChild("IgiQoL_Popout_" + id, HudManager.Instance.rootCanvasTransform);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0f, 1f);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = kind.SortingOrder;
        root.gameObject.AddComponent<GraphicRaycaster>();
        var p = root.gameObject.AddComponent<Popout>();
        p.Kind = kind;
        p.Id = id;
        p.State = state;
        p.root = root;
        p.rootGroup = root.gameObject.AddComponent<CanvasGroup>();
        try { p.Build(); }
        catch
        {
            Object.Destroy(root.gameObject);
            throw;
        }
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
        FleetBarParts.AddLine(header, Kind.TitleFor(State), 12f, ColorHelper.stationCardTitle, TextAlignmentOptions.MidlineLeft);
        CaptainCards.AddSpacer(header);
        Kit.Tooltip(Kit.TextButton(header, "Options", ToggleOptions, 64f).GetComponentInChildren<TMP_Text>(), null,
            "Choose when this window shows and how see-through it is, whether clicks pass through it, and reset its position and size.");
        var collapse = Kit.TextTile(header, State.collapsed ? "+" : "-", 22f, ToggleCollapsed);
        collapseText = collapse.GetComponentInChildren<TMP_Text>();
        if (Kind.DockTab != null)
            Kit.Tooltip(Kit.TextButton(header, "Dock", () => Popouts.DockBack(Id), 50f).GetComponentInChildren<TMP_Text>(), null,
                "Dock back: close this window and open its tab in the QoL window.");
        Kit.Tile(header, PoiIconKeys.Fleet("Close"), ColorHelper.reddish, 22f, () => Popouts.Close(Id), "Close");
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

        var handle = ResizeHandle.Add(root);
        handle.onResize = ResizeBy;
        handle.onEnd = Persist;
        resize = handle.gameObject;

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
        Kit.Dropdown(row, new[] { "Always", "Only when relevant" }, (int)State.show, i =>
        {
            State.show = (PopoutShow)i;
            State.Save(Id);
            nextShowCheck = 0f;
        }, 200f);
        // What "relevant" means for this window, kept out of the dropdown so its text stays at full size.
        var relevantNote = Kit.Muted(column, "Only when relevant shows this window when " + (Kind.RelevantLabel ?? "it has something to show") + ".");
        relevantNote.name = "RelevantNote";

        row = Kit.Row(column, 8f, 26f);
        Kit.Label(row, "Opacity");
        Kit.Dropdown(row, OpacityNames, Mathf.Clamp((State.opacity - MinOpacity) / 10, 0, OpacityNames.Length - 1), i =>
        {
            State.opacity = MinOpacity + i * 10;
            rootGroup.alpha = State.opacity / 100f;
            State.Save(Id);
        }, 90f);

        string key = WindowFeature.Instance != null ? KeyName(WindowFeature.Instance.PopoutInteractKey.Value) : "Ctrl";
        var clickThrough = Kit.Toggle(column, "Click-through (hold " + key + " to use the window)", State.clickThrough, on =>
        {
            State.clickThrough = on;
            bodyGroup.blocksRaycasts = !on;
            State.Save(Id);
        });
        FleetBarParts.SetTooltip(clickThrough, null, "On: clicks on the window's body reach the game under it, and the mouse wheel still scrolls the window. Hold " + key + " to use the window itself while this is on.");

        row = Kit.Row(column, 8f, 26f);
        Kit.Label(row, "Position and size");
        Kit.TextButton(row, "Reset", ResetLayout, 70f);

        Kind.BuildOptions?.Invoke(this, column);
    }

    // A small window grows while its options are open, so the whole panel fits; it goes back when they close.
    private void ToggleOptions()
    {
        options.SetActive(!options.activeSelf);
        float h = State.collapsed ? HeaderHeight : State.h;
        if (options.activeSelf)
        {
            var rt = (RectTransform)options.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            h = Mathf.Max(h, HeaderHeight + LayoutUtility.GetPreferredHeight(rt) + 40f);
        }
        root.sizeDelta = new Vector2(State.w, h);
        Clamp();
    }

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
        State.Save(Id);
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
        if (Kind.Owner != null && !Kind.Owner.Active)
        {
            Popouts.Close(Id);
            return;
        }
        try
        {
            if (Time.unscaledTime >= nextShowCheck) UpdateShown();
            if (flashUntil > 0f && Time.unscaledTime >= flashUntil)
            {
                flashUntil = 0f;
                headerBackground.color = ColorHelper.panelHeaderBlue;
                rootGroup.alpha = State.opacity / 100f;
            }
            // Click-through lets go while the interact key is held, so the body can be used. The wheel over the body
            // always scrolls the window (see Popouts.ClaimWheel).
            bool block = !State.clickThrough || InteractHeld() || WheelMoved() && ClaimsWheelAt(Input.mousePosition);
            if (bodyGroup.blocksRaycasts != block) bodyGroup.blocksRaycasts = block;
        }
        catch (Exception e)
        {
            if (Kind.Owner != null) Kind.Owner.Fail(e, "Popout " + Id);
            else Plugin.Log.LogError($"[Window] pop-out '{Id}': {e}");
            Popouts.Close(Id);
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
        return feature != null && HeldKey.IsDown(feature.PopoutInteractKey.Value);
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

// A corner handle: drags the bottom-right corner of its parent. The owner applies the change, keeping the top-left put.
internal sealed class ResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    // The drag delta in canvas units (y up), and the end of the drag.
    public Action<Vector2> onResize;
    public Action onEnd;

    internal static ResizeHandle Add(RectTransform parent)
    {
        var rt = UiCards.NewChild("Resize", parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(14f, 14f);
        rt.anchoredPosition = Vector2.zero;
        // The mod window lays out its children; the handle sits on top of them in the corner.
        rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        UiCards.AddBackground(rt.gameObject, ColorHelper.windowGripRest);
        return rt.gameObject.AddComponent<ResizeHandle>();
    }

    public void OnBeginDrag(PointerEventData eventData) => eventData.eligibleForClick = false;

    public void OnDrag(PointerEventData eventData) =>
        onResize?.Invoke(eventData.delta / GetComponentInParent<Canvas>().rootCanvas.scaleFactor);

    public void OnEndDrag(PointerEventData eventData) => onEnd?.Invoke();

    public void OnPointerEnter(PointerEventData eventData) => GetComponent<Image>().color = ColorHelper.windowGripHot;

    public void OnPointerExit(PointerEventData eventData) => GetComponent<Image>().color = ColorHelper.windowGripRest;
}

// A strip along the bottom of the mod window: one chip per open pop-out (greyed while its show condition hides it).
// A chip brings its window back on screen and flashes it; its x closes it, or docks it back into its tab for a view
// that has one. The strip always keeps its height, so opening the first pop-out never moves the tab content.
internal sealed class PopoutStrip : MonoBehaviour
{
    // Every chip is this wide (it shrinks only when the window runs out of room).
    internal const float ChipWidth = 130f;
    private int version = -1;
    private RectTransform row;

    internal static void Add(RectTransform window)
    {
        var row = Kit.Row(window, 6f, 24f);
        row.name = "PopoutStrip";
        // The right padding keeps the last chip's x clear of the window's resize corner.
        row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 16, 0, 0);
        UiCards.AddBackground(row.gameObject, ColorHelper.panelHeaderBlue);
        window.gameObject.AddComponent<PopoutStrip>().row = row;
    }

    private void Update()
    {
        if (version == Popouts.Version) return;
        version = Popouts.Version;
        try { Rebuild(); }
        catch (Exception e) { IgiQoL.Features.Window.WindowFeature.Instance.Fail(e, "PopoutStrip.Update"); }
    }

    private void Rebuild()
    {
        for (int i = row.childCount - 1; i >= 0; i--)
        {
            var child = row.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        bool any = false;
        var heading = Kit.Muted(row, "Pop-out windows");
        // One line in a 24 px row: a narrow window cuts the heading short instead of wrapping it out of the strip.
        heading.textWrappingMode = TextWrappingModes.NoWrap;
        heading.overflowMode = TextOverflowModes.Ellipsis;
        foreach (var kind in Popouts.Kinds)
            foreach (var id in Popouts.Windows(kind.Id))
            {
                var p = Popouts.Get(id);
                if (p == null) continue;
                any = true;
                string chipId = id, title = kind.TitleFor(p.State);
                var chip = Kit.TextButton(row, title, () =>
                {
                    var target = Popouts.Get(chipId);
                    if (target != null) target.Flash();
                }, 130f);
                var text = chip.GetComponentInChildren<TMP_Text>();
                // The same width for every chip, so a renamed title or a changed view never moves its neighbours.
                var le = chip.GetComponent<LayoutElement>();
                le.preferredWidth = ChipWidth;
                le.minWidth = 40f;
                text.overflowMode = TextOverflowModes.Ellipsis;
                if (!p.Shown) text.color = ColorHelper.poiTooltipMuted;
                Kit.Tooltip(text, title, p.Shown ? "Click to bring this window back on screen and flash it." : "Hidden until " + (kind.RelevantLabel ?? "it's relevant") + " (its Show option).");
                bool dock = kind.DockTab != null;
                var x = Kit.TextTile(row, "x", 22f, () =>
                {
                    if (dock) Popouts.DockBack(chipId);
                    else Popouts.Close(chipId);
                });
                FleetBarParts.SetTooltip(x, null, dock ? "Dock back: close this window and open its tab." : "Close this window.");
            }
        if (!any) heading.text = "Pop-out windows: none open";
    }
}

// A tab's pop-out button: one caption while the view is docked, another while it floats. It follows changes made
// elsewhere (the window's own Dock or close button, the strip), so the caption never goes stale.
public sealed class PopoutTabButton : MonoBehaviour
{
    private Func<bool> isOut;
    private string popLabel, dockLabel;
    private TMP_Text caption;
    private int version = -1;

    public static Button Add(RectTransform row, string popLabel, string dockLabel, Func<bool> isOut, Action popOut, Action dockBack, float width, string tooltip)
    {
        var button = Kit.TextButton(row, popLabel, () =>
        {
            if (isOut()) dockBack();
            else popOut();
        }, width);
        var self = button.gameObject.AddComponent<PopoutTabButton>();
        self.isOut = isOut;
        self.popLabel = popLabel;
        self.dockLabel = dockLabel;
        self.caption = button.GetComponentInChildren<TMP_Text>();
        if (tooltip != null) Kit.Tooltip(self.caption, null, tooltip);
        self.Sync();
        return button;
    }

    private void Update()
    {
        if (version == Popouts.Version) return;
        try { Sync(); }
        catch (Exception e) { IgiQoL.Features.Window.WindowFeature.Instance.Fail(e, "PopoutTabButton.Update"); }
    }

    private void Sync()
    {
        version = Popouts.Version;
        string text = isOut() ? dockLabel : popLabel;
        if (caption.text != text) caption.text = text;
    }
}
