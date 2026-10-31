using Behavior.UI.GalaxyMap;
using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Managers;
using Behaviour.UI.Missions;
using Behaviour.Util;
using HarmonyLib;
using IgiQoL.Core;
using Source.Galaxy;
using Source.MissionSystem;
using Source.Player;
using Source.UI.Tooltip;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.MissionDistance;

// The mission board, the missions window and the HUD tracker name a mission's location but never say how far away it
// is, so picking missions means opening the galaxy map for each one.
public sealed class MissionDistance : Feature
{
    public static MissionDistance Instance;

    public MissionDistance() { Instance = this; }

    public override string Id => "MissionDistance";
    public override string Title => "Mission distance";
    public override string Description => "Shows how far away a mission's next location is: a jump gate icon with the number of jumps on mission board and missions window rows whose location is in another system (the system is in its tooltip), and the distance after the location on the mission details and the HUD mission tracker (\"here\", \"this system\", or the system and the number of jumps).";
    public override Type[] PatchClasses => new[] { typeof(MissionDistancePatches) };

    private readonly Dictionary<MapPointOfInterest, int> jumps = new();
    private SystemMapData cachedSystem;
    private float cacheUntil;

    // The game's own route search (TravelManager.GenerateShortestRoute, counting jump gates), cached per location until
    // you change system or 10 s pass, since the HUD tracker asks four times a second. -1 when no route is known.
    public int Jumps(MapPointOfInterest poi)
    {
        var player = GamePlayer.current;
        if (poi.system == player.currentSystem) return 0;
        if (player.currentSystem != cachedSystem || Time.unscaledTime > cacheUntil)
        {
            jumps.Clear();
            cachedSystem = player.currentSystem;
            cacheUntil = Time.unscaledTime + 10f;
        }
        if (jumps.TryGetValue(poi, out int n)) return n;
        var travel = Singleton<TravelManager>.Instance;
        n = travel == null ? -1 : travel.JumpDistanceToPoi(poi);
        jumps[poi] = n;
        return n;
    }

    // "here", "this system", "2 jumps"; null when there's no location or no known route. Non-breaking spaces keep
    // "1 jump" on one line in the narrow HUD tracker.
    public string Short(MapPointOfInterest poi)
    {
        if (poi == null || poi.system == null) return null;
        var player = GamePlayer.current;
        if (poi == player.currentPointOfInterest) return "here";
        int n = Jumps(poi);
        if (n == 0) return "this\u00A0system";
        if (n < 0) return null;
        return JumpsText(n);
    }

    public static string JumpsText(int n) => n == 1 ? "1\u00A0jump" : n + "\u00A0jumps";

    // "here", "this system", "Red Gate, 2 jumps".
    public string Long(MapPointOfInterest poi)
    {
        string s = Short(poi);
        if (s == null || poi.system == GamePlayer.current.currentSystem) return s;
        return poi.system.name + ", " + s;
    }

    // The location the mission details show: the turn-in station once it's complete, else the current objective's.
    public static MapPointOfInterest DetailPoi(Mission mission) =>
        mission.isComplete ? mission.turnIn : (mission.currentStep?.currentObjective?.GetPoi() ?? MissionAlerts.GetCurrentStepPoi(mission));

    // Jumps for the row tag; 0 for locations in your current system or without a known route.
    public static int RowJumps(Mission mission)
    {
        var poi = mission.GetActivePoi();
        if (poi == null || poi.system == null || poi.system == GamePlayer.current.currentSystem) return 0;
        return Math.Max(0, Instance.Jumps(poi));
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => MissionDistanceCheck.Run(ctx);
}

[HarmonyPatch]
internal static class MissionDistancePatches
{
    private static MissionDistance F => MissionDistance.Instance;

    // Mission board (available, in progress, ready to turn in) and missions window rows: a jump gate icon and the
    // number after the difficulty and category chips, or after the progress bar, with the system in the tooltip.
    // Only for other systems, and kept narrow: the missions window's list is 190 px and a "38 jumps" chip squeezed
    // its progress bars to a stub.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MissionListRow), "Init")]
    private static void RowInitPostfix(MissionListRow __instance, RectTransform rect)
    {
        if (!F.Active) return;
        try
        {
            int n = MissionDistance.RowJumps(__instance.mission);
            var tags = rect.Find("Text/Tags") as RectTransform;
            if (n <= 0 || tags == null) return;
            var color = ColorHelper.poiTooltipMuted;
            var chip = MissionCardParts.AddChip(tags, n.ToString(), color);
            chip.name = "IgiQoL_Distance";
            chip.GetComponent<HorizontalLayoutGroup>().spacing = 2f;
            FleetBarParts.AddIcon(chip, PoiIconKeys.JumpGate, color, 9f).transform.SetSiblingIndex(0);
            var background = chip.GetComponent<Image>();
            background.raycastTarget = true;
            var poi = __instance.mission.GetActivePoi();
            FleetBarParts.SetTooltip(background, poi.name, F.Long(poi));
        }
        catch (Exception e) { F.Fail(e, nameof(RowInitPostfix)); }
    }

    // Mission details: "Location: Asteroid Field T-20 (Red Gate, 2 jumps)".
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MissionDetailPane), "AddLocationLine")]
    private static void LocationLinePostfix(RectTransform section, Mission ___mission)
    {
        if (!F.Active) return;
        try
        {
            if (section.childCount == 0) return;
            var row = section.GetChild(section.childCount - 1);
            if (row.name != "Location") return;
            string text = F.Long(MissionDistance.DetailPoi(___mission));
            var label = row.GetComponentInChildren<TMP_Text>();
            if (text != null && label != null) label.text += " (" + text + ")";
        }
        catch (Exception e) { F.Fail(e, nameof(LocationLinePostfix)); }
    }

    // The details pane rebuilds only when its state string changes; with the location you're at in it, the distance
    // above is redone after each jump while the pane stays open (the missions window during travel).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MissionDetailPane), "GetState")]
    private static void GetStatePostfix(ref string __result)
    {
        if (!F.Active) return;
        try { __result += "|" + GamePlayer.current.currentPointOfInterest?.guid; }
        catch (Exception e) { F.Fail(e, nameof(GetStatePostfix)); }
    }

    // HUD mission tracker: the same lines as FocusedMissionDisplay.GetLocationLines, each with the distance.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FocusedMissionDisplay), "GetLocationLines")]
    private static void TrackerPostfix(MissionStep step, ref string __result)
    {
        if (!F.Active) return;
        try
        {
            if (step.dynamicPointOfInterest != null)
            {
                __result = TrackerLine(step.dynamicPointOfInterest);
                return;
            }
            string text = "";
            foreach (var poi in step.pointsOfInterest) text += TrackerLine(poi);
            __result = text;
        }
        catch (Exception e) { F.Fail(e, nameof(TrackerPostfix)); }
    }

    internal static string TrackerLine(MapPointOfInterest poi)
    {
        string distance = F.Long(poi);
        return Translation.Translate("@MissionLocation", poi.name) + (distance == null ? "" : " (" + distance + ")") + "\n";
    }
}
