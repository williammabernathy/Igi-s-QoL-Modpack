using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Dungeon;
using Behaviour.Gameplay;
using Behaviour.Item;
using Behaviour.Util;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using Source.Data.Persistable;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.Galaxy.POI.Station;
using Source.Item;
using Source.Player;
using Source.Personnel;
using Source.SpaceShip;
using Behaviour.Managers;
using Behaviour.UI.Side_Menu;
using Behaviour.UI.Spacestation;
using UnityEngine;
using CrewType = Behaviour.Crew.CrewType;

namespace IgiQoL.Features.EchoGates;

public sealed class EchoGates : Feature
{
    public static EchoGates Instance;

    public EchoGates() { Instance = this; }

    public override string Id => "EchoGates";
    public override string Title => "ECHO decisions";
    public override string Description => "Changes when ECHO (the autopilot) picks up loot, raids, starts operations, boards ships and hires crew, where its crew comes from, and how fast it unloads cargo and gets through a station visit. The starting values match the unmodded game, except that ECHO no longer pays for crew that can't fit aboard.";
    public override string SettingsTab => "ECHO";
    public override Type[] PatchClasses => new[] { typeof(EchoGatePatches) };

    public override void OnActivated()
    {
        GameEvents.SaveLoaded += EchoGatePatches.ResetQuickSteps;
        EchoPriority.EchoPriority.EnsureTab();
    }

    public const int VanillaRaidThreshold = -500;
    public const int VanillaDungeonCrewMinimum = 3;
    public const float VanillaBoardingCrewFraction = 0.5f;
    public const float VanillaHireCrewBelowFraction = 0.5f;

