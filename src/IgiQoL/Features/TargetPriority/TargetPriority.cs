using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Behaviour.Equipment.Module;
using Behaviour.Equipment.Turret;
using Behaviour.Equipment.Turret.CombatTurrets;
using Behaviour.Item;
using Behaviour.Unit;
using Behaviour.Weapons;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using Source.Galaxy;
using Source.Player;
using Source.SpaceShip;
using Source.SpaceShip.Auto;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.TargetPriority;

// Which enemy your ships and their individual weapons pick when they choose a target on their own. Only automatic
// choices change: your clicks on the flagship and attack orders always win.
public sealed class TargetPriority : Feature
{
    public static TargetPriority Instance;

    public TargetPriority() { Instance = this; }

    public override string Id => "TargetPriority";
    public override string Title => "Target priority";
    public override string Description => "Decides which enemy your ships attack when they pick a target themselves: the nearest one (the game's choice), the most damaged, the one hurting your fleet most, and more. Single weapons can have their own rule too, for example one turret on drones while the rest follow the ship. Your clicks and attack orders always win. Set it per ship with the last button on its row in the fleet panel.";
    public override Type[] PatchClasses => new[] { typeof(TargetPriorityPatches) };

    public override void OnActivated()
    {
        GameEvents.SaveLoaded += TargetPriorityPatches.Reset;
        GameEvents.MainMenuShown += TargetPriorityPatches.Reset;
    }

    public ConfigEntry<TargetRule> Rule;
    public ConfigEntry<float> RepickSeconds;
    public ConfigEntry<int> FinishBelowPercent;

