using System;
using System.Collections;
using System.Collections.Generic;
using IgiQoL.UI;
using TMPro;
using Behaviour.UI.Spacestation;
using Behaviour.Weapons;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using Source.Galaxy.POI;
using Source.Galaxy.POI.Station;
using Source.MissionSystem;
using Source.Util;

namespace IgiQoL.Features.EchoMissions;

// Which missions ECHO takes from a board and how many at a time. ECHO's missions are generated on the spot for its
// preferred activity (MissionBoard.GenerateIdleMission), one per trip, and the game only lets you rule out the two
// operation kinds (the Autopilot tab's operations setting).
public sealed class EchoMissions : Feature
{
    public static EchoMissions Instance;

    public EchoMissions() { Instance = this; }

    public override string Id => "EchoMissions";
    public override string Title => "ECHO Missions";
    public override string Description => "How many missions ECHO takes from a mission board at once, and which kinds. ECHO only gets missions for its preferred activity (combat, mining or salvage), so only that activity's kinds matter. The starting values match the unmodded game.";
    public override string SettingsTab => "ECHO";
    public override Type[] PatchClasses => new[] { typeof(MissionBatchPatches), typeof(MissionKindPatches) };

    public ConfigEntry<int> MissionsAtOnce;
    public readonly Dictionary<string, ConfigEntry<bool>> Kinds = new();

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        MissionsAtOnce = cfg.Bind(Id, "MissionsAtOnce", 1, SettingInfo.Describe("Missions ECHO takes at once",
            "ECHO takes this many missions from the board in one visit, flies from one mission location to the nearest next one in the system, and turns them all in on one trip home. It still goes home first when the hold is full, ammo or crew run short, or the flagship's hull or armour is below half. Game default: 1 (one mission, then back to the station).",
            new AcceptableValueRange<int>(1, 5)));
        foreach (var k in MissionKinds.All)
            Kinds[k.Id] = cfg.Bind(Id, k.Id, true, SettingInfo.Describe(k.Label, k.Help));
    }

    public override void OnActivated() => EchoPriority.EchoPriority.EnsureTab();

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        MissionKinds.Check(ctx);
        MissionBatch.Check(ctx);
        MissionCheck.Run(ctx);
        yield return Tab(ctx);
    }

    // The ECHO tab shows the section with the missions-at-once row and one row per kind; the screenshot scrolls the
    // section heading to the top of the view.
    private IEnumerator Tab(SelfCheckContext ctx)
    {
        ModWindow.Open("echo");
        yield return null;
        TMP_Text heading = null;
        int rows = 0;
        bool atOnce = false;
        if (ModWindow.IsOpen)
            foreach (var t in ModWindow.Content.GetComponentsInChildren<TMP_Text>())
            {
                if (t.text == Title) heading = t;
                atOnce |= t.text == "Missions ECHO takes at once";
                foreach (var k in MissionKinds.All)
                    if (t.text == k.Label) rows++;
            }
        string detail = $"heading={heading != null} at-once row={atOnce} kind rows={rows}/{MissionKinds.All.Length}";
        if (heading != null && atOnce && rows == MissionKinds.All.Length) ctx.Pass("tab", detail);
        else ctx.Fail("tab", detail);
        var scroll = ModWindow.IsOpen ? ModWindow.Content.GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
        if (scroll != null && heading != null)
        {
            UnityEngine.Canvas.ForceUpdateCanvases();
            var content = scroll.content;
            float below = -content.InverseTransformPoint(heading.rectTransform.position).y - 8f;
            float range = content.rect.height - scroll.viewport.rect.height;
            if (range > 0f) scroll.verticalNormalizedPosition = UnityEngine.Mathf.Clamp01(1f - below / range);
        }
        yield return ctx.Screenshot("window-echo-missions");
        ModWindow.Close();
    }
}

public sealed class MissionKind
{
    public readonly string Id, Label, Help;
    // The activity the game offers this kind under (MissionGenerator.GetMissionType).
    public readonly GameplayType Type;

    public MissionKind(string id, GameplayType type, string label, string help)
    {
        Id = id;
        Type = type;
        Label = label;
        Help = help;
    }
}

