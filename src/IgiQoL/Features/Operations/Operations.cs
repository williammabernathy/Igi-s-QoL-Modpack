using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Dungeon;
using Behaviour.Managers;
using Behaviour.UI;
using Behaviour.UI.Dungeon;
using Behaviour.Util;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using Source.CompartmentSystem;
using Source.Data.Persistable;
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
    public override Type[] PatchClasses => new[] { typeof(DungeonPanelPatch), typeof(PriorityCompartmentPatch) };

    public ConfigEntry<bool> PlayerAutoBuyOut;
    public ConfigEntry<string> BoardingCompartment, InstallationCompartment;

    public const string NoCompartment = "None";

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        PlayerAutoBuyOut = cfg.Bind(Id, "PlayerAutoBuyOut", false, SettingInfo.Describe("Accept buyouts automatically",
            "Ticks 'Auto Accept Buyouts' on the operation panel whenever you start a new operation. The game remembers your stealth, ammo and movement choices between operations, but not this one. Buyouts are offers from defenders to switch to your side for credits."));
        var names = new List<string> { NoCompartment };
        names.AddRange(Enum.GetNames(typeof(CompartmentType)));
        BoardingCompartment = cfg.Bind(Id, "BoardingCompartment", NoCompartment, SettingInfo.Describe("Boardings",
            "The compartment your crew make for first when a boarding of a ship starts, yours and ECHO's: the first compartment of that type in the ship, as if you had clicked it on the operation screen. You can still click another one. A ship without that compartment gets no priority. None: the game default, no priority.",
            new AcceptableValueList<string>(names.ToArray())));
        InstallationCompartment = cfg.Bind(Id, "InstallationCompartment", NoCompartment, SettingInfo.Describe("Installations",
            "The same for operations at installations, where your crew go in on foot instead of by boarding pod: outposts, research stations, derelicts, ruins and the like. None: the game default, no priority.",
            new AcceptableValueList<string>(names.ToArray())));
    }

    public override void OnActivated()
    {
        ModWindow.AddTab(this, "operations", "Operations", 20, BuildOperations);
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

        Kit.Heading(content, "Priority compartment");
        Kit.Muted(content, "The compartment your crew go for first when an operation starts, yours and ECHO's, as if you had clicked it on the operation screen. You can still click another one. The game doesn't remember a priority between operations, and the rooms differ from site to site, so this picks by kind of room.");
        AddCompartmentRow(content, BoardingCompartment, BoardingTypes());
        AddCompartmentRow(content, InstallationCompartment, InstallationTypes());

        Kit.Heading(content, "Running operations");
        Kit.Muted(content, "Operations in progress, yours and ECHO's. Changes here apply to the running operation at once.");
        var column = Kit.Column(content);
        column.gameObject.AddComponent<OperationsList>();
    }

    public static List<DungeonOperation> CurrentOperations() =>
        Singleton<DungeonManager>.HasInstance ? Singleton<DungeonManager>.Instance._operations : null;

    private static void AddCompartmentRow(RectTransform content, ConfigEntry<string> entry, List<CompartmentType> types)
    {
        // A type set in the cfg that no layout uses still shows, so the dropdown never misreports the setting.
        if (Enum.TryParse<CompartmentType>(entry.Value, out var current) && !types.Contains(current)) types.Add(current);
        var names = new List<string> { "None (game default)" };
        int index = 0;
        foreach (var t in types)
        {
            if (t.ToString() == entry.Value) index = names.Count;
            names.Add(CompartmentName(t));
        }
        var row = Kit.Row(content);
        string label = SettingInfo.Of(entry).Label;
        Kit.Tooltip(Kit.Label(row, label), label, entry.Description.Description);
        Kit.Dropdown(row, names, index, i => entry.Value = i == 0 ? NoCompartment : types[i - 1].ToString(), 200f);
    }

    public static string CompartmentName(CompartmentType type)
    {
        string name = Translation.Translate("@Compartment" + type);
        return string.IsNullOrEmpty(name) || name.StartsWith("@") ? SettingRows.Pretty(type.ToString()) : name;
    }

    // Every room type a boarded ship can have: ship layouts depend only on the hull size.
    public static List<CompartmentType> BoardingTypes()
    {
        var set = new HashSet<CompartmentType>();
        foreach (Source.SpaceShip.SpaceShipType size in Enum.GetValues(typeof(Source.SpaceShip.SpaceShipType)))
            foreach (var c in ShipCompartmentLayout.Build(size, 0)) set.Add(c.type);
        return Rooms(set);
    }

    // Every room type the walk-in operations draw their layouts from.
    public static List<CompartmentType> InstallationTypes()
    {
        var set = new HashSet<CompartmentType>();
        foreach (DungeonType t in Enum.GetValues(typeof(DungeonType)))
        {
            var def = DungeonDefinition.Get(t);
            if (def == null || def.approachStyle == ApproachStyle.Pods) continue;
            foreach (var entry in def.GetCompartmentPool()) set.Add(entry.type);
        }
        return Rooms(set);
    }

    // The crew start in the airlock, and corridors repeat with nothing to find, so neither is offered.
    private static List<CompartmentType> Rooms(HashSet<CompartmentType> set)
    {
        set.Remove(CompartmentType.Airlock);
        set.Remove(CompartmentType.Corridor);
        var list = new List<CompartmentType>(set);
        list.Sort();
        return list;
    }

    // Sets the starting priority of a new simulation to the first compartment of the chosen type, unless it already has
    // one. Returns the index set, or -1.
    public static int ApplyPriority(DungeonSimulation sim, string typeName)
    {
        if (sim?.options == null || sim.options.priorityCompartmentIndex.HasValue) return -1;
        if (typeName == NoCompartment || !Enum.TryParse<CompartmentType>(typeName, out var type)) return -1;
        int index = ShipCompartmentLayout.FindFirst(sim.compartments, type);
        if (index >= 0) sim.options.priorityCompartmentIndex = index;
        return index;
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        CheckPlayerBuyOut(ctx);
        CheckExitStates(ctx);
        CheckPriority(ctx);

        ModWindow.Open("operations");
        ctx.Cleanup(ModWindow.Close);
        var ops = CurrentOperations();
        string detail = $"running={(ops == null ? 0 : ops.Count)} tab={ModWindow.CurrentTab}";
        if (ModWindow.IsOpen && ModWindow.CurrentTab == "operations" && ModWindow.Content.GetComponentInChildren<OperationsList>() != null)
            ctx.Pass("tab", detail);
        else ctx.Fail("tab", detail + $" open={ModWindow.IsOpen}");
        ScrollToRunning();
        yield return ctx.Screenshot("window-operations");

        yield return CheckExitButton(ctx);
    }

    // The running list is the tab's last section; scroll to the end so a screenshot shows it, not just its heading.
    private static void ScrollToRunning()
    {
        var scroll = ModWindow.IsOpen ? ModWindow.Content.GetComponentInParent<ScrollRect>() : null;
        if (scroll == null) return;
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 0f;
    }

    // The boarding postfix on the game's own boarding setup: None leaves no priority, Bridge picks the first bridge, and
    // a priority already set stays. Installations use the same helper on a walk-in layout from a real definition.
    private void CheckPriority(SelfCheckContext ctx)
    {
        string savedBoarding = BoardingCompartment.Value, savedInstallation = InstallationCompartment.Value;
        try
        {
            var def = DungeonDefinition.Get(DungeonType.HostileShip);
            if (def == null) ctx.Skip("priority-boarding", "no boarding definition");
            else
            {
                DungeonSimulation Board(DungeonOptions o) => DungeonSimulation.CreateForShipBoarding(
                    new DungeonLocationData { dungeonType = DungeonType.HostileShip, shipSizeTier = Source.SpaceShip.SpaceShipType.Size4, level = 10 }, o, def);
                string Shown(DungeonSimulation s) => s.options.priorityCompartmentIndex?.ToString() ?? "unset";
                BoardingCompartment.Value = NoCompartment;
                var none = Board(new DungeonOptions());
                BoardingCompartment.Value = nameof(CompartmentType.Bridge);
                var bridge = Board(new DungeonOptions());
                var preset = Board(new DungeonOptions { priorityCompartmentIndex = 0 });
                int want = ShipCompartmentLayout.FindFirst(bridge.compartments, CompartmentType.Bridge);
                string detail = $"none={Shown(none)} bridge={Shown(bridge)} (first bridge {want} of {bridge.compartments.Count}) preset={Shown(preset)}";
                if (!none.options.priorityCompartmentIndex.HasValue && want >= 0 && bridge.options.priorityCompartmentIndex == want && preset.options.priorityCompartmentIndex == 0)
                    ctx.Pass("priority-boarding", detail);
                else ctx.Fail("priority-boarding", detail);
            }

            DungeonDefinition walk = null;
            foreach (DungeonType t in Enum.GetValues(typeof(DungeonType)))
            {
                var d = DungeonDefinition.Get(t);
                if (d != null && d.approachStyle != ApproachStyle.Pods) { walk = d; break; }
            }
            if (walk == null) ctx.Skip("priority-installation", "no walk-in dungeon definition");
            else
            {
                var sim = new DungeonSimulation();
                sim.compartments = DungeonCompartmentLayout.Build(walk.layoutStyle, walk.GetCompartmentPool(), 12, new SeededRandom(7UL));
                var types = InstallationTypes();
                // The last offered room of the layout, so the pick has to skip past the airlock and other rooms.
                var room = CompartmentType.Airlock;
                foreach (var c in sim.compartments)
                    if (types.Contains(c.type)) room = c.type;
                InstallationCompartment.Value = room.ToString();
                int got = ApplyPriority(sim, InstallationCompartment.Value);
                int want = ShipCompartmentLayout.FindFirst(sim.compartments, room);
                string detail = $"{walk.dungeonType}: {room} at {got}, first {want} of {sim.compartments.Count}; offered {string.Join(", ", types.ConvertAll(CompartmentName))}";
                if (room != CompartmentType.Airlock && got > 0 && got == want && sim.options.priorityCompartmentIndex == want)
                    ctx.Pass("priority-installation", detail);
                else ctx.Fail("priority-installation", detail);
            }
        }
        finally
        {
            BoardingCompartment.Value = savedBoarding;
            InstallationCompartment.Value = savedInstallation;
        }
    }

    // ExitFor on bare operations in each state the button distinguishes.
    private void CheckExitStates(SelfCheckContext ctx)
    {
        var fails = new List<string>();
        void Expect(string name, DungeonOperation op, OperationsList.ExitAction want)
        {
            var got = OperationsList.ExitFor(op);
            if (got != want) fails.Add($"{name}: {got}, want {want}");
        }
        DungeonOperation Op(DungeonSimulation sim)
        {
            var op = new DungeonOperation(null, new DungeonLocationData(), new DungeonOptions(), false);
            if (sim != null) { op.simulation = sim; op.phase = MissionPhase.Active; }
            return op;
        }

        Expect("approach", Op(null), OperationsList.ExitAction.Abandon);
        Expect("fighting", Op(new DungeonSimulation()), OperationsList.ExitAction.Abandon);
        Expect("victory", Op(new DungeonSimulation { outcome = MissionOutcome.FriendlyVictory }), OperationsList.ExitAction.Extract);
        var awaiting = new DungeonSimulation { outcome = MissionOutcome.FriendlyVictory };
        awaiting.RequestExtraction();
        Expect("extract-requested", Op(awaiting), OperationsList.ExitAction.None);
        var retreating = new DungeonSimulation();
        retreating.isRetreating = true;
        Expect("retreating", Op(retreating), OperationsList.ExitAction.None);
        Expect("lost", Op(new DungeonSimulation { outcome = MissionOutcome.HostileVictory }), OperationsList.ExitAction.None);
        var extraction = Op(null);
        extraction.phase = MissionPhase.Extraction;
        Expect("extraction-phase", extraction, OperationsList.ExitAction.None);
        var pods = Op(null);
        pods._awaitingPodReturn = true;
        Expect("pods-returning", pods, OperationsList.ExitAction.None);

        if (fails.Count == 0) ctx.Pass("exit-states", "8 of 8 states");
        else ctx.Fail("exit-states", string.Join("; ", fails));
    }

    // A walk-approach operation with no ship (its tick returns at once) in the real list: the row's Abandon button
    // asks first, "Keep going" leaves it running, "Abandon" ends it and the row goes.
    private IEnumerator CheckExitButton(SelfCheckContext ctx)
    {
        var ops = CurrentOperations();
        var type = DungeonType.PirateBase;
        bool found = false;
        foreach (DungeonType t in Enum.GetValues(typeof(DungeonType)))
        {
            var def = DungeonDefinition.Get(t);
            if (def != null && def.approachStyle != ApproachStyle.Pods) { type = t; found = true; break; }
        }
        if (ops == null || !found)
        {
            ctx.Skip("exit-button", ops == null ? "no DungeonManager" : "no walk-approach dungeon definition");
            yield break;
        }
        var op = new DungeonOperation(null, new DungeonLocationData { dungeonType = type }, new DungeonOptions(), false);
        ops.Add(op);
        try
        {
            ModWindow.Open("operations");
            yield return null;
            yield return null;
            Button button = null;
            foreach (var b in ModWindow.Content.GetComponentInChildren<OperationsList>().GetComponentsInChildren<Button>())
            {
                var label = b.GetComponentInChildren<TMP_Text>();
                if (label != null && label.text == "Abandon") { button = b; break; }
            }
            if (button == null) { ctx.Fail("exit-button", $"no Abandon button for {type}"); yield break; }
            ScrollToRunning();
            yield return ctx.Screenshot("window-operations-abandon");

            button.onClick.Invoke();
            yield return null;
            var popup = AlertPopup.activeInstance;
            if (popup != null) yield return ctx.Screenshot("operations-abandon-confirm");
            if (popup == null || op.isComplete) { ctx.Fail("exit-button", $"no confirmation (popup={popup != null} complete={op.isComplete})"); yield break; }
            popup.cancelButton.onClick.Invoke();
            yield return null;
            if (op.isComplete || !ops.Contains(op)) { ctx.Fail("exit-button", "Keep going ended the operation"); yield break; }

            button.onClick.Invoke();
            yield return null;
            popup = AlertPopup.activeInstance;
            if (popup == null) { ctx.Fail("exit-button", "no confirmation on the second click"); yield break; }
            popup.submitButton.onClick.Invoke();
            bool complete = op.isComplete;
            yield return null;
            yield return null;
            bool removed = !ops.Contains(op);
            bool rowGone = button == null;
            if (complete && removed && rowGone) ctx.Pass("exit-button", $"{type}: confirm asked, Keep going kept it, Abandon ended it, row removed");
            else ctx.Fail("exit-button", $"complete={complete} removed={removed} rowGone={rowGone}");
        }
        finally
        {
            ops.Remove(op);
            if (AlertPopup.activeInstance != null) UnityEngine.Object.Destroy(AlertPopup.activeInstance.gameObject);
        }
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

// Sets the starting priority compartment from the Boardings / Installations settings once the layout exists.
[HarmonyPatch]
internal static class PriorityCompartmentPatch
{
    private static Operations F => Operations.Instance;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DungeonSimulation), nameof(DungeonSimulation.CreateForShipBoarding))]
    private static void CreateForShipBoardingPostfix(DungeonSimulation __result)
    {
        if (!F.Active) return;
        try { Operations.ApplyPriority(__result, F.BoardingCompartment.Value); }
        catch (Exception e) { F.Fail(e, nameof(CreateForShipBoardingPostfix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DungeonOperation), nameof(DungeonOperation.BeginWalkSimulation))]
    private static void BeginWalkSimulationPostfix(DungeonOperation __instance)
    {
        if (!F.Active) return;
        try { Operations.ApplyPriority(__instance.simulation, F.InstallationCompartment.Value); }
        catch (Exception e) { F.Fail(e, nameof(BeginWalkSimulationPostfix)); }
    }
}

