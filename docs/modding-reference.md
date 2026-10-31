# What mods can do

This comes from reading the decompiled code. Paths are relative to `decompiled/`. "Postfix" and "prefix" are Harmony patches that run after or before a game method and can change its arguments or result. Line numbers are for game version 0.8.3.4 and drift with updates.

## Startup timing

`Bootstrapper.Init` (`Behaviour.Bootstrap/Bootstrapper.cs:49`) runs as `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`, before the first scene. It fills every content registry in a fixed order: items, recipes, aspects, hazards, crew skills, skill trees, equipment builders, item builders, ships, drones, station parts, modules, defensive turrets, crew types, dungeons. Then it loads the main menu.

Plugins load before this runs. The test plugin (`src/HelloVanguard`) saw 0 registered items in `Awake` and 393 once `InventoryItemType.LoadAll` finished. So a content mod should postfix the registry's `LoadAll()` and edit content there. Don't touch registries from `Awake`, because they're still empty then.

This also means content edits are in place before any save loads, which matters because saves refer to content by name.

When a save loads, `SceneLoader.LoadScenesOnStartGame` (`Behaviour.Bootstrap/SceneLoader.cs:175`) sets up the session and then loads the HUD, side panel and gameplay scenes additively and asynchronously (`LoadNeededScenes`). `GamePlayer.current` is set by the time it returns, but `HudManager.Instance` isn't: the HUD exists from `HudManager.Start` on, once per save load. Returning to the main menu tears the scenes down again.

## Gameplay numbers (easy)

| Goal | Hook |
|---|---|
| XP multiplier for kills, mining, salvage and missions together | postfix the 3-argument `Source.Util.GameMath.GetExperienceRewardValue(float, int, int)` (`Source.Util/GameMath.cs:329`); the 2-argument overload calls it |
| Commander XP only | prefix `GamePlayer.GainExperience(long)` (`Source.Player/GamePlayer.cs:3174`) |
| Crew XP | prefix `AbstractUnit.AddCrewExperience` and its `SpaceShip`/`Drone` overrides |
| Mission credit rewards | postfix `MissionGenerator.CreditReward` (`Source.MissionSystem/MissionGenerator.cs`, around line 238) |
| Cheaper or free spending | prefix `GamePlayer.RemoveCredits(double, string)` (`GamePlayer.cs:549`); the string is a spending category |
| Mining yield | prefix `Asteroid.SpawnSurfaceOre` / `SpawnInnerCoreOre` (`Behaviour.Mining/Asteroid.cs:513`) and scale `yield` |
| Salvage scrap | postfix `SalvageData.ComputeScrapAmount` (`Source.Data.Persistable/SalvageData.cs:609`) |
| Loot drops | `LootManager.DropLoot`, `DropRegularLoot`, `CreateLootItem` (`Behaviour.Managers/LootManager.cs`) |
| Damage dealt or taken | `DamageData.CalculateDamage` (`Behaviour.Weapons/DamageData.cs:136`) or `AbstractUnit.TakeDamage`; there is also a static `DamageData.OnDamageDealt` event you can subscribe to without patching |
| Market prices | `MarketPricing.GetTargetValue` and `Trade` (`Source.Simulation.Economy/MarketPricing.cs`) |
| Game speed | `GameTimeScale` (`Behaviour.Util/GameTimeScale.cs`) |
| Patrol intensity | `CombatManager.securityPatrolMultiplier` (`Behaviour.Combat/CombatManager.cs:14`) |

Some tuning values in `GameMath` are C# `const`s (`FastLaneTravelMultiplier`, `LootBoxOpenPrice`, and others). The compiler copies those into every caller, so patching the constant does nothing. Patch the methods that use them.

## Content stats (easy to medium)

Every content type is a Unity prefab or ScriptableObject loaded from `Resources` into a static dictionary keyed by asset name:

