using System.Collections.Generic;
using System.Globalization;
using Behaviour.UI;
using Behaviour.UI.HUD;
using Behaviour.UI.Shared;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.Features.DronesOnArrival;
using IgiQoL.Features.DroneModes;
using IgiQoL.Features.EngagementDistance;
using IgiQoL.Features.MissileRetarget;
using IgiQoL.Features.FleetRetreat;
using IgiQoL.Features.Separation;
using IgiQoL.Features.TargetPriority;
using IgiQoL.Features.CargoPins;
using Source.Player;
using Source.SpaceShip;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.UI;

// Per-ship settings popup (missile retarget, drones, engagement distance, target priority); opened from the fleet strip.
public static class ShipPopup
{
    private static RectTransform root;

    public static bool IsOpen => root != null;

    public static RectTransform Body { get; private set; }

    public static void Open(SpaceShipData ship)
    {
        Close();
        if (ship == null) return;
        root = UiCards.NewChild("IgiQoL_ShipPopup", HudManager.Instance.rootCanvasTransform);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 4; // above the mod window (3)
        root.gameObject.AddComponent<GraphicRaycaster>();
        var frame = CardPopupFrame.Build(root, 460f, Kit.CloseSprite, Close);
        frame.title.text = ship.GetShipName() + " settings";
        Body = frame.body;
        string guid = ship.guid;
        var s = ShipSettings.Edit(guid);

        Kit.Muted(Body, "These apply to this ship only. 'Default' follows the setting on the mod window's Settings tab.");

        var missile = MissileRetarget.Instance;
        if (missile != null && missile.Active)
        {
            var row = Kit.Row(Body, 8f, 26f);
            Kit.Tooltip(Kit.Label(row, "When a missile's target dies"), null, missile.Mode.Description.Description);
            Kit.Dropdown(row, new[] { "Default (" + MissileNames[(int)missile.Mode.Value] + ")", MissileNames[0], MissileNames[1], MissileNames[2] },
                s.missileRetarget.HasValue ? (int)s.missileRetarget.Value + 1 : 0, i =>
                {
                    var e = ShipSettings.Edit(guid);
                    e.missileRetarget = i == 0 ? null : (RetargetMode)(i - 1);
                    ShipSettings.Save(guid);
                });
        }
        else Kit.Muted(Body, "Missile retargeting is turned off on the Settings tab.");

        var drones = DronesOnArrival.Instance;
        if (drones != null && drones.Active)
        {
            var row = Kit.Row(Body, 8f, 26f);
            Kit.Tooltip(Kit.Label(row, "Launch drones on arrival"), null, drones.Default.Description.Description);
            Kit.Dropdown(row, new[] { "Default (" + (drones.Default.Value ? "on" : "off") + ")", "On", "Off" },
                s.dronesOnArrival == null ? 0 : (s.dronesOnArrival.Value ? 1 : 2), i =>
                {
                    ShipSettings.Edit(guid).dronesOnArrival = i == 0 ? null : i == 1;
                    ShipSettings.Save(guid);
                });
        }
        else Kit.Muted(Body, "Drones on arrival is turned off on the Settings tab.");

        var droneModes = DroneModes.Instance;
        if (droneModes != null && droneModes.Active)
        {
            var row = Kit.Row(Body, 8f, 26f);
            Kit.Tooltip(Kit.Label(row, "Drones"), null, "Follow the ship: drones without a target come back once they are further than the distance. Guard the ship: drones only take targets within the distance of the ship and stay inside it; repair drones fix the ship first. Ship's target only: combat and painter drones attack what the ship attacks and otherwise follow it; mining and salvage drones join the ship's asteroid or wreck when they can, else pick one within the distance; repair drones fix the ship first. Game toggle: the HUD's Free roam / Follow ship button decides, as in the game. Your own target clicks still reach the drones.");
            TMP_InputField radius = null;
            RectTransform anchorRow = null;
            Kit.Dropdown(row, DroneModes.ModeNames, (int)s.droneMode, i =>
            {
                var e = ShipSettings.Edit(guid);
                e.droneMode = (DroneMode)i;
                radius.gameObject.SetActive(e.droneMode != DroneMode.Game);
                anchorRow.gameObject.SetActive(e.droneMode != DroneMode.Game);
                ShipSettings.Save(guid);
            }, 200f);
            radius = Kit.Input(row, s.droneRadius.ToString("0.#", CultureInfo.InvariantCulture), TMP_InputField.ContentType.DecimalNumber, v =>
            {
                var e = ShipSettings.Edit(guid);
                if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) e.droneRadius = Mathf.Clamp(f, 2f, 40f);
                radius.SetTextWithoutNotify(e.droneRadius.ToString("0.#", CultureInfo.InvariantCulture));
                ShipSettings.Save(guid);
            }, 50f);
            radius.gameObject.SetActive(s.droneMode != DroneMode.Game);

            // "The ship" in the modes above: this one, or another ship of your fleet the drones stay with.
            anchorRow = Kit.Row(Body, 8f, 26f);
            Kit.Tooltip(Kit.Label(anchorRow, "Drones stay with"), null, "The ship the drone mode is about: drones follow it, guard it, take its target and repair it first. When that ship isn't at the same place (travelling, docked, destroyed), the drones stay with their own ship.");
            var names = new List<string> { "This ship" };
            var guids = new List<string> { null };
            foreach (var other in Fleet.Ships(withMercenaries: true))
            {
                if (other.guid == guid || guids.Contains(other.guid)) continue;
                names.Add(other.GetShipName());
                guids.Add(other.guid);
            }
            int at = guids.IndexOf(s.droneAnchor);
            if (at < 0)
            {
                names.Add("(a ship not in your fleet)");
                guids.Add(s.droneAnchor);
                at = guids.Count - 1;
            }
            Kit.Dropdown(anchorRow, names, at, i =>
            {
                ShipSettings.Edit(guid).droneAnchor = guids[i];
                ShipSettings.Save(guid);
            }, 200f);
            anchorRow.gameObject.SetActive(s.droneMode != DroneMode.Game);
        }

