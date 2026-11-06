# Earning Credits

This page covers every way to earn credits that the game opens before the Conquest stage: missions, facility operations, Signal Traps, Locked Containers, kills, selling, crafting, trading and idle officers. It says what each one pays at levels 10, 30, 60 and 100, what gates it, and which source pays best at your level. Code was read from the decompiled source of game version 0.8.3.5. Recipe, skill, item and facility values come from the 0.8.3.5 data dumps, and English names from the game's translation strings. Nothing here was measured in play. A few per-facility averages were estimated by simulating the game's room rules, and the page says where.

Terms and abbreviations are explained in the [glossary](glossary.md). Other pages cover related numbers:

- The level multipliers and the Escalation table: [scaling.md](scaling.md#growth-with-level) and [Escalation](scaling.md#escalation).
- What a destroyed NPC ship drops: [scaling.md](scaling.md#what-a-destroyed-npc-ship-drops).
- What station parts drop: [stations.md](stations.md#what-drops-when-a-part-dies).
- How facility operations and boarding are fought: [boarding.md](boarding.md).

Later stages add their own credit sources and sinks: fixed story rewards in [Conquest](../conquest/README.md) and your own systems, stations and Empire Fleet raids in [Beyond the Front](../expansion/README.md). This page only points to them.

## How Credit Amounts Scale

Almost every credit amount in the game is a credit factor turned into credits with a level:

```
credits = round(credit factor x 100 x credit multiplier(L))
credit multiplier(L) = 2^(L/14) up to level 100, then it doubles every 25 levels
```

A higher factor pays more, and the credit multiplier doubles every 14 levels. One credit factor point is worth:

| Level | 10 | 30 | 60 | 100 |
|---|---|---|---|---|
| Credit multiplier | 1.64 | 4.42 | 19.5 | 141 |
| Credits per factor point | 164 | 442 | 1,950 | 14,132 |

The level L is usually the level of the place, not your level:

| Source | Level used |
|---|---|
| Board missions | The level of the station that offers the mission |
| Bounty, patrol and industrial missions | The board level (station level plus a rank bonus) |
| Facility and boarding loot | The level of the location that holds the facility or ship |
| Kill and station part drops | The level of the unit that died |
| Locked Containers | The box's item level |
| Signal Trap bosses | Your effective level (captain level plus Vanguard level) + 5 or + 30 |
| Side-mission and skill-tree mission rewards | Your captain level |

So flying to higher-level systems raises almost every payout, and fourteen levels up doubles it. Enemy damage doubles every 10 levels ([Growth With Level](scaling.md#growth-with-level)), so a site above your level gets harder faster than it pays more. You cannot start a facility operation at a location 10 or more levels above your effective level ([Facilities](boarding.md#facilities)).

In the code: [How Credit Amounts Scale](#code-how-credit-amounts-scale).

## Every Source at a Glance

Average credits per event, before reputation and Escalation bonuses. A dash means the source isn't available at that level.

| Source | Credit factor | L10 | L30 | L60 | L100 |
|---|---|---|---|---|---|
| Signal Trap boss kill (boss at your effective level + 5) | 300-499 | 83,955 | 225,991 | 998,059 | 6,485,404 |
| One whole facility, every room and locked room opened (range over facility types, simulated) | caches below | 25k-58k | 81k-168k | 432k-805k | 3.4M-6.1M |
| One locked facility room | 114.5 on average | 18,771 | 50,591 | 223,197 | 1,617,549 |
| Ship Capture mission | 40 x 2.5 to 4 | 15,077 | 42,788 | 196,852 | 1,472,270 |
| Dungeon Assault mission (the mission reward only) | 40 x 1.8 to 4.5 | - | 33,150 | 164,181 | 1,262,754 |
| Bounty Hunt, Escort Duty, Station Battle and other combat, salvage and sample missions | 40 x 1 to 1.6 | 6,031 | 17,115 | 78,741 | 588,908 |
| Clear Asteroid Field, Clear Salvage Field | 40 | 5,278 | 13,880 | 60,145 | 429,302 |
| Mine Ore mission | 40 x 0.75 | 3,959 | 10,410 | 45,109 | 321,977 |
| Courier mission | 40 x 0.6 | 3,167 | 8,328 | 36,087 | 257,581 |
| Bounty Board target | 25 | 4,102 | 11,041 | 48,761 | 353,309 |
| Patrol Roster wave | 15 | 2,461 | 6,625 | 29,256 | 211,985 |
| Station Core kill | 13.5-15.25 | 2,358 | 6,349 | 28,037 | 203,152 |
| SF-CRK Bot credit container, once per wreck | 7.5-15 | 1,846 | 4,968 | 21,942 | 158,989 |
| NPC ship kill (30% chance of 2.5-3.25) | 0.86 on average | 142 | 381 | 1,682 | 12,189 |
| Locked Container opened with credits (net) | -19% of the price | -1,511 | -4,190 | -18,501 | -134,057 |

The mission rows are averages over the board's difficulty mix, including the 30% of missions that pay an item instead of credits (see [What a Mission Pays Out](#what-a-mission-pays-out)). The Ship Capture row at level 10 assumes a station of level 10 or higher, and the Dungeon Assault row needs a station of level 16 or higher.

In the code: [Every Source at a Glance](#code-every-source-at-a-glance).

## Missions

### Board Missions by Type

A station's Mission Board offers missions whose base value is a credit factor of 40 at the station's level. Each mission type multiplies that base, and some types pay more at higher difficulty:

| Mission (category shown in game) | Easy | Normal | Hard | Skull | Insane | Station level | ECHO can run it | Escalation bonus |
|---|---|---|---|---|---|---|---|---|
| Ship Capture | x2.5 | x3.0 | x3.5 | x4.0 | x4.0 | 10+ | Yes, with Boarding mode | Yes |
| Dungeon Assault | x1.8 | x2.2 | x2.8 | x3.5 | x4.5 | 16+ | Yes, with Installations mode | Yes |
| Bounty Hunt, Escort Duty, Station Battle, Mining Support | x1 | x1.2 | x1.4 | x1.6 | x1.6 | Escort and Station Battle above 5 | Not Escort Duty or Station Battle | Yes |
| Collect Ore Samples, Collect Ancient Salvage, Salvage Wreckage | x1 | x1.2 | x1.4 | x1.6 | x1.6 | any | Yes | Above Easy only |
| Clear Asteroid Field, Clear Salvage Field | x1 | x1 | x1 | x1 | x1 | any | Yes | Yes |
| Deliver Crafted Goods | x1 | x1 | x1 | x1 | x1 | station with a Forge | No | No |
| Trader | x1, plus payment for the goods | | | | | any | No | No |
| Mine Ore | x0.75 | | | | | any | Yes | No |
| Trade Materials | x0.5 x the difficulty multiplier (1, 1.8, 2.8, 3.6, 6) | | | | | any | No | No |
| Courier | x0.6 | | | | | above 2 | Yes | No |

Bounty Hunt, Ship Capture, Dungeon Assault, Clear Asteroid Field and Clear Salvage Field also need the prologue's ninth tutorial mission done, or a game started without the prologue. Salvage Wreckage needs the seventh. The Trader mission pays its normal reward plus the value of the trade goods you deliver, which must come from an Intertrade Network market elsewhere in the sector. Its goods payment gets no bonuses.

Which types a board offers depends on the station's faction. The three main factions offer 15 types. Orsanon Security and Omnitac Agency offer the combat set (Bounty Hunt, Ship Capture, Dungeon Assault, the clear-field missions, Trader, Station Battle and Escort Duty). Mindus Holdings offers mining missions and Steel Vultures salvage missions. Intertrade Network offers Courier, Bounty Hunt, Escort Duty and Trade Materials. Forge Industries stations have no Mission Board. Every eligible type is equally likely to be picked for a board slot.

In the code: [Board Missions by Type](#code-board-missions-by-type).

### How Difficulty Is Rolled

Harder missions only appear at higher station levels: Normal from level 3, Hard from 5, Skull from 20 and Insane from 50. Their share grows with level:

| Station level | Easy | Normal | Hard | Skull | Insane |
|---|---|---|---|---|---|
| 10 | 47.4% | 30.9% | 21.7% | | |
| 30 | 34.2% | 27.0% | 20.7% | 18.0% | |
| 60 | 22.0% | 25.4% | 22.0% | 17.0% | 13.6% |
| 100 | 9.1% | 27.3% | 27.3% | 18.2% | 18.2% |

The difficulty multipliers in the type table are the only way difficulty raises credits. The game's own difficulty reward multiplier (1, 1.8, 2.8, 3.6, 6) only changes mission XP, except on Trade Materials.

In the code: [How Difficulty Is Rolled](#code-how-difficulty-is-rolled).

### What a Mission Pays Out

When a mission is generated, its value V (the base times the type multiplier) goes through one roll:

1. 30%: an equipment item instead of credits. If the item is Standard you also get 0.5 x V in credits, and if it is Enhanced 0.25 x V. The item is Standard or Enhanced only on Easy, Normal and Hard missions.
2. Otherwise, at a station with a Forge, 20%: a blueprint instead of credits, with the same small extra credits.
3. Otherwise: credits worth V times a random 0.8 to 1.25. A quarter of these missions also give a Plasma Cell.

Separately, Hard, Skull and Insane missions have a 5% chance of a Hull Upgrade Kit. Missions from a station whose faction is hostile to you (at war, or reputation below -500) pay no credits or items at all, only reputation, XP and Workshop Credit.

Averaged over that roll, a board mission pays this share of V in credits:

| Difficulty | Station without a Forge | Station with a Forge |
|---|---|---|
| Easy | 0.83 | 0.74 |
| Normal | 0.79 | 0.69 |
| Hard | 0.76 | 0.63 |
| Skull, Insane | 0.72 | 0.57 |

The Forge check uses the station you are docked at when the board is filled. The items and blueprints replace those credits, so a Forge station isn't worse overall, only lower in credits.

In the code: [What a Mission Pays Out](#code-what-a-mission-pays-out).

### Reputation and Escalation Bonuses

Mission credits are multiplied by your reputation level with the station's faction:

| Reputation level | Cordial | Friendly | Respected | Distinguished | Exalted |
|---|---|---|---|---|---|
| Mission credits | +5% | +10% | +20% | +30% | +40% |
| Extra missions on the board | 0 | +1 | +2 | +2 | +3 |

Escalation then adds its mission credit bonus, +10% at tier 1 up to +150% at tier 8 ([Escalation](scaling.md#escalation)), but only to missions that have an enemy faction. The last column of the type table says which ones do. Courier, Mine Ore, Deliver Crafted Goods, Trader and Trade Materials never get it. The tier is stored on the mission when it is generated, and changing the tier re-prices missions already on the board or accepted. From the Conquest stage a conquest rank adds another bonus with conquest factions ([Conquest](../conquest/README.md)).

At Exalted and tier 8, an enemy-faction mission pays 1.4 x 2.5 = 3.5 times its base credits.

In the code: [Reputation and Escalation Bonuses](#code-reputation-and-escalation-bonuses).

### The Mission Board

A board holds 6 missions plus the reputation bonus above, so at most 9. Its 300-second timer (game time) keeps running while you are away, but the board only refills while you are docked at that station, so a station you left more than 300 seconds ago refills as soon as you dock. Accepting a mission doesn't replace it until the next refill. You can hold 20 missions at once ("Mission limit reached! Remove one to accept a new one."). Clicking a mission with less than 20 seconds left on the timer pushes the refill back 30 seconds.

In the code: [The Mission Board](#code-the-mission-board).

### Missions ECHO Runs for You

With Autopilot on, ECHO takes missions from the board on its own (idle missions). They pay far less:

| Case | Credits as a share of V |
|---|---|
| A mission you take yourself (no Forge, Easy) | 0.83 |
| ECHO, with "Automated Negotiations" | 0.32 |
| ECHO, without it | 0.16 |

An idle mission's credits are V x 0.33, halved again without the Prompt Engineering skill "Automated Negotiations" (1 point, no requirement). ECHO always takes Easy missions, or Normal with "Risk & Reward" (8 points in the tree). Its missions also have only a 5% item chance (10% with "Find Me Loot") and give a tenth of the reputation (a twentieth without Automated Negotiations). While Autopilot is on, half of the credit drops from regular kills are skipped, but facility loot, boss loot and Locked Containers are not affected.

In the code: [Missions ECHO Runs for You](#code-missions-echo-runs-for-you).

### Bounty, Patrol and Industrial Boards

Three stations carry a separate board with three missions each:

| Board | Where | Credits | Escalation | Also pays |
|---|---|---|---|---|
| Bounty Hunt | Orsanon Security stations of level 10+ | Credit factor 25 | Yes | 7-8 x credit multiplier Vanguard Marks (x0.7, 1 or 1.3 by slot), 175 Orsanon and 350 second-faction reputation |
| Patrol Roster | Canisec stations of level 10+ | Credit factor 15 per wave | Yes | Vanguard Marks, 125 Canisec and 250 patrol-faction reputation |
| Industrial Ops | Forge Industries stations | Credit factor 25 per wave | Yes | Vanguard Marks, 325 Forge Industries reputation |

The three slots are Normal, Elevated and Extreme. Elevated is 3 + rank/2 (rounded up) levels above the station and Extreme is 5 + rank levels above, where rank is your bounty, patrol or industry rank. Reputation doesn't raise these credits. The set of three is picked by the real calendar day and changes when you launch one. A bounty pays the same credits in every slot except for the level difference. Patrol and industrial missions start a new wave each time you finish one: a patrol wave is one level higher than the last, and an industrial wave pays the same as the first. Bounty bosses are Elite or better ([Boss Ranks](scaling.md#boss-ranks)).

In the code: [Bounty, Patrol and Industrial Boards](#code-bounty-patrol-and-industrial-boards).

### Bar Patron Orders

The bar seats up to 5 patrons, rolled once per real calendar day. Three kinds of patron offer a delivery that pays well above shop prices:

| Patron | Wants | Pays |
|---|---|---|
| Ore Trader | 5-9 + 0.55 x station level units of one surface ore of the system (70% the common ore) | 3 x the ore's value per unit |
| Salvage Trader | 8-15 + 0.65 x station level units of a salvage item | 2 x the item's value per unit |
| Equipment Order | 2 or 3 pieces of equipment of at least the station's level and a set rarity | 3 x their sell value (without skill bonuses) per piece |

Ore sells to a shop for at most 0.8 of its value (0.48 from level 50), so an Ore Trader pays 3.75 to 6.25 times the shop price for the same ore. At a level-60 station that is 38 to 42 units, which pays about 334,000 to 369,000 credits for Lithar (value 2,926 each). These rewards get no reputation or Escalation bonus. Each of the three kinds fills 15% of the seats (salesmen fill the rest), and some seats are left empty when the bar is rolled.

In the code: [Bar Patron Orders](#code-bar-patron-orders).

### One-Time Story Rewards

Side missions and skill-tree assignment missions pay once, at your captain level:

| Missions | Credit factor each |
|---|---|
| Volunteer Patrol, Your Days Are Numbered, The Lost Gatepass | 100 |
| Time is an illusion | 50 |
| The seven skill-tree assignments (Asteroid Survey, Leaderboard, Publicity Tour, Forgemaster, Evangelism, Brawler, The Intertrade Challenge) | 200 |

The assignments total a factor of 1,400. Because they pay at your level, finishing them later pays more: 618,000 credits at level 30, 2.7 million at level 60 and 19.8 million at level 100. The Conquest story pays fixed amounts (see [Conquest](../conquest/README.md)).

In the code: [One-Time Story Rewards](#code-one-time-story-rewards).

## Facility Operations and Boarding

Facilities (the walk-in locations described in [boarding.md](boarding.md#facilities)) hide credit caches in their rooms. The caches use the facility location's level, aren't affected by Escalation, and need no specialist to pick up. Each room except the Airlock and Corridors can hold three kinds:

| Cache | Room | Chance | Credit factor | Found at |
|---|---|---|---|---|
| Locked-room cache | Every locked room | 100% | 40-79 | 50% of the room's investigation |
| Living-quarters cache | Living Quarters | 30% | 30-119 | 70% |
| Special-loot cache | Every room | 8%, or 48% in a locked room | 80-149 | 50% |

The game logs each one as "Hidden personal cache found." A Control Room, Storage Bay, Workshop, Laboratory, Server Room or Security Office is locked 35% of the time. Locked rooms need a specialist next to them to open ([Locks and Lockdowns](boarding.md#locks-and-lockdowns)), and a run can be won without them. Skipping them costs about half the money, because a locked room averages a credit factor of 114.5 and an unlocked room 9.2 (31.5 for Living Quarters):

| Level | Unlocked room | Living Quarters | Locked room |
|---|---|---|---|
| 10 | 1,502 | 5,168 | 18,771 |
| 30 | 4,049 | 13,927 | 50,591 |
| 60 | 17,862 | 61,444 | 223,197 |
| 100 | 129,449 | 445,299 | 1,617,549 |

Average credits per facility with every room investigated and every locked room opened. They are computed from the game's room and lock rules (the room pools, the corridor count, the 35% lock roll and the cache chances above), averaged over 4,000 generated facilities per cell, so expect a few percent of sampling noise:

| Facility | L10 | L30 | L60 | L100 |
|---|---|---|---|---|
| Derelict Colony Ship | 57,809 | 168,465 | 805,239 | 6,103,393 |
| Research Station | 52,166 | 158,327 | 764,614 | 5,932,254 |
| Industrial Facility | 49,215 | 144,871 | 670,200 | 5,138,906 |
| Military Outpost | 48,702 | 136,648 | 624,616 | 4,632,276 |
| Ancient Ruin | 45,555 | 131,178 | 606,840 | 4,687,448 |
| Black Market Hub | 34,825 | 109,971 | 538,049 | 4,258,331 |
| Pirate Base | 40,063 | 114,898 | 533,330 | 4,002,743 |
| Salvage Yard | 33,108 | 98,026 | 479,739 | 3,798,003 |
| Relay Station | 32,597 | 97,278 | 453,769 | 3,365,328 |
| Mining Outpost | 25,087 | 80,614 | 432,260 | 3,576,704 |

Derelict Colony Ships and Ancient Ruins are always abandoned, so they have no defenders ([Facilities](boarding.md#facilities)). A Dungeon Assault mission sends you to a Pirate Base, so it pays both the mission reward and that base's caches: at level 30 about 33,000 for the mission and 115,000 in caches.

Facilities appear at locations of level 5 and up. An ordinary location has a 25% chance of one (8% of those have two), mining and salvage locations have their own 25% rolls, and each sector gets one or two dedicated facility locations. Credits are paid when a won run completes and take no cargo space. If you retreat, each cache has a 50% chance to be kept. A facility remembers what you have already found: rooms that fired their loot stay emptied when you return, and unopened ones keep theirs, as long as the facility is still above 10% integrity.

Boarding a ship works differently. Its Cargo Hold and Armory each have only the 8% special-loot cache, with no locked-room or living-quarters caches. A captured ship can be sold at a Dry Dock for its full hull value (see [Ships and Crew](#ships-and-crew)).

The same special-loot roll also gives, per room (locked rooms x6): Signal Fragments 10% from level 38, a Standard Boss Core 0.5%, a Key for opening Locked Containers 3%, officer upgrades 1% from level 30 and 55, and a ship blueprint piece 1.5%. It also rolls a Locked Container at 5%, which gives nothing (see [Locked Containers](#locked-containers)).

In the code: [Facility Operations and Boarding](#code-facility-operations-and-boarding).

## Signal Traps and Pocket Bosses

A Signal Trap summons a boss (the code calls them pocket bosses) and pays the largest single credit drop in the game. You craft the trap at a Forge from one Boss Signal recipe, then use it anywhere. It places a Trap Site in the current system for 30 minutes. When you arrive, a 15-second countdown starts and the boss warps in on you.

The boss's level is your effective level + 5 for a trap made with a Standard Boss Core. A trap made with an Infinite Boss Core (the "(Inf)" recipes) spawns it at your effective level + 30 + one more level for each such kill of that boss. The tooltip says "player level", but the code uses effective level. Only a kill you make pays loot, and the boss drops this instead of the normal kill table:

| Drop | Amount (B = boss level) |
|---|---|
| Credits | Credit factor 300-499 at level B |
| Strange Dust | B to 2B - 1 |
| Hull Upgrade Kits | B/10 to B/5 - 1, with a floor of 2 to 4 |
| Vanguard Marks | 2B to 4B - 1 |
| Standard Boss Core | 1 |
| Signal Fragment | 1 random |
| Resonant booster | 1 Exotic |
| One of the boss's unique items | 1, High Grade 80% or Exotic 20% (Exotic 80% or Legendary 20% from an Infinite trap) |
| "Luminate Trap I" blueprint | 50% while you don't own it |

| Your effective level | Trap | Boss level | Credits (average) | Range |
|---|---|---|---|---|
| 10 | Standard | 15 | 83,955 | 63,045-104,866 |
| 30 | Standard | 35 | 225,991 | 169,706-282,277 |
| 60 | Standard | 65 | 998,059 | 749,481-1,246,636 |
| 100 | Standard | 105 | 6,485,404 | 4,870,141-8,100,667 |
| 60 | Infinite | 90 | 3,441,198 | 2,584,128-4,298,267 |
| 100 | Infinite | 130 | 12,970,808 | 9,740,281-16,201,335 |

The other drops are worth little next to the credits. At level 100 they sell for about 312,000 in total, and the Standard Boss Core and Signal Fragment can't be sold.

Unique items, by boss: Rip drops a Corropult with Armor Breaker or Split Payload, or a Medium Engine with Space Horse. Phrog drops a Cryo Lance with Volatile Plasma, a Microcell Missile Turret with Hugged By Flames, or a Large Shield with Shield X. Heavy Bob drops a Rad Cannon with Radiation Burn, a Cannon with Opening Blow, or an armor module with Hull Bonus. Hydron drops a Large Drone Bay with Massive Drone Bay or Drone Durability, or a Hangar Bay with Hangar Rebuild Speed. The other five bosses (Tear, Blackmustache and the three without a name) drop no unique item. Each boss ship has a boarding chance of 0.005 times the normal one.

In the code: [Signal Traps and Pocket Bosses](#code-signal-traps-and-pocket-bosses).

### Trap Recipes

Every recipe takes one core and four different Signal Fragments, with no credit cost, and crafts in 30 seconds:

| Recipe | Boss | Fragments |
|---|---|---|
| Corsair Trap I | Rip | Alpha, Beta, Gamma, Delta |
| Corsair Trap II | Tear | Alpha, Beta, Epsilon, Zeta |
| Corsair Trap III | Blackmustache | Gamma, Delta, Epsilon, Zeta |
| Meridia Trap I | Phrog | Alpha, Gamma, Delta, Epsilon |
| Meridia Trap II | Heavy Bob | Beta, Gamma, Delta, Zeta |
| Meridia Trap III | (no English name) | Alpha, Beta, Delta, Zeta |
| Luminate Trap I | Hydron | Alpha, Beta, Gamma, Zeta |
| Blue Trap I | (no English name) | Beta, Delta, Epsilon, Zeta |
| Red Trap I | (no English name) | Alpha, Gamma, Epsilon, Zeta |

Each has an "(Inf)" twin with the same fragments and an Infinite Boss Core. No recipe is unlocked from the start. The side mission "Signal in the Dark" opens at captain level 40 and is offered by a Canisec commander you meet in the [Conquest](../conquest/README.md) stage. It unlocks Corsair Trap I, Meridia Trap I and Meridia Trap II, and gives one Standard Boss Core and the Alpha, Beta, Gamma and Delta fragments, which is exactly one Corsair Trap I. Hydron's kill drops Luminate Trap I, but Hydron only appears through a Luminate Trap, so that drop can't start the chain. Nothing else grants the other blueprints or the "(Inf)" recipes, and nothing creates the Infinite Boss Core. The three bosses shown without a name have blank name strings in the game's English text, so the game shows them with no name.

In the code: [Trap Recipes](#code-trap-recipes).

### Fragments and Cores

Each boss kill returns one Standard Boss Core and one random fragment, so after the first trap you need about three more fragments per trap, in the right types:

| Source | Signal Fragment | Standard Boss Core |
|---|---|---|
| Your kill of a hostile ship of level 38+ | 3% | 2% |
| A facility or boarded-ship room, location level 38+ for fragments | 10% (60% in a locked room) | 0.5% (3% locked) |
| A Locked Container of level 38+ | 5% | - |
| A boss kill | 1 | 1 |
| Signal in the Dark | Alpha, Beta, Gamma, Delta | 1 |

At level 60 an average facility holds 1.4 to 2.4 fragments when every room is opened (simulated as above), so one or two facility runs supply a trap.

In the code: [Fragments and Cores](#code-fragments-and-cores).

## Locked Containers

A Locked Container opens only inside a Steel Vultures station ("You need to be in a Steel Vultures space station to open this."). Opening costs a credit factor of 50 at the box's level, or one Key for opening Locked Containers, which is used first if you have one. A box whose location was above your effective level is capped to a lower level, and its price with it.

Every box holds a credits item worth the larger of two amounts: 30% of the opening price, and, in about 62% of boxes, a roll of credit factor 10-59 (76%), 60-149 (19%) or 150-249 (5%). On average that returns 81% of the price at every level:

| Box level | Opening price | Average credits inside |
|---|---|---|
| 10 | 8,203 | 6,692 |
| 30 | 22,082 | 17,892 |
| 60 | 97,521 | 79,020 |
| 100 | 706,617 | 572,560 |

Paying with credits loses about a fifth of the price in credits. The rest of the box (a 50% equipment piece, 15% blueprint, refined products, warp fuel, Strange Dust, 1% Bonus Skillpoint and so on) has to make up the difference. Opening with a key is pure gain.

| Source of boxes and keys | Chance |
|---|---|
| LB-RTR Bot (Salvaging skill, 21 points in the tree) | 1 box per wreck |
| Your kills of NPC ships | 2% box, 2% key |
| A combat station's Core | 80% for 1-2 keys, plus a box in conquest systems |
| A cargo container location | 5% key |
| Salvaging a wreck | 1% per salvage hit while the wreck still has salvage, 5% once it is empty, at most one box per wreck |
| "Smugglers Stash" (Mining skill, 2 points) | 1% per point for each core extraction |
| A facility room | 3% key (18% locked) |
| Inside a box | 1% key, 0.1% box |

The salvage and mining chances drop to a third while Autopilot is on. All of these except the skill bot, facility keys and box contents need a game that has left the prologue (the code's Sandbox storyteller, which every game runs after the prologue or when you skip it). A facility room also rolls a Locked Container at 5% (30% in a locked room), but that entry names an item that doesn't exist, so it gives nothing ([Locked Containers](#code-locked-containers)).

In the code: [Locked Containers](#code-locked-containers).

## Kill and Station Drops

A destroyed NPC ship drops credits 30% of the time, at a credit factor of 2.5 to 3.25 ([What a Destroyed NPC Ship Drops](scaling.md#what-a-destroyed-npc-ship-drops)). That averages 0.86 factor points per kill, so one level-60 Bounty Hunt mission is worth about 47 kills. A combat station's Core always drops 13.5 to 15.25 and a Cargo Dock 70% of the time 4.5 to 5.25 ([What Drops When a Part Dies](stations.md#what-drops-when-a-part-dies)). Credit items are used the moment your tractor beam picks them up and never take cargo space. While Autopilot is on, half the credit drops from kills are skipped.

The "SF-CRK Bot" (Salvaging skill, 10 points in the tree, excludes "Extraction Focus") cracks one credit container per salvage wreck for a credit factor of 7.5 to 15 at the system's level. Its cooldown is 20 seconds (`cooldown` on the `SalvageCreditDrone` ability), and the bot works for up to 100 seconds, travelling to the wreck and then cracking the container after 4 seconds.

In the code: [Kill and Station Drops](#code-kill-and-station-drops).

## Selling

### Sell Prices

Every shop pays the same price for an item, whatever the station, faction or your reputation:

```
sell price = item value x category multiplier x item sell multiplier x skill bonuses
```

The result is rounded and capped at the item's value minus 1. Reputation only lowers buy prices (2% at Cordial to 10% at Exalted).

| Category | Multiplier |
|---|---|
| Data | 1.0 |
| Ore, Refined Product, Salvage, Ammo, Junk, Crystal | 0.8 |
| Trade Goods | 0.5 |
| Signal Fragment | 0.4 (but fragments can't be sold) |
| Blueprint | 0.2 |
| Turret, Module, Booster, Drone, Torpedo, Jump Gate Pass, Consumables, Defensive Turret | 0.12 |
| Boss Signal | 0.1 |
| Currency (Vanguard Marks) | 0.05 |

Two Economy skills raise every sale: "Second Hand Salesman" +1% per point (5 points) and "Expert Negotiator" +0.5% per point (5 points, needs 11 in the tree). "High Demand" (Industrial, 2 points) adds +5% per point on crafted items. Together that is +7.6%, or +18.4% on crafted items, and the cap still applies. Expert Negotiator also lowers buy prices, refinery and forge fees, and Trade Terminal prices in both directions.

Equipment sells for only 12% of its value. A Small Standard turret sells for about 2,976 credits at level 10, 8,020 at 30, 35,381 at 60 and 256,411 at 100. Exotic pieces are worth 3.4 times as much and Legendary 4.5 times, and Medium and Large pieces 1.8 and 3.4 times. Turrets, modules, boosters and consumables you sell can be bought back at the price you got.

In the code: [Sell Prices](#code-sell-prices).

### Ore and Refining

Ore loses 1% of its sell price for each level above 10, down to 60% at level 50 and above. Refined canisters have a flat value that doesn't change with level (Carbon 60, Oxide 140, Titanium 160, Silicon 180, Tungsten 240, Iridium 340, Platinum 520, Astatine 620) and no level penalty. Refining an ore gives canisters worth exactly the ore's value, and higher-level ore simply gives more of them. How much ore you mine per minute is set by your own ship, not by a game table: mining turrets deal damage scaled by the ship's Mining Power, and your Yield stat decides how much of an asteroid's ore you keep.

A refinery charges a fee of 10% of the ore's value per unit. So refined ore sells for 0.7 of the ore's value after the fee, against 0.8 x the level penalty for raw ore. Refining pays better from level 23:

| Common ore | Value | Sold raw | Refined at a station refinery, after the fee | Refining time per unit |
|---|---|---|---|---|
| Onexel (level 10) | 247 | 198 | 173 | 47 s |
| Orminite (level 30) | 663 | 424 | 463 | 82 s |
| Lithar (level 60) | 2,926 | 1,404 | 2,048 | 116 s |
| Lunorite (level 100) | 21,199 | 10,176 | 14,839 | 150 s |

Rare ores are worth 2.2 times as much and take 2.2 times as long, so a refinery job slot earns the same per hour for either. You start with 2 slots, up to 7 with Industrial skills, and "Faster Production I" and "II" shorten the time. "Refining Mastery" gives each unit a 10% chance to double its output. Your own Refinery stations (Beyond the Front) change the fee and yield ([Player Stations](../expansion/your-empire.md#player-stations)).

In the code: [Ore and Refining](#code-ore-and-refining).

### Crafting for Sale

A crafted item's value is its materials' value times the recipe's value multiplier, usually 1.2, and the forge fee is 10% of the material value. Selling an intermediate (Titanium Plate, Circuit Board and so on) therefore earns about 7% more than selling its canisters. The exceptions are Graphite Wafers (multiplier 1.6, +47%) and Computing Unit, Bulkhead and Hull Beam (+32 to 37%), whose own sell multipliers are above 1.

Crafted equipment sells at 12% of its value, like any equipment, and loses money at a shop at every level. A level-60 Small Mining Laser sells for 35,381 against about 113,000 in materials and fee. An Equipment Order patron pays 3 times the sell value per piece, which turns the same Small turret into a small profit (about 1.05 times the materials and fee).

In the code: [Crafting for Sale](#code-crafting-for-sale).

### Trade Goods and the Trade Terminal

Trade goods have flat values that don't rise with level (Synthblood Pack 303 up to Blank Android Shell 1,490). A shop pays half that. A Trade Terminal, at Intertrade Network stations, pays the market price, which runs from 0.55 to 1.84 times the value. The price depends on that station's stock against its demand, a random drift, and a 15% lower price on the 2 to 4 goods each station has a surplus of. Prices update every 1,200 seconds. Buying low at one terminal and selling high at another is the whole profit, and since the values are flat it shrinks next to other sources as you level. Your shop discount (reputation and Expert Negotiator) lowers the price on both sides, so it makes trading margins smaller. The game has no income rate for trading: the margin per unit is set by the two stations' stock, and the trips per hour by your cargo space and travel time.

"Preferential Supplier" (Economy, needs 20 points in the tree) adds a deal at the terminal for a refined product you can craft, at 1.5 to 2.5 times its sell value, worth a credit factor of 175-374 in total. That is about 1.07 million credits at level 60 if you can craft enough of the item.

In the code: [Trade Goods and the Trade Terminal](#code-trade-goods-and-the-trade-terminal).

### Ships and Crew

A ship's hull value is 65,000 x the ship's cost multiplier and doesn't rise with level. Selling one of your own ships at a Hangar pays 40% of the hull plus its equipment's sell value. A captured ship sold at the Dry Dock pays 100% of the hull, plus 40% of its equipment's sell value (times the cube root of the cost multiplier), minus its hull damage (at most 80% of the hull). A captured hull with a cost multiplier of 1 sells for 65,000 undamaged. Crew sell for 25% of their price. Officers can't be sold.

In the code: [Ships and Crew](#code-ships-and-crew).

## Idle Officer Income

With the Leadership skill "Off-Duty Contracts" (1 point, no requirement), every officer not aboard the ship you are flying earns credits each hour:

```
hourly income = officer level x 40 x rarity factor (Standard 1, Enhanced 2, High Grade 5, Exotic 10, Legendary 20)
```

| Officer | Level 10 | Level 30 | Level 60 | Hire price at level 60 |
|---|---|---|---|---|
| Standard | 400 | 1,200 | 2,400 | 624,135 |
| High Grade | 2,000 | 6,000 | 12,000 | 2,184,472 |
| Exotic | 4,000 | 12,000 | 24,000 | 3,744,810 |

The hour is game time, so it stops while popups pause the game, and the timer restarts at zero on every load. Docking doesn't stop it. Nothing accrues while the game is closed. Officers only level up while aboard a ship, and Personnel Centers only offer officers of 2/5 to 3/5 of the station's level, never Legendary. A level-60 Exotic officer needs 156 hours of play to repay its hire price. "Premium Contracts" says it adds 50% to this income, but the game data gives it a value of 0, so it adds nothing.

In the code: [Idle Officer Income](#code-idle-officer-income).

## What Pays Best at Your Level

The code has no mission durations or travel times, so this ranks sources by what one event pays and what it takes, not by credits per hour.

| Level | Best source | Also worth doing | Avoid |
|---|---|---|---|
| 5-9 | Facility runs. A level-10 Derelict Colony Ship averages 58,000 credits with every room opened, about ten missions' worth | Combat and salvage board missions at Normal or Hard (x1.2 to x1.4) | Courier and Mine Ore (x0.6 and x0.75) |
| 10-15 | Facility runs, then Ship Capture missions (x2.5 to x4) | Bounty and Patrol boards at level-10+ Orsanon and Canisec stations, Escalation tier 3 (+30%) | Opening Locked Containers with credits |
| 16-37 | Dungeon Assault missions, which pay the mission (x1.8 to x4.5) and the Pirate Base's caches | Ore Trader patrons, Escalation tier 4 from level 25 (+50%) | Selling raw ore from level 23 |
| 38-39 | Facility runs, now also for Signal Fragments | Collect fragments and cores toward a first trap | Using ECHO for credits |
| 40+ | Signal Trap bosses: about 1 million credits a kill at level 60 | Facility runs to restock fragments, Dungeon Assault, Escalation 5-8 from levels 50, 75 and 100 | Crafting equipment to sell at a shop |

Some reasons behind the table:

- Facility caches pay from level 5 and dwarf missions at every level. Open the locked rooms: they hold about half the money. Abandoned Derelict Colony Ships and Ancient Ruins have no defenders.
- Escalation only raises mission credits, and only on missions with an enemy faction. It doesn't touch facility caches, boss credits, kill drops or selling.
- Go to higher-level stations and locations when you can survive them. Fourteen levels up doubles almost every payout, and a facility can be up to 9 levels above your effective level.
- Reputation with a faction adds up to 40% to its missions and up to 3 board slots.
- A Signal Trap boss always spawns relative to your level, so it pays the same wherever you use the trap.
- Refining turns 0.48 into 0.7 of an ore's value from level 50, and an Ore Trader pays 3.

In the code: [What Pays Best at Your Level](#code-what-pays-best-at-your-level).

## In the Game Code

Code was read from the decompiled source of version 0.8.3.5. Recipe, item, skill and facility values were read from the 0.8.3.5 data dumps (`CraftingRecipe.allRecipes.txt`, `InventoryItemType.allItems.txt`, `Skilltree.allTrees.txt`, `DungeonDefinition.allDefinitions.txt`, `ItemBuilder.allBuilders.txt`, `EquipmentBuilder.allBuilders.txt`, `CrewType.allCrew.txt`), and English names from the translation strings in `resources.assets`. The sections below give the methods and raw expressions behind each claim, in the same order as the page. `RandomRange(int a, int b)` returns a to b - 1, and the float version is continuous.

### Code: How Credit Amounts Scale

```
GameMath.GetCreditsValue(f, L) = round(f * 100 * CostMultiplier(L))
GameMath.CostMultiplier(L)     = 2^PiecewiseDoublings(L, 100, 14, 25)
```

`PiecewiseDoublings` counts one doubling per 14 levels up to 100 and one per 25 after. Board missions use `GetRewardValue(poi.level)` on the offering station. Facility loot uses `dungeonSimulation.dungeonLevel = location.level` (`DungeonOperation`), boarded ships `shipLootLevel = location.level`. Boss level comes from `PocketBossManager.GetBossSpawnLevel`, which reads `GamePlayer.effectiveLevel` (`commander.level + commander.vanguardLevel`). Side and skill-tree missions call `GetCreditsValue(f, GamePlayer.current.level)`.

Source: `GameMath.GetCreditsValue`, `GameMath.CostMultiplier`, `GameMath.PiecewiseDoublings`, `MissionGenerator.GetRewardValue`, `DungeonOperation`, `DungeonSimulation`, `PocketBossManager.GetBossSpawnLevel`, `GamePlayer.effectiveLevel`

### Code: Every Source at a Glance

The averages use the means of the random ranges: 399.5 for `RandomRange(300, 500)`, 114.5 for a locked room (59.5 + 0.48 x 114.5), 1.025 for `RandomRange(0.8f, 1.25f)`, 14.375 for the Core, 11.25 for the SF-CRK Bot and 0.3 x 2.875 for a ship kill. Mission rows multiply `GetRewardValue(L)` by the type factor averaged over the difficulty mix at that level and by the no-Forge payout share from [What a Mission Pays Out](#code-what-a-mission-pays-out). The facility row is the range of the computed facility table. The Locked Container row is the average box credits minus `GetCreditsValue(50, L)`.

### Code: Missions

Board missions are built by the subclasses of `MissionGenerator` in `Source.MissionSystem.Generator`, and their rewards by `MissionGenerator.AddRewards`.

#### Code: Board Missions by Type

| Generator | Reward value | Gate in `GetMissionChance` | `canBeIdled` | Sets `enemyFaction` |
|---|---|---|---|---|
| `CaptureShip` | `GetRewardValue(L) * 2.5`, then `*= 1.2 / 1.4 / 1.6` in `AddDefenders` | level >= 10, `tutorial_9` | Yes (Boarding mode) | Yes |
| `ClearDungeon` | `GetRewardValue(L) * ScaleReward(d)`: 1.8, 2.2, 2.8, 3.5, 4.5 | level >= 16, `tutorial_9` | Yes (Installations mode) | Yes |
| `BountyHunt`, `EscortShip`, `StationBattle`, `HelpMiner` | `GetRewardValue(L)`, then x1.2 / 1.4 / 1.6 | Escort and StationBattle level > 5; BountyHunt `tutorial_9` | Not EscortShip or StationBattle | Yes |
| `OreSamples`, `SalvageSamples`, `SalvageWreck` | same | SalvageWreck `tutorial_7` | Yes | Above Easy only |
| `ClearAsteroidField`, `ClearSalvageField` | `GetRewardValue(L)` | `tutorial_9` | Yes | Yes |
| `DeliverCraftedGoods` | `GetRewardValue(L)` | `source.forge != null` | No | No |
| `TradeTerminal` | `GetRewardValue(L)`, plus `credits.amount += num3 * localEconomyItem.cost` after `AddRewards` | needs an Intertrade market in the sector and none in the system | No | No |
| `MineOre` | `GetRewardValue(L) * 0.75` | none | Yes | No |
| `TradeMaterials` | `GetRewardValue(L) * 0.5 * difficulty.GetRewardMultiplier()` | none | No | No |
| `Courier` | `GetRewardValue(L) * 0.6` | level > 2 | Yes | No |

`GetRewardValue(level)` is `GetCreditsValue(40, level)`. The tutorial gates are `IsMissionCompleted("tutorial_9") || IsInSandBox()` (BountyHunt and the clear-field missions also pass for one starter specialization). `MissionGenerator.GenerateRandomMission` gives every generator in `poi.faction.missionTypes` a weight of 1 or 0 and picks uniformly. The faction lists are in `Source.Galaxy.Factions` (`Blue`, `Gold`, `Red`, `BountyGuild`, `MercenaryGuild`, `PoliceGuild`, `MiningGuild`, `SalvageGuild`, `TradingGuild` and so on). `SandboxWorld` removes the Mission Board from Industrial Guild stations. The `EquipmentOrder` and `SalvageTrader` generators appear in no faction's list, so boards never offer them.

Source: `MissionGenerator.GetRewardValue`, `MissionGenerator.GenerateRandomMission`, the generator classes in `Source.MissionSystem.Generator`, `SandboxWorld.GetStationFacilities`

#### Code: How Difficulty Is Rolled

`MissionDifficultyExtension.GetMissionDifficulty` uses `t = clamp(L/100, 0, 1)` and the weights Easy `lerp(normal ? 0.5 : 1, 0.1, t)`, Normal `0.3` (level >= 3), Hard `lerp(0.2, 0.3, t)` (level >= 5), Skull `0.2` (level >= 20) and Insane `lerp(0.1, 0.2, t)` (level >= 50), normalised by `SeededRandom.Choose`. `GetRewardMultiplier` is 1, 1.8, 2.8, 3.6 and 6, and `AddRewards` only uses it for XP.

Source: `MissionDifficultyExtension.GetMissionDifficulty`, `MissionDifficultyExtension.GetRewardMultiplier`, `MissionGenerator.AddRewards`

#### Code: What a Mission Pays Out

```
num  = PromptEngineeringBasicMissionRunner.isActive ? 1 : 0.5
num3 = round(V * RandomRange(0.8, 1.25) * (idle ? 0.33 * num : 1))
num4 = idle ? 0.05 * num : 0.3                      // item chance
if RandomBool(num4): item; if !idle && rarity <= Enhanced: ExtraCreditReward
else if SpaceStation.current?.forge != null && RandomBool(idle ? 0.05 * num : 0.2): blueprint (+ ExtraCreditReward)
else if num3 > 0: CreditReward(num3); 25% (idle 0.1 * num) Plasma Cell
ExtraCreditReward = V * RandomRange(0.8, 1.25) * (rarity == Standard ? 0.5 : 0.25)
```

Rarity by difficulty: Easy 50% Enhanced, Normal Enhanced, Hard 50% High Grade, Skull and Insane High Grade or Exotic. Hard and above add `HullUpgradeKit` at 5%. If `(mission.sourceFaction ?? sourcePoi.faction).IsEnemy(Faction.player)`, `mission.rewards.Clear()` runs and reputation is multiplied by 4. The payout share in the table is `p(credit) * 1.025 + p(item) * E[extra]`, with `p(credit)` 0.7 without a Forge and 0.56 with one.

Source: `MissionGenerator.AddRewards`, `MissionGenerator.CreditReward`, `MissionGenerator.ExtraCreditReward`

#### Code: Reputation and Escalation Bonuses

`CreditReward` computes `amount * (1 + faction.GetPlayerPerkLevel().GetMissionRewardMultiplier() + conquest rank bonus)` and stores it as `baseAmount` when `applyEscalationBonus` is set, then `amount = EscalationHelper.ApplyRewardBonus(baseAmount)`. `applyEscalationBonus` is `mission.enemyFaction != null`, and `mission.escalationLevel` is set to `EscalationHelper.CurrentTier` at generation. `RecomputeMissionRewards` re-prices every reward with `baseAmount > 0` when the tier changes. `ReputationLevelExtensions.GetMissionRewardMultiplier` is 0.05, 0.1, 0.2, 0.3 and 0.4, and `GetBonusMissionAmount` is 1, 2, 2 and 3 from Friendly up.

Source: `MissionGenerator.CreditReward`, `EscalationHelper.ApplyRewardBonus`, `EscalationHelper.RecomputeMissionRewards`, `ReputationLevelExtensions.GetMissionRewardMultiplier`, `ReputationLevelExtensions.GetBonusMissionAmount`

#### Code: The Mission Board

`MissionBoard.MissionCount = 6` plus `GetBonusMissionAmount()`. `ProgressTimer(delta)` adds `delta` to the timer on every `SpaceStation.UpdateJobs`, which runs from `ActiveUpdate` and, for stations other than the current one, `AmbientUpdate`. It only regenerates when `timer >= 300f && SpaceStation.current == owner`. `Behaviour.UI.MissionBoard` calls `ProgressTimer(-30)` when a mission is clicked with under 20 s left. `GamePlayer.IsMissionsLimitExceeded` is `missions.Count >= 20`. `ReputationLevelExtensions.GetMissionBoardRefreshTimer` and `CanRefreshBoard` have no callers.

Source: `MissionBoard.RegenerateMissions`, `MissionBoard.ProgressTimer`, `GamePlayer.IsMissionsLimitExceeded`

#### Code: Missions ECHO Runs for You

`IdleManager.TryFindNewMission` calls `missionBoard.GenerateIdleMission` and sets `mission.idle = true`. `GenerateRandomMission` fixes idle difficulty to Easy, or Normal with `PromptEngineeringEnhancedMissionRunner`. The idle multipliers are in `AddRewards` above; reputation is `x 0.1 * num * num2`, with `num2 = 2` for `promptRepBonus`, and `promptItemRewardBonus` multiplies the item chance by `1 + currentPoints`. Skill names and requirements are from `Skilltree.allTrees.txt` (`PromptEngineering` nodes 0, 6, 8 and 11). Kill loot skips each non-important entry while `GamePlayer.autoPlay` is on: `item.important || !autoPlay || !random.RandomBool()` in `LootManager.DropRegularLoot`.

Source: `IdleManager.TryFindNewMission`, `MissionGenerator.GenerateRandomMission`, `MissionGenerator.AddRewards`, `LootManager.DropRegularLoot`

#### Code: Bounty, Patrol and Industrial Boards

Bounty credits are `EscalationHelper.ApplyRewardBonus(GetCreditsValue(25, missionLevel))`, patrol credits `ApplyRewardBonus(GetCreditsValue(15, missionLevel))` and industry credits `ApplyRewardBonus(GetCreditsValue(25, poi.level))`. The level is `CareerLevels.BoardLevel(stationLevel, index, rank) = min(stationLevel, 100) + RankBonus`, with `RankBonus` 0, `3 + ceil(rank / 2)` and `5 + rank`. Each board seeds from the station guid, `DateTime.Now.DayOfYear` and a counter that rises when you launch a mission. `SandboxWorld` adds the Bounty Board to Bounty Guild stations and the Patrol Roster to Police Guild stations of level 10 or more, and the industry board to Industrial Guild stations. Bounty Marks are `round(RandomRange(7, 8) * CostMultiplier(level) * {0.7, 1, 1.3})`.

Source: `BountyBoard.GenerateBountyMissions`, `BountyBoard.AddBountyRewards`, `PatrolMission.SetupPatrolWave`, `PatrolMission.StartNextWave`, `IndustryMission.Create`, `CareerLevels.BoardLevel`, `CareerLevels.RankBonus`

#### Code: Bar Patron Orders

`OreTrader.InitializeData`: `requiredAmount = RandomRange(5, 10) + RoundToInt(level * 0.55)`, `creditReward = requiredAmount * _oreType.cost * 3`, with the ore `RandomBool(0.7) ? commonOre : rareOre` from `systemOreData.surfaceOres`. `SalvageTrader`: `RandomRange(8, 16) + RoundToInt(level * 0.65)` units, `cost * requiredAmount * 2`. The patron `EquipmentOrder` pays `GetSellValue(applySkillBonuses: false) * 3 * requiredAmount`, with 2 or 3 items. `Bar` rolls five seats per `DateTime.Now.DayOfYear`: 15% each Ore Trader, Salvage Trader and Equipment Order, 55% Salesman. `cost` is the discounted price, so a shop discount lowers the reward slightly. Lithar's value of 2,926 is `OreCommon60` `calcCost`.

Source: `OreTrader.InitializeData`, `SalvageTrader.InitializeData`, `Patrons.EquipmentOrder`, `Bar`

#### Code: One-Time Story Rewards

`SideMissions` uses `GetCreditsValue(100, level)` for `SideMissionPatrol`, `SideMissionBounty` and `SideMissionMelloy` and 50 for `SideMissionFastLane`. `SkilltreeMissions` uses 200 for each `...2` assignment (Mining, Combat, Salvage, Industrial, Drones, Defense, Economy).

Source: `SideMissions`, `SkilltreeMissions`

### Code: Facility Operations and Boarding

```
InjectLockedCompartmentBonusLoot:  GetCreditsValue(RandomRange(40, 80), dungeonLevel), triggerAt 0.5
InjectDungeonLivingLoot:           RandomBool(0.3) -> GetCreditsValue(RandomRange(30, 120), dungeonLevel), triggerAt 0.7
TryAddSpecialLoot:                 RandomBool(0.08 * chanceMultiplier) -> GetCreditsValue(RandomRange(80, 150), dungeonLevel), triggerAt 0.5
chanceMultiplier = compartment.isLocked ? 6 : 1
```

`DungeonCompartmentLayout` locks a room of a lockable type with `RandomBool(0.35f)`. `DungeonSimulation.FireReadyEvents` fires a discovery when investigation progress crosses `triggerAtProgress`, and the credit discoveries never set `requiresProfession`. `DungeonSimulation.CheckExplorationWinCondition` ignores locked rooms. On victory `DungeonOperation.AddLootEntryToCargo` calls `CreditsItem.OnUse` directly. On extraction `TransferPartialLootToCargo` keeps non-data items with `RandomBool()`. Ship boarding runs `TryAddSpecialLoot` with a multiplier of 1 in `InjectShipCargoHold` and `InjectShipArmory`. Room counts are `RandomRange(min + n/2, max + n + 1)` with `n = min(dungeonLevel / 12, 8)` and `min`/`max` from `DungeonDefinition.allDefinitions.txt`. Facility spawning is `MapPointOfInterest.GenerateDungeonLocations` (`level >= 5 && RandomBool(0.25f)`, `RandomBool(0.08f) ? 2 : 1`), `TrySpawnMiningDungeon`, `Salvage.TrySpawnSalvageDungeon` and `SandboxWorld.AddDungeonPOIs`. The other special-loot chances are `TryAddSignalFragmentDrop` (`dungeonLevel >= 38 && RandomBool(0.1 * m)`), `TryAddStandardCoreDrop` (0.005), `LockedContainerKey` (0.03), `TryAddOfficerUpgradeDiscovery` (0.01 at 30 and 55) and `TryAddShipBlueprintPieceDrop` (0.015).

The per-facility table is computed from the code's room rules: the room pools and caps (`DungeonCompartmentDistributions`, `DungeonDefinition.allDefinitions.txt`), the corridor count, the 35% lock roll and the cache chances above, averaged over 4,000 generated facilities per cell with the expected cache value per room. `DungeonOperation.ApplyDefinitionToSim` builds the rooms from `DungeonData.layoutSeed`, and `DungeonData.simulation` is saved with the location. `PrepareSimulationForEntry` keeps that simulation on re-entry (`PrepareForReentry` clears only the run state), so a room event that has not fired, or fired but needed a specialist who was absent, can still be collected; `DungeonData.IsFullyDepleted` checks exactly that. `DungeonData.isEnterable` stays true while `facilityIntegrity` is above 0.1 and the dock is not destroyed. Boarded ships differ: `DungeonPanel.Close` clears the simulation after an extracted or lost run.

Source: `DungeonCompartmentLoot.InjectDungeonLoot`, `DungeonCompartmentLoot.InjectLockedCompartmentBonusLoot`, `DungeonCompartmentLoot.InjectDungeonLivingLoot`, `DungeonCompartmentLoot.TryAddSpecialLoot`, `DungeonCompartmentLayout.Build`, `DungeonSimulation.FireReadyEvents`, `DungeonOperation.AddLootEntryToCargo`, `DungeonOperation.TransferPartialLootToCargo`, `DungeonOperation.PrepareSimulationForEntry`, `DungeonData.IsFullyDepleted`

### Code: Signal Traps and Pocket Bosses

`SignalTrapItem.OnUse` calls `GetBossSpawnLevel(bossId, levelOffset)` and `SystemMapData.AddLureSite` (`timeLeft = 1800`). `SignalTrapBuilder.RarityToLevelOffset` returns 30 for Legendary recipes and 5 otherwise. `GetBossSpawnLevel` returns `effectiveLevel + 30 + bossSpawnLevels[bossId]` for an offset of 30 or more, else `effectiveLevel + levelOffset`. `UpdateInfiniteCoreEscalation` raises `bossSpawnLevels[bossId]` when `bossLevel >= effectiveLevel + 30 + value`. `LureSite.bossWarpCountdown = 15`. `LootManager.DropLoot` sends a player kill of a unit with a `pocketBossId` to `DropBossLoot` instead of the regular table:

```
StrangeDust     max(1, RandomRange(B, 2B))
HullUpgradeKit  RandomRange(max(2, B/10), max(5, B/5))
Credits         GetCreditsValue(RandomRange(300, 500), B)
StandardCore    1
VanguardMark    RandomRange(2B, 4B)
```

followed by `DropBossResonantBooster`, `DropBossSignalData` (one fragment), `TryDropGoldBossBlueprint` (`!HasBlueprint("BossSignalGold1Normal") && RandomBool()`) and the boss's unique item. `PocketBossManager.IsFirstKill` has no callers. Sell values of the other drops: Strange Dust 500 x 0.8, Hull Upgrade Kit `calcCost` 73,062 x 0.12, Vanguard Mark 7,500 x 0.05. `StandardCore` and the fragments have `canSell False`.

`PocketBossManager` loads the `PocketBossDefinition` assets from `Resources/PocketBosses` (`PocketBoss1` to `PocketBoss9` in `resources.assets`). `LootManager.TryDropBossUniqueWeapon` picks one `uniqueItems` entry at random, skips it when its `builder` is null, and otherwise builds the item with `RollBossWeaponRarity` and `ReplaceAspect(item, aspect, 0)`. The entries are:

```
MaraudersBoss1 (Rip)       SmallCorropult + BossArmorbreaker, SmallCorropult + BossClusterPayload2, LargeEngine + BossThrustIncrease
FanaticsBoss1 (Phrog)      MediumPlasmaLanceBuilder + BossVolatile, SmallMicrocellMissileTurretBuilder + BossHolyWrath, LargeShield + BossShieldIncrease
FanaticsBoss2 (Heavy Bob)  LargeRadiationCannon + BossRadiationDot, LargeTripleCannon + BossFirstHit, LargeHull + BossHullIncrease
GoldBoss1 (Hydron)         LargeDroneBay + BossDroneAmountMk3, MediumHangarBayBuilder + BossHangarRebuildIncrease, LargeDroneBay + BossDroneHullMultiplier
MaraudersBoss2, MaraudersBoss3, FanaticsBoss3, RedBoss1, BlueBoss1: three entries, all with a null builder
```

All nine definitions have `boardingChanceMult` 0.005 (the field default is 0.05). `LureSite` copies it to the boss's unit data, and `SpaceShip.HandleBoardingCheck` multiplies the per-check boarding chance by it.

Source: `SignalTrapItem.OnUse`, `SignalTrapBuilder.RarityToLevelOffset`, `PocketBossManager.GetBossSpawnLevel`, `PocketBossManager.UpdateInfiniteCoreEscalation`, `PocketBossDefinition`, `LureSite`, `LootManager.DropLoot`, `LootManager.DropBossBulkLoot`, `LootManager.TryDropBossUniqueWeapon`, `SpaceShip.HandleBoardingCheck`

#### Code: Trap Recipes

The recipes are the `BossSignal*Normal` and `BossSignal*Infinite` rows of `CraftingRecipe.allRecipes.txt`: `category BossSignal`, `craftingTime 30`, `materials.Count 0`, five `itemMaterials` (`StandardCore` or `InfiniteCore` plus four fragments), `craftingRarity` Enhanced or Legendary, `unlockedFromStart False`, `blueprintAvailable False` (so `CraftingRecipe.GetBlueprintableRecipes` never offers them as random blueprints). Boss ids are on the `BaitBeacon*` builders in `ItemBuilder.allBuilders.txt`, and the names on `MaraudersBoss1Name` to `GoldBoss1Name` in the English text table; `FanaticsBoss3Name`, `BlueBoss1Name` and `RedBoss1Name` are empty strings, which `Translation.Translate` returns as they are. `SideMissions` defines "Signal in the Dark" (`SideMissionPocketBossIntro`, `player.level >= 40`, `sourceFaction = Faction.policeGuild`), which rewards the blueprints `BossSignalMarauders1Normal`, `BossSignalFanatics1Normal` and `BossSignalFanatics2Normal`, one `StandardCore` and fragments Alpha to Delta. `Characters` gives it to `CanisecConquestCommander`. `DungeonCompartmentLoot.TryAddBossBeaconBlueprint` and `DungeonOperation.NormalSignalRecipes` have no callers (the former also lists `SignalGold1Normal`, which is no recipe id). The remaining blueprint grants are `LootManager.TryDropGoldBossBlueprint` (`BossSignalGold1Normal`, from a Hydron kill) and the debug console. `SystemMapData.AddLureSite` is called only by `SignalTrapItem.OnUse` and the debug console, so Hydron appears only through that recipe. `InfiniteCore` is an item in `InventoryItemType.allItems.txt` (`canSell False`), and the only code naming it is the debug console's ingredient list. No loot table, mission or shop code creates it.

Source: `CraftingRecipe.allRecipes.txt`, `SideMissions`, `Characters`, `LootManager.TryDropGoldBossBlueprint`, `DungeonCompartmentLoot.TryAddBossBeaconBlueprint`, `SystemMapData.AddLureSite`

#### Code: Fragments and Cores

`LootManager.TryDropSignalFragment` is `unitLevel >= 38 && RandomBool(0.03f)` and `TryDropStandardCore` is `unitLevel >= 38 && RandomBool(0.02f)`, both from `DropRegularLoot` for player kills of ships. `AddLootBoxSignalFragment` is `itemLevel >= 38 && RandomBool(0.05f)`. The facility chances are in [Facility Operations and Boarding](#code-facility-operations-and-boarding).

Source: `LootManager.TryDropSignalFragment`, `LootManager.TryDropStandardCore`, `LootManager.AddLootBoxSignalFragment`

### Code: Locked Containers

`LootBoxItem.OnUse` requires `spaceStation.faction == Faction.salvageGuild` and the station interior. `TryPayForBox` consumes a `LockedContainerKey` if `CanUseKey()`, otherwise `RemoveCredits(GetCreditsValue(50, itemLevel))`. `ItemBuilder.CreateLootboxItem` sets the level through `GameMath.ApplyItemLevelCap`. The credits item:

```
val  = GetCreditsValue(50, level) * 0.3
if (loot.Count < 3 && RandomBool(0.7)):
    lo, hi = 10, 60;  5%: 150, 250;  else 20%: 60, 150
    val2 = GetCreditsValue(RandomRange(lo, hi), level)
credits = max(val, val2)
```

The 81% return was computed by simulating the `loot.Count < 3` condition over 200,000 boxes per level. Sources: `SalvageCargoLootboxBot.GainItem`; `SpaceShipData` kill loot (2% box, 2% key), `CombatStationPartData` Core loot and `MapPointOfInterest` container loot, all behind `IsInSandBox()`; `SalvageData.CheckLootBoxSpawn` (`HasSalvage ? 0.01 : 0.05`, `IsInSandBox()`); `Asteroid.TryDropCoreTreasureLootBox` (`MiningT2TreasureChance`, `customIncrease 0.01`); `GameMath.ScaleChanceForAutoPlay` (x0.33). `NewGame.SaveInputs` adds the `Sandbox` storyteller when the prologue is skipped, and `GamePlayer.TransitionTutorialToSandbox` adds it when the prologue ends. The facility entry is `DungeonCompartmentLoot.TryAddSpecialLoot` calling `AddDiscovery(..., "LootBox", ...)` (5% x 6 in a locked room), which stores only an `itemTypeId`. `ResolveItemTypeUncached` looks an entry without `builderIdentifier` up with `InventoryItemType.TryGet`, then as a ship blueprint piece, then as a recipe id. `InventoryItemType.LoadAll` keys items by the `Resources/Items` asset name, and the only such asset is `LootBoxTemplate` (the `LootBox` prefab is the `ItemBuilder`, which `LootManager.CreateLootBox` uses). The lookup returns null, and `GroupLoot`, `DungeonOperation.AddLootEntryToCargo` and `OperationLogRow` skip null items, so the entry gives and shows nothing.

Source: `LootBoxItem.OnUse`, `LootBoxItem.TryPayForBox`, `LootManager.OpenLootBox`, `LootManager.AddLootBoxCreditsItem`, `ItemBuilder.CreateLootboxItem`, `GameMath.ApplyItemLevelCap`, `DungeonCompartmentLoot.ResolveItemTypeUncached`

### Code: Kill and Station Drops

`CreditsItem.OnUse` adds `amount` to `GamePlayer.credits` with no multiplier, and `TractorBeam.TryHandleDirectUseItem` uses credit items on pickup. `SalvageCreditDrone.GainItem` drops `GetCreditsValue(RandomRange(7.5f, 15f), SystemMapData.current.level)` and increments `creditsExtracted`. `AbilityButton` only accepts a wreck with `creditsExtracted == 0`. The skill is `SalvagingCreditDrone` in `Skilltree.allTrees.txt` (`requiredPointsInTree 10`, exclusive with `SalvagePowerYieldToggle`).

Source: `CreditsItem.OnUse`, `TractorBeam.TryHandleDirectUseItem`, `SalvageCreditDrone.GainItem`, `AbilityButton`

### Code: Selling

Shop sales go through `InventoryInteractionManager.SellAmount`, which pays `item.sellValue * amount`.

#### Code: Sell Prices

```
GetSellValue = baseValue * itemCategory.GetSellValueMultiplier() * sellValueMultiplier
             * (1 + economySellValue) * (1 + economyGlobalValue)
             * (1 + industrialSellValue)   // crafted items only
ClampBelowBaseValue: clamp(round(value), 1, max(1, round(baseValue - 1)))
```

`ItemCategoryExtensions.GetSellValueMultiplier` holds the category table. `GetSellValue` has no station or faction input. The buy price is `baseValue * discount`, where the discount subtracts `GetShopDiscount()` (0.02 to 0.1), the equipment skill and `economyGlobalValue`. Skill values are `customIncrease` in `Skilltree.allTrees.txt`: `EconomySellValue` 0.01 x 5, `EconomyGlobalValue` 0.005 x 5 (11 points in the tree), `IndustrialT2CraftedValue` 0.05 x 2. Equipment value is `GetBaseValue(item) * GetCreditsValue(valueMultiplier, level) * rarity.GetCostMultiplier() * RandomRange(0.9, 1.2)`, with `GetBaseValue` 120 x 1.2 (turret) or 1.4 (module) x 1.8 (Medium) or 3.4 (Large) and rarity cost 1, 1.6, 2.2, 3.4, 4.5. Turret figures use the mean roll 1.05.

Source: `InventoryItemType.GetSellValue`, `InventoryItemType.ClampBelowBaseValue`, `ItemCategoryExtensions.GetSellValueMultiplier`, `ReputationLevelExtensions.GetShopDiscount`, `EquipmentBuilder.GetBaseValue`, `RarityExtensions.GetCostMultiplier`

#### Code: Ore and Refining

```
OreItemData.GetContentValue = ceil(300 * CostMultiplier(itemLevel) * rarity.GetCostMultiplier() * 0.5)
sellValueMultiplier         = 1 - 0.01 * clamp(itemLevel - 10, 1, 40)     (itemLevel > 10)
refinementCost              = ceil(item.cost * 0.1 * max(0, 1 - Discounted) * refinementDifficulty)
refinementTime              = sqrt(itemLevel) * rarity.GetCostMultiplier() * 7.5 * refinementDifficulty / refinedPerSecond
Refinery.refinedPerSecond   = 0.5 * (1 + FasterProduction1 + FasterProduction2 + 0.005 * masteryLevel)
```

`InitializeFor` sets each product's `dynamicYield` so the yields times `RefinedMaterialExtensions.GetValue` add up to the content value. `GetValue` is `100 x` 1.6, 1.4, 1.8, 2.4, 0.6, 3.4, 5.2 and 6.2 with no level term, and the canisters have `customYield 1`. `Refinery.GetFeePerUnit` multiplies the cost by `PlayerStation.GetJobFeeShare`, which is 1 at any station that isn't yours. `Refinery.maxJobs = 2 + industrialBasicJobs + industrialEnhancedJobs`. `RefineryJob` doubles a unit's yield with `RandomBool(0.1f)` when Refining Mastery is active. Ore values match `calcCost` in the dump (`OreCommon10` 247).

Source: `OreItemData.InitializeFor`, `OreItemData.GetContentValue`, `RefinedMaterialExtensions.GetValue`, `Refinery.GetFeePerUnit`, `Refinery.refinedPerSecond`, `RefineryJob`

#### Code: Crafting for Sale

`CraftingRecipe.UpdateDynamicCost` sets `dynamicValue = num * craftingValueMultiplier` and `dynamicCost = round(num / 10)`, where `num` is the material and ingredient value. A crafted item's value is `recipe.value / resultCount` and it gets `isCraftedItem`. `craftingValueMultiplier` is 1.2 for most recipes in `CraftingRecipe.allRecipes.txt`, 1.6 for Graphite Wafers and 0.85 for ammo. Bulkhead and Computing Unit have `sellValueMultiplier` 1.3 and Hull Beam 1.2. Equipment recipes scale their ingredients with `CostMultiplier(level)` when `levelingItem` is set.

Source: `CraftingRecipe.UpdateDynamicCost`, `InventoryItemType.CalculateDynamicCost`, `Forge.GetFeePerUnit`

#### Code: Trade Goods and the Trade Terminal

```
MarketPricing: target = (0.6 + 1 / (1 + stock / demand)) * (1 - 0.15 * supplyModifier)
               unit price = max(0.55, target * priceNoise) * item.cost      priceNoise in [0.85, 1.15]
LocalEconomyItem.demand = GetCreditsValue(350, stationLevel) / max(1, item.cost)
```

`Economy.EconomyUpdateTime = 1200`. Trade-goods values are `definition.baseCost` in `InventoryItemType.allItems.txt`. The crafted-goods deal is `GetCreditsValue(RandomRange(175, 375), level)` in total at `round(sellValue * RandomRange(1.5, 2.5))` each, behind `economyTradeCraftedDeal`.

Source: `MarketPricing`, `LocalEconomyItem`, `Economy`

#### Code: Ships and Crew

`SpaceShip.shipSellValue = 65000 * costMultiplier * 0.4` and `totalSellValue` adds each equipped item's `sellValue`. `DryDockCostCalculator.GetSellBreakdown` pays `round(65000 * costMultiplier)` plus `sum(componentSellValue * cbrt(costMultiplier)) * 0.4`, minus `min(round(shipCost * damage), round(shipCost * 0.8))`. `PersonnelCenter.GetSellPrice` is `round(crew.cost * 0.25)`. No method sells an officer.

Source: `SpaceShip.totalSellValue`, `DryDockCostCalculator.GetSellBreakdown`, `PersonalHangar`, `PersonnelCenter.GetSellPrice`

### Code: Idle Officer Income

`GamePlayer.TickIdleOfficerIncome` adds `Time.deltaTime` to `idleIncomeHourTimer` and pays every 3600 s, only while `leadershipOfficerIdleIncome.isActive`, `sum(HourlyIdleIncome * IdleIncomeBonusMultiplier)` over officers where `IsOfficerIdle` (not on `currentSpaceShip`). `OfficerData.HourlyIdleIncome = (int)(level * 40 * {1, 2, 5, 10, 20})`. `idleIncomeHourTimer` is not saved. `IdleIncomeBonusMultiplier = 1 + leadershipOfficerIdleIncomeBonus.currentIncrease`, and `currentIncrease = currentPoints * customIncrease`. The `OfficerIdleIncomeBonus` node prefab has `customIncrease` 0.0 (`Skilltree.allTrees.txt` shows the same), no code assigns `customIncrease`, and the multiplier is therefore 1. The node's text (`LeadershipOfficerIdleIncomeBonusDescription`) still says 50%. The tick runs from `GamePlayer.Update` through `GameplayManager.Update`, which only skips while `worldTransferInProgress`. The only time stop is `GameManager.Pause` (popups, dialogues, the debug console and the Steam overlay call it); docking goes through `DockingOption` in the same scene and does not call it. `OfficerData.purchaseCost = GetCreditsValue(rarityHireMult * (200 + 2 * level), level)` with 1, 1.6, 3.5, 6 and 10. `PersonnelCenter.RegenerateOfficers` offers levels from `level * 2 / 5` to `level * 3 / 5`, and `GetRandomOfficerRarity` has no Legendary weight.

Source: `GamePlayer.TickIdleOfficerIncome`, `GamePlayer.IsOfficerIdle`, `SkilltreeNode.IdleIncomeBonusMultiplier`, `GameManager.Pause`, `OfficerData.HourlyIdleIncome`, `OfficerData.purchaseCost`, `PersonnelCenter.RegenerateOfficers`

### Code: What Pays Best at Your Level

The table compares the averages above. The level-9 limit on facilities is the `ExceedsLevelGap` check in [boarding.md](boarding.md#code-facilities). The ten-missions comparison is 57,809 against 6,031 at level 10. The fragment supply uses the computed room counts times the 10% and 60% chances.

