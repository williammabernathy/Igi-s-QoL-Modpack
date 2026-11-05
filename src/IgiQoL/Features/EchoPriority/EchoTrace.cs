using System;
using System.Collections.Generic;
using System.Text;
using Behaviour.Gameplay;
using Behaviour.Managers;
using Behaviour.UI.Missions;
using Behaviour.UI.Side_Menu;
using Behaviour.Util;
using HarmonyLib;
using Source.Galaxy;
using Source.Player;
using IgiQoL.Features.EchoMissions;
using UnityEngine;

namespace IgiQoL.Features.EchoPriority;

// Two things that make ECHO's back-and-forth trips diagnosable and breakable.
//
// The trace keeps ECHO's last few destinations in a fixed ring. When the last four are A, B, A, B inside a few minutes,
// it writes one line to the BepInEx log and to the Log tab naming both places and what ECHO had decided and why.
//
// The empty-location memory remembers where ECHO arrived and found nothing it could work on (the game's "No valid
// targets for modules" branch), so its next search for something to do, and the mod's flight to the next open mission,
// skip that place for a while. Without it the game's search ("this location has something to work on") and its
// handler at the location ("nothing here I can use") can disagree forever.
internal static class EchoTrace
{
    private const int RingSize = 12;
    // Four trips inside this long is a loop. A real mining or salvage cycle (fill the hold, unload, fly back) is longer.
    internal const float LoopSeconds = 360f;
    private const float SameTripSeconds = 5f;

    private sealed class Hop
    {
        public string from, to, status, mission, step, reason;
        public float at;
    }

    private static readonly Hop[] ring = MakeRing();
    private static int total;
    private static string reportedA, reportedB;

    private static Hop[] MakeRing()
    {
        var r = new Hop[RingSize];
        for (int i = 0; i < r.Length; i++) r[i] = new Hop();
        return r;
    }

    // Added to the clock by self-checks to jump past the memory window without waiting.
    internal static float ClockOffset;
    internal static float Now => Time.unscaledTime + ClockOffset;

    // What decided the latest trip, set by the code that decides and cleared at the start of each ECHO decision.
    internal static string Step;

    // True while the game's search for an activity runs (TryPreferredActivity); only then are remembered places hidden.
    internal static bool Searching;
    // True while the game's handler at a location runs (IdleActivityInPoi).
    internal static bool InLocation;

    private static readonly Dictionary<MapPointOfInterest, float> empty = new();
    private static string emptyName;
    private static float emptyAt;

    internal static float WindowSeconds
    {
        get
        {
            var f = EchoPriority.Instance;
            return f != null && f.Active ? f.EmptyMinutes.Value * 60f : 0f;
        }
    }

    internal static void Clear()
    {
        empty.Clear();
        emptyName = null;
        total = 0;
        reportedA = reportedB = null;
        Step = null;
        Searching = InLocation = false;
        for (int i = 0; i < ring.Length; i++) ring[i].from = ring[i].to = ring[i].status = ring[i].mission = ring[i].step = ring[i].reason = null;
    }

    // ECHO reached poi and had nothing it could work on there.
    internal static void MarkEmpty(MapPointOfInterest poi, float now)
    {
        if (poi == null) return;
        emptyName = poi.name;
        emptyAt = now;
        float window = WindowSeconds;
        if (window > 0f) empty[poi] = now + window;
    }

    internal static bool IsEmpty(MapPointOfInterest poi, float now)
    {
        if (poi == null || empty.Count == 0 || !empty.TryGetValue(poi, out float until)) return false;
        if (until > now) return true;
        empty.Remove(poi);
        return false;
    }

    internal static int EmptyCount => empty.Count;

