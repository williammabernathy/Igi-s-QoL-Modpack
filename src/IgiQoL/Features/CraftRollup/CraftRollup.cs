using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Crafting;
using Behaviour.Item;
using Behaviour.Mining;
using Behaviour.UI.Forge;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using Source.Galaxy.POI;
using Source.Item;
using Source.Mining;
using Source.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.CraftRollup;

public sealed class CraftRollup : Feature
{
    public static CraftRollup Instance;

    public CraftRollup() { Instance = this; }

    public override string Id => "CraftRollup";
    public override string Title => "Crafting roll-up";
    public override string Description => "Adds a Craft missing parts button to forge recipes you can't craft yet. It queues the refined parts the recipe needs, refines ore at the station for missing canisters when it has a refinery, then queues the recipe itself once everything is there, also while you're away. Drops nothing makes still have to be at the station.";
    public override Type[] PatchClasses => new[] { typeof(RollupPatches) };

    public override void OnActivated() => GameEvents.SaveLoaded += OnSaveLoaded;

    private void OnSaveLoaded()
    {
        if (!Active) return;
        try { Rollups.Load(); }
        catch (Exception e) { Fail(e, nameof(OnSaveLoaded)); }
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        var station = FindForgeStation();
        if (station == null)
        {
            ctx.Skip("plan", "no station with a forge in this save");
            yield break;
        }
        var forge = station.forge;
        var index = Rollups.Index(forge);
        var target = PickTarget(forge, index);
        if (target == null)
        {
            ctx.Skip("plan", "no uncraftable turret/module recipe whose item ingredients the forge can make");
            yield break;
        }

        var plan = Rollups.Build(forge, target, 1);
        var last = plan.steps[plan.steps.Count - 1];
        bool ok = plan.steps.Count >= 2 && last.recipe == target && last.amount == 1;
        var detail = new System.Text.StringBuilder($"{station.name} ({station.GetType().Name}, {forge.jobs.Count}/{forge.maxJobs} jobs) {target.identifier}:");
        foreach (var (item, count) in target.GetIngredientItems())
        {
            var src = index[item];
            int shortBy = count - station.CountForgeItems(item);
            int expect = shortBy > 0 ? (shortBy + src.per - 1) / src.per : 0;
            var step = plan.steps.Find(s => s.recipe == src.recipe);
            int got = step?.amount ?? 0;
            detail.Append($" {item.identifier} need {count} have {station.CountForgeItems(item)} -> craft {got} (expect {expect});");
            if (got != expect) ok = false;
        }
        detail.Append($" missing {plan.missing.Count} kinds, {plan.credits} cr, {plan.seconds:0}s");
        if (ok) ctx.Pass("plan", detail.ToString());
        else ctx.Fail("plan", detail.ToString());

        yield return CheckPanel(ctx, "panel", station, target, RollupPanel.StartLabel, "Roll-up:");
        yield return CheckRun(ctx, station, target, plan);
        yield return CheckRefine(ctx);
    }

    private static CraftingRecipe PickTarget(Forge forge, Dictionary<InventoryItemType, (CraftingRecipe recipe, int per)> index)
    {
        foreach (var r in forge.recipes)
        {
            if (r == null || (r.category != ItemCategory.Turret && r.category != ItemCategory.Module)) continue;
            if (r.CountAvailableForCrafting(forge) >= 1) continue;
            int items = 0;
            bool allMade = true;
            foreach (var (item, _) in r.GetIngredientItems())
            {
                items++;
                if (!index.ContainsKey(item)) allMade = false;
            }
            if (items >= 2 && allMade) return r;
        }
        return null;
    }

    // The station you're at if it has a forge, else one of yours, else any; forges run remotely too.
    private static SpaceStation FindForgeStation()
    {
        if (GamePlayer.current.currentPointOfInterest is SpaceStation here && here.forge != null) return here;
        SpaceStation any = null;
        foreach (var poi in Source.Galaxy.GalaxyMapData.current.allPointsOfInterest)
        {
            if (poi is not SpaceStation s || s.forge == null) continue;
            if (s is PlayerStation) return s;
            any ??= s;
        }
        return any;
    }

    private static ForgeUI ForgePrefab()
    {
        foreach (var ui in Resources.FindObjectsOfTypeAll<ForgeUI>())
            if (!ui.gameObject.scene.IsValid()) return ui;
        return null;
    }

    private static string Describe(RollupPanel panel) =>
        panel == null ? "no panel" : $"button='{panel.buttonText.text}' summary='{panel.summary.text.Replace("\n", " | ")}'";

