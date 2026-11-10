using System.Collections;
using System.Collections.Generic;
using Behavior.UI.GalaxyMap;
using Behaviour.GalaxyMap;
using Behaviour.UI;
using Behaviour.UI.HUD;
using Behaviour.UI.Tooltip;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using Source.Galaxy;
using Source.Simulation.World.System;
using Source.UI.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.ConquestDetails;

internal static partial class ConquestDetailsCheck
{
    private static readonly System.Reflection.MethodInfo FleetCard = AccessTools.Method(typeof(ConquestFleetMapIcon), "DescribeTooltip");

    internal static IEnumerator Run(SelfCheckContext ctx)
    {
        var conquest = WarMath.Conquest;
        CheckNeeded(ctx);
        CheckEstimate(ctx);
        if (conquest?.systems == null || conquest.systems.Count == 0 || WarMath.Universe == null)
        {
            ctx.Skip("defense-parts", "the Conquest war hasn't started in this save");
            yield break;
        }
        CheckParts(ctx, conquest);
        CheckSystemCard(ctx, conquest);
        CheckAdvice(ctx, conquest);
        CheckJudged(ctx, conquest);
        CheckSiegeStrength(ctx, conquest);

        bool mapWasOpen = AbstractGalaxyMapManager.IsShowing();
        if (!mapWasOpen)
        {
            Behaviour.UI.Side_Menu.SidePanel.instance.SwitchToTab(Behaviour.UI.Side_Menu.SidePanel.SideTabType.Map);
            float wait = Time.realtimeSinceStartup + 20f;
            while (AbstractGalaxyMapManager.current == null && Time.realtimeSinceStartup < wait) yield return null;
            yield return new WaitForSecondsRealtime(3f);
            ctx.Cleanup(() => { if (AbstractGalaxyMapManager.IsShowing()) Behaviour.UI.Side_Menu.SidePanel.instance.CloseMap(); });
        }
        if (AbstractGalaxyMapManager.current == null)
        {
            ctx.Skip("fleet-card", "the galaxy map didn't open");
            yield break;
        }
        yield return CheckCorner(ctx);
        yield return CheckFleetCard(ctx);
        CheckReinforcing(ctx);
        yield return CheckPopout(ctx);
        yield return CheckFactionOverview(ctx);
        yield return ShowCards(ctx, conquest);
        CheckHubKills(ctx, conquest);
    }

    // The table in docs/mechanics/conquest/sector-control.md: systems a faction needs for 30% of 70 to 84.
    private static void CheckNeeded(SelfCheckContext ctx)
    {
        int[] expected = { 21, 22, 22, 22, 23, 23, 23, 24, 24, 24, 24, 25, 25, 25, 26 };
        string got = "";
        bool ok = true;
        for (int total = 70; total <= 84; total++)
        {
            int n = WarMath.NeededFor30(total);
            got += n + " ";
            if (n != expected[total - 70] || WarMath.Share(n, total) < 0.3f || WarMath.Share(n - 1, total) >= 0.3f) ok = false;
        }
        if (ok) ctx.Pass("needed-30", "70..84 -> " + got.Trim());
        else ctx.Fail("needed-30", "70..84 -> " + got.Trim());
    }

    // A medium hub (940 structure, 480 firepower) against a full fleet breaks in 10 rounds, which is also the earliest
    // a capture can happen (600 s). The station's fire falls with its structure, 480 to 276 over the first five rounds
    // and the 250 floor after that, so the fleet loses 157 and keeps 1,843. A 300-point fleet against the same hub is
    // destroyed before it breaks it. A full fleet at the top war footing (WarFooting max bonus 0.25, judged as 2,500)
    // breaks the hub in the same 10 rounds and keeps more.
    private static void CheckEstimate(SelfCheckContext ctx)
    {
        var d = new DefenseParts { Firepower = 480f, Hull = 940f, Multiplier = 1f, Flat = 0f, Structure = 1f };
        var full = WarMath.EstimateHubSiege(d, 2000f);
        var weak = WarMath.EstimateHubSiege(d, 300f);
        var footed = WarMath.EstimateHubSiege(d, 2000f * (1f + ConquestWarTuning.WarFootingMax));
        string detail = $"full: {full.Result} round {full.Rounds} left {full.PointsLeft:0} | weak: {weak.Result} round {weak.Rounds} | full footing: {footed.Result} round {footed.Rounds} left {footed.PointsLeft:0}";
        if (full.Result == SiegeResult.Captured && full.Rounds == 10 && Mathf.Abs(full.PointsLeft - 1843f) < 1f && weak.Result == SiegeResult.Destroyed
            && footed.Result == SiegeResult.Captured && footed.Rounds == 10 && footed.PointsLeft > full.PointsLeft + 400f)
            ctx.Pass("estimate", detail);
        else ctx.Fail("estimate", detail);
    }

