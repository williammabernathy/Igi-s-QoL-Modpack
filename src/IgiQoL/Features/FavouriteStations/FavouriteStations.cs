using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour;
using Behaviour.GalaxyMap;
using Behaviour.Managers;
using Behaviour.UI.DebugScreen;
using Behaviour.UI.NotificationAlert;
using Behaviour.UI.Spacestation;
using Behaviour.UI.Tooltip;
using Behaviour.Util;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using LightJson;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.Player;
using Source.UI.Tooltip;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.FavouriteStations;

public sealed class FavouriteStations : Feature
{
    public static FavouriteStations Instance;

    public FavouriteStations() { Instance = this; }

    public override string Id => "FavouriteStations";
    public override string Title => "Favourite stations";
    public override string Description => "A list of the stations you pick on the Stations tab: what each one has, your storage there, the jumps to it and a Travel to button, plus a search through their storage. Add a station with Shift-click on the galaxy map, the Favourite button in its airlock, or the search on the tab.";
    public override Type[] PatchClasses => new[] { typeof(FavouriteStationsPatches) };

    public const string TabId = "stations";

    // Station guids (MapElement.guid) in the order they were added. Names can repeat; guids don't.
    public static readonly List<string> Guids = new();

    public override void OnActivated()
    {
        Load();
        GameEvents.SaveLoaded += OnSaveLoaded;
        ModWindow.AddTab(this, TabId, "Stations", 22, FavouriteStationsTab.Build);
    }

    private void OnSaveLoaded()
    {
        if (!Active) return;
        try
        {
            Load();
            FavouriteStationsTab.Reset();
        }
        catch (Exception e) { Fail(e, nameof(OnSaveLoaded)); }
    }

    internal static void Load()
    {
        Guids.Clear();
        var rec = SaveData.Feature(Instance.Id, false);
        if (rec == null || !rec["stations"].IsJsonArray) return;
        foreach (var v in rec["stations"].AsJsonArray)
            if (v.IsString && !Guids.Contains(v.AsString)) Guids.Add(v.AsString);
    }

    internal static void Write()
    {
        var arr = new JsonArray();
        foreach (var g in Guids) arr.Add(g);
        SaveData.Feature(Instance.Id, true)["stations"] = arr;
    }

    public static bool IsFavourite(SpaceStation station) => station != null && Guids.Contains(station.guid);

    public static void Toggle(SpaceStation station, bool notify)
    {
        bool added = !Guids.Remove(station.guid);
        if (added) Guids.Add(station.guid);
        Write();
        if (notify)
            Singleton<NotificationManager>.Instance.CreateNotification(station.name + (added ? " added to" : " removed from") + " your favourite stations")
                .WithColor(ColorHelper.greenish).Show();
        if (ModWindow.IsOpen && ModWindow.CurrentTab == TabId) ModWindow.Show(TabId);
    }

    // The favourites that still exist in this galaxy; a guid whose station is gone is skipped, not dropped.
    public static List<SpaceStation> Stations()
    {
        var list = new List<SpaceStation>();
        if (Guids.Count == 0 || GalaxyMapData.current == null) return list;
        var byGuid = new Dictionary<string, SpaceStation>();
        foreach (var poi in GalaxyMapData.current.allPointsOfInterest)
            if (poi is SpaceStation s && Guids.Contains(s.guid)) byGuid[s.guid] = s;
        foreach (var g in Guids)
            if (byGuid.TryGetValue(g, out var s)) list.Add(s);
        return list;
    }

    // Jumps from your current system, as the game's station picker counts them; a system missing from it has no route.
    public static Dictionary<SystemMapData, int> Jumps()
    {
        var here = GamePlayer.current?.currentSystem;
        return here != null && GalaxyMapData.current != null ? GalaxyMapData.current.GetJumpDistances(here, 200) : new Dictionary<SystemMapData, int>();
    }

    public static int? JumpsTo(Dictionary<SystemMapData, int> jumps, SpaceStation s) =>
        s.system != null && jumps.TryGetValue(s.system, out int n) ? n : (int?)null;

    // Nearest first, then by name: the order of the game's station picker.
    public static void SortByJumps(List<SpaceStation> stations, Dictionary<SystemMapData, int> jumps)
    {
        stations.Sort((a, b) =>
        {
            int c = (JumpsTo(jumps, a) ?? int.MaxValue).CompareTo(JumpsTo(jumps, b) ?? int.MaxValue);
            return c != 0 ? c : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        });
    }

    // Stations the galaxy map would show you: not hidden, and in a system you can reach.
    public static bool Listable(SpaceStation s, Dictionary<SystemMapData, bool> unlocked)
    {
        if (s.hidden || !s.visibleOnMap || s.system == null) return false;
        if (!unlocked.TryGetValue(s.system, out bool open)) unlocked[s.system] = open = s.system.isUnlocked;
        return open;
    }

    // The galaxy map's own travel: TryInitiateTravel plots the route through the gates, and a docked start asks first
    // when the game has something to warn about (no ammo aboard, fighters not equipped).
    public static void Travel(SpaceStation s)
    {
        var tm = Singleton<TravelManager>.Instance;
        if (tm == null) return;
        ModWindow.Close();
        tm.ConfirmIfDockedThen(s, () => tm.TryInitiateTravel(s));
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => FavouriteStationsCheck.Run(ctx);
}

[HarmonyPatch]
internal static class FavouriteStationsPatches
{
    private static FavouriteStations F => FavouriteStations.Instance;
    private static int lastToggleFrame = -1;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Airlock), "Start")]
    private static void AirlockStartPostfix(Airlock __instance)
    {
        if (!F.Active) return;
        try { AirlockFavourite.Attach(__instance); }
        catch (Exception e) { F.Fail(e, nameof(AirlockStartPostfix)); }
    }

    // Shift-click on a station marker toggles it instead of plotting a course there.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorldMapPOI), nameof(WorldMapPOI.MouseUpHandling))]
    private static bool MouseUpHandlingPrefix(WorldMapPOI __instance)
    {
        if (!F.Active) return true;
        try
        {
            if (!GlobalControls.modifierShift || !(__instance.content is SpaceStation station) || MapTargetPicker.isActive || ConsoleScreen.DebugModifier())
                return true;
            // OnMouseUpAsButton and OnPointerClick can both report the same click.
            if (Time.frameCount != lastToggleFrame)
            {
                lastToggleFrame = Time.frameCount;
                FavouriteStations.Toggle(station, notify: true);
            }
            return false;
        }
        catch (Exception e)
        {
            F.Fail(e, nameof(MouseUpHandlingPrefix));
            return true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PoiTooltipCardBuilder), nameof(PoiTooltipCardBuilder.Build))]
    private static void TooltipBuildPostfix(MapPointOfInterest poi, bool onMap, TooltipCard __result)
    {
        if (!F.Active || !onMap || !(poi is SpaceStation station)) return;
        try { __result.AddHint(TooltipHints.ShiftLmb(FavouriteStations.IsFavourite(station) ? "Remove from favourites" : "Add to favourites")); }
        catch (Exception e) { F.Fail(e, nameof(TooltipBuildPostfix)); }
    }
}
