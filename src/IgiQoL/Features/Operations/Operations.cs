using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Dungeon;
using Behaviour.Managers;
using Behaviour.UI.Dungeon;
using Behaviour.Util;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using Source.Dungeon;
using Source.Galaxy;
using Source.Player;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.Operations;

public sealed class Operations : Feature
{
    public static Operations Instance;

    public Operations() { Instance = this; }

    public override string Id => "Operations";
    public override string Title => "Operations tab";
    public override string Description => "The Operations tab: your starting choices for operations, ECHO's choices, and the operations running now.";
    public override string SettingsTab => "Operations";
    public override Type[] PatchClasses => new[] { typeof(DungeonPanelPatch) };

    public ConfigEntry<bool> PlayerAutoBuyOut;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        PlayerAutoBuyOut = cfg.Bind(Id, "PlayerAutoBuyOut", false, SettingInfo.Describe("Accept buyouts automatically",
            "Ticks 'Auto Accept Buyouts' on the operation panel whenever you start a new operation. The game remembers your stealth, ammo and movement choices between operations, but not this one. Buyouts are offers from defenders to switch to your side for credits."));
    }

    public override void OnActivated()
    {
        ModWindow.AddTab("operations", "Operations", 20, BuildOperations);
    }

    // Display names in StealthMode / AmmoType declaration order, and the explanations the game's own tooltips give.
    public static readonly string[] StealthNames = { "Silent", "Balanced", "Aggressive" };
    public static readonly string[] AmmoNames = { "Standard", "Armour piercing", "Hollow" };
    public const string StealthHelp = "How your boarding crew advance. Silent: slower and weaker (-20% attack) but fewer casualties and fewer alerts. Balanced: normal. Aggressive: +20% attack but enemies notice much sooner.";
    public const string AmmoHelp = "Standard: normal against everyone. Armour piercing: strong against armored crew (Marines, Gunners) and does the most damage to the ship. Hollow: weak against armored crew, least damage to the ship, more noise.";
    public const string AutoMoveHelp = "Your crew move through the ship's compartments on their own. Turn off to move them yourself.";

    private void BuildOperations(RectTransform content)
    {
        Kit.Heading(content, "Operations you start");
        Kit.Muted(content, "What the operation panel starts with. The game remembers these between operations; changing them here is the same as changing them on the panel.");

        var row = Kit.Row(content);
        Kit.Tooltip(Kit.Label(row, "Stealth"), "Stealth", StealthHelp);
        Kit.Dropdown(row, StealthNames, (int)GameplayerPrefs.GetDungeonStealthMode(),
            i => GameplayerPrefs.SetDungeonStealthMode((StealthMode)i), 200f);

        row = Kit.Row(content);
        Kit.Tooltip(Kit.Label(row, "Ammo"), "Ammo", AmmoHelp);
        Kit.Dropdown(row, AmmoNames, (int)GameplayerPrefs.GetDungeonAmmoType(),
            i => GameplayerPrefs.SetDungeonAmmoType((AmmoType)i), 200f);

        row = Kit.Row(content);
        Kit.Tooltip(Kit.Label(row, "Automatic movement"), "Automatic movement", AutoMoveHelp);
        Kit.Toggle(row, null, GameplayerPrefs.GetDungeonAutoMove(), v => GameplayerPrefs.SetDungeonAutoMove(v));

        row = Kit.Row(content);
        string buyOutLabel = SettingInfo.Of(PlayerAutoBuyOut).Label;
        Kit.Tooltip(Kit.Label(row, buyOutLabel), buyOutLabel, PlayerAutoBuyOut.Description.Description);
        Kit.Toggle(row, null, PlayerAutoBuyOut.Value, v => PlayerAutoBuyOut.Value = v);

        var echo = EchoOperations.EchoOperations.Instance;
        Kit.Heading(content, "Operations ECHO starts");
        if (echo.Active)
        {
            Kit.Muted(content, "Used when ECHO starts an operation on its own. 'Same as yours' copies your choice from the section above.");
            SettingRows.AddEntries(content, echo.Id, SettingRows.Rows.Options);
        }
        else Kit.Muted(content, "ECHO's operations are turned off on the Settings tab, so ECHO uses the game's normal choices: your stealth and ammo from above, and it never accepts buyouts.");

        Kit.Heading(content, "Running operations");
        Kit.Muted(content, "Operations in progress, yours and ECHO's. Changes here apply to the running operation at once.");
        var column = Kit.Column(content);
        column.gameObject.AddComponent<OperationsList>();
    }

    public static List<DungeonOperation> CurrentOperations() =>
        Singleton<DungeonManager>.HasInstance ? Singleton<DungeonManager>.Instance._operations : null;

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        CheckPlayerBuyOut(ctx);

        ModWindow.Open("operations");
        ctx.Cleanup(ModWindow.Close);
        var ops = CurrentOperations();
        string detail = $"running={(ops == null ? 0 : ops.Count)} tab={ModWindow.CurrentTab}";
        if (ModWindow.IsOpen && ModWindow.CurrentTab == "operations" && ModWindow.Content.GetComponentInChildren<OperationsList>() != null)
            ctx.Pass("tab", detail);
        else ctx.Fail("tab", detail + $" open={ModWindow.IsOpen}");
        yield return ctx.Screenshot("window-operations");
    }

    private void CheckPlayerBuyOut(SelfCheckContext ctx)
    {
        DungeonPanel panel = null;
        foreach (var p in Resources.FindObjectsOfTypeAll<DungeonPanel>())
            if (p.gameObject.scene.IsValid()) { panel = p; break; }
        if (panel == null)
        {
            ctx.Skip("player-buyout", "no DungeonPanel in scene");
            return;
        }
        if (panel.autoAcceptBuyOutToggle == null)
        {
            ctx.Skip("player-buyout", "DungeonPanel has no buy-out toggle");
            return;
        }
        bool savedEntry = PlayerAutoBuyOut.Value;
        bool savedToggle = panel.autoAcceptBuyOutToggle.isOn;
        var savedSim = panel._sim;
        try
        {
            PlayerAutoBuyOut.Value = true;
            panel.autoAcceptBuyOutToggle.SetIsOnWithoutNotify(false);
            panel._sim = null;
            panel.SyncPreStartCombatOptions();
            bool on = panel.autoAcceptBuyOutToggle.isOn;
            if (on) ctx.Pass("player-buyout", "toggle ticked by sync");
            else ctx.Fail("player-buyout", "toggle still off after SyncPreStartCombatOptions");
        }
        finally
        {
            panel._sim = savedSim;
            panel.autoAcceptBuyOutToggle.SetIsOnWithoutNotify(savedToggle);
            PlayerAutoBuyOut.Value = savedEntry;
        }
    }
}

