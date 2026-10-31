using System;
using System.Collections;
using System.Text;
using Behaviour.UI.HUD.Fleet;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using Source.SpaceShip;
using Source.Util;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.FleetVitals;

public sealed class FleetVitalsFeature : Feature
{
    public static FleetVitalsFeature Instance;

    public FleetVitalsFeature() { Instance = this; }

    public override string Id => "FleetVitals";
    public override string Title => "Fleet shield and armor bars";
    public override string Description => "Shows a shield bar above and an armor bar below each ship's hull bar in the fleet panel (bottom left). A ship without shields or armor leaves that bar's slot blank.";
    public override Type[] PatchClasses => new[] { typeof(FleetPanelRowPatches) };

    public ConfigEntry<int> BarHeight;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        BarHeight = cfg.Bind(Id, "BarHeight", 4, SettingInfo.Describe("Bar height (px)",
            "Height of each of the three bars. 4 matches the game's own hull bar; larger bars are easier to read and make each fleet row taller. Applies at once.",
            new AcceptableValueRange<int>(2, 12)));
    }

    public override void OnActivated()
    {
        BarHeight.SettingChanged += (_, _) =>
        {
            foreach (var v in Object.FindObjectsByType<FleetVitals>(FindObjectsInactive.Include)) v.Layout();
        };
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        CheckLayout(ctx);
        var rows = Object.FindObjectsByType<FleetPanelRow>(FindObjectsInactive.Include);
        if (rows.Length == 0)
        {
            ctx.Skip("rows", "no fleet rows in this save");
            yield break;
        }
        var sb = new StringBuilder();
        bool ok = true;
        foreach (var row in rows)
        {
            var v = row.GetComponent<FleetVitals>();
            var d = row.shipData;
            if (v == null || v.shieldFill == null || v.armorFill == null) ok = false;
            sb.Append(row.name).Append(' ').Append(d?.GetShipName())
              .Append($" shield={d?.currentShieldHP:0}/{d?.maxShieldHP:0} armor={d?.currentArmorHP:0}/{d?.maxArmorHP:0}; ");
        }
        if (ok) ctx.Pass("rows", sb.ToString());
        else ctx.Fail("rows", "bars missing: " + sb);
        CheckHeightChange(ctx, rows);
    }

    // Changing the setting re-stacks the live rows at once: each bar takes the new height, the row grows by three
    // times the difference, and FleetStrip's tiles move down with it.
    private void CheckHeightChange(SelfCheckContext ctx, FleetPanelRow[] rows)
    {
        FleetVitals v = null;
        foreach (var r in rows)
            if ((v = r.GetComponent<FleetVitals>()) != null && v.shieldGo != null) break;
        if (v == null || v.shieldGo == null)
        {
            ctx.Skip("bar-height", "no fleet row with bars");
            return;
        }
        var row = v.GetComponent<FleetPanelRow>();
        var le = row.GetComponent<LayoutElement>();
        var strip = row.transform.Find(FleetRowLayout.StripName) as RectTransform;
        int saved = BarHeight.Value;
        float height0 = le.preferredHeight, strip0 = strip != null ? strip.anchoredPosition.y : 0f;
        int next = saved + 4 <= 12 ? saved + 4 : saved - 2;
        try
        {
            BarHeight.Value = next;
            var hull = FleetRowLayout.HullBar(row);
            float grew = le.preferredHeight - height0;
            float stripMoved = strip != null ? strip0 - strip.anchoredPosition.y : 3f * (next - saved);
            string detail = $"{saved} -> {next} px: hull bar {hull.rect.height:0.#} px, armor {((RectTransform)v.armorGo.transform).rect.height:0.#} px, row {height0:0.#} -> {le.preferredHeight:0.#}, strip moved {stripMoved:0.#}";
            if (Mathf.Approximately(hull.rect.height, next) && Mathf.Approximately(grew, 3f * (next - saved)) && Mathf.Approximately(stripMoved, grew))
                ctx.Pass("bar-height", detail);
            else ctx.Fail("bar-height", detail);
        }
        finally
        {
            BarHeight.Value = saved;
        }
    }

    // Builds the bars on a detached copy of the row prefab and checks they stack inside the row.
    private static void CheckLayout(SelfCheckContext ctx)
    {
        FleetPanelRow prefab = null;
        foreach (var panel in Resources.FindObjectsOfTypeAll<FleetPanel>())
            if (panel.rowPrefab != null) { prefab = panel.rowPrefab; break; }
        if (prefab == null)
        {
            ctx.Skip("layout", "no FleetPanel loaded");
            return;
        }
        var row = Object.Instantiate(prefab);
        try
        {
            var v = row.gameObject.AddComponent<FleetVitals>();
            v.Build(row);
            var hull = FleetRowLayout.HullBar(row);
            var shield = (RectTransform)v.shieldGo.transform;
            var armor = (RectTransform)v.armorGo.transform;
            float rowHeight = row.GetComponent<LayoutElement>().preferredHeight;
            float h = v.builtHeight;
            string detail = $"bar height {h:0}: shield {FleetRowLayout.Top(shield):0.#}..{FleetRowLayout.Bottom(shield):0.#}, hull {FleetRowLayout.Top(hull):0.#}..{FleetRowLayout.Bottom(hull):0.#}, armor {FleetRowLayout.Top(armor):0.#}..{FleetRowLayout.Bottom(armor):0.#}, w={shield.rect.width:0.#}/{hull.rect.width:0.#}/{armor.rect.width:0.#}, row height {rowHeight:0.#}";
            bool ok = Mathf.Approximately(FleetRowLayout.Bottom(shield) - FleetVitals.Gap, FleetRowLayout.Top(hull))
                && Mathf.Approximately(FleetRowLayout.Bottom(hull) - FleetVitals.Gap, FleetRowLayout.Top(armor))
                && Mathf.Approximately(shield.rect.height, h) && Mathf.Approximately(hull.rect.height, h)
                && Mathf.Approximately(armor.rect.height, h) && FleetRowLayout.Bottom(armor) >= -rowHeight
                && Mathf.Approximately(shield.rect.width, hull.rect.width) && Mathf.Approximately(armor.rect.width, hull.rect.width);
            if (ok) ctx.Pass("layout", detail);
            else ctx.Fail("layout", detail);
        }
        finally { Object.DestroyImmediate(row.gameObject); }
    }
}

