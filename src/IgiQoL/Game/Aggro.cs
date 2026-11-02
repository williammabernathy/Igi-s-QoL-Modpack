using System.Collections.Generic;
using Behaviour.Unit;
using Behaviour.Weapons;
using HarmonyLib;
using Source.Galaxy;
using Source.SpaceShip.Auto;
using Source.Util;

namespace IgiQoL.Game;

// Who a unit is after and who is after it, read from the game's own state without allocating.
public static class Aggro
{
    private static readonly AccessTools.FieldRef<ThreatTable, Dictionary<AbstractUnit, float>> ThreatOf =
        AccessTools.FieldRefAccess<ThreatTable, Dictionary<AbstractUnit, float>>("threat");
    private static readonly AccessTools.FieldRef<TargetableUnit, Dictionary<AbstractUnit, TargetableTracker>> TargetedBy =
        AccessTools.FieldRefAccess<TargetableUnit, Dictionary<AbstractUnit, TargetableTracker>>("targetedBy");

    private static readonly System.Comparison<KeyValuePair<AbstractUnit, float>> HighestFirst = (a, b) => b.Value.CompareTo(a.Value);

    // The unit's current target: its manual target, else its auto target. Null when it has none.
    public static TargetableUnit Target(AbstractUnit unit)
    {
        var provider = unit.targetProvider;
        return provider != null ? provider.priorityTarget : null;
    }

    // The threat table of an NPC combat ship (CombatActions), live entries only, highest first. False when the unit
    // keeps none: your ships and every non-combat unit choose targets another way.
    public static bool Threat(AbstractUnit unit, List<KeyValuePair<AbstractUnit, float>> into)
    {
        into.Clear();
        if (unit.autoActions is not CombatActions combat || combat.threat == null) return false;
        foreach (var entry in ThreatOf(combat.threat))
            if (entry.Value > 0f && Alive(entry.Key)) into.Add(entry);
        into.Sort(HighestFirst);
        return true;
    }

    // Units whose current target is this one. The candidates come from the game's list of units that picked or hit
    // it in the last 3 s; that list also holds attackers of nearby allies (BroadcastThreatToAllies), hence the check.
    public static void Attackers(TargetableUnit unit, List<AbstractUnit> into)
    {
        into.Clear();
        foreach (var u in TargetedBy(unit).Keys)
            if (Alive(u) && Target(u) == unit) into.Add(u);
    }

    // Your ships by the name you gave them, everything else by its translated name; drones say so.
    public static string Name(TargetableUnit t)
    {
        if (t == null) return "?";
        if (t is SpaceShip ship && ship.spaceShipData != null && ship.spaceShipData.faction == Faction.player) return ship.spaceShipData.GetShipName();
        if (t is Drone drone) return DroneName(drone) + " (drone)";
        return Translation.Translate(t.targetName);
    }

    // The game builds a drone's display key from its object name, "(Clone)" included, so the key never matches and the
    // raw "@Drone..." text shows. Look the key up without the suffix, and fall back to the bare object name.
    private static string DroneName(Drone drone)
    {
        string bare = drone.name.Replace("(Clone)", "").Trim();
        string key = "@Drone" + bare.Replace(" ", "");
        return Translation.Exists(key) ? Translation.Translate(key) : bare;
    }

    // The same test the threat table uses to drop an entry (ThreatTable.CleanupDeadEntries).
    private static bool Alive(AbstractUnit u) => u != null && !u.isDestroyed && u.gameObject.activeSelf;
}