    public ConfigEntry<bool> LootBeforeLeaving;
    public ConfigEntry<int> RaidReputationThreshold;
    public ConfigEntry<int> DungeonCrewMinimum;
    public ConfigEntry<float> BoardingCrewFraction;
    public ConfigEntry<float> HireCrewBelowFraction;
    public ConfigEntry<bool> FastUnloading;
    public static bool FastUnloadOn => Instance != null && Instance.Active && Instance.FastUnloading.Value;
    public ConfigEntry<bool> QuickStationSteps;
    // The game's own floor for ECHO's decision time (IdleManager.SetActivityDelay).
    public const float QuickStepSeconds = 2f;
    public ConfigEntry<bool> RecruitPrisoners;
    public ConfigEntry<bool> HireOnlyWhatFits;
    public ConfigEntry<bool> RebalanceCrew;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        LootBeforeLeaving = cfg.Bind(Id, "LootBeforeLeaving", true, SettingInfo.Describe("Pick up loot before leaving",
            "ECHO collects the loot floating nearby before it flies on. Turn off to leave sooner and skip the loot. Game default: on."));
        RaidReputationThreshold = cfg.Bind(Id, "RaidReputationThreshold", VanillaRaidThreshold, SettingInfo.Describe("Raid factions at reputation or below",
            "Raiding a faction's installations costs reputation with it, so ECHO only raids factions you're already at or below this reputation with. Game default: -500. A higher number (for example 0) lets ECHO raid factions you get on better with, and you lose that reputation. The Umbral transponder skips this check."));
        DungeonCrewMinimum = cfg.Bind(Id, "DungeonCrewMinimum", VanillaDungeonCrewMinimum, SettingInfo.Describe("Crew needed to start an operation",
            "ECHO only starts an operation when at least this many of your crew can take part. Pilots don't count. Game default: 3."));
        BoardingCrewFraction = cfg.Bind(Id, "BoardingCrewFraction", VanillaBoardingCrewFraction, SettingInfo.Describe("Crew needed to board a ship (share of max)",
            "For operations aboard ships, ECHO also needs crew equal to at least this share of your flagship's maximum crew. 0.5 means half, 0.25 a quarter. Game default: 0.5."));
        HireCrewBelowFraction = cfg.Bind(Id, "HireCrewBelowFraction", VanillaHireCrewBelowFraction, SettingInfo.Describe("Hire crew when below (share of max)",
            "ECHO flies off to hire crew when your flagship's crew falls below this share of its maximum. 0.5 means half; 0 stops these hiring trips. Game default: 0.5."));
        FastUnloading = cfg.Bind(Id, "FastUnloading", false, SettingInfo.Describe("Fast cargo unloading",
            "When ECHO unloads at a station, it moves a whole stack at a time (as much as storage has room for) instead of a few items every few seconds. A full hold empties in a few steps instead of minutes. Game default: off."));
        QuickStationSteps = cfg.Bind(Id, "QuickStationSteps", false, SettingInfo.Describe("Quick station steps",
            "At a friendly station, ECHO takes its next step 2 seconds after it docks, turns in a mission, hires crew, restocks or takes a mission, instead of waiting out its whole decision time (12 s, less with the Prompt Engineering skills). A visit that turns in a mission and takes the next one is about half a minute shorter. While it waits for repairs, its next step comes 2 seconds after the flagship's hull and armour are full instead of at its next regular check. Waiting for a mission to come up keeps the normal time, so ECHO doesn't make more decisions than it would anyway. Game default: off."));
        RecruitPrisoners = cfg.Bind(Id, "RecruitPrisoners", false, SettingInfo.Describe("Recruit willing prisoners before hiring",
            "When ECHO tops up your flagship's crew at a station, after the crew reserve it recruits prisoners who are willing to join (the flagship's brig, your fleet ships' brigs, then the station brig) before hiring. A recruit costs 40% of the crew type's price, the same as on the brig screens, and frees a brig place for the next boarding's captives. ECHO only hires at the personnel center for what's still missing. Only when ECHO is allowed to buy crew (Autopilot tab, crew settings). Game default: off (crew reserve, then hiring)."));
        HireOnlyWhatFits = cfg.Bind(Id, "HireOnlyWhatFits", true, SettingInfo.Describe("Hire only crew that fits aboard",
            "ECHO tops up each crew type to its share in the Autopilot tab's crew settings. When other types already fill the ship, the game still takes that type's whole shortfall from the crew reserve and pays the personnel center for all of it, but only the crew that fit come aboard: the other reserve crew are lost and the credits are spent for nothing. With this on, ECHO takes and pays for only as many as fit. The unmodded game behaves as if this were off."));
        RebalanceCrew = cfg.Bind(Id, "RebalanceCrew", false, SettingInfo.Describe("Make room for short crew types",
            "ECHO tops up each crew type to its share in the Autopilot tab's crew settings, but it never moves crew out, so when other types already fill the ship a short type stays short (for example too few Marines for boardings). With this on, at a station ECHO first moves crew above their share to the crew reserve, as many as it can replace right away from the reserve, willing prisoners (with the option above) or the personnel center. Types with no share set are never moved. Game default: off."));
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        CheckRaidThreshold(ctx);
        CheckFastUnload(ctx);
        CheckHireThreshold(ctx);
        CheckDungeonMinimum(ctx);
        CheckQuickSteps(ctx);
        CheckRepairWait(ctx);
        CheckRecruitPrisoners(ctx);
        CheckHireFits(ctx);
        CheckRebalance(ctx);
        yield break;
    }

    private void CheckRaidThreshold(SelfCheckContext ctx)
    {
        Faction faction = null;
        foreach (var f in Faction.all)
            if (f != Faction.player) { faction = f; break; }
        if (faction == null)
        {
            ctx.Skip("raid-threshold", "no faction");
            return;
        }
        int saved = RaidReputationThreshold.Value;
        try
        {
            // Pick a threshold that flips vanilla's answer for this faction, so the check fails if the patch is not applied.
            int rep = faction.GetReputation(Faction.player);
            int threshold = rep > VanillaRaidThreshold ? rep : rep - 1;
            RaidReputationThreshold.Value = threshold;
            bool expected = !GamePlayer.current.hasUmbralTransponder && rep > threshold;
            bool actual = IdleManager.WouldIncurFactionPenalty(faction);
            string detail = $"{faction.identifier} rep={rep} threshold={threshold} penalty={actual} (vanilla {!GamePlayer.current.hasUmbralTransponder && rep > VanillaRaidThreshold})";
            if (actual == expected) ctx.Pass("raid-threshold", detail);
            else ctx.Fail("raid-threshold", detail + $" expected {expected}");
        }
        finally { RaidReputationThreshold.Value = saved; }
    }

    // The patched DropFoundItem, called as ECHO's unload does: one call moves the whole stack (as much as the station's
    // material storage takes) from the flagship, not one chunk.
    private void CheckFastUnload(SelfCheckContext ctx)
    {
        var ss = SpaceStation.current;
        var player = GamePlayer.current;
        var ship = player?.currentSpaceShip;
        if (ss == null || SpaceStationInterior.instance == null || !Singleton<IdleManager>.HasInstance || ship == null)
        {
            ctx.Skip("fast-unload", "not docked");
            return;
        }
        InventoryItemType ore = null;
        foreach (var t in InventoryItemType.all)
            if (t.itemCategory == ItemCategory.Ore && Cargo.DepositTarget(ss, t) == ss.materialStorage && ship.cargo.GetCount(t) == 0) { ore = t; break; }
        if (ore == null)
        {
            ctx.Skip("fast-unload", "no ore type that goes to material storage and isn't aboard already");
            return;
        }
        var idle = Singleton<IdleManager>.Instance;
        var storage = ss.materialStorage;
        int normalChunk = 1 + Mathf.FloorToInt(ship.cargoCapacity / 200f);   // the game's own step for ore
        bool saved = FastUnloading.Value, savedFull = idle.inventoryFull;
        float timer = idle.updateTimer;
        int storage0 = storage.GetCount(ore);
        try
        {
            FastUnloading.Value = true;
            ship.cargo.Add(ore, 40);
            int aboard = ship.cargo.GetCount(ore);
            int room = ore.m3 > 0f ? Mathf.FloorToInt(storage.GetSpaceAvailable() / ore.m3) : aboard;
            int expected = Math.Min(aboard, room);
            if (expected <= normalChunk)
            {
                ctx.Skip("fast-unload", $"{ore.identifier}: {aboard} aboard, room for {room} in storage, the game's chunk is {normalChunk}; too little to tell a stack from a chunk");
                return;
            }
            idle.DropFoundItem(ss, ship, ore);
            int moved = aboard - ship.cargo.GetCount(ore);
            string detail = $"{ore.identifier} m3={ore.m3}: {aboard} aboard, storage room {room}, one call moved {moved} (the game's chunk is {normalChunk})";
            if (moved == expected) ctx.Pass("fast-unload", detail);
            else ctx.Fail("fast-unload", detail + $" expected {expected}");
        }
        finally
        {
            FastUnloading.Value = saved;
            idle.inventoryFull = savedFull;
            idle.updateTimer = timer;
            int left = ship.cargo.GetCount(ore);
            if (left > 0) ship.cargo.Remove(ore, left);
            int extra = storage.GetCount(ore) - storage0;
            if (extra > 0) storage.Remove(ore, extra);
        }
    }

    // Vanilla already answers "not below" for a flagship with half its crew, so the check sets up a flagship at 40% crew
    // whose reserve covers the shortfall (vanilla answers yes) and asks with the fraction at 0, 0.25 and 0.5.
    private void CheckHireThreshold(SelfCheckContext ctx)
    {
        var player = GamePlayer.current;
        var ship = player?.currentSpaceShip;
        int max = ship != null ? ship.GetMaxGrunts() : 0;
        if (!Singleton<IdleManager>.HasInstance || max < 10)
        {
            ctx.Skip("hire-threshold", "no IdleManager instance, or no flagship with 10 or more crew places");
            return;
        }
        float saved = HireCrewBelowFraction.Value;
        var savedCrew = new Dictionary<string, int>(ship.crewData.crew);
        var savedReserve = new Dictionary<string, int>(player.unassignedCrew.crew);
        var savedSettings = ship.crewAutopilotSettings;
        try
        {
            var settings = new CrewAutopilotSettings { allowPurchase = false };
            settings.SetMinimumRatio("Deckhand", 1f);
            ship.crewAutopilotSettings = settings;
            ship.crewData.crew.Clear();
            player.unassignedCrew.crew.Clear();
            ship.crewData.crew["Deckhand"] = Mathf.RoundToInt(max * 0.4f);
            player.unassignedCrew.crew["Deckhand"] = max;
            var idle = Singleton<IdleManager>.Instance;
            HireCrewBelowFraction.Value = VanillaHireCrewBelowFraction;
            bool vanilla = idle.IsCrewBelowThreshold();
            HireCrewBelowFraction.Value = 0f;
            bool atZero = idle.IsCrewBelowThreshold();
            HireCrewBelowFraction.Value = 0.25f;
            bool atQuarter = idle.IsCrewBelowThreshold();
            string detail = $"{ship.crewData.totalCrew}/{max} crew: fraction 0.5 -> {vanilla}, 0.25 -> {atQuarter}, 0 -> {atZero}";
            if (!vanilla) ctx.Skip("hire-threshold", "the game doesn't see the test crew as short (" + detail + ")");
            else if (!atZero && !atQuarter) ctx.Pass("hire-threshold", detail);
            else ctx.Fail("hire-threshold", detail);
        }
        finally
        {
            HireCrewBelowFraction.Value = saved;
            ship.crewAutopilotSettings = savedSettings;
            ship.crewData.crew.Clear();
            foreach (var kv in savedCrew) ship.crewData.crew[kv.Key] = kv.Value;
            player.unassignedCrew.crew.Clear();
            foreach (var kv in savedReserve) player.unassignedCrew.crew[kv.Key] = kv.Value;
        }
    }

    // A docked decision that changes something (here: one crew member added and taken back) shortens ECHO's next wait
    // to QuickStepSeconds; a decision that changes nothing keeps the game's time.
    private void CheckQuickSteps(SelfCheckContext ctx)
    {
        var ship = GamePlayer.current.currentSpaceShip;
        if (!Singleton<IdleManager>.HasInstance || SpaceStationInterior.instance == null || ship == null)
        {
            ctx.Skip("quick-steps", "not docked");
            return;
        }
        var idle = Singleton<IdleManager>.Instance;
        string crewType = null;
        foreach (var t in Behaviour.Crew.CrewType.all)
            if (t.availableForPlayer) { crewType = t.identifier; break; }
        bool saved = QuickStationSteps.Value;
        float timer = idle.updateTimer;
        try
        {
            QuickStationSteps.Value = true;
            idle.updateTimer = 12f;
            EchoGatePatches.FindActivityPrefix();
            EchoGatePatches.FindActivityPostfix(idle);
            float idleWait = idle.updateTimer;
            EchoGatePatches.FindActivityPrefix();
            ship.crewData.AddCrew(crewType, 1);
            EchoGatePatches.FindActivityPostfix(idle);
            ship.crewData.RemoveCrew(crewType, 1);
            float stepWait = idle.updateTimer;
            QuickStationSteps.Value = false;
            idle.updateTimer = 12f;
            EchoGatePatches.FindActivityPrefix();
            ship.crewData.AddCrew(crewType, 1);
            EchoGatePatches.FindActivityPostfix(idle);
            ship.crewData.RemoveCrew(crewType, 1);
            float offWait = idle.updateTimer;
            string detail = $"no change -> {idleWait:0.#} s, crew added -> {stepWait:0.#} s, option off -> {offWait:0.#} s";
            if (idleWait == 12f && stepWait == QuickStepSeconds && offWait == 12f) ctx.Pass("quick-steps", detail);
            else ctx.Fail("quick-steps", detail);
        }
        finally
        {
            QuickStationSteps.Value = saved;
            idle.updateTimer = timer;
        }
    }

    // A docked decision with the flagship at 90% hull leaves ECHO waiting for repairs: the game's timer stays while the
    // hull is short, and drops to QuickStepSeconds on the first frame it's full again.
    private void CheckRepairWait(SelfCheckContext ctx)
    {
        var unit = IdleManager.ship;
        if (!Singleton<IdleManager>.HasInstance || SpaceStationInterior.instance == null || unit == null)
        {
            ctx.Skip("repair-wait", "not docked");
            return;
        }
        var idle = Singleton<IdleManager>.Instance;
        var player = GamePlayer.current;
        bool saved = QuickStationSteps.Value, savedAuto = player.autoPlay;
        float timer = idle.updateTimer, hull = unit.currentHullHP, armour = unit.currentArmorHP;
        try
        {
            QuickStationSteps.Value = true;
            player.autoPlay = true;
            unit.currentArmorHP = unit.maxArmorHP;
            unit.currentHullHP = unit.maxHullHP * 0.9f;
            idle.updateTimer = 12f;
            EchoGatePatches.FindActivityPrefix();
            EchoGatePatches.FindActivityPostfix(idle);
            EchoGatePatches.IdleUpdatePostfix(idle);
            float damagedWait = idle.updateTimer;
            unit.currentHullHP = unit.maxHullHP;
            EchoGatePatches.IdleUpdatePostfix(idle);
            float repairedWait = idle.updateTimer;
            idle.updateTimer = 12f;
            EchoGatePatches.IdleUpdatePostfix(idle);
            float laterWait = idle.updateTimer;
            string detail = $"hull 90% -> {damagedWait:0.#} s, repaired -> {repairedWait:0.#} s, next frame (timer reset to 12) -> {laterWait:0.#} s";
            if (damagedWait == 12f && repairedWait == QuickStepSeconds && laterWait == 12f) ctx.Pass("repair-wait", detail);
            else ctx.Fail("repair-wait", detail);
        }
        finally
        {
            QuickStationSteps.Value = saved;
            player.autoPlay = savedAuto;
            unit.currentHullHP = hull;
            unit.currentArmorHP = armour;
            idle.updateTimer = timer;
        }
    }

    // The brigs ECHO recruits from, in order: the ship's own, your other active ships', the station brig.
    private static List<PrisonerData> Brigs(SpaceShipData ship)
    {
        var player = GamePlayer.current;
        var list = new List<PrisonerData>();
        if (ship.prisonerData != null) list.Add(ship.prisonerData);
        foreach (var s in player.activeFleet)
            if (s != null && s != ship && s.prisonerData != null) list.Add(s.prisonerData);
        list.Add(player.stationPrisoners);
        return list;
    }

    // Recruits up to `amount` willing prisoners of `type` into `ship` from Brigs(ship), at 40% of the crew type's cost
    // (SpaceShipData.RecruitWillingByType and GamePlayer.RecruitWillingToReserve charge the same). Capped by the
    // ship's free crew space and your credits.
    public static int RecruitWilling(string type, int amount, SpaceShipData ship)
    {
        var player = GamePlayer.current;
        if (player == null || ship == null || amount <= 0 || !CrewType.TryGet(type, out var crewType)) return 0;
        int want = Math.Min(amount, ship.GetMaxGrunts() - ship.crewData.totalCrew);
        int cost = Mathf.RoundToInt(crewType.cost * 0.4f);
        int total = 0;
        long spent = 0;
        foreach (var brig in Brigs(ship))
        {
            if (total >= want) break;
            int n = Math.Min(want - total, brig.GetWilling(type));
            if (cost > 0) n = (int)Math.Min(n, player.credits / cost);
            if (n <= 0) continue;
            n = brig.RecruitWilling(type, n);
            player.RemoveCreditsForCrew((long)n * cost, type);
            ship.AddCrew(type, n);
            total += n;
            spent += (long)n * cost;
        }
        if (total > 0)
            Plugin.Log.LogInfo($"[EchoGates] recruited {total} willing {type} prisoners into {ship.GetShipName()} for {spent} cr");
        return total;
    }

    // Moves crew above their share (CrewAutopilotSettings ratios, scaled as IdleManager.GetCrewRatioScale does) to the
    // crew reserve, so the top-up that follows has places for the short types. Moves no more than the short types can
    // get right away (reserve; willing prisoners and the personnel center when ECHO may buy crew, within your credits)
    // minus the places already free, and no more than the reserve holds. Types with no share are never moved.
    public static int Rebalance(SpaceShipData ship, SpaceStation ss)
    {
        var player = GamePlayer.current;
        int max = ship?.GetMaxGrunts() ?? 0;
        if (max <= 0) return 0;
        var settings = ship.crewAutopilotSettings;
        float scale = IdleManager.GetCrewRatioScale(settings);
        bool buy = settings.allowPurchase;
        bool recruit = buy && Instance.RecruitPrisoners.Value;
        List<PrisonerData> brigs = recruit ? Brigs(ship) : null;
        long budget = player.credits;
        int fillable = 0;
        foreach (var t in CrewType.all)
        {
            int shortfall = IdleManager.GetCrewShortfall(t, max, scale, settings, ship);
            if (shortfall <= 0) continue;
            int can = Math.Min(shortfall, player.unassignedCrew.GetCrewCount(t.identifier));
            if (recruit && can < shortfall)
            {
                int cost = Mathf.RoundToInt(t.cost * 0.4f);
                int willing = 0;
                foreach (var b in brigs) willing += b.GetWilling(t.identifier);
                int n = Math.Min(shortfall - can, willing);
                if (cost > 0) n = (int)Math.Min(n, budget / cost);
                can += n;
                budget -= (long)n * cost;
            }
            if (buy && can < shortfall && ss?.personnelCenter != null)
            {
                int price = PersonnelCenter.GetBuyPrice(t, ss.level);
                int n = shortfall - can;
                if (price > 0) n = (int)Math.Min(n, budget / price);
                can += n;
                budget -= (long)n * price;
            }
            fillable += can;
        }
        int need = Math.Min(fillable - (max - ship.crewData.totalCrew), player.maxReserve - player.unassignedCrew.totalCrew);
        if (need <= 0) return 0;
        int moved = 0;
        var parts = new List<string>();
        while (need > 0)
        {
            CrewType most = null;
            int surplus = 0;
            foreach (var t in CrewType.all)
            {
                float ratio = settings.GetMinimumRatio(t.identifier);
                if (!t.availableForPlayer || ratio <= 0f) continue;
                int over = ship.crewData.GetCrewCount(t.identifier) - Mathf.RoundToInt(max * ratio * scale);
                if (over > surplus) { most = t; surplus = over; }
            }
            if (most == null) break;
            int n = Math.Min(need, surplus);
            ship.RemoveCrew(most.identifier, n);
            player.unassignedCrew.AddCrew(most.identifier, n);
            need -= n;
            moved += n;
            parts.Add(n + " " + most.identifier);
        }
        if (moved > 0)
            Plugin.Log.LogInfo($"[EchoGates] moved {string.Join(", ", parts)} from {ship.GetShipName()} to the crew reserve to make room for short crew types");
        return moved;
    }

    // Flagship full: Gunner and Deckhand above their share, Marine short. Rebalance moves out what the reserve can
    // replace (Marines put there for the test) minus the free places, takes the biggest surplus first, and nothing
    // with the ratios unset (the game's default: Deckhands only).
    private void CheckRebalance(SelfCheckContext ctx)
    {
        var player = GamePlayer.current;
        var ship = player?.currentSpaceShip;
        int max = ship?.GetMaxGrunts() ?? 0;
        if (max < 20)
        {
            ctx.Skip("rebalance", "no flagship with 20 or more crew places");
            return;
        }
        var savedCrew = new Dictionary<string, int>(ship.crewData.crew);
        var savedReserve = new Dictionary<string, int>(player.unassignedCrew.crew);
        var savedSettings = ship.crewAutopilotSettings;
        bool savedRecruit = RecruitPrisoners.Value;
        try
        {
            RecruitPrisoners.Value = false;
            var settings = new CrewAutopilotSettings { allowPurchase = false };
            settings.SetMinimumRatio("Deckhand", 0.2f);
            settings.SetMinimumRatio("Gunner", 0.3f);
            settings.SetMinimumRatio("Marine", 0.5f);
            ship.crewAutopilotSettings = settings;
            ship.crewData.crew.Clear();
            player.unassignedCrew.crew.Clear();
            int marines = Mathf.RoundToInt(max * 0.5f) - 10;   // 10 short
            int deckhands = Mathf.RoundToInt(max * 0.2f) + 2;  // 2 over
            ship.crewData.crew["Marine"] = marines;
            ship.crewData.crew["Deckhand"] = deckhands;
            ship.crewData.crew["Gunner"] = max - marines - deckhands - 1;   // 7 over (with rounding about), 1 place free
            int gunners = ship.crewData.crew["Gunner"];
            player.unassignedCrew.crew["Marine"] = 6;
            int moved = Rebalance(ship, null);
            int gunnersOut = gunners - ship.crewData.GetCrewCount("Gunner");
            int deckhandsOut = deckhands - ship.crewData.GetCrewCount("Deckhand");
            int reserveGunners = player.unassignedCrew.GetCrewCount("Gunner");
            int again = Rebalance(ship, null);
            ship.crewAutopilotSettings = new CrewAutopilotSettings();
            int unset = Rebalance(ship, null);
            string detail = $"{ship.GetShipName()} max {max}: Marine {marines} (10 short), 6 in the reserve, 1 free -> moved {moved} "
                + $"(Gunner {gunnersOut}, Deckhand {deckhandsOut}, reserve Gunners {reserveGunners}); second call {again}; game's default ratios {unset}";
            if (moved == 5 && gunnersOut == 5 && deckhandsOut == 0 && reserveGunners == 5 && again == 0 && unset == 0) ctx.Pass("rebalance", detail);
            else ctx.Fail("rebalance", detail);
        }
        finally
        {
            RecruitPrisoners.Value = savedRecruit;
            ship.crewAutopilotSettings = savedSettings;
            ship.crewData.crew.Clear();
            foreach (var kv in savedCrew) ship.crewData.crew[kv.Key] = kv.Value;
            player.unassignedCrew.crew.Clear();
            foreach (var kv in savedReserve) player.unassignedCrew.crew[kv.Key] = kv.Value;
        }
    }

    // Willing prisoners in the flagship's brig (2) and the station brig (1) fill a shortfall of 3 through the patched
    // IdleManager.TransferFromReserve; nothing with the option off or when ECHO may not buy crew.
    private void CheckRecruitPrisoners(SelfCheckContext ctx)
    {
        var player = GamePlayer.current;
        var ship = player?.currentSpaceShip;
        if (ship?.prisonerData == null || ship.GetMaxGrunts() <= 0)
        {
            ctx.Skip("recruit-prisoners", "no flagship with a brig and crew");
            return;
        }
        string type = null;
        int most = 0;
        foreach (var kv in ship.crewData.crew)
            if (kv.Value > most && CrewType.TryGet(kv.Key, out var t) && t.availableForPlayer) { type = kv.Key; most = kv.Value; }
        if (type == null || most < 3)
        {
            ctx.Skip("recruit-prisoners", "the flagship has no crew type with 3 or more aboard");
            return;
        }
        var brigs = new List<PrisonerData> { ship.prisonerData, player.stationPrisoners };
        foreach (var s in player.activeFleet)
            if (s?.prisonerData != null && s != ship) brigs.Add(s.prisonerData);
        var savedPrisoners = new List<Dictionary<string, int>>();
        var savedWilling = new List<Dictionary<string, int>>();
        foreach (var b in brigs)
        {
            savedPrisoners.Add(new Dictionary<string, int>(b.prisoners));
            savedWilling.Add(new Dictionary<string, int>(b.willing));
        }
        bool savedOption = RecruitPrisoners.Value;
        bool savedPurchase = ship.crewAutopilotSettings.allowPurchase;
        int savedReserve = player.unassignedCrew.GetCrewCount(type);
        long savedCredits = player.credits;
        int cost = Mathf.RoundToInt(CrewType.Get(type).cost * 0.4f);
        try
        {
            player.unassignedCrew.RemoveCrew(type, savedReserve, false);
            foreach (var b in brigs) { b.prisoners.Remove(type); b.willing.Remove(type); }
            ship.RemoveCrew(type, 3);
            ship.prisonerData.AddPrisoners(type, 2);
            ship.prisonerData.AddWilling(type, 2);
            player.stationPrisoners.AddPrisoners(type, 1);
            player.stationPrisoners.AddWilling(type, 1);

            RecruitPrisoners.Value = false;
            int off = IdleManager.TransferFromReserve(type, 3, ship);
            RecruitPrisoners.Value = true;
            ship.crewAutopilotSettings.allowPurchase = false;
            int noPurchase = IdleManager.TransferFromReserve(type, 3, ship);
            ship.crewAutopilotSettings.allowPurchase = true;
            long before = player.credits;
            int on = IdleManager.TransferFromReserve(type, 3, ship);
            int crewNow = ship.crewData.GetCrewCount(type);
            string detail = $"{ship.GetShipName()} {type}: off {off}, no purchase {noPurchase}, on {on}, crew {crewNow} of {most}, "
                + $"brig willing {ship.prisonerData.GetWilling(type)} / station {player.stationPrisoners.GetWilling(type)}, paid {before - player.credits} (3 x {cost})";
            if (off == 0 && noPurchase == 0 && on == 3 && crewNow == most && ship.prisonerData.GetWilling(type) == 0
                && player.stationPrisoners.GetWilling(type) == 0 && before - player.credits == 3L * cost)
                ctx.Pass("recruit-prisoners", detail);
            else ctx.Fail("recruit-prisoners", detail);
        }
        finally
        {
            RecruitPrisoners.Value = savedOption;
            ship.crewAutopilotSettings.allowPurchase = savedPurchase;
            int diff = most - ship.crewData.GetCrewCount(type);
            if (diff > 0) ship.AddCrew(type, diff, false);
            else if (diff < 0) ship.RemoveCrew(type, -diff);
            player.unassignedCrew.AddCrew(type, savedReserve, false);
            for (int i = 0; i < brigs.Count; i++)
            {
                brigs[i].prisoners.Clear();
                foreach (var kv in savedPrisoners[i]) brigs[i].prisoners[kv.Key] = kv.Value;
                brigs[i].willing.Clear();
                foreach (var kv in savedWilling[i]) brigs[i].willing[kv.Key] = kv.Value;
            }
            player.credits = savedCredits;
        }
    }

    // With `free` places aboard and a shortfall of free + 3: the reserve gives `free` and keeps 3, the station sells
    // `free` and reports 3 still missing. The game's own purchase (option off) charges for all free + 3.
    private void CheckHireFits(SelfCheckContext ctx)
    {
        var player = GamePlayer.current;
        var ship = player?.currentSpaceShip;
        var ss = SpaceStationInterior.instance != null ? SpaceStationInterior.instance.spacestation : null;
        if (ship == null || ss?.personnelCenter == null || ship.GetMaxGrunts() <= 0)
        {
            ctx.Skip("hire-fits", "not docked at a station with a personnel center");
            return;
        }
        string type = null;
        int most = 0;
        foreach (var kv in ship.crewData.crew)
            if (kv.Value > most && CrewType.TryGet(kv.Key, out var t) && t.availableForPlayer) { type = kv.Key; most = kv.Value; }
        if (type == null || most < 2)
        {
            ctx.Skip("hire-fits", "the flagship has no crew type with 2 or more aboard");
            return;
        }
        var crewType = CrewType.Get(type);
        int price = PersonnelCenter.GetBuyPrice(crewType, ss.level);
        bool savedFits = HireOnlyWhatFits.Value, savedRecruit = RecruitPrisoners.Value;
        int savedReserve = player.unassignedCrew.GetCrewCount(type);
        long savedCredits = player.credits;
        try
        {
            RecruitPrisoners.Value = false;
            HireOnlyWhatFits.Value = true;
            ship.RemoveCrew(type, 2);
            int free = ship.GetMaxGrunts() - ship.crewData.totalCrew;
            int ask = free + 3;

            player.unassignedCrew.RemoveCrew(type, savedReserve, false);
            player.unassignedCrew.AddCrew(type, ask, false);
            int fromReserve = IdleManager.TransferFromReserve(type, ask, ship);
            int reserveLeft = player.unassignedCrew.GetCrewCount(type);
            bool fullA = ship.crewData.totalCrew == ship.GetMaxGrunts();
            ship.RemoveCrew(type, free);

            long c0 = player.credits;
            int missing = IdleManager.BuyCrewFromStation(crewType, ask, ss, ship);
            long paid = c0 - player.credits;
            bool fullB = ship.crewData.totalCrew == ship.GetMaxGrunts();
            ship.RemoveCrew(type, free);

            HireOnlyWhatFits.Value = false;
            c0 = player.credits;
            int before = ship.crewData.totalCrew;
            int vanillaMissing = IdleManager.BuyCrewFromStation(crewType, ask, ss, ship);
            long vanillaPaid = c0 - player.credits;
            int vanillaAdded = ship.crewData.totalCrew - before;

            string detail = $"{ship.GetShipName()} {type}, {free} free, asked {ask}: reserve gave {fromReserve} (kept {reserveLeft}), "
                + $"station sold {ask - missing} for {paid} cr ({price} each), game alone: {ask - vanillaMissing} charged ({vanillaPaid} cr), {vanillaAdded} came aboard";
            if (fromReserve == free && reserveLeft == 3 && fullA && missing == 3 && paid == (long)free * price && fullB
                && vanillaPaid == (long)ask * price && vanillaAdded == free)
                ctx.Pass("hire-fits", detail);
            else ctx.Fail("hire-fits", detail);
        }
        finally
        {
            HireOnlyWhatFits.Value = savedFits;
            RecruitPrisoners.Value = savedRecruit;
            int diff = most - ship.crewData.GetCrewCount(type);
            if (diff > 0) ship.AddCrew(type, diff, false);
            else if (diff < 0) ship.RemoveCrew(type, -diff);
            player.unassignedCrew.RemoveCrew(type, player.unassignedCrew.GetCrewCount(type), false);
            player.unassignedCrew.AddCrew(type, savedReserve, false);
            player.credits = savedCredits;
        }
    }

    private void CheckDungeonMinimum(SelfCheckContext ctx)
    {
        DungeonLocationData location = null;
        var poi = MapPointOfInterest.current;
        if (poi != null)
            foreach (var p in poi.GetPersistables())
                if (p is DungeonLocationData d) { location = d; break; }
        if (location == null)
        {
            ctx.Skip("dungeon-min", "no dungeon at the current location");
            return;
        }
        int saved = DungeonCrewMinimum.Value;
        try
        {
            DungeonCrewMinimum.Value = 9999;
            var crew = IdleManager.SelectAutonomousDungeonCrew(location);
            if (crew == null) ctx.Pass("dungeon-min", "minimum 9999 -> no crew selected");
            else ctx.Fail("dungeon-min", $"minimum 9999 still selected {crew.Count} crew types");
        }
        finally { DungeonCrewMinimum.Value = saved; }
    }
}

