# Glossary: Beyond the Front

This page covers the terms introduced once "Beyond the Front" is accepted, which needs level 60 and one finished 30% mission. Earlier terms are in the [basics glossary](../basics/glossary.md) and the [Conquest glossary](../conquest/glossary.md); terms that come later are on later stage pages.

Wording is read from the game's translation strings and interface text of version 0.8.3.4, not from play. The table columns are explained in the basics glossary. The code name for each term is in the last part of this page.

Pages: [your empire](your-empire.md), [empire fleets](empire-fleets.md).

## Places and stations

| Term used here | What the game shows | Meaning |
|---|---|---|
| war zone (expansion) | "Uncharted", "Northern Reaches", "Southern Reaches", "The Expanse" for the expansion regions | Once the expansion is unlocked, the expansion sectors are part of the war zone too. |
| expansion | "Beyond the Front" (mission), "Uncharted" (map label) | The extra war-zone sectors generated when you accept "Beyond the Front". |
| home sector | "Our home sector is safe from invasions" (ECHO tip), "set aside for you - buy it at its hub" | The one expansion sector reserved for you. Unowned systems there are for sale to you and NPC factions cannot enter them. |
| safe sector | "safe from invasions" (ECHO tip) | A sector no NPC faction can enter or attack. Your home sector has this rule. |
| hub (claim refusal) | "destroy its hub" (claim refusal) | The text the game shows when you try to claim a system whose hub still stands. |
| your capital | "Your capital - no faction can take it" (system card), "Can't be taken. Raids can still hit its stations." | The first system you hold. Fleets cannot take it. |
| Defense station (yours) | "Defense Station" (build menu) | The same station type, built by you. It holds patrol fleets where an NPC faction's Defense station holds a garrison. |
| economic outpost (yours) | "Economic Outposts" (Faction overview) | What you build in your own systems. |

Collisions in this group:

- "Defense" is the system card number, the Defense station type, the "Defense Platform" part, the "Station defense" sector bonus, "Defensive stats" on a ship, and the fleet stance "Defend system". The pages write "the Defense row" or "the Defense number" for the number and always "Defense station" for the station.