        var engagement = EngagementDistance.Instance;
        if (engagement != null && engagement.Active)
        {
            var row = Kit.Row(Body, 8f, 26f);
            // A tooltip, not a line under the row: the popup has no scroll, and with a full target rule chain the
            // line pushed it off screen at 130% text.
            Kit.Tooltip(Kit.Label(row, "Engagement distance"), null, "Auto: the game decides how close to get. Weapons' max range: stay as far back as the shortest-range weapon that can hit the target allows. Fixed distance: stay this many units away (1 to 30; the game never holds further than 30).");
            TMP_InputField field = null;
            Kit.Dropdown(row, new[] { "Auto (game default)", "Weapons' max range", "Fixed distance" }, (int)s.engagement, i =>
            {
                var e = ShipSettings.Edit(guid);
                e.engagement = (EngagementMode)i;
                field.gameObject.SetActive(e.engagement == EngagementMode.Fixed);
                ShipSettings.Save(guid);
            });
            field = Kit.Input(row, s.engagementDistance.ToString("0.#", CultureInfo.InvariantCulture), TMP_InputField.ContentType.DecimalNumber, v =>
            {
                var e = ShipSettings.Edit(guid);
                if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    e.engagementDistance = Mathf.Clamp(f, 1f, 30f);
                field.SetTextWithoutNotify(e.engagementDistance.ToString("0.#", CultureInfo.InvariantCulture));
                ShipSettings.Save(guid);
            }, 70f);
            field.gameObject.SetActive(s.engagement == EngagementMode.Fixed);
        }
        else Kit.Muted(Body, "Engagement distance is turned off on the Settings tab.");

        var separation = Separation.Instance;
        if (separation != null && separation.Active)
        {
            var row = Kit.Row(Body, 8f, 26f);
            Kit.Tooltip(Kit.Label(row, "Keep distance from other ships"), null, separation.Default.Description.Description);
            Kit.Dropdown(row, new[] { "Default (" + (separation.Default.Value ? "on" : "off") + ")", "On", "Off" },
                s.separation == null ? 0 : (s.separation.Value ? 1 : 2), i =>
                {
                    ShipSettings.Edit(guid).separation = i == 0 ? null : i == 1;
                    ShipSettings.Save(guid);
                });
        }

