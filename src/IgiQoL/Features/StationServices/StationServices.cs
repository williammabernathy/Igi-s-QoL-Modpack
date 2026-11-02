using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.UI;
using Behaviour.UI.NotificationAlert;
using Behaviour.UI.Spacestation;
using Behaviour.Util;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Rules;
using Source.Player;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.StationServices;

public sealed class StationServices : Feature
{
    public static StationServices Instance;

    public StationServices() { Instance = this; }

    public override string Id => "StationServices";
    public override string Title => "Station services";
    public override string Description => "Repair, restock, sell, replace lost crew and unload brigs for your whole fleet in one click from the station's airlock screen (the buttons show up when there is something to do), or by themselves every time you dock. Repair is the hangar's paid repair, restock tops up ammo to ECHO's \"ammo minutes\" target from the armory and then the shop (plus warp fuel cells for the ship you fly, if you set a number), sell applies the Sell and Sell above N dock actions of the Item rules tab, crew replaces the crew each ship lost to hull hits (reserve, then willing prisoners, then hired here), and brigs moves your ships' prisoners to the station brig.";
    public override Type[] PatchClasses => new[] { typeof(StationServicesPatches), typeof(CrewLossPatches) };

    public ConfigEntry<bool> RepairOnDock, RestockOnDock, SellOnDock, CrewOnDock, BrigOnDock, AirlockButtons;
    public ConfigEntry<int> FuelCells;

