using System;
using System.Collections.Generic;
using System.Text;
using Behaviour.Item;
using IgiQoL.UI;
using Source.Util;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.UsedIn;

// The Stat reference tab's search: every forge recipe you can craft that uses an item, without the tooltip's cap.
internal static class UsedInSearch
{
    internal const int MaxHits = 20;

    // Kept across tab rebuilds, so the search and the open list survive switching tabs.
    private static string search = "";
    private static string expanded;
    private static RectTransform results;

    // The full list of the open item, for the self-check.
    internal static TMP_Text ListText;

    internal static void Build(RectTransform column)
    {
        Kit.Muted(column, "Every forge recipe you can craft that uses an item, with how many one craft takes. The item tooltip shows the first 8.");
        var row = Kit.Row(column, 6f, 26f);
        Kit.Search(row, "Type part of an item name", Run, 300f).SetTextWithoutNotify(search);
        results = Kit.Column(column, 2f);
        Draw(autoOpen: false);
    }

    // Typing: a search with one match opens its list.
    internal static void Run(string text)
    {
        search = text ?? "";
        Draw(autoOpen: true);
    }

    private static void Draw(bool autoOpen)
    {
        ListText = null;
        if (results == null) return;
        for (int i = results.childCount - 1; i >= 0; i--)
        {
            var child = results.GetChild(i).gameObject;
            child.SetActive(false);
            Object.Destroy(child);
        }
        string needle = search.Trim();
        if (needle.Length < 2) return;

        var index = UsedIn.Index();
        var hits = new List<(string name, InventoryItemType type)>();
        foreach (var kv in index)
        {
            if (!InventoryItemType.TryGet(kv.Key, out var type)) continue;
            string name = Name(type);
            if (name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) hits.Add((name, type));
        }
        hits.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        if (autoOpen && hits.Count == 1) expanded = hits[0].type.identifier;

        int shown = Math.Min(MaxHits, hits.Count);
        for (int i = 0; i < shown; i++)
        {
            var type = hits[i].type;
            var recipes = index[type.identifier];
            bool open = type.identifier == expanded;
            var row = Kit.Row(results, 6f, 22f);
            var label = Kit.Label(row, hits[i].name);
            label.color = type.rarity.GetColor();
            Kit.ItemTooltip(label, type);
            int lines = UsedIn.LineCount(recipes);
            Kit.Fixed(row, lines + (lines == 1 ? " recipe" : " recipes"), 90f, TextAlignmentOptions.MidlineRight, ColorHelper.poiTooltipMuted);
            string id = type.identifier;
            Kit.TextButton(row, open ? "Hide" : "Show", () => { expanded = open ? null : id; Draw(autoOpen: false); }, 60f);
            if (!open) continue;

            var sb = new StringBuilder();
            UsedIn.AppendLines(sb, type, recipes, int.MaxValue);
            ListText = Kit.Text(results, sb.ToString(1, sb.Length - 1));
        }
        if (hits.Count > MaxHits) Kit.Muted(results, "Showing " + MaxHits + " of " + hits.Count + "; type more of the name.");
        else if (hits.Count == 0) Kit.Muted(results, "No match among the items your craftable recipes use.");
    }

    private static string Name(InventoryItemType t) =>
        Translation.Translate(t.displayName) + (t.itemLevel > 0 ? " L" + t.itemLevel : "");
}
