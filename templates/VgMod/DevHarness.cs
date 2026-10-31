using System;
using System.Collections;
using Behaviour;
using Behaviour.Bootstrap;
using Behaviour.UI;
using Behaviour.UI.HUD;
using Behaviour.Util;
using BepInEx.Configuration;
using HarmonyLib;
using Source.Util;
using UnityEngine;

namespace VgMod;

// Test run without clicking: load a save, run checks, write the results to the log, quit.
// Turn it on in BepInEx/config/<plugin GUID>.cfg under [Dev].
public static class DevHarness
{
    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<string> AutoLoadSave;
    public static ConfigEntry<bool> SelfCheck;
    public static ConfigEntry<int> QuitAfterSeconds;

    private static bool autoLoadDone;
    private static bool harnessStarted;

    public static void BindConfig(ConfigFile cfg)
    {
        Enabled = cfg.Bind("Dev", "Enabled", false, "Turn the dev harness on. Also stops the game from autosaving.");
        AutoLoadSave = cfg.Bind("Dev", "AutoLoadSave", "", "Save name (without extension) to load automatically at the main menu");
        SelfCheck = cfg.Bind("Dev", "SelfCheck", false, "Run the self-checks after the save is loaded");
        QuitAfterSeconds = cfg.Bind("Dev", "QuitAfterSeconds", 0, "Quit the game this many seconds after the checks finish (0 = never)");
    }

    internal static void OnMainMenuShown()
    {
        if (!Enabled.Value || autoLoadDone || string.IsNullOrEmpty(AutoLoadSave.Value)) return;
        autoLoadDone = true;
        var file = SaveGame.GetSaveGame(AutoLoadSave.Value);
        if (file == null)
        {
            Plugin.Log.LogWarning($"[Dev] save '{AutoLoadSave.Value}' not found");
            return;
        }
        Plugin.Log.LogInfo($"[Dev] loading save '{AutoLoadSave.Value}'");
        PersistentSingleton<GameManager>.Instance.LoadGame(file);
    }

    internal static void OnSaveLoaded()
    {
        if (!Enabled.Value || harnessStarted) return;
        harnessStarted = true;
        Plugin.Instance.StartCoroutine(Run());
    }

    private static IEnumerator Run()
    {
        float deadline = Time.realtimeSinceStartup + 60f;
        bool ready = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (GameplayManager.Instance != null && GameplayManager.Instance.spaceShip != null && HudManager.Instance != null)
            {
                ready = true;
                break;
            }
            yield return null;
        }

        var ctx = new Checks();
        if (!ready) ctx.Fail("ready", "game not ready after 60 s");
        else
        {
            yield return new WaitForSeconds(3f);
            if (SelfCheck.Value)
            {
                IEnumerator checks = RunChecks(ctx);
                while (true)
                {
                    bool more;
                    try { more = checks.MoveNext(); }
                    catch (Exception e) { ctx.Fail("exception", e.ToString()); break; }
                    if (!more) break;
                    yield return checks.Current;
                }
            }
        }

        Plugin.Log.LogInfo($"Self-check complete: {ctx.PassCount} pass, {ctx.FailCount} fail, {ctx.SkipCount} skip");
        if (QuitAfterSeconds.Value > 0)
        {
            yield return new WaitForSeconds(QuitAfterSeconds.Value);
            Application.Quit();
        }
    }

    // Add your own checks here. Yield to wait for the game; call Pass, Fail or Skip once per check.
    private static IEnumerator RunChecks(Checks ctx)
    {
        if (SamplePatch.Ran) ctx.Pass("sample-patch", "MainMenuUI.Start postfix ran");
        else ctx.Skip("sample-patch", "main menu was not shown this run, so the patch had no chance to run");
        yield break;
    }
}

public sealed class Checks
{
    public int PassCount, FailCount, SkipCount;

    public void Pass(string name, string detail) { PassCount++; Write(name, "PASS", detail); }
    public void Fail(string name, string detail) { FailCount++; Write(name, "FAIL", detail); }
    public void Skip(string name, string reason) { SkipCount++; Write(name, "SKIP", reason); }

    private static void Write(string name, string result, string detail) =>
        Plugin.Log.LogInfo($"CHECK {name} {result} - {detail}");
}

[HarmonyPatch]
internal static class DevPatches
{
    private static bool autosaveLogged;

    private static void Note()
    {
        if (autosaveLogged) return;
        autosaveLogged = true;
        Plugin.Log.LogInfo("[Dev] autosave suppressed");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MainMenuUI), "Start")]
    private static void MainMenuPostfix()
    {
        try { DevHarness.OnMainMenuShown(); }
        catch (Exception e) { Plugin.Log.LogError($"[Dev] auto-load failed: {e}"); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SceneLoader), nameof(SceneLoader.LoadScenesOnStartGame))]
    private static void SaveLoadedPostfix()
    {
        try { DevHarness.OnSaveLoaded(); }
        catch (Exception e) { Plugin.Log.LogError($"[Dev] harness start failed: {e}"); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveGame), nameof(SaveGame.StoreAutosaveState))]
    private static bool StoreAutosaveStatePrefix()
    {
        if (!DevHarness.Enabled.Value) return true;
        try { Note(); return false; }
        catch (Exception e) { Plugin.Log.LogError($"[Dev] {e}"); return true; }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveGame), nameof(SaveGame.StoreAutosaveInBackground))]
    private static bool StoreAutosaveInBackgroundPrefix(ref bool __result)
    {
        if (!DevHarness.Enabled.Value) return true;
        try { Note(); __result = false; return false; }
        catch (Exception e) { Plugin.Log.LogError($"[Dev] {e}"); return true; }
    }
}
