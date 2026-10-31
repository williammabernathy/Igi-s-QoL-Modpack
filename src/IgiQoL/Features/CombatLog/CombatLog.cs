using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Behaviour.Equipment.Turret;
using Behaviour.Unit;
using Behaviour.Weapons;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using Source.Combat;
using Source.Galaxy;
using Source.SpaceShip;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.CombatLog;

public enum View { DealtByWeapon, DealtByType, DealtByShip, DealtToTarget, TakenByWeapon, TakenByType, TakenFromShip, TakenByShip }

public sealed class Stat
{
    public string label;
    public double total, shield, armor, hull, max;
    public int hits, crits, kills;

    public void Add(float s, float a, float h, bool crit)
    {
        float sum = s + a + h;
        total += sum;
        shield += s;
        armor += a;
        hull += h;
        if (sum > max) max = sum;
        hits++;
        if (crit) crits++;
    }
}

public sealed class Fight
{
    public float start, last;
    // Seconds with fighting in them: gaps longer than the fight gap don't count (matters for the session total).
    public float active;
    public bool written;
    public readonly Dictionary<object, Stat>[] views = new Dictionary<object, Stat>[8];
    public double dealt, taken;
    public int kills;

    public Fight(float now)
    {
        start = last = now;
        for (int i = 0; i < views.Length; i++) views[i] = new Dictionary<object, Stat>();
    }

    public float Duration => Mathf.Max(1f, active);

    public void Touch(float now, float gap)
    {
        if (now - last <= gap) active += now - last;
        last = now;
    }
}

public sealed class CombatLog : Feature
{
    public static CombatLog Instance;

    public CombatLog() { Instance = this; }

    public override string Id => "CombatLog";
    public override string Title => "Combat log";
    public override string Description => "Records the damage your ships, drones and fighters deal and take: by weapon, damage type, ship and enemy type, split into shield, armour and hull, with hits, crits and kills. See it on the Combat tab.";
    public override string SettingsTab => "Combat";
    public override Type[] PatchClasses => new[] { typeof(CombatLogPatches) };

    public ConfigEntry<int> FightGapSeconds;
    public ConfigEntry<bool> WriteFile;

    public const int KeptFights = 10;

    // Newest first; Fights[0] is the current (or last) fight.
    public static readonly List<Fight> Fights = new();
    public static Fight Session = new(0f);
    public static int Version;

    private static readonly object[] TypeKeys = BuildTypeKeys();

