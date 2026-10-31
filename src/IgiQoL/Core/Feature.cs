using System;
using System.Collections;
using BepInEx.Configuration;
using HarmonyLib;

namespace IgiQoL.Core;

public abstract class Feature
{
    public abstract string Id { get; }
    // Heading shown in the mod window; Description is the plain-language explanation under it.
    public virtual string Title => Id;
    public abstract string Description { get; }
    // Name of the window tab that shows this feature's settings; the Settings tab then shows only the on/off switch.
    public virtual string SettingsTab => null;
    public abstract Type[] PatchClasses { get; }

    public ConfigEntry<bool> Enabled;
    public bool Active { get; internal set; }
    public Harmony Harmony;

    public virtual bool HasEnabledEntry => true;

    public virtual void BindConfig(ConfigFile cfg)
    {
        if (HasEnabledEntry) Enabled = cfg.Bind(Id, "Enabled", true, Description);
    }

    public virtual void OnActivated() { }

    public virtual IEnumerator SelfCheck(SelfCheckContext ctx) { yield break; }

    public void Fail(Exception e, string where)
    {
        Active = false;
        FeatureRegistry.Failures.Add(new FeatureFailure(Id, where, e));
        Plugin.Log.LogError($"[{Id}] switched off at {where}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        if (Harmony != null && Plugin.Instance != null)
            Plugin.Instance.StartCoroutine(UnpatchNextFrame());
    }

    private IEnumerator UnpatchNextFrame()
    {
        yield return null;
        Harmony.UnpatchSelf();
    }
}

public sealed class FeatureFailure
{
    public readonly string Id;
    public readonly string Where;
    public readonly Exception Error;

    public FeatureFailure(string id, string where, Exception error)
    {
        Id = id;
        Where = where;
        Error = error;
    }
}
