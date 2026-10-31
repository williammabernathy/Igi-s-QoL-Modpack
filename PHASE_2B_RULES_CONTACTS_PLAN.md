# Igi's QoL Modpack: Phase 2b, item rules in the save, ECHO cargo rules, Contacts

## Context

Phase 2a shipped the mod window (ECHO, Operations, Notes, Settings tabs), the fleet strip and the per-ship popup; its context fixed the next slice: "Item rules tab, ECHO cargo rules and Contacts are Phase 2b (decided: item rules move from `item-rules.json` into the save)". This plan delivers exactly that: the item-rule table moves into the save and gets an Item rules tab; a new `EchoCargo` feature applies each rule's dock action (keep aboard / sell / sell above N) in ECHO's unload loop and, as the user chose, in the game's fleet auto-deposit on dock; a new `Contacts` feature adds a Contacts tab (boardable ships, wrecks, loot grouped by item type, nearest first, with Board/Salvage/Tractor buttons) and a counter line above the QoL HUD button. Edge-of-screen markers stay in Phase 3.

Repo `/home/will/Projects/vanguard-galaxy-mods`; game source `decompiled/` (0.8.3.4; every `file:line` below points there and is a hint, re-read before editing). Plugin conventions are the Phase 1/2a code itself. Read first: `src/IgiQoL/Core/Feature.cs`, `Core/SaveData.cs`, `Core/SelfCheckContext.cs`, `Rules/ItemRules.cs`, `Features/TractorCaps/TractorCaps.cs`, `Features/EchoGates/EchoGates.cs` (the `DropFoundItemPrefix` and `FastUnload`), `Features/EchoPriority/EchoPriority.cs` (`BuildEcho`, the add-row and `Edit` → `ModWindow.Show` rebuild pattern), `Features/Operations/Operations.cs` (`OperationsList`: rebuild-on-change, refresh-every-second), `Features/Window/WindowHotkey.cs` (`HudButton.Create`), `UI/Kit.cs`, `UI/ModWindow.cs`, `UI/SettingRows.cs`.

## Conventions

Same as Phase 2a:
- `G="/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy"`; log `$G/BepInEx/LogOutput.log` (rewritten per launch); cfg `$G/BepInEx/config/igi.qol.cfg`; mod data `$G/BepInEx/config/IgiQoL/`.
- `S="/mnt/data/SteamLibrary/steamapps/compatdata/3471800/pfx/drive_c/users/steamuser/AppData/LocalLow/Bat Roost Games/VanguardGalaxy/Saves"`; screenshots land in the parent of `Saves` as `igiqol-<name>.png`.
- Build: `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$HOME/.dotnet/tools:$PATH; cd /home/will/Projects/vanguard-galaxy-mods; dotnet build -c Release src/IgiQoL 2>&1 | grep -E "error|warning CS|Build succeeded"`. Fix every `warning CS` as well as errors.
- Launch helper, recreate as `/tmp/igiqol-launch.sh` (delete at the end):
  ```bash
  #!/bin/bash
  # usage: igiqol-launch.sh <regex to wait for>
  G="/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy"; LOG="$G/BepInEx/LogOutput.log"
  pgrep -f VanguardGalaxy.exe >/dev/null && { echo "game already running"; exit 1; }
  start=$(date +%s); steam -applaunch 3471800 >/dev/null 2>&1 &
  for i in $(seq 1 300); do sleep 1
    now=$(stat -c %Y "$LOG" 2>/dev/null || echo 0)
    [ "$now" -ge "$start" ] && grep -qE "$1" "$LOG" && { echo "matched after ${i}s"; break; }
    [ "$i" = 60 ] && ! pgrep -f VanguardGalaxy.exe >/dev/null && { echo "game did not start"; exit 2; }
  done
  for i in $(seq 1 60); do pgrep -f VanguardGalaxy.exe >/dev/null || break; sleep 1; done
  pgrep -f VanguardGalaxy.exe >/dev/null && echo "still running" || echo "game exited"
  ```
  Every verification run: `bash /tmp/igiqol-launch.sh "Self-check complete|switched off"` then `grep -nE "CHECK|startup:|switched|Exception|\[ItemRules\]|\[EchoCargo\]|\[Contacts\]" "$G/BepInEx/LogOutput.log"`. The harness quits the game itself.
- Dev save (the 2a copy was deleted): before the first run, `newest=$(ls -t "$S"/autosave-*.save | head -1); cp "$newest" "$S/igiqol-dev.save"; cp "${newest%.save}.meta" "$S/igiqol-dev.meta"`. The cfg `[Dev]` section already holds `AutoLoadSave = igiqol-dev`, `SelfCheck = true`, `DumpHierarchy = FleetPanelRow,TravelInfo`, `Screenshot = true`, `QuitAfterSeconds = 10`, `DumpIcons = true`, `DumpTurretRanges = true`; set `Enabled = true` for the runs and back to `false` in Step 5. `pgrep -f VanguardGalaxy.exe` must be empty before each launch.
- Every Harmony patch body keeps the guard: `if (!F.Active) return <vanilla>; try { ... } catch (Exception e) { F.Fail(e, nameof(PatchClass)); return <vanilla>; }`. No LINQ/closures/`new` in bodies that run per frame or per row refresh; the Contacts list and counter refresh once per second (`Time.unscaledTime`), which may allocate small lists.
- Private game members are used directly (publicizer), including `protected` ones (`SalvageContainer.OnTargetedHandling`). Namespaces: `IdleManager`/`IdleStat` → `Behaviour.Gameplay`/`Source.Player`; `PlayerCargo`, `InventoryInteractionManager`, `UIHelper` → `Behaviour.UI`; `NotificationManager` → `Behaviour.UI.NotificationAlert`; `SpaceStation` → `Source.Galaxy.POI`; `Faction` → `Source.Galaxy`; `SpaceStationInterior` → `Behaviour.UI.Spacestation`; `Inventory`, `ItemCategory`, `Rarity`, `RefinedMaterialExtensions` → `Source.Item`; `InventoryItemType` → `Behaviour.Item`; `TractorableItem` → `Behaviour.Tractoring`; `SalvageContainer` → `Behaviour.Salvage`; `LootManager`, `DungeonManager`, `BasePoiManager` → `Behaviour.Managers`; `BoardableUnit`, `SpaceShip` → `Behaviour.Unit`; `TractorModule` → `Behaviour.Equipment.Module`; `TargetLayer` → `Behaviour.Weapons`; `FleetCommands` → `Behaviour.UI.Movement`; `HudManager` → `Behaviour.UI.HUD`; `GameplayType`, `RarityExtensions`, `GameMath`, `Translation`, `ColorHelper` → `Source.Util`; `MissionObjective`, `MissionTrigger` → `Source.MissionSystem`; `TractorableItemData`, `SalvageData` → `Source.Data.Persistable`; `ItemTooltipSource`, `UITooltipParent` → `Behaviour.UI.Tooltip`; `Singleton<T>` → `Behaviour.Util`; `GameplayManager`, `CameraMovement` → global namespace; `HudButton` → `IgiQoL.Features.Window` (in `WindowHotkey.cs`).
- New folders: `Features/ItemRulesTab/`, `Features/EchoCargo/`, `Features/Contacts/`. The feature namespace must not be `IgiQoL.Features.ItemRules`: inside it the simple name `ItemRules` would bind to the namespace instead of `IgiQoL.Rules.ItemRules`.
- Registry order (`Core/FeatureRegistry.All`) after this plan: Core, Dev, TractorCaps, JumpsRemaining, FleetVitals, MissileRetarget, DronesOnArrival, EchoGates, EchoOperations, Window, FleetStrip, EngagementDistance, EchoPriority, Operations, Notes, **ItemRulesTab, EchoCargo, Contacts**. Startup becomes `18 active`.
- Window tabs after this plan (id/title/order): `contacts`/Contacts/5, `echo`/ECHO/10, `itemrules`/Item rules/15, `operations`/Operations/20, `notes`/Notes/30, `settings`/Settings/90. Six 90 px tab buttons fit the 760 px header (≈ 694 px used).
- UI text is plain English literals. Game strings reused only where named (`@IdleSellItem`, `@IdleMaterialStorageFull`, `@IdleArmoryStorageFull`, `@FleetCargoStorageFull`, `@SSTransferredCargo`, `@SalvageNoTurrets`).