[HarmonyPatch]
internal static class EchoGatePatches
{
    private static EchoGates F => EchoGates.Instance;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.TryLootBeforeLeaving))]
    private static bool TryLootBeforeLeavingPrefix(ref bool __result)
    {
        if (!F.Active) return true;
        try
        {
            if (F.LootBeforeLeaving.Value) return true;
            __result = false;
            return false;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoGatePatches)); return true; }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.WouldIncurFactionPenalty))]
    private static void WouldIncurFactionPenaltyPostfix(Faction faction, ref bool __result)
    {
        if (!F.Active) return;
        try
        {
            int threshold = F.RaidReputationThreshold.Value;
            if (threshold == EchoGates.VanillaRaidThreshold) return;
            __result = faction != null && !GamePlayer.current.hasUmbralTransponder && faction.GetReputation(Faction.player) > threshold;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoGatePatches)); }
    }

    // Vanilla body (IdleManager.SelectAutonomousDungeonCrew) with the crew minimum and boarding fraction configurable.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.SelectAutonomousDungeonCrew))]
    private static bool SelectAutonomousDungeonCrewPrefix(DungeonLocationData location, ref Dictionary<string, int> __result)
    {
        if (!F.Active) return true;
        try
        {
            int minimum = F.DungeonCrewMinimum.Value;
            float boarding = F.BoardingCrewFraction.Value;
            if (minimum == EchoGates.VanillaDungeonCrewMinimum && boarding == EchoGates.VanillaBoardingCrewFraction) return true;

            var crewData = GamePlayer.current.currentSpaceShip?.crewData;
            if (crewData == null) { __result = null; return false; }
            var dict = new Dictionary<string, int>(crewData.crew);
            foreach (var id in IdleManager.ExcludedFromAutonomousDungeonCrewIds) dict.Remove(id);
            int num = 0;
            foreach (var kv in dict) num += kv.Value;
            if (num < minimum) { __result = null; return false; }
            if (location.isShipBased)
            {
                int maxGrunts = GamePlayer.current.currentSpaceShip.GetMaxGrunts();
                if (num < maxGrunts * boarding) { __result = null; return false; }
                __result = IdleManager.SelectAdvantagedCrew(dict, num, IdleManager.BoardingCrewIds);
                return false;
            }
            var def = DungeonDefinition.Get(location.dungeonType);
            __result = (def == null || !def.usesDiversityBonus)
                ? IdleManager.SelectAdvantagedCrew(dict, num, IdleManager.BuildAdvantagedIds(def))
                : IdleManager.SelectDiverseCrew(dict, num);
            return false;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoGatePatches)); return true; }
    }

    // Vanilla body (IdleManager.IsCrewBelowThreshold) with the 0.5 fraction configurable.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.IsCrewBelowThreshold))]
    private static bool IsCrewBelowThresholdPrefix(ref bool __result)
    {
        if (!F.Active) return true;
        try
        {
            float fraction = F.HireCrewBelowFraction.Value;
            if (fraction == EchoGates.VanillaHireCrewBelowFraction) return true;

            __result = false;
            SpaceShipData ship = GamePlayer.current.currentSpaceShip;
            if (ship == null) return false;
            int maxGrunts = ship.GetMaxGrunts();
            if (maxGrunts <= 0) return false;
            if ((float)ship.crewData.totalCrew >= maxGrunts * fraction) return false;
            var settings = ship.crewAutopilotSettings;
            float scale = IdleManager.GetCrewRatioScale(settings);
            foreach (var type in Behaviour.Crew.CrewType.all)
            {
                int shortfall = IdleManager.GetCrewShortfall(type, maxGrunts, scale, settings, ship);
                if (shortfall <= 0) continue;
                GamePlayer.current.unassignedCrew.crew.TryGetValue(type.identifier, out var available);
                if (available >= shortfall || IdleManager.CanBuyCrewSomewhere(settings, type))
                {
                    __result = true;
                    return false;
                }
            }
            return false;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoGatePatches)); return true; }
    }

    // Runs before the game's "ship full → nothing to do" test, so a full ship with a short type gets its places freed.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.PurchaseCrewIfNeeded))]
    private static void PurchaseCrewIfNeededPrefix(SpaceStation spaceStation)
    {
        if (!F.Active) return;
        try
        {
            if (F.RebalanceCrew.Value) EchoGates.Rebalance(GamePlayer.current.currentSpaceShip, spaceStation);
        }
        catch (Exception e) { F.Fail(e, nameof(EchoGatePatches)); }
    }

    // ECHO's crew top-up (IdleManager.TryHireCrewType) asks for a type's whole shortfall against the flagship's crew
    // ratios: first from the crew reserve (TransferFromReserve), then bought at the station (BuyCrewFromStation). Both
    // take or charge for the whole number, but SpaceShipData.AddCrew only adds what fits, so with a crew mix that's off
    // the ratios the rest of the reserve crew vanish and the credits are spent for nothing. With HireOnlyWhatFits both
    // stop at the free crew space. Willing prisoners (RecruitPrisoners) come after the reserve and count as part of
    // what it gave, so ECHO buys only what's still missing and its "hiring N" status covers both.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.TransferFromReserve))]
    private static bool TransferFromReservePrefix(string crewTypeId, int amount, SpaceShipData shipData, ref int __result)
    {
        if (!F.Active) return true;
        try
        {
            bool fits = F.HireOnlyWhatFits.Value;
            bool recruit = F.RecruitPrisoners.Value && shipData?.crewAutopilotSettings != null && shipData.crewAutopilotSettings.allowPurchase;
            if (!fits && !recruit) return true;
            var reserve = GamePlayer.current.unassignedCrew;
            reserve.crew.TryGetValue(crewTypeId, out int inReserve);
            int take = Math.Min(amount, inReserve);
            if (fits) take = Math.Min(take, Math.Max(0, shipData.GetMaxGrunts() - shipData.crewData.totalCrew));
            if (take > 0)
            {
                reserve.RemoveCrew(crewTypeId, take);
                shipData.AddCrew(crewTypeId, take);
            }
            __result = take;
            if (recruit) __result += EchoGates.RecruitWilling(crewTypeId, amount - take, shipData);
            return false;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoGatePatches)); return true; }
    }

    // Returns what's still missing, as the game's version does; buys no more than fits aboard.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.BuyCrewFromStation))]
    private static bool BuyCrewFromStationPrefix(CrewType crewType, int amount, SpaceStation spaceStation, SpaceShipData shipData, ref int __result)
    {
        if (!F.Active) return true;
        try
        {
            if (!F.HireOnlyWhatFits.Value) return true;
            __result = amount;
            if (spaceStation.personnelCenter == null) return false;
            int price = PersonnelCenter.GetBuyPrice(crewType, spaceStation.level);
            long affordable = price > 0 ? GamePlayer.current.credits / price : amount;
            int room = Math.Max(0, shipData.GetMaxGrunts() - shipData.crewData.totalCrew);
            int n = (int)Math.Min(Math.Min(amount, affordable), room);
            if (n < amount && n < affordable)
                Plugin.Log.LogInfo($"[EchoGates] hiring {n} of {amount} {crewType.identifier}: only {room} fit aboard {shipData.GetShipName()}");
            if (n <= 0) return false;
            GamePlayer.current.RemoveCreditsForCrew((long)n * price, crewType.identifier);
            shipData.AddCrew(crewType.identifier, n);
            __result = amount - n;
            return false;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoGatePatches)); return true; }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.DropFoundItem))]
    private static bool DropFoundItemPrefix(IdleManager __instance, SpaceStation ss, SpaceShipData cargoShip, InventoryItemType dropItem)
    {
        if (!F.Active) return true;
        try
        {
            if (!F.FastUnloading.Value) return true;
            if (Cargo.DepositTarget(ss, dropItem) == null) return true;
            EchoCargo.CargoRules.DepositUpTo(__instance, ss, cargoShip, dropItem, int.MaxValue);
            return false;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoGatePatches)); return true; }
    }

    // What a docked decision can change: missions held, flagship crew, flagship cargo, credits. FindActivity runs in
    // one frame, so a difference between before and after is the decision's own doing.
    private static bool decidingDocked;
    private static int missionsBefore, crewBefore;
    private static float cargoBefore;
    private static long creditsBefore;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.FindActivity))]
    internal static void FindActivityPrefix()
    {
        decidingDocked = false;
        if (!F.Active) return;
        try
        {
            if (!F.QuickStationSteps.Value || SpaceStationInterior.instance == null) return;
            var player = GamePlayer.current;
            var ship = player?.currentSpaceShip;
            if (ship == null) return;
            decidingDocked = true;
            missionsBefore = player.missions.Count;
            crewBefore = ship.crewData.totalCrew;
            cargoBefore = ship.cargoUsed;
            creditsBefore = player.credits;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoGatePatches)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.FindActivity))]
    internal static void FindActivityPostfix(IdleManager __instance)
    {
        if (!F.Active || !decidingDocked) return;
        try
        {
            decidingDocked = false;
            var unit = IdleManager.ship;
            repairPending = SpaceStationInterior.instance != null && unit != null
                && (unit.currentHullHP < unit.maxHullHP || unit.currentArmorHP < unit.maxArmorHP);
            if (SpaceStationInterior.instance == null || __instance.updateTimer <= EchoGates.QuickStepSeconds) return;
            if (Singleton<TravelManager>.Instance.TravelActive() || GamePlayer.current.waypoints.Count > 0) return;
            var player = GamePlayer.current;
            var ship = player.currentSpaceShip;
            if (player.missions.Count == missionsBefore && ship.crewData.totalCrew == crewBefore
                && ship.cargoUsed == cargoBefore && player.credits == creditsBefore) return;
            __instance.updateTimer = EchoGates.QuickStepSeconds;
            if (GamePlayer.current.autoPlay)
            {
                var panel = SidePanel.instance;
                Plugin.Log.LogInfo($"[EchoGates] quick station step after '{(panel != null ? panel.idleStatusMessage.text : "")}'");
            }
        }
        catch (Exception e) { F.Fail(e, nameof(EchoGatePatches)); }
    }

    // A docked decision that found the flagship damaged makes ECHO wait for repairs (IdleManager.HandleAtSpaceStation)
    // until a later decision sees full hull and armour, up to a whole decision time after the repair finished. Once
    // they're full, the next decision comes after QuickStepSeconds.
    private static bool repairPending;

    internal static void ResetQuickSteps() => repairPending = decidingDocked = false;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.Update))]
    internal static void IdleUpdatePostfix(IdleManager __instance)
    {
        if (!repairPending || !F.Active) return;
        try
        {
            var player = GamePlayer.current;
            var unit = IdleManager.ship;
            if (player == null || !F.QuickStationSteps.Value || !player.autoPlay || SpaceStationInterior.instance == null || unit == null)
            {
                repairPending = false;
                return;
            }
            if (unit.currentHullHP < unit.maxHullHP || unit.currentArmorHP < unit.maxArmorHP) return;
            repairPending = false;
            if (__instance.updateTimer <= EchoGates.QuickStepSeconds) return;
            Plugin.Log.LogInfo($"[EchoGates] repairs done: next step in {EchoGates.QuickStepSeconds:0} s instead of {__instance.updateTimer:0.#} s");
            __instance.updateTimer = EchoGates.QuickStepSeconds;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoGatePatches)); }
    }

    // Docking is done: ECHO's first step inside comes after QuickStepSeconds instead of whatever was left of its timer.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpaceStationInterior), nameof(SpaceStationInterior.Start))]
    private static void StationStartPostfix()
    {
        if (!F.Active) return;
        try
        {
            if (!F.QuickStationSteps.Value || !GamePlayer.current.autoPlay || !Singleton<IdleManager>.HasInstance) return;
            var idle = Singleton<IdleManager>.Instance;
            if (idle.updateTimer <= EchoGates.QuickStepSeconds) return;
            Plugin.Log.LogInfo($"[EchoGates] docked: next step in {EchoGates.QuickStepSeconds:0} s instead of {idle.updateTimer:0.#} s");
            idle.updateTimer = EchoGates.QuickStepSeconds;
        }
        catch (Exception e) { F.Fail(e, nameof(EchoGatePatches)); }
    }
}
