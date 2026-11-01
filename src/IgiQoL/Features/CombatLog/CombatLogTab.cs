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
        Kit.Muted(content, "Damage between your side (ships, drones, fighters) and everyone else, as it landed: shield and armour absorbed plus hull lost, after resistances. Per second figures use the time spent fighting.");

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
        Kit.TextButton(row, "Clear", () => { CombatLog.Reset(); scope = 0; ModWindow.Show("combat"); }, 70f);
        var overview = CombatOverview.CombatOverview.Instance;
        if (overview != null && overview.Active)
            Kit.TextButton(row, Popouts.IsOpen(CombatOverview.CombatOverview.PopoutId) ? "Close combat overview" : "Combat overview", () =>
            {
                Popouts.Toggle(CombatOverview.CombatOverview.PopoutId);
                ModWindow.Show("combat");
            }, 170f);

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
        private RectTransform header;
        private readonly List<Cells> pool = new();
        internal int RowCount { get; private set; }

        private void Update()
        {
            if (Time.unscaledTime < next || version == CombatLog.Version) return;
            next = Time.unscaledTime + 1f;
            version = CombatLog.Version;
            Refresh();
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
            if (summary == null) summary = Kit.Label(column, "");
            Set(summary, $"{FormatString.FormatTime(secs)} of fighting. Dealt {GameMath.FormatNumber((float)fight.dealt)} ({GameMath.FormatNumber((float)(fight.dealt / secs))}/s), taken {GameMath.FormatNumber((float)fight.taken)} ({GameMath.FormatNumber((float)(fight.taken / secs))}/s), kills {fight.kills}.");

            bool dealtView = view <= View.DealtToTarget;
            if (header == null)
            {
                header = Kit.Row(column, 6f, 20f);
                Kit.Label(header, CombatLog.ViewName(view)).color = ColorHelper.stationCardTitle;
                Head(header, "Damage", 80f);
                Head(header, "Share", 46f);
                Head(header, "Per s", 64f);
                Head(header, "Hits", 50f);
                Head(header, "Crit", 44f);
                Head(header, "Biggest", 66f);
                Kit.Tooltip(Head(header, "Shd/Arm/Hull", 104f), "Shield / armour / hull", "Of each row's damage, the percent absorbed by shields, absorbed by armour, and taken off the hull.");
                if (dealtView) Head(header, "Kills", 40f);
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
                Set(t[1], GameMath.FormatNumber((float)s.total));
                Set(t[2], CombatLog.Pct(s.total, whole) + "%");
                Set(t[3], GameMath.FormatNumber((float)(s.total / secs)));
                Set(t[4], s.hits.ToString());
                Set(t[5], CombatLog.Pct(s.crits, s.hits) + "%");
                Set(t[6], GameMath.FormatNumber((float)s.max));
                Set(t[7], $"{CombatLog.Pct(s.shield, s.total)}/{CombatLog.Pct(s.armor, s.total)}/{CombatLog.Pct(s.hull, s.total)}");
                if (dealtView) Set(t[8], s.kills > 0 ? s.kills.ToString() : "");
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
            float[] widths = { 80f, 46f, 64f, 50f, 44f, 66f, 104f, 40f };
            for (int i = 1; i < c.text.Length; i++) c.text[i] = Cell(r, "", widths[i - 1]);
            return c;
        }

        private static void Set(TMP_Text t, string text)
        {
            if (t.text != text) t.text = text;
        }

        private static TMP_Text Head(RectTransform row, string text, float width) =>
            Kit.Fixed(row, text, width, TextAlignmentOptions.MidlineRight, ColorHelper.poiTooltipMuted);

        private static TMP_Text Cell(RectTransform row, string text, float width) =>
            Kit.Fixed(row, text, width, TextAlignmentOptions.MidlineRight, ColorHelper.offWhite);
    }
}