public sealed class FleetVitals : MonoBehaviour
{
    // The hull bar becomes the middle of three equal bars: shield, hull, armor.
    public const float Gap = 1f;

    public Image shieldFill, armorFill;
    public GameObject shieldGo, armorGo;
    public float builtHeight;

    private static Sprite Solid;
    private FleetPanelRow row;
    private float hullTop, hullBottom, grown;

    public void Build(FleetPanelRow owner)
    {
        if (shieldGo != null)
        {
            if (builtHeight != FleetVitalsFeature.Instance.BarHeight.Value) Layout();
            return;
        }
        row = owner;
        var src = row.hullBarFill;
        var hull = FleetRowLayout.HullBar(row);
        bool cloneParent = hull != src.transform;
        int fillIndex = src.transform.GetSiblingIndex();
        // The prefab fills use a bordered panel sprite that is mostly dark at 4 px tall; a plain sprite shows the colour.
        // Filled images need a sprite to honour fillAmount, so null would draw a full bar.
        if (Solid == null)
        {
            var white = Texture2D.whiteTexture;
            Solid = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height), new Vector2(0.5f, 0.5f));
            Solid.hideFlags = HideFlags.DontUnloadUnusedAsset;
        }
        src.sprite = Solid;
        hullTop = FleetRowLayout.Top(hull);
        hullBottom = FleetRowLayout.Bottom(hull);
        shieldFill = MakeBar(hull, cloneParent, fillIndex, FleetRowLayout.ShieldName, ColorHelper.shieldColor, out shieldGo);
        armorFill = MakeBar(hull, cloneParent, fillIndex, FleetRowLayout.ArmorName, ColorHelper.armorColor, out armorGo);
        Layout();
    }

    // Stacks shield, hull and armor from the hull bar's original top at the configured height and grows (or shrinks)
    // the row by the difference from the last layout.
    public void Layout()
    {
        if (row == null || shieldGo == null) return;
        float h = FleetVitalsFeature.Instance.BarHeight.Value;
        Place((RectTransform)shieldGo.transform, hullTop, h);
        Place(FleetRowLayout.HullBar(row), hullTop - (h + Gap), h);
        Place((RectTransform)armorGo.transform, hullTop - 2f * (h + Gap), h);
        builtHeight = h;

        float grow = hullBottom - FleetRowLayout.Bottom((RectTransform)armorGo.transform);
        float delta = grow - grown;
        grown = grow;
        if (delta == 0f) return;
        row.GetComponent<LayoutElement>().preferredHeight += delta;
        row.mercContract.collapsedHeight += delta;
        // FleetStrip may already have placed its tiles under the bars.
        if (row.transform.Find(FleetRowLayout.StripName) is RectTransform strip)
            strip.anchoredPosition -= new Vector2(0f, delta);
    }

    private static Image MakeBar(RectTransform hull, bool cloneParent, int fillIndex, string name, Color color, out GameObject go)
    {
        go = Object.Instantiate(hull.gameObject, hull.parent);
        go.name = name;
        foreach (var img in go.GetComponentsInChildren<Image>(true)) img.raycastTarget = false;
        var fill = cloneParent ? go.transform.GetChild(fillIndex).GetComponent<Image>() : go.GetComponent<Image>();
        fill.color = color;
        return fill;
    }

    // The hull bar uses top anchors without vertical stretch, so sizeDelta.y is its height.
    private static void Place(RectTransform rt, float top, float height)
    {
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, top - height * (1f - rt.pivot.y));
    }

    public void Refresh(SpaceShipData d)
    {
        if (shieldGo == null || d == null) return;
        Set(shieldGo, shieldFill, d.currentShieldHP, d.maxShieldHP);
        Set(armorGo, armorFill, d.currentArmorHP, d.maxArmorHP);
    }

    private static void Set(GameObject go, Image fill, float cur, float max)
    {
        bool visible = max > 0f;
        if (go.activeSelf != visible) go.SetActive(visible);
        if (visible) fill.fillAmount = cur < 0f ? 1f : Mathf.Clamp01(cur / max);
    }
}

[HarmonyPatch]
internal static class FleetPanelRowPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FleetPanelRow), nameof(FleetPanelRow.Bind))]
    private static void BindPostfix(FleetPanelRow __instance)
    {
        if (!FleetVitalsFeature.Instance.Active) return;
        try
        {
            var v = __instance.GetComponent<FleetVitals>();
            if (v == null) v = __instance.gameObject.AddComponent<FleetVitals>();
            v.Build(__instance);
            v.Refresh(__instance.shipData);
        }
        catch (Exception e) { FleetVitalsFeature.Instance.Fail(e, nameof(FleetPanelRowPatches)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FleetPanelRow), nameof(FleetPanelRow.Refresh))]
    private static void RefreshPostfix(FleetPanelRow __instance)
    {
        if (!FleetVitalsFeature.Instance.Active) return;
        try
        {
            var v = __instance.GetComponent<FleetVitals>();
            if (v != null) v.Refresh(__instance.shipData);
        }
        catch (Exception e) { FleetVitalsFeature.Instance.Fail(e, nameof(FleetPanelRowPatches)); }
    }
}
