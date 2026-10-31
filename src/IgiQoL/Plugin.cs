using BepInEx;
using BepInEx.Logging;
using IgiQoL.Core;
using UnityEngine;

namespace IgiQoL;

[BepInPlugin("igi.qol", "Igi's QoL Modpack", PluginInfo.Version)]
public class Plugin : BaseUnityPlugin
{
    public const string TestedGameVersion = "0.8.3.4";

    public static Plugin Instance;
    public static ManualLogSource Log;

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        string version = Application.version;
        Log.LogInfo($"Igi's QoL Modpack {PluginInfo.Version} on game {version} (tested on {TestedGameVersion})");
        if (version != TestedGameVersion)
            Log.LogWarning($"Game version {version} differs from the tested version {TestedGameVersion}");
        FeatureRegistry.Start(Config);
    }
}
