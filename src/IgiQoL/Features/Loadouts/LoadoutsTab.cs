using System.Collections.Generic;
using Behaviour.UI;
using IgiQoL.Game;
using IgiQoL.UI;
using Source.Player;
using Source.SpaceShip;
using Source.Util;
using TMPro;
using UnityEngine;

namespace IgiQoL.Features.Loadouts;

public static class LoadoutsTab
{
    private static string selectedGuid;
    private static bool includeEcho = true;
    private static string pendingName = "";
    private static ApplyReport lastReport;
    private static string lastReportFor;

    public static void Open(SpaceShipData ship)
    {
        selectedGuid = ship?.guid;
        ModWindow.Open("loadouts");
    }

    private static void Rebuild() => ModWindow.Show("loadouts");

    // Ships the hangar lists: the one you fly, your fleet, then the rest.
    internal static List<SpaceShipData> Ships()
    {
        var player = GamePlayer.current;
        var list = Fleet.Ships(withMercenaries: false);
        foreach (var s in player.spaceShips)
            if (s != null && !list.Contains(s) && !s.IsWorkBayAssigned && !s.IsEmpireFleetAssigned) list.Add(s);
        return list;
    }

    public static void Build(RectTransform content)
    {
        var ships = Ships();
        int index = ships.FindIndex(s => s.guid == selectedGuid);
        if (index < 0) index = 0;
        var ship = ships[index];
        selectedGuid = ship.guid;

        Kit.Muted(content, "A loadout is a saved fit: weapons, modules and boosters (the exact pieces), drones, officers, crew, the skill preset, this ship's QoL settings and, if you tick it, ECHO's settings. Applying one refits the ship from your armory and the ship's cargo, moves crew to and from your reserve (hiring here if short) and lists what it couldn't do.");

        var row = Kit.Row(content);
        Kit.Label(row, "Ship");
        var names = new List<string>();
        foreach (var s in ships) names.Add(s.GetShipName() + (s == GamePlayer.current.currentSpaceShip ? " (flying)" : "") + " - " + s.shipClass.identifier);
        Kit.Dropdown(row, names, index, i => { selectedGuid = ships[i].guid; Rebuild(); }, 320f);

        string reason = LoadoutApply.CannotApplyReason(ship);
        var status = Kit.Muted(content, reason ?? "Docked with a hangar: you can apply loadouts here.");
        if (reason != null) status.color = ColorHelper.orange75;

        Kit.Heading(content, "Save the current fit");
        row = Kit.Row(content);
        var nameField = Kit.Input(row, pendingName.Length > 0 ? pendingName : DefaultName(ship), TMP_InputField.ContentType.Standard, v => pendingName = v, 220f);
        Kit.Toggle(row, "Include ECHO settings", includeEcho, v => includeEcho = v);
        Kit.TextButton(row, "Save", () => SaveCurrent(ship, nameField.text.Trim()), 80f);

        Kit.Heading(content, "Loadouts of " + ship.GetShipName());
        var own = LoadoutStore.For(ship.guid);
        string last = LoadoutStore.LastApplied(ship.guid);
        if (own.Count == 0) Kit.Muted(content, "None saved yet.");
        foreach (var l in own) LoadoutRow(content, ship, l, l.name == last, reason == null, null);

        var shared = LoadoutStore.SameHull(ship);
        if (shared.Count > 0)
        {
            Kit.Heading(content, "Saved on your other " + ship.shipClass.identifier + " hulls");
            foreach (var (owner, l) in shared) LoadoutRow(content, ship, l, false, reason == null, owner);
        }

        if (lastReport != null && lastReportFor == ship.guid)
        {
            Kit.Heading(content, "Last apply");
            foreach (var d in lastReport.done) Kit.Label(content, "Done: " + d);
            foreach (var p in lastReport.problems) Kit.Label(content, p).color = ColorHelper.orange75;
            if (lastReport.done.Count == 0 && lastReport.problems.Count == 0) Kit.Label(content, "Already matched: nothing to change.");
        }
    }

    private static string DefaultName(SpaceShipData ship)
    {
        var own = LoadoutStore.For(ship.guid);
        int n = own.Count + 1;
        while (own.Exists(l => l.name == "Loadout " + n)) n++;
        return "Loadout " + n;
    }

    private static void LoadoutRow(RectTransform content, SpaceShipData ship, Loadout l, bool applied, bool canApply, SpaceShipData owner)
    {
        var row = Kit.Row(content);
        var label = Kit.Label(row, l.name + (applied ? "  (last applied)" : "") + (owner != null ? "  (on " + owner.GetShipName() + ")" : ""));
        Kit.Tooltip(label, l.name, l.Summary());
        var apply = Kit.TextButton(row, "Apply", () => ApplyNow(ship, l), 70f);
        apply.interactable = canApply;
        if (owner == null)
        {
            Kit.TextButton(row, "Update", () => AlertPopup.ShowQuery($"Replace \"{l.name}\" with {ship.GetShipName()}'s current fit?", "Replace", "Cancel",
                () => { LoadoutStore.Put(ship.guid, Loadout.Capture(ship, l.name, l.echo != null)); Rebuild(); }), 70f);
            Kit.TextButton(row, "Rename", () => AlertPopup.ShowInput("New name", v => Rename(ship, l.name, v), "Rename", l.name), 70f);
            Kit.TextButton(row, "Delete", () => AlertPopup.ShowQuery($"Delete loadout \"{l.name}\"?", "Delete", "Cancel",
                () => { LoadoutStore.Delete(ship.guid, l.name); Rebuild(); }), 70f);
        }
        else
        {
            Kit.TextButton(row, "Copy here", () => { LoadoutStore.Put(ship.guid, Copy(l, Unique(ship, l.name))); Rebuild(); }, 90f);
        }
        Kit.Muted(content, l.Summary());
    }

    private static Loadout Copy(Loadout l, string name)
    {
        var copy = Loadout.FromJson(l.ToJson());
        copy.name = name;
        return copy;
    }

    private static string Unique(SpaceShipData ship, string name)
    {
        var own = LoadoutStore.For(ship.guid);
        if (!own.Exists(l => l.name == name)) return name;
        int n = 2;
        while (own.Exists(l => l.name == name + " " + n)) n++;
        return name + " " + n;
    }

    private static void SaveCurrent(SpaceShipData ship, string name)
    {
        if (name.Length == 0) name = DefaultName(ship);
        void Save()
        {
            LoadoutStore.Put(ship.guid, Loadout.Capture(ship, name, includeEcho));
            pendingName = "";
            Rebuild();
        }
        if (LoadoutStore.Find(ship.guid, name) != null) AlertPopup.ShowQuery($"\"{name}\" exists. Replace it with the current fit?", "Replace", "Cancel", Save);
        else Save();
    }

    private static void Rename(SpaceShipData ship, string from, string to)
    {
        to = to?.Trim();
        if (string.IsNullOrEmpty(to) || to == from) return;
        LoadoutStore.Rename(ship.guid, from, Unique(ship, to));
        Rebuild();
    }

    private static void ApplyNow(SpaceShipData ship, Loadout l)
    {
        lastReport = LoadoutApply.Apply(ship, l);
        lastReportFor = ship.guid;
        Plugin.Log.LogInfo($"[Loadouts] applied \"{l.name}\" to {ship.GetShipName()}: done [{string.Join("; ", lastReport.done)}] problems [{string.Join("; ", lastReport.problems)}]");
        Rebuild();
    }
}
