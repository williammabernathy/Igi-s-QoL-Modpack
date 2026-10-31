using System.Collections;
using Behaviour.Gameplay;
using Behaviour.Item;
using Behaviour.Managers;
using Behaviour.Tractoring;
using Behaviour.UI.NotificationAlert;
using Behaviour.Util;
using IgiQoL.Core;
using IgiQoL.UI;
using Source.Data.Persistable;
using Source.Galaxy;
using Source.Item;
using Source.Player;
using Source.Util;
using TMPro;
using UnityEngine;

namespace IgiQoL.Features.EventLog;

internal static class EventLogCheck
{
    public static IEnumerator Run(SelfCheckContext ctx)
    {
        // A notification shown twice in a row is one line counted twice.
        int before = EventLog.Entries.Count;
        Singleton<NotificationManager>.Instance.CreateNotification("IgiQoL check notice").WithColor(ColorHelper.orange75).Show();
        Singleton<NotificationManager>.Instance.CreateNotification("IgiQoL check notice").WithColor(ColorHelper.orange75).Show();
        var notice = EventLog.Last(LogKind.Notice);
        string d = $"lines {before}->{EventLog.Entries.Count}, last notice '{notice?.text}' x{notice?.repeats}";
        if (notice != null && notice.text == "IgiQoL check notice" && notice.repeats == 2 && EventLog.Entries.Count == before + 1) ctx.Pass("notice", d);
        else ctx.Fail("notice", d);

        Singleton<EventLogManager>.Instance.NewEvent("igi", "IgiQoL check event line");
        var ev = EventLog.Last(LogKind.Event);
        if (ev != null && ev.text == "IgiQoL check event line") ctx.Pass("event", ev.text);
        else ctx.Fail("event", ev?.text ?? "none");

        // ECHO's status: logged only while ECHO is on, only when it changes, and never the "Calculating..." in between.
        var player = GamePlayer.current;
        bool auto0 = player.autoPlay;
        player.autoPlay = true;
        try
        {
            var echo0 = EventLog.Last(LogKind.Echo);
            string expected = Translation.Translate("@IdleWantToTravel");
            before = EventLog.Entries.Count;
            IdleManager.UpdateActivity("@IdleWantToTravel");
            IdleManager.UpdateActivity("@IdleCalculating");
            IdleManager.UpdateActivity("@IdleWantToTravel");
            var echo = EventLog.Last(LogKind.Echo);
            int added = EventLog.Entries.Count - before, want = echo0 != null && echo0.text == expected ? 0 : 1;
            d = $"{added} new lines (expected {want}), last ECHO line '{echo?.text}'";
            if (echo != null && echo.text == expected && added == want) ctx.Pass("echo", d);
            else ctx.Fail("echo", d);
        }
        finally { player.autoPlay = auto0; }

        // A finished operation, as the game records it in its own operation log.
        var opLog = player.dungeonActivityLog;
        var op = new Source.Dungeon.DungeonActivityLogEntry
        {
            timestamp = System.DateTime.Now,
            dungeonLocationName = "@DungeonMiningOutpostName",
            poiName = "Check Field",
            outcome = Source.CompartmentSystem.MissionOutcome.FriendlyVictory,
            isAutonomous = true,
            hostileKilled = 4,
            hostilesCaptured = 1,
            friendlyKilled = 2,
            friendlyWounded = 3,
        };
        op.lootSummary.Add(new Source.Dungeon.DungeonLootSummaryLine { itemName = "Check", totalCount = 6 });
        op.lootSummary.Add(new Source.Dungeon.DungeonLootSummaryLine { itemName = "Credits", builderIdentifier = "Credits", totalCount = 315800 });
        opLog.Record(op);
        opLog.entries.Remove(op);
        var opLine = EventLog.Last(LogKind.Operation);
        string opExpected = $"ECHO: {Translation.Translate("@DungeonMiningOutpostName")} at Check Field: succeeded; 4 defenders killed, 1 captured; 2 crew killed, 3 wounded; 6 items from 1 loot kinds, {Source.Util.GameMath.FormatNumber(315800)} credits";
        if (opLine != null && opLine.text == opExpected) ctx.Pass("operation", opLine.text);
        else ctx.Fail("operation", $"got '{opLine?.text}', expected '{opExpected}'");

        yield return CheckLoot(ctx);

        ModWindow.Open("log");
        ctx.Cleanup(ModWindow.Close);
        yield return null;
        yield return null;
        var list = ModWindow.IsOpen ? ModWindow.Content.GetComponentInChildren<EventLogTab.LogList>() : null;
        bool noticeRow = false, lootRow = false, summary = false;
        if (list != null)
            foreach (var t in list.GetComponentsInChildren<TMP_Text>())
            {
                noticeRow |= t.text == "IgiQoL check notice (x2)";
                lootRow |= t.text.StartsWith("Picked up ", System.StringComparison.Ordinal);
                summary |= t.text.StartsWith("Picked up since the game started", System.StringComparison.Ordinal);
            }
        d = $"rows={(list != null ? list.RowCount : -1)} notice={noticeRow} loot={lootRow} summary={summary}";
        if (list != null && noticeRow && (lootRow && summary || EventLog.Loot.Count == 0)) ctx.Pass("tab", d);
        else ctx.Fail("tab", d);
        yield return ctx.Screenshot("window-log");
        ModWindow.Close();
    }

    // Two stacks of the same ore pulled into the flagship's hold through the game's own pickup add up on one line.
    private static IEnumerator CheckLoot(SelfCheckContext ctx)
    {
        yield return SelfCheckContext.EnsureInSpace();
        var ship = GameplayManager.Instance != null ? GameplayManager.Instance.spaceShip : null;
        if (ship == null || !Singleton<LootManager>.HasInstance)
        {
            ctx.Skip("loot", "not in space");
            yield break;
        }
        InventoryItemType ore = null;
        foreach (var t in InventoryItemType.all)
            if (t.itemCategory == ItemCategory.Ore) { ore = t; break; }
        var cargo = ship.spaceShipData.cargo;
        int cargo0 = cargo.GetCount(ore);
        EventLog.Loot.TryGetValue(ore, out int total0);
        int picked = 0;
        for (int i = 0; i < 2; i++)
        {
            var data = new TractorableItemData
            {
                itemType = ore,
                itemAmount = 3 + i,
                jettisoned = true,
                ownerFaction = Faction.player,
                position = (Vector2)ship.transform.position + new Vector2(2f, 0f),
            };
            var item = Singleton<LootManager>.Instance.SpawnLootItem(data, null);
            picked += item.AddItemToShipInventory(ship);
            Singleton<LootManager>.Instance.RemoveLootItem(item.data);
            Object.Destroy(item.gameObject);
        }
        cargo.Remove(ore, cargo.GetCount(ore) - cargo0);
        var line = EventLog.Last(LogKind.Loot);
        EventLog.Loot.TryGetValue(ore, out int total);
        string d = $"{ore.identifier}: picked {picked}, line '{line?.text}' amount {line?.amount}, session total {total0}->{total}";
        if (picked == 7 && line != null && line.item == ore && line.amount >= 7 && total == total0 + 7) ctx.Pass("loot", d);
        else ctx.Fail("loot", d);
    }
}
