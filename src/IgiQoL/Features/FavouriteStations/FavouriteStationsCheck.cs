using System.Collections;
using System.Collections.Generic;
using Behaviour.UI.Spacestation;
using Behaviour.UI.Tooltip;
using IgiQoL.Core;
using IgiQoL.UI;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.Util;
using Source.UI.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.FavouriteStations;

internal static class FavouriteStationsCheck
{
    public static IEnumerator Run(SelfCheckContext ctx)
    {
        var saved = new List<string>(FavouriteStations.Guids);
        // A station with material storage, for the storage search; the station you're at for the rest.
        SpaceStation here = MapPointOfInterest.current as SpaceStation, stored = null;
        string item = null;
        var unlocked = new Dictionary<SystemMapData, bool>();
        foreach (var poi in GalaxyMapData.current.allPointsOfInterest)
        {
            if (!(poi is SpaceStation s) || !FavouriteStations.Listable(s, unlocked)) continue;
            if (here == null) here = s;
            if (stored != null || s.materialStorage == null) continue;
            foreach (var stack in s.materialStorage.items)
                if (stack.count > 0) { stored = s; item = Translation.Translate(stack.item.displayName); break; }
        }
        if (here == null)
        {
            ctx.Skip("roundtrip", "no station on the map");
            yield break;
        }
        try
        {
            CheckRoundTrip(ctx, here);
            CheckTooltip(ctx, here);
            CheckStationSearch(ctx, here);
            CheckItemSearch(ctx, stored, item);
        }
        finally { Restore(saved); }

        yield return CheckTab(ctx, here, stored);
        Restore(saved);
        yield return CheckAirlock(ctx);
        Restore(saved);
    }

    private static void Restore(List<string> saved)
    {
        FavouriteStations.Guids.Clear();
        FavouriteStations.Guids.AddRange(saved);
        FavouriteStations.Write();
    }

    // Toggle writes the save record, Load reads it back, and the guid resolves to the same station.
    private static void CheckRoundTrip(SelfCheckContext ctx, SpaceStation s)
    {
        FavouriteStations.Guids.Clear();
        FavouriteStations.Toggle(s, notify: false);
        FavouriteStations.Guids.Clear();
        FavouriteStations.Load();
        var resolved = FavouriteStations.Stations();
        bool loaded = FavouriteStations.IsFavourite(s) && resolved.Count == 1 && resolved[0] == s;
        FavouriteStations.Guids.Add("no-such-station");
        bool skipsGone = FavouriteStations.Stations().Count == 1;
        FavouriteStations.Guids.Remove("no-such-station");
        FavouriteStations.Toggle(s, notify: false);
        FavouriteStations.Load();
        bool removed = !FavouriteStations.IsFavourite(s) && FavouriteStations.Guids.Count == 0;
        string detail = $"{s.name}: loaded back={loaded}, unknown guid skipped={skipsGone}, removed={removed}";
        if (loaded && skipsGone && removed) ctx.Pass("roundtrip", detail);
        else ctx.Fail("roundtrip", detail);
    }

    private static string ShiftHint(TooltipCard card)
    {
        string shift = Translation.Translate("@TipHintShiftLeftClick");
        foreach (var h in card.hints)
            if (h.key == shift) return h.action;
        return null;
    }

    // The map tooltip names the Shift-click action for the station's current state, and only on the map.
    private static void CheckTooltip(SelfCheckContext ctx, SpaceStation s)
    {
        FavouriteStations.Guids.Clear();
        string add = ShiftHint(PoiTooltipCardBuilder.Build(s, true));
        FavouriteStations.Guids.Add(s.guid);
        string remove = ShiftHint(PoiTooltipCardBuilder.Build(s, true));
        string offMap = ShiftHint(PoiTooltipCardBuilder.Build(s, false));
        string detail = $"not favourite='{add}' favourite='{remove}' off the map='{offMap}'";
        if (add == "Add to favourites" && remove == "Remove from favourites" && offMap == null) ctx.Pass("tooltip-hint", detail);
        else ctx.Fail("tooltip-hint", detail);
    }

    private static void CheckStationSearch(SelfCheckContext ctx, SpaceStation s)
    {
        string needle = s.name.Length > 4 ? s.name.Substring(1, s.name.Length - 2) : s.name;
        var hits = FavouriteStationsTab.FindStations(needle.ToUpperInvariant());
        var bySystem = FavouriteStationsTab.FindStations(s.system.name);
        string detail = $"'{needle}' found {hits.Count} (has {s.name}={hits.Contains(s)}), system '{s.system.name}' found {bySystem.Count} (has it={bySystem.Contains(s)})";
        if (hits.Contains(s) && bySystem.Contains(s)) ctx.Pass("station-search", detail);
        else ctx.Fail("station-search", detail);
    }

