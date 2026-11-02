using System;
using System.Collections.Generic;
using System.Text;
using Behaviour.Crafting;
using Behaviour.Mining;
using Behaviour.Item;
using Behaviour.UI.Forge;
using Behaviour.UI.NotificationAlert;
using Behaviour.UI.Spacestation;
using Behaviour.Util;
using IgiQoL.Core;
using LightJson;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.Item;
using Source.Mining;
using Source.Player;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.CraftRollup;

public sealed class RollupStep
{
    public CraftingRecipe recipe;
    public int amount;
    public bool started;
    // The forge job this step started; relinked by recipe after a reload.
    public ForgeJob job;
    // What the step makes (first result), so a short ingredient another step makes counts as "in progress".
    public InventoryItemType product;
}

// Ore refined at the station's refinery for canisters the plan is short of.
public sealed class RollupRefine
{
    public OreItemData ore;
    public int amount;
    public bool started;
    // The refinery job this step started; relinked by ore and amount after a reload.
    public RefineryJob job;
}

public sealed class RollupPlan
{
    public SpaceStation station;
    public CraftingRecipe target;
    public int amount;
    // Parts first, the target recipe last.
    public readonly List<RollupStep> steps = new();
    // Refinery jobs for missing canisters; they run before the forge steps that need them.
    public readonly List<RollupRefine> refines = new();
    // Items neither this forge nor the station's ore makes (canisters, drops), with the shortfall at plan time.
    public readonly Dictionary<InventoryItemType, int> missing = new();
    public long credits;
    public float seconds;
    public float refineSeconds;
    public string waiting;
    public float nextCheck;

    public int StartedCount
    {
        get
        {
            int n = 0;
            foreach (var s in steps) if (s.started) n++;
            return n;
        }
    }

    public int PartCount => steps.Count - 1;

    public int RefinesStarted
    {
        get
        {
            int n = 0;
            foreach (var r in refines) if (r.started) n++;
            return n;
        }
    }

    // Something to queue besides the target recipe itself.
    public bool HasWork => PartCount > 0 || refines.Count > 0;
}

public static class Rollups
{
    public const string FeatureKey = "CraftRollup";
    private const int MaxDepth = 6;

    public static readonly List<RollupPlan> Active = new();

    public static RollupPlan Find(Forge forge, CraftingRecipe target)
    {
        foreach (var p in Active)
            if (p.station == forge.spaceStation && p.target == target) return p;
        return null;
    }

    // Item → recipe at this forge that makes it, with how many one craft yields.
    public static Dictionary<InventoryItemType, (CraftingRecipe recipe, int per)> Index(Forge forge)
    {
        var index = new Dictionary<InventoryItemType, (CraftingRecipe, int)>();
        foreach (var r in forge.recipes)
        {
            if (r == null || !r.craftingEnabled) continue;
            foreach (var row in r.RawResults)
            {
                if (row.item == null) continue;
                var t = ItemDefinition.TemplateOf(row.item);
                if (t != null && !index.ContainsKey(t)) index[t] = (r, Math.Max(1, row.count));
            }
        }
        return index;
    }

    public static RollupPlan Build(Forge forge, CraftingRecipe target, int amount)
    {
        var b = new Builder(forge, new RollupPlan { station = forge.spaceStation, target = target, amount = amount });
        b.Expand(target, amount, 0);
        b.PlanRefining();
        var plan = b.plan;
        foreach (var s in plan.steps)
        {
            plan.credits += forge.GetFeePerUnit(s.recipe) * s.amount;
            plan.seconds += s.recipe.craftingTime / forge.craftingSpeed * s.amount;
        }
        var refinery = forge.spaceStation.refinery;
        foreach (var r in plan.refines)
        {
            plan.credits += refinery.GetFeePerUnit(r.ore) * r.amount;
            plan.refineSeconds += r.ore.refinementTime * r.amount;
        }
        return plan;
    }

    private const float Epsilon = 0.0001f;

    // Refinery units of one ore needed to cover `need` of a material, counting what is already pending.
    internal static int UnitsFor(int need, float pending, float yield, float mult) =>
        Math.Max(1, Mathf.CeilToInt((need - pending) / (yield * mult) - Epsilon));

