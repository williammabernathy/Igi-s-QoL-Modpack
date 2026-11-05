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
using LightJson;
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
    public override string Title => "Crafting Roll-Up";
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

        yield return CheckPanel(ctx, "panel", station, target, RollupPanel.StartLabel, "Costs ");
        yield return CheckLayout(ctx, station);
        yield return CheckRun(ctx, station, target, plan);
        yield return CheckReserve(ctx, station, target, plan);
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
        ForgePopup.Open(prefab, station, target, false);
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

    // The recipe with the most ingredients that can't be crafted yet: its ingredient list reaches furthest down the
    // details pane. The roll-up text must fit its strip without overflowing and keep off every ingredient row. The
    // popup has room for it; the station's own forge screen is shorter, which the second pass copies by shrinking the
    // popup until the last ingredient row reaches into the strip: the summary must hide there instead.
    private static IEnumerator CheckLayout(SelfCheckContext ctx, SpaceStation station)
    {
        var prefab = ForgePrefab();
        var forge = station.forge;
        CraftingRecipe recipe = null;
        int most = 0;
        foreach (var r in forge.recipes)
        {
            if (r == null || r.CountAvailableForCrafting(forge) >= 1) continue;
            int n = 0;
            foreach (var _ in r.GetIngredientMaterials()) n++;
            foreach (var _ in r.GetIngredientItems()) n++;
            if (n > most) { most = n; recipe = r; }
        }
        if (prefab == null || recipe == null) { ctx.Skip("panel-layout", $"forge screen {prefab != null}, uncraftable recipe {recipe != null}"); yield break; }
        ForgePopup.Open(prefab, station, recipe, false);
        yield return null;
        yield return null;
        try
        {
            var tab = ForgeUI.current != null ? ForgeUI.current.GetComponentInChildren<ForgeTabContents>() : null;
            var panel = tab != null ? tab.notCraftableContent.GetComponentInChildren<RollupPanel>() : null;
            if (panel == null) { ctx.Fail("panel-layout", $"{recipe.identifier}: no roll-up panel"); yield break; }
            bool ok = Measure(tab, panel, out string detail, out float intrude);
            detail = $"{recipe.identifier}: {most} ingredients; {detail}";
            if (ok && panel.SummaryShown) ctx.Pass("panel-layout", detail);
            else ctx.Fail("panel-layout", detail);
            yield return ctx.Screenshot("forge-rollup-layout");

            var screen = ForgeUI.current.transform.parent.GetComponent<UnityEngine.UI.LayoutElement>();
            var root = (RectTransform)ForgeUI.current.transform.root;
            float scale = ForgeUI.current.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
            if (screen == null) { ctx.Skip("panel-layout-short", "the forge popup has no layout element to shrink"); yield break; }
            float saved = screen.preferredHeight;
            screen.preferredHeight = saved - intrude / scale;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            yield return null;
            Canvas.ForceUpdateCanvases();
            panel.Show();
            bool shortOk = Measure(tab, panel, out string shortDetail, out _);
            shortDetail = $"height {saved:0} -> {screen.preferredHeight:0}; {shortDetail}";
            if (shortOk && !panel.SummaryShown) ctx.Pass("panel-layout-short", shortDetail);
            else ctx.Fail("panel-layout-short", shortDetail);
            yield return ctx.Screenshot("forge-rollup-layout-short");
        }
        finally { ForgePopup.Close(); }
        yield return null;
    }

    // Rectangles in screen pixels. Fine when the summary is hidden, or shown without overflowing, inside the strip
    // and clear of every ingredient row and the result list. intrude: how much shorter the screen would need to be for
    // the last row to reach a row's height into the strip.
    private static bool Measure(ForgeTabContents tab, RollupPanel panel, out string detail, out float intrude)
    {
        Canvas.ForceUpdateCanvases();
        var strip = ScreenRect(tab.notCraftableContent);
        var results = ScreenRect((RectTransform)tab.resultsList.transform);
        var sb = new System.Text.StringBuilder();
        Rect text = default;
        bool shown = panel.SummaryShown;
        if (shown)
        {
            panel.summary.ForceMeshUpdate();
            var bounds = panel.summary.textBounds;
            var st = panel.summary.transform;
            Vector2 a = st.TransformPoint(bounds.min), b = st.TransformPoint(bounds.max);
            text = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
        int overlaps = shown && results.Overlaps(text) ? 1 : 0;
        Rect last = default;
        foreach (var row in tab.ingredientsList.rows)
        {
            var rr = ScreenRect((RectTransform)row.transform);
            bool hit = shown && rr.Overlaps(text);
            if (hit) overlaps++;
            last = rr;
            sb.Append($" [{rr.yMin:0}-{rr.yMax:0}{(hit ? " overlaps" : "")}]");
        }
        intrude = last.yMin - strip.yMax + last.height;
        detail = $"strip y {strip.yMin:0}-{strip.yMax:0}; summary {(shown ? $"y {text.yMin:0}-{text.yMax:0}, {panel.summary.textInfo.lineCount} lines at {panel.summary.fontSize:0.#} pt, overflowing {panel.summary.isTextOverflowing}" : "hidden")}; results y {results.yMin:0}-{results.yMax:0}; rows{sb} | {panel.summary.text.Replace("\n", " | ")}";
        return !shown || !panel.summary.isTextOverflowing && overlaps == 0 && text.yMax <= strip.yMax + 1f;
    }

    private static Rect ScreenRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
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
        if (Rollups.Find(forge, target) != null)
        {
            ctx.Skip("run", $"a roll-up for {target.identifier} is already running at {station.name}");
            yield break;
        }

        // Snapshot what the run touches, so it can be put back.
        var storage = station.materialStorage;
        var products = new List<InventoryItemType>();
        foreach (var s in plan.steps)
            if (s.product != null) products.Add(s.product);
        var snap = new Snapshot(station, products);
        var jobsBefore = snap.ForgeJobs;

        // The player's own roll-ups hold stock for their unstarted steps, so the plan below would come up short of what the
        // check adds. Set them aside; Restore puts them back from the snapshot's saved record.
        Rollups.Active.Clear();
        foreach (var kv in plan.missing) storage.Add(kv.Key, kv.Value);
        int plansBefore = Rollups.Active.Count;
        var live = Rollups.Build(forge, target, 1);
        Rollups.Start(live);
        int startedAtOnce = live.StartedCount;

        // Save round trip: the record holds the plan and Load brings it back with its jobs linked.
        var record = SaveData.Feature(Rollups.FeatureKey, false)?["plans"];
        int savedCount = record.HasValue && record.Value.IsJsonArray ? record.Value.AsJsonArray.Count : -1;
        Rollups.Load();
        var loaded = Rollups.Find(forge, target);
        bool saveOk = savedCount == plansBefore + 1 && Rollups.Active.Count == plansBefore + 1 && loaded != null && loaded.steps.Count == live.steps.Count && loaded.StartedCount == startedAtOnce;
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

        yield return CheckPanel(ctx, "panel-running", station, target, RollupPanel.StopLabel, Rollups.RunningLabel);

        ForgeJob partJob = null;
        foreach (var s in live.steps)
            if (s.started && s != live.steps[live.steps.Count - 1]) { partJob = s.job; break; }
        bool partRouted = partJob != null && Rollups.IsPartJob(partJob);

        // With "deposit in cargo" on and the player docked here, vanilla would drop the parts into the flagship's hold;
        // only then does the routing prefix change anything. Otherwise vanilla uses the storage anyway and nothing is proven.
        var player = GamePlayer.current;
        bool depositWas = player.forgeDepositInCargo;
        bool othersQueued = snap.OtherJobsQueued;
        if (!othersQueued) player.forgeDepositInCargo = true;
        bool routable = !othersQueued && station.CanDepositInDockedCargo(1f);
        var partProducts = new List<InventoryItemType>();
        for (int i = 0; i < live.steps.Count - 1; i++)
            if (live.steps[i].product != null && live.steps[i].product.CanGoInMaterials()) partProducts.Add(live.steps[i].product);
        var cargoBefore = CargoCounts(partProducts);
        int leaked;
        int ticks = 0;
        try
        {
            while (Rollups.Active.Contains(live) && ticks < 600)
            {
                live.nextCheck = 0f;
                station.refinery?.ProgressJobs(30f);
                forge.ProgressJobs(30f);
                ticks++;
                if (ticks % 25 == 0) yield return null;
            }
        }
        finally
        {
            player.forgeDepositInCargo = depositWas;
            leaked = othersQueued ? 0 : TakeBackFromCargo(cargoBefore);
        }
        ForgeJob finalJob = null;
        foreach (var job in forge.jobs)
            if (job.recipe == target && !jobsBefore.Contains(job)) finalJob = job;
        string rd = $"{live.steps.Count} jobs, {startedAtOnce} started at once, done after {ticks} x 30 s of forge time; target job {(finalJob != null ? "queued" : "missing")}; waiting={live.waiting ?? "-"}";
        if (!Rollups.Active.Contains(live) && finalJob != null) ctx.Pass("run", rd);
        else ctx.Fail("run", rd);

        string pd = $"part job marked={partRouted}, target job marked={(finalJob != null && Rollups.IsPartJob(finalJob))}, parts in flagship hold={leaked}";
        if (!(partRouted && finalJob != null && !Rollups.IsPartJob(finalJob))) ctx.Fail("parts-to-storage", pd);
        else if (!routable) ctx.Skip("parts-to-storage", othersQueued ? $"jobs of your own are queued here, so the check doesn't force \"deposit in cargo\" ({pd})" : $"vanilla would use the station storage anyway: not docked at {station.name} or the flagship hold is full ({pd})");
        else if (leaked > 0) ctx.Fail("parts-to-storage", pd);
        else ctx.Pass("parts-to-storage", pd);

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
            // A station without the part's own ingredients can't redo it: the plan then rightly waits for the first of those.
            string firstInput = FirstShortInput(station, cancelled.recipe, cancelled.amount);
            bool namesPart = stall.waiting != null && stall.waiting.EndsWith(part);
            bool namesInput = stall.waiting != null && firstInput != null && stall.waiting.EndsWith(firstInput);
            string std = $"cancelled {Rollups.RecipeName(cancelled.recipe)} job; waiting={stall.waiting ?? "-"}; names the part '{part}'={namesPart}; first short input of that recipe={firstInput ?? "none"}, named={namesInput}";
            if (namesPart || namesInput) ctx.Pass("stalled-part", std);
            else ctx.Fail("stalled-part", std);
        }
        finally { snap.Restore(); }
    }

    private static void Verdict(SelfCheckContext ctx, bool ok, string name, string detail)
    {
        if (ok) ctx.Pass(name, detail);
        else ctx.Fail(name, detail);
    }

    // The first ingredient of a recipe batch the station is short of, as the roll-up would word it ("10 Titanium"); null when none.
    private static string FirstShortInput(SpaceStation station, CraftingRecipe recipe, int amount)
    {
        foreach (var (material, per) in recipe.GetIngredientMaterials())
        {
            if (per <= 0f) continue;
            var canister = material.GetCanisterItem();
            int shortBy = CraftingRecipe.CountCanistersForBatch(per, amount) - station.CountLocalItems(canister);
            if (shortBy > 0) return Rollups.Name(canister);
        }
        foreach (var (item, count) in recipe.GetIngredientItems())
            if (count * amount - station.CountForgeItems(item) > 0) return Rollups.Name(item);
        return null;
    }

    // A second roll-up at the station plans against what the first still needs. Plan A starts its parts and waits with
    // the target recipe; plans for other recipes are then built and compared with the same plans built as if A were not
    // there (the old behaviour): the canister A's waiting step needs must show up as missing, and a part A has queued
    // must count as on its way.
    private static IEnumerator CheckReserve(SelfCheckContext ctx, SpaceStation station, CraftingRecipe target, RollupPlan plan)
    {
        var forge = station.forge;
        foreach (var kv in plan.missing)
        {
            if (kv.Key.identifier.StartsWith("Canister")) continue;
            ctx.Skip("reserve", $"needs {kv.Key.identifier}, which no recipe makes");
            yield break;
        }
        if (plan.PartCount == 0) { ctx.Skip("reserve", $"{target.identifier} needs no parts, so nothing waits"); yield break; }
        // One free slot is enough: A starts a part there and the rest of A waits.
        if (forge.jobs.Count >= forge.maxJobs) { ctx.Skip("reserve", $"forge queue full ({forge.jobs.Count}/{forge.maxJobs})"); yield break; }
        if (!GamePlayer.current.CanAfford(plan.credits)) { ctx.Skip("reserve", $"can't afford {plan.credits} cr"); yield break; }
        if (Rollups.Find(forge, target) != null) { ctx.Skip("reserve", $"a roll-up for {target.identifier} is already running at {station.name}"); yield break; }

        var storage = station.materialStorage;
        var products = new List<InventoryItemType>();
        foreach (var s in plan.steps)
            if (s.product != null) products.Add(s.product);
        foreach (var r in forge.recipes)
            foreach (var (item, _) in r.GetIngredientItems()) products.Add(item);
        var snap = new Snapshot(station, products);
        try
        {
            foreach (var kv in plan.missing) storage.Add(kv.Key, kv.Value);
            var a = Rollups.Build(forge, target, 1);
            Rollups.Start(a);
            var last = a.steps[a.steps.Count - 1];
            RollupStep inFlight = null;
            for (int i = 0; i < a.steps.Count - 1; i++)
                if (a.steps[i].started && a.steps[i].job != null && forge.jobs.Contains(a.steps[i].job)) { inFlight = a.steps[i]; break; }
            if (last.started || inFlight == null)
            {
                ctx.Skip("reserve", $"plan A did not wait with a part queued (target started={last.started}, part queued={inFlight != null}, waiting={a.waiting ?? "-"})");
                yield break;
            }
            var net = Rollups.Committed(station, null);

            // Shared canister: the first canister A's waiting target step needs that another recipe also uses.
            CraftingRecipe other = null;
            RefinedMaterial shared = default;
            float sharedPer = 0f;
            foreach (var (material, _) in target.GetIngredientMaterials())
            {
                var ci = material.GetCanisterItem();
                if (ci == null || !net.TryGetValue(ci, out int held) || held <= 0) continue;
                foreach (var r in forge.recipes)
                {
                    if (r == null || r == target || !r.craftingEnabled) continue;
                    foreach (var (m2, per2) in r.GetIngredientMaterials())
                        if (m2 == material && per2 > 0f) { other = r; shared = material; sharedPer = per2; break; }
                    if (other != null) break;
                }
                if (other != null) break;
            }
            if (other == null) ctx.Skip("reserve-shared", $"{target.identifier} has no canister input that another recipe at {station.name} also uses");
            else
            {
                var canister = shared.GetCanisterItem();
                int held = net[canister];
                // Make sure the station holds what A is counting on, so the comparison isn't against an empty hold.
                if (station.CountLocalItems(canister) < held) storage.Add(canister, held - station.CountLocalItems(canister));
                int stock = station.CountLocalItems(canister);
                int n = 1;
                while (n < 1000 && CraftingRecipe.CountCanistersForBatch(sharedPer, n) <= Math.Max(0, stock - held)) n++;
                int need = CraftingRecipe.CountCanistersForBatch(sharedPer, n);
                var withA = Rollups.Build(forge, other, n);
                var saved = new List<RollupPlan>(Rollups.Active);
                Rollups.Active.Clear();
                RollupPlan without;
                try { without = Rollups.Build(forge, other, n); }
                finally { Rollups.Active.AddRange(saved); }
                int Cover(RollupPlan p)
                {
                    p.missing.TryGetValue(canister, out int m);
                    return m;
                }
                bool refined = withA.refines.Count > 0 || without.refines.Count > 0;
                int expectWith = need - Math.Max(0, stock - held), expectWithout = Math.Max(0, need - stock);
                bool ok = refined
                    ? withA.refines.Count > without.refines.Count || Cover(withA) > Cover(without)
                    : Cover(withA) == expectWith && Cover(without) == expectWithout && expectWith > expectWithout;
                Verdict(ctx, ok, "reserve-shared", $"{other.identifier} x{n} needs {need} {canister.identifier}; station holds {stock}, A's waiting {target.identifier} still takes {held}; still missing {Cover(withA)} with A running (expect {(refined ? "more than without" : expectWith.ToString())}), {Cover(without)} without (expect {(refined ? "-" : expectWithout.ToString())}); refines {withA.refines.Count} vs {without.refines.Count}");
            }

            // Part in flight: a plan for a recipe that uses A's queued part sees the leftover from A's batch, not the whole need.
            var part = inFlight.product;
            CraftingRecipe user = null;
            int userCount = 0;
            foreach (var r in forge.recipes)
            {
                if (r == null || r == target || !r.craftingEnabled) continue;
                foreach (var (item, count) in r.GetIngredientItems())
                    if (item == part) { user = r; userCount = count; break; }
                if (user != null) break;
            }
            if (part == null || user == null) ctx.Skip("reserve-part", $"no other recipe at {station.name} uses {(part != null ? part.identifier : "the queued part")}");
            else
            {
                int per = 1;
                foreach (var row in inFlight.recipe.RawResults)
                    if (row.item != null && ItemDefinition.TemplateOf(row.item) == part) per = Math.Max(1, row.count);
                int taken = 0;
                foreach (var s in a.steps)
                {
                    if (s.started) continue;
                    foreach (var (item, count) in s.recipe.GetIngredientItems())
                        if (item == part) taken += count * s.amount;
                }
                int made = per * inFlight.amount;
                int stock = station.CountForgeItems(part);
                int avail = Math.Max(0, stock - (taken - made));
                int n = Math.Max(1, avail / userCount);
                int need = userCount * n;
                int expectCrafts = Math.Max(0, (need - avail + per - 1) / per);
                var planB = Rollups.Build(forge, user, n);
                var stepB = planB.steps.Find(s => s.recipe == inFlight.recipe);
                int gotCrafts = stepB?.amount ?? 0;
                Verdict(ctx, gotCrafts == expectCrafts, "reserve-part", $"{user.identifier} x{n} needs {need} {part.identifier}; station holds {stock}, A's queue makes {made} and A's unstarted steps take {taken}; plans {gotCrafts} x {Rollups.RecipeName(inFlight.recipe)} (expect {expectCrafts}; without the in-flight batch {Math.Max(0, (need - stock + per - 1) / per)})");
            }
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
        if (Rollups.Find(forge, target) != null)
        {
            ctx.Skip("refine-plan", $"a roll-up for {target.identifier} is already running at {station.name}");
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
            var loaded = Rollups.Find(forge, target);
            var lr = loaded != null && loaded.refines.Count == 1 ? loaded.refines[0] : null;
            string sd = $"refine started={started?.started}, job={(started?.job != null ? "linked" : "none")}; loaded refines={loaded?.refines.Count}, started={lr?.started}, same job={lr?.job != null && lr.job == started?.job}";
            if (started != null && started.started && started.job != null && lr != null && lr.started && lr.job == started.job) ctx.Pass("refine-save", sd);
            else ctx.Fail("refine-save", sd);
            if (loaded != null) live = loaded;
            yield return CheckPanel(ctx, "panel-refine-running", station, target, RollupPanel.StopLabel, "1 of 1 refinery jobs started");

            // With "deposit in cargo" on and the player docked here, vanilla would drop the canisters into the flagship's hold;
            // only then does the routing prefix change anything. Otherwise vanilla uses the storage anyway and nothing is proven.
            var player = GamePlayer.current;
            bool depositWas = player.forgeDepositInCargo;
            bool othersQueued = snap.OtherJobsQueued;
            if (!othersQueued) player.forgeDepositInCargo = true;
            bool routable = !othersQueued && station.CanDepositInDockedCargo(1f);
            var canisters = AllCanisters();
            var cargoBefore = CargoCounts(canisters);
            int leaked;
            int ticks = 0;
            string firstWait = live.waiting;
            try
            {
                while (Rollups.Active.Contains(live) && ticks < 3000)
                {
                    live.nextCheck = 0f;
                    refinery.ProgressJobs(120f);
                    forge.ProgressJobs(120f);
                    ticks++;
                    if (ticks % 50 == 0) yield return null;
                }
            }
            finally
            {
                player.forgeDepositInCargo = depositWas;
                leaked = othersQueued ? 0 : TakeBackFromCargo(cargoBefore);
            }
            ForgeJob finalJob = null;
            foreach (var job in forge.jobs)
                if (job.recipe == target && !snap.ForgeJobs.Contains(job)) finalJob = job;
            string rd = $"waiting at start={firstWait ?? "-"}; done after {ticks} x 120 s; target job {(finalJob != null ? "queued" : "missing")}; waiting={live.waiting ?? "-"}; refinery jobs {refinery.jobs.Count}";
            if (!Rollups.Active.Contains(live) && finalJob != null) ctx.Pass("refine-run", rd);
            else ctx.Fail("refine-run", rd);

            if (!routable) ctx.Skip("refine-to-storage", othersQueued ? "jobs of your own are queued here, so the check doesn't force \"deposit in cargo\"" : $"vanilla would use the station storage anyway: not docked at {station.name} or the flagship hold is full");
            else if (leaked == 0) ctx.Pass("refine-to-storage", $"deposit in cargo on, docked here: no canister reached the flagship hold");
            else ctx.Fail("refine-to-storage", $"{leaked} canister(s) went to the flagship hold instead of the station storage");
        }
        finally { snap.Restore(); }
    }

    // Flagship cargo counts of the given items, to prove output went to the station and not the hold.
    private static Dictionary<InventoryItemType, int> CargoCounts(IEnumerable<InventoryItemType> items)
    {
        var cargo = GamePlayer.current.currentSpaceShip.cargo;
        var counts = new Dictionary<InventoryItemType, int>();
        foreach (var t in items) counts[t] = cargo.GetCount(t);
        return counts;
    }

    // How many units of those items arrived in the flagship's hold since CargoCounts; takes them back out so a failing run leaves no strays.
    private static int TakeBackFromCargo(Dictionary<InventoryItemType, int> before)
    {
        var cargo = GamePlayer.current.currentSpaceShip.cargo;
        int extra = 0;
        foreach (var kv in before)
        {
            int now = cargo.GetCount(kv.Key);
            if (now <= kv.Value) continue;
            cargo.Remove(kv.Key, now - kv.Value);
            extra += now - kv.Value;
        }
        return extra;
    }

    private static List<InventoryItemType> AllCanisters()
    {
        var list = new List<InventoryItemType>();
        foreach (RefinedMaterial m in Enum.GetValues(typeof(RefinedMaterial)))
        {
            var c = m.GetCanisterItem();
            if (c != null) list.Add(c);
        }
        return list;
    }

    // Station storage (canisters, every ore, the given items, with each stack's favourite flag), credits, queued jobs,
    // the player's roll-ups, the refinery's carried-over yield and auto-refine, so a check can put the station back as it found it.
    private sealed class Snapshot
    {
        private readonly SpaceStation station;
        private readonly Dictionary<InventoryItemType, List<(int count, bool favourite)>> stacks = new();
        private readonly long credits;
        public readonly HashSet<ForgeJob> ForgeJobs;
        private readonly HashSet<RefineryJob> refineJobs = new();
        private readonly float[] pending;
        private readonly bool autoRefine;
        private readonly string plansJson;

        public IEnumerable<InventoryItemType> Tracked => stacks.Keys;

        // Jobs of the player's own that were queued before the check. The routing checks progress every job, so with
        // "deposit in cargo" forced on their output would land in the hold, where the check can't tell it from a leak.
        public bool OtherJobsQueued => ForgeJobs.Count > 0 || refineJobs.Count > 0;

        public Snapshot(SpaceStation station, List<InventoryItemType> extra)
        {
            this.station = station;
            var storage = station.materialStorage;
            foreach (RefinedMaterial m in Enum.GetValues(typeof(RefinedMaterial)))
            {
                var c = m.GetCanisterItem();
                if (c != null) Track(storage, c);
            }
            foreach (var o in OreItemData.regularOres) Track(storage, o.item);
            foreach (var it in new List<Inventory.InventoryItem>(storage.items))
                if (it.item.itemCategory == ItemCategory.Ore) Track(storage, it.item);
            foreach (var t in extra) Track(storage, t);
            credits = GamePlayer.current.credits;
            ForgeJobs = new HashSet<ForgeJob>(station.forge.jobs);
            if (station.refinery != null)
            {
                foreach (var j in station.refinery.jobs) refineJobs.Add(j);
                pending = (float[])station.refinery.pendingYield.Clone();
                autoRefine = station.refinery.autoRefine;
            }
            var plans = SaveData.Feature(Rollups.FeatureKey, false)?["plans"];
            plansJson = plans.HasValue && plans.Value.IsJsonArray ? plans.Value.ToString() : null;
        }

        private void Track(Inventory storage, InventoryItemType t)
        {
            if (stacks.ContainsKey(t)) return;
            var list = new List<(int, bool)>();
            foreach (var s in storage.items)
                if (s.item == t) list.Add((s.count, s.favourite));
            stacks[t] = list;
        }

        public void Restore()
        {
            Rollups.Active.Clear();
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
            // Put the player's own roll-ups back (after the test jobs are gone, so they relink to their own jobs), not an empty list.
            if (plansJson == null) Rollups.Save();
            else
            {
                SaveData.Feature(Rollups.FeatureKey, true)["plans"] = JsonValue.Parse(plansJson);
                Rollups.Load();
            }
            var storage = station.materialStorage;
            foreach (var kv in stacks)
            {
                int want = 0;
                foreach (var (count, _) in kv.Value) want += count;
                if (storage.GetCount(kv.Key) == want) continue;
                // Rebuilt stack by stack so the favourite flags survive.
                storage.Remove(kv.Key, storage.GetCount(kv.Key));
                foreach (var (count, favourite) in kv.Value)
                {
                    var stack = storage.Add(kv.Key, count, stack: true);
                    if (stack != null) stack.favourite = favourite;
                }
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