        var retreat = FleetRetreat.Instance;
        if (retreat != null && retreat.Active)
        {
            var row = Kit.Row(Body, 8f, 26f);
            Kit.Tooltip(Kit.Label(row, "Retreat for repairs"), null, "Fleet ships only, not the ship you fly. When a hit leaves the ship below this hull %, it leaves the fight and repairs for 120 s, then comes back, as the game does below 10%. A hired extra mercenary leaves for good, as in the game.");
            Kit.Dropdown(row, FleetRetreat.Names, Mathf.Max(0, System.Array.IndexOf(FleetRetreat.Values, s.retreatBelow)), i =>
            {
                ShipSettings.Edit(guid).retreatBelow = FleetRetreat.Values[i];
                ShipSettings.Save(guid);
            }, 200f);
        }

        var targeting = TargetPriority.Instance;
        if (targeting != null && targeting.Active) AddTargeting(ship, guid, s, targeting);
        else Kit.Muted(Body, "Target priority is turned off on the Settings tab.");

        var pins = CargoPins.Instance;
        if (pins != null && pins.Active) AddPins(ship, guid, s);

        frame.AddButton("Close", Close, 110f);
    }

    public static void Close()
    {
        if (root != null) Object.Destroy(root.gameObject);
        root = null;
        Body = null;
    }

    private static readonly float?[] RepickValues = { null, 0f, 2f, 5f, 10f };
    private static readonly int?[] FinishValues = { null, 0, 25, 50 };

    private static void AddTargeting(SpaceShipData ship, string guid, ShipSettings s, TargetPriority targeting)
    {
        var row = Kit.Row(Body, 8f, 26f);
        Kit.Tooltip(Kit.Label(row, "Target choice"), null, targeting.Rule.Description.Description + StanceNote(ship));
        var rules = new string[TargetPriority.RuleNames.Length + 1];
        rules[0] = "Default (" + TargetPriority.RuleNames[(int)targeting.Rule.Value].Replace(" (game default)", "") + ")";
        TargetPriority.RuleNames.CopyTo(rules, 1);
        var then = new string[TargetPriority.RuleNames.Length + 1];
        then[0] = "None";
        TargetPriority.RuleNames.CopyTo(then, 1);

        // The chain as three dropdowns: the first rule (or Default), then up to two "Then by" rules. "None" ends the
        // chain; the rows after it hide but keep their choice, so turning it back on restores it.
        var chain = s.targetRules;
        int first = chain.Count > 0 ? (int)chain[0] + 1 : 0;
        int second = chain.Count > 1 ? (int)chain[1] + 1 : 0;
        int third = chain.Count > 2 ? (int)chain[2] + 1 : 0;
        RectTransform secondRow = null, thirdRow = null;
        void Apply()
        {
            var e = ShipSettings.Edit(guid);
            e.targetRules.Clear();
            if (first > 0)
            {
                e.targetRules.Add((TargetRule)(first - 1));
                if (second > 0)
                {
                    e.targetRules.Add((TargetRule)(second - 1));
                    if (third > 0) e.targetRules.Add((TargetRule)(third - 1));
                }
            }
            secondRow.gameObject.SetActive(first > 0);
            thirdRow.gameObject.SetActive(first > 0 && second > 0);
            ShipSettings.Save(guid);
        }
        Kit.Dropdown(row, rules, first, i => { first = i; Apply(); }, 230f);
        const string thenTip = "Decides between enemies the rule above sees as about the same: for example less than 10 points of hull apart, 20% of the distance, or less than the lead set for \"Weakest to my weapons\" and \"Debuffed first\" on the Settings tab. None ends the chain; after the last rule, the nearest enemy wins. Re-checks walk the same chain. \"Attacking my flagship\" only pulls the ship onto whoever hits your flagship when it's the first rule.";
        secondRow = Kit.Row(Body, 8f, 26f);
        Kit.Tooltip(Kit.Label(secondRow, "Then by"), null, thenTip);
        Kit.Dropdown(secondRow, then, second, i => { second = i; Apply(); }, 230f);
        thirdRow = Kit.Row(Body, 8f, 26f);
        Kit.Tooltip(Kit.Label(thirdRow, "Then by"), null, thenTip);
        Kit.Dropdown(thirdRow, then, third, i => { third = i; Apply(); }, 230f);
        secondRow.gameObject.SetActive(first > 0);
        thirdRow.gameObject.SetActive(first > 0 && second > 0);

        row = Kit.Row(Body, 8f, 26f);
        Kit.Tooltip(Kit.Label(row, "Re-check the target"), null, targeting.RepickSeconds.Description.Description);
        float repick = targeting.RepickSeconds.Value;
        Kit.Dropdown(row, new[] { "Default (" + (repick > 0f ? "every " + repick.ToString("0.#", CultureInfo.InvariantCulture) + " s" : "never") + ")", "Never", "Every 2 s", "Every 5 s", "Every 10 s" },
            Mathf.Max(0, System.Array.IndexOf(RepickValues, s.repickSeconds)), i =>
            {
                ShipSettings.Edit(guid).repickSeconds = RepickValues[i];
                ShipSettings.Save(guid);
            }, 200f);

        row = Kit.Row(Body, 8f, 26f);
        Kit.Tooltip(Kit.Label(row, "Finish weak targets"), null, targeting.FinishBelowPercent.Description.Description);
        int finish = targeting.FinishBelowPercent.Value;
        Kit.Dropdown(row, new[] { "Default (" + (finish > 0 ? "below " + finish + "%" : "off") + ")", "Off", "Below 25% hull", "Below 50% hull" },
            Mathf.Max(0, System.Array.IndexOf(FinishValues, s.finishBelow)), i =>
            {
                ShipSettings.Edit(guid).finishBelow = FinishValues[i];
                ShipSettings.Save(guid);
            }, 200f);

        row = Kit.Row(Body, 8f, 26f);
        Kit.Tooltip(Kit.Label(row, "Avoid enemies above level"), null, "Enemies above this level are only picked when nothing else is in range. 0 turns this off.");
        TMP_InputField level = null;
        level = Kit.Input(row, s.maxLevel.ToString(CultureInfo.InvariantCulture), TMP_InputField.ContentType.IntegerNumber, v =>
        {
            var e = ShipSettings.Edit(guid);
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) e.maxLevel = Mathf.Clamp(n, 0, 999);
            level.SetTextWithoutNotify(e.maxLevel.ToString(CultureInfo.InvariantCulture));
            ShipSettings.Save(guid);
        }, 70f);

        row = Kit.Row(Body, 8f, 26f);
        Kit.Tooltip(Kit.Label(row, "Only attack within (units of the flagship)"), null, "Fleet ships only: they don't pick enemies further than this from the ship you fly, and drop a target that moves past it. 0 turns this off.");
        TMP_InputField leash = null;
        leash = Kit.Input(row, s.leash.ToString("0.#", CultureInfo.InvariantCulture), TMP_InputField.ContentType.DecimalNumber, v =>
        {
            var e = ShipSettings.Edit(guid);
            if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) e.leash = Mathf.Clamp(f, 0f, 200f);
            leash.SetTextWithoutNotify(e.leash.ToString("0.#", CultureInfo.InvariantCulture));
            ShipSettings.Save(guid);
        }, 70f);
        Kit.Muted(Body, "Target choice only changes what a ship picks by itself. Your clicks on the flagship and attack orders still decide first; the Escort and Defensive stances pick the ship's target, and weapon rules still apply under them.");

        var hardpoints = ship.hardpoints;
        var follow = new string[TargetPriority.RuleNames.Length + 1];
        follow[0] = "Follow the ship";
        TargetPriority.RuleNames.CopyTo(follow, 1);
        for (int h = 0; h < hardpoints.Length; h++)
        {
            var item = hardpoints[h];
            if (!TargetPriority.IsCombatWeapon(item)) continue;
            int index = h;
            s.weaponRules.TryGetValue(index, out var rule);
            string name = (index + 1) + ". " + item.displayName;
            if (rule != null && rule.weapon != null && rule.weapon != item.identifier) name += " (refitted)";
            row = Kit.Row(Body, 8f, 26f);
            Kit.Tooltip(Kit.Label(row, name), null, "What this weapon shoots while the ship is fighting. Follow the ship: the ship's target, as in the game. Any other choice: the best enemy by that rule among those this weapon can hit and has in range; with none in range it follows the ship. The ship's re-check and finish settings apply to weapons too.");
            Kit.Dropdown(row, follow, rule != null ? (int)rule.rule + 1 : 0, i =>
            {
                var e = ShipSettings.Edit(guid);
                if (i == 0) e.weaponRules.Remove(index);
                else e.weaponRules[index] = new WeaponRule((TargetRule)(i - 1), item.identifier);
                ShipSettings.Save(guid);
            }, 200f);
        }
    }

    // The fleet stance decides when the chain gets a say: Defensive and Escort set a manual target the chain never
    // overrides. A tooltip line, not a row: the popup has no scroll.
    private static string StanceNote(SpaceShipData ship)
    {
        if (ship == GamePlayer.current.currentSpaceShip) return "";
        string name, help;
        switch (ship.fleetStance)
        {
            case FleetStance.Aggressive: name = "Aggressive"; help = "The chain decides every pick."; break;
            case FleetStance.Defensive: name = "Defensive"; help = "The game sets its target itself while it's being hit (whoever hits it most) or while your flagship has a target; the chain decides only when there is nothing to copy."; break;
            case FleetStance.Escort: name = "Escort"; help = "The game copies your flagship's target; the chain decides only when the flagship has none."; break;
            case FleetStance.HoldFire: name = "Hold fire"; help = "It doesn't pick targets at all."; break;
            default: return "";
        }
        return "\n\nThis ship's stance is " + name + ". " + help;
    }

    // Two controls and a line: the ammo switch, then one pick-and-toggle row so the popup's height doesn't grow with the pins.
    private static void AddPins(SpaceShipData ship, string guid, ShipSettings s)
    {
        Kit.Toggle(Body, "Keep the ammo this ship fires on board", s.keepAmmo, on =>
        {
            ShipSettings.Edit(guid).keepAmmo = on;
            ShipSettings.Save(guid);
            CargoPins.RefreshMarks(ship);
        });

        var aboard = CargoPins.PinChoices(ship, out var orphans);
        var ids = new List<string>();
        var names = new List<string>();
        foreach (var item in aboard)
        {
            ids.Add(item.identifier);
            names.Add(Translation.Translate(item.displayName) + (s.pins.Contains(item.identifier) ? " (pinned)" : ""));
        }
        foreach (var id in orphans)
        {
            ids.Add(id);
            names.Add(CargoPins.PinName(id) + " (pinned, not aboard)");
        }
        if (names.Count == 0) names.Add("Nothing aboard");

        var row = Kit.Row(Body, 8f, 26f);
        Kit.Tooltip(Kit.Label(row, "Pin an item"), null, "Pinned items stay aboard when the fleet cargo's crown button moves everything to the flagship. Pick an item aboard, or a pin to remove.");
        Button toggle = null;
        var choice = Kit.Dropdown(row, names, 0, i =>
        {
            if (i < ids.Count) toggle.GetComponentInChildren<TMP_Text>().text = ShipSettings.Get(guid).pins.Contains(ids[i]) ? "Unpin" : "Pin";
        }, 200f);
        toggle = Kit.TextButton(row, ids.Count > 0 && s.pins.Contains(ids[0]) ? "Unpin" : "Pin", () =>
        {
            int i = choice.GetIndex();
            if (i >= ids.Count) return;
            var e = ShipSettings.Edit(guid);
            if (!e.pins.Remove(ids[i])) e.pins.Add(ids[i]);
            ShipSettings.Save(guid);
            CargoPins.RefreshMarks(ship);
            Open(ship);
        }, 70f);

        var pinned = new List<string>();
        foreach (var id in s.pins) pinned.Add(CargoPins.PinName(id));
        pinned.Sort(System.StringComparer.CurrentCulture);
        Kit.Muted(Body, (pinned.Count > 0 ? "Pinned: " + string.Join(", ", pinned) + ". " : "")
            + "Applies while this ship is in your fleet. In its fleet cargo, " + CargoPins.Instance.PinKey.Value + " over a stack pins or unpins it.");
    }

    private static readonly string[] MissileNames = { "Fly on", "Weapon's target", "Nearest enemy" };
}