    public static readonly string[] RuleNames =
    {
        "Nearest (game default)", "Lowest hull %", "Highest hull %", "Most damage to my fleet", "Attacking my flagship",
        "Smallest first (drones, fighters)", "Largest first", "Lowest level", "Highest level",
    };

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        Rule = cfg.Bind(Id, "Rule", TargetRule.Nearest, SettingInfo.Describe("Target choice",
            "Which enemy your ships attack when they pick a target on their own. Nearest is the game's normal choice. Lowest hull %: finish off damaged enemies. Highest hull %: hit the healthiest. Most damage to my fleet: whoever has hurt your ships most in the last few seconds. Attacking my flagship: enemies shooting at the ship you fly. Smallest or largest first: by hull size class, drones and carrier fighters count as smallest. Lowest or highest level: by enemy level. Ties go to the nearest enemy.",
            RuleNames));
        RepickSeconds = cfg.Bind(Id, "RepickSeconds", 0f, SettingInfo.Describe("Re-check the target every (seconds)",
            "0 keeps the game's behaviour: a ship sticks with its target until it dies or leaves. Above 0, each ship re-applies its target choice this often and switches when another enemy is clearly a better fit. It switches only when the difference is real (for example 10 points of hull, or 20% closer), so ships don't flip between two similar enemies. While a rule other than Nearest is set, ships also stop jumping to whoever just hit a nearby ally.",
            new AcceptableValueRange<float>(0f, 30f)));
        FinishBelowPercent = cfg.Bind(Id, "FinishBelowPercent", 0, SettingInfo.Describe("Don't switch away from targets below (hull %)",
            "When re-checking, a ship keeps its current target if that target's hull is below this percentage, so nearly dead enemies get finished. 0 turns this off.",
            new AcceptableValueRange<int>(0, 100)));
    }

    public struct Resolved
    {
        public TargetRule rule;
        public float repick;
        public int finishBelow;
        public int maxLevel;
        public float leash;
        public Dictionary<int, WeaponRule> weapons;

        // The ship's own target choice is the game's; weapon rules are checked separately.
        public bool Vanilla => rule == TargetRule.Nearest && repick <= 0f && maxLevel <= 0 && leash <= 0f;
    }

    public Resolved Resolve(SpaceShipData ship)
    {
        var s = ShipSettings.Get(ship.guid);
        return new Resolved
        {
            rule = s.targetRule ?? Rule.Value,
            repick = s.repickSeconds ?? RepickSeconds.Value,
            finishBelow = s.finishBelow ?? FinishBelowPercent.Value,
            maxLevel = s.maxLevel,
            leash = s.leash,
            weapons = s.weaponRules,
        };
    }

    // Hardpoints that hold a combat weapon (not a mining laser or salvage tool) get a weapon rule row.
    public static bool IsCombatWeapon(InventoryItemType item) => item != null && item.GetComponent<AbstractCombatTurret>() != null;

    public static int CombatWeaponCount(SpaceShipData ship)
    {
        int n = 0;
        foreach (var item in ship.hardpoints)
            if (IsCombatWeapon(item)) n++;
        return n;
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        var flagship = GamePlayer.current.currentSpaceShip;
        var g = flagship.guid;
        var before = ShipSettings.Get(g);
        var saved = (before.targetRule, before.repickSeconds, before.finishBelow, before.maxLevel, before.leash);
        var savedWeapons = new Dictionary<int, WeaponRule>(before.weaponRules);
        ctx.Cleanup(() =>
        {
            var c = ShipSettings.Edit(g);
            (c.targetRule, c.repickSeconds, c.finishBelow, c.maxLevel, c.leash) = saved;
            c.weaponRules.Clear();
            foreach (var kv in savedWeapons) c.weaponRules[kv.Key] = kv.Value;
            ShipSettings.Save(g);
        });

        var e = ShipSettings.Edit(g);
        e.targetRule = TargetRule.LowestHull;
        e.repickSeconds = 2f;
        e.finishBelow = 25;
        e.maxLevel = 0;
        e.leash = 0f;
        ShipSettings.Save(g);
        ShipSettings.Invalidate();
        var r = Resolve(flagship);
        var record = SaveData.Ship(g, false);
        string savedRule = record != null && record["targetRule"].IsString ? record["targetRule"].AsString : "none";
        string detail = $"rule={r.rule} repick={r.repick} finish={r.finishBelow} saved={savedRule}";
        if (r.rule == TargetRule.LowestHull && r.repick == 2f && r.finishBelow == 25 && savedRule == "LowestHull") ctx.Pass("settings-roundtrip", detail);
        else ctx.Fail("settings-roundtrip", detail);

        var ship = Fleet.Flagship;
        var module = ship != null ? ship.GetModule<CombatModule>() : null;
        var poi = MapPointOfInterest.current;
        if (module == null || poi == null || module.blocked || module.holdFire)
        {
            ctx.Skip("pick", $"needs the flagship in space with a combat module (module={(module != null)} poi={(poi != null)})");
            yield break;
        }

        // Three small combat hulls at your level, the same spawn the console's SpawnEnemy uses.
        string hullName = null;
        foreach (var kv in SpaceShip.allShips)
            if (kv.Value.shipRoleType != null && kv.Value.shipRoleType.GetRole() == SpaceShipRole.Combat && kv.Value.shipRoleType.GetTypeSize() == 2) { hullName = kv.Key; break; }
        if (hullName == null)
        {
            ctx.Skip("pick", "no size-2 combat hull to spawn");
            yield break;
        }
        var enemy = Faction.player.IsEnemy(Faction.marauders) ? Faction.marauders : Faction.fanatics;
        poi.AddTriggeredSpawnFromFixedPayload(0f, hullName, 3, enemy, null, UnitRank.Rookie, GamePlayer.current.level);
        var candidates = new List<TargetableUnit>();
        float until = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < until)
        {
            candidates.Clear();
            foreach (var u in Object.FindObjectsByType<AbstractUnit>(FindObjectsInactive.Exclude))
                if (u != null && module.IsValidTarget(u)) candidates.Add(u);
            if (candidates.Count >= 2) break;
            yield return new WaitForSeconds(1f);
        }
        if (candidates.Count < 2)
        {
            ctx.Skip("pick", $"spawned enemies did not arrive within 30 s ({candidates.Count} valid targets)");
            yield break;
        }

        Vector2 at = module.transform.position;
        candidates.Sort((a, b) => Vector2.Distance(at, a.transform.position).CompareTo(Vector2.Distance(at, b.transform.position)));
        var nearest = candidates[0];
        var weak = (AbstractUnit)candidates[candidates.Count - 1];
        float hull = weak.currentHullHP;
        weak.currentHullHP = weak.maxHullHP * 0.2f;
        try
        {
            var picked = TargetRanking.Pick(module, candidates, TargetRule.LowestHull, Resolve(flagship), validate: true);
            string pickDetail = $"picked={Name(picked)} weak={Name(weak)} nearest={Name(nearest)} candidates={candidates.Count}";
            if (picked == weak) ctx.Pass("pick", pickDetail);
            else ctx.Fail("pick", pickDetail);

            // First pick through the game's own path: the FindClosestFilteredTarget prefix.
            module.ResetCurrentTargets();
            module.UpdateAvailableTargets(candidates);
            var first = module.FindClosestFilteredTarget();
            string firstDetail = $"first={Name(first)} weak={Name(weak)} filtered={module.filteredTargets.Count}";
            if (first == weak) ctx.Pass("first-pick", firstDetail);
            else ctx.Fail("first-pick", firstDetail);

            // Re-pick: the ship holds the nearest enemy; the scan postfix should move it to the weak one.
            module.ResetCurrentTargets();
            module.SetAutoTarget(nearest);
            TargetPriorityPatches.Reset();
            module.UpdateAvailableTargets(candidates);
            string repickDetail = $"autoTarget={Name(module.autoTarget)} weak={Name(weak)} from={Name(nearest)}";
            if (module.autoTarget == weak) ctx.Pass("repick", repickDetail);
            else ctx.Fail("repick", repickDetail);
        }
        finally
        {
            weak.currentHullHP = hull;
            module.ResetCurrentTargets();
        }

        // Weapon rule: the ship stays on the nearest enemy, one weapon set to "Lowest hull %" takes the weakened one.
        AbstractTurret ruled = null, other = null;
        foreach (var t in module.turrets)
        {
            if (t == null) continue;
            if (ruled == null && t.GetComponentInParent<SpaceShipHardpoint>() != null) ruled = t;
            else if (other == null) other = t;
        }
        AbstractUnit weakInReach = null;
        if (ruled != null)
        {
            for (int i = candidates.Count - 1; i > 0; i--)
                if (TargetRanking.InReach(ruled, candidates[i])) { weakInReach = (AbstractUnit)candidates[i]; break; }
            // Spawn spots are random; bring the farthest enemy inside this weapon's range if none is there yet.
            if (weakInReach == null && candidates[candidates.Count - 1] is AbstractUnit last && last.rigidbody != null)
            {
                Vector2 spot = (Vector2)ruled.transform.position + Vector2.up * (ruled.range * 0.5f);
                last.transform.position = spot;
                last.rigidbody.position = spot;
                if (TargetRanking.InReach(ruled, last)) weakInReach = last;
            }
        }
        if (ruled == null || other == null || weakInReach == null)
        {
            var far = candidates[candidates.Count - 1];
            string reach = ruled == null ? "" : $" canTarget={ruled.CanTarget(far)} dist={Vector2.Distance(ruled.transform.position, far.transform.position):0.0} radius={far.radius:0.0} range={ruled.range:0.0}";
            ctx.Skip("weapon-pick", $"needs two weapons and a second enemy in range (weapons={(ruled != null)}/{(other != null)} inReach={(weakInReach != null)}{reach})");
        }
        else
        {
            int index = ruled.GetComponentInParent<SpaceShipHardpoint>().index;
            var item = flagship.hardpoints[index];
            var we = ShipSettings.Edit(g);
            (we.targetRule, we.repickSeconds) = (TargetRule.Nearest, 0f);
            we.weaponRules[index] = new WeaponRule(TargetRule.LowestHull, item != null ? item.identifier : null);
            float weakHull = weakInReach.currentHullHP;
            weakInReach.currentHullHP = weakInReach.maxHullHP * 0.2f;
            try
            {
                module.ResetCurrentTargets();
                module.SetAutoTarget(nearest);
                module.UpdateAvailableTargets(candidates);
                string wDetail = $"hardpoint {index} ({(item != null ? item.displayName : "?")}) -> {Name(ruled.currentTarget)}, other weapon -> {Name(other.currentTarget)}, ship -> {Name(module.priorityTarget)}, weak={Name(weakInReach)}";
                if (ruled.currentTarget == weakInReach && other.currentTarget == nearest) ctx.Pass("weapon-pick", wDetail);
                else ctx.Fail("weapon-pick", wDetail);

                // The turret's own Update aims through the getter: its tracking point is parented to what it shoots at.
                float aimUntil = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < aimUntil && !(ruled.trackingTarget != null && ruled.trackingTarget.transform.parent == weakInReach.transform))
                    yield return null;
                var aim = ruled.trackingTarget != null ? ruled.trackingTarget.transform.parent : null;
                string aimDetail = $"tracking parent={(aim != null ? aim.name : "none")} weak={weakInReach.name}";
                if (aim == weakInReach.transform) ctx.Pass("weapon-aim", aimDetail);
                else ctx.Fail("weapon-aim", aimDetail);
            }
            finally
            {
                weakInReach.currentHullHP = weakHull;
                we.weaponRules.Remove(index);
                module.ResetCurrentTargets();
            }
        }

        // Live: an Aggressive fleet ship with "Lowest hull %" and a 1 s re-check, left to the game's own scanner.
        SpaceShipData wingData = null;
        foreach (var d in GamePlayer.current.activeFleet)
            if (d != null && d != flagship && d.unit != null && d.unit.autoActions is WingmanActions && d.fleetStance == FleetStance.Aggressive) { wingData = d; break; }
        if (wingData == null)
        {
            ctx.Skip("live-fleet", "no Aggressive fleet ship in space");
            yield break;
        }
        var wg = wingData.guid;
        var wb = ShipSettings.Get(wg);
        var wsaved = (wb.targetRule, wb.repickSeconds, wb.finishBelow);
        var w = ShipSettings.Edit(wg);
        (w.targetRule, w.repickSeconds, w.finishBelow) = ((TargetRule?)TargetRule.LowestHull, (float?)1f, (int?)0);
        // The game pauses every player ship's scanner while a route is set; clear it for the test.
        var route = new List<MapPointOfInterest>(GamePlayer.current.waypoints);
        GamePlayer.current.waypoints.Clear();
        var wingModule = wingData.unit.GetModule<CombatModule>();
        weak.currentHullHP = weak.maxHullHP * 0.15f;
        // Start the fleet ship on a healthy enemy so only the re-check can move it to the weak one.
        var start = candidates[0] != weak ? candidates[0] : candidates[1];
        wingModule.ResetCurrentTargets();
        wingModule.SetAutoTarget(start);
        string startName = Name(wingModule.autoTarget);
        string liveDetail = "";
        bool switched = false;
        try
        {
            float end = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < end && weak != null && !weak.isDestroyed)
            {
                if (wingModule.autoTarget == weak) { switched = true; break; }
                yield return new WaitForSeconds(0.25f);
            }
            liveDetail = $"ship={wingData.GetShipName()} start={startName} autoTarget={Name(wingModule.autoTarget)} manual={Name(wingModule.manualTarget)} weak={Name(weak)} weakDestroyed={(weak == null || weak.isDestroyed)}";
        }
        finally
        {
            (w.targetRule, w.repickSeconds, w.finishBelow) = wsaved;
            ShipSettings.Save(wg);
            GamePlayer.current.waypoints.AddRange(route);
        }
        if (switched) ctx.Pass("live-fleet", liveDetail);
        else if (weak == null || weak.isDestroyed) ctx.Skip("live-fleet", "the weakened enemy died first: " + liveDetail);
        else ctx.Fail("live-fleet", liveDetail);
    }

    private static string Name(TargetableUnit t) => t == null ? "none" : t.targetName + "#" + t.GetHashCode();
}

