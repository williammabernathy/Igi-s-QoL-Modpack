using System.Collections.Generic;
using Behaviour.Ability;
using Behaviour.Item;
using Behaviour.Item.Usable;
using IgiQoL.Game;
using IgiQoL.UI;
using Source.Item;
using Source.Player;
using Source.Util;
using TMPro;
using UnityEngine;

namespace IgiQoL.Features.EchoKit;

// Rule rows on the ECHO tab: one per ability on the flagship and per deployable type in its cargo or your armory.
public static class EchoKitSection
{
    private static readonly string[] WhenNames = { "Off", "In combat, when ready", "Shields below %", "Armour below %", "Hull below %", "Targeted by at least", "Whenever ready" };

    public static void Build(RectTransform content)
    {
        Kit.Muted(content, "Every rule except \"Whenever ready\" only fires during a fight (your ship took damage in the last 10 s or has a target). Abilities need to be off cooldown; aimed abilities go on ECHO's target (hostile), your most damaged ship (friendly), the asteroid the flagship is mining (asteroid), or the nearest wreck whose credits or cargo box are still there (wreck), and wait while there is no such target. A deployable is dropped next to the flagship from its cargo, one every 2 s at most, when the game's own deploy cooldown allows.");

        var ship = Fleet.Flagship;
        Kit.Heading(content, "Abilities" + (ship != null ? " of " + ship.spaceShipData.GetShipName() : ""));
        if (ship == null) Kit.Muted(content, "Shown while you're in space.");
        else
        {
            int n = 0;
            foreach (var ability in ship.GetActivatedAbilities())
            {
                n++;
                string name = Translation.Translate(ability.displayName) + (ability.toggledAbility ? " (toggle)" : "") + $" - {ability.cooldown:0} s cooldown";
                if (!EchoKit.CanAutomate(ability.targetType))
                {
                    Kit.Muted(content, name + ": needs a " + SettingRows.Pretty(ability.targetType.ToString()).ToLowerInvariant() + " target, which ECHO can't pick.");
                    continue;
                }
                RuleRow(content, name, EchoKit.Abilities, ability.identifier, ability.descriptionText);
            }
            if (n == 0) Kit.Muted(content, "This ship has no abilities to activate.");
        }

        Kit.Heading(content, "Deployables");
        var types = DeployableTypes();
        if (types.Count == 0) Kit.Muted(content, "No deployables in your flagship's cargo or the armory. Rules for ones you carry later can be set here once you have one.");
        foreach (var item in types)
            RuleRow(content, Translation.Translate(item.displayName), EchoKit.Deployables, item.identifier, item.description);
        Kit.Muted(content, "Only the flagship's cargo is used, so keep deployables aboard for ECHO.");
    }

    private static List<InventoryItemType> DeployableTypes()
    {
        var list = new List<InventoryItemType>();
        var seen = new HashSet<string>();
        void Scan(Inventory inv)
        {
            if (inv == null) return;
            foreach (var e in inv.items)
                if (e?.item != null && e.item.TryGetComponent<DefensiveTurretItem>(out _) && seen.Add(e.item.identifier)) list.Add(e.item);
        }
        Scan(GamePlayer.current?.currentSpaceShip?.cargo);
        Scan(Inventory.global);
        foreach (var id in EchoKit.Deployables.Keys)
            if (seen.Add(id) && InventoryItemType.TryGet(id, out var t)) list.Add(t);
        return list;
    }

    private static void RuleRow(RectTransform content, string name, Dictionary<string, KitRule> rules, string id, string help)
    {
        var rule = EchoKit.Rule(rules, id);
        var row = Kit.Row(content, 6f, 26f);
        var label = Kit.Label(row, name);
        if (!string.IsNullOrEmpty(help)) Kit.Tooltip(label, name, Translation.Translate(help));
        int when = rule != null ? (int)rule.when : 0;
        int value = rule?.value ?? 50;
        TMP_InputField field = null;
        Kit.Dropdown(row, WhenNames, when, i =>
        {
            int v = field != null && int.TryParse(field.text, out int parsed) ? parsed : value;
            EchoKit.SetRule(rules, id, (KitWhen)i, v);
        }, 190f);
        field = Kit.Input(row, value.ToString(), TMP_InputField.ContentType.IntegerNumber, text =>
        {
            var current = EchoKit.Rule(rules, id);
            int v = int.TryParse(text, out int parsed) ? Mathf.Clamp(parsed, 1, 100) : 50;
            field.SetTextWithoutNotify(v.ToString());
            if (current != null) EchoKit.SetRule(rules, id, current.when, v);
        }, 50f);
        Kit.Tooltip(field.textComponent, "Threshold", "Percent for the shield, armour and hull rules; number of enemies for 'Targeted by at least'.");
    }
}
