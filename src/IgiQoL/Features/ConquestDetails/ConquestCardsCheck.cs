using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Behaviour;
using Behaviour.GalaxyMap;
using Behaviour.UI;
using Behaviour.UI.HUD;
using Behaviour.UI.Tooltip;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Features.Window;
using IgiQoL.UI;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.Simulation.Story;
using Source.Simulation.World.System;
using Source.UI.Tooltip;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.ConquestDetails;

// The checks for what the cards and the corner widget look like once drawn, and for the advice behind the verdicts.
internal static partial class ConquestDetailsCheck
{
    private static readonly Regex Clock = new(@"^\d+:\d\d:\d\d$");
    private static readonly System.Reflection.MethodInfo DeliveryCard = AccessTools.Method(typeof(WarshipDeliveryMapIcon), "DescribeTooltip");

    // ---- the verdicts ----

    // Every verdict, rederived from the game's own numbers by plain arithmetic: the fleets an attack needs, what killing
    // the defending fleets leaves, and what the full war effort penalty leaves with the game's own Defense function run
    // on a system whose penalty is set by hand (and put back in a finally).
    private static void CheckAdvice(SelfCheckContext ctx, Conquest conquest)
    {
        var tracked = WarMath.AutoTracked();
        if (tracked == null) { ctx.Skip("advice", "no faction to give advice for"); return; }
        var reach = new Reach();
        reach.Build(tracked);
        var d = new DefenseParts();
        var counts = new Dictionary<AdviceKind, int>();
        int bad = 0, restored = 0, formula = 0;
        string first = null;
        foreach (var cs in conquest.systems)
        {
            d.Read(cs);
            float penalty = cs.warEffortPenalty;
            var a = WarAdvice.Compute(d, tracked, reach);
            counts[a.Kind] = counts.TryGetValue(a.Kind, out int n) ? n + 1 : 1;
            if (cs.warEffortPenalty != penalty) { bad++; first ??= cs.system.name + ": war effort penalty not put back"; continue; }
            restored++;
            string why = ExpectAdvice(cs, d, a, tracked, reach, ref formula);
            if (why != null) { bad++; first ??= cs.system.name + ": " + why; }
        }
        var parts = new List<string>();
        foreach (var pair in counts) parts.Add(pair.Key + " " + pair.Value);
        string detail = $"{restored} systems for {tracked.identifier}: {string.Join(", ", parts)}; {formula} war effort what-ifs matched the closed formula" + (first != null ? "; first mismatch " + first : "");
        if (bad == 0) ctx.Pass("advice", detail);
        else ctx.Fail("advice", detail);
    }

