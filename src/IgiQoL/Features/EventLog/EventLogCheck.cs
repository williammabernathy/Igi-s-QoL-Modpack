using System.Collections;
using System.Collections.Generic;
using Behaviour.Gameplay;
using Behaviour.Item;
using Behaviour.Managers;
using Behaviour.Tractoring;
using Behaviour.UI.NotificationAlert;
using Behaviour.Util;
using IgiQoL.Core;
using IgiQoL.Game;
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
        string opExpected = $"ECHO: {Translation.Translate("@DungeonMiningOutpostName")} at Check Field: succeeded; 4 defenders killed, 1 captured; 2 crew killed, 3 wounded; 6 items from 1 loot kind, {Source.Util.GameMath.FormatNumber(315800)} credits";
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

        yield return CheckPopout(ctx);
    }

    // The pop-out lists the newest lines first, keeps to its own line count and filter, and docks back into the tab.
    private static IEnumerator CheckPopout(SelfCheckContext ctx)
    {
        const string id = EventLogTab.PopoutId;
        string pref = PlayerPrefs.GetString(PopoutState.Pref(id), "");
        bool wasOpen = Popouts.IsOpen(id);
        ctx.Cleanup(() =>
        {
            Popouts.Close(id);
            PlayerPrefs.SetString(PopoutState.Pref(id), pref);
            Popouts.ForgetState(id);
            if (wasOpen) Popouts.Open(id);
        });
        Popouts.Close(id);
        var state = Popouts.State(id);
        state.show = PopoutShow.Always;
        state.collapsed = false;
        state.options[EventLogTab.FilterOption] = -1;
        state.options[EventLogTab.LinesOption] = 10;
        for (int i = 0; i < 12; i++) Singleton<EventLogManager>.Instance.NewEvent("igi", "IgiQoL check pop-out line " + i);
        Popouts.Open(id);
        var p = Popouts.Get(id);
        if (p == null)
        {
            ctx.Fail("popout", "the pop-out did not open");
            yield break;
        }
        yield return null;
        var list = p.GetComponentInChildren<EventLogTab.LogList>();
        // Redraw now and read the log in the same frame: game notices and event lines keep arriving, so the rows are
        // compared with the log's own newest entries, not with the lines pushed above.
        if (list != null) list.Invalidate();
        yield return null;
        var shown = LineTexts(list, 2);
        var entries = EventLog.Entries;
        bool same = list != null && list.RowCount == 10 && shown.Count == 10 && entries.Count >= 10;
        for (int i = 0; same && i < 10; i++)
        {
            var e = entries[entries.Count - 1 - i];
            same = shown[i] == (e.repeats > 1 ? e.text + " (x" + e.repeats + ")" : e.text);
        }
        string d = $"rows={(list != null ? list.RowCount : -1)} first='{(shown.Count > 0 ? shown[0] : null)}' newest='{(entries.Count > 0 ? entries[entries.Count - 1].text : null)}'";
        if (same) ctx.Pass("popout", d);
        else ctx.Fail("popout", d);
        yield return ctx.Screenshot("log-popout");

        // Its own filter: notifications only, while the tab keeps showing everything.
        int rows = 0;
        bool onlyNotices = true;
        if (list != null)
        {
            list.filterKind = (int)LogKind.Notice;
            list.Invalidate();
            yield return null;
            rows = list.RowCount;
            var column = list.transform;
            for (int i = 0; i < column.childCount; i++)
            {
                var row = column.GetChild(i);
                if (!row.gameObject.activeSelf || row.childCount < 3) continue;
                onlyNotices &= row.GetChild(1).GetComponent<TMP_Text>().text == EventLogTab.KindName(LogKind.Notice);
            }
        }
        d = $"rows={rows} onlyNotices={onlyNotices} tabFilter={EventLogTab.filter}";
        if (rows > 0 && onlyNotices && EventLogTab.filter == -1) ctx.Pass("popout-filter", d);
        else ctx.Fail("popout-filter", d);

        Popouts.DockBack(id);
        yield return null;
        d = $"open={Popouts.IsOpen(id)} window={ModWindow.IsOpen} tab={ModWindow.CurrentTab}";
        if (!Popouts.IsOpen(id) && ModWindow.IsOpen && ModWindow.CurrentTab == "log") ctx.Pass("popout-dock", d);
        else ctx.Fail("popout-dock", d);
        ModWindow.Close();
    }

    // The texts of the log lines (not the loot summary) in a LogList, in the order shown; cell 2 is the line's text.
    private static List<string> LineTexts(EventLogTab.LogList list, int cell)
    {
        var texts = new List<string>();
        if (list == null) return texts;
        var column = list.transform;
        for (int i = 0; i < column.childCount; i++)
        {
            var row = column.GetChild(i);
            if (row.gameObject.activeSelf && row.childCount > cell) texts.Add(row.GetChild(cell).GetComponent<TMP_Text>().text);
        }
        return texts;
    }

    // Two stacks of the same ore pulled into the flagship's hold through the game's own pickup add up on one line.
    private static IEnumerator CheckLoot(SelfCheckContext ctx)
    {
        yield return SelfCheckContext.EnsureInSpace();
        var ship = Fleet.Flagship;
        if (ship == null || !Singleton<LootManager>.HasInstance)
        {
            ctx.Skip("loot", "not in space");
            yield break;
        }
        InventoryItemType ore = null;
        foreach (var t in InventoryItemType.all)
            if (t.itemCategory == ItemCategory.Ore) { ore = t; break; }
        var cargo = ship.spaceShipData.cargo;
        // The game's pickup clamps to free space, so a full hold would pick up less than the 7 the check expects.
        if (ore == null || cargo.GetSpaceAvailable() < 7f * ore.m3)
        {
            ctx.Skip("loot", ore == null ? "no ore item type" : "flagship hold can't take the ore");
            yield break;
        }
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