// Lists the operations in DungeonManager; rows are rebuilt only when the set of operations changes, labels refresh once per second.
public sealed class OperationsList : MonoBehaviour
{
    // What the row's button does, mirroring the game panel's extract button (DungeonPanel.OnExtractClicked).
    public enum ExitAction { None, Extract, Abandon }

    public const string ExitHelp = "Extract (after a victory): your crew heads back to your ship with what it collected. Abandon: your crew retreats and the operation ends. Same as the extract button on the game's operation panel; Abandon asks first.";

    private sealed class OpRow
    {
        public DungeonOperation op;
        public TMP_Text summary;
        public Kit.Choice stealth, ammo;
        public Button autoMove, buyOut;
        public Button exit;
        public TMP_Text exitLabel;
        public ExitAction shown;
    }

    // None once the crew is already leaving or the operation is over. In the extraction phase the game's Abandon would
    // complete the extraction, so the button hides there too.
    public static ExitAction ExitFor(DungeonOperation op)
    {
        if (op.isComplete || op._awaitingPodReturn || op.phase == MissionPhase.Extraction || op.phase == MissionPhase.Complete) return ExitAction.None;
        var sim = op.simulation;
        if (sim == null) return ExitAction.Abandon;
        if (sim.isRetreating || sim.awaitingPlayerExtraction) return ExitAction.None;
        if (sim.canRequestExtraction) return ExitAction.Extract;
        return sim.isComplete ? ExitAction.None : ExitAction.Abandon;
    }

    public static string ExitText(ExitAction a) => a == ExitAction.Extract ? "Extract" : "Abandon";

    private void OnExit(DungeonOperation op)
    {
        switch (ExitFor(op))
        {
            case ExitAction.Extract:
                op.RequestExtraction();
                Refresh();
                break;
            case ExitAction.Abandon:
                string name = MapPointOfInterest.GetDungeonPoiName(op.location.dungeonType, op.location.faction);
                AlertPopup.ShowQuery($"Abandon the {name} operation? Crew already aboard retreat to your ship.", "Abandon", "Keep going", op.Abandon);
                break;
        }
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
            r.exit = Kit.TextButton(line1, "Abandon", () => OnExit(op), 80f);
            r.exitLabel = r.exit.GetComponentInChildren<TMP_Text>();
            Kit.Tooltip(r.exit.targetGraphic, "Extract or abandon", ExitHelp);
            r.shown = ExitAction.Abandon;
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

            var exit = ExitFor(op);
            if (exit != r.shown)
            {
                r.shown = exit;
                r.exit.gameObject.SetActive(exit != ExitAction.None);
                if (exit != ExitAction.None) r.exitLabel.text = ExitText(exit);
            }

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
