using System;
using System.Collections.Generic;
using Behaviour.Gameplay;
using Behaviour.Managers;
using Behaviour.UI.Missions;
using Behaviour.Util;
using IgiQoL.Core;
using IgiQoL.Features.EchoPriority;
using LightJson;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.MissionSystem;
using Source.Player;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.EchoMissions;

// A mission with one objective at a place, of a chosen kind, that never completes. Only the self-checks build these.
internal sealed class FakeObjective : MissionObjective
{
    private readonly MapPointOfInterest poi;
    private readonly GameplayType type;

    public FakeObjective(MapPointOfInterest poi, GameplayType type)
    {
        this.poi = poi;
        this.type = type;
    }

    public override GameplayType gameplayType { get => type; set { } }
    public override string statusText => "self-check";
    public override bool IsComplete() => false;
    public override MapPointOfInterest GetPoi() => poi;
    public override Sprite GetIcon() => null;
    public override void DataToJson(JsonObject data) { }
    public override void LoadFromJson(JsonObject data) { }
}

// Held() and the game's mission pick with a courier mission you accepted yourself, and the empty-location memory in
// NextOpen and in the game's search. Each check swaps the player's missions for fabricated ones, measures, and puts the
// real ones back before anything else runs.
internal static class MissionCheck
{
    private static Mission Fake(string name, MapPointOfInterest poi, GameplayType type)
    {
        var m = new Mission { name = name, canBeIdled = true };
        var step = new MissionStep();
        step.objectives.Add(new FakeObjective(poi, type));
        m.steps.Add(step);
        return m;
    }

    public static void Run(SelfCheckContext ctx)
    {
        var player = GamePlayer.current;
        var system = player?.currentSystem;
        if (player == null || system == null || !Singleton<IdleManager>.HasInstance || !Singleton<FocusedMissionHandler>.HasInstance)
        {
            ctx.Skip("courier", "no player, system or ECHO");
            ctx.Skip("empty-memory", "no player, system or ECHO");
            return;
        }
        var idle = Singleton<IdleManager>.Instance;
        var focus = Singleton<FocusedMissionHandler>.Instance;
        var here = MapPointOfInterest.current;
        MapPointOfInterest far = null, other = null;
        foreach (var poi in SystemMapData.current.allPointsOfInterest)
        {
            if (poi.hidden || poi is SpaceStation || poi == here || poi.system != system) continue;
            if (far == null) far = poi;
            else if (other == null) { other = poi; break; }
        }
        if (far == null)
        {
            ctx.Skip("courier", "no location that isn't a station in this system");
            ctx.Skip("empty-memory", "no location that isn't a station in this system");
            return;
        }

        var savedMissions = new List<Mission>(player.missions);
        var savedFocus = focus.focusedMission;
        bool savedCourier = player.autopilotSettings.allowCourierMissions;
        try
        {
            focus.SetMission(null);
            Courier(ctx, player, idle, far, other ?? far);
            Memory(ctx, player, far);
        }
        finally
        {
            player.missions.Clear();
            player.missions.AddRange(savedMissions);
            player.autopilotSettings.allowCourierMissions = savedCourier;
            focus.SetMission(savedFocus);
        }
    }

    private static void Swap(GamePlayer player, params Mission[] missions)
    {
        player.missions.Clear();
        player.missions.AddRange(missions);
    }

    // A courier mission first in the list, then a mission of any kind: with courier missions off the game's own pick
    // (the same call ECHO makes) must be the second one, and Held() must not count the courier.
    private static void Courier(SelfCheckContext ctx, GamePlayer player, IdleManager idle, MapPointOfInterest courierPoi, MapPointOfInterest workPoi)
    {
        var courier = Fake("self-check courier", courierPoi, GameplayType.Cargo);
        var work = Fake("self-check mission", workPoi, GameplayType.Generic);
        var ap = player.autopilotSettings;

        Swap(player, courier, work);
        ap.allowCourierMissions = false;
        int heldOff = MissionBatch.Held();
        var pickOff = idle.GetIdleMission();
        ap.allowCourierMissions = true;
        int heldOn = MissionBatch.Held();
        var pickOn = idle.GetIdleMission();
        string detail = $"courier first, then a mission: off -> held {heldOff}, pick '{pickOff?.name}'; on -> held {heldOn}, pick '{pickOn?.name}'";
        if (heldOff == 1 && pickOff == work && heldOn == 2 && pickOn == courier) ctx.Pass("courier-shadowing", detail);
        else ctx.Fail("courier-shadowing", detail + " (expected held 1 and the mission when off, held 2 and the courier when on)");

        Swap(player, courier);
        ap.allowCourierMissions = false;
        var alone = idle.GetIdleMission();
        int heldAlone = MissionBatch.Held();
        string d2 = $"only a courier held, couriers off: pick '{alone?.name}', held {heldAlone}";
        if (alone == null && heldAlone == 0) ctx.Pass("courier-alone", d2);
        else ctx.Fail("courier-alone", d2 + " (expected no pick and held 0)");
    }

