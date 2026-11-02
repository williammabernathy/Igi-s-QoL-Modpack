using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using IgiQoL.Core;
using IgiQoL.UI;
using Source.Util;
using TMPro;
using UnityEngine;

namespace IgiQoL.Features.Mods;

// The Mods tab: every other BepInEx plugin with the settings in its config file, the game methods more than one
// mod patches, and the plugins BepInEx refused to load.
public sealed class Mods : Feature
{
    public static Mods Instance;

    public Mods() { Instance = this; }

    public override string Id => "Mods";
    public override string Title => "Other mods";
    public override string Description => "The Mods tab: the settings of every other BepInEx mod you have installed, the game methods that more than one mod changes, and the mods BepInEx refused to load. The same overlap list goes to BepInEx/LogOutput.log at the main menu.";
    public override Type[] PatchClasses => Type.EmptyTypes;

    private bool logged;

    public override void OnActivated()
    {
        ModWindow.AddTab(this, "mods", "Mods", 88, Build);
        GameEvents.MainMenuShown += LogOnce;
    }

    // Once per session, after every plugin has loaded and applied its startup patches.
    private void LogOnce()
    {
        if (logged || !Active) return;
        logged = true;
        try
        {
            var shared = PatchOverlap.Find();
            Plugin.Log.LogInfo($"[Mods] {Others().Count} other plugin(s), {shared.Count} game method(s) patched by more than one mod, {Chainloader.DependencyErrors.Count} BepInEx load error(s)");
            foreach (var s in shared)
                Plugin.Log.LogInfo($"[Mods] {(s.Risky ? "check" : "shared")} {s.Method}: {string.Join("; ", s.Mods)}");
        }
        catch (Exception e) { Fail(e, nameof(LogOnce)); }
    }

