using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Gameplay;
using Behaviour.UI.Side_Menu.SideTabs;
using Behaviour.Unit;
using Behaviour.Util;
using Behaviour.Weapons;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using LightJson;
using Source.Player;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.EchoPriority;

public enum EchoActivity { MiningSurface, MiningCore, SalvageSurface, SalvageCore, Combat, Cargo }

public static class EchoActivityInfo
{
    public static (GameplayType type, TargetLayer layer) Of(EchoActivity a)
    {
        switch (a)
        {
            case EchoActivity.MiningSurface: return (GameplayType.Mining, TargetLayer.Surface);
            case EchoActivity.MiningCore: return (GameplayType.Mining, TargetLayer.Core);
            case EchoActivity.SalvageSurface: return (GameplayType.Salvage, TargetLayer.Surface);
            case EchoActivity.SalvageCore: return (GameplayType.Salvage, TargetLayer.Core);
            case EchoActivity.Combat: return (GameplayType.Combat, TargetLayer.Both);
            default: return (GameplayType.Cargo, TargetLayer.Both);
        }
    }

    // Same texts as the vanilla Autopilot loadout dropdown (Autopilot.SetLoadoutOptions).
    public static string Name(EchoActivity a)
    {
        switch (a)
        {
            case EchoActivity.MiningSurface: return "Mining Surface";
            case EchoActivity.MiningCore: return "Mining Core";
            case EchoActivity.SalvageSurface: return "Salvage Surface";
            case EchoActivity.SalvageCore: return "Salvage " + SpaceShip.GetTargetLayerName(GameplayType.Salvage, TargetLayer.Core);
            case EchoActivity.Combat: return "Combat";
            default: return "Cargo";
        }
    }
}

public sealed class EchoPriority : Feature
{
    public static EchoPriority Instance;

    public EchoPriority() { Instance = this; }

    public override string Id => "EchoPriority";
    public override string Title => "ECHO activity priority";
    public override string Description => "Gives ECHO an ordered list of activities to try, set on the ECHO tab, instead of the game's single preferred activity.";
    public override Type[] PatchClasses => new[] { typeof(EchoPriorityPatches) };

    public static List<EchoActivity> List = new List<EchoActivity>();

    private static bool tabAdded;

    // Every feature whose settings live on the ECHO tab calls this from OnActivated; the tab is added once. It has no
    // owner: several features share it, and BuildEcho guards each section by its own feature.
    public static void EnsureTab()
    {
        if (tabAdded) return;
        tabAdded = true;
        ModWindow.AddTab(null, "echo", "ECHO", 10, BuildEcho);
    }

    public override void OnActivated()
    {
        LoadList();
        GameEvents.SaveLoaded += OnSaveLoaded;
        EnsureTab();
    }

    private void OnSaveLoaded()
    {
        if (!Active) return;
        try
        {
            LoadList();
            Reconcile();
        }
        catch (Exception e) { Fail(e, nameof(OnSaveLoaded)); }
    }

    private static void LoadList()
    {
        List.Clear();
        var arr = SaveData.Feature("EchoPriority", true)["list"];
        if (!arr.IsJsonArray) return;
        foreach (var v in arr.AsJsonArray)
        {
            if (!v.IsString) continue;
            if (Enum.TryParse<EchoActivity>(v.AsString, out var a) && Enum.IsDefined(typeof(EchoActivity), a) && !List.Contains(a))
                List.Add(a);
        }
    }

    private static void WriteList()
    {
        var arr = new JsonArray();
        for (int i = 0; i < List.Count; i++) arr.Add(List[i].ToString());
        SaveData.Feature("EchoPriority", true)["list"] = arr;
    }

    public static void SetList(IList<EchoActivity> items)
    {
        var copy = new List<EchoActivity>(items.Count);
        for (int i = 0; i < items.Count; i++)
            if (!copy.Contains(items[i])) copy.Add(items[i]);
        List = copy;
        WriteList();
        ApplyHead();
    }

    public static void ApplyHead()
    {
        var player = GamePlayer.current;
        if (player == null) return;
        var ap = player.autopilotSettings;
        if (List.Count == 0)
        {
            ap.preferredGameplayType = GameplayType.Generic;
            ap.preferredTargetLayer = TargetLayer.Both;
        }
        else
        {
            var (type, layer) = EchoActivityInfo.Of(List[0]);
            ap.preferredGameplayType = type;
            ap.preferredTargetLayer = layer;
            if (type == GameplayType.Cargo) ap.runMissions = true;
        }
        int index = List.Count == 0 ? 0 : (int)List[0] + 1;
        foreach (var a in Resources.FindObjectsOfTypeAll<Autopilot>())
        {
            if (!a.gameObject.scene.IsValid()) continue;
            if (a.loadoutDropdown != null) a.loadoutDropdown.SetValueWithoutNotify(index);
            if (a.runMissionsToggle != null) a.runMissionsToggle.SetIsOnWithoutNotify(ap.runMissions);
        }
        if (Singleton<IdleManager>.HasInstance) IdleManager.ForceImmediateActivityCheck();
    }

