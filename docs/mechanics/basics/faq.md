# Common Questions

Short answers to questions players ask about the systems you meet from the start of a new game: damage, ship stats, enemy strength, loot, credits, combat stations and boarding. Each answer ends with a link to the section with the full numbers and how they were read from the code. The code was read from game version 0.8.3.4 and re-checked against 0.8.3.5, and the answers follow the 0.8.3.5 code. Values stored in the game's assets (weapons, armor, aspects, skill trees, crew, facilities and station parts) were compared with the 0.8.3.5 data dumps and match what was read from 0.8.3.4. The game's text strings were not re-read for 0.8.3.5. Terms are explained in the [glossary](glossary.md).

## Damage

### How Is Damage Calculated?

A hit starts as your turret's power and gets a random roll. Your bonuses then boost it, the target's resistances cut it, and the target's shield, armor and hull take what is left, in that order.

1. The roll: turret power / 5, times a random number from 0.8 to 1.25, divided by the weapon's shots per second. Beams skip the division and deal power / 5 per second of firing. A beam fires for 2 seconds at a time and then reloads.
2. Critical hits: each critical hit multiplies the shot by 2 + Critical Damage.
3. Your bonuses: multiply by 1 + the damage-type stat (Kinetic, Energy and so on) + Damage. Nothing in 0.8.3.5 grants either stat, so in practice this step multiplies by 1.
4. Target resistance: multiply by max(0.2, 1 - Damage Reduction - resistance to the shot's type).
5. Shield: the shield absorbs the hit first. If you have a damage-versus-shield bonus, it multiplies the hit here. Shields treat all seven damage types the same.
6. Armor: the armor takes what is left, multiplied by your damage-versus-armor bonus. Armor Plating resists 15% of every type except Corrosion, which it takes at +25%.
7. Hull: the hull takes the rest. At zero hull the unit is destroyed, unless it is a ship that becomes boardable instead.

Worked example: a level 30 Standard Medium Autocannon rolls a main Combat Power of 400 to 420, and this example uses 410. Your ship has +10% Kinetic Damage and +5% Damage and no critical hits. Nothing in 0.8.3.5 grants those two bonuses, so they stand in for any bonus of that kind. The target has Damage Reduction 10%, Kinetic Resistance 20%, a shield of 1000 and 500 Armor Plating.

| Step | Value |
|---|---|
| Average base per shot | 410 / 5 / 2.331 = 35.2 (rolls 28 to 44) |
| After bonuses | 35.2 x 1.15 = 40.5 |
| After resistance (1 - 0.10 - 0.20 = 0.70) | 28.3 |
| Shield (1000) | about 35 shots to break |
| Armor (500), 28.3 x 0.85 = 24.1 per shot | about 21 shots to break |
| Hull | 28.3 per shot |

At 2.33 shots per second the shield alone lasts about 15 seconds.

Details: [The Path of a Hit](combat-damage.md#the-path-of-a-hit) and [A Worked Example](combat-damage.md#a-worked-example).

### How Much Damage per Second Does a Weapon Do?

On average one turret deals turret power / 5 x 1.025 damage per second, before critical hits and bonuses. The 1.025 is the mean of the 0.8 to 1.25 roll. This holds for every weapon that fires shots. The reload downtime is already inside the shots per second, so a weapon's rate of fire tells you how fine-grained its damage is, not how much there is.

A beam (Plasma Beam, Cryo Lance) deals power / 5 only while its beam is on, 2 seconds at a time, and then reloads for its full reload time. It averages about half of that. What separates two weapons is the power multiplier (0.9 to 1.15), range, projectile behaviour, damage type and size.

Turret power has two parts. The weapon's own power stays with that weapon, multiplied by your Combat Power multipliers. Everything else that adds Combat Power goes into one pool. That includes optional rolls on any item, flat skill amounts, crew and hull upgrades. The pool is split between your active turrets by size, with weights of Tiny 0.45, Small 1, Medium 2 and Large 3. A Large turret gets three times the pooled share of a Small one. A turret you disable leaves the pool.

A weapon's own power is a roll times the item's power level times its rarity multiplier. The roll is 1.0 to 1.05 for Small, 2.0 to 2.1 for Medium and 3.0 to 3.15 for Large. The power level is 25 x 2^(level / 10) up to level 100: 50 at level 10, 100 at 20, 200 at 30, 400 at 40 and 800 at 50. Rarity multipliers are Standard 1.0, Enhanced 1.1, High Grade 1.2, Exotic 1.35 and Legendary 1.5.

Attack Speed, Reload Speed and Magazine Size raise the number of shots without shrinking each shot, so they are real damage.

Details: [Where a Turret's Damage Comes From](combat-damage.md#where-a-turrets-damage-comes-from) and [Weapon Power by Level](combat-damage.md#weapon-power-by-level).

### Should I Stack Critical Chance, Critical Damage or Plain Damage?

Raise Critical Chance first, and add Critical Damage once the chance is high. Critical Damage only pays in proportion to the chance. At the base 3% Critical Chance, one point of Critical Damage is worth about 0.03 points of damage, while one point of Critical Chance is worth about 1.

Your Critical Chance is (3% + the chance from Precision + flat Critical Chance) x the Critical Chance multiplier. Precision is measured against your gear's power level. Precision equal to one power level gives +5% Critical Chance, and past +5% the gain is damped. A raw 10% becomes 8.7%, a raw 20% becomes 16.1% and a raw 50% becomes 37.1%. A critical hit multiplies the shot by 2 + Critical Damage.

Plain Damage and the damage-type bonuses all add into one 1 + D term, where D is their sum. Each point is worth less as you stack more. In 0.8.3.5 nothing grants them, so D is 0 in practice. A Combat Power multiplier never diminishes: it is worth exactly its own percentage. Critical Recursion lets one hit be a critical hit more than once, with each extra chance halved, which makes Critical Chance worth more still.

Details: [Critical Chance, Critical Damage or Plain Damage](combat-damage.md#critical-chance-critical-damage-or-plain-damage) and [Critical Hits](combat-damage.md#critical-hits).

### What Do Damage Types Do Against Shields, Armor and Hull?

Almost nothing, with one exception. Shields ignore the damage type: they have no resistance and no weakness. Hull has no type weakness either. The type only matters in two places. It picks which of your damage bonuses applies, and it picks which of the target's resistances applies.

The exception is armor. The only armor in 0.8.3.5 is Armor Plating, which resists 15% of everything except Corrosion, which it takes at +25%. Corrosion therefore hits armor for 1.25 against 0.85 for any other type, about 47% more damage. In armor points, 100 armor lasts about 118 points of ordinary damage but only 80 points of Corrosion. Only the Corropult deals Corrosion directly.

A Painter Drone (the only painter in the game) strips 10 points of armor resistance (15% to 5%) and 5 points of Damage Reduction from its target for 6 seconds. It helps your whole fleet. It is worth bringing against heavily armored targets.

Pick the type the target resists least. No turret deals Heat directly. Heat only comes from torpedoes, Cannon splash and Heat-type extra damage.

Details: [Damage Types](combat-damage.md#damage-types), [7. Armor](combat-damage.md#7-armor) and [Against Armor](combat-damage.md#against-armor).

### How Do Resistances Stack, and What Is the Cap?

Damage Reduction and the type resistance are added together, and the sum is capped at 80%. A hit is never cut below 20% of its size. A ship with 40% Damage Reduction and 50% Kinetic resistance takes the same Kinetic damage as one with 20% and 60%, and a further point of either does nothing.

Below the cap each point of either removes one point of the hit. Damage Reduction works against all seven types and a resistance only against its own. Damage Reduction is the better point when attackers mix types, and a resistance is better when one type does most of the damage. Resistances are flat additions in a sum, not multipliers.

Both stats can go negative, from the Painter Drone and from the Escalation debuff on your ships. Negative values raise incoming damage above 100% with no upper limit.

Details: [The Resistance Cap and Stacking](combat-damage.md#the-resistance-cap-and-stacking) and [4. Target Resistance](combat-damage.md#4-target-resistance).

### How Fast Do Shields Recharge?

With no bonuses a shield starts refilling 5 seconds after the last hit and then recovers 4% of its maximum per second. That is 25 seconds from empty after the delay. Every hit resets the 5 second delay, even when the shield is already down. That is why weapons that never stop firing keep a shield down for good, and a burst that breaks the shield helps less than a steady stream.

Shield Recharge Rate stacks on a base of 1, so a +0.10 bonus makes the rate 2.1 instead of 2, which is 5% faster, not 10%. Flat Shield Regeneration runs on top of this at all times, with no delay. These recharge numbers are a direct reading of the code and were not observed in game.

Details: [Shields](combat-damage.md#shields).

### How Close Does My Ship Fly to Its Target?

Closer than its weapon range allows, by design, and only while it is chasing.

Every 0.5 seconds the game sets an approach distance. The distance is 5/6 of the shortest reach among the turrets that can hit the target. A turret's reach is its range minus its distance from the ship's center minus 0.5. The result is kept between 6 and 30 units. The ship flies in when it is farther than 1.2 times that distance from the target's center and backs off when it is closer than 0.6 times. Range itself is measured to the target's hull, so a large target is in range sooner than a small one.

These ships are flown to their formation slot or order point instead, so they ignore all of this: a fleet ship on Escort or Hold Fire, a ship with a Hold or Move order, and a Defensive ship that has no enemy to answer (see the next answers). The HUD's Hold Position switch holds only the ship you fly. The fleet order Hold holds one fleet ship.

Details: [How Far a Ship Parks From Its Target](combat-damage.md#how-far-a-ship-parks-from-its-target) and [Hitting: Range, Tracking and Projectiles](combat-damage.md#hitting-range-tracking-and-projectiles).

### What Do Missiles Do When I Pick Another Target?

They turn to it. A missile does not keep a target of its own. Each step it steers to wherever its launcher is aiming, so every missile in flight follows your new pick.

Only a missile that has lost its tracking marker can fail to follow. That happens when the launcher changes target or the target dies. The missile gets a new marker only if its launcher fires seeking missiles, which every missile launcher does except the Small Microcell Swarm Launcher. Otherwise it flies straight until its lifetime ends.

Details: [What a Missile Follows](combat-damage.md#what-a-missile-follows).

### Which Target Does My Ship Take When I Do Not Pick One?

The ship you fly auto-targets only while Autopilot is on (the ECHO system, switched with "Toggle Autopilot"). When it is hit, it switches to the attacker if it has no auto target or its auto target is out of range. About once a second it also takes the pick of its faction's shared threat table, unless you hold a manual target. Since 0.8.3.5 the old rule is gone, where a hit on any unit of yours within 80 units made the ship you fly take the attacker.

Fleet ships follow their stance:

- Aggressive ships choose from their faction's threat table at the location, or from every enemy there when the table is empty.
- Escort ships copy the target of the ship they follow.
- Defensive ships choose only from the threat table: the enemies that have damaged their faction at that location. They don't chase anything else.
- Hold Fire ships do not fire.

A target you pick yourself beats all of these.

Details: [Which Target a Ship Picks](combat-damage.md#which-target-a-ship-picks).

### What Do My Drones Do When They Have Nothing to Attack?

Follow Ship (the default) sends a drone with no target back to your ship only when it is more than 10 units away. Free Roam never does. Between targets a drone has no destination but keeps a small forward thrust and keeps turning, so it drifts and turns around one spot. Repair drones have a target only while something of yours is missing HP they can restore.

While a route is set, the game pauses all your scanners, so drones find no targets during travel or autopilot. When your ship is hit, its drones launch if docked, and every drone without a manual target takes the attacker. Hold Position does not affect drones.

Details: [Drones, Torpedoes, Fighters](combat-damage.md#drones-torpedoes-fighters).

## Ship Stats and Gear

### What Decides My Ship's Stats, and Where Do Bonuses Come From?

Each stat is (base + the sum of flat amounts) x the product of all multipliers. Flat amounts add. Multipliers multiply each other, so a x1.10 and a x1.20 give x1.32, not x1.30. A rolled item stat is either flat or a multiplier, not both.

Bonuses come from:

| Source | Notes |
|---|---|
| Every equipped item | An item registers all its stat lines with the whole ship. A Weapon Range roll on a reactor or hull piece applies to every turret. |
| Hull upgrade path | Each Combat node gives +2% Combat Power, each Torpedo node +2% Torpedo Power and each Drones node +2% Drone Power. Each Defense node gives +1% hull, +1% armor and +1% shield. Milestones come at nodes 10, 20 and 30. |
| Skills, mastery, officers | Commander skill nodes. Combat Mastery multiplies Combat Power by 1 + 0.005 x mastery level. Defense Mastery adds 0.1% Damage Reduction per mastery level. Mastery levels stop at 100. Each officer adds the stat chosen for that officer. |
| Hull badge and size | A Combat hull gets Combat Power x1.3 and +3% Critical Chance. Weapon Range rises with hull size from 1 to 8, up to +2.0 for a size 8 Combat hull. |
| Item aspects | Extra effects on weapons and modules, for example +25% reload or +5% Damage Reduction. |
| Temporary boosts | Buffs and debuffs from abilities. Each lasts for a set time. |
| Crew | On a player ship, crew add to Combat Power according to their type. The crew effectiveness skill scales this. |

Damage and the seven damage-type stats (Kinetic, Energy and so on) have no source at all in 0.8.3.5. No gear rolls them, and the boss aspects Overcharge and Siege Mode only multiply a Damage total of 0, so they add nothing despite their text. The damage-versus-shield and damage-versus-armor bonuses come only from the boss aspects Shield Slapper and Armor Breaker.

Critical Chance and Damage Reduction never roll on gear either. They come from aspects, skills, hull badges, mastery, hull upgrade milestones and officers.

After the bonuses are added up, the game applies the reactor energy modifier, which affects player ships only. It then applies a level-based HP multiplier to hull, armor and shield.

Details: [How a Stat Is Calculated](combat-damage.md#how-a-stat-is-calculated), [Where Bonuses Come From](combat-damage.md#where-bonuses-come-from) and [What the Damage Stat Is](combat-damage.md#what-the-damage-stat-is).

### Which Stats Affect Drones, Torpedoes and Fighters?

Drones use the same turret rules as your ship. A drone weapon's power is its own main power x your Combat Power multiplier, plus your Combat Power split by the drone's size rating, plus your Drone Power divided by the base drone count. Drones also read your Damage, damage-type bonuses, Critical Damage, range, attack speed and resistances directly. They do not read your Critical Chance. They work it out from their own Precision, which is the drone's own plus your Precision divided across your drones. The Composite Drone Armor skill and the second Drones hull milestone (+15%) add Damage Reduction to drones.

Torpedoes always deal Heat damage. Torpedo power is your Torpedo Power plus your normalized Combat Power. It is rolled as power / 5 x random(0.8, 1.25), with no division by shots per second. Critical hits, Damage and Heat Damage come from the launching ship. A torpedo explodes in a radius of 6 on impact, with falloff down to a minimum of 10%. It hits every enemy in range including the target, using the damage left after the direct hit. A Small bay waits 8 seconds between launches and 4 seconds to reload. A Medium bay waits 1 second between launches and 3 seconds to reload.

Fighters from a hangar bay are ships with their own turrets and follow the ship rules. Hangar Build Speed sets how fast they are repaired and rebuilt.

The reactor energy modifier does not touch Drone Power or Torpedo Power themselves. Drones and torpedoes add a share of your Combat Power, so they feel it through that share.

Details: [Drones, Torpedoes, Fighters](combat-damage.md#drones-torpedoes-fighters).

### What Does the Reactor Do, and What Happens if I Go Over Its Energy?

Nothing is drained and nothing switches off. The reactor's Energy stat is a budget. Usage is the energy draw of your connected equipment divided by that capacity, and usage sets a multiplier on your Combat Power:

| Usage | Multiplier |
|---|---|
| up to 50% | x1.20 |
| up to 75% | x1.10 |
| up to 100% | x1.00 |
| up to 125% | x0.75 |
| up to 150% | x0.50 |
| above 150% | x0.25 |

Keep usage at or under 50% for the bonus and at or under 100% to avoid the penalty. The multiplier covers every turret and the pooled flat power. It applies to player-owned ships only. Ships controlled by the game (non-player characters, or NPCs) and drones ignore the reactor. The Divert Power skill has 2 ranks and adds 5% per rank to the multiplier of the 50% bracket.

A higher-level reactor has more capacity while weapon draws are fixed numbers, so it carries more weapons. Rarity does not raise a weapon's energy draw (see [What Is Gear Rarity Worth?](#what-is-gear-rarity-worth)).

Details: [Energy and the Reactor](combat-damage.md#energy-and-the-reactor).

### Which Stats Shown in Game Do Nothing?

- Shield Recharge Delay and Shield Gate Time are shown, but the game never reads them in this version. The shield delay is always 5 seconds.
- Resistances past the 80% combined cap have no effect.
- The Range bonus on station parts is never switched on.
- A weapon's shots per second says little about its damage. Damage per second follows power, not shots. Beams only fire for 2 seconds at a time and then reload, so they average about half of power / 5.
- A loadout built around Heat damage cannot work, because no turret deals Heat directly.

Details: [What Looks Useful and Is Not](combat-damage.md#what-looks-useful-and-is-not).

## Enemy Strength

### How Much Tougher Is an Enemy Five or Ten Levels Above Me?

Below level 30, an enemy 5 levels higher has about 1.3 to 1.4 times the HP and hits 1.4 times as hard. An enemy 10 levels higher is about double on both. The damage ramp starts at level 30 and the HP curve at level 40, so the gap hurts more from there. Past level 70, a 10-level gap multiplies HP by 4.5 to 5. From level 30 on, damage is x1.56 for 5 levels and x2.44 for 10.

| Starting level | HP, +5 levels | HP, +10 levels | Damage, +5 | Damage, +10 |
|---|---|---|---|---|
| 10 | x1.30 | x1.72 | x1.42 | x2.00 |
| 30 | x1.57 | x2.53 | x1.56 | x2.44 |
| 60 | x1.83 | x3.35 | x1.56 | x2.44 |
| 80 | x2.20 | x4.86 | x1.56 | x2.44 |

These ratios assume a size 5 hull, a Large Standard hull module, the Frontier tier (tier 0) and the same ship rank and rarity. They are worked out from the formulas, not measured in play. At damage x2.44, you need about 2.4 times the damage to keep pace and must survive 2.4 times the damage taken.

Details: [How Much Tougher Is an Enemy Five or Ten Levels Higher](scaling.md#how-much-tougher-is-an-enemy-five-or-ten-levels-higher).

### What Are Ship Ranks Worth, and Which Will I Meet?

A ship rank multiplies an enemy's hull, armor and shield HP and adds to its fire rate and reload. It does not change damage per shot. A Legendary is a damage sponge that fires up to 30% faster, not a harder hitter.

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

- At levels 1 to 5, only Rookie ships appear.
- At levels 6 to 11, Rookie and Regular ships appear.
- From level 12 at Escalation tier 0, ships up to Veteran appear. At level 60 the split is Rookie 26.4%, Regular 26.8% and Veteran 46.8%.
- Elite ships appear from Escalation tier 4 (Havoc) and Champions from tier 6 (Maelstrom).
- Commander and Legendary ships never come from this roll.

Bounty bosses are always Elite or better. Champions can appear from level 40, Commanders from level 70 and Legendaries from level 91. Escalation does not change boss ranks.

Details: [Rank Table](scaling.md#rank-table), [Ship Rank Chances for Normal Enemies](scaling.md#ship-rank-chances-for-normal-enemies), [Boss Ranks](scaling.md#boss-ranks) and [How Much a Ship Rank Is Worth in Levels](scaling.md#how-much-a-ship-rank-is-worth-in-levels).

### What Does Escalation Change?

Escalation is a difficulty setting you choose, from tier 0 to tier 8. Nothing in the game code changes it for you. Only the Escalation popup's confirm button does, and you can move it up or down at any time. A tier stays locked until your captain level reaches its unlock level.

| Tier | Unlock (captain level) | Enemy HP | Enemy damage | Enemy forces | Mission credits | Mission XP | Your resistances |
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

"Enemy forces" multiplies the points budget of generated hostile sites, so higher tiers bring more enemies. "Your resistances" is subtracted from all seven of your resistances on every ship you own.

From tier 4, each kill also has a chance to drop a Hull Upgrade Kit. The popup calls this line "Combat Upgrade Kit drops". The chance is 0.2% at tier 4 and rises to 1.0% at tier 8.

Converted to levels, tier 4 HP equals about +15 levels and its damage about +6. Tier 8 is about +30 levels of HP and +33 of damage. The popup also has an override with toughness and damage sliders from x1.0 to x4.0 that multiply the tier value.

Details: [Escalation](scaling.md#escalation) and [What Escalation Is Worth in Levels](scaling.md#what-escalation-is-worth-in-levels).

## Loot and XP

### What Does a Destroyed Enemy Ship Drop?

Each drop is rolled on its own:

| Drop | Chance |
|---|---|
| One of its fitted modules | 12% |
| One of its fitted weapons | 12% |
| One cargo item (not ammo) | 30% |
| Ammo | 10% |
| Credits | 30% |
| Blueprint | 4%. For Elite and higher ranks, the blueprint is at least High Grade. |
| Ship blueprint piece | 5%, only for ship classes you have not unlocked |
| Officer upgrade (High Grade) | 1%, from level 30 |

A dropped module or weapon is one the ship carries, so its rarity follows the enemy gear table. In 95% of cases the drop is picked from the lowest-rarity fitted items, so a ship with one Legendary and one Standard module almost always drops the Standard. A credit drop is a random 2.5 to 3.25 x 100 x the credit multiplier for the level. On average that is 472 credits at level 10, 1,270 at 30, 5,607 at 60 and 40,630 at 100.

Details: [What a Destroyed NPC Ship Drops](scaling.md#what-a-destroyed-npc-ship-drops).

### What Rarity of Gear Will I Find at My Level?

Locked Containers, compartments in boardable facilities and research files roll rarity from a weighted table by item level:

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

Enemy gear is much richer than box loot at high level. Each module slot and hardpoint on an NPC ship rolls its own rarity:

- At level 20, 16.7% are High Grade and none are Exotic.
- At level 60, 15% are Exotic and 5% are Legendary.
- At level 80, 20.8% are Legendary.
- At level 100, 32.1% are Legendary.

Enemy gear is not limited by your level.

An item's level is the location level plus a roll of -1, 0 or +1, with a minimum of 1. Items built for your faction are capped relative to your effective level. An item 10 levels above you becomes 6 levels above you, and one 30 levels above becomes 11 levels above you. Locked Containers are not capped.

Details: [Rarity of Locked Container and Facility Gear](scaling.md#rarity-of-locked-container-and-facility-gear), [Which Rarity NPC Gear Has](scaling.md#which-rarity-npc-gear-has) and [Item Level](scaling.md#item-level).

### What Is Gear Rarity Worth?

Rarity is free power: it multiplies almost every stat on the item and does not raise its energy draw. A Legendary weapon draws the same energy as a Standard one of the same level and size, with 50% more main stat. The extra bonus stat lines and aspect slots come on top.

| Rarity | Main stat | Bonus stat lines | Worth in item levels |
|---|---|---|---|
| Standard | x1.00 | 0 | 0 |
| Enhanced | x1.10 | 1 | 1.4 |
| High Grade | x1.20 | 2 | 2.6 |
| Exotic | x1.35 | 3 | 4.3 |
| Legendary | x1.50 | 3 | 5.9 |

Item level, by contrast, raises power and energy draw together. A level 54 Legendary weapon has about the power of a level 60 Standard one with a lower energy draw. Reactor capacity is the exception to the rarity multiplier: it takes only the square root, so Legendary gives x1.22.

Details: [Rarity](scaling.md#rarity) and [What Gear Rarity Is Worth](scaling.md#what-gear-rarity-is-worth).

### How Much XP Does a Kill Give?

XP per kill is ceil(5 x base XP reward x level factor x 2^(your level / 9)) x the ship rank multiplier. The base XP reward is 10 unless a ship overrides it. Your level is your effective level. The level factor depends on the target's level against yours (since 0.8.3.7):

| Target vs you | Level factor |
|---|---|
| 1 to 6 levels above | 1 + 0.4 / 6 per level (up to 1.4) |
| Same level | 1.0 |
| 1 to 9 levels below | 0.9 at 1 below, 0.1 less per level, down to 0.1 at 9 below |
| 10 or more levels below | 0.00001 (effectively nothing) |

Kills up to 9 levels below you still pay something, but the most XP comes from enemies at or above your level. A higher ship rank pays more: Regular x1.2, Veteran x1.3, Elite x1.5, Champion x1.6, Commander x2.0 and Legendary x2.4. Each level takes more kills than the last, because the XP you need doubles every 5 levels while XP per kill doubles every 9. For a Rookie ship (x1.0) of your own level:

| Level | XP per kill | XP to finish the level | Kills per level |
|---|---|---|---|
| 10 | 109 | 4,000 | 37 |
| 30 | 504 | 64,000 | 127 |
| 50 | 2,352 | 1,024,000 | 435 |
| 100 | 110,599 | 1,048,576,000 | 9,481 |

Details: [XP Rewards](scaling.md#xp-rewards).

## Credits

### What Is the Best Way to Earn Credits at My Level?

Nearly every payout is a credit factor times 100 times 2^(L/14), where L is usually the level of the place, not yours. So the ranking stays the same as you level, and a source only drops out when a better one opens:

| Level | Best source |
|---|---|
| 5-15 | Facility runs with every locked room opened (a level-10 Derelict Colony Ship averages 58,000 credits), then Ship Capture missions from level 10 |
| 16-37 | Dungeon Assault missions, which pay the mission and the Pirate Base's own caches |
| 40+ | Signal Trap bosses (a credit factor of 300-499, about 1 million credits at level 60), with facility runs to collect Signal Fragments |

Missions with an enemy faction also gain the Escalation bonus (up to +150%) and up to +40% from reputation. Ship kills pay little: one level-60 Bounty Hunt mission equals about 47 kills' worth of credit drops.

Details: [What Pays Best at Your Level](credits.md#what-pays-best-at-your-level) and [Every Source at a Glance](credits.md#every-source-at-a-glance).

### Does Escalation Raise All Credits?

No. It raises mission credits only, and only on missions with an enemy faction, plus Bounty, Patrol and Industrial Ops credits. Courier, Mine Ore, Deliver Crafted Goods, Trader and Trade Materials missions never get it. Facility caches, Signal Trap bosses, kill drops and sales are not affected.

Details: [Reputation and Escalation Bonuses](credits.md#reputation-and-escalation-bonuses).

### Should I Refine Ore or Sell It Raw?

Refine it from level 23. Ore loses 1% of its sell price per level above 10, down to 60% at level 50. Refined canisters keep their full value, so refined ore sells for 0.7 of the ore's value after the 10% refinery fee, against 0.48 raw at level 50 and above. Better still, an Ore Trader at a bar pays 3 times the ore's value.

Details: [Ore and Refining](credits.md#ore-and-refining) and [Bar Patron Orders](credits.md#bar-patron-orders).

### Is Opening Locked Containers Worth the Credits?

Not in credits alone. Opening one with credits costs a credit factor of 50 at the box's level, and the credits inside average 81% of that. The equipment, blueprints and other contents have to make up the rest. Opening with a Key for opening Locked Containers costs nothing, so use keys first (the game does this for you).

Details: [Locked Containers](credits.md#locked-containers).

### Do ECHO's Missions Pay Well?

No. A mission ECHO runs on Autopilot pays 0.33 of the normal credits with the Prompt Engineering skill "Automated Negotiations", and half that without it, and ECHO only takes Easy missions (Normal with "Risk & Reward"). Autopilot also skips half of the credit drops from kills.

Details: [Missions ECHO Runs for You](credits.md#missions-echo-runs-for-you).

## Combat Stations

### What Do the Parts of a Combat Station Do?

Only Defense Platforms shoot. Every other part is a hull target with loot.

| Part | In a fight |
|---|---|
| Station Core | Always drops credits. It must be destroyed for the station to count as destroyed. A Station Defense mission asks you to protect it. |
| Energy Field | A hull target that drops loot. It does nothing else. |
| Refinery | A hull target that drops loot (ore and blueprints). The ore is the system's surface ore and core ore. |
| Cargo Dock | A hull target that drops loot (credits and refined parts). |
| Docking Pad, Docking Tunnel | Hull targets that drop loot. They hold the docking points, which matter only for boardable facilities. |
| Connector | A hull target that drops a little loot. It is never required for the station to count as destroyed. |
| Defense Platform (turret) | Shoots, has the only shields on any part, and can drop one of its turrets. |
| Defense Platform (drone bay) | Carries a Drone Bay module and no turrets. It drops nothing from the platform loot table. |

Details: [What Each Part Does in a Live Fight](stations.md#what-each-part-does-in-a-live-fight) and [Every Station Part](stations.md#every-station-part).

### What Does the Energy Field Do?

Nothing special. It is a 1350 base hull part with no weapons, no aura and no effect on the other parts. It does not extend turret range for any part. It drops warp fuel and, rarely, Energy Crystals.

Details: [The Energy Field](stations.md#the-energy-field).

### Which Part Should I Destroy First?

Start with the Defense Platforms, because they are the only parts that shoot. Once they are gone, nothing on the station can hurt you.

You do not have to destroy everything. The station counts as destroyed once every part that is not a Connector is gone, so you can skip Connectors. Each part you kill also costs reputation with the owner (see [What Does Destroying Station Parts Cost in Reputation?](#what-does-destroying-station-parts-cost-in-reputation)). Killing a part does not weaken or remove its neighbours, so there is no cascade.

Details: [What Counts as Destroyed](stations.md#what-counts-as-destroyed) and [What Each Part Does in a Live Fight](stations.md#what-each-part-does-in-a-live-fight).

### How Much HP Does a Station Part Have?

Hull, armor and shield are each built as base HP x a module multiplier x a multiplier. The Hull, Shield Generator or Armor module supplies the module multiplier (about x20 to x28 for a Small module at level 60). Modules do not add flat HP. The base hull values are:

- Connector: 315 or 735.
- Core, Cargo Dock and docking parts: 900.
- Refinery and Energy Field: 1350.

Defense Platforms have hull 450 to 900 and the only shields, 150 to 900. The drone bay platform has hull 600 and armor 600.

The multiplier is the Rookie ship rank (0.7) x an HP balance curve x an NPC base curve x the Escalation tier HP multiplier x a context multiplier. The game never shows the two curves on screen. Both rise with the part's level. Against the Frontier tier (tier 0), the first three combine to:

| Part level | 10 | 30 | 45 | 60 | 80 | 100 |
|---|---|---|---|---|---|---|
| Combined multiplier | 0.70 | 0.83 | 1.22 | 1.81 | 3.20 | 5.73 |

The context multiplier is x10 for a boardable facility and x1 for every other mission, skirmish and pirate station, and faction outposts. Stations in war-zone systems are far tougher. That is covered by the [Conquest stage](../conquest/README.md), which opens at level 28. The module multiplier comes on top of the base values and these multipliers, so treat the number on the tooltip as the true value.

Details: [How Tough a Part Is](stations.md#how-tough-a-part-is) and [Context Multiplier](stations.md#context-multiplier).

### What Does a Destroyed Station Part Drop?

Loot is rolled when the part dies and differs on every kill. While Autopilot is on, each non-fixed item has a 50% chance to be skipped.

| Part | Drop |
|---|---|
| Station Core | Always credits, plus a 30% chance of a blueprint of the part's level |
| Cargo Dock | The refined-part roll, plus a 70% chance of credits |
| Energy Field | A 50% chance of High Grade warp fuel. Above level 25, a 4% chance of Energy Crystals |
| Refinery | A 50% chance of ore from this system and a 30% chance of a blueprint |
| Docking Pad, Docking Tunnel | Refined parts (Titanium Plate, Bulkhead, Hull Beam, Structural Component) |
| Connector | A 70% chance of one refined part, at half the amount |
| Defense Platform with turrets | A 40% chance of one of its turrets (only turret types you can use) and a 20% chance of a stack of ammo |
| Defense Platform with a drone bay | Nothing from the table |

Killing a part also pays XP: 50 x the level-gap factor x 2^(effective level / 9), rounded up. Industrial Outpost stations pay no loot, XP or reputation change. Faction skirmish stations, pirate hideouts and the harder Station Offense and Defense missions add extra gear and a skill point item to the Core.

Details: [What Drops When a Part Dies](stations.md#what-drops-when-a-part-dies) and [Extra Loot on Mission and Skirmish Stations](stations.md#extra-loot-on-mission-and-skirmish-stations).

### What Does Destroying Station Parts Cost in Reputation?

Every part you kill hits the owner's reputation, unless the part is exempt. Several mission stations are exempt. A part counts as your kill if you hit it at any point before it died, even if something else landed the finishing hit.

- If your reputation with the owner is above -500 (Neutral or better), each part costs 100 and the event log reads "Betrayal! -100 {faction name} reputation."
- At -500 or below (Wary or worse), each part costs 5.
- Below -500 the faction also counts as your enemy, so you can target its parts with a plain click.

Destroying a whole faction station is a second, bigger cost, called wrecking. It applies to faction stations, meaning Defense stations and economic outposts such as Construction Yards, refineries and production stations. It does not apply to hubs or guild stations. It happens when the last part that is not a Connector dies and at least one part you hit has died on that station. It costs 5000 reputation, or 1000 if you are already Hostile or worse with the owner. The log reads "Station destroyed! -N {faction name} reputation." Leaving one part alive avoids it.

If you did not contribute, for example because a siege killed the station, the last part drops no loot and costs no reputation, but the station can still leave a salvage site.

A wrecked station leaves a "Wreckage of {station}" salvage site for 3600 seconds (1 hour), holding 10% of each item stack that was in its storage, if anything recoverable remains. One late-game item cancels both the per-part and the wreck reputation loss while it is active. See the [late-game pages](../late-game/README.md).

If the faction that owns the system you are in is an enemy of the part's owner and you are Neutral with it, you gain 1 reputation with it per kill. That is the second faction, not the owner of the part.

Details: [Killing Parts](stations.md#killing-parts) and [Wrecking a Faction Station](stations.md#wrecking-a-faction-station).

### Can I Attack a Station That Is Not Hostile to Me?

Only at combat-type locations. Ctrl + left-click on a part that is not hostile to you (a "force attack") works when all of these are true:

- The part is not already hostile to you.
- You are in a subsector of the Conquest quadrant or of a quadrant beyond it. The Frontier quadrant and the prologue never allow it.
- The location you are in is not one of the space station kinds.

Faction stations, embassy stations, player stations, Industrial Outposts and every guild or neutral station are space station kinds, so you cannot force-attack them. That leaves mission, skirmish and pirate stations and boardable facilities.

Since 0.8.3.5, a plain left-click on a part of the station you are at docks you at its hub when the part belongs to the station's own faction, docking is possible right now and the station has a hub. The tooltip then shows a "dock" hint.

When you attack a non-hostile part, it becomes hostile to you. Damaging any unit of a faction that is not your enemy marks every unit of that faction at that location hostile. The reputation cost is the same as the kill penalty above. Parts that are already hostile need only a plain click.

Details: [Ctrl-Click on a Friendly Station](stations.md#ctrl-click-on-a-friendly-station).

### Why Is a Station I Damaged Back at Full Health?

Damage and kills are saved. A space station (a faction station, hub, embassy, player station, Industrial Outpost or guild station) is rebuilt from its seed, with every part at full HP, when more than 3600 seconds (1 hour) of game time have passed since you last visited it. The rebuild happens the next time the game saves.

Mission, skirmish and pirate stations do not use this rule. Their part damage is not reset by it, and only their guard ships are regenerated after 7200 seconds away.

Two more rebuild triggers apply to stations in war-zone systems once the war opens at level 28. See the [Conquest stage](../conquest/README.md).

Details: [How Long Damage Lasts](stations.md#how-long-damage-lasts).

## Boarding

### How Do I Get a Ship to Become Boardable?

A ship becomes boardable only through a chance roll made while it is badly damaged, not because it is weak. Once a hostile ship's hull is under 40%, every 15% of max hull in damage taken below that line gives one roll, so a ship gets at most 2 rolls.

With no EMP charge the base chance is 0.005, times 1/3 at 40% hull, 2/3 at 20% and 1 at 0%, using the hull left after the hit. The first roll comes once the hull is at 25% or lower and succeeds with about 0.29%. The second comes at 10% hull or lower and succeeds with about 0.42%. The most is 0.5%, at 0% hull. Flying manually doubles that. Under autopilot the doubling is skipped and half the rolls are skipped instead. Unassisted boardings are rare.

EMP (electromagnetic pulse) weapons build up an EMP charge on the target, and a full charge raises the base chance to 0.025, five times higher. EMP weapons are what make boarding likely.

A ship cannot become boardable if any of these is true:

- It is flagged as unboardable.
- It is a carrier fighter.
- It is a Drone-size ship.
- It is at an Industrial Outpost location.
- It has an empty loot list.

Left-clicking a boardable ship opens the boarding panel. The game's hint reads "LMB to board", where LMB means the left mouse button. Ctrl+click attacks the ship instead, but only before a boarding has started. The ship then revives with at least 15% hull, can never be boarded afterwards, and any capture mission tied to it fails.

Details: [Ships](boarding.md#ships).

### What Do the Numbers on the Boarding Tooltip Mean?

On a ship under attack the tooltip shows the live estimate, the number of attackers versus defenders (counting your crew still waiting in staging), Ship Integrity, and the average morale of each side.

The Estimate compares total power. The ratio is your power divided by their power, with their power counted as at least 1. The win chance is ratio / (ratio + 1). The game shows it as a label:

| Win chance | Label |
|---|---|
| above 75% | Favourable |
| above 50% | Contested |
| above 25% | Difficult |
| 25% or below | Very Difficult |

This is a power ratio, not a simulation, and it is not a win probability. A 50% estimate means equal power, not equal outcomes. It ignores HP, positions, specialists and events. Wounded crew count for half their power, Panicked crew for 60%, Heroic crew for 140% and Dazed crew for two thirds. Your side also includes the stealth setting, your Assault Doctrine skill and equipment power, but not the ammo setting.

The estimate in the crew-selection panel, before you start, is cruder and is shown for ships only. It counts every defender as 3 power times the faction's defender power multiplier, so it undercounts Marine-heavy factions and defenders at a level gap. It also leaves out your Assault Doctrine skill, so you are usually a little stronger than it says. A facility with hostility None is always Favourable at 100%, because it has no defenders.

The facility tooltip adds these rows:

- Status.
- Hostiles, before entry only. It shows the expected resistance, from "No hostiles expected" to "Expect heavy resistance".
- Specialist.
- Crews and the estimate, once defenders exist.
- Progress, which is friendly rooms over total rooms.
- Integrity.

After a run, the tooltip shows the Outcome instead of the live rows.

Details: [What Each Tooltip Number Means](boarding.md#what-each-tooltip-number-means) and [The Estimate Formula](boarding.md#the-estimate-formula).

### Which Crew Should I Bring to Board a Ship?

Bring Marines first. Compartment capacity limits how many units fit and fight, not their power, so the crew with the best power and HP per slot wins.

| Crew type | Boarding power | Resistance | HP | Cost (credits) |
|---|---|---|---|---|
| Deckhand | 1 | 1 | 6 | 3,500 |
| Gunner | 2 | 3 | 8 | 7,000 |
| Marine | 4 | 8 | 13 | 16,000 |
| Pilot | 2 | 2 | 7 | 22,500 |
| Miner | 1 | 2 | 7 | 7,000 |
| Salvager | 1 | 2 | 7 | 7,000 |

A Marine has four times a Deckhand's power and over twice the HP for under five times the price. A room that holds six units holds 24 power of Marines or 6 power of Deckhands. Pods carry 10 units each, so sending 21 units launches three pods, the last with 1 unit.

Compare the live Estimate with the one before the start, because the earlier number understates strong defenders. Skills help your units only. Assault Doctrine gives +10% boarding power per point, up to +30%. Crew Conditioning gives +10% crew resistance per point, up to 3 points.

Details: [Crew Types](boarding.md#crew-types), [Picking Crew for a Ship](boarding.md#picking-crew-for-a-ship) and [Skills and Gear](boarding.md#skills-and-gear).

### Which Crew Should I Bring Into a Facility?

Specialists matter most. The Specialist row of the facility tooltip lists the crew types that count. With a specialist in the room, ambushes are 30% smaller and hazard damage drops by up to 50%. Even one specialist gives a 33% chance to prevent each negative event outright at a level gap under 3.

- Bring the first-listed specialist for the loot. For example, bring Miners to a Mining Outpost and Marines to Relay and Military facilities. The second-listed type opens locks and shields you from events but misses the profession loot.
- Locked rooms need a specialist in an adjacent room, and they carry bonus loot. Winning does not require opening them.
- At an Ancient Ruin, bring 3 or more distinct crew types. Its bonuses depend on it.
- Armed facilities use the ship combat rules, so bring Marines. Military Outpost defenders are 70% Marines.
- Do not overstack one room. Investigation speed is 1 + 0.15 x (n - 1) for n units, and capacity is 3 to 6 per room. Six units investigate a room in 57% of the time one unit needs, not 17%.

Details: [Picking Crew for a Facility](boarding.md#picking-crew-for-a-facility).

### Should I Board Silent, Balanced or Aggressive, and Which Ammo?

Use Silent on large ships where defenders are spread out and you clear room by room. Use Aggressive only when you are strong enough that speed matters more. Armor Piercing is the better ammo against Marine-heavy factions. Hollow ammo wastes 20% against Marines and Gunners.

| Option | Effect |
|---|---|
| Silent | Your power x0.8 and casualty rate x0.75. Rooms next to yours go Alert after about 350 seconds on average, with Standard or Armor Piercing ammo. |
| Balanced | No change to your power or casualty rate. Neighbouring rooms go Alert after about 70 seconds. |
| Aggressive | Your power x1.2. Neighbouring rooms go Alert after about 35 seconds. |
| Hollow ammo | 0.8x damage to Marines and Gunners. Structural damage per kill is 1.5. |
| Standard ammo | 1.0x damage. Structural damage per kill is 2. |
| Armor Piercing (AP) | 1.2x damage to Marines and Gunners. Structural damage per kill is 2.5. |

Silent's net effect on a single fight is close to neutral: you deal less but take 25% fewer hits. What it gives you is time.

Details: [Ammo and Stealth](boarding.md#ammo-and-stealth) and [Picking Crew for a Ship](boarding.md#picking-crew-for-a-ship).

### How Much Tougher Are Defenders at a Higher Level?

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

A gap also shifts the defender mix toward Marines, up to 80% at a gap of 15. Ships use your effective level and facilities use your captain level. A facility whose level is 10 or more above your effective level cannot be started at all.

Some factions are much harder to board. Their ships are simulated at least 10 levels above you, unless the boarding comes from a capture mission. That puts their HP multiplier at about 2.8 at least, and up to 4 at a gap of 15 or more.

Details: [Level Scaling of Defenders](boarding.md#level-scaling-of-defenders) and [Factions That Are Harder to Board](boarding.md#factions-that-are-harder-to-board).

### When Should I Call Reinforcements?

Call them only when the Airlock and the rooms beside it have free capacity. New crew enter through the Airlock, up to its capacity, and wait in staging otherwise. The button reads Add Attackers for ships and Call Reinforcements for facilities. Pressing Send takes the selected crew out of your flagship's crew at once. They return only if they survive and your ship has free crew space.

On ships the enemy can call a wave too, if the Enemy Reinforcements event was rolled. It lands in your entry point, the Airlock, 4 to 24 seconds into the fight, even if you hold that room. A strong first wave in the Airlock room matters more than depth in the ship. Wounded units leave the fight and recover in staging at about 1.3% of max HP per second, so a long fight heals your first wave there.

Details: [Reinforcements](boarding.md#reinforcements) and [When to Call Reinforcements](boarding.md#when-to-call-reinforcements).

### How Do I Lose Fewer Crew?

- On ships, put the Bridge on priority early. While any room is in combat, holding it gives +0.02 morale per second, and it is required for mass surrender.
- Keep your rooms connected to the Airlock. While any room is in combat, a room cut off from it by enemy-held rooms costs 0.05 morale per second.
- Barricade secured rooms next to the front line to take 35% less damage there.
- Carry Body Armor to lower your casualty rate by 10%.
- Take Assault Doctrine and Crew Conditioning if you board often.
- Throw grenades into rooms with defenders and none of your crew. Each grenade does 3 damage to everyone in the room and dazes them for 15 seconds. It also sets the room to Alert and costs 5% of the structure.
- Do not let a ship's integrity reach zero. Without a won fight, the ship explodes 15 seconds later.
- If the fight is lost, retreat. Retreating crew take no combat damage.
- At a facility, retreat while integrity is still above 10% if hazards keep firing. A retreat keeps your crew and, on average, half the loot, because each non-data item is kept with a 50% chance. The facility stays enterable.

Details: [What Lowers Your Losses](boarding.md#what-lowers-your-losses) and [Morale](boarding.md#morale).

### What Does Boarding Cost in Reputation?

If the target's faction has a reputation with you above -500 (Neutral or better), you pay when you press start, before the approach finishes. The penalty is ceil(base XP x 0.5) x 40. Any facility costs 520, because its base XP is 25. A ship uses its own base XP, so a ship with base XP 10 costs 200. Cancelling the approach does not refund it.

Once your crew lands, the friendly faction is marked hostile to you in that location, and every unit of that faction there turns hostile. Some ships are flagged to skip both the penalty and the hostility. The late-game item that blocks reputation loss until you next dock skips the penalty but not the hostility.

Details: [Reputation Cost of Entering](boarding.md#reputation-cost-of-entering).

### What Happens When I Win or Retreat?

If you win a ship boarding, you capture the ship. It becomes yours, its crew and commander are removed, and all loot goes to your cargo. If it has more than two equipped modules, at least two are destroyed (the more hull it has left, the more survive). The node count on each of its hull upgrade paths is halved.

If you win a facility run, all loot goes to your cargo, and a completed clear-the-base mission with no turn-in step is claimed automatically. The run stays open after the win until you ask for extraction.

If you retreat from a ship, it recovers (to at least 15% hull), becomes unboardable and flees. Each non-data loot item has a 50% chance to reach your cargo, and boarding missions fail. A facility retreat follows the same partial-loot rule. Data items are always kept, and integrity stays reduced.

Details: [How a Run Ends](boarding.md#how-a-run-ends).

### What Looks Useful in Boarding but Is Not?

- Estimate percentages are not win probabilities. They map a power ratio through ratio / (ratio + 1).
- The Enemy Dazed event changes no number, because every room already starts Dazed.
- The profession discovery bonus is stored in the game, but nothing reads it. Only the profession speed bonus is read.
- Each room has an efficiency modifier that drops when the room is cut off from the Airlock. The game computes it, but nothing reads it.
- Cancelling an approach does not refund the reputation penalty.

Details: [What Looks Useful but Is Not](boarding.md#what-looks-useful-but-is-not).