    // The breakdown must add up to the game's Defense row and its station term must follow the game's formula, for
    // every owned war-zone system.
    private static void CheckParts(SelfCheckContext ctx, Source.Simulation.Story.Conquest conquest)
    {
        var d = new DefenseParts();
        int owned = 0, bad = 0, withStations = 0, withFleets = 0;
        string first = null;
        foreach (var cs in conquest.systems)
        {
            if (cs.isUnowned) continue;
            owned++;
            d.Read(cs);
            if (d.DefenseStations > 0) withStations++;
            if (d.FleetCount > 0) withFleets++;
            float sum = d.StationTerm + d.Garrison + d.Fleets + d.Patrols;
            float term = Mathf.Max(WarMath.FirepowerFloor * d.Multiplier, Mathf.Min(d.Firepower, WarMath.FirepowerCap * d.Multiplier) * d.Structure) + d.Flat;
            float raw = (d.HubFirepower + d.StationsFirepower) * (cs.isSettling ? 0.5f : 1f);
            if (Mathf.Abs(sum - d.Total) > 0.5f || Mathf.Abs(term - d.StationTerm) > 0.5f || Mathf.Abs(raw - d.Firepower) > 0.5f)
            {
                bad++;
                first ??= $"{cs.system.name}: parts {sum:0} total {d.Total:0} term {term:0}/{d.StationTerm:0} fp {raw:0}/{d.Firepower:0}";
            }
        }
        string detail = $"{owned} owned systems, {withStations} with Defense stations, {withFleets} with fleets" + (first != null ? ", first mismatch " + first : "");
        if (owned > 0 && bad == 0) ctx.Pass("defense-parts", detail);
        else ctx.Fail("defense-parts", detail);
    }

    // The default card of an owned system is one short War section with the hint for the key and none of the breakdown;
    // the full card adds the Defense breakdown, whose total equals the game's Defense row on the same card, and the
    // siege rules. An unowned system says it is free to take.
    private static void CheckSystemCard(SelfCheckContext ctx, Source.Simulation.Story.Conquest conquest)
    {
        ConquestSystem owned = null, unowned = null;
        var trackedFaction = WarMath.AutoTracked();
        foreach (var cs in conquest.systems)
        {
            // The verdict row is for systems the tracked faction doesn't hold already, so prefer one of those.
            if (!cs.isUnowned && !cs.cannotBeTaken && (owned == null || owned.faction == trackedFaction && cs.faction != trackedFaction)) owned = cs;
            if (cs.isUnowned && unowned == null) unowned = cs;
        }
        if (owned == null) { ctx.Skip("system-card", "no capturable owned system"); return; }
        bool tracked = trackedFaction != null && owned.faction != trackedFaction;
        var summary = Summary(owned);
        var war = Find(summary, WarCards.SummaryTitle);
        bool hint = summary.hints.Exists(h => h.action == "for the full breakdown");
        bool short_ = war != null && war.rows.Count >= 3 && war.rows.Count <= 7 && war.description == null
            && Find(summary, WarCards.DefenseTitle) == null && Find(summary, WarCards.TakingTitle) == null && Find(summary, WarCards.BattleTitle) == null;
        bool verdict = !tracked || war != null && war.rows.Count > 0 && war.rows[0].label == WarCards.VerdictLabel;
        string detail = $"{owned.system.name}: summary rows {war?.rows.Count ?? -1}, description {war?.description != null}, hint {hint}, verdict row {verdict}";
        if (short_ && hint && verdict) ctx.Pass("system-card", detail);
        else ctx.Fail("system-card", detail);

        var full = Full(owned);
        var defense = Find(full, WarCards.DefenseTitle);
        var taking = Find(full, WarCards.TakingTitle);
        string gameRow = null;
        foreach (var s in full.sections)
            foreach (var r in s.rows)
                if (r.label == Source.Util.Translation.Translate("@SystemCardDefense")) gameRow = r.value;
        string ours = defense?.count;
        bool totalsMatch = gameRow != null && ours == "= " + WarMath.Points(ConquestCombat.GetDefenseStrength(owned));
        bool noHint = !full.hints.Exists(h => h.action == "for the full breakdown");
        detail = $"{owned.system.name}: game Defense '{gameRow}', breakdown '{ours}', taking description {(taking?.description != null)}, hint gone {noHint}";
        if (defense != null && taking != null && taking.description != null && taking.description.StartsWith("Fights in order") && totalsMatch && noHint) ctx.Pass("system-card-full", detail);
        else ctx.Fail("system-card-full", detail);

        if (unowned == null) { ctx.Skip("unowned-card", "every war-zone system is owned"); return; }
        var open = Find(Summary(unowned), WarCards.SummaryTitle);
        var openFull = Find(Full(unowned), WarCards.SummaryTitle);
        bool free = trackedFaction == null || open != null && open.rows.Count > 0 && open.rows[0].value == "Free: claimed on arrival";
        bool says = trackedFaction == null || openFull != null && openFull.description != null && openFull.description.Contains("first fleet to arrive");
        bool noBreakdown = Find(Full(unowned), WarCards.DefenseTitle) == null;
        if (open != null && free && says && noBreakdown) ctx.Pass("unowned-card", unowned.system.name + ": " + (openFull?.description ?? "no description"));
        else ctx.Fail("unowned-card", unowned.system.name + $": section {open != null}, free row {free}, says {says}, no breakdown {noBreakdown}");
    }

