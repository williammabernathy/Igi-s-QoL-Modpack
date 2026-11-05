using System;
using System.Collections.Generic;
using Behaviour.GalaxyMap;
using Behaviour.UI.Tooltip;
using IgiQoL.UI;
using Source.Galaxy;
using Source.Player;
using Source.Simulation.Story;
using Source.Simulation.World.System;
using Source.UI.Tooltip;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.ConquestDetails;

// The War overview pop-out: every conquest faction's systems, share and fleets, what the faction the player backs needs
// for 30% and what to do next, and a list of systems with their Defense, next-battle strength and what to do about each:
// either the targets one faction's fleets pick from, or the sector the galaxy map shows. Texts refresh once a second;
// the rows are rebuilt only when the listed set changes.
internal sealed class WarOverview : MonoBehaviour
{
    internal const string ListOption = "list", FactionOption = "faction", SortOption = "sort";
    internal const string TargetsList = "targets", SectorList = "sector", AutoFaction = "auto";
    internal const string HelpSort = "help", PreferenceSort = "preference";
    internal static readonly string[] ListNames = { "Targets of a faction", "Sector shown on the map" };
    internal static readonly string[] SortNames = { "Where your help counts first", "Order its fleets prefer" };
    private const float RefreshSeconds = 1f;

    private static readonly string[] FactionHeads = { "Systems", "Systems Now %", "Area Conquered", "30% Goal", "Fleets", "Combat Power", "Wealth" };
    private static readonly float[] FactionWidths = { 68f, 108f, 118f, 76f, 58f, 100f, 72f };
    private static readonly string[] SystemHeads = { "Owner", "Defense", "Next Battle", "What to Do" };
    private static readonly float[] SystemWidths = { 160f, 76f, 108f, 250f };

    internal static WarOverview Instance;

    // What the table and the next-step block need from one system, read once a second.
    private struct Entry
    {
        public Advice Advice;
        public float Total, NextBattle;
    }

    private Popout popout;
    private TMP_Text clock, goal, next, leave, levers, listTitle, empty;
    private RectTransform factionTable, systemTable;
    private float grow, nextRefresh;
    private Faction tracked;
    private readonly DefenseParts parts = new();
    private readonly Reach reach = new();
    private readonly List<Faction> factions = new();
    private readonly List<Faction> shownFactions = new();
    private readonly List<TMP_Text[]> factionRows = new();
    private readonly List<ConquestSystem> systems = new();
    private readonly List<ConquestSystem> shownSystems = new();
    private readonly List<TMP_Text[]> systemRows = new();
    private readonly Dictionary<ConquestSystem, Entry> entries = new();
    private readonly Dictionary<Faction, int> territory = new();
    private Comparison<ConquestSystem> byPreference, byHelp;

    internal int FactionRowCount => factionRows.Count;
    internal int SystemRowCount => systemRows.Count;
    internal string MissionText => goal.text;
    internal string NextStepText => next.text;

    // Self-check: the first fixed-width text in either table that is wider than its cell, or null. Fixed cells cut their
    // text (the pixel font has no ellipsis glyph), so "Systems now %" would read "Systems no". Names are flexible and
    // may shorten.
    internal string TableFitProblem()
    {
        foreach (var table in new[] { factionTable, systemTable })
        {
            if (table == null) continue;
            foreach (var t in table.GetComponentsInChildren<TMP_Text>())
            {
                var le = t.GetComponent<LayoutElement>();
                if (string.IsNullOrEmpty(t.text) || le == null || le.flexibleWidth > 0f) continue;
                float need = t.GetPreferredValues(t.text).x + t.margin.x + t.margin.z, room = t.rectTransform.rect.width;
                if (need > room + 0.5f) return $"'{t.text}' needs {need:0} of {room:0}";
            }
        }
        return null;
    }

