using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Behaviour;
using Behaviour.Equipment.Turret;
using Behaviour.Item;
using Behaviour.UI.HUD;
using Behaviour.UI.Side_Menu;
using Behaviour.UI.Tooltip;
using Behaviour.UI.Travel;
using Behaviour.Util;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using IgiQoL.Game;
using Source.Item;
using Source.Player;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.Core;

public sealed class DevFeature : Feature
{
    public static DevFeature Instance;

    public DevFeature() { Instance = this; }

    public override string Id => "Dev";
    public override string Description => "Developer harness: auto-load a save, run self-checks, dump UI hierarchy, screenshot, quit. Suppresses autosaves while enabled.";
    public override Type[] PatchClasses => new[] { typeof(DevAutosavePatches) };

    public ConfigEntry<string> AutoLoadSave;
    public ConfigEntry<bool> RunSelfCheck;
    public ConfigEntry<string> OnlyChecks;
    public ConfigEntry<string> DumpHierarchy;
    public ConfigEntry<bool> Screenshot;
    public ConfigEntry<int> QuitAfterSeconds;
    public ConfigEntry<bool> DumpIcons;
    public ConfigEntry<bool> DumpTurretRanges;
    public ConfigEntry<bool> DumpRecipes;
    public ConfigEntry<bool> DumpMissiles;
    public ConfigEntry<string> DumpStatics;
    public ConfigEntry<bool> DumpGameData;
    public ConfigEntry<int> EchoSoakSeconds;
    public ConfigEntry<int> ProfileSeconds;
    public ConfigEntry<int> ProfileEnemies;

    private bool autoLoadDone;
    private bool harnessStarted;
    internal bool autosaveLogged;

    public override void BindConfig(ConfigFile cfg)
    {
        Enabled = cfg.Bind(Id, "Enabled", false, Description);
        AutoLoadSave = cfg.Bind(Id, "AutoLoadSave", "", "Save name (without extension) to load automatically at the main menu");
        RunSelfCheck = cfg.Bind(Id, "SelfCheck", false, "Run every active feature's self-check after the save is loaded");
        OnlyChecks = cfg.Bind(Id, "OnlyChecks", "", "Comma-separated feature ids whose self-checks run (empty = every active feature)");
        DumpHierarchy = cfg.Bind(Id, "DumpHierarchy", "", "Comma-separated component type names whose UI hierarchy is dumped to BepInEx/config/IgiQoL/dumps");
        Screenshot = cfg.Bind(Id, "Screenshot", false, "Write igiqol-screenshot.png to the game's data folder after the checks");
        QuitAfterSeconds = cfg.Bind(Id, "QuitAfterSeconds", 0, "Quit the game this many seconds after the harness finishes (0 = never)");
        DumpIcons = cfg.Bind(Id, "DumpIcons", false, "Write every icon key of the game's icon library to dumps/icons.txt");
        DumpTurretRanges = cfg.Bind(Id, "DumpTurretRanges", false, "Write identifier, rarity, item level and base range of every turret item to dumps/turret-ranges.txt");
        DumpRecipes = cfg.Bind(Id, "DumpRecipes", false, "Write every forge recipe with its ingredients, results and the recipe that makes each item ingredient to dumps/recipes.txt");
        DumpMissiles = cfg.Bind(Id, "DumpMissiles", false, "Write every missile launcher's missile flight values and your ships' thrust and mass to dumps/missiles.txt");
        DumpStatics = cfg.Bind(Id, "DumpStatics", "", "Comma-separated TypeName.staticMember[:depth] entries (default depth 3) dumped to dumps/<game version>/<TypeName>.<member>.txt for `VersionDiff data`");
        DumpGameData = cfg.Bind(Id, "DumpGameData", false, "Dump the game's content registries (items, recipes, ships, drones, crew, skills, ...) to dumps/<game version>/ for `VersionDiff data`");
        EchoSoakSeconds = cfg.Bind(Id, "EchoSoakSeconds", 0, "After the checks, let ECHO play this many seconds, logging its status every 15 s and the event log at the end (0 = off)");
        ProfileSeconds = cfg.Bind(Id, "ProfileSeconds", 0, "After the checks, spawn ProfileEnemies hostile ships next to the fleet and measure frame times, this mod's patch and update costs and the game's own sections for this many seconds; report in dumps/profile.txt (0 = off)");
        ProfileEnemies = cfg.Bind(Id, "ProfileEnemies", 12, "Hostile ships the profile spawns (0 = profile wherever the save is, without a fight)");
    }

