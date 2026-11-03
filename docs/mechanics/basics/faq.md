# Common questions

Short answers to questions players ask about the systems you meet from a new game: damage, ship stats, enemy strength, loot, combat stations and boarding. Each answer ends with a link to the section with the full numbers and how they were read from the code. Values are for game version 0.8.3.4. Terms are explained in the [glossary](glossary.md).

## Damage

### How is damage calculated?

A hit starts as your turret's power, gets a random roll, is boosted by your bonuses, is cut by the target's resistances, and is then spent on the target's shield, armor and hull in that order.

1. The roll: turret power / 5, times a random number from 0.8 to 1.25, divided by the weapon's shots per second. Beams skip the division and deal power / 5 per second of firing.
2. Crits: each crit multiplies the shot by 2 + Critical Damage.
3. Your bonuses: multiply by 1 + the type damage stat (Kinetic, Energy and so on) + Damage.
4. Target resistance: multiply by max(0.2, 1 - Damage Reduction - resistance to the shot's type).
5. Shield: absorbs first, multiplied by your damage-versus-shield bonus if you have one. Shields treat all seven damage types the same.
6. Armor: takes what is left, multiplied by your damage-versus-armor bonus. Armor Plating resists 15%, except against Corrosion, which it takes at +25%.
7. Hull: takes the rest. At zero hull the unit is destroyed, unless it is a ship that becomes boardable instead.

Worked example: a level 30 Standard Medium Autocannon rolls a main Combat Power of 400 to 420; say 410. Your ship has +10% Kinetic Damage and +5% Damage and no crits. The target has Damage Reduction 10%, Kinetic Resistance 20%, a shield of 1000 and 500 Armor Plating.

| Step | Value |
|---|---|
| Average base per shot | 410 / 5 / 2.331 = 35.2 (rolls 28 to 44) |
| After bonuses | 35.2 x 1.15 = 40.5 |
| After resistance (1 - 0.10 - 0.20 = 0.70) | 28.3 |
| Shield (1000) | about 35 shots to break |
| Armor (500), 28.3 x 0.85 = 24.1 per shot | about 21 shots to break |
| Hull | 28.3 per shot |

At 2.33 shots per second the shield alone lasts about 15 s.

Details: [The path of a hit](combat-damage.md#the-path-of-a-hit) and [the worked example](combat-damage.md#a-worked-example).

### How much damage per second does a weapon do?

On average one turret deals turret power / 5 x 1.025 damage per second, before crits and bonuses. The 1.025 is the mean of the 0.8 to 1.25 roll. This holds for every weapon family: the reload downtime is already inside the shots per second, so a weapon's rate of fire tells you how fine-grained its damage is, not how much there is. What separates two weapons is the power multiplier (0.9 to 1.15), range, projectile behavior, damage type and size.

Turret power has two parts. The weapon's own power stays with that weapon, multiplied by your Combat Power multipliers. Everything else that adds Combat Power (optional rolls on any item, flat skill amounts, crew, hull upgrades) goes into one pool that is split between your active turrets by size: Tiny 0.45, Small 1, Medium 2, Large 3. A Large turret gets three times the pooled share of a Small one. A turret you disable leaves the pool.

A weapon's own power is a roll times the item's power level times its rarity multiplier. The roll is 1.0 to 1.05 for Small, 2.0 to 2.1 for Medium and 3.0 to 3.15 for Large. The power level is 25 x 2^(level / 10) up to level 100: 50 at level 10, 100 at 20, 200 at 30, 400 at 40 and 800 at 50. Rarity multipliers are Standard 1.0, Enhanced 1.1, High Grade 1.2, Exotic 1.35 and Legendary 1.5.

Attack Speed, Reload Speed and Magazine Size raise the number of shots without shrinking each shot, so they are real damage.

Details: [Where a turret's damage comes from](combat-damage.md#where-a-turrets-damage-comes-from) and [Weapon power by level](combat-damage.md#weapon-power-by-level).

### Should I stack crit chance, crit damage or plain damage?

Raise crit chance first, and add crit damage once the chance is high. Critical Damage only pays in proportion to the chance: at the base 3% crit chance, one point of Critical Damage is worth about 0.03 points of damage, while one point of Critical Chance is worth about 1.

Your crit chance is (3% + the chance from Precision + flat Critical Chance) x the Critical Chance multiplier. Precision is measured against your gear's power level: Precision equal to one power level gives +5% crit chance, and past +5% the gain is damped (raw 10% becomes 8.7%, raw 20% becomes 16.1%, raw 50% becomes 37.2%). A crit multiplies the shot by 2 + Critical Damage.

Plain Damage and the type damage bonuses all add into one 1 + D term, so each point is worth less as you stack more. A Combat Power multiplier never diminishes: it is worth exactly its own percentage. Critical Recursion lets one hit crit more than once, each extra chance halved, which makes chance worth more still.

Details: [Crit chance, crit damage or plain damage](combat-damage.md#crit-chance-crit-damage-or-plain-damage) and [Critical hits](combat-damage.md#critical-hits).

### What do damage types do against shields, armor and hull?

Almost nothing, with one exception. Shields ignore the damage type: they have no resistance and no weakness. Hull has no type weakness either. The type only matters in two places: it picks which of your damage bonuses applies, and it picks which of the target's resistances applies.

The exception is armor. The only armor in 0.8.3.4 is Armor Plating, which resists 15% of everything except Corrosion, which it takes at +25%. Corrosion therefore hits armor for 1.25 against 0.85 for any other type, about 47% more damage. In armor points, 100 armor lasts about 118 points of ordinary damage but only 80 points of Corrosion. Only the Corropult deals Corrosion directly.

A laser painter turret or drone strips 10 points of armor resistance (15% to 5%) and 5 points of Damage Reduction from its target for 6 seconds, and it helps your whole fleet. It is worth bringing against heavily armored targets.

Pick the type the target resists least. No turret deals Heat directly: it only comes from torpedoes, Cannon splash and Heat-type extra damage.

Details: [Damage types](combat-damage.md#damage-types), [Armor](combat-damage.md#7-armor) and [Against armor](combat-damage.md#against-armor).

### How do resistances stack, and what is the cap?

Damage Reduction and the type resistance are added together, and the sum is capped at 80%. A hit is never cut below 20% of its size. A ship with 40% Damage Reduction and 50% Kinetic resistance takes the same Kinetic damage as one with 20% and 60%, and a further point of either does nothing.

Below the cap each point of either removes one point of the hit. Damage Reduction works against all seven types and a resistance only against its own, so Damage Reduction is the better point when attackers mix types and a resistance is better when one type does most of the damage. Resistances are flat additions in a sum, not multipliers.

Both stats can go negative (the painter, and the Escalation debuff on your ships), which raises incoming damage above 100% with no upper limit.

Details: [The resistance cap and stacking](combat-damage.md#the-resistance-cap-and-stacking) and [Target resistance](combat-damage.md#4-target-resistance).

### How fast do shields recharge?

With no bonuses a shield starts refilling 5 seconds after the last hit and then recovers 4% of its maximum per second, so 25 seconds from empty after the delay. Every hit resets the 5 second delay, even when the shield is already down. That is why weapons that never stop firing keep a shield down for good, and a burst that breaks the shield helps less than a steady stream.

Shield Recharge Rate stacks on a base of 1, so a +0.10 bonus makes the rate 2.1 instead of 2, which is 5% faster, not 10%. Flat Shield Regeneration runs on top of this at all times, with no delay. These recharge numbers are a direct reading of the code and were not observed in game.

Details: [Shields](combat-damage.md#shields).

## Ship stats and gear

### What decides my ship's stats, and where do bonuses come from?

Each stat is (base + the sum of flat amounts) x the product of all multipliers. Flat amounts add. Multipliers multiply each other, so a x1.10 and a x1.20 give x1.32, not x1.30. A rolled item stat is either flat or a multiplier, not both.

Bonuses come from:

| Source | Notes |
|---|---|
| Every equipped item | An item registers all its stat lines with the whole ship. A Weapon Range roll on a reactor or hull piece applies to every turret |
| Hull upgrade path | Combat +2% Combat Power per node, Torpedo +2% Torpedo Power, Drones +2% Drone Power, Defense +1% hull, armor and shield each. Milestones at nodes 10, 20 and 30 |
| Skills, mastery, officers | Commander skill nodes, Offense mastery (Combat Power x(1 + 0.005 x mastery level)), Defense mastery (+0.1% Damage Reduction per mastery level), and officers' chosen stat |
| Hull badge and size | A Combat hull gets Combat Power x1.3 and +3% Critical Chance. Weapon Range rises with hull size 1 to 8, up to +2.0 for a size 8 Combat hull |
| Item aspects | Extra effects on weapons and modules, for example +25% reload or +5% Damage Reduction |
| Temporary boosts | Ability buffs and debuffs, each with a duration |
| Crew | On a player ship, crew types add to Combat Power, scaled by the crew effectiveness skill |

Damage, the type damage stats (Kinetic, Energy and so on), the damage-versus-shield and damage-versus-armor bonuses, Critical Chance and Damage Reduction never roll on gear in 0.8.3.4. They come from aspects, skills, hull badges, mastery, hull upgrade milestones and officers.

After the bonuses are added up, the game applies the reactor energy modifier (player ships only), then a level-based HP multiplier on hull, armor and shield.

Details: [How a stat is calculated](combat-damage.md#how-a-stat-is-calculated), [Where bonuses come from](combat-damage.md#where-bonuses-come-from) and [What the Damage stat is](combat-damage.md#what-the-damage-stat-is).

### Which stats affect drones, torpedoes and fighters?

Drones use the same turret rules as your ship. A drone weapon's power is its own main power x your Combat Power multiplier, plus your Combat Power split by the drone's size rating, plus your Drone Power divided by the base drone count. Drones also read your Damage, type bonuses, Critical Damage, range, attack speed and resistances directly. They do not read your Critical Chance: they work it out from their own Precision. The Drone Defense skill and the second Drones hull milestone (+15%) add Damage Reduction to drones.

Torpedoes always deal Heat damage. Torpedo power is your Torpedo Power plus your normalized Combat Power, rolled as power / 5 x random(0.8, 1.25) with no division by shots per second. Crits, Damage and Heat Damage come from the launching ship. A torpedo explodes in a radius of 6 on impact, with falloff down to a minimum of 10%, and hits every enemy in range including the target, using the damage left after the direct hit. A Small bay waits 8 s between launches and 4 s to reload. A Medium bay waits 1 s and 3 s.

Fighters from a hangar bay are ships with their own turrets and follow the ship rules. Hangar Build Speed sets how fast they are repaired and rebuilt.

The reactor energy modifier does not touch Drone Power or Torpedo Power.

Details: [Drones, torpedoes, fighters](combat-damage.md#drones-torpedoes-fighters).

### What does the reactor do, and what happens if I go over its energy?

Nothing is drained and nothing switches off. The reactor's Energy stat is a budget. Usage is the energy draw of your connected equipment divided by that capacity, and usage sets a multiplier on your Combat Power:

| Usage | Multiplier |
|---|---|
| up to 50% | x1.20 |
| up to 75% | x1.10 |
| up to 100% | x1.00 |
| up to 125% | x0.75 |
| up to 150% | x0.50 |
| above 150% | x0.25 |

Keep usage at or under 50% for the bonus and at or under 100% to avoid the penalty. The multiplier covers every turret and the pooled flat power, but not Drone Power or Torpedo Power. It runs on player-owned ships only: NPC ships and drones ignore the reactor. The Divert Power skill adds to the multiplier of the 50% bracket.

A higher-level reactor has more capacity while weapon draws are fixed numbers, so it carries more weapons. Rarity does not raise a weapon's energy draw (see [What is gear rarity worth?](#what-is-gear-rarity-worth)).

Details: [Energy and the reactor](combat-damage.md#energy-and-the-reactor).

### Which stats shown in game do nothing?

- Shield Recharge Delay and Shield Gate Time: shown, but the game never reads them in this version. The shield delay is always 5 seconds.
- Resistances past the 80% combined cap.
- Range bonus on station parts: it is never switched on.
- A weapon's shots per second as a measure of its damage: damage per second follows power, not shots.
- Heat damage as a loadout: no turret deals it directly.

Details: [What looks useful and is not](combat-damage.md#what-looks-useful-and-is-not).

## Enemy strength

### How much tougher is an enemy five or ten levels above me?

Below level 30, an enemy 5 levels higher has about 1.3 to 1.4 times the HP and hits 1.4 times as hard, and one 10 levels higher is about double on both. From level 30 the damage ramp kicks in and from level 40 the HP curve does, so the gap hurts more. Past level 70, a 10-level gap multiplies HP by 4.5 to 5. Damage from level 30 on is x1.56 for 5 levels and x2.44 for 10.

| Starting level | HP, +5 levels | HP, +10 levels | Damage, +5 | Damage, +10 |
|---|---|---|---|---|
| 10 | x1.30 | x1.72 | x1.42 | x2.00 |
| 30 | x1.57 | x2.53 | x1.56 | x2.44 |
| 60 | x1.83 | x3.35 | x1.56 | x2.44 |
| 80 | x2.20 | x4.86 | x1.56 | x2.44 |

These ratios assume a size 5 hull, a Large Standard hull module, the Frontier tier (tier 0) and the same ship rank and rarity, and they are worked out from the formulas, not measured in play. At damage x2.44 you need about 2.4 times more damage to keep pace and must survive 2.4 times more damage taken.

Details: [How much tougher is an enemy five or ten levels higher](scaling.md#how-much-tougher-is-an-enemy-five-or-ten-levels-higher).

### What are ship ranks worth, and which will I meet?

A ship rank multiplies an enemy's hull, armor and shield HP and adds to its fire rate and reload. It does not change damage per shot, so a Legendary is a damage sponge that fires up to 30% faster, not a harder hitter.

| Ship rank | HP | Fire rate and reload | Kill XP multiplier |
|---|---|---|---|
| Rookie | x0.7 | -30% | x1.0 |
| Regular | x1.0 | -10% | x1.2 |
| Veteran | x1.3 | +10% | x1.3 |
| Elite | x1.5 | +15% | x1.5 |
| Champion | x1.9 | +20% | x1.6 |
| Commander | x2.4 | +30% | x2.0 |
| Legendary | x3.0 | +30% | x2.4 |

In levels of HP, a Champion at level 60 has about as much HP as a Regular enemy 5.4 levels higher. Ship ranks matter more at low levels: at level 20 a Champion is worth +10.2 levels.

What you meet in normal generated sites:

- Levels 1 to 5: Rookie only.
- Levels 6 to 11: Rookie and Regular.
- Level 12 and up at Escalation tier 0: up to Veteran. At level 60 the split is Rookie 26.4%, Regular 26.8%, Veteran 46.8%.
- Elite from Escalation tier 4 (Havoc), Champion from tier 6 (Maelstrom).
- Commander and Legendary never come from this roll.

Bounty bosses are always Elite or better. Champions can appear from level 40, Commanders from level 70 and Legendaries from level 91. Escalation does not change boss ranks.

Details: [Rank table](scaling.md#rank-table), [Ship rank chances for normal enemies](scaling.md#ship-rank-chances-for-normal-enemies), [Boss ranks](scaling.md#boss-ranks) and [How much a ship rank is worth in levels](scaling.md#how-much-a-ship-rank-is-worth-in-levels).

### What does Escalation change?

Escalation is a difficulty setting you choose, from tier 0 to tier 8. Nothing I read changes it for you: only the Escalation popup's confirm button does, and you can move it up or down at any time. A tier stays locked until your player level reaches its unlock level.

| Tier | Unlock (player level) | Enemy HP | Enemy damage | Enemy forces | Mission credits | Mission XP | Your resists |
|---|---|---|---|---|---|---|---|
| 0 Frontier | 0 | x1.00 | x1.00 | x1.0 | +0% | +0% | 0 |
| 1 Static | 0 | x1.15 | x1.08 | x1.1 | +10% | +4% | 0 |
| 2 Fracture | 0 | x1.35 | x1.18 | x1.2 | +20% | +8% | 0 |
| 3 Breach | 10 | x1.60 | x1.30 | x1.3 | +30% | +12% | 0 |
| 4 Havoc | 25 | x2.80 | x1.50 | x1.5 | +50% | +20% | -20% |
| 5 Onslaught | 50 | x3.80 | x2.00 | x1.7 | +75% | +30% | -40% |
| 6 Maelstrom | 75 | x5.00 | x4.00 | x1.9 | +100% | +40% | -60% |
| 7 Cataclysm | 100 | x6.20 | x6.00 | x2.1 | +125% | +50% | -75% |
| 8 Extinction | 100 | x8.00 | x10.00 | x2.3 | +150% | +60% | -85% |

"Enemy forces" multiplies the points budget of generated hostile sites, so higher tiers bring more enemies. "Your resists" is subtracted from all seven of your resistances on every ship you own. From tier 4, each kill also has a chance to drop a Combat Upgrade Kit: 0.2% at tier 4, rising to 1.0% at tier 8. Converted to levels, tier 4 HP equals about +15 levels and its damage about +6; tier 8 is about +30 levels of HP and +33 of damage. The popup also has an override with toughness and damage sliders from x1.0 to x4.0 that multiply the tier value.

Details: [Escalation](scaling.md#escalation) and [What escalation is worth in levels](scaling.md#what-escalation-is-worth-in-levels).

## Loot and XP

### What does a destroyed enemy ship drop?

Each drop is rolled on its own:

| Drop | Chance |
|---|---|
| One of its fitted modules | 12% |
| One of its fitted weapons | 12% |
| One cargo item (not ammo) | 30% |
| Ammo | 10% |
| Credits | 30% |
| Blueprint | 4% (Elite or above asks for at least High Grade) |
| Ship blueprint piece | 5% (only for ship classes you have not unlocked) |
| Officer upgrade (High Grade) | 1% (level 30+) |

A dropped module or weapon is one the ship carries, so its rarity follows the enemy gear table. In 95% of cases the drop is picked from the lowest-rarity fitted items, so a ship with one Legendary and one Standard module almost always drops the Standard. A credit drop is round(base x 100 x credit multiplier for the level): 472 credits at level 10, 1,270 at 30, 5,607 at 60 and 40,630 at 100.

Details: [What a destroyed NPC ship drops](scaling.md#what-a-destroyed-npc-ship-drops).

### What rarity of gear will I find at my level?

Loot boxes, dungeon compartments and research files roll rarity from a weighted table by item level:

| Level | Standard | Enhanced | High Grade | Exotic | Legendary |
|---|---|---|---|---|---|
| 1-10 | 57.1% | 42.9% | | | |
| 11-24 | 44% | 33% | 23-24% | | |
| 25 | 38.6% | 28.9% | 21.7% | 10.8% | |
| 40 | 37.7% | 28.3% | 22.6% | 11.3% | |
| 60 | 36.7% | 27.5% | 23.9% | 11.9% | 0 |
| 80 | 35.4% | 26.5% | 24.8% | 12.4% | 0.9% |
| 100 | 34.2% | 25.6% | 25.6% | 12.8% | 1.7% |

High Grade starts at level 11, Exotic at level 25 and Legendary at level 60.

Enemy gear is much richer than box loot at high level. Each module slot and hardpoint on an NPC ship rolls its own rarity: at level 20, 16.7% High Grade and no Exotic; at level 60, 15% Exotic and 5% Legendary; at level 80, 20.8% Legendary; at level 100, 32.1% Legendary. Enemy gear is not limited by your level.

An item's level is the location level plus a roll of -1, 0 or +1, and at least 1. Items built for your faction are capped relative to your effective level: an item 10 levels above you becomes 6 above you, and 30 above becomes 11. Loot boxes are not capped.

Details: [Rarity of loot-box and dungeon gear](scaling.md#rarity-of-loot-box-and-dungeon-gear), [Which rarity NPC gear has](scaling.md#which-rarity-npc-gear-has) and [Item level](scaling.md#item-level).

### What is gear rarity worth?

Rarity is free power: it multiplies almost every stat on the item and does not raise its energy draw. A Legendary weapon draws the same energy as a Standard one of the same level and size, with 50% more main stat. The extra bonus stat lines and aspect slots come on top.

| Rarity | Main stat | Bonus stat lines | Worth in item levels |
|---|---|---|---|
| Standard | x1.00 | 0 | 0 |
| Enhanced | x1.10 | 1 | 1.4 |
| High Grade | x1.20 | 2 | 2.6 |
| Exotic | x1.35 | 3 | 4.3 |
| Legendary | x1.50 | 3 | 5.9 |

Item level, by contrast, raises power and energy draw together. A level 54 Legendary weapon has about the power of a level 60 Standard one with a lower energy draw. Reactor capacity is the exception to the rarity multiplier: it takes only the square root, so Legendary gives x1.22.

Details: [Rarity](scaling.md#rarity) and [What gear rarity is worth](scaling.md#what-gear-rarity-is-worth).

### How much XP does a kill give?

XP per kill is ceil(5 x base XP reward x level factor x 2^(your level / 9)) x the ship rank multiplier. The base XP reward is 10 unless a ship overrides it. Your level is your effective level, and the level factor depends on the target's level against yours:

| Target vs you | Level factor |
|---|---|
| Up to 3 levels above | 1 + 0.1 per level (up to 1.3) |
| Same level | 1.0 |
| 1 / 2 / 3 below | 0.8 / 0.6 / 0.4 |
| 4 or more below | 0.00001 (effectively nothing) |

So XP farming needs enemies close to your level. A higher ship rank pays more: Elite x1.5, Champion x1.6, Commander x2.0, Legendary x2.4. Each level takes more kills than the last, because the XP you need doubles every 5 levels while XP per kill doubles every 9. For a ship of your own level with no ship rank bonus:

| Level | XP per kill | XP to finish the level | Kills per level |
|---|---|---|---|
| 10 | 108 | 4,000 | 37 |
| 30 | 504 | 64,000 | 127 |
| 50 | 2,352 | 1,024,000 | 435 |
| 100 | 110,598 | 1,048,576,000 | 9,481 |

Details: [XP rewards](scaling.md#xp-rewards).

## Combat stations

### What do the parts of a combat station do?

Only Defense Platforms shoot. Every other part is a hull target with loot.

| Part | In a fight |
|---|---|
| Station Core | Always drops credits. Needed for the station to count as destroyed. It is what a Station Defense mission asks you to protect |
| Energy Field | Hull and loot only |
| Refinery | Hull and loot (ore, canisters, blueprints) |
| Cargo Dock | Hull and loot (credits and refined parts) |
| Docking Pad, Docking Tunnel | Hull and loot. They hold the docking points, which matter only for boardable facilities |
| Connector | Hull and a little loot. Never required for the station to count as destroyed |
| Defense Platform (turret) | Shoots, has the only shields on any part, and can drop one of its turrets |
| Defense Platform (drone bay) | Carries a Drone Bay module and no turrets. It drops nothing from the platform loot table |

Details: [What each part does in a live fight](stations.md#what-each-part-does-in-a-live-fight) and [Every station part](stations.md#every-station-part).

### What does the Energy Field do?

Nothing special. It is a 1350 base hull part with no weapons, no aura and no effect on the other parts. It does not extend turret range for any part. It drops warp fuel and, rarely, Energy Crystals.

Details: [The Energy Field](stations.md#the-energy-field).

### Which part should I destroy first?

Start with the Defense Platforms, because they are the only parts that shoot. Once they are gone, nothing on the station can hurt you.

You do not have to destroy everything. The station counts as destroyed once every part that is not a Connector is gone, so you can skip Connectors. Each part you kill also costs reputation with the owner (see [What does destroying station parts cost in reputation?](#what-does-destroying-station-parts-cost-in-reputation)). Killing a part does not weaken or remove its neighbors: there is no cascade.

Details: [What counts as destroyed](stations.md#what-counts-as-destroyed) and [What each part does in a live fight](stations.md#what-each-part-does-in-a-live-fight).

### How much HP does a station part have?

Hull, armor and shield are each built as (base HP + the HP of equipped modules) x a multiplier. The base hull values are Connector 315 or 735, Core, Cargo Dock and docking parts 900, and Refinery and Energy Field 1350. Defense Platforms have hull 450 to 900 and the only shields, 150 to 900; the drone bay platform has hull 600 and armor 600.

The multiplier is the Rookie ship rank (0.7) x a level balance x a level scaling x the Escalation tier HP multiplier x a context multiplier. Against the Frontier tier (tier 0), the first three combine to:

| Part level | 10 | 30 | 45 | 60 | 80 | 100 |
|---|---|---|---|---|---|---|
| Combined multiplier | 0.70 | 0.83 | 1.22 | 1.81 | 3.20 | 5.73 |

The context multiplier is x10 for a boardable facility and x1 for every other mission, skirmish and pirate station, and faction outposts. Stations in war-zone systems are far tougher; that is covered by the [Conquest stage](../conquest/README.md), which opens at level 28. The modules add HP on top of the base values, so treat the tooltip number as the truth and these terms as the floor.

Details: [How tough a part is](stations.md#how-tough-a-part-is) and [Context multiplier](stations.md#context-multiplier).

### What does a destroyed station part drop?

Loot is rolled when the part dies and differs on every kill. In auto-play (idle) mode each non-fixed item has a 50% chance to be skipped.

| Part | Drop |
|---|---|
| Station Core | Always credits. 30% for a blueprint of the part's level |
| Cargo Dock | The refined-part roll, plus 70% for credits |
| Energy Field | 50% for High Grade warp fuel. Above level 25, 4% for Energy Crystals |
| Refinery | 50% for ore of this system. 30% for a blueprint |
| Docking Pad, Docking Tunnel | Refined parts (Titanium Plate, Bulkhead, Hull Beam, Structural Component) |
| Connector | 70% for one refined part, at half the amount |
| Defense Platform with turrets | 40% for one of its turrets, and 20% for a stack of ammo |
| Defense Platform with a drone bay | Nothing from the table |

Killing a part also pays XP: 50 x the level-gap factor x 2^(effective level / 9), rounded up. Industrial Outpost stations pay no loot, XP or reputation change. Faction skirmish stations, pirate hideouts and the harder Station Offense and Defense missions add extra gear and a skill point item to the Core.

Details: [What drops when a part dies](stations.md#what-drops-when-a-part-dies) and [Extra loot on mission and skirmish stations](stations.md#extra-loot-on-mission-and-skirmish-stations).

### What does destroying station parts cost in reputation?

Every part you kill hits the owner's reputation, unless the part is exempt (several mission stations are). If your reputation with the owner is above -500 (Neutral or better), each part costs 100 and logs "Betrayal! -100 reputation". At -500 or below (Wary or worse), each part costs 5. Below -500 the faction also counts as your enemy, so you can target its parts with a plain click.

Destroying a whole faction station is a second, bigger cost. Wrecking applies to faction stations (Defense stations and economic outposts such as Construction Yards, refineries and production stations) when you contributed to the kill by hitting any part. It costs 5000 reputation, or 1000 if you are already Hostile or worse with the owner, and the log reads "Station destroyed! -N reputation". Without your contribution there is no loot and no reputation cost. A wrecked station leaves a "Wreckage of {station}" salvage site for 3600 seconds (1 hour), holding 10% of each item stack that was in its storage, if anything recoverable remains.

If the faction that owns the system you are in is an enemy of the part's owner and you are Neutral with it, you gain 1 reputation with it per kill.

Details: [Killing parts](stations.md#killing-parts) and [Wrecking a faction station](stations.md#wrecking-a-faction-station).

### Can I attack a station that is not hostile to me?

Only at combat-type locations. Ctrl + left-click on a part that is not hostile to you ("force attack") works when all of these are true: the part is not already hostile to you, you are in a sector in a quadrant above 1, and the location you are in is not one of the space station kinds. Faction stations, embassy stations, player stations, Industrial Outposts and every guild or neutral station are space station kinds, so you cannot force-attack them. That leaves mission, skirmish and pirate stations and boardable facilities.

When you attack a non-hostile part it becomes hostile to you, and damaging any unit of a faction that is not your enemy marks every unit of that faction at that location hostile. The reputation cost is the same as the kill penalty above. Parts that are already hostile need only a plain click.

Details: [Ctrl-click on a friendly station](stations.md#ctrl-click-on-a-friendly-station).

### Why is a station I damaged back at full health?

Damage and kills are saved, but a station is rebuilt from its seed, with every part at full HP, when more than 3600 seconds (1 hour) of game time have passed since you last visited it. The rebuild happens the next time the game saves. Two more rebuild triggers apply to stations in war-zone systems once the war opens at level 28; see the [Conquest stage](../conquest/README.md).

Details: [How long damage lasts](stations.md#how-long-damage-lasts).

## Boarding

### How do I get a ship to become boardable?

A ship becomes boardable only when you shoot it down to a wreck state, not when it is merely weak. Once a hostile ship's hull is under 40%, every 15% of max hull in damage taken below that line gives one roll. With no EMP charge each roll is a base chance of 0.005 times 1/3 at 40% hull, 2/3 at 20% and 1 at 0%, so about 0.17% to 0.5%. Flying manually doubles that. Under autopilot the doubling is skipped and half the rolls are skipped instead. A ship gets at most about 2.7 rolls, so unassisted boardings are rare.

EMP (electromagnetic pulse) weapons build up an EMP charge on the target, and a full charge raises the base chance to 0.025, five times higher. EMP weapons are what make boarding likely.

A ship cannot become boardable if it is flagged as unboardable, is a carrier fighter, is a Drone-size ship, is at an Industrial Outpost location, or has an empty loot list. Left-clicking a boardable ship ("LMB to board") opens the boarding panel. Ctrl+click attacks it instead: the ship revives with at least 15% hull, can never be boarded afterwards, and any capture mission tied to it fails.

Details: [Ships](boarding.md#ships).

### What do the numbers on the boarding tooltip mean?

On a ship under attack the tooltip shows the live estimate, the number of attackers versus defenders (counting your crew still waiting in staging), Ship Integrity, and the average morale of each side.

The Estimate compares total power: ratio = your power / their power (their power counted as at least 1), and win chance = ratio / (ratio + 1). It is shown as a label:

| Win chance | Label |
|---|---|
| above 75% | Favourable |
| above 50% | Contested |
| above 25% | Difficult |
| 25% or below | Very Difficult |

This is a power ratio, not a simulation, and it is not a win probability. A 50% estimate means equal power, not equal outcomes. It ignores HP, positions, specialists and events. Wounded crew count half, Panicked 60% and Heroic 140%.

The estimate in the crew-selection panel, before you start, is cruder. It counts every defender as 3 power times the faction's defense multiplier, so it undercounts Marine-heavy factions and defenders at a level gap. It also leaves out your Assault Doctrine skill, so you are usually a little stronger than it says. A facility with hostility None is always Favourable, 100%, because it has no defenders.

The facility tooltip adds Hostiles (the expected resistance, from "No hostiles expected" to "Expect heavy resistance"), Specialist, Crews, Progress (friendly rooms over total rooms) and Integrity.

Details: [What each tooltip number means](boarding.md#what-each-tooltip-number-means) and [The Estimate formula](boarding.md#the-estimate-formula).

### Which crew should I bring to board a ship?

Marines first. Compartment capacity limits how many units fit and fight, not their power, so the crew with the best power and HP per slot wins.

| Crew type | Boarding power | Resistance | HP | Cost (credits) |
|---|---|---|---|---|
| Deckhand | 1 | 1 | 6 | 3,500 |
| Gunner | 2 | 3 | 8 | 7,000 |
| Marine | 4 | 8 | 13 | 16,000 |
| Pilot | 2 | 2 | 7 | 22,500 |
| Miner | 1 | 2 | 7 | 7,000 |
| Salvager | 1 | 2 | 7 | 7,000 |

A Marine has four times a Deckhand's power and over twice the HP for under five times the price. A room that holds six units holds 24 power of Marines and 6 of Deckhands. Pods carry 10 units each, so sending 21 units launches three pods, the last with 1 unit.

Compare the live Estimate with the pre-start one: the pre-start number understates strong defenders. Skills help your units only: Assault Doctrine (+10% boarding power per point, up to +30%) and Crew Conditioning (+10% crew resistance per point, up to 3 points).

Details: [Crew types](boarding.md#crew-types), [Picking crew for a ship](boarding.md#picking-crew-for-a-ship) and [Skills and gear](boarding.md#skills-and-gear).

### Which crew should I bring into a facility?

Count specialists first. The Specialist row of the facility tooltip lists the crew types that count. With a specialist in the room, ambushes are 30% smaller and hazard damage drops by up to 50%, and even one specialist gives a 33% chance to prevent each negative event outright at a level gap under 3.

- Bring the first-listed specialist for the loot. For example Miners at a Mining Outpost, and Marines at Relay and Military facilities. The second-listed type opens locks and shields you from events but misses the profession loot.
- Locked rooms need a specialist in an adjacent room, and they hold the best loot. Winning does not require them.
- At an Ancient Ruin, bring 3 or more distinct crew types. Its bonuses depend on it.
- Armed facilities use the ship combat rules, so bring Marines. Military Outpost defenders are 70% Marines.
- Do not overstack one room. Investigation speed is 1 + 0.15 x (n - 1) for n units, and capacity is 3 to 6 per room, so six units cover a room in 57% of the time of one unit, not 17%.

Details: [Picking crew for a facility](boarding.md#picking-crew-for-a-facility).

### Should I board Silent, Balanced or Aggressive, and which ammo?

Use Silent on large ships where defenders are spread out and you clear room by room, and Aggressive only when you are strong enough that speed matters more. Armor Piercing is the better ammo against Marine-heavy factions. Hollow ammo wastes 20% against Marines and Gunners.

| Option | Effect |
|---|---|
| Silent | Your power x0.8, casualty rate x0.75. Rooms next to yours go Alert after about 350 s on average |
| Balanced | No change. About 70 s before neighboring rooms go Alert |
| Aggressive | Your power x1.2. About 35 s |
| Hollow ammo | 0.8x damage to Marines and Gunners, structural damage per kill 1.5 |
| Standard ammo | 1.0x, structural damage per kill 2 |
| Armor Piercing (AP) | 1.2x to Marines and Gunners, structural damage per kill 2.5 |

Silent's net effect on a single fight is close to neutral: you deal less but take 25% fewer hits. What it buys is time.

Details: [Ammo and stealth](boarding.md#ammo-and-stealth) and [Picking crew for a ship](boarding.md#picking-crew-for-a-ship).

### How much tougher are defenders at a higher level?

Defenders get an HP multiplier from how far the location's level is above yours (0 if it is not above). It applies to their HP at spawn and to the number of units in a combat ambush, not to their power.

| Level gap | HP multiplier |
|---|---|
| 0 | 1.00 |
| 1 | 1.12 |
| 3 | 1.40 |
| 5 | 1.73 |
| 7 | 2.12 |
| 10 | 2.83 |
| 15 and above | 4.00 |

A gap also shifts the defender mix toward Marines, up to 80% at a gap of 15. Ships use your effective level and facilities use your player level. A facility whose level is 10 or more above your effective level cannot be started at all. Some factions are hard-boarding: their ships are simulated at least 10 levels above you, unless the boarding comes from a capture mission. That puts their HP multiplier at about 2.8.

Details: [Level scaling of defenders](boarding.md#level-scaling-of-defenders) and [Hard-boarding factions](boarding.md#hard-boarding-factions).

### When should I call reinforcements?

Only when the Airlock and the rooms beside it have free capacity. New crew enter through the Airlock, up to its capacity, and wait in staging otherwise. The button reads Add Attackers for ships and Call Reinforcements for facilities. Pressing Send takes the selected crew out of your flagship's crew at once, and they return only if they survive and your ship has free crew space.

On ships the enemy can call a wave too, if the Enemy Reinforcements event was rolled. It lands in your entry point, the Airlock, 4 to 24 seconds into the fight, even if you hold that room. A strong first wave in the Airlock room matters more than depth in the ship. Wounded units leave the fight and recover in staging at about 1.3% of max HP per second, so a long fight heals your first wave there.

Details: [Reinforcements](boarding.md#reinforcements) and [When to call reinforcements](boarding.md#when-to-call-reinforcements).

### How do I lose fewer crew?

- On ships, put the Bridge on priority early. Holding it gives +0.02 morale per second and is required for mass surrender.
- Keep your rooms connected to the Airlock. A room cut off from it by non-friendly rooms costs 0.05 morale per second.
- Barricade secured rooms next to a front: -35% damage taken there.
- Carry a Body Armor: -10% casualty rate.
- Take Assault Doctrine and Crew Conditioning if you board often.
- Throw grenades into rooms with defenders and none of your crew. Each does 3 damage to everyone in the room and dazes them for 15 s. It also sets the room to Alert and costs 5% of the structure.
- Do not let a ship's integrity reach zero. Without a won fight it explodes 15 seconds later.
- If the fight is lost, retreat. Retreating crew take no combat damage. At a facility, retreat while integrity is still above 10% if hazards keep firing: a retreat keeps your crew and half the loot, and the facility stays enterable.

Details: [What lowers your losses](boarding.md#what-lowers-your-losses) and [Morale](boarding.md#morale).

### What does boarding cost in reputation?

If the target's faction has a reputation with you above -500 (Neutral or better), you pay when you press start, before the approach finishes. The penalty is ceil(base XP x 0.5) x 40. Any facility costs 520 (base 25). A ship uses its own base XP, so a ship with base XP 10 costs 200. Cancelling the approach does not refund it. Once your crew lands, the friendly faction is marked hostile to you in that location. Some ships are flagged to skip both the penalty and the hostility.

Details: [Reputation cost of entering](boarding.md#reputation-cost-of-entering).

### What happens when I win or retreat?

Win a ship boarding and you capture the ship: it becomes yours, its crew and commander are removed, its modules are damaged by 1 minus its hull fraction, and all loot goes to your cargo. Win a facility run and all loot goes to your cargo and the clear-the-base mission is claimed. The run stays open after the win until you ask for extraction.

Retreat from a ship and it recovers (at least 15% hull), becomes unboardable and flees. Each non-data loot item has a 50% chance to reach your cargo, and boarding missions fail. A facility retreat follows the same partial-loot rule, data items are always kept and integrity stays reduced.

Details: [How a run ends](boarding.md#how-a-run-ends).

### What looks useful in boarding but is not?

- Estimate percentages are not win probabilities. They map a power ratio through ratio / (ratio + 1).
- The Enemy Dazed event changes no number: every room already starts Dazed.
- The profession discovery bonus is dead data. Only the profession speed bonus is read.
- Each room has an efficiency modifier that drops when it is cut off from the Airlock. It is computed but nothing reads it.
- Cancelling an approach does not refund the reputation penalty.

Details: [What looks useful but is not](boarding.md#what-looks-useful-but-is-not).
