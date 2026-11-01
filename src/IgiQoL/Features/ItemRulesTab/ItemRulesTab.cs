using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Item;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.Rules;
using IgiQoL.UI;
using Source.Item;
using Source.Util;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.ItemRulesTab;

public sealed class ItemRulesTab : Feature
{
    public static ItemRulesTab Instance;

    public ItemRulesTab() { Instance = this; }

    public override string Id => "ItemRules";
    public override string Title => "Item rules tab";
    public override string Description => "The Item rules tab: per-item, per-category and default rules for loot pickup limits and for what ECHO does with cargo at a station.";
    public override Type[] PatchClasses => Type.EmptyTypes;

    public static readonly ItemCategory[] Categories = BuildCategories();
    private static readonly Rarity[] Rarities = (Rarity[])Enum.GetValues(typeof(Rarity));

    private static string search = "";
    private static RectTransform results;
    private static Dictionary<string, InventoryItemType> byKey;

    public override void OnActivated() => ModWindow.AddTab("itemrules", "Item rules", 15, Build);

    private static ItemCategory[] BuildCategories()
    {
        var list = new List<ItemCategory>();
        foreach (ItemCategory c in Enum.GetValues(typeof(ItemCategory)))
            if (c != ItemCategory.Empty && c != ItemCategory.UnusedMissionItem) list.Add(c);
        return list.ToArray();
    }

    private static Dictionary<string, InventoryItemType> ByKey()
    {
        if (byKey != null) return byKey;
        byKey = new Dictionary<string, InventoryItemType>();
        foreach (var t in InventoryItemType.all) byKey[Items.RuleKey(t)] = t;
        return byKey;
    }

    private static string CategoryName(ItemCategory c) => SettingRows.Pretty(c.ToString());

    private static string ItemName(InventoryItemType t) =>
        Translation.Translate(t.displayName) + (t.itemLevel > 0 ? " L" + t.itemLevel : "");

    private static string UpToName(Rarity r) => r.GetDisplayName() + " and below";

    private static void Rebuild() => ModWindow.Show("itemrules");

