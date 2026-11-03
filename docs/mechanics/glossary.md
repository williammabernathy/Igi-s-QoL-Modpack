# Glossary

Terms and abbreviations used across these pages, with what the game itself shows for each and the name the code uses. Wording is read from the game's translation strings (`resources.assets`) and the decompiled interface code of version 0.8.3.4, not from play. Where the game has no name for a thing, the entry says so and gives the plain phrase these pages use.

How to read the tables: "Term used here" is the one wording the pages use for that meaning. "What the game shows" is the exact interface text and where it appears. "Code name" is for modders; you can ignore it. Collisions, meaning words the game or the pages use for more than one thing, are listed at the end of each group and again in the last section.

Pages: [conquest](conquest.md), [faction economy](faction-economy.md), [stations](stations.md), [combat damage](combat-damage.md), [scaling](scaling.md), [boarding](boarding.md).

## Places and stations

| Term used here | What the game shows | Code name | Meaning |
|---|---|---|---|
| system | "System" (map), "Systems Held" (Faction overview) | `SystemMapData` | One star system. Jump gates link systems. |
| sector | "Subsector" (map label); "Quadrant" for the big map regions | `SectorMapData` | A group of systems. The pages say "sector" for the game's "Subsector". |
| war zone | "war zone" in fleet messages ("Fleets can only attack and raid inside the war zone"); the map region label "Conquest >"; "Uncharted", "Northern Reaches", "Southern Reaches", "The Expanse" for the expansion regions | `ConquestScope.IsWarZone`, `SectorMapData.isWarZone` | Every system where factions fight over ownership: the Conquest sector (Ara Martis) and, once unlocked, the expansion sectors. The Frontier, Staging and Darkspace regions are not part of it. |
| Conquest sector | "Conquest sector" (embassy dialogue), sector names starting "Ara Martis" | `ConquestRegion.AraMartis`, `conquestSector` | The original war zone. The "Area Conquered" percentage is counted over its systems only (before the expansion). |
| expansion | "Beyond the Front" (mission), "Uncharted" (map label) | `expansionUnlocked`, `quadrantExpansion*` | The extra war-zone sectors generated when you accept "Beyond the Front". |
| home sector | "Our home sector is safe from invasions" (ECHO tip), "set aside for you - buy it at its hub" | `isPlayerSector` | The one expansion sector reserved for you. Unowned systems there are for sale to you and NPC factions cannot enter them. |
| safe sector | "safe from invasions" (ECHO tip) | `SectorBonusKind.SafeSector` | A sector no NPC faction can enter or attack. Your home sector has this rule. |
| hub | "Space Station" (the system's main station on the map), "Conquest Stations" (Faction overview row), "its hub" (right-click hint), "destroy its hub" (claim refusal, ECHO tip) | `ConquestStation` | The main station of a war-zone system. It carries the owner's flag. Destroying it abandons the system. |
| headquarters | "Conquest headquarters" (system card title), "Headquarters" (Faction overview row), "Gate to {faction} headquarters" | `ConquestSystem.headquarters` | The one system a faction cannot lose to a fleet. Its hub has the faction commander and shop. Shortened "HQ" in the game's mission text; these pages write "headquarters". |
| outpost system | "Conquest outpost" (system card title) | `ConquestSystem` that is neither headquarters nor your capital | An ordinary war-zone system that fleets can besiege and capture. Pages write "outpost system" for this, always with the word "system". |
| your capital | "Your capital - no faction can take it" (system card), "Can't be taken. Raids can still hit its stations." | `isPlayerCapital` | The first system you hold. Fleets cannot take it. |
| Defense station | "Defense" (station type), "Defense Station" (build menu), "Defense stations" (ECHO tip) | `PlayerStationType.Defense` on `FactionStation` or `PlayerStation` | A station type whose job is to hold a garrison or patrol fleets and fire on enemies. Not the same as the Defense number. |
| Defense platform | "Defense Platform" | `CombatStationPartType.DefensePlatform` | A turret-carrying part inside a combat station. It is what gives a station its firepower. |
| economic outpost | "Economic Outposts" (Faction overview, shown as "{n} / {m} slots used"); "outpost" in "click the outpost to raid" and "outpost raided" | `FactionStation` that is not a Defense station; `PlayerStation` | A station that produces something: Mining, Salvage, Refinery, Forge, Production, Trading, Recruitment or Construction Yard. Raids target these. Two per system. |
| Construction Yard | "Construction Yard" | `PlayerStationType.ConstructionYard` | Builds warships. See [faction economy](faction-economy.md). |
| embassy | "Embassy Station", "Embassy" (Faction overview row), "Neutral embassy - Hostilities are not tolerated", "Gate to {faction} embassy" | `EmbassyStation`, `EmbassyJumpgate` | A faction's neutral station in the Staging sector. Its missions pay the faction wealth and rebuild your reputation faster. It is not part of the war zone. |
| combat station | "Combat Station" (map location type) | `CombatStationData`, `CombatStationPart` | Any station built from destroyable parts: hubs, Defense stations, mission stations, pirate stations. [Stations](stations.md) is about these. |
| Industrial Outpost | "Industrial Outpost" (map location type) | `IndustryStation` | A separate, boardable neutral location. Not the same as an economic outpost. |
| Automated Outpost | "Automated Outpost" (map location type) | `MapPOIUmbralStation` | An Umbral Reach station. Not the same as an economic outpost. |
| location | none | `MapPointOfInterest`, "POI" | Anything you can fly to inside a system: a station, gate, asteroid field, Fleet Battle zone. The pages write "location" instead of "point of interest". |

Collisions in this group:

- "Outpost" is five different things in the game: the system card's "Conquest outpost", the Faction overview's "Economic Outposts" (also what raids target and what you build in your own systems), an "Industrial Outpost" location, and an "Automated Outpost" location. These pages say "outpost system" for the first and "economic outpost" for the second. The other two keep their full names.
- "Defense" is the system card number, the Defense station type, the "Defense Platform" part, the "Station defense" sector bonus, "Defensive stats" on a ship, and the fleet stance "Defend system". The pages write "the Defense row" or "the Defense number" for the number and always "Defense station" for the station.
- "Station" alone is generic. The pages use it only for any of the kinds above, and name the kind when it matters.
- "Hub", "main station" and "Space Station" are the same thing. The pages use "hub".

## Conquest map

| Term used here | What the game shows | Code name | Meaning |
|---|---|---|---|
| Defense row | "Defense" row in the system card's conquest section | `SystemCardDefense`, `ConquestCombat.GetDefenseStrength` | The strength the defenders put into a siege round: station firepower plus garrison ships plus defending fleets plus your patrol ships. 0 for an unowned system. |
| next-battle strength | not shown anywhere | `ConquestCombat.GetNextBattleStrength` | What the factions' fleet planning compares attackers against: the weakest Defense station's strength (its own firepower, its garrison, all defending fleets) if the system has Defense stations, otherwise the full Defense number, plus fleets already traveling to relieve it. You can estimate it: with no Defense station it equals the Defense row; with Defense stations it is at most the Defense row. |
| war effort | "Your war effort" row ("+10% firepower (2h 59m)"); mission reward "+10% Station firepower, -10% for {faction} next system"; the "ECHO The Conqueror" skill text | `warEffortBonus`, `warEffortPenalty`, `warEffortMultiplier` | A multiplier on a system's station firepower, raised by Conquest missions you complete and lasting 3 hours from the last one. |
| Umbral Reach control | "Umbral Reach control" row | `umbralControlLevel` | How far the Umbral Reach has spread into a system, as a percentage. |
| Control | "Control" is a text key that exists but the system card never shows it | `controlLevel` | An internal 0 to 2 counter. The pages mostly ignore it. |
| Area Conquered | "Area Conquered" (Faction overview, Conquest section) | `currentConqueredPercentage` | A conquest faction's share of all Conquest-sector systems, refreshed at each conquest update. |
| Your Contribution | "Your Contribution" (Faction overview) | `ConquestFactionStanding.playerContribution` | The total contribution you have earned with a faction. It sets your conquest rank. |
| conquest update | "Conquest Update" card with a countdown bar on the galaxy map; tooltip "Damaged stations are rebuilt and factions rethink their strategy" | `Conquest.DoConquestTick`, `conquestTickTime` | The hourly step (3600 s of game time) that recalculates Area Conquered, rebuilds damaged stations, spreads the Umbral Reach and counts down settling. The pages call it "the conquest update". |
| frontline | "frontline" (ECHO tips, "Fleets are concentrated along the frontline systems") | `ConquestFrontline`, `FrontlineSystem` | A faction's systems that touch an enemy or unowned war-zone system. |
| unrest, settling | not named in the game | `isInUnrest`, `isSettling`, `settlingTicks` | The hours after a system changes owner. Unrest (the first 2 conquest updates) pays no taxes. Settling (about 5 to 6 hours) halves the Defense station profile. The pages use these two plain words. |
| abandoned | "Abandoned - the first faction to arrive claims this system" (hub tooltip), "{faction} lost {system} - the system is unclaimed" | `abandonedBy`, `IsAbandonedHulk` | A system whose hub was destroyed. It has no owner until a fleet arrives or you claim it. |
| free territory | "Free territory is settled by the factions - you can only take a system from its owner" | `SystemClaim.IsFreeTerritory` | An unowned system whose hub is Independent. Only NPC factions can settle it. |
| claim | "Take over {system}?" dialog, "Claimed {system}." | `SystemClaim`, `ConquestOwnership.Claim` | Becoming the owner of an unowned system, by arriving (NPC fleets) or through the takeover dialog (you). |
| siege | "Under siege" (system), "Under Attack! Holds {n} min at most" (hub tooltip), "{fleet} began a siege of {system}" (Empire Log) | `BattleStage.StationSiege`, `DefenseStationSiege` | The fight over a system's hub or one of its Defense stations. |
| Fleet Battle | "Fleet Battle" (location name), "Under Attack, resolves in {n} min" | `BattleStage.FleetClash`, `BattleZone` | The fight between an arriving fleet and a fleet defending the system. The pages call it a "clash" or "Fleet Battle". |
| raid | "Raiding {station}", "raid succeeded", "outpost raided", "Raided - the station is paused while it recovers" | `BattleStage.Raid`, `StationRaid` | A fleet attacking one economic outpost to loot it and stop its production. |
| assault | "Gathering for an assault" (fleet label) | `ConquestFleetOrders.PlanAssault` | Several of one faction's fleets meeting next to a target and merging to attack it. |
| relief | none | `GetIncomingRelief` | Same-faction fleets traveling toward a system under threat. They add to next-battle strength. |
| Empire Log | "Empire Log" (window), categories Combat, Fleets, Stations, Industry, Warnings | `EmpireLogBook` | Your log of battles, captures and claims. It uses the outcome wording in the outcome table below. |

## Fleets and battles

| Term used here | What the game shows | Code name | Meaning |
|---|---|---|---|
| points | "strength" on fleet cards ("{n} ships, {m} strength", "STRENGTH"), "{n} points" in fleet budgets | `pointValue`, `totalPoints`, `FleetPoints` | A ship's fleet value. Fleet size, strength, garrison size and damage in simulated battles are all counted in points. How a ship's value is set is in [scaling](scaling.md). |
| Combat Power (faction) | "Combat Power" tile on the Faction overview | `FactionEconomy.combatPower` | The sum of a faction's fleet points. Not the same as the Combat Power ship stat; see the stats table. |
| fleet | "Fleets Fielded" (Faction overview), "Battlefleet", "Battlegroup" names | `ConquestFleet` | A group of ships that moves and fights together on the map. NPC fleets aim for 2000 points. |
| fleet cap | not shown | `ConquestFleetPlanner.MaxFleetsFor` | How many fleet slots a faction may fill. |
| fleet slots (yours) | "{n} / {m} fleet slots", "Hold {n} systems to field another fleet" | `EmpireFleets.MaxFleets` | How many Empire Fleets you may field. |
| Empire Fleet | "Empire Fleets" (button and window), "Fleets" tab | `EmpireFleets`, a `ConquestFleet` of faction player | Your own fleets on the Conquest Map. Their ships are your real ships. |
| patrol fleet | "Patrol fleets" (station window), "Patrol strength" | `PatrolFleet` | Ships you assign to one of your Defense stations to guard its system. They count toward Defense and are lost for good if destroyed. |
| garrison | "joined a station garrison" (build log) | `FactionGarrison`, `garrisonShips` | The warships held by an NPC Defense station. They count toward Defense. |
| defense stance | "Defend system", "Defend sector", "Not defending" | `FleetDefenseStance` | Whether an idle Empire Fleet joins defense of its system, or of any of your systems in its sector. |
| hull rating, hull edge | "hull rating" (fleet card tooltip), "Hull edge {x} vs {faction} fleets nearby" | `HullRating`, `GetEdge` | How upgraded a ship's hull is, and the up to 15% strength swing between two sides' ratings in simulated battles. |
| fleet mix | "Fleet mix {x} off-screen strength ({n} ship sizes)" | `FleetMixBonus` | A bonus for Empire Fleets with two or more ship sizes, applied in simulated battles only. |
| retreat setting | "RETREAT AT", "Never" (hidden in this version) | `retreatFraction` | The strength at which an Empire Fleet leaves a battle. Hidden, and 0, so your fleets never retreat on their own. |
| refit | "Refitting at a construction yard", "Refit due" | `ConquestFleetOrders.BeginRefit` | Visiting a Construction Yard to raise ship levels. |
| warship delivery | "{ship} flying to its fleet at {system}" (Faction overview) | `WarshipDelivery` | A newly built ship flying from a yard to its fleet or garrison. You can intercept it. |
| interception | "Interception" (location name), "Chase" messages | `InterceptZone`, `IInterceptable` | Catching a fleet, delivery or cargo run from the galaxy map and fighting it in a special location. |
| Great Gate fleet | "Great Gate fleets" (Faction overview ledger), "The Great Gate" (map location) | `GreatGateReinforcement` | A free or paid 1500-point fleet that appears from the Great Gate for Kolyatov, Stellar or Luminate when they have few fleets. |

Battle outcomes, with the wording the Empire Log shows (attacker's view / defender's view):

| Pages say | Empire Log text | Code name |
|---|---|---|
| captured | "system captured" / "system lost" | `BattleOutcome.Captured` |
| skirmish | "inconclusive skirmish" | `Skirmish` |
| repelled | "attack repelled" | `Repelled` |
| attacker destroyed | "fleet destroyed" / "attackers destroyed" | `AttackerDestroyed` |
| Defense station destroyed | "Defense station destroyed" / "Defense station lost" | `DefenseStationDestroyed` |
| withdrew | "withdrew before relief arrived" / "attackers withdrew" | `Withdrew` |
| raid succeeded | "raid succeeded" / "outpost raided" | `Raided` |
| aborted | no text of its own | `Aborted` |

## Faction economy

| Term used here | What the game shows | Code name | Meaning |
|---|---|---|---|
| wealth | "Economic Power" tile and graph on the Faction overview; "Wealth (last 24h)" ledger header; "Faction wealth" in mission rewards | `FactionEconomy.wealth`, `economicStrength` | The money a faction spends on warships, stations and Defense stations. The pages use "wealth". For your own empire the same tile reads "Income / Hour". |
| taxes | "Taxes" ledger line | `LedgerEntry.Passive` | Wealth a faction earns every hour from the systems it owns. Stops while a system is in unrest. |
| home subsidy | "Home subsidy" ledger line | `LedgerEntry.Subsidy`, `GreatGateReinforcement.GetSubsidy` | Money handed to Kolyatov, Stellar, Luminate and Darkspace each hour, larger the smaller their share of the war zone. Not a tax and not income from systems. |
| trade deliveries, intertrade sales | "Trade deliveries", "Intertrade sales" | `LedgerEntry.Trade`, `Sales` | Wealth from goods moved between stations or sold. |
| Warships, Work bay ships, Stations | "Warships", "Work bay ships", "Stations" | `LedgerEntry.Warships`, `BayShips`, `Stations` | What the faction spends wealth on. |
| Great Gate fleets | "Great Gate fleets" ledger line | `LedgerEntry.GreatGate` | Wealth spent on a paid Great Gate fleet. |
| your embassy missions | "Your embassy missions" ledger line | `LedgerEntry.PlayerSupport` | Wealth you added by completing embassy missions. |
| raids, lost to raids | "Raids", "Lost to raids", "Lost to your raids" | `LedgerEntry.Raids`, `Raided`, `PlayerRaids` | Wealth taken by or lost to raids. |
| upkeep | "Upkeep" (your empire and faction ledger) | `EmpireUpkeep`, `LedgerEntry.Upkeep` | Hourly credits you pay for holding more systems than your Empire Level allows. |
| hourly economy tick | not shown | `LivingUniverse.RunHourlyTick` | The hourly step that pays taxes and subsidies and plans builds. It runs on its own 3600 s timer, separate from the conquest update. |
| under-cap discount | not shown | `fleetSlotsUsed < MaxFleetsFor` | Warships cost half and build three times faster while a faction has fewer fleet slots than its cap. |

Collisions in this group: "income" in the game's faction ledger means taxes, trade and sales together; for you it also covers officer idle income and mission rewards. "Subsidy" and "tax" are different ledger lines. Taxes come from owned systems; the home subsidy is a top-up that shrinks as a faction grows.

## Missions and standing

| Term used here | What the game shows | Code name | Meaning |
|---|---|---|---|
| Conquest mission | missions from a hub or embassy mission board; reward line "+x% Station firepower" | `ConquestStrength` reward | A mission at a war-zone hub that raises war effort, or at an embassy that gives wealth. |
| idle mission | "Idle Conquest missions" (skill text) | `mission.idle` | A mission your ship AI ECHO accepts and completes for you while you are away. |
| ECHO | "ECHO" | `Characters.shipAi` | Your ship's AI companion. |
| contribution | "Your Contribution" (Faction overview); "Run missions for {faction}" step; "Wealth your missions brought {n}: x of 1,500,000" | `playerContribution`, `ConquestFleetStrength`, `joinWealth` | Three related counters: your conquest standing with a faction (earned by Conquest missions), the 10 points the first sector-control mission asks for, and the wealth your embassy missions delivered (needed to join a small faction's war). |
| conquest rank | "Intern" to "Sector Partner" (Stellar), "Conscript" to "Hero of the People's Glory" (Kolyatov), "Seeker" to "Oracle's Chosen" (Luminate) and similar | `ConquestRank`, `GetConquestRankTranslation` | Your title with one faction, from Your Contribution. Higher ranks unlock ships. Six ranks plus none. |
| Vanguard Marks | "Vanguard Marks" | `vanguardMarks`, item `VanguardMark` | A currency from missions, used to buy ships and systems. Pages write "marks". |
| Empire Level | "Empire Level {n} - you can now hold {m} systems without upkeep" | `EmpireLevel` | Your empire's level. It sets how many systems you may hold without paying upkeep. |
| Empire XP | "Empire XP" | `EmpireExperience` | Experience your empire earns from systems, stations, trade and battles. |
| escalation | "Escalation" (button), "Enemy toughness / damage / forces" | `EscalationHelper.CurrentTier` | A difficulty setting you choose that scales enemies up and rewards with them. |
| ship rank | "Rookie", "Regular", "Veteran", "Elite", "Champion", "Commander", "Legendary" (badge on a ship tooltip) | `UnitRank`; "Regular" is `UnitRank.Standard` | An enemy ship's strength tier. Distinct from conquest rank and from rarity. |
| rarity | Standard, Enhanced, High Grade, Exotic, Legendary (item colors; High Grade is blue) | `Rarity` | An item's quality tier. Legendary appears at level 60 or higher. |
| sector control mission | "Sector Control" (`ConquestThreeFactions`), "Hostile Takeover" (Stellar), "The People's Sector" (Kolyatov), "Harmony" (Luminate) | `ConquestStellar2`, `ConquestKolyatov2`, `ConquestLuminate2` | The mission that asks for 30% of the Conquest sector. The pages also call it "the 30% mission". |
| Beyond the Front | "Beyond the Front" | `StoryMissionGoToExpansion` | The story mission that unlocks the expansion and, when finished, claiming. |

## Reputation

Reputation with a faction is a number. The game shows its level name. Bands, from `ReputationLevelExtensions`:

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

Each level starts at the number shown in the code threshold table, counted toward zero from below: a reputation of exactly -5000 is Hostile, exactly -500 is Wary, and 0 up to 1,499 is Neutral.

"Enemy" does not mean a level. A faction is your enemy when you are at war with it or your reputation is below -500, which is Wary (except exactly -500) and everything worse. Enemy ships and stations attack you and can be targeted with a plain click. Only non-enemy stations and ships need the force-attack steps in [stations](stations.md) and [faction economy](faction-economy.md).

The pages' wording: "at or below -5000" means Hostile or worse; "above -500" means Neutral or better.

## Stats and combat abbreviations

| Term | What the game shows | Code name | Meaning |
|---|---|---|---|
| HP | "Hull HP", "Armor HP", "Shield HP" | `EquipStat.HullHP` and so on | Hit points. A ship or station part has three pools: shield, armor, hull. |
| Combat Power (stat) | "Combat Power" ("The effectiveness of combat systems and weapons") | `EquipStat.CombatPower` | A ship or station part stat that scales weapon damage. The Faction overview's "Combat Power" tile is a different thing (a faction's total fleet points). |
| Drone Power, Torpedo Power | "Drone Power", "Torpedo Power" | `EquipStat.DronePower`, `TorpedoPower` | The same scaling for drones and torpedoes. War effort multiplies these three stats on conquest stations. |
| DPS | none | none | Damage per second. |
| DoT | none | none | Damage over time: damage applied across several seconds, for example Heat damage from an aspect. |
| Damage Reduction, resistance | "Damage Reduction", "Kinetic Resistance" and so on | `EquipStat.DamageReduction`, `*Resist` | See [combat damage](combat-damage.md). |
| EMP | "EMP Output" | `EmpOutput` | Electromagnetic pulse. EMP weapons raise the chance that a damaged ship becomes boardable. |
| AP ammo | "Armor Piercing" rounds | `BoardingAmmo` | Boarding ammunition that does well against armored crew. |
| DT Chaingun | "DT Chaingun" | weapon name | A large kinetic weapon. "DT" is part of its name. |

## Other abbreviations

| Abbreviation | Spelled out | Notes |
|---|---|---|
| NPC | non-player character | Anything the game controls: enemy ships, faction fleets, stations. |
| AI | artificial intelligence | The pages avoid the bare term: they say "NPC factions" or "fleet planning". ECHO is your own ship's AI. |
| HQ | headquarters | The game's mission text uses "HQ". The pages write "headquarters". |
| POI | point of interest | A code term for a location in a system. The pages write "location". |
| XP | experience points | |
| UI | user interface | |
| LMB, RMB | left and right mouse button | Used by the game's hints ("RMB to build"). |
| NBS | next-battle strength | The pages no longer use the abbreviation; see the Conquest map table. |

## Collisions in one list

- Outpost: outpost system, economic outpost, Industrial Outpost, Automated Outpost.
- Defense: the number, the station type, the part, the sector bonus, the stance, the stat group.
- Combat Power: the ship stat and the Faction overview tile.
- Contribution: conquest standing, the 10-point mission step, and embassy wealth.
- Rank: conquest rank, ship rank, Patrol Officer rank. Conquest ranks have faction-specific titles.
- Strength: "strength" on fleet cards and "Total strength" on the Empire Fleets header are points.
- Tick: the conquest update and the hourly economy tick are separate 3600 s timers.
- Sector: the game says "Subsector" for a sector and "Quadrant" for the bigger region.
- War zone: the game uses the phrase in fleet messages. The map shows no label for it.
- Under siege and Under Attack: both appear on stations and the map for a siege.
- Wealth, income, Economic Power: wealth is the pool; Economic Power is the tile that shows it; income is what flows in.