internal static class TargetRanking
{
    private static readonly List<AbstractUnit> fleetUnits = new();

    // Best candidate under the rule, or null. Lower keys rank first; ties go to the nearest.
    // Candidates above the level cap only win when nothing else is left; candidates past the leash never do.
    // With a turret, only enemies that weapon can hit and has in range count, and distance is measured from it.
    public static TargetableUnit Pick(AbstractTargetingModule module, List<TargetableUnit> candidates, TargetRule rule, TargetPriority.Resolved r, bool validate, AbstractTurret turret = null)
    {
        var flagship = Fleet.Flagship;
        bool leashed = turret == null && r.leash > 0f && flagship != null && module.parent != flagship && module.parent.autoActions is WingmanActions;
        Vector2 from = turret != null ? turret.transform.position : module.transform.position;

        TargetableUnit best = null;
        int bestTier = 0;
        float bestKey = 0f, bestDist = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            var t = candidates[i];
            if (t == null || (validate && !module.IsValidTarget(t))) continue;
            if (turret != null && !InReach(turret, t)) continue;
            if (leashed && Vector2.Distance(t.transform.position, flagship.transform.position) > r.leash) continue;
            float dist = Vector2.Distance(from, t.transform.position);
            int tier = r.maxLevel > 0 && t is AbstractUnit u && u.level > r.maxLevel ? 1 : 0;
            float key = Key(rule, t, dist, flagship);
            if (best == null || tier < bestTier || (tier == bestTier && (key < bestKey || (key == bestKey && dist < bestDist))))
            {
                best = t;
                bestTier = tier;
                bestKey = key;
                bestDist = dist;
            }
        }
        return best;
    }

    // The weapon can damage the target and the target's hull edge is within its range.
    public static bool InReach(AbstractTurret turret, TargetableUnit t) =>
        turret.CanTarget(t) && Vector2.Distance(turret.transform.position, t.transform.position) - t.radius <= turret.range;

    // True when the candidate beats the current target by enough to be worth switching.
    public static bool ClearlyBetter(Vector2 from, TargetableUnit candidate, TargetableUnit current, TargetRule rule, TargetPriority.Resolved r)
    {
        var flagship = Fleet.Flagship;
        if (r.maxLevel > 0)
        {
            int ct = candidate is AbstractUnit cu && cu.level > r.maxLevel ? 1 : 0;
            int pt = current is AbstractUnit pu && pu.level > r.maxLevel ? 1 : 0;
            if (ct != pt) return ct < pt;
        }
        float a = Key(rule, candidate, Vector2.Distance(from, candidate.transform.position), flagship);
        float b = Key(rule, current, Vector2.Distance(from, current.transform.position), flagship);
        return a < b - Margin(rule, b);
    }

    public static bool BelowFinish(TargetableUnit t, int finishBelow) =>
        finishBelow > 0 && t is AbstractUnit u && u.maxHullHP > 0f && u.currentHullHP / u.maxHullHP * 100f < finishBelow;

    private static float Margin(TargetRule rule, float current) => rule switch
    {
        TargetRule.Nearest => Mathf.Max(2f, current * 0.2f),
        TargetRule.LowestHull or TargetRule.HighestHull => 0.1f,
        TargetRule.MostDamage => Mathf.Abs(current) * 0.25f + 1f,
        _ => 0.5f,
    };

    private static float Key(TargetRule rule, TargetableUnit t, float dist, AbstractUnit flagship)
    {
        var u = t as AbstractUnit;
        switch (rule)
        {
            case TargetRule.LowestHull: return Hull(u, 1f);
            case TargetRule.HighestHull: return -Hull(u, 0f);
            case TargetRule.MostDamage: return u != null ? -DamageToFleet(u) : 0f;
            case TargetRule.AttackingFlagship: return flagship != null && u != null && flagship.IsTargetedBy(u) ? 0f : 1f;
            case TargetRule.SmallestFirst: return Size(t);
            case TargetRule.LargestFirst: return -Size(t);
            case TargetRule.LowestLevel: return u != null ? u.level : 9999f;
            case TargetRule.HighestLevel: return u != null ? -u.level : 0f;
            default: return dist;
        }
    }

    private static float Hull(AbstractUnit u, float fallback)
    {
        if (u == null) return fallback;
        float max = u.maxHullHP;
        return max > 0f ? u.currentHullHP / max : fallback;
    }

    // Drones and carrier fighters are 0, hulls 1-8 by size class, anything that isn't a ship (turrets, station parts) 9.
    private static float Size(TargetableUnit t)
    {
        if (t is Drone) return 0f;
        if (t is SpaceShip s)
        {
            if (s.isCarrierFighter) return 0f;
            return s.shipRoleType != null ? s.shipRoleType.GetTypeSize() : 5f;
        }
        return 9f;
    }

    // Damage this enemy dealt to your flagship and fleet ships in the game's last-few-seconds tracker.
    private static float DamageToFleet(AbstractUnit enemy)
    {
        float total = 0f;
        for (int i = 0; i < fleetUnits.Count; i++)
        {
            var ship = fleetUnits[i];
            if (ship != null && ship.targetedBy.TryGetValue(enemy, out var tracker)) total += tracker.damage;
        }
        return total;
    }

    internal static void CollectFleet()
    {
        fleetUnits.Clear();
        var flagship = Fleet.Flagship;
        if (flagship != null) fleetUnits.Add(flagship);
        var player = GamePlayer.current;
        if (player == null) return;
        foreach (var data in player.activeFleet)
        {
            var unit = data != null ? data.unit : null;
            if (unit != null && unit != flagship) fleetUnits.Add(unit);
        }
    }
}

