using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Behaviour.UI.Missions;
using Behaviour.UI.Spacestation;
using IgiQoL.Core;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.MissionSystem;
using Source.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Board = Behaviour.UI.MissionBoard;

namespace IgiQoL.Features.MissionSort;

internal static class MissionSortCheck
{
    public static IEnumerator Run(SelfCheckContext ctx)
    {
        Board board = null;
        yield return OpenBoard(ctx, b => board = b);
        if (board == null) yield break;
        var f = MissionSort.Instance;
        var saved = f.Board.Value;
        try
        {
            Apply(board, BoardSort.GameOrder);
            yield return null;
            var sections = Sections(board);
            int count = sections.Sum(s => s.Count);
            if (count < 2)
            {
                ctx.Skip("order", $"the board shows {count} mission(s)");
                yield break;
            }
            var available = GamePlayer.current.currentPointOfInterest is SpaceStation ss ? ss.missionBoard.availableMissions : new List<Mission>();
            var shownAvailable = sections.SelectMany(s => s).Where(available.Contains).ToList();
            string gameDetail = $"{shownAvailable.Count} offers: " + string.Join(", ", shownAvailable.Select(m => m.name));
            if (shownAvailable.SequenceEqual(available)) ctx.Pass("game-order", gameDetail);
            else ctx.Fail("game-order", gameDetail + " | board: " + string.Join(", ", available.Select(m => m.name)));
            if (Chips(board) > 0) ctx.Fail("game-order-chips", $"{Chips(board)} level chips without the Level sort");

            foreach (var sort in new[] { BoardSort.Type, BoardSort.Level, BoardSort.Difficulty })
            {
                Apply(board, sort);
                yield return null;
                CheckOrder(ctx, sort, Sections(board));
                if (sort == BoardSort.Level)
                {
                    CheckChips(ctx, board);
                    yield return ctx.Screenshot("mission-board-sort");
                }
                else if (Chips(board) > 0) ctx.Fail("chips-" + sort, $"{Chips(board)} level chips without the Level sort");
            }

            yield return CheckButton(ctx, board);
        }
        finally { f.Board.Value = saved; }
    }

    // What a header click does, for a given order: the setting, the button's label and the rebuilt board.
    private static void Apply(Board board, BoardSort sort)
    {
        MissionSort.Instance.Board.Value = sort;
        var rect = board.window.headerRight.Find(MissionSort.ButtonName);
        if (rect != null)
        {
            var button = rect.GetComponent<Button>();
            MissionSort.Show(sort, rect.GetComponentInChildren<TMP_Text>(), button.GetComponent<Image>(), button);
        }
        board.ShowMissionBoard();
    }

    // Each section must be ordered by the sort key, with ties in the game's order.
    private static void CheckOrder(SelfCheckContext ctx, BoardSort sort, List<List<Mission>> sections)
    {
        int bad = 0, keys = 0;
        string sample = "";
        foreach (var section in sections)
        {
            keys += section.Select(m => MissionSort.Key(sort, m)).Distinct().Count();
            for (int i = 1; i < section.Count; i++)
            {
                Mission a = section[i - 1], b = section[i];
                int ka = MissionSort.Key(sort, a), kb = MissionSort.Key(sort, b);
                if (ka > kb || (ka == kb && GameIndex(a) > GameIndex(b))) bad++;
            }
            sample += " [" + string.Join(", ", section.Select(m => Describe(sort, m))) + "]";
        }
        string detail = $"{sections.Count} sections, {keys} distinct keys, {bad} out of order:{sample}";
        if (bad == 0) ctx.Pass("order-" + sort, detail);
        else ctx.Fail("order-" + sort, detail);
    }

    private static void CheckChips(SelfCheckContext ctx, Board board)
    {
        int good = 0, bad = 0;
        foreach (var row in board.GetComponentsInChildren<MissionListRow>())
        {
            var chip = row.GetComponentsInChildren<RectTransform>().FirstOrDefault(t => t.name == MissionSort.LevelChipName);
            if (chip != null && chip.GetComponentInChildren<TMP_Text>().text == "L" + row.mission.level) good++;
            else bad++;
        }
        string detail = $"{good} rows with the right level chip, {bad} without";
        if (bad == 0 && good > 0) ctx.Pass("level-chip", detail);
        else ctx.Fail("level-chip", detail);
    }

