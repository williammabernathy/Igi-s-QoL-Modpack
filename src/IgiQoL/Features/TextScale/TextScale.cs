using System;
using System.Collections;
using BepInEx.Configuration;
using Behaviour.UI.HUD;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.TextScale;

// The game's Scale setting resizes the whole canvas, text and panels together. This multiplies only the font size,
// and only while TextMeshPro lays text out, so every value the game reads or writes (fontSize, auto-size limits)
// stays the game's own and nothing compounds when the game copies or re-sets a size.
public sealed class TextScale : Feature
{
    public static TextScale Instance;

    public TextScale() { Instance = this; }

    public override string Id => "TextScale";
    public override string Title => "Text size";
    public override string Description => "Draws all text bigger or smaller without resizing the panels around it. The game's Scale setting (Settings > Graphics) still sizes panels and text together.";
    public override Type[] PatchClasses => new[] { typeof(TextScalePatches) };

    public ConfigEntry<int> Percent;

    // The size the feature last set; 1 is vanilla. Factor reads 1 once the feature is off.
    private static float current = 1f;
    internal static float Factor => Instance != null && Instance.Active ? current : 1f;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        Percent = cfg.Bind(Id, "Percent", 100, SettingInfo.Describe("Text size (%)",
            "100 is the game's own size. Applies at once. Panels keep their size, so bigger text can be cut off or run into the next line where a panel has no spare room.",
            new AcceptableValueRange<int>(50, 200)));
    }

    public override void OnActivated()
    {
        SetFactor(Percent.Value / 100f);
        Percent.SettingChanged += (_, _) =>
        {
            if (!Active) return;
            try
            {
                SetFactor(Percent.Value / 100f);
                // The mod window and the pop-outs size their columns for the text size when they're built.
                Popouts.RebuildAll();
                if (!ModWindow.IsOpen) return;
                string tab = ModWindow.CurrentTab;
                ModWindow.Close();
                ModWindow.Open(tab);
            }
            catch (Exception e) { Fail(e, nameof(Percent)); }
        };
    }

    // Every text lays itself out again at the new size.
    internal static void SetFactor(float factor)
    {
        if (Mathf.Approximately(factor, current)) return;
        current = factor;
        foreach (var t in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include))
        {
            t.havePropertiesChanged = true;
            t.SetLayoutDirty();
        }
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        int percent = Percent.Value;
        ctx.Cleanup(() =>
        {
            Percent.Value = percent;
            SetFactor(percent / 100f);
        });
        var canvas = HudManager.Instance != null && HudManager.Instance.dockButton != null ? HudManager.Instance.dockButton.GetComponentInParent<Canvas>(true) : null;
        var font = Object.FindAnyObjectByType<TextMeshProUGUI>();
        if (canvas == null || font == null)
        {
            ctx.Skip("layout", "no HUD canvas");
            yield break;
        }
        var go = new GameObject("IgiQoL_TextScaleCheck", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        try
        {
            text.font = font.font;
            text.fontSize = 12f;
            text.text = "Vanguard Galaxy";
            SetFactor(1f);
            float plain = text.preferredWidth;
            SetFactor(1.5f);
            float big = text.preferredWidth;
            text.ForceMeshUpdate();
            float drawn = text.textInfo.characterInfo[0].pointSize;
            string detail = $"width {plain:0.#} -> {big:0.#} at 150%, drawn point size {drawn:0.#}, fontSize still {text.fontSize:0.#}";
            if (plain > 0f && Mathf.Abs(big / plain - 1.5f) < 0.05f && Mathf.Approximately(drawn, 18f) && Mathf.Approximately(text.fontSize, 12f)) ctx.Pass("layout", detail);
            else ctx.Fail("layout", detail);
        }
        finally { Object.DestroyImmediate(go); }

        // Same path as the Settings tab: the value changes while the window is open on it, and the window rebuilds.
        ModWindow.Open("settings");
        Percent.Value = 130;
        string setDetail = $"factor={Factor:0.##} open={ModWindow.IsOpen} tab={ModWindow.CurrentTab}";
        if (Mathf.Approximately(Factor, 1.3f) && ModWindow.IsOpen && ModWindow.CurrentTab == "settings") ctx.Pass("setting", setDetail);
        else ctx.Fail("setting", setDetail);
        yield return ctx.Screenshot("text-130");
        Percent.Value = percent;
        ModWindow.Close();
    }
}

