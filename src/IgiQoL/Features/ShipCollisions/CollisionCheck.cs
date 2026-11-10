using System.Collections.Generic;
using System.Collections;
using Behaviour.Managers;
using Behaviour.Unit;
using BepInEx.Configuration;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.UI;
using Source.SpaceShip;
using Source.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.ShipCollisions;

internal static class CollisionCheck
{
    public static IEnumerator Run(SelfCheckContext ctx)
    {
        var f = ShipCollisions.Instance;
        var toggles = new[] { f.FleetPassThrough, f.PassThroughWhileTurning, f.KeepOwnHeading, f.AnchorStations, f.HeavyStations };
        var saved = new bool[toggles.Length];
        for (int i = 0; i < toggles.Length; i++) saved[i] = toggles[i].Value;
        float savedWeight = f.StationWeight.Value;
        ctx.Cleanup(() =>
        {
            AllOff(toggles);
            for (int i = 0; i < toggles.Length; i++) if (saved[i]) toggles[i].Value = true;
            f.StationWeight.Value = savedWeight;
        });

        // Turning on one option of a box turns the other off; the formation option stays on beside either.
        AllOff(toggles);
        f.FleetPassThrough.Value = true;
        f.KeepOwnHeading.Value = true;
        f.PassThroughWhileTurning.Value = true;
        f.AnchorStations.Value = true;
        f.HeavyStations.Value = true;
        string ex = $"fleetPass={f.FleetPassThrough.Value} turning={f.PassThroughWhileTurning.Value} heading={f.KeepOwnHeading.Value} anchor={f.AnchorStations.Value} heavy={f.HeavyStations.Value}";
        if (!f.FleetPassThrough.Value && f.PassThroughWhileTurning.Value && f.KeepOwnHeading.Value && !f.AnchorStations.Value && f.HeavyStations.Value)
            ctx.Pass("exclusive", ex);
        else ctx.Fail("exclusive", ex);
        AllOff(toggles);

        yield return WindowRows(ctx, f);
        AllOff(toggles);

        yield return SelfCheckContext.EnsureInSpace();
        var flagship = Fleet.Flagship;
        SpaceShip wing = null;
        if (GameplayManager.Instance != null)
            foreach (var s in GameplayManager.Instance.fleetSpaceShips)
                if (s != null && s != flagship && s.structureCollider != null) { wing = s; break; }
        if (flagship == null || flagship.structureCollider == null || wing == null)
        {
            foreach (var n in new[] { "pass-through", "turning", "heading" }) ctx.Skip(n, "no fleet ship in space");
        }
        else
        {
            yield return PassThrough(ctx, f, flagship, wing);
            // Turning and heading need the fleet ship flying in formation, which it doesn't while it fights (the full
            // run's earlier checks leave enemies about). Hold fire and a Guard order on the flagship are the game's own
            // way to keep a fleet ship in formation (WingmanActions.RunCurrentOrder flies it to its anchor), and the HUD's
            // hold position keeps the flagship from chasing its own target meanwhile. Stance, orders and hold come back
            // afterwards.
            var wingman = wing.autoActions as Source.SpaceShip.Auto.WingmanActions;
            var stance0 = wing.spaceShipData.fleetStance;
            var orders0 = wingman != null ? new List<Source.SpaceShip.Auto.FleetOrder>(wingman.queuedOrders) : null;
            bool hold0 = GamePlayer.current.holdPosition;
            try
            {
                if (wingman != null)
                {
                    wing.spaceShipData.fleetStance = FleetStance.HoldFire;
                    Behaviour.UI.Movement.FleetCommands.IssueRegroup(new List<SpaceShip> { wing });
                }
                GamePlayer.current.holdPosition = true;
                yield return Turning(ctx, f, flagship, wing);
                yield return Heading(ctx, f, flagship, wing);
            }
            finally
            {
                GamePlayer.current.holdPosition = hold0;
                if (wingman != null && wing != null)
                {
                    wing.spaceShipData.fleetStance = stance0;
                    wingman.ClearOrders();
                    foreach (var o in orders0) wingman.GiveOrder(o, queue: true);
                }
            }
        }
        AllOff(toggles);
        yield return Stations(ctx, f);
    }

