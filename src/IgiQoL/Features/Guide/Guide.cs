using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Behaviour.Equipment;
using Behaviour.Equipment.Module;
using Behaviour.Equipment.Turret;
using Behaviour.Equipment.Turret.CombatTurrets;
using Behaviour.Item;
using Behaviour.Unit;
using IgiQoL.Core;
using IgiQoL.UI;
using Source.Combat;
using Source.Item;
using Source.Util;
using TMPro;
using UnityEngine;

namespace IgiQoL.Features.Guide;

// Help text built from the game's own data and formulas: what each damage type is good against, how a hit is
// reduced on its way to the hull, and what your ship's handling numbers mean.
public sealed class Guide : Feature
{
    public static Guide Instance;

    public Guide() { Instance = this; }

    public override string Id => "Guide";
    public override string Title => "Combat guide";
    public override string Description => "The Guide tab: damage types with the weapons that deal them and the armour weak to them, every armour type's resistance, how a hit is reduced on its way to the hull, and your ship's handling and crit numbers, all read from the game's data.";
    public override Type[] PatchClasses => Type.EmptyTypes;

    public override void OnActivated() => ModWindow.AddTab("guide", "Guide", 85, Build);

    internal sealed class WeaponType
    {
        public string name;
        public DamageType type;
        public float minRange = float.MaxValue, maxRange;
    }

    internal sealed class ArmourType
    {
        public string name;
        public float minResist = float.MaxValue, maxResist;
        public float minWeak = float.MaxValue, maxWeak;
        public readonly HashSet<DamageType> weak = new();
    }

    internal static List<WeaponType> Weapons;
    internal static List<ArmourType> Armours;

    // Item templates and equipment builders don't change during a session, so the tables are read once.
    internal static void Collect()
    {
        if (Weapons != null) return;
        var weapons = new Dictionary<string, WeaponType>();
        var armours = new Dictionary<string, ArmourType>();
        foreach (var item in InventoryItemType.all)
            Consider(SafeEquipment(item), item.displayName, weapons, armours);
        // Rolled gear comes from builders; their prefabs carry the same equipment components.
        foreach (var builder in Behaviour.Equipment.Builder.EquipmentBuilder.allBuilders.Values)
            if (builder != null && builder.prefab != null)
                Consider(builder.prefab.GetComponent<AbstractEquipment>(), builder.prefab.displayName, weapons, armours);
        // Torpedo bays are modules, not turrets; their hits are Heat (TorpedoBayModule.CreateDamageData).
        weapons["Torpedo bay"] = new WeaponType { name = "Torpedo bay", type = DamageType.Heat, minRange = 0f, maxRange = 0f };
        Weapons = new List<WeaponType>(weapons.Values);
        Weapons.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
        Armours = new List<ArmourType>(armours.Values);
        Armours.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
    }

    private static void Consider(AbstractEquipment eq, string fallbackName, Dictionary<string, WeaponType> weapons, Dictionary<string, ArmourType> armours)
    {
        if (eq is AbstractCombatTurret turret)
        {
            string name = Name(turret.typeDisplayName, fallbackName);
            if (!weapons.TryGetValue(name, out var w)) weapons[name] = w = new WeaponType { name = name, type = turret.damageType };
            w.minRange = Mathf.Min(w.minRange, turret._range);
            w.maxRange = Mathf.Max(w.maxRange, turret._range);
        }
        else if (eq is ArmorModule armour)
        {
            string name = Name(armour.typeDisplayName, fallbackName);
            if (!armours.TryGetValue(name, out var a)) armours[name] = a = new ArmourType { name = name };
            a.minResist = Mathf.Min(a.minResist, armour.resistAmount);
            a.maxResist = Mathf.Max(a.maxResist, armour.resistAmount);
            if (armour.weakTypes != null && armour.weakTypes.Length > 0)
            {
                foreach (var t in armour.weakTypes) a.weak.Add(t);
                a.minWeak = Mathf.Min(a.minWeak, armour.weakAmount);
                a.maxWeak = Mathf.Max(a.maxWeak, armour.weakAmount);
            }
        }
    }