    private static string EmptyPlaces(float now)
    {
        var sb = new StringBuilder();
        foreach (var kv in empty)
        {
            if (kv.Value <= now) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(kv.Key.name);
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    // Records one trip. Returns the report line when this trip completes an A, B, A, B pattern that hasn't been reported yet.
    internal static string RecordHop(string from, string to, float now, string status, string mission, string step, string reason, string activityList)
    {
        if (to == null) return null;
        if (total > 0)
        {
            var prev = ring[(total - 1) % RingSize];
            if (prev.to == to && now - prev.at < SameTripSeconds) return null;   // the same trip noted twice
        }
        var h = ring[total % RingSize];
        h.from = from;
        h.to = to;
        h.at = now;
        h.status = status;
        h.mission = mission;
        h.step = step;
        h.reason = reason;
        total++;
        if (reportedA != null && to != reportedA && to != reportedB) reportedA = reportedB = null;
        if (total < 4) return null;

        var h0 = ring[(total - 4) % RingSize];
        var h1 = ring[(total - 3) % RingSize];
        var h2 = ring[(total - 2) % RingSize];
        var h3 = h;
        if (h0.to == h1.to || h0.to != h2.to || h1.to != h3.to || h3.at - h0.at > LoopSeconds) return null;
        if (reportedA != null && (reportedA == h0.to || reportedA == h1.to) && (reportedB == h0.to || reportedB == h1.to)) return null;
        reportedA = h0.to;
        reportedB = h1.to;

        var sb = new StringBuilder();
        sb.Append("ECHO made four trips in a row between ").Append(h0.to).Append(" and ").Append(h1.to)
            .Append(" in ").Append(((h3.at - h0.at) / 60f).ToString("0.#")).Append(" minutes. ");
        sb.Append("Status: \"").Append(status).Append("\". ");
        sb.Append("Decided by: ").Append(step ?? "the game's own decision").Append(". ");
        sb.Append("Mission: ").Append(mission).Append(". ");
        sb.Append("Activity list: ").Append(activityList).Append(". ");
        if (reason != null) sb.Append("Last stop: ").Append(reason).Append(". ");
        string skipped = EmptyPlaces(now);
        if (skipped != null) sb.Append("Skipping as empty for now: ").Append(skipped).Append(". ");
        sb.Append("If ECHO was mining, salvaging or unloading in between, this is normal.");
        return sb.ToString();
    }

    // The ring, newest last, one line per trip, for the BepInEx log after a report.
    internal static void DumpTo(Action<string> write, float now)
    {
        int n = Math.Min(total, RingSize);
        for (int i = n; i >= 1; i--)
        {
            var h = ring[(total - i) % RingSize];
            write($"  {(now - h.at):0} s ago: {h.from} -> {h.to} | {h.status} | mission: {h.mission} | decided by: {h.step ?? "the game"}{(h.reason != null ? " | " + h.reason : "")}");
        }
    }

    internal static string ActivityList()
    {
        var list = EchoPriority.List;
        if (list.Count == 0) return "none (the game's own preferred activity)";
        var sb = new StringBuilder();
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(EchoActivityInfo.Name(list[i]));
        }
        return sb.ToString();
    }

    private static string MissionText()
    {
        if (!Singleton<IdleManager>.HasInstance) return "none";
        var m = Singleton<IdleManager>.Instance.GetIdleMission();
        if (m == null) return GamePlayer.current.missions.Count == 0 ? "none held" : $"none ECHO can work on ({GamePlayer.current.missions.Count} held)";
        var focus = Singleton<FocusedMissionHandler>.HasInstance ? Singleton<FocusedMissionHandler>.Instance.focusedMission : null;
        return $"'{m.name}' at {m.GetActivePoi(forEcho: true)?.name ?? "no location"}, {(m.idle ? "taken by ECHO" : "accepted by you")}{(m == focus ? ", tracked on the HUD" : "")}";
    }

    // Called after ECHO has set a destination: notes it and writes the report when the pattern shows.
    internal static void OnTravel(MapPointOfInterest destination)
    {
        var player = GamePlayer.current;
        if (destination == null || player == null || !player.autoPlay) return;
        float now = Now;
        string reason = emptyName != null && now - emptyAt < SameTripSeconds && emptyName == MapPointOfInterest.current?.name
            ? $"found nothing it could work on at {emptyName} (the game's \"No valid targets for modules\")" : null;
        var panel = SidePanel.instance;
        string status = panel != null && panel.idleStatusMessage != null ? panel.idleStatusMessage.text : "";
        string line = RecordHop(MapPointOfInterest.current?.name ?? "open space", destination.name, now, status, MissionText(), Step, reason, ActivityList());
        if (line == null) return;
        Plugin.Log.LogWarning("[EchoPriority] " + line);
        DumpTo(s => Plugin.Log.LogInfo("[EchoPriority] " + s), now);
        var log = EventLog.EventLog.Instance;
        if (log != null && log.Active) EventLog.EventLog.Add(EventLog.LogKind.Echo, line, EventLog.EventLog.EchoTint);
    }
}

[HarmonyPatch]
internal static class EchoTracePatches
{
    private static EchoPriority F => EchoPriority.Instance;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.FindActivity))]
    private static void FindActivityPrefix() => EchoTrace.Step = null;

    // The game's search for an activity (and the mod's list in its place) runs between this prefix and the finalizer.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.TryPreferredActivity))]
    private static void SearchStart() => EchoTrace.Searching = F.Active && EchoTrace.WindowSeconds > 0f;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.TryPreferredActivity))]
    private static Exception SearchEnd(Exception __exception)
    {
        EchoTrace.Searching = false;
        return __exception;
    }

    // Every search for a place to work goes through CanWeTravel(place) first; a remembered empty place counts as unreachable.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TravelManager), nameof(TravelManager.CanWeTravel))]
    private static void CanTravelPostfix(MapPointOfInterest target, ref bool __result)
    {
        if (!EchoTrace.Searching || !__result || target == null) return;
        try { if (EchoTrace.IsEmpty(target, EchoTrace.Now)) __result = false; }
        catch (Exception e) { F.Fail(e, nameof(EchoTracePatches)); }
    }

    // The game's mining search also returns to the last mining location without CanWeTravel.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(IdleManager), "IsSuitableForMining")]
    private static void SuitablePostfix(MapPointOfInterest poi, ref bool __result)
    {
        if (!EchoTrace.Searching || !__result) return;
        try { if (EchoTrace.IsEmpty(poi, EchoTrace.Now)) __result = false; }
        catch (Exception e) { F.Fail(e, nameof(EchoTracePatches)); }
    }

    // The game's handler at a mining, salvage or combat location. When none of its modules has a target it sends ECHO
    // to a station; that call, made from here, is the moment to remember the place.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), "IdleActivityInPoi")]
    private static void InLocationStart() => EchoTrace.InLocation = true;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(IdleManager), "IdleActivityInPoi")]
    private static Exception InLocationEnd(Exception __exception)
    {
        EchoTrace.InLocation = false;
        return __exception;
    }

    // First in line: the missions feature's own prefix on this method may skip the rest.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(IdleManager), "IdleTravelToSpaceStation")]
    private static void NothingHerePrefix()
    {
        if (!EchoTrace.InLocation || !F.Active) return;
        try { EchoTrace.MarkEmpty(MapPointOfInterest.current, EchoTrace.Now); }
        catch (Exception e) { F.Fail(e, nameof(EchoTracePatches)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(IdleManager), "TravelToPoi")]
    private static void TravelToPoiPostfix(MapPointOfInterest poi, bool __result)
    {
        if (!F.Active || !__result) return;
        try { EchoTrace.OnTravel(poi); }
        catch (Exception e) { F.Fail(e, nameof(EchoTracePatches)); }
    }

    // The nearest-station branch doesn't go through TravelToPoi.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(IdleManager), "IdleTravelToSpaceStation")]
    private static void TravelToStationPostfix()
    {
        if (!F.Active || !Singleton<TravelManager>.HasInstance) return;
        try
        {
            var travel = Singleton<TravelManager>.Instance;
            if (travel.TravelActive()) EchoTrace.OnTravel(travel.targetPoi);
        }
        catch (Exception e) { F.Fail(e, nameof(EchoTracePatches)); }
    }

    // A new autopilot session starts without what the last one learned.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GamePlayer), nameof(GamePlayer.StartAutopilotSession))]
    private static void SessionPostfix()
    {
        if (F.Active) EchoTrace.Clear();
    }
}