// A weapon's own target, refreshed on the ship's scan and read back by the AbstractTurret.currentTarget postfix.
internal sealed class WeaponPick
{
    public TargetableUnit target;
    public float due;
}

internal sealed class ReferenceComparer : IEqualityComparer<AbstractTurret>
{
    public bool Equals(AbstractTurret a, AbstractTurret b) => ReferenceEquals(a, b);
    public int GetHashCode(AbstractTurret o) => RuntimeHelpers.GetHashCode(o);
}

[HarmonyPatch]
internal static class TargetPriorityPatches
{
    private static readonly Dictionary<CombatModule, float> nextRepick = new();
    internal static readonly Dictionary<AbstractTurret, WeaponPick> WeaponPicks = new(new ReferenceComparer());
    private static readonly List<AbstractTurret> deadTurrets = new();

    // Keys are Unity objects that die with the save; drop them on load and at the main menu.
    internal static void Reset()
    {
        nextRepick.Clear();
        WeaponPicks.Clear();
    }

    private static bool Resolve(AbstractTargetingModule module, out TargetPriority.Resolved r)
    {
        r = default;
        if (!(module is CombatModule)) return false;
        var unit = module.parent;
        if (unit == null || !Fleet.IsPlayerShip(unit)) return false;
        if (!(unit.unitData is SpaceShipData data)) return false;
        r = TargetPriority.Instance.Resolve(data);
        return true;
    }

