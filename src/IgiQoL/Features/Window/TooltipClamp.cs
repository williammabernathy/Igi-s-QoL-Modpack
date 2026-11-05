using Behaviour.UI;
using HarmonyLib;
using UnityEngine;

namespace IgiQoL.Features.Window;

// The game puts a hover card's corner at the mouse and only flips it above or below the mouse by checking the side
// it starts on, never that the flipped card fits. A card taller than both the space above and the space below the
// mouse runs off the screen and its last sections can't be read. This slides such a card along the screen edge, so it
// stays beside the mouse and never covers it.
[HarmonyPatch]
internal static class TooltipClampPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UITooltip), "Update")]
    private static void UpdatePostfix(UITooltip __instance)
    {
        var window = WindowFeature.Instance;
        if (window == null || !window.Active || !window.KeepCardsOnScreen.Value) return;
        try { Clamp(__instance); }
        catch (System.Exception e) { window.Fail(e, nameof(UpdatePostfix)); }
    }

    // The compare card of an item draws its neighbours next to itself and keeps them on screen on its own.
    internal static void Clamp(UITooltip tooltip)
    {
        if (tooltip is CompareTooltip) return;
        var canvas = UITooltip._rootCanvas;
        if (canvas == null) return;
        float scale = canvas.scaleFactor;
        if (scale <= 0f) return;
        float screenW = Screen.width / scale, screenH = Screen.height / scale;
        var rect = (RectTransform)tooltip.transform;
        Vector2 position = rect.anchoredPosition, pivot = rect.pivot;

        float bottom = position.y - pivot.y * tooltip.SizeY, top = bottom + tooltip.SizeY;
        float shiftY = 0f;
        if (bottom < 0f) shiftY = -bottom;
        // A card taller than the screen keeps its top, where the title is.
        if (top + shiftY > screenH) shiftY = screenH - top;

        float left = position.x - pivot.x * tooltip.SizeX, right = left + tooltip.SizeX;
        float shiftX = 0f;
        if (left < 0f) shiftX = -left;
        else if (right > screenW) shiftX = screenW - right;

        if (shiftX == 0f && shiftY == 0f) return;
        rect.anchoredPosition = new Vector2(position.x + shiftX, position.y + shiftY);
    }
}