    private static AbstractEquipment SafeEquipment(InventoryItemType item)
    {
        try { return item.equipmentDefinition; }
        catch (Exception) { return null; }
    }

    private static string Name(string typeName, string fallback) =>
        Translation.Translate(!string.IsNullOrEmpty(typeName) ? typeName : fallback ?? "?");

    internal static string Percent(float min, float max) =>
        Mathf.Approximately(min, max) ? (min * 100f).ToString("0") + "%" : (min * 100f).ToString("0") + "-" + (max * 100f).ToString("0") + "%";

    private static string Colored(DamageType t) => "<color=#" + ColorUtility.ToHtmlStringRGB(t.GetColor()) + ">" + t.GetDisplayName() + "</color>";

    private static void Build(RectTransform content)
    {
        Collect();

        Kit.Heading(content, "How a hit lands");
        Kit.Muted(content,
            "1. The weapon rolls its damage: power / 5, times 0.8 to 1.25, spread over its shots per second. A critical hit multiplies it by 2 plus the weapon's critical damage bonus; with the Mega crit skill one hit can crit more than once.\n" +
            "2. The weapon's bonus for its damage type and its general damage bonus are added on top.\n" +
            "3. The target's damage reduction and its resistance to that damage type take their share off. Together they never remove more than 80%.\n" +
            "4. Shields absorb the hit first and treat every damage type the same. Weapons with a damage-vs-shield bonus hit shields harder.\n" +
            "5. Armour takes what's left: it cuts damage by its resistance, unless the armour is weak to that damage type, then it takes extra instead. Painter drones strip armour resistance for a while. Damage-vs-armour bonuses apply here.\n" +
            "6. Whatever gets through comes off the hull.");

        Kit.Heading(content, "Damage types and the weapons that deal them");
        foreach (DamageType t in Enum.GetValues(typeof(DamageType)))
        {
            var sb = new StringBuilder();
            foreach (var w in Weapons)
                if (w.type == t) sb.Append(sb.Length > 0 ? ", " : "").Append(w.name);
            string weapons = sb.Length > 0 ? sb.ToString() : "no turret deals it";
            sb.Clear();
            foreach (var a in Armours)
                if (a.weak.Contains(t)) sb.Append(sb.Length > 0 ? ", " : "").Append(a.name).Append(" takes +").Append(Percent(a.minWeak, a.maxWeak));
            Line(content, Colored(t) + ": " + weapons + "." + (sb.Length > 0 ? " Strong against armour: " + sb + "." : ""));
        }

        Kit.Heading(content, "Armour types");
        foreach (var a in Armours)
        {
            var sb = new StringBuilder(a.name).Append(": resists ").Append(Percent(a.minResist, a.maxResist));
            if (a.weak.Count > 0)
            {
                sb.Append("; weak to ");
                bool first = true;
                foreach (DamageType t in Enum.GetValues(typeof(DamageType)))
                {
                    if (!a.weak.Contains(t)) continue;
                    sb.Append(first ? "" : ", ").Append(Colored(t));
                    first = false;
                }
                sb.Append(" (takes +").Append(Percent(a.minWeak, a.maxWeak)).Append(')');
            }
            Line(content, sb.ToString());
        }

        ShipSection(content);
    }

    // A wrapping, rich-text line in body colour (Kit.Label is one line, Kit.Muted is grey).
    private static void Line(RectTransform content, string text)
    {
        var t = Kit.Muted(content, text);
        t.color = ColorHelper.offWhite;
        t.fontSize = 12f;
        t.richText = true;
    }

