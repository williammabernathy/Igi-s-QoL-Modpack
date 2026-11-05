using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Behavior.UI.GalaxyMap;
using Behaviour.UI;
using Behaviour.UI.Missions;
using Behaviour.UI.Shared;
using Behaviour.UI.Side_Menu.SideTabs.Captain;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using Source.MissionSystem;
using Source.Player;
using Source.UI.Tooltip;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.MissionSort;

public enum BoardSort { GameOrder, Type, Level, Difficulty }

// The board's orders in the same places plus Distance, so a BoardSort converts to a WindowSort by value.
public enum WindowSort { GameOrder, Type, Level, Difficulty, Distance }

// The station mission board lists its offers in the order they were generated, and the missions window lists yours
// in the order you took them. This sorts each section of both by type, level or difficulty (the window also by
// distance); ties keep the game's order.
public sealed class MissionSort : Feature
{
    public static MissionSort Instance;

    public MissionSort() { Instance = this; }

    public override string Id => "MissionSort";
    public override string Title => "Mission Sort";
    public override string Description => "Sorts the station mission board and the missions window by type, level or difficulty, and the missions window also by distance. Pick the order with the sort button in the board's header or under the missions window's list (each click moves to the next one), or here. Each section is sorted on its own, and missions that tie keep the game's order. While sorting by level, each row shows the mission's level.";
    public override Type[] PatchClasses => new[] { typeof(MissionSortPatches) };

    // Display names by WindowSort value; the board uses the first four.
    internal static readonly string[] Names = { "Game order", "Type", "Level", "Difficulty", "Distance" };
    internal const string ButtonName = "IgiQoL_Sort";
    internal const string FooterName = "IgiQoL_SortFooter";
    internal const string LevelChipName = "IgiQoL_Level";
    internal const string DistanceChipName = "IgiQoL_Distance";

