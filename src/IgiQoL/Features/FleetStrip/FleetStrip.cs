using System;
using System.Collections;
using System.Collections.Generic;
using Behavior.UI.GalaxyMap;
using Behaviour.Equipment.Module;
using Behaviour.UI;
using Behaviour.UI.HUD.Fleet;
using Behaviour.UI.Movement;
using Behaviour.Unit;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using Source.Player;
using Source.SpaceShip;
using Source.SpaceShip.Auto;
using Source.UI.Tooltip;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.FleetStrip;

public sealed class FleetStrip : Feature
{
    public static FleetStrip Instance;

    public FleetStrip() { Instance = this; }

    public override string Id => "FleetStrip";
    public override string Title => "Fleet command strip";
    public override string Description => "Adds a row of buttons under each ship in the fleet panel (bottom left): stop, hold position, regroup, combat stance, auto-tractor, drones, cargo, and the ship's own settings.";
    public override Type[] PatchClasses => new[] { typeof(FleetStripPatches) };

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        CheckPrefab(ctx);

        var rows = Object.FindObjectsByType<FleetPanelRow>(FindObjectsInactive.Include);
        if (rows.Length == 0) ctx.Skip("rows", "no fleet rows in this save");
        else
        {
            bool ok = true;
            foreach (var row in rows)
            {
                var s = row.GetComponent<FleetStripRow>();
                if (s == null || s.strip == null) ok = false;
            }
            if (ok) ctx.Pass("rows", rows.Length + " rows with strip");
            else ctx.Fail("rows", "strip missing on some of " + rows.Length + " rows");
        }

        var ship = GamePlayer.current?.currentSpaceShip;
        if (ship == null) ctx.Skip("popup", "no current ship");
        else
        {
            ShipPopup.Open(ship);
            int controls = 0;
            if (ShipPopup.IsOpen)
            {
                var body = ShipPopup.Body;
                controls = body.GetComponentsInChildren<TMP_Dropdown>(true).Length;
                if (controls == 0)
                    foreach (var b in body.GetComponentsInChildren<Button>(true))
                        if (b.name == "Button") controls++;
            }
            int expected = (MissileRetarget.MissileRetarget.Instance.Active ? 1 : 0) + (DronesOnArrival.DronesOnArrival.Instance.Active ? 1 : 0)
                + (EngagementDistance.EngagementDistance.Instance.Active ? 1 : 0) + (DroneModes.DroneModes.Instance.Active ? 2 : 0)
                + (Separation.Separation.Instance.Active ? 1 : 0)
                + (TargetPriority.TargetPriority.Instance.Active ? 3 + TargetPriority.TargetPriority.CombatWeaponCount(ship) : 0);
            string detail = $"open={ShipPopup.IsOpen} controls={controls} expected={expected} ship={ship.GetShipName()}";
            if (ShipPopup.IsOpen && controls == expected) ctx.Pass("popup", detail);
            else ctx.Fail("popup", detail);
            yield return ctx.Screenshot("ship-popup");
            ShipPopup.Close();
        }
    }

    // Builds the strip on a detached copy of the row prefab: geometry, then a click through the event hierarchy.
    private static void CheckPrefab(SelfCheckContext ctx)
    {
        FleetPanelRow prefab = null;
        foreach (var panel in Resources.FindObjectsOfTypeAll<FleetPanel>())
            if (panel.rowPrefab != null) { prefab = panel.rowPrefab; break; }
        if (prefab == null)
        {
            ctx.Skip("layout", "no FleetPanel loaded");
            ctx.Skip("click", "no FleetPanel loaded");
            return;
        }
        var row = Object.Instantiate(prefab);
        try
        {
            // Same order as on live rows when both features are on: the bars, then the tiles under them.
            if (FleetVitals.FleetVitalsFeature.Instance.Active) row.gameObject.AddComponent<FleetVitals.FleetVitals>().Build(row);
            var strip = row.gameObject.AddComponent<FleetStripRow>();
            strip.Build(row);
            var rt = strip.strip;
            float top = FleetRowLayout.Top(rt);
            float bottom = FleetRowLayout.Bottom(rt);
            float barsBottom = FleetRowLayout.BarsBottom(row);
            float rowHeight = row.GetComponent<LayoutElement>().preferredHeight;
            int tiles = rt.childCount;
            string detail = $"tiles={tiles} strip {top:0.#}..{bottom:0.#} h={rt.rect.height:0.#}, bars end {barsBottom:0.#}, row height {rowHeight:0.#}, merc collapsed {row.mercContract.collapsedHeight:0.#}";
            if (tiles == 11 && Mathf.Approximately(top, barsBottom - FleetStripRow.StripGap) && bottom >= -rowHeight) ctx.Pass("layout", detail);
            else ctx.Fail("layout", detail);

            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            var handled = ExecuteEvents.ExecuteHierarchy(strip.settings.gameObject, data, ExecuteEvents.pointerClickHandler);
            if (handled == strip.settings.gameObject) ctx.Pass("click", "tile consumed the click");
            else ctx.Fail("click", "handled by " + (handled == null ? "nobody" : handled.name));
        }
        finally { Object.DestroyImmediate(row.gameObject); }
    }
}

