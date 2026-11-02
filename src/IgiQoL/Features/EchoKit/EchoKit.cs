using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Ability;
using Behaviour.Ability.Payload;
using Behaviour.Equipment.Module;
using Behaviour.Gameplay;
using Behaviour.Item;
using Behaviour.Item.Usable;
using Behaviour.Managers;
using Behaviour.Mining;
using Behaviour.Salvage;
using Behaviour.Tractoring;
using Behaviour.UI.Spacestation;
using Behaviour.Unit;
using Behaviour.Util;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using LightJson;
using Source.Data.Persistable;
using Source.Galaxy;
using Source.Item;
using Source.Player;
using Source.Util;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.EchoKit;

public enum KitWhen { Off, InCombat, ShieldBelow, ArmourBelow, HullBelow, TargetedBy, Ready }

public sealed class KitRule
{
    public KitWhen when;
    public int value = 50;
}

// ECHO (and optionally you) fires the flagship's abilities and drops deployables from its cargo by rule, then picks
// the turrets it dropped back up once the fight is over.
public sealed class EchoKit : Feature
{
    public static EchoKit Instance;

    public EchoKit() { Instance = this; }

    public override string Id => "EchoKit";
    public override string Title => "ECHO abilities and deployables";
    public override string Description => "Lets ECHO use your flagship's abilities and the deployables in its cargo (turrets, shield pylons, decoys, mines) by rules you set on the ECHO tab, and pick its turrets back up after the fight. Every rule starts off.";
    public override string SettingsTab => "ECHO";

    public override Type[] PatchClasses => new[] { typeof(EchoKitPatches) };

    public ConfigEntry<bool> AlsoWithoutEcho;
    public ConfigEntry<bool> PickUpTurrets;

    public static readonly Dictionary<string, KitRule> Abilities = new();
    public static readonly Dictionary<string, KitRule> Deployables = new();

