# Settings Reference

Every setting of Igi's QoL Modpack is in `BepInEx/config/igi.qol.cfg`, with one section per feature. You don't need to edit the file. The mod window shows every row under the name in the second column, with the full explanation on hover, and it writes the file as you change a setting. BepInEx rewrites the file at each start with a description and the default above every key, so the file explains itself too.

`Enabled` ("Feature on" in the window) is in every section, defaults to `true`, and applies at the next game start. Per-ship overrides from the ship settings popup are saved in the save game, not in this file.

Where a setting is a list of choices, the values below are the names the `.cfg` file uses. The window shows plainer names for some of them: `Vanilla` reads "Same as yours" (the Stealth and Ammo choices in `[EchoOperations]`), `Off` in `[MissileRetarget]` reads "Fly on (game default)", `ArmourPiercing` reads "Armor Piercing", and `SideMenu` and `DockButton` read "On top of the side menu" and "Above the Dock button".

The `[Dev]` section is the developer harness. It is off by default and documented in [dev-harness.md](https://github.com/williammabernathy/Igi-s-QoL-Modpack/blob/main/docs/modpack/dev-harness.md).

## Window and HUD

### `[Window]`

[Mod Window](features/interface.md#mod-window), [pop-out windows](features/interface.md#pop-out-windows)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Hotkey` | Mod window hotkey | `F6` | a key name, for example `F6`, `F7`, `Backquote` or `K` |
| `ButtonPlace` | QoL button in space | `SideMenu` | `SideMenu`, `DockButton` |
| `PopoutInteractKey` | Key to use a click-through pop-out | `LeftCtrl` | a key name, for example `LeftCtrl`, `LeftShift`, `LeftAlt` or `F8`; either the left or right Ctrl, Shift or Alt key counts |
| `KeepCardsOnScreen` | Keep hover cards on the screen | `true` | `true` or `false`. On: a tall hover card slides up or down to stay on screen, still beside the mouse. Off: the game's own placement |

The Window section in the mod window also has a "QoL button position" row with a Reset button. It puts the QoL button back after you dragged it. The offset is kept in Unity's PlayerPrefs, not in this file.

### `[TextScale]`

[Text Size](features/interface.md#text-size)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Percent` | Text size (%) | `100` | 50 to 200 |

### `[CameraZoom]`

[Zoom Out Further](features/interface.md#zoom-out-further)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Percent` | Furthest zoom out (%) | `100` | 100 to 150 |

### `[FleetVitals]`

[Fleet Shield and Armor Bars](features/interface.md#fleet-shield-and-armor-bars)

| Key | In the window | Default | Values |
|---|---|---|---|
| `BarHeight` | Bar height (px) | `4` | 2 to 12 |

### `[FleetStrip]`

[Fleet Command Strip](features/interface.md#fleet-command-strip): this feature has no setting besides `Enabled`.

### `[JumpsRemaining]`

[Jumps Remaining](features/interface.md#jumps-remaining): this feature has no setting besides `Enabled`.

### `[MissionDistance]`

[Mission Distance](features/interface.md#mission-distance): this feature has no setting besides `Enabled`.

### `[MissionSort]`

[Mission Sort](features/interface.md#mission-sort)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Board` | Mission board order | `GameOrder` | `GameOrder`, `Type`, `Level`, `Difficulty` |
| `Window` | Missions window order | `GameOrder` | `GameOrder`, `Type`, `Level`, `Difficulty`, `Distance` |

### `[MissionFit]`

[Mission Fit](features/interface.md#mission-fit): this feature has no setting besides `Enabled`.

## ECHO, the Autopilot

### `[EchoPriority]`

[ECHO Activity Priority](features/echo.md#echo-activity-priority)

| Key | In the window | Default | Values |
|---|---|---|---|
| `EmptyMinutes` | Skip empty locations for (minutes) | `5` | 0 to 60 (0 turns it off) |

### `[EchoGates]`

[ECHO Decisions](features/echo.md#echo-decisions)

| Key | In the window | Default | Values |
|---|---|---|---|
| `LootBeforeLeaving` | Pick up loot before leaving | `true` | `true`, `false` |
| `RaidReputationThreshold` | Raid factions at reputation or below | `-500` | whole number |
| `DungeonCrewMinimum` | Crew needed to start an operation | `3` | whole number |
| `BoardingCrewFraction` | Crew needed to board a ship (share of the flagship's maximum crew) | `0.5` | number |
| `HireCrewBelowFraction` | Hire crew when below (share of the flagship's maximum crew) | `0.5` | number |
| `FastUnloading` | Fast cargo unloading | `false` | `true`, `false` |
| `QuickStationSteps` | Quick station steps | `false` | `true`, `false` |
| `RecruitPrisoners` | Recruit willing prisoners before hiring | `false` | `true`, `false` |
| `HireOnlyWhatFits` | Hire only crew that fits aboard | `true` | `true`, `false` |
| `RebalanceCrew` | Make room for short crew types | `false` | `true`, `false` |

### `[EchoMissions]`

[ECHO Missions](features/echo.md#echo-missions)

| Key | In the window | Default | Values |
|---|---|---|---|
| `MissionsAtOnce` | Missions ECHO takes at once | `1` | 1 to 5 |
| `BountyHunt` | Combat: Eliminate Pirate Presence or Eliminate (faction) forces | `true` | `true`, `false` |
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

[ECHO Retreat](features/echo.md#echo-retreat)

| Key | In the window | Default | Values |
|---|---|---|---|
| `FlagshipHullPercent` | Retreat when the flagship's hull is below (%) | `0` | 0 to 90 |
| `FleetHullPercent` | Retreat when a fleet ship's hull is below (%) | `0` | 0 to 90 |

### `[EchoKit]`

[ECHO Abilities and Deployables](features/echo.md#echo-abilities-and-deployables)

| Key | In the window | Default | Values |
|---|---|---|---|
| `AlsoWithoutEcho` | Also while you fly | `false` | `true`, `false` |
| `PickUpTurrets` | Pick deployed turrets back up | `true` | `true`, `false` |

### `[OrePreference]`

[Preferred Ores](features/echo.md#preferred-ores): this feature has no setting besides `Enabled`.

### `[EchoOperations]`

[ECHO's Operations](features/echo.md#echos-operations)

| Key | In the window | Default | Values |
|---|---|---|---|
| `StealthMode` | ECHO: Stealth | `Vanilla` | `Vanilla`, `Silent`, `Balanced`, `Aggressive` |
| `AmmoType` | ECHO: Ammo | `Vanilla` | `Vanilla`, `Standard`, `ArmourPiercing`, `Hollow` |
| `AutoBuyOut` | ECHO: Auto Accept Buyouts | `false` | `true`, `false` |

### `[EchoCargo]`

[ECHO Cargo Rules](features/echo.md#echo-cargo-rules)

| Key | In the window | Default | Values |
|---|---|---|---|
| `ApplyToAutoDeposit` | Apply when fleet cargo auto-deposits on dock | `true` | `true`, `false` |
| `SellFromData` | Sell from the data inventory | `true` | `true`, `false` |
| `SellFromStorage` | Sell from station storage | `false` | `true`, `false` |
| `SellFromArmory` | Sell from the armory | `false` | `true`, `false` |
| `KeepMissionItems` | Keep items your missions need | `true` | `true`, `false` |

## Fleet and Combat

### `[TargetPriority]`

[Target Priority](features/fleet-combat.md#target-priority)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Rule` | Target choice | `Nearest` | `Nearest`, `LowestHull`, `HighestHull`, `MostDamage`, `AttackingFlagship`, `SmallestFirst`, `LargestFirst`, `LowestLevel`, `HighestLevel`, `WeakestToMyWeapons`, `DebuffedFirst` |
| `RepickSeconds` | Re-check target every (seconds) | `0` | 0 to 30 |
| `FinishBelowPercent` | Finish off targets below (% of hull) | `0` | 0 to 100 |
| `WeakestGuardPercent` | Most vulnerable to my weapons: needs a lead of (%) | `25` | 0 to 200 |
| `DebuffedGuardPercent` | Most debuffs first: needs a lead of (%) | `25` | 0 to 200 |

### `[EngagementDistance]`

[Engagement Distance](features/fleet-combat.md#engagement-distance): this feature has no setting besides `Enabled`. The distance is set per ship in its popup, and it only applies while the ship is chasing its target.

### `[Separation]`

[Keep Distance in Combat](features/fleet-combat.md#keep-distance-in-combat): this feature applies only while a ship is chasing its target. It never applies to the ship's own target, and it doesn't apply to ships the game flies in formation (Escort, Hold Fire, a Hold or Move order, or Defensive until hit).

| Key | In the window | Default | Values |
|---|---|---|---|
| `Default` | Keep distance from other ships | `false` | `true`, `false` |
| `Gap` | Gap between hulls (units) | `2` | 0.5 to 10 |
| `OtherShips` | Other ships keep distance too | `false` | `true`, `false` |

### `[ShipCollisions]`

[Ship Collisions](features/fleet-combat.md#ship-collisions): at most one of `FleetPassThrough` and `PassThroughWhileTurning` is on, and at most one of `AnchorStations` and `HeavyStations`. Turning one on turns the other off. If you edit the file to turn both on, the first one in the file wins at the next start.

| Key | In the window | Default | Values |
|---|---|---|---|
| `FleetPassThrough` | Fleet passes through your ships | `false` | `true`, `false` |
| `PassThroughWhileTurning` | Fleet passes through while turning in formation | `false` | `true`, `false` |
| `KeepOwnHeading` | Formation ships keep their own heading | `false` | `true`, `false` |
| `AnchorStations` | Stations can't be pushed | `false` | `true`, `false` |
| `HeavyStations` | Heavier stations | `false` | `true`, `false` |
| `StationWeight` | Station weight (x) | `10` | 2 to 100 |

### `[FleetRetreat]`

[Retreat for Repairs](features/fleet-combat.md#retreat-for-repairs): this feature has no setting besides `Enabled`. The threshold is set per ship in the popup.

### `[ShipStats]`

[Ship Stats on Hover](features/fleet-combat.md#ship-stats-on-hover): this feature has no setting besides `Enabled`.

### `[CombatOverview]`

[Combat Overview](features/fleet-combat.md#combat-overview): its show condition, opacity, click-through and "Ships shown" are per machine and are set in the window's Options.

| Key | In the window | Default | Values |
|---|---|---|---|
| `Hotkey` | Combat overview hotkey | `F7` | a key name, for example `F7` or `F8`; `None` turns it off |

### `[MissileRetarget]`

[Missile Retargeting](features/fleet-combat.md#missile-retargeting)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Mode` | When a missile's target dies | `Off` | `Off`, `WeaponTarget`, `Nearest` |

### `[DroneModes]`

[Drone Modes](features/fleet-combat.md#drone-modes)

| Key | In the window | Default | Values |
|---|---|---|---|
| `ShipTargetFallback` | Ship's target only: guard when the ship has none | `false` | `true`, `false` |
| `StayOnHold` | Drones stay near the ship on Hold Position | `false` | `true`, `false` |

### `[DronesOnArrival]`

[Drones on Arrival](features/fleet-combat.md#drones-on-arrival)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Default` | Launch drones on arrival | `false` | `true`, `false` |

## Loot and Cargo

### `[ItemRules]`

[Item Rules](features/loot-and-cargo.md#item-rules): this feature has no setting besides `Enabled`.

### `[TractorCaps]`

[Loot Pickup Limits](features/loot-and-cargo.md#loot-pickup-limits)

| Key | In the window | Default | Values |
|---|---|---|---|
| `MinCreditsPerM3` | Don't pick up loot worth less than (credits per m3) | `0` | 0 to 1000000 |

### `[FleetHolds]`

[Shared Fleet Holds](features/loot-and-cargo.md#shared-fleet-holds)

| Key | In the window | Default | Values |
|---|---|---|---|
| `Overflow` | Pass cargo to ships with room | `false` | `true`, `false` |

### `[CargoPins]`

[Pinned Cargo](features/loot-and-cargo.md#pinned-cargo)

| Key | In the window | Default | Values |
|---|---|---|---|
| `PinKey` | Pin key | `P` | a key name, for example `P`, `K` or `F7` |

### `[Contacts]`

[Scanner](features/loot-and-cargo.md#scanner)

| Key | In the window | Default | Values |
|---|---|---|---|
| `HudCounter` | Counter above the Dock button | `true` | `true`, `false` |
| `MarkBoardable` | Edge markers: boardable ships | `true` | `true`, `false` |
| `MarkWrecks` | Edge markers: wrecks | `true` | `true`, `false` |
| `MarkLoot` | Edge markers: loot | `true` | `true`, `false` |
| `MarkOres` | Edge markers: preferred ores | `true` | `true`, `false` |

## Stations, Loadouts and Crafting

### `[StationServices]`

[Station Services](features/stations.md#station-services)

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

[Ship Loadouts](features/stations.md#ship-loadouts): this feature has no setting besides `Enabled`.

### `[CraftRollup]`

[Crafting Roll-Up](features/stations.md#crafting-roll-up): this feature has no setting besides `Enabled`.

### `[RefineryFilter]`

[Refinery Filter](features/stations.md#refinery-filter): this feature has no setting besides `Enabled`.

### `[UsedIn]`

[Used in (Forge Recipes)](features/stations.md#used-in-forge-recipes): this feature has no setting besides `Enabled`.

### `[Operations]`

[Operations](features/stations.md#operations-tab)

| Key | In the window | Default | Values |
|---|---|---|---|
| `PlayerAutoBuyOut` | Auto Accept Buyouts | `false` | `true`, `false` |
| `BoardingCompartment` | Priority compartment: boardings | `None` | `None` or the name of one of the game's compartment types, for example `Armory`, `CargoHold`, `Bridge`, `LivingQuarters`, `ReactorRoom`, `MessHall`, `Brig` or `OfficersLounge` |
| `InstallationCompartment` | Priority compartment: installations | `None` | `None` or a compartment type name, from the same list as `BoardingCompartment`. Installations mostly have rooms such as `StorageBay`, `ControlRoom`, `GeneratorRoom`, `ServerRoom` and `Workshop` |

### `[FavouriteStations]`

[Favourite Stations](features/stations.md#favourite-stations): this feature has no setting besides `Enabled`. The list itself is kept in the save.

## Conquest

### `[ConquestDetails]`

[Conquest Details](features/conquest.md#conquest-details)

| Key | In the window | Default | Values |
|---|---|---|---|
| `CardDetail` | System card detail | `Summary` | `Summary` shows a short War block, and the full breakdown while `DetailKey` is held. `Always` shows the full breakdown at all times |
| `DetailKey` | Key for the full breakdown | `LeftShift` | a key name, for example `LeftShift`, `LeftCtrl`, `LeftAlt` or `F8`; either the left or right Shift, Ctrl or Alt key counts; ignored while you type in a text field |

The War Overview's show condition, opacity, click-through, "List", "Targets of" and "Sort targets by" are per machine and are set in the window's Options. Its system cards always show the full breakdown.

## Logs, Reference and Other Mods

### `[EventLog]`

[Event Log](features/logs-and-reference.md#event-log): this feature has no setting besides `Enabled`.

### `[CombatLog]`

[DPS Parser](features/logs-and-reference.md#dps-parser)

| Key | In the window | Default | Values |
|---|---|---|---|
| `FightGapSeconds` | End a fight after this many quiet seconds | `15` | 5 to 120 |
| `WriteFile` | Write each fight to a file | `false` | `true`, `false` |

### `[Guide]`

[Stat Reference](features/logs-and-reference.md#stat-reference): this feature has no setting besides `Enabled`.

### `[Notes]`

[Notes](features/logs-and-reference.md#notes): this feature has no setting besides `Enabled`.

### `[Mods]`

[Other Mods](features/logs-and-reference.md#other-mods): this feature has no setting besides `Enabled`.
