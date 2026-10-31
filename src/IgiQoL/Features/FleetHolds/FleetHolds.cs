using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Equipment.Module;
using Behaviour.Item;
using Behaviour.Tractoring;
using Behaviour.UI;
using Behaviour.Unit;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using Source.Item;
using Source.Player;
using Source.SpaceShip;
using UnityEngine;

namespace IgiQoL.Features.FleetHolds;

// A ship whose hold can't take the loot its tractor is reaching for passes cargo to another ship of the fleet here.
// Without it, one full fleet ship sets ECHO's "inventory full" (TractorModule.Update, TractorBeam partial pickup) and
// the whole fleet flies home while the flagship still has room.
public sealed class FleetHolds : Feature
{
    public static FleetHolds Instance;

    public FleetHolds() { Instance = this; }

    public override string Id => "FleetHolds";
    public override string Title => "Shared fleet holds";
    public override string Description => "When one of your ships can't fit the loot its tractor beam is reaching for, it hands cargo to another ship of your fleet at the same location that has room (the flagship first), so it keeps collecting. Without this, a single full ship makes ECHO fly the whole fleet home even when the others have space.";
    public override Type[] PatchClasses => new[] { typeof(FleetHoldsPatches) };

    public ConfigEntry<bool> Overflow;

    // Extra room made beyond the stack being picked up, as a share of the ship's hold, so it doesn't hand over cargo
    // again for every following pickup.
    public const float Headroom = 0.25f;

    private static readonly Dictionary<SpaceShipData, float> nextTry = new();

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        Overflow = cfg.Bind(Id, "Overflow", false, SettingInfo.Describe("Pass cargo to ships with room",
            "When a ship's hold can't take the loot its tractor beam is about to pick up, it moves cargo to other ships of your fleet at the same location, the flagship first, freeing the stack's volume plus a quarter of its hold. Favourites, ammo the ship uses, usable items and items your missions need stay where they are. ECHO then only heads home once every ship is full."));
    }

    // Vanilla IdleManager.ShouldKeepInCargo for any ship, plus warp fuel and mission items.
    public static bool Keep(SpaceShipData ship, SpaceShip unit, Inventory.InventoryItem stack)
    {
        if (stack.favourite) return true;
        var item = stack.item;
        if (item.itemCategory == ItemCategory.Ammo) return unit != null && unit.DoWeUseAmmo(item);
        if (item.itemCategory == ItemCategory.Usable) return true;
        if (item.TryGetState<Behaviour.Item.Usable.WarpFuelState>(out _)) return true;
        return GamePlayer.current.RequiredItemCountForMissions(item) > 0;
    }

    // Your other ships at this location whose hold takes cargo: the flagship first, then the most free space.
    public static List<SpaceShip> Receivers(SpaceShip from)
    {
        var list = new List<SpaceShip>();
        var player = GamePlayer.current;
        var flagship = GameplayManager.Instance != null ? GameplayManager.Instance.spaceShip : null;
        if (flagship != null && flagship != from) list.Add(flagship);
        var rest = new List<SpaceShip>();
        foreach (var data in player.activeFleet)
        {
            if (data == null || !(data.unit is SpaceShip u) || u == from || u == flagship || u.isDestroyed || !u.gameObject.activeInHierarchy) continue;
            if (data.cargo == null || !PlayerCargo.CanManage(data)) continue;
            rest.Add(u);
        }
        rest.Sort((a, b) => b.spaceShipData.GetCargoAvailable().CompareTo(a.spaceShipData.GetCargoAvailable()));
        list.AddRange(rest);
        return list;
    }

    // Moves cargo off `from` until `need` m3 are free (plus headroom), biggest stacks first. Returns the m3 moved.
    public static float MakeRoom(SpaceShip from, float need, List<string> moved = null)
    {
        var data = from.spaceShipData;
        if (data?.cargo == null || !PlayerCargo.CanManage(data)) return 0f;
        float want = Mathf.Min(need + data.cargoCapacity * Headroom, data.cargoCapacity) - data.GetCargoAvailable();
        if (want <= 0f) return 0f;
        var stacks = new List<Inventory.InventoryItem>();
        foreach (var s in data.cargo.items)
            if (s != null && s.item.m3 > 0f && !Keep(data, from, s)) stacks.Add(s);
        stacks.Sort((a, b) => b.spaceRequired.CompareTo(a.spaceRequired));
        float freed = 0f;
        var receivers = Receivers(from);
        foreach (var stack in stacks)
        {
            if (freed >= want) break;
            foreach (var to in receivers)
            {
                if (freed >= want || stack.count <= 0) break;
                int n = Mathf.Min(stack.count, Mathf.CeilToInt((want - freed) / stack.item.m3));
                var item = stack.item;
                int done = PlayerCargo.MoveStack(data.cargo, stack, n, to.spaceShipData.cargo);
                if (done <= 0) continue;
                freed += done * item.m3;
                moved?.Add($"{done} {Source.Util.Translation.Translate(item.displayName)} to {to.spaceShipData.GetShipName()}");
            }
        }
        return freed;
    }

    internal static bool Due(SpaceShipData ship)
    {
        if (nextTry.TryGetValue(ship, out float t) && Time.time < t) return false;
        nextTry[ship] = Time.time + 2f;
        return true;
    }

    internal static void Forget(SpaceShipData ship) => nextTry.Remove(ship);

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => FleetHoldsCheck.Run(ctx);
}

[HarmonyPatch]
internal static class FleetHoldsPatches
{
    // Runs for every loot item in a tractor's list every frame, so it returns at once while the stack fits whole.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TractorModule), "CanFitInCargo")]
    private static void FitPostfix(TractorModule __instance, TractorableItem item, ref bool __result)
    {
        var f = FleetHolds.Instance;
        if (!f.Active || !f.Overflow.Value) return;
        try
        {
            if (!(__instance.parent is SpaceShip ship) || !ship.IsPlayer() || ship.isCarrierFighter || ship.isAutomatedWorkCameo) return;
            var data = ship.spaceShipData;
            var type = item.data?.itemType;
            if (data == null || type == null || type.m3 <= 0f || type.CanGoInDataInventory()) return;
            float stack = type.m3 * Mathf.Max(1, item.data.itemAmount);
            if (data.GetCargoAvailable() >= stack || !FleetHolds.Due(data)) return;
            var moved = new List<string>();
            float freed = FleetHolds.MakeRoom(ship, stack, moved);
            if (freed > 0f)
            {
                Plugin.Log.LogInfo($"[FleetHolds] {data.GetShipName()} passed {freed:0} m3 on: {string.Join(", ", moved)}");
                __result = data.GetCargoAvailable() >= type.m3;
            }
        }
        catch (Exception e) { f.Fail(e, nameof(FitPostfix)); }
    }
}
