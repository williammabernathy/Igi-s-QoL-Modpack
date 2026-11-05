using System.Collections.Generic;
using Behaviour.Managers;
using Behaviour.Unit;
using Behaviour.Weapons;
using HarmonyLib;
using Source.Galaxy;
using Source.Util;

namespace IgiQoL.Game;

// Who a unit is after and who is after it, read from the game's own state without allocating.
public static class Aggro
{
    private static readonly AccessTools.FieldRef<TargetableUnit, Dictionary<AbstractUnit, TargetableTracker>> TargetedBy =
        AccessTools.FieldRefAccess<TargetableUnit, Dictionary<AbstractUnit, TargetableTracker>>("targetedBy");

    private static readonly System.Comparison<(AbstractUnit unit, float threat)> HighestFirst = (a, b) => b.threat.CompareTo(a.threat);

    // The unit's current target: its manual target, else its auto target. Null when it has none.
    public static TargetableUnit Target(AbstractUnit unit)
    {
        var provider = unit.targetProvider;
        return provider != null ? provider.priorityTarget : null;
    }

    // The threat table the unit picks its targets from, live entries only, highest first. Every faction at a location
    // shares one table (FactionFleet.threat), fed by the hits on any of its units and fading 15% a second; your fleet
    // has one too. False when the unit picks no targets (no combat module, a drone, a fighter) or is in no fleet.
    public static bool Threat(AbstractUnit unit, List<(AbstractUnit unit, float threat)> into)
    {
        into.Clear();
        if (!FactionFleet.PicksTargets(unit)) return false;
        var fleet = PoiFleets.For(unit)?.FleetOf(unit);
        if (fleet == null) return false;
        fleet.threat.GetThreats(into);
        for (int i = into.Count - 1; i >= 0; i--)
            if (into[i].threat <= 0f) into.RemoveAt(i);
        into.Sort(HighestFirst);
        return true;
    }

    // Units whose current target is this one. The candidates come from the game's list of units that picked or hit
    // it in the last 3 s, which also keeps a unit that has since moved on to another target, hence the check.
    public static void Attackers(TargetableUnit unit, List<AbstractUnit> into)
    {
        into.Clear();
        foreach (var u in TargetedBy(unit).Keys)
            if (FactionFleet.IsLive(u) && Target(u) == unit) into.Add(u);
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
}
