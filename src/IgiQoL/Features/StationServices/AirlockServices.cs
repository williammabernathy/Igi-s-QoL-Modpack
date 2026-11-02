using System;
using System.Collections.Generic;
using System.Text;
using Behaviour.UI;
using Behaviour.UI.Spacestation;
using Behaviour.UI.Tooltip;
using Behaviour.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.StationServices;

// Repair / Restock / Sell / Crew buttons in the airlock's button row (the one under the station characters), cloned from the
// airlock's own buttons. Each one shows only while its service has something to do, greyed when it can't do it here.
internal sealed class AirlockServices : MonoBehaviour
{
    public static AirlockServices Instance { get; private set; }

    private Airlock airlock;
    internal GameObject repair, restock, sell, crew, brig;
    private GameObject[] buttons;
    private float nextRefresh;
    private bool compact, undockSearched;
    private RectTransform undock;

    public static void Attach(Airlock airlock)
    {
        if (airlock.homeStationButton == null || airlock.GetComponent<AirlockServices>() != null) return;
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

    private GameObject Add(string name, Action onClick)
    {
        var template = airlock.homeStationButton;
        var go = Object.Instantiate(template, template.transform.parent);
        go.name = name;
        foreach (var t in go.GetComponentsInChildren<Translatable>(true)) Object.Destroy(t);
        var button = go.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() => onClick());
        go.SetActive(false);
        airlock.PlaceBelowCharacters((RectTransform)go.transform);
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
        // Ours plus the game's own buttons can reach past the panel into the Undock button; then every label of ours gets
        // its short form until you leave the airlock screen. The row is measured after laying it out, and the labels are
        // redone at once, so the first frame the airlock shows is already right.
        if (!compact && RowOverflows())
        {
            compact = true;
            Apply();
        }
    }

    private void Apply()
    {
        bool c = compact;
        var r = Services.Repair(false);
        Show(repair, r, (c ? "Repair " : "Repair fleet ") + Services.Money(r.credits), c ? "Repair" : "Repair fleet", "Repair fleet",
            "Starts the hangar's paid repair on every damaged ship in your fleet (about 20 seconds instead of the free repair's minutes).");
        r = Services.Restock(false);
        bool fuelOnly = r.cells > 0 && r.count == r.cells;
        string restockTitle = fuelOnly ? "Restock fuel" : "Restock ammo";
        string restockLabel = c ? (fuelOnly ? "Fuel" : "Ammo") : restockTitle;
        int cells = StationServices.Instance.FuelCells.Value;
        Show(restock, r, r.credits > 0 ? restockLabel + " " + Services.Money(r.credits) : restockLabel, restockLabel, restockTitle,
            "Tops up every ship's ammo to the \"ammo minutes\" target in ECHO's Autopilot tab: from your armory first, then from this station's shops."
            + (cells > 0 ? $" The ship you fly also gets up to {cells} warp fuel cell{(cells == 1 ? "" : "s")}." : ""));
        r = Services.Sell(false);
        var echo = EchoCargo.EchoCargo.Instance;
        var from = new List<string>(3);
        if (echo.SellFromData.Value) from.Add("your data inventory");
        if (echo.SellFromStorage.Value) from.Add("this station's storage");
        if (echo.SellFromArmory.Value) from.Add("your armory");
        string stored = from.Count == 0 ? ""
            : " It also sells from " + (from.Count == 1 ? from[0] : string.Join(", ", from.GetRange(0, from.Count - 1)) + " and " + from[from.Count - 1])
              + ", keeping N there for Sell above N.";
        string armoryKept = echo.SellFromArmory.Value ? " The armory also keeps warp fuel, every ammo type your fleet fires and the pieces your saved loadouts use." : "";
        Show(sell, r, (c ? "Sell +" : "Sell by rules +") + Services.Money(r.credits), c ? "Sell" : "Sell by rules", "Sell by rules",
            "Sells your fleet's cargo whose Item rules dock action is Sell; for Sell above N it stores up to N first." + stored + " Favourites, mission items and the ammo a ship uses stay aboard." + armoryKept);
        r = Services.Crew(false);
        string crewLabel = c ? "Crew" : "Replace crew";
        Show(crew, r, r.credits > 0 ? crewLabel + " " + Services.Money(r.credits) : crewLabel, crewLabel, "Replace crew",
            "Puts back the crew your ships lost to hull hits since they last had crew of that type added: from your crew reserve first, then willing prisoners from the brigs (40% of the hire price), then hired at this station's personnel center.");
        r = Services.Brig(false);
        Show(brig, r, c ? "Brig" : "Unload brigs", c ? "Brig" : "Unload brigs", "Unload brigs",
            "Moves the prisoners in your ships' brigs to the station brig, so the next boarding's captives fit aboard. Willing prisoners stay willing.");
    }