    // Your own clicks on the flagship and attack orders to fleet ships win over weapon rules.
    // Escort and Defensive assists are manual targets too, but they are the stance's pick, so weapon rules still apply.
    internal static bool ManualWins(AbstractTargetingModule module)
    {
        if (module.manualTarget == null) return false;
        var unit = module.parent;
        if (!(unit.autoActions is WingmanActions wing)) return true;
        var order = wing.currentOrder;
        return order != null && order.type == FleetOrderType.Attack;
    }

    // First pick when the ship has no target: rank filteredTargets by the rule instead of taking the nearest.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AbstractTargetingModule), "FindClosestFilteredTarget")]
    private static bool FindClosestPrefix(AbstractTargetingModule __instance, ref TargetableUnit __result)
    {
        var feature = TargetPriority.Instance;
        if (!feature.Active) return true;
        try
        {
            if (!Resolve(__instance, out var r) || r.Vanilla) return true;
            if (r.rule == TargetRule.MostDamage) TargetRanking.CollectFleet();
            __result = TargetRanking.Pick(__instance, __instance.filteredTargets, r.rule, r, validate: false);
            return false;
        }
        catch (Exception e)
        {
            feature.Fail(e, nameof(TargetPriorityPatches));
            return true;
        }
    }

    // Each scan: the ship's own re-pick and leash, then each weapon's pick.
    // The scan list is the scanner's fresh List of everything in range, sorted by distance.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatModule), "UpdateAvailableTargets")]
    private static void ScanPostfix(CombatModule __instance, IEnumerable<TargetableUnit> targets)
    {
        var feature = TargetPriority.Instance;
        if (!feature.Active) return;
        try
        {
            if (!Resolve(__instance, out var r)) return;
            var list = targets as List<TargetableUnit>;
            if (!r.Vanilla) UpdateShipTarget(__instance, list, r);
            if (list != null && (r.weapons.Count > 0 || WeaponPicks.Count > 0)) UpdateWeaponPicks(__instance, list, r);
        }
        catch (Exception e) { feature.Fail(e, nameof(TargetPriorityPatches)); }
    }