    private static void AllOff(ConfigEntry<bool>[] toggles)
    {
        foreach (var t in toggles) t.Value = false;
    }

    // Clicks the toggles on the Settings tab the way a player does and reads back what the tiles show.
    private static IEnumerator WindowRows(SelfCheckContext ctx, ShipCollisions f)
    {
        ModWindow.Open("settings");
        yield return null;
        var a = FindTile(f.FleetPassThrough);
        var b = FindTile(f.PassThroughWhileTurning);
        if (a == null || b == null)
        {
            ctx.Fail("window-toggles", $"tiles not found: fleetPass={a != null} turning={b != null}");
            ModWindow.Close();
            yield break;
        }
        a.onClick.Invoke();
        bool aFirst = On(a);
        b.onClick.Invoke();
        string detail = $"after first click a={aFirst}; after second a={On(a)} b={On(b)} entries a={f.FleetPassThrough.Value} b={f.PassThroughWhileTurning.Value}";
        if (aFirst && !On(a) && On(b) && !f.FleetPassThrough.Value && f.PassThroughWhileTurning.Value) ctx.Pass("window-toggles", detail);
        else ctx.Fail("window-toggles", detail);
        // Scroll the feature's heading into view for the screenshot: the boxes sit far down the Settings tab.
        Canvas.ForceUpdateCanvases();
        var content = ModWindow.Content;
        Transform box = a.transform;
        while (box.parent != content) box = box.parent;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, -((RectTransform)box).anchoredPosition.y - 150f);
        yield return ctx.Screenshot("ship-collisions-settings");
        ModWindow.Close();
    }

    private static bool On(Button tile) => tile.GetComponent<Kit.ToggleState>().on;

    private static Button FindTile(ConfigEntryBase entry)
    {
        string label = SettingInfo.Of(entry).Label;
        var content = ModWindow.Content;
        for (int i = 0; i < content.childCount; i++)
        {
            var box = content.GetChild(i);
            if (box.name != Kit.BoxName) continue;
            for (int j = 0; j < box.childCount; j++)
            {
                var row = box.GetChild(j);
                var text = row.GetComponentInChildren<TMP_Text>();
                var state = row.GetComponentInChildren<Kit.ToggleState>();
                if (text != null && state != null && text.text == label) return state.GetComponent<Button>();
            }
        }
        return null;
    }

    private static IEnumerator PassThrough(SelfCheckContext ctx, ShipCollisions f, SpaceShip flagship, SpaceShip wing)
    {
        bool before = Physics2D.GetIgnoreCollision(flagship.structureCollider, wing.structureCollider);
        f.FleetPassThrough.Value = true;
        yield return new WaitForSeconds(1f);
        bool on = Physics2D.GetIgnoreCollision(flagship.structureCollider, wing.structureCollider);
        f.FleetPassThrough.Value = false;
        yield return new WaitForSeconds(1f);
        bool off = Physics2D.GetIgnoreCollision(flagship.structureCollider, wing.structureCollider);
        string detail = $"{wing.name}: ignoring the flagship before={before} with the option on={on} after turning it off={off}";
        if (!before && on && !off) ctx.Pass("pass-through", detail);
        else ctx.Fail("pass-through", detail);
    }

    private static IEnumerator WaitForFormation(SpaceShip wing, float seconds)
    {
        float until = Time.time + seconds;
        while (wing != null && !wing.inFormationPosition && Time.time < until) yield return null;
    }

    // Earlier checks fight and move ships about, so the fleet ship can start far from its slot: at its 3 to 6 u/s cruise
    // it takes a minute to fly back, and until it's within one hull of the slot its navigator steers around your other
    // ships (ShipNavigator.IsObstacle), so near a crowded flagship it can circle the slot for longer than the wait.
    // These checks are about turning once in formation, not the flight there, so a ship outside its slot is put on it
    // (still, facing your heading) and left to settle in.
    private static IEnumerator PlaceOnSlot(SpaceShip flagship, SpaceShip wing, System.Action<string> note)
    {
        for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
        if (wing == null || !wing.overrideTarget.HasValue) yield break;
        Vector2 slot = wing.overrideTarget.Value;
        float off = Vector2.Distance(slot, wing.rigidbody.position);
        if (off < wing.radius) yield break;
        wing.transform.position = slot;
        wing.rigidbody.position = slot;
        wing.rigidbody.linearVelocity = Vector2.zero;
        wing.rigidbody.angularVelocity = 0f;
        wing.rigidbody.rotation = flagship.rigidbody.rotation;
        wing.transform.rotation = flagship.transform.rotation;
        Physics2D.SyncTransforms();
        note($" (placed on its slot from {off:0} u away)");
    }

    // Why a fleet ship isn't in formation: its order and stance, how far it is from where it's steering, and whether
    // the flagship it guards is moving.
    private static string FormationNote(SpaceShip flagship, SpaceShip wing)
    {
        if (wing == null) return "the fleet ship is gone";
        var wingman = wing.autoActions as Source.SpaceShip.Auto.WingmanActions;
        string order = wingman == null ? "no fleet AI" : wingman.currentOrder == null ? "no order" : wingman.currentOrder.type.ToString();
        string off = "no anchor";
        if (wing.overrideTarget.HasValue)
        {
            var anchor = wing.overrideTarget.Value;
            var nav = wing.navigationCommand;
            off = $"{Vector2.Distance(anchor, wing.transform.position):0.0} u from its anchor (radius {wing.radius:0.0}, height {wing.height:0.0}), destination {Vector2.Distance(anchor, wing.currentDestination):0.0} u off it, "
                + $"navigation {wing.ResolveNavigationMode()} waypoint {(nav.hasWaypoint ? $"{Vector2.Distance(anchor, nav.waypoint):0.0} u off it" : "none")}, speed {wing.rigidbody.linearVelocity.magnitude:0.0}, "
                + $"flagship {(flagship != null ? Vector2.Distance(flagship.transform.position, wing.transform.position) : -1f):0.0} u away, anchor {(flagship != null ? Vector2.Distance(flagship.transform.position, anchor) : -1f):0.0} u from it";
        }
        return $"order {order}, stance {wing.spaceShipData.fleetStance}, {off}, target {(wing.targetProvider?.priorityTarget != null)}, travelling {wing.unitData.travelling}, flagship speed {(flagship != null && flagship.rigidbody != null ? flagship.rigidbody.linearVelocity.magnitude : 0f):0.0}";
    }

    // Turns the flagship at 30° a second, the way a player turns, so the fleet ship in formation has to turn with it:
    // it must lose its contacts while it turns and get them back once it has stopped. (A sudden 90° snap swings its
    // place so far that it leaves formation and flies there instead, which this option doesn't cover.)
    private static IEnumerator Turning(SelfCheckContext ctx, ShipCollisions f, SpaceShip flagship, SpaceShip wing)
    {
        string placed = "";
        yield return PlaceOnSlot(flagship, wing, s => placed = s);
        float waited = Time.time;
        yield return WaitForFormation(wing, 45f);
        waited = Time.time - waited;
        if (wing == null || !wing.inFormationPosition) { ctx.Skip("turning", $"the fleet ship didn't reach formation in {waited:0} s{placed}: {FormationNote(flagship, wing)}"); yield break; }
        f.PassThroughWhileTurning.Value = true;
        float start = Time.time, maxOff = 0f;
        int steps = 0, inFormation = 0;
        bool paused = false;
        while (Time.time < start + 1.5f)
        {
            yield return new WaitForFixedUpdate();
            float r = flagship.rigidbody.rotation + 30f * Time.fixedDeltaTime;
            flagship.rigidbody.rotation = r;
            flagship.transform.rotation = Quaternion.Euler(0f, 0f, r);
            steps++;
            if (wing.inFormationPosition) inFormation++;
            if (wing.forceWorldAngle.HasValue) maxOff = Mathf.Max(maxOff, Mathf.Abs(wing.GetCurrentAngleToTarget()));
            paused |= (wing.contactGate.activePauses & CollisionPatches.Turning) != 0;
        }
        float until = Time.time + 20f;
        while (Time.time < until && (wing.contactGate.activePauses & CollisionPatches.Turning) != 0) yield return null;
        float clearedAt = Time.time - start;
        bool cleared = (wing.contactGate.activePauses & CollisionPatches.Turning) == 0;
        string detail = $"{wing.name}{placed}: in formation {inFormation}/{steps} steps of the turn, up to {maxOff:0.0}° off your heading, lost contacts while turning={paused}, back after {clearedAt:0.0} s={cleared}, pauses now {wing.contactGate.activePauses}";
        if (paused && cleared) ctx.Pass("turning", detail);
        else ctx.Fail("turning", detail);
        f.PassThroughWhileTurning.Value = false;
    }

    private static IEnumerator Heading(SelfCheckContext ctx, ShipCollisions f, SpaceShip flagship, SpaceShip wing)
    {
        string placed = "";
        yield return PlaceOnSlot(flagship, wing, s => placed = s);
        float waited = Time.time;
        yield return WaitForFormation(wing, 30f);
        waited = Time.time - waited;
        if (wing == null || !wing.inFormationPosition) { ctx.Skip("heading", $"the fleet ship didn't reach formation in {waited:0} s{placed}: {FormationNote(flagship, wing)}"); yield break; }
        bool gameHolds = wing.forceWorldAngle.HasValue;
        f.KeepOwnHeading.Value = true;
        yield return new WaitForSeconds(0.5f);
        bool on = wing.forceWorldAngle.HasValue, formationOn = wing.inFormationPosition;
        f.KeepOwnHeading.Value = false;
        yield return new WaitForSeconds(0.5f);
        bool off = wing.forceWorldAngle.HasValue, formationOff = wing.inFormationPosition;
        string detail = $"{wing.name}: told to hold your heading: game={gameHolds} option on={on} (in formation {formationOn}) option off={off} (in formation {formationOff})";
        if (!formationOn || !formationOff) ctx.Skip("heading", "left formation during the check: " + detail);
        else if (gameHolds && !on && off) ctx.Pass("heading", detail);
        else ctx.Fail("heading", detail);
    }

    private static IEnumerator Stations(SelfCheckContext ctx, ShipCollisions f)
    {
        var parts = BasePoiManager.current != null ? BasePoiManager.current.GetComponentsInChildren<CombatStationPart>() : new CombatStationPart[0];
        if (parts.Length == 0) { ctx.Skip("stations", "no station parts at this location"); yield break; }
        var p = parts[0];
        var rb = p.rigidbody;
        f.AnchorStations.Value = true;
        yield return null;
        string anchored = $"{rb.bodyType}";
        bool anchorOk = rb.bodyType == RigidbodyType2D.Kinematic;
        f.StationWeight.Value = 10f;
        f.HeavyStations.Value = true;
        yield return null;
        string heavy = $"{rb.bodyType} mass {rb.mass:0} (game {p.mass:0}) damping {rb.linearDamping:0.0} anchor={f.AnchorStations.Value}";
        bool heavyOk = rb.bodyType == RigidbodyType2D.Dynamic && Mathf.Approximately(rb.mass, p.mass * 10f) && !f.AnchorStations.Value;
        f.HeavyStations.Value = false;
        yield return null;
        string game = $"{rb.bodyType} mass {rb.mass:0} damping {rb.linearDamping:0.0}";
        bool gameOk = rb.bodyType == RigidbodyType2D.Dynamic && Mathf.Approximately(rb.mass, p.mass);
        string detail = $"{parts.Length} parts, {p.name}: anchored {anchored}; heavier {heavy}; both off {game}";
        if (anchorOk && heavyOk && gameOk) ctx.Pass("stations", detail);
        else ctx.Fail("stations", detail);
    }
}
