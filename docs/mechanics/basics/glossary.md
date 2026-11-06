# Glossary

This page covers the terms the game introduces from the start of a new game: stats, combat, ship ranks, rarity, reputation, combat stations and the shared abbreviations. Terms that come later are on later stage pages.

The page lists terms and abbreviations used across these pages, with what the game itself shows for each. The wording is read from the game's translation strings and the decompiled interface code of version 0.8.3.4, and the code was checked again against 0.8.3.5. The translation strings are not in the 0.8.3.5 data dumps, so they were not re-read for 0.8.3.5. Where the game has no name for a thing, the entry says so and gives the plain phrase these pages use. The names the code uses for these terms are in the last section, for modders.

How to read the tables: "Term used here" is the one wording the pages use for that meaning. "What the game shows" is the exact interface text and where it appears. Some words are used by the game or by these pages for more than one thing. They are gathered in "Easily Confused Terms" below.

Pages: [Combat Damage](combat-damage.md), [Scaling](scaling.md), [Combat Stations](stations.md), [Boarding and Facility Operations](boarding.md) and [Earning Credits](credits.md).

## Places and Stations

| Term used here | What the game shows | Meaning |
|---|---|---|
| system | "System" (map) | One star system. Jump gates link systems. |
| sector | "Subsector" (map label). "Quadrant" for the larger map regions. | A group of systems. The pages say "sector" for the game's "Subsector". |
| Defense Platform | "Defense Platform" | A turret-carrying part inside a combat station. It gives a station its firepower. |
| combat station | "Combat Station" (map location type) | Any station built from destroyable parts, such as mission stations and pirate stations. [Combat Stations](stations.md) is about these. |
| Industrial Outpost | "Industrial Outpost" (map location type) | A separate, neutral location: a small station you defend against waves, with repair, ammo and turret packs. It is not the same as a faction's economic outposts. See [Industrial Outpost Stations](stations.md#industrial-outpost-stations). |
| location | Not shown | Anything you can fly to inside a system, such as a station, a gate or an asteroid field. The pages write "location" instead of "point of interest". |