Game facts the steps rely on (verified this session):
- ECHO docked unload: `IdleManager.FindActivity` → `HandleAtSpaceStation` → `TryDropCargo(ss)` (`Behaviour.Gameplay/IdleManager.cs:310`) iterates `GamePlayer.current.GetCargoShips()` (flagship first, then every active-fleet ship with cargo, `Source.Player/GamePlayer.cs:1086`) and calls `private bool TryDropCargoOf(SpaceStation ss, SpaceShipData cargoShip)` (`:321-355`, body quoted in Step 3). One action per tick; `SetQuickerUpdateTimer()` (`:452`) shortens the next tick. `ShouldKeepInCargo` (`:357`) keeps favourites, flagship ammo in use (`ship.DoWeUseAmmo`) and `Usable` items with `storageOverride == StorageOverride.None`. `DropFoundItem` (`:370`) moves `1 + floor(cargoCapacity/200)` items (ammo: `max(20, GetMagSizeForAmmo)`, currency: 20) to the armory (`GamePlayer.current.globalInventory`) when `CanGoInArmory()`, else to `ss.materialStorage`, then `inventoryFull = false; SetQuickerUpdateTimer(); ReportDeposit(cargoShip, dropItem)`. `SellItemFromMaterials` (`:456`) sells the cheapest stored material only when materials storage is full and `autopilotSettings.autoSell` is on; it first calls `SpaceStationInterior.instance.GoToLocation(spaceStation.shopInventory.facility)` (synchronous, `Behaviour.UI.Spacestation/SpaceStationInterior.cs:743`) because `InventoryInteractionManager.SellAmount(Inventory.InventoryItem item, int amount, InventoryItemSlot targetSlot = null)` (`Behaviour.UI/InventoryInteractionManager.cs:1108`) adds the sold stack to `inventoryShop.shopInventory` (`:1134-1136`), which is null until the shop screen is open (`isShopOpen`, `:87`). `SellAmount` refuses `amount > item.count`, `!canSell`, `sellValue == 0`; it credits `sellValue * amount` and removes from `item.inventory`, so it works on a ship's cargo stack directly. Vanilla auto-sell excludes refinery canisters (`Inventory.CanAutoSell`, `Source.Item/Inventory.cs:421`, via `RefinedMaterialExtensions.IsCanister`).
- Fleet auto-deposit: `PlayerCargo.AutoDepositOnDock()` (`Behaviour.UI/PlayerCargo.cs:113`): `if (GamePlayer.current.autoDepositFleetCargo && !DepositFleetCargo()) ShowRedNotification("@FleetCargoStorageFull")`. `DepositFleetCargo` (`:121`) is also the manual Deposit button (`FleetCargoSection.cs:426`), so the patch goes on `AutoDepositOnDock` (callers: `SpacestationExteriorManager.cs:167`, `:338`, `IndustryMissionDock.cs:51`), never on `DepositFleetCargo`/`TransferCargo`. The vanilla whole-stack move is `SpaceShipData.TransferCargo(materials: true, armory: true, keepEssential: false)` (`Source.SpaceShip/SpaceShipData.cs:673`): skip favourites; `CanGoInMaterials() && materialStorage.GetSpaceAvailable() >= stack.spaceRequired` → `materialStorage.Add(item, count); cargo.Set(slot, null)`; else `CanGoInArmory() && !Inventory.global.IsFull(stack.spaceRequired)` → `global.Add(item, count); cargo.Set(slot, null)`; notification `@SSTransferredCargo` (`ColorHelper.orange75`) when anything moved. `PlayerCargo.GetManagedFleetShips()` (`:58`) = fleet cargo ships that are not mercenaries.
- Contacts sources: `BoardableUnit.all` (`Behaviour.Unit/BoardableUnit.cs:33`, static list, add/remove in `OnEnable`/`OnDisable` `:180-188`; `ship` `:47` is the `SpaceShip` with `displayName`; `data` `:31` is `DungeonLocationData` with `faction`, `isFullyDepleted`, `shipTemplate`; `simulation` `:49`). `SalvageContainer.all` (`Behaviour.Salvage/SalvageContainer.cs:33`; `displayName` `:41`; `data` `:29` is `SalvageData` with `HasItems()` `:461`, `HasSalvage(TargetLayer)` `:436`, `itemContent`). Loot has no registry: every piece is created by `LootManager.SpawnLootItem(TractorableItemData data, Transform parent)` (`Behaviour.Managers/LootManager.cs:197`, public, returns the `TractorableItem`); `TractorableItem.data.itemType`/`data.itemAmount`, `canBeTractorAssisted` (`Behaviour.Tractoring/TractorableItem.cs:37`). The in-world click handlers all require `UIHelper.clickTargetingAvailable` (`Behaviour.UI/UIHelper.cs:14`), which is false while the mouse is over UI, so the tab calls the underlying actions: `HudManager.Instance.ToggleBoardingPanel(BoardableUnit)` (`HudManager.cs:389`), the salvage click body (`SalvageContainer.cs:171-191`, quoted in Step 4), `TractorModule.SetManualTarget(TargetableUnit)` (`Behaviour.Equipment.Module/TractorModule.cs:216`) on `GameplayManager.Instance.spaceShip.GetModule<TractorModule>()`.
- Items: `ItemCategory` (`Source.Item/ItemCategory.cs`: Empty, Ore, Ammo, Turret, Module, Booster, Junk, UnusedMissionItem, Drone, RefinedProduct, Torpedo, JumpgatePass, TradeGoods, Usable, DefensiveTurret, Salvage, Currency, Crystal, Data, Blueprint, BossSignal, SignalFragment); `Rarity` (Standard, Enhanced, HighGrade, Exotic, Legendary); `rarity.GetColor()` / `rarity.GetDisplayName()` (`Source.Util/RarityExtensions.cs:9`, `:61`, English literals). `InventoryItemType`: `all` (`:51`), `identifier`, `displayName` (raw; show through `Translation.Translate`), `icon`, `rarity`, `itemLevel`, `itemCategory`, `canSell`, `sellValue`, `m3`, `CanGoInArmory()`/`CanGoInMaterials()`, `static GetMagSizeForAmmo(item)` (`:1194`). Item tooltip on any UI element: add `ItemTooltipSource` and call `SetItem(item, 1, allowCompare: false, ItemTooltipContext.InInventory)` (`Behaviour.UI.Tooltip/ItemTooltipSource.cs:24`); needs `UITooltipParent.ItemTooltipPrefab != null` (`UITooltipParent.cs:15`). Search box: `UiSearchField.Build(parent, placeholder, bg, fontSize)` + `UiSearchField.AddLens(field, 12f, 22f)` + `onValueChanged` (`Behaviour.UI/InventoryToolbar.cs:79-86`). `GameMath.FormatNumber(float)` (`Source.Util/GameMath.cs:397`) formats credits ("12.3K").
- LightJson: `JsonValue` is a struct; `JsonObject.Clear()`, `Remove`, `ContainsKey`, enumeration of `KeyValuePair<string, JsonValue>`; ints convert through `double`; `JsonValue.Parse(string)`.

## Approach

### Step 1. Item rules engine moves into the save; shared cargo helpers

