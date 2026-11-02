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
using LightJson;
using Source.Item;
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
    public override string Description => "Decides which enemy your ships attack when they pick a target themselves: the nearest one (the game's choice), the most damaged, the one hurting your fleet most, the one your guns hurt most, and more. Each ship can chain up to three rules, so a later rule decides when an earlier one sees no real difference. Single weapons can have their own rule too, for example one turret on drones while the rest follow the ship. Your clicks and attack orders always win. Set it per ship with the last button on its row in the fleet panel.";
    public override Type[] PatchClasses => new[] { typeof(TargetPriorityPatches) };

    public override void OnActivated()
    {
        GameEvents.SaveLoaded += TargetPriorityPatches.Reset;
        GameEvents.MainMenuShown += TargetPriorityPatches.Reset;
    }

    public ConfigEntry<TargetRule> Rule;
    public ConfigEntry<float> RepickSeconds;
    public ConfigEntry<int> FinishBelowPercent;
    public ConfigEntry<int> WeakestGuardPercent;
    public ConfigEntry<int> DebuffedGuardPercent;

    public static readonly string[] RuleNames =
    {
        "Nearest (game default)", "Lowest hull %", "Highest hull %", "Most damage to my fleet", "Attacking my flagship",
        "Smallest first (drones, fighters)", "Largest first", "Lowest level", "Highest level", "Weakest to my weapons",
        "Debuffed first",
    };

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        Rule = cfg.Bind(Id, "Rule", TargetRule.Nearest, SettingInfo.Describe("Target choice",
            "Which enemy your ships attack when they pick a target on their own. Nearest is the game's normal choice. Lowest hull %: finish off damaged enemies. Highest hull %: hit the healthiest. Most damage to my fleet: whoever has hurt your ships most in the last few seconds. Attacking my flagship: enemies shooting at or aiming at the ship you fly; a ship with this rule first also switches to whoever hits your flagship. Smallest or largest first: by hull size class, drones and carrier fighters count as smallest. Lowest or highest level: by enemy level. Weakest to my weapons: the enemy that takes the most damage from the ship's guns after its resistances. Debuffed first: the enemy carrying the most debuffs. Each ship can add up to two more rules in its popup; a rule that sees only a small difference passes the choice to the next one. Ties go to the nearest enemy.",
            RuleNames));
        RepickSeconds = cfg.Bind(Id, "RepickSeconds", 0f, SettingInfo.Describe("Re-check the target every (seconds)",
            "0 keeps the game's behaviour: a ship sticks with its target until it dies or leaves. Above 0, each ship re-applies its target choice this often and switches when another enemy is clearly a better fit. It switches only when the difference is real (for example 10 points of hull, or 20% closer), so ships don't flip between two similar enemies. While a rule other than Nearest is set, ships also stop jumping to whoever just hit a nearby ally, except that \"Attacking my flagship\" still answers hits on your flagship.",
            new AcceptableValueRange<float>(0f, 30f)));
        FinishBelowPercent = cfg.Bind(Id, "FinishBelowPercent", 0, SettingInfo.Describe("Don't switch away from targets below (hull %)",
            "When re-checking, a ship keeps its current target if that target's hull is below this percentage, so nearly dead enemies get finished. 0 turns this off.",
            new AcceptableValueRange<int>(0, 100)));
        WeakestGuardPercent = cfg.Bind(Id, "WeakestGuardPercent", 25, SettingInfo.Describe("Weakest to my weapons: needs a lead of (%)",
            "\"Weakest to my weapons\" only prefers an enemy that takes more than this much more damage from the ship's guns than the other choice. A smaller difference counts as a tie and the next rule (or the nearest enemy) decides, so a small resistance gap never drags a ship across the field.",
            new AcceptableValueRange<int>(0, 200)));
        DebuffedGuardPercent = cfg.Bind(Id, "DebuffedGuardPercent", 25, SettingInfo.Describe("Debuffed first: needs a lead of (%)",
            "\"Debuffed first\" only prefers an enemy carrying more than this much more debuffs than the other choice (any debuff beats none). A smaller difference counts as a tie and the next rule (or the nearest enemy) decides.",
            new AcceptableValueRange<int>(0, 200)));
    }

    public struct Resolved
    {
        // The rule chain, first rule first; never empty.
        public List<TargetRule> rules;
        public float repick;
        public int finishBelow;
        public int maxLevel;
        public float leash;
        public Dictionary<int, WeaponRule> weapons;

        // Only "Nearest": the game's own choice and its "help a hit ally" switch.
        public bool NearestOnly => rules.Count == 1 && rules[0] == TargetRule.Nearest;

        // The ship's own target choice is the game's; weapon rules are checked separately.
        public bool Vanilla => NearestOnly && repick <= 0f && maxLevel <= 0 && leash <= 0f;
    }

    // The chain of a ship without its own: the config rule alone. Reused, so resolving doesn't allocate.
    private static readonly List<TargetRule> DefaultChain = new() { TargetRule.Nearest };

    public Resolved Resolve(SpaceShipData ship)
    {
        var s = ShipSettings.Get(ship.guid);
        DefaultChain[0] = Rule.Value;
        return new Resolved
        {
            rules = s.targetRules.Count > 0 ? s.targetRules : DefaultChain,
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
        // The legacy and loadout steps below replace the whole record, so the cleanup puts the whole record back.
        var full = ShipSettings.Snapshot(g);
        ctx.Cleanup(() => ShipSettings.Restore(g, full));

        var e = ShipSettings.Edit(g);
        SetChain(e, new[] { TargetRule.LowestHull, TargetRule.WeakestToMyWeapons });
        e.repickSeconds = 2f;
        e.finishBelow = 25;
        e.maxLevel = 0;
        e.leash = 0f;
        ShipSettings.Save(g);
        ShipSettings.Invalidate();
        var r = Resolve(flagship);
        var record = SaveData.Ship(g, false);
        string savedRules = record != null && record["targetRules"].IsJsonArray ? record["targetRules"].ToString() : "none";
        bool oldKey = record != null && !record["targetRule"].IsNull;
        string detail = $"rules={Chain(r.rules)} repick={r.repick} finish={r.finishBelow} saved={savedRules} oldKey={oldKey}";
        if (Chain(r.rules) == "LowestHull>WeakestToMyWeapons" && r.repick == 2f && r.finishBelow == 25 && savedRules.Contains("WeakestToMyWeapons") && !oldKey) ctx.Pass("settings-roundtrip", detail);
        else ctx.Fail("settings-roundtrip", detail);

        // A save from before chains has one "targetRule"; it loads as a one-rule chain and is written back as an array.
        // Then the loadout round trip: a snapshot taken now brings the chain back after it changed.
        var legacy = new JsonObject();
        legacy["targetRule"] = "MostDamage";
        ShipSettings.Restore(g, legacy);
        string legacyChain = Chain(ShipSettings.Get(g).targetRules);
        record = SaveData.Ship(g, false);
        bool rewritten = record != null && record["targetRules"].IsJsonArray && record["targetRule"].IsNull;
        SetChain(ShipSettings.Edit(g), new[] { TargetRule.AttackingFlagship, TargetRule.DebuffedFirst, TargetRule.Nearest });
        ShipSettings.Save(g);
        var snap = ShipSettings.Snapshot(g);
        SetChain(ShipSettings.Edit(g), new[] { TargetRule.HighestLevel });
        ShipSettings.Restore(g, snap);
        string restored = Chain(ShipSettings.Get(g).targetRules);
        string legacyDetail = $"legacy={legacyChain} rewritten={rewritten} loadout={restored}";
        if (legacyChain == "MostDamage" && rewritten && restored == "AttackingFlagship>DebuffedFirst>Nearest") ctx.Pass("settings-legacy", legacyDetail);
        else ctx.Fail("settings-legacy", legacyDetail);
        ShipSettings.Restore(g, full);
        e = ShipSettings.Edit(g);
        SetChain(e, new[] { TargetRule.LowestHull });
        e.repickSeconds = 2f;
        e.finishBelow = 25;
        e.maxLevel = 0;
        e.leash = 0f;
        e.weaponRules.Clear();
        ShipSettings.Save(g);

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
            var picked = TargetRanking.Pick(module, candidates, TargetRanking.Single(TargetRule.LowestHull), Resolve(flagship), validate: true);
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

        ChainChecks(ctx, module, candidates, flagship);

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
            SetChain(we, new[] { TargetRule.Nearest });
            we.repickSeconds = 0f;
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

        // "Attacking my flagship" also counts an enemy that has the flagship as its target before it lands a hit.
        // The spawned enemies may have hit the flagship already, so the two picked here leave its hit tracker for the
        // test and get their entries back afterwards. One drops its target, the other aims at the flagship.
        AbstractUnit calm = null, aimer = null;
        foreach (var c in candidates)
        {
            if (!(c is AbstractUnit cu) || cu.isDestroyed || cu.targetProvider == null) continue;
            if (calm == null) calm = cu;
            else { aimer = cu; break; }
        }
        if (aimer == null) ctx.Skip("flagship-aim", "needs two live enemies with a targeting module");
        else
        {
            var hits = (IDictionary)AccessTools.Field(typeof(TargetableUnit), "targetedBy").GetValue(ship);
            var calmHit = hits.Contains(calm) ? hits[calm] : null;
            var aimerHit = hits.Contains(aimer) ? hits[aimer] : null;
            string aimDetail;
            bool aimOk;
            try
            {
                hits.Remove(calm);
                hits.Remove(aimer);
                calm.targetProvider.ResetCurrentTargets();
                aimer.SetManualTarget(ship);
                var aimed = aimer.targetProvider.priorityTarget;
                var fr = Resolve(flagship);
                var flagshipChain = TargetRanking.Single(TargetRule.AttackingFlagship);
                var aimPick = TargetRanking.Pick(module, new List<TargetableUnit> { calm, aimer }, flagshipChain, fr, validate: true);
                bool better = TargetRanking.ClearlyBetter(module.transform.position, aimer, calm, flagshipChain, fr);
                aimDetail = $"aimer={Name(aimer)} aims={Name(aimed)} calm={Name(calm)} calmAims={Name(calm.targetProvider.priorityTarget)} picked={Name(aimPick)} better={better}";
                aimOk = aimed == ship && aimPick == aimer && better;
            }
            finally
            {
                if (calmHit != null) hits[calm] = calmHit;
                if (aimerHit != null) hits[aimer] = aimerHit;
            }
            if (aimOk) ctx.Pass("flagship-aim", aimDetail);
            else ctx.Fail("flagship-aim", aimDetail);
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
        var wsaved = (wb.repickSeconds, wb.finishBelow);
        var wchain = new List<TargetRule>(wb.targetRules);
        var w = ShipSettings.Edit(wg);
        (w.repickSeconds, w.finishBelow) = (1f, 0);
        SetChain(w, new[] { TargetRule.LowestHull });
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
            (w.repickSeconds, w.finishBelow) = wsaved;
            SetChain(w, wchain);
            ShipSettings.Save(wg);
            GamePlayer.current.waypoints.AddRange(route);
        }
        if (switched) ctx.Pass("live-fleet", liveDetail);
        else if (weak == null || weak.isDestroyed) ctx.Skip("live-fleet", "the weakened enemy died first: " + liveDetail);
        else ctx.Fail("live-fleet", liveDetail);

        // A hit on the flagship moves an "Attacking my flagship" ship onto the attacker with Re-check at 0; a hit on
        // another ship doesn't. The fleet ship stands in for that other ship (vanilla never calls it on itself, but
        // the prefix only looks at which unit was hit).
        AbstractUnit startOn = null, attacker = null;
        foreach (var c in candidates)
        {
            if (!(c is AbstractUnit cu) || cu.isDestroyed || !wingModule.IsValidTarget(cu)) continue;
            if (startOn == null && !TargetRanking.AttacksFlagship(cu, ship)) { startOn = cu; continue; }
            if (attacker == null) attacker = cu;
        }
        if (startOn == null || attacker == null)
        {
            ctx.Skip("flagship-assist", $"needs a live enemy not attacking the flagship and a second one (start={Name(startOn)} attacker={Name(attacker)})");
            yield break;
        }
        var ab = ShipSettings.Get(wg);
        var asaved = (ab.repickSeconds, ab.finishBelow, ab.maxLevel, ab.leash);
        var achain = new List<TargetRule>(ab.targetRules);
        var aw = ShipSettings.Edit(wg);
        (aw.repickSeconds, aw.finishBelow, aw.maxLevel, aw.leash) = (0f, 0, 0, 0f);
        SetChain(aw, new[] { TargetRule.AttackingFlagship });
        string assistDetail, lowerDetail;
        bool assisted, lowerHeld;
        try
        {
            var assist = AccessTools.Method(typeof(AbstractUnit), "AssistPlayerAgainstAttacker");
            wingModule.ResetCurrentTargets();
            wingModule.SetAutoTarget(startOn);
            // A hit on the flagship, as the TakeDamage prefix records it.
            TargetRanking.RecordHit(attacker, ship, 0f);
            assist.Invoke(wingData.unit, new object[] { wingData.unit, attacker });
            var afterOther = wingModule.autoTarget;
            assist.Invoke(ship, new object[] { wingData.unit, attacker });
            var afterFlagship = wingModule.autoTarget;
            assistDetail = $"start={Name(startOn)} attacker={Name(attacker)} afterOtherHit={Name(afterOther)} afterFlagshipHit={Name(afterFlagship)}";
            assisted = afterOther == startOn && afterFlagship == attacker;

            // Lower in the chain, "Attacking my flagship" only breaks ties: the same hit on the flagship moves nobody.
            SetChain(aw, new[] { TargetRule.Nearest, TargetRule.AttackingFlagship });
            wingModule.ResetCurrentTargets();
            wingModule.SetAutoTarget(startOn);
            assist.Invoke(ship, new object[] { wingData.unit, attacker });
            lowerDetail = $"chain={Chain(aw.targetRules)} start={Name(startOn)} afterFlagshipHit={Name(wingModule.autoTarget)}";
            lowerHeld = wingModule.autoTarget == startOn;
        }
        finally
        {
            (aw.repickSeconds, aw.finishBelow, aw.maxLevel, aw.leash) = asaved;
            SetChain(aw, achain);
            ShipSettings.Save(wg);
        }
        if (assisted) ctx.Pass("flagship-assist", assistDetail);
        else ctx.Fail("flagship-assist", assistDetail);
        if (lowerHeld) ctx.Pass("flagship-assist-lower", lowerDetail);
        else ctx.Fail("flagship-assist-lower", lowerDetail);

        // A hit on another of your ships doesn't make its attacker an attacker of the flagship (the game's own
        // trackers did: BroadcastThreatToAllies copies every hit to allies within 80 u).
        AbstractUnit ally = null;
        foreach (var c in candidates)
            if (c is AbstractUnit cu && cu != startOn && cu != attacker && !cu.isDestroyed && wingModule.IsValidTarget(cu)
                && !TargetRanking.AttacksFlagship(cu, ship)) { ally = cu; break; }
        if (ally == null) ctx.Skip("flagship-assist-ally", "needs a third live enemy not attacking the flagship");
        else
        {
            TargetRanking.RecordHit(ally, wingData.unit, 10f);
            bool attacks = TargetRanking.AttacksFlagship(ally, ship);
            string allyDetail = $"hit on {Name(wingData.unit)} by {Name(ally)}: attacksFlagship={attacks}";
            if (!attacks) ctx.Pass("flagship-assist-ally", allyDetail);
            else ctx.Fail("flagship-assist-ally", allyDetail);
        }
    }

    private static string Name(TargetableUnit t) => t == null ? "none" : t.targetName + "#" + t.GetHashCode();

    private static string Chain(List<TargetRule> rules) => string.Join(">", rules);

    private static void SetChain(ShipSettings s, IEnumerable<TargetRule> rules)
    {
        s.targetRules.Clear();
        s.targetRules.AddRange(rules);
    }

    private const string CheckBoost = "igiqol-selfcheck-";

    // Rule chains on two of the spawned enemies: a gap inside a rule's margin falls through to the next rule, a real
    // gap doesn't, and the new rules' guard turns a small lead into a tie that the nearest enemy wins.
    private void ChainChecks(SelfCheckContext ctx, CombatModule module, List<TargetableUnit> candidates, SpaceShipData flagship)
    {
        var near = candidates[0] as AbstractUnit;
        var far = candidates[candidates.Count - 1] as AbstractUnit;
        if (near == null || far == null || near == far || near.unitData == null || far.unitData == null)
        {
            ctx.Skip("chain-tie", "needs two spawned enemy units");
            return;
        }
        // Enemies move; order the two by distance now (nothing below waits a frame).
        Vector2 at = module.transform.position;
        if (Vector2.Distance(at, far.transform.position) < Vector2.Distance(at, near.transform.position)) (near, far) = (far, near);
        var pair = new List<TargetableUnit> { near, far };
        var r = Resolve(flagship);
        float nearHull = near.currentHullHP, farHull = far.currentHullHP;
        float nearShield = near.currentShieldHP, farShield = far.currentShieldHP;
        int weakestGuard = WeakestGuardPercent.Value, debuffedGuard = DebuffedGuardPercent.Value;
        WeakestGuardPercent.Value = 25;
        DebuffedGuardPercent.Value = 25;
        try
        {
            // Hull 45% vs 50% is inside the 10-point margin: "Debuffed first" decides, and the far one, given one debuff
            // more than the near one, wins. 25% vs 50% is a real gap: the first rule keeps the near one. The fleet may
            // have debuffed either already (painter drones), so the counts are set relative to what they carry.
            var chain = new List<TargetRule> { TargetRule.LowestHull, TargetRule.DebuffedFirst };
            near.currentHullHP = near.maxHullHP * 0.45f;
            far.currentHullHP = far.maxHullHP * 0.5f;
            DebuffTo(far, Effects.AllDebuffs(near) + 1);
            var tie = TargetRanking.Pick(module, pair, chain, r, validate: true);
            bool switchOnTie = TargetRanking.ClearlyBetter(module.transform.position, far, near, chain, r);
            var alone = TargetRanking.Pick(module, pair, TargetRanking.Single(TargetRule.LowestHull), r, validate: true);
            near.currentHullHP = near.maxHullHP * 0.25f;
            var real = TargetRanking.Pick(module, pair, chain, r, validate: true);
            string tieDetail = $"debuffs near={Effects.AllDebuffs(near)} far={Effects.AllDebuffs(far)}; tie->{Name(tie)} switch={switchOnTie}, hull alone->{Name(alone)}, real gap->{Name(real)}; near={Name(near)} far={Name(far)}";
            if (tie == far && switchOnTie && alone == near && real == near) ctx.Pass("chain-tie", tieDetail);
            else ctx.Fail("chain-tie", tieDetail);

            // The scan postfix walks the ship's own chain on a re-check.
            near.currentHullHP = near.maxHullHP * 0.45f;
            var e = ShipSettings.Edit(flagship.guid);
            SetChain(e, chain);
            module.ResetCurrentTargets();
            module.SetAutoTarget(near);
            TargetPriorityPatches.Reset();
            module.UpdateAvailableTargets(pair);
            string repickDetail = $"chain={Chain(e.targetRules)} from={Name(near)} autoTarget={Name(module.autoTarget)} far={Name(far)}";
            if (module.autoTarget == far) ctx.Pass("chain-repick", repickDetail);
            else ctx.Fail("chain-repick", repickDetail);
            SetChain(e, new[] { TargetRule.LowestHull });
            module.ResetCurrentTargets();
            near.currentHullHP = nearHull;
            far.currentHullHP = farHull;

            // "Debuffed first" guard at 25%: 10 debuffs against 8 is no lead (the nearest wins), 11 against 8 is.
            RemoveDebuffs(far);
            int nearBase = Effects.AllDebuffs(near), farBase = Effects.AllDebuffs(far);
            if (nearBase > 8 || farBase > 10) ctx.Skip("guard-debuffed", $"the enemies already carry {nearBase} and {farBase} debuffs");
            else
            {
                DebuffTo(near, 8);
                DebuffTo(far, 10);
                var debuffChain = TargetRanking.Single(TargetRule.DebuffedFirst);
                var small = TargetRanking.Pick(module, pair, debuffChain, r, validate: true);
                string counts = $"{Effects.AllDebuffs(near)} vs {Effects.AllDebuffs(far)}";
                DebuffTo(far, 11);
                var big = TargetRanking.Pick(module, pair, debuffChain, r, validate: true);
                string debuffDetail = $"{counts} -> {Name(small)}, {Effects.AllDebuffs(near)} vs {Effects.AllDebuffs(far)} -> {Name(big)}; near={Name(near)} far={Name(far)}";
                if (small == near && big == far) ctx.Pass("guard-debuffed", debuffDetail);
                else ctx.Fail("guard-debuffed", debuffDetail);
            }
            RemoveDebuffs(near);
            RemoveDebuffs(far);

            // "Weakest to my weapons" guard at 25%: lower the far one's damage reduction until the guns' score on it
            // leads by 10% (a tie: the nearest wins), then by 50% (it wins). Both on full shields, so a hit lands on
            // the same layer.
            float dps = 0f;
            foreach (var t in module.turrets)
                if (t != null) dps += t.GetExpectedDps();
            if (dps <= 0f)
            {
                ctx.Skip("guard-weakest", "the flagship's guns have no expected DPS");
                return;
            }
            near.currentShieldHP = near.maxShieldHP;
            far.currentShieldHP = far.maxShieldHP;
            var weakestChain = TargetRanking.Single(TargetRule.WeakestToMyWeapons);
            TargetRanking.Prepare(weakestChain, module, null);
            float nearScore = TargetRanking.WeaponScore(near), farScore = TargetRanking.WeaponScore(far);
            far.unitData.SetStatBoost(CheckBoost + "dr", "selfcheck", EquipStat.DamageReduction, -(1.1f * nearScore - farScore) / dps, 1f);
            var lead10 = TargetRanking.Pick(module, pair, weakestChain, r, validate: true);
            float score10 = TargetRanking.WeaponScore(far);
            far.unitData.SetStatBoost(CheckBoost + "dr", "selfcheck", EquipStat.DamageReduction, -(1.5f * nearScore - farScore) / dps, 1f);
            var lead50 = TargetRanking.Pick(module, pair, weakestChain, r, validate: true);
            float score50 = TargetRanking.WeaponScore(far);
            string weakDetail = $"dps={dps:0.0} near={nearScore:0.0} far={farScore:0.0} -> {score10:0.0}: {Name(lead10)}, -> {score50:0.0}: {Name(lead50)}; near={Name(near)} far={Name(far)}";
            if (lead10 == near && lead50 == far) ctx.Pass("guard-weakest", weakDetail);
            else ctx.Fail("guard-weakest", weakDetail);
        }
        finally
        {
            WeakestGuardPercent.Value = weakestGuard;
            DebuffedGuardPercent.Value = debuffedGuard;
            near.currentHullHP = nearHull;
            far.currentHullHP = farHull;
            near.currentShieldHP = nearShield;
            far.currentShieldHP = farShield;
            RemoveDebuffs(near);
            RemoveDebuffs(far);
            far.unitData.RemoveStatBoost(CheckBoost + "dr");
            module.ResetCurrentTargets();
        }
    }

    private const int MaxCheckDebuffs = 20;

    // Adds outside stat penalties (a tiny thrust cut, which "Debuffed first" counts) until the unit carries this many.
    private static void DebuffTo(AbstractUnit unit, int total)
    {
        for (int i = 0; i < MaxCheckDebuffs && Effects.AllDebuffs(unit) < total; i++)
            unit.unitData.SetStatBoost(CheckBoost + i, "selfcheck", EquipStat.Thrust, -0.01f, 1f);
    }

    private static void RemoveDebuffs(AbstractUnit unit)
    {
        for (int i = 0; i < MaxCheckDebuffs; i++) unit.unitData.RemoveStatBoost(CheckBoost + i);
    }
}