    // Vanilla -> list: the preferred activity the Autopilot tab shows becomes the head; the rest keeps its order.
    public static void Reconcile()
    {
        var player = GamePlayer.current;
        if (player == null) return;
        var ap = player.autopilotSettings;
        if (ap.preferredGameplayType == GameplayType.Generic)
        {
            List.Clear();
        }
        else
        {
            bool found = false;
            EchoActivity match = EchoActivity.Combat;
            foreach (EchoActivity a in Enum.GetValues(typeof(EchoActivity)))
            {
                var (type, layer) = EchoActivityInfo.Of(a);
                if (type == ap.preferredGameplayType && layer == ap.preferredTargetLayer) { match = a; found = true; break; }
            }
            if (!found) return;
            List.Remove(match);
            List.Insert(0, match);
        }
        WriteList();
    }

    private static void BuildEcho(RectTransform content)
    {
        if (Instance.Active) BuildPriority(content);
        else
        {
            Kit.Heading(content, "Activity priority");
            Kit.Muted(content, "Turned off on the Settings tab.");
        }

        var gates = EchoGates.EchoGates.Instance;
        SettingRows.AddSection(content, gates);
        if (gates.Active) SettingRows.AddEntries(content, gates.Id, SettingRows.Rows.Options);
        else Kit.Muted(content, "Turned off on the Settings tab.");
        Kit.Muted(content, "Stealth, ammo and buyouts for operations ECHO starts are on the Operations tab.");

        var missions = EchoMissions.EchoMissions.Instance;
        SettingRows.AddSection(content, missions);
        if (missions.Active) SettingRows.AddEntries(content, missions.Id, SettingRows.Rows.Options);
        else Kit.Muted(content, "Turned off on the Settings tab.");

        var retreat = EchoRetreat.EchoRetreat.Instance;
        SettingRows.AddSection(content, retreat);
        if (retreat.Active) SettingRows.AddEntries(content, retreat.Id, SettingRows.Rows.Options);
        else Kit.Muted(content, "Turned off on the Settings tab.");

        var kit = EchoKit.EchoKit.Instance;
        SettingRows.AddSection(content, kit);
        if (kit.Active)
        {
            SettingRows.AddEntries(content, kit.Id, SettingRows.Rows.Options);
            EchoKit.EchoKitSection.Build(content);
        }
        else Kit.Muted(content, "Turned off on the Settings tab.");

        var ores = OrePreference.OrePreference.Instance;
        SettingRows.AddSection(content, ores);
        if (ores.Active) OrePreference.OrePreferenceSection.Build(content);
        else Kit.Muted(content, "Turned off on the Settings tab.");
    }

    private static void BuildPriority(RectTransform content)
    {
        Kit.Heading(content, "Activity priority");
        Kit.Muted(content, "When ECHO is looking for something to do (docked, or in empty space), it tries these activities from the top down and takes the first one your ship is equipped for and can find work for. The top entry is also the preferred activity on the game's Autopilot tab: picking an activity in that dropdown moves it to the top here, and picking Autodetect empties this list.");

        for (int i = 0; i < List.Count; i++)
        {
            int index = i;
            var row = Kit.Row(content, 6f, 26f);
            Kit.Label(row, (i + 1) + ". " + EchoActivityInfo.Name(List[i]));
            Kit.Tooltip(Kit.TextTile(row, "^", 22f, () => Edit(l => { if (index > 0) { var t = l[index]; l[index] = l[index - 1]; l[index - 1] = t; } })).targetGraphic, null, "Move up");
            Kit.Tooltip(Kit.TextTile(row, "v", 22f, () => Edit(l => { if (index < l.Count - 1) { var t = l[index]; l[index] = l[index + 1]; l[index + 1] = t; } })).targetGraphic, null, "Move down");
            Kit.Tooltip(Kit.TextTile(row, "x", 22f, () => Edit(l => l.RemoveAt(index))).targetGraphic, null, "Remove from the list");
        }

        var missing = new List<EchoActivity>();
        var names = new List<string>();
        foreach (EchoActivity a in Enum.GetValues(typeof(EchoActivity)))
        {
            if (List.Contains(a)) continue;
            missing.Add(a);
            names.Add(EchoActivityInfo.Name(a));
        }
        if (missing.Count == 0)
        {
            Kit.Muted(content, "All activities are listed.");
        }
        else
        {
            var addRow = Kit.Row(content, 6f, 26f);
            Kit.Label(addRow, "Add an activity");
            var choice = Kit.Dropdown(addRow, names, 0, _ => { }, 180f);
            Kit.TextButton(addRow, "Add", () =>
            {
                int pick = choice.GetIndex();
                if (pick < 0 || pick >= missing.Count) return;
                var entry = missing[pick];
                Edit(l => l.Add(entry));
            }, 60f);
        }
        Kit.Muted(content, "Leave the list empty to use the game's normal behaviour: the single preferred activity from the Autopilot tab.");
    }

