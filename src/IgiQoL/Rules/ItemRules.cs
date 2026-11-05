using System;
using System.Collections.Generic;
using System.IO;
using Behaviour.Item;
using BepInEx;
using IgiQoL.Core;
using IgiQoL.Game;
using LightJson;
using Source.Item;

namespace IgiQoL.Rules;

public enum DockAction { Default, Keep, Sell, SellAbove }

public sealed class ItemRule
{
    public int tractorCap;            // 0 = no limit, Never = not auto-tractored at all
    public const int Never = -1;
    public DockAction dockAction;     // Default = what the game does
    public int keepAmount;            // SellAbove: deposit until the destination storage holds this many, sell the rest
    public bool IsVanilla => tractorCap == 0 && dockAction == DockAction.Default;
}

// Per-save item rules (SaveData feature record "ItemRules"). A save without rules (no record, or an empty one) is seeded from item-rules.json on load.
public static class ItemRules
{
    public const string FeatureKey = "ItemRules";
    public static string ImportPath => Paths.ConfigPath + "/IgiQoL/item-rules.json";
    public static readonly string[] DockActionNames = { "Game default", "Keep aboard", "Sell", "Sell, keep up to" };

    public static ItemRule Default { get; private set; } = new();
    public static readonly Dictionary<ItemCategory, ItemRule> ByCategory = new();
    public static readonly Dictionary<(ItemCategory, Rarity), ItemRule> ByCategoryRarity = new();
    // "This rarity and below": the key's rarity is the highest one the rule covers. When several cover an item, the lowest wins.
    public static readonly Dictionary<(ItemCategory, Rarity), ItemRule> ByCategoryUpTo = new();
    public static readonly Dictionary<Rarity, ItemRule> ByRarityUpTo = new();   // any category
    // Key = Items.RuleKey(type): "identifier" or "identifier@level".
    public static readonly Dictionary<string, ItemRule> ByItem = new();
    private static readonly Dictionary<(string id, int level), ItemRule> ByItemLevel = new();   // per-level rules of ByItem by (identifier, level), so Resolve needs no key string
    public static bool HasDockRules { get; private set; }

    public static int Count => ByCategory.Count + ByCategoryRarity.Count + ByCategoryUpTo.Count + ByRarityUpTo.Count + ByItem.Count;

    public static ItemRule Resolve(InventoryItemType item)
    {
        var rule = Explicit(item);
        if (rule != null) return rule;
        if (ByRarityUpTo.Count > 0)
            for (var r = item.rarity; r <= Rarity.Legendary; r++)
                if (ByRarityUpTo.TryGetValue(r, out var anyRule)) return anyRule;
        return Default;
    }

    // The rule that names the item or its category; null when only an any-category rule or the default covers it.
    public static ItemRule Explicit(InventoryItemType item)
    {
        if (ByItem.Count > 0 && item.identifier != null)
        {
            if (item.itemLevel > 0 && ByItemLevel.Count > 0 && ByItemLevel.TryGetValue((item.identifier, item.itemLevel), out var levelRule)) return levelRule;
            if (ByItem.TryGetValue(item.identifier, out var idRule)) return idRule;
        }
        if (ByCategoryRarity.Count > 0 && ByCategoryRarity.TryGetValue((item.itemCategory, item.rarity), out var crRule)) return crRule;
        if (ByCategoryUpTo.Count > 0)
            for (var r = item.rarity; r <= Rarity.Legendary; r++)
                if (ByCategoryUpTo.TryGetValue((item.itemCategory, r), out var upRule)) return upRule;
        if (ByCategory.Count > 0 && ByCategory.TryGetValue(item.itemCategory, out var catRule)) return catRule;
        return null;
    }

