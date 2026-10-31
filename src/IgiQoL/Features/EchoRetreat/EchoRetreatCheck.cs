using System.Collections;
using Behaviour.Gameplay;
using Behaviour.Managers;
using Behaviour.UI.Side_Menu;
using Behaviour.UI.Spacestation;
using Behaviour.Unit;
using Behaviour.Util;
using IgiQoL.Core;
using Source.Galaxy;
using IgiQoL.UI;
using Source.Player;
using TMPro;
using UnityEngine;

namespace IgiQoL.Features.EchoRetreat;

internal static class EchoRetreatCheck
{
    public static IEnumerator Run(SelfCheckContext ctx)
    {
        yield return Tab(ctx);
        yield return SelfCheckContext.EnsureInSpace();
        var f = EchoRetreat.Instance;
        var gm = GameplayManager.Instance;
        var flagship = gm != null ? gm.spaceShip : null;
        if (flagship == null)
        {
            ctx.Skip("low-ship", "no flagship in space");
            ctx.Skip("retreat", "no flagship in space");
            yield break;
        }
        SpaceShip wing = null;
        foreach (var s in gm.fleetSpaceShips)
            if (s != null && s != flagship && !s.travelling && s.gameObject.activeInHierarchy && !s.isDestroyed) { wing = s; break; }

        int flag0 = f.FlagshipHullPercent.Value, fleet0 = f.FleetHullPercent.Value;
        float flagHull = flagship.currentHullHP, wingHull = wing != null ? wing.currentHullHP : 0f;
        try
        {
            f.FlagshipHullPercent.Value = 50;
            f.FleetHullPercent.Value = 0;
            flagship.currentHullHP = flagship.maxHullHP * 0.4f;
            string at40 = f.LowShip();
            flagship.currentHullHP = flagship.maxHullHP * 0.6f;
            string at60 = f.LowShip();
            flagship.currentHullHP = flagship.maxHullHP * 0.4f;
            f.FlagshipHullPercent.Value = 0;
            string off = f.LowShip();
            flagship.currentHullHP = flagHull;
            string wingLow = null, wingOff = null;
            if (wing != null)
            {
                f.FleetHullPercent.Value = 50;
                wing.currentHullHP = wing.maxHullHP * 0.3f;
                wingLow = f.LowShip();
                f.FleetHullPercent.Value = 0;
                wingOff = f.LowShip();
            }
            string detail = $"threshold 50: flagship at 40% -> '{at40}', at 60% -> '{at60}'; threshold 0 at 40% -> '{off}'; fleet ship {(wing != null ? wing.spaceShipData.GetShipName() : "-")} at 30% with 50 -> '{wingLow}', with 0 -> '{wingOff}'";
            bool ok = at40 != null && at40.Contains("40%") && at60 == null && off == null && (wing == null || (wingLow != null && wingLow.StartsWith(wing.spaceShipData.GetShipName()) && wingOff == null));
            if (ok) ctx.Pass("low-ship", detail);
            else ctx.Fail("low-ship", detail);
        }
        finally
        {
            flagship.currentHullHP = flagHull;
            if (wing != null) wing.currentHullHP = wingHull;
            f.FlagshipHullPercent.Value = flag0;
            f.FleetHullPercent.Value = fleet0;
        }

        // Live: ECHO on with the flagship at 20% and a 30% threshold flies off; the check calls the trip off again.
        var tab = SidePanel.instance != null ? SidePanel.instance.autopilotTab : null;
        var player = GamePlayer.current;
        var travel = Singleton<TravelManager>.Instance;
        if (tab == null || !player.autoPlayUnlocked)
        {
            ctx.Skip("retreat", "ECHO isn't available in this save");
            yield break;
        }
        // Next to a station ECHO stays put (it docks there anyway); the live check needs a location in open space.
        if (MapPointOfInterest.current is Source.Galaxy.POI.SpaceStation station)
        {
            if (EchoRetreat.CanLeaveHere()) ctx.Fail("at-station", $"would leave {station.name}");
            else ctx.Pass("at-station", $"no retreat from {station.name}");
            MapPointOfInterest away = null;
            foreach (var p in SystemMapData.current.allPointsOfInterest)
                if (!p.hidden && (p is Source.Galaxy.POI.Combat || p is Source.Galaxy.POI.Mining || p is Source.Galaxy.POI.Salvage)) { away = p; break; }
            if (away != null)
            {
                travel.SetRouteToPOI(away);
                float arrive = Time.realtimeSinceStartup + 180f;
                while (Time.realtimeSinceStartup < arrive && (MapPointOfInterest.current != away || travel.TravelActive())) yield return null;
                yield return new WaitForSeconds(3f);
                Plugin.Log.LogInfo($"[EchoRetreat] check flew to {away.name}: arrived={MapPointOfInterest.current == away}");
            }
        }
        if (!EchoRetreat.CanLeaveHere())
        {
            ctx.Skip("retreat", $"ECHO wouldn't leave here (location {MapPointOfInterest.current?.name}, travelling {travel.TravelActive()}, operation running {Singleton<DungeonManager>.Instance?.HasAnyActiveOperation})");
            yield break;
        }
        var here = MapPointOfInterest.current;
        var flagData = flagship.spaceShipData;
        bool echoWasOn = player.autoPlay;
        bool noTravel0 = player.autopilotSettings.noTravel;
        try
        {
            player.autopilotSettings.noTravel = false;
            f.FlagshipHullPercent.Value = 30;
            f.FleetHullPercent.Value = 0;
            flagship.currentHullHP = flagship.maxHullHP * 0.2f;
            // ECHO's own next decision is held back, so a "nothing to do here, going home" from it can't win the race.
            Singleton<IdleManager>.Instance.updateTimer = 30f;
            if (!player.autoPlay) tab.ToggleChanged();
            float until = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < until && !travel.TravelActive()) yield return null;
            bool left = travel.TravelActive();
            var target = travel.targetPoi as Source.Galaxy.POI.SpaceStation;
            string status = SidePanel.instance.idleStatusMessage.text;
            string detail = $"left {here?.name}={left} for {(travel.targetPoi != null ? travel.targetPoi.name : "none")} (a station: {target != null}); status '{status}'";
            if (left && target != null && status.StartsWith("Retreating")) ctx.Pass("retreat", detail);
            else ctx.Fail("retreat", detail);
            if (target == null) yield break;

            // ECHO carries on by itself: arrives, docks, and the hangar's free auto-repair starts on the flagship.
            float dockBy = Time.realtimeSinceStartup + 240f;
            while (Time.realtimeSinceStartup < dockBy && SpaceStationInterior.instance == null) yield return new WaitForSeconds(1f);
            yield return new WaitForSeconds(3f);
            var hangar = Source.Spacestation.PersonalHangar.current;
            bool repairing = false;
            if (hangar != null)
                foreach (var job in hangar.jobs)
                    if (job.spaceshipGuid == flagData.guid) repairing = true;
            float hull = flagData.currentHullHP / Mathf.Max(1f, flagData.maxHullHP);
            string dockDetail = $"docked at {MapPointOfInterest.current?.name}={SpaceStationInterior.instance != null}, flagship repair job={repairing}, hull {hull * 100f:0}%, status '{SidePanel.instance.idleStatusMessage.text}'";
            if (SpaceStationInterior.instance != null && MapPointOfInterest.current == target && (repairing || hull >= 0.99f)) ctx.Pass("dock-repair", dockDetail);
            else ctx.Fail("dock-repair", dockDetail);
        }
        finally
        {
            if (player.autoPlay != echoWasOn) tab.ToggleChanged();
            if (travel.TravelActive()) travel.CancelTravel();
            player.autopilotSettings.noTravel = noTravel0;
            flagData.currentHullHP = flagHull;
            f.FlagshipHullPercent.Value = flag0;
            f.FleetHullPercent.Value = fleet0;
        }
    }

    // The ECHO tab lists the section with both thresholds; the screenshot scrolls it to the top of the view.
    private static IEnumerator Tab(SelfCheckContext ctx)
    {
        var f = EchoRetreat.Instance;
        ModWindow.Open("echo");
        yield return null;
        TMP_Text heading = null;
        bool flagRow = false, fleetRow = false;
        if (ModWindow.IsOpen)
            foreach (var t in ModWindow.Content.GetComponentsInChildren<TMP_Text>())
            {
                if (t.text == f.Title) heading = t;
                flagRow |= t.text == "Retreat when the flagship's hull is below (%)";
                fleetRow |= t.text == "Retreat when a fleet ship's hull is below (%)";
            }
        string detail = $"heading={heading != null} flagship row={flagRow} fleet row={fleetRow}";
        if (heading != null && flagRow && fleetRow) ctx.Pass("tab", detail);
        else ctx.Fail("tab", detail);
        var scroll = ModWindow.IsOpen ? ModWindow.Content.GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
        if (scroll != null && heading != null)
        {
            Canvas.ForceUpdateCanvases();
            var content = scroll.content;
            float below = -content.InverseTransformPoint(heading.rectTransform.position).y - 8f;
            float range = content.rect.height - scroll.viewport.rect.height;
            if (range > 0f) scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - below / range);
        }
        yield return ctx.Screenshot("window-echo-retreat");
        ModWindow.Close();
    }
}
