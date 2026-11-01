using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Behaviour.Ability;
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
using Source.Util;
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

        // Buffs and debuffs: a buff, a debuff and an effect the game keeps out of its buff HUD, as child effects the
        // way projectiles and abilities attach them. Only the first two get rows; the debuff counts in the header.
        var rowsBefore = Rows(EffectsSection(target));
        int debuffsBefore = Effects.DebuffCount(target);
        var added = new List<TemporaryEffect>
        {
            AddEffect(target, "IgiQoL check buff", debuff: false, shown: true, 30f),
            AddEffect(target, "IgiQoL check debuff", debuff: true, shown: true, 20f),
            AddEffect(target, "IgiQoL check hidden", debuff: true, shown: false, 20f),
        };
        try
        {
            yield return null; // TemporaryEffect.Start sets the time left
            var section = EffectsSection(target);
            var rows = Rows(section);
            int debuffs = Effects.DebuffCount(target);
            string effectsDetail = $"{target.displayName}: {Describe(section)}; {rowsBefore.Count} rows before";
            TooltipRow debuffRow = null;
            if (section != null)
                foreach (var r in section.rows)
                    if (r.label == "IgiQoL check debuff") debuffRow = r;
            if (rows.Count == rowsBefore.Count + 2 && rows.Contains("IgiQoL check buff") && debuffRow != null
                && !rows.Contains("IgiQoL check hidden") && debuffs == debuffsBefore + 1
                && debuffRow.dotColor == ColorHelper.debuffBorder && debuffRow.fill > 0.9f
                && section.count.EndsWith(debuffs + (debuffs == 1 ? " debuff" : " debuffs")))
                ctx.Pass("effects", effectsDetail);
            else ctx.Fail("effects", effectsDetail + $"; debuffs {debuffsBefore} -> {debuffs}");

            // A real ability fired from the flagship: its effects show, translated, each once.
            ActivatedAbility ability = null;
            foreach (var a in flagship.GetComponentsInChildren<ActivatedAbility>())
                if (ability == null && !a.toggledAbility && a.isReady && !a.IsPayloadActive()) ability = a;
            var abilityBefore = Rows(EffectsSection(flagship));
            if (ability == null) ctx.Skip("ability", "no ready, untoggled ability on the flagship");
            else if (ability.TriggerPayload() == null) ctx.Fail("ability", ability.displayName + ": the game didn't trigger it");
            else
            {
                yield return null;
                var abilityRows = Rows(EffectsSection(flagship));
                var fresh = new List<string>();
                foreach (var r in abilityRows)
                    if (!abilityBefore.Contains(r)) fresh.Add(r);
                bool clean = fresh.Count > 0;
                foreach (var r in fresh)
                    if (r.StartsWith("@") || abilityRows.IndexOf(r) != abilityRows.LastIndexOf(r)) clean = false;
                string abilityDetail = $"{flagship.displayName} fired {Translation.Translate(ability.displayName)}: {Describe(EffectsSection(flagship))}";
                if (clean) ctx.Pass("ability", abilityDetail);
                else ctx.Fail("ability", abilityDetail + $"; {fresh.Count} new rows");
            }

            // The tooltip the game shows on hover carries both sections and rebuilds itself while it shows.
            var source = target.GetComponent<TooltipSource>();
            if (source == null || !UITooltip.TooltipEnabled)
            {
                ctx.Skip("tooltip", source == null ? "the ship has no tooltip source" : "tooltips are off");
                yield break;
            }
            try
            {
                UITooltip.Show(source, source.Prefab);
                string shownText = TooltipText();
                bool resists = shownText.Contains("\n" + ShipStats.SectionTitle.ToUpperInvariant() + "\n");
                bool effects = shownText.Contains("\n" + ShipStats.EffectsTitle.ToUpperInvariant() + "\n");
                if (resists && effects) ctx.Pass("tooltip", target.displayName + ": both sections shown");
                else ctx.Fail("tooltip", $"{target.displayName}: resistances {resists}, buffs and debuffs {effects}");

                // Effect timers run on the shown card: the text changes as time passes. Runs before the screenshot,
                // which waits 2 s, because the fleet may be fighting this enemy.
                float until = Time.realtimeSinceStartup + 1.5f;
                while (Time.realtimeSinceStartup < until && UITooltip.IsShowing(source)) yield return null;
                string laterText = UITooltip.IsShowing(source) ? TooltipText() : null;
                if (laterText == null && (target == null || target.isDestroyed)) ctx.Skip("refresh", "the enemy was destroyed while waiting");
                else if (laterText == null)
                    ctx.Fail("refresh", "the tooltip closed while waiting; now showing " + (UITooltip._current != null && UITooltip._current.Source != null ? UITooltip._current.Source.name : "nothing"));
                else if (laterText != shownText) ctx.Pass("refresh", target.displayName + ": card rebuilt with new timers");
                else ctx.Fail("refresh", target.displayName + ": card text unchanged after 1.5 s");
                if (UITooltip.IsShowing(source)) yield return ctx.Screenshot("ship-stats");
            }
            finally { UITooltip.Hide(source); }
        }
        finally
        {
            foreach (var e in added)
                if (e != null) Object.Destroy(e.gameObject);
        }
    }

    private static TemporaryEffect AddEffect(SpaceShip ship, string name, bool debuff, bool shown, float seconds)
    {
        var go = new GameObject("IgiQoL_ShipStatsCheck " + name);
        go.transform.SetParent(ship.transform, false);
        var effect = go.AddComponent<TemporaryEffect>();
        var t = Traverse.Create(effect);
        t.Property("displayName").SetValue(name);
        t.Property("isDebuff").SetValue(debuff);
        t.Property("showInBuffHud").SetValue(shown);
        effect.SetDuration(seconds);
        return effect;
    }

    private static TooltipSection EffectsSection(SpaceShip ship)
    {
        var card = (TooltipCard)BuildCard.Invoke(ship, null);
        foreach (var s in card.sections)
            if (s.title == ShipStats.EffectsTitle) return s;
        return null;
    }

    private static List<string> Rows(TooltipSection section)
    {
        var labels = new List<string>();
        if (section != null)
            foreach (var r in section.rows) labels.Add(r.label);
        return labels;
    }

    private static string TooltipText()
    {
        var parts = new List<string> { "" };
        foreach (var t in UITooltip._current.Content.GetComponentsInChildren<TMP_Text>(true)) parts.Add(t.text);
        parts.Add("");
        return string.Join("\n", parts);
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
