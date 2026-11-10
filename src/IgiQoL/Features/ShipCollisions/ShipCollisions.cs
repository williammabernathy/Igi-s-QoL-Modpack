using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Managers;
using Behaviour.Unit;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using Source.Data.Persistable;
using Source.SpaceShip.Auto;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.ShipCollisions;

// Switches for what bumps into what. Each one copies something the game already does elsewhere: a carrier's fighters
// pass through their carrier, docking ships pass through everyone (ShipContactGate), an outpost is pinned in place
// during an industry mission. Options in one box replace each other; the boxes combine freely.
public sealed class ShipCollisions : Feature
{
    public static ShipCollisions Instance;

    public ShipCollisions() { Instance = this; }

    public override string Id => "ShipCollisions";
    public override string Title => "Ship Collisions";
    public override string Description => "Changes what bumps into what. Everything here is off until you turn it on. Options in the same box replace each other: turning one on turns the others in that box off. Options in different boxes work together.";
    public override Type[] PatchClasses => new[] { typeof(CollisionPatches) };

    public ConfigEntry<bool> FleetPassThrough, PassThroughWhileTurning, KeepOwnHeading, AnchorStations, HeavyStations;
    public ConfigEntry<float> StationWeight;

    private ConfigEntry<bool>[] fleetGroup, stationGroup;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        FleetPassThrough = cfg.Bind(Id, "FleetPassThrough", false, SettingInfo.Describe("Fleet passes through your ships",
            "Your fleet ships and the ship you fly never bump into each other. Enemies and other ships still collide with them as usual. The game already does this for a carrier and its fighters."));
        PassThroughWhileTurning = cfg.Bind(Id, "PassThroughWhileTurning", false, SettingInfo.Describe("Fleet passes through while turning in formation",
            "When you turn, a fleet ship flying in formation turns on the spot to match you, and a long hull swings into whatever is beside it. With this on, a fleet ship in formation stops colliding with any ship while it turns, and becomes solid again once it has stopped turning and no longer overlaps another hull."));
        KeepOwnHeading = cfg.Bind(Id, "KeepOwnHeading", false, SettingInfo.Describe("Formation ships keep their own heading",
            "Fleet ships in formation slide into their place beside you without turning to match your heading, so they don't swing their hulls around when you turn. A ship still turns when its place is far away and it has to fly there."));
        AnchorStations = cfg.Bind(Id, "AnchorStations", false, SettingInfo.Describe("Stations can't be pushed",
            "Station parts stay where they are: ships bounce off them like a wall, and nothing can shove a station out of place. The game does the same to an outpost during an industry mission."));
        HeavyStations = cfg.Bind(Id, "HeavyStations", false, SettingInfo.Describe("Heavier stations",
            "Station parts weigh more and stop drifting soon after a bump, so a ship can still nudge a station a little. \"Station weight\" sets how much heavier."));
        StationWeight = cfg.Bind(Id, "StationWeight", 10f, SettingInfo.Describe("Station weight (x)",
            "With \"Heavier stations\" on, each station part weighs this many times what the game gives it.",
            new AcceptableValueRange<float>(2f, 100f)));