    // The card the game builds for a system with the detail key not held, and with the full breakdown asked for. Both
    // put the setting back.
    private static TooltipCard Summary(ConquestSystem cs)
    {
        var f = ConquestDetails.Instance;
        var saved = f.CardDetail.Value;
        f.CardDetail.Value = SystemCardDetail.Summary;
        try { return MapTooltipCardBuilder.BuildSystem(cs.system); }
        finally { f.CardDetail.Value = saved; }
    }

    private static TooltipCard Full(ConquestSystem cs)
    {
        ConquestDetails.ForceFull = true;
        try { return MapTooltipCardBuilder.BuildSystem(cs.system); }
        finally { ConquestDetails.ForceFull = false; }
    }

    // A fleet icon's card has the strength row with the fleet's own points, and an NPC fleet below 35% or retreating
    // says so instead of the bare "Reinforcing". The map only draws the fleets of the system it shows, and the view it
    // opens on often has none, so with no fleet icon in view the map is taken to a system with a live fleet (the
    // game's own ShowSystemMap, as a click on that system does) and put back afterwards: later checks read the sector
    // and the view the map was on.
    private static IEnumerator CheckFleetCard(SelfCheckContext ctx)
    {
        var move = new MapMove();
        try
        {
            if (Object.FindObjectsByType<ConquestFleetMapIcon>(FindObjectsInactive.Exclude).Length == 0)
            {
                var system = FleetMapSystem();
                if (system == null) ctx.Skip("fleet-card", "no conquest fleet exists anywhere in the universe");
                else
                {
                    yield return move.Arrive<ConquestFleetMapIcon>(system);
                    if (!move.Arrived) ctx.Skip("fleet-card", $"the map didn't show a fleet icon for {system.name} within {MapMove.WaitSeconds:0} s");
                }
            }
            if (move.Arrived || !move.Moved && Object.FindObjectsByType<ConquestFleetMapIcon>(FindObjectsInactive.Exclude).Length > 0) JudgeFleetCards(ctx, move);
        }
        finally { move.Restore(); }
        yield return move.Settle();
    }

    // A system a live fleet is drawn in, one of an NPC faction's by preference: only those have the strength bar and
    // the order text this check looks for.
    private static SystemMapData FleetMapSystem()
    {
        SystemMapData any = null;
        foreach (var fleet in WarMath.Universe.AllFleets())
        {
            var system = ConquestFleetPlanner.GetMapSystem(fleet);
            if (system == null) continue;
            if (!fleet.isWarFleet) return system;
            any ??= system;
        }
        return any;
    }

    private static void JudgeFleetCards(SelfCheckContext ctx, MapMove move)
    {
        int checkedCards = 0, bad = 0, explained = 0;
        string sample = null;
        foreach (var icon in Object.FindObjectsByType<ConquestFleetMapIcon>(FindObjectsInactive.Exclude))
        {
            var fleet = icon.fleet;
            if (fleet == null) continue;
            var card = (TooltipCard)FleetCard.Invoke(icon, null);
            var s = card != null ? Find(card, WarCards.FleetTitle) : null;
            checkedCards++;
            if (s == null) { bad++; continue; }
            // A war fleet never retreats, so it has no strength bar: its section starts with its target rows or none.
            if (fleet.isWarFleet)
            {
                foreach (var row in s.rows)
                    if (row.label == "Of full strength" || row.label == "Retreats below") bad++;
            }
            else if (s.rows.Count == 0 || !s.rows[0].value.StartsWith(WarMath.Points(fleet.totalPoints) + " of ")) { bad++; continue; }
            if (s.description != null)
            {
                explained++;
                sample ??= fleet.displayName + ": " + s.description;
            }
        }
        string detail = $"{checkedCards} fleet icon cards, {explained} with an order explained" + (move.Moved ? $", map moved to {move.Target.name}" : "") + (sample != null ? ", e.g. " + sample : "");
        if (checkedCards == 0) ctx.Skip("fleet-card", "no fleet icons in the map's current view");
        else if (bad == 0) ctx.Pass("fleet-card", detail);
        else ctx.Fail("fleet-card", bad + " cards without the strength row; " + detail);
    }

