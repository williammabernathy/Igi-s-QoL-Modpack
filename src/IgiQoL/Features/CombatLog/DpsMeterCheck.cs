using System.Collections;
using System.Linq;
using System.Collections.Generic;
using IgiQoL.Core;
using IgiQoL.UI;
using TMPro;
using UnityEngine;

namespace IgiQoL.Features.CombatLog;

internal static class DpsMeterCheck
{
    private const string Kind = DpsMeter.KindId;

    internal static IEnumerator Run(SelfCheckContext ctx)
    {
        // Meters the player already has: their layouts and the list go back as they were.
        string listPref = PopoutState.Pref(Kind) + ".list";
        string savedList = PlayerPrefs.GetString(listPref, "");
        var existing = new List<string>(Popouts.Windows(Kind));
        var savedLayouts = new Dictionary<string, string>();
        var wasOpen = new List<string>();
        foreach (var id in existing)
        {
            savedLayouts[id] = PlayerPrefs.GetString(PopoutState.Pref(id), "");
            if (Popouts.IsOpen(id)) wasOpen.Add(id);
        }
        var created = new List<string>();
        ctx.Cleanup(() =>
        {
            foreach (var id in created) Popouts.Remove(id);
            Popouts.CloseAll(Kind);
            PlayerPrefs.SetString(listPref, savedList);
            foreach (var kv in savedLayouts) PlayerPrefs.SetString(PopoutState.Pref(kv.Key), kv.Value);
            Popouts.ForgetState(Kind);
            foreach (var id in existing) Popouts.ForgetState(id);
            foreach (var id in wasOpen) Popouts.Open(id);
        });

        yield return SelfCheckContext.EnsureInSpace();
        Popouts.CloseAll(Kind);

        string first = DpsMeter.AddPreset(0);
        string second = DpsMeter.AddPreset(1);
        if (first != null) created.Add(first);
        if (second != null) created.Add(second);
        var a = first != null ? Popouts.Get(first) : null;
        var b = second != null ? Popouts.Get(second) : null;
        string ids = $"{first} / {second}";
        if (a != null && b != null && first != second && Popouts.Windows(Kind).Contains(first) && Popouts.Windows(Kind).Contains(second))
            ctx.Pass("meters-open", ids);
        else
        {
            ctx.Fail("meters-open", "the meters did not open: " + ids);
            yield break;
        }

        string titleA = TitleText(a), titleB = TitleText(b);
        string detail = $"'{titleA}' and '{titleB}'";
        if (titleA == "DPS: Dealt by ship" && titleB == "DPS: Taken by ship") ctx.Pass("meters-title", detail);
        else ctx.Fail("meters-title", detail);

        // Settings are saved with each window and the list names both.
        var kind = Popouts.Find(Kind);
        var reloadedA = PopoutState.Load(kind, first);
        var reloadedB = PopoutState.Load(kind, second);
        string listText = PlayerPrefs.GetString(listPref, "");
        string savedDetail = $"list={listText} a={reloadedA.options["view"].AsString}/{reloadedA.options["bar"].AsString} b={reloadedB.options["view"].AsString}/{reloadedB.options["bar"].AsString}";
        if (listText.Contains(first) && listText.Contains(second) && reloadedA.open && reloadedB.open
            && reloadedA.options["view"].AsString == "DealtByShip" && reloadedB.options["view"].AsString == "TakenByShip"
            && reloadedA.options["bar"].AsString == "Plain" && reloadedB.options["bar"].AsString == "Split")
            ctx.Pass("meters-saved", savedDetail);
        else ctx.Fail("meters-saved", savedDetail);

        // Side by side.
        var canvas = ((RectTransform)a.transform.parent).rect.size;
        var ra = (RectTransform)a.transform;
        var rb = (RectTransform)b.transform;
        ra.anchoredPosition = new Vector2(-canvas.x / 2f + 20f, canvas.y / 2f - 140f);
        rb.anchoredPosition = new Vector2(ra.anchoredPosition.x + ra.sizeDelta.x + 12f, ra.anchoredPosition.y);
        a.Persist();
        b.Persist();

        yield return new WaitForSeconds(1.2f);
        var listA = a.Body.GetComponentInChildren<MeterList>();
        var listB = b.Body.GetComponentInChildren<MeterList>();
        listA?.Tick();
        listB?.Tick();
        if (CombatLog.Fights.Count == 0 || CombatLog.Fights[0].dealt + CombatLog.Fights[0].taken <= 0)
            ctx.Skip("meters-rows", "no damage recorded in the newest fight");
        else
        {
            string rowsDetail = $"dealt rows={listA?.RowCount} top={listA?.TopFraction:0.00} '{listA?.TopNumber}'; taken rows={listB?.RowCount} top={listB?.TopFraction:0.00} '{listB?.TopNumber}'";
            bool dealt = CombatLog.Fights[0].dealt <= 0 || (listA != null && listA.RowCount > 0 && Mathf.Approximately(listA.TopFraction, 1f));
            bool taken = CombatLog.Fights[0].taken <= 0 || (listB != null && listB.RowCount > 0 && Mathf.Approximately(listB.TopFraction, 1f));
            if (dealt && taken) ctx.Pass("meters-rows", rowsDetail);
            else ctx.Fail("meters-rows", rowsDetail);
        }
        yield return ctx.Screenshot("dps-meters");

        // A number change redraws the bars without rebuilding; a view change rebuilds, and the title follows.
        if (listA != null && listA.RowCount > 0)
        {
            a.State.options["number"] = MeterNumber.Share.ToString();
            a.State.Save(first);
            listA.Apply();
            listA.Tick();
            if (listA.TopNumber.EndsWith("%")) ctx.Pass("meters-number", listA.TopNumber);
            else ctx.Fail("meters-number", "top number '" + listA.TopNumber + "'");
        }
        else ctx.Skip("meters-number", "no rows to change");

        b.State.options["view"] = View.DealtByWeapon.ToString();
        b.State.Save(second);
        Popouts.Rebuild(second);
        yield return null;
        var rebuilt = Popouts.Get(second);
        string rebuiltTitle = rebuilt != null ? TitleText(rebuilt) : "";
        if (rebuilt != null && rebuilt != b && rebuiltTitle == "DPS: Dealt by weapon") ctx.Pass("meters-rebuild", rebuiltTitle);
        else ctx.Fail("meters-rebuild", "title '" + rebuiltTitle + "'");

        Popouts.Remove(second);
        created.Remove(second);
        bool gone = !Popouts.Windows(Kind).Contains(second) && !Popouts.IsOpen(second)
            && PlayerPrefs.GetString(PopoutState.Pref(second), "").Length == 0 && Popouts.Windows(Kind).Contains(first);
        if (gone) ctx.Pass("meters-remove", $"{second} dropped, {first} kept");
        else ctx.Fail("meters-remove", $"{second} still listed or open; windows={string.Join(",", Popouts.Windows(Kind))}");
    }

    // The first text in the header is the window's title.
    private static string TitleText(Popout p)
    {
        var header = p.transform.Find("Frame/Header");
        var text = header != null ? header.GetComponentInChildren<TMP_Text>() : null;
        return text != null ? text.text : "";
    }
}
