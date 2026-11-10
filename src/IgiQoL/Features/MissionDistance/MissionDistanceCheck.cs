using System.Collections;
using System.Linq;
using Behaviour.UI.Missions;
using Behaviour.UI.Spacestation;
using HarmonyLib;
using IgiQoL.Core;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.Player;
using TMPro;
using UnityEngine;
using Board = Behaviour.UI.MissionBoard;
using SidePanel = Behaviour.UI.Side_Menu.SidePanel;

namespace IgiQoL.Features.MissionDistance;

internal static class MissionDistanceCheck
{
    public static IEnumerator Run(SelfCheckContext ctx)
    {
        CheckJumps(ctx);
        CheckTracker(ctx);
        yield return CheckBoard(ctx);
        yield return CheckWindow(ctx);
    }

    // A location two jumps away by the galaxy's own jump distances must read "2 jumps".
    private static void CheckJumps(SelfCheckContext ctx)
    {
        var f = MissionDistance.Instance;
        var player = GamePlayer.current;
        var here = player.currentPointOfInterest;
        var local = player.currentSystem.pointsOfInterest.FirstOrDefault(p => p != here);
        var dist = GalaxyMapData.current.GetJumpDistances(player.currentSystem, 4);
        var far = dist.Where(kv => kv.Value == 2).Select(kv => kv.Key).FirstOrDefault();
        var farPoi = far?.pointsOfInterest.FirstOrDefault();
        if (farPoi == null)
        {
            ctx.Skip("jumps", "no system two jumps from " + player.currentSystem.name);
            return;
        }
        if (local == null)
        {
            ctx.Skip("jumps", "the current system has no second location to read as 'this system'");
            return;
        }
        string a = f.Short(here), b = f.Short(local), c = f.Long(farPoi);
        int n = f.Jumps(farPoi);
        string detail = $"current '{here?.name}' -> '{a}', '{local?.name}' -> '{b}', '{farPoi.name}' in {far.name} -> '{c}' (route {n}, galaxy 2)";
        // The route may use a wormhole the gate-only galaxy distance doesn't, so it can be shorter but never longer.
        if (a == "here" && b == "this\u00A0system" && n >= 1 && n <= 2 && c == far.name + ", " + MissionDistance.JumpsText(n)) ctx.Pass("jumps", detail);
        else ctx.Fail("jumps", detail);
    }

    // The game's tracker text for an accepted mission's current step, through the patched private method.
    private static void CheckTracker(SelfCheckContext ctx)
    {
        var display = Object.FindObjectsByType<FocusedMissionDisplay>(FindObjectsInactive.Include).FirstOrDefault();
        var mission = GamePlayer.current.missions.FirstOrDefault(m => m.currentStep != null && m.currentStep.pointsOfInterest.Any(p => p != null));
        if (display == null || mission == null)
        {
            ctx.Skip("tracker", display == null ? "no FocusedMissionDisplay" : "no accepted mission with a location");
            return;
        }
        string text = (string)AccessTools.Method(typeof(FocusedMissionDisplay), "GetLocationLines").Invoke(display, new object[] { mission.currentStep });
        var poi = mission.currentStep.pointsOfInterest.First(p => p != null);
        string want = MissionDistance.Instance.Long(poi);
        string detail = $"'{mission.name}': '{text.Trim()}'";
        if (want != null && text.Contains("(" + want + ")")) ctx.Pass("tracker", detail);
        else ctx.Fail("tracker", detail + $", expected '({want})'");
    }

