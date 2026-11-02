using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Item;
using Behaviour.Managers;
using Behaviour.Tractoring;
using Behaviour.UI.NotificationAlert;
using Behaviour.UI.Side_Menu;
using Behaviour.Unit;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.UI;
using Source.Galaxy;
using Source.NotificationAlert;
using Source.Player;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.EventLog;

public enum LogKind { Notice, Event, Echo, Travel, Loot, Operation }

public sealed class LogEntry
{
    public DateTime when;
    public float at;            // Time.unscaledTime of the last update, for merging
    public LogKind kind;
    public string text;
    public Color color;
    public int repeats = 1;     // identical notices in a row
    public InventoryItemType item;
    public int amount;
}

// Keeps what the game only shows for a few seconds (notifications, the event lines above the HUD, ECHO's activity,
// arrivals, loot picked up), so you can read back what happened while you were away.
public sealed class EventLog : Feature
{
    public static EventLog Instance;

    public EventLog() { Instance = this; }

    public override string Id => "EventLog";
    public override string Title => "Event log";
    public override string Description => "The Log tab: the game's pop-up notifications, the event lines above the HUD, ECHO's activity, arrivals, finished operations and the loot your ships pick up, with the time each happened, so you can read back what happened while you were away. Kept until you quit the game.";
    public override Type[] PatchClasses => new[] { typeof(EventLogPatches) };

    public const int MaxEntries = 1000;
    // Newest last.
    public static readonly List<LogEntry> Entries = new();
    // Session loot totals by item.
    public static readonly Dictionary<InventoryItemType, int> Loot = new();
    public static int Version;

    private static readonly Color EchoColor = new(0.55f, 0.8f, 1f), TravelColor = new(0.75f, 0.75f, 0.75f);

    public override void OnActivated()
    {
        ModWindow.AddTab(this, "log", "Log", 8, EventLogTab.Build);
        EventLogTab.RegisterPopout(this);
        GameEvents.Arrived += OnArrived;
    }

    private void OnArrived(SpaceShip ship)
    {
        if (!Active) return;
        try
        {
            if (ship == null || ship != Fleet.Flagship) return;
            var poi = MapPointOfInterest.current;
            if (poi != null) Add(LogKind.Travel, "Arrived at " + poi.name + (poi.system != null ? " (" + poi.system.name + ")" : ""), TravelColor);
        }
        catch (Exception e) { Fail(e, nameof(OnArrived)); }
    }

    // One line per finished operation, from the entry the game adds to its own operation log.
    internal static string OperationText(Source.Dungeon.DungeonActivityLogEntry e)
    {
        string outcome = e.outcome switch
        {
            Source.CompartmentSystem.MissionOutcome.FriendlyVictory => "succeeded",
            Source.CompartmentSystem.MissionOutcome.FriendlyExtracted => "crew pulled out",
            Source.CompartmentSystem.MissionOutcome.HostileVictory => "crew lost",
            Source.CompartmentSystem.MissionOutcome.ShipExploded => "ship exploded",
            _ => e.outcome.ToString(),
        };
        string what = !string.IsNullOrEmpty(e.targetName) ? "Boarding " + e.targetName : Translation.Translate(e.dungeonLocationName);
        // Credits found aboard (personal caches) are loot lines too, built by the "Credits" item builder; added to the
        // item count they turned 1.2M credits into "1.2M items".
        long items = 0, credits = 0;
        int kinds = 0;
        foreach (var l in e.lootSummary)
        {
            if (l.builderIdentifier == "Credits")
            {
                credits += l.totalCount;
                continue;
            }
            items += l.totalCount;
            kinds++;
        }
        string money = credits > 0 ? ", " + Source.Util.GameMath.FormatNumber(credits) + " credits" : "";
        return $"{(e.isAutonomous ? "ECHO: " : "")}{what} at {e.poiName}: {outcome}; {e.hostileKilled} defenders killed, {e.hostilesCaptured} captured; {e.friendlyKilled} crew killed, {e.friendlyWounded} wounded; {items} items from {kinds} loot {(kinds == 1 ? "kind" : "kinds")}{money}";
    }

    public static void Clear()
    {
        Entries.Clear();
        Loot.Clear();
        Version++;
    }

    internal static LogEntry Last(LogKind kind)
    {
        for (int i = Entries.Count - 1; i >= 0; i--)
            if (Entries[i].kind == kind) return Entries[i];
        return null;
    }

    internal static void Add(LogKind kind, string text, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        // The same notice again within a few seconds counts up instead of adding a line.
        var last = Entries.Count > 0 ? Entries[Entries.Count - 1] : null;
        if (last != null && last.kind == kind && last.text == text && Time.unscaledTime - last.at < 10f)
        {
            last.repeats++;
            last.at = Time.unscaledTime;
            last.when = DateTime.Now;
            Version++;
            return;
        }
        Push(new LogEntry { when = DateTime.Now, at = Time.unscaledTime, kind = kind, text = text, color = color });
    }