1. `Rules/ItemRules.cs`, rewrite (same namespace `IgiQoL.Rules`):
   ```csharp
   public enum DockAction { Default, Keep, Sell, SellAbove }

   public sealed class ItemRule
   {
       public int tractorCap;            // 0 = no limit
       public DockAction dockAction;     // Default = what the game does
       public int keepAmount;            // SellAbove: deposit until the destination storage holds this many, sell the rest
       public bool IsVanilla => tractorCap == 0 && dockAction == DockAction.Default;
   }

   public static class ItemRules
   {
       public const string FeatureKey = "ItemRules";
       public static string ImportPath => Paths.ConfigPath + "/IgiQoL/item-rules.json";
       public static readonly string[] DockActionNames = { "Game default", "Keep aboard", "Sell", "Sell above N" };

       public static ItemRule Default { get; private set; } = new();
       public static readonly Dictionary<ItemCategory, ItemRule> ByCategory = new();
       public static readonly Dictionary<(ItemCategory, Rarity), ItemRule> ByCategoryRarity = new();
       public static readonly Dictionary<string, ItemRule> ByItem = new();     // key = Items.RuleKey(type): "identifier" or "identifier@level" (named ByItem so the Game.Items adapter stays reachable inside the class)
       public static bool HasDockRules { get; private set; }                    // any rule (Default included) with dockAction != Default

       public static ItemRule Resolve(InventoryItemType item);   // today's lookup order (item@level, item, category/rarity, category, Default); no file polling, no Time check
       public static void Load();                                // from SaveData; subscribed to GameEvents.SaveLoaded by CoreFeature
       public static void Write();                               // tables -> SaveData.Feature(FeatureKey, true); recomputes HasDockRules
       public static ItemRule Edit(ItemCategory c);              // get-or-create the live table entry (callers mutate it, then Write())
       public static ItemRule Edit(ItemCategory c, Rarity r);
       public static ItemRule Edit(string key);
       public static bool Remove(ItemCategory c);  public static bool Remove(ItemCategory c, Rarity r);  public static bool Remove(string key);
       public static void UseForTest(string json);               // tables only, no save write (self-checks)
       public static int Count => ByCategory.Count + ByCategoryRarity.Count + ByItem.Count;
   }
   ```
   - Record shape in the save (`SaveData.Feature("ItemRules")`), identical to today's file: `{"default":{"tractorCap":0,"dockAction":"Default","keepAmount":0},"categories":{"Ore":{...}},"categoryRarity":{"Ammo/Standard":{...}},"items":{"iron_ore":{...},"laser_mk2@3":{...}}}`. `Write()` does `record.Clear()` then writes all four keys; every rule writes all three fields.
   - `Apply(JsonObject root, string source)` is today's `Load(string, string)` minus the parsing: rebuilds the four tables; unknown category/rarity names → `[ItemRules] {source}: unknown ... skipped` warning; `dockAction` parsed with `Enum.TryParse<DockAction>` (unknown → `Default`); `keepAmount`/`tractorCap` clamped to ≥ 0. Recomputes `HasDockRules`.
   - `Load()`: `var record = SaveData.Feature(FeatureKey, false); if (record != null) { Apply(record, "save"); return; }` Otherwise create the record (`Feature(FeatureKey, true)`), clear the tables, and import once: if `File.Exists(ImportPath)`, `try { Apply(JsonValue.Parse(File.ReadAllText(ImportPath)).AsJsonObject, ImportPath); if (Count > 0 || !Default.IsVanilla) { Write(); log "[ItemRules] imported {Count} rules from item-rules.json into this save" } } catch (Exception e) { log error "[ItemRules] could not import {path}: {e.Message}"; clear tables }`. The file is never read again for a save that has the record and is never created or renamed; the `Template` constant and the stamp polling are deleted. `Load()` catches its own exceptions (log + empty tables) so the `SaveLoaded` chain continues.
   - `Items` adapter unchanged (`Items.RuleKey`).
2. `Game/Cargo.cs`: add
   ```csharp
   // Moves up to `max` of `item`, never more than `to` has room for. Returns the amount moved.
   public static int Move(Inventory from, Inventory to, InventoryItemType item, int max)
   {
       int have = from.GetCount(item);
       int fit = item.m3 > 0f ? Mathf.FloorToInt(to.GetSpaceAvailable() / item.m3) : have;
       int want = Math.Min(Math.Min(have, fit), max);
       if (want <= 0) return 0;
       int removed = from.Remove(item, want);
       if (removed > 0) to.Add(item, removed);
       return removed;
   }
   // Where ECHO's DropFoundItem puts the item at this station; null when it fits neither storage.
   public static Inventory DepositTarget(SpaceStation station, InventoryItemType item) =>
       item.CanGoInArmory() ? GamePlayer.current.globalInventory : item.CanGoInMaterials() ? station.materialStorage : null;
   ```
   `Features/EchoGates/EchoGates.cs`: delete the `FastUnload` class; `CheckFastUnload` and `DropFoundItemPrefix` call `Cargo.Move(..., int.MaxValue)` instead; add `public static bool FastUnloadOn => Instance != null && Instance.Active && Instance.FastUnloading.Value;` on `EchoGates`.
3. `Core/CoreFeature.OnActivated`: `GameEvents.SaveLoaded += ShipSettings.Invalidate; GameEvents.SaveLoaded += ItemRules.Load;`.
4. `Features/TractorCaps/TractorCaps.cs`: remove `OnActivated`; `Description` → `"Your ships stop pulling in an item automatically once they carry the limit set for it on the Item rules tab. Clicking the item yourself still picks it up."`; add `public override string SettingsTab => "Item rules";`; in `SelfCheck` replace `ctx.Cleanup(ItemRules.ReloadFromDisk)` with `ctx.Cleanup(ItemRules.Load)`. `Allows` unchanged.
5. `UI/Kit.cs`: add
   - `public static TMP_InputField Search(RectTransform parent, string placeholder, Action<string> onChanged, float width = 300f)`: `UiSearchField.Build(parent, placeholder, ColorHelper.panelHeaderBlue, 12f)`, `UiSearchField.AddLens(field, 12f, 22f)`, `field.onValueChanged.AddListener(v => onChanged(v))`, `CaptainCards.SetFixedSize((RectTransform)field.transform, width, 24f)`.
   - `public static TMP_Text Fixed(RectTransform parent, string text, float width, TextAlignmentOptions align, Color color)`: `FleetBarParts.AddLine(parent, text, 12f, color, align)` then `CaptainCards.SetFixedSize((RectTransform)t.transform, width, 22f)`.
   - `public static void ItemTooltip(Graphic target, InventoryItemType item)`: return when `UITooltipParent.ItemTooltipPrefab == null`; `target.raycastTarget = true; var src = target.GetComponent<ItemTooltipSource>() ?? target.gameObject.AddComponent<ItemTooltipSource>(); src.SetItem(item, 1, false, ItemTooltipContext.InInventory);`.
6. `Features/Window/WindowFeature.BuildSettings`: the muted intro's "(ECHO, Operations)" becomes "(ECHO, Operations, Item rules)".
7. Build; launch; expect `CHECK TractorCaps/resolve-ammo PASS`, `gate PASS|SKIP`, `EchoGates/fast-unload PASS`, log line `[ItemRules]` absent (the dev save has no record and the file holds no rules, so nothing is imported) and startup `15 active`.

### Step 2. Item rules tab

Feature `ItemRulesTab` (`Features/ItemRulesTab/ItemRulesTab.cs`, namespace `IgiQoL.Features.ItemRulesTab`, `Id = "ItemRules"`, `Title = "Item rules tab"`, `Description = "The Item rules tab: per-item, per-category and default rules for loot pickup limits and for what ECHO does with cargo at a station."`, `Enabled` only, `PatchClasses = Type.EmptyTypes`). `OnActivated`: `ModWindow.AddTab("itemrules", "Item rules", 15, Build)`.

