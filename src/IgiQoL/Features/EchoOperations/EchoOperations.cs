using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Gameplay;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using Source.Dungeon;

namespace IgiQoL.Features.EchoOperations;

public enum EchoStealth { Vanilla, Silent, Balanced, Aggressive }

public enum EchoAmmo { Vanilla, Standard, ArmourPiercing, Hollow }

public sealed class EchoOperations : Feature
{
    public static EchoOperations Instance;

    public EchoOperations() { Instance = this; }

    public override string Id => "EchoOperations";
    public override string Title => "ECHO's Operations";
    public override string Description => "The stealth, ammo and buyout choices for operations that ECHO starts on its own.";
    public override string SettingsTab => "Operations";
    public override Type[] PatchClasses => new[] { typeof(EchoOperationsPatch) };

    public ConfigEntry<EchoStealth> StealthMode;
    public ConfigEntry<EchoAmmo> AmmoType;
    public ConfigEntry<bool> AutoBuyOut;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        StealthMode = cfg.Bind(Id, "StealthMode", EchoStealth.Vanilla, SettingInfo.Describe("ECHO: Stealth",
            "How the boarding crew of operations that ECHO starts advance. Silent: slower and weaker (-20% attack) but fewer casualties and fewer alerts. Balanced: normal. Aggressive: +20% attack but enemies notice much sooner. 'Same as yours' uses whatever Stealth you last picked yourself (game default).",
            "Same as yours", "Silent", "Balanced", "Aggressive"));
        AmmoType = cfg.Bind(Id, "AmmoType", EchoAmmo.Vanilla, SettingInfo.Describe("ECHO: Ammo",
            "The ammo that the boarding crew of operations ECHO starts use. Standard: normal damage against every crew type. Armor Piercing: strong against armored crew (Marines, Gunners) and does the most structural damage to the ship. Hollow: normal against unarmored crew, less effective against armored crew, and does the least structural damage. 'Same as yours' uses whatever Ammo you last picked yourself (game default).",
            "Same as yours", "Standard", "Armor Piercing", "Hollow"));
        AutoBuyOut = cfg.Bind(Id, "AutoBuyOut", false, SettingInfo.Describe("ECHO: Auto Accept Buyouts",
            "During an operation, defenders sometimes offer to switch to your side for credits (a buyout offer). When on, ECHO accepts these offers in operations it starts, if you can pay. When off, ECHO never accepts them. Game default: off."));
    }

    public void Apply(DungeonOptions options)
    {
        switch (StealthMode.Value)
        {
            case EchoStealth.Silent: options.stealthMode = Source.Dungeon.StealthMode.Silent; break;
            case EchoStealth.Balanced: options.stealthMode = Source.Dungeon.StealthMode.Balanced; break;
            case EchoStealth.Aggressive: options.stealthMode = Source.Dungeon.StealthMode.Aggressive; break;
        }
        switch (AmmoType.Value)
        {
            case EchoAmmo.Standard: options.ammoType = Source.Dungeon.AmmoType.Standard; break;
            case EchoAmmo.ArmourPiercing: options.ammoType = Source.Dungeon.AmmoType.ArmourPiercing; break;
            case EchoAmmo.Hollow: options.ammoType = Source.Dungeon.AmmoType.Hollow; break;
        }
        if (AutoBuyOut.Value) options.autoAcceptBuyOut = true;
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        var stealth = StealthMode.Value;
        var ammo = AmmoType.Value;
        bool buyOut = AutoBuyOut.Value;
        try
        {
            StealthMode.Value = EchoStealth.Silent;
            AmmoType.Value = EchoAmmo.Hollow;
            AutoBuyOut.Value = true;
            var o = IdleManager.BuildAutonomousDungeonOptions(new Dictionary<string, int>(), true);
            string detail = $"stealth={o.stealthMode} ammo={o.ammoType} buyOut={o.autoAcceptBuyOut} autoMove={o.autoMove}";
            if (o.stealthMode == Source.Dungeon.StealthMode.Silent && o.ammoType == Source.Dungeon.AmmoType.Hollow && o.autoAcceptBuyOut && o.autoMove)
                ctx.Pass("options", detail);
            else
                ctx.Fail("options", detail);
        }
        finally
        {
            StealthMode.Value = stealth;
            AmmoType.Value = ammo;
            AutoBuyOut.Value = buyOut;
        }
        yield break;
    }
}

[HarmonyPatch(typeof(IdleManager), nameof(IdleManager.BuildAutonomousDungeonOptions))]
internal static class EchoOperationsPatch
{
    private static void Postfix(DungeonOptions __result)
    {
        if (!EchoOperations.Instance.Active) return;
        try { EchoOperations.Instance.Apply(__result); }
        catch (Exception e) { EchoOperations.Instance.Fail(e, nameof(EchoOperationsPatch)); }
    }
}
