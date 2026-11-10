using Behaviour.Equipment.Turret;
using Behaviour.Weapons;
using Source.SpaceShip;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Behaviour.Item;
using Behaviour.UI.Missions;
using IgiQoL.Core;
using IgiQoL.Game;
using Source.Galaxy.POI;
using Source.MissionSystem;
using Source.Player;
using Source.Util;
using UnityEngine;
using Board = Behaviour.UI.MissionBoard;

namespace IgiQoL.Features.MissionFit;

internal static class MissionFitCheck
{
    // The board as the save has it. Then a turret for the first greyed offer's work goes into a fleet ship's first
    // hardpoint (in data only), and then into the flagship's instead: either must clear that offer's mark, since your
    // fleet ships count as much as the flagship. When the fleet fits every offer, the turrets for one offer's work are
    // first taken off every ship (in data only) so that offer greys. Hardpoints go back in a finally before anything
    // else runs.
    public static IEnumerator Run(SelfCheckContext ctx)
    {
        Board board = null;
        yield return MissionSort.MissionSortCheck.OpenBoard(ctx, b => board = b, "rows");
        if (board == null) yield break;
        if (Offers().Count == 0) { ctx.Skip("rows", "the board has no offers"); yield break; }
        board.ShowMissionBoard();
        yield return null;
        int before = CheckRows(ctx, "rows", board);
        var offersBefore = new List<Mission>(Offers());
        yield return ctx.Screenshot("mission-board-fit");

        Mission target = null;
        InventoryItemType gear = null;
        foreach (var m in Offers())
            if (MissionFit.Needs(m, out var t, out var l) && !MissionFit.Fits(t, l) && (gear = GearFor(t, l)) != null) { target = m; break; }
        var ships = Fleet.Ships(withMercenaries: false);
        var saved = ships.Select(s => s.hardpoints).ToList();
        try
        {
            // The hardpoints and greyed count each fit step starts from: the save's, or the stripped ones.
            var baseline = saved;
            int greyed = before;
            if (target == null && before == 0 && (target = StripOne(ships, out gear, out string stripped)) != null)
            {
                baseline = ships.Select(s => s.hardpoints).ToList();
                board.ShowMissionBoard();
                yield return null;
                greyed = CheckRows(ctx, "rows-stripped", board, stripped + ": ");
            }
            if (target == null)
            {
                string why = before == 0 ? "no greyed offer, and no offer whose work only turrets do" : "no turret in the game for the greyed offers' work";
                foreach (var n in new[] { "fit-fleet-ship-rows", "fit-fleet-ship", "fit-flagship-rows", "fit-flagship" }) ctx.Skip(n, why);
            }
            else
            {
                if (ships.Count < 2)
                    foreach (var n in new[] { "fit-fleet-ship-rows", "fit-fleet-ship" }) ctx.Skip(n, "no fleet ship besides the flagship");
                else yield return Fit(ctx, "fit-fleet-ship", board, ships[1], gear, target, greyed);
                if (ships.Count > 1) ships[1].hardpoints = baseline[1];
                yield return Fit(ctx, "fit-flagship", board, ships[0], gear, target, greyed);
            }
        }
        finally
        {
            for (int i = 0; i < ships.Count; i++) ships[i].hardpoints = saved[i];
        }
        board.ShowMissionBoard();
        yield return null;
        int after = CheckRows(ctx, "restored", board);
        // The board can refresh its offers while the check runs; then the counts describe different missions, and
        // "restored" above has already checked the new rows against the fleet's fit.
        var offersAfter = Offers();
        bool same = offersAfter.Count == offersBefore.Count && offersBefore.TrueForAll(offersAfter.Contains);
        if (!same) ctx.Skip("restored-count", $"the board refreshed during the check ({offersBefore.Count} offers before, {offersAfter.Count} after, not the same missions)");
        else if (after != before) ctx.Fail("restored-count", $"{after} greyed after the fit came back, {before} before");
        else ctx.Pass("restored-count", $"{after} greyed after the fit came back, as before");
    }

