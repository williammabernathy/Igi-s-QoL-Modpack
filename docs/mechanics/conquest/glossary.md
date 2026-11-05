# Glossary: Conquest

This page covers the terms introduced by the Conquest stage, which opens at level 28 with the Sector Control missions and the embassies. Earlier terms are in the [basics glossary](../basics/glossary.md); terms that come later are on later stage pages.

Wording is read from the game's translation strings and the decompiled interface code of version 0.8.3.4, not from play. The interface code was compared with the 0.8.3.5 source. The translation strings were not re-read for 0.8.3.5. The table columns are explained in the basics glossary. The code names for each term are in the last section, "In the Game Code".

Pages: [war](war.md), [sector control](sector-control.md), [war stations](war-stations.md), [faction economy](faction-economy.md).

## Places and Stations

| Term used here | What the game shows | Meaning |
|---|---|---|
| war zone | "war zone" in fleet messages ("Fleets can only attack and raid inside the war zone"); the map region label "Conquest >" | Every system where factions fight over ownership. This is the Conquest sector (Ara Martis). The Frontier, Staging and Darkspace regions are not part of it. |
| Conquest sector | "Conquest sector" (embassy dialogue), sector names starting "Ara Martis" | The original war zone. The "Area Conquered" percentage is counted over its systems only. |
| hub | "Space Station" (the system's main station on the map), "Conquest Stations" (Faction overview row), "its hub" (right-click hint), "destroy its hub" (ECHO tip) | The main station of a war-zone system. It carries the owner's flag. Destroying it abandons the system. |
| headquarters | "Conquest headquarters" (system card title), "Headquarters" (Faction overview row), "Gate to {faction} headquarters" | The one system a faction cannot lose to a fleet. Its hub has the faction commander and shop. The game's mission text shortens the word to "HQ", but these pages write "headquarters". Destroying its hub by hand still abandons it (not verified in play). |
| outpost system | "Conquest outpost" (system card title) | An ordinary war-zone system that fleets can besiege and capture. It is not a headquarters or your capital (the capital's card title is "Your capital - no faction can take it"). These pages write "outpost system" for it, always with the word "system". |
| Defense station | "Defense" (station type), "Defense stations" (ECHO tip) | A station type whose job is to hold a garrison and fire on enemies. Not the same as the Defense number. |
| economic outpost | "Economic Outposts" (Faction overview, shown as "{n} / {m} slots used"); "outpost" in "click the outpost to raid" and "outpost raided" | A station that produces something: Mining, Salvage, Refinery, Forge, Production, Trading, Recruitment or Construction Yard. Raids target these, including yours. A system holds two faction stations of any type, and the "slots used" count includes Defense stations. |
| Construction Yard | "Construction Yard" | Builds warships. See [faction economy](faction-economy.md). |
| embassy | "Embassy Station", "Embassy" (Faction overview row), "Neutral embassy - Hostilities are not tolerated", "Gate to {faction} embassy" | A faction's neutral station in the Staging sector. Its missions pay the faction wealth and rebuild your reputation faster. It is not part of the war zone. |
| combat station (war) | "Combat Station" (map location type) | Hubs and Defense stations are also combat stations, built from destroyable parts like the ones in [stations](../basics/stations.md). [War stations](war-stations.md) covers them. |

Terms in this group that are easily confused:

- "Station" alone is generic. The pages use it only for any of the kinds above, and name the kind when it matters.
- "Hub", "main station" and "Space Station" are the same thing. The pages use "hub".

In the code: [Places and Stations](#code-places-and-stations).

## Conquest Map

| Term used here | What the game shows | Meaning |
|---|---|---|
| Defense row | "Defense" row in the system card's conquest section | The strength the defenders put into a siege round: station firepower plus garrison ships plus defending fleets. It is 0 for an unowned system. |
| next-battle strength | The game never shows this term. | What the factions' fleet planning compares attackers against. With Defense stations in the system, it is the weakest Defense station's strength (its own firepower, its garrison and all defending fleets). Otherwise it is the full Defense number. Fleets already traveling to relieve the system are added, and it is 0 for an unowned system. A fleet or assault attacks at 1.15 times this figure. You can estimate it: with no Defense station it equals the Defense row, and with Defense stations it is usually lower. The exception is a settling system, where the row halves the station profile but a Defense station's own strength is not halved. |
| war effort | "Your war effort" row ("+10% firepower (2h 59m)"); mission reward "+10% Station firepower, -10% for {faction} next system"; the "ECHO The Conqueror" skill text | A multiplier on a system's station firepower, raised by Conquest missions you complete and lasting 3 hours from the last one. |
| Control | The game has a "Control" text line that the system card never shows. "Tenuous", "Contested" and "Secure" also exist as text, but no code shows them. | An internal counter from 0 to 2. The pages mostly ignore it and call it the "control level", which is not a game label. |
| Area Conquered | "Area Conquered" (Faction overview, Conquest section) | A conquest faction's share of all Conquest-sector systems, refreshed at each conquest update and whenever a system is abandoned. |
| Your Contribution | "Your Contribution" (Faction overview) | The total contribution you have earned with a faction. It sets your conquest rank. |
| conquest update | "Conquest Update" card with a countdown bar on the galaxy map; tooltip "Damaged stations are rebuilt and factions rethink their strategy" | The hourly step (3600 s of game time) that recalculates Area Conquered, rebuilds damaged stations and counts down settling. The pages call it "the conquest update". It does not assign fleet orders, despite the tooltip; see "planning pass". |
| planning pass | The game does not show this term. | The moment a faction's idle fleets get new orders. It runs for every faction every 600 s, at once for the factions of a battle that just ended, and at the hourly economy tick. The pages also call it military planning. |
| frontline | "frontline" (ECHO tips, "Fleets are concentrated along the frontline systems") | A faction's systems that touch an enemy or unowned war-zone system. |
| unrest, settling | The game does not name these. | The hours after a system changes owner. Unrest (the first 2 conquest updates) pays no taxes. Settling (about 5 to 6 hours) halves the station profile of the hub and every Defense station. That shows in the Defense row and in a hub siege, but not in a Defense station's own siege. The pages use these two plain words. |
| abandoned | "Abandoned - the first faction to arrive claims this system" (hub tooltip), "Abandoned" (the Stations list on the system card), "{faction} lost {system} - the system is unclaimed" | A system whose hub was destroyed. It has no owner until the lead attacker of a hub siege takes it at once, you claim it, or the first fleet lands. It remembers which faction abandoned it. Its hub is an ordinary station with no owner, not a wreck or a derelict ship; these pages call it an abandoned hub. |
| claim | "Claimed {system}.", "Take over {system}?" (your prompt) | Becoming the owner of an unowned system: by arriving (NPC fleets), through the offer you get when you destroy a hub with claiming unlocked, or by being the lead attacker of a hub siege when the hub dies. The claim destroys the Defense stations of the faction that abandoned the system, unless that faction takes it back. |
| siege | "Under siege" (system), "Under Attack! Holds {n} min at most" (hub tooltip), "{fleet} began a siege of {system}" (Empire Log) | The fight over a system's hub or one of its Defense stations. |
| station structure | The game never shows this term. | The hull total of the hub or Defense station that the attackers must wear down in a simulated siege before the station counts as broken. These pages use "station structure" for it; the code calls it the station hull. |
| sitting out | The game never shows this term. | The 5 conquest updates after a faction loses its last war-zone system. In that time the faction gets no Great Gate reinforcement and its fleets do not go out to claim open systems. |
| Fleet Battle | "Fleet Battle" (location name), "Under Attack, resolves in {n} min" | The fight between an arriving fleet and a fleet defending the system. The pages call it a "clash" or "Fleet Battle". |
| raid | "Raiding {station}", "raid succeeded", "outpost raided", "Raided - the station is paused while it recovers" | A fleet attacking one economic outpost (an NPC faction's, or yours) to loot it and stop its production. |
| assault | "Gathering for an assault" (fleet label) | Several of one faction's fleets meeting next to a target and merging to attack it. |
| relief | The game does not show this term. | Same-faction fleets traveling toward a system under threat. They add to next-battle strength. |
| Empire Log | "Empire Log" (window), categories Combat, Fleets, Stations, Industry, Warnings | Your log of battles and captures. It uses the outcome wording in the outcome table below. |

In the code: [Conquest Map](#code-conquest-map).

## Fleets and Battles

| Term used here | What the game shows | Meaning |
|---|---|---|
| points (in battles) | "strength" on fleet cards ("{n} ships, {m} strength", "STRENGTH") | Fleet strength, garrison size and damage in simulated battles are all counted in points, the ship value described in the [basics glossary](../basics/glossary.md) and [scaling](../basics/scaling.md). |
| Combat Power (faction) | "Combat Power" tile on the Faction overview | The sum of a faction's fleet points. Not the same as the Combat Power ship stat; see the [stats table](../basics/glossary.md#stats-and-combat-abbreviations) in the basics glossary. |
| fleet | "Fleets Fielded" (Faction overview), "Battlefleet", "Battlegroup" names | A group of ships that moves and fights together on the map. NPC fleets aim for 2000 points. |
| fleet cap | The game does not show this term. | How many fleet slots a faction may fill. For the three majors it rises with war-zone systems held. For Mindus Holdings, Steel Vultures and Stranded it follows the embassy wealth you deliver. |
| garrison | "joined a station garrison" (build log) | The warships held by an NPC Defense station. They count toward Defense. |
| hull rating, hull edge | "hull rating" (fleet card tooltip), "Hull edge {x} vs {faction} fleets nearby" | The hull rating is how upgraded a ship's hull is. The hull edge is the swing of up to 15% in strength between two sides' hull ratings in simulated battles. |
| warship delivery | "{ship} flying to its fleet at {system}" (Faction overview) | A newly built ship flying from a yard to its fleet or garrison. You can intercept it. |
| interception | "Interception" (location name), "Chase" messages | Catching a fleet, delivery or cargo run from the galaxy map and fighting it in a special location. |
| Great Gate fleet | "Great Gate fleets" (Faction overview ledger), "The Great Gate" (map location) | A free or paid 1500-point fleet that appears from the Great Gate for Kolyatov, Stellar or Luminate when they have few fleets. |

Battle outcomes, with the wording the Empire Log shows (attacker's view / defender's view):

| Pages say | Empire Log text |
|---|---|
| captured | "system captured" / "system lost" |
| skirmish | "inconclusive skirmish" |
| repelled | "attack repelled" |
| attacker destroyed | "fleet destroyed" / "attackers destroyed" |
| Defense station destroyed | "Defense station destroyed" / "Defense station lost" |
| withdrew | "withdrew before relief arrived" / "attackers withdrew" |
| raid succeeded | "raid succeeded" / "outpost raided" |
| aborted | no text of its own |

In the code: [Fleets and Battles](#code-fleets-and-battles).

## Faction Economy

| Term used here | What the game shows | Meaning |
|---|---|---|
| wealth | "Economic Power" tile and graph on the Faction overview; "Wealth (last 24h)" ledger header; "Faction wealth" in mission rewards | The money a faction spends on warships, stations and Defense stations. The pages use "wealth". |
| taxes | "Taxes" ledger line | Wealth a faction earns every hour from the systems it owns. Stops while a system is in unrest. |
| home subsidy | "Home subsidy" ledger line | Money handed to Kolyatov, Stellar, Luminate and Darkspace each hour, larger the smaller their share of the war zone. Not a tax and not income from systems. |
| trade deliveries, intertrade sales | "Trade deliveries", "Intertrade sales" | Wealth from goods moved between stations or sold. |
| Warships, Work bay ships, Stations | "Warships", "Work bay ships", "Stations" | What the faction spends wealth on. Work bay ships are the mining and salvage ships that a faction station's work bay (its Mining/Salvage Bay) sends out to claim a site. |
| Great Gate fleets | "Great Gate fleets" ledger line | Wealth spent on a paid Great Gate fleet. |
| your embassy missions | "Your embassy missions" ledger line | Wealth you added by completing embassy missions. |
| raids, lost to raids | "Raids", "Lost to raids", "Lost to your raids" | Wealth taken by or lost to raids. |
| pocket system | "{name}, the Motherlode", "{name} Graveyard", "Warzone {name}", "Dread Port {name}" (system names) | A small side system linked to a host system by a locked jumpgate. Factions only build economic outposts in the Motherlode and Graveyard ones. |
| hourly economy tick | The game does not show this term. | The hourly step that pays taxes and subsidies and plans builds. It runs on its own 3600 s timer, separate from the conquest update. |
| under-cap discount | The game does not show this term. | Warships cost half and build three times faster while a faction has fewer fleet slots than its cap. |

Terms in this group that are easily confused: "income" in the game's faction ledger means taxes, trade and sales together, and for you it also covers officer idle income and mission rewards. "Subsidy" and "tax" are different ledger lines. Taxes come from owned systems. The home subsidy is a top-up that shrinks as a faction grows.

In the code: [Faction Economy](#code-faction-economy).

## Missions and Standing

| Term used here | What the game shows | Meaning |
|---|---|---|
| Conquest mission | missions from a hub or embassy mission board; reward line "+x% Station firepower" | A mission at a war-zone hub that raises war effort, or at an embassy that gives wealth. |
| idle mission | "Idle Conquest missions" (skill text) | A mission your ship AI ECHO accepts and completes for you while you are away. |
| contribution | "Your Contribution" (Faction overview); "Run missions for {faction}" step; "Wealth your missions brought {n}: x of 1,500,000" | Three related counters. The first is your conquest standing with a faction, earned by Conquest missions. The second is the 10 points the first sector-control mission asks for. The same mission amounts also add to your standing, but they only count toward the 10 while that step is current. The third is the wealth your embassy missions delivered, which you need to join a small faction's war. |
| conquest rank | "Intern" to "Sector Partner" (Stellar), "Conscript" to "Hero of the People's Glory" (Kolyatov), "Seeker" to "Oracle's Chosen" (Luminate) and similar | Your title with one faction, from Your Contribution. Higher ranks unlock ships. There are six ranks, plus having no rank. |
| Vanguard Marks | "Vanguard Marks" | A currency from missions, used to buy ships. Pages write "marks". |
| sector control mission | "Sector Control", "Hostile Takeover" (Stellar), "The People's Sector" (Kolyatov), "Harmony" (Luminate) | The mission that asks for 30% of the Conquest sector. The pages also call it "the 30% mission". |

In the code: [Missions and Standing](#code-missions-and-standing).

## Stats and Combat Abbreviations

| Term | What the game shows | Meaning |
|---|---|---|
| Combat Power, Drone Power, Torpedo Power (war effort) | "Combat Power", "Drone Power", "Torpedo Power" | War effort multiplies these three stats on conquest stations. The Faction overview's "Combat Power" tile is a different thing (a faction's total fleet points). |

In the code: [Stats and Combat Abbreviations](#code-stats-and-combat-abbreviations).

## Other Abbreviations

| Abbreviation | Spelled out | Notes |
|---|---|---|
| HQ | headquarters | The game's mission text uses "HQ". The pages write "headquarters". |
| NBS | next-battle strength | The pages no longer use the abbreviation; see the Conquest map table. |

## Easily Confused Terms

- Combat Power is both a ship stat and the Faction overview tile.
- Contribution means your conquest standing, the 10-point mission step or embassy wealth.
- Rank means conquest rank, ship rank or Patrol Officer rank. Conquest ranks have faction-specific titles.
- The word "tick" can mean the conquest update or the hourly economy tick. They are separate 3600 s timers, and fleet orders are also assigned on a third timer, the 600 s planning pass.
- The game uses "war zone" in fleet messages, but the map shows no label for it.
- "Under siege" and "Under Attack" both appear on stations and the map for a siege.
- Wealth, income and Economic Power are different things. Wealth is the pool, Economic Power is the tile that shows it, and income is what flows in.
- "Station" alone is generic. "Hub", "main station" and "Space Station" are the same thing.

## In the Game Code

The wording above comes from the translation strings (`resources.assets`, read in 0.8.3.4) and the decompiled interface code of version 0.8.3.4, compared with the 0.8.3.5 source. Each table below maps a term to the class, method, field or enum name that carries it, so you can search the decompiled source for it. Where the game shows a text key that has no visible text, the key is named here.

### Code: Places and Stations

| Term | Code name |
|---|---|
| war zone | `ConquestScope.IsWarZone`, `SectorMapData.isWarZone` |
| Conquest sector | `ConquestRegion.AraMartis`, `conquestSector` |
| hub | `ConquestStation` |
| headquarters | `ConquestSystem.headquarters` |
| outpost system | `ConquestSystem` that is not a headquarters |
| Defense station | `PlayerStationType.Defense` on `FactionStation` |
| economic outpost | `FactionStation` that is not a Defense station |
| Construction Yard | `PlayerStationType.ConstructionYard` |
| embassy | `EmbassyStation`, `EmbassyJumpgate` |
| combat station (war) | `CombatStationData`, `CombatStationPart` |

### Code: Conquest Map

| Term | Code name |
|---|---|
| Defense row | `SystemCardDefense`, `ConquestCombat.GetDefenseStrength` |
| next-battle strength | `ConquestCombat.GetNextBattleStrength` |
| war effort | `warEffortBonus`, `warEffortPenalty`, `warEffortMultiplier` |
| Control | `controlLevel` ("Control" is a text key that exists but the system card never shows it) |
| Area Conquered | `currentConqueredPercentage` |
| Your Contribution | `ConquestFactionStanding.playerContribution` |
| conquest update | `Conquest.DoConquestTick`, `conquestTickTime` |
| planning pass | `LivingUniverse.TickMilitaryPlanning`, `ConquestFleetPlanner.PlanBetweenTicks`, `ConquestFleetOrders.AssignOrders` |
| frontline | `ConquestFrontline`, `FrontlineSystem` |
| unrest, settling | `isInUnrest`, `isSettling`, `settlingTicks` |
| abandoned | `abandonedBy`, `IsAbandonedHulk` (a `ConquestStation` with no faction; the name is misleading, because it is not a salvage wreck) |
| claim | `SystemClaim`, `ConquestOwnership.Claim` |
| siege | `BattleStage.StationSiege`, `DefenseStationSiege` |
| Fleet Battle | `BattleStage.FleetClash`, `BattleZone` |
| raid | `BattleStage.Raid`, `StationRaid` |
| assault | `ConquestFleetOrders.PlanAssault` |
| relief | `GetIncomingRelief` |
| Empire Log | `EmpireLogBook` |

### Code: Fleets and Battles

| Term | Code name |
|---|---|
| points (in battles) | `pointValue`, `totalPoints`, `FleetPoints` |
| Combat Power (faction) | `FactionEconomy.combatPower` |
| fleet | `ConquestFleet` |
| fleet cap | `ConquestFleetPlanner.MaxFleetsFor` |
| garrison | `FactionGarrison`, `garrisonShips` |
| hull rating, hull edge | `HullRating`, `GetEdge` |
| warship delivery | `WarshipDelivery` |
| interception | `InterceptZone`, `IInterceptable` |
| Great Gate fleet | `GreatGateReinforcement` |

Battle outcomes:

| Pages say | Code name |
|---|---|
| captured | `BattleOutcome.Captured` |
| skirmish | `Skirmish` |
| repelled | `Repelled` |
| attacker destroyed | `AttackerDestroyed` |
| Defense station destroyed | `DefenseStationDestroyed` |
| withdrew | `Withdrew` |
| raid succeeded | `Raided` |
| aborted | `Aborted` |

### Code: Faction Economy

| Term | Code name |
|---|---|
| wealth | `FactionEconomy.wealth`, `economicStrength` |
| taxes | `LedgerEntry.Passive` |
| home subsidy | `LedgerEntry.Subsidy`, `GreatGateReinforcement.GetSubsidy` |
| trade deliveries, intertrade sales | `LedgerEntry.Trade`, `Sales` |
| Warships, Work bay ships, Stations | `LedgerEntry.Warships`, `BayShips`, `Stations` |
| Great Gate fleets | `LedgerEntry.GreatGate` |
| your embassy missions | `LedgerEntry.PlayerSupport` |
| raids, lost to raids | `LedgerEntry.Raids`, `Raided`, `PlayerRaids` |
| hourly economy tick | `LivingUniverse.RunHourlyTick` |
| under-cap discount | `fleetSlotsUsed < MaxFleetsFor` |

### Code: Missions and Standing

| Term | Code name |
|---|---|
| Conquest mission | `ConquestStrength` reward |
| idle mission | `mission.idle` |
| contribution | `playerContribution`, `ConquestFleetStrength`, `joinWealth` |
| conquest rank | `ConquestRank`, `GetConquestRankTranslation` |
| Vanguard Marks | `vanguardMarks`, item `VanguardMark` |
| sector control mission | "Sector Control" is `ConquestThreeFactions`; `ConquestStellar2`, `ConquestKolyatov2`, `ConquestLuminate2` |

### Code: Stats and Combat Abbreviations

| Term | Code name |
|---|---|
| Combat Power, Drone Power, Torpedo Power (war effort) | `EquipStat.CombatPower`, `DronePower`, `TorpedoPower` |