1. Static data: `public static readonly ItemCategory[] Categories` = every `ItemCategory` except `Empty` and `UnusedMissionItem`, in enum order; `static string search = ""` (survives rebuilds); `static Dictionary<string, InventoryItemType> byKey` built lazily once from `InventoryItemType.all` keyed by `Items.RuleKey`. Category display name: `SettingRows.Pretty(cat.ToString())`; rarity: `rarity.GetDisplayName()`; item: `Translation.Translate(type.displayName) + (type.itemLevel > 0 ? " L" + type.itemLevel : "")`, coloured `type.rarity.GetColor()`, with `Kit.ItemTooltip`.
2. `Build(RectTransform content)`, top to bottom:
   - `Kit.Heading(content, "Item rules")`; `Kit.Muted(content, "The most specific rule wins: a specific item, then category + rarity, then category, then the default. Pickup limit: your ships stop auto-tractoring the item once they carry this many (0 = no limit). At dock: what ECHO does with the item when it unloads at a station. Rules are saved with this game.")`.
   - Column header `Kit.Row(content, 6f, 22f)`: `Kit.Label(row, "Rule")`; `Kit.Fixed(row, "Pickup limit", 60f, Center, ColorHelper.poiTooltipMuted)` with `Kit.Tooltip(text, "Pickup limit", "Stop auto-tractoring once a ship carries this many. 0 = no limit. Needs 'Loot pickup limits' on.")`; `Kit.Fixed(row, "At dock", 130f, Center, muted)` with tooltip `"Game default: deposited in the station's storage as usual. Keep aboard: never unloaded. Sell: sold at the station shop when ECHO unloads. Sell above N: deposited until the storage holds N, the rest is sold. Favourite stacks, ammo the flagship uses and the usable items the game keeps aboard are never touched. Needs 'ECHO cargo rules' on."`; `Kit.Fixed(row, "N", 60f, Center, muted)` with tooltip `"For Sell above N: the amount to keep in storage."`; `Kit.Fixed(row, "", 22f, Center, muted)`.
   - `Kit.Heading(content, "Default")`; `RuleRow(content, "Everything without a rule of its own", null, ItemRules.Default, remove: null)`.
   - `Kit.Heading(content, "Categories")`; one `RuleRow` per entry of `ItemRules.ByCategory` in `Categories` array order (label `Pretty(cat)`, remove → `ItemRules.Remove(cat); ItemRules.Write(); Rebuild();`). Add row `Kit.Row(content, 6f, 26f)`: `Kit.Label(row, "Add a category rule")`, `Kit.Dropdown(row, names of categories without a rule, 0, _ => { }, 180f)`, `Kit.TextButton(row, "Add", () => { ItemRules.Edit(pick); ItemRules.Write(); Rebuild(); }, 60f)` where `pick` is read from the dropdown's `GetIndex()` at click time (as `BuildEcho` does); when every category has a rule, `Kit.Muted(content, "Every category has a rule.")` instead.
   - `Kit.Heading(content, "Category and rarity")`; one `RuleRow` per `ItemRules.ByCategoryRarity` entry sorted by (category order, rarity), label `Pretty(cat) + " / " + rarity.GetDisplayName()` coloured `rarity.GetColor()`. Add row: `Kit.Label(row, "Add a category + rarity rule")`, `Kit.Dropdown(all Categories names, 0, 150f)`, `Kit.Dropdown(five rarity names, 0, 110f)`, `Kit.TextButton("Add", 60f)` → `Edit(cat, rarity); Write(); Rebuild();` (an existing pair just rebuilds).
   - `Kit.Heading(content, "Items")`; one `RuleRow` per `ItemRules.ByItem` entry sorted by key: when `byKey` has the key, the item label/colour/tooltip above; else label `key + " (not in this game version)"`. Then `Kit.Row(content, 6f, 26f)` with `Kit.Label(row, "Add an item rule")` and `Kit.Search(row, "Type part of an item name", RunSearch, 300f)` whose text is restored from `search` (`SetTextWithoutNotify`), followed by a `results = Kit.Column(content, 2f)`; after building, `if (search.Trim().Length >= 2) RunSearch(search)`.
   - `RunSearch(string text)`: store in `search`; destroy `results` children; return when `text.Trim().Length < 2`; collect every `InventoryItemType.all` entry whose item label contains the text (`IndexOf(..., StringComparison.OrdinalIgnoreCase) >= 0`) and whose key is not already in `ItemRules.ByItem`, sorted by label, first 20; per hit `Kit.Row(results, 6f, 22f)`: item label (coloured, tooltip), `Kit.Fixed(row, Pretty(category), 120f, MidlineLeft, muted)`, `Kit.TextButton(row, "Add", () => { ItemRules.Edit(key); ItemRules.Write(); Rebuild(); }, 60f)`; when more than 20 matched, `Kit.Muted(results, "Showing 20 of N; type more of the name.")`.
   - `SettingRows.AddSection(content, EchoCargo.Instance)`; when `EchoCargo.Instance.Active`, `SettingRows.AddEntries(content, "EchoCargo", SettingRows.Rows.Options)`, else `Kit.Muted(content, "Turned off on the Settings tab.")`. When `!TractorCaps.Instance.Active`, `Kit.Muted(content, "Pickup limits do nothing while 'Loot pickup limits' is off (Settings tab).")`.
3. `RuleRow(RectTransform parent, string label, Color? colour, ItemRule rule, Action remove, InventoryItemType tooltipItem = null)`: `row = Kit.Row(parent, 6f, 26f)`; `text = Kit.Label(row, label)` (+ colour, + `Kit.ItemTooltip` when `tooltipItem != null`); `cap = Kit.Input(row, rule.tractorCap.ToString(), IntegerNumber, v => { if (int.TryParse(v, out var n)) rule.tractorCap = Math.Max(0, n); cap.SetTextWithoutNotify(rule.tractorCap.ToString()); ItemRules.Write(); }, 60f)`; `TMP_InputField keep = null;` `Kit.Dropdown(row, ItemRules.DockActionNames, (int)rule.dockAction, i => { rule.dockAction = (DockAction)i; SyncKeep(keep, rule); ItemRules.Write(); }, 130f)`; `keep = Kit.Input(row, "", IntegerNumber, v => { if (int.TryParse(v, out var n)) rule.keepAmount = Math.Max(0, n); SyncKeep(keep, rule); ItemRules.Write(); }, 60f); SyncKeep(keep, rule);` where `SyncKeep` sets `keep.interactable = rule.dockAction == DockAction.SellAbove` and the text to `rule.keepAmount.ToString()` when SellAbove, else `""` (the field stays in the row so columns line up). Last column: `remove != null` → `Kit.Tooltip(Kit.TextTile(row, "x", 22f, () => remove()).targetGraphic, null, "Remove this rule")`; else `Kit.Fixed(row, "", 22f, Center, ColorHelper.offWhite)` as a spacer.
4. `Rebuild()` = `ModWindow.Show("itemrules")`.
5. SelfCheck: `roundtrip`: remember whether `ItemRules.ByCategory` has an `Ore` entry (and its three values); `var r = ItemRules.Edit(ItemCategory.Ore); r.tractorCap = 5; r.dockAction = DockAction.SellAbove; r.keepAmount = 10; ItemRules.Write(); ItemRules.Load();` then with the first `InventoryItemType.all` entry of category `Ore` (SKIP "no ore item type" if none): PASS when `Resolve(ore)` gives `5/SellAbove/10`, `ItemRules.HasDockRules` is true and `SaveData.Feature("ItemRules", false)["categories"]["Ore"]["keepAmount"].AsInteger == 10`; detail with the values. Cleanup restores the previous Ore entry or removes it, then `Write()`. `tab`: `ModWindow.Open("itemrules")`; PASS when `ModWindow.CurrentTab == "itemrules"` and the content has ≥ 5 children named `Row` (header, default, the Ore row from `roundtrip`, two add rows, the search row); `yield return ctx.Screenshot("window-itemrules")`; `ctx.Cleanup(ModWindow.Close)`.
6. Build; launch; expect `[ItemRules] active (0 patches)`, `CHECK ItemRules/roundtrip PASS`, `tab PASS`; read `igiqol-window-itemrules.png`: header columns aligned over the Default and Ore rows, the Ore row showing `5`, `Sell above N`, `10`, the add rows and the search box. Fix widths in `RuleRow`/header before continuing if a control sits outside its column.

### Step 3. ECHO cargo rules

