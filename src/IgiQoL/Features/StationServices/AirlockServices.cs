using System;
using System.Collections.Generic;
using System.Text;
using Behaviour.UI;
using Behaviour.UI.Shared;
using IgiQoL.Rules;
using IgiQoL.UI;
using Behaviour.UI.Spacestation;
using Behaviour.UI.Tooltip;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.StationServices;

// Repair / Restock / Sell / Crew buttons at the end of the airlock's text buttons (the row in the action card under the
// station characters), made like the airlock's own. Each one appears when its service has something to do and then stays
// for the visit, greyed when it can't do it here. When they don't fit there even with short labels, they become icon
// buttons.
internal sealed class AirlockServices : MonoBehaviour
{
    public static AirlockServices Instance { get; private set; }

    private Airlock airlock;
    internal GameObject repair, restock, sell, crew, brig;
    // A button that has shown this visit stays in the row, dimmed once it has nothing to do, so the row never shifts.
    private readonly bool[] everShown = new bool[5];
    private readonly float[] widths = new float[10];
    private GameObject[] buttons;
    private float nextRefresh;
    private bool compact;
    // The last step when even the short labels don't fit: icon buttons, like the game's own at the start of the row.
    private bool iconsOnly;

    public static void Attach(Airlock airlock)
    {
        if (airlock.textButtons == null || airlock.GetComponent<AirlockServices>() != null) return;
        var c = airlock.gameObject.AddComponent<AirlockServices>();
        c.airlock = airlock;
        c.repair = c.Add("IgiRepairFleet", () => c.Click(Services.Repair, r => StationServices.Summary(r, null, null)));
        c.restock = c.Add("IgiRestockAmmo", () => c.Click(Services.Restock, r => StationServices.Summary(null, r, null)));
        c.sell = c.Add("IgiSellByRules", () => c.Click(Services.Sell, r => StationServices.Summary(null, null, r)));
        c.crew = c.Add("IgiReplaceCrew", () => c.Click(Services.Crew, r => StationServices.Summary(null, null, null, r)));
        c.brig = c.Add("IgiUnloadBrigs", () => c.Click(Services.Brig, r => StationServices.Summary(null, null, null, null, r)));
        c.buttons = new[] { c.repair, c.restock, c.sell, c.crew, c.brig };
        Instance = c;
    }

    // The same button the game's own text buttons are (height 30, font 12), hidden until its service has something to do.
    private GameObject Add(string name, Action onClick)
    {
        var button = UiButton.Add(airlock.textButtons, "", () => onClick(), ButtonRole.Plain, 0f, null, null, 30f, 12f);
        button.name = name;
        var go = button.gameObject;
        Airlock.SetTooltip(go, "");
        Airlock.IgnoreForEcho(go);
        go.SetActive(false);
        return go;
    }

    private void Click(Func<bool, ServiceReport> service, Func<ServiceReport, string> summarize)
    {
        var f = StationServices.Instance;
        if (!f.Active) return;
        try
        {
            var r = service(true);
            var iim = InventoryInteractionManager.Instance;
            if (iim != null) iim.ReloadUI();
            string summary = summarize(r);
            if (summary != null) Plugin.Log.LogInfo("[StationServices] airlock: " + summary);
            StationServices.Notify(summary != null ? char.ToUpper(summary[0]) + summary.Substring(1) : null, r);
            nextRefresh = 0f;
        }
        catch (Exception e) { f.Fail(e, nameof(AirlockServices)); }
    }

