using System;
using System.Collections.Generic;
using Behaviour.UI.Shared;
using IgiQoL.UI;
using Source.Util;
using TMPro;
using UnityEngine;

namespace IgiQoL.Features.CombatLog;

public static class CombatLogTab
{
    // 0 = the newest fight, 1.. = older ones, -1 = the whole session.
    internal static int scope;
    internal static View view = View.DealtByWeapon;
    private const int MaxRows = 25;

    public static void Build(RectTransform content)
    {
        SettingRows.AddEntries(content, CombatLog.Instance.Id, SettingRows.Rows.Options);
        Kit.Muted(content, "Damage between your side (ships, drones, fighters) and everyone else, as it landed: shield and armor absorbed plus hull lost, after resistances. Per Second figures use the time spent fighting.");

        var row = Kit.Row(content);
        var scopes = new List<string>();
        int fights = CombatLog.Fights.Count;
        for (int i = 0; i < fights; i++) scopes.Add(FightName(i));
        scopes.Add("Whole session");
        int scopeIndex = scope < 0 || scope >= fights ? fights : scope;
        Kit.Dropdown(row, scopes, scopeIndex, i => { scope = i >= fights ? -1 : i; ModWindow.Show("combat"); }, 260f);
        var views = new List<string>();
        foreach (View v in System.Enum.GetValues(typeof(View))) views.Add(CombatLog.ViewName(v));
        Kit.Dropdown(row, views, (int)view, i => { view = (View)i; ModWindow.Show("combat"); }, 220f);
        Kit.TextButton(row, "Clear", () => { CombatLog.Reset(); scope = -1; ModWindow.Show("combat"); }, 70f);
        var overview = CombatOverview.CombatOverview.Instance;
        if (overview != null && overview.Active)
            Kit.TextButton(row, Popouts.IsOpen(CombatOverview.CombatOverview.PopoutId) ? "Close combat overview" : "Combat overview", () =>
            {
                Popouts.Toggle(CombatOverview.CombatOverview.PopoutId);
                ModWindow.Show("combat");
            }, 170f);

        var meters = Kit.Row(content);
        PopoutTabButton.Add(meters, "Pop out meters", "Dock back all", () => Popouts.AnyOpen(DpsMeter.KindId), DpsMeter.PopOutAll,
            () => Popouts.CloseAll(DpsMeter.KindId), 150f,
            "Bar meters that float over the game, as many as you like, each with its own view, scope and look. Their Options button changes them.");
        var presets = new List<string> { "Add a meter..." };
        presets.AddRange(DpsMeter.PresetNames);
        Kit.Choice add = null;
        add = Kit.Dropdown(meters, presets, 0, i =>
        {
            if (i > 0) DpsMeter.AddPreset(i - 1);
            add.SetIndex(0);
        }, 220f);

        Kit.Column(content, 2f).gameObject.AddComponent<CombatTable>();
    }

    internal static string FightName(int i)
    {
        var f = CombatLog.Fights[i];
        float ago = Time.time - f.last;
        bool running = i == 0 && ago <= CombatLog.Instance.FightGapSeconds.Value;
        string when = running ? "now" : FormatString.FormatTime(ago) + " ago";
        return (i == 0 ? (running ? "Current fight" : "Last fight") : "Fight " + (i + 1)) + " (" + FormatString.FormatTime(f.Duration) + ", " + when + ")";
    }

    internal static Fight Selected()
    {
        if (scope < 0) return CombatLog.Session;
        return scope < CombatLog.Fights.Count ? CombatLog.Fights[scope] : null;
    }

    // Refreshed once a second while something new was recorded; rows are built once and only their texts change.
    internal sealed class CombatTable : MonoBehaviour
    {
        private sealed class Cells
        {
            public RectTransform row;
            public TMP_Text[] text;
        }

        private int version = -1;
        private float next;
        private TMP_Text empty, summary, more;
        private string summaryTip;
        private RectTransform header;
        private readonly List<Cells> pool = new();
        internal int RowCount { get; private set; }

        private void Update()
        {
            if (!CombatLog.Instance.Active || Time.unscaledTime < next || version == CombatLog.Version) return;
            next = Time.unscaledTime + 1f;
            version = CombatLog.Version;
            try { Refresh(); }
            catch (Exception e) { CombatLog.Instance.Fail(e, nameof(CombatTable)); }
        }

