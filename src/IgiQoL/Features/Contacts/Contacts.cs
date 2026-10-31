using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Equipment.Module;
using Behaviour.Item;
using Behaviour.Managers;
using Behaviour.Mining;
using Behaviour.Salvage;
using Behaviour.Tractoring;
using Behaviour.UI;
using Behaviour.UI.HUD;
using Behaviour.UI.Movement;
using Behaviour.Unit;
using Behaviour.Util;
using Behaviour.Weapons;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using Source.Data.Persistable;
using Source.Galaxy;
using Source.Item;
using Source.MissionSystem;
using Source.Util;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.Contacts;

public sealed class Contacts : Feature
{
    public static Contacts Instance;

    public Contacts() { Instance = this; }

    public override string Id => "Contacts";
    public override string Title => "Contacts tab";
    public override string Description => "The Contacts tab: the boardable ships, wrecks, loot and asteroid ores at your location with the distance to each and a button to board, salvage, tractor or mine it, a counter above the QoL button showing what is left, and markers at the screen edge pointing at the nearest ones off screen.";
    public override string SettingsTab => "Contacts";
    public override Type[] PatchClasses => new[] { typeof(LootSpawnPatch) };

    public ConfigEntry<bool> HudCounter;
    public ConfigEntry<bool> MarkBoardable;
    public ConfigEntry<bool> MarkWrecks;
    public ConfigEntry<bool> MarkLoot;
    public ConfigEntry<bool> MarkOres;

