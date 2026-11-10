using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using Behaviour.Util;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using Source.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IgiQoL.Features.CameraZoom;

// The flight camera's wheel zoom stops at CameraMovement.maxZoom. The game also sizes the dust, the background
// tiles and each location's play area from that static, so the feature leaves it alone and swaps only the limit the
// wheel handler clamps to. Docked, on the galaxy map and in other menus the game's handler returns before that clamp.
public sealed class CameraZoom : Feature
{
    public static CameraZoom Instance;

    public CameraZoom() { Instance = this; }

    public override string Id => "CameraZoom";
    public override string Title => "Zoom Out Further";
    public override string Description => "Lets the mouse wheel zoom the camera out further while you fly in space. Stations, the galaxy map and other menus keep the game's own zoom.";
    public override Type[] PatchClasses => new[] { typeof(CameraZoomPatches) };

    public ConfigEntry<int> Percent;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        // The cap is the background's size: the near smoke layer is built 1.5 times the game's furthest view and
        // doesn't grow with zoom, so beyond 150% its edge shows as a darker rectangle in the middle of the screen.
        Percent = cfg.Bind(Id, "Percent", 100, SettingInfo.Describe("Furthest zoom out (%)",
            "How far the mouse wheel zooms out in space, as a share of the game's own limit. 100 is the game's limit; 150 shows half as far again in each direction. The background isn't drawn any wider than that. Lowering it zooms the camera back in at once.",
            new AcceptableValueRange<int>(100, 150)));
    }

    // The furthest the wheel zooms out, in the flight camera's orthographic size.
    internal static float Limit() =>
        Instance.Active ? CameraMovement.maxZoom * Instance.Percent.Value / 100f : CameraMovement.maxZoom;

    public override void OnActivated()
    {
        Percent.SettingChanged += (_, _) =>
        {
            if (!Active) return;
            try { ClampCamera(); }
            catch (Exception e) { Fail(e, nameof(Percent)); }
        };
    }

    // The game clamps only while the wheel turns, so a lowered limit would otherwise wait for the next wheel turn.
    private static void ClampCamera()
    {
        float limit = Limit();
        if (GameplayerPrefs.GetZoom() > limit) GameplayerPrefs.SetZoom(limit);
        var camera = FlightCamera();
        if (camera != null && camera.orthographicSize > limit) camera.orthographicSize = limit;
    }

    private static Camera FlightCamera()
    {
        if (!Singleton<BackdropManager>.HasInstance) return null;
        var movement = Singleton<BackdropManager>.Instance.cameraMovement;
        return movement != null ? movement.gameCamera : null;
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        yield return SelfCheckContext.EnsureInSpace();
        var movement = Singleton<BackdropManager>.HasInstance ? Singleton<BackdropManager>.Instance.cameraMovement : null;
        var camera = movement != null ? movement.gameCamera : null;
        var ship = Fleet.Flagship;
        if (camera == null || ship == null || Mouse.current == null)
        {
            ctx.Skip("wheel", $"camera {camera != null}, flagship {ship != null}, mouse {Mouse.current != null}");
            yield break;
        }
        int percent = Percent.Value;
        float size = camera.orthographicSize, saved = GameplayerPrefs.GetZoom();
        ctx.Cleanup(() =>
        {
            Percent.Value = percent;
            GameplayerPrefs.SetZoom(saved);
            if (camera != null) camera.orthographicSize = size;
        });

        // The game ignores the wheel while the cursor is over its UI, so put the cursor on open space first.
        bool free = false;
        Vector2 point = default;
        foreach (var f in new[] { new Vector2(0.5f, 0.65f), new Vector2(0.35f, 0.6f), new Vector2(0.65f, 0.6f), new Vector2(0.5f, 0.35f), new Vector2(0.3f, 0.4f), new Vector2(0.7f, 0.4f) })
        {
            point = new Vector2(Screen.width * f.x, Screen.height * f.y);
            Mouse.current.WarpCursorPosition(point);
            yield return null;
            yield return null;
            if (!movement.IsMouseOverUI()) { free = true; break; }
        }
        if (!free)
        {
            ctx.Skip("wheel", "UI covers every test point");
            yield break;
        }

        // CameraMovement reads Unity's old Input.mouseScrollDelta, which neither the Input System's queued events nor
        // an unfocused window feed. For the check's frames the getter reports a wheel turn; the rest is the game's path.
        var wheel = new Harmony("igi.qol.CameraZoom.check");
        wheel.Patch(AccessTools.PropertyGetter(typeof(Input), nameof(Input.mouseScrollDelta)),
            postfix: new HarmonyMethod(typeof(CameraZoom), nameof(FakeWheel)));
        try
        {
            // Zooms out to the highest value the setting allows, so the check also shows that limit is safe to fly at.
            float game = CameraMovement.maxZoom;
            int max = ((AcceptableValueRange<int>)Percent.Description.AcceptableValues).MaxValue;
            float expected = game * max / 100f;
            Percent.Value = max;
            camera.orthographicSize = game - 1f;
            yield return null;
            float reached = 0f;
            yield return WheelOut(camera, r => reached = r);
            var view = camera.WorldToViewportPoint(ship.transform.position);
            bool shipInView = view.x > 0f && view.x < 1f && view.y > 0f && view.y < 1f;
            string detail = $"game limit {game:0.##}, reached {reached:0.##} (expected {expected:0.##} at {max}%), saved zoom {GameplayerPrefs.GetZoom():0.##}, ship at viewport {view.x:0.##},{view.y:0.##}, cursor {point}";
            if (reached <= game + 0.5f)
            {
                ctx.Skip("wheel", "the wheel didn't zoom the camera; " + detail);
                yield break;
            }
            if (Mathf.Abs(reached - expected) < 0.01f && shipInView) ctx.Pass("wheel", detail);
            else ctx.Fail("wheel", detail);
            yield return ctx.Screenshot("zoom-out-max");

            // Lowering the setting pulls the camera in at once, and the wheel then stops at the game's limit again.
            Percent.Value = 100;
            float pulled = camera.orthographicSize;
            yield return WheelOut(camera, r => reached = r);
            string limitDetail = $"after setting 100: {pulled:0.##}, after wheel {reached:0.##}, game limit {game:0.##}";
            if (Mathf.Approximately(pulled, game) && Mathf.Approximately(reached, game)) ctx.Pass("game-limit", limitDetail);
            else ctx.Fail("game-limit", limitDetail);
        }
        finally
        {
            wheelNotches = 0f;
            wheel.UnpatchSelf();
        }
    }

    private static float wheelNotches;

    private static void FakeWheel(ref Vector2 __result)
    {
        if (wheelNotches != 0f) __result = new Vector2(0f, wheelNotches);
    }

    // Turns the wheel towards you (zoom out) until the camera stops growing or 10 s pass, and reports where it stopped.
    private static IEnumerator WheelOut(Camera camera, Action<float> done)
    {
        float last = camera.orthographicSize, end = Time.realtimeSinceStartup + 10f;
        int still = 0;
        wheelNotches = -5f;
        while (still < 10 && Time.realtimeSinceStartup < end)
        {
            yield return null;
            float now = camera.orthographicSize;
            still = Mathf.Approximately(now, last) ? still + 1 : 0;
            last = now;
        }
        wheelNotches = 0f;
        done(last);
    }
}

[HarmonyPatch]
internal static class CameraZoomPatches
{
    // HandleZoom reads maxZoom once, in its clamp; that read becomes the feature's limit.
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CameraMovement), nameof(CameraMovement.HandleZoom))]
    private static IEnumerable<CodeInstruction> HandleZoomTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var maxZoom = AccessTools.PropertyGetter(typeof(CameraMovement), nameof(CameraMovement.maxZoom));
        var limit = new Func<float>(CameraZoom.Limit).Method;
        var list = new List<CodeInstruction>(instructions);
        int swapped = 0;
        foreach (var ins in list)
            if (ins.Calls(maxZoom)) { ins.operand = limit; swapped++; }
        if (swapped != 1) throw new InvalidOperationException($"expected one maxZoom read in CameraMovement.HandleZoom, found {swapped}");
        return list;
    }
}
