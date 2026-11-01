using System;
using System.Collections.Generic;
using System.Text;
using Behavior.UI.GalaxyMap;
using Behaviour.Item;
using IgiQoL.UI;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.UI.Tooltip;
using Source.Util;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.FavouriteStations;

// The Stations tab: the favourites with their facilities and storage, a search through that storage, and a station search
// to add more.
internal static class FavouriteStationsTab
{
    internal const int MaxStationHits = 12, MaxItemHits = 30, StorageItemsShown = 3;
    internal const string RemoveName = "RemoveFavourite";

    internal readonly struct ItemHit
    {
        public readonly SpaceStation station;
        public readonly InventoryItemType item;
        public readonly string name;
        public readonly int count;

        public ItemHit(SpaceStation station, InventoryItemType item, string name, int count)
        {
            this.station = station;
            this.item = item;
            this.name = name;
            this.count = count;
        }
    }

    // Kept across tab rebuilds, so adding or removing a favourite doesn't clear what you typed.
    private static string itemSearch = "", stationSearch = "";
    private static RectTransform itemResults, stationResults;
    private static List<SpaceStation> favourites = new();
    private static Dictionary<SystemMapData, int> jumps = new();

    // For the self-check: the result rows of the two searches.
    internal static RectTransform ItemResults => itemResults;
    internal static RectTransform StationResults => stationResults;

    internal static void Reset()
    {
        favourites = new List<SpaceStation>();
        jumps = new Dictionary<SystemMapData, int>();
        itemResults = stationResults = null;
    }

    internal static void Build(RectTransform content)
    {
        jumps = FavouriteStations.Jumps();
        favourites = FavouriteStations.Stations();
        FavouriteStations.SortByJumps(favourites, jumps);

        Kit.Heading(content, "Favourite stations");
        Kit.Muted(content, "Shift-click a station on the galaxy map, press Favourite in a station's airlock, or find it under \"Add a station\" below. Nearest first. Storage is your material storage at that station; the armory and the data inventory are the same at every station.");

        if (favourites.Count > 0)
        {
            var searchRow = Kit.Row(content);
            Kit.Search(searchRow, "Find an item in their storage", RunItems, 300f).SetTextWithoutNotify(itemSearch);
            itemResults = Kit.Column(content, 2f);
            DrawItems();
            Kit.Gap(content, 4f);
            foreach (var s in favourites) StationCard(content, s);
        }
        else
        {
            itemResults = null;
            Kit.Muted(content, "No favourites yet.");
        }

        Kit.Gap(content, 6f);
        Kit.Heading(content, "Add a station");
        var addRow = Kit.Row(content);
        Kit.Search(addRow, "Type part of a station or system name", RunStations, 300f).SetTextWithoutNotify(stationSearch);
        stationResults = Kit.Column(content, 2f);
        DrawStations();
    }

    private static string JumpText(SpaceStation s)
    {
        int? n = FavouriteStations.JumpsTo(jumps, s);
        if (n == null) return "no known route";
        return n == 0 ? "this system" : n == 1 ? "1 jump" : n + " jumps";
    }

    private static void TravelOrHere(RectTransform row, SpaceStation s)
    {
        if (MapPointOfInterest.current == s) Kit.Fixed(row, "You're here", 90f, TextAlignmentOptions.Center, ColorHelper.poiTooltipMuted);
        else Kit.TextButton(row, "Travel to", () => FavouriteStations.Travel(s), 90f);
    }

    private static void StationCard(RectTransform content, SpaceStation s)
    {
        var row = Kit.Row(content);
        var name = Kit.Label(row, s.name, 13f);
        name.color = ColorHelper.stationCardTitle;
        Kit.Fixed(row, (s.system != null ? s.system.name + " | " : "") + JumpText(s), 280f, TextAlignmentOptions.MidlineRight, ColorHelper.poiTooltipMuted);
        TravelOrHere(row, s);
        var remove = Kit.TextTile(row, "x", 22f, () => FavouriteStations.Toggle(s, notify: false));
        remove.name = RemoveName;
        Kit.Tooltip(remove.targetGraphic, null, "Remove from favourites");

        var facilities = s.GetListedFacilities();
        if (facilities.Count > 0)
        {
            var icons = Kit.Row(content, 4f, 18f);
            foreach (var (facility, key) in facilities)
            {
                var icon = FleetBarParts.AddIcon(icons, Kit.Icon("Info", PoiIconKeys.Facility(facility)), facility.GetColor(), 16f);
                Kit.Tooltip(icon, null, Translation.Translate(key));
            }
        }

        var storage = s.materialStorage;
        var stacks = new List<Source.Item.Inventory.InventoryItem>();
        if (storage != null)
            foreach (var stack in storage.items)
                if (stack.count > 0) stacks.Add(stack);
        if (stacks.Count == 0)
        {
            Kit.Muted(content, "Storage: empty");
            return;
        }
        stacks.Sort((a, b) => b.count.CompareTo(a.count));
        var line = new StringBuilder("Storage: ")
            .Append(GameMath.FormatNumber(storage.spaceUsed)).Append(" of ").Append(GameMath.FormatNumber(storage.capacity)).Append(" m3. ");
        var full = new StringBuilder();
        for (int i = 0; i < stacks.Count; i++)
        {
            string entry = Translation.Translate(stacks[i].item.displayName) + " " + GameMath.FormatNumber(stacks[i].count);
            if (i < StorageItemsShown) line.Append(i == 0 ? "" : ", ").Append(entry);
            full.Append(i == 0 ? "" : "\n").Append(entry);
        }
        if (stacks.Count > StorageItemsShown) line.Append(" and ").Append(stacks.Count - StorageItemsShown).Append(" more");
        Kit.Tooltip(Kit.Muted(content, line.ToString()), s.name + " storage", full.ToString());
    }

