using System.Collections;
using System.Text;
using IgiQoL.Core;
using IgiQoL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace IgiQoL.Features.Window;

// The mod window resizes from its corner with its top-left put, never gets narrower than the open tab needs, moves
// tabs that don't fit into More, and a layout saved bigger than the screen comes back inside it.
internal static class WindowResizeCheck
{
    private static readonly string[] Prefs = { "IgiQoL.Window.X", "IgiQoL.Window.Y", "IgiQoL.Window.W", "IgiQoL.Window.H" };
    private const string TabPref = "IgiQoL.Window.Tab";

    internal static IEnumerator Run(SelfCheckContext ctx)
    {
        var had = new bool[Prefs.Length];
        var values = new float[Prefs.Length];
        for (int i = 0; i < Prefs.Length; i++)
        {
            had[i] = PlayerPrefs.HasKey(Prefs[i]);
            values[i] = PlayerPrefs.GetFloat(Prefs[i]);
        }
        string tab = PlayerPrefs.GetString(TabPref, "");
        void Restore()
        {
            for (int i = 0; i < Prefs.Length; i++)
                if (had[i]) PlayerPrefs.SetFloat(Prefs[i], values[i]);
                else PlayerPrefs.DeleteKey(Prefs[i]);
            PlayerPrefs.SetString(TabPref, tab);
        }
        ctx.Cleanup(Restore);

        ModWindow.Close();
        yield return null;
        ModWindow.Open("settings");
        yield return null;
        yield return null;
        var root = ModWindow.Root;
        var resize = root != null ? root.Find("Resize") : null;
        var handle = resize != null ? resize.GetComponent<ResizeHandle>() : null;
        if (handle == null)
        {
            ctx.Fail("resize", "no window or no resize handle");
            Restore();
            yield break;
        }
        var canvas = ((RectTransform)root.parent).rect.size;
        float scale = root.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;

        // Top-left 20 px in from the screen's top-left corner, then the corner dragged 100 right and 60 down.
        var size0 = root.sizeDelta;
        root.anchoredPosition = new Vector2(-canvas.x / 2f + size0.x / 2f + 20f, canvas.y / 2f - size0.y / 2f - 20f);
        Vector2 TopLeft() => root.anchoredPosition + new Vector2(-root.sizeDelta.x / 2f, root.sizeDelta.y / 2f);
        var corner0 = TopLeft();
        Drag(handle, new Vector2(100f, -60f) * scale);
        var grown = root.sizeDelta - size0;
        string d = $"grew by ({grown.x:0},{grown.y:0}) top-left moved by {Vector2.Distance(TopLeft(), corner0):0.#} saved w={PlayerPrefs.GetFloat(Prefs[2]):0}";
        if (Vector2.Distance(grown, new Vector2(100f, 60f)) < 0.5f && Vector2.Distance(TopLeft(), corner0) < 0.5f && Mathf.Approximately(PlayerPrefs.GetFloat(Prefs[2]), root.sizeDelta.x)) ctx.Pass("resize", d);
        else ctx.Fail("resize", d);

        // As narrow as it goes: Settings is the last tab, so it sits in More and More names it.
        Drag(handle, new Vector2(-5000f, 0f) * scale);
        yield return null;
        string settingsTitle = ModWindow.Tabs[ModWindow.Tabs.Count - 1].Title;
        d = $"width={root.sizeDelta.x:0} min={ModWindow.MinWidth:0} header tabs={ModWindow.HeaderTabCount}/{ModWindow.Tabs.Count} more='{ModWindow.MoreCaption}'";
        bool narrow = Mathf.Approximately(root.sizeDelta.x, ModWindow.MinWidth) && ModWindow.HeaderTabCount < ModWindow.Tabs.Count && ModWindow.MoreCaption == settingsTitle;
        ModWindow.Show(ModWindow.Tabs[0].Id);
        d += $"; on {ModWindow.Tabs[0].Id} more='{ModWindow.MoreCaption}'";
        if (narrow && ModWindow.MoreCaption == "More") ctx.Pass("overflow", d);
        else ctx.Fail("overflow", d);

        // Every tab at its narrowest, for the screenshots: the window widens for a tab whose rows need more.
        var widths = new StringBuilder();
        bool fits = true;
        foreach (var t in ModWindow.Tabs)
        {
            ModWindow.Show(t.Id);
            yield return null;
            yield return null;
            Drag(handle, new Vector2(-5000f, 0f) * scale);
            yield return null;
            widths.Append(t.Id).Append('=').Append(root.sizeDelta.x.ToString("0")).Append(' ');
            fits &= root.sizeDelta.x <= canvas.x + 0.5f && root.sizeDelta.x >= ModWindow.MinWidth - 0.5f;
            yield return ctx.Screenshot("window-min-" + t.Id);
        }
        if (fits) ctx.Pass("tab-widths", widths.ToString().TrimEnd());
        else ctx.Fail("tab-widths", widths + $"canvas={canvas.x:0}");

        // At the default width no label on the Settings tab is cut off: each one's text fits the room its row gives it.
        ModWindow.Show("settings");
        yield return null;
        yield return null;
        Drag(handle, new Vector2(760f - root.sizeDelta.x, 0f) * scale);
        yield return null;
        yield return null;
        int labels = 0, cut = 0;
        string firstCut = null;
        var content = ModWindow.Content;
        for (int i = 0; i < content.childCount; i++)
        {
            var row = content.GetChild(i);
            if (row.name != "Row" || row.childCount == 0) continue;
            var label = row.GetChild(0).GetComponent<TMP_Text>();
            if (label == null) continue;
            labels++;
            float need = label.GetPreferredValues(label.text).x, room = label.rectTransform.rect.width;
            if (need > room + 0.5f)
            {
                cut++;
                firstCut ??= $"'{label.text}' needs {need:0} has {room:0}";
            }
        }
        d = $"window {root.sizeDelta.x:0} px, {labels} labels, {cut} cut off{(firstCut == null ? "" : ", first " + firstCut)}";
        if (labels > 0 && cut == 0) ctx.Pass("setting-labels", d);
        else ctx.Fail("setting-labels", d);

        // The pop-out strip keeps its place and height, and every chip is one width, so opening a pop-out moves nothing.
        var strip = root.Find("PopoutStrip") as RectTransform;
        if (strip == null) ctx.Fail("popout-strip", "no PopoutStrip in the window");
        else
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            var bodyRect = (RectTransform)root.Find("Body");
            float bodyBefore = bodyRect.rect.height, stripBefore = strip.rect.height;
            bool activeBefore = strip.gameObject.activeSelf;
            PopoutKind kind = null;
            foreach (var k in Popouts.Kinds)
                if (!k.Multiple && !Popouts.IsOpen(k.Id)) { kind = k; break; }
            string chipDetail = "no pop-out kind to open";
            bool chipsEqual = true;
            float bodyAfter = bodyBefore, stripAfter = stripBefore;
            if (kind != null)
            {
                Popouts.Open(kind.Id);
                yield return null;
                yield return null;
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
                bodyAfter = bodyRect.rect.height;
                stripAfter = strip.rect.height;
                int chips = 0;
                foreach (var le in strip.GetComponentsInChildren<LayoutElement>(true))
                    if (le.GetComponent<Button>() != null && le.preferredWidth > 30f)
                    {
                        chips++;
                        chipsEqual &= Mathf.Approximately(le.preferredWidth, PopoutStrip.ChipWidth);
                    }
                chipDetail = $"{chips} chips at {PopoutStrip.ChipWidth:0} px";
                Popouts.Close(kind.Id);
                yield return null;
            }
            d = $"strip active {activeBefore} height {stripBefore:0.#} -> {stripAfter:0.#}, tab content height {bodyBefore:0.#} -> {bodyAfter:0.#}, {chipDetail}";
            if (activeBefore && Mathf.Approximately(stripBefore, stripAfter) && Mathf.Approximately(bodyBefore, bodyAfter) && chipsEqual) ctx.Pass("popout-strip", d);
            else ctx.Fail("popout-strip", d);
        }