internal static class TargetRanking
{
    // Hits on your ships, kept by the mod: the game's targetedBy trackers also hold whoever hit any ally within 80 u
    // (AbstractUnit.BroadcastThreatToAllies), which made every enemy in a fleet fight an "attacker" of the flagship.
    private struct Hit { public float flagshipUntil, until, damage; }
    private static readonly Dictionary<AbstractUnit, Hit> hits = new();
    private static readonly List<AbstractUnit> deadHits = new();
    // The game's own tracker window per hit (TargetableUnit.AddTargetedBy adds 3 s).
    private const float HitSeconds = 3f;
    // The guns' expected DPS by damage type (indexed by DamageType), for "Weakest to my weapons".
    private static readonly float[] dpsByType = new float[Resists.Types.Length];
    private static readonly float[] candidateKeys = new float[ShipSettings.MaxChain];
    private static readonly float[] bestKeys = new float[ShipSettings.MaxChain];
    private static readonly List<TargetRule> single = new() { TargetRule.Nearest };

    // A one-rule chain for a weapon rule. Reused: valid until the next call.
    public static List<TargetRule> Single(TargetRule rule)
    {
        single[0] = rule;
        return single;
    }

    // Gathers what the chain's rules read once per pick: the guns' DPS for "Weakest to my weapons" (the turret's
    // alone with a weapon rule, else the ship's).
    public static void Prepare(List<TargetRule> chain, AbstractTargetingModule module, AbstractTurret turret)
    {
        if (!Has(chain, TargetRule.WeakestToMyWeapons)) return;
        Array.Clear(dpsByType, 0, dpsByType.Length);
        if (turret != null) AddDps(turret);
        else if (module.turrets != null)
            foreach (var t in module.turrets)
                if (t != null) AddDps(t);
    }

