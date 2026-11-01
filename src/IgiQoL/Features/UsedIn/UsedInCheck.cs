using System.Collections;
using System.Collections.Generic;
using Behaviour.Crafting;
using Behaviour.Item;
using Behaviour.Mining;
using Behaviour.UI;
using Behaviour.UI.Tooltip;
using IgiQoL.Core;
using Source.Item;
using Source.Player;
using Source.Util;
using TMPro;
using UnityEngine;

namespace IgiQoL.Features.UsedIn;

// Opens real item tooltips through the game's own UITooltip.Show and reads what they say.
internal static class UsedInCheck
{
    public static IEnumerator Run(SelfCheckContext ctx)
    {
        var player = GamePlayer.current;
        if (player == null || UITooltipParent.ItemTooltipPrefab == null || !UITooltip.TooltipEnabled)
        {
            ctx.Skip("tooltip", "no player, item tooltip prefab or tooltips are switched off");
            yield break;
        }

        // Every (craftable recipe, ingredient) pair is indexed, and nothing else is.
        var available = new HashSet<CraftingRecipe>();
        foreach (var r in CraftingRecipe.GetAvailable())
            if (r != null && r.craftingEnabled) available.Add(r);
        int pairs = 0, missing = 0, extra = 0;
        InventoryItemType most = null;
        int mostCount = 0;
        foreach (var r in available)
        {
            foreach (var (m, _) in r.GetIngredientMaterials()) { pairs++; if (!Lists(m.GetCanisterItem(), r)) missing++; }
            foreach (var (i, _) in r.GetIngredientItems())
            {
                pairs++;
                if (!Lists(i, r)) { missing++; continue; }
                int uses = UsedIn.Index()[i.identifier].Count;
                if (uses > mostCount) { most = i; mostCount = uses; }
            }
        }
        foreach (var kv in UsedIn.Index())
            foreach (var r in kv.Value) if (!available.Contains(r)) extra++;
        string indexDetail = $"{available.Count} recipes, {pairs} ingredient rows, {UsedIn.Index().Count} items; missing={missing} extra={extra}";
        if (pairs > 0 && missing == 0 && extra == 0) ctx.Pass("index", indexDetail);
        else ctx.Fail("index", indexDetail);
        if (most == null)
        {
            ctx.Skip("tooltip", "no recipe you can craft has an ingredient");
            yield break;
        }

        var holder = new GameObject("IgiQoL_UsedInCheck");
        var source = holder.AddComponent<ItemTooltipSource>();
        try
        {
            // The most-used ingredient: one line per recipe, capped, first entry with the forge's per-craft count.
            var recipes = UsedIn.Index()[most.identifier];
            var parents = new List<CraftingRecipe>();
            foreach (var r in recipes) if (!parents.Contains(r.parentRecipe ?? r)) parents.Add(r.parentRecipe ?? r);
            source.SetItem(most, 1, false, ItemTooltipContext.InInventory, interactive: false);
            string text = Show(source);
            int expectedLines = 1 + Mathf.Min(parents.Count, UsedIn.MaxLines) + (parents.Count > UsedIn.MaxLines ? 1 : 0);
            string[] rows = text == null ? new string[0] : text.Split('\n');
            string first = Translation.Translate(recipes[0].displayName);
            string count = "x" + UsedIn.PerCraft(recipes[0], most);
            string detail = $"{most.displayName}: {recipes.Count} recipes in {parents.Count} lines, {rows.Length} of {expectedLines} rows: {(text ?? "no line").Replace("\n", " | ")}";
            if (text != null && rows.Length == expectedLines && rows[1].Contains(first) && rows[1].Contains(count)) ctx.Pass("tooltip", detail);
            else ctx.Fail("tooltip", detail);

            // A recipe you have in several rarities gets one count per rarity on its line.
            int shown = Mathf.Min(parents.Count, UsedIn.MaxLines), variantLine = -1, variants = 0;
            for (int p = 0; p < shown && variantLine < 0; p++)
            {
                int n = 0;
                foreach (var r in recipes) if ((r.parentRecipe ?? r) == parents[p]) n++;
                if (n > 1) { variantLine = p; variants = n; }
            }
            if (variantLine < 0) ctx.Skip("rarities", "no shown recipe is craftable in more than one rarity");
            else
            {
                string row = rows.Length > variantLine + 1 ? rows[variantLine + 1] : "";
                int counts = row.Split(new[] { ">x" }, System.StringSplitOptions.None).Length - 1;
                if (counts == variants) ctx.Pass("rarities", $"{variants} rarities, {counts} counts: {row}");
                else ctx.Fail("rarities", $"{variants} rarities, {counts} counts: {row}");
            }
            yield return ctx.Screenshot("used-in");

            // The fitted piece's compare tooltip gets nothing.
            source.SetItem(most, 1, false, ItemTooltipContext.Compare, interactive: false);
            text = Show(source);
            if (text == null) ctx.Pass("compare", "no line in the compare context");
            else ctx.Fail("compare", text.Replace("\n", " | "));

            // Ore goes to the refinery, not the forge.
            InventoryItemType ore = null;
            foreach (var o in OreItemData.regularOres)
                if (!UsedIn.Index().ContainsKey(o.item.identifier)) { ore = o.item; break; }
            if (ore == null) ctx.Skip("none", "every ore is a forge ingredient");
            else
            {
                source.SetItem(ore, 1, false, ItemTooltipContext.InInventory, interactive: false);
                text = Show(source);
                if (text == null) ctx.Pass("none", ore.displayName + ": no line");
                else ctx.Fail("none", ore.displayName + ": " + text.Replace("\n", " | "));
            }

            yield return CheckLearned(ctx, player, available, source);
        }
        finally
        {
            UITooltip.Hide(source);
            Object.Destroy(holder);
        }
    }