In the code: [Places and Stations](#code-places-and-stations).

## Fleets and Battles

| Term used here | What the game shows | Meaning |
|---|---|---|
| points | "{n} points" in fleet budgets | A ship's fleet value. Fleet size and strength are counted in points. [Scaling](scaling.md) explains how a ship's value is set. |

In the code: [Fleets and Battles](#code-fleets-and-battles).

## Missions and Standing

| Term used here | What the game shows | Meaning |
|---|---|---|
| ECHO | "ECHO" | Your ship's artificial intelligence (AI) companion. |
| Autopilot | "Autopilot", "Toggle Autopilot" | The mode in which ECHO plays for you when the game is left idle. While it is on, your ship auto-targets and a share of enemy loot is skipped. |
| Escalation | "Escalation" (button), "Enemy toughness / damage / forces" | A difficulty setting you choose. Higher tiers make enemies stronger and raise mission rewards. |
| ship rank | "Rookie", "Regular", "Veteran", "Elite", "Champion", "Commander", "Legendary" (coloured badge on a ship tooltip title) | An enemy ship's strength tier. It is not the same as rarity. |
| rarity | Standard, Enhanced, High Grade, Exotic, Legendary (item colours; High Grade is blue) | An item's quality tier. Legendary Locked Container gear appears at level 60 or higher. Enemy gear can carry it a few levels earlier. |
| credit factor | Not shown | The number the game turns into credits with a level: credits = factor x 100 x 2^(L/14) up to level 100. A higher factor pays more. [Earning Credits](credits.md#how-credit-amounts-scale) lists the factor of each credit source. |
| Signal Trap | "Signal Trap - {boss}" (item), "Trap Site - {boss}" (map location) | A crafted item that summons a boss to the current system. The code calls these bosses pocket bosses. See [Signal Traps and Pocket Bosses](credits.md#signal-traps-and-pocket-bosses). |

In the code: [Missions and Standing](#code-missions-and-standing).

## Reputation

Reputation with a faction is a number. The game shows its level name instead. The bands are:

| Level | Reputation |
|---|---|
| Absolute Threat | -50,000 or lower |
| Hated | -49,999 to -30,000 |
| Despised | -29,999 to -15,000 |
| Hostile | -14,999 to -5,000 |
| Wary | -4,999 to -500 |
| Neutral | -499 to 1,499 |
| Cordial | 1,500 to 4,999 |
| Friendly | 5,000 to 14,999 |
| Respected | 15,000 to 29,999 |
| Distinguished | 30,000 to 49,999 |
| Exalted | 50,000 or higher |

Boundary values follow the table above. A reputation of exactly -5000 is Hostile, exactly -500 is Wary, and 0 up to 1,499 is Neutral.

"Enemy" is not a reputation level. A faction is your enemy when you are at war with it or your reputation with it is below -500. That means Wary (except exactly -500) and every level worse. Enemy ships and stations attack you and can be targeted with a plain click. Only non-enemy stations and ships need the force-attack steps in [Combat Stations](stations.md).

Wording on these pages: "at or below -5000" means Hostile or worse, and "above -500" means Neutral or better.

In the code: [Reputation](#code-reputation).

## Stats and Combat Abbreviations

| Term | What the game shows | Meaning |
|---|---|---|
| HP | "Hull HP", "Armor HP", "Shield HP" | Hit points. A ship or station part has three pools: shield, armor and hull. |
| Combat Power (stat) | "Combat Power" ("The effectiveness of combat systems and weapons") | A stat of a ship or station part that scales weapon damage. |
| Drone Power, Torpedo Power | "Drone Power", "Torpedo Power" | The same scaling for drones and torpedoes. |
| DPS | Not shown | Damage per second. |
| DoT | Not shown | Damage over time. The damage is applied across several seconds, for example Heat damage from an aspect. |
| Damage Reduction, resistance | "Damage Reduction", "Kinetic Resistance" and so on | Stats that cut incoming damage. See [Combat Damage](combat-damage.md). |
| EMP | "EMP Output" | Electromagnetic pulse. EMP weapons raise the chance that a damaged ship becomes boardable. |
| AP ammo | "Armor Piercing" rounds | Armor Piercing (AP) boarding ammunition. It deals 1.2x damage to Marines and Gunners. |
| DT Chaingun | "DT Chaingun" | A large kinetic weapon. "DT" is part of its name. |

In the code: [Stats and Combat Abbreviations](#code-stats-and-combat-abbreviations).

## Other Abbreviations

| Abbreviation | Spelled out | Notes |
|---|---|---|
| NPC | non-player character | Anything the game controls, such as enemy ships, faction fleets and stations. |
| AI | artificial intelligence | The pages avoid the bare term and say "NPC factions" instead. ECHO is your own ship's AI. |
| POI | point of interest | A code term for a location in a system. The pages write "location". |
| XP | experience points | The game's screens also use the word "Experience". |
| UI | user interface | The screens and panels of the game or the mod. |
| LMB, RMB | left and right mouse button | Used by the game's hints ("RMB to build"). |

## Easily Confused Terms

- Sector: the game says "Subsector" for a sector and "Quadrant" for the larger region.
- Defense: the word names both the "Defense Platform" part and a ship stat group. The Conquest stage adds more meanings, listed in the [Conquest glossary](../conquest/glossary.md).
- Combat Power: the word names both the ship stat and the Faction overview tile (see the [Conquest glossary](../conquest/glossary.md#fleets-and-battles)).
- Rank: the word names both a ship rank and a Patrol Officer rank.
- Hold: the HUD switch "Hold Position" holds only the ship you fly. The fleet order "Hold" holds one fleet ship at its spot. See [How Far a Ship Parks From Its Target](combat-damage.md#how-far-a-ship-parks-from-its-target).

## In the Game Code

The wording above comes from the translation strings in `resources.assets` and the decompiled interface code of version 0.8.3.4, and the code was checked again against 0.8.3.5. The strings were not re-read for 0.8.3.5. This part gives the name the code uses for each term, in the same groups as the tables. Search a decompiler for the name to see where the game uses it.

### Code: Places and Stations

| Term | Code name |
|---|---|
| system | `SystemMapData` |
| sector | `SectorMapData` |
| Defense Platform | `CombatStationPartType.DefensePlatform` |
| combat station | `CombatStationData`, `CombatStationPart` |
| Industrial Outpost | `IndustryStation` |
| location | `MapPointOfInterest`, "POI" |

### Code: Fleets and Battles

| Term | Code name |
|---|---|
| points | `pointValue`, `totalPoints`, `FleetPoints` |

### Code: Missions and Standing

| Term | Code name |
|---|---|
| ECHO | `Characters.shipAi` |
| Autopilot | `GamePlayer.autoPlay` |
| Escalation | `EscalationHelper.CurrentTier` |
| ship rank | `UnitRank`. "Regular" is `UnitRank.Standard`. The seven names are hard-coded in `UnitRankHelper.displayNames`, not in the text table. |
| rarity | `Rarity` |
| credit factor | the first argument of `GameMath.GetCreditsValue(f, level)` |
| Signal Trap | `SignalTrapItem`, built by `SignalTrapBuilder` (item definition `BaitBeacon`); the boss is a `PocketBossDefinition`, the Trap Site a `LureSite` |

### Code: Reputation

The bands come from `ReputationLevelExtensions`. Each level starts at the number shown in the code's threshold table, counted toward zero from below.

### Code: Stats and Combat Abbreviations

| Term | Code name |
|---|---|
| HP | `EquipStat.HullHP` and so on |
| Combat Power (stat) | `EquipStat.CombatPower` |
| Drone Power, Torpedo Power | `EquipStat.DronePower`, `TorpedoPower` |
| DPS, DoT | No code name |
| Damage Reduction, resistance | `EquipStat.DamageReduction`, `*Resist` |
| EMP | `EmpOutput` |
| AP ammo | `BoardingAmmo` |
| DT Chaingun | The weapon's own name. It has no separate code name. |