public static class MissionKinds
{
    // The game's mission generators that ECHO can be given (canBeIdled), except Courier, which the Autopilot tab's
    // courier switch already covers. Id = MissionGenerator.identifier (its class name).
    public static readonly MissionKind[] All =
    {
        new("BountyHunt", GameplayType.Combat, "Combat: Eliminate Pirate Presence or Eliminate (faction) forces",
            "Fly to a location and destroy the enemy ships there. Short."),
        new("ClearSalvageField", GameplayType.Combat, "Combat: Reclaim Salvage Rights",
            "Clear the guards and turrets from a wreck field. Short."),
        new("ClearAsteroidField", GameplayType.Combat, "Combat: Reclaim Asteroid Belt",
            "Chase hostile miners from an asteroid field. The miners arrive over time, so ECHO sometimes leaves and comes back."),
        new("ClearDungeon", GameplayType.Combat, "Combat: Clear Pirate Base",
            "An installation exploration: your crew raids a pirate base (3-5 minutes), finds credits and items aboard, and loses some crew, which ECHO hires again at the station. In two 30-minute test runs these paid far more credits than the other combat missions (about 0.3M and 1.3M credits found aboard against 5-50K). The Autopilot tab's operations setting can also rule them out."),
        new("CaptureShip", GameplayType.Combat, "Combat: Capture Enemy Vessel",
            "Disable a ship and board it with your crew. The Autopilot tab's operations setting can also rule these out."),
        new("SalvageWreck", GameplayType.Salvage, "Salvage: Recover (item)",
            "Salvage one named item from a wreck at a new salvage location."),
        new("SalvageSamples", GameplayType.Salvage, "Salvage: Salvage (material)",
            "Salvage an amount of a material at a salvage location, some with a radiation hazard."),
        new("MineOre", GameplayType.Mining, "Mining: Mine (ore)",
            "Mine an amount of an ore."),
        new("OreSamples", GameplayType.Mining, "Mining: Collect (ore)",
            "Mine an amount of an ore at a new asteroid field, some with a hazard."),
        new("HelpMiner", GameplayType.Mining, "Mining: Support (ore) Mining",
            "Mine an ore next to a faction's miners; on harder missions enemies arrive and the miner has to be protected."),
    };

    public static bool Allowed(string generatorId)
    {
        var f = EchoMissions.Instance;
        return f == null || !f.Active || !f.Kinds.TryGetValue(generatorId, out var e) || e.Value;
    }

    public static bool NoneAllowed()
    {
        var f = EchoMissions.Instance;
        if (f == null || !f.Active) return false;
        foreach (var e in f.Kinds.Values)
            if (e.Value) return false;
        return true;
    }

    // Whether every kind of one activity is off. ECHO is only offered missions of the flagship's preferred activity, so
    // that is the question that decides whether it has any mission to wait for. Generic (no turrets to tell) offers
    // nothing at all, so the all-kinds answer is the best available. An activity with no kinds here (Cargo: Courier
    // missions stay on the Autopilot tab's own switch) is never blocked.
    public static bool NoneAllowedFor(GameplayType type)
    {
        if (type == GameplayType.Generic) return NoneAllowed();
        var f = EchoMissions.Instance;
        if (f == null || !f.Active) return false;
        bool any = false;
        foreach (var k in All)
        {
            if (k.Type != type) continue;
            any = true;
            if (f.Kinds[k.Id].Value) return false;
        }
        return any;
    }

    // Combat kinds the check isolates one at a time.
    private static string[] CombatIds()
    {
        var ids = new List<string>();
        foreach (var k in All)
            if (k.Type == GameplayType.Combat) ids.Add(k.Id);
        return ids.ToArray();
    }

