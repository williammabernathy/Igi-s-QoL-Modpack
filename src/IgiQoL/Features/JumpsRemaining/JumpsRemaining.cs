using System;
using System.Collections;
using Behaviour.UI.Tooltip;
using Behaviour.UI.Travel;
using HarmonyLib;
using IgiQoL.Core;
using Source.Player;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.JumpsRemaining;

public sealed class JumpsRemaining : Feature
{
    public static JumpsRemaining Instance;

    public JumpsRemaining() { Instance = this; }

    public override string Id => "JumpsRemaining";
    public override string Title => "Jumps remaining";
    public override string Description => "Shows how many jumps are left to your destination on the travel panel.";
    public override Type[] PatchClasses => new[] { typeof(TravelInfoPatch) };

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        var info = TravelInfo.instance;
        JumpsLabel.Set(info, 3);
        var label = JumpsLabel.Label;
        bool ok = label != null && label.transform.parent == info.distanceText.transform.parent && label.text.EndsWith(": 3");
        string detail = label == null ? "label not created" : $"text='{label.text}' parent={label.transform.parent.name} pos={((RectTransform)label.transform).anchoredPosition}";
        if (ok) ctx.Pass("label", detail);
        else ctx.Fail("label", detail);
        info.ToggleVisible(true);
        ctx.Cleanup(() =>
        {
            info.ToggleVisible(false);
            if (JumpsLabel.Label != null) JumpsLabel.Label.gameObject.SetActive(false);
        });
        yield break;
    }
}

internal static class JumpsLabel
{
    public static TextMeshProUGUI Label;
    private static TravelInfo owner;
    private static string prefix;
    private static int shown = int.MinValue;

    public static void Set(TravelInfo info, int jumps)
    {
        if (Label == null || owner != info)
        {
            Create(info);
            shown = int.MinValue;
        }
        if (jumps != shown)
        {
            shown = jumps;
            Label.text = prefix + jumps;
        }
        bool visible = jumps > 0;
        if (Label.gameObject.activeSelf != visible) Label.gameObject.SetActive(visible);
    }

    private static void Create(TravelInfo info)
    {
        var src = info.distanceText;
        var go = Object.Instantiate(src.gameObject, src.transform.parent);
        go.name = "IgiQoL_Jumps";
        var tooltip = go.GetComponent<TooltipSource>();
        if (tooltip != null) Object.Destroy(tooltip);
        if (src.transform.parent.GetComponent<LayoutGroup>() != null)
        {
            go.transform.SetSiblingIndex(src.transform.GetSiblingIndex() + 1);
        }
        else
        {
            var srcRt = (RectTransform)src.transform;
            ((RectTransform)go.transform).anchoredPosition = srcRt.anchoredPosition + new Vector2(0f, -srcRt.rect.height);
        }
        Label = go.GetComponent<TextMeshProUGUI>();
        owner = info;
        prefix = Translation.Translate("@TravelJumps") + ": ";
    }
}

[HarmonyPatch(typeof(TravelInfo), nameof(TravelInfo.UpdateTravelInfo))]
internal static class TravelInfoPatch
{
    private static void Postfix(TravelInfo __instance)
    {
        if (!JumpsRemaining.Instance.Active) return;
        try
        {
            JumpsLabel.Set(__instance, GamePlayer.current.waypoints.Count - 1);
        }
        catch (Exception e) { JumpsRemaining.Instance.Fail(e, nameof(TravelInfoPatch)); }
    }
}