    // Takes the galaxy map to a system's view and back. The view is read when the move is made: zoom level (0 is a
    // system, 1 the sector and galaxy layer), the system, the sector the map counts as viewed, the galaxy quadrant and
    // the camera. Restore() is plain code so it can run in a finally; Settle() then waits out the game's own camera
    // tween (a sector view is reached by one) and sets the camera again, since the tween ends on the sector's framing.
    internal sealed class MapMove
    {
        internal const float WaitSeconds = 5f;
        private readonly AbstractGalaxyMapManager map = AbstractGalaxyMapManager.current;
        private readonly int zoom, quadrant;
        private readonly SystemMapData system;
        private readonly SectorMapData sector;
        private readonly Vector3 cameraPosition;
        private readonly float cameraSize;

        internal SystemMapData Target { get; private set; }
        internal bool Moved { get; private set; }
        internal bool Arrived { get; private set; }

        internal MapMove()
        {
            zoom = map.zoomLevel;
            quadrant = map.currentQuadrant;
            system = map.currentSystem;
            sector = map.viewedSector;
            cameraPosition = map.mapCamera.transform.position;
            cameraSize = map.mapCamera.orthographicSize;
        }

        // The system view of `target`, locks bypassed (the game's lock only notifies), then up to WaitSeconds for the map
        // to draw a T: its wide movers (fleets, deliveries) refresh a quarter second apart.
        internal IEnumerator Arrive<T>(SystemMapData target) where T : Object
        {
            Target = target;
            Moved = true;
            map.ShowSystemMap(target, true);
            float until = Time.realtimeSinceStartup + WaitSeconds;
            while (Object.FindObjectsByType<T>(FindObjectsInactive.Exclude).Length == 0 && Time.realtimeSinceStartup < until) yield return null;
            Arrived = map.zoomLevel == 0 && map.currentSystem == target && Object.FindObjectsByType<T>(FindObjectsInactive.Exclude).Length > 0;
        }

        internal void Restore()
        {
            if (!Moved || map == null) return;
            Moved = false;
            if (zoom == 0)
            {
                if (system != null) map.ShowSystemMap(system, true);
            }
            else
            {
                if (sector != null) map.ShowSectorMap(sector);
                // The galaxy view of the quadrant the map was on; sector view and galaxy view share zoom level 1.
                if (map.currentQuadrant != quadrant) map.ShowGalaxyMap(quadrant);
            }
            ApplyCamera();
        }

        internal IEnumerator Settle()
        {
            if (zoom == 0 || sector == null) yield break;
            yield return new WaitForSecondsRealtime(1f);
            if (map != null) ApplyCamera();
        }

        private void ApplyCamera()
        {
            map.mapCamera.transform.position = cameraPosition;
            map.mapCamera.orthographicSize = cameraSize;
        }
    }

    // Every fleet of every faction: a fleet that the map labels "Reinforcing" gets the order it really has, and one
    // under 35% is never called a frontline move.
    private static void CheckReinforcing(SelfCheckContext ctx)
    {
        int reinforcing = 0, retreating = 0, frontline = 0, resting = 0, bad = 0;
        string first = null;
        foreach (var fleet in WarMath.Universe.AllFleets())
        {
            bool label = fleet.orderReason == ConquestFleetOrderReason.Reinforcing
                         && (fleet.state == ConquestFleetState.Traveling || fleet.state == ConquestFleetState.Retreating);
            if (!label) continue;
            reinforcing++;
            string order = WarCards.FleetSection(fleet).description;
            bool weak = !fleet.isWarFleet && fleet.totalPoints < fleet.targetPoints * ConquestWarTuning.RetreatThreshold;
            if (order == null) bad++;
            else if (order.StartsWith("Resting")) resting++;
            else if (order.StartsWith("Retreating")) { retreating++; if (!weak) bad++; }
            else if (order.StartsWith("Moving up")) { frontline++; if (weak) bad++; }
            first ??= fleet.displayName + " " + fleet.totalPoints + "/" + fleet.targetPoints + ": " + order;
        }
        string detail = $"{reinforcing} Reinforcing fleets: {resting} resting, {retreating} retreating, {frontline} moving up" + (first != null ? ", e.g. " + first : "");
        if (reinforcing == 0) ctx.Skip("reinforcing", "no fleet shows Reinforcing right now");
        else if (bad == 0) ctx.Pass("reinforcing", detail);
        else ctx.Fail("reinforcing", bad + " wrong; " + detail);
    }