    // Every TractorableItem spawned since startup (the game keeps no list of its own); pruned of destroyed pieces on read.
    public static readonly List<TractorableItem> Loot = new();

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        HudCounter = cfg.Bind(Id, "HudCounter", true, SettingInfo.Describe("Counter above the QoL button",
            "Shows how many boardable ships, wrecks and loot stacks are left at your location. Click it to open the Contacts tab."));
        MarkBoardable = cfg.Bind(Id, "MarkBoardable", true, SettingInfo.Describe("Edge markers: boardable ships",
            $"Marks the {ContactMarkers.MaxPerKind} nearest boardable ships that are off screen at the screen edge, with their distance. Click a marker to open boarding."));
        MarkWrecks = cfg.Bind(Id, "MarkWrecks", true, SettingInfo.Describe("Edge markers: wrecks",
            $"Marks the {ContactMarkers.MaxPerKind} nearest off-screen wrecks that still hold items or salvage. Click a marker to salvage it."));
        MarkLoot = cfg.Bind(Id, "MarkLoot", true, SettingInfo.Describe("Edge markers: loot",
            $"Marks the {ContactMarkers.MaxPerKind} nearest off-screen loot stacks, coloured by rarity. Click a marker to tractor it."));
        MarkOres = cfg.Bind(Id, "MarkOres", true, SettingInfo.Describe("Edge markers: preferred ores",
            $"Marks the {ContactMarkers.MaxPerKind} nearest off-screen asteroids with an ore from the Preferred ores list on the ECHO tab, coloured by the ore's rarity. Click a marker to send your mining ships there. Nothing shows while that list is empty."));
    }

    public override void OnActivated()
    {
        ModWindow.AddTab("contacts", "Contacts", 5, Build);
        GameEvents.SaveLoaded += OnSaveLoaded;
    }

    private void OnSaveLoaded()
    {
        if (!Active) return;
        Plugin.Instance.StartCoroutine(ContactsBadge.CreateWhenReady());
        Plugin.Instance.StartCoroutine(ContactMarkers.CreateWhenReady());
    }

    public static int PruneLoot()
    {
        for (int i = Loot.Count - 1; i >= 0; i--)
        {
            var item = Loot[i];
            if (item == null || !item.gameObject.activeInHierarchy) Loot.RemoveAt(i);
        }
        return Loot.Count;
    }

    public static void Board(BoardableUnit b)
    {
        ModWindow.Close();
        HudManager.Instance.ToggleBoardingPanel(b);
    }

    // SalvageContainer.OnMouseUpAsButton without the mouse-over-UI check (the click comes from this window).
    public static void Salvage(SalvageContainer c)
    {
        ModWindow.Close();
        var list = FleetCommands.WithLoadout(FleetCommands.GetClickWorkCandidates(), GameplayType.Salvage);
        if (list.Count > 0) FleetCommands.IssueClickWork(list, c);
        c.OnTargetedHandling();
        if (c.data.itemContent.Count > 0) HudManager.Instance.ToggleSalvageWindow(c, c.data);
        else if (list.Count == 0) InventoryInteractionManager.ShowRedNotification("@SalvageNoTurrets");
        MissionObjective.Trigger(MissionTrigger.TargetWreckage);
    }

    // Asteroid.OnMouseDown without the mouse-over-UI check (the click comes from this window).
    public static void Mine(Asteroid a)
    {
        ModWindow.Close();
        var list = FleetCommands.GetClickWorkCandidates();
        string note = a.GetLoadoutNotification(list);
        if (note != null) InventoryInteractionManager.ShowRedNotification(note);
        else FleetCommands.IssueClickWork(list.FindAll(s => a.GetLoadoutNotification(new[] { s }) == null), a);
        MissionObjective.Trigger(MissionTrigger.TargetAsteroid);
    }

    public static void Tractor(InventoryItemType type)
    {
        var ship = GameplayManager.Instance?.spaceShip;
        if (ship == null) return;
        var tractor = ship.GetModule<TractorModule>();
        if (tractor == null) return;
        PruneLoot();
        Vector2 from = ship.transform.position;
        TractorableItem nearest = null;
        float best = float.MaxValue;
        foreach (var item in Loot)
        {
            if (item.data == null || item.data.itemType != type || !item.canBeTractorAssisted) continue;
            float d = Vector2.Distance(from, item.transform.position);
            if (d < best) { best = d; nearest = item; }
        }
        if (nearest != null) tractor.SetManualTarget(nearest);
    }

    private static void Build(RectTransform content)
    {
        SettingRows.AddEntries(content, Instance.Id, SettingRows.Rows.Options);
        Kit.Muted(content, "Everything at this location you can board, salvage, tractor or mine, nearest first. Distances are in map units from your ship; the list refreshes every second.");
        Kit.Column(content, 2f).gameObject.AddComponent<ContactsList>();
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        CheckLootRegistry(ctx);

        ModWindow.Open("contacts");
        ctx.Cleanup(ModWindow.Close);
        yield return null;
        var list = ModWindow.IsOpen ? ModWindow.Content.GetComponentInChildren<ContactsList>() : null;
        bool boardHeading = false, wreckHeading = false, lootHeading = false;
        if (list != null)
            foreach (var t in list.GetComponentsInChildren<TMP_Text>())
            {
                boardHeading |= t.text.StartsWith("Boardable ships (", StringComparison.Ordinal);
                wreckHeading |= t.text.StartsWith("Wrecks (", StringComparison.Ordinal);
                lootHeading |= t.text.StartsWith("Loot (", StringComparison.Ordinal);
            }
        string detail = list == null ? "no list" : $"boardable={list.BoardableCount} wrecks={list.WreckCount} lootGroups={list.LootGroupCount}";
        if (ModWindow.CurrentTab == "contacts" && boardHeading && wreckHeading && lootHeading) ctx.Pass("tab", detail);
        else ctx.Fail("tab", detail + $" tab={ModWindow.CurrentTab} headings={boardHeading}/{wreckHeading}/{lootHeading}");
        yield return ctx.Screenshot("window-contacts");

        if (ContactsBadge.Instance != null) ctx.Pass("counter", $"text='{ContactsBadge.Instance.Text}' shown={ContactsBadge.Instance.Shown}");
        else ctx.Skip("counter", "no QoL button");
        ModWindow.Close();

        yield return CheckMarkers(ctx);
        yield return CheckOres(ctx);
    }

    // Two Cerrax asteroids and one Amberic spawned next to the ship show up as two ore rows, the listed ore marked.
    private static IEnumerator CheckOres(SelfCheckContext ctx)
    {
        yield return SelfCheckContext.EnsureInSpace();
        var ship = GameplayManager.Instance?.spaceShip;
        var ores = OrePreference.OrePreference.KnownOres();
        if (ship == null || ores.Count < 2)
        {
            ctx.Skip("ores", "not in space");
            yield break;
        }
        OreItemData a = ores[0], b = ores[1];
        Vector2 at = ship.transform.position;
        var spawned = new[]
        {
            SelfCheckContext.SpawnAsteroid(at + new Vector2(10f, 0f), a),
            SelfCheckContext.SpawnAsteroid(at + new Vector2(18f, 4f), a),
            SelfCheckContext.SpawnAsteroid(at + new Vector2(-14f, 0f), b),
        };
        if (Array.IndexOf(spawned, null) >= 0)
        {
            foreach (var d in spawned) SelfCheckContext.RemoveAsteroid(d);
            ctx.Skip("ores", "no asteroid field in this system to copy");
            yield break;
        }
        var mode0 = OrePreference.OrePreference.Mode;
        var list0 = new List<string>(OrePreference.OrePreference.Ores);
        try
        {
            OrePreference.OrePreference.Set(OrePreference.OreMode.Prefer, new[] { b.item.identifier });
            ModWindow.Open("contacts");
            yield return new WaitForSeconds(1.5f);
            var list = ModWindow.IsOpen ? ModWindow.Content.GetComponentInChildren<ContactsList>() : null;
            string aName = Translation.Translate(a.item.displayName), bName = Translation.Translate(b.item.displayName);
            // At a real mining location more asteroids of that ore may be around; the row counts them all.
            int aCount = 0;
            foreach (var x in BasePoiManager.current.GetComponentsInChildren<Asteroid>())
                if (x.asteroidData != null && x.asteroidData.surfaceItem == a && x.asteroidData.surfaceAmount > 0) aCount++;
            string aExpected = $"{aName}, surface | {aCount} asteroids";
            bool heading = false, aRow = false, bRow = false;
            var texts = new List<string>();
            if (list != null)
                foreach (var t in list.GetComponentsInChildren<TMP_Text>())
                {
                    heading |= t.text.StartsWith("Asteroid ores (", StringComparison.Ordinal);
                    if (t.text.StartsWith(aName + ", ", StringComparison.Ordinal) || t.text.StartsWith(bName + ", ", StringComparison.Ordinal)) texts.Add(t.text);
                    aRow |= t.text == aExpected;
                    bRow |= t.text.StartsWith(bName + ", surface | ", StringComparison.Ordinal) && t.text.EndsWith("| preferred", StringComparison.Ordinal);
                }
            string detail = $"groups={(list != null ? list.OreGroupCount : -1)} heading={heading} expected '{aExpected}' rows=[{string.Join("; ", texts)}]";
            if (heading && aRow && bRow) ctx.Pass("ores", detail);
            else ctx.Fail("ores", detail);
            var scroll = ModWindow.Content != null ? ModWindow.Content.GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
            if (scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 0f;
            }
            yield return ctx.Screenshot("window-contacts-ores");
            ModWindow.Close();

            // A preferred-ore asteroid far below the ship gets a marker on the bottom edge; the other ore gets none.
            var far = SelfCheckContext.SpawnAsteroid(at + new Vector2(0f, -400f), b);
            var farPlain = SelfCheckContext.SpawnAsteroid(at + new Vector2(400f, 0f), a);
            try
            {
                OrePreference.OrePreference.ForgetScene();
                yield return new WaitForSeconds(1.2f);
                Asteroid farA = null, plainA = null;
                foreach (var x in BasePoiManager.current.GetComponentsInChildren<Asteroid>())
                {
                    if (x.asteroidData == far) farA = x;
                    if (x.asteroidData == farPlain) plainA = x;
                }
                bool marked = farA != null && ContactMarkers.Instance != null && ContactMarkers.Instance.Marks(farA, out var anchor) && anchor.y < 0.01f;
                bool plainMarked = plainA != null && ContactMarkers.Instance != null && ContactMarkers.Instance.Marks(plainA, out _);
                string md = $"preferred far asteroid found={(farA != null)} marked at bottom={marked}; other ore marked={plainMarked}; markers shown={ContactMarkers.Instance?.ShownCount}";
                if (marked && !plainMarked) ctx.Pass("ore-markers", md);
                else ctx.Fail("ore-markers", md);
            }
            finally
            {
                SelfCheckContext.RemoveAsteroid(far);
                SelfCheckContext.RemoveAsteroid(farPlain);
            }
        }
        finally
        {
            ModWindow.Close();
            OrePreference.OrePreference.Set(mode0, list0);
            foreach (var d in spawned) SelfCheckContext.RemoveAsteroid(d);
        }
    }

    // Loot dropped far above the ship gets a marker pinned to the top edge of the screen.
    private static IEnumerator CheckMarkers(SelfCheckContext ctx)
    {
        yield return SelfCheckContext.EnsureInSpace();
        var ship = GameplayManager.Instance?.spaceShip;
        if (ContactMarkers.Instance == null || ship == null || BasePoiManager.current == null || !Singleton<LootManager>.HasInstance)
        {
            ctx.Skip("markers", "no markers or not in space");
            yield break;
        }
        InventoryItemType ore = null;
        foreach (var t in InventoryItemType.all)
            if (t.itemCategory == ItemCategory.Ore) { ore = t; break; }
        var data = new TractorableItemData
        {
            itemType = ore,
            itemAmount = 3,
            jettisoned = true,
            ownerFaction = Faction.player,
            position = (Vector2)ship.transform.position + new Vector2(0f, 400f),
        };
        var item = Singleton<LootManager>.Instance.SpawnLootItem(data, null);
        bool on = MarkLootOn();
        yield return new WaitForSeconds(1f);
        bool marked = ContactMarkers.Instance.Marks(item, out var anchor);
        string detail = $"marked={marked} anchor=({anchor.x:0.00},{anchor.y:0.00}) shown={ContactMarkers.Instance.ShownCount}";
        if (marked && anchor.y > 0.99f) ctx.Pass("markers", detail);
        else ctx.Fail("markers", detail + $" markLoot={on}");
        yield return ctx.Screenshot("contact-markers");
        Object.DestroyImmediate(item.gameObject);
        PruneLoot();
    }

    private static bool MarkLootOn() => Instance.MarkLoot.Value;

    private static void CheckLootRegistry(SelfCheckContext ctx)
    {
        var ship = GameplayManager.Instance?.spaceShip;
        if (BasePoiManager.current == null || !Singleton<LootManager>.HasInstance || ship == null)
        {
            ctx.Skip("loot-registry", "not in space");
            return;
        }
        InventoryItemType ore = null;
        foreach (var t in InventoryItemType.all)
            if (t.itemCategory == ItemCategory.Ore) { ore = t; break; }
        if (ore == null)
        {
            ctx.Skip("loot-registry", "no ore item type");
            return;
        }
        var data = new TractorableItemData
        {
            itemType = ore,
            itemAmount = 3,
            jettisoned = true,
            ownerFaction = Faction.player,
            position = (Vector2)ship.transform.position + new Vector2(0f, 60f),
        };
        var item = Singleton<LootManager>.Instance.SpawnLootItem(data, null);
        bool registered = Loot.Contains(item);
        Object.DestroyImmediate(item.gameObject);
        int after = PruneLoot();
        bool pruned = !Loot.Contains(item);
        string detail = $"registered={registered} pruned={pruned} loot={after}";
        if (registered && pruned) ctx.Pass("loot-registry", detail);
        else ctx.Fail("loot-registry", detail);
    }
}