    // Opens the station's forge popup on the target recipe, checks the panel, screenshots it and closes the popup.
    private static IEnumerator CheckPanel(SelfCheckContext ctx, string name, SpaceStation station, CraftingRecipe target, string button, string summaryPart)
    {
        var prefab = ForgePrefab();
        if (prefab == null)
        {
            ctx.Skip(name, "no forge screen prefab loaded");
            yield break;
        }
        ForgePopup.Open(prefab, Kit.CloseSprite, station, target, false);
        yield return null;
        yield return null;
        var panel = ForgeUI.current != null ? ForgeUI.current.GetComponentInChildren<RollupPanel>() : null;
        string d = Describe(panel);
        if (panel != null && panel.buttonText.text == button && panel.summary.text.Contains(summaryPart)) ctx.Pass(name, d);
        else ctx.Fail(name, d);
        yield return ctx.Screenshot(name == "panel" ? "forge-rollup" : name == "panel-running" ? "forge-rollup-running" : "forge-rollup-" + name);
        ForgePopup.Close();
        yield return null;
    }

    private static IEnumerator CheckRun(SelfCheckContext ctx, SpaceStation station, CraftingRecipe target, RollupPlan plan)
    {
        var forge = station.forge;
        foreach (var kv in plan.missing)
        {
            if (kv.Key.identifier.StartsWith("Canister")) continue;
            ctx.Skip("run", $"needs {kv.Key.identifier}, which no recipe makes");
            yield break;
        }
        if (forge.jobs.Count >= forge.maxJobs)
        {
            ctx.Skip("run", $"forge queue full ({forge.jobs.Count}/{forge.maxJobs})");
            yield break;
        }
        if (!GamePlayer.current.CanAfford(plan.credits))
        {
            ctx.Skip("run", $"can't afford {plan.credits} cr");
            yield break;
        }

        // Snapshot what the run touches, so it can be put back.
        var storage = station.materialStorage;
        var products = new List<InventoryItemType>();
        foreach (var s in plan.steps)
            if (s.product != null) products.Add(s.product);
        var snap = new Snapshot(station, products);
        var jobsBefore = snap.ForgeJobs;

        foreach (var kv in plan.missing) storage.Add(kv.Key, kv.Value);
        var live = Rollups.Build(forge, target, 1);
        Rollups.Start(live);
        int startedAtOnce = live.StartedCount;

        // Save round trip: the record holds the plan and Load brings it back with its jobs linked.
        var record = SaveData.Feature(Rollups.FeatureKey, false)?["plans"];
        int savedCount = record.HasValue && record.Value.IsJsonArray ? record.Value.AsJsonArray.Count : -1;
        Rollups.Load();
        var loaded = Rollups.Active.Count == 1 ? Rollups.Active[0] : null;
        bool saveOk = savedCount == 1 && loaded != null && loaded.steps.Count == live.steps.Count && loaded.StartedCount == startedAtOnce;
        if (loaded != null)
        {
            for (int i = 0; i < live.steps.Count; i++)
            {
                var a = live.steps[i];
                var b = loaded.steps[i];
                if (a.recipe != b.recipe || a.amount != b.amount || a.started != b.started || (b.started && b.job == null)) saveOk = false;
            }
            live = loaded;
        }
        string sd = $"saved={savedCount} loaded steps={loaded?.steps.Count} started={loaded?.StartedCount}/{startedAtOnce}";
        if (saveOk) ctx.Pass("save", sd);
        else ctx.Fail("save", sd);

        yield return CheckPanel(ctx, "panel-running", station, target, RollupPanel.StopLabel, "Roll-up running");

        ForgeJob partJob = null;
        foreach (var s in live.steps)
            if (s.started && s != live.steps[live.steps.Count - 1]) { partJob = s.job; break; }
        bool partRouted = partJob != null && Rollups.IsPartJob(partJob);

        int ticks = 0;
        while (Rollups.Active.Contains(live) && ticks < 600)
        {
            live.nextCheck = 0f;
            station.refinery?.ProgressJobs(30f);
            forge.ProgressJobs(30f);
            ticks++;
            if (ticks % 25 == 0) yield return null;
        }
        ForgeJob finalJob = null;
        foreach (var job in forge.jobs)
            if (job.recipe == target && !jobsBefore.Contains(job)) finalJob = job;
        string rd = $"{live.steps.Count} jobs, {startedAtOnce} started at once, done after {ticks} x 30 s of forge time; target job {(finalJob != null ? "queued" : "missing")}; waiting={live.waiting ?? "-"}";
        if (!Rollups.Active.Contains(live) && finalJob != null) ctx.Pass("run", rd);
        else ctx.Fail("run", rd);

        string pd = $"part job marked={partRouted}, target job marked={(finalJob != null && Rollups.IsPartJob(finalJob))}";
        if (partRouted && finalJob != null && !Rollups.IsPartJob(finalJob)) ctx.Pass("parts-to-storage", pd);
        else ctx.Fail("parts-to-storage", pd);

        snap.Restore();

        // A part job that disappears (cancelled by hand) leaves its part short: the status names it instead of "in progress".
        try
        {
            foreach (var kv in plan.missing) storage.Add(kv.Key, kv.Value);
            var stall = Rollups.Build(forge, target, 1);
            Rollups.Start(stall);
            var cancelled = stall.steps[0];
            if (cancelled.job != null) forge.CancelJob(cancelled.job);
            stall.nextCheck = 0f;
            Rollups.Advance(stall);
            string part = Rollups.Name(cancelled.product);
            string std = $"cancelled {Rollups.RecipeName(cancelled.recipe)} job; waiting={stall.waiting ?? "-"}";
            if (stall.waiting != null && stall.waiting.EndsWith(part)) ctx.Pass("stalled-part", std);
            else ctx.Fail("stalled-part", std);
        }
        finally { snap.Restore(); }
    }

