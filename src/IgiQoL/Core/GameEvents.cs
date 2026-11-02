using System;
using Behaviour.Bootstrap;
using Behaviour.UI.HUD;
using Behaviour.Unit;
using HarmonyLib;

namespace IgiQoL.Core;

public static class GameEvents
{
    public static event Action MainMenuShown;
    public static event Action SaveLoaded;
    // The HUD's Start: once per save load, after SaveLoaded, with HudManager.Instance and its serialized buttons set.
    public static event Action HudReady;
    public static event Action<SpaceShip> Arrived;

    internal static void RaiseMainMenuShown() => Raise(MainMenuShown, nameof(MainMenuShown));
    internal static void RaiseSaveLoaded() => Raise(SaveLoaded, nameof(SaveLoaded));
    internal static void RaiseHudReady() => Raise(HudReady, nameof(HudReady));

    internal static void RaiseArrived(SpaceShip ship)
    {
        if (Arrived == null) return;
        foreach (Action<SpaceShip> h in Arrived.GetInvocationList())
        {
            if (h.Target is Feature { Active: false }) continue;
            try { h(ship); }
            catch (Exception e) { Report(h, e, nameof(Arrived)); }
        }
    }

    private static void Raise(Action ev, string name)
    {
        if (ev == null) return;
        foreach (Action h in ev.GetInvocationList())
        {
            if (h.Target is Feature { Active: false }) continue;
            try { h(); }
            catch (Exception e) { Report(h, e, name); }
        }
    }

    private static void Report(Delegate h, Exception e, string name)
    {
        string handler = h.Method.DeclaringType.Name + "." + h.Method.Name;
        Plugin.Log.LogError($"[Core] {name} handler {handler}: {e}");
        if (h.Target is Feature f) f.Fail(e, name);
    }
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
        catch (Exception e) { Plugin.Log.LogError($"[Core] {nameof(GameEventPatches)}: {e}"); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HudManager), "Start")]
    private static void HudStartPostfix()
    {
        if (!CoreFeature.Instance.Active) return;
        try { GameEvents.RaiseHudReady(); }
        catch (Exception e) { Plugin.Log.LogError($"[Core] {nameof(GameEventPatches)}: {e}"); }
    }
}