        // A layout saved far off and bigger than the screen opens inside it.
        ModWindow.Close();
        yield return null;
        PlayerPrefs.SetFloat(Prefs[0], 99999f);
        PlayerPrefs.SetFloat(Prefs[1], -99999f);
        PlayerPrefs.SetFloat(Prefs[2], 99999f);
        PlayerPrefs.SetFloat(Prefs[3], 99999f);
        ModWindow.Open("settings");
        yield return null;
        root = ModWindow.Root;
        var pos = root.anchoredPosition;
        var size = root.sizeDelta;
        bool inside = pos.x - size.x / 2f >= -canvas.x / 2f - 0.5f && pos.x + size.x / 2f <= canvas.x / 2f + 0.5f
                      && pos.y - size.y / 2f >= -canvas.y / 2f - 0.5f && pos.y + size.y / 2f <= canvas.y / 2f + 0.5f;
        d = $"pos=({pos.x:0},{pos.y:0}) size=({size.x:0},{size.y:0}) canvas=({canvas.x:0},{canvas.y:0})";
        if (inside) ctx.Pass("clamp-on-load", d);
        else ctx.Fail("clamp-on-load", d);

        ModWindow.Close();
        Restore();
        yield return null;
        ModWindow.Open("settings");
    }

    private static void Drag(ResizeHandle handle, Vector2 delta)
    {
        var ev = new PointerEventData(EventSystem.current) { delta = delta };
        handle.OnBeginDrag(ev);
        handle.OnDrag(ev);
        handle.OnEndDrag(ev);
    }
}