    private static string ExpectAdvice(ConquestSystem cs, DefenseParts d, Advice a, Faction tracked, Reach reach, ref int formula)
    {
        AdviceKind expected;
        if (cs.cannotBeTaken) expected = AdviceKind.CannotTake;
        else if (cs.faction == tracked) expected = AdviceKind.Own;
        else if (BattleAt(cs)) expected = AdviceKind.Battle;
        else if (cs.isUnowned) expected = AdviceKind.Free;
        else if (!reach.Contains(cs)) expected = AdviceKind.NotOnFront;
        else if (!reach.Allowed.Contains(cs)) expected = AdviceKind.Skipped;
        else if (WarMath.Incoming(tracked, cs)) expected = AdviceKind.OnTheWay;
        else
        {
            double next = ConquestCombat.GetNextBattleStrength(cs);
            int fleets = Math.Max(1, (int)Math.Ceiling(next * 1.15 / 2000.0 - 0.0001));
            if (a.Fleets != fleets) return $"needs {a.Fleets} fleets, arithmetic says {fleets} (next battle {next:0})";
            if (fleets == 1)
            {
                if (!WarMath.CanEstimate(d)) expected = AdviceKind.LeaveStart;
                else if (WarMath.EstimateHubSiege(d, 2000f).Result == SiegeResult.Captured) expected = AdviceKind.LeaveIt;
                else
                {
                    var cut = EstimateWithPenalty(cs, 0.5f);
                    expected = cs.warEffortPenalty < 0.499f && cut.Result == SiegeResult.Captured ? AdviceKind.WarEffort : AdviceKind.FallsShort;
                }
            }
            else
            {
                int kill = d.FleetCount > 0 ? Math.Max(1, (int)Math.Ceiling((next - d.Fleets) * 1.15 / 2000.0 - 0.0001)) : fleets;
                int effort = fleets;
                if (cs.warEffortPenalty < 0.499f) effort = Math.Max(1, (int)Math.Ceiling(NextWithPenalty(cs, 0.5f) * 1.15 / 2000.0 - 0.0001));
                expected = kill < fleets && kill <= effort ? AdviceKind.KillFleet : effort < fleets ? AdviceKind.WarEffort : AdviceKind.Skip;
                if (expected == AdviceKind.KillFleet && a.FleetsAfter != kill) return $"killing the fleets leaves {kill}, advice says {a.FleetsAfter}";
                if (expected == AdviceKind.WarEffort)
                {
                    if (a.FleetsAfter != effort) return $"full penalty leaves {effort} fleets, advice says {a.FleetsAfter}";
                    string err = CheckMissions(cs, d, a, ref formula);
                    if (err != null) return err;
                }
            }
        }
        // A hub kill replaces a verdict with no lever, or a running hub siege, only when it hands the system to the faction
        // and you can shoot the hub. The planner check (hub-kill-planner) tests the prediction itself.
        if (a.Kind == AdviceKind.KillHub && expected is AdviceKind.Skip or AdviceKind.FallsShort or AdviceKind.Skipped or AdviceKind.Battle)
        {
            if (a.Hub.Result != HubKillResult.Claims || a.Hub.Claimer != tracked) return $"kill-hub verdict, but the hub kill goes to {a.Hub.Claimer?.identifier ?? "nobody"} ({a.Hub.Result})";
            if (!cs.faction.IsEnemy(Faction.player)) return "kill-hub verdict, but the owner isn't your enemy";
            if (expected == AdviceKind.Battle && !a.Hub.Siege) return "kill-hub verdict on a running battle that isn't at the hub";
            expected = AdviceKind.KillHub;
        }
        return a.Kind == expected ? null : $"expected {expected}, got {a.Kind}";
    }

    // The mission count is the fewest that reach the fleet count, and the next battle after them equals both the game's
    // own function and the closed formula for a system with no Defense station: the station term is swapped for the one
    // with the new war effort multiplier.
    private static string CheckMissions(ConquestSystem cs, DefenseParts d, Advice a, ref int formula)
    {
        if (a.Missions < 1 || a.Missions > 5) return "missions " + a.Missions;
        float applied = Mathf.Min(0.5f, cs.warEffortPenalty + 0.1f * a.Missions);
        float game = NextWithPenalty(cs, applied);
        if (Mathf.Abs(game - a.NextAfter) > 0.5f) return $"next battle after {a.Missions} missions {a.NextAfter:0}, game says {game:0}";
        if (a.Missions > 1)
        {
            float fewer = NextWithPenalty(cs, Mathf.Min(0.5f, cs.warEffortPenalty + 0.1f * (a.Missions - 1)));
            if (Mathf.Max(1, Mathf.CeilToInt(fewer * 1.15f / 2000f - 0.0001f)) <= a.FleetsAfter) return $"{a.Missions - 1} missions already reach {a.FleetsAfter} fleets";
        }
        if (d.DefenseStations == 0)
        {
            float mult = Mathf.Max(0f, 1f + cs.warEffortBonus - applied);
            float term = Mathf.Max(250f * d.Multiplier, Mathf.Min(d.Firepower, 700f * d.Multiplier) * d.Structure) * mult + d.Flat;
            float closed = d.NextBattle - d.StationTerm + term;
            if (Mathf.Abs(closed - a.NextAfter) > 0.5f) return $"closed formula {closed:0}, advice {a.NextAfter:0}";
            formula++;
        }
        return null;
    }

