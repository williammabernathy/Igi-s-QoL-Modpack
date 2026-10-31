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
    public const string StopLabel = "Stop roll-up";

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
        // The strip is 70 px; the text reaches 30 px above it, into the empty part of the details pane, left of the deposit toggle.
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = new Vector2(0f, 30f);
        var panel = root.gameObject.AddComponent<RollupPanel>();
        panel.tab = tab;
        foreach (Transform child in parent)
        {
            if (child == root || child.GetComponent<TMP_Text>() == null) continue;
            panel.vanillaText = child.gameObject;
            break;
        }

        panel.summary = FleetBarParts.AddLine(root, "", 11f, ColorHelper.offWhite, TextAlignmentOptions.BottomLeft);
        panel.summary.textWrappingMode = TextWrappingModes.Normal;
        panel.summary.overflowMode = TextOverflowModes.Ellipsis;
        panel.summary.richText = true;
        var srt = (RectTransform)panel.summary.transform;
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = new Vector2(8f, 6f);
        srt.offsetMax = new Vector2(-216f, -2f);

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
        Show();
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
            summary.text = insufficient + "\n" + Rollups.Status(plan);
            string running = Details(plan, true);
            Kit.Tooltip(summary, "Roll-up", running);
            Kit.Tooltip(button.targetGraphic, "Roll-up", running);
            buttonText.text = StopLabel;
            button.interactable = true;
            amountField.gameObject.SetActive(false);
            amountLabel.SetActive(false);
            return;
        }
        preview = Rollups.Build(forge, recipe, amount);
        amountField.gameObject.SetActive(true);
        amountLabel.SetActive(true);
        buttonText.text = StartLabel;
        button.interactable = preview.HasWork;
        summary.text = insufficient + "\n" + Summary(preview);
        string details = Details(preview, false);
        Kit.Tooltip(summary, "Craft missing parts", details);
        Kit.Tooltip(button.targetGraphic, "Craft missing parts", details);
    }

    private void OnClick()
    {
        var forge = tab.forge;
        var recipe = tab.subRecipe;
        var plan = Rollups.Find(forge, recipe);
        if (plan != null) Rollups.Stop(plan);
        else if (preview != null && preview.HasWork) Rollups.Start(Rollups.Build(forge, recipe, amount));
        Show();
    }

    private void OnAmount(string text)
    {
        amount = int.TryParse(text, out int n) ? Mathf.Clamp(n, 1, 999) : 1;
        amountField.SetTextWithoutNotify(amount.ToString());
        Show();
    }

    private static string Orange => ColorUtility.ToHtmlStringRGB(ColorHelper.orange75);

    private static string Summary(RollupPlan plan)
    {
        var sb = new StringBuilder();
        if (!plan.HasWork)
        {
            sb.Append("<color=#").Append(Orange).Append(">No recipe or ore here makes what's missing: ");
            AppendItems(sb, plan.missing);
            return sb.Append("</color>").ToString();
        }
        sb.Append("Roll-up: ");
        if (plan.PartCount > 0)
            sb.Append(plan.PartCount).Append(plan.PartCount == 1 ? " part, " : " parts, ").Append(FormatString.FormatTime(plan.seconds)).Append(" forge time, ");
        if (plan.refines.Count > 0)
            sb.Append(plan.refines.Count).Append(plan.refines.Count == 1 ? " ore to refine, " : " ores to refine, ").Append(FormatString.FormatTime(plan.refineSeconds)).Append(" refinery time, ");
        sb.Append(GameMath.FormatNumber(plan.credits)).Append(" cr");
        if (plan.missing.Count > 0)
        {
            sb.Append(". <color=#").Append(Orange).Append(">Bring: ");
            AppendItems(sb, plan.missing);
            sb.Append("</color>");
        }
        return sb.ToString();
    }

    private static void AppendItems(StringBuilder sb, Dictionary<InventoryItemType, int> items)
    {
        bool first = true;
        foreach (var kv in items)
        {
            if (!first) sb.Append(", ");
            first = false;
            sb.Append(kv.Value).Append(' ').Append(Rollups.Name(kv.Key));
        }
    }

    private static string Details(RollupPlan plan, bool running)
    {
        var sb = new StringBuilder();
        if (!plan.HasWork)
        {
            sb.Append("Nothing at this forge makes the missing ingredients and the station holds no ore that refines into them, so there's nothing to roll up. Bring them to the station first.");
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
            if (i == plan.PartCount) sb.Append(running ? "" : "Then:\n");
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
                sb.Append("\nNot made here from what the station holds; bring these to the station's materials storage (the roll-up waits for them):\n");
                foreach (var kv in plan.missing) sb.Append(kv.Value).Append(' ').Append(Rollups.Name(kv.Key)).Append('\n');
            }
        }
        sb.Append("\nParts and canisters go into this station's materials storage. Each job is queued as soon as its ingredients, a free slot and the credits are there, also while you're away.");
        return sb.ToString();
    }
}