        fleetGroup = new[] { FleetPassThrough, PassThroughWhileTurning };
        stationGroup = new[] { AnchorStations, HeavyStations };
        Exclusive(fleetGroup);
        Exclusive(stationGroup);
    }

    // At most one entry of a group is on. A cfg file edited by hand to have two keeps the first; after that,
    // turning one on (from the window or anywhere else) turns the rest off.
    private static void Exclusive(ConfigEntry<bool>[] group)
    {
        bool seen = false;
        foreach (var e in group)
        {
            if (e.Value && seen) e.Value = false;
            seen |= e.Value;
        }
        foreach (var e in group)
        {
            var self = e;
            self.SettingChanged += (_, _) =>
            {
                if (!self.Value) return;
                foreach (var other in group)
                    if (other != self) other.Value = false;
            };
        }
    }

    public override void OnActivated()
    {
        GameEvents.SaveLoaded += CollisionPatches.Reset;
        GameEvents.MainMenuShown += CollisionPatches.Reset;
        AnchorStations.SettingChanged += OnStationSettingChanged;
        HeavyStations.SettingChanged += OnStationSettingChanged;
        StationWeight.SettingChanged += OnStationSettingChanged;
    }

    private void OnStationSettingChanged(object sender, EventArgs e)
    {
        if (!Active) return;
        try { CollisionPatches.ApplyToStationsHere(); }
        catch (Exception ex) { Fail(ex, nameof(OnStationSettingChanged)); }
    }

    public override void AddSettingRows(RectTransform column)
    {
        SettingRows.AddEntries(column, Id, SettingRows.Rows.EnabledOnly);
        AddGroup(column, "Your fleet and your ship: one at most", fleetGroup);
        var formation = Kit.Box(column, "Formation: works with any other option");
        AddEntryRow(formation, KeepOwnHeading);
        var stations = AddGroup(column, "Stations: one at most", stationGroup);
        AddEntryRow(stations, StationWeight);
    }

    // A box of toggles that replace each other. The exclusive handlers change the entries; the box then redraws
    // every toggle from its entry so the ones just turned off show it.
    private static RectTransform AddGroup(RectTransform column, string title, ConfigEntry<bool>[] group)
    {
        var box = Kit.Box(column, title);
        var tiles = new Button[group.Length];
        for (int i = 0; i < group.Length; i++)
        {
            var entry = group[i];
            var info = SettingInfo.Of(entry);
            var row = Kit.Row(box);
            var label = Kit.Label(row, info.Label);
            Kit.Tooltip(label, info.Label, entry.Description.Description);
            tiles[i] = Kit.Toggle(row, null, entry.Value, v =>
            {
                entry.Value = v;
                for (int j = 0; j < group.Length; j++) Kit.SetToggle(tiles[j], group[j].Value);
            });
        }
        return box;
    }

    private static void AddEntryRow(RectTransform box, ConfigEntryBase entry)
    {
        var info = SettingInfo.Of(entry);
        SettingRows.AddEntry(box, entry, info.Label, entry.Description.Description, info);
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => CollisionCheck.Run(ctx);
}

[HarmonyPatch]
internal static class CollisionPatches
{
    private static ShipCollisions F => ShipCollisions.Instance;

    // A reason of our own for ShipContactGate, next to the game's Docking (1) and Settling (2). The gate drops a
    // ship's contacts with the Ship layer while any reason is set, and when the last one clears it waits until the
    // hull no longer overlaps another before making it solid again.
    internal const ShipContactPause Turning = (ShipContactPause)4;

    // A fleet ship counts as turning while it spins faster than this or is this far off the heading it is told to hold.
    private const float TurningDegPerSec = 15f, TurningOffDeg = 5f;

    // How often the fleet pass-through pairs are brought in line with the setting and the fleet.
    private const float ReconcileEvery = 0.5f;

    // A shoved station with the game's damping (0.1) drifts for a long time; at 1 it stops within a few seconds.
    private const float HeavyLinearDamping = 1f, GameLinearDamping = 0.1f;

    private static readonly List<SpaceShip> playerShips = new();
    // Pairs this feature made pass through each other, so turning the option off restores only those.
    private static readonly List<(SpaceShip a, SpaceShip b)> ghosted = new();
    private static float nextReconcile;

    internal static void Reset()
    {
        ghosted.Clear();
        playerShips.Clear();
        nextReconcile = 0f;
    }

    internal static bool IsTurningInFormation(SpaceShip s) =>
        s.inFormationPosition && s.rigidbody != null
        && (Mathf.Abs(s.rigidbody.angularVelocity) > TurningDegPerSec
            || (s.forceWorldAngle.HasValue && Mathf.Abs(s.GetCurrentAngleToTarget()) > TurningOffDeg));

    // Runs for every ship each physics step, after the game sets its own Docking pause the same way.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ShipNavigator), "FixedUpdate")]
    private static void NavigatorFixedUpdatePostfix(ShipNavigator __instance)
    {
        if (!F.Active) return;
        try
        {
            var unit = __instance.unit;
            if (unit == null) return;
            bool turning = F.PassThroughWhileTurning.Value && unit is SpaceShip s && s.isPlayerFleetShip && IsTurningInFormation(s);
            unit.contactGate.SetPaused(Turning, turning);
            if (Time.time >= nextReconcile)
            {
                nextReconcile = Time.time + ReconcileEvery;
                ReconcileFleetPassThrough();
            }
        }
        catch (Exception e) { F.Fail(e, nameof(NavigatorFixedUpdatePostfix)); }
    }