public sealed class FleetStripRow : MonoBehaviour
{
    private const float Grow = 20f;
    // Space between the row's lowest bar and the tiles.
    public const float StripGap = 4f;

    public FleetPanelRow row;
    public Button stop, hold, regroup, holdFire, aggressive, defensive, escort, tractor, drones, cargo, settings;
    public Image tractorIcon, dronesIcon;
    public RectTransform strip;

    private readonly List<SpaceShip> one = new(1);
    private SpaceShip cachedShip;
    private TractorModule tractorModule;
    private DroneBayModule droneBay;

    public void Build(FleetPanelRow owner)
    {
        if (strip != null) return;
        row = owner;
        var le = row.GetComponent<LayoutElement>();
        le.preferredHeight += Grow;
        row.mercContract.collapsedHeight += Grow;

        strip = FleetBarParts.AddRow((RectTransform)row.transform, FleetRowLayout.StripName, 2f);
        strip.anchorMin = new Vector2(0f, 1f);
        strip.anchorMax = new Vector2(1f, 1f);
        strip.pivot = new Vector2(0f, 1f);
        strip.anchoredPosition = new Vector2(4f, FleetRowLayout.BarsBottom(row) - StripGap);
        strip.sizeDelta = new Vector2(-8f, 16f);

        stop = Tile(PoiIconKeys.Fleet("Stop"), ColorHelper.fleetCommandStopIcon, "Stop", OnStop);
        hold = Tile(PoiIconKeys.Fleet("HoldPosition"), ColorHelper.fleetCommandHoldIcon, "Hold position", OnHold);
        regroup = Tile(PoiIconKeys.Fleet("Regroup"), ColorHelper.fleetCommandRegroupIcon, "Regroup on flagship", OnRegroup);
        holdFire = Tile(PoiIconKeys.Fleet("HoldFire"), ColorHelper.fleetCommandHoldFireIcon, "Hold fire", OnHoldFire);
        aggressive = Tile(PoiIconKeys.Fleet("Aggressive"), ColorHelper.fleetCommandAggressiveIcon, "Aggressive", OnAggressive);
        defensive = Tile(PoiIconKeys.Fleet("Defend"), ColorHelper.fleetCommandDefensiveIcon, "Defensive", OnDefensive);
        escort = Tile(PoiIconKeys.Fleet("Escort"), ColorHelper.fleetCommandEscortIcon, "Escort", OnEscort);
        tractor = Tile(PoiIconKeys.Fleet("Tractor"), ColorHelper.offWhite, "Auto-tractor on/off", OnTractor);
        drones = Tile(Kit.Icon("Ability", "Fleet.Drones", "Fleet.Drone", "Fleet.Hangar"), ColorHelper.offWhite, "Deploy or recall drones", OnDrones);
        cargo = Tile(PoiIconKeys.Fleet("FleetCargo"), ColorHelper.offWhite, "Cargo", OnCargo);
        settings = Tile(Kit.Icon("Info", PoiIconKeys.RouteUi("Edit")), ColorHelper.offWhite, "Ship settings", OnSettings);
        tractorIcon = tractor.transform.Find("Icon").GetComponent<Image>();
        dronesIcon = drones.transform.Find("Icon").GetComponent<Image>();
        Refresh();
    }

    private Button Tile(string key, Color tint, string tooltip, Action onClick) =>
        Kit.Tile(strip, key, tint, 16f, () => onClick(), tooltip);

    private SpaceShip Ship()
    {
        if (row == null || row.shipData == null) return null;
        return row.shipData.unit as SpaceShip;
    }

    private void Command(Action<IReadOnlyList<SpaceShip>> run)
    {
        var ship = Ship();
        if (ship == null) return;
        one.Clear();
        one.Add(ship);
        run(one);
    }