    private static object[] BuildTypeKeys()
    {
        var values = (DamageType[])Enum.GetValues(typeof(DamageType));
        var keys = new object[values.Length];
        for (int i = 0; i < values.Length; i++) keys[i] = values[i];
        return keys;
    }

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        FightGapSeconds = cfg.Bind(Id, "FightGapSeconds", 15, SettingInfo.Describe("Seconds without damage that end a fight",
            "A hit after this many quiet seconds starts a new fight in the log.", new AcceptableValueRange<int>(5, 120)));
        WriteFile = cfg.Bind(Id, "WriteFile", false, SettingInfo.Describe("Write each fight to a file",
            "When a fight ends, append its damage tables to BepInEx/config/IgiQoL/combat-log.txt."));
    }

    public override void OnActivated()
    {
        ModWindow.AddTab("combat", "Combat", 7, CombatLogTab.Build);
        GameEvents.SaveLoaded += Reset;
        Plugin.Instance.StartCoroutine(WriteFinishedFights());
    }

    // A fight only ends by going quiet, so finished fights are written from here.
    private System.Collections.IEnumerator WriteFinishedFights()
    {
        var wait = new WaitForSecondsRealtime(5f);
        while (true)
        {
            yield return wait;
            if (!Active || !WriteFile.Value || Fights.Count == 0) continue;
            var f = Fights[0];
            if (!f.written && Time.time - f.last > FightGapSeconds.Value) WriteFight(f);
        }
    }

    public override System.Collections.IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        // Earlier checks spawn enemies next to the fleet, so real hits may already be in.
        if (Session.dealt + Session.taken > 0)
            ctx.Pass("live", $"dealt {Session.dealt:0} in {Session.views[(int)View.DealtByWeapon].Count} weapon rows, taken {Session.taken:0}, kills {Session.kills}, fights {Fights.Count}");
        else ctx.Skip("live", "no combat happened during the harness");

        var flagship = GameplayManager.Instance?.spaceShip;
        var ourTurret = flagship != null ? flagship.GetComponentInChildren<AbstractTurret>() : null;
        var poi = MapPointOfInterest.current;
        if (ourTurret == null || poi == null)
        {
            ctx.Skip("dealt", "flagship has no weapon or isn't at a location");
            yield break;
        }
        var enemy = FindEnemy();
        if (enemy == null)
        {
            SelfCheckContext.SpawnEnemies(1);
            float until = Time.realtimeSinceStartup + 30f;
            while ((enemy = FindEnemy()) == null && Time.realtimeSinceStartup < until) yield return new WaitForSeconds(1f);
        }
        if (enemy == null)
        {
            ctx.Skip("dealt", "no armed enemy arrived");
            yield break;
        }

        // One hit each way, compared with what the target's shield, armour and hull actually lost.
        var dealtRow = Find(View.DealtByWeapon, ourTurret.item);
        double before = dealtRow?.total ?? 0;
        float lost = Hit(new DamageData(ourTurret) { power = 2000f, type = DamageType.Kinetic }, enemy);
        dealtRow = Find(View.DealtByWeapon, ourTurret.item);
        string d = $"{dealtRow?.label}: +{(dealtRow?.total ?? 0) - before:0.0}, target lost {lost:0.0}";
        if (dealtRow != null && Math.Abs(dealtRow.total - before - lost) < 0.5 && lost > 0) ctx.Pass("dealt", d);
        else ctx.Fail("dealt", d);

        var theirTurret = enemy.GetComponentInChildren<AbstractTurret>();
        object weaponKey = (object)theirTurret.item?.equipmentBuilder ?? theirTurret.GetType();
        var takenRow = Find(View.TakenByWeapon, weaponKey);
        before = takenRow?.total ?? 0;
        lost = Hit(new DamageData(theirTurret) { power = 2000f, type = DamageType.Energy }, flagship);
        takenRow = Find(View.TakenByWeapon, weaponKey);
        d = $"{takenRow?.label}: +{(takenRow?.total ?? 0) - before:0.0}, flagship lost {lost:0.0}";
        if (takenRow != null && Math.Abs(takenRow.total - before - lost) < 0.5 && lost > 0) ctx.Pass("taken", d);
        else ctx.Fail("taken", d);

        CombatLogTab.scope = -1;
        CombatLogTab.view = View.DealtByWeapon;
        ModWindow.Open("combat");
        yield return new WaitForSeconds(1.2f);
        var table = ModWindow.IsOpen ? ModWindow.Content.GetComponentInChildren<CombatLogTab.CombatTable>() : null;
        if (table != null && table.RowCount > 0) ctx.Pass("tab", $"{table.RowCount} rows");
        else ctx.Fail("tab", table == null ? "no table" : "no rows");
        yield return ctx.Screenshot("window-combat");
        ModWindow.Close();

        string text = Describe(Session, "Session");
        if (text.Contains(dealtRow.label) && text.Contains(takenRow.label)) ctx.Pass("file-text", $"{text.Length} chars, {text.Split('\n').Length} lines");
        else ctx.Fail("file-text", text.Length > 200 ? text.Substring(0, 200) : text);
    }

    private static Stat Find(View view, object key) => Session.views[(int)view].TryGetValue(key, out var s) ? s : null;

    private static AbstractUnit FindEnemy()
    {
        foreach (var u in UnityEngine.Object.FindObjectsByType<AbstractUnit>(FindObjectsInactive.Exclude))
            if (u != null && !u.isDestroyed && !u.isBoardable && u.enabled && u.faction != null && Faction.player.IsEnemy(u.faction) && u.currentHullHP > 0f && u.GetComponentInChildren<AbstractTurret>() != null)
                return u;
        return null;
    }

    // What the unit's shield, armour and hull lost to one hit.
    private static float Hit(DamageData dd, AbstractUnit target)
    {
        float Pool() => (target.shieldGeneratorModule != null ? target.shieldGeneratorModule.currentShieldCapacity : 0f)
            + (target.armorModule != null ? target.armorModule.currentArmor : 0f) + target.currentHullHP;
        float before = Pool();
        // A projectile hit carries the point it struck (DetermineBattleDamage reads it).
        dd.hitCoordinates = target.transform.position;
        dd.CreateHitTransform(target.transform);
        target.TakeDamage(dd);
        return before - Pool();
    }

    public static void Reset()
    {
        Fights.Clear();
        Session = new Fight(Time.time);
        Version++;
    }

    public static string FilePath => Paths.ConfigPath + "/IgiQoL/combat-log.txt";

    private static Fight Current(float now)
    {
        if (Fights.Count > 0 && now - Fights[0].last <= Instance.FightGapSeconds.Value) return Fights[0];
        if (Fights.Count > 0 && !Fights[0].written && Instance.WriteFile.Value) WriteFight(Fights[0]);
        var f = new Fight(now);
        Fights.Insert(0, f);
        if (Fights.Count > KeptFights) Fights.RemoveAt(Fights.Count - 1);
        return f;
    }

    // One hit between your side and anyone else. Labels are built once per new row, not per hit.
    internal static void Record(AbstractUnit source, AbstractTurret turret, AbstractUnit target, DamageType type, float shield, float armor, float hull, bool crit, bool killed, bool dealt)
    {
        float now = Time.time;
        var fight = Current(now);
        float gap = Instance.FightGapSeconds.Value;
        fight.Touch(now, gap);
        Session.Touch(now, gap);
        float sum = shield + armor + hull;
        object typeKey = TypeKeys[(int)type];
        if (dealt)
        {
            fight.dealt += sum;
            Session.dealt += sum;
            if (killed) { fight.kills++; Session.kills++; }
            object weapon = WeaponKey(source, turret);
            object ship = Owner(source);
            object foe = Definition(target);
            Add(fight, Session, View.DealtByWeapon, weapon, source, turret, shield, armor, hull, crit, killed);
            Add(fight, Session, View.DealtByType, typeKey, source, turret, shield, armor, hull, crit, killed);
            Add(fight, Session, View.DealtByShip, ship, source, turret, shield, armor, hull, crit, killed);
            Add(fight, Session, View.DealtToTarget, foe, target, null, shield, armor, hull, crit, killed);
        }
        else
        {
            fight.taken += sum;
            Session.taken += sum;
            object weapon = turret != null ? (object)turret.item?.equipmentBuilder ?? turret.GetType() : typeof(AbstractUnit);
            Add(fight, Session, View.TakenByWeapon, weapon, source, turret, shield, armor, hull, crit, killed);
            Add(fight, Session, View.TakenByType, typeKey, source, turret, shield, armor, hull, crit, killed);
            Add(fight, Session, View.TakenFromShip, source != null ? Definition(source) : typeof(DamageData), source, turret, shield, armor, hull, crit, killed);
            Add(fight, Session, View.TakenByShip, Owner(target), target, null, shield, armor, hull, crit, killed);
        }
        Version++;
    }

    private static void Add(Fight fight, Fight session, View view, object key, AbstractUnit unit, AbstractTurret turret, float s, float a, float h, bool crit, bool killed)
    {
        var stat = Row(fight, view, key, unit, turret);
        stat.Add(s, a, h, crit);
        var total = Row(session, view, key, unit, turret);
        total.Add(s, a, h, crit);
        if (killed && (view == View.DealtToTarget || view == View.DealtByShip || view == View.DealtByWeapon))
        {
            stat.kills++;
            total.kills++;
        }
    }

    private static Stat Row(Fight fight, View view, object key, AbstractUnit unit, AbstractTurret turret)
    {
        var dict = fight.views[(int)view];
        if (dict.TryGetValue(key, out var stat)) return stat;
        stat = new Stat { label = Label(view, key, unit, turret) };
        dict[key] = stat;
        return stat;
    }

    // Your fitted weapons are distinct pieces; a drone's weapon is rebuilt with each drone, so drones group by drone type.
    private static object WeaponKey(AbstractUnit source, AbstractTurret turret)
    {
        if (source is Drone) return Definition(source);
        if (turret != null && turret.item != null) return turret.item;
        return (object)turret ?? Definition(source);
    }

    private static object Definition(AbstractUnit unit) => (object)unit.unitData?.unitDefinition ?? unit;

    // Damage by a drone counts for the ship that launched it.
    private static object Owner(AbstractUnit unit)
    {
        if (unit is Drone d && d.droneCommander != null) unit = d.droneCommander;
        return (object)unit.unitData ?? unit;
    }

    private static string UnitName(AbstractUnit unit)
    {
        if (unit == null) return "?";
        if (unit is SpaceShip ship && ship.spaceShipData != null && ship.spaceShipData.faction == Faction.player) return ship.spaceShipData.GetShipName();
        return Translation.Translate(unit.displayName);
    }

    private static string Label(View view, object key, AbstractUnit unit, AbstractTurret turret)
    {
        if (key is DamageType type) return type.GetDisplayName();
        switch (view)
        {
            case View.DealtByWeapon:
                if (unit is Drone drone) return "Drone: " + Translation.Translate(drone.displayName) + (drone.droneCommander != null ? " (" + UnitName(drone.droneCommander) + ")" : "");
                if (turret != null && turret.item != null)
                {
                    // Two copies of a launcher on one ship would read the same; the slot tells them apart.
                    int slot = unit.unitData?.hardpoints != null ? Array.IndexOf(unit.unitData.hardpoints, turret.item) : -1;
                    return Translation.Translate(turret.item.displayName) + " (" + UnitName(unit) + (slot >= 0 ? ", slot " + (slot + 1) : "") + ")";
                }
                return "Other damage (" + UnitName(unit) + ")";
            case View.DealtByShip:
            case View.TakenByShip:
                var owner = unit is Drone d && d.droneCommander != null ? d.droneCommander : unit;
                return UnitName(owner) + (unit is Drone && view == View.TakenByShip ? " (drones)" : "");
            case View.DealtToTarget:
            case View.TakenFromShip:
                if (unit == null) return "Other";
                return Translation.Translate(unit.displayName) + (unit is Drone ? " (drone)" : "");
            case View.TakenByWeapon:
                if (turret == null) return "Other damage";
                string name = !string.IsNullOrEmpty(turret.typeDisplayName) ? Translation.Translate(turret.typeDisplayName) : turret.GetType().Name;
                return (unit is Drone ? "Drone " : "") + name;
        }
        return "?";
    }

    public static void WriteFight(Fight fight)
    {
        fight.written = true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.AppendAllText(FilePath, Describe(fight, "Fight ending " + DateTime.Now.ToString("yyyy-MM-dd HH:mm")) + "\n");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[CombatLog] couldn't write {FilePath}: {e.Message}"); }
    }

    // Plain-text tables for the file.
    public static string Describe(Fight fight, string title)
    {
        var sb = new StringBuilder();
        sb.Append("== ").Append(title).Append($": {fight.Duration:0} s, dealt {GameMath.FormatNumber((float)fight.dealt)} ({GameMath.FormatNumber((float)(fight.dealt / fight.Duration))}/s), taken {GameMath.FormatNumber((float)fight.taken)}, kills {fight.kills}\n");
        foreach (View v in Enum.GetValues(typeof(View)))
        {
            var rows = Sorted(fight, v);
            if (rows.Count == 0) continue;
            sb.Append("-- ").Append(ViewName(v)).Append('\n');
            foreach (var s in rows)
                sb.Append($"  {s.label,-48} {s.total,12:0} {s.hits,6} hits {100.0 * s.crits / Math.Max(1, s.hits),5:0}% crit  shield/armour/hull {Pct(s.shield, s.total)}/{Pct(s.armor, s.total)}/{Pct(s.hull, s.total)}\n");
        }
        return sb.ToString();
    }

    public static List<Stat> Sorted(Fight fight, View view)
    {
        var list = new List<Stat>(fight.views[(int)view].Values);
        list.Sort((a, b) => b.total.CompareTo(a.total));
        return list;
    }

    public static string Pct(double part, double whole) => whole > 0 ? (100.0 * part / whole).ToString("0") : "0";

    public static string ViewName(View v) => v switch
    {
        View.DealtByWeapon => "Dealt, by weapon",
        View.DealtByType => "Dealt, by damage type",
        View.DealtByShip => "Dealt, by your ship",
        View.DealtToTarget => "Dealt, by enemy type",
        View.TakenByWeapon => "Taken, by enemy weapon",
        View.TakenByType => "Taken, by damage type",
        View.TakenFromShip => "Taken, by enemy type",
        _ => "Taken, by your ship",
    };
}

