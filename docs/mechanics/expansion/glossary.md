# Glossary: Beyond the Front

This page covers the terms that appear once you accept "Beyond the Front", which needs level 60 and one finished 30% sector-control mission. Earlier terms are in the [basics glossary](../basics/glossary.md) and the [Conquest glossary](../conquest/glossary.md). Terms that come later are on the pages for later stages.

The wording comes from the game's translation strings and interface text of version 0.8.3.4, not from play. The interface code was compared with the 0.8.3.5 source. The translation strings were not re-read for 0.8.3.5. The basics glossary explains the table columns. The code name for each term is in the last part of this page.

Pages: [Your Empire](your-empire.md) and [Empire Fleets](empire-fleets.md).

## Places and Stations

| Term used here | What the game shows | Meaning |
|---|---|---|
| war zone (expansion) | "Uncharted", "Northern Reaches", "Southern Reaches" and "The Expanse" for the expansion regions | Once the expansion is unlocked, the expansion sectors are part of the war zone too. |
| expansion | "Beyond the Front" (mission), "Uncharted" (map label) | The extra war-zone sectors that the game generates when you accept "Beyond the Front". |
| player sector | "Set aside for you" (system card), "set aside for you - buy it at its hub" (claim refusal), "Our home sector is safe from invasions" (ECHO tip) | The one expansion sector reserved for you. Unowned systems there are for sale to you, and NPC factions cannot enter them. The game also calls it your home sector. |
| safe sector | "safe from invasions" (ECHO tip) | A sector that no NPC faction can enter or attack. Your player sector has this rule. |
| hub (claim refusal) | "destroy its hub" (claim refusal) | The game shows this text when you try to claim a system whose hub still stands. |
| your capital | "Your capital - no faction can take it" (system card), "Can't be taken. Raids can still hit its stations." (tooltip) | The first system you hold. Fleets cannot take it. |
| Defense station (yours) | "Defense Station" (build menu) | The same type of station that NPC factions build, built by you. Yours holds patrol fleets, where an NPC faction's Defense station holds a garrison. |
| economic outpost (yours) | "Economic Outposts" (Faction overview) | The outposts you build in your own systems. |

The word "Defense" has several meanings in the game:

- the number on the system card
- the Defense station type
- the "Defense Platform" part
- the "Station defense" sector bonus
- "Defensive stats" on a ship
- the fleet stance "Defend system"

These pages write "the Defense row" or "the Defense number" for the number, and always "Defense station" for the station.

