# Settings reference

Every setting of Igi's QoL Modpack is in `BepInEx/config/igi.qol.cfg`, one section per feature. You don't need to edit the file: the mod window shows every row under the name in the second column, with the full explanation on hover, and writes the file as you change it. BepInEx rewrites the file at each start with a description and the default above every key, so the file explains itself too.

`Enabled` ("Feature on" in the window) is in every section, defaults to `true` and applies at the next game start. Per-ship overrides from the ship settings popup are saved in the save game, not in this file.

The `[Dev]` section is the developer harness, off by default; it's documented in [dev-harness.md](dev-harness.md).

## Window and HUD

### `[Window]`

[Mod window](features/interface.md#mod-window)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Hotkey` | Hotkey | `F6` | a key name: `F6`, `F7`, `Backquote`, `K`, ... |
| `ButtonPlace` | QoL button in space | `SideMenu` | `SideMenu`, `DockButton` |

### `[TextScale]`

[Text size](features/interface.md#text-size)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Percent` | Text size (%) | `100` | 50 to 200 |

### `[FleetVitals]`

[Fleet shield and armor bars](features/interface.md#fleet-shield-and-armor-bars)

| Key | In the window | Default | Values |
|---|---|---|---|
| `BarHeight` | Bar height (px) | `4` | 2 to 12 |

### `[FleetStrip]`

[Fleet command strip](features/interface.md#fleet-command-strip): only `Enabled`.

### `[JumpsRemaining]`

[Jumps remaining](features/interface.md#jumps-remaining): only `Enabled`.

### `[MissionDistance]`

[Mission distance](features/interface.md#mission-distance): only `Enabled`.

## ECHO, the autopilot

### `[EchoPriority]`

[ECHO activity priority](features/echo.md#echo-activity-priority): only `Enabled`.

### `[EchoGates]`

[ECHO decisions](features/echo.md#echo-decisions)

| Key | In the window | Default | Values |
|---|---|---|---|
| `LootBeforeLeaving` | Pick up loot before leaving | `true` | `true`, `false` |
| `RaidReputationThreshold` | Raid factions at reputation or below | `-500` | whole number |
| `DungeonCrewMinimum` | Crew needed to start an operation | `3` | whole number |
| `BoardingCrewFraction` | Crew needed to board a ship (share of max) | `0.5` | number |
| `HireCrewBelowFraction` | Hire crew when below (share of max) | `0.5` | number |
| `FastUnloading` | Fast cargo unloading | `false` | `true`, `false` |
| `QuickStationSteps` | Quick station steps | `false` | `true`, `false` |
| `RecruitPrisoners` | Recruit willing prisoners before hiring | `false` | `true`, `false` |
| `HireOnlyWhatFits` | Hire only crew that fits aboard | `true` | `true`, `false` |
| `RebalanceCrew` | Make room for short crew types | `false` | `true`, `false` |

### `[EchoMissions]`

[ECHO missions](features/echo.md#echo-missions)

| Key | In the window | Default | Values |
|---|---|---|---|
| `MissionsAtOnce` | Missions ECHO takes at once | `1` | 1 to 5 |
| `BountyHunt` | Combat: Eliminate Pirate Presence / Eliminate (faction) forces | `true` | `true`, `false` |
| `ClearSalvageField` | Combat: Reclaim Salvage Rights | `true` | `true`, `false` |
| `ClearAsteroidField` | Combat: Reclaim Asteroid Belt | `true` | `true`, `false` |
| `ClearDungeon` | Combat: Clear Pirate Base | `true` | `true`, `false` |
| `CaptureShip` | Combat: Capture Enemy Vessel | `true` | `true`, `false` |
| `SalvageWreck` | Salvage: Recover (item) | `true` | `true`, `false` |
| `SalvageSamples` | Salvage: Salvage (material) | `true` | `true`, `false` |
| `MineOre` | Mining: Mine (ore) | `true` | `true`, `false` |
| `OreSamples` | Mining: Collect (ore) | `true` | `true`, `false` |
| `HelpMiner` | Mining: Support (ore) Mining | `true` | `true`, `false` |

### `[EchoRetreat]`

[ECHO retreat](features/echo.md#echo-retreat)

| Key | In the window | Default | Values |
|---|---|---|---|
| `FlagshipHullPercent` | Retreat when the flagship's hull is below (%) | `0` | 0 to 90 |
| `FleetHullPercent` | Retreat when a fleet ship's hull is below (%) | `0` | 0 to 90 |

### `[EchoKit]`

[ECHO abilities and deployables](features/echo.md#echo-abilities-and-deployables)

| Key | In the window | Default | Values |
|---|---|---|---|
| `AlsoWithoutEcho` | Also while you fly | `false` | `true`, `false` |
| `PickUpTurrets` | Pick deployed turrets back up | `true` | `true`, `false` |

### `[OrePreference]`

[Preferred ores](features/echo.md#preferred-ores): only `Enabled`.

### `[EchoOperations]`

[ECHO's operations](features/echo.md#echos-operations)

| Key | In the window | Default | Values |
|---|---|---|---|
| `StealthMode` | Stealth | `Vanilla` | `Vanilla`, `Silent`, `Balanced`, `Aggressive` |
| `AmmoType` | Ammo | `Vanilla` | `Vanilla`, `Standard`, `ArmourPiercing`, `Hollow` |
| `AutoBuyOut` | Accept buyouts automatically | `false` | `true`, `false` |

### `[EchoCargo]`

[ECHO cargo rules](features/echo.md#echo-cargo-rules)

| Key | In the window | Default | Values |
|---|---|---|---|
| `ApplyToAutoDeposit` | Apply when fleet cargo auto-deposits on dock | `true` | `true`, `false` |
| `SellFromData` | Sell from the data inventory | `true` | `true`, `false` |
| `SellFromStorage` | Sell from station storage | `false` | `true`, `false` |

## Fleet and combat

### `[TargetPriority]`

[Target priority](features/fleet-combat.md#target-priority)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Rule` | Target choice | `Nearest` | `Nearest`, `LowestHull`, `HighestHull`, `MostDamage`, `AttackingFlagship`, `SmallestFirst`, `LargestFirst`, `LowestLevel`, `HighestLevel` |
| `RepickSeconds` | Re-check the target every (seconds) | `0` | 0 to 30 |
| `FinishBelowPercent` | Don't switch away from targets below (hull %) | `0` | 0 to 100 |

### `[EngagementDistance]`

[Engagement distance](features/fleet-combat.md#engagement-distance): only `Enabled`.

### `[Separation]`

[Keep distance in combat](features/fleet-combat.md#keep-distance-in-combat)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Default` | Keep distance from other ships | `false` | `true`, `false` |
| `Gap` | Gap between hulls (units) | `2` | 0.5 to 10 |
| `OtherShips` | Other ships keep distance too | `false` | `true`, `false` |

### `[MissileRetarget]`

[Missile retargeting](features/fleet-combat.md#missile-retargeting)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Mode` | When a missile's target dies | `Off` | `Off`, `WeaponTarget`, `Nearest` |

### `[DroneModes]`

[Drone modes](features/fleet-combat.md#drone-modes): only `Enabled`.

### `[DronesOnArrival]`

[Drones on arrival](features/fleet-combat.md#drones-on-arrival)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Default` | Launch drones on arrival | `false` | `true`, `false` |

## Loot and cargo

### `[ItemRules]`

[Item rules](features/loot-and-cargo.md#item-rules): only `Enabled`.

### `[TractorCaps]`

[Loot pickup limits](features/loot-and-cargo.md#loot-pickup-limits)

| Key | In the window | Default | Values |
|---|---|---|---|
| `MinCreditsPerM3` | Skip loot worth less per m3 than | `0` | 0 to 1000000 |

### `[FleetHolds]`

[Shared fleet holds](features/loot-and-cargo.md#shared-fleet-holds)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Overflow` | Pass cargo to ships with room | `false` | `true`, `false` |

### `[Contacts]`

[Scanner](features/loot-and-cargo.md#scanner)

| Key | In the window | Default | Values |
|---|---|---|---|
| `HudCounter` | Counter above the Dock button | `true` | `true`, `false` |
| `MarkBoardable` | Edge markers: boardable ships | `true` | `true`, `false` |
| `MarkWrecks` | Edge markers: wrecks | `true` | `true`, `false` |
| `MarkLoot` | Edge markers: loot | `true` | `true`, `false` |
| `MarkOres` | Edge markers: preferred ores | `true` | `true`, `false` |

## Stations, loadouts and crafting

### `[StationServices]`

[Station services](features/stations.md#station-services)

| Key | In the window | Default | Values |
|---|---|---|---|
| `RepairOnDock` | Pay for repairs when you dock | `false` | `true`, `false` |
| `RestockOnDock` | Restock ammo when you dock | `false` | `true`, `false` |
| `SellOnDock` | Sell by item rules when you dock | `false` | `true`, `false` |
| `FuelCells` | Warp fuel cells to keep aboard | `0` | 0 to 50 |
| `CrewOnDock` | Replace lost crew when you dock | `false` | `true`, `false` |
| `BrigOnDock` | Unload brigs when you dock | `false` | `true`, `false` |
| `AirlockButtons` | Buttons on the airlock screen | `true` | `true`, `false` |

### `[Loadouts]`

[Ship loadouts](features/stations.md#ship-loadouts): only `Enabled`.

### `[CraftRollup]`

[Crafting roll-up](features/stations.md#crafting-roll-up): only `Enabled`.

### `[RefineryFilter]`

[Refinery filter](features/stations.md#refinery-filter): only `Enabled`.

### `[UsedIn]`

["Used in" tooltips](features/stations.md#used-in-tooltips): only `Enabled`.

### `[Operations]`

[Operations tab](features/stations.md#operations-tab)

| Key | In the window | Default | Values |
|---|---|---|---|
| `PlayerAutoBuyOut` | Accept buyouts automatically | `false` | `true`, `false` |

## Logs, reference and other mods

### `[EventLog]`

[Event log](features/logs-and-reference.md#event-log): only `Enabled`.

### `[CombatLog]`

[DPS parser](features/logs-and-reference.md#dps-parser)

| Key | In the window | Default | Values |
|---|---|---|---|
| `FightGapSeconds` | Seconds without damage that end a fight | `15` | 5 to 120 |
| `WriteFile` | Write each fight to a file | `false` | `true`, `false` |

### `[Guide]`

[Stat reference](features/logs-and-reference.md#stat-reference): only `Enabled`.

### `[Notes]`

[Notes](features/logs-and-reference.md#notes): only `Enabled`.

### `[Mods]`

[Other mods](features/logs-and-reference.md#other-mods): only `Enabled`.
