using System;
using System.Collections;
using Behaviour.Equipment.Module;
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
    public override string Title => "Drones on Arrival";
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

    internal void OnArrived(SpaceShip ship)
    {
        if (!Active) return;
        try
        {
            if (!Fleet.IsPlayerShip(ship)) return;
            if (GamePlayer.current.currentPointOfInterest is SpaceStation) return;
            // A stop on a longer route: the reached waypoint is still listed, and the next leg would only recall the
            // drones again and wait for them.
            if (GamePlayer.current.waypoints.Count > 1) return;
            if (!IsOn(ship)) return;
            var bay = ship.droneBayModule;
            if (bay == null || bay.playerRecalled || !bay.AreDronesDocked()) return;
            bay.DeployDrones();
        }
        catch (Exception e) { Fail(e, nameof(OnArrived)); }
    }

    private bool IsOn(SpaceShip ship) => ShipSettings.Get(ship.spaceShipData.guid).dronesOnArrival ?? Default.Value;

    private static readonly string[] CheckNames = { "deploy", "deploy-recalled", "deploy-off", "deploy-route", "deploy-station" };

    // Calls the arrival handler itself (the game's own arrival needs a jump) with each rule in turn and watches the
    // flagship's drones: out on arrival, not after a recall, not when the ship's setting is off, not on a stop of a
    // longer route, not at a station.
    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        var ship = Fleet.Flagship;
        if (ship == null) { foreach (var n in CheckNames) ctx.Skip(n, "not in space"); yield break; }
        var bay = ship.droneBayModule;
        if (bay == null)
        {
            foreach (var n in CheckNames) ctx.Skip(n, "flagship has no drone bay");
            yield break;
        }
        var player = GamePlayer.current;
        var g = ship.spaceShipData.guid;
        bool defaultBefore = Default.Value;
        var settingBefore = ShipSettings.Get(g).dronesOnArrival;
        var route = new System.Collections.Generic.List<Source.Galaxy.MapPointOfInterest>(player.waypoints);
        ctx.Cleanup(() =>
        {
            Default.Value = defaultBefore;
            ShipSettings.Edit(g).dronesOnArrival = settingBefore;
            ShipSettings.Save(g);
            bay.playerRecalled = false;
        });

        // Start from drones docked.
        yield return DockDrones(bay);
        if (!bay.AreDronesDocked() || !bay.AllDronesDocked())
        {
            foreach (var n in CheckNames) ctx.Skip(n, "the flagship's drones would not dock");
            yield break;
        }

        // The handler reads the current location when it is called, so it is called with the location swapped and
        // the real one put straight back; the waits that follow run at the real location.
        Source.Galaxy.MapPointOfInterest open = null, station = null;
        var here = player.currentPointOfInterest;
        if (here is SpaceStation) station = here; else open = here;
        foreach (var p in Source.Galaxy.SystemMapData.current.allPointsOfInterest)
        {
            if (p is SpaceStation) { if (station == null) station = p; }
            else if (open == null) open = p;
        }
        void Arrive(Source.Galaxy.MapPointOfInterest at)
        {
            var keep = player.currentPointOfInterest;
            player.currentPointOfInterest = at;
            try { OnArrived(ship); }
            finally { player.currentPointOfInterest = keep; }
        }

        Default.Value = true;
        ShipSettings.Edit(g).dronesOnArrival = null;
        try
        {
            player.waypoints.Clear();
            if (open == null)
            {
                foreach (var n in new[] { "deploy", "deploy-recalled", "deploy-off", "deploy-route" }) ctx.Skip(n, "no location in this system that isn't a station");
            }
            else
            {
                // Recalled by hand: stays docked.
                bay.playerRecalled = true;
                Arrive(open);
                yield return new WaitForSecondsRealtime(3f);
                string recalledDetail = $"after a recall: deployed={bay.AreDronesDeployed()} deploying={bay.deploying}";
                if (!bay.AreDronesDeployed() && !bay.deploying) ctx.Pass("deploy-recalled", recalledDetail);
                else ctx.Fail("deploy-recalled", recalledDetail);
                bay.playerRecalled = false;
                yield return DockDrones(bay);

                // Off for this ship: stays docked.
                ShipSettings.Edit(g).dronesOnArrival = false;
                Arrive(open);
                yield return new WaitForSecondsRealtime(3f);
                string offDetail = $"ship setting off, default on: deployed={bay.AreDronesDeployed()} deploying={bay.deploying}";
                if (!bay.AreDronesDeployed() && !bay.deploying) ctx.Pass("deploy-off", offDetail);
                else ctx.Fail("deploy-off", offDetail);
                ShipSettings.Edit(g).dronesOnArrival = null;
                yield return DockDrones(bay);

                // A stop on a longer route: stays docked.
                player.waypoints.Add(open);
                player.waypoints.Add(open);
                Arrive(open);
                yield return new WaitForSecondsRealtime(3f);
                string routeDetail = $"{player.waypoints.Count} waypoints listed: deployed={bay.AreDronesDeployed()} deploying={bay.deploying}";
                if (!bay.AreDronesDeployed() && !bay.deploying) ctx.Pass("deploy-route", routeDetail);
                else ctx.Fail("deploy-route", routeDetail);
                player.waypoints.Clear();
                yield return DockDrones(bay);

                // The real thing: last stop, setting on, drones docked.
                Arrive(open);
                float until = Time.realtimeSinceStartup + 10f;
                while (!bay.AreDronesDeployed() && Time.realtimeSinceStartup < until) yield return new WaitForSecondsRealtime(0.5f);
                string detail = $"docked={bay.AreDronesDocked()} deployed={bay.AreDronesDeployed()} at {open.GetType().Name}";
                if (bay.AreDronesDeployed()) ctx.Pass("deploy", detail);
                else ctx.Fail("deploy", detail);
                yield return DockDrones(bay);
            }

            // At a station: stays docked.
            if (station == null) ctx.Skip("deploy-station", "no station in this system");
            else
            {
                Arrive(station);
                yield return new WaitForSecondsRealtime(3f);
                string stationDetail = $"at a station: deployed={bay.AreDronesDeployed()} deploying={bay.deploying}";
                if (!bay.AreDronesDeployed() && !bay.deploying) ctx.Pass("deploy-station", stationDetail);
                else ctx.Fail("deploy-station", stationDetail);
            }
        }
        finally
        {
            player.waypoints.Clear();
            player.waypoints.AddRange(route);
            Default.Value = defaultBefore;
            ShipSettings.Edit(g).dronesOnArrival = settingBefore;
            ShipSettings.Save(g);
            bay.playerRecalled = false;
            // The other checks expect the drones out, as before this one.
            if (!bay.AreDronesDeployed() && !bay.deploying) bay.DeployDrones();
        }
    }

    // Recalls the drones and waits (up to 20 s) until none is deployed.
    private static IEnumerator DockDrones(DroneBayModule bay)
    {
        if (bay.AllDronesDocked()) yield break;
        bay.ReturnDrones();
        float until = Time.realtimeSinceStartup + 20f;
        while (!bay.AllDronesDocked() && Time.realtimeSinceStartup < until) yield return new WaitForSecondsRealtime(0.5f);
        yield return new WaitForSecondsRealtime(1f);
    }
}
