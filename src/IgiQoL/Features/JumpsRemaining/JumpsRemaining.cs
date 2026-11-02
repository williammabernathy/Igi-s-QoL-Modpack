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
        if (info == null || GamePlayer.current == null) { ctx.Skip("label", "no travel panel"); yield break; }
        // Drive the real UpdateTravelInfo so the postfix reads the waypoint count. Panel visibility and the
        // waypoint list are put back before the check ends, so the panel never stays on screen.
        var waypoints = GamePlayer.current.waypoints;
        var savedWaypoints = waypoints.ToArray();
        bool wasVisible = info.visible;
        string text = null;
        string parentName = null;
        bool sameParent = false;
        bool active = false;
        Exception thrown = null;
        try
        {
            waypoints.Clear();
            waypoints.Add(null);
            waypoints.Add(null);
            waypoints.Add(null);
            info.ToggleVisible(true);
            info.UpdateTravelInfo(1f, 1f, 1f, 1f);
            var label = JumpsLabel.Label;
            if (label != null)
            {
                text = label.text;
                parentName = label.transform.parent.name;
                sameParent = label.transform.parent == info.distanceText.transform.parent;
                active = label.gameObject.activeSelf;
            }
        }
        catch (Exception e) { thrown = e; }
        finally
        {
            waypoints.Clear();
            waypoints.AddRange(savedWaypoints);
            info.ToggleVisible(wasVisible);
            if (!wasVisible && JumpsLabel.Label != null) JumpsLabel.Label.gameObject.SetActive(false);
        }
        string detail = thrown != null ? thrown.GetType().Name + ": " + thrown.Message : text == null ? "label not created" : $"text='{text}' parent={parentName} active={active}";
        // 3 waypoints is 2 jumps: the first one is where the fleet is.
        if (thrown == null && text != null && sameParent && active && text.EndsWith(": 2")) ctx.Pass("label", detail);
        else ctx.Fail("label", detail);
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
