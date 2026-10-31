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
    // -1 = everything, else a LogKind.
    internal static int filter = -1;
    private const int MaxRows = 150;

    public static void Build(RectTransform content)
    {
        Kit.Muted(content, "What the game showed for a few seconds, newest first: notifications, the event lines above the HUD, ECHO's activity (only when it changes), arrivals of the ship you fly, finished operations, and items your ships picked up (pickups of the same item within a minute share a line). Times are your computer's clock. The log is kept until you quit the game.");
        var row = Kit.Row(content);
        Kit.Dropdown(row, FilterNames, filter + 1, i => { filter = i - 1; ModWindow.Show("log"); }, 200f);
        Kit.TextButton(row, "Clear", () => { EventLog.Clear(); ModWindow.Show("log"); }, 70f);
        Kit.Column(content, 2f).gameObject.AddComponent<LogList>();
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
            bool loot = (filter < 0 || filter == (int)LogKind.Loot) && EventLog.Loot.Count > 0;
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
                if (filter >= 0 && (int)e.kind != filter) continue;
                matching++;
                if (shown >= MaxRows) continue;
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
