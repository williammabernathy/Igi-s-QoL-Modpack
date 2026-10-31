using System;
using Behaviour.Bootstrap;
using Behaviour.Unit;
using HarmonyLib;

namespace IgiQoL.Core;

public static class GameEvents
{
    public static event Action MainMenuShown;
    public static event Action SaveLoaded;
    public static event Action<SpaceShip> Arrived;

    internal static void RaiseMainMenuShown() => MainMenuShown?.Invoke();
    internal static void RaiseSaveLoaded() => SaveLoaded?.Invoke();
    internal static void RaiseArrived(SpaceShip ship) => Arrived?.Invoke(ship);
}

[HarmonyPatch]
internal static class GameEventPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SceneLoader), nameof(SceneLoader.LoadScenesOnStartGame))]
    private static void SaveLoadedPostfix()
    {
        if (!CoreFeature.Instance.Active) return;
        try
        {
            SaveData.Attach();
            GameEvents.RaiseSaveLoaded();
        }
        catch (Exception e) { CoreFeature.Instance.Fail(e, nameof(GameEventPatches)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpaceShip), nameof(SpaceShip.CompleteTravel))]
    private static void ArrivedPostfix(SpaceShip __instance)
    {
        if (!CoreFeature.Instance.Active) return;
        try { GameEvents.RaiseArrived(__instance); }
        catch (Exception e) { CoreFeature.Instance.Fail(e, nameof(GameEventPatches)); }
    }
}