Feature `EchoCargo` (`Features/EchoCargo/EchoCargo.cs`, `Id = "EchoCargo"`, `Title = "ECHO cargo rules"`, `Description = "When ECHO unloads at a station, the dock action from the Item rules tab decides what happens to each stack: deposit as usual, keep it aboard, sell it, or deposit up to an amount and sell the rest. Favourite stacks, ammo the flagship uses and the usable items the game keeps aboard are never touched."`, `SettingsTab = "Item rules"`, `PatchClasses = { typeof(EchoCargoPatches) }`). Config: `ApplyToAutoDeposit` (bool, true, `SettingInfo.Describe("Apply when fleet cargo auto-deposits on dock", "With the game's fleet auto-deposit on, fleet holds are emptied the moment you dock. On: your rules apply there too: Keep aboard stays aboard, Sell stays aboard until ECHO unloads it, Sell above N deposits up to N. Nothing is sold when you dock yourself. Off: the game deposits everything as usual, so fleet-ship rules only matter with auto-deposit off.")`). The manual Deposit button and the inventory toolbar stay vanilla.

1. `public static class CargoRules` (same file):
   ```csharp
   public struct Step { public DockAction action; public int deposit; public int sell; }   // action Default = run the vanilla branch

   // Pure. count: stack size; stored: amount already in the destination storage; reserved: GamePlayer.RequiredItemCountForMissions; sellable: CanSellHere.
   public static Step Plan(ItemRule rule, int count, int stored, int reserved, bool sellable)
   {
       switch (rule.dockAction)
       {
           case DockAction.Keep: return new Step { action = DockAction.Keep };
           case DockAction.Sell:
           {
               if (!sellable) return default;                       // unsellable here: deposit as the game would
               int sell = count - reserved;
               return sell > 0 ? new Step { action = DockAction.Sell, sell = sell } : new Step { action = DockAction.Keep };
           }
           case DockAction.SellAbove:
           {
               int room = Math.Max(0, rule.keepAmount - stored);
               if (room > 0) return new Step { action = DockAction.SellAbove, deposit = Math.Min(room, count) };
               if (!sellable) return default;
               int sell = count - reserved;
               return sell > 0 ? new Step { action = DockAction.SellAbove, sell = sell } : new Step { action = DockAction.Keep };
           }
           default: return default;
       }
   }

   public static bool CanSellHere(SpaceStation ss, InventoryItemType item) =>
       ss.shopInventory != null && SpaceStationInterior.instance != null
       && item.canSell && item.sellValue > 0 && !RefinedMaterialExtensions.IsCanister(item);

   // DropFoundItem's chunk, or the whole stack with EchoGates' fast unloading.
   public static int ChunkSize(SpaceShipData ship, InventoryItemType item)
   {
       if (EchoGates.EchoGates.FastUnloadOn) return int.MaxValue;
       if (item.itemCategory == ItemCategory.Ammo) return Mathf.Max(20, InventoryItemType.GetMagSizeForAmmo(item));
       if (item.itemCategory == ItemCategory.Currency) return 20;
       return 1 + Mathf.FloorToInt(ship.cargoCapacity / 200f);
   }

   public static int DepositUpTo(IdleManager im, SpaceStation ss, SpaceShipData ship, InventoryItemType item, int max)
   {
       var dest = Cargo.DepositTarget(ss, item);
       if (dest == null) return 0;
       int moved = Cargo.Move(ship.cargo, dest, item, Math.Min(max, ChunkSize(ship, item)));
       if (moved > 0) { im.inventoryFull = false; im.SetQuickerUpdateTimer(); IdleManager.ReportDeposit(ship, item); }
       return moved;
   }

   private static bool shopWarned;

   // Mirrors IdleManager.SellItemFromMaterials: open the shop screen (registers it with InventoryInteractionManager), sell, report, count the stat.
   public static bool Sell(IdleManager im, SpaceStation ss, Inventory.InventoryItem stack, int amount)
   {
       SpaceStationInterior.instance.GoToLocation(ss.shopInventory.facility);
       var iim = InventoryInteractionManager.Instance;
       if (iim == null || !iim.isShopOpen) { if (!shopWarned) { shopWarned = true; Plugin.Log.LogWarning("[EchoCargo] shop screen did not open; nothing sold"); } return false; }
       var item = stack.item; long value = item.sellValue;
       if (!iim.SellAmount(stack, amount)) return false;
       IdleManager.UpdateActivity("@IdleSellItem", amount, item.displayName);
       im.SetQuickerUpdateTimer();
       GamePlayer.current.AddAutopilotStat(IdleStat.MaterialsSoldValue, value * amount);
       return true;
   }

   // Dock path for one fleet ship; returns false when a stack the rules wanted deposited did not fit (vanilla's "storage full" condition).
   public static bool DepositFleetShip(SpaceStation ss, SpaceShipData ship)
   {
       var materials = ss.materialStorage; var armory = GamePlayer.current.globalInventory;
       bool clear = true; int moved = 0;
       foreach (var stack in ship.cargo.items)                       // same in-place walk as SpaceShipData.TransferCargo; Set(slot, null) is safe mid-walk
       {
           if (stack.favourite) continue;
           var item = stack.item;
           var rule = ItemRules.Resolve(item);
           if (rule.dockAction == DockAction.Keep || rule.dockAction == DockAction.Sell) continue;   // stays aboard; ECHO's loop sells later
           if (rule.dockAction == DockAction.SellAbove)
           {
               var dest = Cargo.DepositTarget(ss, item);
               if (dest != null) { int room = Math.Max(0, rule.keepAmount - dest.GetCount(item)); if (room > 0 && Cargo.Move(ship.cargo, dest, item, room) > 0) moved++; }
               continue;
           }
           if (item.CanGoInMaterials() && materials.GetSpaceAvailable() >= stack.spaceRequired) { materials.Add(item, stack.count); ship.cargo.Set(stack.slot, null); moved++; }
           else if (item.CanGoInArmory() && !armory.IsFull(stack.spaceRequired)) { armory.Add(item, stack.count); ship.cargo.Set(stack.slot, null); moved++; }
           else clear = false;
       }
       if (moved > 0) Singleton<NotificationManager>.Instance.CreateNotification(Translation.Translate("@SSTransferredCargo")).WithColor(ColorHelper.orange75).Show();
       return clear;
   }
   ```
   "Sell above N" counts the destination storage of the item at this station: the shared armory for armory items, this station's materials storage for the rest (`Cargo.DepositTarget`).