    private static readonly HashSet<string> ourToggles = new();
    private static readonly HashSet<DefensiveTurret> deployed = new();
    private static float nextDeploy, quietSince = -1f;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        AlsoWithoutEcho = cfg.Bind(Id, "AlsoWithoutEcho", false, SettingInfo.Describe("Also while you fly",
            "Applies the ability and deployable rules below when ECHO is off too, so the flagship uses them while you play."));
        PickUpTurrets = cfg.Bind(Id, "PickUpTurrets", true, SettingInfo.Describe("Pick deployed turrets back up",
            "Five seconds after a fight ends, the flagship tractors back the turrets these rules dropped, so they aren't left behind when ECHO flies on."));
    }

    // The game's asteroid and wreck abilities (from the skill trees), and a salvage one aimed at a wreck spawned next to
    // the flagship: picked while its credits (or lootbox) are there, passed over once they're taken.
    private static IEnumerator CheckWreckTarget(SelfCheckContext ctx, SpaceShip ship)
    {
        var found = new List<ActivatedAbility>();
        var seen = new HashSet<ActivatedAbility>();
        void Consider(ActivatedAbility aa)
        {
            if (aa != null && (aa.targetType == AbilityTargetType.Asteroid || aa.targetType == AbilityTargetType.Salvage) && seen.Add(aa)) found.Add(aa);
        }
        foreach (var node in Behaviour.Crew.SkilltreeNode.crewNodes.Values)
            foreach (var ab in node.abilities)
                if (ab != null) Consider(ab.GetComponent<ActivatedAbility>());
        foreach (var aa in Resources.FindObjectsOfTypeAll<ActivatedAbility>()) Consider(aa);
        string names = string.Join(", ", found.ConvertAll(a => $"{Translation.Translate(a.displayName)} ({a.targetType}, payload {(a.payload != null ? a.payload.name : "none")})"));
        var salvager = found.Find(a => a.targetType == AbilityTargetType.Salvage && a.payload != null && a.payload.TryGetComponent<SalvageCreditDrone>(out _));
        var poi = MapPointOfInterest.current;
        if (salvager == null || poi == null)
        {
            ctx.Skip("wreck-target", "no salvage ability with a credit drone or lootbox bot payload; found: " + names);
            yield break;
        }
        int level = Mathf.Max(1, ship.spaceShipData.level);
        var data = new SalvageData
        {
            position = (Vector2)ship.transform.position + Vector2.right * 4f,
            angle = 0,
            shipTemplate = poi.FindSalvageShipTemplate(Faction.marauders),
        };
        data.AddScrapContent(level, 0.5f);
        data.AddStructuralContent(level);
        var go = poi.AddPersistable(data);
        try
        {
            yield return null;
            SalvageContainer spawned = null;
            foreach (var c in SalvageContainer.all)
                if (c != null && c.data == data) spawned = c;
            bool lootbox = salvager.payload.TryGetComponent<SalvageCargoLootboxBot>(out _);
            var first = WreckFor(ship.transform.position, salvager);
            if (lootbox) data.lootboxExtracted = 1;
            else data.creditsExtracted = 1;
            var after = WreckFor(ship.transform.position, salvager);
            string detail = $"{Translation.Translate(salvager.displayName)} ({(lootbox ? "lootbox bot" : "credit drone")}): fresh wreck -> {(first == spawned ? "it" : first == null ? "none" : first.name)}, after extraction -> {(after == spawned ? "it" : after == null ? "none" : after.name)}; abilities with asteroid or wreck targets: {names}";
            if (spawned != null && first == spawned && after != spawned) ctx.Pass("wreck-target", detail);
            else ctx.Fail("wreck-target", detail);
        }
        finally
        {
            poi.RemovePersistable(data);
            if (go != null) Object.Destroy(go);
        }
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        // Rules survive a save round trip.
        SetRule(Abilities, "igi-check", KitWhen.ShieldBelow, 40);
        Load();
        var back = Rule(Abilities, "igi-check");
        if (back != null && back.when == KitWhen.ShieldBelow && back.value == 40) ctx.Pass("roundtrip", "ShieldBelow 40");
        else ctx.Fail("roundtrip", back == null ? "rule lost" : $"{back.when} {back.value}");
        SetRule(Abilities, "igi-check", KitWhen.Off, 0);

        var calm = new ShipState { fighting = false, shield = 0.1f, armour = 1f, hull = 1f, targetedBy = 5 };
        var hot = new ShipState { fighting = true, shield = 0.3f, armour = 0.9f, hull = 1f, targetedBy = 2 };
        bool logic = !Holds(new KitRule { when = KitWhen.ShieldBelow, value = 50 }, calm)
            && Holds(new KitRule { when = KitWhen.ShieldBelow, value = 50 }, hot)
            && !Holds(new KitRule { when = KitWhen.ArmourBelow, value = 50 }, hot)
            && Holds(new KitRule { when = KitWhen.TargetedBy, value = 2 }, hot)
            && !Holds(new KitRule { when = KitWhen.TargetedBy, value = 3 }, hot)
            && Holds(new KitRule { when = KitWhen.InCombat }, hot)
            && Holds(new KitRule { when = KitWhen.Ready }, calm);
        if (logic) ctx.Pass("rules", "shield/armour/targeted/in-combat rules hold only in a fight and past their threshold; whenever-ready holds outside one");
        else ctx.Fail("rules", "a rule fired when it shouldn't or didn't when it should");

        UI.ModWindow.Open("echo");
        yield return null;
        var scroll = UI.ModWindow.IsOpen ? UI.ModWindow.Content.GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
        if (scroll != null) scroll.verticalNormalizedPosition = 0f;
        yield return ctx.Screenshot("window-echo-kit");
        UI.ModWindow.Close();

        yield return SelfCheckContext.EnsureInSpace();
        if (!Allowed(out var ship) && (ship = Fleet.Flagship) == null || MapPointOfInterest.current == null || SpaceStationInterior.instance != null)
        {
            ctx.Skip("ability", "not in space");
            yield break;
        }
        yield return CheckWreckTarget(ctx, ship);

        ActivatedAbility pick = null;
        foreach (var a in ship.GetActivatedAbilities())
            if (!a.toggledAbility && a.isReady && a.targetType == AbilityTargetType.None) { pick = a; break; }
        if (pick == null) ctx.Skip("ability", "the flagship has no ready untargeted ability");
        else
        {
            SetRule(Abilities, pick.identifier, KitWhen.InCombat, 50);
            int used = UseAbilities(ship, hot);
            string d = $"{Translation.Translate(pick.displayName)} ({pick.identifier}): used={used} ready after={pick.isReady} cooldown={pick.cooldownRemaining:0}";
            if (used == 1 && !pick.isReady) ctx.Pass("ability", d);
            else ctx.Fail("ability", d);
            SetRule(Abilities, pick.identifier, KitWhen.Off, 0);
        }

        // A deployable from cargo goes out, then comes back with the pick-up.
        InventoryItemType deployable = null;
        foreach (var t in InventoryItemType.all)
            if (t.TryGetComponent<ShieldPylonItem>(out _)) { deployable = t; break; }
        if (deployable == null)
            foreach (var t in InventoryItemType.all)
                if (t.TryGetComponent<DefensiveTurretItem>(out _)) { deployable = t; break; }
        if (deployable == null)
        {
            ctx.Skip("deploy", "no deployable item type");
            yield break;
        }
        var cargo = ship.spaceShipData.cargo;
        int before = cargo.GetCount(deployable);
        cargo.Add(deployable, 1);
        SetRule(Deployables, deployable.identifier, KitWhen.InCombat, 50);
        nextDeploy = 0f;
        var used2 = Deploy(ship, hot);
        yield return new WaitForSeconds(1.5f);
        DefensiveTurret placed = null;
        foreach (var turret in Object.FindObjectsByType<DefensiveTurret>(FindObjectsInactive.Exclude))
            if (turret.defensiveTurretData?.turretItem == deployable) placed = turret;
        string dd = $"{deployable.identifier}: deployed={(used2 != null)} cargo {before + 1}->{cargo.GetCount(deployable)} turret in space={(placed != null)}";
        if (used2 == deployable && cargo.GetCount(deployable) == before && placed != null) ctx.Pass("deploy", dd);
        else ctx.Fail("deploy", dd);
        SetRule(Deployables, deployable.identifier, KitWhen.Off, 0);

        if (placed == null)
        {
            ctx.Skip("pick-up", "nothing deployed");
            yield break;
        }
        int started = PickUp(ship);
        float until = Time.realtimeSinceStartup + 20f;
        while (placed != null && Time.realtimeSinceStartup < until) yield return new WaitForSeconds(0.5f);
        yield return new WaitForSeconds(1f);
        string pd = $"tractor started={started}, turret gone={(placed == null)}, cargo {cargo.GetCount(deployable)} (before deploy {before + 1})";
        if (started > 0 && placed == null && cargo.GetCount(deployable) == before + 1) ctx.Pass("pick-up", pd);
        else ctx.Fail("pick-up", pd);
        if (cargo.GetCount(deployable) > before) cargo.Remove(deployable, cargo.GetCount(deployable) - before);
    }

    public override void OnActivated()
    {
        GameEvents.SaveLoaded += OnSaveLoaded;
        EchoPriority.EchoPriority.EnsureTab();
        Plugin.Instance.StartCoroutine(Run());
    }

    // Some cargo items (builder-made equipment) have no identifier.
    public static KitRule Rule(Dictionary<string, KitRule> rules, string id) => id != null && rules.TryGetValue(id, out var r) ? r : null;

    public static void SetRule(Dictionary<string, KitRule> rules, string id, KitWhen when, int value)
    {
        if (when == KitWhen.Off) rules.Remove(id);
        else rules[id] = new KitRule { when = when, value = value };
        Save();
    }

    private static void OnSaveLoaded()
    {
        if (Instance.Active) Load();
    }

    private static void Load()
    {
        Abilities.Clear();
        Deployables.Clear();
        ourToggles.Clear();
        deployed.Clear();
        try
        {
            var record = SaveData.Feature(Instance.Id, false);
            if (record == null) return;
            Read(record["abilities"], Abilities);
            Read(record["deployables"], Deployables);
        }
        catch (Exception e) { Instance.Fail(e, nameof(Load)); }
    }

    private static void Read(JsonValue v, Dictionary<string, KitRule> into)
    {
        if (!v.IsJsonObject) return;
        foreach (var kv in v.AsJsonObject)
        {
            var entry = kv.Value;
            if (!entry.IsJsonObject) continue;
            if (entry["when"].IsString && Enum.TryParse(entry["when"].AsString, out KitWhen when) && when != KitWhen.Off)
                into[kv.Key] = new KitRule { when = when, value = entry["value"].IsNumber ? entry["value"].AsInteger : 50 };
        }
    }

    private static void Save()
    {
        var record = SaveData.Feature(Instance.Id, true);
        record["abilities"] = Write(Abilities);
        record["deployables"] = Write(Deployables);
    }

    private static JsonObject Write(Dictionary<string, KitRule> rules)
    {
        var o = new JsonObject();
        foreach (var kv in rules) o[kv.Key] = new JsonObject { { "when", kv.Value.when.ToString() }, { "value", kv.Value.value } };
        return o;
    }

    internal struct ShipState
    {
        public bool fighting;
        public float shield, armour, hull;
        public int targetedBy;
        public CombatModule combat;
    }

    internal static ShipState Read(SpaceShip ship)
    {
        var combat = ship.GetModule<CombatModule>();
        return new ShipState
        {
            fighting = ship.inCombat || (combat != null && combat.priorityTarget != null),
            shield = ship.maxShieldHP > 0f ? ship.currentShieldHP / ship.maxShieldHP : 1f,
            armour = ship.maxArmorHP > 0f ? ship.currentArmorHP / ship.maxArmorHP : 1f,
            hull = ship.maxHullHP > 0f ? ship.currentHullHP / ship.maxHullHP : 1f,
            targetedBy = ship.targetedCount,
            combat = combat,
        };
    }

    // Everything but "whenever ready" also needs a fight going on, so nothing fires while ECHO mines in peace.
    internal static bool Holds(KitRule rule, ShipState s)
    {
        if (rule == null) return false;
        if (rule.when == KitWhen.Ready) return true;
        if (!s.fighting) return false;
        float limit = rule.value / 100f;
        return rule.when switch
        {
            KitWhen.InCombat => true,
            KitWhen.ShieldBelow => s.shield < limit,
            KitWhen.ArmourBelow => s.armour < limit,
            KitWhen.HullBelow => s.hull < limit,
            KitWhen.TargetedBy => s.targetedBy >= rule.value,
            _ => false,
        };
    }

    private IEnumerator Run()
    {
        var wait = new WaitForSeconds(0.5f);
        while (true)
        {
            yield return wait;
            if (!Active) continue;
            try { Tick(); }
            catch (Exception e) { Fail(e, nameof(Run)); }
        }
    }

    internal static bool Allowed(out SpaceShip ship)
    {
        ship = Fleet.Flagship;
        if (ship == null || GamePlayer.current == null) return false;
        if (!GamePlayer.current.autoPlay && !Instance.AlsoWithoutEcho.Value) return false;
        var travel = Singleton<TravelManager>.Current;
        return SpaceStationInterior.instance == null && travel != null && !travel.TravelActive() && MapPointOfInterest.current != null;
    }

    private static void Tick()
    {
        if (!Allowed(out var ship)) return;
        var s = Read(ship);
        if (Abilities.Count > 0) UseAbilities(ship, s);
        if (Deployables.Count > 0 && Time.time >= nextDeploy) Deploy(ship, s);
        if (s.fighting) quietSince = -1f;
        else if (quietSince < 0f) quietSince = Time.time;
        else if (Time.time - quietSince > 5f && deployed.Count > 0 && Instance.PickUpTurrets.Value) PickUp(ship);
    }

    internal static int UseAbilities(SpaceShip ship, ShipState s)
    {
        int used = 0;
        foreach (var ability in ship.GetActivatedAbilities())
        {
            var rule = Rule(Abilities, ability.identifier);
            if (rule == null) continue;
            bool want = Holds(rule, s);
            if (ability.toggledAbility)
            {
                if (want && !ability.toggleActive && ability.isReady && Fire(ship, s.combat, ability)) { ourToggles.Add(ability.identifier); used++; }
                else if (!want && ability.toggleActive && ability.isReady && ourToggles.Contains(ability.identifier))
                {
                    ability.TriggerPayload();   // toggles off, and starts no cooldown
                    if (!ability.toggleActive) ourToggles.Remove(ability.identifier);
                }
                continue;
            }
            if (want && ability.isReady && Fire(ship, s.combat, ability)) used++;
        }
        return used;
    }

    // Targeted abilities go on ECHO's current target (hostile), your most damaged ship (friendly), the asteroid the
    // flagship mines, or the nearest wreck the payload can still work.
    private static bool Fire(SpaceShip ship, CombatModule combat, ActivatedAbility ability)
    {
        switch (ability.targetType)
        {
            case AbilityTargetType.None:
                return ability.TriggerPayload() != null;
            case AbilityTargetType.HostileShip:
                var target = combat != null ? combat.priorityTarget as AbstractUnit : null;
                return target != null && !target.isDestroyed && ability.TriggerPayload(target.gameObject) != null;
            case AbilityTargetType.FriendlyShip:
                var friend = MostDamaged(ship);
                return friend != null && ability.TriggerPayload(friend.gameObject) != null;
            case AbilityTargetType.Asteroid:
                var rock = MinedAsteroid(ship);
                return rock != null && ability.TriggerPayload(rock.gameObject) != null;
            case AbilityTargetType.Salvage:
                var wreck = WreckFor(ship.transform.position, ability);
                return wreck != null && ability.TriggerPayload(wreck.gameObject) != null;
            default:
                return false;
        }
    }

    internal static Asteroid MinedAsteroid(SpaceShip ship)
    {
        var a = ship.GetModule<MiningModule>()?.priorityTarget as Asteroid;
        return a != null && (a.hasSurfaceOre || a.hasInnerCore && !a.innerCoreDepleted) ? a : null;
    }

    // The HUD's own target test (AbilityButton.SelectTargetAndActivate): the lootbox bot needs a wreck whose lootbox is
    // still there, the credit drone one whose credits are; other salvage payloads take no wreck at all.
    internal static SalvageContainer WreckFor(Vector2 from, ActivatedAbility ability)
    {
        var payload = ability.payload;
        if (payload == null) return null;
        bool lootbox = payload.TryGetComponent<SalvageCargoLootboxBot>(out _);
        if (!lootbox && !payload.TryGetComponent<SalvageCreditDrone>(out _)) return null;
        SalvageContainer best = null;
        float bestDist = float.MaxValue;
        foreach (var c in SalvageContainer.all)
        {
            if (c == null || !c.gameObject.activeInHierarchy || c.data == null) continue;
            if (lootbox ? c.data.lootboxExtracted != 0 : c.data.creditsExtracted != 0) continue;
            float d = Vector2.Distance(from, c.transform.position);
            if (d < bestDist) { best = c; bestDist = d; }
        }
        return best;
    }

    private static AbstractUnit MostDamaged(SpaceShip flagship)
    {
        AbstractUnit best = flagship;
        float worst = flagship.maxHullHP > 0f ? flagship.currentHullHP / flagship.maxHullHP : 1f;
        foreach (var data in GamePlayer.current.activeFleet)
        {
            if (data?.unit is not SpaceShip wing || wing.isDestroyed || wing.maxHullHP <= 0f) continue;
            float f = wing.currentHullHP / wing.maxHullHP;
            if (f < worst) { worst = f; best = wing; }
        }
        return best;
    }

    public static bool CanAutomate(AbilityTargetType t) => t != AbilityTargetType.FriendlyDrone;

    // One deployable per call, from the flagship's cargo; the game's own cooldown decides when the next can go.
    internal static InventoryItemType Deploy(SpaceShip ship, ShipState s)
    {
        var cargo = ship.spaceShipData.cargo;
        foreach (var entry in cargo.items)
        {
            var item = entry?.item;
            if (item == null || !Holds(Rule(Deployables, item.identifier), s)) continue;
            if (!item.TryGetComponent<DefensiveTurretItem>(out var dt) || dt.IsOnCooldown(out _)) continue;
            var before = new HashSet<DefensiveTurret>(Object.FindObjectsByType<DefensiveTurret>(FindObjectsInactive.Exclude));
            if (!dt.OnUse(item)) continue;
            cargo.Remove(entry, 1);
            foreach (var turret in Object.FindObjectsByType<DefensiveTurret>(FindObjectsInactive.Exclude))
                if (!before.Contains(turret) && turret.defensiveTurretData?.turretItem == item) deployed.Add(turret);
            nextDeploy = Time.time + 2f;
            Plugin.Log.LogInfo($"[EchoKit] deployed {item.identifier}");
            return item;
        }
        return null;
    }

    // The game's click-to-retrieve (DefensiveTurret.OnMouseUpAsButton) without the mouse checks.
    internal static int PickUp(SpaceShip ship)
    {
        var tractor = ship.GetModule<TractorModule>();
        if (tractor == null) return 0;
        deployed.RemoveWhere(t => t == null);
        int started = 0;
        foreach (var turret in deployed)
        {
            var item = turret.defensiveTurretData?.turretItem;
            if (item == null || turret.faction != Faction.player || turret.GetComponent<TractorableItem>() != null) continue;
            if (GamePlayer.current.currentSpaceShip.IsCargoBayFull(item.m3) || tractor.GetAvailableTractorBeam(bonus: true) == null) continue;
            turret.UpdateAmmoData();
            var tractorable = turret.gameObject.AddComponent<TractorableItem>();
            tractorable.SetData(new TractorableItemData { itemType = item, itemAmount = 1, jettisoned = true });
            tractor.SetManualTarget(tractorable);
            var col = turret.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            started++;
        }
        return started;
    }

    // Turrets the rules dropped are collected before ECHO leaves, as the game does with its own loot: start one, or
    // name the one still in the beam. Null when nothing is left to collect.
    internal static InventoryItemType PickUpBeforeLeaving(SpaceShip ship)
    {
        PickUp(ship);
        foreach (var t in deployed)
            if (t != null && t.TryGetComponent<TractorableItem>(out var item) && item.isTractored && item.data != null) return item.data.itemType;
        return null;
    }
}

[HarmonyPatch]
internal static class EchoKitPatches
{
    // ECHO waits a decision while a manual tractor target is set; this makes it wait for the turrets the rules dropped.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IdleManager), nameof(IdleManager.TryLootBeforeLeaving))]
    private static bool TryLootBeforeLeavingPrefix(ref bool __result)
    {
        var f = EchoKit.Instance;
        if (!f.Active) return true;
        try
        {
            if (!f.PickUpTurrets.Value || !EchoKit.Allowed(out var ship)) return true;
            var item = EchoKit.PickUpBeforeLeaving(ship);
            if (item == null) return true;
            IdleManager.UpdateActivity("@IdleLooting", item.displayName);
            __result = true;
            return false;
        }
        catch (Exception e) { f.Fail(e, nameof(TryLootBeforeLeavingPrefix)); return true; }
    }
}