    // The last on-dock run, for the self-check.
    internal static string lastDockSummary;
    private static bool dockRunPending;
    // The fleet auto-deposit of a dock that sells is held back (depositPending) and runs right after the sell (depositing).
    private static bool depositPending, depositing;
    // AutoDepositOnDock can run several times in one station visit (docking callbacks; a harness run showed the same
    // "station brig full" notice 10 times in 10 s), so each problem notice is shown once per visit.
    private static SpaceStationInterior problemsShownFor;
    private static readonly HashSet<string> problemsShown = new();

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        RepairOnDock = cfg.Bind(Id, "RepairOnDock", false, SettingInfo.Describe("Pay for repairs when you dock",
            "Starts the hangar's paid repair for every damaged ship in your fleet as soon as you dock, also while ECHO flies. Paid repair takes about 20 seconds; the free repair the station does by itself takes minutes, and ECHO waits for it. Ships the hangar is already repairing for money are skipped."));
        RestockOnDock = cfg.Bind(Id, "RestockOnDock", false, SettingInfo.Describe("Restock ammo when you dock",
            "Tops up the ammo of every ship in your fleet when you dock, to the \"ammo minutes\" target in ECHO's Autopilot tab: from your armory first, then bought from the station's shops (credits only). Vanilla ECHO only restocks the ship you fly."));
        SellOnDock = cfg.Bind(Id, "SellOnDock", false, SettingInfo.Describe("Sell by item rules when you dock",
            "When you dock yourself, sells cargo whose Item rules dock action is Sell, and for Sell above N stores up to N and sells the rest. It sells before the game's fleet auto-deposit, so the deposit gets the room the sale frees in station storage and the armory. With ECHO on, the ECHO cargo rules feature does this instead. Favourites, mission items and the ammo a ship uses are never sold."));
        FuelCells = cfg.Bind(Id, "FuelCells", 0, SettingInfo.Describe("Warp fuel cells to keep aboard",
            "Restock (the airlock button, and on dock with \"Restock ammo when you dock\") also tops up the ship you fly to this many warp fuel cells: from your armory first, then bought, fastest cells first (Hyper, then Ion, then Plasma). Only the ship you fly burns warp fuel. A part-used cell counts as one, as it does for ECHO. 0 turns this off. ECHO by itself keeps 2 cells, and only with the Prompt Engineering warp fuel skill.",
            new AcceptableValueRange<int>(0, 50)));
        CrewOnDock = cfg.Bind(Id, "CrewOnDock", false, SettingInfo.Describe("Replace lost crew when you dock",
            "Every hull hit of 2% or more can kill one crew member, on every ship of yours. The mod counts those deaths per ship and crew type, and when you dock (also while ECHO flies) puts the same crew back: from your crew reserve first, then willing prisoners of that type from the ship's brig and the station brig (recruited at 40% of the hire price, as the game does), then hired at the station's personnel center at its price, as far as its stock goes. Crew added to a ship any other way counts as a replacement; crew coming back from an operation doesn't. Vanilla ECHO only hires crew for the ship you fly, by that ship's crew ratios, so fleet ships never get their losses back."));
        BrigOnDock = cfg.Bind(Id, "BrigOnDock", false, SettingInfo.Describe("Unload brigs when you dock",
            "Moves the prisoners in your ships' brigs to the station brig when you dock (also while ECHO flies), so the captives of the next boarding fit aboard instead of being left behind with \"Brig full\". Prisoners willing to join stay willing and can still be recruited, by you or by Replace crew. The station brig grows with your level; whatever doesn't fit stays aboard. With the Crew Slavery skill, prisoners aboard add to the ship's bonuses, so leave this off if you rely on that."));
        AirlockButtons = cfg.Bind(Id, "AirlockButtons", true, SettingInfo.Describe("Buttons on the airlock screen",
            "Shows Repair fleet, Restock ammo, Sell by rules, Replace crew and Unload brigs buttons next to the airlock's own buttons while there is something for them to do. Hover a button to see exactly what it will do and what it costs."));
    }

    public static string Summary(ServiceReport repair, ServiceReport restock, ServiceReport sell, ServiceReport crew = null, ServiceReport brig = null)
    {
        var parts = new List<string>();
        if (repair != null && repair.count > 0) parts.Add($"repairing {repair.count} ship{(repair.count == 1 ? "" : "s")} ({Services.Money(repair.credits)})");
        int rounds = restock != null ? restock.count - restock.cells : 0;
        if (rounds > 0 || restock != null && restock.cells > 0)
        {
            var loaded = new List<string>();
            if (rounds > 0) loaded.Add($"{rounds} rounds");
            if (restock.cells > 0) loaded.Add($"{restock.cells} warp fuel cell{(restock.cells == 1 ? "" : "s")}");
            parts.Add("loaded " + string.Join(" and ", loaded) + (restock.credits > 0 ? $" ({Services.Money(restock.credits)})" : ""));
        }
        if (sell != null && sell.count > 0) parts.Add($"sold {sell.count} stack{(sell.count == 1 ? "" : "s")} (+{Services.Money(sell.credits)})");
        if (crew != null && crew.count > 0) parts.Add($"replaced {crew.count} crew" + (crew.credits > 0 ? $" ({Services.Money(crew.credits)})" : ""));
        if (brig != null && brig.count > 0) parts.Add($"moved {brig.count} prisoner{(brig.count == 1 ? "" : "s")} to the station brig");
        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    public static void Notify(string text, params ServiceReport[] reports)
    {
        if (text != null)
            Singleton<NotificationManager>.Instance.CreateNotification(text).WithColor(ColorHelper.greenish).Show();
        foreach (var r in reports)
            if (r != null && r.problems.Count > 0)
                Singleton<NotificationManager>.Instance.CreateNotification(r.problems[0]).WithColor(ColorHelper.orange75).Show();
    }

    public override void OnActivated()
    {
        GameEvents.SaveLoaded += () =>
        {
            if (!Active) return;
            try { CrewLosses.Prune(); CrewLossPatches.ResetReturning(); problemsShownFor = null; problemsShown.Clear(); }
            catch (Exception e) { Fail(e, nameof(CrewLosses)); }
        };
    }

    private static bool EchoSells() => GamePlayer.current.autoPlay;

    // The fleet auto-deposit runs at docking, before the interior the sell needs exists. Selling from station storage or
    // the armory frees room the deposit could use, so a dock that sells holds the deposit back and runs it after the sale.
    internal bool DeferDeposit()
    {
        if (depositing || !SellOnDock.Value || EchoSells() || !ItemRules.HasDockRules) return false;
        depositPending = true;
        return true;
    }

    private static void DepositNow()
    {
        depositing = true;
        try { PlayerCargo.AutoDepositOnDock(); }
        finally { depositing = false; }
    }

    internal void OnDocked()
    {
        if (depositing) return;
        if (!RepairOnDock.Value && !RestockOnDock.Value && !SellOnDock.Value && !CrewOnDock.Value && !BrigOnDock.Value) return;
        if (dockRunPending) return;
        dockRunPending = true;
        Plugin.Instance.StartCoroutine(RunWhenInside());
    }

    // EnterSpacestation loads the interior scene asynchronously; the hangar's repair jobs need it.
    private IEnumerator RunWhenInside()
    {
        var player = GamePlayer.current;
        float until = Time.realtimeSinceStartup + 15f;
        while (SpaceStationInterior.instance == null && Time.realtimeSinceStartup < until) yield return null;
        yield return null;
        dockRunPending = false;
        bool deposit = depositPending;
        depositPending = false;
        // A save loaded during the wait is a different game; the run belongs to the one that docked.
        if (GamePlayer.current != player) yield break;
        if (!Active || SpaceStationInterior.instance == null)
        {
            // Nothing sells without the interior, but the held-back deposit still runs.
            if (deposit) DepositNow();
            yield break;
        }
        try
        {
            // Sell first so the credits are there for repairs and ammo, and the fleet deposit gets the room the sale frees.
            ServiceReport sell;
            try { sell = SellOnDock.Value && !EchoSells() ? Services.Sell(run: true) : null; }
            finally { if (deposit) DepositNow(); }
            var repair = RepairOnDock.Value ? Services.Repair(run: true) : null;
            var restock = RestockOnDock.Value ? Services.Restock(run: true) : null;
            var crew = CrewOnDock.Value ? Services.Crew(run: true) : null;
            var brig = BrigOnDock.Value ? Services.Brig(run: true) : null;
            var iim = InventoryInteractionManager.Instance;
            if (iim != null && (deposit || sell != null && sell.count > 0 || restock != null && restock.count > 0)) iim.ReloadUI();
            string summary = Summary(repair, restock, sell, crew, brig);
            lastDockSummary = summary ?? "";
            if (summary != null) Plugin.Log.LogInfo("[StationServices] on dock: " + summary);
            if (summary != null) Notify("Docked: " + summary);
            if (problemsShownFor != SpaceStationInterior.instance)
            {
                problemsShownFor = SpaceStationInterior.instance;
                problemsShown.Clear();
            }
            foreach (var r in new[] { repair, restock, sell, crew, brig })
                if (r != null && r.problems.Count > 0 && problemsShown.Add(r.problems[0])) Notify(null, r);
        }
        catch (Exception e) { Fail(e, nameof(RunWhenInside)); }
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => StationServicesCheck.Run(ctx);
}

[HarmonyPatch]
internal static class StationServicesPatches
{
    private static StationServices F => StationServices.Instance;

    // Runs before EchoCargo's prefix, which deposits by item rules: returning false skips that prefix and the game's deposit.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(PlayerCargo), nameof(PlayerCargo.AutoDepositOnDock))]
    private static bool AutoDepositOnDockPrefix()
    {
        if (!F.Active) return true;
        try { return !F.DeferDeposit(); }
        catch (Exception e) { F.Fail(e, nameof(StationServicesPatches)); return true; }
    }

    // Runs right after the game docks your ship (EnterSpacestation), for you and for ECHO; loading a save inside a station doesn't call it.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerCargo), nameof(PlayerCargo.AutoDepositOnDock))]
    private static void AutoDepositOnDockPostfix()
    {
        if (!F.Active) return;
        try { F.OnDocked(); }
        catch (Exception e) { F.Fail(e, nameof(StationServicesPatches)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Airlock), "Start")]
    private static void AirlockStartPostfix(Airlock __instance)
    {
        if (!F.Active || !F.AirlockButtons.Value) return;
        try { AirlockServices.Attach(__instance); }
        catch (Exception e) { F.Fail(e, nameof(StationServicesPatches)); }
    }
}
