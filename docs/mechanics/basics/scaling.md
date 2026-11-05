# Scaling

This page explains how enemy and item strength grows with level, ship rank, rarity and Escalation. It covers which levels the game uses, how NPC (non-player character) ships get their ship rank, every multiplier applied to an NPC's HP (hit points) and damage, how gear rarity and item level turn into stats, and what loot to expect. The code was read from game version 0.8.3.4 (the decompiled source), not from in-game testing, and the cited classes were re-checked against 0.8.3.5 without finding a change to these rules. The item stat ranges, spawn weights and the reactor and thrust exponents come from the equipment builders, which are asset data, so they were re-read from the 0.8.3.5 data dumps and match this page. Translation strings were not re-read for 0.8.3.5. For how the resulting stats are used in a hit, see the [combat damage page](combat-damage.md).

Terms and abbreviations are explained in the [glossary](glossary.md).

Three different things are called a rank or a tier of quality, and this page keeps them apart:

- Ship rank is the strength tier of an enemy ship: Rookie, Regular, Veteran, Elite, Champion, Commander, Legendary. It is rolled when an NPC ship is generated and shown as a coloured badge at the start of the ship's tooltip title. These seven words are the game's own on-screen names. They are written into the game's code and are not in its text table.
- Rarity is the quality tier of an item or ship hull: Standard, Enhanced, High Grade, Exotic, Legendary. Items show it by colour. A ship hull shows it as a type letter (Type-A, Type-B, Type-C, Type-X and Type-S). "Standard" and "Legendary" appear in both lists, so this page uses "Standard" only for rarity. The ship rank the game calls "Regular" is named Standard in the code.
- Conquest rank is your own title with one faction, set by Your Contribution. It is not covered here (see the [conquest stage](../conquest/README.md)). The Patrol Officer rank is a separate counter in the patrol missions and is not covered here either.

"Points" are the value a ship adds to a fleet or to a site's enemy budget. Fleet cards in the game call the same number "strength".

## Which Level Is Used for What

The game has several "levels". They are not interchangeable.

| Name | Where it comes from | What it drives |
|---|---|---|
| Captain level | Your own level, which the game calls your captain level | Escalation tier unlocks and fleet budget steps |
| Effective level | Your captain level plus your Vanguard level | The item level cap and kill XP (experience points). The conquest levels use it too, see the [war page](../conquest/war.md). |
| System level or location level | The level of the system, copied to every location in it | The level of everything generated there: enemy gear, loot and mission rewards |
| Item level | The location level, plus a roll of -1, 0 or +1 for each item | The power of one piece of equipment |
| Unit level | Calculated from the unit's equipment | HP curves, the HP balance bonus, the escalation base ramp and credit drops |

Your empire's own level ("Empire Level {n}") is separate from every level above. It unlocks with Beyond the Front ([Your empire](../expansion/your-empire.md)).

A ship does not store its unit level. The game works it out from the gear. It takes the mean of all equipped item levels (rounded to a whole number), adds the highest item level, and halves the sum, dropping any fraction. An enemy generated at location level 40 therefore has unit level 39 to 41, and every level-based modifier below uses that number.