[HarmonyPatch]
internal static class CombatLogPatches
{
    internal struct HitState
    {
        public bool valid;
        public float shield, armor, hull;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AbstractUnit), nameof(AbstractUnit.TakeDamage))]
    private static void TakeDamagePrefix(AbstractUnit __instance, DamageData damageData, out HitState __state)
    {
        __state = default;
        if (!CombatLog.Instance.Active || damageData == null) return;
        try
        {
            // TakeDamage returns at once for these; nothing is dealt.
            if (!__instance.enabled || __instance.isDestroyed || __instance.isBoardable) return;
            __state.valid = true;
            __state.shield = damageData.absorbedByShield;
            __state.armor = damageData.absorbedByArmor;
            __state.hull = __instance.currentHullHP;
        }
        catch (Exception e) { CombatLog.Instance.Fail(e, nameof(CombatLogPatches)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AbstractUnit), nameof(AbstractUnit.TakeDamage))]
    private static void TakeDamagePostfix(AbstractUnit __instance, DamageData damageData, HitState __state)
    {
        if (!__state.valid || !CombatLog.Instance.Active) return;
        try
        {
            var source = damageData.sourceUnit;
            var player = Faction.player;
            bool dealt = source != null && source.faction == player;
            bool taken = __instance.faction == player;
            if (dealt == taken) return;
            float shield = damageData.absorbedByShield - __state.shield;
            float armor = damageData.absorbedByArmor - __state.armor;
            float hull = Mathf.Max(0f, __state.hull - __instance.currentHullHP);
            if (shield + armor + hull <= 0f) return;
            bool killed = __instance.isDestroyed || __instance.isBoardable || __instance.currentHullHP <= 0f;
            CombatLog.Record(source, damageData.sourceTurret, __instance, damageData.type, shield, armor, hull, damageData.IsCriticalHit(), killed, dealt);
        }
        catch (Exception e) { CombatLog.Instance.Fail(e, nameof(CombatLogPatches)); }
    }
}