| Content | Registry | Lookup |
|---|---|---|
| Items | `InventoryItemType` | `Get`, `TryGet`, `all` |
| Crafting recipes | `CraftingRecipe` | `Get`, `TryGet`, `all` |
| Equipment templates | `EquipmentBuilder` | `Get`, `TryGet` |
| Item templates | `ItemBuilder` | `Get`, `TryGet` |
| Ship hulls | `SpaceShip.allShips` | `Get`, `GetAll` |
| Drones, defensive turrets, station parts | `Drone`, `DefensiveTurret`, `CombatStationPart` | `Get` |
| Crew types, skill trees, skill nodes | `CrewType`, `Skilltree`, `SkilltreeNode` | `Get` |
| Equipment aspects, hazards, dungeons | `EquipAspect`, `LocalHazard`, `DungeonDefinition` | `Get` |
| Factions | `Faction` (plain C# class, created lazily by `Faction.Get(id)`) | `Get`, `all` |

You can rebalance anything those objects expose: item value, rarity and level (`InventoryItemType.SetBaseValue`, `SetRarity`, `SetItemLevel`), equipment `capacityCost` and `stats`, hull warp speed and cost (`baseMaxWarpSpeed`, `costMultiplier`), recipe `craftingValueMultiplier` and `unlockedFromStart`, crew cost. Edit the templates before the game clones them, or items that already exist keep their old values. Recipe ingredient lists are private serialized fields. The publicizer exposes them, but there are no setters.

`InventoryItemType.CreateLeveled(baseType, level)` makes a levelled copy of an item type (the game does this for dungeon loot). It throws for item prefabs whose parts keep per-item values on the shared prefab, which includes every ore.

To find a value without writing code, dump the registries with the modpack's `[Dev] DumpGameData` and grep the files ([dev-harness.md](modpack/dev-harness.md#data-dumps)).

## New content (hard)

You can add a new item, recipe, equipment type or hull by cloning an existing one (`Object.Instantiate`), changing it, and adding it to the registry under a new name. Saves store items by builder or asset name. If your mod isn't loaded, or a name changes, the game won't find the content on load: missing items get dropped with a warning, and `ItemBuilder.Get`/`EquipmentBuilder.Get` throw. Removing a content mod can therefore damage a save that uses its content.

Cloning only reuses existing art. New sprites are fine (load a PNG into a `Texture2D` and call `Sprite.Create`). New prefabs, models, shaders or sounds need an AssetBundle built with the same Unity version (6000.4.7f1).

Text comes from `Resources/Language/*` and goes through `Translation.Translate("@Key")`. Postfix `Translation.Translate` to add your own keys or change existing strings. Item names are translated once at creation and saved as plain text, so a text mod won't rename items you already own.

## Saving your mod's own data (medium)

- Write: postfix `GamePlayer.ToJson()` (`Source.Player/GamePlayer.cs:1705`) and add your own key to the returned JSON object. The game uses the `LightJson` library, not Newtonsoft.
- Read: after a save loads, `GamePlayer.current` is set. Postfix `SceneLoader.LoadScenesOnStartGame` (`Behaviour.Bootstrap/SceneLoader.cs:175`) and read your key back.
- Version migrations for vanilla data run in `GamePlayer` around lines 2389-2514 (`GamePatch.IsBelowAndReport`).
- Autosaves serialise on a background thread. Hand `ToJson` a copy of your data, not the live object your code keeps changing.

Unknown keys are likely ignored when the game loads, so an unmodded game should still open the save. I haven't tested that.

## UI (medium)

The game uses uGUI and TextMeshPro, not UI Toolkit.

- Toast message, no patch needed: `Singleton<NotificationManager>.Instance.CreateNotification("text").WithCustomTime(4f).Show();`
- Yes/no or text-input dialogs: `AlertPopup.ShowQuery` / `ShowInput` (`Behaviour.UI/AlertPopup.cs`).
- Extra tooltip lines: postfix `TooltipSource.AddCustomContent(UITooltip)` and call `tooltip.AddTextLine(...)`. Ship, POI, crew, skill and drone tooltips have static card builders (`ShipTooltipCardBuilder.Build` and others) you can postfix to add rows.
- New windows: the side-menu tabs are a closed enum (`SidePanel.SideTabType`) built from prefabs, so a mod can't add a real tab. Build your own uGUI panel under `HudManager.Instance` or `UITooltip.tooltipParent`, or draw an IMGUI window from your plugin's `OnGUI` (the quickest option for tools and debug panels).
- HUD elements: create them from a postfix on `HudManager.Start`, which runs once per save load with `HudManager.Instance`, `dockButton` and `rootCanvasTransform` set. The side panel (`SidePanel.instance`) loads in the same batch but can arrive a few frames later.
- Settings: `SettingsMenu` sub-pages can be postfixed to add rows. Mod settings normally live in the BepInEx `.cfg` file instead. The modpack's Mods tab shows every other plugin's `.cfg` entries (and reads ConfigurationManager's `ConfigurationManagerAttributes` tags), so a mod gets in-game settings by binding them with `Config.Bind`. ConfigurationManager does the same in its own window.
- UI scale: the game's Scale setting is one `CanvasScaler.scaleFactor` for the whole canvas (`Behaviour.Transparency/CanvasScaleUpdater.cs`, `GameplayerPrefs.GetScaleFactor`), so text and panels always grow together and there's no separate text size. The HUD is laid out as a strip 1920 units wide and 324 tall, so Scale tops out at window width / 1920 × 100 (176 for a 3389 px wide window). A higher value only takes effect when the window is also tall enough for the stacked two-row layout (`ScreenSettings.DetermineStack`, `CalculateScale`). The modpack's `TextScale` feature scales fonts on top of that by multiplying the size TextMeshPro lays text out at (`TextMeshProUGUI.GenerateTextMesh`, `TMP_Text.CalculatePreferredValues`).
- Extra station buttons: `PersonalFleetButton.Create(tabPrefab, below, root, icon, windowPrefab, spec)` (`Behaviour.UI.Spacestation/PersonalFleetButton.cs`) makes a station tab that floats above another tab; set the result's `SpaceStationButton.clickOverride` to use it for something else. The modpack's QoL station tab is built this way.
- Unity objects: compare them with `== null` / `!= null`. The `?.` and `??` operators skip Unity's destroyed-object check, so they treat a destroyed component as alive.

## Input

The game uses Unity's new Input System through a generated `PlayerControls` class (`decompiled/PlayerControls.cs`). Some UI code also uses legacy `Input.GetKeyDown`. Both work from a mod. Create your own `InputAction` with a binding like `"<Keyboard>/f9"`, or check `UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame` in `Update()`. Skip the hotkey while a text field has focus (`UIHelper.IsTypingInInputField()`).

## Galaxy, missions, AI (hard)

- Galaxy generation is in `SandboxWorld.SetupWorld` (`Source.Simulation.World/SandboxWorld.cs:209`), seeded from the clock through `SeededRandom.Global` (a public static you can replace for repeatable galaxies). Sector count, jump-gate links, dungeons and side content all have their own methods in that file.
- New mission types: subclass `MissionGenerator` and implement `GenerateMission` and `GetMissionType`. How generators get registered in `MissionGenerator.generators` hasn't been traced yet. Missions are generated per station in `MissionBoard`.
- Ship AI is in `Source.SpaceShip.Auto/` (`CombatActions`, `ThreatTable`, miner and salvager actions). Fleet and faction AI is in `Source.Galaxy/` (`ConquestFleetPlanner`, `EmpireFleets`, raids). It runs on `LivingUniverse`'s hourly tick.
- ECHO, the autopilot, is `Behaviour.Gameplay/IdleManager.cs`. It decides in its `Update` loop every 8-12 s; most of the modpack's ECHO features patch it.

## Leave alone

- `SteamAchievement.Clear` resets all your achievements and stats. Don't call it, and don't unlock vanilla achievements from a mod.
- `SteamCommunication.supporterEdition` and the DLC checks gate paid content.
