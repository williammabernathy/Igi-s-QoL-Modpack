using BepInEx;
using BepInEx.Logging;
using Behaviour.Item;
using HarmonyLib;

namespace HelloVanguard;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "local.hellovanguard";
    public const string Name = "Hello Vanguard";
    public const string Version = "0.1.0";

    internal static ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"{Name} loaded. Items already registered at plugin load: {InventoryItemType.allItems.Count}");
        new Harmony(Guid).PatchAll();
    }
}

// Runs after the game fills its item registry (Bootstrapper.Init -> InventoryItemType.LoadAll).
[HarmonyPatch(typeof(InventoryItemType), nameof(InventoryItemType.LoadAll))]
internal static class InventoryItemTypeLoadAllPatch
{
    private static void Postfix() =>
        Plugin.Log.LogInfo($"InventoryItemType.LoadAll finished: {InventoryItemType.allItems.Count} items");
}