    private static List<BepInEx.PluginInfo> Others()
    {
        var list = new List<BepInEx.PluginInfo>();
        foreach (var info in Chainloader.PluginInfos.Values)
            if (info.Instance != Plugin.Instance) list.Add(info);
        list.Sort((a, b) => string.Compare(a.Metadata.Name, b.Metadata.Name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private static void Build(RectTransform content)
    {
        Kit.Heading(content, "Mod compatibility");
        string game = Application.version;
        Kit.Muted(content, game == Plugin.TestedGameVersion
            ? $"Igi's QoL {PluginInfo.Version} was tested on this game version ({game})."
            : $"Igi's QoL {PluginInfo.Version} was tested on game {Plugin.TestedGameVersion}; you're running {game}. A feature that breaks switches itself off and is listed at the main menu.");

        string warn = "<color=#" + ColorUtility.ToHtmlStringRGB(ColorHelper.orange75) + ">";
        foreach (var error in Chainloader.DependencyErrors)
            Kit.Text(content, warn + "Not loaded:</color> " + error);

        var shared = PatchOverlap.Find();
        if (shared.Count == 0) Kit.Muted(content, "No game method is changed by more than one mod.");
        else
        {
            Kit.Muted(content, "These game methods are changed by more than one mod. That usually works: each mod's code runs in turn. " +
                "\"Check\" marks the ones where a mod can stop the others' changes from running, through a prefix that can skip the game's code or a transpiler that rewrites it. " +
                "If two of these mods misbehave together, try the game with one of them removed. Only Harmony patches are listed.");
            foreach (var s in shared)
                Kit.Text(content, (s.Risky ? warn + "Check</color> " : "") + "<b>" + s.Method + "</b>: " + string.Join("; ", s.Mods));
        }

        Kit.Gap(content, 10f);
        Kit.Heading(content, "Other mods (not part of Igi's QoL)");
        var others = Others();
        if (others.Count == 0)
        {
            Kit.Muted(content, "No other BepInEx mods are installed.");
            return;
        }
        Kit.Muted(content, "Mods you installed separately, each with the settings in its own BepInEx config file. Changes are written to that file at once. " +
            "Some mods only read their settings when the game starts, so restart if a change doesn't take effect. Settings a mod keeps elsewhere aren't shown.");
        foreach (var info in others) AddPlugin(content, info);
    }

    private static void AddPlugin(RectTransform content, BepInEx.PluginInfo info)
    {
        var about = new StringBuilder(info.Metadata.GUID).Append(", ").Append(Path.GetFileName(info.Location));
        bool first = true;
        foreach (var dep in info.Dependencies)
        {
            about.Append(first ? ". Needs " : ", ").Append(dep.DependencyGUID);
            first = false;
        }
        SettingRows.AddSection(content, info.Metadata.Name + " " + info.Metadata.Version, about.Append('.').ToString());
        if (AddSettings(content, info.Instance.Config) == 0) Kit.Muted(content, "No settings in its config file.");
    }

    private sealed class Row
    {
        public ConfigEntryBase Entry;
        public string Section, Label, Help;
        public int Order;
        public bool ReadOnly;
    }

    // Honours ConfigurationManager's per-entry tag (Browsable, Category, DispName, Description, Order, ReadOnly),
    // the display hints many BepInEx mods already ship, and its order: by section, then Order (highest first), then name.
    private static int AddSettings(RectTransform content, ConfigFile config)
    {
        var rows = new List<Row>();
        foreach (var kv in config)
        {
            object tag = ManagerTag(kv.Value);
            if (Field(tag, "Browsable") is false) continue;
            rows.Add(new Row
            {
                Entry = kv.Value,
                Section = Field(tag, "Category") as string ?? kv.Key.Section,
                Label = Field(tag, "DispName") as string ?? SettingRows.Pretty(kv.Key.Key),
                Help = Field(tag, "Description") as string ?? kv.Value.Description?.Description ?? "",
                Order = Field(tag, "Order") is int order ? order : 0,
                ReadOnly = Field(tag, "ReadOnly") is true,
            });
        }
        rows.Sort((a, b) =>
        {
            int c = string.Compare(a.Section, b.Section, StringComparison.OrdinalIgnoreCase);
            if (c == 0) c = b.Order.CompareTo(a.Order);
            return c != 0 ? c : string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
        });

        string section = null;
        foreach (var row in rows)
        {
            if (row.Section != section)
            {
                section = row.Section;
                Kit.Text(content, "<b>" + section + "</b>");
            }
            if (!row.ReadOnly)
            {
                SettingRows.AddEntry(content, row.Entry, row.Label, row.Help);
                continue;
            }
            var line = Kit.Row(content);
            var text = Kit.Label(line, row.Label);
            if (row.Help.Length > 0) Kit.Tooltip(text, row.Label, row.Help);
            Kit.Fixed(line, row.Entry.GetSerializedValue(), 200f, TextAlignmentOptions.MidlineRight, ColorHelper.poiTooltipMuted);
        }
        return rows.Count;
    }

    private static object ManagerTag(ConfigEntryBase entry)
    {
        var tags = entry.Description?.Tags;
        if (tags == null) return null;
        foreach (var t in tags)
            if (t != null && t.GetType().Name == "ConfigurationManagerAttributes") return t;
        return null;
    }

    private static object Field(object tag, string name) => tag?.GetType().GetField(name)?.GetValue(tag);

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        var others = Others();
        int expected = 0;
        foreach (var info in others)
            foreach (var kv in info.Instance.Config)
                if (Field(ManagerTag(kv.Value), "Browsable") is not false) expected++;

        ModWindow.Open("mods");
        ctx.Cleanup(ModWindow.Close);
        yield return null;
        // One gap above the "Other mods" heading, then one above each plugin's section.
        int gaps = 0, rows = 0;
        if (ModWindow.IsOpen)
        {
            var content = ModWindow.Content;
            for (int i = 0; i < content.childCount; i++)
            {
                string name = content.GetChild(i).name;
                if (name == "Gap") gaps++;
                else if (name == "Row") rows++;
            }
        }
        string detail = $"tab={ModWindow.CurrentTab} plugins={gaps - 1}/{others.Count} rows={rows}/{expected}";
        if (ModWindow.CurrentTab == "mods" && gaps - 1 == others.Count && rows == expected) ctx.Pass("tab", detail);
        else ctx.Fail("tab", detail);
        yield return ctx.Screenshot("window-mods");
    }
}