[HarmonyPatch]
internal static class DungeonPanelPatch
{
    private static Operations F => Operations.Instance;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DungeonPanel), nameof(DungeonPanel.SyncPreStartCombatOptions))]
    private static void SyncPreStartCombatOptionsPostfix(DungeonPanel __instance)
    {
        if (!F.Active) return;
        try
        {
            if (F.PlayerAutoBuyOut.Value && __instance._sim == null && __instance.autoAcceptBuyOutToggle != null)
                __instance.autoAcceptBuyOutToggle.SetIsOnWithoutNotify(true);
        }
        catch (Exception e) { F.Fail(e, nameof(DungeonPanelPatch)); }
    }
}

// Lists the operations in DungeonManager; rows are rebuilt only when the set of operations changes, labels refresh once per second.
public sealed class OperationsList : MonoBehaviour
{
    private sealed class OpRow
    {
        public DungeonOperation op;
        public TMP_Text summary;
        public Kit.Choice stealth, ammo;
        public Button autoMove, buyOut;
    }

    private readonly List<OpRow> rows = new();
    private float nextRefresh;
    private RectTransform column;

    private void Awake() { column = (RectTransform)transform; }

    private void Start()
    {
        Rebuild(Operations.CurrentOperations());
        Refresh();
    }

    private void Update()
    {
        if (!Operations.Instance.Active) return;
        try
        {
            var ops = Operations.CurrentOperations();
            if (Changed(ops))
            {
                Rebuild(ops);
                Refresh();
                nextRefresh = Time.unscaledTime + 1f;
                return;
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f;
            Refresh();
        }
        catch (Exception e) { Operations.Instance.Fail(e, nameof(OperationsList)); }
    }

    private bool Changed(List<DungeonOperation> ops)
    {
        int count = ops == null ? 0 : ops.Count;
        if (count != rows.Count) return true;
        for (int i = 0; i < count; i++)
            if (rows[i].op != ops[i]) return true;
        return false;
    }

    private void Rebuild(List<DungeonOperation> ops)
    {
        for (int i = column.childCount - 1; i >= 0; i--)
        {
            var child = column.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        rows.Clear();
        if (ops == null || ops.Count == 0)
        {
            Kit.Muted(column, "None");
            return;
        }
        for (int i = 0; i < ops.Count; i++)
        {
            var op = ops[i];
            var r = new OpRow { op = op };
            var line1 = Kit.Row(column, 6f, 22f);
            r.summary = Kit.Label(line1, "", 12f);
            var line2 = Kit.Row(column, 6f, 26f);
            Kit.Label(line2, "Stealth");
            r.stealth = Kit.Dropdown(line2, Operations.StealthNames, (int)op.options.stealthMode, v => SetStealth(op, (StealthMode)v), 110f);
            Kit.Label(line2, "Ammo");
            r.ammo = Kit.Dropdown(line2, Operations.AmmoNames, (int)op.options.ammoType, v => SetAmmo(op, (AmmoType)v), 130f);
            r.autoMove = Kit.Toggle(line2, "Auto movement", op.options.autoMove, v => SetAutoMove(op, v));
            r.buyOut = Kit.Toggle(line2, "Auto buyouts", op.options.autoAcceptBuyOut, v => SetBuyOut(op, v));
            rows.Add(r);
        }
    }

    // The simulation reads its options live; write the same field on both objects when they differ.
    private static void SetStealth(DungeonOperation op, StealthMode v)
    {
        op.options.stealthMode = v;
        var s = op.simulation?.options;
        if (s != null && s != op.options) s.stealthMode = v;
    }

    private static void SetAmmo(DungeonOperation op, AmmoType v)
    {
        op.options.ammoType = v;
        var s = op.simulation?.options;
        if (s != null && s != op.options) s.ammoType = v;
    }

    private static void SetAutoMove(DungeonOperation op, bool v)
    {
        op.options.autoMove = v;
        var s = op.simulation?.options;
        if (s != null && s != op.options) s.autoMove = v;
    }

    private static void SetBuyOut(DungeonOperation op, bool v)
    {
        op.options.autoAcceptBuyOut = v;
        var s = op.simulation?.options;
        if (s != null && s != op.options) s.autoAcceptBuyOut = v;
    }

    private void Refresh()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var op = r.op;
            // The game's pixel font has no middle dot, so the separators are plain ASCII.
            string text = MapPointOfInterest.GetDungeonPoiName(op.location.dungeonType, op.location.faction)
                + " | " + (op.isAutonomous ? "ECHO" : "You") + " | " + op.phase;
            var sim = op.simulation;
            if (sim != null)
                text += " | " + GameMath.FormatTime((int)sim.elapsed, true) + "/" + GameMath.FormatTime((int)sim.estimatedDuration, true)
                    + " | kills " + sim.hostileKilled + " | lost " + sim.friendlyKilled + " | wounded " + sim.friendlyWounded;
            if (r.summary.text != text) r.summary.text = text;

            // Follow changes made elsewhere (the vanilla panel edits the same options object).
            var o = sim?.options ?? op.options;
            if (r.stealth.GetIndex() != (int)o.stealthMode) r.stealth.SetIndex((int)o.stealthMode);
            if (r.ammo.GetIndex() != (int)o.ammoType) r.ammo.SetIndex((int)o.ammoType);
            var moveState = r.autoMove.GetComponent<Kit.ToggleState>();
            if (moveState.on != o.autoMove) Kit.SetToggle(r.autoMove, o.autoMove);
            var buyState = r.buyOut.GetComponent<Kit.ToggleState>();
            if (buyState.on != o.autoAcceptBuyOut) Kit.SetToggle(r.buyOut, o.autoAcceptBuyOut);
        }
    }
}