In the code: [Places and Stations](#code-places-and-stations).

## Conquest Map

| Term used here | What the game shows | Meaning |
|---|---|---|
| Defense row (yours) | "Defense" row in the conquest section of the system card | In your systems, the Defense row also counts your patrol ships. |
| Area Conquered (before the expansion) | "Area Conquered" (Faction overview, Conquest section) | Before the expansion is unlocked, the game counts this figure over Conquest sector systems only. |
| free territory | "Free territory is settled by the factions - you can only take a system from its owner" | An unowned system whose hub is Independent. Only NPC factions can settle it. |
| abandoned (claiming) | "Abandoned - the first faction to arrive claims this system" (hub tooltip), "{faction} lost {system} - the system is unclaimed" (notice) | A system whose hub was destroyed has no owner. It goes to you if you accept the claim offer. Otherwise it goes to the fleet that was besieging its hub, or, if there was none, to the first fleet that lands. |
| claim (yours) | "Take over {system}?" (dialog) | Becoming the owner of an unowned system through the takeover dialog. |
| Empire Log (claims) | "Empire Log" (window) | The window also records your claims. |

In the code: [Conquest Map](#code-conquest-map).

## Fleets and Battles

| Term used here | What the game shows | Meaning |
|---|---|---|
| fleet slots (yours) | "{n} / {m} fleet slots", "Hold {n} systems to field another fleet" | How many Empire Fleets you may field. |
| Empire Fleet | "Empire Fleets" (button and window), "Fleets" (tab) | Your own fleets on the Conquest Map. Their ships are your real ships. |
| patrol fleet | "Patrol fleets" (station window), "Patrol strength" | Ships you assign to one of your Defense stations to guard its system. They count toward Defense, and they are lost for good if destroyed. |
| defense stance | "Defend system", "Defend sector", "Not defending" | Whether an idle Empire Fleet joins the defense of its own system, or of any of your systems in its sector. |
| fleet mix | "Fleet mix {x} off-screen strength ({n} ship sizes)" | A bonus for Empire Fleets that have two or more ship sizes. It applies in simulated battles and in the Defense number, not in live combat. |
| retreat setting | "RETREAT AT", "Never" (hidden in this version) | The strength at which an Empire Fleet leaves a battle. The setting is hidden and set to 0, so your fleets never retreat on their own. |
| refit | "Refitting at a construction yard" | A visit to a Construction Yard to raise ship levels. Your own Empire Fleets no longer have one, because they use your level. The old "Refit due" chip on the fleet card is gone. |

In the code: [Fleets and Battles](#code-fleets-and-battles).

## Faction Economy

| Term used here | What the game shows | Meaning |
|---|---|---|
| wealth (your empire) | "Economic Power" (tile) | For your own empire, the same tile reads "Income / Hour". |
| upkeep | "Upkeep" (your empire and the faction ledger) | The credits per hour you pay for holding more systems than your Empire Level allows. The game uses your effective level. After 6 unpaid hours in a row, the newest system revolts and goes Independent. The capital never revolts. |

In the code: [Faction Economy](#code-faction-economy).

## Missions and Standing

| Term used here | What the game shows | Meaning |
|---|---|---|
| Vanguard Marks (systems) | "Vanguard Marks" | Buying a system in your player sector costs Vanguard Marks as well as credits. |
| Empire Level | "Your empire reached Empire Level {n} - you can now hold {m} systems without upkeep" | Your empire's level, from 1 to 20. It sets how many systems you may hold without paying upkeep. |
| Empire XP | "Empire XP" | The experience your empire earns from systems, stations, trade and battles. |
| Beyond the Front | "Beyond the Front" | The story mission that unlocks the expansion and, when you finish it, claiming. |

In the code: [Missions and Standing](#code-missions-and-standing).

## Reputation

The reputation bands are in the [basics glossary](../basics/glossary.md#reputation). Accepting "Beyond the Front" raises the reputation cap to Exalted.

In the code: [Reputation](#code-reputation).

## Easily Confused Terms

These words have more than one meaning in the game:

- Defense: the number, the station type, the part, the sector bonus, the stance and the stat group.
- Strength: "strength" on fleet cards and "Total strength" on the Empire Fleets header both mean points.

## In the Game Code

This part holds the code name column of the tables above, one table per group. The wording comes from the game's translation strings (`resources.assets`, read in 0.8.3.4) and the decompiled interface code of version 0.8.3.4, compared with the 0.8.3.5 source.

### Code: Places and Stations

| Term used here | Code name |
|---|---|
| war zone (expansion) | `ConquestScope.IsWarZone`, `SectorMapData.isWarZone` |
| expansion | `expansionUnlocked`, `quadrantExpansion*` |
| player sector | `isPlayerSector` |
| safe sector | `SectorBonusKind.SafeSector` |
| hub (claim refusal) | `ConquestStation` |
| your capital | `isPlayerCapital` |
| Defense station (yours) | `PlayerStationType.Defense` on `PlayerStation` |
| economic outpost (yours) | `PlayerStation` |

### Code: Conquest Map

| Term used here | Code name |
|---|---|
| Defense row (yours) | `SystemCardDefense`, `ConquestCombat.GetDefenseStrength` |
| Area Conquered (before the expansion) | `currentConqueredPercentage` |
| free territory | `SystemClaim.IsFreeTerritory` |
| abandoned (claiming) | `abandonedBy`, `IsAbandonedHulk` |
| claim (yours) | `SystemClaim`, `ConquestOwnership.Claim` |
| Empire Log (claims) | `EmpireLogBook` |

### Code: Fleets and Battles

| Term used here | Code name |
|---|---|
| fleet slots (yours) | `EmpireFleets.MaxFleets` |
| Empire Fleet | `EmpireFleets`, a `ConquestFleet` of faction player |
| patrol fleet | `PatrolFleet` |
| defense stance | `FleetDefenseStance` |
| fleet mix | `FleetMixBonus` |
| retreat setting | `retreatFraction` |
| refit | `ConquestFleetOrders.BeginRefit` |

### Code: Faction Economy

| Term used here | Code name |
|---|---|
| wealth (your empire) | `FactionEconomy.wealth`, `economicStrength` |
| upkeep | `EmpireUpkeep`, `LedgerEntry.Upkeep` |

### Code: Missions and Standing

| Term used here | Code name |
|---|---|
| Vanguard Marks (systems) | `vanguardMarks`, item `VanguardMark` |
| Empire Level | `EmpireLevel` |
| Empire XP | `EmpireExperience` |
| Beyond the Front | `StoryMissionGoToExpansion` |

### Code: Reputation

Accepting "Beyond the Front" raises the reputation cap to Exalted (`Conquest.UnlockExpansion`).
