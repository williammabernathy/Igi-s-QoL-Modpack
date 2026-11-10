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

    // Every verdict, rederived from the game's own numbers by plain arithmetic: the fleets an attack needs (the next
    // battle times 1.15, over 2,000 points times the tracked faction's war footing from WarFooting.GetMultiplier) and
    // what killing the defending fleets leaves.
    private static void CheckAdvice(SelfCheckContext ctx, Conquest conquest)
    {
        var tracked = WarMath.AutoTracked();
        if (tracked == null) { ctx.Skip("advice", "no faction to give advice for"); return; }
        var reach = new Reach();
        reach.Build(tracked);
        var d = new DefenseParts();
        var counts = new Dictionary<AdviceKind, int>();
        int bad = 0, checkedSystems = 0;
        string first = null;
        foreach (var cs in conquest.systems)
        {
            d.Read(cs);
            var a = WarAdvice.Compute(d, tracked, reach);
            counts[a.Kind] = counts.TryGetValue(a.Kind, out int n) ? n + 1 : 1;
            checkedSystems++;
            string why = ExpectAdvice(cs, d, a, tracked, reach);
            if (why != null) { bad++; first ??= cs.system.name + ": " + why; }
        }
        var parts = new List<string>();
        foreach (var pair in counts) parts.Add(pair.Key + " " + pair.Value);
        string detail = $"{checkedSystems} systems for {tracked.identifier} at war footing x{WarFooting.GetMultiplier(tracked):0.000}: {string.Join(", ", parts)}" + (first != null ? "; first mismatch " + first : "");
        if (bad == 0) ctx.Pass("advice", detail);
        else ctx.Fail("advice", detail);
    }

    private static string ExpectAdvice(ConquestSystem cs, DefenseParts d, Advice a, Faction tracked, Reach reach)
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
            double judgedFleet = 2000.0 * WarFooting.GetMultiplier(tracked);
            int fleets = Math.Max(1, (int)Math.Ceiling(next * 1.15 / judgedFleet - 0.0001));
            if (a.Fleets != fleets) return $"needs {a.Fleets} fleets, arithmetic says {fleets} (next battle {next:0})";
            if (fleets == 1)
            {
                if (!WarMath.CanEstimate(d)) expected = AdviceKind.LeaveStart;
                else if (WarMath.EstimateHubSiege(d, (float)judgedFleet).Result == SiegeResult.Captured) expected = AdviceKind.LeaveIt;
                else expected = AdviceKind.FallsShort;
            }
            else
            {
                int kill = d.FleetCount > 0 ? Math.Max(1, (int)Math.Ceiling((next - d.Fleets) * 1.15 / judgedFleet - 0.0001)) : fleets;
                expected = kill < fleets ? AdviceKind.KillFleet : AdviceKind.Skip;
                if (a.FleetsAfter != kill) return $"killing the fleets leaves {kill}, advice says {a.FleetsAfter}";
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

    private static bool BattleAt(ConquestSystem cs)
    {
        foreach (var battle in WarMath.Universe.battleManager.activeBattles)
            if (battle.targetSystemGuid == cs.system.guid) return true;
        return false;
    }

    // The mod's judged-strength arithmetic against the game's own decision. For every fleet and every owned war-zone
    // system, ConquestFleetOrders.CanBeat (a fleet starts an attack only when WarFooting.GetJudgedStrength reaches 1.15
    // times the system's next battle) must agree with points x footing >= WarMath.ToStart(next battle). A faction with
    // no footing proves little, so each faction's footing is set by hand for the test and put back in a finally.
    private static void CheckJudged(SelfCheckContext ctx, Conquest conquest)
    {
        var universe = WarMath.Universe;
        int pairs = 0, bad = 0, flipped = 0, factions = 0;
        string first = null;
        var targets = new List<ConquestSystem>();
        foreach (var cs in conquest.systems)
            if (!cs.isUnowned) targets.Add(cs);
        foreach (var economy in universe.factionEconomies.Values)
        {
            var f = economy.faction;
            if (f == null || economy.fleets.Count == 0 || !Conquest.conquestFactions.Contains(f)) continue;
            var standing = conquest.GetFactionStanding(f);
            float savedContribution = standing.warContribution, savedSeconds = standing.warFootingSeconds;
            factions++;
            try
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    standing.warContribution = pass == 0 ? 0f : ConquestWarTuning.WarFootingFullContribution / 2f;
                    standing.warFootingSeconds = ConquestWarTuning.WarFootingSeconds;
                    float footing = WarMath.Footing(f);
                    if (Mathf.Abs(footing - (1f + ConquestWarTuning.WarFootingMax * pass * 0.5f)) > 0.0001f)
                    {
                        bad++;
                        first ??= $"{f.identifier}: footing x{footing:0.0000} for contribution {standing.warContribution:0}";
                        continue;
                    }
                    foreach (var fleet in economy.fleets)
                    {
                        if (fleet.totalPoints <= 0) continue;
                        foreach (var cs in targets)
                        {
                            float next = ConquestCombat.GetNextBattleStrength(cs);
                            bool game = ConquestFleetOrders.CanBeat(fleet, cs);
                            bool mine = fleet.totalPoints * footing >= WarMath.ToStart(next);
                            pairs++;
                            if (pass == 1 && !(fleet.totalPoints >= WarMath.ToStart(next)) && game) flipped++;
                            if (game != mine)
                            {
                                bad++;
                                first ??= $"{f.identifier} fleet {fleet.totalPoints} vs {cs.system.name} next {next:0} at x{footing:0.000}: game {game}, mine {mine}";
                            }
                        }
                    }
                }
            }
            finally
            {
                standing.warContribution = savedContribution;
                standing.warFootingSeconds = savedSeconds;
            }
        }
        string detail = $"{pairs} fleet/system pairs for {factions} factions, {flipped} only beatable with footing" + (first != null ? "; first mismatch " + first : "");
        if (factions == 0) ctx.Skip("judged-strength", "no faction with fleets");
        else if (bad == 0) ctx.Pass("judged-strength", detail);
        else ctx.Fail("judged-strength", detail);
    }

    // A fleet's strength in a siege round is the game's FleetMixBonus.GetBattleStrength (what the Defense number sums for
    // defending fleets too); it carries the faction's war footing next to the Empire-only fleet mix bonus. The judged
    // strength the capture tests use is points times the same footing.
    private static void CheckSiegeStrength(SelfCheckContext ctx, Conquest conquest)
    {
        int checkedFleets = 0, bad = 0;
        string first = null;
        foreach (var fleet in WarMath.Universe.AllFleets())
        {
            if (fleet.totalPoints <= 0 || !Conquest.conquestFactions.Contains(fleet.faction)) continue;
            float want = fleet.totalPoints * (1f + FleetMixBonus.Get(fleet) + conquest.GetFactionStanding(fleet.faction).warFootingBonus);
            float got = FleetMixBonus.GetBattleStrength(fleet);
            checkedFleets++;
            if (Mathf.Abs(want - got) > 0.5f)
            {
                bad++;
                first ??= $"{fleet.displayName}: {want:0} expected, game {got:0}";
            }
        }
        string detail = $"{checkedFleets} fleets" + (first != null ? "; first mismatch " + first : "");
        if (checkedFleets == 0) ctx.Skip("siege-strength", "no fleets");
        else if (bad == 0) ctx.Pass("siege-strength", detail);
        else ctx.Fail("siege-strength", detail);
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
    // delivery and station cards. A short card's War block has at most seven rows and the whole card fits on the
    // screen; the line also says how many cards pass half the screen and how much of the tallest one is the mod's.
    private static IEnumerator CheckCardLayout(SelfCheckContext ctx, Conquest conquest, ConquestSystem pick)
    {
        if (HudManager.Instance == null || !UITooltip.TooltipEnabled || UITooltip.tooltipParent == null || UITooltip._rootCanvas == null)
        {
            ctx.Skip("card-fit", "no tooltip layer to draw cards in");
            yield break;
        }
        var titles = new HashSet<string> { WarCards.SummaryTitle, WarCards.DefenseTitle, WarCards.TakingTitle, WarCards.BattleTitle, WarCards.FleetTitle, WarCards.StationTitle };
        var offenders = new List<string>();
        int cards = 0, rows = 0, systems = 0, overHalf = 0, overScreen = 0, maxWarRows = 0;
        float tallest = 0f;
        string tallestName = null, tallestMakeup = null, mostRowsName = null;
        ConquestSystem tallestSystem = null;
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
                if (height > tallest) { tallest = height; tallestName = name; tallestMakeup = Makeup(card); tallestSystem = cs; }
                if (height > limit) overHalf++;
                if (height > Screen.height) overScreen++;
                var war = card.sections.Find(s => s.title == WarCards.SummaryTitle);
                if (war != null && war.rows.Count > maxWarRows) { maxWarRows = war.rows.Count; mostRowsName = name; }
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
        // Same as the fleet card: the map draws only the deliveries of the system it shows, so with none in view it's
        // taken to the system a delivery is in and put back after the cards are drawn.
        string deliveryDetail = "no delivery icon on the map";
        string deliverySkip = null;
        bool deliveryOk = true;
        int deliveries = 0;
        var move = new MapMove();
        try
        {
            if (Object.FindObjectsByType<WarshipDeliveryMapIcon>(FindObjectsInactive.Exclude).Length == 0)
            {
                SystemMapData deliverySystem = null;
                foreach (var economy in WarMath.Universe.AllEconomies())
                {
                    foreach (var delivery in economy.warshipDeliveries)
                    {
                        deliverySystem = delivery.GetCurrentSystem();
                        if (deliverySystem != null) break;
                    }
                    if (deliverySystem != null) break;
                }
                if (deliverySystem == null) deliverySkip = "no warship delivery is under way anywhere in the universe";
                else
                {
                    yield return move.Arrive<WarshipDeliveryMapIcon>(deliverySystem);
                    if (!move.Arrived) deliverySkip = $"the map didn't show a delivery icon for {deliverySystem.name} within {MapMove.WaitSeconds:0} s";
                }
            }
            if (deliverySkip == null)
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
                        deliveryDetail = $"delivery card note {note}, old row gone {noRow}" + (move.Moved ? $", map moved to {move.Target.name}" : "");
                    });
                }
        }
        finally { move.Restore(); }
        yield return move.Settle();

        string detail = $"{cards} cards, {rows} mod rows measured" + (offenders.Count > 0 ? "; too wide: " + string.Join("; ", offenders.GetRange(0, Math.Min(6, offenders.Count))) + (offenders.Count > 6 ? $" (+{offenders.Count - 6} more)" : "") : "");
        if (offenders.Count == 0 && rows > 0) ctx.Pass("card-fit", detail);
        else ctx.Fail("card-fit", detail);
        if (deliverySkip != null) ctx.Skip("delivery-card", deliverySkip);
        else if (deliveries == 0) ctx.Skip("delivery-card", deliveryDetail);
        else if (deliveryOk) ctx.Pass("delivery-card", deliveryDetail);
        else ctx.Fail("delivery-card", deliveryDetail);

        // The tallest short card again without what the mod adds (its War block and its "Hold ... for the full
        // breakdown" hint): the rest is the game's own card, which the short form doesn't change.
        float gameOnly = 0f;
        if (tallestSystem != null)
            Draw(() =>
            {
                var c = Summary(tallestSystem);
                c.sections.RemoveAll(s => s.title == WarCards.SummaryTitle);
                c.hints.RemoveAll(h => h.action == "for the full breakdown");
                return c;
            }, (shown, _) => gameOnly = shown.SizeY * scale);
        string heightDetail = $"most War rows {maxWarRows} ({mostRowsName}); tallest short card {tallest:0} px of {Screen.height} ({tallestName}: {tallestMakeup}), of it the game's own card {gameOnly:0} px and the mod's {tallest - gameOnly:0} px; "
            + $"{overHalf} of {Mathf.Min(systems, 60)} over half the screen, {overScreen} taller than the screen, at scale {scale:0.##}";
        if (tallestName == null) ctx.Skip("summary-height", "no system card was drawn");
        else if (maxWarRows <= 7 && overScreen == 0 && gameOnly > 0f) ctx.Pass("summary-height", heightDetail);
        else ctx.Fail("summary-height", heightDetail);
        yield return null;
    }

    // What a card is made of, for a height failure: the count of each block, and each section with its row count.
    private static string Makeup(TooltipCard card)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"banners {card.banners.Count}, owner rows {card.ownerRows.Count}, rows {card.rows.Count}, scroll rows {card.scrollRows.Count} (shows {card.scrollVisibleRows}), chips {card.chips.Count}, facilities {card.facilities.Count}, missions {card.missions.Count}, notes {card.notes.Count}, hints {card.hints.Count}, description {!string.IsNullOrEmpty(card.description)}");
        foreach (var s in card.sections) sb.Append($", section '{s.title}' {s.rows.Count} rows");
        return sb.ToString();
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