    public override void OnActivated()
    {
        GameEvents.MainMenuShown += OnMainMenuShown;
        GameEvents.SaveLoaded += OnSaveLoaded;
    }

    private void OnMainMenuShown()
    {
        if (!Active || autoLoadDone || string.IsNullOrEmpty(AutoLoadSave.Value)) return;
        autoLoadDone = true;
        try
        {
            var file = SaveGame.GetSaveGame(AutoLoadSave.Value);
            if (file == null)
            {
                Plugin.Log.LogWarning($"[Dev] save '{AutoLoadSave.Value}' not found");
                return;
            }
            Plugin.Log.LogInfo($"[Dev] loading save '{AutoLoadSave.Value}'");
            PersistentSingleton<GameManager>.Instance.LoadGame(file);
        }
        catch (Exception e) { Fail(e, nameof(OnMainMenuShown)); }
    }

    private void OnSaveLoaded()
    {
        if (harnessStarted || !Active) return;
        harnessStarted = true;
        Plugin.Instance.StartCoroutine(RunHarness());
    }

    private IEnumerator RunHarness()
    {
        float deadline = Time.realtimeSinceStartup + 60f;
        bool ready = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (Fleet.Flagship != null && HudManager.Instance != null && TravelInfo.instance != null)
            {
                ready = true;
                break;
            }
            yield return null;
        }

        var cleanups = new List<Action>();
        int pass = 0, fail = 0, skip = 0;
        if (!ready)
        {
            Plugin.Log.LogInfo("CHECK Dev/ready FAIL - game not ready after 60 s");
            fail++;
        }
        else
        {
            yield return new WaitForSecondsRealtime(3f);
            var config = Plugin.Instance.Config;
            bool saveOnSet = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;

            if (RunSelfCheck.Value)
            {
                foreach (var feature in FeatureRegistry.All)
                {
                    if (!feature.Active || !Selected(feature.Id)) continue;
                    var ctx = new SelfCheckContext { FeatureId = feature.Id };
                    yield return Protected(feature.SelfCheck(ctx), ctx);
                    pass += ctx.PassCount;
                    fail += ctx.FailCount;
                    skip += ctx.SkipCount;
                    cleanups.AddRange(ctx.Cleanups);
                }
            }

            if (!string.IsNullOrEmpty(DumpHierarchy.Value))
            {
                foreach (var name in DumpHierarchy.Value.Split(','))
                {
                    var trimmed = name.Trim();
                    if (trimmed.Length == 0) continue;
                    try { Dump(trimmed); }
                    catch (Exception e) { Plugin.Log.LogError($"[Dev] dump of {trimmed} failed: {e}"); }
                }
            }

            if (DumpIcons.Value)
            {
                try { DumpIconKeys(); }
                catch (Exception e) { Plugin.Log.LogError($"[Dev] icon dump failed: {e}"); }
            }

            if (DumpTurretRanges.Value)
            {
                try { DumpTurrets(); }
                catch (Exception e) { Plugin.Log.LogError($"[Dev] turret range dump failed: {e}"); }
            }

            if (DumpRecipes.Value)
            {
                try { DumpAllRecipes(); }
                catch (Exception e) { Plugin.Log.LogError($"[Dev] recipe dump failed: {e}"); }
            }

            if (DumpMissiles.Value)
            {
                try { DumpMissileValues(); }
                catch (Exception e) { Plugin.Log.LogError($"[Dev] missile dump failed: {e}"); }
            }

            if (DumpGameData.Value || !string.IsNullOrEmpty(DumpStatics.Value))
            {
                try { DevDump.Run(DumpStatics.Value, DumpGameData.Value); }
                catch (Exception e) { Plugin.Log.LogError($"[Dev] data dump failed: {e}"); }
            }

            if (Screenshot.Value)
            {
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                string path = Application.persistentDataPath + "/igiqol-screenshot.png";
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForSecondsRealtime(2f);
                Plugin.Log.LogInfo($"[Dev] screenshot -> {path}");
            }

            foreach (var cleanup in cleanups)
            {
                try { cleanup(); }
                catch (Exception e) { Plugin.Log.LogError($"[Dev] cleanup failed: {e}"); }
            }
            config.SaveOnConfigSet = saveOnSet;

            var devCtx = new SelfCheckContext { FeatureId = Id };
            if (ProfileSeconds.Value > 0) yield return Protected(DevProfile.Run(ProfileSeconds.Value, ProfileEnemies.Value), devCtx);
            if (EchoSoakSeconds.Value > 0) yield return Protected(Soak(EchoSoakSeconds.Value), devCtx);
            fail += devCtx.FailCount;
        }