    // A station with both a forge and a refinery, preferring the one you're at, then your own.
    private static SpaceStation FindRefineStation()
    {
        if (GamePlayer.current.currentPointOfInterest is SpaceStation here && here.forge != null && here.refinery != null) return here;
        SpaceStation any = null;
        foreach (var poi in Source.Galaxy.GalaxyMapData.current.allPointsOfInterest)
        {
            if (poi is not SpaceStation s || s.forge == null || s.refinery == null) continue;
            if (s is PlayerStation) return s;
            any ??= s;
        }
        return any;
    }

    // With no ore and no canisters at the station, the plan lists canisters as missing; one ore type that yields
    // the first of them is then planned for exactly that shortfall, refined, and the recipe queued at the end.
    private static IEnumerator CheckRefine(SelfCheckContext ctx)
    {
        var station = FindRefineStation();
        if (station == null)
        {
            ctx.Skip("refine-plan", "no station with a forge and a refinery in this save");
            yield break;
        }
        var forge = station.forge;
        var refinery = station.refinery;
        var target = PickTarget(forge, Rollups.Index(forge));
        if (target == null)
        {
            ctx.Skip("refine-plan", $"no uncraftable turret/module recipe at {station.name} whose item ingredients the forge can make");
            yield break;
        }
        var storage = station.materialStorage;
        var basePlan = Rollups.Build(forge, target, 1);
        var products = new List<InventoryItemType>();
        foreach (var s in basePlan.steps)
            if (s.product != null) products.Add(s.product);
        var snap = new Snapshot(station, products);
        try
        {
            refinery.autoRefine = false;

            foreach (var t in new List<InventoryItemType>(snap.Tracked))
                if (t.itemCategory == ItemCategory.Ore || RefinedMaterialExtensions.IsCanister(t)) storage.Remove(t, storage.GetCount(t));
            if (Rollups.RefinableOre(refinery).Count > 0)
            {
                ctx.Skip("refine-plan", "ore left in docked cargo");
                yield break;
            }
            basePlan = Rollups.Build(forge, target, 1);
            InventoryItemType canister = null;
            foreach (var kv in basePlan.missing)
                if (RefinedMaterialExtensions.IsCanister(kv.Key)) { canister = kv.Key; break; }
            var material = canister != null ? RefinedMaterialExtensions.FromCanister(canister) : null;
            OreItemData ore = null;
            float bestYield = 0f;
            if (material != null)
            {
                foreach (var o in OreItemData.regularOres)
                {
                    float y = Rollups.YieldOf(o, material.Value);
                    if (!o.disableAutoRefine && y > bestYield) { ore = o; bestYield = y; }
                }
            }
            if (ore == null)
            {
                ctx.Skip("refine-plan", canister == null ? "the recipe needs no canisters" : $"no ore yields {canister.identifier}");
                yield break;
            }

            float mult = PlayerStation.GetRefineryYieldMultiplier(station);
            int need = basePlan.missing[canister];
            int added = Rollups.UnitsFor(need, refinery.GetPendingYield(material.Value), bestYield, mult) + 3;
            storage.Add(ore.item, added);
            // The same ore is topped up for every other missing material it yields, up to what's there.
            int expect = 0;
            foreach (var c in ore.contents)
            {
                var ci = c.product.GetCanisterItem();
                if (ci == null || !basePlan.missing.TryGetValue(ci, out int n)) continue;
                expect = Math.Max(expect, Rollups.UnitsFor(n, refinery.GetPendingYield(c.product), Rollups.YieldOf(ore, c.product), mult));
            }
            expect = Math.Min(expect, added);
            var plan = Rollups.Build(forge, target, 1);
            var r0 = plan.refines.Count > 0 ? plan.refines[0] : null;
            string pd = $"{station.name} {target.identifier}: base refines={basePlan.refines.Count}, missing {need} {canister.identifier}; added {added} {ore.item.identifier} (yield {bestYield:0.###} x{mult}); planned {plan.refines.Count} refine(s), {r0?.amount}x {r0?.ore.item.identifier} (expect {expect}); still missing {canister.identifier}={plan.missing.ContainsKey(canister)}; refinery {plan.refineSeconds:0}s, {plan.credits} cr";
            if (basePlan.refines.Count == 0 && plan.refines.Count == 1 && r0.ore == ore && r0.amount == expect && !plan.missing.ContainsKey(canister)) ctx.Pass("refine-plan", pd);
            else ctx.Fail("refine-plan", pd);
            yield return CheckPanel(ctx, "panel-refine", station, target, RollupPanel.StartLabel, "ore to refine");

            if (refinery.jobs.Count >= refinery.maxJobs || forge.jobs.Count >= forge.maxJobs)
            {
                ctx.Skip("refine-run", $"queue full (refinery {refinery.jobs.Count}/{refinery.maxJobs}, forge {forge.jobs.Count}/{forge.maxJobs})");
                yield break;
            }
            foreach (var kv in plan.missing) storage.Add(kv.Key, kv.Value);
            if (!GamePlayer.current.CanAfford(plan.credits)) GamePlayer.current.credits += plan.credits;
            var live = Rollups.Build(forge, target, 1);
            Rollups.Start(live);
            var started = live.refines.Count == 1 ? live.refines[0] : null;

            Rollups.Load();
            var loaded = Rollups.Active.Count == 1 ? Rollups.Active[0] : null;
            var lr = loaded != null && loaded.refines.Count == 1 ? loaded.refines[0] : null;
            string sd = $"refine started={started?.started}, job={(started?.job != null ? "linked" : "none")}; loaded refines={loaded?.refines.Count}, started={lr?.started}, same job={lr?.job != null && lr.job == started?.job}";
            if (started != null && started.started && started.job != null && lr != null && lr.started && lr.job == started.job) ctx.Pass("refine-save", sd);
            else ctx.Fail("refine-save", sd);
            if (loaded != null) live = loaded;
            yield return CheckPanel(ctx, "panel-refine-running", station, target, RollupPanel.StopLabel, "1 of 1 refinery jobs started");

            int ticks = 0;
            string firstWait = live.waiting;
            while (Rollups.Active.Contains(live) && ticks < 3000)
            {
                live.nextCheck = 0f;
                refinery.ProgressJobs(120f);
                forge.ProgressJobs(120f);
                ticks++;
                if (ticks % 50 == 0) yield return null;
            }
            ForgeJob finalJob = null;
            foreach (var job in forge.jobs)
                if (job.recipe == target && !snap.ForgeJobs.Contains(job)) finalJob = job;
            string rd = $"waiting at start={firstWait ?? "-"}; done after {ticks} x 120 s; target job {(finalJob != null ? "queued" : "missing")}; waiting={live.waiting ?? "-"}; refinery jobs {refinery.jobs.Count}";
            if (!Rollups.Active.Contains(live) && finalJob != null) ctx.Pass("refine-run", rd);
            else ctx.Fail("refine-run", rd);
        }
        finally { snap.Restore(); }
    }