    // Ore at the station the refinery may take for a roll-up: unfavourited, not needed for missions, auto-refine allowed.
    public static Dictionary<OreItemData, int> RefinableOre(Refinery refinery)
    {
        var ores = new Dictionary<OreItemData, int>();
        foreach (var kv in refinery.GetAvailableItems(false, true))
        {
            if (kv.Key.disableAutoRefine) continue;
            ores.TryGetValue(kv.Key, out int n);
            ores[kv.Key] = n + kv.Value;
        }
        return ores;
    }

    public static float YieldOf(OreItemData ore, RefinedMaterial material)
    {
        float y = 0f;
        foreach (var c in ore.contents)
            if (c.product == material) y += c.yield;
        return y;
    }

    private sealed class Builder
    {
        private readonly Forge forge;
        public readonly RollupPlan plan;
        private readonly Dictionary<InventoryItemType, (CraftingRecipe recipe, int per)> index;
        private readonly Dictionary<InventoryItemType, int> stock = new();
        private readonly HashSet<CraftingRecipe> visiting = new();

        public Builder(Forge forge, RollupPlan plan)
        {
            this.forge = forge;
            this.plan = plan;
            index = Index(forge);
        }

        // Takes up to `need` from what the station holds, net of what earlier branches of this plan already claimed.
        private int Take(InventoryItemType item, int need, bool canister)
        {
            if (!stock.TryGetValue(item, out int have))
                have = canister ? forge.spaceStation.CountLocalItems(item) : forge.spaceStation.CountForgeItems(item);
            int used = Math.Min(have, need);
            stock[item] = have - used;
            return used;
        }

        private void Missing(InventoryItemType item, int count)
        {
            plan.missing.TryGetValue(item, out int n);
            plan.missing[item] = n + count;
        }

        public void Expand(CraftingRecipe recipe, int n, int depth)
        {
            visiting.Add(recipe);
            foreach (var (material, per) in recipe.GetIngredientMaterials())
            {
                if (per <= 0f) continue;
                var canister = material.GetCanisterItem();
                int need = CraftingRecipe.CountCanistersForBatch(per, n);
                int got = Take(canister, need, true);
                if (need > got) Missing(canister, need - got);
            }
            foreach (var (item, count) in recipe.GetIngredientItems())
            {
                int need = count * n;
                int shortBy = need - Take(item, need, false);
                if (shortBy <= 0) continue;
                if (depth < MaxDepth && index.TryGetValue(item, out var src) && !visiting.Contains(src.recipe))
                {
                    int crafts = (shortBy + src.per - 1) / src.per;
                    Expand(src.recipe, crafts, depth + 1);
                    stock[item] += crafts * src.per - shortBy;
                }
                else Missing(item, shortBy);
            }
            visiting.Remove(recipe);
            AddStep(recipe, n);
        }

        // Covers missing canisters with ore at the station: per material the ore with the best yield first,
        // counting the refinery's carried-over fractions and every by-product of ore already picked.
        public void PlanRefining()
        {
            var refinery = forge.spaceStation.refinery;
            if (refinery == null || plan.missing.Count == 0) return;
            var ores = RefinableOre(refinery);
            if (ores.Count == 0) return;
            float mult = PlayerStation.GetRefineryYieldMultiplier(forge.spaceStation);
            var supply = new float[Enum.GetValues(typeof(RefinedMaterial)).Length];
            for (int i = 0; i < supply.Length; i++) supply[i] = refinery.GetPendingYield((RefinedMaterial)i);

            var canisters = new List<InventoryItemType>(plan.missing.Keys);
            foreach (var item in canisters)
            {
                var m = RefinedMaterialExtensions.FromCanister(item);
                if (m == null) continue;
                int mi = (int)m.Value;
                int need = plan.missing[item];
                while (supply[mi] + Epsilon < need)
                {
                    OreItemData best = null;
                    float bestYield = 0f;
                    foreach (var kv in ores)
                    {
                        if (kv.Value <= 0) continue;
                        float y = YieldOf(kv.Key, m.Value);
                        if (y > bestYield) { best = kv.Key; bestYield = y; }
                    }
                    if (best == null) break;
                    int units = UnitsFor(need, supply[mi], bestYield, mult);
                    units = Math.Min(units, ores[best]);
                    ores[best] -= units;
                    AddRefine(best, units);
                    foreach (var c in best.contents) supply[(int)c.product] += units * c.yield * mult;
                }
            }
            // By-products of later picks count for materials handled earlier, so settle the shortfalls last.
            foreach (var item in canisters)
            {
                var m = RefinedMaterialExtensions.FromCanister(item);
                if (m == null) continue;
                int need = plan.missing[item];
                int covered = Math.Min(need, Mathf.FloorToInt(supply[(int)m.Value] + Epsilon));
                supply[(int)m.Value] -= covered;
                if (covered >= need) plan.missing.Remove(item);
                else plan.missing[item] = need - covered;
            }
        }