    private static void ShipSection(RectTransform content)
    {
        var ship = GameplayManager.Instance != null ? GameplayManager.Instance.spaceShip : null;
        Kit.Heading(content, "Your ship" + (ship != null ? ": " + ship.spaceShipData.GetShipName() : ""));
        if (ship == null || ship.engine == null)
        {
            Kit.Muted(content, "Shown while you're in space.");
            return;
        }
        var rb = ship.rigidbody;
        float accel = ship.GetFullForwardAcceleration();
        float top = ship.GetReachableTopSpeed();
        float side = rb.mass > 0f ? ship.engine.sideThrust / rb.mass : 0f;
        Line(content, $"Tonnage {ship.unitData.unitDefinition.tonnage}: the hull's mass is tonnage x 10 ({rb.mass:0}). Engines push against it, so a heavier hull with the same engine is slower to speed up, turn and sidestep.");
        Line(content, $"Acceleration {accel:0.00} units/s² (thrust / mass), top speed {top:0.0} units/s, about {(accel > 0f ? top / accel : 0f):0} s from standstill.");
        Line(content, $"Sidestep {side:0.00} units/s² (side thrust / mass): {0.5f * side * 9f:0.0} units in 3 s from standing, too little to dodge missiles, which turn on a 4-11 unit circle. Turning speed is rotational thrust against the hull's inertia, so it also falls with tonnage.");
        float precision = ship.GetPrecisionCrit();
        Line(content, $"Base crit chance 3% + {precision * 100f:0.0}% from Precision ({ship.GetStat(EquipStat.Precision):0}), before weapon crit bonuses. Each crit multiplies damage by 2 plus the critical damage bonus (x{2f + ship.GetStat(EquipStat.CriticalDamage):0.00} for this ship).");
        var resists = new StringBuilder();
        foreach (DamageType t in Enum.GetValues(typeof(DamageType)))
        {
            float r = ship.GetStat(EquipStat.DamageReduction) + ship.GetStat(t.GetResistStat());
            resists.Append(resists.Length > 0 ? ", " : "").Append(Colored(t)).Append(' ').Append(Mathf.Min(80f, r * 100f).ToString("0")).Append('%');
        }
        Line(content, "Incoming damage removed before shields: " + resists + ".");

        var stats = new[] { EquipStat.Thrust, EquipStat.SideThrust, EquipStat.RotationalThrust, EquipStat.Precision, EquipStat.CriticalChance, EquipStat.CriticalDamage, EquipStat.DamageReduction, EquipStat.DamageVsShield, EquipStat.DamageVsArmor };
        var sb = new StringBuilder();
        foreach (var s in stats)
        {
            string key = "@EquipStat" + s + "Description";
            if (!Translation.Exists(key)) continue;
            string name = Translation.Exists("@EquipStat" + s) ? Translation.Translate("@EquipStat" + s) : s.ToString();
            sb.Append(name).Append(": ").Append(Translation.Translate(key)).Append('\n');
        }
        if (sb.Length > 0)
        {
            Kit.Heading(content, "The game's own stat descriptions");
            Kit.Muted(content, sb.ToString().TrimEnd());
        }
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        Collect();
        int typed = 0;
        foreach (DamageType t in Enum.GetValues(typeof(DamageType)))
            if (Weapons.Exists(w => w.type == t)) typed++;
        string d = $"{Weapons.Count} weapon types, {Armours.Count} armour types, {typed}/{Enum.GetValues(typeof(DamageType)).Length} damage types dealt by a weapon; weak links {Armours.FindAll(a => a.weak.Count > 0).Count}";
        if (Weapons.Count > 0 && Armours.Count > 0) ctx.Pass("data", d);
        else ctx.Fail("data", d);

        ModWindow.Open("guide");
        yield return null;
        int lines = 0;
        if (ModWindow.IsOpen)
            foreach (var t in ModWindow.Content.GetComponentsInChildren<TMP_Text>()) lines++;
        if (ModWindow.CurrentTab == "guide" && lines > 10) ctx.Pass("tab", $"{lines} text lines");
        else ctx.Fail("tab", $"tab={ModWindow.CurrentTab} lines={lines}");
        yield return ctx.Screenshot("window-guide");
        ModWindow.Close();

        var sb = new StringBuilder();
        foreach (var a in Armours) sb.Append(a.name).Append(' ').Append(Percent(a.minResist, a.maxResist)).Append(" weak ").Append(string.Join("/", a.weak)).Append("; ");
        Plugin.Log.LogInfo("[Guide] armour: " + sb);
        sb.Clear();
        foreach (var w in Weapons) sb.Append(w.name).Append('=').Append(w.type).Append("; ");
        Plugin.Log.LogInfo("[Guide] weapons: " + sb);
    }
}
