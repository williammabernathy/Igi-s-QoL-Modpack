using System;
using System.Collections;
using Behaviour.UI.HUD;
using Behaviour.UI.Spacestation;
using BepInEx.Configuration;
using IgiQoL.Core;
using IgiQoL.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IgiQoL.Features.Window;

public sealed class WindowFeature : Feature
{
    public static WindowFeature Instance;

    public WindowFeature() { Instance = this; }

    public override string Id => "Window";
    public override string Title => "Mod window";
    public override string Description => "This window. Open and close it with the hotkey below, the QoL button above the Dock button in space, or the QoL tab above the station tabs while docked.";
    public override Type[] PatchClasses => new[] { typeof(HudButtonPatches) };

    public ConfigEntry<Key> Hotkey;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        Hotkey = cfg.Bind(Id, "Hotkey", Key.F6, SettingInfo.Describe("Hotkey",
            "Key that opens and closes this window. Type a key name such as F6, F7 or Backquote. Ignored while you type in a text field."));
    }

    public override void OnActivated()
    {
        Plugin.Instance.gameObject.AddComponent<WindowHotkey>();
        GameEvents.SaveLoaded += OnSaveLoaded;
        ModWindow.Register("settings", "Settings", 90, BuildSettings);
    }

    private void OnSaveLoaded()
    {
        if (!Active) return;
        Plugin.Instance.StartCoroutine(HudButton.CreateWhenReady());
    }

    private static void BuildSettings(RectTransform content)
    {
        Kit.Muted(content, "Every change is saved at once (to BepInEx/config/igi.qol.cfg). Hover over a setting's name for details. Features with their own tab (ECHO, Operations, Item rules) keep their settings there; this tab only has their on/off switch.");
        foreach (var feature in FeatureRegistry.All)
        {
            if (!feature.HasEnabledEntry || feature.Id == "Dev") continue;
            SettingRows.AddSection(content, feature);
            if (feature.SettingsTab == null)
            {
                SettingRows.AddEntries(content, feature.Id);
                continue;
            }
            SettingRows.AddEntries(content, feature.Id, SettingRows.Rows.EnabledOnly);
            Kit.Muted(content, "Its settings are on the " + feature.SettingsTab + " tab.");
        }
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        ModWindow.Open("settings");
        ctx.Cleanup(ModWindow.Close);
        // Settings shows every entry outside [Dev], except those of features whose settings live on another tab (only their Enabled).
        int entries = 0;
        foreach (var kv in Plugin.Instance.Config)
        {
            if (kv.Key.Section == "Dev") continue;
            var owner = Array.Find(FeatureRegistry.All, f => f.Id == kv.Key.Section);
            if (kv.Key.Key == "Enabled" || owner == null || owner.SettingsTab == null) entries++;
        }
        int rows = 0;
        if (ModWindow.IsOpen)
        {
            var content = ModWindow.Content;
            for (int i = 0; i < content.childCount; i++)
                if (content.GetChild(i).name == "Row") rows++;
        }
        string detail = $"rows={rows} entries={entries} tab={ModWindow.CurrentTab}";
        if (ModWindow.IsOpen && rows == entries) ctx.Pass("window-open", detail);
        else ctx.Fail("window-open", detail + $" open={ModWindow.IsOpen}");
        yield return ctx.Screenshot("window-settings");

        if (HudButton.Instance == null) ctx.Skip("hud-button", "no dock button in this scene");
        else
        {
            // Hidden with the HUD (docked, dialogues), so it never covers station screens.
            bool shown = HudButton.Instance.activeSelf, hud = HudManager.Instance.showHud;
            string hudDetail = $"parent={HudButton.Instance.transform.parent.name} active={shown} showHud={hud}";
            if (shown == hud) ctx.Pass("hud-button", hudDetail);
            else ctx.Fail("hud-button", hudDetail);
        }

        if (SpaceStationInterior.instance == null) ctx.Skip("station-button", "not docked");
        else if (HudButton.StationInstance == null) ctx.Fail("station-button", "docked but no QoL station tab");
        else
        {
            var placed = HudButton.StationInstance.GetComponent<PersonalFleetButton>();
            string stationDetail = $"above {(placed.target != null ? placed.target.name : "-")} rect={placed.rectTransform.rect.width:0.#}x{placed.rectTransform.rect.height:0.#}";
            if (placed.target != null) ctx.Pass("station-button", stationDetail);
            else ctx.Fail("station-button", stationDetail);
        }

        // Undock and check the HUD button comes back with the HUD (the station tab dies with the station screen).
        if (SpaceStationInterior.instance == null || HudButton.StationInstance == null) yield break;
        ModWindow.Close();
        yield return ctx.Screenshot("docked");
        SpaceStationInterior.instance.ExitSpacestation();
        float until = Time.realtimeSinceStartup + 20f;
        while ((SpaceStationInterior.instance != null || !HudManager.Instance.showHud) && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSeconds(2f);
        bool back = HudButton.Instance != null && HudButton.Instance.activeSelf;
        string undockDetail = $"docked={SpaceStationInterior.instance != null} showHud={HudManager.Instance.showHud} hudButton={back} stationTab={HudButton.StationInstance != null}";
        if (SpaceStationInterior.instance == null && back && HudButton.StationInstance == null) ctx.Pass("undock", undockDetail);
        else ctx.Fail("undock", undockDetail);
        yield return ctx.Screenshot("undocked");
    }
}