    private static IEnumerator CheckPopout(SelfCheckContext ctx)
    {
        // The check opens the window with the show condition forced on; afterwards the layout goes back to exactly
        // what it was, so a run leaves no window size or position of its own behind.
        const string id = ConquestDetails.PopoutId;
        string pref = PopoutState.Pref(id);
        string before = PlayerPrefs.HasKey(pref) ? PlayerPrefs.GetString(pref) : null;
        bool wasOpen = Popouts.IsOpen(id);
        var state = Popouts.State(id);
        var savedShow = state.show;
        state.show = PopoutShow.Always;
        if (!wasOpen) Popouts.Open(id);
        ctx.Cleanup(() =>
        {
            if (wasOpen)
            {
                state.show = savedShow;
                state.Save(id);
                return;
            }
            Popouts.Close(id);
            if (before == null) PlayerPrefs.DeleteKey(pref);
            else PlayerPrefs.SetString(pref, before);
            Popouts.ForgetState(id);
        });
        yield return new WaitForSecondsRealtime(1.5f);
        var view = WarOverview.Instance;
        if (view == null) { ctx.Fail("popout", "no War Overview after opening it"); yield break; }
        view.Tick();
        int inWar = 0;
        var conquest = WarMath.Conquest;
        foreach (var f in Source.Simulation.Story.Conquest.conquestFactions)
        {
            var economy = WarMath.Universe.PeekFactionEconomy(f);
            if (WarMath.CountOwned(conquest, f) > 0 || economy != null && economy.fleets.Count > 0) inWar++;
        }
        var tracked = view.Tracked();
        int targets = tracked != null ? WarMath.AttackCandidates(tracked, new HashSet<ConquestSystem>()).Count : 0;
        string detail = $"faction rows {view.FactionRowCount} of {inWar}, target rows {view.SystemRowCount} of {targets} for {(tracked != null ? tracked.identifier : "none")} | {view.MissionText}";
        if (view.FactionRowCount == inWar && view.SystemRowCount == targets && view.MissionText.Contains(" holds ")) ctx.Pass("popout", detail);
        else ctx.Fail("popout", detail);
        Canvas.ForceUpdateCanvases();
        string fit = view.TableFitProblem();
        if (fit == null) ctx.Pass("popout-fit", "every header and number in the two tables fits its column");
        else ctx.Fail("popout-fit", fit);
        CheckOverviewAdvice(ctx, view, state);

        // The window has to draw over the galaxy map's own window, or it opens unseen behind it.
        var p = Popouts.Get(ConquestDetails.PopoutId);
        var mapCanvas = AbstractGalaxyMapManager.current.mapWindow != null ? AbstractGalaxyMapManager.current.mapWindow.GetComponentInParent<Canvas>() : null;
        int ours = p != null ? p.GetComponent<Canvas>().sortingOrder : -1, map = mapCanvas != null ? mapCanvas.sortingOrder : -1;
        if (p != null && p.Shown && mapCanvas != null && ours > map) ctx.Pass("popout-over-map", $"pop-out order {ours}, map window {map}");
        else ctx.Fail("popout-over-map", $"pop-out order {ours} shown {p != null && p.Shown}, map window {map}");
        yield return ctx.Screenshot("war-overview");
        if (p != null) yield return CheckWheel(ctx, p);

        // The sector list follows the sector the map shows.
        var savedList = state.options[WarOverview.ListOption];
        state.options[WarOverview.ListOption] = WarOverview.SectorList;
        view.Tick();
        var sector = AbstractGalaxyMapManager.current.viewedSector;
        int inSector = 0;
        if (sector != null && sector.isWarZone)
            foreach (var system in sector.allSystems)
                if (WarMath.Of(system) != null) inSector++;
        string sectorDetail = $"{(sector != null ? sector.name : "no sector")}: {view.SystemRowCount} rows of {inSector} war-zone systems";
        if (inSector == 0) ctx.Skip("popout-sector", "the map shows no war-zone sector; " + sectorDetail);
        else if (view.SystemRowCount == inSector) ctx.Pass("popout-sector", sectorDetail);
        else ctx.Fail("popout-sector", sectorDetail);
        yield return ctx.Screenshot("war-overview-sector");
        state.options[WarOverview.ListOption] = savedList;
        view.Tick();
    }