    public static void Load()
    {
        try
        {
            var record = SaveData.Feature(FeatureKey, false);
            if (record != null && record.Count > 0)
            {
                Apply(record, "save");
                return;
            }
            Clear();
            if (!File.Exists(ImportPath)) return;
            try
            {
                Apply(JsonValue.Parse(File.ReadAllText(ImportPath)).AsJsonObject, ImportPath);
                if (Count > 0 || !Default.IsVanilla)
                {
                    Write();
                    Plugin.Log.LogInfo($"[ItemRules] imported {Count} rules from item-rules.json into this save");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[ItemRules] could not import {ImportPath}: {e.Message}");
                Clear();
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[ItemRules] could not load the rules: {e}");
            Clear();
        }
    }

    public static void Write()
    {
        var record = SaveData.Feature(FeatureKey, true);
        record.Clear();
        record["default"] = ToJson(Default);
        var cats = new JsonObject();
        foreach (var kv in ByCategory) cats[kv.Key.ToString()] = ToJson(kv.Value);
        record["categories"] = cats;
        var crs = new JsonObject();
        foreach (var kv in ByCategoryRarity) crs[kv.Key.Item1 + "/" + kv.Key.Item2] = ToJson(kv.Value);
        record["categoryRarity"] = crs;
        var cups = new JsonObject();
        foreach (var kv in ByCategoryUpTo) cups[kv.Key.Item1 + "/" + kv.Key.Item2] = ToJson(kv.Value);
        record["categoryUpTo"] = cups;
        var rups = new JsonObject();
        foreach (var kv in ByRarityUpTo) rups[kv.Key.ToString()] = ToJson(kv.Value);
        record["rarityUpTo"] = rups;
        var its = new JsonObject();
        foreach (var kv in ByItem) its[kv.Key] = ToJson(kv.Value);
        record["items"] = its;
        UpdateHasDockRules();
    }

    public static ItemRule Edit(ItemCategory c)
    {
        if (!ByCategory.TryGetValue(c, out var rule)) ByCategory[c] = rule = new ItemRule();
        return rule;
    }

    public static ItemRule Edit(ItemCategory c, Rarity r)
    {
        if (!ByCategoryRarity.TryGetValue((c, r), out var rule)) ByCategoryRarity[(c, r)] = rule = new ItemRule();
        return rule;
    }

    public static ItemRule EditUpTo(ItemCategory c, Rarity r)
    {
        if (!ByCategoryUpTo.TryGetValue((c, r), out var rule)) ByCategoryUpTo[(c, r)] = rule = new ItemRule();
        return rule;
    }

    public static ItemRule EditUpTo(Rarity r)
    {
        if (!ByRarityUpTo.TryGetValue(r, out var rule)) ByRarityUpTo[r] = rule = new ItemRule();
        return rule;
    }

    public static ItemRule Edit(string key)
    {
        if (!ByItem.TryGetValue(key, out var rule))
        {
            ByItem[key] = rule = new ItemRule();
            IndexLevels();
        }
        return rule;
    }

    public static bool Remove(ItemCategory c) => ByCategory.Remove(c);
    public static bool Remove(ItemCategory c, Rarity r) => ByCategoryRarity.Remove((c, r));
    public static bool RemoveUpTo(ItemCategory c, Rarity r) => ByCategoryUpTo.Remove((c, r));
    public static bool RemoveUpTo(Rarity r) => ByRarityUpTo.Remove(r);

    public static bool Remove(string key)
    {
        if (!ByItem.Remove(key)) return false;
        IndexLevels();
        return true;
    }

    // Tables only, no save write (self-checks).
    public static void UseForTest(string json) => Apply(JsonValue.Parse(json).AsJsonObject, "test rules");

    private static void Clear()
    {
        Default = new ItemRule();
        ByCategory.Clear();
        ByCategoryRarity.Clear();
        ByCategoryUpTo.Clear();
        ByRarityUpTo.Clear();
        ByItem.Clear();
        ByItemLevel.Clear();
        HasDockRules = false;
    }

    private static void Apply(JsonObject root, string source)
    {
        Clear();
        if (root == null)
        {
            Plugin.Log.LogWarning($"[ItemRules] {source}: top level is not a JSON object; no rules");
            return;
        }
        Default = ParseRule(root["default"]) ?? new ItemRule();

        var cats = root["categories"].AsJsonObject;
        if (cats != null)
            foreach (var kv in cats)
            {
                var rule = ParseRule(kv.Value);
                if (rule == null) continue;
                if (TryEnum(kv.Key, out ItemCategory cat)) ByCategory[cat] = rule;
                else Plugin.Log.LogWarning($"[ItemRules] {source}: unknown category '{kv.Key}', entry skipped");
            }

        ParsePairs(root["categoryRarity"].AsJsonObject, ByCategoryRarity, source, "category/rarity");
        ParsePairs(root["categoryUpTo"].AsJsonObject, ByCategoryUpTo, source, "category/rarity (and below)");

        var rups = root["rarityUpTo"].AsJsonObject;
        if (rups != null)
            foreach (var kv in rups)
            {
                var rule = ParseRule(kv.Value);
                if (rule == null) continue;
                if (TryEnum(kv.Key, out Rarity rarity)) ByRarityUpTo[rarity] = rule;
                else Plugin.Log.LogWarning($"[ItemRules] {source}: unknown rarity '{kv.Key}' (and below), entry skipped");
            }

        var its = root["items"].AsJsonObject;
        if (its != null)
            foreach (var kv in its)
            {
                var rule = ParseRule(kv.Value);
                if (rule != null) ByItem[kv.Key] = rule;
            }
        IndexLevels();
        UpdateHasDockRules();
    }

    private static void UpdateHasDockRules()
    {
        bool any = Default.dockAction != DockAction.Default;
        foreach (var r in ByCategory.Values) any |= r.dockAction != DockAction.Default;
        foreach (var r in ByCategoryRarity.Values) any |= r.dockAction != DockAction.Default;
        foreach (var r in ByCategoryUpTo.Values) any |= r.dockAction != DockAction.Default;
        foreach (var r in ByRarityUpTo.Values) any |= r.dockAction != DockAction.Default;
        foreach (var r in ByItem.Values) any |= r.dockAction != DockAction.Default;
        HasDockRules = any;
    }

    // "Category/Rarity" keys.
    private static void ParsePairs(JsonObject obj, Dictionary<(ItemCategory, Rarity), ItemRule> into, string source, string what)
    {
        if (obj == null) return;
        foreach (var kv in obj)
        {
            var rule = ParseRule(kv.Value);
            if (rule == null) continue;
            int slash = kv.Key.IndexOf('/');
            if (slash > 0 && TryEnum(kv.Key.Substring(0, slash), out ItemCategory cat) && TryEnum(kv.Key.Substring(slash + 1), out Rarity rarity))
                into[(cat, rarity)] = rule;
            else Plugin.Log.LogWarning($"[ItemRules] {source}: unknown {what} '{kv.Key}', entry skipped");
        }
    }

    private static void IndexLevels()
    {
        ByItemLevel.Clear();
        foreach (var kv in ByItem)
        {
            int at = kv.Key.LastIndexOf('@');
            if (at > 0 && int.TryParse(kv.Key.Substring(at + 1), out int level)) ByItemLevel[(kv.Key.Substring(0, at), level)] = kv.Value;
        }
    }

    private static JsonObject ToJson(ItemRule rule)
    {
        var obj = new JsonObject();
        obj["tractorCap"] = rule.tractorCap;
        obj["dockAction"] = rule.dockAction.ToString();
        obj["keepAmount"] = rule.keepAmount;
        return obj;
    }

    private static ItemRule ParseRule(JsonValue value)
    {
        var obj = value.AsJsonObject;
        if (obj == null) return null;
        var rule = new ItemRule();
        if (obj["tractorCap"].IsNumber) rule.tractorCap = Math.Max(ItemRule.Never, obj["tractorCap"].AsInteger);
        if (obj["dockAction"].IsString && TryEnum(obj["dockAction"].AsString, out DockAction action)) rule.dockAction = action;
        if (obj["keepAmount"].IsNumber) rule.keepAmount = Math.Max(0, obj["keepAmount"].AsInteger);
        return rule;
    }

    private static bool TryEnum<T>(string name, out T value) where T : struct
    {
        value = default;
        if (string.IsNullOrEmpty(name) || char.IsDigit(name[0])) return false;
        return Enum.TryParse(name, false, out value) && Enum.IsDefined(typeof(T), value);
    }
}