        Plugin.Log.LogInfo($"Self-check complete: {pass} pass, {fail} fail, {skip} skip");
        if (QuitAfterSeconds.Value > 0)
        {
            yield return new WaitForSecondsRealtime(QuitAfterSeconds.Value);
            Application.Quit();
        }
    }

    private bool Selected(string id)
    {
        string only = OnlyChecks.Value;
        if (string.IsNullOrEmpty(only)) return true;
        foreach (var part in only.Split(','))
            if (part.Trim() == id) return true;
        return false;
    }

    // Runs a self-check, including the coroutines it yields, so an exception anywhere fails the check instead of
    // stopping the harness; the iterators it abandons are disposed so their finally blocks still restore things.
    private static IEnumerator Protected(IEnumerator routine, SelfCheckContext ctx)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        while (stack.Count > 0)
        {
            object current;
            try
            {
                if (!stack.Peek().MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                current = stack.Peek().Current;
            }
            catch (Exception e)
            {
                ctx.Fail("exception", $"{e.GetType().Name}: {e.Message}");
                Plugin.Log.LogError(e);
                while (stack.Count > 0)
                {
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception d) { Plugin.Log.LogError(d); }
                }
                break;
            }
            if (current is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            yield return current;
        }
    }

    // ECHO plays for a while; what it did ends up in the BepInEx log.
    private static IEnumerator Soak(int seconds)
    {
        var player = GamePlayer.current;
        var tab = SidePanel.instance != null ? SidePanel.instance.autopilotTab : null;
        if (player == null || tab == null)
        {
            Plugin.Log.LogInfo("[Dev] soak skipped: no player or Autopilot tab");
            yield break;
        }
        if (!player.autoPlay) tab.ToggleChanged();
        Plugin.Log.LogInfo($"[Dev] soak: ECHO on={player.autoPlay} for {seconds} s");
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
        {
            yield return new WaitForSecondsRealtime(15f);
            var ship = IgiQoL.Game.Fleet.Flagship;
            var poi = Source.Galaxy.MapPointOfInterest.current;
            Plugin.Log.LogInfo($"[Dev] soak t={seconds - (end - Time.realtimeSinceStartup):0}s: status='{(SidePanel.instance != null ? SidePanel.instance.idleStatusMessage.text : "")}' at={(poi != null ? poi.name : "travelling")} docked={(Behaviour.UI.Spacestation.SpaceStationInterior.instance != null)} hull={(ship != null ? ship.currentHullHP / ship.maxHullHP : 0f):P0} cargo={player.currentSpaceShip.cargoUsed:0}/{player.currentSpaceShip.cargoCapacity:0} credits={player.credits}");
        }
        if (player.autoPlay) tab.ToggleChanged();
        foreach (var e in Features.EventLog.EventLog.Entries)
            Plugin.Log.LogInfo($"[Dev] log {e.when:HH:mm:ss} {e.kind} {e.text}{(e.repeats > 1 ? " (x" + e.repeats + ")" : "")}");
    }

    private static string DumpDir()
    {
        string dir = Paths.ConfigPath + "/IgiQoL/dumps";
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void DumpIconKeys()
    {
        var keys = new List<string>();
        foreach (var entry in PoiIconLibrary.Instance.entries) keys.Add(entry.key);
        keys.Sort(StringComparer.Ordinal);
        string path = DumpDir() + "/icons.txt";
        File.WriteAllLines(path, keys);
        Plugin.Log.LogInfo($"[Dev] dumped {keys.Count} icon keys -> {path}");
    }

    private static void DumpTurrets()
    {
        var lines = new List<string>();
        foreach (var item in InventoryItemType.all)
        {
            AbstractTurret turret;
            try
            {
                if (!item.TryGetComponent(out turret)) continue;
            }
            catch (Exception) { continue; }
            lines.Add(item.identifier + "\t" + item.rarity + "\t" + item.itemLevel + "\t" + turret._range);
        }
        lines.Sort(StringComparer.Ordinal);
        string path = DumpDir() + "/turret-ranges.txt";
        File.WriteAllLines(path, lines);
        Plugin.Log.LogInfo($"[Dev] dumped {lines.Count} turret ranges -> {path}");
    }

    // Missile flight (speed, turn rate, the tightest circle it can fly) next to how fast your ships can sidestep.
    private static void DumpMissileValues()
    {
        var sb = new StringBuilder();
        var seen = new HashSet<string>();
        foreach (var item in InventoryItemType.all)
        {
            Behaviour.Equipment.Turret.CombatTurrets.MissileTurret turret;
            try
            {
                if (!item.TryGetComponent(out turret) || turret.missilePrefab == null) continue;
            }
            catch (Exception) { continue; }
            var m = turret.missilePrefab;
            string key = turret.name + "|" + m.name;
            if (!seen.Add(key)) continue;
            float radius = m.rotateSpeed > 0f ? turret._projectileSpeed / (m.rotateSpeed * Mathf.Deg2Rad) : float.PositiveInfinity;
            sb.Append($"{item.identifier}\tmissile={m.name}\tspeed={turret._projectileSpeed}\trotate={m.rotateSpeed}deg/s\tthrust={m.thrust}\tmass={m.mass}\timpulse={m.initialImpulse}\tignition={m.ignitionDelay}\tlifetime={m.lifetime}\tseeking={turret.seekingMissiles}\trange={turret._range}\tturnRadius={radius:0.0}\n");
        }
        sb.Append('\n');
        foreach (var ship in Source.Player.GamePlayer.current.activeFleet)
        {
            if (ship?.unit is not Behaviour.Unit.SpaceShip unit || unit.engine == null) continue;
            var rb = unit.rigidbody;
            sb.Append($"{ship.GetShipName()} ({ship.shipClass.identifier})\tmass={rb.mass:0}\tradius={unit.radius:0.00}\tthrust={unit.engine.thrust:0}\tsideThrust={unit.engine.sideThrust:0}\tsideAccel={unit.engine.sideThrust / rb.mass:0.00}\tforwardAccel={unit.GetFullForwardAcceleration():0.00}\tmaxSpeed={unit.maxSpeed:0.0}\tturnRate={unit.GetMaxTurnRate():0.0}\tdamping={rb.linearDamping:0.00}\n");
        }
        var flag = Fleet.Flagship;
        if (flag != null && flag.engine != null)
            sb.Append($"flagship {flag.spaceShipData.GetShipName()}\tmass={flag.rigidbody.mass:0}\tradius={flag.radius:0.00}\tsideAccel={flag.engine.sideThrust / flag.rigidbody.mass:0.00}\tforwardAccel={flag.GetFullForwardAcceleration():0.00}\tmaxSpeed={flag.maxSpeed:0.0}\tdamping={flag.rigidbody.linearDamping:0.00}\n");
        string path = DumpDir() + "/missiles.txt";
        File.WriteAllText(path, sb.ToString());
        Plugin.Log.LogInfo($"[Dev] dumped {seen.Count} missile launchers -> {path}");
    }

    private static void DumpAllRecipes()
    {
        var available = new HashSet<string>();
        foreach (var r in Behaviour.Crafting.CraftingRecipe.GetAvailable()) if (r != null) available.Add(r.identifier);
        var sb = new StringBuilder();
        int count = 0;
        foreach (var parent in Behaviour.Crafting.CraftingRecipe.all)
        {
            foreach (var r in parent.subRecipes)
            {
                count++;
                sb.Append(r.identifier).Append(" | ").Append(r.displayName).Append(" | cat=").Append(r.category).Append(" rarity=").Append(r.craftingRarity)
                  .Append(" start=").Append(r.unlockedFromStart).Append(" enabled=").Append(r.craftingEnabled).Append(" avail=").Append(available.Contains(r.identifier))
                  .Append(" time=").Append(r.craftingTime).Append(" cost=").Append(r.craftingCost).Append(" minLevel=").Append(r.minLevel).Append('\n');
                foreach (var m in r.GetIngredientMaterials())
                    sb.Append("  mat ").Append(m.material).Append(" x").Append(m.amount).Append(" canister=").Append(m.material.GetCanisterItem()?.identifier).Append('\n');
                foreach (var it in r.GetIngredientItems())
                {
                    var src = Behaviour.Crafting.CraftingRecipe.GetSourceRecipe(it.item);
                    sb.Append("  item ").Append(it.item.identifier).Append(" x").Append(it.count).Append(" cat=").Append(it.item.itemCategory)
                      .Append(" lvl=").Append(it.item.itemLevel).Append(" mats=").Append(it.item.CanGoInMaterials()).Append(" armory=").Append(it.item.CanGoInArmory()).Append(" data=").Append(it.item.CanGoInDataInventory())
                      .Append(" from=").Append(src != null ? src.identifier + (available.Contains(src.identifier) ? "(avail)" : "(locked)") : "-").Append('\n');
                }
                foreach (var res in r.RawResults)
                {
                    var t = ItemDefinition.TemplateOf(res.item);
                    sb.Append("  result ").Append(t != null ? t.identifier : "builder:" + res.item.name).Append(" x").Append(res.count).Append('\n');
                }
            }
        }
        string path = DumpDir() + "/recipes.txt";
        File.WriteAllText(path, sb.ToString());
        Plugin.Log.LogInfo($"[Dev] dumped {count} recipes -> {path}");
    }

    private static void Dump(string typeName)
    {
        string dir = Paths.ConfigPath + "/IgiQoL/dumps";
        Directory.CreateDirectory(dir);
        string path = dir + "/" + typeName + ".txt";
        var type = AccessTools.TypeByName(typeName);
        Component found = null;
        Component asset = null;
        if (type != null)
        {
            foreach (var obj in Resources.FindObjectsOfTypeAll(type))
            {
                if (obj is not Component c) continue;
                if (c.gameObject.scene.IsValid())
                {
                    found = c;
                    break;
                }
                if (asset == null) asset = c;
            }
        }
        var sb = new StringBuilder();
        if (found == null)
        {
            sb.Append("no scene instance of ").Append(typeName).Append('\n');
            if (asset != null)
            {
                sb.Append("prefab asset (not in a scene; rect sizes are prefab-local):\n");
                DumpNode(asset.transform, 0, sb);
            }
        }
        else
        {
            var parent = found.transform.parent;
            sb.Append("parent: ").Append(parent != null ? parent.name : "-").Append('\n');
            sb.Append("grandparent: ").Append(parent != null && parent.parent != null ? parent.parent.name : "-").Append('\n');
            DumpNode(found.transform, 0, sb);
        }
        File.WriteAllText(path, sb.ToString());
        Plugin.Log.LogInfo($"[Dev] dumped {typeName} -> {path}");
    }

    private static void DumpNode(Transform t, int depth, StringBuilder sb)
    {
        sb.Append(' ', depth * 2).Append(t.name).Append(" active=").Append(t.gameObject.activeSelf);
        if (t is RectTransform rt)
        {
            var r = rt.rect;
            sb.Append($" anchoredPos=({rt.anchoredPosition.x:0.#},{rt.anchoredPosition.y:0.#}) sizeDelta=({rt.sizeDelta.x:0.#},{rt.sizeDelta.y:0.#}) rect=({r.width:0.#}x{r.height:0.#}) anchors=({rt.anchorMin.x},{rt.anchorMin.y})-({rt.anchorMax.x},{rt.anchorMax.y}) pivot=({rt.pivot.x},{rt.pivot.y})");
        }
        sb.Append(" comps=[");
        bool first = true;
        foreach (var c in t.GetComponents<Component>())
        {
            if (c == null || c is Transform) continue;
            if (!first) sb.Append('|');
            first = false;
            if (c is Image img)
                sb.Append($"Image({img.type},fill={img.fillAmount:0.##},color=#{ColorUtility.ToHtmlStringRGBA(img.color)},sprite={(img.sprite != null ? img.sprite.name : "-")})");
            else if (c is LayoutElement le)
                sb.Append($"LayoutElement(min={le.minHeight:0.#},pref={le.preferredHeight:0.#},flex={le.flexibleHeight:0.#})");
            else if (c is HorizontalOrVerticalLayoutGroup lg)
                sb.Append($"{lg.GetType().Name}(spacing={lg.spacing:0.#},pad={lg.padding.top}/{lg.padding.bottom},ctrlH={lg.childControlHeight},expandH={lg.childForceExpandHeight})");
            else if (c is TextMeshProUGUI tmp)
            {
                string text = tmp.text ?? "";
                if (text.Length > 40) text = text.Substring(0, 40);
                sb.Append("TextMeshProUGUI(\"").Append(text.Replace("\n", "\\n")).Append($"\",size={tmp.fontSize:0.#}");
                if (tmp.enableAutoSizing) sb.Append($",auto={tmp.fontSizeMin:0.#}-{tmp.fontSizeMax:0.#}");
                sb.Append(",font=").Append(tmp.font != null ? tmp.font.name : "-").Append(",wrap=").Append(tmp.textWrappingMode).Append(",overflow=").Append(tmp.overflowMode).Append(')');
            }
            else
                sb.Append(c.GetType().Name);
        }
        sb.Append("]\n");
        for (int i = 0; i < t.childCount; i++) DumpNode(t.GetChild(i), depth + 1, sb);
    }
}

[HarmonyPatch]
internal static class DevAutosavePatches
{
    private static void Note()
    {
        if (DevFeature.Instance.autosaveLogged) return;
        DevFeature.Instance.autosaveLogged = true;
        Plugin.Log.LogInfo("[Dev] autosave suppressed");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveGame), nameof(SaveGame.StoreAutosaveState))]
    private static bool StoreAutosaveStatePrefix()
    {
        if (!DevFeature.Instance.Active) return true;
        try { Note(); return false; }
        catch (Exception e) { DevFeature.Instance.Fail(e, nameof(DevAutosavePatches)); return true; }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveGame), nameof(SaveGame.StoreAutosaveInBackground))]
    private static bool StoreAutosaveInBackgroundPrefix(ref bool __result)
    {
        if (!DevFeature.Instance.Active) return true;
        try { Note(); __result = false; return false; }
        catch (Exception e) { DevFeature.Instance.Fail(e, nameof(DevAutosavePatches)); return true; }
    }
}
