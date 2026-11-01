using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Behaviour.Crafting;
using Behaviour.Crew;
using Behaviour.Item;
using Behaviour.UI;
using Behaviour.UI.Tooltip;
using HarmonyLib;
using IgiQoL.Core;
using Source.Item;
using Source.Player;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.UsedIn;

public sealed class UsedIn : Feature
{
    public static UsedIn Instance;

    public UsedIn() { Instance = this; }

    public override string Id => "UsedIn";
    public override string Title => "Used in";
    public override string Description => "Item tooltips list the forge recipes you can craft that use the item, with how many one craft takes. The Stat reference tab searches the full list.";
    public override Type[] PatchClasses => new[] { typeof(UsedInPatches) };

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => UsedInCheck.Run(ctx);

    internal const int MaxLines = 8;
    internal const string Header = "Used in:";

    // Ingredient identifier → the recipes you can craft that take it, sorted by name, then recipe, then rarity.
    // Each rarity variant you have is its own recipe with its own ingredients, so every one is listed.
    private static readonly Dictionary<string, List<CraftingRecipe>> index = new();
    private static readonly HashSet<CraftingRecipe> seen = new();
    private static GamePlayer indexedFor;
    private static int indexedBlueprints = -1, indexedMissions = -1;
    private static bool indexedBasic;

    // The forge's recipe list (CraftingRecipe.GetAvailable) changes with learned blueprints, the basic-recipes
    // skill and two story missions. Rebuilding only when one of those changes keeps a hover to a dictionary lookup.
    internal static Dictionary<string, List<CraftingRecipe>> Index()
    {
        var player = GamePlayer.current;
        bool basic = SkilltreeNode.industrialBasicRecipes != null && SkilltreeNode.industrialBasicRecipes.isActive;
        if (player == indexedFor && player.blueprintLevels.Count == indexedBlueprints && player.missions.Count == indexedMissions && basic == indexedBasic)
            return index;

        index.Clear();
        seen.Clear();
        indexedFor = player;
        indexedBlueprints = player.blueprintLevels.Count;
        indexedMissions = player.missions.Count;
        indexedBasic = basic;
        foreach (var r in CraftingRecipe.GetAvailable())
        {
            if (r == null || !r.craftingEnabled || !seen.Add(r)) continue;
            foreach (var (material, _) in r.GetIngredientMaterials())
                Add(material.GetCanisterItem(), r);
            foreach (var (item, _) in r.GetIngredientItems())
                Add(item, r);
        }
        foreach (var list in index.Values)
            list.Sort((a, b) =>
            {
                int by = string.CompareOrdinal(Translation.Translate(a.displayName), Translation.Translate(b.displayName));
                if (by == 0) by = string.CompareOrdinal((a.parentRecipe ?? a).identifier, (b.parentRecipe ?? b).identifier);
                return by != 0 ? by : a.craftingRarity.CompareTo(b.craftingRarity);
            });
        return index;
    }

    private static void Add(InventoryItemType item, CraftingRecipe recipe)
    {
        if (item == null || string.IsNullOrEmpty(item.identifier)) return;
        if (!index.TryGetValue(item.identifier, out var list)) index[item.identifier] = list = new List<CraftingRecipe>();
        if (list.Count == 0 || list[list.Count - 1] != recipe) list.Add(recipe);
    }

    // How much of the item one craft takes, as the forge's ingredient list shows it (scaled to the output level).
    internal static string PerCraft(CraftingRecipe recipe, InventoryItemType item)
    {
        float amount = 0f;
        foreach (var (material, per) in recipe.GetIngredientMaterials())
            if (material.GetCanisterItem()?.identifier == item.identifier) amount += per;
        foreach (var (ingredient, count) in recipe.GetIngredientItems())
            if (ingredient != null && ingredient.identifier == item.identifier) amount += count;
        return GameMath.FormatNumber(amount, amount == Mathf.Floor(amount) ? 0 : 1);
    }

    // The tooltip block for an item, or null when no recipe you can craft takes it.
    internal static string Describe(InventoryItemType item)
    {
        if (item == null || string.IsNullOrEmpty(item.identifier) || GamePlayer.current == null) return null;
        if (!Index().TryGetValue(item.identifier, out var recipes)) return null;

        var sb = new StringBuilder(Header);
        AppendLines(sb, item, recipes, MaxLines);
        return sb.ToString();
    }

    // One "\n  name xN" line per recipe, at most maxLines, then "+N more". When you have more than one rarity of a
    // recipe, each rarity's count is shown in that rarity's colour. Variants of one recipe sit next to each other in
    // the index, so a new line starts whenever the parent recipe changes.
    internal static void AppendLines(StringBuilder sb, InventoryItemType item, List<CraftingRecipe> recipes, int maxLines)
    {
        CraftingRecipe line = null;
        int lines = 0;
        foreach (var r in recipes)
        {
            var parent = r.parentRecipe ?? r;
            if (parent != line)
            {
                line = parent;
                if (++lines <= maxLines) sb.Append("\n  ").Append(Translation.Translate(r.displayName));
            }
            if (lines > maxLines) continue;
            if (parent.subRecipes.Count > 1)
                sb.Append(" <color=#").Append(ColorUtility.ToHtmlStringRGB(r.craftingRarity.GetColor())).Append(">x")
                  .Append(PerCraft(r, item)).Append("</color>");
            else sb.Append(" x").Append(PerCraft(r, item));
        }
        if (lines > maxLines) sb.Append("\n  +").Append(lines - maxLines).Append(" more");
    }

    // How many lines AppendLines writes without a cap: one per recipe, whatever rarities you have of it.
    internal static int LineCount(List<CraftingRecipe> recipes)
    {
        CraftingRecipe line = null;
        int lines = 0;
        foreach (var r in recipes)
        {
            var parent = r.parentRecipe ?? r;
            if (parent != line) { line = parent; lines++; }
        }
        return lines;
    }
}

[HarmonyPatch]
internal static class UsedInPatches
{
    private static UsedIn F => UsedIn.Instance;

    // After the item's description, before volume and value. Compare tooltips (the fitted piece shown beside it) are left alone.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CompareTooltip), "AddDescriptionAndPreview")]
    private static void AddDescriptionAndPreviewPostfix(CompareTooltip __instance, InventoryItemType item, ItemTooltipContext context)
    {
        if (!F.Active || context == ItemTooltipContext.Compare) return;
        try
        {
            string text = UsedIn.Describe(item);
            if (text != null) __instance.AddTextLine(text).Text.color = CompareTooltip.detailsColor;
        }
        catch (Exception e) { F.Fail(e, nameof(AddDescriptionAndPreviewPostfix)); }
    }
}
