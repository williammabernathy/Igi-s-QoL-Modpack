using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;

namespace IgiQoL.Core;

public static class FeatureRegistry
{
    public static readonly Feature[] All =
    {
        new CoreFeature(),
        new DevFeature(),
        new Features.TractorCaps.TractorCaps(),
        new Features.JumpsRemaining.JumpsRemaining(),
        new Features.FleetVitals.FleetVitalsFeature(),
        new Features.MissileRetarget.MissileRetarget(),
        new Features.DronesOnArrival.DronesOnArrival(),
        new Features.EchoGates.EchoGates(),
        new Features.EchoMissions.EchoMissions(),
        new Features.EchoOperations.EchoOperations(),
        new Features.Window.WindowFeature(),
        new Features.FleetStrip.FleetStrip(),
        new Features.EngagementDistance.EngagementDistance(),
        new Features.EchoPriority.EchoPriority(),
        new Features.Operations.Operations(),
        new Features.Notes.Notes(),
        new Features.ItemRulesTab.ItemRulesTab(),
        new Features.EchoCargo.EchoCargo(),
        new Features.Contacts.Contacts(),
        new Features.TextScale.TextScale(),
        new Features.TargetPriority.TargetPriority(),
        new Features.DroneModes.DroneModes(),
        new Features.Separation.Separation(),
        new Features.CraftRollup.CraftRollup(),
        new Features.CombatLog.CombatLog(),
        new Features.EventLog.EventLog(),
        new Features.Guide.Guide(),
        new Features.EchoKit.EchoKit(),
        new Features.OrePreference.OrePreference(),
        new Features.FleetHolds.FleetHolds(),
        new Features.Loadouts.Loadouts(),
        new Features.StationServices.StationServices(),
        new Features.RefineryFilter.RefineryFilter(),
        new Features.UsedIn.UsedIn(),
        new Features.MissionDistance.MissionDistance(),
        new Features.EchoRetreat.EchoRetreat(),
        new Features.Mods.Mods(),
    };

    public static readonly List<FeatureFailure> Failures = new();

    public static void Start(ConfigFile cfg)
    {
        foreach (var feature in All) feature.BindConfig(cfg);

        int active = 0, disabled = 0, failed = 0;
        bool coreFailed = false;
        foreach (var feature in All)
        {
            if (feature.HasEnabledEntry && !feature.Enabled.Value)
            {
                Plugin.Log.LogInfo($"[{feature.Id}] disabled in config");
                disabled++;
                continue;
            }
            if (coreFailed)
            {
                Plugin.Log.LogInfo($"[{feature.Id}] skipped: core failed");
                failed++;
                continue;
            }
            try
            {
                feature.Harmony = new Harmony("igi.qol." + feature.Id);
                foreach (var type in feature.PatchClasses)
                    feature.Harmony.CreateClassProcessor(type).Patch();
                feature.OnActivated();
                feature.Active = true;
                int count = 0;
                foreach (var _ in feature.Harmony.GetPatchedMethods()) count++;
                Plugin.Log.LogInfo($"[{feature.Id}] active ({count} patches)");
                active++;
            }
            catch (Exception e)
            {
                feature.Fail(e, "startup");
                failed++;
                if (feature is CoreFeature) coreFailed = true;
            }
        }
        Plugin.Log.LogInfo($"IgiQoL startup: {active} active, {disabled} disabled, {failed} failed");
    }
}