        private void AddRefine(OreItemData ore, int units)
        {
            foreach (var r in plan.refines)
            {
                if (r.ore != ore) continue;
                r.amount += units;
                return;
            }
            plan.refines.Add(new RollupRefine { ore = ore, amount = units });
        }

        private void AddStep(CraftingRecipe recipe, int n)
        {
            foreach (var s in plan.steps)
            {
                if (s.recipe != recipe) continue;
                s.amount += n;
                return;
            }
            plan.steps.Add(new RollupStep { recipe = recipe, amount = n, product = Product(recipe) });
        }
    }

    private static InventoryItemType Product(CraftingRecipe recipe)
    {
        foreach (var row in recipe.RawResults)
            return row.item != null ? ItemDefinition.TemplateOf(row.item) : null;
        return null;
    }

    public static string Name(InventoryItemType item) => item != null ? Translation.Translate(item.displayName) : "?";

    public static string OreName(OreItemData ore) => ore != null ? Name(ore.item) : "?";

    public static string RecipeName(CraftingRecipe recipe) => Translation.Translate(recipe.displayName);

    public static void Start(RollupPlan plan)
    {
        Active.Add(plan);
        Save();
        Advance(plan);
    }

    public static void Stop(RollupPlan plan)
    {
        Active.Remove(plan);
        Save();
    }

