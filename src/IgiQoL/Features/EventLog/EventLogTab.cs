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

    // Rebuilt once a second while something new was logged.
    internal sealed class LogList : MonoBehaviour
    {
        private int version = -1;
        private float next;
        internal int RowCount { get; private set; }

        private void Update()
        {
            if (Time.unscaledTime < next || version == EventLog.Version) return;
            next = Time.unscaledTime + 1f;
            version = EventLog.Version;
            Rebuild();
        }

        private void Rebuild()
        {
            var column = (RectTransform)transform;
            for (int i = column.childCount - 1; i >= 0; i--)
            {
                var child = column.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            RowCount = 0;
            if (filter < 0 || filter == (int)LogKind.Loot) LootSummary(column);

            var entries = EventLog.Entries;
            int shown = 0, matching = 0;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (filter >= 0 && (int)e.kind != filter) continue;
                matching++;
                if (shown >= MaxRows) continue;
                var r = Kit.Row(column, 6f, 20f);
                Kit.Fixed(r, e.when.ToString("HH:mm:ss"), 70f, TextAlignmentOptions.MidlineLeft, ColorHelper.poiTooltipMuted);
                Kit.Fixed(r, KindName(e.kind), 56f, TextAlignmentOptions.MidlineLeft, ColorHelper.poiTooltipMuted);
                var text = Kit.Label(r, e.repeats > 1 ? e.text + " (x" + e.repeats + ")" : e.text);
                text.color = e.color;
                text.overflowMode = TextOverflowModes.Ellipsis;
                if (e.item != null) Kit.ItemTooltip(text, e.item);
                else Kit.Tooltip(text, null, text.text);
                shown++;
                RowCount++;
            }
            if (matching == 0) Kit.Muted(column, "Nothing logged yet.");
            else if (matching > shown) Kit.Muted(column, $"{matching - shown} older lines not shown.");
        }

        private static void LootSummary(RectTransform column)
        {
            if (EventLog.Loot.Count == 0) return;
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
            var label = Kit.Label(column, $"Picked up since the game started (or Clear): {pieces} items of {EventLog.Loot.Count} {(EventLog.Loot.Count == 1 ? "kind" : "kinds")}, worth {GameMath.FormatNumber(value)} cr to sell. Hover for the list.");
            Kit.Tooltip(label, "Loot picked up", sb.ToString().TrimEnd());
        }
    }
}