    private static void Build(RectTransform content)
    {
        var muted = ColorHelper.poiTooltipMuted;
        Kit.Heading(content, "Item rules");
        Kit.Muted(content, "The most specific rule wins: a specific item, then category + rarity, then category + rarity and below, then category, then any category + rarity and below, then the default. Pickup limit: your ships stop auto-tractoring the item once they carry this many (0 = no limit, -1 = never). At dock: what ECHO does with the item when it unloads at a station. Rules are saved with this game.");

        var header = Kit.Row(content, 6f, 22f);
        Kit.Label(header, "Rule");
        Kit.Tooltip(Kit.Fixed(header, "Pickup limit", 96f, TextAlignmentOptions.Center, muted), "Pickup limit",
            "Stop auto-tractoring once a ship carries this many. 0 = no limit. -1 = never auto-tractor it (items your missions need and ammo the ship fires are still picked up; clicking an item yourself always works). Needs 'Loot pickup limits' on.");
        Kit.Tooltip(Kit.Fixed(header, "At dock", 130f, TextAlignmentOptions.Center, muted), "At dock",
            "Game default: deposited in the station's storage as usual. Keep aboard: never unloaded. Sell: sold at the station shop when ECHO unloads. Sell above N: deposited until the storage holds N, the rest is sold. Data items sit in your data inventory, not cargo: Sell and Sell above N sell them from there when the rule names the item or its category ('Sell from the data inventory'). Favourite stacks, ammo the flagship uses and the usable items the game keeps aboard are never touched. Needs 'ECHO cargo rules' on.");
        Kit.Tooltip(Kit.Fixed(header, "N", 60f, TextAlignmentOptions.Center, muted), "N", "For Sell above N: the amount to keep in storage.");
        Kit.Fixed(header, "", 22f, TextAlignmentOptions.Center, muted);

        Kit.Heading(content, "Default");
        RuleRow(content, "Everything without a rule of its own", null, ItemRules.Default, null);

        Kit.Heading(content, "Categories");
        var freeCategories = new List<ItemCategory>();
        var freeNames = new List<string>();
        foreach (var cat in Categories)
        {
            if (ItemRules.ByCategory.TryGetValue(cat, out var rule))
            {
                var c = cat;
                RuleRow(content, CategoryName(cat), null, rule, () => { ItemRules.Remove(c); ItemRules.Write(); Rebuild(); });
            }
            else
            {
                freeCategories.Add(cat);
                freeNames.Add(CategoryName(cat));
            }
        }
        if (freeCategories.Count == 0) Kit.Muted(content, "Every category has a rule.");
        else
        {
            var row = Kit.Row(content, 6f, 26f);
            Kit.Label(row, "Add a category rule");
            var choice = Kit.Dropdown(row, freeNames, 0, _ => { }, 180f);
            Kit.TextButton(row, "Add", () =>
            {
                int pick = choice.GetIndex();
                if (pick < 0 || pick >= freeCategories.Count) return;
                ItemRules.Edit(freeCategories[pick]);
                ItemRules.Write();
                Rebuild();
            }, 60f);
        }

        Kit.Heading(content, "Category and rarity");
        var pairs = new List<(ItemCategory, Rarity)>(ItemRules.ByCategoryRarity.Keys);
        pairs.Sort((a, b) =>
        {
            int c = Array.IndexOf(Categories, a.Item1).CompareTo(Array.IndexOf(Categories, b.Item1));
            return c != 0 ? c : a.Item2.CompareTo(b.Item2);
        });
        foreach (var pair in pairs)
        {
            var p = pair;
            RuleRow(content, CategoryName(p.Item1) + " / " + p.Item2.GetDisplayName(), p.Item2.GetColor(), ItemRules.ByCategoryRarity[p],
                () => { ItemRules.Remove(p.Item1, p.Item2); ItemRules.Write(); Rebuild(); });
        }
        {
            var row = Kit.Row(content, 6f, 26f);
            Kit.Label(row, "Add a category + rarity rule");
            var allNames = new List<string>();
            foreach (var cat in Categories) allNames.Add(CategoryName(cat));
            var rarityNames = new List<string>();
            foreach (var r in Rarities) rarityNames.Add(r.GetDisplayName());
            var catChoice = Kit.Dropdown(row, allNames, 0, _ => { }, 150f);
            var rarityChoice = Kit.Dropdown(row, rarityNames, 0, _ => { }, 110f);
            Kit.TextButton(row, "Add", () =>
            {
                int ci = catChoice.GetIndex(), ri = rarityChoice.GetIndex();
                if (ci < 0 || ci >= Categories.Length || ri < 0 || ri >= Rarities.Length) return;
                ItemRules.Edit(Categories[ci], Rarities[ri]);
                ItemRules.Write();
                Rebuild();
            }, 60f);
        }

        Kit.Heading(content, "Rarity and below");
        var rarityUps = new List<Rarity>(ItemRules.ByRarityUpTo.Keys);
        rarityUps.Sort();
        foreach (var rarity in rarityUps)
        {
            var r = rarity;
            RuleRow(content, "Any category / " + UpToName(r), r.GetColor(), ItemRules.ByRarityUpTo[r],
                () => { ItemRules.RemoveUpTo(r); ItemRules.Write(); Rebuild(); });
        }
        var ups = new List<(ItemCategory, Rarity)>(ItemRules.ByCategoryUpTo.Keys);
        ups.Sort((a, b) =>
        {
            int c = Array.IndexOf(Categories, a.Item1).CompareTo(Array.IndexOf(Categories, b.Item1));
            return c != 0 ? c : a.Item2.CompareTo(b.Item2);
        });
        foreach (var pair in ups)
        {
            var p = pair;
            RuleRow(content, CategoryName(p.Item1) + " / " + UpToName(p.Item2), p.Item2.GetColor(), ItemRules.ByCategoryUpTo[p],
                () => { ItemRules.RemoveUpTo(p.Item1, p.Item2); ItemRules.Write(); Rebuild(); });
        }
        {
            var row = Kit.Row(content, 6f, 26f);
            Kit.Tooltip(Kit.Label(row, "Add a rarity-and-below rule"), "Rarity and below",
                "Covers the chosen rarity and every rarity under it. When several cover an item, the lowest rarity wins, so 'Standard and below: Sell' beats 'Exotic and below: Keep aboard' for a Standard item. A category's own rules win over 'Any category'.");
            var catNames = new List<string> { "Any category" };
            foreach (var cat in Categories) catNames.Add(CategoryName(cat));
            var rarityNames = new List<string>();
            foreach (var r in Rarities) rarityNames.Add(UpToName(r));
            var catChoice = Kit.Dropdown(row, catNames, 0, _ => { }, 150f);
            var rarityChoice = Kit.Dropdown(row, rarityNames, 0, _ => { }, 150f);
            Kit.TextButton(row, "Add", () =>
            {
                int ci = catChoice.GetIndex(), ri = rarityChoice.GetIndex();
                if (ci < 0 || ci > Categories.Length || ri < 0 || ri >= Rarities.Length) return;
                if (ci == 0) ItemRules.EditUpTo(Rarities[ri]);
                else ItemRules.EditUpTo(Categories[ci - 1], Rarities[ri]);
                ItemRules.Write();
                Rebuild();
            }, 60f);
        }

        Kit.Heading(content, "Items");
        var keys = new List<string>(ItemRules.ByItem.Keys);
        keys.Sort(StringComparer.Ordinal);
        var known = ByKey();
        foreach (var key in keys)
        {
            var k = key;
            Action remove = () => { ItemRules.Remove(k); ItemRules.Write(); Rebuild(); };
            if (known.TryGetValue(key, out var type))
                RuleRow(content, ItemName(type), type.rarity.GetColor(), ItemRules.ByItem[key], remove, type);
            else
                RuleRow(content, key + " (not in this game version)", null, ItemRules.ByItem[key], remove);
        }
        {
            var row = Kit.Row(content, 6f, 26f);
            Kit.Label(row, "Add an item rule");
            var field = Kit.Search(row, "Type part of an item name", RunSearch, 300f);
            field.SetTextWithoutNotify(search);
        }
        results = Kit.Column(content, 2f);
        if (search.Trim().Length >= 2) RunSearch(search);

        var caps = TractorCaps.TractorCaps.Instance;
        SettingRows.AddSection(content, caps);
        if (caps.Active) SettingRows.AddEntries(content, caps.Id, SettingRows.Rows.Options);
        else Kit.Muted(content, "Pickup limits do nothing while 'Loot pickup limits' is off (Settings tab).");

        var cargo = EchoCargo.EchoCargo.Instance;
        SettingRows.AddSection(content, cargo);
        if (cargo.Active) SettingRows.AddEntries(content, cargo.Id, SettingRows.Rows.Options);
        else Kit.Muted(content, "Turned off on the Settings tab.");
    }