    private static void Edit(Action<List<EchoActivity>> change)
    {
        var copy = new List<EchoActivity>(List);
        change(copy);
        SetList(copy);
        ModWindow.Show("echo");
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        var player = GamePlayer.current;
        if (player == null)
        {
            ctx.Skip("sync", "no player");
            yield break;
        }
        var ap = player.autopilotSettings;
        var original = new List<EchoActivity>(List);
        var origType = ap.preferredGameplayType;
        var origLayer = ap.preferredTargetLayer;
        bool origRun = ap.runMissions;
        ctx.Cleanup(() =>
        {
            SetList(original);
            ap.preferredGameplayType = origType;
            ap.preferredTargetLayer = origLayer;
            ap.runMissions = origRun;
            ModWindow.Close();
        });

        Autopilot autopilot = null;
        foreach (var a in Resources.FindObjectsOfTypeAll<Autopilot>())
            if (a.gameObject.scene.IsValid() && a.loadoutDropdown != null) { autopilot = a; break; }

        SetList(new[] { EchoActivity.Combat, EchoActivity.MiningSurface });
        string detail = $"type={ap.preferredGameplayType} layer={ap.preferredTargetLayer}";
        bool fieldsOk = ap.preferredGameplayType == GameplayType.Combat && ap.preferredTargetLayer == TargetLayer.Both;
        if (!fieldsOk) ctx.Fail("sync", detail);
        else if (autopilot == null) ctx.Pass("sync", detail + " (dropdown not checked: no Autopilot tab in scene)");
        else if (autopilot.loadoutDropdown.value == 5) ctx.Pass("sync", detail + " dropdown=5");
        else ctx.Fail("sync", detail + $" dropdown={autopilot.loadoutDropdown.value} expected 5");

        if (autopilot == null)
        {
            ctx.Skip("dropdown-to-list", "no Autopilot tab in scene");
        }
        else
        {
            SetList(new[] { EchoActivity.Combat, EchoActivity.MiningSurface, EchoActivity.SalvageSurface });
            autopilot.loadoutDropdown.SetValueWithoutNotify(1);
            autopilot.ChangeLoadout();
            string d = "list=" + string.Join(",", List);
            if (List.Count == 3 && List[0] == EchoActivity.MiningSurface) ctx.Pass("dropdown-to-list", d);
            else ctx.Fail("dropdown-to-list", d + " expected MiningSurface first with 3 entries");
        }

        ModWindow.Open("echo");
        yield return ctx.Screenshot("window-echo");
    }
}

[HarmonyPatch]
internal static class EchoPriorityPatches
{
    private static EchoPriority F => EchoPriority.Instance;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Autopilot), nameof(Autopilot.ChangeLoadout))]
    private static void ChangeLoadoutPostfix()
    {
        if (!F.Active) return;
        try { EchoPriority.Reconcile(); }
        catch (Exception e) { F.Fail(e, nameof(EchoPriorityPatches)); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.TryPreferredActivity))]
    private static bool TryPreferredActivityPrefix(IdleManager __instance, ref bool __result)
    {
        if (!F.Active) return true;
        try
        {
            var list = EchoPriority.List;
            if (list.Count == 0) return true;
            var ship = GamePlayer.current.currentSpaceShip;
            bool anyCandidate = false, anyLoadout = false;
            for (int i = 0; i < list.Count; i++)
            {
                var (type, layer) = EchoActivityInfo.Of(list[i]);
                if (type == GameplayType.Cargo) continue; // vanilla has no search for Cargo either
                anyCandidate = true;
                if (!ship.HasLoadout(type, layer)) continue;
                anyLoadout = true;
                bool found = type == GameplayType.Mining ? __instance.FindMiningActivity(layer)
                    : type == GameplayType.Salvage ? __instance.FindSalvageActivity(layer)
                    : __instance.FindCombatActivity();
                if (found) { __result = true; return false; }
            }
            if (anyCandidate && !anyLoadout)
            {
                var head = EchoActivityInfo.Of(list[0]).type;
                IdleManager.UpdateActivity("@IdleNoSuitableLoadoutForActivity", head);
                __result = true;
                return false;
            }
            __result = false;
            return false;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoPriorityPatches)); return true; }
    }
}
