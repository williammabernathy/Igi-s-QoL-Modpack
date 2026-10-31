using System;
using System.Collections;
using Behaviour.Unit;
using BepInEx.Configuration;
using IgiQoL.Core;
using IgiQoL.Game;
using Source.Galaxy.POI;
using Source.Player;
using UnityEngine;

namespace IgiQoL.Features.DronesOnArrival;

public sealed class DronesOnArrival : Feature
{
    public static DronesOnArrival Instance;

    public DronesOnArrival() { Instance = this; }

    public override string Id => "DronesOnArrival";
    public override string Title => "Drones on arrival";
    public override string Description => "Launches your ships' docked drones by themselves when the ship arrives somewhere that isn't a station.";
    public override Type[] PatchClasses => Type.EmptyTypes;

    public ConfigEntry<bool> Default;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        Default = cfg.Bind(Id, "Default", false, SettingInfo.Describe("Launch drones on arrival",
            "When on, every ship of yours launches its docked drones as soon as it arrives somewhere that isn't a station. Drones you recalled yourself stay docked. Each ship can override this in its own settings (the last button on its row in the fleet panel)."));
    }

    public override void OnActivated() => GameEvents.Arrived += OnArrived;

    private void OnArrived(SpaceShip ship)
    {
        if (!Active) return;
        try
        {
            if (!Fleet.IsPlayerShip(ship)) return;
            if (GamePlayer.current.currentPointOfInterest is SpaceStation) return;
            if (!IsOn(ship)) return;
            var bay = ship.droneBayModule;
            if (bay == null || bay.playerRecalled || !bay.AreDronesDocked()) return;
            bay.DeployDrones();
        }
        catch (Exception e) { Fail(e, nameof(OnArrived)); }
    }

    private bool IsOn(SpaceShip ship) => ShipSettings.Get(ship.spaceShipData.guid).dronesOnArrival ?? Default.Value;

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        var ship = Fleet.Flagship;
        if (ship == null) { ctx.Skip("deploy", "not in space"); yield break; }
        var bay = ship.droneBayModule;
        if (bay == null)
        {
            ctx.Skip("deploy", "flagship has no drone bay");
            yield break;
        }
        if (!bay.AreDronesDocked())
        {
            ctx.Skip("deploy", "flagship drones are not docked");
            yield break;
        }
        bay.DeployDrones();
        yield return new WaitForSeconds(3f);
        string detail = $"docked={bay.AreDronesDocked()} deployed={bay.AreDronesDeployed()} at {GamePlayer.current.currentPointOfInterest?.GetType().Name}";
        if (bay.AreDronesDeployed()) ctx.Pass("deploy", detail);
        else ctx.Fail("deploy", detail);
    }
}