    internal static void RunItems(string text)
    {
        itemSearch = text ?? "";
        DrawItems();
    }

    internal static void RunStations(string text)
    {
        stationSearch = text ?? "";
        DrawStations();
    }

    private static void Clear(RectTransform column)
    {
        for (int i = column.childCount - 1; i >= 0; i--)
        {
            var child = column.GetChild(i).gameObject;
            child.SetActive(false);
            Object.Destroy(child);
        }
    }

    // Every item in the favourites' material storage whose name contains the text, stacks of one item added up, in
    // the favourites' order and then most first.
    internal static List<ItemHit> FindItems(string needle, List<SpaceStation> stations)
    {
        var hits = new List<ItemHit>();
        var counts = new Dictionary<InventoryItemType, int>();
        var names = new Dictionary<InventoryItemType, string>();
        foreach (var s in stations)
        {
            if (s.materialStorage == null) continue;
            counts.Clear();
            foreach (var stack in s.materialStorage.items)
            {
                if (stack.count <= 0) continue;
                if (!names.TryGetValue(stack.item, out string name)) names[stack.item] = name = Translation.Translate(stack.item.displayName);
                if (name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;
                counts.TryGetValue(stack.item, out int n);
                counts[stack.item] = n + stack.count;
            }
            int first = hits.Count;
            foreach (var kv in counts) hits.Add(new ItemHit(s, kv.Key, names[kv.Key], kv.Value));
            hits.Sort(first, hits.Count - first, Comparer<ItemHit>.Create((a, b) => b.count.CompareTo(a.count)));
        }
        return hits;
    }

    private static void DrawItems()
    {
        if (itemResults == null) return;
        Clear(itemResults);
        string needle = itemSearch.Trim();
        if (needle.Length < 2) return;
        var hits = FindItems(needle, favourites);
        int shown = Math.Min(MaxItemHits, hits.Count);
        for (int i = 0; i < shown; i++)
        {
            var hit = hits[i];
            var row = Kit.Row(itemResults, 6f, 22f);
            var label = Kit.Label(row, hit.name);
            label.color = hit.item.rarity.GetColor();
            Kit.ItemTooltip(label, hit.item);
            Kit.Fixed(row, GameMath.FormatNumber(hit.count), 70f, TextAlignmentOptions.MidlineRight, ColorHelper.offWhite);
            Kit.Fixed(row, hit.station.name + " | " + JumpText(hit.station), 240f, TextAlignmentOptions.MidlineRight, ColorHelper.poiTooltipMuted);
            TravelOrHere(row, hit.station);
        }
        if (hits.Count > MaxItemHits) Kit.Muted(itemResults, "Showing " + MaxItemHits + " of " + hits.Count + "; type more of the name.");
        else if (hits.Count == 0) Kit.Muted(itemResults, "None of your favourites stores an item with that name.");
    }

    // Stations the galaxy map shows whose name or system name contains the text, nearest first.
    internal static List<SpaceStation> FindStations(string needle)
    {
        var hits = new List<SpaceStation>();
        if (GalaxyMapData.current == null) return hits;
        var unlocked = new Dictionary<SystemMapData, bool>();
        foreach (var poi in GalaxyMapData.current.allPointsOfInterest)
        {
            if (!(poi is SpaceStation s) || !FavouriteStations.Listable(s, unlocked)) continue;
            if (s.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 || s.system.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                hits.Add(s);
        }
        FavouriteStations.SortByJumps(hits, jumps);
        return hits;
    }

    private static void DrawStations()
    {
        if (stationResults == null) return;
        Clear(stationResults);
        string needle = stationSearch.Trim();
        if (needle.Length < 2) return;
        var hits = FindStations(needle);
        int shown = Math.Min(MaxStationHits, hits.Count);
        for (int i = 0; i < shown; i++)
        {
            var s = hits[i];
            var row = Kit.Row(stationResults, 6f, 22f);
            Kit.Label(row, s.name);
            Kit.Fixed(row, s.system.name + " | " + JumpText(s), 240f, TextAlignmentOptions.MidlineRight, ColorHelper.poiTooltipMuted);
            bool favourite = FavouriteStations.IsFavourite(s);
            Kit.TextButton(row, favourite ? "Remove" : "Add", () => FavouriteStations.Toggle(s, notify: false), 80f);
        }
        if (hits.Count > MaxStationHits) Kit.Muted(stationResults, "Showing " + MaxStationHits + " of " + hits.Count + "; type more of the name.");
        else if (hits.Count == 0) Kit.Muted(stationResults, "No station you can see on the galaxy map has that name.");
    }
}
