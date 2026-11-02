using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Mining;
using Behaviour.UI.Refinery;
using Behaviour.UI.Shared;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Features.CraftRollup;
using Source.Item;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IgiQoL.Features.RefineryFilter;

public sealed class RefineryFilter : Feature
{
    public static RefineryFilter Instance;

    public RefineryFilter() { Instance = this; }

    public override string Id => "RefineryFilter";
    public override string Title => "Refinery filter";
    public override string Description => "Click a material in the refinery's left column to show only the ore that refines into it, best yield first. Click it again to show all ore.";
    public override Type[] PatchClasses => new[] { typeof(RefineryFilterPatches) };

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => RefineryFilterCheck.Run(ctx);

    internal const float DimmedAlpha = 0.4f;
    internal const string EmptyLabelName = "IgiQoL_RefineryFilterEmpty";

    // The material the open refinery screen is filtered to; null shows all ore. Each visit builds a new screen, which starts unfiltered.
    internal static RefinedMaterial? Filter;

    internal static void Toggle(RefinedMaterial material)
    {
        Filter = Filter == material ? null : material;
        var ui = RefineryUI.current;
        if (ui == null) return;
        foreach (var badge in ui.GetComponentsInChildren<MaterialFilterClick>()) badge.ShowFilter(Filter);
        ui.UpdateContent();
    }

    // Runs after the game rebuilt the ore row: hides ore without the material, packs the rest best yield first,
    // and moves the selection off a hidden ore so the amount and results don't describe something you can't see.
    internal static void Apply(RefineryJobTabContents contents, List<RefineryOreBadge> badges, RectTransform oreParent)
    {
        var material = Filter.Value;
        var shown = new List<RefineryOreBadge>();
        foreach (var badge in badges)
        {
            bool match = Rollups.YieldOf(badge.ore, material) > 0f;
            badge.gameObject.SetActive(match);
            if (match) shown.Add(badge);
        }
        shown.Sort((a, b) =>
        {
            int byYield = Rollups.YieldOf(b.ore, material).CompareTo(Rollups.YieldOf(a.ore, material));
            return byYield != 0 ? byYield : badges.IndexOf(a).CompareTo(badges.IndexOf(b));
        });

        float x = 0f;
        foreach (var badge in shown)
        {
            ((RectTransform)badge.transform).anchoredPosition = new Vector2(x, 0f);
            x += 68f;
        }

        if (shown.Count == 0)
        {
            var template = (RectTransform)badges[0].transform;
            var label = UiCards.AddLabel(oreParent, $"No ore here refines into {material.GetDisplayName()}. Click {material.GetDisplayName()} again to show all ore.", 12f, ColorHelper.poiTooltipMuted);
            label.name = EmptyLabelName;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var rt = label.rectTransform;
            rt.anchorMin = template.anchorMin;
            rt.anchorMax = template.anchorMax;
            rt.pivot = template.pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(Mathf.Max(label.preferredWidth, 200f), template.rect.height);
            x = rt.sizeDelta.x;
        }
        oreParent.sizeDelta = new Vector2(x, oreParent.sizeDelta.y);

        var selected = contents.selectedOre;
        if (shown.Count > 0 && (selected == null || Rollups.YieldOf(selected, material) <= 0f))
            contents.SetSelectedOre(shown[0].ore);
    }
}

// Added to each material badge in the refinery's left column; the game gives those badges no click action.
internal sealed class MaterialFilterClick : MonoBehaviour, IPointerClickHandler
{
    public RefinedMaterial material;
    private CanvasGroup group;

    public void OnPointerClick(PointerEventData eventData)
    {
        var f = RefineryFilter.Instance;
        if (!f.Active || eventData.button != PointerEventData.InputButton.Left) return;
        try { RefineryFilter.Toggle(material); }
        catch (Exception e) { f.Fail(e, nameof(MaterialFilterClick)); }
    }

    // Dims the other materials while a filter is on, so the active one stands out.
    public void ShowFilter(RefinedMaterial? filter)
    {
        if (group == null && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = filter == null || filter == material ? 1f : RefineryFilter.DimmedAlpha;
    }
}

[HarmonyPatch]
internal static class RefineryFilterPatches
{
    private static RefineryFilter F => RefineryFilter.Instance;

    // Awake already shows the ore row, so the last visit's filter has to be gone before it runs.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RefineryUI), "Awake")]
    private static void AwakePrefix() => RefineryFilter.Filter = null;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RefineryUI), "Awake")]
    private static void AwakePostfix(RectTransform ___materialsParent)
    {
        if (!F.Active) return;
        try
        {
            foreach (var badge in ___materialsParent.GetComponentsInChildren<RefineryMaterialBadge>())
                badge.gameObject.AddComponent<MaterialFilterClick>().material = badge.material;
        }
        catch (Exception e) { F.Fail(e, nameof(AwakePostfix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RefineryJobTabContents), nameof(RefineryJobTabContents.Show))]
    private static void ShowPostfix(RefineryJobTabContents __instance, List<RefineryOreBadge> ___oreBadges, RectTransform ___oreParent)
    {
        if (!F.Active || RefineryFilter.Filter == null || ___oreBadges == null || ___oreBadges.Count == 0) return;
        try { RefineryFilter.Apply(__instance, ___oreBadges, ___oreParent); }
        catch (Exception e) { F.Fail(e, nameof(ShowPostfix)); }
    }

    // The forge screen uses the same badge without the click, so only badges that have it get the hint.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RefineryMaterialBadge), nameof(RefineryMaterialBadge.GetTooltipText))]
    private static void GetTooltipTextPostfix(RefineryMaterialBadge __instance, ref string __result)
    {
        if (!F.Active) return;
        try
        {
            if (!__instance.TryGetComponent<MaterialFilterClick>(out var click)) return;
            __result += RefineryFilter.Filter == click.material
                ? "\n\nClick to show all ore again."
                : $"\n\nClick to show only ore that refines into {click.material.GetDisplayName()}.";
        }
        catch (Exception e) { F.Fail(e, nameof(GetTooltipTextPostfix)); }
    }
}