    // The game never makes the ship you fly and your fleet ships ignore each other, so any pair found ignoring each
    // other that this feature didn't set was set by the game and is left alone.
    internal static void ReconcileFleetPassThrough()
    {
        bool on = F.FleetPassThrough.Value;
        playerShips.Clear();
        if (on) CollectPlayerShips();
        for (int i = ghosted.Count - 1; i >= 0; i--)
        {
            var (a, b) = ghosted[i];
            if (a != null && b != null && on && playerShips.Contains(a) && playerShips.Contains(b)) continue;
            if (HasColliders(a) && HasColliders(b)) a.EnableCollisionWith(b);
            ghosted.RemoveAt(i);
        }
        if (!on) return;
        for (int i = 0; i < playerShips.Count; i++)
        for (int j = i + 1; j < playerShips.Count; j++)
        {
            var a = playerShips[i];
            var b = playerShips[j];
            if (!HasColliders(a) || !HasColliders(b)) continue;
            // Checked every time, not only once per pair: a ship that loses its core gets a new core collider.
            if (Physics2D.GetIgnoreCollision(a.structureCollider, b.structureCollider)) continue;
            a.DisableCollisionWith(b);
            if (!ghosted.Contains((a, b))) ghosted.Add((a, b));
        }
    }

    private static void CollectPlayerShips()
    {
        var gm = GameplayManager.Instance;
        if (gm == null) return;
        if (gm.spaceShip != null) playerShips.Add(gm.spaceShip);
        foreach (var s in gm.fleetSpaceShips)
            if (s != null && s != gm.spaceShip && !s.isCarrierFighter) playerShips.Add(s);
    }

    private static bool HasColliders(SpaceShip s) => s != null && s.structureCollider != null && s.surfaceCollider != null;

    // The game points a formation ship at your heading here every update; clearing it afterwards leaves the ship on
    // its current heading. ApplyThrust then only turns it when its place is more than two hull radii away.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(WingmanActions), "FlyToAnchor")]
    private static void FlyToAnchorPostfix(WingmanActions __instance)
    {
        if (!F.Active || !F.KeepOwnHeading.Value) return;
        try
        {
            var s = __instance.spaceShip;
            if (s != null && s.isPlayerFleetShip) s.forceWorldAngle = null;
        }
        catch (Exception e) { F.Fail(e, nameof(FlyToAnchorPostfix)); }
    }

    // Station parts are spawned here with dynamic bodies at the game's weight. Without either station option the
    // parts are left as the game made them.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatStationData), nameof(CombatStationData.AddToWorld))]
    private static void AddToWorldPostfix(CombatStationData __instance)
    {
        if (!F.Active || !(F.AnchorStations.Value || F.HeavyStations.Value)) return;
        try
        {
            foreach (var part in __instance.spawnedParts) ApplyToStationPart(part);
        }
        catch (Exception e) { F.Fail(e, nameof(AddToWorldPostfix)); }
    }

    // After a setting change: every station part at this location, including putting back the game's values.
    internal static void ApplyToStationsHere()
    {
        if (BasePoiManager.current == null) return;
        foreach (var part in BasePoiManager.current.GetComponentsInChildren<CombatStationPart>())
            ApplyToStationPart(part);
    }

    internal static void ApplyToStationPart(CombatStationPart part)
    {
        if (part == null) return;
        var rb = part.rigidbody;
        if (rb == null) return;
        if (F.AnchorStations.Value)
        {
            // A kinematic body keeps whatever velocity it had and ignores damping, so a drifting part would drift forever.
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            return;
        }
        // During an industry mission the game sets the outpost kinematic again within 0.2 s (IndustrialOutpost).
        rb.bodyType = RigidbodyType2D.Dynamic;
        bool heavy = F.HeavyStations.Value;
        float mass = part.mass * (heavy ? F.StationWeight.Value : 1f);
        rb.mass = mass;
        rb.inertia = part.ComputeMomentOfInertia(mass);
        rb.linearDamping = heavy ? HeavyLinearDamping : GameLinearDamping;
    }
}
