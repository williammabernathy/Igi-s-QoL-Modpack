# Glossary

This page covers the terms the game introduces from a new game on: stats, combat, ship ranks, rarity, reputation, combat stations and the shared abbreviations. Terms that come later are on later stage pages.

Terms and abbreviations used across these pages, with what the game itself shows for each. Wording is read from the game's translation strings and the decompiled interface code of version 0.8.3.4, not from play. Where the game has no name for a thing, the entry says so and gives the plain phrase these pages use. The names the code uses for these terms are in the last section, for modders.

How to read the tables: "Term used here" is the one wording the pages use for that meaning. "What the game shows" is the exact interface text and where it appears. Collisions, meaning words the game or the pages use for more than one thing, are gathered in "Collisions in one list" below.

Pages: [combat damage](combat-damage.md), [scaling](scaling.md), [stations](stations.md), [boarding](boarding.md).

## Places and stations

| Term used here | What the game shows | Meaning |
|---|---|---|
| system | "System" (map) | One star system. Jump gates link systems. |
| sector | "Subsector" (map label); "Quadrant" for the big map regions | A group of systems. The pages say "sector" for the game's "Subsector". |
| Defense platform | "Defense Platform" | A turret-carrying part inside a combat station. It is what gives a station its firepower. |
| combat station | "Combat Station" (map location type) | Any station built from destroyable parts: mission stations, pirate stations and others. [Stations](stations.md) is about these. |
| Industrial Outpost | "Industrial Outpost" (map location type) | A separate, neutral location: a small station you defend against waves, with repair, ammo and turret packs. Not the same as a faction's economic outposts. See [stations](stations.md#industrial-outpost-stations). |
| location | none | Anything you can fly to inside a system: a station, gate, asteroid field. The pages write "location" instead of "point of interest". |

In the code: [places and stations](#code-places-and-stations).

## Fleets and battles

| Term used here | What the game shows | Meaning |
|---|---|---|
| points | "{n} points" in fleet budgets | A ship's fleet value. Fleet size and strength are counted in points. How a ship's value is set is in [scaling](scaling.md). |

In the code: [fleets and battles](#code-fleets-and-battles).

## Missions and standing

| Term used here | What the game shows | Meaning |
|---|---|---|
| ECHO | "ECHO" | Your ship's AI companion. |
| escalation | "Escalation" (button), "Enemy toughness / damage / forces" | A difficulty setting you choose that scales enemies up and rewards with them. |
| ship rank | "Rookie", "Regular", "Veteran", "Elite", "Champion", "Commander", "Legendary" (badge on a ship tooltip) | An enemy ship's strength tier. Distinct from rarity. |
| rarity | Standard, Enhanced, High Grade, Exotic, Legendary (item colors; High Grade is blue) | An item's quality tier. Legendary loot-box gear appears at level 60 or higher; enemy gear can carry it a few levels earlier. |

In the code: [missions and standing](#code-missions-and-standing).

## Reputation

Reputation with a faction is a number. The game shows its level name. Bands:

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

Each level starts at the number shown in the threshold table, counted toward zero from below: a reputation of exactly -5000 is Hostile, exactly -500 is Wary, and 0 up to 1,499 is Neutral.

"Enemy" does not mean a level. A faction is your enemy when you are at war with it or your reputation is below -500, which is Wary (except exactly -500) and everything worse. Enemy ships and stations attack you and can be targeted with a plain click. Only non-enemy stations and ships need the force-attack steps in [stations](stations.md).

The pages' wording: "at or below -5000" means Hostile or worse; "above -500" means Neutral or better.

In the code: [reputation](#code-reputation).

## Stats and combat abbreviations

| Term | What the game shows | Meaning |
|---|---|---|
| HP | "Hull HP", "Armor HP", "Shield HP" | Hit points. A ship or station part has three pools: shield, armor, hull. |
| Combat Power (stat) | "Combat Power" ("The effectiveness of combat systems and weapons") | A ship or station part stat that scales weapon damage. |
| Drone Power, Torpedo Power | "Drone Power", "Torpedo Power" | The same scaling for drones and torpedoes. |
| DPS | none | Damage per second. |
| DoT | none | Damage over time: damage applied across several seconds, for example Heat damage from an aspect. |
| Damage Reduction, resistance | "Damage Reduction", "Kinetic Resistance" and so on | See [combat damage](combat-damage.md). |
| EMP | "EMP Output" | Electromagnetic pulse. EMP weapons raise the chance that a damaged ship becomes boardable. |
| AP ammo | "Armor Piercing" rounds | Boarding ammunition that deals 1.2x damage to Marines and Gunners. |
| DT Chaingun | "DT Chaingun" | A large kinetic weapon. "DT" is part of its name. |

In the code: [stats and combat abbreviations](#code-stats-and-combat-abbreviations).

## Other abbreviations

| Abbreviation | Spelled out | Notes |
|---|---|---|
| NPC | non-player character | Anything the game controls: enemy ships, faction fleets, stations. |
| AI | artificial intelligence | The pages avoid the bare term: they say "NPC factions". ECHO is your own ship's AI. |
| POI | point of interest | A code term for a location in a system. The pages write "location". |
| XP | experience points | |
| UI | user interface | |
| LMB, RMB | left and right mouse button | Used by the game's hints ("RMB to build"). |

## Collisions in one list

- Sector: the game says "Subsector" for a sector and "Quadrant" for the bigger region.
- Defense: the "Defense Platform" part and the ship stat group. The conquest stage adds more meanings, listed in the [Conquest glossary](../conquest/glossary.md).
- Combat Power: the ship stat and the Faction overview tile (see the [Conquest glossary](../conquest/glossary.md#fleets-and-battles)).
- Rank: ship rank and Patrol Officer rank.

## In the game code

The wording above comes from the translation strings in `resources.assets` and the decompiled interface code of version 0.8.3.4. This part gives the name the code uses for each term, in the same groups as the tables. Search a decompiler for the name to see where the game uses it.

### Code: Places and stations

| Term | Code name |
|---|---|
| system | `SystemMapData` |
| sector | `SectorMapData` |
| Defense platform | `CombatStationPartType.DefensePlatform` |
| combat station | `CombatStationData`, `CombatStationPart` |
| Industrial Outpost | `IndustryStation` |
| location | `MapPointOfInterest`, "POI" |

### Code: Fleets and battles

| Term | Code name |
|---|---|
| points | `pointValue`, `totalPoints`, `FleetPoints` |

### Code: Missions and standing

| Term | Code name |
|---|---|
| ECHO | `Characters.shipAi` |
| escalation | `EscalationHelper.CurrentTier` |
| ship rank | `UnitRank`; "Regular" is `UnitRank.Standard` |
| rarity | `Rarity` |

### Code: Reputation

The bands come from `ReputationLevelExtensions`. Each level starts at the number shown in the code threshold table, counted toward zero from below.

### Code: Stats and combat abbreviations

| Term | Code name |
|---|---|
| HP | `EquipStat.HullHP` and so on |
| Combat Power (stat) | `EquipStat.CombatPower` |
| Drone Power, Torpedo Power | `EquipStat.DronePower`, `TorpedoPower` |
| DPS, DoT | none |
| Damage Reduction, resistance | `EquipStat.DamageReduction`, `*Resist` |
| EMP | `EmpOutput` |
| AP ammo | `BoardingAmmo` |
| DT Chaingun | the weapon's own name; no separate code name |