    // ECHO repeats its status every decision and shows "Calculating..." between decisions; only a change to a real
    // status is a new line.
    private static string calculating;

    internal static void Echo(string text)
    {
        calculating ??= Translation.Translate("@IdleCalculating");
        if (text == calculating) return;
        var last = Last(LogKind.Echo);
        if (last != null && last.text == text) return;
        Push(new LogEntry { when = DateTime.Now, at = Time.unscaledTime, kind = LogKind.Echo, text = text, color = EchoColor });
    }

    // Pickups of the same item within a minute of each other share a line.
    internal static void Picked(InventoryItemType item, int amount)
    {
        Loot.TryGetValue(item, out int total);
        Loot[item] = total + amount;
        for (int i = Entries.Count - 1; i >= 0 && i >= Entries.Count - 20; i--)
        {
            var e = Entries[i];
            if (e.kind == LogKind.Loot && e.item == item && Time.unscaledTime - e.at < 60f)
            {
                e.amount += amount;
                e.at = Time.unscaledTime;
                e.when = DateTime.Now;
                e.text = LootText(item, e.amount);
                Version++;
                return;
            }
        }
        Push(new LogEntry { when = DateTime.Now, at = Time.unscaledTime, kind = LogKind.Loot, item = item, amount = amount, text = LootText(item, amount), color = item.rarity.GetColor() });
    }

    private static string LootText(InventoryItemType item, int amount) => "Picked up " + Translation.Translate(item.displayName) + " x" + amount;

    private static void Push(LogEntry e)
    {
        Entries.Add(e);
        if (Entries.Count > MaxEntries) Entries.RemoveRange(0, Entries.Count - MaxEntries);
        Version++;
    }

    internal static Color EchoTint => EchoColor;

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => EventLogCheck.Run(ctx);
}

[HarmonyPatch]
internal static class EventLogPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NotificationBuilder), nameof(NotificationBuilder.Show))]
    private static void NoticePrefix(NotificationBuilder __instance)
    {
        var f = EventLog.Instance;
        if (!f.Active) return;
        try { EventLog.Add(LogKind.Notice, __instance.text, __instance.color ?? Color.white); }
        catch (Exception e) { f.Fail(e, nameof(NoticePrefix)); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EventLogManager), nameof(EventLogManager.NewEvent))]
    private static void EventPrefix(string log)
    {
        var f = EventLog.Instance;
        if (!f.Active) return;
        try { EventLog.Add(LogKind.Event, log, ColorHelper.offWhite); }
        catch (Exception e) { f.Fail(e, nameof(EventPrefix)); }
    }

    // ECHO's status line (IdleManager.UpdateActivity and DelayIdleActivities both end here).
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SidePanel), nameof(SidePanel.SetIdleStatus))]
    private static void EchoPrefix(string msg)
    {
        var f = EventLog.Instance;
        if (!f.Active) return;
        try
        {
            if (GamePlayer.current != null && GamePlayer.current.autoPlay) EventLog.Echo(msg);
        }
        catch (Exception e) { f.Fail(e, nameof(EchoPrefix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GamePlayer), nameof(GamePlayer.StartAutopilotSession))]
    private static void EchoOnPostfix()
    {
        var f = EventLog.Instance;
        if (!f.Active) return;
        try { EventLog.Add(LogKind.Echo, "ECHO switched on", EventLog.EchoTint); }
        catch (Exception e) { f.Fail(e, nameof(EchoOnPostfix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GamePlayer), nameof(GamePlayer.EndAutopilotSession))]
    private static void EchoOffPostfix()
    {
        var f = EventLog.Instance;
        if (!f.Active) return;
        try { EventLog.Add(LogKind.Echo, "ECHO switched off", EventLog.EchoTint); }
        catch (Exception e) { f.Fail(e, nameof(EchoOffPostfix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TractorableItem), nameof(TractorableItem.AddItemToShipInventory))]
    private static void PickupPostfix(TractorableItem __instance, AbstractUnit unit, int __result)
    {
        var f = EventLog.Instance;
        if (!f.Active || __result <= 0) return;
        try
        {
            var item = __instance.data?.itemType;
            if (item != null && unit != null && unit.IsPlayer()) EventLog.Picked(item, __result);
        }
        catch (Exception e) { f.Fail(e, nameof(PickupPostfix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Source.Dungeon.DungeonActivityLog), nameof(Source.Dungeon.DungeonActivityLog.Record))]
    private static void OperationPostfix(Source.Dungeon.DungeonActivityLogEntry entry)
    {
        var f = EventLog.Instance;
        if (!f.Active || entry == null) return;
        try
        {
            bool good = entry.outcome == Source.CompartmentSystem.MissionOutcome.FriendlyVictory;
            EventLog.Add(LogKind.Operation, EventLog.OperationText(entry), good ? ColorHelper.greenish : ColorHelper.orange75);
        }
        catch (Exception e) { f.Fail(e, nameof(OperationPostfix)); }
    }
}