    private static readonly Vector3[] corners = new Vector3[4];

    internal bool RowOverflows()
    {
        if (!undockSearched)
        {
            undockSearched = true;
            foreach (var t in airlock.transform.root.GetComponentsInChildren<TMP_Text>(true))
                if (t.text.Trim() == "Undock" && t.GetComponentInParent<Button>() != null)
                {
                    undock = (RectTransform)t.GetComponentInParent<Button>().transform;
                    break;
                }
        }
        // The airlock lays the row out once a frame; do it now so buttons shown or resized a moment ago are where they will be.
        airlock.LayoutButtonRow();
        float limit;
        if (undock != null && undock.gameObject.activeInHierarchy)
        {
            undock.GetWorldCorners(corners);
            limit = corners[0].x;
        }
        else
        {
            ((RectTransform)airlock.transform).GetWorldCorners(corners);
            limit = corners[2].x;
        }
        // Every button of the row counts, the Favourite button and the game's own included, not only ours.
        foreach (var rt in airlock.buttonRow)
        {
            if (rt == null || !rt.gameObject.activeSelf) continue;
            rt.GetWorldCorners(corners);
            if (corners[2].x > limit) return true;
        }
        return false;
    }

    internal bool Compact => compact;

    // Vanilla adds its situational buttons to the same row later; ours stay at the end so theirs never get pushed aside.
    private void KeepLast()
    {
        var row = airlock.buttonRow;
        int n = row.Count, k = buttons.Length;
        bool inPlace = n >= k;
        for (int i = 0; inPlace && i < k; i++) inPlace = row[n - k + i] == buttons[i].transform;
        if (inPlace) return;
        foreach (var go in buttons)
        {
            row.Remove((RectTransform)go.transform);
            row.Add((RectTransform)go.transform);
        }
    }

    private void Show(GameObject go, ServiceReport r, string label, string idleLabel, string title, string what)
    {
        bool work = r.count > 0;
        bool visible = work || r.problems.Count > 0;
        if (go.activeSelf != visible) go.SetActive(visible);
        if (!visible) return;

        var text = go.GetComponentInChildren<TMP_Text>();
        if (!work) label = idleLabel;
        if (text.text != label)
        {
            text.text = label;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(Mathf.Max(compact ? 50f : 80f, text.GetPreferredValues(label).x + 24f), rt.sizeDelta.y);
        }

        var sb = new StringBuilder(what);
        if (r.lines.Count > 0) sb.Append("\n");
        foreach (var line in r.lines) sb.Append("\n").Append(line);
        if (r.problems.Count > 0) sb.Append("\n");
        foreach (var p in r.problems) sb.Append("\n").Append(p);
        airlock.SetButtonState(go, work, airlock.GetBaseButtonColor(), sb.ToString());
        go.GetComponent<Button>().interactable = work;
        var tip = go.GetComponent<TooltipSource>();
        if (tip != null) tip.Title = title + " (QoL)";
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
