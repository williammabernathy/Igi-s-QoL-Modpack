using System.Collections.Generic;
using Behaviour.Unit;
using Source.Personnel;
using Source.Player;
using Source.SpaceShip;

namespace IgiQoL.Game;

public static class Fleet
{
    // The ship you fly, or null outside space (main menu, loading).
    public static SpaceShip Flagship => GameplayManager.Instance != null ? GameplayManager.Instance.spaceShip : null;

    public static bool IsPlayerShip(AbstractUnit u) =>
        u is SpaceShip s && (s.isPlayerFleetShip || Flagship == s);

    // The ship you fly, then your active fleet: no nulls, no duplicates, hired mercenaries only when asked for. Empty outside a game.
    public static List<SpaceShipData> Ships(bool withMercenaries)
    {
        var list = new List<SpaceShipData>();
        var player = GamePlayer.current;
        if (player == null) return list;
        if (player.currentSpaceShip != null) list.Add(player.currentSpaceShip);
        foreach (var s in player.activeFleet)
            if (s != null && (withMercenaries || !(s.commanderData is Mercenary)) && !list.Contains(s)) list.Add(s);
        return list;
    }

    // The live ship of a SpaceShipData: the flown one comes from GameplayManager, the others from their data.
    public static SpaceShip Unit(SpaceShipData d)
    {
        var player = GamePlayer.current;
        if (player != null && d == player.currentSpaceShip) return Flagship;
        return d.unit as SpaceShip;
    }
}
