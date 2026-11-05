using System;
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
using Object = UnityEngine.Object;
using SidePanel = Behaviour.UI.Side_Menu.SidePanel;

namespace IgiQoL.Features.MissionSort;

internal static class MissionSortCheck
{
    public static IEnumerator Run(SelfCheckContext ctx)
    {
        yield return CheckBoard(ctx);
        yield return CheckWindow(ctx);
    }

    private static IEnumerator CheckBoard(SelfCheckContext ctx)
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
            var sections = Sections(board.window.list);
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
                CheckOrder(ctx, "order-" + sort, (WindowSort)sort, Sections(board.window.list), GameIndex);
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
        var f = MissionSort.Instance;
        f.Board.Value = sort;
        Relabel(f.Board, board.window.headerRight.Find(MissionSort.ButtonName));
        board.ShowMissionBoard();
    }

    private static void Relabel(BepInEx.Configuration.ConfigEntryBase entry, Transform rect)
    {
        if (rect == null) return;
        var button = rect.GetComponent<Button>();
        MissionSort.Show(entry, rect.GetComponentInChildren<TMP_Text>(), button.GetComponent<Image>(), button);
    }

    // Each section must be ordered by the sort key, with ties in the game's order.
    private static void CheckOrder(SelfCheckContext ctx, string name, WindowSort sort, List<List<Mission>> sections, Func<Mission, int> gameIndex)
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
                if (ka > kb || (ka == kb && gameIndex(a) > gameIndex(b))) bad++;
            }
            sample += " [" + string.Join(", ", section.Select(m => Describe(sort, m))) + "]";
        }
        string detail = $"{sections.Count} sections, {keys} distinct keys, {bad} out of order:{sample}";
        if (bad == 0) ctx.Pass(name, detail);
        else ctx.Fail(name, detail);
    }

    private static void CheckChips(SelfCheckContext ctx, Board board)
    {
        int good = 0, bad = 0;
        foreach (var row in board.GetComponentsInChildren<MissionListRow>())
        {
            var chip = Find(row, MissionSort.LevelChipName);
            // A mission with no known level deliberately gets no chip.
            if (MissionSort.Level(row.mission) < 0) { if (chip == null) good++; else bad++; continue; }
            if (chip != null && chip.GetComponentInChildren<TMP_Text>().text == "L" + MissionSort.Level(row.mission)) good++;
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
        var last = Sections(board.window.list).SelectMany(s => s).Last();
        board.ShowMission(last);
        button.onClick.Invoke();
        yield return null;
        var selected = board.window.detail.mission;
        var sections = Sections(board.window.list);
        float width = ((RectTransform)rect).rect.width, over = Overflow(label, 4);
        string detail = $"after Difficulty: setting {f.Board.Value}, label '{label.text}', selected '{selected?.name}' (was '{last.name}'), header index {rect.GetSiblingIndex()}, width {width:0} px, longest label overflow {over:0.0} px";
        if (f.Board.Value == BoardSort.GameOrder && label.text == MissionSort.Names[0] && selected == last && sections.SelectMany(s => s).Contains(last) && Snug(over))
            ctx.Pass("button", detail);
        else ctx.Fail("button", detail);
    }

    // The side panel's Missions tab: your accepted missions, ready to turn in and in progress.
    private static IEnumerator CheckWindow(SelfCheckContext ctx)
    {
        var panel = SidePanel.instance;
        if (panel == null) { ctx.Skip("window", "no side panel"); yield break; }
        // Switching tabs unloads the panel's content, so an open MissionUI (Mission distance's check leaves one) is
        // destroyed at the end of the frame; wait for the new one.
        var old = Object.FindObjectsByType<MissionUI>(FindObjectsInactive.Exclude).FirstOrDefault();
        panel.SwitchToTab(SidePanel.SideTabType.Missions);
        ctx.Cleanup(() => { if (SidePanel.instance != null) SidePanel.instance.SwitchToTab(SidePanel.SideTabType.Inventory); });
        float until = Time.realtimeSinceStartup + 10f;
        MissionUI ui = null;
        while (((ui = Object.FindObjectsByType<MissionUI>(FindObjectsInactive.Exclude).FirstOrDefault(u => u != old)) == null) && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSeconds(1.5f);
        if (ui == null) { ctx.Fail("window", "missions window did not open"); yield break; }
        var f = MissionSort.Instance;
        var saved = f.Window.Value;
        var savedSelection = MissionUI.lastSelected;
        try
        {
            ApplyWindow(ui, WindowSort.GameOrder);
            yield return null;
            var all = GamePlayer.current.allMissions.ToList();
            var sections = Sections(ui.window.list);
            int count = sections.Sum(s => s.Count);
            if (count < 2)
            {
                ctx.Skip("window", $"{count} accepted mission(s)");
                yield break;
            }
            string gameDetail = string.Join(" ", sections.Select(s => "[" + string.Join(", ", s.Select(m => m.name)) + "]"));
            if (sections.All(s => s.Select(m => all.IndexOf(m)).SequenceEqual(s.Select(m => all.IndexOf(m)).OrderBy(i => i)))) ctx.Pass("window-game-order", gameDetail);
            else ctx.Fail("window-game-order", gameDetail);
            CheckWindowChips(ctx, ui, WindowSort.GameOrder);

            foreach (var sort in new[] { WindowSort.Type, WindowSort.Level, WindowSort.Difficulty, WindowSort.Distance })
            {
                ApplyWindow(ui, sort);
                yield return null;
                CheckOrder(ctx, "window-order-" + sort, sort, Sections(ui.window.list), all.IndexOf);
                CheckWindowChips(ctx, ui, sort);
                if (sort == WindowSort.Level) yield return ctx.Screenshot("missions-window-sort");
            }

            // Under a sort and with nothing picked before, the first row shown is the one selected.
            MissionUI.lastSelected = null;
            ui.UpdateMissionList();
            yield return null;
            var first = ui.rows.Count > 0 ? ui.rows[0].mission : null;
            var vanilla = all.FirstOrDefault(MissionProgress.IsReadyToTurnIn) ?? all.FirstOrDefault();
            var picked = ui.window.detail.mission;
            string selectDetail = $"Distance: selected '{picked?.name}', first row '{first?.name}', game's pick '{vanilla?.name}'";
            if (first != null && picked == first && MissionUI.lastSelected == first) ctx.Pass("window-select-first", selectDetail);
            else ctx.Fail("window-select-first", selectDetail);

            yield return CheckWindowButton(ctx, ui);
        }
        finally
        {
            f.Window.Value = saved;
            MissionUI.lastSelected = savedSelection;
        }
    }

    private static void ApplyWindow(MissionUI ui, WindowSort sort)
    {
        var f = MissionSort.Instance;
        f.Window.Value = sort;
        Relabel(f.Window, WindowButton(ui));
        ui.UpdateMissionList();
    }

    private static Transform WindowButton(MissionUI ui) =>
        ui.window.footer.transform.parent.Find(MissionSort.FooterName + "/" + MissionSort.ButtonName);

    // Under Level every row with a known level carries it and no jump chip, and the progress bar keeps 40 px; rows
    // without a level, and every row under any other order, have no level chip and Mission distance's jump chips.
    private static void CheckWindowChips(SelfCheckContext ctx, MissionUI ui, WindowSort sort)
    {
        bool distanceOn = MissionDistance.MissionDistance.Instance.Active;
        int good = 0, bad = 0, jumpChips = 0, unknown = 0;
        float narrowest = float.MaxValue;
        foreach (var row in ui.GetComponentsInChildren<MissionListRow>())
        {
            var chip = Find(row, MissionSort.LevelChipName);
            bool hasJumps = Find(row, MissionSort.DistanceChipName) != null;
            if (hasJumps) jumpChips++;
            int level = MissionSort.Level(row.mission);
            if (level < 0) unknown++;
            bool ok = sort == WindowSort.Level && level >= 0
                ? chip != null && chip.GetComponentInChildren<TMP_Text>().text == "L" + level && !hasJumps
                : chip == null && (!distanceOn || hasJumps == MissionDistance.MissionDistance.RowJumps(row.mission) > 0);
            if (ok) good++; else bad++;
            if (row.barFill != null) narrowest = Mathf.Min(narrowest, ((RectTransform)row.barFill.parent).rect.width);
        }
        string detail = $"{good} rows right, {bad} wrong, {jumpChips} jump chips, {unknown} without a level, narrowest bar {narrowest:0} px";
        // The same floor as Mission distance's row check: about 58 px of bar without a chip since the game's 16 px scrollbar.
        if (bad == 0 && good > 0 && narrowest >= 30f) ctx.Pass("window-chips-" + sort, detail);
        else ctx.Fail("window-chips-" + sort, detail);
    }

    // The button under the list: a click after Distance goes back to Game order, relabels, re-sorts and keeps the
    // selection, and the game's "N / 20 slots used" line below it still fits.
    private static IEnumerator CheckWindowButton(SelfCheckContext ctx, MissionUI ui)
    {
        var f = MissionSort.Instance;
        var rect = WindowButton(ui);
        if (rect == null) { ctx.Fail("window-button", "no sort button under the missions window's list"); yield break; }
        var button = rect.GetComponent<Button>();
        var label = rect.GetComponentInChildren<TMP_Text>();
        ApplyWindow(ui, WindowSort.Distance);
        yield return null;
        var last = ui.rows[ui.rows.Count - 1].mission;
        ui.ShowMission(last);
        button.onClick.Invoke();
        yield return null;
        var selected = ui.window.detail.mission;
        var footer = ui.window.footer;
        float width = ((RectTransform)rect).rect.width, footerWidth = footer.rectTransform.rect.width, over = Overflow(label, 5);
        bool fits = !footer.isTextTruncated && footer.preferredWidth <= footerWidth + 0.5f;
        string detail = $"after Distance: setting {f.Window.Value}, label '{label.text}', selected '{selected?.name}' (was '{last.name}'), width {width:0} px, longest label overflow {over:0.0} px, footer '{footer.text}' needs {footer.preferredWidth:0} of {footerWidth:0} px";
        if (f.Window.Value == WindowSort.GameOrder && label.text == MissionSort.Names[0] && selected == last && Snug(over) && fits)
            ctx.Pass("window-button", detail);
        else ctx.Fail("window-button", detail);
    }

    // Every name fits and the button isn't stretched (a flexible button once took the whole header).
    private static bool Snug(float overflow) => overflow <= 0.5f && overflow >= -10f;

    // How far the longest of the first count order names runs past the button's label; at most 0 when all fit.
    private static float Overflow(TMP_Text label, int count)
    {
        float worst = float.MinValue;
        for (int i = 0; i < count; i++)
            worst = Mathf.Max(worst, label.GetPreferredValues(MissionSort.Names[i]).x - label.rectTransform.rect.width);
        return worst;
    }

    private static Transform Find(Component row, string name) =>
        row.GetComponentsInChildren<RectTransform>().FirstOrDefault(t => t.name == name);

    private static int Chips(Board board) =>
        board.GetComponentsInChildren<RectTransform>().Count(t => t.name == MissionSort.LevelChipName);

    // The list's rows in order, split where the game put a section heading.
    private static List<List<Mission>> Sections(RectTransform list)
    {
        var sections = new List<List<Mission>>();
        List<Mission> current = null;
        foreach (Transform child in list)
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

    private static string Describe(WindowSort sort, Mission m) => sort switch
    {
        WindowSort.Type => m.GetCategory().ToString(),
        WindowSort.Level => MissionSort.Level(m) is var level && level >= 0 ? "L" + level : "no level",
        WindowSort.Distance => m.name + " " + (MissionDistance.MissionDistance.Instance.Short(m.GetActivePoi()) ?? "-"),
        _ => m.difficulty.ToString(),
    };

    // Docks if needed and opens the station's mission board; skips or fails under the given check name.
    internal static IEnumerator OpenBoard(SelfCheckContext ctx, Action<Board> found, string check = "order")
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
            ctx.Skip(check, "not docked at a station with a mission board: " + MapPointOfInterest.current?.name);
            yield break;
        }
        interior.GoToLocation(SpaceStationFacility.MissionBoard);
        ctx.Cleanup(() => { if (SpaceStationInterior.instance != null) SpaceStationInterior.instance.GoToLocation(SpaceStationFacility.Airlock); });
        float until = Time.realtimeSinceStartup + 10f;
        Board board = null;
        while ((board = interior.GetComponentInChildren<Board>()) == null && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSeconds(1.5f);
        // A board already open (an earlier check's) may have been replaced by GoToLocation and destroyed since.
        board = interior.GetComponentInChildren<Board>();
        if (board == null) ctx.Fail(check, "mission board did not open");
        else found(board);
    }
}