    private static void AddDps(AbstractTurret turret)
    {
        int type = (int)turret.damageType;
        if (type >= 0 && type < dpsByType.Length) dpsByType[type] += turret.GetExpectedDps();
    }

    // Best candidate under the chain, or null. Lower keys rank first. A difference within a rule's margin is a tie
    // that the next rule decides, and the nearest wins a tie on every rule.
    // Candidates above the level cap only win when nothing else is left; candidates past the leash never do.
    // With a turret, only enemies that weapon can hit and has in range count, and distance is measured from it.
    public static TargetableUnit Pick(AbstractTargetingModule module, List<TargetableUnit> candidates, List<TargetRule> chain, TargetPriority.Resolved r, bool validate, AbstractTurret turret = null)
    {
        Prepare(chain, module, turret);
        var flagship = Fleet.Flagship;
        bool leashed = turret == null && r.leash > 0f && flagship != null && module.parent != flagship && module.parent.autoActions is WingmanActions;
        Vector2 from = turret != null ? turret.transform.position : module.transform.position;

        TargetableUnit best = null;
        int bestTier = 0;
        float bestDist = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            var t = candidates[i];
            if (t == null || (validate && !module.IsValidTarget(t))) continue;
            if (turret != null && !InReach(turret, t)) continue;
            if (leashed && Vector2.Distance(t.transform.position, flagship.transform.position) > r.leash) continue;
            float dist = Vector2.Distance(from, t.transform.position);
            int tier = r.maxLevel > 0 && t is AbstractUnit u && u.level > r.maxLevel ? 1 : 0;
            if (best != null && tier > bestTier) continue;
            Keys(chain, t, dist, flagship, candidateKeys);
            int c = best == null || tier < bestTier ? -1 : Compare(chain, candidateKeys, bestKeys);
            if (c < 0 || (c == 0 && dist < bestDist))
            {
                best = t;
                bestTier = tier;
                bestDist = dist;
                Array.Copy(candidateKeys, bestKeys, chain.Count);
            }
        }
        return best;
    }

    private static void Keys(List<TargetRule> chain, TargetableUnit t, float dist, AbstractUnit flagship, float[] keys)
    {
        for (int i = 0; i < chain.Count; i++) keys[i] = Key(chain[i], t, dist, flagship);
    }

    // -1 when a beats b on the first rule that sees more than its margin of difference, 1 when b does, 0 on a tie.
    private static int Compare(List<TargetRule> chain, float[] a, float[] b)
    {
        for (int i = 0; i < chain.Count; i++)
        {
            float m = Margin(chain[i], b[i]);
            if (a[i] < b[i] - m) return -1;
            if (a[i] > b[i] + m) return 1;
        }
        return 0;
    }

    // The weapon can damage the target and the target's hull edge is within its range.
    public static bool InReach(AbstractTurret turret, TargetableUnit t) =>
        turret.CanTarget(t) && Vector2.Distance(turret.transform.position, t.transform.position) - t.radius <= turret.range;

    // True when the candidate beats the current target by enough to be worth switching. Uses what the last Pick or
    // Prepare for this chain gathered.
    public static bool ClearlyBetter(Vector2 from, TargetableUnit candidate, TargetableUnit current, List<TargetRule> chain, TargetPriority.Resolved r)
    {
        var flagship = Fleet.Flagship;
        if (r.maxLevel > 0)
        {
            int ct = candidate is AbstractUnit cu && cu.level > r.maxLevel ? 1 : 0;
            int pt = current is AbstractUnit pu && pu.level > r.maxLevel ? 1 : 0;
            if (ct != pt) return ct < pt;
        }
        Keys(chain, candidate, Vector2.Distance(from, candidate.transform.position), flagship, candidateKeys);
        Keys(chain, current, Vector2.Distance(from, current.transform.position), flagship, bestKeys);
        return Compare(chain, candidateKeys, bestKeys) < 0;
    }

    public static bool BelowFinish(TargetableUnit t, int finishBelow) =>
        finishBelow > 0 && t is AbstractUnit u && u.maxHullHP > 0f && u.currentHullHP / u.maxHullHP * 100f < finishBelow;

    private static float Margin(TargetRule rule, float current) => rule switch
    {
        TargetRule.Nearest => Mathf.Max(2f, current * 0.2f),
        TargetRule.LowestHull or TargetRule.HighestHull => 0.1f,
        TargetRule.MostDamage => Mathf.Abs(current) * 0.25f + 1f,
        TargetRule.WeakestToMyWeapons => Mathf.Abs(current) * TargetPriority.Instance.WeakestGuardPercent.Value / 100f,
        TargetRule.DebuffedFirst => Mathf.Abs(current) * TargetPriority.Instance.DebuffedGuardPercent.Value / 100f,
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
            case TargetRule.AttackingFlagship: return AttacksFlagship(u, flagship) ? 0f : 1f;
            case TargetRule.SmallestFirst: return Size(t);
            case TargetRule.LargestFirst: return -Size(t);
            case TargetRule.LowestLevel: return u != null ? u.level : 9999f;
            case TargetRule.HighestLevel: return u != null ? -u.level : 0f;
            case TargetRule.WeakestToMyWeapons: return u != null ? -WeaponScore(u) : 0f;
            case TargetRule.DebuffedFirst: return u != null ? -Effects.AllDebuffs(u) : 0f;
            default: return dist;
        }
    }

    // The guns' expected DPS after this enemy's resistances, on the layer a hit lands on now: shields have no
    // per-type resistance, armour adds its own step.
    internal static float WeaponScore(AbstractUnit enemy)
    {
        bool armor = enemy.currentShieldHP <= 0f && Resists.HasArmor(enemy) && enemy.currentArmorHP > 0f;
        float score = 0f;
        for (int i = 0; i < dpsByType.Length; i++)
        {
            if (dpsByType[i] <= 0f) continue;
            var type = Resists.Types[i];
            float m = Resists.Multiplier(enemy, type);
            if (armor) m *= Resists.ArmorMultiplier(enemy, type);
            score += dpsByType[i] * m;
        }
        return score;
    }

    // List.Contains on an enum boxes on this runtime.
    private static bool Has(List<TargetRule> chain, TargetRule rule)
    {
        for (int i = 0; i < chain.Count; i++)
            if (chain[i] == rule) return true;
        return false;
    }

    // Hit the flagship in the last 3 s (the mod's own record), or has it as its target. Enemy combat ships aim from
    // their threat table (CombatActions.ChooseCombatTarget), so this also catches attackers that haven't landed a hit yet.
    internal static bool AttacksFlagship(AbstractUnit u, AbstractUnit flagship)
    {
        if (flagship == null || u == null) return false;
        if (hits.TryGetValue(u, out var h) && h.flagshipUntil > Time.time) return true;
        var provider = u.targetProvider;
        return provider != null && provider.priorityTarget == flagship;
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

    // Damage this enemy dealt to your flagship and fleet ships in the last 3 s, from the mod's own hit record.
    private static float DamageToFleet(AbstractUnit enemy) =>
        hits.TryGetValue(enemy, out var h) && h.until > Time.time ? h.damage : 0f;

    internal static void RecordHit(AbstractUnit attacker, AbstractUnit target, float damage)
    {
        if (attacker == null || target == null) return;
        float now = Time.time;
        hits.TryGetValue(attacker, out var h);
        if (h.until < now) h.damage = 0f;
        h.damage += damage;
        h.until = now + HitSeconds;
        if (target == Fleet.Flagship) h.flagshipUntil = now + HitSeconds;
        hits[attacker] = h;
        if (hits.Count > 64) PruneHits(now);
    }

    private static void PruneHits(float now)
    {
        deadHits.Clear();
        foreach (var kv in hits)
            if (kv.Key == null || kv.Value.until < now) deadHits.Add(kv.Key);
        foreach (var k in deadHits) hits.Remove(k);
    }

    internal static void ResetHits() => hits.Clear();
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
        TargetRanking.ResetHits();
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
            __result = TargetRanking.Pick(__instance, __instance.filteredTargets, r.rules, r, validate: false);
            return false;
        }
        catch (Exception e)
        {
            feature.Fail(e, nameof(TargetPriorityPatches));
            return true;
        }
    }

    // Who hit your ships, for "Attacking my flagship" and "Most damage to my fleet". A prefix: TakeDamage ends by
    // broadcasting the hit to allies (AssistPlayerAgainstAttacker), and the assist must already see it.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AbstractUnit), nameof(AbstractUnit.TakeDamage))]
    private static void TakeDamagePrefix(AbstractUnit __instance, DamageData damageData)
    {
        var feature = TargetPriority.Instance;
        if (!feature.Active) return;
        try
        {
            var source = damageData != null ? damageData.sourceUnit : null;
            if (source == null || __instance.isDestroyed || !Fleet.IsPlayerShip(__instance)) return;
            TargetRanking.RecordHit(source, __instance, damageData.damageAmount);
        }
        catch (Exception e) { feature.Fail(e, nameof(TargetPriorityPatches)); }
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
                var pick = TargetRanking.Pick(module, list, r.rules, r, validate: true);
                if (pick != null) module.SetAutoTarget(pick);
            }
            return;
        }

        if (r.repick <= 0f || list == null) return;
        float now = Time.time;
        if (nextRepick.TryGetValue(module, out var due) && now < due) return;
        nextRepick[module] = now + r.repick;

        if (TargetRanking.BelowFinish(current, r.finishBelow)) return;
        var best = TargetRanking.Pick(module, list, r.rules, r, validate: true);
        if (best == null || best == current || !TargetRanking.ClearlyBetter(module.transform.position, best, current, r.rules, r)) return;
        module.SetAutoTarget(best);
        var bay = module.torpedoBayModule;
        if (bay != null) bay.AddManualTarget(best, overrideTarget: false);
    }

    // Weapons with a rule pick among enemies they can hit and have in range; without one (or with nothing in reach)
    // they follow the ship's target. Picks only exist while the ship itself is fighting.
    private static void UpdateWeaponPicks(CombatModule module, List<TargetableUnit> list, TargetPriority.Resolved r)
    {
        var turrets = module.turrets;
        if (turrets == null) return;
        bool engaged = module.priorityTarget != null && !module.blocked && !module.holdFire && !ManualWins(module);
        float now = Time.time;
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
            var chain = TargetRanking.Single(rule.rule);
            var best = TargetRanking.Pick(module, list, chain, r, validate: true, turret);
            if (best == null)
            {
                if (!keep) WeaponPicks.Remove(turret);
                continue;
            }
            if (keep && best != current && !TargetRanking.ClearlyBetter(turret.transform.position, best, current, chain, r))
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

    // Vanilla moves a player ship's auto target to whoever just hit a nearby ally (__instance, the ally that was hit).
    // With a chain other than Nearest alone, the chain decides instead; a ship without a target still answers the
    // attack. A chain that starts with "Attacking my flagship" still answers hits on the flagship, so a ship defends it
    // without a re-check timer.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AbstractUnit), "AssistPlayerAgainstAttacker")]
    private static bool AssistPrefix(AbstractUnit __instance, AbstractUnit playerShip, AbstractUnit attacker)
    {
        var feature = TargetPriority.Instance;
        if (!feature.Active) return true;
        try
        {
            var module = playerShip != null ? playerShip.GetModule<CombatModule>() : null;
            if (module == null || module.autoTarget == null) return true;
            if (!Resolve(module, out var r) || r.NearestOnly) return true;
            // Only a chain that starts with "Attacking my flagship" answers the hit; lower down that rule only breaks ties.
            return r.rules[0] == TargetRule.AttackingFlagship && attacker != null && __instance == Fleet.Flagship && FlagshipAssist(module, attacker, r);
        }
        catch (Exception e)
        {
            feature.Fail(e, nameof(TargetPriorityPatches));
            return true;
        }
    }

    // The same limits as a re-pick: stay on a target being finished, never past the leash, and only switch when the
    // attacker ranks clearly better (so a ship already on another flagship attacker stays on it).
    private static bool FlagshipAssist(CombatModule module, AbstractUnit attacker, TargetPriority.Resolved r)
    {
        var current = module.autoTarget;
        if (TargetRanking.BelowFinish(current, r.finishBelow)) return false;
        var flagship = Fleet.Flagship;
        if (r.leash > 0f && module.parent != flagship && module.parent.autoActions is WingmanActions
            && Vector2.Distance(attacker.transform.position, flagship.transform.position) > r.leash) return false;
        TargetRanking.Prepare(r.rules, module, null);
        return TargetRanking.ClearlyBetter(module.transform.position, attacker, current, r.rules, r);
    }
}