    // Station storage counts (canisters, every ore, the given items), credits, queued jobs, the refinery's
    // carried-over yield and auto-refine, so a check can put the station back as it found it.
    private sealed class Snapshot
    {
        private readonly SpaceStation station;
        private readonly Dictionary<InventoryItemType, int> counts = new();
        private readonly long credits;
        public readonly HashSet<ForgeJob> ForgeJobs;
        private readonly HashSet<RefineryJob> refineJobs = new();
        private readonly float[] pending;
        private readonly bool autoRefine;

        public IEnumerable<InventoryItemType> Tracked => counts.Keys;

        public Snapshot(SpaceStation station, List<InventoryItemType> extra)
        {
            this.station = station;
            var storage = station.materialStorage;
            foreach (RefinedMaterial m in Enum.GetValues(typeof(RefinedMaterial)))
            {
                var c = m.GetCanisterItem();
                if (c != null) counts[c] = storage.GetCount(c);
            }
            foreach (var o in OreItemData.regularOres) counts[o.item] = storage.GetCount(o.item);
            foreach (var it in storage.items)
                if (it.item.itemCategory == ItemCategory.Ore) counts[it.item] = storage.GetCount(it.item);
            foreach (var t in extra) counts[t] = storage.GetCount(t);
            credits = GamePlayer.current.credits;
            ForgeJobs = new HashSet<ForgeJob>(station.forge.jobs);
            if (station.refinery != null)
            {
                foreach (var j in station.refinery.jobs) refineJobs.Add(j);
                pending = (float[])station.refinery.pendingYield.Clone();
                autoRefine = station.refinery.autoRefine;
            }
        }