    private static bool BattleAt(ConquestSystem cs)
    {
        foreach (var battle in WarMath.Universe.battleManager.activeBattles)
            if (battle.targetSystemGuid == cs.system.guid) return true;
        return false;
    }

    // The game's next battle number with the system's penalty set by hand; the field is put back.
    private static float NextWithPenalty(ConquestSystem cs, float penalty)
    {
        float saved = cs.warEffortPenalty;
        try
        {
            cs.warEffortPenalty = penalty;
            return ConquestCombat.GetNextBattleStrength(cs);
        }
        finally { cs.warEffortPenalty = saved; }
    }

    // A hub siege estimate with every figure read again by the game's own functions under the hand-set penalty.
    private static SiegeEstimate EstimateWithPenalty(ConquestSystem cs, float penalty)
    {
        float saved = cs.warEffortPenalty;
        try
        {
            cs.warEffortPenalty = penalty;
            var d = new DefenseParts();
            d.Read(cs);
            return WarMath.EstimateHubSiege(d, 2000f);
        }
        finally { cs.warEffortPenalty = saved; }
    }

    // The War overview's block and table follow the verdicts: help first in the default order, the next step names a
    // system that helps, and the clock line has the three timers in H:MM:SS.
    private static void CheckOverviewAdvice(SelfCheckContext ctx, WarOverview view, PopoutState state)
    {
        string clock = view.ClockText;
        bool clockOk = Regex.IsMatch(clock, @"^Conquest Update \d+:\d\d:\d\d \| Fleet orders \d+:\d\d:\d\d \| Economy tick \d+:\d\d:\d\d");
        if (clockOk) ctx.Pass("popout-clock", clock);
        else ctx.Fail("popout-clock", clock);

        var savedList = state.options[WarOverview.ListOption];
        var savedSort = state.options[WarOverview.SortOption];
        try
        {
            state.options[WarOverview.ListOption] = WarOverview.TargetsList;
            state.options[WarOverview.SortOption] = WarOverview.HelpSort;
            view.Tick();
            int lastRank = -1, listed = 0, helps = 0;
            bool ordered = true;
            string firstHelp = null;
            foreach (var cs in view.Listed)
            {
                if (!view.TryAdvice(cs, out var a)) { ordered = false; continue; }
                listed++;
                if (a.Rank < lastRank) ordered = false;
                lastRank = a.Rank;
                if (a.Helps) { helps++; firstHelp ??= cs.system.name; }
            }
            string next = view.NextStepText;
            bool nextOk = helps > 0 ? next.StartsWith("Do this next: ") : next.StartsWith("Nothing");
            string detail = $"{listed} targets, {helps} to help (first {firstHelp ?? "none"}), sorted {ordered} | {next}";
            if (listed == 0) ctx.Skip("popout-advice", "the tracked faction has no targets right now");
            else if (ordered && nextOk) ctx.Pass("popout-advice", detail);
            else ctx.Fail("popout-advice", detail);
        }
        finally
        {
            state.options[WarOverview.ListOption] = savedList;
            state.options[WarOverview.SortOption] = savedSort;
            view.Tick();
        }
    }

    // ---- the corner widget ----

