using Behaviour.Unit;
using Source.SpaceShip;

namespace IgiQoL.Game;

public static class Fleet
{
    public static bool IsPlayerShip(AbstractUnit u) =>
        u is SpaceShip s && (s.isPlayerFleetShip || (GameplayManager.Instance != null && GameplayManager.Instance.spaceShip == s));

    public static bool IsPlayerShip(SpaceShipData d) => IsPlayerShip(d.unit);
}