    internal string ClockText => clock.text;
    internal IReadOnlyList<ConquestSystem> Listed => shownSystems;
    internal bool TryAdvice(ConquestSystem cs, out Advice advice)
    {
        bool found = entries.TryGetValue(cs, out var e);
        advice = e.Advice;
        return found;
    }

    internal static void Build(Popout p, RectTransform body)
    {
        var self = body.gameObject.AddComponent<WarOverview>();
        self.popout = p;
        self.grow = Mathf.Max(1f, TextScale.TextScale.Factor);
        self.clock = Kit.Muted(body, "");
        Kit.Tooltip(self.clock, "Timers", "Conquest Update: Area Conquered and the 30% objective refresh, settling counts down, control levels rise, damaged stations are rebuilt.\nFleet orders: every NPC faction re-plans its fleets (also right after a battle it was in).\nEconomy tick: taxes and the home subsidy are paid, builds are queued and Great Gate fleets sent.\nThe game shows only the first.");
        var scroll = Kit.Scroll(body);
        self.factionTable = Kit.Column(scroll, 2f);
        Kit.Gap(scroll, 4f);
        self.goal = Kit.Text(scroll, "");
        self.next = Kit.Text(scroll, "");
        self.leave = Kit.Muted(scroll, "");
        self.levers = Kit.Muted(scroll, "");
        Kit.Gap(scroll, 4f);
        self.listTitle = Kit.Heading(scroll, "");
        self.systemTable = Kit.Column(scroll, 2f);
        self.empty = Kit.Muted(scroll, "");
        Instance = self;
    }

    internal static void BuildOptions(Popout p, RectTransform column)
    {
        var row = Kit.Row(column, 8f, 26f);
        Kit.Label(row, "List");
        Kit.Dropdown(row, ListNames, ListMode(p.State) == SectorList ? 1 : 0, i =>
        {
            p.State.options[ListOption] = i == 1 ? SectorList : TargetsList;
            p.State.Save(p.Id);
            Instance?.Tick();
        }, 220f);

        var choices = new List<Faction>();
        ChoosableFactions(choices);
        var names = new List<string> { "Your 30% mission's faction" };
        int index = 0;
        string chosen = p.State.options[FactionOption].IsString ? p.State.options[FactionOption].AsString : AutoFaction;
        for (int i = 0; i < choices.Count; i++)
        {
            names.Add(Translation.Translate(choices[i].name));
            if (choices[i].identifier == chosen) index = i + 1;
        }
        var row2 = Kit.Row(column, 8f, 26f);
        Kit.Label(row2, "Targets of");
        Kit.Dropdown(row2, names, index, i =>
        {
            p.State.options[FactionOption] = i == 0 ? AutoFaction : choices[i - 1].identifier;
            p.State.Save(p.Id);
            Instance?.Tick();
        }, 220f);

        var row3 = Kit.Row(column, 8f, 26f);
        Kit.Label(row3, "Sort targets by");
        Kit.Dropdown(row3, SortNames, SortMode(p.State) == PreferenceSort ? 1 : 0, i =>
        {
            p.State.options[SortOption] = i == 1 ? PreferenceSort : HelpSort;
            p.State.Save(p.Id);
            Instance?.Tick();
        }, 220f);
    }

    internal static string ListMode(PopoutState s) =>
        s.options[ListOption].IsString && s.options[ListOption].AsString == SectorList ? SectorList : TargetsList;

    internal static string SortMode(PopoutState s) =>
        s.options[SortOption].IsString && s.options[SortOption].AsString == PreferenceSort ? PreferenceSort : HelpSort;

    // The conquest factions, the three majors first, as the faction choice lists them.
    private static void ChoosableFactions(List<Faction> into)
    {
        into.Clear();
        foreach (var f in ConquestWarTuning.GreatGateFactions) into.Add(f);
        foreach (var f in Conquest.conquestFactions)
            if (!into.Contains(f)) into.Add(f);
    }