    // Learning a blueprint adds its recipe without a reload; forgetting it (the cleanup) takes it out again.
    private static IEnumerator CheckLearned(SelfCheckContext ctx, GamePlayer player, HashSet<CraftingRecipe> available, ItemTooltipSource source)
    {
        CraftingRecipe locked = null;
        InventoryItemType ingredient = null;
        foreach (var parent in CraftingRecipe.all)
        {
            foreach (var r in parent.subRecipes)
            {
                if (r == null || !r.craftingEnabled || available.Contains(r) || player.blueprintLevels.ContainsKey(r.identifier)) continue;
                foreach (var (i, _) in r.GetIngredientItems()) { if (i != null && !string.IsNullOrEmpty(i.identifier)) { ingredient = i; break; } }
                if (ingredient != null) { locked = r; break; }
            }
            if (locked != null) break;
        }
        if (locked == null)
        {
            ctx.Skip("learned", "every recipe with an item ingredient is already craftable");
            yield break;
        }

        player.blueprintLevels[locked.identifier] = 1;
        bool listed;
        string text;
        try
        {
            listed = Lists(ingredient, locked);
            source.SetItem(ingredient, 1, false, ItemTooltipContext.InInventory, interactive: false);
            text = Show(source);
        }
        finally { player.blueprintLevels.Remove(locked.identifier); }
        bool dropped = !Lists(ingredient, locked);
        string detail = $"{locked.identifier} → {ingredient.displayName}: listed={listed}, dropped after={dropped}, tooltip: {(text ?? "no line").Replace("\n", " | ")}";
        if (listed && dropped && text != null) ctx.Pass("learned", detail);
        else ctx.Fail("learned", detail);
        yield return null;
    }

    private static bool Lists(InventoryItemType item, CraftingRecipe recipe) =>
        item != null && !string.IsNullOrEmpty(item.identifier)
        && UsedIn.Index().TryGetValue(item.identifier, out var list) && list.Contains(recipe);

    // The "Used in" text of the tooltip the game builds for this source, or null when it has none.
    private static string Show(ItemTooltipSource source)
    {
        UITooltip.Show(source, source.Prefab);
        var tip = UITooltip._current;
        if (tip == null) return null;
        foreach (var t in tip.Content.GetComponentsInChildren<TMP_Text>(true))
            if (t.text.StartsWith(UsedIn.Header)) return t.text;
        return null;
    }
}