        public void Restore()
        {
            Rollups.Active.Clear();
            Rollups.Save();
            var forge = station.forge;
            foreach (var job in new List<ForgeJob>(forge.jobs))
                if (!ForgeJobs.Contains(job)) forge.CancelJob(job);
            var refinery = station.refinery;
            if (refinery != null)
            {
                foreach (var job in new List<RefineryJob>(refinery.jobs))
                    if (!refineJobs.Contains(job)) refinery.CancelJob(job);
                Array.Copy(pending, refinery.pendingYield, pending.Length);
                refinery.autoRefine = autoRefine;
            }
            var storage = station.materialStorage;
            foreach (var kv in counts)
            {
                int diff = storage.GetCount(kv.Key) - kv.Value;
                if (diff > 0) storage.Remove(kv.Key, diff);
                else if (diff < 0) storage.Add(kv.Key, -diff);
            }
            GamePlayer.current.credits = credits;
        }
    }
}

[HarmonyPatch]
internal static class RollupPatches
{
    private static CraftRollup F => CraftRollup.Instance;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ForgeTabContents), nameof(ForgeTabContents.SetSelectedRecipe))]
    private static void SetSelectedRecipePostfix(ForgeTabContents __instance)
    {
        if (!F.Active) return;
        try
        {
            if (__instance.notCraftableContent.gameObject.activeSelf) RollupPanel.Refresh(__instance);
        }
        catch (Exception e) { F.Fail(e, nameof(RollupPatches)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Forge), nameof(Forge.ProgressJobs))]
    private static void ProgressJobsPostfix(Forge __instance)
    {
        if (!F.Active || Rollups.Active.Count == 0) return;
        try { Rollups.Tick(__instance); }
        catch (Exception e) { F.Fail(e, nameof(RollupPatches)); }
    }

    // Parts made for a roll-up stay at the station, where the forge can use them, even with "deposit in cargo" on.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ForgeJob), nameof(ForgeJob.AddForgeItemToCargo))]
    private static bool AddForgeItemToCargoPrefix(ForgeJob __instance, InventoryItemType item, int amount)
    {
        if (!F.Active || Rollups.Active.Count == 0) return true;
        try
        {
            if (!item.CanGoInMaterials() || !Rollups.IsPartJob(__instance)) return true;
            __instance.forge.spaceStation.materialStorage.Add(item, amount);
            return false;
        }
        catch (Exception e) { F.Fail(e, nameof(RollupPatches)); return true; }
    }

    // Set while a roll-up refinery job finishes one ore, so its canisters skip "deposit in cargo" like forge parts do.
    private static Refinery routing;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RefineryJob), nameof(RefineryJob.CompleteOneItem))]
    private static void CompleteOneItemPrefix(RefineryJob __instance)
    {
        routing = null;
        if (!F.Active || Rollups.Active.Count == 0) return;
        try
        {
            if (Rollups.IsRefineJob(__instance)) routing = __instance.parent;
        }
        catch (Exception e) { F.Fail(e, nameof(RollupPatches)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RefineryJob), nameof(RefineryJob.CompleteOneItem))]
    private static void CompleteOneItemPostfix() => routing = null;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Refinery), nameof(Refinery.DepositOutputItem))]
    private static bool DepositOutputItemPrefix(Refinery __instance, InventoryItemType item, int amount)
    {
        if (!F.Active || routing != __instance) return true;
        try
        {
            if (!RefinedMaterialExtensions.IsCanister(item)) return true;
            var storage = __instance.spaceStation.materialStorage;
            if (storage.IsFull(item.m3 * amount)) return true;
            storage.Add(item, amount);
            return false;
        }
        catch (Exception e) { F.Fail(e, nameof(RollupPatches)); return true; }
    }
}