    // With click-through on, a wheel turn over the body belongs to the window: in that frame the top raycast hit is
    // the window (so the galaxy map's own "is the top hit the map" test says no), the map doesn't zoom and the list
    // scrolls. Without a wheel turn the body lets clicks through to whatever is under it. The check moves the cursor
    // onto the body and queues a real wheel event; the window is made short enough for its list to scroll.
    private static IEnumerator CheckWheel(SelfCheckContext ctx, Popout p)
    {
        var mouse = Mouse.current;
        var map = AbstractGalaxyMapManager.current;
        var camera = map != null && map.mapCamera != null ? map.mapCamera.GetComponent<GalaxyMapCameraMovement>() : null;
        if (mouse == null || camera == null || !p.Shown) { ctx.Skip("popout-wheel", $"mouse {mouse != null}, map camera {camera != null}, shown {p.Shown}"); yield break; }
        bool savedClickThrough = p.State.clickThrough;
        float shrink = p.State.h - p.Kind.MinSize.y - 40f;
        p.State.clickThrough = true;
        if (shrink > 0f) p.ResizeBy(new Vector2(0f, shrink));
        try
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            var body = p.Body;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, body.TransformPoint(body.rect.center));
            Vector2 outside = new(center.x, Mathf.Max(2f, RectTransformUtility.WorldToScreenPoint(null, body.TransformPoint(body.rect.min)).y - 40f));
            bool claimsIn = p.ClaimsWheelAt(center), claimsOut = p.ClaimsWheelAt(outside);

            mouse.WarpCursorPosition(center);
            yield return null;
            yield return null;
            Vector2 cursor = Input.mousePosition;
            if ((cursor - center).sqrMagnitude > 4f)
            {
                ctx.Skip("popout-wheel", $"the cursor didn't move: asked {center}, at {cursor}; claims in {claimsIn}, out {claimsOut}");
                yield break;
            }
            bool passesIdle = p.ClicksPass;
            var idleHit = TopHit(center);
            bool idleOnWindow = idleHit != null && idleHit.transform.IsChildOf(p.transform);

