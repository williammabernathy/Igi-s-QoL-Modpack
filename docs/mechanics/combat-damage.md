# Combat damage

How a shot becomes hull damage: where a weapon's damage comes from, what each stat does, the order in which a target's shields, armor and resistances take their share, and what a player can stack against each layer. Values are read from the game code of version 0.8.3.4 (the decompiled source and the Unity prefab data on disk, not in-game testing). Where a number lives in prefab data rather than code, the section says so.

Terms and abbreviations are explained in the [glossary](glossary.md).

Three words on this page have more than one meaning in the game. "Combat Power" always means the ship stat (the Faction overview also has a "Combat Power" tile, which is a faction's total fleet points; see the [glossary row](glossary.md#stats-and-combat-abbreviations)). "Defense" means the ship-side group of stats, skills, mastery and hull upgrade path, never the Defense station type or the Defense number on a system card. "Rank" for an enemy means ship rank (Rookie, Regular, Veteran, Elite, Champion, Commander, Legendary). The abbreviation HP means hit points; shield, armor and hull each have their own HP pool.

Non-player character (NPC) level, ship rank and escalation scaling is in [scaling](scaling.md). Station parts, their HP multipliers and the war-effort damage multiplier (the conquest firepower bonus from Conquest missions you complete) are in [stations](stations.md). A ship at zero hull can become boardable instead of dying; that is in [boarding](boarding.md).

## How a stat is calculated

Every ship, drone, torpedo and station part keeps two arrays indexed by `EquipStat`: a sum of flat amounts and a product of multipliers. What the game calls a stat is:

```
stat = (base + sum of flat amounts) x product of multipliers
```

Each source contributes `EquipStatLine`s with an `amount` and a `multiplier`. Flat amounts add. Multipliers multiply each other, so a x1.10 and a x1.20 give x1.32, not x1.30. A rolled item stat is either one or the other: a "+4% Weapon Range" roll is a flat 0.04 added to the sum, and the multiplier on that stat stays 1 unless something else sets it. Percentage stats (crit, resist, range and so on) are stored as fractions, 0.15 for 15%.

The base term only exists for a few stats: `HullHP`, `ArmorHP` and `ShieldHP` (from the unit definition), `ShieldRechargeRate`, `ShieldRechargeDelay` and `ShieldRegen` (from the shield generator), `CargoCapacity`, and `CriticalChance` (3% plus Precision, below).

The stat sources, all folded in by `AggregateStatLines`:

| Source | Notes |
|---|---|
| Every equipped item | An item registers all its stat lines with the ship, not just with itself. A "Weapon Range" roll on a reactor or a hull piece applies to every turret. |
| Hull upgrade path | `ShipUpgradeStatSource`: see [hull upgrades](#hull-upgrades) |
| Derived boosts | Aspects on items, the ship's hull bonus badge, range bonus, commander skill nodes, mastery, officer skill nodes and unique officer boosts. Rebuilt by `RebuildDerivedStatBoosts` when gear or skills change |
| Temporary boosts | `StatBoostEntry` with a duration: ability buffs, debuffs, the painter strip. Stack count multiplies the amount, and a multiplier stacks as `1 + (m - 1) x stacks` |
| Crew (player ship) | Crew types add a Combat Power multiplier of `1 + combatBonus` and a flat amount, scaled by the crew effectiveness skill |
| Officers (player ship) | Needs the officer passive skill. Combat officers pick Combat Power, Critical Chance or Attack Speed |

Two exceptions in the aggregation:

- A turret's own power line only counts while the turret is not manually disabled. A turret converted to drone power adds its amount to `DronePower` instead.
- A bonus the game wants limited to one turret (the per-weapon aspects) lives in a separate `TurretBoostStat` list. `AbstractEquipment.GetStat` adds those on top of the ship value for that turret only: `(shipStat + amount) x multiplier`.

After the aggregation `RecalculateStats` applies, in this order: the reactor energy modifier (player-owned ships only), NPC ship rank modifiers, the NPC ship HP curve, escalation modifiers (NPC damage and HP, player resist debuff), `HpBalanceMultiplier(level)` on hull, armor and shield, then class-specific modifiers such as the station HP multiplier.

```
HpBalanceMultiplier(L) = (1 + 0.28 x clamp01((L - 10) / 30)) x (1 + 0.6 x max(0, (L - 40) / 60)^1.5)
```

Source: `AbstractUnit.RecalculateStats`, `AbstractUnit.GetStatRaw`, `AbstractUnitData.AggregateStatLines`, `AbstractUnitData.ApplyStatSourceLines`, `StatBoostEntry.GetStats`, `AbstractEquipment.GetStat`, `EquipStatLineMerging.MergeLinesFor`, `GameMath.HpBalanceMultiplier`, `SpaceShipData.ComputeCrewBonuses`.

## Every stat

In-game names come from the translation strings. Four stats have no translation string in `resources.assets` (marked "no string"); the game then shows the raw key. `Power` is special: any amount or multiplier on `Power` is also applied to `CombatPower`, `MiningPower` and `SalvagePower`.

| Id | `EquipStat` | In-game name | What it does in code |
|---|---|---|---|
| 1 | `HullHP` | Hull HP | Last layer. Base from the unit definition (`hullHPScale x 150` for station parts) |
| 2 | `ArmorHP` | Armor HP | Armor layer capacity. Base is the unit's base armor if it has an armor module (unless the module overrides it), and 0 without one |
| 3 | `ShieldHP` | Shield HP | Shield layer capacity, same rule with the shield generator module |
| 10 | `HullRegen` | Hull Regeneration | Flat HP per second. Only while out of combat and below max |
| 11 | `ArmorRegen` | Armor Regeneration | Flat armor per second |
| 12 | `ShieldRegen` | Shield Regeneration | Flat shield per second, always on, even during the recharge delay. Base is 0.1% of max shield per second (set when the ship spawns) |
| 13 | `HullRegenPercentage` | no string | Fraction of max hull per second, added to `HullRegen` |
| 14 | `ArmorRegenPercentage` | no string | Fraction of max armor per second, added to `ArmorRegen` |
| 20 | `Power` | Power | Feeds Combat, Mining and Salvage Power |
| 21 | `CombatPower` | Combat Power | Weapon damage. See [turret power](#where-a-turrets-damage-comes-from) |
| 22 | `MiningPower` | Mining Power | Mining turrets (not covered here) |
| 23 | `SalvagePower` | Salvage Power | Salvage turrets (not covered here) |
| 24 | `Precision` | Precision | Raises critical chance. See [critical hits](#critical-hits) |
| 25 | `DronePower` | Drone Power | Divided across the drone bay's drones and added to each drone's weapon power |
| 26 | `TorpedoPower` | Torpedo Power | Torpedo damage, plus the ship's normalized Combat Power |
| 27 | `RepairPower` | Repair Power | Repair turrets |
| 30 | `ShieldRechargeDelay` | Shield Recharge Delay | Shown on the ship panel. The code that would use it (`modifiedRechargeDelay`) is never read, so the shield timer always uses the generator's base delay (5 s on every prefab) |
| 31 | `ShieldGateTime` | Shield Gate Time | Described in-game as immunity after the shield is depleted. Nothing in the code reads it |
| 32 | `DamageReduction` | Damage Reduction | Flat fraction removed from every hit, shares the 80% cap with resists |
| 33 | `ShieldRechargeRate` | Shield Recharge Rate | Scales the shield's recharge ticks |
| 40-46 | `KineticResist` to `ExplosiveResist` | Kinetic, Energy, Radiation, Heat, Cold, Corrosion, Explosive Resistance | Fraction removed from hits of that damage type |
| 50 | `CriticalChance` | Critical Chance | Chance per hit to crit |
| 51 | `CriticalDamage` | Critical Damage | Added to the x2 crit multiplier |
| 52 | `Damage` | Damage | Additive bonus to all weapon damage |
| 53 | `WeaponRange` | Weapon Range | Range = base x (1 + stat) |
| 54 | `AttackSpeed` | Attack Speed | Fire delay = base / (1 + stat) |
| 55 | `ReloadSpeed` | Reload Speed | Reload time = base / (1 + stat) |
| 56 | `MagazineSize` | Magazine Size | Magazine = round(base x (1 + stat)) |
| 57 | `TurretRotationSpeed` | Turret Rotation Speed | Rotation = base x (1 + stat) |
| 58 | `ProjectileSpeed` | Projectile Speed | Speed = base x (1 + stat) |
| 60-66 | `KineticDamage` to `ExplosiveDamage` | Kinetic, Energy, Radiation, Heat, Cold, Corrosion, Explosive Damage | Additive damage bonus for that type only |
| 67 | `DamageVsShield` | no string | Multiplies damage while the target still has shield |
| 68 | `DamageVsArmor` | no string | Multiplies damage while the target still has armor |
| 70-72 | `Thrust`, `SideThrust`, `RotationalThrust` | Thrust, Side Thrust, Rotational Thrust | Movement |
| 74 | `CargoCapacity` | Cargo Capacity | Hold size |
| 75 | `Yield` | Resource Yield | Mining and salvage output |
| 76 | `EnergyCapacity` | Energy | Reactor capacity. See [energy](#energy-and-the-reactor) |
| 77 | `OreUpgradeChance` | Ore Upgrade Chance | Mining |
| 78, 79 | `BoardingSuppression`, `BoardingPower` | Boarding Suppression, Boarding Power | See [boarding](boarding.md) |
| 80 | `OfficerPassiveBonus` | Officer Bonus | Multiplies the officers' chosen bonus by `1 + stat` |
| 81 | `HangarBayRepairRate` | Hangar Build Speed | Fighter repair and rebuild speed in the hangar bay |

Source: `EquipStat`, `EquipStatExtensions.IsPercentageStat`, `EquipStatExtension.GetDisplayName`, `AbstractUnit.GetStatRaw`, `AbstractUnit.ApplyPassiveHullRegen`, `ShieldGeneratorModule.Start`, `ShieldGeneratorModule.Update`, `ShieldGeneratorModule.modifiedRechargeDelay`, `AbstractTurret.range`, `AbstractTurret.fireDelay`, `AbstractTurret.reloadDelay`, `AbstractTurret.maxMagSize`, `AbstractTurret.rotationSpeed`, `AbstractTurret.projectileSpeed`, `ApplyStatSourceLines`, `SpaceShipData.ApplyOfficerBonuses`.

## What the Damage stat is

`Damage` (52) and the seven type stats (60-66) are additive with each other and then multiply the shot once:

```
damage multiplier = 1 + (type damage stat) + Damage
```

`Damage` never rolls on items in 0.8.3.4: none of the 131 equipment builders lists it, or any type damage stat, `DamageVsShield`, `DamageVsArmor`, `CriticalChance` or `DamageReduction`, as a roll. These stats come from aspects, skills, hull badges, mastery, hull upgrade milestones and officers. See [where bonuses come from](#where-bonuses-come-from).

## Where a turret's damage comes from

A weapon item carries a main power line (`CombatPower` for guns, `TorpedoPower` for torpedo bays). The ship pools all `CombatPower` it has and hands each active combat turret a share:

```
turret power = (main x M  +  (Ship - Mains x M) / N x rating) x power multiplier

Ship  = stat CombatPower, with its multiplier M already applied
Mains = sum of the main power lines of all active combat turrets
M     = the CombatPower multiplier product
N     = max(0.45, sum of ratings of active combat turrets)
rating = Tiny 0.45, Small 1, Medium 2, Large 3
```

So the weapon's own number is multiplied by M and stays with that weapon. Everything else that adds Combat Power (optional rolls on any item, flat skill amounts, crew, hull upgrades) goes into one pool split in proportion to turret size. A Large turret gets three times the pooled share of a Small one. A disabled turret (manually, or converted to drones) leaves the pool. Multipliers on Combat Power (skills, hull badge, hull upgrades, reactor bonus, crew) multiply the whole pool and every main line.

The per-turret power multiplier is a prefab value (0.9 to 1.15, listed in the [weapon table](#typical-weapon-numbers)).

Average damage per second (DPS) from one turret is `power / 5 x 1.025`, before crits and bonuses. The 1.025 is the mean of the 0.8 to 1.25 roll. The game's own expected-DPS function uses these exact constants.

Source: `AbstractTurret.CalculateAttackPower`, `AbstractTurret.CalculatePooledPower`, `AbstractTurret.turretEquivalentRating`, `AbstractTurret.GetExpectedDps`, `AbstractUnit.UpdateEquivalentTurretCounts`, `AbstractUnit.GetEquivalentTurretsCount`, `DamageData.PowerPerDamage`.

## The path of a hit

The numbered steps run in this order for every damage event, whether it comes from a bullet, a beam tick, a missile or an explosion.

### 1. The shot is rolled (`DamageData.CalculateDamage`)

```
base   = power / 5  x  random(0.8, 1.25)  /  attacks per second (default)
```

`power` is the turret power above. "Attacks per second (default)" is computed from the prefab's own, unmodified fire delay, reload, magazine and burst:

```
cycle = (burst - 1) x burstDelay + fireDelay
aps0  = magazine / ( ceil(magazine / burst) x cycle + reloadDelay )
```

Because `aps0` ignores your Attack Speed, Reload Speed and Magazine Size bonuses, those stats raise the number of shots without shrinking each shot. Each one speeds up part of the firing cycle, so its gain depends on how much of the cycle that part takes: Attack Speed shortens the time between shots, Reload Speed the reload, Magazine Size cuts the number of reloads.

Beams skip the `/ aps0` part: each 0.2 s tick carries `power x dt` instead, so a beam deals `power / 5` per second of firing.

### 2. Critical hits

See [critical hits](#critical-hits). Each crit multiplies the roll by `2 + CriticalDamage`.

### 3. Damage bonuses

```
shot = base x crit multiplier x (1 + type damage stat + Damage)
```

The stats are read from the firing turret (ship value plus that turret's own boosts). A drone or torpedo with no turret reads them from itself.

### 4. Target resistance (`AbstractUnit.TakeDamage`)

```
shot = shot x max(0.2, 1 - DamageReduction - Resist(type))
```

This happens before any layer. The floor of 0.2 is the resist cap: `DamageReduction` and the type resistance together can remove at most 80% of a hit. Past that total, extra resistance does nothing. Both stats can also go negative (the painter below, the escalation debuff on player ships), which raises the multiplier above 1 with no upper limit.

While the player's ship is in a warp travel, damage it takes is multiplied by 0.1 after this step.

### 5. Incoming damage cap

A player ship with the third milestone of the Defense hull upgrade path caps every single hit at 20% of its maximum total HP (hull + armor + shield), after step 4:

```
shot = min(shot, 0.2 x max total HP)
```

### 6. Shield

If the target has a shield generator, its recharge delay timer resets on every hit, even once the shield is down. If the shield has capacity:

```
shot = shot x (1 + DamageVsShield)         (read from the firing turret, only if above 0)
absorbed = min(shot, current shield)
shield -= absorbed;  shot -= absorbed
```

Shields treat all seven damage types the same: there is no shield resistance and no weakness. Whatever the shield could not absorb carries on to armor already multiplied by the shield bonus.

### 7. Armor

If damage is left and the target has armor:

```
shot = shot x (1 + DamageVsArmor)           (from the firing turret, only if above 0)
if the damage type is in the armor's weak types:  shot = shot x (1 + weakAmount)
else:                                              shot = shot x (1 - max(0, resistAmount - paintStrip))
absorbed = min(shot, current armor);  armor -= absorbed;  shot -= absorbed
```

The only armor type in 0.8.3.4 is Armor Plating (every armor prefab has the same values): 15% resistance, weak to Corrosion with +25% damage taken. Armor resistance and weakness are exclusive: a weak type gets the +25% and not the -15%. Because resistance is applied before absorption, 100 armor HP lasts for 100 / 0.85, about 118, points of ordinary shot damage, and 100 / 1.25 = 80 points of Corrosion.

The painter (`LaserPainterTurret`) sets `paintStrip` to 0.10 for 6 seconds, which takes Armor Plating from 15% to 5% resistance. It also adds -0.05 `DamageReduction` to the target for the same 6 seconds.

### 8. Hull

```
hull = clamp(hull - shot, 0, max hull)
```

There is no hull resistance beyond step 4. At zero hull the unit is destroyed, with these exceptions: a boardable ship becomes boardable (see [boarding](boarding.md)); a player-owned ship is left at 0.1 HP and not destroyed (fighters and arena battles excepted); an emergency jump can intervene.

### Shorthand

```
to shields:  base x crit x (1 + bonuses) x resistFactor x (1 + VsShield)
to armor:    base x crit x (1 + bonuses) x resistFactor x (1 + VsShield, if it passed through shield) x (1 + VsArmor) x armorFactor
to hull:     base x crit x (1 + bonuses) x resistFactor   (minus what shield and armor soaked up first)
```

`resistFactor = max(0.2, 1 - DamageReduction - Resist)`, and `armorFactor` is `1.25` for a weak type or `0.85` (`0.95` painted) otherwise.

Source: `DamageData.CalculateDamage`, `AbstractTurret.CreateDamageData`, `AbstractUnit.TakeDamage`, `AbstractUnit.DamageResistMultiplier`, `AbstractUnit.ApplyIncomingDamageCap`, `AbstractUnit.DamagePrevention`, `AbstractUnit.ApplyVsLayerBonus`, `ShieldGeneratorModule.TakeDamage`, `ShieldGeneratorModule.ResetDelayTimer`, `ArmorModule.TakeDamage`, `ArmorModule.ApplyArmorResistance`, `ArmorModule.AbsorbArmorDamage`, `ArmorModule.ApplyArmorPaintStrip`, `LaserPainterTurret.TickBeam`, `AbstractCombatBeamTurret.ApplyDamageTick`, `ShipUpgradeCalculator.GetDefenseStatLines`, `TravelManager.TravelActive`.

### A worked example

Illustrative inputs, computed from the formulas above: a level 30 Standard Medium Autocannon rolls a main Combat Power of 400 to 420 (`2.0 to 2.1 x powerLevel 200`, see [weapon power](#weapon-power-by-level)). Say 410. The firing ship has +10% Kinetic Damage and +5% Damage, and no crits. Its shots against a target with Damage Reduction 10%, Kinetic Resistance 20%, a shield of 1000 and 500 Armor Plating:

| Step | Value |
|---|---|
| Average base per shot | 410 / 5 / 2.331 = 35.2 (rolls 28 to 44) |
| After bonuses | 35.2 x 1.15 = 40.5 |
| After resistance (1 - 0.10 - 0.20 = 0.70) | 28.3 |
| Shield (1000) | about 35 shots to break |
| Armor (500), 28.3 x 0.85 = 24.1 per shot | about 21 shots to break |
| Hull | 28.3 per shot |

At 2.33 shots per second the shield alone lasts about 15 s.

## Critical hits

```
crit chance = (0.03 + precision crit + flat CriticalChance) x CriticalChance multiplier
precision raw = 0.05 x Precision / round(25 x DamageMultiplier(level))
precision crit = raw                                    if raw <= 0.05
               = 0.05 + (raw - 0.05 + 1)^0.75 - 1       if raw > 0.05
```

`round(25 x DamageMultiplier(level))` is the same "power level" that item stat rolls scale with: 50 at level 10, 100 at 20, 200 at 30, 400 at 40, 800 at 50, 25 600 at 100. Precision equal to one power level gives +5% crit chance. Past +5% the gain is damped: raw 10% becomes 8.7%, raw 20% becomes 16.1%, raw 50% becomes 37.2%.

```
roll the chance c;  on success: crits += 1,  c = c / 2,  roll again
stop after the roll that fails, or when crits > Critical Recursion ranks
crit multiplier = (2 + CriticalDamage) ^ crits
```

Without ranks in Critical Recursion at most one crit happens per hit. With `r` ranks a hit can crit up to `r + 1` times, each extra chance halved, and each crit multiplies. The crit multiplier is applied to the shot before type bonuses.

Crits are rolled per hit and per beam tick, so a continuous beam rolls five times a second. Burst and tick weapons smooth out the variance.

Source: `AbstractUnit.GetBaseCritChance`, `AbstractUnit.GetPrecisionCrit`, `AbstractUnit.GetStatRaw`, `DamageData.CalculateDamage`, `GameMath.DamageMultiplier`, `SkilltreeNode.combatMegaCrit`.

## Damage types

Seven types: Kinetic, Energy, Radiation, Heat, Cold, Corrosion, Explosive. The type picks which `...Damage` stat adds to the shot and which `...Resist` stat the target uses. Beyond that only armor knows about types, and only one armor type exists.

| Type | Weapons in 0.8.3.4 | Special |
|---|---|---|
| Kinetic | Autocannon, Twin and Triple Autocannon, Gatling, Double Gatling, DT Chaingun ("DT" is part of the weapon's name), Railgun, Burst Railgun | Rail shots pierce, up to 6 targets |
| Energy | Lasers, Laser Repeater, Laser Shotgun, Plasma Cannon, Plasma Repeater, Plasma Battery, Plasma Beam, Microcell Swarm Launcher | |
| Radiation | Rad Cannon | Applies an irradiated effect (prefab data, not read) |
| Heat | none directly | Torpedoes, Cannon splash, and Firestarter-type extra damage |
| Cold | Cryo Lance | |
| Corrosion | Corropult | Armor Plating is weak to it. Leaves a damaging cloud |
| Explosive | Missile Launchers, Hailfire, Cannon | |

Source: `DamageType`, `DamageTypeExtensions.GetDamageBoostStat`, `DamageTypeExtensions.GetResistStat`, `ArmorModule.weakTypes` (prefab), `AbstractTurret.damageType` (prefab).

## Hitting: range, tracking and projectiles

Range is a plain distance check from the firing point to the nearest point of the target's hull outline, `<= range`, with `range = base x (1 + WeaponRange)`. There is no damage falloff with distance for direct hits.

A turret fires when it has a target in range, is aimed within `maxFiringAngle` of the target and is off cooldown. Turrets that cannot rotate fire whenever the target is in range. Rotation speed is degrees per second (`rotation = base x (1 + TurretRotationSpeed)`). Most guns use a 1 degree firing window; lasers use 0.25, and the plasma cannons 0.01, so a slow turret on a fast target can fail to fire at all. Missile turrets use 360.

How each weapon family then hits:

| Family | How the hit is resolved |
|---|---|
| Combat Laser | Beam plays for its effect time, then damage lands on the target. No miss while the target lives |
| Laser Repeater, Gatling, Chaingun, Shotgun (hitscan) | A line cast with random scatter of `accuracyAngle x distance x 0.015` units either way. Hits only the current target |
| Railgun | Instant ray. Damages every enemy on the line, each hit after the first at `0.5^n` of the power, up to 6 hits |
| Plasma Beam, Cryo Lance (beams) | Instant while in range. Damage ticks every 0.2 s for up to 2 s, then a reload |
| Autocannon, Plasma, Corropult, Rad Cannon | A projectile flies at its speed with a random heading spread of `+-accuracyAngle` degrees (4 to 5.5 for autocannons) and a lifetime of 5 s. It hits the first enemy it overlaps, not necessarily the target |
| Missiles, Microcell | Homing. A missile steers to a predicted intercept point with limited thrust and turn rate; it expires with its lifetime |

The aim point leads a moving target: target position plus target velocity times `(1 + cos) x distance / projectile speed`, where `cos` is the dot product of the target's heading with the line to it. Slow projectiles at long range against fast, turning ships miss more. Raising `ProjectileSpeed` shortens the lead.

Reload takes the full `reloadDelay`. An instant reload skill can skip it on the player's ship. A weapon with an ammo type spends cargo ammo per shot (Gatling-class weapons spend one ammo per four shots); with an empty hold the magazine refills to zero and the weapon cannot fire.

Source: `AbstractTurret.Update`, `AbstractTurret.HandleFire`, `AbstractTurret.RefreshTargetInRange`, `AbstractTurret.ApplyProjectileLead`, `AbstractTurret.RotateTurret`, `AbstractTurret.Reload`, `AbstractTurret.ReloadCoroutine`, `AbstractTurret.RecordShotFired`, `AbstractProjectile.Update`, `AbstractProjectile.ProcessHit`, `AutoCannonTurret.FireInternal`, `GatlingTurret.GetScatteredAimPoint`, `RailCannonTurret.TryApplyRailgunHit`, `LaserTurret.FireForEffect`, `AbstractCombatBeamTurret.TickBeam`, `Missile.FixedUpdate`, `SkilltreeNode.combatInstantReload`.

## Energy and the reactor

Every piece of equipment has an energy draw (`capacityCost`, listed per weapon in the [table](#typical-weapon-numbers)) multiplied by the ship's `energyDrawMultiplier`. The reactor's capacity is the `EnergyCapacity` stat. This is a static budget: nothing is drained over time and nothing switches off when the budget is exceeded. What changes is a damage multiplier.

```
usage = sum of connected equipment energy draw / EnergyCapacity
```

| Usage | Multiplier on Power, Combat, Mining, Salvage Power |
|---|---|
| up to 50% | x1.20 |
| up to 75% | x1.10 |
| up to 100% | x1.00 |
| up to 125% | x0.75 |
| up to 150% | x0.50 |
| above 150% | x0.25 |

The multiplier applies to the ship's overall `CombatPower`, so it scales every turret and the pooled flat power together. It does not touch `DronePower` or `TorpedoPower`. The rule runs for player-owned ships only; NPC ships and drones ignore the reactor. The skill "Divert Power" adds to the Combat Power multiplier of the 50%-or-less bracket. The only draw modifier in the code is the efficient-drilling effect (x0.25, mining).

Reactor capacity comes from the reactor item's main roll, a flat amount of `(1.35 to 1.4, Base; 1.5 to 1.6 Small; 3.4 to 3.5 Medium; 4.5 to 4.8 Large) x powerLevel x rarityMultiplier^0.5`. Weapon draws are fixed prefab numbers, so a higher-level reactor carries more weapons.

Source: `AbstractUnit.ApplyReactorModifier`, `AbstractUnit.UpdateEnergyDrawMultiplier`, `AbstractUnit.reactorAffectedStats`, `ReactorModule.energyThresholdModifiers`, `ReactorModule.GetEnergyUsage`, `AbstractEquipment.energyDraw`, `EfficientDrillingEffect`, `SkilltreeNode.combatReactorOutputCP`, `EquipmentBuilder.ApplyStatScaling`.

## Shields

```
recharge delay    = 5 s on every shield prefab, reset by every hit
every 0.25 s after the delay:  shield += max shield x 0.005 x modifiedRechargeRate
modifiedRechargeRate = generator rate x (1 + ShieldRechargeRate stat)
```

The stat read here is the full ship stat, which already includes the generator's base rate of 1.0. With no bonuses the recharge rate is 1 x (1 + 1) = 2, so a recharge tick restores 1% of max shield and the shield refills at 4% of max per second, 25 s from empty after the 5 s delay. A bonus of +0.10 on `ShieldRechargeRate` makes the rate 2.1, which is 5% faster recharge, not 10%. This is a direct reading of the code; the value was not observed in game.

Flat `ShieldRegen` runs in addition, continuously, with no delay. `ShieldRechargeDelay` and `ShieldGateTime` have no effect on the timer in this version (see the [stat table](#every-stat)).

Source: `ShieldGeneratorModule.TickRechargeTimers`, `ShieldGeneratorModule.TickShieldRecharge`, `ShieldGeneratorModule.modifiedRechargeRate`, `ShieldGeneratorModule.ResetDelayTimer`, `ShieldGeneratorModule.Update`, `AbstractEquipment.GetStat`, prefab `rechargeRate` and `rechargeDelay`.

## Armor regeneration

Armor Plating regenerates `ArmorRegen + ArmorRegenPercentage x max armor` per second, with no delay. NPC ships have the percentage part capped at 0.1% per second and the whole rate halved while in combat. Hull regeneration only runs out of combat.

Source: `ArmorModule.GetArmorRegenPerSecond`, `ArmorModule.ComputeArmorRegen`, `AbstractUnit.ApplyPassiveHullRegen`.

## Damage over time and area effects

Everything below goes back through the full pipeline (steps 4 to 8) on every tick or hit, one target at a time.

| Effect | Rule |
|---|---|
| Damage over time, DoT (`DamageOverTime`) | A fixed total (the template's damage) divided into `tickCount` equal ticks every `tickDelay`. Each tick is a fresh hit of that size, with no new crit roll. A second application merges: both remaining totals add and the ticks reset to the full count. Tick count and delay are prefab values (not extracted) |
| Per-weapon aspects (`PayloadTurretExtraDamage`) | After a hit by that weapon, a second hit of `percentage x the hit's total damage` in another type, either instant or as a DoT. Examples from the strings: Punch Through +10% Kinetic, Firestarter +15% Heat over 6 s, Frozen Core +10% Cold. The trigger event, and whether the total is taken before or after the target's resistance, were not traced |
| Corropult cloud | Impact hit, then a cloud of radius 6 for 5 ticks 1 s apart. Each tick hits every enemy inside for `impact damage x 0.5 / 5` (code defaults; prefab overrides not checked) |
| Cannon explosion | Large Cannon shot (`CannonProjectile`) explodes in radius 6. Each enemy in range takes 20% of what the shot had left after the target's defenses, as Heat, with distance falloff. The prefab link from the Cannon item to this projectile was not traced |
| Area falloff | Explosions scale the hit by `max(1 - distance / radius, 0.1)` |
| Radiation Cannon | Applies an irradiated effect from prefab data (not read) |
| Cryo / cold slow, Cold Snap and similar | Prefab effects (not read) |

The area helper copies the damage left over after the first target's defenses, so splash damage is whatever the direct hit did not use up. Against a shielded ship that soaked the whole direct hit, the splash does nothing.

Source: `DamageOverTime.Tick`, `DamageOverTime.Add`, `DamageOverTime.CalculateDamagePerTick`, `PayloadTurretExtraDamage.PayloadTriggered`, `CorrosiveCloudAOE`, `CannonProjectile.OnHit`, `AreaDamageHelper.CalculateDropoff`, `AreaDamageHelper.CreateNewDamageData`, `TemporaryEffect`.

### Temporary effects

`TemporaryEffect` buffs and debuffs are stat boosts with a duration. Two effects with the same object name merge into one: stacks add and the duration refreshes to the longer of the two. A stat boost's amount times its stack count is what the stat sees. The Defense skill "Threat Reducer" (a ship skill, not a station) gives +2% resistance per stack to the type of the hit that triggered it, for 10 s, 20% of the time.

Source: `TemporaryEffect.CheckStackability`, `TemporaryEffect.AddStack`, `StatBoostEntry.AddStack`, `AddTemporaryResist`.

## Drones, torpedoes, fighters

| Unit | Damage |
|---|---|
| Drones | Same turret pipeline. A drone weapon's power is `main x commander's Combat Power multiplier + commander's normalized Combat Power x rating + commander's Drone Power / base drone count`. A drone also reads the commander's percentage stats directly (Damage, type bonuses, Critical Damage, range, attack speed, resists) but not Critical Chance, which it works out from its own Precision. The Drone Defense skill and the second Drones hull milestone add Damage Reduction to drones |
| Torpedoes | Always Heat. `power = TorpedoPower + normalized Combat Power`, rolled as `power / 5 x random(0.8, 1.25)` with **no** division by shots per second. Crits, Damage and Heat Damage read from the launching ship. Explodes in radius 6 on impact with falloff (minimum 10%) on every enemy in range, the target included, using the damage left after the direct hit. Bay timing is prefab data: Small bay 8 s between launches and 4 s reload, Medium bay 1 s and 3 s |
| Fighters | Hangar-bay fighters are ships with their own turrets and use the ship pipeline (not traced further). Their repair and rebuild speed is `HangarBayRepairRate` |

"Normalized Combat Power" means ship Combat Power divided by the number of equivalent turrets, the same share a Small turret gets.

Source: `Drone.GetStat`, `Drone.ApplyCommanderPowerBonuses`, `Drone.ApplyPlayerCommanderBonuses`, `DroneBayModule.GetSlotPowerValue`, `TorpedoBayModule.CreateDamageData`, `TorpedoBayModule.GetAttackPower`, `Torpedo.ProcessHit`, `Torpedo.Explode`, `TorpedoBayModule.FinishFiring`, `AbstractUnit.GetNormalizedPower`.

## Where bonuses come from

None of the stats in the [Damage stat section](#what-the-damage-stat-is) roll on gear. This is what grants them.

| Source | Combat-relevant bonuses |
|---|---|
| Combat hull badge (ships with the Combat role) | Combat Power x1.3, Critical Chance +3% |
| Range bonus (by hull size, 1 to 8) | Weapon Range + 0.0, 0.2, 0.4, 0.7, 1.0, 1.2, 1.8, 2.0 for Combat ships; 0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.7, 0.8 for Industrial. Added to the `WeaponRange` sum, so a size 8 Combat hull has x3 range before other bonuses |
| Hull upgrades | See below |
| Commander skill nodes | Per-rank stat boosts. Examples from prefab: +1% Critical Chance, +5% Weapon Range, +5% Critical Damage, +3% Attack Speed, +12% Attack Speed (Tier 3), +2% Critical Chance and +5% Critical Damage, +4 to 5% Turret Rotation. Combat Power nodes have both a x1.01 and a +3 flat line per rank (how the "minimum of +3" wording maps to them was not traced). Other nodes add abilities: Critical Recursion, Instant Reload, Divert Power, Lone Gun, Redline and others |
| Mastery | Offense mastery: Combat Power x(1 + 0.005 x mastery level). Defense mastery: +0.1% Damage Reduction per mastery level. Mastery experience points (XP) come from shield and armor damage taken (Defense) |
| Officers | One chosen stat from Combat Power, Critical Chance, Attack Speed (Engineering officers: Drone Power or Shield HP). Combat Power and Shield HP apply as multipliers, the others as flat amounts at 0.8 of the officer's bonus |
| Item aspects | Common and rare slots on weapons and modules: Reload Subroutine +25% reload, Critical Attenuation +25% critical damage, Weakpoint Scan +5% crit chance, Rangefinder +25% weapon range, Hardened Exterior +5% Damage Reduction, Hardened Plating, Extremophile and Gamma Ward +2 to 5% resists, Operational Reserves x1.02 Power |
| Boss aspects | Overcharge (no crits, +25% damage), Shield Slapper (+10% to shields), Armor Breaker (+10% to armor, +5% critical damage), Quick Quick Quick (+40% reload, +10% fire rate), Siege Mode, Opening Blow (+40% to targets above 90% HP), Volatile Plasma and others, as described in their strings. Which of these use `DamageVsShield` and `DamageVsArmor` was not traced; those two stats are only read in step 6 and 7 above |

### Hull upgrades

Paths grow per node (kits spent): Combat +2% Combat Power each, Torpedo +2% Torpedo Power each, Drones +2% Drone Power each, Defense +1% hull, armor and shield each. Milestones sit at node 10, 20 and 30 and also need a lifetime count (kills for Combat and Torpedo, damage absorbed for Defense), scaled by ship size:

| Path | Milestone 1 | Milestone 2 | Milestone 3 |
|---|---|---|---|
| Combat (100, 1000, 5000 kills) | +5% Weapon Range, +15% Critical Damage | Combat Power x1.1 | Combat Power x1.2 |
| Torpedo (same kills) | Torpedo Power x1.05 | x1.10 | x1.20 |
| Defense (1 M, 50 M, 1 B damage absorbed) | +2% Damage Reduction | Shield, Armor, Hull HP x1.1 | HP x1.2 and the 20% single-hit cap |
| Drones (5 M, 250 M, 5 B drone activity) | not traced | +15% drone Damage Reduction | not traced |

Source: `SpaceShipData.AddHullAndRangeStatBoosts`, `SpaceShipRoleType.GetShipRangeBonus`, `SpaceShipData.AddCommanderTreeStatBoosts`, `SpaceShipData.AddMasteryStatBoosts`, `SpaceShipData.ApplyOfficerBonuses`, `OfficerData.GetBonusPool`, `OfficerData.GetBonusForStat`, `SpaceShipData.ComputeCrewBonuses`, `ShipUpgradeCalculator.GetPathStatLines`, `ShipUpgradeCalculator.GetMilestoneThreshold`, `ShipUpgradeCalculator.IsMilestoneUnlocked`, `AbstractUnit.AddSkillNodeStatBoosts`; hull badge, aspect and skill values are prefab data.

## Typical weapon numbers

Weapon bodies live in the item prefabs (`AbstractTurret` fields), not in code. "Shots/s" is the `aps0` above (it includes reload). "Power mult" is the prefab `powerMultiplier`. "Energy" is the draw before the ship's multiplier. Ranges are game units. Projectile "instant" means no travel time; "hitscan" is a line cast. Sizes follow `ModuleSize`; the Tiny items are drone weapons.

| Weapon | Size | Type | Range | Shots/s | Mag / reload | Projectile | Power mult | Energy |
|---|---|---|---|---|---|---|---|---|
| Cryo Lance | Small | Cold | 10 | 0.45 | 1/2s | instant | 0.9 | 0 |
| Cryo Lance | Medium | Cold | 14 | 0.45 | 1/2s | instant | 0.9 | 0 |
| Corropult | Small | Corrosion | 10 | 0.67 | 2/2s | 8 | 1.0 | 5 |
| Corropult | Medium | Corrosion | 14 | 1.0 | 4/2s | 10 | 1.0 | 10 |
| Laser | Tiny | Energy | 10 | 0.5 | 1/1s | instant | 0.95 | 7.5 |
| Laser | Small | Energy | 10 | 0.5 | 1/1s | instant | 0.95 | 7.5 |
| Laser | Medium | Energy | 14 | 0.5 | 1/1s | instant | 0.95 | 15 |
| Laser | Large | Energy | 14 | 0.36 | 1/1.75s | instant | 0.95 | 25 |
| Laser Repeater | Small | Energy | 12 | 3.0 | 30/4s | hitscan | 0.9 | 7 |
| Laser Repeater | Medium | Energy | 16 | 3.56 | 45/5s | hitscan | 0.9 | 11 |
| Laser Repeater | Large | Energy | 18 | 4.17 | 60/6s | hitscan | 0.9 | 16 |
| Laser Shotgun | Small | Energy | 10 | 2.42 | 24/3s | hitscan | 1.1 | 5 |
| Microcell Swarm Launcher | Small | Energy | 12 | 2.27 | 50/3s | 8 | 1.0 | 0 |
| Microcell Swarm Launcher | Medium | Energy | 16 | 3.34 | 100/3s | 8 | 1.0 | 0 |
| Plasma Battery | Large | Energy | 16 | 2.13 | 80/6s | 18 | 1.1 | 20 |
| Plasma Beam | Small | Energy | 12 | 2.5 | 10/2s | instant | 0.95 | 0 |
| Plasma Beam | Medium | Energy | 16 | 2.5 | 10/2s | instant | 0.95 | 0 |
| Plasma Cannon | Medium | Energy | 12 | 0.53 | 8/3s | 14 | 1.0 | 10 |
| Plasma Cannon | Large | Energy | 12 | 0.38 | 6/4s | 15 | 1.0 | 20 |
| Plasma Repeater | Small | Energy | 11 | 1.61 | 33/4s | 18 | 1.1 | 5 |
| Plasma Repeater | Medium | Energy | 16 | 2.07 | 60/4s | 18 | 1.15 | 10 |
| Cannon | Large | Explosive | 20 | 0.25 | 1/3s | 15 | 1.0 | 20 |
| Hailfire M-Launcher | Small | Explosive | 100 | 2.0 | 15/6s | 15 | 0.9 | 5 |
| Missile Launcher | Tiny | Explosive | 100 | 1.0 | 5/4s | 8 | 1.0 | 0 |
| Missile Launcher | Small | Explosive | 100 | 1.0 | 5/4s | 8 | 1.0 | 5 |
| Missile Launcher | Medium | Explosive | 100 | 1.0 | 5/4s | 6 | 1.0 | 10 |
| Missile Launcher | Large | Explosive | 100 | 0.97 | 6/5s | 6 | 1.0 | 20 |
| Autocannon | Small | Kinetic | 13 | 2.04 | 22/2s | 12 | 1.0 | 5 |
| Autocannon | Medium | Kinetic | 18 | 2.33 | 38/3s | 15 | 1.0 | 10 |
| Twin Autocannon | Medium | Kinetic | 16 | 4.46 | 54/4s | 15 | 1.0 | 10 |
| Triple Autocannon | Large | Kinetic | 16 | 6.94 | 102/4.5s | 18 | 1.0 | 20 |
| Gatling | Small | Kinetic | 10 | 15.38 | 200/3s | hitscan | 1.0 | 5 |
| Double Gatling | Small | Kinetic | 10 | 16.13 | 250/3s | hitscan | 1.1 | 5 |
| DT Chaingun | Large | Kinetic | 10 | 7.14 | 100/5s | hitscan | 1.0 | 20 |
| Railgun | Small | Kinetic | 75 | 0.33 | 1/2s | instant | 1.0 | 5 |
| Railgun | Medium | Kinetic | 75 | 0.25 | 1/3s | instant | 1.1 | 10 |
| Railgun | Large | Kinetic | 75 | 0.4 | 2/3s | instant | 1.1 | 20 |
| Burst Railgun | Medium | Kinetic | 75 | 0.75 | 3/3s | instant | 1.0 | 10 |
| Burst Railgun | Large | Kinetic | 75 | 0.81 | 6/3s | instant | 1.0 | 20 |
| Rad Cannon | Medium | Radiation | 14 | 0.17 | 1/3s | 10 | 1.0 | 22 |
| Rad Cannon | Large | Radiation | 18 | 0.17 | 1/3s | 10 | 1.0 | 22 |

Shots per second tells you how fine-grained the damage is, not how much there is. Damage per second is `power / 5` whatever the weapon family; the reload downtime is already inside `Shots/s`. Apart from the power multiplier, what separates two weapons is range, projectile behaviour, damage type and size (which sets the main power roll and the pool share), not their listed rate of fire.

### Weapon power by level

An item's rolled stats come from its builder prefab. For a weapon main power line:

```
power level = round(25 x 2^(level / 10))               up to level 100
            = round(25 x 2^(10 + (level - 100) / 25))  above 100
main power  = random(min, max) x power level x rarity multiplier
```

Rarity multiplier (`GetPowerMultiplier`): Standard 1.0, Enhanced 1.1, High Grade 1.2, Exotic 1.35, Legendary 1.5. Item level is the target level plus or minus 1 unless the level is exact.

| Weapon size | Main power roll (x power level x rarity) |
|---|---|
| Tiny | 0.5 to 0.6 |
| Small | 1.0 to 1.05 |
| Medium | 2.0 to 2.1 |
| Large | 3.0 to 3.15 |

Torpedo bays use the same numbers for `TorpedoPower` (Small 1.0 to 1.05, Medium 2.0 to 2.1). Weapons also roll optional stats, count by rarity (Standard 0, Enhanced 1, High Grade 2, Exotic 3, Legendary 3), with these ranges for guns (before level and rarity scaling, the first four scaled by power level): Combat Power 0.20 to 0.25, Precision 0.20 to 0.25, Power 0.12 to 0.18, Drone Power 0.20 to 0.25, Critical Damage 0.02 to 0.025, Weapon Range 0.04 to 0.045 (rarer), Attack Speed 0.01 to 0.012, Reload Speed 0.01 to 0.012, Magazine Size 0.01 to 0.012, Turret Rotation 0.04 to 0.045 (rarer), Projectile Speed 0.04 to 0.05 (missiles). A rolled Precision of 0.2 to 0.25 power levels is worth about +1% to +1.25% crit chance. Armor, shield and hull items roll their main HP stat as a multiplier, `1 + (0.013 to 0.014) x power level x rarity`, so HP grows with item level (see [scaling](scaling.md)).

Source: `EquipmentBuilder.SetupItem`, `EquipmentBuilder.BuildInitialStats`, `EquipmentBuilder.ApplyStatScaling`, `GameMath.DamageMultiplier`, `RarityExtensions.GetPowerMultiplier`, `RarityExtensions.GetStatCount`; builder stat ranges and weapon bodies are prefab data (`EquipmentBuilderStat`, `AbstractTurret` serialized fields).

## NPC and station modifiers

NPC ships and station parts use the same hit pipeline. What differs is the stat set they carry:

- Faction NPCs apply their ship rank's flat addition to Reload Speed and Attack Speed, and the ship rank's HP multiplier to hull, armor and shield, then an HP curve by level and ship size.
- Escalation multiplies NPC hull, armor and shield by an HP multiplier, and NPC Combat, Drone and Torpedo Power by a damage multiplier, both scaled by a base multiplier of the NPC's level. It also subtracts a flat amount from all seven of the player's resistances.
- Station parts in conquest and Defense station contexts get an HP multiplier (up to x30) and a war-effort multiplier on Combat, Drone and Torpedo Power.

Numbers for these are in [scaling](scaling.md) and [stations](stations.md).

Source: `AbstractUnit.ApplyNpcRankModifiers`, `AbstractUnit.ApplyNpcShipHpCurve`, `AbstractUnit.ApplyEscalationNpcModifiers`, `AbstractUnit.ApplyEscalationPlayerResistDebuff`, `CombatStationPart.GetStationHpMultiplier`, `CombatStationPart.ApplyWarEffort`.

## What to do about it

### Against shields

Raw damage wins. Shields ignore damage type and have no resistance of their own, but your hit still passes through the target's `DamageReduction` and type resistance first. Pick the type the target resists least, and use `DamageVsShield` if you have it (it multiplies the damage that overflows into armor too). Since a shield refills only after 5 s without being hit, weapons that never stop firing keep it down for good. A burst of damage that breaks the shield helps less than a steady stream.

### Against armor

Armor takes the leftover damage after shields. Plating resists 15% of everything except Corrosion, which it takes at +25%. Corrosion hits armor for 1.25 against 0.85 for any other type, about 47% more damage per point of damage. Armor and shield do not share a resist stat, so a ship with a shield strong against your type can still be weak at the armor stage. A painter drone or painter turret removes 10 points of armor resistance (15% to 5%) and 5 points of Damage Reduction from a target for 6 seconds; it is a fleet-wide benefit and worth bringing against heavily armored targets. `DamageVsArmor` multiplies on top of all of that.

### Against hull

Once shields and armor are gone, only the target's `DamageReduction` and type resistance remain, both already taken in step 4. There is no hull type weakness. Pick the type the target resists least and keep the Painter's Damage Reduction strip up.

### The resistance cap and stacking

`DamageReduction` and the type resistance share a combined cap of 80%. A ship with 40% Damage Reduction and 50% Kinetic resistance takes the same damage from Kinetic fire as one with 20% and 60%, and gains nothing from a further point of either. Each point of either removes one point of the hit, below the cap. `DamageReduction` does it for all seven types and a resistance only for its own, so Damage Reduction is the better point when attackers mix types, and a resistance is the better point when one type does most of the damage. Once the sum for a type reaches 80%, stop adding to it.

Resistances are flat additions in a sum, not multipliers, and the escalation debuff on player ships subtracts from them directly.

### Crit chance, crit damage or plain damage

With crit chance `c`, crit damage `d` (so a crit multiplies by `2 + d`) and damage bonus `D`, one crit roll per shot and no Critical Recursion:

```
expected damage  proportional to  (1 + c x (1 + d)) x (1 + D)
```

Per percentage point, in relative gain:

| Stat | Gain per +1 point |
|---|---|
| Critical Chance | `(1 + d) / (1 + c x (1 + d))` points of damage |
| Critical Damage | `c / (1 + c x (1 + d))` points of damage |
| Damage or a type bonus | `1 / (1 + D)` points of damage |
| Combat Power multiplier | the same as its percentage, always |

Critical Damage only helps in proportion to the chance. At the base 3% crit chance a point of Critical Damage is worth about 0.03 points of damage, while a point of Critical Chance is worth about 1. Critical Chance gives a larger gain per point than Critical Damage whenever the chance is below `1 + d`, which in practice is always. So raise chance first, and add Critical Damage once the chance is high. The game's own skill pairs (Target Weak Spots, Targeting Specialist) combine both. Critical Recursion adds extra rolls at half the chance each, which makes chance worth more still.

Plain `Damage` and type bonuses have diminishing value as they stack, since they add into one `1 + D` term. Combat Power multipliers do not diminish. Attack Speed, Reload Speed and Magazine Size are real damage as shown above.

### Energy

Keep reactor usage at or under 50% for x1.20 Combat Power, and at or under 100% to avoid the penalty. Above 100% the ship loses 25% of its Combat Power, above 125% half, above 150% three quarters.

### What looks useful and is not

- Shield Recharge Delay and Shield Gate Time: shown, but the code never reads them in this version.
- Resistances past the 80% combined cap.
- Range bonus on stations: station parts carry a "RangeBonus" prefab that nothing registers (see [stations](stations.md)).
- A weapon's shots per second as a measure of its damage: damage per second follows power, not shots.
- Heat damage as a loadout: no turret deals it directly; it only appears on torpedoes, Cannon splash and Heat-type extra damage aspects.

Source: `AbstractUnit.DamageResistMultiplier`, `DamageData.CalculateDamage`, `AbstractTurret.GetExpectedDps`, `ArmorModule.ApplyArmorResistance`, `AbstractUnit.DamagePrevention`, `ReactorModule.GetEnergyBonusOrPenalty`, `LaserPainterTurret`.
