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

    // Rebuilt once a second while something new was recorded.
    internal sealed class CombatTable : MonoBehaviour
    {
        private int version = -1;
        private float next;
        internal int RowCount { get; private set; }

        private void Update()
        {
            if (Time.unscaledTime < next || version == CombatLog.Version) return;
            next = Time.unscaledTime + 1f;
            version = CombatLog.Version;
            Rebuild();
        }

        private void Rebuild()
        {
            var column = (RectTransform)transform;
            for (int i = column.childCount - 1; i >= 0; i--)
            {
                var child = column.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            RowCount = 0;
            var fight = Selected();
            if (fight == null || fight.dealt + fight.taken <= 0)
            {
                Kit.Muted(column, "Nothing recorded yet. Hits by and on your ships, drones and fighters show up here.");
                return;
            }
            float secs = fight.Duration;
            Kit.Label(column, $"{FormatString.FormatTime(secs)} of fighting. Dealt {GameMath.FormatNumber((float)fight.dealt)} ({GameMath.FormatNumber((float)(fight.dealt / secs))}/s), taken {GameMath.FormatNumber((float)fight.taken)} ({GameMath.FormatNumber((float)(fight.taken / secs))}/s), kills {fight.kills}.");

            bool dealtView = view <= View.DealtToTarget;
            var header = Kit.Row(column, 6f, 20f);
            Kit.Label(header, CombatLog.ViewName(view)).color = ColorHelper.stationCardTitle;
            Head(header, "Damage", 80f);
            Head(header, "Share", 46f);
            Head(header, "Per s", 64f);
            Head(header, "Hits", 50f);
            Head(header, "Crit", 44f);
            Head(header, "Biggest", 66f);
            Kit.Tooltip(Head(header, "Shd/Arm/Hull", 104f), "Shield / armour / hull", "Of each row's damage, the percent absorbed by shields, absorbed by armour, and taken off the hull.");
            if (dealtView) Head(header, "Kills", 40f);

            double whole = dealtView ? fight.dealt : fight.taken;
            var rows = CombatLog.Sorted(fight, view);
            for (int i = 0; i < rows.Count && i < MaxRows; i++)
            {
                var s = rows[i];
                var r = Kit.Row(column, 6f, 20f);
                var name = Kit.Label(r, s.label);
                name.overflowMode = TextOverflowModes.Ellipsis;
                Cell(r, GameMath.FormatNumber((float)s.total), 80f);
                Cell(r, CombatLog.Pct(s.total, whole) + "%", 46f);
                Cell(r, GameMath.FormatNumber((float)(s.total / secs)), 64f);
                Cell(r, s.hits.ToString(), 50f);
                Cell(r, CombatLog.Pct(s.crits, s.hits) + "%", 44f);
                Cell(r, GameMath.FormatNumber((float)s.max), 66f);
                Cell(r, $"{CombatLog.Pct(s.shield, s.total)}/{CombatLog.Pct(s.armor, s.total)}/{CombatLog.Pct(s.hull, s.total)}", 104f);
                if (dealtView) Cell(r, s.kills > 0 ? s.kills.ToString() : "", 40f);
                RowCount++;
            }
            if (rows.Count > MaxRows) Kit.Muted(column, $"{rows.Count - MaxRows} smaller rows not shown.");
        }

        private static TMP_Text Head(RectTransform row, string text, float width) =>
            Kit.Fixed(row, text, width, TextAlignmentOptions.MidlineRight, ColorHelper.poiTooltipMuted);

        private static void Cell(RectTransform row, string text, float width) =>
            Kit.Fixed(row, text, width, TextAlignmentOptions.MidlineRight, ColorHelper.offWhite);
    }
}