    // The chosen faction, or the one of an accepted 30% mission, or the major with the most systems.
    internal Faction Tracked()
    {
        var v = popout.State.options[FactionOption];
        if (v.IsString && v.AsString != AutoFaction)
        {
            var f = Faction.Get(v.AsString);
            if (f != null) return f;
        }
        return WarMath.AutoTracked();
    }

    private void Awake()
    {
        byPreference = (a, b) =>
        {
            bool pa = reach.Allowed.Contains(a), pb = reach.Allowed.Contains(b);
            if (pa != pb) return pa ? -1 : 1;
            if (a.isUnowned != b.isUnowned) return a.isUnowned ? -1 : 1;
            int ta = Territory(a.faction), tb = Territory(b.faction);
            if (ta != tb) return tb.CompareTo(ta);
            return ConquestCombat.GetNextBattleStrength(a).CompareTo(ConquestCombat.GetNextBattleStrength(b));
        };
        // Help first, the fewest fleets left after the lever first among them; then what the faction does on its own,
        // then what isn't worth a visit. The faction's own order breaks ties.
        byHelp = (a, b) =>
        {
            var ea = entries[a].Advice;
            var eb = entries[b].Advice;
            if (ea.Rank != eb.Rank) return ea.Rank.CompareTo(eb.Rank);
            if (ea.Rank == 0 && ea.Order != eb.Order) return ea.Order.CompareTo(eb.Order);
            return byPreference(a, b);
        };
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!ConquestDetails.Instance.Active || !popout.Shown || Time.unscaledTime < nextRefresh) return;
        Tick();
    }

    internal void Tick()
    {
        nextRefresh = Time.unscaledTime + RefreshSeconds;
        try
        {
            var conquest = WarMath.Conquest;
            if (conquest?.systems == null || conquest.systems.Count == 0)
            {
                Set(clock, "The Conquest war hasn't started in this save.");
                return;
            }
            tracked = Tracked();
            RefreshClock(conquest);
            RefreshFactions(conquest);
            reach.Build(tracked);
            if (ListMode(popout.State) == SectorList) CollectSector();
            else CollectTargets(tracked);
            ReadEntries();
            if (ListMode(popout.State) == TargetsList && tracked != null)
                systems.Sort(SortMode(popout.State) == PreferenceSort ? byPreference : byHelp);
            RefreshBlock(conquest);
            if (!Same(systems, shownSystems)) RebuildSystems();
            for (int i = 0; i < systemRows.Count; i++) FillSystem(systemRows[i], shownSystems[i]);
        }
        catch (Exception e)
        {
            ConquestDetails.Instance.Fail(e, nameof(WarOverview));
            Popouts.Close(ConquestDetails.PopoutId);
        }
    }

    private void RefreshClock(Conquest conquest)
    {
        var u = WarMath.Universe;
        string text = "Conquest Update " + WarMath.Timer(conquest.conquestTickTime);
        if (u != null) text += " | Fleet orders " + WarMath.Timer(u.militaryPlanningTime) + " | Economy tick " + WarMath.Timer(u.tickTime);
        Set(clock, text + "  (hover for what each does)");
    }

    private void RefreshFactions(Conquest conquest)
    {
        factions.Clear();
        var u = WarMath.Universe;
        foreach (var f in Conquest.conquestFactions)
        {
            var economy = u?.PeekFactionEconomy(f);
            if (WarMath.CountOwned(conquest, f) > 0 || economy != null && economy.fleets.Count > 0) factions.Add(f);
        }
        factions.Sort((a, b) => WarMath.CountOwned(conquest, b).CompareTo(WarMath.CountOwned(conquest, a)));
        if (!SameFactions())
        {
            Clear(factionTable);
            factionRows.Clear();
            shownFactions.Clear();
            Header(factionTable, "Faction", FactionHeads, FactionWidths);
            foreach (var f in factions)
            {
                shownFactions.Add(f);
                factionRows.Add(Line(factionTable, FactionWidths));
            }
        }
        int total = conquest.systems.Count;
        int need = WarMath.NeededFor30(total);
        for (int i = 0; i < shownFactions.Count; i++)
        {
            var f = shownFactions[i];
            var cells = factionRows[i];
            int owned = WarMath.CountOwned(conquest, f);
            var economy = u?.PeekFactionEconomy(f);
            Set(cells[0], (f == tracked ? "> " : "") + Translation.Translate(f.name), f.color);
            Set(cells[1], owned.ToString());
            Set(cells[2], WarMath.Pct((float)owned / total));
            Set(cells[3], WarMath.Pct(conquest.GetFactionStanding(f).currentConqueredPercentage));
            Set(cells[4], owned + "/" + need, owned >= need ? ColorHelper.greenish : ColorHelper.offWhite);
            Set(cells[5], economy != null ? economy.fleetSlotsUsed + "/" + ConquestFleetPlanner.MaxFleetsFor(f) : "-");
            Set(cells[6], economy != null ? WarMath.Points(economy.combatPower) : "-");
            Set(cells[7], economy != null ? GameMath.FormatNumber(economy.wealth) : "-");
        }
    }

    // The lines above the target list: where the faction stands against 30%, the one thing most worth doing, what to
    // leave to its fleets, and, before you can claim systems, what you can do instead.
    private void RefreshBlock(Conquest conquest)
    {
        if (tracked == null)
        {
            Set(goal, "");
            Set(next, "");
            Set(leave, "");
            Set(levers, "");
            return;
        }
        int total = conquest.systems.Count;
        int have = WarMath.CountOwned(conquest, tracked), need = WarMath.NeededFor30(total);
        string name = Translation.Translate(tracked.name);
        float stored = conquest.GetFactionStanding(tracked).currentConqueredPercentage;
        string text = name + " holds " + have + " of " + total + " systems; 30% needs " + need
            + (have >= need ? ", enough." : ", " + (need - have) + " more.");
        if (have >= need)
        {
            if (stored >= WarMath.Mission30) text += " Area Conquered shows " + WarMath.Pct(stored) + ": talk to the commander" + HeadquartersAt(conquest) + " now.";
            else text += " Area Conquered shows " + WarMath.Pct(stored) + " until the Conquest Update in " + WarMath.Timer(conquest.conquestTickTime) + "; talk to the commander after that.";
        }
        else text += " Area Conquered shows " + WarMath.Pct(stored) + " until the next Conquest Update.";
        Set(goal, text);

        // The best help: a system where one lever makes it a one-fleet job first, then the one that comes closest.
        ConquestSystem best = null;
        int onTheWay = 0, leaveCount = 0;
        string leaveNames = null;
        foreach (var cs in reach.List)
        {
            if (!entries.TryGetValue(cs, out var e)) continue;
            var a = e.Advice;
            if (a.Helps && (best == null || Better(a, e.NextBattle, entries[best].Advice, entries[best].NextBattle))) best = cs;
            if (a.Kind == AdviceKind.OnTheWay) onTheWay++;
            if (a.Kind is AdviceKind.LeaveIt or AdviceKind.LeaveStart or AdviceKind.Free)
            {
                if (leaveCount < 3) leaveNames = leaveNames == null ? cs.system.name : leaveNames + ", " + cs.system.name;
                leaveCount++;
            }
        }
        if (best != null) Set(next, "Do this next: " + NextStep(best, entries[best].Advice));
        else if (onTheWay > 0) Set(next, "Nothing to do but wait: " + name + "'s fleets are already on the way to " + onTheWay + (onTheWay == 1 ? " system." : " systems."));
        else Set(next, "Nothing worth doing right now.");
        Set(leave, leaveCount == 0 ? "" : "Leave alone: " + leaveCount + (leaveCount == 1 ? " system " : " systems ") + name + " will take on its own: "
            + leaveNames + (leaveCount > 3 ? " and " + (leaveCount - 3) + " more." : "."));
        Set(levers, GamePlayer.current != null && !GamePlayer.current.systemClaimUnlocked
            ? "Until \"Beyond the Front\" you can't claim systems yourself. You help by killing a defending fleet, running war effort missions at a hub next door, or destroying a hub where "
                + name + "'s fleet would be first to claim the system."
            : "");
    }

    // Whether advice a is a better use of the player's time than b.
    private static bool Better(Advice a, float nextA, Advice b, float nextB)
    {
        bool oneA = a.Kind != AdviceKind.FallsShort && a.FleetsAfter == 1, oneB = b.Kind != AdviceKind.FallsShort && b.FleetsAfter == 1;
        if (oneA != oneB) return oneA;
        if (a.Order != b.Order) return a.Order < b.Order;
        return nextA < nextB;
    }

    private string NextStep(ConquestSystem cs, Advice a)
    {
        string where = cs.system.name + " (" + Translation.Translate(cs.faction.name) + ")";
        string drop = "its attack drops from " + a.Fleets + " fleets to " + a.FleetsAfter + ".";
        switch (a.Kind)
        {
            case AdviceKind.KillFleet: return "kill the fleet at " + where + ": " + drop;
            case AdviceKind.WarEffort:
                return "run " + a.Missions + " war effort " + (a.Missions == 1 ? "mission" : "missions") + " next to " + where + ": "
                    + (a.Fleets == 1 ? "one full fleet would then take it." : drop);
            case AdviceKind.KillHub:
                string name = Translation.Translate(a.Tracked.name);
                return "destroy the hub at " + where + ": " + (a.Hub.Siege
                    ? name + "'s siege there takes the system at once."
                    : name + "'s nearest free fleet claims it in about " + WarMath.Clock(a.Hub.Seconds) + ".");
            default: return where + ": one full fleet falls short. Open its card.";
        }
    }

    // The headquarters system of the tracked faction, as " at X", or nothing when it has none.
    private string HeadquartersAt(Conquest conquest)
    {
        foreach (var cs in conquest.systems)
            if (cs.headquarters && cs.faction == tracked) return " at " + cs.system.name;
        return "";
    }

    private bool SameFactions()
    {
        if (factions.Count != shownFactions.Count) return false;
        for (int i = 0; i < factions.Count; i++)
            if (factions[i] != shownFactions[i]) return false;
        return true;
    }

    private void CollectTargets(Faction faction)
    {
        systems.Clear();
        territory.Clear();
        if (faction == null)
        {
            Set(listTitle, "Targets");
            return;
        }
        foreach (var cs in reach.List)
        {
            systems.Add(cs);
            if (cs.faction != null && !territory.ContainsKey(cs.faction)) territory[cs.faction] = ConquestScope.CountWarZoneSystems(cs.faction);
        }
        Set(listTitle, "Systems " + Translation.Translate(faction.name) + " Can Attack, " + (SortMode(popout.State) == PreferenceSort ? "in the Order Its Fleets Prefer" : "Where Your Help Counts First"));
        Set(empty, systems.Count == 0 ? "None right now." : "Next Battle is what NPC fleets compare: a fleet, or an assault, attacks at 1.15 times it. " + HoverHint());
    }

    private int Territory(Faction f) => f != null && territory.TryGetValue(f, out int n) ? n : 0;

    // With click-through on, the rows take the mouse only while the pop-out interact key is held.
    private string HoverHint()
    {
        if (!popout.State.clickThrough) return "Hover a system's name for its full card.";
        var window = Window.WindowFeature.Instance;
        string key = window != null ? Popout.KeyName(window.PopoutInteractKey.Value) : "Ctrl";
        return "Hold " + key + " and hover a system's name for its full card.";
    }

    private void CollectSector()
    {
        systems.Clear();
        var map = AbstractGalaxyMapManager.current;
        var sector = map != null ? map.viewedSector : null;
        if (sector == null || !sector.isWarZone)
        {
            Set(listTitle, "Sector Shown on the Map");
            Set(empty, map == null ? "Open the galaxy map and click a war-zone sector." : "The map isn't showing a war-zone sector. Click one to list its systems.");
            return;
        }
        foreach (var system in sector.allSystems)
        {
            var cs = WarMath.Of(system);
            if (cs != null) systems.Add(cs);
        }
        systems.Sort((a, b) => string.CompareOrdinal(a.system.name, b.system.name));
        Set(listTitle, "Systems in " + sector.name);
        Set(empty, systems.Count == 0 ? "No war-zone systems here." : HoverHint());
    }

    // The Defense, next battle and advice of every system the table or the block uses, each read once.
    private void ReadEntries()
    {
        entries.Clear();
        foreach (var cs in reach.List) ReadEntry(cs);
        foreach (var cs in systems)
            if (!entries.ContainsKey(cs)) ReadEntry(cs);
    }

    private void ReadEntry(ConquestSystem cs)
    {
        parts.Read(cs);
        entries[cs] = new Entry { Advice = WarAdvice.Compute(parts, tracked, reach), Total = parts.Total, NextBattle = parts.NextBattle };
    }

    private static bool Same(List<ConquestSystem> a, List<ConquestSystem> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private void RebuildSystems()
    {
        Clear(systemTable);
        systemRows.Clear();
        shownSystems.Clear();
        if (systems.Count > 0) Header(systemTable, "System", SystemHeads, SystemWidths);
        foreach (var cs in systems)
        {
            shownSystems.Add(cs);
            var cells = Line(systemTable, SystemWidths);
            cells[0].raycastTarget = true;
            cells[0].gameObject.AddComponent<TooltipSource>();
            cells[0].gameObject.AddComponent<SystemCardSource>().System = cs.system;
            cells[cells.Length - 1].overflowMode = TextOverflowModes.Ellipsis;
            systemRows.Add(cells);
        }
    }

    private void FillSystem(TMP_Text[] cells, ConquestSystem cs)
    {
        var e = entries[cs];
        Set(cells[0], cs.system.name);
        Set(cells[1], cs.faction != null ? Translation.Translate(cs.faction.name) : "unowned", cs.faction != null ? cs.faction.color : ColorHelper.poiTooltipMuted);
        Set(cells[2], cs.isUnowned ? "-" : WarMath.Points(e.Total));
        Set(cells[3], cs.isUnowned ? "-" : WarMath.Points(e.NextBattle));
        var (text, color) = Verdict(e.Advice);
        Set(cells[4], text, color);
    }

    // The table's wording for one verdict: a running fight shows its stage and clock instead of the card's short phrase.
    private (string, Color) Verdict(Advice a)
    {
        switch (a.Kind)
        {
            case AdviceKind.Battle: return (WarCards.StageName(a.Stage) + ", " + WarMath.Clock(a.TimeLeft), ColorHelper.reddish);
            case AdviceKind.Own: return ("held by " + (tracked != null ? Translation.Translate(tracked.name) : "its faction"), ColorHelper.poiTooltipMuted);
            case AdviceKind.KillFleet: return ("Help: kill its fleet, " + a.Fleets + " to " + a.FleetsAfter + " fleets", a.Color);
            case AdviceKind.WarEffort: return ("Help: " + a.Missions + " war effort " + (a.Missions == 1 ? "mission" : "missions") + (a.Fleets == 1 ? ", then 1 fleet finishes" : ", " + a.Fleets + " to " + a.FleetsAfter + " fleets"), a.Color);
            case AdviceKind.KillHub: return (a.Hub.Siege ? "Help: kill the hub now" : "Help: kill the hub", a.Color);
            default: return (a.Text, a.Color);
        }
    }

    private void Header(RectTransform table, string first, string[] heads, float[] widths)
    {
        var row = Kit.Row(table, 4f, 18f);
        Kit.Label(row, first, 11f).color = ColorHelper.poiTooltipMuted;
        for (int i = 0; i < heads.Length; i++)
        {
            bool last = i == heads.Length - 1 && table == systemTable;
            var cell = Kit.Fixed(row, heads[i], widths[i] * grow, last ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight, ColorHelper.poiTooltipMuted);
            if (last) cell.margin = new Vector4(8f, 0f, 0f, 0f);
            // A header wider than its column would be cut off with no ellipsis: the column grows to fit it, and the
            // rows built after the header use the wider column. Measuring needs an active label.
            if (!cell.gameObject.activeInHierarchy) continue;
            float need = Mathf.Ceil(cell.GetPreferredValues(heads[i]).x + cell.margin.x + 4f);
            if (need <= widths[i] * grow) continue;
            widths[i] = need / grow;
            Behaviour.UI.Side_Menu.SideTabs.Captain.CaptainCards.SetFixedSize((RectTransform)cell.transform, need, 22f);
        }
    }

    private TMP_Text[] Line(RectTransform table, float[] widths)
    {
        var row = Kit.Row(table, 4f, 20f);
        var cells = new TMP_Text[widths.Length + 1];
        cells[0] = Kit.Label(row, "");
        cells[0].overflowMode = TextOverflowModes.Ellipsis;
        for (int i = 0; i < widths.Length; i++)
        {
            bool last = i == widths.Length - 1 && table == systemTable;
            cells[i + 1] = Kit.Fixed(row, "", widths[i] * grow, last ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight, ColorHelper.offWhite);
            if (last) cells[i + 1].margin = new Vector4(8f, 0f, 0f, 0f);
        }
        return cells;
    }

    private static void Clear(RectTransform table)
    {
        for (int i = table.childCount - 1; i >= 0; i--)
        {
            var child = table.GetChild(i).gameObject;
            child.SetActive(false);
            Object.Destroy(child);
        }
    }

    private static void Set(TMP_Text t, string text)
    {
        if (t.text != text) t.text = text;
    }

    private static void Set(TMP_Text t, string text, Color color)
    {
        Set(t, text);
        if (t.color != color) t.color = color;
    }
}

