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
using Source.UI.Tooltip;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.MissionSort;

public enum BoardSort { GameOrder, Type, Level, Difficulty }

// The station mission board lists its offers in the order they were generated. This sorts each of its sections
// (ready to turn in, in progress, available) by type, level or difficulty; ties keep the game's order.
public sealed class MissionSort : Feature
{
    public static MissionSort Instance;

    public MissionSort() { Instance = this; }

    public override string Id => "MissionSort";
    public override string Title => "Mission sort";
    public override string Description => "Sorts the station mission board by type, level or difficulty. Pick the order with the sort button in the board's header (each click moves to the next one) or here. Each section of the board is sorted on its own, and missions that tie keep the game's order. While sorting by level, each row shows the mission's level.";
    public override Type[] PatchClasses => new[] { typeof(MissionSortPatches) };

    internal static readonly string[] Names = { "Game order", "Type", "Level", "Difficulty" };
    internal const string ButtonName = "IgiQoL_Sort";
    internal const string LevelChipName = "IgiQoL_Level";

    public ConfigEntry<BoardSort> Board;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        Board = cfg.Bind(Id, "Board", BoardSort.GameOrder, SettingInfo.Describe("Mission board order",
            "Order of the missions in each section of the station mission board. Game order: as the game lists them. Type: story, combat, mining, salvage, cargo, repair, then the rest. Level: highest first, with the level shown on each row. Difficulty: hardest first (Insane, Hard, Challenging, Normal, Easy), then faction and story missions. The sort button in the board's header changes this too.",
            Names));
    }

    // Sort key, smallest first. Type follows the game's MissionCategory order with Generic moved last. Difficulty
    // ranks the five rolled ladders hardest first; Faction, Tutorial and Story have no place on it and go after.
    internal static int Key(BoardSort sort, Mission mission)
    {
        switch (sort)
        {
            case BoardSort.Type:
                var category = mission.GetCategory();
                return category == MissionCategory.Generic ? int.MaxValue : (int)category;
            case BoardSort.Level:
                return -mission.level;
            case BoardSort.Difficulty:
                return mission.difficulty <= MissionDifficulty.Insane ? -(int)mission.difficulty : 1;
            default:
                return 0;
        }
    }

    // OrderBy is stable, so missions with the same key keep the game's order.
    internal static void Sort(List<Mission> missions, BoardSort sort)
    {
        if (sort == BoardSort.GameOrder || missions.Count < 2) return;
        var sorted = missions.OrderBy(m => Key(sort, m)).ToList();
        missions.Clear();
        missions.AddRange(sorted);
    }

    internal static BoardSort Next(BoardSort sort) => (BoardSort)(((int)sort + 1) % Names.Length);

    internal static string Tooltip(BoardSort sort)
    {
        string now = sort switch
        {
            BoardSort.Level => "Level, highest first",
            BoardSort.Difficulty => "Difficulty, hardest first",
            _ => Names[(int)sort],
        };
        return $"Now: {now}.\nClick to sort by {Names[(int)Next(sort)]}.";
    }

    // Moves to the next order and rebuilds the board, keeping the mission you had selected.
    internal static void Cycle(MissionBoard board, TMP_Text label, Image background, Button button)
    {
        var f = Instance;
        f.Board.Value = Next(f.Board.Value);
        Show(f.Board.Value, label, background, button);
        MissionBoard.preselectMission = board.window.detail.mission;
        board.ShowMissionBoard();
    }

    internal static void Show(BoardSort sort, TMP_Text label, Image background, Button button)
    {
        label.text = Names[(int)sort];
        background.color = sort == BoardSort.GameOrder ? ColorHelper.mapDockButton : ColorHelper.mapDockButtonActive;
        FleetBarParts.SetTooltip(button, "Sort missions", Tooltip(sort));
    }

    // Same look as the game's ship list sort button (ShipListToolbar.AddSortButton): sort icon and the current order.
    internal static void AddButton(MissionBoard board, RectTransform headerRight)
    {
        var rect = FleetBarParts.AddRow(headerRight, ButtonName, 4f);
        rect.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(5, 5, 0, 0);
        var background = UiCards.AddBackground(rect.gameObject, ColorHelper.mapDockButton);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        WindowBorder.Add(rect, ColorHelper.stationRowBorder);
        CaptainCards.SetFixedSize(rect, 100f, 24f);
        // The flexible label would otherwise make the button take all the header's free width.
        rect.GetComponent<LayoutElement>().flexibleWidth = 0f;
        FleetBarParts.AddIcon(rect, PoiIconKeys.Fleet("Sort"), ColorHelper.poiTooltipMuted, 16f);
        var label = FleetBarParts.AddFlexibleLine(rect, "", 11f, ColorHelper.offWhite);
        rect.SetSiblingIndex(0);
        Show(Instance.Board.Value, label, background, button);
        button.onClick.AddListener(() =>
        {
            try { Cycle(board, label, background, button); }
            catch (Exception e) { Instance.Fail(e, nameof(Cycle)); }
        });
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
        try { MissionSort.AddButton(__instance, ___window.headerRight); }
        catch (Exception e) { F.Fail(e, nameof(HeaderPostfix)); }
    }

    // Each section's list is a copy made in ShowMissionBoard, so sorting it in place leaves the game's lists alone.
    // The ready and available lists are also what SelectFirst picks from, so the first row shown is the one selected.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MissionBoard), "AddSection")]
    private static void AddSectionPrefix(List<Mission> missions)
    {
        if (!F.Active) return;
        try { MissionSort.Sort(missions, F.Board.Value); }
        catch (Exception e) { F.Fail(e, nameof(AddSectionPrefix)); }
    }

    // "L12" after the board row's chips while sorting by level; no row shows the level otherwise. It goes before the
    // Mission distance chip when that one is already there.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MissionListRow), "Init")]
    private static void RowInitPostfix(MissionListRow __instance, RectTransform rect)
    {
        if (!F.Active || F.Board.Value != BoardSort.Level) return;
        try
        {
            var tags = rect.Find("Text/Tags") as RectTransform;
            if (tags == null || rect.GetComponentInParent<MissionBoard>() == null) return;
            var chip = MissionCardParts.AddChip(tags, "L" + __instance.mission.level, ColorHelper.poiTooltipMuted);
            chip.name = MissionSort.LevelChipName;
            var distance = tags.Find("IgiQoL_Distance");
            if (distance != null) chip.SetSiblingIndex(distance.GetSiblingIndex());
        }
        catch (Exception e) { F.Fail(e, nameof(RowInitPostfix)); }
    }
}