    // Stacks of one item add up per station, and the match ignores case.
    private static void CheckItemSearch(SelfCheckContext ctx, SpaceStation s, string item)
    {
        if (s == null)
        {
            ctx.Skip("item-search", "no station with material storage");
            return;
        }
        int want = 0;
        foreach (var stack in s.materialStorage.items)
            if (stack.count > 0 && Translation.Translate(stack.item.displayName) == item) want += stack.count;
        var hits = FavouriteStationsTab.FindItems(item.ToLowerInvariant(), new List<SpaceStation> { s });
        int got = -1;
        foreach (var h in hits)
            if (h.station == s && h.name == item) got = h.count;
        string detail = $"{item} at {s.name}: found {got}, stored {want}; {hits.Count} hits";
        if (got == want && want > 0) ctx.Pass("item-search", detail);
        else ctx.Fail("item-search", detail);
    }

    private static bool HasText(RectTransform root, string text)
    {
        if (root == null) return false;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>())
            if (t.text.Contains(text)) return true;
        return false;
    }

    // The tab shows the favourite, the storage search lists its item, and the x button removes it.
    private static IEnumerator CheckTab(SelfCheckContext ctx, SpaceStation here, SpaceStation stored)
    {
        FavouriteStations.Guids.Clear();
        FavouriteStations.Guids.Add(here.guid);
        if (stored != null && stored != here) FavouriteStations.Guids.Add(stored.guid);
        FavouriteStations.Write();
        ModWindow.Open(FavouriteStations.TabId);
        ctx.Cleanup(ModWindow.Close);
        yield return null;
        bool listed = ModWindow.CurrentTab == FavouriteStations.TabId && HasText(ModWindow.Content, here.name);
        string item = null;
        if (stored != null)
            foreach (var stack in stored.materialStorage.items)
                if (stack.count > 0) { item = Translation.Translate(stack.item.displayName); break; }
        bool found = true;
        if (item != null)
        {
            FavouriteStationsTab.RunItems(item);
            FavouriteStationsTab.RunStations(here.system.name);
            yield return null;
            found = HasText(FavouriteStationsTab.ItemResults, item) && HasText(FavouriteStationsTab.ItemResults, stored.name)
                    && HasText(FavouriteStationsTab.StationResults, here.name);
        }
        yield return ctx.Screenshot("window-stations");

        Button remove = null;
        foreach (var b in ModWindow.Content.GetComponentsInChildren<Button>())
            if (b.name == FavouriteStationsTab.RemoveName) { remove = b; break; }
        int before = FavouriteStations.Guids.Count;
        // The click rebuilds the tab, which destroys the button, so note it was there first.
        bool clicked = remove != null;
        if (remove != null) remove.onClick.Invoke();
        yield return null;
        bool removedOne = clicked && FavouriteStations.Guids.Count == before - 1;
        FavouriteStationsTab.RunItems("");
        FavouriteStationsTab.RunStations("");
        string detail = $"{here.name} listed={listed}; search for '{item ?? "(no stored item)"}' and system found={found}; x clicked={clicked}, favourites {before} -> {FavouriteStations.Guids.Count}";
        if (listed && found && removedOne) ctx.Pass("tab", detail);
        else ctx.Fail("tab", detail);
        ModWindow.Close();
    }

    // Docked: the airlock button toggles the station and its label follows.
    private static IEnumerator CheckAirlock(SelfCheckContext ctx)
    {
        var interior = SpaceStationInterior.instance;
        if (interior == null || SpaceStation.current == null)
        {
            ctx.Skip("airlock", "not docked");
            yield break;
        }
        // Earlier checks can leave another facility open; the airlock (and its button) only exists while it's shown.
        interior.GoToLocation(SpaceStationFacility.Airlock);
        AirlockFavourite fav = null;
        float until = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < until)
        {
            if (interior.currentAirlock != null) fav = interior.currentAirlock.GetComponent<AirlockFavourite>();
            if (fav != null) break;
            yield return null;
        }
        if (fav == null)
        {
            ctx.Fail("airlock", $"no Favourite button on the airlock (airlock shown={interior.currentAirlock != null})");
            yield break;
        }
        var station = SpaceStation.current;
        FavouriteStations.Guids.Remove(station.guid);
        fav.Refresh();
        var label = fav.button.GetComponentInChildren<TMP_Text>();
        string first = label.text;
        fav.button.GetComponent<Button>().onClick.Invoke();
        bool added = FavouriteStations.IsFavourite(station);
        string second = label.text;
        yield return ctx.Screenshot("airlock-favourite");
        fav.button.GetComponent<Button>().onClick.Invoke();
        bool removed = !FavouriteStations.IsFavourite(station);
        string detail = $"{station.name}: '{first}' -> added={added} '{second}' -> removed={removed} '{label.text}'; active={fav.button.activeInHierarchy}";
        if (first == "Favourite" && added && second == "Unfavourite" && removed && label.text == "Favourite" && fav.button.activeInHierarchy) ctx.Pass("airlock", detail);
        else ctx.Fail("airlock", detail);
    }
}