        private void Refresh()
        {
            var column = (RectTransform)transform;
            RowCount = 0;
            var fight = Selected();
            bool has = fight != null && fight.dealt + fight.taken > 0;
            if (empty == null && !has) empty = Kit.Muted(column, "Nothing recorded yet. Hits by and on your ships, drones and fighters show up here.");
            if (empty != null) empty.gameObject.SetActive(!has);
            if (summary != null) summary.gameObject.SetActive(has);
            if (header != null) header.gameObject.SetActive(has);
            if (!has)
            {
                foreach (var c in pool) c.row.gameObject.SetActive(false);
                if (more != null) more.gameObject.SetActive(false);
                return;
            }

            float secs = fight.Duration;
            if (summary == null)
            {
                // Two fixed lines, so a longer figure or a narrow window never changes the height of the table below.
                summary = Kit.Label(column, "");
                summary.textWrappingMode = TextWrappingModes.Normal;
                summary.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = Mathf.Ceil(2f * 16f * TextScale.TextScale.Factor + 2f);
            }
            string line = $"{FormatString.FormatTime(secs)} of fighting. Dealt {GameMath.FormatNumber((float)fight.dealt)} ({GameMath.FormatNumber((float)(fight.dealt / secs))}/s), taken {GameMath.FormatNumber((float)fight.taken)} ({GameMath.FormatNumber((float)(fight.taken / secs))}/s), kills {fight.kills}.";
            Set(summary, line);
            if (summaryTip != line)
            {
                summaryTip = line;
                Kit.Tooltip(summary, null, line);
            }

            bool dealtView = view <= View.DealtToTarget;
            if (header == null)
            {
                header = Kit.Row(column, 6f, 20f);
                Kit.Label(header, CombatLog.ViewName(view)).color = ColorHelper.stationCardTitle;
                Head(header, "Damage", ColumnWidths[0]);
                Head(header, "Share", ColumnWidths[1]);
                Head(header, "Per Second", ColumnWidths[2]);
                Head(header, "Hits", ColumnWidths[3]);
                Head(header, "Crit", ColumnWidths[4]);
                Head(header, "Biggest", ColumnWidths[5]);
                Kit.Tooltip(Head(header, "Shield/Armor/Hull", ColumnWidths[6]), "Shield / Armor / Hull", "Of each row's damage, the percent absorbed by shields, absorbed by armor, and taken off the hull.");
                if (dealtView) Head(header, "Kills", ColumnWidths[7]);
            }

            double whole = dealtView ? fight.dealt : fight.taken;
            var rows = CombatLog.Sorted(fight, view);
            int shown = Mathf.Min(rows.Count, MaxRows);
            for (int i = 0; i < shown; i++)
            {
                var s = rows[i];
                if (i == pool.Count) pool.Add(NewRow(column, dealtView));
                var c = pool[i];
                c.row.gameObject.SetActive(true);
                var t = c.text;
                Set(t[0], s.label);
                Set(t[1], DpsMeter.Mono(GameMath.FormatNumber((float)s.total)));
                Set(t[2], DpsMeter.Mono(CombatLog.Pct(s.total, whole) + "%"));
                Set(t[3], DpsMeter.Mono(GameMath.FormatNumber((float)(s.total / secs))));
                Set(t[4], DpsMeter.Mono(s.hits.ToString()));
                Set(t[5], DpsMeter.Mono(CombatLog.Pct(s.crits, s.hits) + "%"));
                Set(t[6], DpsMeter.Mono(GameMath.FormatNumber((float)s.max)));
                Set(t[7], DpsMeter.Mono($"{CombatLog.Pct(s.shield, s.total)}/{CombatLog.Pct(s.armor, s.total)}/{CombatLog.Pct(s.hull, s.total)}"));
                if (dealtView) Set(t[8], s.kills > 0 ? DpsMeter.Mono(s.kills.ToString()) : "");
            }
            for (int i = shown; i < pool.Count; i++) pool[i].row.gameObject.SetActive(false);
            RowCount = shown;

            bool extra = rows.Count > MaxRows;
            if (extra && more == null) more = Kit.Muted(column, "");
            if (more != null)
            {
                more.gameObject.SetActive(extra);
                if (extra)
                {
                    Set(more, $"{rows.Count - MaxRows} smaller rows not shown.");
                    // Rows may have been added after it.
                    if (more.transform.GetSiblingIndex() != column.childCount - 1) more.transform.SetAsLastSibling();
                }
            }
        }

        private static Cells NewRow(RectTransform column, bool dealtView)
        {
            var r = Kit.Row(column, 6f, 20f);
            var c = new Cells { row = r, text = new TMP_Text[dealtView ? 9 : 8] };
            c.text[0] = Kit.Label(r, "");
            c.text[0].overflowMode = TextOverflowModes.Ellipsis;
            var widths = ColumnWidths;
            for (int i = 1; i < c.text.Length; i++) c.text[i] = Cell(r, "", widths[i - 1]);
            return c;
        }

        private static void Set(TMP_Text t, string text)
        {
            if (t.text != text) t.text = text;
        }

        // Column widths shared by the header and every row, in the order Damage, Share, Per second, Hits, Crit, Biggest,
        // Shield/armor/hull, Kills.
        private static readonly float[] ColumnWidths = { 80f, 46f, 72f, 50f, 44f, 66f, 120f, 40f };

        // A header that doesn't fit at a large text size shrinks instead of ending in "...".
        private static TMP_Text Head(RectTransform row, string text, float width)
        {
            var t = Kit.Fixed(row, text, width, TextAlignmentOptions.MidlineRight, ColorHelper.poiTooltipMuted);
            t.enableAutoSizing = true;
            t.fontSizeMin = 8f;
            t.fontSizeMax = 12f;
            return t;
        }

        private static TMP_Text Cell(RectTransform row, string text, float width) =>
            Kit.Fixed(row, text, width, TextAlignmentOptions.MidlineRight, ColorHelper.offWhite);
    }
}
