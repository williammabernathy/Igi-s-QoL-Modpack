# Planning brief for the detailed plan

This brief is the starting point for the session that writes the detailed plan. For each feature it gives the current stance after game update 0.8.3.4, where the code now lives, what vanilla already does, what you've already decided, and what still has to be worked out. It isn't the plan itself. The designs in [`qol-modpack-plan.md`](qol-modpack-plan.md) and [`mod-plans.md`](mod-plans.md) still apply unless an item below says otherwise.

## Sources and line numbers

- `decompiled/` is game version 0.8.3.4 (beta build 26304333, 29 October 2026). Every `file:line` in this brief points there.
- `decompiled-0.8.3.2/` is the previous build. The line numbers in `qol-modpack-plan.md` and `mod-plans.md` point there. To see what the update changed, run `diff -u decompiled-0.8.3.2/<file> decompiled/<file>`.
- I checked the 83 `Class.Member` names in the two plan docs against 0.8.3.4. 82 still exist. The only one gone is `FleetCargoPopup` (#13). Method bodies changed in places; the items below say where.
- One statement in `mod-plans.md` §4 is wrong for 0.8.3.4: jump gate passes now go to the data inventory and boss signals to materials storage. Neither goes to the armory any more.
- Anything not read in code is marked [INFERENCE].

## What 0.8.3.4 changed that matters to the pack

- Item refactor. `InventoryItemType` is now a plain C# class wrapping an `ItemDefinition` prefab component (`Behaviour.Item/ItemDefinition.cs`), not a `MonoBehaviour`. Per-item data moved from `GetComponent<XItem>()` to `TryGetState<XState>()` (`InventoryItemType.cs:594`); for example `CrewPodItem.rollOnPickup` is now `CrewPodState.rollOnPickup`. Recipes and turrets refer to items through `ItemDefinition` (`ItemDefinition.TemplateOf(prefab)`; `AbstractTurret.ammoType` is an `ItemDefinition`, `AbstractTurret.cs:80`). The public surface the pack uses survived: `identifier`, `CanStackWith` (`:613`, logic unchanged), `ToJson` (`:935`), `Get`/`TryGet`/`all`, `LoadAll` (`:1162`), `CanBeEquippedOn` (`:780`).
- Data inventory. Data, blueprints, signal fragments and now jump gate passes go to `GamePlayer.dataInventory` (`GamePlayer.cs:352`). `Inventory.Add` redirects them there from any player-owned inventory, fleet cargo included (`Inventory.TryRedirectFromPlayerInventory`, `Inventory.cs:560`). Picking them up needs no cargo space (`TractorModule.CanFitInCargo`, `:84`).
- Fleet cargo window. `FleetCargoPopup` was replaced by `FleetCargoSection` (`Behaviour.UI/FleetCargoSection.cs`). It opens as a window from a fleet row's right-click (`ToggleWindow`, `:102`) and as a slide-out beside the Inventory side tab. It has per-ship tabs, drag and drop, "move all to flagship", a per-ship tractor toggle (`:451`) and a fleet Deposit button.
- Fleet auto-deposit. `GamePlayer.autoDepositFleetCargo` (`GamePlayer.cs:231`) is saved in the save file. On dock, `PlayerCargo.AutoDepositOnDock` (`PlayerCargo.cs:113`, called from `SpacestationExteriorManager.cs:338`) empties every non-mercenary fleet ship's hold in one step through `SpaceShipData.TransferCargo(materials, armory, keepEssential: false)` (`SpaceShipData.cs:673`). Favourites stay; the flagship isn't included. [INFERENCE: this also fires when ECHO docks, because ECHO docks through the same callback (`IdleManager.cs:262`).]
- ECHO ammo restock now works out the amount from fire rate × `autopilotSettings.ammoSeconds`, capped at 50% of cargo (`Source.SpaceShip/AmmoSupply.cs`). It takes partial amounts from the armory or the shop (`IdleManager.TryRestockAmmo`, `:608`).
- ECHO now takes over a boarding or dungeon operation you started by hand (`DungeonManager.TakeOverPlayerOperation`, `:427`, called from `IdleManager.HandleActiveDungeonOp`, `:215`).
- Torpedo bays now follow the scanner's auto target (`AbstractTargetingModule.cs:217`).
- Drones no longer carry cargo. `Drone.cantFitMoreCargo` and `DroneBayModule.UnloadDroneCargo` are gone.
- `CompleteTravel` now runs only after the destination scene has loaded (`TravelManager.WaitForArrivalScene`; call at `:703`, fleet ships at `:704-710`).
- Unchanged: `AutopilotSettings.cs` and the Autopilot side tab (`Autopilot.cs`) are identical to 0.8.3.2. Nothing new was added to ECHO's settings.

## Core layer (#21)

Stance: internal, as decided. No public API until someone else wants to build on the pack. This update supports that: a point beta release changed about 400 files.

Two additions came out of this update and need designing:

- Game adapter folder. A few static classes (working names `Items`, `Cargo`, `Fleet`) wrap the game calls that two or more features share, so the next refactor like the item one is fixed in one folder. Candidates found so far:
  - `Items.StorageOf(item)`: cargo, armory, materials or data (`CanGoInDataInventory` `:1247`, `CanGoInArmory` `:1263`, `CanGoInMaterials` `:1300`).
  - `Cargo.Count(ship, item)`, which adds `dataInventory` for data items. #3 needs it.
  - `Cargo.MayLeave(ship, stack, path)`: one decision point for the item rules (#15), called from each unload path.
  - `Fleet.CargoShips()` → `GamePlayer.GetCargoShips()` (`GamePlayer.cs:1086`; new public method, it used to be private in `IdleManager`).
  - `Items.State<T>(item)` → `TryGetState<T>`.

  Keep the folder narrow; wrapping every game call would make it a second game API to maintain.
- Startup warm-up. Harmony patches a method even when the patch body refers to a field or type that has changed. The failure only comes when the body first runs, usually inside an `Update` loop. After patching each feature, force-compile its patch methods (`RuntimeHelpers.PrepareMethod`) inside the feature's try/catch so a broken feature switches off at the main menu. Phase 0 has to check that Mono raises `MissingFieldException`/`MissingMethodException` at that point. If it doesn't, fall back to a try/catch inside each patch that switches its feature off on the first exception.
  Warm-up probe on 0.8.3.4: `PrepareMethod` did not throw for a missing field or a missing type; only the direct call threw `MissingFieldException`. Broken patches therefore switch off on first use through the per-patch try/catch, not at the main menu.

Settled pieces to carry over unchanged: the feature registry, the version check (`GameVersion.GetVersion`), one `igiQol` save key with per-ship records keyed by `SpaceShipData.guid`, the mod window tabs, the per-ship popup, and the shared engines for targeting and item rules.

Lifecycle hooks in 0.8.3.4: `GamePlayer.ToJson` `:1793`, `FromJsonStaged` `:2079`, `SetSpaceShipData` `:3273`, `StartAutopilotSession` `:958`, `EndAutopilotSession` `:940`, `SpaceShip.CompleteTravel` `SpaceShip.cs:1252`, `SceneLoader.ToggleSpaceStationInterior` `:151`, `LoadScenesOnStartGame` `:175`.

UI kit: the game builds newer UI in code with `FleetBarParts` (`Behavior.UI.GalaxyMap/FleetBarParts.cs`: `AddRow`, `AddTile`, `AddTextTile`, `AddActionButton`, `AddFillBar`, `SetTooltip`, `SetTileState`), `CaptainCards.SetFixedSize` and the icons from `PoiIconKeys.Fleet(name)`. The new fleet cargo window is built entirely this way. Calling those helpers matches the game's style with no prefab cloning, so the UI kit should start from them and clone prefabs only for controls they don't cover (dropdowns, text fields, sliders).

Update workflow:
1. Rebuild against the new DLL. `Directory.Build.props` references the installed `Assembly-CSharp.dll`, so `dotnet build` turns changed types and removed members into compile errors. Hello Vanguard rebuilt against 0.8.3.4 with 0 errors.
2. Decompile into a new folder and diff the files the pack patches.
3. Start the game once and read the per-feature report.

Steps 1 and 2 could be one short script.

Open for planning: the adapter's first list of methods; where the warm-up runs; whether the patch-target list comes from the patch attributes at build time or from reflection at startup.

## Phase 0 checks that block planning

- Save-data test: add `igiQol`, save, reload, remove the plugin, reload.
- Warm-up test (core layer above).
- UnityExplorer pass: fleet row (#12, #13), travel HUD (#1), forge recipe panel (#18), installation panel (#20), Inventory side tab with the fleet cargo slide-out (#15 tab placement).
- Logging pass: turret `_range` per weapon (#7), missile speed and turn rate (#8), whether a destroyed ship's GameObject is destroyed or deactivated (#6), whether `BoardableUnit.all` keeps captured or destroyed ships (#2).
- Recipe dump (#18).
- In game: whether fleet auto-deposit fires on ECHO docks (#15, #17).

## Features

### 1. Jumps remaining (wishlist: "number of jumps remaining")

Stance: Easy, unchanged. Vanilla still shows only distance, speed, ETA and fuel on the HUD.

- Hook: postfix on `TravelInfo.UpdateTravelInfo` (`Behaviour.UI.Travel/TravelInfo.cs:121`, unchanged since 0.8.3.2). Count from `GamePlayer.waypoints` (`GamePlayer.cs:183`). The destination tooltip already computes `waypoints.Count - 1` (`TravelDestinationInfo.cs:53`).
- Waypoints are removed on arrival (`TravelManager.cs:715`).
- Open: whether `Count - 1` matches the real hops, or whether the count should include only `JumpGate` and `Wormhole` entries. Label placement needs the UnityExplorer look at the travel HUD.

### 2. Contacts after battles (wishlist: "local scanner")

Stance: Medium, unchanged. You decided this is a visibility feature: everything at a POI is already spawned, so there's nothing hidden to reveal.

- Sources: `BoardableUnit.all` (`BoardableUnit.cs:33`, event `OnBoardingChanged` `:53`); salvage through a postfix on `SalvageData.AddToWorld` (`SalvageData.cs:393`, returns the spawned `GameObject`); loot through `LootManager.CreateLootItem`/`SpawnLootItem`/`RemoveLootItem` (`LootManager.cs:155`, `:197`, `:245`).
- Vanilla overlap: the new dock button lists dockable stations and installations at the POI (`Behaviour.UI.HUD/DockTargets.cs`). It leaves out ship-based targets, so boardable ships are still uncovered.
- Edge markers: copy `EnemyIndicatorManager`/`EdgeIndicator`.
- Open: badge placement; whether clicking a row should reuse `BoardableUnit.OnMouseUpAsButton` (`:219`); how often the list refreshes; whether wrecks with nothing left get filtered out.

### 3. Tractor caps (wishlist: "loot filter")

Stance: Easy, still needed. Decided: a per-item cap, "stop auto-tractoring once cargo holds N", with 0 meaning never.

- Hook: postfix on `TractorableItem.CanBeAutoTractoredBy` (`:130`). It's still the one gate for your ship, fleet ships and ECHO's loot-before-leaving pass.
- New in 0.8.3.4: your own ships and fleet ships skip the claim grace delay (`IsPersonalFleetShip`, `:155`). Data items go to `dataInventory`, so the cap count has to include it (`Cargo.Count` in the adapter). Clicking an item by hand still bypasses the gate (`TractorModule.SetManualTarget`, `:216`).
- Shares the rule table with #15. Category rows can reuse `ItemCategory`. The inventory's own filter groups (`InventoryFilterCategory`: Ore, Salvage, Modules, AmmoAndFuel, …) are a possible coarser grouping for the UI.
- Open: whether a cap counts the ship's own cargo only or the whole fleet's (`GamePlayer.CountInFleetCargo`, new, `:1106`); how rejected loot gets flagged so you can see why it wasn't picked up.

### 4–5. Weapon targeting rules and fleet target priority (wishlist: "better turret targeting")

Stance: Medium, unchanged; still not in vanilla. Decided: one rule per weapon per ship; manual targets and attack orders always win.

- Hooks: `AbstractTurret.currentTarget` getter (`:201`); `AbstractTargetingModule.priorityTarget` (`:68`), `UpdateAutoTarget` (`:199`), `filteredTargets` (`:18`), `FindClosestFilteredTarget` (`:205`, `:221`); `SpaceShipHardpoint.index` (`:52`); `WingmanActions.UpdateStanceTargeting` (`:291`), `IsPastDefensiveLeash` (`:373`), `OnDamageTaken` (`:471`, the flee check at 10%, now skipped for arena units).
- New in 0.8.3.4: torpedo bays copy the scanner's auto target (`AbstractTargetingModule.cs:217`). They aren't `AbstractTurret`s, so the plan has to decide whether weapon rules cover torpedo bays and which hook does it (`TorpedoBayModule.AddManualTarget`).
- The four stances already existed in 0.8.3.2; the update didn't change them.
- Open: the getter runs every frame for every turret, so the lookup has to avoid allocating (planned: an array indexed by hardpoint); how far to re-pick when a rule changes mid-fight; what the per-ship popup shows for a hardpoint whose weapon changed.

### 6. Missile auto-retarget (wishlist: "missiles auto-retarget toggle")

Stance: Easy, unchanged.

- Hooks: `Missile.FixedUpdate` (`:112`), `SeekNewTarget` (`:164`), `Initialize` (`:71`); `SwarmMissile` overrides `Initialize`.
- Open: the destroyed-GameObject check from Phase 0; whether "nearest valid enemy" uses the parent ship's `filteredTargets` or a fresh search.

### 7–9. Engagement distance, missile evasion, separation (wishlist: "better AI ship behaviour")

Stance: unchanged. Decided: separation and evasion are per-ship toggles, off by default.

- Distance: `UpdateApproachDistance` (`AbstractTargetingModule.cs:130`). The cap of 30 is still hard-coded at `:144`.
- Evasion: reactive dodging uses the constants at `AbstractUnit.cs:166-178` and the trigger at `:2172-2174`; `SpaceShip.MoveOnCombatDamage` (`:649`) still limits it to ships of size under 3. Missile registry from `Missile.Initialize`.
- Separation: `ShipNavigator.navigators` (`:25`); the navigator still switches off in combat at `:85`.
- Open: all three depend on the Phase 0 measurements. The plan should decide whether evasion also applies to large ships (vanilla's size limit is deliberate tuning, which brushes against your "don't change the game's tuning" rule).

### 10–11. Drones (wishlist: "better drone behaviour")

Stance: unchanged; still only `FreeRoam` and `FollowShip`. Decided: all candidate modes, follow radius first.

- Deploy on arrival: `DroneBayModule.DeployDrones` (`:526`, still blocked while `travelling` or `inShipYard`), `playerRecalled` (`:76`). Drones are recalled when a trip starts (`TravelManager.cs:599`, `:673-679`). Hook the arrival event after `CompleteTravel` (`TravelManager.cs:703-710`), which now runs once the destination scene has loaded.
- Modes: `Drone.ApplyBehaviorMode` (`:370`), `FollowShipDistance` (`:62`), saved mode written at `DroneBayModule.cs:1122` and read back at `:1145` (unknown names fall back to FollowShip), label in `Behaviour.UI/DroneControls.cs`.
- Drones no longer carry cargo, so the "assist mining" mode doesn't need to handle cargo.
- Open: how the mod's mode shows in the vanilla drone toggle; whether "guard a fleet ship" picks the ship from the per-ship popup or from the fleet row.

### 12. Fleet row vitals (wishlist: "fleet ships vitals display")

Stance: Easy, still needed; the row content didn't change.

- Hook: postfix on `FleetPanelRow.Refresh` (`:108`). It shows name, stance plus activity, a hull bar from `shipData.currentHullHP`, and the cargo gauge.
- Partly answered: shield values are data-side (`AbstractUnitData.currentShieldHP`; `AbstractUnit.currentShieldHP` at `:424` reads it), so rows can read them without a live ship. Whether those values change while a ship is out of the POI is still [INFERENCE].
- Mercenary rows hide the cargo gauge and shift the row buttons (`BindRowButtons`, `:67-78`); vitals have to fit both layouts.
- Open: which vitals (shield, armor, target, distance to flagship); text or bars; refresh cost at the row's 0.25 s rate.

### 13. Fleet row command strip (wishlist: "per-ship behaviour toggles always visible")

Stance: Medium, still needed, smaller than before. Decided: everything visible on each row and usable without selecting the ship.

- Vanilla overlap: the fleet cargo window (right-click a row) has a per-ship tractor toggle and cargo access. The strip can drop the tractor button or keep it as a one-click shortcut; you decide.
- Calls: `FleetCommands.IssueStop` `:150`, `IssueHold` `:158`, `IssueRegroup` `:169`, `SetStance` `:182`. The cargo button is now `FleetCargoSection.ToggleWindow(shipData)`. Drones: `DeployDrones`/`ReturnDrones` (`DroneBayModule.cs:526`, `:730`).
- Build with `FleetBarParts.AddTile` and `PoiIconKeys.Fleet(...)` icons, the way the fleet cargo window does.
- Selection still hijacks right-click (`MovementHandler.TryIssueMoveOrder`, `:76-82`); the command bar still only shows with a selection (`FleetPanel.cs:88`).
- Open: one line or a second line per row; how mercenary rows are handled (the game won't let you manage their cargo, `PlayerCargo.CanManage`); that clicking a strip button doesn't also select the row.

### 14. Ship loadouts (wishlist: "loadouts")

Stance: Medium to hard, still needed. Vanilla has nothing new here. Decided: several loadouts per ship; apply order skill preset → officers → grunt crew → drones → modules and turrets → boosters → weapon rules → autopilot → rebuild; apply what's possible and list what's missing; grunt crew included and never sold.

- Hooks: `PersonalHangar.skillLoadoutButton` (`:51`, shown around `:657`); `CaptainData.SetSelectedLoadout(int)` (`:417`), `RefundAllSkills` (`:198`, call with `updateLoadout: false`); `SpaceShipData.skillLoadout` (`:95`), `AssignOfficer` (`:1104`), `AddCrew` (`:937`), `CheckDroneLoadout` (`:1761`); `AbstractUnitData.EquipItem` (`:673`), `EquipModule` (`:697`), `RemoveModuleOfType` (`:715`); `GameplayManager.RefreshShipInSpace` (`:488`).
- New in 0.8.3.4: the hangar ship-assignment UI was rebuilt (`Behaviour.UI.Spacestation.Hangar/Assignment*.cs`, `IShipAssignmentContext`). Check the button injection point against it. `ShipLoadoutCloner.CopyLoadout` (`Source.SpaceShip/ShipLoadoutCloner.cs`) copies modules and hardpoints from a template ship for NPC and arena use; it may be reusable for the equipment step.
- The item refactor matters here: equipped gear is an `InventoryItemType` with `equipmentBuilder` state, saved with `ToJson`. Matching saved gear against the armory needs a rule: exact JSON match, or same builder plus rarity and level.
- Open: the item-matching rule; how the "missing" popup is built; the same-hull sharing UI; where loadouts are saved (per ship in `igiQol`).

### 15. ECHO cargo handling and selling (wishlist: "better autopilot/ECHO settings")

Stance: Medium, still needed, with one new conflict. Decided: Default, Keep, Stash, Sell and Sell above N; selling by rarity threshold, whole category or specific item; stackable items can keep an amount.

- Hooks, unchanged: `IdleManager.TryDropCargoOf` (`:321`), `ShouldKeepInCargo` (`:357`), `DropFoundItem` (`:370`), `SellItemFromMaterials` (`:456`), `InventoryInteractionManager.SellAmount` (public overload `:1108`).
- Conflict: with fleet auto-deposit on, vanilla empties fleet holds on dock (`PlayerCargo.DepositFleetCargo`, `:121`) before ECHO's unload loop runs, so fleet-ship rules would never apply. Patch `DepositFleetCargo`, not `SpaceShipData.TransferCargo`. `TransferCargo` has five other callers (hangar transfer, relinquish command, the inventory toolbar), and those are manual actions the rules shouldn't touch.
- Unload paths the rules have to cover: ECHO's loop (flagship and fleet ships) and vanilla auto-deposit (fleet ships). Manual Deposit and the toolbar's move buttons stay manual.
- Vanilla auto-sell now skips refinery canisters (`Inventory.CanAutoSell`, `:421`). The mod's Sell action should do the same.
- UI: the inventory now has a toolbar with category filters (`InventoryToolbar`, `InventoryFilterFlyout`). The Item rules tab can copy that look.
- Open: whether rules apply only while ECHO runs or also on your own docks with auto-deposit on; rules per fleet ship or one table for all; how "Sell above N" counts across storages (materials storage is per station, the armory is shared).

### 16. ECHO activity priority list

Stance: Medium, unchanged. Decided: several activities in priority order.

- Hooks: `TryPreferredActivity` (`:649`) still shows "no suitable loadout" at `:659` and returns true, which blocks every fallback. `FindActivityForEquipment` (`:567`) is unchanged apart from restocking. `FindMiningActivity` `:1088`, `FindSalvageActivity` `:1147`, `FindCombatActivity` `:1167`, `IdleActivityInPoi` `:1187`. The "can this fit do it" check is `SpaceShipData.HasLoadout(type, layer)`.
- The list head stands in for the single preferred activity wherever vanilla reads it (`SpaceShip.cs:1690-1695`, guild mission offers).
- Open: whether rotation (switch after N drops or M minutes) is in the first build; how the list edits from the Autopilot tab and the mod's ECHO tab stay in sync.

### 17. ECHO gates and fast unloading

Stance: Easy. Gates unchanged; fast unloading shrinks to the flagship. Decided: fast unloading is a toggle, off by default.

- Gates: `TryLootBeforeLeaving` `:504`, `WouldIncurFactionPenalty` `:1711`, `SelectAutonomousDungeonCrew` `:1735`, `IsCrewBelowThreshold` `:694`.
- Fast unloading: `DropFoundItem` (`:370`) still moves `1 + cargo/200` items per drop. Fleet ships already unload instantly when auto-deposit is on, so the toggle only matters for the flagship, and for fleet ships when auto-deposit is off.
- New: ECHO resumes your manual operations; the interaction with the crew gates is in #20.
- Open: whether the fast-unload toggle should also switch on fleet auto-deposit, or leave that to the vanilla button.

### 18. Crafting roll-up (wishlist: "crafting roll-up")

Stance: Medium, still needed. Decided: target case is a weapon or module recipe missing 3+ refined parts; long forge times are fine; no moving items between storages.

- Hooks: `Forge.TryStartJob` (`:94`), repeating-order limits (`:42-44`); `CraftingRecipe.all` (`:94`), `GetAvailable` (`:487`), `ConsumeIngredientsForCrafting` (`:446`); `ForgeJob.AddForgeItemToCargo` (`:176`); `ForgeTabContents.NavigateToRecipeForItem` (`:278`).
- New in 0.8.3.4: recipe ingredients and results resolve through `ItemDefinition.TemplateOf` (the index has to use it); the forge also draws from the data inventory (`SpaceStation.CountForgeItems`, `:666`); one recipe can now have several repeating orders, and a stock cap applies to all of them.
- Open: answered by the recipe dump (chain depth, forge vs refinery parts, leveled refined items); the plan's state machine for "parts queued → parts arrived → queue the weapon" and where it's saved.

### 19. Notes (wishlist: "personal notes panel")

Stance: Medium, unchanged; no vanilla equivalent. Decided: one set of notes for all saves, user tabs, light Markdown, clickable links.

- Storage: `BepInEx/config/IgiQoL/notes.json`. Hotkeys are already blocked while a text field has focus (`UIHelper.cs:53`).
- Open: the multi-line text field (cloned prefab, or built in code); link clicks under Proton (`Application.OpenURL` is untested on Linux).

### 20. Operations tab (wishlist: "set behaviours from a single panel")

Stance: Easy to medium, unchanged. Decided: pre-set everything that today can only be set at the installation, mainly for ECHO's AFK runs.

- Hooks: `IdleManager.BuildAutonomousDungeonOptions` (`:1724`); `DungeonOperation` constructors (`:91`, `:101`, `:113`, `:124`, `:150`), `SetAutonomous` (`:970`); `DungeonPanel` pre-sets from prefs (`:238-249`), `OnAutoAcceptBuyOutChanged` (`:1693`); prefs in `GameplayerPrefs.cs:422-429`; `DungeonManager.Update` (`:49`, now catches per-operation exceptions).
- New in 0.8.3.4: ECHO takes over your manual operation through `SetAutonomous(true)` on the existing object. The constructor postfix never sees that operation, which is correct: defaults apply to new operations only, and a takeover keeps your choices. Boarding now has a jump-drive countdown (`DungeonPanel` jump-drive row, 120 s); the Operations tab could show it.
- Open: the list layout for running operations; whether the tab also lists ECHO-owned operations.