// The game's system hover card, with this feature's sections, on a row of the War overview. The window is where the
// player studies systems, so its cards are always the full breakdown.
internal sealed class SystemCardSource : MonoBehaviour, ITooltipCardSource
{
    public SystemMapData System;

    public TooltipCard BuildTooltipCard()
    {
        ConquestDetails.ForceFull = true;
        try { return Behaviour.UI.Tooltip.MapTooltipCardBuilder.BuildSystem(System); }
        finally { ConquestDetails.ForceFull = false; }
    }
}

// The extras on the galaxy map's corner panel: the two hidden clocks under the Conquest Update countdown, as rows in
// the same H:MM:SS format, and a tile that opens the War overview.
internal sealed class MapCornerExtras : MonoBehaviour
{
    internal const string OrdersLabel = "Fleet orders", EconomyLabel = "Economy tick";
    private const float FontSize = 10f;

    internal TMP_Text ordersName, ordersValue, economyName, economyValue;
    private int shownOrders = -1, shownEconomy = -1;

    internal static void Add(Behavior.UI.GalaxyMap.MapCornerPanel panel)
    {
        var card = panel.conquestCard;
        if (card == null || panel.GetComponent<MapCornerExtras>() != null) return;
        var self = panel.gameObject.AddComponent<MapCornerExtras>();
        var rt = (RectTransform)card.transform;

        // The game's (i) tooltip says factions rethink their strategy at the Conquest Update; they do it on their own
        // 10-minute clock, so the player who reads it where it sits gets the right picture.
        var info = card.GetComponentInChildren<TooltipSource>(true);
        if (info != null)
            info.BodyText += "\n\nQoL: fleets re-plan on their own 10-minute clock (Fleet orders), and the economy pays out hourly on a separate clock (Economy tick).";

        self.Row(rt, OrdersLabel, out self.ordersName, out self.ordersValue,
            "Fleet orders (QoL)", "NPC factions re-plan their fleets every 10 minutes, and right after a battle they were in. This counts down to the next time.");
        self.Row(rt, EconomyLabel, out self.economyName, out self.economyValue,
            "Economy tick (QoL)", "The hourly tick that pays taxes and the home subsidy, queues builds and sends Great Gate fleets. It runs on its own clock, apart from the Conquest Update.");

        var tile = panel.BuildTile("QoLWarOverview", "Flag", ColorHelper.stationCardTitle, () => Popouts.Toggle(ConquestDetails.PopoutId), null);
        tile.tooltip.Title = "";
        tile.tooltip.BodyText = "War Overview (QoL): every faction's systems and fleets, what its 30% mission needs and what to do next, and the Defense, next battle and advice for a faction's targets or for the sector on the map.";
    }