    private void OnStop() => Command(FleetCommands.IssueStop);
    private void OnHold() => Command(FleetCommands.IssueHold);
    private void OnRegroup() => Command(FleetCommands.IssueRegroup);
    private void OnHoldFire() => Command(l => FleetCommands.SetStance(l, FleetStance.HoldFire));
    private void OnAggressive() => Command(l => FleetCommands.SetStance(l, FleetStance.Aggressive));
    private void OnDefensive() => Command(l => FleetCommands.SetStance(l, FleetStance.Defensive));
    private void OnEscort() => Command(l => FleetCommands.SetStance(l, FleetStance.Escort));

    private void OnTractor()
    {
        var data = row.shipData;
        if (data == null) return;
        data.tractorOff = !data.tractorOff;
        (data.unit as SpaceShip)?.ApplyTractorToggle();
        Refresh();
    }

    private void OnDrones()
    {
        var bay = (Ship())?.GetModule<DroneBayModule>();
        if (bay == null || !bay.CanDeployReturn()) return;
        if (bay.AreDronesDeployed())
        {
            bay.playerRecalled = true;
            bay.ReturnDrones();
        }
        else bay.DeployDrones();
    }

    private void OnCargo()
    {
        if (row.shipData != null) FleetCargoSection.ToggleWindow(row.shipData);
    }

    private void OnSettings()
    {
        if (row.shipData != null) ShipPopup.Open(row.shipData);
    }

    // Runs every 0.25 s per row from the FleetPanelRow.Refresh postfix; allocation-free.
    public void Refresh()
    {
        if (strip == null || row == null) return;
        var data = row.shipData;
        var ship = data == null ? null : data.unit as SpaceShip;
        if (ship != cachedShip)
        {
            cachedShip = ship;
            tractorModule = ship == null ? null : ship.GetModule<TractorModule>();
            droneBay = ship == null ? null : ship.GetModule<DroneBayModule>();
        }
        bool fleet = ship != null && ship.autoActions is WingmanActions;
        SetActive(stop, fleet);
        SetActive(hold, fleet);
        SetActive(regroup, fleet);
        SetActive(holdFire, fleet);
        SetActive(aggressive, fleet);
        SetActive(defensive, fleet);
        SetActive(escort, fleet);
        if (data != null)
        {
            var stance = data.fleetStance;
            FleetBarParts.SetTileState(holdFire, stance == FleetStance.HoldFire ? FleetTileState.Active : FleetTileState.Normal);
            FleetBarParts.SetTileState(aggressive, stance == FleetStance.Aggressive ? FleetTileState.Active : FleetTileState.Normal);
            FleetBarParts.SetTileState(defensive, stance == FleetStance.Defensive ? FleetTileState.Active : FleetTileState.Normal);
            FleetBarParts.SetTileState(escort, stance == FleetStance.Escort ? FleetTileState.Active : FleetTileState.Normal);
        }
        bool manage = data != null && PlayerCargo.CanManage(data);
        SetActive(tractor, manage && tractorModule != null);
        if (data != null) tractorIcon.color = data.tractorOff ? ColorHelper.fleetTractorOff : ColorHelper.fleetTractorOn;
        SetActive(drones, droneBay != null);
        if (droneBay != null)
            FleetBarParts.SetTileState(drones, droneBay.AreDronesDeployed() ? FleetTileState.ToggledOn : FleetTileState.Normal);
        SetActive(cargo, manage && !row.isFlagship);
    }

    private static void SetActive(Button tile, bool on)
    {
        var go = tile.gameObject;
        if (go.activeSelf != on) go.SetActive(on);
    }
}

[HarmonyPatch]
internal static class FleetStripPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FleetPanelRow), nameof(FleetPanelRow.Bind))]
    private static void BindPostfix(FleetPanelRow __instance)
    {
        if (!FleetStrip.Instance.Active) return;
        try
        {
            var s = __instance.GetComponent<FleetStripRow>();
            if (s == null) s = __instance.gameObject.AddComponent<FleetStripRow>();
            s.Build(__instance);
            s.Refresh();
        }
        catch (Exception e) { FleetStrip.Instance.Fail(e, nameof(FleetStripPatches)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FleetPanelRow), nameof(FleetPanelRow.Refresh))]
    private static void RefreshPostfix(FleetPanelRow __instance)
    {
        if (!FleetStrip.Instance.Active) return;
        try
        {
            var s = __instance.GetComponent<FleetStripRow>();
            if (s != null) s.Refresh();
        }
        catch (Exception e) { FleetStrip.Instance.Fail(e, nameof(FleetStripPatches)); }
    }
}