[HarmonyPatch]
internal static class TextScalePatches
{
    private static readonly AccessTools.FieldRef<TMP_Text, float> Size = AccessTools.FieldRefAccess<TMP_Text, float>("m_fontSize");
    private static readonly AccessTools.FieldRef<TMP_Text, float> SizeMin = AccessTools.FieldRefAccess<TMP_Text, float>("m_fontSizeMin");
    private static readonly AccessTools.FieldRef<TMP_Text, float> SizeMax = AccessTools.FieldRefAccess<TMP_Text, float>("m_fontSizeMax");
    private static readonly AccessTools.FieldRef<TMP_Text, float> WorkMin = AccessTools.FieldRefAccess<TMP_Text, float>("m_minFontSize");
    private static readonly AccessTools.FieldRef<TMP_Text, float> WorkMax = AccessTools.FieldRefAccess<TMP_Text, float>("m_maxFontSize");
    // Only read by the vertical overflow checks in GenerateTextMesh (alignment uses the rect's corners). Scaling it
    // keeps Truncate/Ellipsis labels showing the lines they show at 100% instead of hiding a line that grew past
    // its box; the text spills over the box edge instead.
    private static readonly AccessTools.FieldRef<TMP_Text, float> MarginHeight = AccessTools.FieldRefAccess<TMP_Text, float>("m_marginHeight");

    // The sizes before scaling. Auto-sizing changes some of them during layout; those get divided back,
    // the untouched ones get their exact old value so repeated layouts don't drift.
    internal struct Saved
    {
        public bool on;
        public float k, size, min, max, workMin, workMax, margin, param;

        public static Saved Scale(TMP_Text t, float k)
        {
            var s = new Saved { on = true, k = k, size = Size(t), min = SizeMin(t), max = SizeMax(t), workMin = WorkMin(t), workMax = WorkMax(t), margin = MarginHeight(t) };
            Size(t) = s.size * k;
            SizeMin(t) = s.min * k;
            SizeMax(t) = s.max * k;
            WorkMin(t) = s.workMin * k;
            WorkMax(t) = s.workMax * k;
            MarginHeight(t) = s.margin * k;
            return s;
        }

        public void Restore(TMP_Text t)
        {
            Size(t) = Back(Size(t), size);
            SizeMin(t) = Back(SizeMin(t), min);
            SizeMax(t) = Back(SizeMax(t), max);
            WorkMin(t) = Back(WorkMin(t), workMin);
            WorkMax(t) = Back(WorkMax(t), workMax);
            MarginHeight(t) = margin;
        }

        public float Back(float now, float old) => now == old * k ? old : now / k;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TextMeshProUGUI), "GenerateTextMesh")]
    private static void GeneratePrefix(TextMeshProUGUI __instance, out Saved __state)
    {
        __state = default;
        if (!TextScale.Instance.Active || TextScale.Factor == 1f) return;
        try { __state = Saved.Scale(__instance, TextScale.Factor); }
        catch (Exception e) { TextScale.Instance.Fail(e, nameof(TextScalePatches)); }
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TextMeshProUGUI), "GenerateTextMesh")]
    private static void GenerateFinalizer(TextMeshProUGUI __instance, Saved __state)
    {
        if (!__state.on) return;
        try { __state.Restore(__instance); }
        catch (Exception e) { TextScale.Instance.Fail(e, nameof(TextScalePatches)); }
    }

    // Preferred width and height (content size fitters, layout groups) are computed from the size passed in here.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TMP_Text), "CalculatePreferredValues")]
    private static void PreferredPrefix(TMP_Text __instance, ref float fontSize, out Saved __state)
    {
        __state = default;
        if (!TextScale.Instance.Active || TextScale.Factor == 1f || __instance is not TextMeshProUGUI) return;
        try
        {
            __state = Saved.Scale(__instance, TextScale.Factor);
            __state.param = fontSize;
            fontSize *= __state.k;
        }
        catch (Exception e) { TextScale.Instance.Fail(e, nameof(TextScalePatches)); }
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TMP_Text), "CalculatePreferredValues")]
    private static void PreferredFinalizer(TMP_Text __instance, ref float fontSize, Saved __state)
    {
        if (!__state.on) return;
        try
        {
            __state.Restore(__instance);
            fontSize = __state.Back(fontSize, __state.param);
        }
        catch (Exception e) { TextScale.Instance.Fail(e, nameof(TextScalePatches)); }
    }
}