    private static void RunSearch(string text)
    {
        search = text ?? "";
        if (results == null) return;
        for (int i = results.childCount - 1; i >= 0; i--)
        {
            var child = results.GetChild(i).gameObject;
            child.SetActive(false);
            Object.Destroy(child);
        }
        string needle = search.Trim();
        if (needle.Length < 2) return;

        var hits = new List<(string name, InventoryItemType type)>();
        foreach (var t in InventoryItemType.all)
        {
            string name = ItemName(t);
            if (name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (ItemRules.ByItem.ContainsKey(Items.RuleKey(t))) continue;
            hits.Add((name, t));
        }
        hits.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        int shown = Math.Min(20, hits.Count);
        for (int i = 0; i < shown; i++)
        {
            var type = hits[i].type;
            string key = Items.RuleKey(type);
            var row = Kit.Row(results, 6f, 22f);
            var label = Kit.Label(row, hits[i].name);
            label.color = type.rarity.GetColor();
            Kit.ItemTooltip(label, type);
            Kit.Fixed(row, CategoryName(type.itemCategory), 120f, TextAlignmentOptions.MidlineLeft, ColorHelper.poiTooltipMuted);
            Kit.TextButton(row, "Add", () => { ItemRules.Edit(key); ItemRules.Write(); Rebuild(); }, 60f);
        }
        if (hits.Count > 20) Kit.Muted(results, "Showing 20 of " + hits.Count + "; type more of the name.");
        else if (hits.Count == 0) Kit.Muted(results, "No item without a rule matches.");
    }

    private static void RuleRow(RectTransform parent, string label, Color? colour, ItemRule rule, Action remove, InventoryItemType tooltipItem = null)
    {
        var row = Kit.Row(parent, 6f, 26f);
        var text = Kit.Label(row, label);
        if (colour.HasValue) text.color = colour.Value;
        if (tooltipItem != null) Kit.ItemTooltip(text, tooltipItem);

        TMP_InputField cap = null;
        cap = Kit.Input(row, rule.tractorCap.ToString(), TMP_InputField.ContentType.IntegerNumber, v =>
        {
            if (int.TryParse(v, out var n)) rule.tractorCap = Math.Max(ItemRule.Never, n);
            cap.SetTextWithoutNotify(rule.tractorCap.ToString());
            ItemRules.Write();
        }, 96f);

        TMP_InputField keep = null;
        Kit.Dropdown(row, ItemRules.DockActionNames, (int)rule.dockAction, i =>
        {
            rule.dockAction = (DockAction)i;
            SyncKeep(keep, rule);
            ItemRules.Write();
        }, 130f);
        keep = Kit.Input(row, "", TMP_InputField.ContentType.IntegerNumber, v =>
        {
            if (int.TryParse(v, out var n)) rule.keepAmount = Math.Max(0, n);
            SyncKeep(keep, rule);
            ItemRules.Write();
        }, 60f);
        SyncKeep(keep, rule);

        if (remove != null) Kit.Tooltip(Kit.TextTile(row, "x", 22f, () => remove()).targetGraphic, null, "Remove this rule");
        else Kit.Fixed(row, "", 22f, TextAlignmentOptions.Center, ColorHelper.offWhite);
    }

    // The N field stays in every row so the columns line up; it only takes input for Sell above N.
    private static void SyncKeep(TMP_InputField keep, ItemRule rule)
    {
        bool on = rule.dockAction == DockAction.SellAbove;
        keep.interactable = on;
        keep.SetTextWithoutNotify(on ? rule.keepAmount.ToString() : "");
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        bool had = ItemRules.ByCategory.TryGetValue(ItemCategory.Ore, out var prev);
        int prevCap = had ? prev.tractorCap : 0, prevKeep = had ? prev.keepAmount : 0;
        var prevAction = had ? prev.dockAction : DockAction.Default;
        ctx.Cleanup(() =>
        {
            if (had)
            {
                var e = ItemRules.Edit(ItemCategory.Ore);
                e.tractorCap = prevCap;
                e.dockAction = prevAction;
                e.keepAmount = prevKeep;
            }
            else ItemRules.Remove(ItemCategory.Ore);
            ItemRules.Write();
        });

        var r = ItemRules.Edit(ItemCategory.Ore);
        r.tractorCap = 5;
        r.dockAction = DockAction.SellAbove;
        r.keepAmount = 10;
        ItemRules.Write();
        ItemRules.Load();

        InventoryItemType ore = null;
        foreach (var t in InventoryItemType.all)
            if (t.itemCategory == ItemCategory.Ore) { ore = t; break; }
        if (ore == null) ctx.Skip("roundtrip", "no ore item type");
        else
        {
            var got = ItemRules.Resolve(ore);
            var record = SaveData.Feature(ItemRules.FeatureKey, false);
            int stored = record != null ? record["categories"]["Ore"]["keepAmount"].AsInteger : -1;
            string detail = $"{Items.RuleKey(ore)} -> {got.tractorCap}/{got.dockAction}/{got.keepAmount} hasDockRules={ItemRules.HasDockRules} record keepAmount={stored}";
            if (got.tractorCap == 5 && got.dockAction == DockAction.SellAbove && got.keepAmount == 10 && ItemRules.HasDockRules && stored == 10)
                ctx.Pass("roundtrip", detail);
            else ctx.Fail("roundtrip", detail);
        }

        // Per-level rules resolve by (identifier, level) and follow Edit/Remove; the unlevelled type's rule is the fallback.
        // CreateLeveled refuses prefabs whose parts keep per-item values on the shared prefab (ores among them).
        InventoryItemType levelBase = null;
        foreach (var t in InventoryItemType.all)
            if (t.itemLevel == 0 && t.identifier != null && t.definition != null
                && Array.TrueForAll(t.definition.GetComponents<InventoryItemPart>(), p => p.usesItemState)) { levelBase = t; break; }
        if (levelBase == null) ctx.Skip("resolve-level", "no item type the game can level");
        else
        {
            var leveled = InventoryItemType.CreateLeveled(levelBase, 3);
            string levelKey = Items.RuleKey(leveled);
            int baseCap = ItemRules.Resolve(levelBase).tractorCap;
            try
            {
                ItemRules.Edit(levelKey).tractorCap = baseCap + 2;
                bool hit = ItemRules.Resolve(leveled).tractorCap == baseCap + 2;
                bool otherLevel = ItemRules.Resolve(InventoryItemType.CreateLeveled(levelBase, 4)).tractorCap == baseCap;
                ItemRules.Remove(levelKey);
                bool gone = ItemRules.Resolve(leveled).tractorCap == baseCap;
                string levelDetail = $"{levelKey}: baseCap={baseCap} hit={hit} otherLevel={otherLevel} afterRemove={gone}";
                if (hit && otherLevel && gone) ctx.Pass("resolve-level", levelDetail);
                else ctx.Fail("resolve-level", levelDetail);
            }
            finally { ItemRules.Remove(levelKey); }
        }

        // Rarity-and-below rules: placement in the resolve order, lowest covering rarity wins, and the save round trip.
        InventoryItemType mid = null;
        foreach (var t in InventoryItemType.all)
            if (t.identifier != null && t.rarity > Rarity.Standard && t.rarity < Rarity.Legendary) { mid = t; break; }
        if (mid == null) ctx.Skip("resolve-upto", "no item type between Standard and Legendary");
        else
        {
            string c = mid.itemCategory.ToString(), rar = mid.rarity.ToString(), lo = (mid.rarity - 1).ToString(), hi = (mid.rarity + 1).ToString();
            string Cap(int n) => "{\"tractorCap\":" + n + "}";
            string Rules(string body) => "{\"default\":" + Cap(1) + "," + body + "}";
            var cases = new (string name, string json, int want)[]
            {
                ("lowest-wins", Rules($"\"categoryUpTo\":{{\"{c}/{rar}\":{Cap(4)},\"{c}/{hi}\":{Cap(5)}}}"), 4),
                ("covers-above", Rules($"\"categoryUpTo\":{{\"{c}/{hi}\":{Cap(5)}}}"), 5),
                ("not-below", Rules($"\"categoryUpTo\":{{\"{c}/{lo}\":{Cap(5)}}},\"rarityUpTo\":{{\"{lo}\":{Cap(6)}}}"), 1),
                ("exact-first", Rules($"\"categoryRarity\":{{\"{c}/{rar}\":{Cap(3)}}},\"categoryUpTo\":{{\"{c}/{rar}\":{Cap(4)}}}"), 3),
                ("before-category", Rules($"\"categories\":{{\"{c}\":{Cap(2)}}},\"categoryUpTo\":{{\"{c}/{rar}\":{Cap(4)}}}"), 4),
                ("category-before-any", Rules($"\"categories\":{{\"{c}\":{Cap(2)}}},\"rarityUpTo\":{{\"Legendary\":{Cap(6)}}}"), 2),
                ("any", Rules($"\"rarityUpTo\":{{\"{rar}\":{Cap(6)},\"Legendary\":{Cap(7)}}}"), 6),
            };
            var bad = new List<string>();
            bool dock;
            try
            {
                foreach (var (name, json, want) in cases)
                {
                    ItemRules.UseForTest(json);
                    int got = ItemRules.Resolve(mid).tractorCap;
                    if (got != want) bad.Add($"{name}: got {got}, want {want}");
                }
                ItemRules.UseForTest($"{{\"rarityUpTo\":{{\"{rar}\":{{\"dockAction\":\"Sell\"}}}}}}");
                dock = ItemRules.HasDockRules;
            }
            finally { ItemRules.Load(); }
            string upDetail = $"{Items.RuleKey(mid)} ({c}/{rar}): {cases.Length} cases, hasDockRules={dock}" + (bad.Count > 0 ? "; " + string.Join("; ", bad) : "");
            if (bad.Count == 0 && dock) ctx.Pass("resolve-upto", upDetail);
            else ctx.Fail("resolve-upto", upDetail);

            string snapshot = SaveData.Feature(ItemRules.FeatureKey, false)?.ToString() ?? "{}";
            try
            {
                ItemRules.EditUpTo(mid.itemCategory, mid.rarity).tractorCap = 7;
                ItemRules.EditUpTo(mid.rarity).dockAction = DockAction.Sell;
                ItemRules.Write();
                ItemRules.Load();
                var record = SaveData.Feature(ItemRules.FeatureKey, false);
                int storedCap = record != null ? record["categoryUpTo"][c + "/" + rar]["tractorCap"].AsInteger : -1;
                string storedAction = record != null ? record["rarityUpTo"][rar]["dockAction"].AsString : null;
                int resolved = ItemRules.Resolve(mid).tractorCap;
                string rtDetail = $"record categoryUpTo cap={storedCap} rarityUpTo action={storedAction} resolve cap={resolved}";
                if (storedCap == 7 && storedAction == "Sell" && resolved == 7) ctx.Pass("roundtrip-upto", rtDetail);
                else ctx.Fail("roundtrip-upto", rtDetail);
            }
            finally
            {
                ItemRules.UseForTest(snapshot);
                ItemRules.Write();
            }
        }

        ModWindow.Open("itemrules");
        ctx.Cleanup(ModWindow.Close);
        int rows = 0;
        if (ModWindow.IsOpen)
        {
            var content = ModWindow.Content;
            for (int i = 0; i < content.childCount; i++)
                if (content.GetChild(i).name == "Row") rows++;
        }
        string tabDetail = $"rows={rows} tab={ModWindow.CurrentTab}";
        if (ModWindow.CurrentTab == "itemrules" && rows >= 5) ctx.Pass("tab", tabDetail);
        else ctx.Fail("tab", tabDetail);
        yield return ctx.Screenshot("window-itemrules");
    }
}