In the code: [Which Level Is Used for What](#code-which-level-is-used-for-what).

### How Many Enemies a Hostile Site Gets

A hostile combat site spawns a points budget that depends on the site level and on the Escalation tier. The "Enemy forces" column of the Escalation table below is the multiplier of this budget.

```
points = the larger of (the site's own points value) and (tier share * personal points for the level)
```

"Personal points" is the same table that sets your personal fleet budget, counted in points. It is 0 below level 10 and rises by one row every 5 levels, ending at level 100.

| Level | 10 | 15 | 20 | 25 | 30 | 35 | 40 | 45 | 50 | 55 | 60 | 65 | 70 | 75 | 80 | 85 | 90 | 95 | 100+ |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Points | 40 | 80 | 130 | 190 | 260 | 335 | 410 | 490 | 570 | 655 | 740 | 830 | 920 | 1015 | 1110 | 1205 | 1300 | 1400 | 1500 |
| Ships | 2 | 2 | 2 | 3 | 4 | 4 | 5 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 13 | 14 | 16 | 18 | 20 |

Each ship is charged its own points times its ship rank's point multiplier. Higher ship ranks therefore use up the budget faster and produce fewer ships (see the [Rank Table](#rank-table)). The ship count cap for combat payloads also grows with the site level.

Two rules limit the tier-scaled budget. Only payloads of a faction hostile to you use it. Small escort groups keep the site's own points value when their per-ship cap is under a fifth of the site's largest unit. A site remembers the tier it was generated at for its budget and ship ranks while it stays recent. The HP and damage multipliers always use your current tier.

In the code: [How Many Enemies a Hostile Site Gets](#code-how-many-enemies-a-hostile-site-gets).

## Ship Ranks

Every NPC ship has a ship rank. It is rolled when the ship is generated, from weights that depend on the level.

### Rank Table

| Ship rank | Max HP, armor and shield | Fire rate and reload (added) | Point cost and kill XP multiplier | Base number of hull nodes |
|---|---|---|---|---|
| Rookie | x0.7 | -30% | x1.0 | 20 |
| Regular | x1.0 | -10% | x1.2 | 27 |
| Veteran | x1.3 | +10% | x1.3 | 40 |
| Elite | x1.5 | +15% | x1.5 | 50 |
| Champion | x1.9 | +20% | x1.6 | 56 |
| Commander | x2.4 | +30% | x2.0 | 60 |
| Legendary | x3.0 | +30% | x2.4 | 60 |

What each column does:

- The HP column multiplies hull HP, armor HP and shield HP, the three pools of hit points a ship has.
- The fire rate column is added to the ship's attack speed and reload speed. A turret's delay is its base delay divided by (1 + that stat), so a Rookie fires at 0.7 times the base rate and a Commander at 1.3 times. It does not change damage per shot, and the ship rank does not change combat power.
- The point column is charged against a site's unit budget. It also multiplies the XP you get for the kill.
- The hull nodes column is explained in [Ship Rarity and Hull Nodes](#ship-rarity-and-hull-nodes).

Ships of your own faction do not get ship rank modifiers.

In the code: [Rank Table](#code-rank-table).

### Ship Rank Chances for Normal Enemies

Each ship rank has a weight that depends on the site level, written "level" below:

```
Rookie    = 60 * e^(-0.02 * level)
Regular   = 0 if level < 6,  else 35 * e^(-0.012 * (level - 6))
Veteran   = 0 if level < 12, else 5 + 0.45 * level
Elite     = Veteran * Elite share for the tier
Champion  = Veteran * Champion share for the tier
```

The chance of each ship rank is its weight divided by the sum of all the weights. The Elite and Champion shares are 0 below Escalation tier 4 and 6 respectively (table in [Escalation](#escalation)). At tier 0 the best ship rank a normal enemy can have is Veteran, from level 12 up. Commander and Legendary never come from this roll.

The chance of each ship rank in percent at Escalation tier 0:

| Level | Rookie | Regular | Veteran |
|---|---|---|---|
| 1-5 | 100 | 0 | 0 |
| 6-11 | 60 | 40 | 0 |
| 12 | 52 | 36 | 12 |
| 20 | 48 | 35 | 17 |
| 30 | 42 | 34 | 24 |
| 45 | 34 | 31 | 35 |
| 60 | 26 | 27 | 47 |
| 80 | 18 | 21 | 61 |
| 100 | 12 | 16 | 72 |

The same chances at higher tiers, using level 60 as the example:

| Tier | Rookie | Regular | Veteran | Elite | Champion |
|---|---|---|---|---|---|
| 0 Frontier | 26.4 | 26.8 | 46.8 | 0 | 0 |
| 4 Havoc | 24.7 | 25.0 | 43.7 | 6.6 | 0 |
| 6 Maelstrom | 21.8 | 22.1 | 38.7 | 13.5 | 3.9 |
| 8 Extinction | 18.6 | 18.8 | 32.9 | 19.8 | 9.9 |

Only generated hostile sites use the Escalation tier for this roll.

In the code: [Ship Rank Chances for Normal Enemies](#code-ship-rank-chances-for-normal-enemies).

### Boss Ranks

Boss ranks are the ship ranks used for bounty targets. A bounty boss is one ship worth 40 to 80 points. It is worth more if the site's largest unit is worth more than 80 points. At high mission levels the lower end rises toward the upper end, because the minimum is also at least the site's budget times 2.5 divided by 16. The weights depend on the mission level, written `L` below:

```
Elite     = the larger of (40 - 0.3 * (L - 1)) and 5
Champion  = 0 if L < 40, else the smaller of 30 and (5 + 0.25 * (L - 40))
Commander = 0 if L < 70, else the smaller of 15 and (5 + 0.3 * (L - 70))
Legendary = 0 if L < 90, else the smaller of 15 and 0.2 * (L - 90)
```

| Level | Elite | Champion | Commander | Legendary |
|---|---|---|---|---|
| 1-39 | 100% | 0 | 0 | 0 |
| 40 | 85% | 15% | 0 | 0 |
| 50 | 77% | 23% | 0 | 0 |
| 70 | 52% | 34% | 14% | 0 |
| 90 | 32% | 42% | 26% | 0 |
| 100 | 22% | 43% | 30% | 4% |

So a bounty boss is never below Elite. The first Commander can appear at level 70 and the first Legendary at level 91. Escalation does not change boss ship ranks.

In the code: [Boss Ranks](#code-boss-ranks).

## How an NPC's Stats Are Built

A ship's final value for each stat is:

```
stat = (base value of the hull + sum of the equipment amounts) * product of all multipliers
```

The equipment adds amounts and multipliers. After the equipment is added up, these steps run in this order:

| Step | Who it affects | Effect |
|---|---|---|
| Reactor modifier | Ships you own only | Multiplies power, combat power, mining power and salvage power by 1 plus a bonus that depends on energy usage |
| Ship rank modifiers | Non-player factions | Applies the ship rank's fire rate and HP changes (see the [Rank Table](#rank-table)) |
| NPC ship HP curve | Every ship you do not own | Multiplies HP by the curve value for its level and size |
| Escalation NPC modifiers | Non-player factions | Multiplies HP by the tier multiplier and the base ramp. Multiplies combat power, drone power and torpedo power by the tier multiplier and the base ramp. |
| Escalation resist debuff | Your faction | Lowers all seven of your resists by the tier amount |
| HP balance bonus | Every ship and station, whoever owns it | Multiplies HP by the HP balance multiplier for its level |

Who is covered by these steps:

- NPCs never pay the energy penalty. The reactor modifier only runs for ships you own, so an NPC with a heavy loadout fires at full power.
- The NPC ship HP curve is for ships that are not yours. Your own ship does not get it.
- The HP balance bonus also applies to your own ships and to stations.

In the code: [How an NPC's Stats Are Built](#code-how-an-npcs-stats-are-built).

### Reactor Modifier (Your Ships)

Energy usage is the used capacity divided by the total capacity. The ship's power stats are multiplied by 1 plus the bonus from this table:

| Usage | Up to 50% | Up to 75% | Up to 100% | Up to 125% | Up to 150% | Above 150% |
|---|---|---|---|---|---|---|
| Bonus | +20% | +10% | 0 | -25% | -50% | -75% |

At or below 50% usage, the output bonus of the Combat Reactor skill is also added to the combat power multiplier.

In the code: [Reactor Modifier (Your Ships)](#code-reactor-modifier-your-ships).

### HP Balance Bonus

```
multiplier(L) = (1 + 0.28 * clamp01((L - 10) / 30)) * (1 + 0.6 * max(0, (L - 40) / 60) ^ 1.5)
```

Here `L` is the unit level, and clamp01 keeps a number between 0 and 1.

| Level | 10 | 20 | 30 | 40 | 50 | 60 | 70 | 80 | 90 | 100 |
|---|---|---|---|---|---|---|---|---|---|---|
| Multiplier | 1.00 | 1.09 | 1.19 | 1.28 | 1.33 | 1.43 | 1.55 | 1.70 | 1.86 | 2.05 |

In the code: [HP Balance Bonus](#code-hp-balance-bonus).

### NPC Ship HP Curve

The NPC ship HP curve multiplies the HP of NPC ships by a table of ratios, interpolated on a log scale. Below level 20 the ratio is 1. From level 20 to 30 it blends from 1 to the first table value. From level 30 on, it blends between table values every 10 levels. The table ends at level 200, and the last segment keeps extending past that. The result is never below 1.

The ship size selects the row. Sizes run from 1 to 8. Sizes 7 and 8 use the size 6 row, and a ship with no class counts as size 5. The target share per size 1 to 6 is 0.95, 0.75, 0.85, 0.95, 0.95 and 0.95. The result is the larger of 1 and (target share * ratio).

| Level | Size 1 | Size 2 | Size 3 | Size 4 | Size 5 | Size 6 |
|---|---|---|---|---|---|---|
| 30 or less | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 |
| 40 | 1.04 | 1.00 | 1.01 | 1.05 | 1.04 | 1.00 |
| 50 | 1.12 | 1.16 | 1.19 | 1.15 | 1.16 | 1.05 |
| 60 | 1.31 | 1.66 | 1.68 | 1.38 | 1.36 | 1.19 |
| 70 | 1.68 | 2.90 | 2.57 | 1.85 | 1.74 | 1.45 |
| 80 | 2.71 | 5.29 | 4.72 | 3.14 | 2.94 | 2.02 |
| 90 | 4.84 | 10.5 | 8.50 | 5.54 | 5.36 | 3.65 |
| 100 | 9.36 | 22.0 | 16.6 | 10.8 | 10.9 | 6.32 |
| 150 | 42.7 | 109 | 77.6 | 52.3 | 46.8 | 28.7 |

The curve stays flat up to level 40 and is steep after level 70. Smaller ships (sizes 2 and 3) get the steepest curve.

In the code: [NPC Ship HP Curve](#code-npc-ship-hp-curve).

### Base Ramp for NPCs

Independent of the Escalation tier, every ship that is not yours gets a level-based ramp on its HP and on its three power stats (combat, drone and torpedo):

```
base ramp(L) = 4 ^ clamp01((L - 30) / 70)
```

| Level | 30 or less | 40 | 50 | 60 | 70 | 80 | 90 | 100 and up |
|---|---|---|---|---|---|---|---|---|
| Multiplier | 1.00 | 1.22 | 1.49 | 1.81 | 2.21 | 2.69 | 3.28 | 4.00 |

The peak of 4 is fixed in a normal game. The Escalation popup's override takes a value for it but never stores it, and no other way to change it was found.

In the code: [Base Ramp for NPCs](#code-base-ramp-for-npcs).

### Putting the Multipliers Together

For an NPC ship at unit level `L`:

```
HP  = (base HP of the hull + module amounts)
      * module multiplier (Hull / Armor / Shield module main stat, see below)
      * ship rank HP multiplier
      * NPC ship HP curve(L, size)
      * base ramp(L) * Escalation tier HP multiplier
      * HP balance multiplier(L)
      * hull node Defense bonus (1 + 0.01 * defense nodes)

Combat power = sum of the weapons' combat power (each about weapon base * power level(L) * rarity multiplier)
      * base ramp(L) * Escalation tier damage multiplier
      * hull node Combat bonus (1 + 0.02 * combat nodes)
```

```mermaid
---
config:
  flowchart:
    wrappingWidth: 260
---
flowchart TD
    subgraph HP["HP"]
        direction TD
        H1["Hull base HP + module amounts"]
        H2["× module multiplier"]
        H3["× ship rank HP multiplier"]
        H4["× NPC ship HP curve"]
        H5["× base ramp (1 to 4)"]
        H6["× Escalation tier HP multiplier"]
        H7["× HP balance multiplier"]
        H8["× hull node Defense bonus<br/>1 + 0.01 per node"]
        H1 --> H2 --> H3 --> H4 --> H5 --> H6 --> H7 --> H8
    end
    subgraph CP["Combat power"]
        direction TD
        C1["Sum of the weapons' combat power"]
        C2["× base ramp (1 to 4)"]
        C3["× Escalation tier damage multiplier"]
        C4["× hull node Combat bonus<br/>1 + 0.02 per node"]
        C1 --> C2 --> C3 --> C4
    end
```

NPC modules do not roll bonus HP stats. Apart from hull nodes, the module multiplier is therefore the only way gear raises NPC HP. The module multiplier is `1 + main stat * size factor * power level(L) * rarity multiplier`. The power level is 25 times the damage multiplier for level L, which doubles every 10 levels up to level 100. The main stat is 0.01 to 0.014 for the Hull module and 0.013 to 0.014 for the Armor and Shield modules. The size factor is 1, 1.5 or 2.25 for a small, medium or large module (see [Equipment](#equipment)).

Worked example: a size 5 ship at unit level 60, Veteran rank, Frontier tier, with a Large Standard hull module (main stat 0.012):

```
module   = 1 + 0.012 * 2.25 * 1600 = 44.2
HP mult  = 44.2 * 1.3 * 1.36 * 1.81 * 1.43 = about 202 times the base HP
```

Damage per weapon at the same level is 3.0 to 3.15 times 1600. That is about 4900 combat power for a Large weapon at Standard rarity, and the base ramp of 1.81 multiplies it. How combat power turns into damage per shot is explained on the [combat damage page](combat-damage.md). The damage ratios in the tables below assume that damage is linear in combat power, which was not verified here.

In the code: [Putting the Multipliers Together](#code-putting-the-multipliers-together).

### Growth With Level

| Level | Power level (25 * 2^(L/10)) | Credit multiplier (2^(L/14)) | HP balance multiplier | Base ramp multiplier |
|---|---|---|---|---|
| 1 | 27 | 1.05 | 1.00 | 1.00 |
| 10 | 50 | 1.64 | 1.00 | 1.00 |
| 20 | 100 | 2.69 | 1.09 | 1.00 |
| 30 | 200 | 4.42 | 1.19 | 1.00 |
| 40 | 400 | 7.25 | 1.28 | 1.22 |
| 50 | 800 | 11.9 | 1.33 | 1.49 |
| 60 | 1,600 | 19.5 | 1.43 | 1.81 |
| 70 | 3,200 | 32.0 | 1.55 | 2.21 |
| 80 | 6,400 | 52.5 | 1.70 | 2.69 |
| 90 | 12,800 | 86.1 | 1.86 | 3.28 |
| 100 | 25,600 | 141 | 2.05 | 4.00 |
| 120 | 44,572 | 246 | 2.46 | 4.00 |

Damage doubles every 10 levels up to level 100, and every 25 levels after that. Credits double every 14 levels up to level 100, and every 25 levels after that.

In the code: [Growth With Level](#code-growth-with-level).

## Escalation

Escalation is a difficulty setting that you choose. The slider runs from tier 0 to tier 8. Nothing in the game code raises or lowers it by itself. Only the confirm button in the Escalation popup changes it, and you can move it up or down at any time (the popup pauses the game). A tier stays locked until your captain level reaches its unlock level.

| Tier | Name | Unlock (captain level) | Enemy toughness (HP) | Enemy damage | Enemy forces | Mission credits | Mission XP | Elite share | Champion share | Your resist | Combat Upgrade Kit drops on a kill |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | Frontier | 0 | x1.00 | x1.00 | x1.0 | +0% | +0% | 0 | 0 | 0 | 0 |
| 1 | Static | 0 | x1.15 | x1.08 | x1.1 | +10% | +4% | 0 | 0 | 0 | 0 |
| 2 | Fracture | 0 | x1.35 | x1.18 | x1.2 | +20% | +8% | 0 | 0 | 0 | 0 |
| 3 | Breach | 10 | x1.60 | x1.30 | x1.3 | +30% | +12% | 0 | 0 | 0 | 0 |
| 4 | Havoc | 25 | x2.80 | x1.50 | x1.5 | +50% | +20% | 0.15 | 0 | -20% | 0.2% |
| 5 | Onslaught | 50 | x3.80 | x2.00 | x1.7 | +75% | +30% | 0.25 | 0 | -40% | 0.4% |
| 6 | Maelstrom | 75 | x5.00 | x4.00 | x1.9 | +100% | +40% | 0.35 | 0.10 | -60% | 0.6% |
| 7 | Cataclysm | 100 | x6.20 | x6.00 | x2.1 | +125% | +50% | 0.45 | 0.20 | -75% | 0.8% |
| 8 | Extinction | 100 | x8.00 | x10.00 | x2.3 | +150% | +60% | 0.60 | 0.30 | -85% | 1.0% |

Column meanings:

- Enemy toughness and enemy damage multiply the NPC's HP stats and its combat, drone and torpedo power, on top of the base ramp. They apply to every ship that is not yours.
- Enemy forces is the tier's share of the points budget divided by the tier 0 share. It multiplies the points budget of generated hostile sites (see [How Many Enemies a Hostile Site Gets](#how-many-enemies-a-hostile-site-gets)). Levels below 10 have no budget table, so forces only grow where the personal points are above the site's own points value.
- Mission credits and mission XP: the game stores the tier on a mission when it generates the mission. Changing the tier recomputes the rewards of existing missions from their base amounts. The XP bonus is 0.4 times the credit bonus.
- The Elite share and Champion share columns set the ship rank weights. The Elite weight is the Veteran weight times the Elite share, as in the ship rank formula.
- Your resist is subtracted (in percentage points) from each of your seven resist stats on every ship of your faction.
- Combat Upgrade Kit drops is the chance per kill that a destroyed enemy drops one. The Escalation popup calls this line "Combat Upgrade Kit drops". The item itself is named Hull Upgrade Kit elsewhere in the game.

The popup also has an override with two sliders, one for toughness and one for damage. They move in steps of 0.1 from x1.0 to x4.0. The override factor multiplies the tier value, so Extinction with both sliders at x4.0 gives x32 HP and x40 damage. The override is stored in the save.

The tier also affects the ship rank rolls of generated hostile sites only (see above), not those of non-hostile payloads.

In the code: [Escalation](#code-escalation).

## Ship Rarity and Hull Nodes

Ship rarity (Standard to Legendary) does not multiply any stat. For an NPC it sets a cap on how many hull nodes the ship can have. It also sets the letter in the ship's hull rating on the tooltip. Rarity here is the ship's own quality tier, not its ship rank.

| Rarity | Node budget | Hull rating letter |
|---|---|---|
| Standard | 25 | A |
| Enhanced | 50 | B |
| High Grade | 75 | C |
| Exotic | 100 | X |
| Legendary | no cap | S |

The fleet hull rating on the map reads "Type-" followed by the letter and the node count, for example "Type-C62". The letter is the lowest rarity whose node budget holds the count, and the fleet average is kept between 0 and 100. The ship tooltip chip reads "Type-" plus the ship's own rarity letter and node count. Conquest fleet and garrison ships roll their starting node count with the same formula, capped at 85.

In the code: [Ship Rarity and Hull Nodes](#code-ship-rarity-and-hull-nodes).

### What Nodes Do

NPC ships spend their nodes on upgrade paths at random. Each path has a weight:

- 5 for the ship's own role path (Combat for combat ships, Mining for mining ships, Salvage for salvage ships).
- 3 each for the Defense path (a ship upgrade path, not a Defense station), for Torpedo (if the ship has a torpedo bay) and for Drones (if it has a drone bay).
- 2 for any other path in the list.

Every ship can use Combat and Defense.

| Path | Per node |
|---|---|
| Combat | +2% combat power |
| Torpedo | +2% torpedo power |
| Drones | +2% drone power |
| Defense | +1% shield HP, armor HP and hull HP each |
| Mining and Salvage | +2% mining power or +2% salvage power, respectively |

The node bonuses add up with the node count. A ship with 30 combat nodes has 1 + 0.02 * 30 = x1.6. Milestone bonuses at 10, 20 and 30 nodes in a path also exist, but they need accumulated activity (kills or yield) on the ship. A freshly generated NPC has none, so they are not counted here. Whether long-lived conquest ships accrue that progress was not verified.

For a plain combat ship (only the Combat and Defense paths), about 71% of nodes go to Combat and 29% to Defense. That is 5 of 7 slots against 2 of 7, because the pick range stops one short of the total weight, so the last path in the list loses one slot. With a drone or torpedo bay, these shares drop.

In the code: [What Nodes Do](#code-what-nodes-do).

### How Many Nodes an NPC Gets

```
nodes = floor( (level / 60) ^ 0.7 * hull nodes base * random(0.7 to 1.0) )     (level held between 1 and 100)
nodes = the smaller of nodes and the rarity's node budget
```

The base number of hull nodes is the last column of the ship rank table. The average roll factor is 0.85. This table shows the average number of nodes before the rarity cap, by ship rank:

| Level | Rookie | Regular | Veteran | Elite | Champion | Legendary |
|---|---|---|---|---|---|---|
| 10 | 5 | 7 | 10 | 12 | 14 | 15 |
| 30 | 10 | 14 | 21 | 26 | 29 | 31 |
| 60 | 17 | 23 | 34 | 42 | 48 | 51 |
| 100 | 24 | 33 | 49 | 61 | 68 | 73 |

A Veteran at level 60 with 34 nodes on a plain combat ship has about 24 combat nodes (x1.49 combat power) and 10 defense nodes (x1.10 HP).

In the code: [How Many Nodes an NPC Gets](#code-how-many-nodes-an-npc-gets).

### Which Rarity an NPC Ship Gets

When an NPC ship is fitted out, the game rolls its ship rarity from this table, by ship rank:

| Ship rank | Standard | Enhanced | High Grade | Exotic |
|---|---|---|---|---|
| Rookie | 80% | 20% | | |
| Regular | 60% | 40% | | |
| Veteran | 35% | 55% | 10% | |
| Elite | 15% | 60% | 25% | |
| Champion | | 50% | 45% | 5% |
| Commander | | 30% | 55% | 15% |
| Legendary | | 15% | 50% | 35% |

The code has a second, more generous table. For example, it gives a Commander 10% Standard, 30% Enhanced, 45% High Grade, 14% Exotic and 1% Legendary. Generated sites roll it first, but the fitting step runs afterward and overwrites it with the table above in every path that was read. No path was found where the first roll survives.

A generated NPC also cannot reach Legendary through nodes. The node roll tops out at 85 (rank base 60 times a level factor of at most 1.43, at level 100), which is under the 100 that an Exotic ship holds.

In the code: [Which Rarity an NPC Ship Gets](#code-which-rarity-an-npc-ship-gets).

## Equipment

### Item Level

An item's level is the larger of 1 and (location level + a roll), where the roll is -1, 0 or +1 at equal chance. Enemy gear is not limited by your level. Items built for your faction go through the item level cap:

```
if item level > your effective level:
    capped level = effective level + ceil((item level - effective level) ^ 0.7)
```

An item 10 levels above your effective level becomes 6 levels above it. An item 30 levels above becomes 11 levels above. Locked Containers are not capped.

In the code: [Item Level](#code-item-level).

### Rarity

| Rarity | Power multiplier | Bonus stat lines | Aspect slots | Credit value multiplier |
|---|---|---|---|---|
| Standard | 1.00 | 0 | No aspect slot | 1.0 |
| Enhanced | 1.10 | 1 | 25% chance of one common slot | 1.6 |
| High Grade | 1.20 | 2 | One slot: a common slot 75% of the time, or a rare slot instead 25% of the time | 2.2 |
| Exotic | 1.35 | 3 | One rare slot, plus a 25% chance of one common slot | 3.4 |
| Legendary | 1.50 | 3 | 75% of the time one common and one rare slot, 25% of the time two rare slots | 4.5 |

A "bonus stat line" is a random optional stat on top of the item's main stat. Main stats are always present. A builder can disable a rarity, and then the game uses the lowest rarity that builder allows.

Rarity multiplies almost every stat by the power multiplier above. Reactor energy capacity is the exception: it takes only the square root of the multiplier, so Legendary gives x1.22. Engine thrust stats grow much more slowly with level than weapons and modules do (a level exponent of 0.1).

An energy cost scales with level but not with rarity. Rarity is therefore free power: a Legendary weapon draws the same energy as a Standard weapon of the same level and size, with 50% more main stat. A level increase raises power and energy draw together.

This table shows how many item levels each rarity is worth, for a main stat that scales with 2^(L/10):

| Rarity | Main stat gain | Same as this many levels |
|---|---|---|
| Enhanced | +10% | 1.4 |
| High Grade | +20% | 2.6 |
| Exotic | +35% | 4.3 |
| Legendary | +50% | 5.9 |

The extra bonus stat lines and aspect slots come on top.

In the code: [Rarity](#code-rarity).

### Main Stat and Bonus Rolls

```
power level = round(25 * damage multiplier(item level))    // 2^(L/10) up to 100, then 2^(L/25) pace
value       = random(min, max)
            * size factor   (only if the stat is not a percentage stat, not a main Combat/Mining/Salvage Power, not energy capacity or thrust)
            * power level ^ level exponent
            * rarity multiplier ^ rarity exponent
```

The size factor is 1 for Small, 1.5 for Medium and 2.25 for Large. An "amount" stat adds the value. A "multiplier" stat multiplies by 1 + the value.

These are the main stats, from the equipment builders in the 0.8.3.5 data dumps (minimum to maximum, before level and rarity scaling):

| Slot | Stat | Small | Medium | Large |
|---|---|---|---|---|
| Hull module | Hull HP multiplier | 0.010-0.014 x1 | 0.010-0.014 x1.5 | 0.010-0.014 x2.25 |
| Armor module | Armor HP multiplier | 0.013-0.014 x1 | 0.013-0.014 x1.5 | 0.013-0.014 x2.25 |
| Shield module | Shield HP multiplier | 0.013-0.014 x1 | 0.013-0.014 x1.5 | 0.013-0.014 x2.25 |
| Weapon (most types) | Combat power amount | 1.0-1.05 | 2.0-2.1 | 3.0-3.15 |
| Reactor | Energy capacity amount | 1.5-1.6 | 3.4-3.5 | 4.5-4.8 |

Small weapons of the "Tiny" type use 0.5-0.6. Drone combat weapons use 0.5-0.6 as well, and drone mining and salvage turrets use 0.5-0.55. Mining and Salvage turrets follow the same 1, 2 and 3 pattern for mining power and salvage power. All stat ranges, spawn weights and the reactor and thrust exponents on this page come from the equipment builder asset data, not from the C# code. They were checked against the 0.8.3.5 data dumps (`EquipmentBuilder.allBuilders`).

Typical bonus stat lines are listed below. Unless a note says otherwise, each stat is as likely to be picked as any other, and it can appear at any item level:

| Stat | Range | Grows with level | Notes |
|---|---|---|---|
| Combat power, mining power, salvage power, drone power, precision, armor HP, shield HP, hull HP (flat) | 0.2-0.25 | yes | The size factor applies. HP lines are never rolled on NPC gear. |
| Power | 0.12-0.18 | yes | The size factor applies. |
| Torpedo power | 0.2-0.25 | yes | Only on items of level 45 and above. |
| Seven resists | 0.04-0.07 | no | Picked at a quarter of the usual chance (0.15 of the usual chance on reactors). Rarity scales them. |
| Attack speed, reload speed | 0.010-0.012 | no | Rolled on weapons. |
| Weapon range | 0.04-0.045 | no | Picked at 0.6 of the usual chance. |
| Critical damage | 0.02-0.025 | no | Rolled on weapons. |
| Magazine size | 0.010-0.012 | no | Rolled on weapons. |

Weapon range (a base value on the weapon itself) is also fixed per weapon. It is 13-14 for the Large Combat Laser, 17-18 for the Large Plasma Cannon and 31-33 for the Large Rail Cannon, and level and rarity do not change it.

To pick bonus stats, the game gives each candidate a random number, divides it by the stat's pick weight, and takes the lowest results first. It takes as many stats as the rarity's bonus stat line count plus the main stats. A lower pick weight makes a stat less likely to be chosen.

In the code: [Main Stat and Bonus Rolls](#code-main-stat-and-bonus-rolls).

### Roll Rules for NPC and Station Gear

For some owners, certain stats are removed from the bonus pool and the size scaling is capped:

| Owner | Rule |
|---|---|
| NPC ships | No bonus lines for hull HP, armor HP or shield HP |
| Station parts | The same, and the HP size factor is capped at Medium (x1.5) |

Station part modules always roll High Grade, unlike ships. See the [stations page](stations.md).

In the code: [Roll Rules for NPC and Station Gear](#code-roll-rules-for-npc-and-station-gear).

### Which Rarity NPC Gear Has

Each module slot and each hardpoint on an NPC ship gets its own rarity roll:

```
difficulty = 0.4 + L / 100        (L = the level of the gear being fitted)
x = random(0, difficulty)
x < 0.20  -> Standard
x < 0.50  -> Enhanced
x < 0.80  -> High Grade
x < 0.95  -> Exotic
else      -> Legendary
```

| Level | Standard | Enhanced | High Grade | Exotic | Legendary |
|---|---|---|---|---|---|
| 1 | 48.8% | 51.2% | 0 | 0 | 0 |
| 10 | 40.0% | 60.0% | 0 | 0 | 0 |
| 20 | 33.3% | 50.0% | 16.7% | 0 | 0 |
| 30 | 28.6% | 42.9% | 28.6% | 0 | 0 |
| 40 | 25.0% | 37.5% | 37.5% | 0 | 0 |
| 55 | 21.1% | 31.6% | 31.6% | 15.8% | 0 |
| 60 | 20.0% | 30.0% | 30.0% | 15.0% | 5.0% |
| 80 | 16.7% | 25.0% | 25.0% | 12.5% | 20.8% |
| 100 | 14.3% | 21.4% | 21.4% | 10.7% | 32.1% |

NPC gear is therefore much richer at high level than Locked Containers (next section). A level 100 NPC has a one in three chance of Legendary per slot.

Some ships are fitted differently. Starter ships built for you always get Standard. Mercenaries get a fixed rarity. Drones copy their drone bay's rarity for weapons and use Standard for everything else.

In the code: [Which Rarity NPC Gear Has](#code-which-rarity-npc-gear-has).

### Standard-Issue Gear

Fighters launched from hangar bays use "standard-issue" fitting. It differs from the usual NPC fitting in three ways:

- The gear is picked from every equipment design in the game, not only those you have unlocked.
- Items are built without the item level cap.
- Rarity uses the same roll as in the table above.

Empire Fleet ships, which unlock with Beyond the Front, use it too ([Empire Fleets](../expansion/empire-fleets.md)).

In the code: [Standard-Issue Gear](#code-standard-issue-gear).

## Loot

### Rarity of Locked Container and Facility Gear

Loot gear rarity is a weighted roll, and the chances depend on the item level:

| Level | Standard | Enhanced | High Grade | Exotic | Legendary |
|---|---|---|---|---|---|
| 1-10 | 57.1% | 42.9% | | | |
| 11-24 | 44% | 33% | 23-24% | | |
| 25 | 38.6% | 28.9% | 21.7% | 10.8% | |
| 40 | 37.7% | 28.3% | 22.6% | 11.3% | |
| 60 | 36.7% | 27.5% | 23.9% | 11.9% | 0 |
| 80 | 35.4% | 26.5% | 24.8% | 12.4% | 0.9% |
| 100 | 34.2% | 25.6% | 25.6% | 12.8% | 1.7% |

High Grade starts at level 11, Exotic at level 25 and Legendary at level 60. Before they are scaled to add up to 100%, the weights are: Standard 0.40, Enhanced 0.30, High Grade 0.2 + level/1000 (kept between 0.2 and 0.3), Exotic 0.1 + level/2000 (kept between 0.1 and 0.15), and Legendary (level - 60)/2000.

This roll is used by Locked Containers (gear and blueprints), facility compartments, research files and skirmish station loot, which is at least High Grade. Boosters are Enhanced by default. From level 20 they have a 35% chance of High Grade. Where Exotic is allowed (Locked Containers and facility rewards, but not NPC-fitted boosters), a booster of level 45 or higher first gets a 15% chance of being Exotic.

In the code: [Rarity of Locked Container and Facility Gear](#code-rarity-of-locked-container-and-facility-gear).

### What a Destroyed NPC Ship Drops

Each of these is rolled independently:

| Drop | Chance | Notes |
|---|---|---|
| One of its fitted modules | 12% | 95% of the time, the pool is only the lowest-rarity fitted items. |
| One of its fitted weapons | 12% | The same 95% lowest-rarity rule applies. |
| One cargo item (not ammo) | 30% | Worth up to a whole number from 3 to 9, times 100 * the credit multiplier for its level, in credits of value. |
| Ammo | 10% | Worth up to 1 or 2 times 100 * the credit multiplier for its level. |
| Credits | 30% | random(2.5, 3.25) * 100 * the credit multiplier for its level. |
| Blueprint | 4% | A ship of rank Elite or above requests at least High Grade. Lower ship ranks use the default roll (not verified). |
| Ship blueprint piece | 5% | Only for ship classes you have not unlocked, at or above the class unlock level. |
| Officer upgrade (High Grade) | 1% | Level 30 and above. |
| Commendation tag (officers only) | In quadrant 2: a 20% roll, then, if it fails, a 10% roll at system level 10 or higher. Elsewhere: 10% at system level 10 or higher. | In quadrant 2, the first roll gives tag 3 at Champion or above (25%), tag 2 at Veteran or above (33%), and tag 1 otherwise. The second roll, and every other quadrant, gives tag 1. |

The items it drops are the ones it carries, so their rarity follows the NPC gear table above. Because 95% of module and weapon drops filter to the lowest rarity on the ship, you almost always get its worst fitted item. A station defense platform has its own table: a 40% chance of one weapon, with the filter at 55% instead of 95%.

Credit drops follow this formula:

```
credits = round(base * 100 * credit multiplier(L))
```

| Level | 10 | 30 | 50 | 60 | 80 | 100 |
|---|---|---|---|---|---|---|
| Average credit drop when it triggers (30% chance) | 472 | 1,270 | 3,418 | 5,607 | 15,094 | 40,630 |
| Bounty credit reward (base 25, before Escalation bonus) | 4,102 | 11,041 | 29,720 | 48,761 | 131,254 | 353,309 |

In the code: [What a Destroyed NPC Ship Drops](#code-what-a-destroyed-npc-ship-drops).

## XP Rewards

```
XP per kill = ceil( 5 * base XP reward * level factor * 2^(your level / 9) ) * ship rank point multiplier
```

The base XP reward is a per-ship value. In the 0.8.3.5 data dump it is 10 on 45 of the 159 ship prefabs, and the others range from 2 (the small drone ships) to 110 (for example Aquila, Bogatyr and Monsoon). The tables below use 10. Your level is your effective level. The level factor depends on the target's unit level compared with yours:

| Target's level compared with yours | Level factor |
|---|---|
| Up to 3 levels above | 1 + 0.1 per level (up to 1.3) |
| The same level | 1.0 |
| 1, 2 or 3 levels below | 0.8, 0.6 or 0.4 |
| 4 or more levels below | 0.00001 (effectively nothing) |

XP needed to finish a level is 1000 * 2^(level / 5). Kill XP grows by 2^(1/9) per level, while the XP needed grows by 2^(1/5), so each level takes more kills than the last. The next table is for a Rookie ship (point multiplier x1.0) at the same level as you:

| Level | XP per kill | XP to finish the level | Kills per level |
|---|---|---|---|
| 10 | 109 | 4,000 | 37 |
| 20 | 234 | 16,000 | 68 |
| 30 | 504 | 64,000 | 127 |
| 50 | 2,352 | 1,024,000 | 435 |
| 70 | 10,973 | 16,384,000 | 1,493 |
| 100 | 110,599 | 1,048,576,000 | 9,481 |

Vanguard XP (earned past the maximum level) uses a 0.25 scale on the same curve. It is not covered further here.

Killing an enemy of a higher ship rank is worth more. Compared with a Rookie's base, a Regular is worth x1.2, a Veteran x1.3, an Elite x1.5, a Champion x1.6, a Commander x2.0 and a Legendary x2.4. Killing targets 4 or more levels below you gives almost nothing, so XP farming needs enemies close to your level.

In the code: [XP Rewards](#code-xp-rewards).

## Player-Side Progression That Scales Combat

| What | Effect |
|---|---|
| Effective level | Sets the item level cap and kill XP. The conquest levels also follow it, see the [war page](../conquest/war.md). |
| Captain level | Unlocks Escalation tiers and sets your personal fleet budget step (one step per 5 levels from level 10). The budget is counted in points. |
| Unit level of your ship | The HP balance multiplier applies to your own ship's HP. The power level also scales your precision into crit chance: 0.03 + 0.05 * precision / power level. The precision part has a soft cap once it passes 5%. |
| Escalation tier | Lowers your resists by 0 to 85 points at tiers 4 to 8. |

Empire Level, which unlocks with Beyond the Front, adds a fleet mix bonus. See [Your empire](../expansion/your-empire.md).

In the code: [Player-Side Progression That Scales Combat](#code-player-side-progression-that-scales-combat).

## Practical Reading

### How Much Tougher Is an Enemy Five or Ten Levels Higher

This table assumes a size 5 hull, a Large Standard hull module, the Frontier tier, and the same ship rank and rarity on both enemies. The HP ratios include the module multiplier, the ship HP curve, the base ramp and the HP balance multiplier. The damage ratios cover the power level and the base ramp. Hull nodes are not included.

| Starting level | HP, +5 levels | HP, +10 levels | Damage, +5 levels | Damage, +10 levels |
|---|---|---|---|---|
| 10 | x1.30 | x1.72 | x1.42 | x2.00 |
| 20 | x1.35 | x1.88 | x1.41 | x2.00 |
| 30 | x1.57 | x2.53 | x1.56 | x2.44 |
| 40 | x1.63 | x2.70 | x1.56 | x2.44 |
| 50 | x1.72 | x2.99 | x1.56 | x2.44 |
| 60 | x1.83 | x3.35 | x1.56 | x2.44 |
| 70 | x2.12 | x4.49 | x1.56 | x2.44 |
| 80 | x2.20 | x4.86 | x1.56 | x2.44 |

Below level 30, an enemy 5 levels higher is about 1.3 to 1.4 times tougher and hits 1.4 times harder. An enemy 10 levels higher is about twice as tough and hits twice as hard. From level 30 the base ramp adds to damage, and from level 40 the HP curve adds to HP. Past level 70, a 10-level gap multiplies HP by 4.5 to 5. To match an enemy 10 levels higher in that range, you need about 2.4 times more damage, and you must survive about 2.4 times more damage taken.

### How Much a Ship Rank Is Worth in Levels

This table compares HP only. The ship rank also raises fire rate by up to 30% and costs the site more of its budget.

| Your level | Veteran | Elite | Champion | Commander | Legendary |
|---|---|---|---|---|---|
| 20 | +4.3 | +6.6 | +10.2 | +12.9 | +15.3 |
| 40 | +2.8 | +4.2 | +6.6 | +8.9 | +11.0 |
| 60 | +2.2 | +3.4 | +5.4 | +7.3 | +9.1 |
| 80 | +1.7 | +2.6 | +4.1 | +5.6 | +7.0 |

Each cell is the number of extra levels a Regular-rank enemy (x1.0) would need to match that ship rank's HP. A Champion at level 60 has as much HP as a Regular-rank enemy about 5 levels higher. Ship ranks matter more at low levels because HP grows more slowly per level there (1.3 times per 5 levels below level 30, against 2 times per 5 levels above level 70). Ship rank does not change damage per shot. A Legendary therefore soaks up more damage and fires up to 30% faster, but it does not hit harder per shot.

These are the ship ranks you can meet:

- At levels 1 to 5, only Rookie ships appear.
- At levels 6 to 11, Rookie and Regular ships appear.
- From level 12 at tier 0, ships up to Veteran appear.
- In generated sites, Elite ships appear from tier 4 (Havoc) and Champion ships from tier 6 (Maelstrom).
- Bounty bosses are Elite or better. Commanders appear from level 70 and Legendary ships from level 91.

### What Escalation Is Worth in Levels

This table converts the HP and damage multipliers into levels, at the 2^(L/10) pace:

| Tier | HP is worth | Damage is worth |
|---|---|---|
| 1 Static | +2 levels | +1 level |
| 2 Fracture | +4 levels | +2 levels |
| 3 Breach | +7 levels | +4 levels |
| 4 Havoc | +15 levels | +6 levels |
| 5 Onslaught | +19 levels | +10 levels |
| 6 Maelstrom | +23 levels | +20 levels |
| 7 Cataclysm | +26 levels | +26 levels |
| 8 Extinction | +30 levels | +33 levels |

Tiers 6 to 8 also lower your resists by 60, 75 and 85 points, so the real gap is larger than these numbers show.

### What Gear Rarity Is Worth

- Rarity is free power. It adds +10% to +50% on main stats at no extra energy draw, plus extra stat lines and aspect slots. A Legendary item is worth about 6 levels of item level, and it also costs no extra energy for that.
- Item level raises power and energy cost together. A level 60 Standard weapon has a power level of 1,600. A level 54 Legendary weapon has about the same power with lower energy draw.
- At level 60, an NPC rolls Legendary on a given slot 5% of the time. That rises to 20.8% at level 80 and 32.1% at level 100. Late-game enemies therefore carry much better gear than Locked Containers, which stay below 2% Legendary at level 100.
- To find better rarity, fight enemies above level 60 (see the NPC gear table) and open boxes at level 25 or higher for Exotic. Aim for ship rank Elite or above so the blueprint drop is at least High Grade. Ships drop their worst fitted item 95% of the time, so a ship with one Legendary and one Standard module almost always drops the Standard one.

These tables are worked out from the formulas on this page, not measured in play.

## In the Game Code

This part backs the sections above with the names and formulas from the decompiled code of version 0.8.3.4, re-checked against 0.8.3.5 for the cited classes (the formulas are the same). Search for the class and method names in a decompiler to check a claim. The `Source:` lines list where each rule lives. The prefab values come from the equipment builder components in the game's resources and were re-read from the 0.8.3.5 data dumps. Translation strings were not re-read for 0.8.3.5.

### Code: Which Level Is Used for What

| Term | Code name |
|---|---|
| Captain level | `GamePlayer.level` |
| Effective level | `GamePlayer.effectiveLevel` |
| System level / location level | `SystemMapData.level`, copied to every location (`MapPointOfInterest`, "POI" in code) in the system |
| Unit level | `AbstractUnitData.level` |
| Fleet budget steps | `FleetLevelSteps` |

Unit level is not stored on the ship. `RecalculateLevel` takes the mean of all equipped item levels (rounded), adds the highest item level, and halves that with integer division (the fraction is dropped). An enemy generated at location level 40 therefore has unit level 39-41, and every level-based modifier below uses that number.

Source: `GamePlayer.level`, `GamePlayer.effectiveLevel`, `EquipmentBuilder.BuildNewItem`, `AbstractUnitData.RecalculateLevel`

#### Code: How Many Enemies a Hostile Site Gets

```
points = max(site.pointsValue, round(threatShare(tier) * PersonalPoints(level)))
```

`PersonalPoints` is the same table that sets your personal fleet budget (counted in points). It is 0 below level 10 and rises by one row every 5 levels, ending at level 100.

Each unit is charged `ship points * ship rank point multiplier`. The ship count cap for combat payloads also grows by `floor(level * pointsScale / 20)`. The tier-scaled budget applies when `IsThreatPayload` is true (combat payload of a faction hostile to the player) and the payload is not a filler (`IsFillerCap`: `maxPointsPerUnit < maxUnitPoints / 5`). `payloadTier` is the `generationTier` stored on the guard descriptors (`UnitGenerationDescriptor.rolledTier`, cleared when the site is no longer in the Recent retention bucket), falling back to `EscalationHelper.CurrentTier`.

Source: `MapPointOfInterest.GetPayloadPoints`, `MapPointOfInterest.CreateUnitPayload`, `MapPointOfInterest.ResolveMaxUnits`, `FleetLevelSteps.PersonalPoints`

### Code: Ship Ranks

Every NPC ship has a ship rank (`UnitRank`). The ship rank is rolled when the ship is generated. The game shows the rank name from `UnitRankHelper.displayNames`, which is hard-coded English and not in the text table. The badge is added in `SpaceShip.BuildTooltipCard` through `UnitRankHelper.GetRankName` and `GetColor`. The "Regular" rank is `UnitRank.Standard` in the code. Rarity's lowest tier is also called `Standard`.

#### Code: Rank Table

- HP column: multiplies `HullHP`, `ArmorHP` and `ShieldHP` (`npcHealthAffectedStats`).
- Fire rate column: added to the unit's `AttackSpeed` and `ReloadSpeed` stats. A turret's delay is `baseDelay / (1 + stat)`. It does not change damage per shot, and nothing in the rank touches `CombatPower`.
- Point column: the ship rank's `GetAdjustedPoints` multiplier.

The code also defines a "stat multiplier" per ship rank (0.7, 0.9, 1.1, 1.3, 1.6, 2.0, 2.4) and a helper `ApplyRankMultiplier` that uses it. Both stats the rank modifies are percentage stats, so only the additive column above runs; the stat multiplier values and `ApplyRankMultiplier` have no callers outside `UnitRankHelper`.

Player-faction units skip rank modifiers entirely.

Source: `UnitRankHelper` (rank tables), `AbstractUnit.ApplyNpcRankModifiers`, `AbstractTurret.fireDelay`, `AbstractTurret.reloadDelay`, `LootManager.AddKillExperience`, `MapPointOfInterest.AddUnitFromShip`

#### Code: Ship Rank Chances for Normal Enemies

Weights, with `L` the site level:

```
Rookie    = 60 * exp(-0.02 * L)
Regular   = 0 if L < 6,  else 35 * exp(-0.012 * (L - 6))
Veteran   = 0 if L < 12, else 5 + 0.45 * L
Elite     = Veteran * eliteShare(tier)
Champion  = Veteran * championShare(tier)
```

`eliteShare` and `championShare` are 0 below Escalation tier 4 and 6 respectively. Only generated hostile sites (the threat payload in `CreateUnitPayload`) pass the current tier.

Source: `UnitRankHelper.GetRandomUnitRankForLevel`, `UnitRankHelper.GetNormalRankWeights`, `EscalationHelper.GetEliteRankShare`, `EscalationHelper.GetChampionRankShare`

#### Code: Boss Ranks

Boss ranks are ship ranks used for bounty targets (`BountyBoard.SpawnBountyBoss`, one ship of 40 to max(80, site max unit points) points; `ResolvePointsPerUnitRange` also raises the minimum to `round(budget * 2.5 / 16)`, capped at the maximum). Weights, with `L` the mission level:

```
Elite     = max(40 - 0.3 * (L - 1), 5)
Champion  = 0 if L < 40, else min(30, 5 + 0.25 * (L - 40))
Commander = 0 if L < 70, else min(15, 5 + 0.3 * (L - 70))
Legendary = 0 if L < 90, else min(15, 0.2 * (L - 90))
```

Source: `UnitRankHelper.GetBossRankWeights`, `BountyBoard.SpawnBountyBoss`, `BountyBoard.GetBossPointWindow`

### Code: How an NPC's Stats Are Built

Every unit recalculates its stats through `AbstractUnit.RecalculateStats`. The final value of a stat is:

```
stat = (prefab base + sum of equipment amounts) * product of all multipliers
```

The prefab base is `hullHPScale * 150`, `armorHPScale * 300` or `shieldHPScale * 300`. Equipment lines add amounts and multiply. After the equipment is aggregated, these steps run in this order:

| Step | Who it affects | Effect |
|---|---|---|
| `ApplyReactorModifier` | Player-owned ships only | Multiplies `Power`, `CombatPower`, `MiningPower`, `SalvagePower` by `1 + bonus(energy usage)` |
| `ApplyNpcRankModifiers` | Non-player faction | Ship rank fire rate and HP (see rank table) |
| `ApplyNpcShipHpCurve` | Every ship whose data has `isPlayer == false` | HP x `NpcShipHpCurve.Multiplier(level, size)` |
| `ApplyEscalationNpcModifiers` | Non-player faction | HP x tier x base ramp; `CombatPower`, `DronePower`, `TorpedoPower` x tier x base ramp |
| `ApplyEscalationPlayerResistDebuff` | Player faction | All seven resists lowered by the tier amount |
| `ApplyHpBalanceBonus` | Every unit, no faction check | HP x `HpBalanceMultiplier(level)` |

- The reactor modifier only runs for player-owned ships.
- `ApplyNpcShipHpCurve` checks the `isPlayer` flag on the data, not the faction. Your own ship does not get the curve.
- `ApplyHpBalanceBonus` also applies to your own ships and to stations.

#### Code: Reactor Modifier (Your Ships)

Energy usage is `used capacity / capacity`. The thresholds are `ReactorModule.energyThresholdModifiers`.

Source: `AbstractUnit.RecalculateStats`, `AbstractUnit.ApplyReactorModifier`, `ReactorModule.energyThresholdModifiers`

#### Code: HP Balance Bonus

```
HpBalanceMultiplier(L) = (1 + 0.28 * clamp01((L - 10) / 30)) * (1 + 0.6 * max(0, (L - 40) / 60) ^ 1.5)
```

Source: `GameMath.HpBalanceMultiplier`, `AbstractUnit.ApplyHpBalanceBonus`

#### Code: NPC Ship HP Curve

`NpcShipHpCurve` multiplies HP of NPC ships by a table of ratios that is interpolated on a log scale. Below level 20 the ratio is 1. From 20 to 30 it blends from 1 to the first table value; from 30 on it blends between table values every 10 levels (the table ends at level 200, and the last segment keeps extending past that). The result is `max(1, targetShare * ratio)`.

The ship size (1 to 8; sizes 7 and 8 use the size 6 row; a ship with no class counts as size 5) selects the row. Target share per size 1 to 6 is 0.95, 0.75, 0.85, 0.95, 0.95, 0.95.

Source: `NpcShipHpCurve.Multiplier`, `NpcShipHpCurve.MedianRatio`, `ShipUpgradeCalculator.GetShipSize`

#### Code: Base Ramp for NPCs

```
baseRamp(L) = BasePeak ^ clamp01((L - 30) / 70)        BasePeak = 4
```

`BasePeak` is 4 unless `basePeakOverride` is set. The override popup takes a base-peak argument but never stores it, and I found no other writer, so it stays 4.

Source: `EscalationHelper.GetNpcBaseMultiplier`, `EscalationHelper.BasePeak`, `AbstractUnit.ApplyEscalationNpcModifiers`, `EscalationPopup.ApplyOverride`

#### Code: Putting the Multipliers Together

```
HP  = (prefab base + module amounts)
      * moduleMultiplier(L, rarity)       // the Hull / Armor / Shield module's main stat, see below
      * rankHp
      * NpcShipHpCurve(L, size)
      * baseRamp(L) * tierHp
      * HpBalanceMultiplier(L)
      * hull-node Defense-path bonus (1 + 0.01 * defenseNodes)

CombatPower = sum of weapon CombatPower (each ~ weapon base * powerLevel(L) * rarityMult)
      * baseRamp(L) * tierDamage
      * hull-node Combat bonus (1 + 0.02 * combatNodes)
```

NPC modules do not roll HP bonus stats (`NpcShipGearRollRules` excludes them), so apart from hull nodes the module multiplier is the only way gear raises NPC HP. The module multiplier is `1 + mainStat * sizeFactor * powerLevel(L) * rarityPower`, where `powerLevel(L) = round(25 * DamageMultiplier(L))`, the main stat is 0.01-0.014 for the Hull module and 0.013-0.014 for Armor and Shield, and `sizeFactor` is 1, 1.5 or 2.25 for a small, medium or large module.

In the worked example, `mainStat` is 0.012. Damage per weapon at the same level is `3.0-3.15 * 1600 = ~4900` "combat power" for a Large weapon at Standard rarity, before the base ramp.

#### Code: Growth With Level

`powerLevel` is 25 * `2^(L/10)` and the credit multiplier is 2^(L/14). Damage doubles every 10 levels up to level 100 (`DamageMultiplier`), and every 25 levels past it. Credits double every 14 levels up to 100 and every 25 after.

Source: `GameMath.DamageMultiplier`, `GameMath.CostMultiplier`, `GameMath.PiecewiseDoublings`, `EquipmentBuilder.SetupItem`

### Code: Escalation

The only caller of `EscalationHelper.SetTier` is the Escalation popup's confirm button.

Column meanings:

- Enemy HP and damage multiply the NPC's HP stats and `CombatPower`, `DronePower`, `TorpedoPower`, on top of the base ramp. They apply to every non-player unit that recalculates its stats.
- Enemy forces is `threatShare(tier) / threatShare(0)`, the multiplier on the points budget of generated hostile sites. Raw shares are 0.50, 0.55, 0.60, 0.65, 0.75, 0.85, 0.95, 1.05, 1.15. Levels below 10 have no budget table, so forces only grow where `PersonalPoints` is above `pointsValue`.
- Mission credits and mission XP: the tier is stored on a mission when it is generated. Changing tier recomputes rewards on existing missions from their base amounts; XP gets 0.4 times the credit bonus.
- Elite and champion share: the Elite weight is `Veteran weight * share`.
- Your resists: subtracted from each of your seven resist stats (percentage points) on every player-faction unit.
- Combat Upgrade Kit: chance per kill, from `LootChance.GetHullUpgradeKitKillChance`.

The popup's override factor multiplies the tier value. The override is stored in the save.

Source: `EscalationHelper` (tables, `SetTier`, `RecomputeMissionRewards`, `GetNpcHpMultiplier`, `GetNpcDamageMultiplier`), `EscalationPopup`, `LootChance.GetHullUpgradeKitKillChance`, `MapPointOfInterest.GetPayloadPoints`

### Code: Ship Rarity and Hull Nodes

`HullRating.ToRating` clamps the fleet average to 0-100. The map text is `Type-<letter><nodes>`, for example `Type-C62`, built by `HullRating.FormatRating` (letter from `SmallestRarityHolding`). The tooltip chip uses `ShipTooltipCardBuilder.GetRefitCode` (`shipRarity` letter + `TotalPointsSpent`) inside `@ShipTooltipRefitCode` (`Type-{0}`). `HullRating.RollStartingPoints` (conquest fleets and garrisons) caps the roll at 85.

#### Code: What Nodes Do

The path weights are built by `ShipUpgradeCalculator.BuildWeightedNpcPaths`. Per-node amounts:

| Path | Per node |
|---|---|
| Combat | +2% `CombatPower` |
| Torpedo | +2% `TorpedoPower` |
| Drones | +2% `DronePower` |
| Defense | +1% `ShieldHP`, `ArmorHP`, `HullHP` each |
| Mining, Salvage | +2% `MiningPower` / `SalvagePower` |

The node bonuses are additive in the node count (a ship with 30 combat nodes has `1 + 0.02 * 30 = x1.6`). Milestone bonuses at 10, 20 and 30 nodes in a path also exist (`IsMilestoneUnlocked`), but they need accumulated activity (kills or yield) on the ship, and a freshly generated NPC has none, so I did not count them. Whether long-lived conquest ships accrue progress was not verified. `PickWeightedPath` calls `RandomRange(0, totalWeight - 1)` and `SeededRandom.RandomRange(int, int)` excludes the upper bound, so the last path in the list (`GetNpcApplicablePaths` order: Combat, Defense, Mining or Salvage, Drones, Torpedo) is one slot short.

Source: `ShipUpgradeCalculator.BuildWeightedNpcPaths`, `ShipUpgradeCalculator.GetPathAmountPerNode`, `ShipUpgradeCalculator.GetDefenseStatLines`, `ShipUpgradeCalculator.PointBudget`, `ShipUpgradeCalculator.IsMilestoneUnlocked`, `HullRating.ToRating`, `HullRating.FormatRating`

#### Code: How Many Nodes an NPC Gets

```
nodes = floor( (clamp(L, 1, 100) / 60) ^ 0.7 * rankBase * U(0.7, 1.0) )
nodes = min(nodes, PointBudget(rarity))
```

`rankBase` is the last column of the ship rank table.

#### Code: Which Rarity an NPC Ship Gets

`LoadDefaultEquipment` on an NPC ship calls `AssignNpcUpgrades`, which rolls ship rarity from the table above by ship rank.

`ShipUpgradeCalculator.GetRandomShipRarity` is a second, more generous table (for example Commander: 10% Standard, 30% Enhanced, 45% High Grade, 14% Exotic, 1% Legendary). Generated sites set the rarity with it first, but `LoadDefaultEquipment` runs afterward and overwrites it with the table above in every path I read (`MapPointOfInterest`, `ConquestCombat.BuildShip`, `LureSite`, `HangarBayModule`). I could not find a path where the first roll survives.

Source: `ShipUpgradeCalculator.AssignNpcUpgrades`, `ShipUpgradeCalculator.RollNpcShipRarity`, `ShipUpgradeCalculator.CalculateNpcHullNodes`, `ShipUpgradeCalculator.GetRandomShipRarity`, `SpaceShipData.LoadDefaultEquipment`

### Code: Equipment

#### Code: Item Level

An item's level is `max(1, poiLevel + roll)` (`poiLevel` is the location level) with the roll -1, 0 or +1 with equal chance. NPC gear is built with `ignoreLevelCap` on, so enemy items are not limited by your level. Items built for your faction go through the item level cap:

```
if itemLevel > effectiveLevel:
    cappedLevel = effectiveLevel + ceil((itemLevel - effectiveLevel) ^ 0.7)
```

Locked Containers pass `ignoreLevelCap` and are not capped.

Source: `EquipmentBuilder.BuildNewItem`, `GameMath.ApplyItemLevelCap`, `LootManager` (Locked Container item creation)

#### Code: Rarity

Rarity multiplies a stat by `powerMultiplier ^ rarityScaling`. Almost every stat has `rarityScaling` 1, so the table above applies directly. Reactor energy capacity has 0.5 (so Legendary gives x1.22). Engine thrust stats have `levelScaling` 0.1.

An energy cost field (`capacityCost`) scales with level (`levelScaling` 1) but not with rarity (`rarityScaling` 0).

Aspect slots come from `EquipmentBuilder.GetAspectSlotAmount` as a (common, rare) pair: Standard (0, 0), Enhanced (25% 1, else 0; rare 0), High Grade `GetHighGradeAspectSlots` (a 25% roll gives (0, 1), otherwise (1, 0)), Exotic (25% 1, else 0; rare 1) and Legendary `GetLegendaryAspectSlots` ((1, 1) 75% of the time, (0, 2) 25%). `FillAspectSlots` fills the rare slots first from the builder's rare aspect list (the common list if the builder has no rare aspects), then the common slots from the common list. So a High Grade item has exactly one slot, never two.

Source: `RarityExtensions.GetPowerMultiplier`, `RarityExtensions.GetCostMultiplier`, `RarityExtensions.GetStatCount`, `EquipmentBuilder.GetAspectSlotAmount`, `EquipmentBuilder.IsRarityAvailable`, `EquipmentBuilder.ApplyDynamicFields`

#### Code: Main Stat and Bonus Rolls

```
powerLevel     = round(25 * DamageMultiplier(itemLevel))    // 2^(L/10) up to 100, then 2^(L/25) pace
value          = random(min, max)
               * sizeFactor        (only if the stat is not a percentage stat, not a main Combat/Mining/Salvage Power, not energy capacity or thrust)
               * powerLevel ^ levelScaling
               * rarityPower ^ rarityScaling
```

`sizeFactor` is 1 for Small, 1.5 for Medium, 2.25 for Large. An "amount" stat adds `value`; a "multiplier" stat multiplies by `1 + value`.

Main stats by slot: Hull module `HullHP` multiplier, Armor module `ArmorHP` multiplier, Shield module `ShieldHP` multiplier, Weapon (most types) `CombatPower` amount, Reactor `EnergyCapacity` amount.

Typical bonus stat lines (all have `spawnWeight` 1 unless noted; `minSpawnLevel` 0 unless noted):

| Stat | Range | Level scaling | Notes |
|---|---|---|---|
| `CombatPower`, `MiningPower`, `SalvagePower`, `DronePower`, `Precision`, `ArmorHP`, `ShieldHP`, `HullHP` (flat) | 0.2-0.25 | yes | size factor applies; HP lines are never rolled on NPC gear |
| `Power` | 0.12-0.18 | yes | size factor applies |
| `TorpedoPower` | 0.2-0.25 | yes | only items of level 45+ |
| Seven resists | 0.04-0.07 | no | `spawnWeight` 0.25 (0.15 on reactors); rarity scales them |
| Attack speed, reload speed | 0.010-0.012 | no | weapons |
| Weapon range | 0.04-0.045 | no | `spawnWeight` 0.6 |
| Critical damage | 0.02-0.025 | no | weapons |
| Magazine size | 0.010-0.012 | no | weapons |

Bonus stats are chosen by sorting the candidates by `random / spawnWeight` and taking the first N, where N is the rarity's stat line count plus the main stats.

Source: `EquipmentBuilder.SetupItem`, `EquipmentBuilder.BuildInitialStats`, `EquipmentBuilder.ApplyStatScaling`, `EquipmentBuilder.AutoScaleBySize`, `GameMath.DamageMultiplier`; builder prefab data (`EquipmentBuilderStat`, `EquipmentBuilderField` components in the game's resources)

#### Code: Roll Rules for NPC and Station Gear

`EquipmentRollRules` removes stats from the bonus pool and caps size scaling. NPC ships use `SpaceShipData.NpcShipGearRollRules`, station parts use `CombatStationPartData.StationGearRollRules`. Station part modules always roll High Grade (`fixedModuleRarity`).

Source: `EquipmentRollRules`, `SpaceShipData.NpcShipGearRollRules`, `CombatStationPartData.StationGearRollRules`

#### Code: Which Rarity NPC Gear Has

`LoadDefaultEquipment` picks gear per slot. Each module slot and hardpoint gets its own roll:

```
difficultyModifier = 0.4 + L / 100        (L = the equipment level passed in)
x = random(0, difficultyModifier)
x < 0.20  -> Standard
x < 0.50  -> Enhanced
x < 0.80  -> High Grade
x < 0.95  -> Exotic
else      -> Legendary
```

Other callers pass a different modifier. Starter ships built for you (`GameplayManager`) pass 0, which always gives Standard. Mercenaries pass 1 and a fixed rarity. Drones copy their drone bay's rarity for weapons and use Standard for everything else.

Source: `AbstractUnitData.LoadDefaultEquipment`, `AbstractUnitData.GetDefaultEquipmentRarity`, `AbstractUnitData.GetDefaultModuleRarity`, `DroneBayModule`, `GameplayManager`

#### Code: Standard-Issue Gear

`LoadStandardIssueEquipment(level, loadout, seed)` is `LoadDefaultEquipment` with the faction temporarily cleared. That has three effects:

- Equipment builders are picked from the full list, not only those you have unlocked.
- Items are built with the level cap off.
- Rarity uses the same `0.4 + L/100` roll.

It is used for fighters launched from hangar bays (`HangarBayModule`) and for the standard-issue copies of your ships in conquest battles (`ConquestCombat.BuildStandardIssueCopy`).

Source: `AbstractUnitData.LoadStandardIssueEquipment`, `HangarBayModule`, `ConquestCombat.BuildStandardIssueCopy`

### Code: Loot

#### Code: Rarity of Locked Container and Facility Gear

`LootRarity.RollRarity(level)` is a weighted roll:

| Rarity | Weight | From level |
|---|---|---|
| Standard | 0.40 | 1 |
| Enhanced | 0.30 | 1 |
| High Grade | clamp(0.2 + L/1000, 0.2, 0.3) | 11 |
| Exotic | clamp(0.1 + L/2000, 0.1, 0.15) | 25 |
| Legendary | (L - 60) / 2000 | 60 |

The weights are normalized by their sum.

Locked Containers use `lootBox.itemLevel`. Boosters come from `GetBoosterRarity`.

Source: `LootRarity.GetRarityChances`, `LootRarity.RollRarity`, `LootRarity.GetBoosterRarity`, `LootManager`, `DungeonCompartmentLoot`, `CombatStationFactory.ResolveSkirmishLootRarity`

#### Code: What a Destroyed NPC Ship Drops

`SpaceShipData.CreateLootTable` rolls each of the drops independently. The cargo item is worth up to `RandomRange(3, 10) * 100 * costMultiplier(level)` (integer range, so 3 to 9), ammo up to `RandomRange(1, 3) * 100 * costMultiplier(level)` (1 or 2), and the credit drop is `random(2.5, 3.25) * 100 * costMultiplier(level)`. The commendation tag block is an `if / else if`: in quadrant 2 a failed 20% roll falls through to the 10% system-level-10 roll. Station defense platforms use `CombatStationPartData.CreateLootTable` (40% weapon drop, `RandomBool(0.55f)` for the lowest-rarity filter).

Credit drops, using `GetCreditsValue`:

```
GetCreditsValue(base, L) = round(base * 100 * CostMultiplier(L))
```

The bounty credit reward is `GetCreditsValue(25, L)`, before the Escalation bonus.

Source: `SpaceShipData.CreateLootTable`, `SpaceShipData.GetEquipedModulesForLootTable`, `LootRarity.SelectDroppableEquipment`, `SpaceShipData.RollShipBlueprintPieceLoot`, `GameMath.GetCreditsValue`, `GameMath.CostMultiplier`, `BountyBoard.AddBountyRewards`

### Code: XP Rewards

```
XP per kill = ceil( 5 * baseExperienceReward * levelFactor * 2^(ownLevel / 9) ) * rankPointMultiplier
```

`baseExperienceReward` is a prefab value that defaults to 10. In the 0.8.3.5 `SpaceShip.allShips` dump it is 10 on 45 of the 159 ship prefabs and ranges from 2 to 110 on the rest. `LootManager.AddKillExperience` reads it through `unit.baseExperienceReward`. `ownLevel` is your effective level. `levelFactor` depends on the target's unit level compared with yours.

XP needed to finish a level is `1000 * 2^(level / 5)`. Kill XP grows by `2^(1/9)` per level while the need grows by `2^(1/5)`. The XP-per-level curve and the kill XP formula are read from `GameMath` and `AbstractPersonnelData.GiveExperience`; vanguard (past max level) XP uses a 0.25 scale on the same curve (`CaptainData.maxVanguardExperience`).

Source: `GameMath.GetExperienceRewardValue`, `GameMath.ExperienceRewardScale`, `GameMath.GetMaxExperienceForLevel`, `LootManager.AddKillExperience`, `AbstractPersonnelData.GiveExperience`, `UnitRankHelper.GetAdjustedPoints`

### Code: Player-Side Progression That Scales Combat

| What | Code |
|---|---|
| Captain level step | `FleetLevelSteps`, one step per 5 levels from 10 |
| Unit level of your ship | `HpBalanceMultiplier(level)` applies to your own ship HP, and `powerLevel(level)` scales your `Precision` into crit chance (`0.03 + GetPrecisionCrit()`, where the precision part is `0.05 * Precision / powerLevel` and anything above 0.05 is compressed by `0.05 + (1 + excess)^0.75 - 1`) |

Source: `GameMath.ApplyItemLevelCap`, `FleetLevelSteps`, `AbstractUnit.ApplyHpBalanceBonus`, `AbstractUnit.GetPrecisionCrit`