    // Called after every Forge.ProgressJobs: each frame for the station you're at, every 10 s for the others.
    internal static void Tick(Forge forge)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            var plan = Active[i];
            if (plan.station.forge != forge || Time.unscaledTime < plan.nextCheck) continue;
            plan.nextCheck = Time.unscaledTime + 1f;
            Advance(plan);
        }
    }

    // Starts every step whose ingredients, credits and a forge slot are there. Returns true when something started.
    public static bool Advance(RollupPlan plan)
    {
        var forge = plan.station.forge;
        if (forge == null)
        {
            Stop(plan);
            return false;
        }
        // Missing ingredients or credits are reported before a full queue: they need you, a slot frees itself.
        string blocker = null;
        bool slotFull = false;
        bool refinerySlotFull = false;
        bool changed = false;
        bool refineChanged = false;
        var refinery = plan.station.refinery;
        foreach (var r in plan.refines)
        {
            if (r.started) continue;
            if (refinery == null)
            {
                blocker ??= "a refinery at this station";
                continue;
            }
            RefinableOre(refinery).TryGetValue(r.ore, out int have);
            if (have < r.amount)
            {
                blocker ??= (r.amount - have) + " " + OreName(r.ore);
                continue;
            }
            if (!GamePlayer.current.CanAfford(refinery.GetFeePerUnit(r.ore) * r.amount))
            {
                blocker ??= "credits";
                continue;
            }
            if (!refinery.HasRoomForOutput(r.ore))
            {
                blocker ??= "room in the materials storage";
                continue;
            }
            if (refinery.jobs.Count >= refinery.maxJobs)
            {
                refinerySlotFull = true;
                continue;
            }
            int before = refinery.jobs.Count;
            if (!refinery.StartJob(r.ore, r.amount, excludeFavourited: true)) continue;
            r.started = true;
            if (refinery.jobs.Count > before) r.job = refinery.jobs[refinery.jobs.Count - 1];
            changed = refineChanged = true;
        }
        foreach (var step in plan.steps)
        {
            if (step.started) continue;
            if (step.recipe.CountAvailableForCrafting(forge) < step.amount)
            {
                blocker ??= ShortBlocker(plan, step, forge);
                continue;
            }
            if (!GamePlayer.current.CanAfford(forge.GetFeePerUnit(step.recipe) * step.amount))
            {
                blocker ??= "credits";
                continue;
            }
            if (forge.jobs.Count >= forge.maxJobs)
            {
                slotFull = true;
                continue;
            }
            int before = forge.jobs.Count;
            if (!forge.StartJob(step.recipe, step.amount)) continue;
            step.started = true;
            if (forge.jobs.Count > before) step.job = forge.jobs[forge.jobs.Count - 1];
            changed = true;
        }
        plan.waiting = blocker ?? (slotFull ? "a free forge slot" : refinerySlotFull ? "a free refinery slot" : null);
        if (plan.StartedCount == plan.steps.Count)
        {
            Active.Remove(plan);
            Singleton<NotificationManager>.Instance.CreateNotification($"Roll-up finished: {RecipeName(plan.target)} x{plan.amount} is in the forge queue at {plan.station.name}")
                .WithColor(ColorHelper.orange75).Show();
        }
        if (changed)
        {
            Save();
            if (SpaceStationInterior.instance != null && SpaceStation.current == plan.station) SpaceStationInterior.instance.UpdateJobs();
            if (ForgeUI.current != null && ForgeUI.current.forge == forge) ForgeUI.current.UpdateContent();
            if (refineChanged && Behaviour.UI.Refinery.RefineryUI.current != null && Refinery.current == refinery) Behaviour.UI.Refinery.RefineryUI.current.UpdateContent();
        }
        return changed;
    }

    // The first short ingredient of a step that isn't on its way; null when every short one is still being made.
    private static string ShortBlocker(RollupPlan plan, RollupStep step, Forge forge)
    {
        var station = forge.spaceStation;
        foreach (var (material, per) in step.recipe.GetIngredientMaterials())
        {
            if (per <= 0f) continue;
            var canister = material.GetCanisterItem();
            int shortBy = CraftingRecipe.CountCanistersForBatch(per, step.amount) - station.CountLocalItems(canister);
            if (shortBy > 0 && !BeingRefined(plan, material, station.refinery)) return shortBy + " " + Name(canister);
        }
        foreach (var (item, count) in step.recipe.GetIngredientItems())
        {
            int shortBy = count * step.amount - station.CountForgeItems(item);
            if (shortBy <= 0 || OnItsWay(plan, item, forge)) continue;
            return shortBy + " " + Name(item);
        }
        return null;
    }

    // A step of this plan that hasn't started yet, or is still in the forge queue, makes the item.
    // A part job that finished or was cancelled and still left the item short is a blocker.
    private static bool OnItsWay(RollupPlan plan, InventoryItemType item, Forge forge)
    {
        foreach (var s in plan.steps)
        {
            if (s.product != item) continue;
            if (!s.started || (s.job != null && forge.jobs.Contains(s.job))) return true;
        }
        return false;
    }

    // A refine step of this plan that yields the material hasn't started yet or is still in the refinery.
    private static bool BeingRefined(RollupPlan plan, RefinedMaterial material, Refinery refinery)
    {
        foreach (var r in plan.refines)
        {
            if (YieldOf(r.ore, material) <= 0f) continue;
            if (!r.started || (r.job != null && refinery != null && refinery.jobs.Contains(r.job))) return true;
        }
        return false;
    }

    // A refinery job started for an active plan.
    public static bool IsRefineJob(RefineryJob job)
    {
        foreach (var plan in Active)
            foreach (var r in plan.refines)
                if (r.job == job) return true;
        return false;
    }

    // A job started for a part of an active plan (not the target itself).
    public static bool IsPartJob(ForgeJob job)
    {
        foreach (var plan in Active)
        {
            for (int i = 0; i < plan.steps.Count - 1; i++)
                if (plan.steps[i].job == job) return true;
        }
        return false;
    }

    public static string Status(RollupPlan plan)
    {
        var sb = new StringBuilder();
        sb.Append("Roll-up running: ").Append(plan.StartedCount).Append(" of ").Append(plan.steps.Count).Append(" forge jobs queued");
        if (plan.refines.Count > 0) sb.Append(", ").Append(plan.RefinesStarted).Append(" of ").Append(plan.refines.Count).Append(" refinery jobs started");
        sb.Append(". ");
        if (plan.waiting != null) sb.Append("Waiting for ").Append(plan.waiting).Append('.');
        else sb.Append("Parts in progress; the recipe is queued when they're done.");
        return sb.ToString();
    }

    public static void Save()
    {
        var arr = new JsonArray();
        foreach (var plan in Active)
        {
            var steps = new JsonArray();
            foreach (var s in plan.steps)
                steps.Add(new JsonObject { { "recipe", s.recipe.identifier }, { "amount", s.amount }, { "started", s.started } });
            var refines = new JsonArray();
            foreach (var r in plan.refines)
                refines.Add(new JsonObject { { "ore", r.ore.item.identifier }, { "amount", r.amount }, { "started", r.started } });
            arr.Add(new JsonObject
            {
                { "station", plan.station.guid },
                { "target", plan.target.identifier },
                { "amount", plan.amount },
                { "steps", steps },
                { "refines", refines },
            });
        }
        SaveData.Feature(FeatureKey, true)["plans"] = arr;
    }

    public static void Load()
    {
        Active.Clear();
        var taken = new HashSet<ForgeJob>();
        var takenRefine = new HashSet<RefineryJob>();
        var record = SaveData.Feature(FeatureKey, false);
        if (record == null || !record["plans"].IsJsonArray) return;
        foreach (var v in record["plans"].AsJsonArray)
        {
            if (!v.IsJsonObject) continue;
            var station = GalaxyMapData.current?.GetPointOfInterest(v["station"].AsString) as SpaceStation;
            if (station?.forge == null || !CraftingRecipe.TryGet(v["target"].AsString ?? "", out var target)) continue;
            var plan = new RollupPlan { station = station, target = target, amount = v["amount"].AsInteger };
            bool ok = v["steps"].IsJsonArray;
            if (ok)
            {
                foreach (var sv in v["steps"].AsJsonArray)
                {
                    if (!CraftingRecipe.TryGet(sv["recipe"].AsString ?? "", out var recipe)) { ok = false; break; }
                    plan.steps.Add(new RollupStep { recipe = recipe, amount = sv["amount"].AsInteger, started = sv["started"].AsBoolean, product = Product(recipe) });
                }
            }
            if (!ok || plan.steps.Count == 0 || plan.steps[plan.steps.Count - 1].recipe != target)
            {
                Plugin.Log.LogWarning($"[{FeatureKey}] dropped a saved roll-up for {v["target"].AsString}: a recipe no longer exists");
                continue;
            }
            // An ore type that no longer exists only drops its refine step; the canisters then show as missing.
            if (v["refines"].IsJsonArray)
            {
                foreach (var rv in v["refines"].AsJsonArray)
                {
                    var ore = InventoryItemType.TryGet(rv["ore"].AsString ?? "", out var oreItem) ? oreItem.GetComponent<OreItemData>() : null;
                    if (ore == null)
                    {
                        Plugin.Log.LogWarning($"[{FeatureKey}] dropped a refine step for {rv["ore"].AsString}: no such ore");
                        continue;
                    }
                    plan.refines.Add(new RollupRefine { ore = ore, amount = rv["amount"].AsInteger, started = rv["started"].AsBoolean });
                }
            }
            Relink(plan, taken, takenRefine);
            Active.Add(plan);
        }
        if (Active.Count > 0) Plugin.Log.LogInfo($"[{FeatureKey}] resumed {Active.Count} roll-up(s)");
    }

    // Saved jobs come back as new objects; pair started parts with queued jobs of the same recipe.
    private static void Relink(RollupPlan plan, HashSet<ForgeJob> taken, HashSet<RefineryJob> takenRefine)
    {
        // taken is shared across the plans of one Load so two plans never claim the same queued job.
        foreach (var s in plan.steps)
        {
            if (!s.started) continue;
            foreach (var job in plan.station.forge.jobs)
            {
                if (job.recipe != s.recipe || taken.Contains(job)) continue;
                s.job = job;
                taken.Add(job);
                break;
            }
        }
        var refinery = plan.station.refinery;
        if (refinery == null) return;
        foreach (var r in plan.refines)
        {
            if (!r.started) continue;
            foreach (var job in refinery.jobs)
            {
                if (job.ore != r.ore || job.initialAmount != r.amount || takenRefine.Contains(job)) continue;
                r.job = job;
                takenRefine.Add(job);
                break;
            }
        }
    }
}