    // A location marked empty is skipped by NextOpen and by the game's own travel test during a search, until the
    // memory's window passes or the save loads again. Marking here is what the game's "No valid targets" branch does.
    private static void Memory(SelfCheckContext ctx, GamePlayer player, MapPointOfInterest far)
    {
        var f = EchoPriority.EchoPriority.Instance;
        if (f == null || !f.Active)
        {
            ctx.Skip("empty-memory", "ECHO Activity Priority is off");
            return;
        }
        var work = Fake("self-check open mission", far, GameplayType.Generic);
        Swap(player, work);
        int savedMinutes = f.EmptyMinutes.Value;
        var list = new List<EchoActivity>(EchoPriority.EchoPriority.List);
        EchoTrace.Clear();
        try
        {
            f.EmptyMinutes.Value = 5;
            var travel = Singleton<TravelManager>.Instance;
            var baseline = MissionBatch.NextOpen();
            if (baseline != work)
            {
                ctx.Skip("empty-memory", $"the fabricated mission at {far.name} isn't open to ECHO here (next open: '{baseline?.name}'); no baseline to compare");
                return;
            }

            float t0 = EchoTrace.Now;
            EchoTrace.MarkEmpty(far, t0);
            var marked = MissionBatch.NextOpen();
            EchoTrace.ClockOffset += 4 * 60f;
            var before = MissionBatch.NextOpen();
            EchoTrace.ClockOffset += 2 * 60f;
            var after = MissionBatch.NextOpen();
            string d = $"{far.name} marked empty for 5 minutes: next open at once '{marked?.name}', after 4 min '{before?.name}', after 6 min '{after?.name}'";
            if (marked == null && before == null && after == work) ctx.Pass("empty-next-open", d);
            else ctx.Fail("empty-next-open", d + " (expected none, none, then the mission)");

            // The game's search asks the travel manager about each location; only while the search runs does it hide remembered ones.
            EchoTrace.ClockOffset = 0f;
            bool canTravel = travel.CanWeTravel(far);
            if (!canTravel)
            {
                ctx.Skip("empty-search", $"the game's own travel test already refuses {far.name}; no baseline");
            }
            else
            {
                EchoTrace.MarkEmpty(far, EchoTrace.Now);
                bool outside = travel.CanWeTravel(far);
                EchoTrace.Searching = true;
                bool during = travel.CanWeTravel(far);
                EchoTrace.ClockOffset += 6 * 60f;
                bool expired = travel.CanWeTravel(far);
                EchoTrace.Searching = false;
                string d2 = $"{far.name} marked empty: travel test outside a search {outside}, during a search {during}, during a search after 6 min {expired}";
                if (outside && !during && expired) ctx.Pass("empty-search", d2);
                else ctx.Fail("empty-search", d2 + " (expected true, false, true)");
            }

            // Switching the setting to 0 means nothing is remembered.
            EchoTrace.Clear();
            EchoTrace.ClockOffset = 0f;
            f.EmptyMinutes.Value = 0;
            EchoTrace.MarkEmpty(far, EchoTrace.Now);
            var off = MissionBatch.NextOpen();
            if (off == work) ctx.Pass("empty-off", $"setting 0: next open '{off?.name}' right after marking");
            else ctx.Fail("empty-off", $"setting 0: next open '{off?.name}' right after marking, expected the mission");

            // Loading a save forgets everything.
            f.EmptyMinutes.Value = 5;
            EchoTrace.ClockOffset = 0f;
            EchoTrace.MarkEmpty(far, EchoTrace.Now);
            int remembered = EchoTrace.EmptyCount;
            f.OnSaveLoaded();
            int afterLoad = EchoTrace.EmptyCount;
            var open = MissionBatch.NextOpen();
            string d3 = $"remembered {remembered} place(s), after the save-load handler {afterLoad}, next open '{open?.name}'";
            if (remembered == 1 && afterLoad == 0 && open == work) ctx.Pass("empty-reset-on-load", d3);
            else ctx.Fail("empty-reset-on-load", d3 + " (expected 1, 0 and the mission)");
        }
        finally
        {
            f.EmptyMinutes.Value = savedMinutes;
            EchoPriority.EchoPriority.List = list;
            EchoTrace.ClockOffset = 0f;
            EchoTrace.Searching = false;
            EchoTrace.Clear();
        }
    }
}