[HarmonyPatch(typeof(LootManager), nameof(LootManager.SpawnLootItem))]
internal static class LootSpawnPatch
{
    private static void Postfix(TractorableItem __result)
    {
        if (!Contacts.Instance.Active) return;
        try
        {
            if (__result != null) Contacts.Loot.Add(__result);
        }
        catch (Exception e) { Contacts.Instance.Fail(e, nameof(LootSpawnPatch)); }
    }
}

// Rows are rebuilt only when the set of contacts changes; texts and distances refresh once per second.
public sealed class ContactsList : MonoBehaviour
{
    private sealed class LootGroup
    {
        public InventoryItemType type;
        public int total, pieces;
        public float nearest;
    }

    // Asteroids by visible ore and layer; the core only once the game's tooltip names it (Basic Scanning).
    private sealed class OreGroup
    {
        public string key;
        public OreItemData ore;
        public bool core;
        public int asteroids, amount;
        public float nearest;
        public Asteroid closest;
    }

    private enum Kind { Boardable, Wreck, Loot, Ore }

    private sealed class Row
    {
        public Kind kind;
        public object key;
        public TMP_Text main, dist, value;
    }

    private readonly List<BoardableUnit> boardables = new();
    private readonly List<SalvageContainer> wrecks = new();
    private readonly Dictionary<InventoryItemType, LootGroup> groups = new();
    private readonly List<LootGroup> groupList = new();
    private readonly Dictionary<string, OreGroup> oreGroups = new();
    private readonly List<OreGroup> oreList = new();
    private int asteroidsHere, scanLevel;
    private bool oreSection;
    private readonly Dictionary<object, float> distance = new();
    private readonly HashSet<object> keys = new();
    private readonly List<Row> rows = new();
    private Comparison<BoardableUnit> byBoardDistance;
    private Comparison<SalvageContainer> byWreckDistance;
    private Comparison<LootGroup> byLootDistance;
    private Comparison<OreGroup> byOreDistance;
    private TractorModule tractor;
    private RectTransform column;
    private float nextRefresh;