    private void Update()
    {
        if (buttons == null || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 1f;
        var f = StationServices.Instance;
        if (!f.Active || !f.AirlockButtons.Value)
        {
            foreach (var go in buttons) go.SetActive(false);
            return;
        }
        try { Refresh(); }
        catch (Exception e) { f.Fail(e, nameof(AirlockServices)); }
    }

    internal void Refresh()
    {
        KeepLast();
        Apply();
        // Ours plus the game's own buttons can be wider than the airlock's action row; then every label of ours gets its short
        // form until you leave the airlock screen, and if even that doesn't fit, ours become icon buttons (the row is one
        // fixed-height line, and the cards above it can't give up height). The row is measured after laying it out, and the
        // labels are redone at once, so the first frame the airlock shows is already right.
        if (!iconsOnly && RowOverflows())
        {
            if (!compact)
            {
                compact = true;
                Apply();
            }
            if (RowOverflows())
            {
                Iconize();
                Apply();
            }
        }
        // Buttons shown or hidden above change whether the divider between the icon buttons and the text buttons is needed.
        airlock.RefreshDivider();
    }

    // The widest credit amount a label has to hold, so a button's width never depends on the amount it shows now.
    private const string WidestMoney = " 888.8K cr";

    // Full labels carry the amount; short labels are the same words without it (the amount stays in the tooltip), so
    // the row never swaps one word for another.
    private void Apply()
    {
        bool c = compact;
        var r = Services.Repair(false);
        Show(0, repair, r, c ? "Repair" : "Repair fleet " + Services.Money(r.credits), c ? "Repair" : "Repair fleet", "Repair fleet",
            c ? "Repair" : "Repair fleet" + WidestMoney,
            "Starts the hangar's paid repair on every damaged ship in your fleet (about 20 seconds instead of the free repair's minutes)."
            + (c && r.credits > 0 ? " Costs " + Services.Money(r.credits) + "." : ""));
        r = Services.Restock(false);
        bool fuelOnly = r.cells > 0 && r.count == r.cells;
        string restockTitle = fuelOnly ? "Restock fuel" : "Restock ammo";
        string restockLabel = c ? "Restock" : restockTitle;
        int cells = StationServices.Instance.FuelCells.Value;
        Show(1, restock, r, r.credits > 0 && !c ? restockLabel + " " + Services.Money(r.credits) : restockLabel, restockLabel, restockTitle,
            c ? "Restock" : "Restock ammo" + WidestMoney,
            "Tops up every ship's ammo to the \"Ammo Supply\" target in ECHO's Autopilot tab: from your armory first, then from this station's shops."
            + (cells > 0 ? $" The ship you fly also gets up to {cells} warp fuel cell{(cells == 1 ? "" : "s")}." : "")
            + (c && r.credits > 0 ? " Costs " + Services.Money(r.credits) + "." : ""));
        r = Services.Sell(false);
        var echo = EchoCargo.EchoCargo.Instance;
        var from = new List<string>(3);
        if (echo.SellFromData.Value) from.Add("your data inventory");
        if (echo.SellFromStorage.Value) from.Add("this station's storage");
        if (echo.SellFromArmory.Value) from.Add("your armory");
        string stored = from.Count == 0 ? ""
            : " It also sells from " + (from.Count == 1 ? from[0] : string.Join(", ", from.GetRange(0, from.Count - 1)) + " and " + from[from.Count - 1])
              + ", keeping the amount you set there for the \"" + ItemRules.DockActionNames[3] + "\" dock action.";
        string armoryKept = echo.SellFromArmory.Value ? " The armory also keeps warp fuel, every ammo type your fleet fires and the pieces your saved loadouts use." : "";
        Show(2, sell, r, c ? "Sell" : "Sell by rules " + Services.Money(r.credits), c ? "Sell" : "Sell by rules", "Sell by rules",
            c ? "Sell" : "Sell by rules" + WidestMoney,
            "Sells your fleet's cargo whose Item Rules dock action is Sell; for \"" + ItemRules.DockActionNames[3] + "\" it stores up to the amount you set first." + stored + " Favourites, mission items and the ammo a ship uses stay aboard." + armoryKept
            + (c && r.credits > 0 ? " Brings in " + Services.Money(r.credits) + "." : ""));
        r = Services.Crew(false);
        string crewLabel = "Replace crew";
        Show(3, crew, r, r.credits > 0 && !c ? crewLabel + " " + Services.Money(r.credits) : crewLabel, crewLabel, "Replace crew",
            c ? crewLabel : crewLabel + WidestMoney,
            "Puts back the crew your ships lost to hull hits since they last had crew of that type added: from your crew reserve first, then willing prisoners from the brigs (40% of the hire price), then hired at this station's personnel center."
            + (c && r.credits > 0 ? " Costs " + Services.Money(r.credits) + "." : ""));
        r = Services.Brig(false);
        Show(4, brig, r, "Unload brigs", "Unload brigs", "Unload brigs", "Unload brigs",
            "Moves the prisoners in your ships' brigs to the station brig, so the next boarding's captives fit aboard. Willing prisoners stay willing.");
    }

    // Each button keeps its tooltip, whose title names it; the icons are the game's own for the same things.
    private void Iconize()
    {
        iconsOnly = true;
        string[] keys =
        {
            Kit.Icon("Info", "Facility.DryDock", "Fleet.Refit"), Kit.Icon("Info", "Ammo"), Kit.Icon("Info", "Credits"),
            Kit.Icon("Info", "Crew"), Kit.Icon("Info", "Lock"),
        };
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i].GetComponent<Button>();
            UiButton.SetIconOnly(button, keys[i], ColorHelper.stationCardTitle);
            var size = buttons[i].GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 30f;
        }
    }

    internal bool IconsOnly => iconsOnly;

    // Where each text button of the row is and how wide, for the self-check that the row holds still while amounts change.
    internal string RowSnapshot()
    {
        var row = airlock.layout.actionRow;
        LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        var sb = new StringBuilder();
        foreach (Transform t in airlock.textButtons)
        {
            var rt = (RectTransform)t;
            sb.Append(rt.name).Append(rt.gameObject.activeSelf ? " x=" : " hidden x=").Append(row.InverseTransformPoint(rt.position).x.ToString("0.#")).Append(" w=").Append(rt.rect.width.ToString("0.#")).Append("; ");
        }
        return sb.ToString();
    }

    private static readonly Vector3[] corners = new Vector3[4];

    // True when any visible button of the action row (icon buttons, the Favourite button and the game's own included, not
    // only ours) ends past the right edge of its card, or got squeezed below its own width: the row's layout shrinks
    // buttons that may shrink (the game's icon buttons) before anything spills out.
    internal bool RowOverflows()
    {
        var row = airlock.layout.actionRow;
        // The layout runs once a frame; do it now, from the airlock layout's root so the row has its real width, so buttons
        // shown or resized a moment ago are where they will be. Before the screen's first layout the row has no width yet
        // and everything in it looks squeezed; the next refresh judges it.
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)row.parent.parent);
        if (row.rect.width < 1f) return false;
        return Spills(row, airlock.iconButtons) || Spills(row, airlock.textButtons);
    }

    private static bool Spills(RectTransform card, RectTransform strip)
    {
        card.GetWorldCorners(corners);
        float limit = corners[2].x;
        foreach (Transform t in strip)
        {
            if (!t.gameObject.activeSelf) continue;
            var rt = (RectTransform)t;
            if (rt.rect.width < LayoutUtility.GetPreferredWidth(rt) - 0.5f) return true;
            rt.GetWorldCorners(corners);
            if (corners[2].x > limit + 0.01f) return true;
        }
        return false;
    }

    internal bool Compact => compact;

    // Vanilla adds its situational buttons (Build outpost, Buy system, ...) to the same row later, some at the front; ours stay
    // at the end so theirs never get pushed aside.
    private void KeepLast()
    {
        var row = airlock.textButtons;
        int n = row.childCount, k = buttons.Length;
        bool inPlace = n >= k;
        for (int i = 0; inPlace && i < k; i++) inPlace = row.GetChild(n - k + i) == buttons[i].transform;
        if (inPlace) return;
        foreach (var go in buttons) go.transform.SetAsLastSibling();
    }

    private void Show(int slot, GameObject go, ServiceReport r, string label, string idleLabel, string title, string widest, string what)
    {
        bool work = r.count > 0;
        bool visible = work || r.problems.Count > 0 || everShown[slot];
        if (go.activeSelf != visible) go.SetActive(visible);
        if (!visible) return;
        everShown[slot] = true;

        if (!work) label = idleLabel;
        if (!iconsOnly)
        {
            var text = go.GetComponentInChildren<TMP_Text>();
            if (text.text != label) text.text = label;
            // One width per button and label form, from its widest label, so the amount on it never moves its neighbours.
            int w = slot * 2 + (compact ? 1 : 0);
            if (widths[w] == 0f) widths[w] = Mathf.Ceil(text.GetPreferredValues(widest).x + 24f);
            var size = go.GetComponent<LayoutElement>();
            if (size.preferredWidth != widths[w]) size.minWidth = size.preferredWidth = widths[w];
        }

        var sb = new StringBuilder(what);
        if (r.lines.Count > 0) sb.Append("\n");
        foreach (var line in r.lines) sb.Append("\n").Append(line);
        if (r.problems.Count > 0) sb.Append("\n");
        foreach (var p in r.problems) sb.Append("\n").Append(p);
        Airlock.SetButtonState(go, work, sb.ToString());
        go.GetComponent<Button>().interactable = work;
        var tip = go.GetComponent<TooltipSource>();
        if (tip != null) tip.Title = title + " (QoL)";
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