            var scroll = FindScroll(body);
            float scrollBefore = scroll != null ? scroll.verticalNormalizedPosition : -1f;
            float zoomBefore = map.mapCamera.orthographicSize;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = center, scroll = new Vector2(0f, -120f) });
            yield return null;
            bool wheel = Popout.WheelMoved(), passesWheel = p.ClicksPass, mapWheel = camera.IsMouseOverMap();
            var wheelHit = TopHit(center);
            bool wheelOnWindow = wheelHit != null && wheelHit.transform.IsChildOf(p.transform);
            for (int i = 0; i < 10; i++) yield return null;
            float zoomAfter = map.mapCamera.orthographicSize;
            float scrollAfter = scroll != null ? scroll.verticalNormalizedPosition : -1f;
            bool scrollable = scroll != null && scroll.content.rect.height > scroll.viewport.rect.height + 1f;

            string module = EventSystem.current != null && EventSystem.current.currentInputModule != null ? EventSystem.current.currentInputModule.GetType().Name : "none";
            string detail = $"claims in {claimsIn} out {claimsOut} | idle: clicks pass {passesIdle}, top hit {Name(idleHit)} | wheel {wheel}: clicks pass {passesWheel}, top hit {Name(wheelHit)}, map test {mapWheel}, zoom {zoomBefore:0.##} -> {zoomAfter:0.##}, list {scrollBefore:0.###} -> {scrollAfter:0.###} (scrollable {scrollable}, {module})";
            if (!wheel) ctx.Skip("popout-wheel", "the queued wheel event didn't arrive; " + detail);
            else if (claimsIn && !claimsOut && passesIdle && !idleOnWindow && !passesWheel && wheelOnWindow && !mapWheel
                && Mathf.Approximately(zoomBefore, zoomAfter) && scrollable && !Mathf.Approximately(scrollBefore, scrollAfter))
                ctx.Pass("popout-wheel", detail);
            else ctx.Fail("popout-wheel", detail);
            if (scroll != null) scroll.verticalNormalizedPosition = scrollBefore;
        }
        finally
        {
            p.State.clickThrough = savedClickThrough;
            if (shrink > 0f) p.ResizeBy(new Vector2(0f, -shrink));
        }
    }

    private static readonly List<RaycastResult> hits = new();

    private static GameObject TopHit(Vector2 point)
    {
        hits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        return hits.Count > 0 ? hits[0].gameObject : null;
    }

    private static string Name(GameObject go) => go == null ? "none" : go.name + " (layer " + go.layer + ")";

    private static ScrollRect FindScroll(RectTransform body)
    {
        foreach (var s in body.GetComponentsInChildren<ScrollRect>())
            if (s.vertical) return s;
        return null;
    }

    private static IEnumerator CheckFactionOverview(SelfCheckContext ctx)
    {
        EmpireWindow.Open();
        yield return new WaitForSecondsRealtime(1f);
        var window = EmpireWindow.activeInstance;
        if (window == null) { ctx.Skip("faction-overview", "the Empire window didn't open (locked in this save?)"); yield break; }
        ctx.Cleanup(EmpireWindow.Close);
        window.OpenTab(Source.Player.CommandTab.Factions);
        var faction = WarMath.MissionFaction() ?? Faction.red;
        window.SelectFaction(faction);
        yield return new WaitForSecondsRealtime(1.5f);
        var content = window.activeFactionDetail;
        var labels = new List<string>();
        if (content != null)
            foreach (var row in content.conquestRows)
                labels.Add(row.label.text + "=" + row.value.text);
        bool rows = labels.Exists(l => l.StartsWith("Systems held now=")) && labels.Exists(l => l.StartsWith("Area Conquered refreshes in="));
        // The game adds its own War Footing rows once the player has helped the faction (FactionOverviewContent.AddWarRows);
        // the feature's rows must come after every one of them.
        var standing = WarMath.Conquest?.GetFactionStanding(faction);
        string footingLabel = Source.Util.Translation.Translate("@FactionOverviewWarFooting") + "=";
        bool gameWarRows = labels.Exists(l => l.StartsWith(footingLabel));
        int mine = labels.FindIndex(l => l.StartsWith("Systems held now="));
        int lastGame = labels.FindLastIndex(l => l.StartsWith(Source.Util.Translation.Translate("@FactionOverviewContribution") + "=")
            || l.StartsWith(Source.Util.Translation.Translate("@FactionOverviewWarShipsDestroyed") + "="));
        bool placed = lastGame >= 0 && mine > lastGame && standing != null && gameWarRows == standing.hasHelpedInWar;
        rows &= placed;
        bool ledger = content != null && content.shipyardCard.activeSelf && content.shipyardRows.Count > 0;
        string detail = $"{faction.identifier}: {string.Join("; ", labels)} | ledger card {(ledger ? content.shipyardRows.Count + " rows" : "hidden")}";
        if (rows && ledger) ctx.Pass("faction-overview", detail);
        else ctx.Fail("faction-overview", detail);
        // The added rows and the ledger card sit below the fold of the detail's scroll view.
        UnityEngine.UI.ScrollRect scroll = null;
        if (content != null)
        {
            scroll = content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scroll == null) scroll = content.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
        }
        if (scroll != null)
        {
            // Scroll so the conquest section's top sits at the top of the view.
            Canvas.ForceUpdateCanvases();
            var c = scroll.content;
            var section = (RectTransform)content.conquestSection.transform;
            float offset = (c.TransformPoint(new Vector3(0f, c.rect.yMax, 0f)).y - section.TransformPoint(new Vector3(0f, section.rect.yMax, 0f)).y) / c.lossyScale.y;
            var view = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            float range = c.rect.height - view.rect.height;
            scroll.verticalNormalizedPosition = range > 0f ? Mathf.Clamp01(1f - (offset - 4f) / range) : 1f;
            yield return ctx.Screenshot("faction-overview-conquest");
            scroll.verticalNormalizedPosition = 0f;
            yield return ctx.Screenshot("faction-overview-ledger");
        }
        else yield return ctx.Screenshot("faction-overview");
        EmpireWindow.Close();
    }

    // A Defense station's card reports the garrison the Defense breakdown counts, and a hub's card its live HP
    // multiplier (x30 at a headquarters). Then the cards are drawn by the game's tooltip for the screenshots: the
    // system with the most going on, its Defense station or hub, and a fleet.
    private static IEnumerator ShowCards(SelfCheckContext ctx, Source.Simulation.Story.Conquest conquest)
    {
        ConquestSystem pick = null;
        int most = -1;
        var d = new DefenseParts();
        Source.Galaxy.POI.FactionStation defense = null;
        ConquestSystem hq = null;
        foreach (var cs in conquest.systems)
        {
            if (cs.isUnowned) continue;
            if (cs.headquarters && cs.station != null && hq == null) hq = cs;
            if (defense == null)
                foreach (var st in ConquestCombat.GetDefenseStations(cs)) { defense = st; break; }
            if (cs.cannotBeTaken) continue;
            d.Read(cs);
            int score = d.DefenseStations * 2 + d.FleetCount;
            if (score > most) { most = score; pick = cs; }
        }
        if (defense != null)
        {
            var s = Find(PoiTooltipCardBuilder.Build(defense), WarCards.StationTitle);
            string want = defense.garrisonShips.Count + " of " + Source.Spacestation.FactionGarrison.GetWantedCount(defense) + " ships";
            string got = s != null ? Value(s, "Garrison") : null;
            if (got == want) ctx.Pass("station-card", defense.name + ": garrison " + got);
            else ctx.Fail("station-card", defense.name + ": garrison '" + got + "', expected '" + want + "'");
        }
        else ctx.Skip("station-card", "no Defense station in the war zone");
        if (hq != null)
        {
            var s = Find(PoiTooltipCardBuilder.Build(hq.station), WarCards.StationTitle);
            string hp = s != null ? Value(s, WarCards.HitPointsLabel) : null;
            if (hp == "x30") ctx.Pass("hq-hub-card", hq.system.name + ": " + hp);
            else ctx.Fail("hq-hub-card", hq.system.name + ": '" + hp + "'");
        }

        yield return CheckCardLayout(ctx, conquest, pick);
        yield return CheckKeyRefresh(ctx, pick);
        yield return CheckClamp(ctx, pick);
        if (!DevFeature.Instance.Screenshot.Value || HudManager.Instance == null || pick == null) yield break;
        // The cards are see-through; the War overview behind them would show through, so it's hidden for the pictures.
        var overview = Popouts.Get(ConquestDetails.PopoutId);
        bool overviewShown = overview != null && overview.gameObject.activeSelf;
        if (overviewShown) overview.gameObject.SetActive(false);
        try
        {
            yield return Show(ctx, "system-card", () => Summary(pick));
            yield return Show(ctx, "system-card-full", () => Full(pick));
            var station = defense != null ? (Source.Galaxy.POI.SpaceStation)defense : pick.station;
            if (station != null) yield return Show(ctx, "station-card", () => PoiTooltipCardBuilder.Build(station));
            // A hub whose kill hands the system to someone, best to the faction the verdicts are for while the owner is
            // your enemy, and that system's full card with the verdict's reasons.
            var tracked = WarMath.AutoTracked();
            ConquestSystem hubKill = null;
            int best = -1;
            foreach (var cs in conquest.systems)
            {
                if (cs.station == null) continue;
                var h = HubKill.Predict(cs);
                if (h.Result != HubKillResult.Claims) continue;
                int score = (h.Claimer == tracked ? 2 : 0) + (cs.faction != null && cs.faction.IsEnemy(Faction.player) ? 1 : 0);
                if (score > best) (best, hubKill) = (score, cs);
            }
            if (hubKill != null)
            {
                yield return Show(ctx, "hub-card", () => PoiTooltipCardBuilder.Build(hubKill.station));
                yield return Show(ctx, "system-card-hub-kill-short", () => Summary(hubKill));
                yield return Show(ctx, "system-card-hub-kill", () => Full(hubKill));
            }
            foreach (var icon in Object.FindObjectsByType<ConquestFleetMapIcon>(FindObjectsInactive.Exclude))
            {
                if (icon.fleet == null) continue;
                var shown = icon;
                yield return Show(ctx, "fleet-card", () => (TooltipCard)FleetCard.Invoke(shown, null));
                break;
            }
        }
        finally
        {
            if (overviewShown && overview != null) overview.gameObject.SetActive(true);
        }
    }

    private static IEnumerator Show(SelfCheckContext ctx, string name, System.Func<TooltipCard> build)
    {
        var go = new GameObject("IgiQoL check card", typeof(RectTransform));
        go.transform.SetParent(HudManager.Instance.rootCanvasTransform, false);
        var source = go.AddComponent<TooltipSource>();
        go.AddComponent<CardProbe>().Build = build;
        UITooltip.Show(source, source.Prefab);
        // The tooltip follows the mouse, which the harness doesn't move; pin it fully on screen for the picture.
        var shown = UITooltip._current;
        if (shown != null)
        {
            shown.enabled = false;
            var rt = (RectTransform)shown.transform;
            rt.pivot = new Vector2(0f, 1f);
            rt.position = new Vector3(Screen.width * 0.02f, Screen.height * 0.96f, 0f);
        }
        yield return ctx.Screenshot(name);
        UITooltip.Hide(source);
        Object.Destroy(go);
    }

    private static string Value(TooltipSection s, string label)
    {
        foreach (var r in s.rows)
            if (r.label == label) return r.value;
        return null;
    }

    private sealed class CardProbe : MonoBehaviour, ITooltipCardSource
    {
        public System.Func<TooltipCard> Build;
        public TooltipCard BuildTooltipCard() => Build();
    }

    private static TooltipSection Find(TooltipCard card, string title)
    {
        foreach (var s in card.sections)
            if (s.title == title) return s;
        return null;
    }
}