    public int BoardableCount => boardables.Count;
    public int WreckCount => wrecks.Count;
    public int LootGroupCount => groupList.Count;
    public int OreGroupCount => oreList.Count;

    private void Awake()
    {
        column = (RectTransform)transform;
        byBoardDistance = (a, b) => distance[a].CompareTo(distance[b]);
        byWreckDistance = (a, b) => distance[a].CompareTo(distance[b]);
        byLootDistance = (a, b) => a.nearest.CompareTo(b.nearest);
        byOreDistance = (a, b) => a.nearest.CompareTo(b.nearest);
    }

    private void Start()
    {
        Collect();
        Rebuild();
        nextRefresh = Time.unscaledTime + 1f;
    }

    private void Update()
    {
        if (!Contacts.Instance.Active || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 1f;
        try
        {
            Collect();
            if (Changed()) Rebuild();
            else Refresh();
        }
        catch (Exception e) { Contacts.Instance.Fail(e, nameof(ContactsList)); }
    }

    private void Collect()
    {
        var ship = GameplayManager.Instance?.spaceShip;
        bool haveShip = ship != null;
        Vector2 from = haveShip ? (Vector2)ship.transform.position : Vector2.zero;
        distance.Clear();

        boardables.Clear();
        foreach (var b in BoardableUnit.all)
        {
            if (b == null || !b.gameObject.activeInHierarchy) continue;
            boardables.Add(b);
            distance[b] = haveShip ? Vector2.Distance(from, b.transform.position) : float.MaxValue;
        }

        wrecks.Clear();
        foreach (var c in SalvageContainer.all)
        {
            if (c == null || !c.gameObject.activeInHierarchy) continue;
            wrecks.Add(c);
            distance[c] = haveShip ? Vector2.Distance(from, c.transform.position) : float.MaxValue;
        }

        groups.Clear();
        groupList.Clear();
        Contacts.PruneLoot();
        foreach (var item in Contacts.Loot)
        {
            var data = item.data;
            if (data?.itemType == null) continue;
            if (!groups.TryGetValue(data.itemType, out var g))
            {
                g = new LootGroup { type = data.itemType, nearest = float.MaxValue };
                groups[data.itemType] = g;
                groupList.Add(g);
            }
            g.total += data.itemAmount;
            g.pieces++;
            if (haveShip) g.nearest = Mathf.Min(g.nearest, Vector2.Distance(from, item.transform.position));
        }
        foreach (var g in groupList) distance[g.type] = g.nearest;

        oreGroups.Clear();
        oreList.Clear();
        asteroidsHere = 0;
        scanLevel = Asteroid.GetTooltipScanLevel();
        var poiManager = BasePoiManager.current;
        if (poiManager != null)
            foreach (var a in poiManager.GetComponentsInChildren<Asteroid>())
            {
                var data = a.asteroidData;
                if (data == null || data.isVisualOnly) continue;
                asteroidsHere++;
                float d = haveShip ? Vector2.Distance(from, a.transform.position) : float.MaxValue;
                if (data.surfaceAmount > 0 && data.surfaceItem != null) AddOre(data.surfaceItem, false, data.surfaceAmount, a, d);
                if (scanLevel > 0 && data.innerCoreAmount > 0 && data.innerCoreItem != null) AddOre(data.innerCoreItem, true, data.innerCoreAmount, a, d);
            }
        foreach (var g in oreList) distance[g.key] = g.nearest;
    }

    private void AddOre(OreItemData ore, bool core, int amount, Asteroid a, float d)
    {
        string key = ore.item.identifier + (core ? "|core" : "|surface");
        if (!oreGroups.TryGetValue(key, out var g))
        {
            g = new OreGroup { key = key, ore = ore, core = core, nearest = float.MaxValue };
            oreGroups[key] = g;
            oreList.Add(g);
        }
        g.asteroids++;
        g.amount += amount;
        if (d < g.nearest || g.closest == null)
        {
            g.nearest = d;
            g.closest = a;
        }
    }

    private bool Changed()
    {
        if (keys.Count != boardables.Count + wrecks.Count + groupList.Count + oreList.Count) return true;
        if (oreSection != asteroidsHere > 0) return true;
        foreach (var b in boardables) if (!keys.Contains(b)) return true;
        foreach (var c in wrecks) if (!keys.Contains(c)) return true;
        foreach (var g in groupList) if (!keys.Contains(g.type)) return true;
        foreach (var g in oreList) if (!keys.Contains(g.key)) return true;
        return false;
    }

    private void Rebuild()
    {
        for (int i = column.childCount - 1; i >= 0; i--)
        {
            var child = column.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        rows.Clear();
        keys.Clear();
        boardables.Sort(byBoardDistance);
        wrecks.Sort(byWreckDistance);
        groupList.Sort(byLootDistance);
        var ship = GameplayManager.Instance?.spaceShip;
        tractor = ship != null ? ship.GetModule<TractorModule>() : null;

        Kit.Heading(column, "Boardable ships (" + boardables.Count + ")");
        foreach (var b in boardables)
        {
            var unit = b;
            keys.Add(b);
            var row = Kit.Row(column, 6f, 22f);
            var r = new Row { kind = Kind.Boardable, key = b, main = Kit.Label(row, "") };
            r.dist = Kit.Fixed(row, "", 50f, TextAlignmentOptions.MidlineRight, ColorHelper.offWhite);
            Kit.TextButton(row, "Board", () => Contacts.Board(unit), 70f);
            rows.Add(r);
        }
        if (boardables.Count == 0) Kit.Muted(column, "None");

        Kit.Heading(column, "Wrecks (" + wrecks.Count + ")");
        foreach (var c in wrecks)
        {
            var wreck = c;
            keys.Add(c);
            var row = Kit.Row(column, 6f, 22f);
            var r = new Row { kind = Kind.Wreck, key = c, main = Kit.Label(row, "") };
            r.dist = Kit.Fixed(row, "", 50f, TextAlignmentOptions.MidlineRight, ColorHelper.offWhite);
            Kit.TextButton(row, "Salvage", () => Contacts.Salvage(wreck), 70f);
            rows.Add(r);
        }
        if (wrecks.Count == 0) Kit.Muted(column, "None");

        Kit.Heading(column, "Loot (" + groupList.Count + " kinds)");
        foreach (var g in groupList)
        {
            var type = g.type;
            keys.Add(type);
            var row = Kit.Row(column, 6f, 22f);
            var r = new Row { kind = Kind.Loot, key = type, main = Kit.Label(row, "") };
            r.main.color = type.rarity.GetColor();
            Kit.ItemTooltip(r.main, type);
            r.value = Kit.Fixed(row, "", 70f, TextAlignmentOptions.MidlineRight, ColorHelper.poiTooltipMuted);
            r.dist = Kit.Fixed(row, "", 50f, TextAlignmentOptions.MidlineRight, ColorHelper.offWhite);
            if (tractor != null) Kit.TextButton(row, "Tractor", () => Contacts.Tractor(type), 70f);
            rows.Add(r);
        }
        if (groupList.Count == 0) Kit.Muted(column, "None");

        // Only where there are asteroids, so stations and combat sites don't get an empty section.
        oreSection = asteroidsHere > 0;
        if (oreSection)
        {
            oreList.Sort(byOreDistance);
            Kit.Heading(column, "Asteroid ores (" + asteroidsHere + " asteroids)");
            foreach (var g in oreList)
            {
                string key = g.key;
                keys.Add(key);
                var row = Kit.Row(column, 6f, 22f);
                var r = new Row { kind = Kind.Ore, key = key, main = Kit.Label(row, "") };
                r.main.color = g.ore.item.rarity.GetColor();
                Kit.Tooltip(r.main, Translation.Translate(g.ore.item.displayName), g.ore.GetDescription());
                r.value = Kit.Fixed(row, "", 70f, TextAlignmentOptions.MidlineRight, ColorHelper.poiTooltipMuted);
                r.dist = Kit.Fixed(row, "", 50f, TextAlignmentOptions.MidlineRight, ColorHelper.offWhite);
                Kit.TextButton(row, "Mine", () => { if (oreGroups.TryGetValue(key, out var now) && now.closest != null) Contacts.Mine(now.closest); }, 70f);
                rows.Add(r);
            }
            if (oreList.Count == 0) Kit.Muted(column, "All mined out");
            Kit.Muted(column, scanLevel == 0 ? "Core ores show up here with the Basic Scanning mining skill, ore amounts with Advanced Scanning, as on the asteroid tooltips."
                : scanLevel == 1 ? "Ore amounts show up here with the Advanced Scanning mining skill, as on the asteroid tooltips." : "Amounts are ore units left in those asteroids.");
        }

        Refresh();
    }

    private void Refresh()
    {
        foreach (var r in rows)
        {
            string main, value = null;
            switch (r.kind)
            {
                case Kind.Boardable: main = BoardableText((BoardableUnit)r.key); break;
                case Kind.Wreck: main = WreckText((SalvageContainer)r.key); break;
                case Kind.Ore:
                {
                    var g = oreGroups[(string)r.key];
                    main = Translation.Translate(g.ore.item.displayName) + (g.core ? ", core" : ", surface") + " | " + g.asteroids + (g.asteroids == 1 ? " asteroid" : " asteroids")
                           + (OrePreference.OrePreference.Instance.Active && OrePreference.OrePreference.Ores.Contains(g.ore.item.identifier) ? " | preferred" : "");
                    value = scanLevel == 2 ? GameMath.FormatNumber(g.amount) : "";
                    break;
                }
                default:
                {
                    var g = groups[(InventoryItemType)r.key];
                    main = Translation.Translate(g.type.displayName) + " x" + g.total + (g.pieces > 1 ? " (" + g.pieces + " stacks)" : "");
                    value = GameMath.FormatNumber(g.type.sellValue * g.total) + " cr";
                    break;
                }
            }
            if (r.main.text != main) r.main.text = main;
            if (r.value != null && r.value.text != value) r.value.text = value;
            float d = distance.TryGetValue(r.key, out var v) ? v : float.MaxValue;
            string dist = d == float.MaxValue ? "-" : d.ToString("0");
            if (r.dist.text != dist) r.dist.text = dist;
        }
    }

    // The game's pixel font has no middle dot, so the separators are plain ASCII.
    private static string BoardableText(BoardableUnit b)
    {
        string text = b.ship != null ? b.ship.displayName : b.data?.shipTemplate;
        var data = b.data;
        if (data == null) return text;
        if (data.faction != null) text += " | " + data.faction.name;
        var sim = b.simulation;
        Behaviour.Dungeon.DungeonOperation op;
        if (sim != null && !sim.isComplete) text += " | boarding in progress";
        else if (Singleton<DungeonManager>.HasInstance && (op = Singleton<DungeonManager>.Instance.GetOperation(b)) != null) text += " | " + op.phase;
        else if (data.isFullyDepleted) text += " | depleted";
        return text;
    }

    private static string WreckText(SalvageContainer c)
    {
        var data = c.data;
        string parts = "";
        if (data != null)
        {
            if (data.HasItems()) parts = "items";
            if (data.HasSalvage(TargetLayer.Surface)) parts += (parts.Length > 0 ? ", " : "") + "surface";
            if (data.HasSalvage(TargetLayer.Core)) parts += (parts.Length > 0 ? ", " : "") + "core";
        }
        return c.displayName + " | " + (parts.Length > 0 ? parts : "empty");
    }
}