    public ConfigEntry<BoardSort> Board;
    public ConfigEntry<WindowSort> Window;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        Board = cfg.Bind(Id, "Board", BoardSort.GameOrder, SettingInfo.Describe("Mission board order",
            "Order of the missions in each section of the station mission board. Game order: as the game lists them. Type: story, combat, mining, salvage, cargo, repair, then the rest. Level: highest first, with the level shown on each row. Difficulty: hardest first (Insane, Hard, Challenging, Normal, Easy), then faction and story missions. The sort button in the board's header changes this too.",
            Names.Take(4).ToArray()));
        Window = cfg.Bind(Id, "Window", WindowSort.GameOrder, SettingInfo.Describe("Missions window order",
            "Order of your missions in each section of the missions window (\"Ready to turn in\" and \"In progress\"). Game order, Type, Level and Difficulty work as on the mission board; under Level, each row's level replaces its jump count, since the list has no room for both. Distance: nearest first (the location you're at, then this system, then by jumps), with missions whose location or route is unknown last. The sort button under the window's list changes this too.",
            Names));
    }

    // Sort key, smallest first. Type follows the game's MissionCategory order with Generic moved last. Difficulty
    // ranks the five rolled ladders hardest first; Faction, Tutorial and Story have no place on it and go after.
    internal static int Key(WindowSort sort, Mission mission)
    {
        switch (sort)
        {
            case WindowSort.Type:
                var category = mission.GetCategory();
                return category == MissionCategory.Generic ? int.MaxValue : (int)category;
            case WindowSort.Level:
                int level = Level(mission);
                return level < 0 ? int.MaxValue : -level;
            case WindowSort.Difficulty:
                return mission.difficulty <= MissionDifficulty.Insane ? -(int)mission.difficulty : 1;
            case WindowSort.Distance:
                return DistanceKey(mission);
            default:
                return 0;
        }
    }

    // Mission.level without its throw: the game reads sourcePoi.level, and some story missions you hold have no
    // source location. -1 when neither the source nor any step names a location.
    internal static int Level(Mission mission)
    {
        if (mission.dynamicLevel) return GamePlayer.current.level;
        int level = mission.sourcePoi?.level ?? -1;
        foreach (var step in mission.steps)
            foreach (var poi in step.pointsOfInterest)
                if (poi != null) level = Mathf.Max(level, poi.level);
        return level;
    }

    // The location the row's jump chip names: 0 when you're there, 1 in this system, jumps + 1 elsewhere, and last
    // without a location or a known route. Mission distance's route cache holds the jumps.
    private static int DistanceKey(Mission mission)
    {
        var poi = mission.GetActivePoi();
        if (poi == null || poi.system == null) return int.MaxValue;
        if (poi == GamePlayer.current.currentPointOfInterest) return 0;
        int jumps = MissionDistance.MissionDistance.Instance.Jumps(poi);
        return jumps < 0 ? int.MaxValue : jumps + 1;
    }

    // OrderBy is stable, so missions with the same key keep the game's order.
    internal static void Sort(List<Mission> missions, WindowSort sort)
    {
        if (sort == WindowSort.GameOrder || missions.Count < 2) return;
        var sorted = missions.OrderBy(m => Key(sort, m)).ToList();
        missions.Clear();
        missions.AddRange(sorted);
    }

    // Board and Window share these: the entry's value as a WindowSort, and the next one in its own enum.
    internal static WindowSort Current(ConfigEntryBase entry) => (WindowSort)Convert.ToInt32(entry.BoxedValue);

    internal static WindowSort Next(ConfigEntryBase entry) =>
        (WindowSort)(((int)Current(entry) + 1) % Enum.GetValues(entry.SettingType).Length);

    internal static string Tooltip(ConfigEntryBase entry)
    {
        var sort = Current(entry);
        string now = sort switch
        {
            WindowSort.Level => "Level, highest first",
            WindowSort.Difficulty => "Difficulty, hardest first",
            WindowSort.Distance => "Distance, nearest first",
            _ => Names[(int)sort],
        };
        return $"Now: {now}.\nClick to sort by {Names[(int)Next(entry)]}.";
    }

    // Moves to the next order and rebuilds the list; each list's rebuild keeps the mission you had selected.
    internal static void Cycle(ConfigEntryBase entry, TMP_Text label, Image background, Button button, Action rebuild)
    {
        entry.BoxedValue = Enum.ToObject(entry.SettingType, (int)Next(entry));
        Show(entry, label, background, button);
        rebuild();
    }

    internal static void Show(ConfigEntryBase entry, TMP_Text label, Image background, Button button)
    {
        var sort = Current(entry);
        label.text = Names[(int)sort];
        background.color = sort == WindowSort.GameOrder ? ColorHelper.mapDockButton : ColorHelper.mapDockButtonActive;
        FleetBarParts.SetTooltip(button, "Sort missions", Tooltip(entry));
    }

    // Same look as the game's ship list sort button (ShipListToolbar.AddSortButton): sort icon and the current order.
    internal static RectTransform AddButton(RectTransform parent, ConfigEntryBase entry, Action rebuild)
    {
        var rect = FleetBarParts.AddRow(parent, ButtonName, 4f);
        rect.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(5, 5, 0, 0);
        var background = UiCards.AddBackground(rect.gameObject, ColorHelper.mapDockButton);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        WindowBorder.Add(rect, ColorHelper.stationRowBorder);
        FleetBarParts.AddIcon(rect, PoiIconKeys.Fleet("Sort"), ColorHelper.poiTooltipMuted, 16f);
        var label = FleetBarParts.AddFlexibleLine(rect, "", 11f, ColorHelper.offWhite);
        // Wide enough for the longest order this entry can take: at 100 px "Game order" was cut to "Game or".
        float widest = 0f;
        for (int i = 0; i < Enum.GetValues(entry.SettingType).Length; i++)
            widest = Mathf.Max(widest, label.GetPreferredValues(Names[i]).x);
        CaptainCards.SetFixedSize(rect, Mathf.Max(100f, Mathf.Ceil(widest) + 32f), 24f);
        // The flexible label would otherwise make the button take all the row's free width.
        rect.GetComponent<LayoutElement>().flexibleWidth = 0f;
        Show(entry, label, background, button);
        button.onClick.AddListener(() =>
        {
            try { Cycle(entry, label, background, button, rebuild); }
            catch (Exception e) { Instance.Fail(e, nameof(Cycle)); }
        });
        return rect;
    }

    internal static void AddBoardButton(MissionBoard board, RectTransform headerRight)
    {
        var rect = AddButton(headerRight, Instance.Board, () =>
        {
            MissionBoard.preselectMission = board.window.detail.mission;
            board.ShowMissionBoard();
        });
        rect.SetSiblingIndex(0);
    }

    // The missions window has no header, so the button gets its own row under the list, above the game's
    // "N / 20 slots used" line; the 190 px column can't hold both on one line.
    internal static void AddWindowButton(MissionUI ui)
    {
        var footer = ui.window.footer.transform;
        var row = FleetBarParts.AddRow((RectTransform)footer.parent, FooterName, 0f);
        row.SetSiblingIndex(footer.GetSiblingIndex());
        var group = row.GetComponent<HorizontalLayoutGroup>();
        group.childAlignment = TextAnchor.MiddleCenter;
        group.padding = new RectOffset(0, 0, 4, 0);
        var layout = row.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = 28f;
        AddButton(row, Instance.Window, ui.UpdateMissionList);
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => MissionSortCheck.Run(ctx);
}