    // The two clocks are rows under the bar in the game's H:MM:SS, and nothing in the card runs past its edge, at the
    // size set now and at 130%.
    private static IEnumerator CheckCorner(SelfCheckContext ctx)
    {
        var extras = Object.FindAnyObjectByType<MapCornerExtras>();
        if (extras == null || extras.ordersValue == null) { ctx.Fail("corner-timers", "no corner rows"); yield break; }
        string o = extras.ordersValue.text, e = extras.economyValue.text;
        var info = extras.GetComponentInChildren<Behaviour.UI.Tooltip.TooltipSource>(true);
        bool names = extras.ordersName.text == MapCornerExtras.OrdersLabel && extras.economyName.text == MapCornerExtras.EconomyLabel;
        bool clocks = Clock.IsMatch(o) && Clock.IsMatch(e);
        bool inTooltip = info != null && info.BodyText != null && info.BodyText.Contains("Fleet orders");
        string detail = $"{extras.ordersName.text} {o} | {extras.economyName.text} {e}; (i) tooltip names the clocks {inTooltip}";
        if (names && clocks && inTooltip) ctx.Pass("corner-timers", detail);
        else ctx.Fail("corner-timers", detail);

        string now = CornerFit(extras);
        if (now == null) ctx.Pass("corner-fit", "at the current text size every label and clock fits its row, inside the card");
        else ctx.Fail("corner-fit", now);

        var scale = TextScale.TextScale.Instance;
        if (scale == null || !scale.Active) { ctx.Skip("corner-fit-130", "the text size feature is off"); yield break; }
        int saved = scale.Percent.Value;
        ctx.Cleanup(() => { scale.Percent.Value = saved; });
        string big;
        try
        {
            scale.Percent.Value = 130;
            yield return null;
            yield return null;
            big = CornerFit(extras);
        }
        finally { scale.Percent.Value = saved; }
        yield return null;
        yield return null;
        if (big == null) ctx.Pass("corner-fit-130", "at 130% text size every label and clock fits its row, inside the card");
        else ctx.Fail("corner-fit-130", big);
    }