2. `EchoCargoPatches`:
   ```csharp
   [HarmonyPrefix, HarmonyPatch(typeof(IdleManager), nameof(IdleManager.TryDropCargoOf))]
   private static bool TryDropCargoOfPrefix(IdleManager __instance, SpaceStation ss, SpaceShipData cargoShip, ref bool __result)
   {
       if (!F.Active) return true;
       try
       {
           if (!ItemRules.HasDockRules) return true;                                  // vanilla body untouched
           var player = GamePlayer.current;
           foreach (var stack in cargoShip.cargo.items)
           {
               if (__instance.ShouldKeepInCargo(cargoShip, stack)) continue;           // vanilla protections always win
               var item = stack.item;
               var rule = ItemRules.Resolve(item);
               if (rule.dockAction != DockAction.Default)
               {
                   var dest = Cargo.DepositTarget(ss, item);
                   var step = CargoRules.Plan(rule, stack.count, dest != null ? dest.GetCount(item) : 0,
                                              player.RequiredItemCountForMissions(item), CargoRules.CanSellHere(ss, item));
                   if (step.action == DockAction.Keep) continue;
                   if (step.deposit > 0)
                   {
                       if (CargoRules.DepositUpTo(__instance, ss, cargoShip, item, step.deposit) > 0) { __result = true; return false; }
                       // storage full: the vanilla branch below reports it (or sells the cheapest stored material when auto-sell is on)
                   }
                   else if (step.sell > 0)
                   {
                       if (CargoRules.Sell(__instance, ss, stack, step.sell)) { __result = true; return false; }
                       continue;                                                        // refused this tick; the stack stays aboard
                   }
               }
               // vanilla TryDropCargoOf body for this stack (IdleManager.cs:329-351)
               if (item.CanGoInMaterials())
               {
                   if (ss.materialStorage.IsFull(item.m3))
                   {
                       if (player.autopilotSettings.autoSell && __instance.SellItemFromMaterials(ss, item.m3)) { __result = true; return false; }
                       IdleManager.UpdateActivity("@IdleMaterialStorageFull"); __result = true; return false;
                   }
                   __instance.DropFoundItem(ss, cargoShip, item); __result = true; return false;
               }
               if (item.CanGoInArmory())
               {
                   if (player.globalInventory.IsFull(item.m3)) { IdleManager.UpdateActivity("@IdleArmoryStorageFull"); __result = true; return false; }
                   __instance.DropFoundItem(ss, cargoShip, item); __result = true; return false;
               }
           }
           __result = false; return false;
       }
       catch (Exception e) { F.Fail(e, nameof(EchoCargoPatches)); return true; }
   }

   [HarmonyPrefix, HarmonyPatch(typeof(PlayerCargo), nameof(PlayerCargo.AutoDepositOnDock))]
   private static bool AutoDepositOnDockPrefix()
   {
       if (!F.Active) return true;
       try
       {
           if (!F.ApplyToAutoDeposit.Value || !ItemRules.HasDockRules) return true;
           if (!GamePlayer.current.autoDepositFleetCargo) return false;                 // vanilla: nothing happens
           if (!(GamePlayer.current.currentPointOfInterest is SpaceStation ss)) return false;
           bool clear = true;
           foreach (var ship in new List<SpaceShipData>(PlayerCargo.GetManagedFleetShips())) clear &= CargoRules.DepositFleetShip(ss, ship);
           if (!clear) InventoryInteractionManager.ShowRedNotification("@FleetCargoStorageFull");
           return false;
       }
       catch (Exception e) { F.Fail(e, nameof(EchoCargoPatches)); return true; }
   }
   ```
   `__instance.DropFoundItem(...)` goes through the patched method, so EchoGates' fast unloading still applies to the vanilla branch.
3. SelfCheck:
   - `plan`: PASS when all hold, detail listing each: `Plan(SellAbove 10, count 8, stored 7, reserved 0, sellable)` → `deposit 3`; `Plan(SellAbove 10, count 5, stored 10, 0, sellable)` → `sell 5`; `Plan(SellAbove 10, 5, 10, 0, !sellable)` → `action Default`; `Plan(Sell, count 5, 0, reserved 2, sellable)` → `sell 3`; `Plan(Sell, 2, 0, reserved 2, sellable)` → `action Keep`; `Plan(Sell, 5, 0, 0, !sellable)` → `action Default`; `Plan(Keep, ...)` → `Keep`; `Plan(Default, ...)` → `Default`.
   - `move-cap`: as `EchoGates.CheckFastUnload`: `from = new Inventory(false, false, () => 1000f)` with 50 of the first `Ore` type, `to = new Inventory(false, false, () => 1000f)`; `Cargo.Move(from, to, ore, 7)` → PASS when it returns 7, `from` holds 43, `to` holds 7 (SKIP "no ore item type" if none).
   - `live-keep`: SKIP "not docked" unless `SpaceStation.current != null && SpaceStationInterior.instance != null && Singleton<IdleManager>.HasInstance`; SKIP "no droppable cargo" unless the flagship has a non-favourite stack with `CanGoInMaterials() || CanGoInArmory()`; else `ctx.Cleanup(ItemRules.Load); ItemRules.UseForTest("{\"default\":{\"dockAction\":\"Keep\"}}"); int before = flagship.cargo.GetCount(stack.item); bool r = Singleton<IdleManager>.Instance.TryDropCargoOf(SpaceStation.current, flagship);` → PASS when `r == false` and the count is unchanged.
4. Build; launch; expect `[EchoCargo] active (2 patches)`, `CHECK EchoCargo/plan PASS`, `move-cap PASS`, `live-keep PASS|SKIP`, and `ItemRules/roundtrip` still PASS. The Item rules tab now shows the "ECHO cargo rules" section with the auto-deposit toggle (re-read `igiqol-window-itemrules.png`).

### Step 4. Contacts tab and HUD counter

Feature `Contacts` (`Features/Contacts/Contacts.cs`, `Id = "Contacts"`, `Title = "Contacts tab"`, `Description = "The Contacts tab: the boardable ships, wrecks and loot at your location with the distance to each and a button to board, salvage or tractor it, plus a counter above the QoL button showing what is left."`, `PatchClasses = { typeof(LootSpawnPatch) }`). Config: `HudCounter` (bool, true, `SettingInfo.Describe("Counter above the QoL button", "Shows how many boardable ships, wrecks and loot stacks are left at your location. Click it to open the Contacts tab.")`). `OnActivated`: `ModWindow.AddTab("contacts", "Contacts", 5, Build); GameEvents.SaveLoaded += OnSaveLoaded;` where `OnSaveLoaded` starts `ContactsBadge.CreateWhenReady()` on `Plugin.Instance` (guarded by `Active`).

1. Loot registry: `public static readonly List<TractorableItem> Loot = new();` `public static int PruneLoot()`: walk backwards, remove entries that are Unity-null or `!gameObject.activeInHierarchy`, return `Loot.Count`.
   ```csharp
   [HarmonyPatch(typeof(LootManager), nameof(LootManager.SpawnLootItem))]
   internal static class LootSpawnPatch
   {
       private static void Postfix(TractorableItem __result)
       {
           if (!Contacts.Instance.Active) return;
           try { if (__result != null) Contacts.Loot.Add(__result); }
           catch (Exception e) { Contacts.Instance.Fail(e, nameof(LootSpawnPatch)); }
       }
   }
   ```
2. Actions (static on `Contacts`):
   - `Board(BoardableUnit b)`: `ModWindow.Close(); HudManager.Instance.ToggleBoardingPanel(b);`.
   - `Salvage(SalvageContainer c)` (the `OnMouseUpAsButton` body without the mouse-over-UI check): `ModWindow.Close(); var list = FleetCommands.WithLoadout(FleetCommands.GetClickWorkCandidates(), GameplayType.Salvage); if (list.Count > 0) FleetCommands.IssueClickWork(list, c); c.OnTargetedHandling(); if (c.data.itemContent.Count > 0) HudManager.Instance.ToggleSalvageWindow(c, c.data); else if (list.Count == 0) InventoryInteractionManager.ShowRedNotification("@SalvageNoTurrets"); MissionObjective.Trigger(MissionTrigger.TargetWreckage);`.
   - `Tractor(InventoryItemType type)`: nearest entry of `Loot` (after `PruneLoot`) with `data.itemType == type && canBeTractorAssisted`, measured from `GameplayManager.Instance.spaceShip.transform.position`; `GameplayManager.Instance.spaceShip.GetModule<TractorModule>()?.SetManualTarget(nearest)` when both exist. The window stays open.