    private static void UpdateShipTarget(CombatModule module, List<TargetableUnit> list, TargetPriority.Resolved r)
    {
        var current = module.autoTarget;
        if (current == null || module.manualTarget != null || module.blocked || module.holdFire) return;

        var unit = module.parent;
        var flagship = Fleet.Flagship;
        if (r.leash > 0f && flagship != null && unit != flagship && unit.autoActions is WingmanActions
            && Vector2.Distance(current.transform.position, flagship.transform.position) > r.leash)
        {
            module.ResetAutoTarget();
            if (list != null)
            {
                if (r.rule == TargetRule.MostDamage) TargetRanking.CollectFleet();
                var pick = TargetRanking.Pick(module, list, r.rule, r, validate: true);
                if (pick != null) module.SetAutoTarget(pick);
            }
            return;
        }

        if (r.repick <= 0f || list == null) return;
        float now = Time.time;
        if (nextRepick.TryGetValue(module, out var due) && now < due) return;
        nextRepick[module] = now + r.repick;

        if (TargetRanking.BelowFinish(current, r.finishBelow)) return;
        if (r.rule == TargetRule.MostDamage) TargetRanking.CollectFleet();
        var best = TargetRanking.Pick(module, list, r.rule, r, validate: true);
        if (best == null || best == current || !TargetRanking.ClearlyBetter(module.transform.position, best, current, r.rule, r)) return;
        module.SetAutoTarget(best);
        module.torpedoBayModule?.AddManualTarget(best, overrideTarget: false);
    }

