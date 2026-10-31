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
    public override string Title => "ECHO missions";
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

    public MissionKind(string id, string label, string help)
    {
        Id = id;
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
        new("BountyHunt", "Combat: Eliminate Pirate Presence / Eliminate (faction) forces",
            "Fly to a location and destroy the enemy ships there. Short."),
        new("ClearSalvageField", "Combat: Reclaim Salvage Rights",
            "Clear the guards and turrets from a wreck field. Short."),
        new("ClearAsteroidField", "Combat: Reclaim Asteroid Belt",
            "Chase hostile miners from an asteroid field. The miners arrive over time, so ECHO sometimes leaves and comes back."),
        new("ClearDungeon", "Combat: Clear Pirate Base",
            "An installation exploration: your crew raids a pirate base (3-5 minutes), finds credits and items aboard, and loses some crew, which ECHO hires again at the station. In two 30-minute test runs these paid far more credits than the other combat missions (about 0.3M and 1.3M credits found aboard against 5-50K). The Autopilot tab's operations setting can also rule them out."),
        new("CaptureShip", "Combat: Capture Enemy Vessel",
            "Disable a ship and board it with your crew. The Autopilot tab's operations setting can also rule these out."),
        new("SalvageWreck", "Salvage: Recover (item)",
            "Salvage one named item from a wreck at a new salvage location."),
        new("SalvageSamples", "Salvage: Salvage (material)",
            "Salvage an amount of a material at a salvage location, some with a radiation hazard."),
        new("MineOre", "Mining: Mine (ore)",
            "Mine an amount of an ore."),
        new("OreSamples", "Mining: Collect (ore)",
            "Mine an amount of an ore at a new asteroid field, some with a hazard."),
        new("HelpMiner", "Mining: Support (ore) Mining",
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

    // Docked at a board: with only "Clear Pirate Base" allowed every mission ECHO is offered is one, and with nothing
    // allowed it is offered none. The generated missions are taken off the board again.
    public static void Check(SelfCheckContext ctx)
    {
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
        var f = EchoMissions.Instance;
        var saved = new Dictionary<string, bool>();
        foreach (var kv in f.Kinds) saved[kv.Key] = kv.Value.Value;
        var offered = new List<Mission>();
        try
        {
            foreach (var kv in f.Kinds) kv.Value.Value = kv.Key == "ClearDungeon";
            string expected = Source.Util.Translation.Translate("@ClearDungeonMissionName");
            var names = new List<string>();
            int wrong = 0;
            for (int i = 0; i < 6; i++)
            {
                var m = interior.GetMissionAvailableForAutopilot();
                if (m == null) { names.Add("none"); continue; }
                offered.Add(m);
                names.Add(m.name);
                if (m.name != expected) wrong++;
            }
            string detail = $"only '{expected}': offered [{string.Join(", ", names)}]";
            if (offered.Count >= 3 && wrong == 0) ctx.Pass("kinds", detail);
            else ctx.Fail("kinds", detail);

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
