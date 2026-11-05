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
    public override string Title => "Fleet Command Strip";
    public override string Description => "Adds a row of buttons under each ship in the fleet panel (bottom left): Stop, Hold, Regroup, the four stances (Hold Fire, Aggressive, Defensive, Escort), the tractor beam, the Drone Bay, Cargo, and the ship's own settings. A button that doesn't apply to a ship stays in its place, dimmed.";
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
                + (Separation.Separation.Instance.Active ? 1 : 0) + (FleetRetreat.FleetRetreat.Instance.Active ? 1 : 0)
                + (TargetPriority.TargetPriority.Instance.Active ? 5 + TargetPriority.TargetPriority.CombatWeaponCount(ship) : 0)
                + (CargoPins.CargoPins.Instance.Active ? 1 : 0);
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

            // The strip's Hold tile is the fleet Hold order and says so in the game's own words, like the HUD's bar.
            var holdTip = strip.hold.GetComponent<Behaviour.UI.Tooltip.TooltipSource>();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            string gameHold = Translation.Translate("@FleetCommandHold", kb == null ? "C" : kb[UnityEngine.InputSystem.Key.C].displayName);
            string holdBody = holdTip == null ? null : holdTip.BodyText;
            string holdDetail = $"game text '{gameHold}', tile tooltip '{holdBody}'";
            if (holdBody != null && gameHold.Length > 0 && holdBody.StartsWith(gameHold, StringComparison.Ordinal) && holdBody.Contains("holds only the ship you fly")) ctx.Pass("hold-label", holdDetail);
            else ctx.Fail("hold-label", holdDetail);

            // Every order tile shows the game's text for its order, with the key the fleet bar binds to it.
            var specs = new (Button tile, string key, UnityEngine.InputSystem.Key hotkey)[]
            {
                (strip.stop, "@FleetCommandStop", UnityEngine.InputSystem.Key.X), (strip.regroup, "@FleetCommandRegroup", UnityEngine.InputSystem.Key.V),
                (strip.holdFire, "@FleetCommandStanceHoldFire", UnityEngine.InputSystem.Key.F1), (strip.aggressive, "@FleetCommandStanceAggressive", UnityEngine.InputSystem.Key.F2),
                (strip.defensive, "@FleetCommandStanceDefensive", UnityEngine.InputSystem.Key.F3), (strip.escort, "@FleetCommandStanceEscort", UnityEngine.InputSystem.Key.F4),
            };
            int matching = 0;
            string firstBad = null;
            foreach (var spec in specs)
            {
                string expected = Translation.Translate(spec.key, FleetStripRow.KeyName(spec.hotkey));
                var body = spec.tile.GetComponent<Behaviour.UI.Tooltip.TooltipSource>()?.BodyText;
                if (body != null && expected.Length > 0 && body.StartsWith(expected, StringComparison.Ordinal)) matching++;
                else firstBad ??= spec.key;
            }
            string tractorBody = strip.tractor.GetComponent<Behaviour.UI.Tooltip.TooltipSource>()?.BodyText;
            string tractorOn = Translation.Translate("@FleetCargoTractorTipOn"), tractorOff = Translation.Translate("@FleetCargoTractorTipOff");
            string tipDetail = $"{matching}/{specs.Length} order tiles match the game text{(firstBad == null ? "" : ", first wrong " + firstBad)}; tractor tile '{tractorBody}' (game: '{tractorOn}' / '{tractorOff}')";
            if (matching == specs.Length && tractorOn.Length > 0 && (tractorBody == tractorOn || tractorBody == tractorOff)) ctx.Pass("tile-labels", tipDetail);
            else ctx.Fail("tile-labels", tipDetail);

            // A tile stays in its slot whether or not it applies to the ship: the strip never shifts when a state changes.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            var slots = rt.GetComponentsInChildren<FleetStripRow.TileSlot>(true);
            var enabledX = new float[slots.Length];
            for (int i = 0; i < slots.Length; i++) { slots[i].Enable(true); enabledX[i] = ((RectTransform)slots[i].transform).anchoredPosition.x; }
            for (int i = 0; i < slots.Length; i++) slots[i].Enable(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            float maxShift = 0f;
            int hidden = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                maxShift = Mathf.Max(maxShift, Mathf.Abs(((RectTransform)slots[i].transform).anchoredPosition.x - enabledX[i]));
                if (!slots[i].gameObject.activeSelf) hidden++;
            }
            string slotDetail = $"{slots.Length} tiles, {hidden} hidden, largest x shift between all enabled and all disabled {maxShift:0.##}";
            if (slots.Length == 11 && hidden == 0 && maxShift < 0.01f) ctx.Pass("slots", slotDetail);
            else ctx.Fail("slots", slotDetail);
            strip.settings.GetComponent<FleetStripRow.TileSlot>().Enable(true);

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
    public RectTransform strip;

    private readonly List<SpaceShip> one = new(1);
    private SpaceShip cachedShip;
    private TractorModule tractorModule;
    private DroneBayModule droneBay;
    // What the tiles last showed, so unchanged state costs nothing.
    private FleetStance lastStance = (FleetStance)(-1);
    private int lastDeployed = -1, lastTractorOff = -1;

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

        stop = Tile(PoiIconKeys.Fleet("Stop"), ColorHelper.fleetCommandStopIcon, CommandTooltip("@FleetCommandStop", UnityEngine.InputSystem.Key.X), OnStop);
        hold = Tile(PoiIconKeys.Fleet("HoldPosition"), ColorHelper.fleetCommandHoldIcon, HoldTooltip(), OnHold);
        regroup = Tile(PoiIconKeys.Fleet("Regroup"), ColorHelper.fleetCommandRegroupIcon, CommandTooltip("@FleetCommandRegroup", UnityEngine.InputSystem.Key.V), OnRegroup);
        holdFire = Tile(PoiIconKeys.Fleet("HoldFire"), ColorHelper.fleetCommandHoldFireIcon, CommandTooltip("@FleetCommandStanceHoldFire", UnityEngine.InputSystem.Key.F1), OnHoldFire);
        aggressive = Tile(PoiIconKeys.Fleet("Aggressive"), ColorHelper.fleetCommandAggressiveIcon, CommandTooltip("@FleetCommandStanceAggressive", UnityEngine.InputSystem.Key.F2), OnAggressive);
        defensive = Tile(PoiIconKeys.Fleet("Defend"), ColorHelper.fleetCommandDefensiveIcon, CommandTooltip("@FleetCommandStanceDefensive", UnityEngine.InputSystem.Key.F3), OnDefensive);
        escort = Tile(PoiIconKeys.Fleet("Escort"), ColorHelper.fleetCommandEscortIcon, CommandTooltip("@FleetCommandStanceEscort", UnityEngine.InputSystem.Key.F4), OnEscort);
        tractor = Tile(PoiIconKeys.Fleet("Tractor"), ColorHelper.offWhite, TractorTooltip(false), OnTractor);
        drones = Tile(Kit.Icon("Ability", "Fleet.Drones", "Fleet.Drone", "Fleet.Hangar"), ColorHelper.offWhite, DroneTooltip(false), OnDrones);
        cargo = Tile(PoiIconKeys.Fleet("FleetCargo"), ColorHelper.offWhite, Translation.Translate("@FleetRowCargoGaugeTitle"), OnCargo);
        settings = Tile(Kit.Icon("Info", PoiIconKeys.RouteUi("Edit")), ColorHelper.offWhite, "Ship settings", OnSettings);
        Refresh();
    }

    // The game's text for a fleet order, with the key the HUD's fleet bar binds to it. That key orders every selected
    // ship; this tile orders only its own ship.
    internal static string CommandTooltip(string key, UnityEngine.InputSystem.Key hotkey) =>
        Translation.Translate(key, KeyName(hotkey)) + "\n\nThis button orders only this ship. The hotkey orders every selected ship.";

    // The game's own tractor tile text (its cargo window uses the same two lines); the tile toggles that state.
    internal static string TractorTooltip(bool off) => Translation.Translate(off ? "@FleetCargoTractorTipOff" : "@FleetCargoTractorTipOn");

    // What a click does now: the game's own words for it (Drone Bay, Deploy, Return).
    internal static string DroneTooltip(bool deployed) =>
        Translation.Translate("@EquipSlotDroneBay") + ": " + Translation.Translate(deployed ? "@UIDroneReturn" : "@UIDroneDeploy");

    // The game's own text for the Hold order, as its fleet bar shows it (the hotkey name goes into the text), then what
    // the tile does here. The HUD's Hold position switch is a different control that reaches only the ship you fly.
    internal static string HoldTooltip() =>
        Translation.Translate("@FleetCommandHold", HoldKeyName()) +
        "\n\nHolds this ship only: it stays at its spot until you give it another order or the fleet travels. The Hold position switch on the HUD holds only the ship you fly.";

    internal static string HoldKeyName() => KeyName(UnityEngine.InputSystem.Key.C);

    // The key's name as the game's fleet bar shows it.
    internal static string KeyName(UnityEngine.InputSystem.Key key)
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        return keyboard == null ? key.ToString() : keyboard[key].displayName;
    }

    private Button Tile(string key, Color tint, string tooltip, Action onClick) =>
        Slot(Kit.Tile(strip, key, tint, 16f, () => onClick(), tooltip), tint);

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
        var unit = data.unit as SpaceShip;
        if (unit != null) unit.ApplyTractorToggle();
        Refresh();
    }

    private void OnDrones()
    {
        var ship = Ship();
        var bay = ship != null ? ship.GetModule<DroneBayModule>() : null;
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
        // Every tile keeps its slot: one that doesn't apply to this ship is dimmed and can't be clicked, as the HUD's fleet bar does.
        bool fleet = ship != null && ship.autoActions is WingmanActions;
        Enable(stop, fleet);
        Enable(hold, fleet);
        Enable(regroup, fleet);
        Enable(holdFire, fleet);
        Enable(aggressive, fleet);
        Enable(defensive, fleet);
        Enable(escort, fleet);
        // SetTileState recolours the border (a Find and an array each); only when the state changed.
        if (data != null && data.fleetStance != lastStance)
        {
            var stance = lastStance = data.fleetStance;
            FleetBarParts.SetTileState(holdFire, stance == FleetStance.HoldFire ? FleetTileState.Active : FleetTileState.Normal);
            FleetBarParts.SetTileState(aggressive, stance == FleetStance.Aggressive ? FleetTileState.Active : FleetTileState.Normal);
            FleetBarParts.SetTileState(defensive, stance == FleetStance.Defensive ? FleetTileState.Active : FleetTileState.Normal);
            FleetBarParts.SetTileState(escort, stance == FleetStance.Escort ? FleetTileState.Active : FleetTileState.Normal);
        }
        bool manage = data != null && PlayerCargo.CanManage(data);
        if (data != null)
        {
            int off = data.tractorOff ? 1 : 0;
            if (off != lastTractorOff)
            {
                lastTractorOff = off;
                tractor.GetComponent<TileSlot>().SetTint(off == 1 ? ColorHelper.fleetTractorOff : ColorHelper.fleetTractorOn);
                FleetBarParts.SetTooltip(tractor, null, TractorTooltip(off == 1));
            }
        }
        Enable(tractor, manage && tractorModule != null);
        if (droneBay != null)
        {
            int deployed = droneBay.AreDronesDeployed() ? 1 : 0;
            if (deployed != lastDeployed)
            {
                lastDeployed = deployed;
                FleetBarParts.SetTileState(drones, deployed == 1 ? FleetTileState.ToggledOn : FleetTileState.Normal);
                FleetBarParts.SetTooltip(drones, null, DroneTooltip(deployed == 1));
            }
        }
        Enable(drones, droneBay != null);
        Enable(cargo, manage && !row.isFlagship);
        Enable(settings, true);
    }

    private static void Enable(Button tile, bool on) => tile.GetComponent<TileSlot>().Enable(on);

    // A tile's icon colour and whether it is enabled, so a change is applied once.
    internal sealed class TileSlot : MonoBehaviour
    {
        public Button button;
        public Image icon;
        public Color tint;
        // -1 unknown, 0 disabled, 1 enabled.
        public int state = -1;

        public void SetTint(Color color)
        {
            tint = color;
            state = -1;
        }

        public void Enable(bool on)
        {
            int want = on ? 1 : 0;
            if (state == want) return;
            state = want;
            button.interactable = on;
            icon.color = on ? tint : tint * ColorHelper.panelButtonDisabled;
        }
    }

    // The tile's own slot data: set up when the tile is built.
    private Button Slot(Button tile, Color tint)
    {
        var slot = tile.gameObject.AddComponent<TileSlot>();
        slot.button = tile;
        slot.icon = tile.transform.Find("Icon").GetComponent<Image>();
        slot.tint = tint;
        return tile;
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