    // Weapons with a rule pick among enemies they can hit and have in range; without one (or with nothing in reach)
    // they follow the ship's target. Picks only exist while the ship itself is fighting.
    private static void UpdateWeaponPicks(CombatModule module, List<TargetableUnit> list, TargetPriority.Resolved r)
    {
        var turrets = module.turrets;
        if (turrets == null) return;
        bool engaged = module.priorityTarget != null && !module.blocked && !module.holdFire && !ManualWins(module);
        float now = Time.time;
        bool fleetCollected = false;
        for (int i = 0; i < turrets.Length; i++)
        {
            var turret = turrets[i];
            if (turret == null) continue;
            WeaponRule rule = null;
            if (engaged && r.weapons.Count > 0)
            {
                var hardpoint = turret.GetComponentInParent<SpaceShipHardpoint>();
                if (hardpoint != null) r.weapons.TryGetValue(hardpoint.index, out rule);
            }
            if (rule == null)
            {
                WeaponPicks.Remove(turret);
                continue;
            }

            WeaponPicks.TryGetValue(turret, out var pick);
            var current = pick?.target;
            bool keep = current != null && !current.isDestroyed && module.IsValidTarget(current) && TargetRanking.InReach(turret, current);
            if (keep && (r.repick <= 0f || now < pick.due)) continue;
            if (keep && TargetRanking.BelowFinish(current, r.finishBelow))
            {
                pick.due = now + r.repick;
                continue;
            }
            if (rule.rule == TargetRule.MostDamage && !fleetCollected)
            {
                TargetRanking.CollectFleet();
                fleetCollected = true;
            }
            var best = TargetRanking.Pick(module, list, rule.rule, r, validate: true, turret);
            if (best == null)
            {
                if (!keep) WeaponPicks.Remove(turret);
                continue;
            }
            if (keep && best != current && !TargetRanking.ClearlyBetter(turret.transform.position, best, current, rule.rule, r))
            {
                pick.due = now + r.repick;
                continue;
            }
            if (pick == null) WeaponPicks[turret] = pick = new WeaponPick();
            pick.target = best;
            pick.due = now + r.repick;
        }

        if (WeaponPicks.Count > 64)
        {
            deadTurrets.Clear();
            foreach (var kv in WeaponPicks)
                if (kv.Key == null) deadTurrets.Add(kv.Key);
            foreach (var t in deadTurrets) WeaponPicks.Remove(t);
        }
    }

    // Every turret reads its target through this getter every frame (aim, lead, fire, missiles).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AbstractTurret), "currentTarget", MethodType.Getter)]
    private static void CurrentTargetPostfix(AbstractTurret __instance, ref TargetableUnit __result)
    {
        if (WeaponPicks.Count == 0) return;
        var feature = TargetPriority.Instance;
        if (!feature.Active) return;
        try
        {
            if (!WeaponPicks.TryGetValue(__instance, out var pick)) return;
            var target = pick.target;
            if (target == null || target.isDestroyed) return;
            var module = __instance.targetingModule;
            if (module == null || module.priorityTarget == null || module.holdFire || module.blocked || ManualWins(module)) return;
            __result = target;
        }
        catch (Exception e) { feature.Fail(e, nameof(TargetPriorityPatches)); }
    }

    // Vanilla moves a player ship's auto target to whoever just hit a nearby ally. With a rule set, the rule decides
    // instead; a ship without a target still answers the attack.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AbstractUnit), "AssistPlayerAgainstAttacker")]
    private static bool AssistPrefix(AbstractUnit playerShip)
    {
        var feature = TargetPriority.Instance;
        if (!feature.Active) return true;
        try
        {
            var module = playerShip != null ? playerShip.GetModule<CombatModule>() : null;
            if (module == null || module.autoTarget == null) return true;
            return !(Resolve(module, out var r) && r.rule != TargetRule.Nearest);
        }
        catch (Exception e)
        {
            feature.Fail(e, nameof(TargetPriorityPatches));
            return true;
        }
    }
}
