using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Behaviour.Equipment.Module;
using Behaviour.UI;
using Behaviour.UI.Tooltip;
using Behaviour.Unit;
using Behaviour.Weapons;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using Source.Combat;
using Source.Galaxy;
using Source.Item;
using Source.UI.Tooltip;
using TMPro;
using UnityEngine;

namespace IgiQoL.Features.ShipStats;

// Compares the shared resistance helper with the game's own private damage steps, then reads the hover card the
// game builds and the tooltip it shows.
internal static class ShipStatsCheck
{
    private static readonly MethodInfo ResistStep = AccessTools.Method(typeof(AbstractUnit), "DamageResistMultiplier");
    private static readonly MethodInfo ArmorStep = AccessTools.Method(typeof(ArmorModule), "ApplyArmorResistance");
    private static readonly MethodInfo BuildCard = AccessTools.Method(typeof(SpaceShip), "BuildTooltipCard");
    private const string BoostId = "IgiQoL_ShipStatsCheck";

    public static IEnumerator Run(SelfCheckContext ctx)
    {
        yield return SelfCheckContext.EnsureInSpace();
        var flagship = Fleet.Flagship;
        if (flagship == null)
        {
            ctx.Skip("match", "no flagship in space");
            yield break;
        }
        var enemy = Enemy();
        if (enemy == null && SelfCheckContext.SpawnEnemies(1))
        {
            float until = Time.realtimeSinceStartup + 30f;
            while ((enemy = Enemy()) == null && Time.realtimeSinceStartup < until) yield return new WaitForSeconds(1f);
        }
        var ships = new List<SpaceShip> { flagship };
        foreach (var d in Fleet.Ships(withMercenaries: true))
        {
            var u = Fleet.Unit(d);
            if (u != null && !ships.Contains(u)) ships.Add(u);
        }
        if (enemy != null) ships.Add(enemy);

        // The helper gives what the game's own steps do, for every damage type on every ship here.
        int armored = 0, weakTypes = 0;
        string mismatch = null;
        foreach (var ship in ships)
        {
            if (Resists.HasArmor(ship)) { armored++; weakTypes += ship.armorModule.weakTypes.Length; }
            foreach (var type in Resists.Types)
                mismatch ??= Compare(ship, type);
        }
        string matchDetail = $"{ships.Count} ships ({(enemy != null ? "1 enemy" : "no enemy")}), {armored} with armour, {weakTypes} armour weak types";
        if (mismatch == null) ctx.Pass("match", matchDetail);
        else ctx.Fail("match", matchDetail + "; " + mismatch);

        // Every ship's card gets the section, one row per type, with the helper's figures.
        string cardProblem = null;
        foreach (var ship in ships)
        {
            var section = Section(ship);
            if (section == null) { cardProblem = ship.displayName + ": no section"; break; }
            if (section.rows.Count != Resists.Types.Length) { cardProblem = $"{ship.displayName}: {section.rows.Count} rows"; break; }
            for (int i = 0; i < Resists.Types.Length && cardProblem == null; i++)
            {
                string want = ShipStats.Pct(Resists.Multiplier(ship, Resists.Types[i]));
                if (!section.rows[i].value.StartsWith(want)) cardProblem = $"{ship.displayName} {Resists.Types[i]}: '{section.rows[i].value}', want '{want}'";
            }
        }
        var target = enemy != null ? enemy : flagship;
        string cardDetail = $"{ships.Count} cards; {target.displayName}: {Describe(Section(target))}";
        if (cardProblem == null) ctx.Pass("card", cardDetail);
        else ctx.Fail("card", cardProblem);

        // A debuff shows on the card at once: -50% kinetic resistance as a stat boost, the way abilities apply one.
        string before = Value(target, DamageType.Kinetic), after;
        string afterMismatch;
        try
        {
            target.AddStatBoost(BoostId, "IgiQoL check", EquipStat.KineticResist, -0.5f, 1f);
            after = Value(target, DamageType.Kinetic);
            afterMismatch = Compare(target, DamageType.Kinetic);
        }
        finally { target.RemoveStatBoost(BoostId); }
        string debuffDetail = $"{target.displayName} kinetic: '{before}' -> '{after}' with -50%, back to '{Value(target, DamageType.Kinetic)}'";
        if (after != before && afterMismatch == null && Value(target, DamageType.Kinetic) == before) ctx.Pass("debuff", debuffDetail);
        else ctx.Fail("debuff", debuffDetail + (afterMismatch != null ? "; " + afterMismatch : ""));

        // The tooltip the game shows on hover carries the section.
        var source = target.GetComponent<TooltipSource>();
        if (source == null || !UITooltip.TooltipEnabled)
        {
            ctx.Skip("tooltip", source == null ? "the ship has no tooltip source" : "tooltips are off");
            yield break;
        }
        bool shown = false;
        try
        {
            UITooltip.Show(source, source.Prefab);
            var tip = UITooltip._current;
            if (tip != null)
                foreach (var t in tip.Content.GetComponentsInChildren<TMP_Text>(true))
                    if (t.text == ShipStats.SectionTitle.ToUpperInvariant()) shown = true;
            if (shown) ctx.Pass("tooltip", target.displayName + ": section shown");
            else ctx.Fail("tooltip", target.displayName + ": no section title in the shown tooltip");
            yield return ctx.Screenshot("ship-stats");
        }
        finally { UITooltip.Hide(source); }
    }

    private static SpaceShip Enemy()
    {
        foreach (var s in Object.FindObjectsByType<SpaceShip>(FindObjectsInactive.Exclude))
            if (s != null && !s.isDestroyed && s.faction != null && Faction.player.IsEnemy(s.faction)) return s;
        return null;
    }

    // Null when the helper agrees with the game's private steps for this ship and type.
    private static string Compare(SpaceShip ship, DamageType type)
    {
        var hit = new DamageData { type = type };
        float game = (float)ResistStep.Invoke(ship, new object[] { hit });
        float mine = Resists.Multiplier(ship, type);
        if (Mathf.Abs(game - mine) > 0.0001f) return $"{ship.displayName} {type}: game {game:0.0000}, helper {mine:0.0000}";
        if (!Resists.HasArmor(ship)) return null;
        hit.OverrideDamageAmount(100f);
        ArmorStep.Invoke(ship.armorModule, new object[] { hit });
        float gameArmor = hit.damageAmount / 100f, myArmor = Resists.ArmorMultiplier(ship, type);
        if (Mathf.Abs(gameArmor - myArmor) > 0.0001f) return $"{ship.displayName} {type} armour: game {gameArmor:0.0000}, helper {myArmor:0.0000}";
        return null;
    }

    private static TooltipSection Section(SpaceShip ship)
    {
        var card = (TooltipCard)BuildCard.Invoke(ship, null);
        foreach (var s in card.sections)
            if (s.title == ShipStats.SectionTitle) return s;
        return null;
    }

    private static string Value(SpaceShip ship, DamageType type)
    {
        var section = Section(ship);
        int i = System.Array.IndexOf(Resists.Types, type);
        return section != null && section.rows.Count > i ? section.rows[i].value : null;
    }

    private static string Describe(TooltipSection section)
    {
        if (section == null) return "no section";
        var parts = new List<string>();
        if (section.count != null) parts.Add("[" + section.count + "]");
        foreach (var r in section.rows) parts.Add(r.label + " " + r.value);
        return string.Join(", ", parts);
    }
}
