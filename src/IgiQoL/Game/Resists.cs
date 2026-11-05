using Behaviour.Equipment.Module;
using Behaviour.Unit;
using HarmonyLib;
using Source.Combat;
using Source.Item;
using UnityEngine;

namespace IgiQoL.Game;

// How much of a hit a unit takes, by damage type, copied from the game's damage steps. Buffs and debuffs are
// stat boosts, which GetStat already includes.
public static class Resists
{
    public static readonly DamageType[] Types =
    {
        DamageType.Kinetic, DamageType.Energy, DamageType.Radiation, DamageType.Heat,
        DamageType.Cold, DamageType.Corrosion, DamageType.Explosive,
    };

    internal static readonly AccessTools.FieldRef<ArmorModule, float> PaintStrip =
        AccessTools.FieldRefAccess<ArmorModule, float>("_paintStripAmount");

    // Applies to every hit before shields, armor and hull (private AbstractUnit.DamageResistMultiplier).
    public static float Multiplier(AbstractUnit unit, DamageType type) =>
        Mathf.Max(0.2f, 1f - unit.GetStat(EquipStat.DamageReduction) - unit.GetStat(type.GetResistStat()));

    // The extra step for the part of a hit that reaches armor while it has HP left (private
    // ArmorModule.ApplyArmorResistance): weak types take more, the rest less, minus any paint strip. 1 without armor.
    public static float ArmorMultiplier(AbstractUnit unit, DamageType type)
    {
        var armor = unit.armorModule;
        if (armor == null) return 1f;
        foreach (var weak in armor.weakTypes)
            if (weak == type) return 1f + armor.weakAmount;
        return 1f - Mathf.Max(0f, armor.resistAmount - PaintStrip(armor));
    }

    public static bool HasArmor(AbstractUnit unit) => unit.armorModule != null && unit.maxArmorHP > 0f;
}
