using System;
using System.Collections.Generic;
using System.Text;
using Behavior.UI.GalaxyMap;
using Behaviour.Item;
using Behaviour.UI.Forge;
using Behaviour.UI.Shared;
using Behaviour.Util;
using IgiQoL.UI;
using Source.Util;
using Source.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.CraftRollup;

// Lives inside the forge's "insufficient materials" strip: plan summary, amount, and the start/stop button.
internal sealed class RollupPanel : MonoBehaviour
{
    public const string StartLabel = "Craft missing parts";
    public const string StopLabel = "Stop crafting parts";

    private ForgeTabContents tab;
    private GameObject vanillaText;
    internal TMP_Text summary;
    internal TMP_Text buttonText;
    private Button button;
    private TMP_InputField amountField;
    private GameObject amountLabel;
    private int amount = 1;
    private RollupPlan preview;
    private float nextRefresh;

    public static void Refresh(ForgeTabContents tab)
    {
        var panel = tab.notCraftableContent.GetComponentInChildren<RollupPanel>(true);
        if (panel == null) panel = Create(tab);
        panel.Show();
    }

    private static RollupPanel Create(ForgeTabContents tab)
    {
        var parent = tab.notCraftableContent;
        var root = UiCards.NewChild("IgiRollup", parent);
        // The strip is 70 px; the text stays inside it, left of the amount field and the button. Above it the game's
        // ingredient list grows down with the ingredient count, so anything taller would draw over the last rows.
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        var panel = root.gameObject.AddComponent<RollupPanel>();
        panel.tab = tab;
        foreach (Transform child in parent)
        {
            if (child == root || child.GetComponent<TMP_Text>() == null) continue;
            panel.vanillaText = child.gameObject;
            break;
        }

        // Top aligned, so the game's "Insufficient materials" line stays where it is whatever the second line does.
        panel.summary = FleetBarParts.AddLine(root, "", 11f, ColorHelper.offWhite, TextAlignmentOptions.TopLeft);
        panel.summary.textWrappingMode = TextWrappingModes.Normal;
        panel.summary.overflowMode = TextOverflowModes.Ellipsis;
        // A long time or fee shrinks the text rather than spilling out of the strip.
        panel.summary.enableAutoSizing = true;
        panel.summary.fontSizeMax = 11f;
        panel.summary.fontSizeMin = 8f;
        panel.summary.richText = true;
        var srt = (RectTransform)panel.summary.transform;
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = new Vector2(8f, 6f);
        srt.offsetMax = new Vector2(-216f, -4f);

        // A copy of the game's Craft button, so it looks the same; its click and translation key are replaced.
        var template = tab.craftableContent.Find("Button");
        var go = Object.Instantiate(template.gameObject, root);
        go.name = "IgiRollupButton";
        foreach (var t in go.GetComponentsInChildren<Translatable>(true)) Object.Destroy(t);
        panel.button = go.GetComponent<Button>();
        panel.button.onClick = new Button.ButtonClickedEvent();
        panel.button.onClick.AddListener(panel.OnClick);
        panel.buttonText = go.GetComponentInChildren<TMP_Text>();
        panel.buttonText.enableAutoSizing = true;
        panel.buttonText.fontSizeMax = 14f;
        panel.buttonText.fontSizeMin = 9f;
        panel.buttonText.textWrappingMode = TextWrappingModes.NoWrap;
        panel.buttonText.margin = new Vector4(8f, 0f, 8f, 0f);
        var brt = (RectTransform)go.transform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 0f);
        brt.anchoredPosition = new Vector2(-8f, 8f);
        brt.sizeDelta = new Vector2(150f, 34f);

        panel.amountField = Kit.Input(root, "1", TMP_InputField.ContentType.IntegerNumber, panel.OnAmount, 44f);
        var art = (RectTransform)panel.amountField.transform;
        art.anchorMin = art.anchorMax = art.pivot = new Vector2(1f, 0f);
        art.anchoredPosition = new Vector2(-164f, 12f);
        art.sizeDelta = new Vector2(44f, 26f);