    private static IEnumerator CheckBoard(SelfCheckContext ctx)
    {
        yield return ctx.EnsureDocked(s => s.HasFacility(SpaceStationFacility.MissionBoard));
        var interior = SpaceStationInterior.instance;
        if (interior == null || !interior.HasMissionBoard())
        {
            ctx.Skip("board", ctx.DockNote ?? "not docked at a station with a mission board: " + MapPointOfInterest.current?.name);
            yield break;
        }
        interior.GoToLocation(SpaceStationFacility.MissionBoard);
        ctx.Cleanup(() => { if (SpaceStationInterior.instance != null) SpaceStationInterior.instance.GoToLocation(SpaceStationFacility.Airlock); });
        float until = Time.realtimeSinceStartup + 10f;
        Board board = null;
        while ((board = interior.GetComponentInChildren<Board>()) == null && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSeconds(1.5f);
        if (board == null)
        {
            ctx.Fail("board", "mission board did not open");
            yield break;
        }
        var f = MissionDistance.Instance;
        var rows = board.GetComponentsInChildren<MissionListRow>();
        int good = 0, bad = 0;
        string sample = "";
        foreach (var row in rows)
        {
            string want = Want(row.mission);
            string got = Tag(row);
            if (got == want) good++;
            else bad++;
            if (sample.Length < 300) sample += $" '{row.mission.name}'={got ?? "-"};";
        }
        string detail = $"{rows.Length} rows, {good} right, {bad} wrong:{sample}";
        if (rows.Length > 0 && bad == 0) ctx.Pass("board-rows", detail);
        else if (rows.Length == 0) ctx.Skip("board-rows", "board has no missions");
        else ctx.Fail("board-rows", detail);

        var pane = board.GetComponentInChildren<MissionDetailPane>();
        var mission = pane == null ? null : Traverse.Create(pane).Field<Source.MissionSystem.Mission>("mission").Value;
        var location = pane == null ? null : pane.GetComponentsInChildren<RectTransform>().FirstOrDefault(t => t.name == "Location");
        if (mission == null || location == null)
        {
            ctx.Skip("board-detail", mission == null ? "no mission selected" : "selected mission has no location line");
        }
        else
        {
            string want = f.Long(MissionDistance.DetailPoi(mission));
            string got = location.GetComponentInChildren<TMP_Text>().text;
            string d = $"'{mission.name}': '{got}'";
            if (want != null && got.EndsWith("(" + want + ")")) ctx.Pass("board-detail", d);
            else ctx.Fail("board-detail", d + $", expected '({want})'");
        }
        // Board missions sit in the station's own system, so the rows above rarely carry a tag. A real mission from a
        // station board in another system, built as a row at the top of this board's list, must.
        var far = FarMission(out var farPoi);
        if (far == null)
        {
            ctx.Skip("board-far", "no station board within 3 jumps holds a mission outside this system");
            yield return ctx.Screenshot("mission-board-distance");
            yield break;
        }
        var farRow = MissionListRow.Build(board.window.list, far, showProgress: false, null);
        farRow.transform.SetSiblingIndex(0);
        yield return null;
        string farGot = Tag(farRow), farWant = Want(far);
        int n = f.Jumps(farPoi);
        string farDetail = $"'{far.name}' at {farPoi.name} in {farPoi.system.name} (route {n}): tag '{farGot ?? "-"}'";
        if (n > 0 && farGot != null && farGot == farWant) ctx.Pass("board-far", farDetail);
        else ctx.Fail("board-far", farDetail + $", expected '{farWant}'");
        yield return ctx.Screenshot("mission-board-distance");
        Object.Destroy(farRow.gameObject);
    }

    // The side panel's Missions tab lists every accepted mission with a progress bar, in a 190 px list.
    private static IEnumerator CheckWindow(SelfCheckContext ctx)
    {
        var panel = SidePanel.instance;
        if (panel == null) { ctx.Skip("window-rows", "no side panel"); yield break; }
        // The window's sort setting removes the distance chip in some orders, so the check reads rows in game order.
        var sort = MissionSort.MissionSort.Instance;
        bool swapSort = sort != null && sort.Active;
        var savedSort = swapSort ? sort.Window.Value : default;
        // Switching tabs unloads the panel's content, so a MissionUI that is already open is destroyed at the end of
        // the frame; wait for a new one.
        var old = Object.FindObjectsByType<MissionUI>(FindObjectsInactive.Exclude).FirstOrDefault();
        try
        {
            if (swapSort) sort.Window.Value = MissionSort.WindowSort.GameOrder;
            panel.SwitchToTab(SidePanel.SideTabType.Missions);
            ctx.Cleanup(() => { if (SidePanel.instance != null) SidePanel.instance.SwitchToTab(SidePanel.SideTabType.Inventory); });
            float until = Time.realtimeSinceStartup + 10f;
            MissionUI ui = null;
            while ((ui = Object.FindObjectsByType<MissionUI>(FindObjectsInactive.Exclude).FirstOrDefault(u => u != old)) == null && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSeconds(1.5f);
            if (ui == null) { ctx.Fail("window-rows", "missions window did not open"); yield break; }
            var rows = ui.GetComponentsInChildren<MissionListRow>();
            int good = 0, bad = 0, tagged = 0;
            float taggedBar = float.MaxValue, plainBar = float.MaxValue;
            string sample = "";
            foreach (var row in rows)
            {
                string want = Want(row.mission);
                string got = Tag(row);
                if (got == want) good++; else bad++;
                float bar = row.barFill == null ? 0f : ((RectTransform)row.barFill.parent).rect.width;
                if (got != null) { tagged++; taggedBar = Mathf.Min(taggedBar, bar); }
                else plainBar = Mathf.Min(plainBar, bar);
                if (sample.Length < 300) sample += $" '{row.mission.name}'={got ?? "-"};";
            }
            // The 190 px list, less the game's 16 px scrollbar (4 px before 0.8.3.5), leaves a progress bar of about
            // 58 px beside the category chip; a tag must not squeeze it below 30.
            string detail = $"{rows.Length} rows, {tagged} tagged, {good} right, {bad} wrong, narrowest bar {taggedBar:0} px tagged / {plainBar:0} px untagged:{sample}";
            if (rows.Length > 0 && bad == 0 && (tagged == 0 || taggedBar >= 30f)) ctx.Pass("window-rows", detail);
            else if (rows.Length == 0) ctx.Skip("window-rows", "no accepted missions");
            else ctx.Fail("window-rows", detail);
            yield return ctx.Screenshot("missions-window-distance");
        }
        finally
        {
            if (swapSort) sort.Window.Value = savedSort;
        }
    }

    // A row's tag as "number | tooltip"; null without one.
    private static string Tag(MissionListRow row)
    {
        var chip = row.GetComponentsInChildren<RectTransform>().FirstOrDefault(t => t.name == "IgiQoL_Distance");
        if (chip == null) return null;
        var tooltip = chip.GetComponent<Behaviour.UI.Tooltip.TooltipSource>();
        return chip.GetComponentInChildren<TMP_Text>().text + " | " + (tooltip != null ? tooltip.BodyText : null);
    }

    private static string Want(Source.MissionSystem.Mission mission)
    {
        int n = MissionDistance.RowJumps(mission);
        return n <= 0 ? null : n + " | " + MissionDistance.Instance.Long(mission.GetActivePoi());
    }

    // Only a board that already holds an offer outside this system is used. Generating one on a station you've never
    // visited would add map points to its system, and no cleanup can remove those.
    private static Source.MissionSystem.Mission FarMission(out MapPointOfInterest poi)
    {
        var here = GamePlayer.current.currentSystem;
        var stations = GalaxyMapData.current.GetJumpDistances(here, 3).Where(kv => kv.Value > 0).OrderBy(kv => kv.Value)
            .SelectMany(kv => kv.Key.pointsOfInterest.OfType<SpaceStation>()).Where(ss => ss.missionBoard != null).ToList();
        foreach (var ss in stations)
        {
            foreach (var m in ss.missionBoard.availableMissions)
            {
                poi = m.GetActivePoi();
                if (poi?.system != null && poi.system != here) return m;
            }
        }
        poi = null;
        return null;
    }
}