3. `Build(RectTransform content)`: `Kit.Muted(content, "Everything at this location you can board, salvage or tractor, nearest first. Distances are in map units from your ship; the list refreshes every second.")`; then `Kit.Column(content, 2f).gameObject.AddComponent<ContactsList>()`.
4. `ContactsList : MonoBehaviour` (copy `OperationsList`'s structure):
   - Data: `List<BoardableUnit> boardables`, `List<SalvageContainer> wrecks`, `Dictionary<InventoryItemType, LootGroup> groups` + `List<LootGroup> groupList` where `LootGroup { InventoryItemType type; int total, pieces; float nearest; }`; `HashSet<object> keys` (membership of the last build); `List<Row> rows` with `Row { object key; TMP_Text main, dist, value; }`; `TractorModule tractor` (looked up at `Rebuild`); cached `Comparison<>` delegates for the three sorts.
   - `Collect()`: `boardables` = entries of `BoardableUnit.all` that are not null and `gameObject.activeInHierarchy`; `wrecks` likewise from `SalvageContainer.all`; `Contacts.PruneLoot()`, then group `Contacts.Loot` by `data.itemType` reference (`total += itemAmount`, `pieces++`, `nearest = min`). Distances from `GameplayManager.Instance?.spaceShip` (`Vector2.Distance`); when the ship is null every distance is `float.MaxValue` and shows as `"-"`.
   - `Start`: `Collect(); Rebuild();`. `Update`: return unless `Contacts.Instance.Active`; once per second (`Time.unscaledTime`): `Collect(); if (Changed()) Rebuild(); else Refresh();` inside the usual try/catch → `Fail`.
   - `Changed()`: `keys.Count != boardables.Count + wrecks.Count + groups.Count` or any collected object/type not in `keys`.
   - `Rebuild()`: destroy children (`SetActive(false)` + `Destroy`), sort the three lists by distance, rebuild `keys`, then: `Kit.Heading(column, "Boardable ships (" + n + ")")` and per unit `Kit.Row(column, 6f, 22f)`: `main = Kit.Label(row, "")`, `dist = Kit.Fixed(row, "", 50f, MidlineRight, ColorHelper.offWhite)`, `Kit.TextButton(row, "Board", () => Contacts.Board(b), 70f)`; `Kit.Heading("Wrecks (n)")` rows with a `Salvage` button; `Kit.Heading("Loot (" + groups.Count + " kinds)")` (counts kinds, not stacks, so headings only change when the membership changes) rows: `main`, `value = Kit.Fixed(row, "", 70f, MidlineRight, ColorHelper.poiTooltipMuted)`, `dist`, and a `Tractor` button only when `tractor != null`; `Kit.ItemTooltip(main, type)`. Each empty group gets `Kit.Muted(column, "None")`. Then `Refresh()`.
   - `Refresh()` texts (assign only when changed): boardable `main` = `name + (faction) + (status)` with name `b.ship != null ? b.ship.displayName : b.data.shipTemplate`, faction `" | " + b.data.faction.name` when non-null, status `" | boarding in progress"` when `b.simulation != null && !b.simulation.isComplete`, else `" | " + op.phase` when `Singleton<DungeonManager>.HasInstance && (op = Singleton<DungeonManager>.Instance.GetOperation(b)) != null`, else `" | depleted"` when `b.data.isFullyDepleted`, else nothing; wreck `main` = `c.displayName + " | " + parts` where parts lists `"items"` (`data.HasItems()`), `"surface"` (`HasSalvage(TargetLayer.Surface)`), `"core"` (`HasSalvage(TargetLayer.Core)`) joined by `", "`, or `"empty"`; loot `main` = `Translation.Translate(type.displayName) + " x" + total + (pieces > 1 ? " (" + pieces + " stacks)" : "")` coloured `type.rarity.GetColor()`, `value` = `GameMath.FormatNumber(type.sellValue * total) + " cr"`; every `dist` = `distance.ToString("0")` or `"-"`. Separators are ASCII ` | ` (the pixel font has no middle dot).
5. `ContactsBadge : MonoBehaviour` (`Features/Contacts/ContactsBadge.cs`, `public static ContactsBadge Instance`):
   - `CreateWhenReady()`: wait up to 60 s for `HudButton.Instance != null`; on timeout log `[Contacts] no QoL button; counter not shown`; if `Instance != null` return. Build: `var qol = (RectTransform)HudButton.Instance.transform; var rt = UiCards.NewChild("IgiQoL_Contacts", qol.parent); rt.anchorMin = qol.anchorMin; rt.anchorMax = qol.anchorMax; rt.pivot = qol.pivot; rt.sizeDelta = new Vector2(260f, 18f); rt.anchoredPosition = qol.anchoredPosition + new Vector2(0f, qol.rect.height + 4f);` (the QoL button placed itself above the Dock button the same way; the parent `BottomMiddleDivide` has no layout group); `bg = UiCards.AddBackground(rt.gameObject, ColorHelper.panelHeaderBlue)`; `text = FleetBarParts.AddLine(rt, "", 11f, ColorHelper.offWhite, TextAlignmentOptions.Center); UiCards.Stretch((RectTransform)text.transform);` `button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = bg; button.onClick.AddListener(() => ModWindow.Open("contacts"));` `FleetBarParts.SetTooltip(button, null, "What is left at this location. Click for the Contacts tab.")`; `Instance = rt.gameObject.AddComponent<ContactsBadge>()` holding `bg`, `text`, `button`. Wrap in try/catch → `Contacts.Instance.Fail`.
   - `Update` once per second: `b` = active entries of `BoardableUnit.all`, `w` = `SalvageContainer.all.Count`, `l = Contacts.PruneLoot()`; `show = Contacts.Instance.HudCounter.Value && GameplayManager.Instance?.spaceShip != null && b + w + l > 0`; `bg.enabled = text.enabled = button.interactable = show`; text = the non-zero parts of `b + " boardable"`, `w + (w == 1 ? " wreck" : " wrecks")`, `l + " loot"` joined by `" | "`. `OnDestroy`: `Instance = null`.
6. SelfCheck:
   - `loot-registry`: SKIP "not in space" unless `BasePoiManager.current != null && Singleton<LootManager>.HasInstance && GameplayManager.Instance.spaceShip != null`; SKIP "no ore item type" without an `Ore` type. Else `var data = new TractorableItemData { itemType = ore, itemAmount = 3, jettisoned = true, ownerFaction = Faction.player, position = (Vector2)GameplayManager.Instance.spaceShip.transform.position + new Vector2(0f, 60f) }; var item = Singleton<LootManager>.Instance.SpawnLootItem(data, null); bool registered = Contacts.Loot.Contains(item); Object.DestroyImmediate(item.gameObject); int after = Contacts.PruneLoot(); bool pruned = !Contacts.Loot.Contains(item);` → PASS when `registered && pruned`, detail `registered=.. pruned=.. loot=<after>`. The item never lives through a frame.
   - `tab`: `ModWindow.Open("contacts")`; `yield return null;` (lets `Start` build the rows); PASS with detail `boardable=<n> wrecks=<n> lootGroups=<n>` when `ModWindow.CurrentTab == "contacts"` and `column.GetComponentsInChildren<TMP_Text>()` contains texts starting with `Boardable ships (`, `Wrecks (` and `Loot (`; `yield return ctx.Screenshot("window-contacts")`; `ctx.Cleanup(ModWindow.Close)`.
   - `counter`: PASS with the badge text when `ContactsBadge.Instance != null`, else SKIP "no QoL button".
7. Build; launch; expect `[Contacts] active (1 patches)`, `CHECK Contacts/loot-registry PASS|SKIP`, `tab PASS`, `counter PASS|SKIP`; read `igiqol-window-contacts.png` (three headings, rows or "None"; the counter line above the QoL button when anything is around).

### Step 5. Full run and hand-off

1. Build; launch with `[Dev] Enabled = true`; expect `IgiQoL startup: 18 active, 0 disabled, 0 failed`, zero `FAIL`, every SKIP with its stated reason, screenshots `igiqol-window-itemrules.png`, `igiqol-window-contacts.png` plus the Phase 2a set; read the two new ones and fix layout defects (a control off its column, clipped tab buttons, overlapping text) before finishing.
2. Launch without the harness: `[Dev] Enabled = false`, `bash /tmp/igiqol-launch.sh "IgiQoL startup:"`, wait 20 s, `pkill -f VanguardGalaxy.exe` at the main menu; the log shows `17 active, 1 disabled` and no `switched off`.
3. Delete `$S/igiqol-dev.save`, `$S/igiqol-dev.meta`, `/tmp/igiqol-launch.sh`; leave `[Dev] Enabled = false`. Copy this plan to `PHASE_2B_RULES_CONTACTS_PLAN.md` in the repo root and append an `## Execution notes (<date>)` section there with what differed from the plan (icon/font/layout findings, SKIP reasons), as `PHASE_2A_MOD_WINDOW_PLAN.md` does.

## Critical files & anchors

- `decompiled/Behaviour.Gameplay/IdleManager.cs` — `TryDropCargoOf` (:321-355), `ShouldKeepInCargo` (:357), `DropFoundItem` (:370), `SellItemFromMaterials` (:456): the prefix in Step 3 repeats this body verbatim around the rule hooks.
- `decompiled/Behaviour.UI/PlayerCargo.cs` (`AutoDepositOnDock` :113, `DepositFleetCargo` :121, `GetManagedFleetShips` :58) with `decompiled/Source.SpaceShip/SpaceShipData.cs` `TransferCargo` (:673-715): the whole-stack move `DepositFleetShip` copies.
- `decompiled/Behaviour.Salvage/SalvageContainer.cs` — `OnMouseUpAsButton` (:171-191): the body `Contacts.Salvage` copies; `all` (:33), `displayName` (:41).
- `src/IgiQoL/Features/Operations/Operations.cs` — `OperationsList` (:168-304): rebuild-on-change / refresh-per-second pattern for `ContactsList` and `ContactsBadge`.
- `src/IgiQoL/Features/EchoPriority/EchoPriority.cs` — `BuildEcho` (:161-216): add-row dropdown + button and `Edit` → `ModWindow.Show` rebuild pattern for the Item rules tab.

## Verification

All runs use the loop and `[Dev]` settings from Conventions with the dev copy of the newest autosave.

- Step 1: `CHECK TractorCaps/resolve-ammo PASS`, `gate PASS|SKIP`, `EchoGates/fast-unload PASS`; no `[ItemRules]` import line (file has no rules); `15 active`. Then, with the game closed, put `{"default":{"tractorCap":0},"categories":{"Ammo":{"tractorCap":40,"dockAction":"Sell"}}}` into `$G/BepInEx/config/IgiQoL/item-rules.json`, launch once more and expect `[ItemRules] imported 1 rules from item-rules.json into this save` and `CHECK TractorCaps/resolve-ammo PASS - ... tractorCap=40`; restore the file to its previous content afterwards (the dev save is discarded in Step 5, so the imported record goes with it).
- Step 2: `[ItemRules] active (0 patches)`, `CHECK ItemRules/roundtrip PASS` (`5/SellAbove/10`, record written), `tab PASS`; `igiqol-window-itemrules.png` shows the column header, the Default row, the Ore category row with `5` / `Sell above N` / `10`, both add rows and the search box with the lens icon.
- Step 3: `[EchoCargo] active (2 patches)`, `CHECK EchoCargo/plan PASS`, `move-cap PASS` (`moved=7 from=43 to=7`), `live-keep PASS|SKIP`. User confirms in play (ECHO on, docked at a station with a shop): a category rule `Sell` on Junk sells junk from the flagship and from a fleet ship with the ECHO status line "Sold N <item>"; `Keep aboard` on Ore leaves ore in cargo; `Sell above 50` on an ore type deposits until materials storage holds 50 and sells the rest; with fleet auto-deposit on, docking by hand leaves `Keep`/`Sell` items on the fleet ship and deposits the rest; with no dock rules at all (`HasDockRules` false) the log shows no `[EchoCargo]` lines and unloading is vanilla.
- Step 4: `[Contacts] active (1 patches)`, `CHECK Contacts/loot-registry PASS` (SKIP only with the stated reason), `tab PASS`, `counter PASS|SKIP`; `igiqol-window-contacts.png` shows the three headings. User confirms in play after a fight: the counter reads e.g. `2 boardable | 1 wreck | 14 loot` above the QoL button and disappears when everything is collected; Board opens the boarding panel, Salvage opens the salvage window / issues click-work, Tractor makes the flagship's tractor pull the nearest piece of that item.
- Step 5: `18 active, 0 disabled, 0 failed`, zero `FAIL`; main-menu launch shows `17 active, 1 disabled` and no `switched off`; `[Dev] Enabled = false`; dev save and helper removed; `PHASE_2B_RULES_CONTACTS_PLAN.md` present with execution notes.

## Assumptions & contingencies

- Rules are per save; a save without an `ItemRules` record is seeded once from `item-rules.json` if that file holds any rule (every existing save gets the seed on its first load). If you'd rather keep one shared file for all saves, say so before approval; the plan then keeps today's file store and only adds the tab on top of it.
- The dev save is wherever the newest autosave left you; `live-keep`, `loot-registry` and `counter` SKIP when their stated precondition is missing, and you confirm those in play. No second save is prepared.
- `GoToLocation(shop facility)` registers the shop screen synchronously (vanilla sells right after it, `IdleManager.cs:458-463`). If `isShopOpen` is still false after the call (the `[EchoCargo] shop screen did not open` warning appears), change `CargoRules.Sell` to a two-tick form: tick 1 calls `GoToLocation` and returns true without selling, tick 2 sells when `isShopOpen`; keep the warning for the case where it is still closed on tick 2.
- `UITooltipParent.ItemTooltipPrefab` is expected to be set in the HUD scene; `Kit.ItemTooltip` no-ops when it is null, so item rows then have no hover card and nothing else changes.
- The six tab buttons (90 px) fit the 760 px header by arithmetic; if the Settings tab or the close tile is clipped in `igiqol-window-itemrules.png`, set the tab width to 80 in both places in `ModWindow.Build` (`Kit.TextButton(..., 80f)` and `SetFixedSize(..., 80f, 22f)`).
- `BoardableUnit.all` and `SalvageContainer.all` may briefly hold objects being destroyed; the lists filter null/inactive entries, and a row whose object died between refreshes is removed at the next second.
- If `SpawnLootItem(data, null)` throws inside `TractorableItem.SetData` because of a missing POI field (the `loot-registry` check FAILs with that trace), set `data.keepUntilCollected = true` and `data.producerGuid = GameplayManager.Instance.spaceShip.unitData.guid` before spawning; if it still throws, downgrade the check to SKIP with the exception message and rely on the in-play confirmation.

## Execution notes (2026-10-30)

- `TractorCaps/gate` and `EchoCargo/live-keep` restore the rules with `ItemRules.Load()` in a `finally` right after the test, not through `ctx.Cleanup`. Cleanups only run after every feature's check, so the test tables stayed live during later checks, and `ItemRules/roundtrip` would have written them into the save record.
- Item rules tab: the "Pickup limit" header was cut to "Pickup" at 60 px and to "Pickup li" at 80 px. The column is 96 px in both the header and `RuleRow`. Six 90 px tab buttons fit the header with the close tile visible, so the tab width stays 90.
- The search results show "No item without a rule matches." when nothing matches. The plan only covered the over-20 case.
- The import check worked as planned: `[ItemRules] imported 1 rules from item-rules.json into this save`, then `TractorCaps/resolve-ammo ... tractorCap=40`. `item-rules.json` was restored afterwards.
- `SpawnLootItem(data, null)` did not throw. `loot-registry` passed without the `keepUntilCollected`/`producerGuid` fallback.
- The dev save sits at a cleared Combat location (no boardables, wrecks or loot), so the final Contacts screenshot shows three "None" groups. A throwaway run (reverted) kept two spawned ore stacks alive through the screenshot. It showed one loot row `Empty Cell x12 (2 stacks) | 768 cr | 400 | Tractor` and the counter `2 loot` above the QoL button.
- Final harness run: 18 active, 33 pass, 0 fail, 3 skip. The skips: `EngagementDistance/live` (no target), `EchoPriority/dropdown-to-list` (no Autopilot tab in the scene), `EchoCargo/live-keep` (not docked). The selling paths of ECHO cargo rules (`CargoRules.Sell`, `DepositFleetShip`) have not run in this session and need the in-play confirmation listed under Verification.
- Main-menu launch without the harness showed `17 active, 1 disabled` (Dev) and no `switched off`. The dev save and the launch helper are deleted, and `[Dev] Enabled = false`.