    // The header button: a click moves to the next order, relabels itself, re-sorts and keeps the selection.
    private static IEnumerator CheckButton(SelfCheckContext ctx, Board board)
    {
        var f = MissionSort.Instance;
        var rect = board.window.headerRight.Find(MissionSort.ButtonName);
        if (rect == null) { ctx.Fail("button", "no sort button in the board header"); yield break; }
        var button = rect.GetComponent<Button>();
        var label = rect.GetComponentInChildren<TMP_Text>();
        Apply(board, BoardSort.Difficulty);
        yield return null;
        var last = Sections(board).SelectMany(s => s).Last();
        board.ShowMission(last);
        button.onClick.Invoke();
        yield return null;
        var selected = board.window.detail.mission;
        var sections = Sections(board);
        float width = ((RectTransform)rect).rect.width;
        string detail = $"after Difficulty: setting {f.Board.Value}, label '{label.text}', selected '{selected?.name}' (was '{last.name}'), header index {rect.GetSiblingIndex()}, width {width:0} px";
        if (f.Board.Value == BoardSort.GameOrder && label.text == MissionSort.Names[0] && selected == last && sections.SelectMany(s => s).Contains(last) && Mathf.Approximately(width, 100f))
            ctx.Pass("button", detail);
        else ctx.Fail("button", detail);
    }

    private static int Chips(Board board) =>
        board.GetComponentsInChildren<RectTransform>().Count(t => t.name == MissionSort.LevelChipName);

    // The list's rows in order, split where the game put a section heading.
    private static List<List<Mission>> Sections(Board board)
    {
        var sections = new List<List<Mission>>();
        List<Mission> current = null;
        foreach (Transform child in board.window.list)
        {
            if (child.TryGetComponent<MissionListRow>(out var row))
            {
                if (current == null) sections.Add(current = new List<Mission>());
                current.Add(row.mission);
            }
            else current = null;
        }
        return sections;
    }

    // Board offers by their place on the board, your missions by their place in your list.
    private static int GameIndex(Mission m)
    {
        var poi = GamePlayer.current.currentPointOfInterest as SpaceStation;
        int i = poi?.missionBoard.availableMissions.IndexOf(m) ?? -1;
        return i >= 0 ? i : GamePlayer.current.missions.IndexOf(m);
    }

    private static string Describe(BoardSort sort, Mission m) => sort switch
    {
        BoardSort.Type => m.GetCategory().ToString(),
        BoardSort.Level => "L" + m.level,
        _ => m.difficulty.ToString(),
    };

    private static IEnumerator OpenBoard(SelfCheckContext ctx, System.Action<Board> found)
    {
        if (SpaceStationInterior.instance == null && Behaviour.Managers.BasePoiManager.current is SpacestationExteriorManager exterior)
        {
            exterior.EnterSpacestation();
            float wait = Time.realtimeSinceStartup + 30f;
            while (SpaceStationInterior.instance == null && Time.realtimeSinceStartup < wait) yield return null;
            yield return new WaitForSeconds(3f);
            if (SpaceStationInterior.instance != null) ctx.Cleanup(() => { if (SpaceStationInterior.instance != null) SpaceStationInterior.instance.ExitSpacestation(); });
        }
        var interior = SpaceStationInterior.instance;
        if (interior == null || !interior.HasMissionBoard())
        {
            ctx.Skip("order", "not docked at a station with a mission board: " + MapPointOfInterest.current?.name);
            yield break;
        }
        interior.GoToLocation(SpaceStationFacility.MissionBoard);
        ctx.Cleanup(() => { if (SpaceStationInterior.instance != null) SpaceStationInterior.instance.GoToLocation(SpaceStationFacility.Airlock); });
        float until = Time.realtimeSinceStartup + 10f;
        Board board = null;
        while ((board = interior.GetComponentInChildren<Board>()) == null && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSeconds(1.5f);
        if (board == null) ctx.Fail("order", "mission board did not open");
        else found(board);
    }
}
