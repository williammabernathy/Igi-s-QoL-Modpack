using System;
using BepInEx;
using BepInEx.Logging;
using Behaviour.UI;
using HarmonyLib;
using UnityEngine;

namespace VgMod;

[BepInPlugin(PluginGuid, "VgMod", "0.1.0")]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "AUTHOR_TOKEN.MODID_TOKEN";
    // Bump this after you have checked the mod against a new game version.
    public const string TestedGameVersion = "0.8.3.4";

    public static Plugin Instance;
    public static ManualLogSource Log;

    // One class per patch group. Each is patched on its own, so a patch broken by a game update is skipped, not fatal.
    private static readonly Type[] PatchClasses = { typeof(SamplePatch), typeof(DevPatches) };

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        string version = Application.version;
        Log.LogInfo($"VgMod 0.1.0 on game {version} (tested on {TestedGameVersion})");
        if (version != TestedGameVersion)
            Log.LogWarning($"Game version {version} differs from the tested version {TestedGameVersion}");

        DevHarness.BindConfig(Config);

        var harmony = new Harmony(PluginGuid);
        int failed = 0;
        foreach (var type in PatchClasses)
        {
            try { harmony.CreateClassProcessor(type).Patch(); }
            catch (Exception e)
            {
                failed++;
                Log.LogError($"Patch class {type.Name} failed and is skipped: {e}");
            }
        }
        Log.LogInfo($"VgMod startup: {PatchClasses.Length - failed} patch classes active, {failed} failed");
    }
}

// Sample patch: runs after the main menu is built. Replace it with your own.
[HarmonyPatch(typeof(MainMenuUI), "Start")]
internal static class SamplePatch
{
    public static bool Ran;

    private static void Postfix()
    {
        try
        {
            Ran = true;
            Plugin.Log.LogInfo("VgMod sample patch ran at the main menu");
            AlertPopup.ShowMessage("VgMod is loaded. Remove SamplePatch in Plugin.cs when you write your own patches.");
        }
        catch (Exception e) { Plugin.Log.LogError($"SamplePatch failed: {e}"); }
    }
}
