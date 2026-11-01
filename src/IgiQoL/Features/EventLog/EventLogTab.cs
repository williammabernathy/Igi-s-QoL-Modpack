using System.Collections.Generic;
using System.Text;
using Behaviour.Item;
using IgiQoL.UI;
using Source.Util;
using TMPro;
using UnityEngine;

namespace IgiQoL.Features.EventLog;

public static class EventLogTab
{
    private static readonly string[] FilterNames = { "Everything", "Notifications", "Event lines", "ECHO", "Arrivals", "Loot", "Operations" };
    private static readonly string[] LineNames = { "10", "25", "50", "100", "150" };
    private static readonly int[] LineCounts = { 10, 25, 50, 100, 150 };
    // -1 = everything, else a LogKind.
    internal static int filter = -1;
    private const int MaxRows = 150;

    public const string PopoutId = "event-log";
    internal const string FilterOption = "filter", LinesOption = "lines";

    public static void Build(RectTransform content)
    {
        Kit.Muted(content, "What the game showed for a few seconds, newest first: notifications, the event lines above the HUD, ECHO's activity (only when it changes), arrivals of the ship you fly, finished operations, and items your ships picked up (pickups of the same item within a minute share a line). Times are your computer's clock. The log is kept until you quit the game.");
        var row = Kit.Row(content);
        Kit.Dropdown(row, FilterNames, filter + 1, i => { filter = i - 1; ModWindow.Show("log"); }, 200f);
        Kit.TextButton(row, "Clear", () => { EventLog.Clear(); ModWindow.Show("log"); }, 70f);
        PopoutTabButton.Add(row, "Pop out", "Dock back", () => Popouts.IsOpen(PopoutId), () => Popouts.Open(PopoutId), () => Popouts.Close(PopoutId), 90f,
            "The newest lines in a window of their own, with their own filter. Clear stays here.");
        Kit.Column(content, 2f).gameObject.AddComponent<LogList>().filterKind = filter;
    }

    internal static void RegisterPopout(EventLog owner)
    {
        Popouts.Register(new PopoutKind
        {
            Id = PopoutId,
            Title = "Log",
            Owner = owner,
            DockTab = "log",
            DefaultSize = new Vector2(460f, 300f),
            MinSize = new Vector2(320f, 120f),
            RelevantLabel = "ECHO is running",
            Relevant = () => Source.Player.GamePlayer.current != null && Source.Player.GamePlayer.current.autoPlay,
            Build = BuildPopout,
            BuildOptions = BuildPopoutOptions,
        });
    }

    internal static int PopoutFilter(PopoutState s)
    {
        var v = s.options[FilterOption];
        return v.IsNumber ? Mathf.Clamp((int)v.AsNumber, -1, FilterNames.Length - 2) : -1;
    }

    internal static int PopoutLines(PopoutState s)
    {
        var v = s.options[LinesOption];
        return v.IsNumber ? Mathf.Clamp((int)v.AsNumber, 1, MaxRows) : 25;
    }

    // The newest lines with a filter of their own; no help text, and Clear stays on the tab.
    private static void BuildPopout(Popout p, RectTransform body)
    {
        var row = Kit.Row(body);
        var content = Kit.Scroll(body);
        var list = Kit.Column(content, 2f).gameObject.AddComponent<LogList>();
        list.filterKind = PopoutFilter(p.State);
        list.maxRows = PopoutLines(p.State);
        Kit.Dropdown(row, FilterNames, list.filterKind + 1, i =>
        {
            p.State.options[FilterOption] = i - 1;
            p.State.Save(p.Id);
            list.filterKind = i - 1;
            list.Invalidate();
        }, 200f);
    }

    private static void BuildPopoutOptions(Popout p, RectTransform column)
    {
        var row = Kit.Row(column, 8f, 26f);
        Kit.Label(row, "Lines shown");
        int lines = PopoutLines(p.State), index = System.Array.IndexOf(LineCounts, lines);
        Kit.Dropdown(row, LineNames, index < 0 ? 1 : index, i =>
        {
            p.State.options[LinesOption] = LineCounts[i];
            p.State.Save(p.Id);
            Popouts.Rebuild(p.Id);
        }, 90f);
    }

    internal static string KindName(LogKind k) => k switch
    {
        LogKind.Notice => "Notice",
        LogKind.Event => "Event",
        LogKind.Echo => "ECHO",
        LogKind.Travel => "Arrival",
        LogKind.Operation => "Op",
        _ => "Loot",
    };

    // Refreshed once a second while something new was logged; each line keeps its row and only changed texts are set.
    internal sealed class LogList : MonoBehaviour
    {
        private sealed class Line
        {
            public RectTransform row;
            public TMP_Text time, text;
            public System.DateTime when;
            public string baseText;
            public int repeats;
            public int pass;
        }