        var label = FleetBarParts.AddLine(root, "Amount", 10f, ColorHelper.poiTooltipMuted, TextAlignmentOptions.BottomRight);
        var lrt = (RectTransform)label.transform;
        lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(1f, 0f);
        lrt.anchoredPosition = new Vector2(-164f, 40f);
        lrt.sizeDelta = new Vector2(52f, 14f);
        panel.amountLabel = label.gameObject;
        return panel;
    }

    private void Update()
    {
        if (!CraftRollup.Instance.Active)
        {
            if (vanillaText != null) vanillaText.SetActive(true);
            Destroy(gameObject);
            return;
        }
        if (Time.unscaledTime < nextRefresh) return;
        try { Show(); }
        catch (Exception e) { CraftRollup.Instance.Fail(e, nameof(RollupPanel)); }
    }

    public void Show()
    {
        nextRefresh = Time.unscaledTime + 1f;
        var recipe = tab.subRecipe;
        var forge = tab.forge;
        if (recipe == null || forge == null) return;
        if (vanillaText != null) vanillaText.SetActive(false);
        string insufficient = Translation.Translate("@UIInsufficientMaterials");
        var plan = Rollups.Find(forge, recipe);
        if (plan != null)
        {
            preview = null;
            SetTexts(insufficient + "\n" + Rollups.Status(plan), Rollups.RunningLabel, Details(plan, true), StopLabel);
            button.interactable = true;
            amountField.gameObject.SetActive(false);
            amountLabel.SetActive(false);
            FitStrip();
            return;
        }
        preview = Rollups.Build(forge, recipe, amount);
        amountField.gameObject.SetActive(true);
        amountLabel.SetActive(true);
        button.interactable = preview.HasWork;
        SetTexts(insufficient + "\n" + Summary(preview), StartLabel, Details(preview, false), StartLabel);
        FitStrip();
    }

    // The panel refreshes every second, but the texts only change when the plan does: a text set again every time would
    // re-layout the strip and rebuild both tooltips for nothing.
    private string shownSummary, shownDetails, shownButton;

    private void SetTexts(string text, string title, string details, string label)
    {
        if (shownSummary != text)
        {
            shownSummary = text;
            summary.text = text;
        }
        if (shownDetails != details || shownButton != label)
        {
            shownDetails = details;
            Kit.Tooltip(summary, title, details);
            Kit.Tooltip(button.targetGraphic, title, details);
        }
        if (shownButton != label)
        {
            shownButton = label;
            buttonText.text = label;
        }
    }

    private static readonly Vector3[] corners = new Vector3[4];

    // The game's ingredient list grows down with the ingredient count, and in the station's own forge screen, which
    // is shorter than the popup, a long recipe reaches into this strip. The summary then hides rather than drawing
    // over the rows; the button's tooltip still has all of it.
    private void FitStrip()
    {
        var box = WorldRect((RectTransform)summary.transform);
        bool crowded = WorldRect((RectTransform)tab.resultsList.transform).Overlaps(box);
        foreach (var row in tab.ingredientsList.rows)
            if (!crowded && row != null && WorldRect((RectTransform)row.transform).Overlaps(box)) crowded = true;
        if (summary.gameObject.activeSelf == crowded) summary.gameObject.SetActive(!crowded);
    }

    internal bool SummaryShown => summary.gameObject.activeSelf;

    private static Rect WorldRect(RectTransform rt)
    {
        rt.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    private void OnClick()
    {
        if (!CraftRollup.Instance.Active) return;
        try
        {
            var forge = tab.forge;
            var recipe = tab.subRecipe;
            var plan = Rollups.Find(forge, recipe);
            if (plan != null) Rollups.Stop(plan);
            else if (preview != null && preview.HasWork) Rollups.Start(Rollups.Build(forge, recipe, amount));
            Show();
        }
        catch (Exception e) { CraftRollup.Instance.Fail(e, nameof(RollupPanel)); }
    }

    private void OnAmount(string text)
    {
        if (!CraftRollup.Instance.Active) return;
        try
        {
            amount = int.TryParse(text, out int n) ? Mathf.Clamp(n, 1, 999) : 1;
            amountField.SetTextWithoutNotify(amount.ToString());
            Show();
        }
        catch (Exception e) { CraftRollup.Instance.Fail(e, nameof(RollupPanel)); }
    }

    private static string Orange => ColorUtility.ToHtmlStringRGB(ColorHelper.orange75);

    // Two short lines at most: what crafting the missing parts costs, and how many items still have to be brought. The
    // times and the item list are in the tooltip.
    private static string Summary(RollupPlan plan)
    {
        var sb = new StringBuilder();
        if (!plan.HasWork)
        {
            sb.Append("<color=#").Append(Orange).Append(">Nothing here can make the missing items (hover for the list).</color>");
            return sb.ToString();
        }
        sb.Append("Costs ").Append(GameMath.FormatNumber(plan.credits)).Append(" cr: ");
        if (plan.PartCount > 0) sb.Append(plan.PartCount).Append(plan.PartCount == 1 ? " part" : " parts");
        if (plan.PartCount > 0 && plan.refines.Count > 0) sb.Append(" and ");
        if (plan.refines.Count > 0) sb.Append(plan.refines.Count).Append(plan.refines.Count == 1 ? " ore to refine" : " ores to refine");
        sb.Append('.');
        if (plan.missing.Count > 0)
            sb.Append("\n<color=#").Append(Orange).Append(">Still missing ").Append(Kinds(plan.missing.Count)).Append(" (hover for the list).</color>");
        return sb.ToString();
    }

    private static string Kinds(int n) => n == 1 ? "1 item" : n + " items";

    private static string Details(RollupPlan plan, bool running)
    {
        var sb = new StringBuilder();
        if (!plan.HasWork)
        {
            sb.Append("Nothing at this forge makes the missing ingredients, and the station holds no ore that refines into them, so there is nothing to craft. Bring them to the station first:\n");
            foreach (var kv in plan.missing) sb.Append(kv.Value).Append(' ').Append(Rollups.Name(kv.Key)).Append('\n');
            return sb.ToString();
        }
        if (plan.refines.Count > 0)
        {
            sb.Append(running ? "Refinery jobs:\n" : "Ore to refine first:\n");
            foreach (var r in plan.refines)
            {
                sb.Append(running ? (r.started ? "[started] " : "[waiting] ") : "").Append(r.amount).Append("x ").Append(Rollups.OreName(r.ore)).Append(" (");
                bool first = true;
                foreach (var c in r.ore.contents)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(c.product.GetDisplayName());
                }
                sb.Append(")\n");
            }
            sb.Append('\n');
        }
        sb.Append(running ? "Forge jobs, in order:\n" : plan.PartCount > 0 ? "Parts to craft:\n" : "");
        for (int i = 0; i < plan.steps.Count; i++)
        {
            var s = plan.steps[i];
            if (i == plan.PartCount) sb.Append(running ? "" : "Then the recipe itself:\n");
            sb.Append(running ? (s.started ? "[queued] " : "[waiting] ") : "").Append(s.amount).Append("x ").Append(Rollups.RecipeName(s.recipe)).Append('\n');
        }
        if (!running)
        {
            if (plan.PartCount > 0)
                sb.Append("\nForge time ").Append(FormatString.FormatTime(plan.seconds)).Append(" if run one after another; free forge slots run jobs side by side.");
            if (plan.refines.Count > 0)
                sb.Append("\nRefinery time ").Append(FormatString.FormatTime(plan.refineSeconds)).Append(" if run one after another; each ore is one refinery job. Ore comes from the station's materials storage (and your cargo when docked here); favourites and ore your missions need are left alone.");
            sb.Append("\nCost ").Append(GameMath.FormatNumber(plan.credits)).Append(" cr, paid as each job starts.\n");
            if (plan.missing.Count > 0)
            {
                sb.Append("\nNot made here from what the station holds. Bring these to the station's materials storage; crafting waits for them:\n");
                foreach (var kv in plan.missing) sb.Append(kv.Value).Append(' ').Append(Rollups.Name(kv.Key)).Append('\n');
            }
        }
        sb.Append("\nParts and canisters go into this station's materials storage. Each job is queued as soon as its ingredients, a free slot and the credits are there, also while you're away.");
        return sb.ToString();
    }
}