    // Docked at a board: with one combat kind allowed every mission ECHO is offered is of that kind (two kinds offer
    // different mission names), and with nothing allowed it is offered none. The generated missions are taken off the
    // board again. Which kinds a station can offer depends on its level and the Autopilot tab's operations setting, so
    // every combat kind is tried and the check needs two that the station offers.
    public static void Check(SelfCheckContext ctx)
    {
        var f = EchoMissions.Instance;
        var saved = new Dictionary<string, bool>();
        foreach (var kv in f.Kinds) saved[kv.Key] = kv.Value.Value;
        try
        {
            // The game offers ECHO only the preferred activity's kinds, so switching off all of those is what leaves
            // ECHO with nothing to take, whatever the other activities allow.
            foreach (var kv in f.Kinds) kv.Value.Value = true;
            foreach (var k in All)
                if (k.Type == GameplayType.Combat) f.Kinds[k.Id].Value = false;
            string perType = $"combat kinds off, others on: none for Combat {NoneAllowedFor(GameplayType.Combat)}, none for Mining {NoneAllowedFor(GameplayType.Mining)}, none at all {NoneAllowed()}";
            if (NoneAllowedFor(GameplayType.Combat) && !NoneAllowedFor(GameplayType.Mining) && !NoneAllowed()) ctx.Pass("kinds-per-activity", perType);
            else ctx.Fail("kinds-per-activity", perType);
        }
        finally
        {
            foreach (var kv in saved) f.Kinds[kv.Key].Value = kv.Value;
        }

        var interior = SpaceStationInterior.instance;
        var board = interior != null && interior.HasMissionBoard(missionCheck: true) ? interior.spacestation.missionBoard : null;
        if (board == null)
        {
            ctx.Skip("kinds", "not docked at a station with a mission board");
            return;
        }
        var ship = Fleet.Flagship;
        if (ship == null) { ctx.Skip("kinds", "not in space"); return; }
        var type = ship.GetPreferredGameplayType();
        if (type != GameplayType.Combat)
        {
            ctx.Skip("kinds", $"the flagship's preferred activity is {type}; the check uses combat missions");
            return;
        }
        var offered = new List<Mission>();
        try
        {
            var seen = new List<string>();      // kind: names offered, for the log line
            var offering = new List<HashSet<string>>();
            foreach (string id in CombatIds())
            {
                foreach (var kv in f.Kinds) kv.Value.Value = kv.Key == id;
                var names = new HashSet<string>();
                for (int i = 0; i < 6; i++)
                {
                    var m = interior.GetMissionAvailableForAutopilot();
                    if (m == null) continue;
                    offered.Add(m);
                    names.Add(m.name);
                }
                seen.Add($"{id}: [{string.Join(", ", names)}]");
                if (names.Count > 0) offering.Add(names);
            }
            string detail = "one kind at a time: " + string.Join("; ", seen);
            if (offering.Count < 2)
                ctx.Skip("kinds", $"this station offers ECHO fewer than two combat kinds ({detail})");
            else
            {
                // A different kind must come out as different missions: no name may be offered under two kinds.
                var all = new HashSet<string>();
                bool overlap = false;
                foreach (var set in offering)
                    foreach (var n in set)
                        overlap |= !all.Add(n);
                if (!overlap) ctx.Pass("kinds", detail);
                else ctx.Fail("kinds", "a mission name came from two kinds: " + detail);
            }

            foreach (var kv in f.Kinds) kv.Value.Value = false;
            var none = interior.GetMissionAvailableForAutopilot();
            if (none != null) offered.Add(none);
            string noneDetail = $"all off: offered {(none == null ? "nothing" : none.name)}, NoneAllowed {NoneAllowed()}";
            if (none == null && NoneAllowed()) ctx.Pass("kinds-none", noneDetail);
            else ctx.Fail("kinds-none", noneDetail);
        }
        finally
        {
            foreach (var kv in saved) f.Kinds[kv.Key].Value = kv.Value;
            foreach (var m in offered) board.availableMissions.Remove(m);
        }
    }
}

[HarmonyPatch]
internal static class MissionKindPatches
{
    private static bool generatingIdle;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MissionBoard), nameof(MissionBoard.GenerateIdleMission))]
    private static void GenerateIdleMissionPrefix() => generatingIdle = true;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(MissionBoard), nameof(MissionBoard.GenerateIdleMission))]
    private static void GenerateIdleMissionFinalizer() => generatingIdle = false;

    // A mission kind you switched off comes out as no mission, before the generator builds its location;
    // GenerateIdleMission then draws again (10 draws per decision, as for missions too risky for ECHO).
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MissionGenerator), nameof(MissionGenerator.GenerateMission),
        new[] { typeof(MissionGenerator), typeof(MissionDifficulty), typeof(SpaceStation), typeof(SeededRandom), typeof(TargetLayer?) })]
    private static bool GenerateMissionPrefix(MissionGenerator gen, ref Mission __result)
    {
        if (!generatingIdle) return true;
        var f = EchoMissions.Instance;
        if (!f.Active) return true;
        try
        {
            if (MissionKinds.Allowed(gen.identifier)) return true;
            __result = null;
            return false;
        }
        catch (Exception e) { f.Fail(e, nameof(MissionKindPatches)); return true; }
    }
}