    // A label on the left that gives way when the card is narrow, and the clock on the right that never does, the way the
    // game's own header row is laid out.
    private void Row(RectTransform card, string label, out TMP_Text name, out TMP_Text value, string tipTitle, string tip)
    {
        var row = Behaviour.UI.Shared.UiCards.NewChild("Clock", card);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        name = Behaviour.UI.Shared.UiCards.AddLabel(row, label, FontSize, ColorHelper.mapConquestCardTitle);
        name.textWrappingMode = TextWrappingModes.NoWrap;
        // At large text sizes the 180-wide card can't fit label and clock at full size: the label shrinks a little
        // before it would lose letters.
        name.enableAutoSizing = true;
        name.fontSizeMin = FontSize * 0.75f;
        name.fontSizeMax = FontSize;
        name.overflowMode = TextOverflowModes.Ellipsis;
        name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        value = Behaviour.UI.Shared.UiCards.AddLabel(row, "", FontSize, ColorHelper.mapConquestCardTimer);
        value.textWrappingMode = TextWrappingModes.NoWrap;
        value.alignment = TextAlignmentOptions.MidlineRight;
        value.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0f;
        Kit.Tooltip(name, tipTitle, tip);
    }

    private void Update()
    {
        if (!ConquestDetails.Instance.Active || ordersValue == null) return;
        try
        {
            var u = WarMath.Universe;
            if (u == null) return;
            int orders = Mathf.RoundToInt(u.militaryPlanningTime), economy = Mathf.RoundToInt(u.tickTime);
            if (orders != shownOrders)
            {
                shownOrders = orders;
                ordersValue.text = GameMath.FormatTime(orders, shortFormat: true);
            }
            if (economy != shownEconomy)
            {
                shownEconomy = economy;
                economyValue.text = GameMath.FormatTime(economy, shortFormat: true);
            }
            // The clocks keep the width of the widest time, so the rows don't shift as the digits change, and get it
            // again when the text size setting changes.
            var textScale = TextScale.TextScale.Instance;
            float size = ordersValue.fontSize * (textScale != null && textScale.Active ? textScale.Percent.Value : 100);
            if (!Mathf.Approximately(size, shownSize))
            {
                shownSize = size;
                float width = Mathf.Ceil(ordersValue.GetPreferredValues("00:00:00").x) + 1f;
                ordersValue.GetComponent<LayoutElement>().minWidth = width;
                economyValue.GetComponent<LayoutElement>().minWidth = width;
            }
        }
        catch (Exception e) { ConquestDetails.Instance.Fail(e, nameof(MapCornerExtras)); }
    }

    private float shownSize = -1f;
}