[HarmonyPatch]
internal static class MissionSortPatches
{
    private static MissionSort F => MissionSort.Instance;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MissionBoard), "BuildHeaderRight")]
    private static void HeaderPostfix(MissionBoard __instance, MissionWindowLayout ___window)
    {
        if (!F.Active) return;
        try { MissionSort.AddBoardButton(__instance, ___window.headerRight); }
        catch (Exception e) { F.Fail(e, nameof(HeaderPostfix)); }
    }

    // Each section's list is a copy made in ShowMissionBoard, so sorting it in place leaves the game's lists alone.
    // The ready and available lists are also what SelectFirst picks from, so the first row shown is the one selected.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MissionBoard), "AddSection")]
    private static void AddSectionPrefix(List<Mission> missions)
    {
        if (!F.Active) return;
        try { MissionSort.Sort(missions, (WindowSort)F.Board.Value); }
        catch (Exception e) { F.Fail(e, nameof(AddSectionPrefix)); }
    }

    // When no mission is ready or available, the game selects the first in-progress mission in its own order, not the
    // first row shown. Under a sort, select the first row, as everywhere else.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MissionBoard), "SelectFirst")]
    private static void SelectFirstPostfix(MissionBoard __instance, List<Mission> turnIns, List<Mission> ready, List<Mission> available, List<MissionListRow> ___rows)
    {
        if (!F.Active || F.Board.Value == BoardSort.GameOrder) return;
        try
        {
            if (ready.Count + available.Count > 0 || turnIns.Count == 0 || ___rows.Count == 0) return;
            if (MissionBoard.preselectMission != null && turnIns.Contains(MissionBoard.preselectMission)) return;
            __instance.ShowMission(___rows[0].mission);
        }
        catch (Exception e) { F.Fail(e, nameof(SelectFirstPostfix)); }
    }

    // MissionUI builds its layout in Start and fills the list right after.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MissionUI), "Start")]
    private static void WindowStartPostfix(MissionUI __instance)
    {
        if (!F.Active) return;
        try { MissionSort.AddWindowButton(__instance); }
        catch (Exception e) { F.Fail(e, nameof(WindowStartPostfix)); }
    }

    // The window's sections are lazy queries over allMissions, so each one is replaced by a sorted copy.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MissionUI), "AddSection")]
    private static void WindowSectionPrefix(ref IEnumerable<Mission> missions)
    {
        if (!F.Active || F.Window.Value == WindowSort.GameOrder) return;
        try
        {
            var list = missions.ToList();
            MissionSort.Sort(list, F.Window.Value);
            missions = list;
        }
        catch (Exception e) { F.Fail(e, nameof(WindowSectionPrefix)); }
    }

    // Without a mission you picked before (first open, or it was turned in), the game selects the first one in its
    // own order; under a sort, the first row shown is selected instead, as on the board.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MissionUI), nameof(MissionUI.UpdateMissionList))]
    private static void UpdateListPrefix(out bool __state)
    {
        __state = false;
        if (!F.Active || F.Window.Value == WindowSort.GameOrder) return;
        try { __state = MissionUI.lastSelected == null || !GamePlayer.current.allMissions.Contains(MissionUI.lastSelected); }
        catch (Exception e) { F.Fail(e, nameof(UpdateListPrefix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MissionUI), nameof(MissionUI.UpdateMissionList))]
    private static void UpdateListPostfix(MissionUI __instance, bool __state)
    {
        if (!__state || !F.Active || __instance.rows.Count == 0) return;
        try { __instance.ShowMission(__instance.rows[0].mission); }
        catch (Exception e) { F.Fail(e, nameof(UpdateListPostfix)); }
    }

    // "L12" after the row's chips while sorting by level; no row shows the level otherwise. Low priority runs this
    // after Mission distance's postfix, so its chip is already there: on the board the level goes before it, and in
    // the missions window's 190 px list it takes the jump chip's place so the progress bar keeps its width. A mission
    // without a known level (no location anywhere) keeps its row as it is.
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Low)]
    [HarmonyPatch(typeof(MissionListRow), "Init")]
    private static void RowInitPostfix(MissionListRow __instance, RectTransform rect)
    {
        if (!F.Active) return;
        try
        {
            bool board = F.Board.Value == BoardSort.Level && rect.GetComponentInParent<MissionBoard>() != null;
            bool window = !board && F.Window.Value == WindowSort.Level && rect.GetComponentInParent<MissionUI>() != null;
            if (!board && !window) return;
            int level = MissionSort.Level(__instance.mission);
            var tags = rect.Find("Text/Tags") as RectTransform;
            if (tags == null || level < 0) return;
            var chip = MissionCardParts.AddChip(tags, "L" + level, ColorHelper.poiTooltipMuted);
            chip.name = MissionSort.LevelChipName;
            var distance = tags.Find(MissionSort.DistanceChipName);
            if (distance == null) return;
            if (board) chip.SetSiblingIndex(distance.GetSiblingIndex());
            else
            {
                distance.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(distance.gameObject);
            }
        }
        catch (Exception e) { F.Fail(e, nameof(RowInitPostfix)); }
    }
}