    // Null when every text of the Conquest Update card fits the width it is given and the card keeps the rows inside.
    private static string CornerFit(MapCornerExtras extras)
    {
        var panel = extras.GetComponent<Behavior.UI.GalaxyMap.MapCornerPanel>();
        var card = (RectTransform)panel.conquestCard.transform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        string bad = null;
        foreach (var t in card.GetComponentsInChildren<TMP_Text>(true))
        {
            if (string.IsNullOrEmpty(t.text) || t.textWrappingMode != TextWrappingModes.NoWrap) continue;
            // A label that shrinks to fit is measured as drawn; preferredWidth is its width at the largest size.
            if (t.enableAutoSizing) t.ForceMeshUpdate();
            float need = t.enableAutoSizing ? t.textBounds.size.x : t.preferredWidth, room = t.rectTransform.rect.width;
            bool cut = t.enableAutoSizing && t.isTextOverflowing;
            if (need > room + 0.5f || cut) bad ??= $"'{t.text}' needs {need:0} but has {room:0} in a card {card.rect.width:0} wide{(t.enableAutoSizing ? $" (shrunk to {t.fontSize:0.#} pt, cut {cut})" : "")}";
        }
        foreach (var row in new[] { extras.ordersValue, extras.economyValue })
        {
            var rt = (RectTransform)row.transform.parent;
            float right = card.InverseTransformPoint(rt.TransformPoint(new Vector3(rt.rect.xMax, 0f, 0f))).x;
            if (right > card.rect.xMax + 0.5f) bad ??= $"a clock row ends at {right:0}, past the card edge {card.rect.xMax:0}";
        }
        return bad;
    }

    // ---- the drawn cards ----

    private static float TooltipScale => UITooltip._rootCanvas != null ? UITooltip._rootCanvas.scaleFactor : 1f;

    // Draws a card with the game's tooltip, hands it to the measure, then takes it down.
    private static void Draw(System.Func<TooltipCard> build, System.Action<UITooltip, TooltipCard> measure)
    {
        var go = new GameObject("IgiQoL check card", typeof(RectTransform));
        go.transform.SetParent(HudManager.Instance.rootCanvasTransform, false);
        var source = go.AddComponent<TooltipSource>();
        TooltipCard built = null;
        go.AddComponent<CardProbe>().Build = () => built = build();
        try
        {
            UITooltip.Show(source, source.Prefab);
            var shown = UITooltip._current;
            if (shown != null && built != null) measure(shown, built);
        }
        finally
        {
            UITooltip.Hide(source);
            Object.Destroy(go);
        }
    }

    // No row the mod adds is wider than the slot the game gives its text, on the system card (short and full), the fleet,
    // delivery and station cards; and a short system card is at most half the screen tall.
    private static IEnumerator CheckCardLayout(SelfCheckContext ctx, Conquest conquest, ConquestSystem pick)
    {
        if (HudManager.Instance == null || !UITooltip.TooltipEnabled || UITooltip.tooltipParent == null || UITooltip._rootCanvas == null)
        {
            ctx.Skip("card-fit", "no tooltip layer to draw cards in");
            yield break;
        }
        var titles = new HashSet<string> { WarCards.SummaryTitle, WarCards.DefenseTitle, WarCards.TakingTitle, WarCards.BattleTitle, WarCards.FleetTitle, WarCards.StationTitle };
        var offenders = new List<string>();
        int cards = 0, rows = 0, systems = 0, tooTall = 0;
        float tallest = 0f;
        string tallestName = null;
        float limit = Screen.height * 0.5f, scale = TooltipScale;

        void Measure(string name, UITooltip shown, TooltipCard card)
        {
            cards++;
            var mine = new HashSet<string>();
            foreach (var s in card.sections)
            {
                if (!titles.Contains(s.title)) continue;
                foreach (var r in s.rows)
                {
                    mine.Add(r.label);
                    mine.Add(r.value);
                    if (r.badge != null) mine.Add(r.badge);
                }
            }
            foreach (var t in shown.Content.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.overflowMode != TextOverflowModes.Truncate || !mine.Contains(t.text)) continue;
                rows++;
                float need = t.preferredWidth, room = t.rectTransform.rect.width;
                if (need > room + 0.5f) offenders.Add($"{name}: '{t.text}' needs {need:0} has {room:0}");
            }
        }

        foreach (var cs in conquest.systems)
        {
            if (systems++ >= 60) break;
            string name = cs.system.name;
            Draw(() => Summary(cs), (shown, card) =>
            {
                Measure(name + " (short)", shown, card);
                float height = shown.SizeY * scale;
                if (height > tallest) { tallest = height; tallestName = name; }
                if (height > limit) tooTall++;
            });
            Draw(() => Full(cs), (shown, card) => Measure(name + " (full)", shown, card));
        }
        int stations = 0;
        foreach (var cs in conquest.systems)
        {
            if (stations >= 40) break;
            foreach (var st in cs.system.GetPointsOfInterest<SpaceStation>())
            {
                if (stations++ >= 40) break;
                var station = st;
                Draw(() => PoiTooltipCardBuilder.Build(station), (shown, card) => Measure(station.name, shown, card));
            }
        }
        foreach (var icon in Object.FindObjectsByType<ConquestFleetMapIcon>(FindObjectsInactive.Exclude))
        {
            if (icon.fleet == null) continue;
            var shownIcon = icon;
            Draw(() => (TooltipCard)FleetCard.Invoke(shownIcon, null), (shown, card) => Measure("fleet " + shownIcon.fleet.displayName, shown, card));
        }
        string deliveryDetail = "no delivery icon on the map";
        bool deliveryOk = true;
        int deliveries = 0;
        foreach (var icon in Object.FindObjectsByType<WarshipDeliveryMapIcon>(FindObjectsInactive.Exclude))
        {
            deliveries++;
            var shownIcon = icon;
            Draw(() => (TooltipCard)DeliveryCard.Invoke(shownIcon, null), (shown, card) =>
            {
                Measure("delivery", shown, card);
                bool note = card.notes.Exists(n => n.text == WarCards.DeliveryNote);
                bool noRow = !card.rows.Exists(r => r.label == "What it is");
                if (!note || !noRow) deliveryOk = false;
                deliveryDetail = $"delivery card note {note}, old row gone {noRow}";
            });
        }

        string detail = $"{cards} cards, {rows} mod rows measured" + (offenders.Count > 0 ? "; too wide: " + string.Join("; ", offenders.GetRange(0, Math.Min(6, offenders.Count))) + (offenders.Count > 6 ? $" (+{offenders.Count - 6} more)" : "") : "");
        if (offenders.Count == 0 && rows > 0) ctx.Pass("card-fit", detail);
        else ctx.Fail("card-fit", detail);
        if (deliveries == 0) ctx.Skip("delivery-card", deliveryDetail);
        else if (deliveryOk) ctx.Pass("delivery-card", deliveryDetail);
        else ctx.Fail("delivery-card", deliveryDetail);

        string heightDetail = $"tallest short card {tallest:0} px of {Screen.height} ({tallestName}); {tooTall} of {Mathf.Min(systems, 60)} over half the screen at scale {scale:0.##}";
        if (tallestName == null) ctx.Skip("summary-height", "no system card was drawn");
        else if (tooTall == 0) ctx.Pass("summary-height", heightDetail);
        else ctx.Fail("summary-height", heightDetail);
        yield return null;
    }

    // ---- the key ----

    // Holding the detail key while a short card shows rebuilds it with the breakdown, and letting go takes it away.
    private static IEnumerator CheckKeyRefresh(SelfCheckContext ctx, ConquestSystem pick)
    {
        var kb = Keyboard.current;
        var f = ConquestDetails.Instance;
        if (pick == null || kb == null || HudManager.Instance == null || !UITooltip.TooltipEnabled) { ctx.Skip("detail-key", "no system, keyboard or tooltip layer"); yield break; }
        var key = f.DetailKey.Value;
        var savedMode = f.CardDetail.Value;
        f.CardDetail.Value = SystemCardDetail.Summary;
        var go = new GameObject("IgiQoL check card", typeof(RectTransform));
        go.transform.SetParent(HudManager.Instance.rootCanvasTransform, false);
        var source = go.AddComponent<TooltipSource>();
        go.AddComponent<CardProbe>().Build = () => MapTooltipCardBuilder.BuildSystem(pick.system);
        bool before = false, during = false, after = false, pressed = false, hintBefore = false;
        // Without focus (a run started while you work in another window) the Input System drops keyboard events by
        // default, so the check asks it to take them for its own frames and puts the setting back.
        var settings = InputSystem.settings;
        var background = settings.backgroundBehavior;
        bool focused = Application.isFocused, enabledBefore = kb.enabled;
        try
        {
            if (!focused) settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            if (!kb.enabled) InputSystem.EnableDevice(kb);
            UITooltip.Show(source, source.Prefab);
            yield return null;
            before = ShowsBreakdown(); hintBefore = ShowsText("for the full breakdown");
            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            yield return null;
            yield return null;
            pressed = HeldKey.IsDown(key);
            during = ShowsBreakdown();
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
            yield return null;
            after = ShowsBreakdown();
        }
        finally
        {
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            settings.backgroundBehavior = background;
            f.CardDetail.Value = savedMode;
            UITooltip.Hide(source);
            Object.Destroy(go);
        }
        string detail = $"{pick.system.name}, key {key}: short card hint {hintBefore}, breakdown before {before}, held {during}, after letting go {after}; window focused {focused}, keyboard enabled {enabledBefore}";
        if (!pressed) ctx.Skip("detail-key", "the queued key press didn't register; " + detail);
        else if (!before && hintBefore && during && !after) ctx.Pass("detail-key", detail);
        else ctx.Fail("detail-key", detail);
    }

    private static bool ShowsBreakdown() => ShowsText(WarCards.DefenseTitle);

    private static bool ShowsText(string text)
    {
        var shown = UITooltip._current;
        if (shown == null) return false;
        foreach (var t in shown.Content.GetComponentsInChildren<TMP_Text>(true))
            if (t.text != null && t.text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    // ---- the clamp ----

    // A card that runs off the screen ends up inside it, still beside the mouse. The game's own placement is read first
    // (the setting off) to show the card did overflow, so the check can't pass on a card that never needed the clamp.
    private static IEnumerator CheckClamp(SelfCheckContext ctx, ConquestSystem pick)
    {
        var window = WindowFeature.Instance;
        if (window == null || !window.Active || HudManager.Instance == null || !UITooltip.TooltipEnabled || UITooltip._rootCanvas == null)
        {
            ctx.Skip("tooltip-clamp", "no tooltip layer or the mod window feature is off");
            yield break;
        }
        float scale = TooltipScale, height = Screen.height / scale, width = Screen.width / scale;
        int rowsNeeded = Mathf.CeilToInt(height * 0.8f / 22f);
        string tall = Probe("tall card", () =>
        {
            var card = new TooltipCard { title = "Keep on screen" };
            for (int i = 1; i <= rowsNeeded; i++) card.AddRow("Row " + i, "value " + i);
            return card;
        }, 0.5f, 0.4f, true);
        string system = pick != null ? Probe("full system card", () => Full(pick), 0.6f, 0.3f, false) : null;
        bool ok = tall != null && tall.StartsWith("ok") && (system == null || system.StartsWith("ok"));
        string detail = $"tall card: {tall}; system card: {system ?? "none"}; screen {width:0}x{height:0} units";
        if (tall == null) ctx.Skip("tooltip-clamp", "the tooltip didn't show");
        else if (ok) ctx.Pass("tooltip-clamp", detail);
        else ctx.Fail("tooltip-clamp", detail);
        yield return null;
    }

    // Shows the card with the mouse at a fraction of the screen, moves it by the game's own Update with the clamp off and
    // on, and reports the edges in canvas units.
    private static string Probe(string name, System.Func<TooltipCard> build, float mouseY, float mouseX, bool mustOverflow)
    {
        var window = WindowFeature.Instance;
        bool savedSetting = window.KeepCardsOnScreen.Value;
        var savedMouse = GlobalControls.mousePosition;
        string result = null;
        Draw(build, (shown, card) =>
        {
            try
            {
                float scale = TooltipScale, height = Screen.height / scale;
                GlobalControls.mousePosition = new Vector2(Screen.width * mouseX, Screen.height * mouseY);
                float cursorX = GlobalControls.mousePosition.x / scale;
                shown.enabled = false;
                var rt = (RectTransform)shown.transform;
                window.KeepCardsOnScreen.Value = false;
                shown.Update();
                Edges(shown, rt, out float bottom0, out float top0);
                window.KeepCardsOnScreen.Value = true;
                shown.Update();
                Edges(shown, rt, out float bottom1, out float top1);
                float left = rt.anchoredPosition.x - rt.pivot.x * shown.SizeX, right = left + shown.SizeX;
                bool beside = left >= cursorX - 0.5f || right <= cursorX + 0.5f;
                bool overflowed = bottom0 < -0.5f || top0 > height + 0.5f;
                bool inside = bottom1 >= -0.5f && top1 <= height + 0.5f;
                string edges = $"{name} {shown.SizeY:0} of {height:0} tall: game {bottom0:0}..{top0:0}, clamped {bottom1:0}..{top1:0}, beside mouse {beside}";
                result = (inside && beside && (overflowed || !mustOverflow) ? "ok, " : "FAILED, ") + edges + (overflowed ? "" : " (game placement already fit)");
            }
            finally
            {
                window.KeepCardsOnScreen.Value = savedSetting;
                GlobalControls.mousePosition = savedMouse;
            }
        });
        return result;
    }

    private static void Edges(UITooltip shown, RectTransform rt, out float bottom, out float top)
    {
        bottom = rt.anchoredPosition.y - rt.pivot.y * shown.SizeY;
        top = bottom + shown.SizeY;
    }
}
