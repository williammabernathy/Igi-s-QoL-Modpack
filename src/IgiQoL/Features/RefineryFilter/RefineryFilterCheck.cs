using System.Collections;
using System.Collections.Generic;
using System.Text;
using Behaviour.Item;
using Behaviour.Managers;
using Behaviour.Mining;
using Behaviour.UI.Refinery;
using Behaviour.UI.Spacestation;
using IgiQoL.Core;
using IgiQoL.Features.CraftRollup;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.Item;
using Source.Mining;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IgiQoL.Features.RefineryFilter;

// Puts two test ores in the station's material storage (one yields the test material, one doesn't), opens the
// refinery screen and clicks its material badges through the event system, as a mouse click would.
internal static class RefineryFilterCheck
{
    private const int TestAmount = 5;

    public static IEnumerator Run(SelfCheckContext ctx)
    {
        yield return ctx.EnsureDocked(s => s.HasFacility(SpaceStationFacility.Refinery));
        var interior = SpaceStationInterior.instance;
        var station = MapPointOfInterest.current as SpaceStation;
        if (interior == null || station == null || station.refinery == null)
        {
            ctx.Skip("filter", ctx.DockNote ?? "not docked at a station with a refinery: " + MapPointOfInterest.current?.name);
            yield break;
        }

        // Two ores that yield the material, the better one later in the game's own order (item level first) so the
        // filter has to re-sort them, and one ore that doesn't yield it.
        OreItemData low = null, high = null, other = null;
        RefinedMaterial material = default;
        foreach (var a in OreItemData.regularOres)
        {
            foreach (var b in OreItemData.regularOres)
            {
                if (b.item.itemLevel <= a.item.itemLevel) continue;
                foreach (var c in a.contents)
                {
                    float ya = Rollups.YieldOf(a, c.product);
                    if (ya > 0f && Rollups.YieldOf(b, c.product) > ya) { low = a; high = b; material = c.product; break; }
                }
                if (low != null) break;
            }
            if (low != null) break;
        }
        if (low != null)
            foreach (var o in OreItemData.regularOres)
                if (Rollups.YieldOf(o, material) <= 0f) { other = o; break; }
        if (low == null || other == null)
        {
            ctx.Skip("filter", "no two ores that yield a material in reverse item-level order plus one that doesn't");
            yield break;
        }

        var storage = station.materialStorage;
        var refinery = station.refinery;
        bool autoRefine = refinery.autoRefine;
        refinery.autoRefine = false;
        var testOres = new[] { low, high, other };
        var added = new int[testOres.Length];
        for (int i = 0; i < testOres.Length; i++) added[i] = Add(storage, testOres[i].item);
        try
        {
            if (System.Array.IndexOf(added, 0) >= 0)
            {
                ctx.Skip("filter", $"material storage at {station.name} took {added[0]} {low.item.identifier}, {added[1]} {high.item.identifier} and {added[2]} {other.item.identifier}");
                yield break;
            }

            yield return Open(interior);
            var ui = RefineryUI.current;
            if (ui == null)
            {
                ctx.Fail("filter", "refinery screen did not open");
                yield break;
            }
            var contents = ui.GetComponentInChildren<RefineryJobTabContents>();
            var clicks = ui.GetComponentsInChildren<MaterialFilterClick>();
            var target = Find(clicks, material);
            int materials = System.Enum.GetValues(typeof(RefinedMaterial)).Length;
            if (clicks.Length != materials || target == null)
            {
                ctx.Fail("filter", $"{clicks.Length} of {materials} material badges are clickable, {material} {(target == null ? "missing" : "present")}");
                yield break;
            }

            // A real click: raycast at the badge's centre and send the click to what was hit.
            var hit = RaycastAt((RectTransform)target.transform);
            if (hit == null || hit.GetComponentInParent<MaterialFilterClick>() != target)
            {
                ctx.Fail("raycast", $"a click on {material} hits {(hit == null ? "nothing" : hit.name)}");
                Click(target);
            }
            else
            {
                ctx.Pass("raycast", $"a click on {material} hits {hit.name}");
                ExecuteEvents.ExecuteHierarchy(hit, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            }
            yield return null;
            Verify(ctx, "filter", contents, clicks, material, low, high, other);

            string tip = target.GetComponent<RefineryMaterialBadge>().GetTooltipText();
            if (tip.Contains("Click to show all ore again.")) ctx.Pass("tooltip", tip.Replace("\n", " | "));
            else ctx.Fail("tooltip", tip.Replace("\n", " | "));
            yield return ctx.Screenshot("refinery-filter");

            ui.UpdateContent();
            yield return null;
            Verify(ctx, "rebuild", contents, clicks, material, low, high, other);

            yield return CheckNone(ctx, refinery, contents, clicks);

            // Clicking the active material again shows every ore, undimmed.
            if (RefineryFilter.Filter != null) Click(Find(clicks, RefineryFilter.Filter.Value));
            yield return null;
            int shown = Active(contents).Count, expected = DistinctOre(refinery);
            bool undimmed = true;
            foreach (var c in clicks) undimmed &= Alpha(c) == 1f;
            string clearDetail = $"filter={RefineryFilter.Filter?.ToString() ?? "none"} {shown} of {expected} ores shown, undimmed={undimmed}, empty label={EmptyLabel(contents) != null}";
            if (RefineryFilter.Filter == null && shown == expected && undimmed && EmptyLabel(contents) == null) ctx.Pass("clear", clearDetail);
            else ctx.Fail("clear", clearDetail);

            // Leaving the refinery and coming back starts unfiltered.
            Click(target);
            yield return Open(interior);
            ui = RefineryUI.current;
            contents = ui == null ? null : ui.GetComponentInChildren<RefineryJobTabContents>();
            shown = contents == null ? -1 : Active(contents).Count;
            string reopenDetail = $"filter={RefineryFilter.Filter?.ToString() ?? "none"} {shown} of {expected} ores shown";
            if (RefineryFilter.Filter == null && shown == expected) ctx.Pass("reopen", reopenDetail);
            else ctx.Fail("reopen", reopenDetail);
        }
        finally
        {
            RefineryFilter.Filter = null;
            for (int i = 0; i < testOres.Length; i++)
                if (added[i] > 0) storage.Remove(testOres[i].item, Mathf.Min(added[i], storage.GetCount(testOres[i].item)));
            refinery.autoRefine = autoRefine;
            if (SpaceStationInterior.instance != null) SpaceStationInterior.instance.GoToLocation(SpaceStationFacility.Airlock);
        }
    }

    // A material no ore at the station yields shows the "no ore here" line instead of an empty row.
    private static IEnumerator CheckNone(SelfCheckContext ctx, Refinery refinery, RefineryJobTabContents contents, MaterialFilterClick[] clicks)
    {
        MaterialFilterClick none = null;
        foreach (var c in clicks)
        {
            bool any = false;
            foreach (var kv in refinery.GetAvailableItems())
                if (Rollups.YieldOf(kv.Key, c.material) > 0f) { any = true; break; }
            if (!any) { none = c; break; }
        }
        if (none == null)
        {
            ctx.Skip("none", "ore at this station yields every material");
            yield break;
        }
        Click(none);
        yield return null;
        var label = EmptyLabel(contents);
        int shown = Active(contents).Count;
        string detail = $"{none.material}: {shown} ores shown, label='{(label == null ? "none" : label.text)}'";
        if (shown == 0 && label != null && label.text.Contains(none.material.GetDisplayName())) ctx.Pass("none", detail);
        else ctx.Fail("none", detail);
        yield return ctx.Screenshot("refinery-filter-none");
    }

    private static void Verify(SelfCheckContext ctx, string name, RefineryJobTabContents contents, MaterialFilterClick[] clicks,
        RefinedMaterial material, OreItemData low, OreItemData high, OreItemData other)
    {
        var shown = Active(contents);
        var selectedOre = contents.selectedOre;
        var detail = new StringBuilder($"filter={RefineryFilter.Filter?.ToString() ?? "none"} selected={(selectedOre != null ? selectedOre.item.identifier : null)}:");
        bool ok = RefineryFilter.Filter == material && contents.selectedOre != null && Rollups.YieldOf(contents.selectedOre, material) > 0f;
        bool hasLow = false, hasHigh = false, hasOther = false;
        float last = float.MaxValue;
        for (int i = 0; i < shown.Count; i++)
        {
            var ore = shown[i].ore;
            float y = Rollups.YieldOf(ore, material);
            float x = ((RectTransform)shown[i].transform).anchoredPosition.x;
            detail.Append($" {ore.item.identifier} {y:0.##}@{x:0}");
            if (y <= 0f || y > last || !Mathf.Approximately(x, i * 68f)) ok = false;
            last = y;
            hasLow |= ore == low;
            hasHigh |= ore == high;
            hasOther |= ore == other;
        }
        foreach (var c in clicks)
            if (Alpha(c) != (c.material == material ? 1f : RefineryFilter.DimmedAlpha)) { ok = false; detail.Append($" {c.material} alpha {Alpha(c)}"); }
        if (!hasLow || !hasHigh || hasOther) ok = false;
        detail.Append($" (test ores: {low.item.identifier} shown={hasLow}, {high.item.identifier} shown={hasHigh}, {other.item.identifier} shown={hasOther})");
        if (ok) ctx.Pass(name, detail.ToString());
        else ctx.Fail(name, detail.ToString());
    }

    private static IEnumerator Open(SpaceStationInterior interior)
    {
        var before = RefineryUI.current;
        interior.GoToLocation(SpaceStationFacility.Refinery);
        float until = Time.realtimeSinceStartup + 10f;
        while ((RefineryUI.current == null || RefineryUI.current == before) && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSeconds(1f);
    }

    private static int Add(Inventory storage, InventoryItemType item)
    {
        int before = storage.GetCount(item);
        storage.Add(item, TestAmount);
        return storage.GetCount(item) - before;
    }

    private static void Click(MaterialFilterClick c) =>
        c.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });

    private static MaterialFilterClick Find(MaterialFilterClick[] clicks, RefinedMaterial material)
    {
        foreach (var c in clicks)
            if (c.material == material) return c;
        return null;
    }

    private static GameObject RaycastAt(RectTransform rt)
    {
        var canvas = rt.GetComponentInParent<Canvas>().rootCanvas;
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var ped = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center)) };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        return results.Count == 0 ? null : results[0].gameObject;
    }

    // Ore badges the row shows, left to right; filtered-out badges and the ones the game is destroying are inactive.
    private static List<RefineryOreBadge> Active(RefineryJobTabContents contents)
    {
        var list = new List<RefineryOreBadge>(contents.GetComponentsInChildren<RefineryOreBadge>());
        list.Sort((a, b) => ((RectTransform)a.transform).anchoredPosition.x.CompareTo(((RectTransform)b.transform).anchoredPosition.x));
        return list;
    }

    private static TMP_Text EmptyLabel(RefineryJobTabContents contents)
    {
        foreach (var t in contents.GetComponentsInChildren<TMP_Text>())
            if (t.name == RefineryFilter.EmptyLabelName) return t;
        return null;
    }

    private static int DistinctOre(Refinery refinery)
    {
        var seen = new HashSet<OreItemData>();
        foreach (var kv in refinery.GetAvailableItems()) seen.Add(kv.Key);
        return seen.Count;
    }

    private static float Alpha(MaterialFilterClick c) => c.TryGetComponent<CanvasGroup>(out var g) ? g.alpha : 1f;
}
