using System;
using System.Collections;
using Behavior.UI.GalaxyMap;
using Behaviour.UI;
using Behaviour.UI.Missions;
using Behaviour.Weapons;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using Source.MissionSystem;
using Source.Player;
using Source.Util;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.MissionFit;

// The station mission board offers mining, salvage and repair work whether or not any of your ships carries the gear
// for it, and nothing on the board says so; ECHO finds out only when it refuses the mission ("no suitable loadout").
public sealed class MissionFit : Feature
{
    public static MissionFit Instance;

    public MissionFit() { Instance = this; }

    public override string Id => "MissionFit";
    public override string Title => "Mission fit";
    public override string Description => "Greys out station mission board offers none of your ships has the equipment for, such as a mining mission when no ship carries a mining turret, mining drones or a hangar that mines. The row keeps its place and can still be picked and accepted, and a red \"No fit\" chip says what's missing. Your flagship and fleet ships count, wherever they are; mercenaries don't. ECHO checks only the flagship, so it can still refuse an offer that isn't greyed out.";
    public override Type[] PatchClasses => new[] { typeof(MissionFitPatches) };

    internal const string ChipName = "IgiQoL_NoFit";
    internal const float DimAlpha = 0.45f;

    // The work an offer needs: its first objective with a gameplay type (as Mission.primaryGameplayType picks it) and
    // that objective's layer, which generated mining and salvage missions set to Surface or Core. False for work any
    // ship can do (none, or cargo), as SpaceShipData.HasLoadout answers it.
    internal static bool Needs(Mission mission, out GameplayType type, out TargetLayer layer)
    {
        foreach (var step in mission.steps)
            foreach (var objective in step.objectives)
                if (objective.gameplayType != GameplayType.Generic)
                {
                    type = objective.gameplayType;
                    layer = objective.targetLayer ?? TargetLayer.Both;
                    return type != GameplayType.Cargo;
                }
        type = GameplayType.Generic;
        layer = TargetLayer.Both;
        return false;
    }

    // One of your ships (mercenaries left out) has a turret, drone bay, torpedo bay or hangar bay for the work.
    // HasLoadout reads the fitted items from data, so ships away from your location count too.
    internal static bool Fits(GameplayType type, TargetLayer layer)
    {
        foreach (var ship in Fleet.Ships(withMercenaries: false))
            if (ship.HasLoadout(type, layer)) return true;
        return false;
    }

    // "Mining (Core)", "Salvage (Structural)", "Combat"; the layer names are the game's own.
    internal static string WorkName(GameplayType type, TargetLayer layer)
    {
        string name = type == GameplayType.Salvage ? layer.GetSalvageName() : layer.GetName();
        return string.IsNullOrEmpty(name) ? type.ToString() : $"{type} ({name})";
    }

    // Board offers only: missions you hold are listed on the board too, and the missions window is left alone.
    internal static void Mark(Mission mission, RectTransform rect)
    {
        if (rect.GetComponentInParent<MissionBoard>() == null || GamePlayer.current.missions.Contains(mission)) return;
        if (!Needs(mission, out var type, out var layer) || Fits(type, layer)) return;
        var tags = rect.Find("Text/Tags") as RectTransform;
        if (tags == null) return;
        // The icon tile, the name and the chips already on the row fade; the row's background and selection stripe
        // don't, so a picked offer still looks picked.
        Dim(rect.Find("IconTile"));
        foreach (Transform child in tags.parent)
            if (child != tags) Dim(child);
        foreach (Transform child in tags) Dim(child);
        var chip = MissionCardParts.AddChip(tags, "No fit", ColorHelper.reddish);
        chip.name = ChipName;
        var background = chip.GetComponent<Image>();
        background.raycastTarget = true;
        FleetBarParts.SetTooltip(background, "No fit in your fleet",
            $"None of your ships has a turret, drone bay, torpedo bay or hangar bay for {WorkName(type, layer)} work. Your flagship and fleet ships count; mercenaries don't.");
    }

    private static void Dim(Transform t)
    {
        if (t != null) t.gameObject.AddComponent<CanvasGroup>().alpha = DimAlpha;
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => MissionFitCheck.Run(ctx);
}

[HarmonyPatch]
internal static class MissionFitPatches
{
    private static MissionFit F => MissionFit.Instance;

    // Lowest priority, so the chips Mission distance and Mission sort add to the row are there to be dimmed.
    [HarmonyPostfix]
    [HarmonyPriority(Priority.VeryLow)]
    [HarmonyPatch(typeof(MissionListRow), "Init")]
    private static void RowInitPostfix(MissionListRow __instance, RectTransform rect)
    {
        if (!F.Active) return;
        try { MissionFit.Mark(__instance.mission, rect); }
        catch (Exception e) { F.Fail(e, nameof(RowInitPostfix)); }
    }
}
