using System;
using System.Collections;
using Behaviour.Unit;
using HarmonyLib;
using IgiQoL.Core;
using Source.SpaceShip;
using Source.SpaceShip.Auto;

namespace IgiQoL.Features.FleetRetreat;

// A fleet ship leaves the fight for its 120 s repair below 10% hull (WingmanActions.OnDamageTaken). This moves that
// threshold up per ship. It can't go below the game's 10%: under it a fleet ship sits at 0.1 hull and can't be destroyed.
public sealed class FleetRetreat : Feature
{
    public static FleetRetreat Instance;

    public FleetRetreat() { Instance = this; }

    public override string Id => "FleetRetreat";
    public override string Title => "Retreat for repairs";
    public override string Description => "Lets each fleet ship leave a fight for repairs at a higher hull % than the game's 10%, so it loses less crew to hull hits. It repairs and comes back as it does in the game. Set it per ship with the last button on the ship's row in the fleet panel.";
    public override Type[] PatchClasses => new[] { typeof(RetreatPatch) };

    public static readonly int?[] Values = { null, 20, 30, 50, 75 };
    public static readonly string[] Names = { "Game default (below 10%)", "Below 20% hull", "Below 30% hull", "Below 50% hull", "Below 75% hull" };

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        yield return SelfCheckContext.EnsureInSpace();
        var gm = GameplayManager.Instance;
        var flagship = gm != null ? gm.spaceShip : null;
        SpaceShip wing = null;
        if (gm != null)
            foreach (var s in gm.fleetSpaceShips)
                if (s != null && s != flagship && !s.travelling && s.gameObject.activeInHierarchy && !s.isDestroyed
                    && s.autoActions is WingmanActions w && !w.leaving) { wing = s; break; }
        if (wing == null)
        {
            ctx.Skip("settings-roundtrip", "no fleet ship in space");
            ctx.Skip("live", "no fleet ship in space");
            yield break;
        }

        var data = wing.spaceShipData;
        var g = data.guid;
        var before = ShipSettings.Get(g).retreatBelow;
        var actions = (WingmanActions)wing.autoActions;
        float hull = wing.currentHullHP;
        string detail;
        bool ok;
        try
        {
            ShipSettings.Edit(g).retreatBelow = 30;
            ShipSettings.Save(g);
            ShipSettings.Invalidate();
            var record = SaveData.Ship(g, false);
            int? saved = record != null && record["retreatBelow"].IsNumber ? (int)record["retreatBelow"].AsNumber : null;
            detail = $"ship={data.GetShipName()} read={ShipSettings.Get(g).retreatBelow} saved={saved}";
            if (ShipSettings.Get(g).retreatBelow == 30 && saved == 30) ctx.Pass("settings-roundtrip", detail);
            else ctx.Fail("settings-roundtrip", detail);

            // A hit at 40% hull: the game's 10% keeps the ship in, a 50% threshold at 60% hull keeps it in,
            // and a 50% threshold at 40% hull sends it off for repairs.
            ShipSettings.Edit(g).retreatBelow = null;
            wing.currentHullHP = wing.maxHullHP * 0.4f;
            actions.OnDamageTaken(null);
            bool gameDefault = actions.leaving;
            ShipSettings.Edit(g).retreatBelow = 50;
            wing.currentHullHP = wing.maxHullHP * 0.6f;
            actions.OnDamageTaken(null);
            bool above = actions.leaving;
            wing.currentHullHP = wing.maxHullHP * 0.4f;
            actions.OnDamageTaken(null);
            bool below = actions.leaving;
            var activity = actions.GetActivity();
            detail = $"ship={data.GetShipName()} default40%={gameDefault} threshold50@60%={above} threshold50@40%={below} away={data.awayForRepairs} activity={activity}";
            ok = !gameDefault && !above && below && data.awayForRepairs && activity == FleetShipActivity.Retreating;
        }
        finally
        {
            wing.currentHullHP = hull;
            ShipSettings.Edit(g).retreatBelow = before;
            ShipSettings.Save(g);
        }
        // The ship is on its way out now; it repairs for 120 s and rejoins, as after a retreat in play.
        if (ok) ctx.Pass("live", detail);
        else ctx.Fail("live", detail);
    }
}

[HarmonyPatch]
internal static class RetreatPatch
{
    // The game's own check runs first (below 10%); this repeats it at the ship's threshold, with the same steps
    // as the private WingmanActions.StartFleeRepair.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(WingmanActions), nameof(WingmanActions.OnDamageTaken))]
    private static void Postfix(WingmanActions __instance, AbstractUnit ___parent)
    {
        var feature = FleetRetreat.Instance;
        if (!feature.Active) return;
        try
        {
            if (__instance.leaving || ___parent == null || !(___parent.unitData is SpaceShipData data) || data.arenaFleetBattleUnit) return;
            var below = ShipSettings.Get(data.guid).retreatBelow;
            if (below == null || ___parent.fractionHullHP >= below.Value / 100f) return;
            if (data.ReturnsAfterRepair)
            {
                data.awayForRepairs = true;
                data.StartFleetRepair(data.GetFleetRepairTime());
            }
            __instance.ExitPOI();
            Plugin.Log.LogInfo($"[FleetRetreat] {data.GetShipName()} leaves for repairs at {___parent.fractionHullHP * 100f:0}% hull (below {below.Value}%)");
        }
        catch (Exception e) { feature.Fail(e, nameof(RetreatPatch)); }
    }
}