        private int version = -1;
        private float next;
        private int pass;
        private TMP_Text summary, footer;
        private readonly Dictionary<LogEntry, Line> lines = new();
        private readonly List<LogEntry> stale = new();
        internal int RowCount { get; private set; }
        // -1 = everything, else a LogKind; and how many lines to show.
        internal int filterKind = -1;
        internal int maxRows = MaxRows;

        // Redraws at the next frame (after the filter changed).
        internal void Invalidate()
        {
            version = -1;
            next = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime < next || version == EventLog.Version) return;
            next = Time.unscaledTime + 1f;
            version = EventLog.Version;
            Refresh();
        }

        private void Refresh()
        {
            var column = (RectTransform)transform;
            pass++;
            int index = 0;
            bool loot = (filterKind < 0 || filterKind == (int)LogKind.Loot) && EventLog.Loot.Count > 0;
            if (loot)
            {
                if (summary == null) summary = Kit.Label(column, "");
                LootSummary(summary);
                Place(summary.transform, index++);
            }
            if (summary != null) summary.gameObject.SetActive(loot);

            var entries = EventLog.Entries;
            int shown = 0, matching = 0;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (filterKind >= 0 && (int)e.kind != filterKind) continue;
                matching++;
                if (shown >= maxRows) continue;
                if (!lines.TryGetValue(e, out var line))
                {
                    line = NewLine(column, e);
                    lines[e] = line;
                }
                line.pass = pass;
                // A pickup merged into this line moves its time.
                if (line.when != e.when)
                {
                    line.when = e.when;
                    line.time.text = e.when.ToString("HH:mm:ss");
                }
                if (line.repeats != e.repeats || line.baseText != e.text)
                {
                    line.repeats = e.repeats;
                    line.baseText = e.text;
                    string text = e.repeats > 1 ? e.text + " (x" + e.repeats + ")" : e.text;
                    line.text.text = text;
                    if (e.item == null) Kit.Tooltip(line.text, null, text);
                }
                Place(line.row, index++);
                shown++;
            }
            RowCount = shown;

            stale.Clear();
            foreach (var kv in lines)
                if (kv.Value.pass != pass) stale.Add(kv.Key);
            foreach (var e in stale)
            {
                var gone = lines[e].row.gameObject;
                gone.SetActive(false);
                Destroy(gone);
                lines.Remove(e);
            }

            bool note = matching == 0 || matching > shown;
            if (note && footer == null) footer = Kit.Muted(column, "");
            if (footer != null)
            {
                footer.gameObject.SetActive(note);
                if (note)
                {
                    string text = matching == 0 ? "Nothing logged yet." : $"{matching - shown} older lines not shown.";
                    if (footer.text != text) footer.text = text;
                    Place(footer.transform, index);
                }
            }
        }

        private static Line NewLine(RectTransform column, LogEntry e)
        {
            var r = Kit.Row(column, 6f, 20f);
            var time = Kit.Fixed(r, "", 70f, TextAlignmentOptions.MidlineLeft, ColorHelper.poiTooltipMuted);
            Kit.Fixed(r, KindName(e.kind), 56f, TextAlignmentOptions.MidlineLeft, ColorHelper.poiTooltipMuted);
            var text = Kit.Label(r, "");
            text.color = e.color;
            text.overflowMode = TextOverflowModes.Ellipsis;
            if (e.item != null) Kit.ItemTooltip(text, e.item);
            return new Line { row = r, time = time, text = text, repeats = -1 };
        }

        private static void Place(Transform t, int index)
        {
            if (t.GetSiblingIndex() != index) t.SetSiblingIndex(index);
        }

        private static void LootSummary(TMP_Text label)
        {
            long value = 0;
            int pieces = 0;
            var top = new List<KeyValuePair<InventoryItemType, int>>(EventLog.Loot);
            foreach (var kv in top)
            {
                pieces += kv.Value;
                value += (long)kv.Key.sellValue * kv.Value;
            }
            top.Sort((a, b) => ((long)b.Key.sellValue * b.Value).CompareTo((long)a.Key.sellValue * a.Value));
            var sb = new StringBuilder();
            foreach (var kv in top) sb.Append(Translation.Translate(kv.Key.displayName)).Append(" x").Append(kv.Value).Append(": ").Append(GameMath.FormatNumber((long)kv.Key.sellValue * kv.Value)).Append(" cr\n");
            label.text = $"Picked up since the game started (or Clear): {pieces} items of {EventLog.Loot.Count} {(EventLog.Loot.Count == 1 ? "kind" : "kinds")}, worth {GameMath.FormatNumber(value)} cr to sell. Hover for the list.";
            Kit.Tooltip(label, "Loot picked up", sb.ToString().TrimEnd());
        }
    }
}