    private static IEnumerator Fit(SelfCheckContext ctx, string name, Board board, SpaceShipData ship, InventoryItemType gear, Mission target, int before)
    {
        if (ship.hardpoints.Length == 0) { ctx.Skip(name, ship.GetShipName() + " has no hardpoints"); yield break; }
        var fitted = (InventoryItemType[])ship.hardpoints.Clone();
        fitted[0] = gear;
        ship.hardpoints = fitted;
        board.ShowMissionBoard();
        yield return null;
        int marked = CheckRows(ctx, name + "-rows", board);
        Transform row = null;
        foreach (Transform child in board.window.list)
            if (child.TryGetComponent<MissionListRow>(out var r) && r.mission == target) row = child;
        bool cleared = row != null && row.Find("Text/Tags/" + MissionFit.ChipName) == null;
        string detail = $"{Translation.Translate(gear.displayName)} on {ship.GetShipName()}: '{Translation.Translate(target.name)}' {(cleared ? "no longer greyed" : "still greyed")}, {before} -> {marked} greyed";
        if (cleared && marked < before) ctx.Pass(name, detail);
        else ctx.Fail(name, detail);
    }

    // Any turret in the game that does this work on this layer.
    private static InventoryItemType GearFor(GameplayType type, TargetLayer layer)
    {
        foreach (var item in InventoryItemType.all)
            if (item != null && item.TryGetComponent<AbstractTurret>(out var turret) && turret.gameplayType == type && turret.CanTargetLayer(layer)) return item;
        return null;
    }

    // The first offer whose work only turrets give the fleet: those turrets come off every ship's hardpoints (in data),
    // and if a drone, torpedo or hangar bay still covers the work, the next offer is tried. Leaves the stripped
    // hardpoints in place for the caller to put back.
    private static Mission StripOne(List<SpaceShipData> ships, out InventoryItemType gear, out string stripped)
    {
        var saved = ships.Select(s => s.hardpoints).ToList();
        foreach (var m in Offers())
        {
            if (!MissionFit.Needs(m, out var t, out var l) || (gear = GearFor(t, l)) == null) continue;
            int removed = 0;
            foreach (var ship in ships)
            {
                var left = (InventoryItemType[])ship.hardpoints.Clone();
                for (int i = 0; i < left.Length; i++)
                    if (left[i] != null && left[i].TryGetComponent<AbstractTurret>(out var turret) && turret.gameplayType == t && turret.CanTargetLayer(l)) { left[i] = null; removed++; }
                ship.hardpoints = left;
            }
            if (removed > 0 && !MissionFit.Fits(t, l))
            {
                stripped = $"took {removed} {MissionFit.WorkName(t, l)} turret(s) off the fleet for '{Translation.Translate(m.name)}'";
                return m;
            }
            for (int i = 0; i < ships.Count; i++) ships[i].hardpoints = saved[i];
        }
        gear = null;
        stripped = null;
        return null;
    }

    private static List<Mission> Offers() =>
        GamePlayer.current.currentPointOfInterest is SpaceStation ss ? ss.missionBoard.availableMissions : new List<Mission>();

    // Every row on the board: a "No equipment" chip, and a dimmed icon and name, exactly on the offers no ship of yours fits;
    // the chip itself isn't dimmed and missions you hold are never marked. Returns how many rows are marked.
    private static int CheckRows(SelfCheckContext ctx, string name, Board board, string prefix = "")
    {
        var offers = Offers();
        int rows = 0, marked = 0, bad = 0;
        var greyed = new List<string>();
        var wrong = new List<string>();
        foreach (Transform child in board.window.list)
        {
            if (!child.TryGetComponent<MissionListRow>(out var row)) continue;
            rows++;
            var m = row.mission;
            bool expect = offers.Contains(m) && !GamePlayer.current.missions.Contains(m)
                && MissionFit.Needs(m, out var type, out var layer) && !MissionFit.Fits(type, layer);
            var chip = child.Find("Text/Tags/" + MissionFit.ChipName);
            var iconTile = child.Find("IconTile");
            var icon = iconTile != null ? iconTile.GetComponent<CanvasGroup>() : null;
            bool dimmed = icon != null && Mathf.Approximately(icon.alpha, MissionFit.DimAlpha);
            bool ok = chip != null
                ? expect && dimmed && chip.GetComponent<CanvasGroup>() == null
                : !expect && icon == null;
            if (chip != null) { marked++; greyed.Add(Translation.Translate(m.name) + " " + (MissionFit.Needs(m, out var t, out var l) ? MissionFit.WorkName(t, l) : "-")); }
            if (!ok) { bad++; wrong.Add($"{Translation.Translate(m.name)} (chip {chip != null}, expected {expect}, dimmed {dimmed})"); }
        }
        string detail = $"{prefix}{marked} of {rows} rows greyed: [{string.Join(", ", greyed)}]" + (bad > 0 ? " | wrong: " + string.Join(", ", wrong) : "");
        if (bad == 0 && rows > 0) ctx.Pass(name, detail);
        else ctx.Fail(name, detail);
        return marked;
    }
}