In the code: [places and stations](#code-places-and-stations).

## Conquest map

| Term used here | What the game shows | Meaning |
|---|---|---|
| Defense row (yours) | "Defense" row in the system card's conquest section | In your systems the Defense row also counts your patrol ships. |
| Area Conquered (before the expansion) | "Area Conquered" (Faction overview, Conquest section) | The game counts it over Conquest-sector systems only before the expansion is unlocked. |
| free territory | "Free territory is settled by the factions - you can only take a system from its owner" | An unowned system whose hub is Independent. Only NPC factions can settle it. |
| abandoned (claiming) | "Abandoned - the first faction to arrive claims this system" (hub tooltip) | An abandoned system has no owner until a fleet arrives or you claim it. |
| claim (yours) | "Take over {system}?" dialog | Becoming the owner of an unowned system through the takeover dialog. |
| Empire Log (claims) | "Empire Log" (window) | Your log also records your claims. |

In the code: [conquest map](#code-conquest-map).

## Fleets and battles

| Term used here | What the game shows | Meaning |
|---|---|---|
| fleet slots (yours) | "{n} / {m} fleet slots", "Hold {n} systems to field another fleet" | How many Empire Fleets you may field. |
| Empire Fleet | "Empire Fleets" (button and window), "Fleets" tab | Your own fleets on the Conquest Map. Their ships are your real ships. |
| patrol fleet | "Patrol fleets" (station window), "Patrol strength" | Ships you assign to one of your Defense stations to guard its system. They count toward Defense and are lost for good if destroyed. |
| defense stance | "Defend system", "Defend sector", "Not defending" | Whether an idle Empire Fleet joins defense of its system, or of any of your systems in its sector. |
| fleet mix | "Fleet mix {x} off-screen strength ({n} ship sizes)" | A bonus for Empire Fleets with two or more ship sizes, applied in simulated battles only. |
| retreat setting | "RETREAT AT", "Never" (hidden in this version) | The strength at which an Empire Fleet leaves a battle. Hidden, and 0, so your fleets never retreat on their own. |
| refit | "Refitting at a construction yard", "Refit due" | Visiting a Construction Yard to raise ship levels. |

In the code: [fleets and battles](#code-fleets-and-battles).

## Faction economy

| Term used here | What the game shows | Meaning |
|---|---|---|
| wealth (your empire) | "Economic Power" tile | For your own empire the same tile reads "Income / Hour". |
| upkeep | "Upkeep" (your empire and faction ledger) | Hourly credits you pay for holding more systems than your Empire Level allows. |

In the code: [faction economy](#code-faction-economy).

## Missions and standing

| Term used here | What the game shows | Meaning |
|---|---|---|
| Vanguard Marks (systems) | "Vanguard Marks" | Marks also buy systems. |
| Empire Level | "Empire Level {n} - you can now hold {m} systems without upkeep" | Your empire's level. It sets how many systems you may hold without paying upkeep. |
| Empire XP | "Empire XP" | Experience your empire earns from systems, stations, trade and battles. |
| Beyond the Front | "Beyond the Front" | The story mission that unlocks the expansion and, when finished, claiming. |

In the code: [missions and standing](#code-missions-and-standing).

## Reputation

The reputation bands are in the [basics glossary](../basics/glossary.md#reputation). Accepting "Beyond the Front" raises the reputation cap to Exalted.

In the code: [reputation](#code-reputation).

## Collisions in one list

- Defense: the number, the station type, the part, the sector bonus, the stance, the stat group.
- Strength: "strength" on fleet cards and "Total strength" on the Empire Fleets header are points.

## In the game code

This part holds the code name column of the tables above, one table per group. The wording comes from the game's translation strings (`resources.assets`) and the decompiled interface code of version 0.8.3.4.

### Code: Places and stations

| Term used here | Code name |
|---|---|
| war zone (expansion) | `ConquestScope.IsWarZone`, `SectorMapData.isWarZone` |
| expansion | `expansionUnlocked`, `quadrantExpansion*` |
| home sector | `isPlayerSector` |
| safe sector | `SectorBonusKind.SafeSector` |
| hub (claim refusal) | `ConquestStation` |
| your capital | `isPlayerCapital` |
| Defense station (yours) | `PlayerStationType.Defense` on `PlayerStation` |
| economic outpost (yours) | `PlayerStation` |

### Code: Conquest map

| Term used here | Code name |
|---|---|
| Defense row (yours) | `SystemCardDefense`, `ConquestCombat.GetDefenseStrength` |
| Area Conquered (before the expansion) | `currentConqueredPercentage` |
| free territory | `SystemClaim.IsFreeTerritory` |
| abandoned (claiming) | `abandonedBy`, `IsAbandonedHulk` |
| claim (yours) | `SystemClaim`, `ConquestOwnership.Claim` |
| Empire Log (claims) | `EmpireLogBook` |

### Code: Fleets and battles

| Term used here | Code name |
|---|---|
| fleet slots (yours) | `EmpireFleets.MaxFleets` |
| Empire Fleet | `EmpireFleets`, a `ConquestFleet` of faction player |
| patrol fleet | `PatrolFleet` |
| defense stance | `FleetDefenseStance` |
| fleet mix | `FleetMixBonus` |
| retreat setting | `retreatFraction` |
| refit | `ConquestFleetOrders.BeginRefit` |

### Code: Faction economy

| Term used here | Code name |
|---|---|
| wealth (your empire) | `FactionEconomy.wealth`, `economicStrength` |
| upkeep | `EmpireUpkeep`, `LedgerEntry.Upkeep` |

### Code: Missions and standing

| Term used here | Code name |
|---|---|
| Vanguard Marks (systems) | `vanguardMarks`, item `VanguardMark` |
| Empire Level | `EmpireLevel` |
| Empire XP | `EmpireExperience` |
| Beyond the Front | `StoryMissionGoToExpansion` |

### Code: Reputation

Accepting "Beyond the Front" raises the reputation cap to Exalted (`Conquest.UnlockExpansion`).
