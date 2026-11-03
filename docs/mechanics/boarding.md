# Boarding and facility operations

This page covers the two crew-based operations in Vanguard Galaxy: boarding a disabled ship and entering a facility (a station-type location such as a Mining Outpost or Pirate Base; the game's tooltip title is "Facility", other game text says "installation", and the code calls these operations "dungeon" operations). Both run on the same compartment simulation. You pick crew, the game plays the fight out in real time, and you steer it with a handful of orders. All values here are read from the decompiled game code of version 0.8.3.4, not from in-game testing. Prefab-stored numbers (crew stats, facility definitions) were read from the game's asset files.

Terms and abbreviations are explained in the [glossary](glossary.md).

Words used on this page: HP means hit points. A compartment is one room of a ship or facility. A unit is one crew member, either yours (an attacker) or theirs (a defender). Staging is the area outside the ship or facility where crew wait before they enter through the Airlock. The simulation runs in short simulation steps, `dt` seconds each (I did not find a fixed step rate). Facility type names such as Mining Outpost and Military Outpost are facility types only; they have nothing to do with the economic outposts that factions build and raid in the conquest pages.

Damage to ships and stations in space combat is covered in [combat-damage.md](combat-damage.md). How ship and NPC (non-player character) levels scale is in [scaling.md](scaling.md). Station parts and their destruction are in [stations.md](stations.md).

## What you can board or enter

### Ships

A ship is boardable (the game's tooltip calls it a "Boardable ship") when it has been shot down to a wreck state, not when it is merely weak. In `SpaceShip.HandleBoardingCheck`, once a hostile ship's hull is under 40%, every 15% of max hull in damage taken below that line gives one roll. `empCharge` is the ship's EMP (electromagnetic pulse) charge, which EMP weapons build up:

```
chance = (0.005 + empFraction * 0.02) * (1 + (1 - hullFraction / 0.4) * 2) / 3 * boardingChanceMult
chance is doubled when autopilot is not running
empFraction = empCharge / maxHull
```

| Hull fraction at the roll | Multiplier on the base chance |
|---|---|
| 40% | 1/3 |
| 20% | 2/3 |
| 0% | 1 |

With no EMP charge the base is 0.005, so a roll succeeds with about 0.17% at 40% hull up to 0.5% at 0% hull. Flying manually doubles that to 0.33%-1%. Under autopilot the doubling is skipped and half of all rolls are skipped instead. A full EMP charge raises the base to 0.025, five times higher. A ship gets at most about 2.7 rolls in total (40% of its max hull in damage, one roll per 15%), so unassisted boardings are rare and EMP weapons are what make them likely. `boardingChanceMult` is 1 for normal ships. Pocket boss definitions set it to 0.05.

A ship can only become boardable if `CanBecomeBoardable` passes: it is not the player, not flagged `noBoardable`, not a carrier fighter, not a Drone-size ship, not at an Industrial Outpost location, and has no loot list or an empty one. When the roll succeeds the ship is replaced by a `HostileShip` location. Its turrets and modules switch off, it stops moving, and your targeting ignores it (`CombatModule` skips `isBoardable` units).

A boardable ship can be clicked to open the boarding panel (the tooltip reads "LMB to board", where LMB is the left mouse button). Ctrl+click engages it as a normal target instead ("ctrl+click to engage and destroy"): `BoardableUnit.EngageAsHostile` reactivates the ship with at least 15% hull, marks it `noBoardable` so it cannot be boarded afterwards, and fails any capture mission tied to it. Capture missions also spawn boardable ships directly (`CaptureShip`): those cannot flee or scuttle, and have a crew fill rate of 0.4.

Source: `SpaceShip.HandleBoardingCheck`, `SpaceShip.CanBecomeBoardable`, `SpaceShip.TransitionToBoardable`, `BoardableUnit.EngageAsHostile`.

### Facilities

Facilities are station-type locations on the map (`DungeonLocationData`). The 11 `DungeonType` values are Mining Outpost, Salvage Yard, Research Station, Pirate Base, Relay Station, Industrial Facility, Black Market Hub, Military Outpost, Derelict Colony Ship, Ancient Ruin and Hostile Ship (the boardable ship above). They are generated with these rules:

| Type | Chance to be abandoned | Default hostility in the definition |
|---|---|---|
| Ancient Ruin, Derelict Colony Ship | 100% | Low, None |
| Mining Outpost | 50% | None |
| Research Station | 50% | Low |
| Relay Station | 45% | None |
| Salvage Yard | 40% | None |
| Industrial Facility | 20% | Low |
| Everything else | 10% | see the type table below |

An abandoned facility has no faction and hostility None (the tooltip's Hostiles row), so it has no defenders. A staffed one gets hostility High if its owning faction is your enemy (at war with you, or reputation below -500), otherwise Low, but only if the definition's own hostility is above None. Mining and Industrial facilities spawned by the 25% mining-location roll (levels 5 and up) skip that last check and take the faction-based rating directly. Mission Pirate Bases take their rating from mission difficulty (Easy Low, Hard/Skull/Insane High, otherwise Medium).

Entry conditions:

| Condition | Effect |
|---|---|
| Integrity at or below 10% | Cannot enter ("Facility too damaged to enter. Structural integrity must exceed 10%."). `DungeonData.isEnterable` |
| No docking pad or tunnel left standing | Marks `dockingDestroyed` permanently and abandons the attempt |
| Location level 10 or more above your effective level | Cannot start (`DungeonManager.ExceedsLevelGap`). Applies to facilities only, not to ship boarding |
| Ship is travelling | Cannot start |
| Host station destroyed during approach | Abandons. During the run it sets integrity to zero |

Source: `DungeonData.isEnterable`, `DungeonManager.StartOperation`, `DungeonOperation.HandleDockingUnavailable`, `DungeonOperation.NotifyHostStationDestroyed`, `MapPointOfInterest.RollIsAbandoned`, `MapPointOfInterest.HostilityFromFaction`, `MapPointOfInterest.BuildDungeonLocationData`.

### Reputation cost of entering

Entering a facility or boarding a ship whose faction has a reputation with you above -500 (Neutral or better) costs reputation at the moment you press start, before the approach finishes:

```
penalty = ceil(baseExp * 0.5) * 40
```

| Target | baseExp | Penalty |
|---|---|---|
| Any facility | 25 | 520 |
| Ship | the ship's `baseExperienceReward` (its base experience points, XP) | 40 per half-point of base experience, rounded up |

Having the Umbral transponder removes the penalty. Ships flagged `noReputationLoss` skip both the penalty and the "faction goes hostile" step. Once the crew actually lands, a friendly faction is marked hostile to you in that location (`MarkFactionPlayerHostile`). Cancelling the approach does not refund the reputation.

Source: `DungeonManager.FriendlyEntryRepPenalty`, `DungeonManager.ApplyFriendlyFactionPenalty`, `DungeonManager.TriggerFriendlyFactionAggro`.

### Hard-boarding factions

Meridia's Chosen (`fanatics`), Canisec (`policeGuild`), the Malcyte (`amalgam`) and The Radiant (`holyRadicals`) are flagged `isHardBoardingFaction`. Their ships are simulated at level `max(shipLevel, yourEffectiveLevel + 10)` unless the boarding comes from a capture mission. That alone puts the defenders at least 10 levels above you, which sets their HP multiplier to about 2.8 (see [level scaling](#level-scaling-of-defenders)).

Source: `Faction.isHardBoardingFaction`, `DungeonSimulation.ComputeCombatLevel`.

## What each tooltip number means

### Ship tooltip (boarding in progress)

Hovering a ship under attack shows:

| Line | Meaning |
|---|---|
| Status + percentage | The live estimate (see below) |
| N attackers vs. M defenders | Count of living units on each side, including your crew still waiting in staging |
| Ship Integrity | `structureIntegrity / maxStructureIntegrity` of the target |
| Attacker Morale | Average `morale` of your living units |
| Defender Morale | Average `morale` of living defenders |

Source: `BoardableUnit.BuildActiveTooltipContent`, `BoardableUnit.AddSimStatBars`, `DungeonSimulation.EstimateOutcome`.

### Facility tooltip

| Row | Meaning |
|---|---|
| Type | Facility type |
| Status | In Progress while the simulation runs; Abandoned if abandoned |
| Hostiles | The location's `DungeonHostilityRating`: None "No hostiles expected", Low "Unlikely to be dangerous", Medium "Expect some resistance", High "Expect heavy resistance" |
| Specialist | The crew types that count as specialists for this facility (hidden for Ancient Ruin, which uses the diversity bonus) |
| Estimate | Outcome label with win chance, only shown once defenders exist |
| Crews | `your living units vs their living units` |
| Progress | Compartments in state Friendly divided by total compartments |
| Integrity | `structureIntegrity / maxStructureIntegrity`; before entry it shows the stored `facilityIntegrity` |
| Attacker morale / Defender morale | Averages as above; the defender row only appears if defenders exist |

Source: `CombatStationPart.AddActiveDungeonContent`, `CombatStationPart.AddIdleDungeonContent`, `CombatStationPart.AddEstimateRows`, `CombatStationPart.ComputeExplorationProgress`.

### The Estimate formula

Once a simulation exists:

```
ratio     = totalAttackerPower / max(1, totalDefenderPower)
winChance = ratio / (ratio + 1)
```

Both totals are summed over every living unit, so troops still in staging count. Units in transit between rooms count as zero for that instant.

| Win chance | Label |
|---|---|
| above 75% | Favourable |
| above 50% | Contested |
| above 25% | Difficult |
| 25% or below | Very Difficult |

This is a power ratio, not a simulation. It ignores HP, positions, specialists, events and everything defenders do besides fight. Crew states change it live: Wounded units count half, Panicked 60%, Heroic 140%. Defender power uses the faction defense multiplier and armory efficiency but not Boarding Suppression, the drone bonus or the level HP multiplier.

The pre-start estimate in the crew-selection panel is cruder. It does not use the simulation at all:

```
attackerPower = sum(boardingPower * count) * stealthMod * ammoEstimate
   stealthMod:   Silent 0.8, Balanced 1.0, Aggressive 1.2
   ammoEstimate: Hollow 0.9, Standard 1.0, Armor Piercing (AP) 1.1

Ships:
  defenders     = round(maxGrunts * crewFillRate)       (crewFillRate defaults to 0.7; faction value or per-ship override)
  defenderPower = defenders * 3 * defenseModifier * (1 + droneBonus if the faction has drones)
                  * sqrt(levelDifficultyMod(combatLevel, yourEffectiveLevel))

Facilities:
  compartments  = average of the definition's min and max compartments
  defenders     = max(2, round(compartments * fraction)),  fraction: Low 0.3, Medium 0.5, High 0.8
  defenderPower = defenders * (weighted average boardingPower of the facility's defender mix)
                  * sqrt(levelDifficultyMod(level, yourEffectiveLevel))
  hostility None: Favourable, 100%, no defenders
```

The pre-start estimate leaves out your Assault Doctrine skill and equipment boarding power, so you are usually a little stronger than it says. It assumes every defender has 3 power; an actual ship's crew is a mix of the faction's crew types. The pre-start figure is also computed from the location's current level, so it moves with your own level.

Source: `DungeonSimulation.EstimateOutcome`, `DungeonSimulation.EstimateFromData`, `DungeonSimulation.EstimateBoardingFromData`, `DungeonSimulation.EstimateInstallationFromData`, `DungeonSimulation.BuildEstimate`, `DungeonSimulation.ClassifyWinChance`, `DungeonOptions.GetAmmoAttackerPowerEstimate`.

## Crew types

Crew stats come from the `CrewType` prefabs. In the simulation a unit has `5 + resistance` HP (before modifiers) and `boardingPower` power.

| Crew type (in-game name) | Boarding power | Resistance | HP | Cost (credits) |
|---|---|---|---|---|
| Deckhand | 1 | 1 | 6 | 3,500 |
| Gunner | 2 | 3 | 8 | 7,000 |
| Marine | 4 | 8 | 13 | 16,000 |
| Pilot | 2 | 2 | 7 | 22,500 |
| Miner (`Prospector`) | 1 | 2 | 7 | 7,000 |
| Salvager (`Scrapper`) | 1 | 2 | 7 | 7,000 |

Marines carry four times a Deckhand's power and over twice the HP for under five times the price. They also use one slot in a compartment where four Deckhands would use four. Compartment capacity limits headcount, not power, so a room that holds six units holds 24 power of Marines and 6 of Deckhands.

Your own units get two multipliers from skills (see [Skills and gear](#skills-and-gear)): HP is multiplied by `1 + CrewResistanceBonus`, and power by `1 + AssaultDoctrine + equipment BoardingPower`. Defenders get neither.

Source: `CrewType` prefabs, `SimCrewUnit.InitHp`, `SimCrewUnit.EffectivePower`.

## How the fight is resolved

### Unit states and power

`EffectivePower` for a living, non-transit unit:

```
power = boardingPower
      * stateMod (Active 1, Heroic 1.4, Wounded 0.5, Panicked 0.6)
      * dazed (2/3 while daze timer is running)
      * sideMod
      * armoryEfficiency  (defenders only, 1.0 for you)
      * (1 + AssaultDoctrine + equipment BoardingPower)   (your units only)
```

`sideMod` for your units is the stealth power modifier: Silent 0.8, Balanced 1.0, Aggressive 1.2. For defenders it is the faction `defenseModifier` (table below).

Defender power in a compartment is then multiplied by `(1 + droneDefenseBonus)` if the faction has drones, by `(1 - BoardingSuppression)`, and by a level power modifier that is fixed at 1 in the current code.

Defenders in a compartment do not fight unless that compartment's alert state is Alert (ships and armed facilities set `requiresAlertForDefenderCombat`). A contested compartment is forced to Alert on the next simulation step.

Source: `SimCrewUnit.EffectivePower`, `DungeonSimulation.ComputeHostilePowerIn`, `DungeonSimulation.ComputeFriendlyPowerIn`, `DungeonSimulation.ApplyAttackerModifiers`.

### Damage per simulation step

Only compartments in state Contested (both sides present) fight. For each:

```
A = friendly power in the compartment,  D = hostile power in the compartment

hitsOnYou      = 10 * (D/(A+D)) * 0.5 * dt * casualtyMod * barricadeMod * armorMod
hitsOnDefender = 10 * (A/(A+D)) * 0.5 * dt * ammoMod

casualtyMod: Silent 0.75, else 1.0
barricadeMod: 0.65 if the compartment's barricade is complete, else 1.0
armorMod: 0.9 if you have Body Armor in cargo or have found one during this run, else 1.0
ammoMod: average over defenders present; see the ammo table
```

The fractional part of the hit count is rolled each simulation step. Each hit removes 1 HP from a random living, non-transit unit on that side in that compartment. At 0 HP the unit is killed.

Two consequences follow:

1. The total damage rate is capped near 5 hits per second across both sides, split by power share. More power on one side shifts the split but does not speed up the fight. Absolute numbers matter only through HP pools: more bodies and more HP in the room mean the fight lasts longer.
2. A side with 80% of the power takes 1 hit per second on average and deals 4.

Hits that do not kill can wound. After the hit, a unit below its resistance in HP rolls `1 - hp/resistance`; a success makes it Wounded (half power, flees to staging). Heroic units are protected above half HP and wound at half the usual chance. Wounded units retreat to staging and recover `maxHp/75` HP per second there; at 80% HP they return to Active.

Source: `DungeonSimulation.TickCompartmentCombat`, `DungeonSimulation.ApplyCombatDamage`, `DungeonSimulation.RollDamage`, `DungeonSimulation.ApplyDamageToUnit`, `SimCrewUnit.TryWound`, `DungeonSimulation.TickWoundedRecovery`, `DungeonSimulation.GetBarricadeDamageMultiplier`, `DungeonSimulation.GetArmorDamageMultiplier`.

### Ammo and stealth

| Option | Effect |
|---|---|
| Hollow ammo | 0.8x damage dealt to Marines and Gunners; 1.0x to everyone else. Structural damage per kill 1.5. Adds 0.2 to alert noise |
| Standard ammo | 1.0x. Structural damage per kill 2 |
| Armor Piercing (AP) | 1.2x to Marines and Gunners; 1.0x to the rest. Structural damage per kill 2.5 |
| Silent | Your power x0.8, casualty rate x0.75, alert noise 0.1 |
| Balanced | Alert noise 0.5 |
| Aggressive | Your power x1.2, alert noise 1.0 |

Silent's net effect on a single fight is close to neutral: you deal less (your power share drops) and take 25% fewer hits. What it buys is time before the defenders' nearby rooms go on alert.

Source: `DungeonOptions.GetAmmoTargetMod`, `DungeonOptions.GetShipIntegrityDamagePerKill`, `DungeonOptions.GetNoiseLevel`, `DungeonOptions.GetStealthAttackerPowerMod`, `DungeonOptions.GetStealthCasualtyMod`.

### Morale

Morale is 0-1 per unit. It moves in these ways:

| Event | Effect |
|---|---|
| Your unit is killed | Hostile units in that compartment +0.04 each. Your remaining units in that compartment -0.02 each |
| A defender is killed | Your units in that compartment +0.04 each plus +0.02 more (from `ApplyCasualties`) |
| In a contested room, your units | `-(hostiles/friendlies) * 0.02` per second |
| In a contested room, defenders | `-(your units/their units) * 0.03` per second, times a health factor from 0.2 (full HP) up to 1.5 (near death) |
| Assault momentum (your units, any room, while any fight is active) | `+0.02` per second, fading to zero over the first 30 s |
| Bridge taken | `+0.02` per second to all your units |
| Unit's room is cut off from the airlock by non-friendly rooms | `-0.05` per second |
| More than 60% of deployed crew wounded while defenders are stronger | `-0.015` per second |
| Loss trauma | `-(deadFriendlies / totalFriendlies) * 0.015` per second, to all your living units |
| Not in a contested room (or in staging) | `+1/150` (0.0067) per second recovery |
| Defender becomes Heroic | `+0.15` for that defender |

The assault bonus, cut-off and wounded penalties only apply when `tracksAttackerMorale` is set (ships and armed facilities). Unarmed facilities skip morale tracking altogether.

Below 0.2 morale an Active attacker has a 3% chance each update to become Panicked; a Panicked unit above 0.5 morale has a 2% chance to recover. These chances are per update, not scaled by time. Panicked and Wounded units retreat toward the airlock after a 2-5 second delay when morale is 0.35 or below (Wounded units always retreat).

Defender morale works the same way with the faction `moraleThreshold` (default 0.25). Below it, each defender in a contested room has a `surrenderChance * dt` per second chance to surrender, and every update an Active defender anywhere has a `surrenderChance * 0.1 * min(1, yourPower/theirPower)` chance to surrender, otherwise a 2% chance to panic. Panicked defenders in a room you hold or contest retreat to an adjacent defender-held room with spare capacity. If none exists, they may surrender (`surrenderChance * 3 * dt` per second) or, for factions with `kamikazeChance`, blow themselves up for 1 damage to one of your units in the room.

If the average morale of your living units falls under 0.15 on a ship boarding, the crew breaks off and retreats (`DungeonOutcomeReason.MoraleCollapse`). Facilities only retreat on morale collapse through the autopilot director (see [Autopilot](#autopilot)). During a manual facility run there is no automatic morale retreat.

Source: `DungeonSimulation.ApplyCombatDamage`, `DungeonSimulation.TickCompartmentCombatMorale`, `DungeonSimulation.TickHostileMoraleInCompartment`, `DungeonSimulation.TickAttackerMorale`, `DungeonSimulation.ComputeWoundedMoralePenalty`, `DungeonSimulation.TickLossTraumaDrain`, `DungeonSimulation.TickIdleMoraleRecovery`, `DungeonSimulation.UpdateAttackerState`, `DungeonSimulation.TickGlobalDefenderMorale`, `DungeonSimulation.CheckAttackerMoraleCollapse`, `DungeonSimulation.TickPanickedUnit`.

### Surrender, capture, defection, heroics

| Mechanic | Rule |
|---|---|
| Surrender in combat | Defender morale under the faction threshold and `surrenderChance * dt` per second per defender. Surrendered units are prisoners |
| Mass surrender | Needs your units on the Bridge, your total power at least equal to theirs, no `noMassSurrender` flag, `surrenderChance` above zero, and defender-held rooms at or under 20% of the ship. Then `surrenderChance * 5` per update. All living defenders surrender and you win |
| Outnumbered capture | One defender alone against two or more of your units: `0.04 * min(yourUnits - 1, 4) * dt` per second to capture that defender (x0.25 if the faction never surrenders) |
| Defection | Defender under 0.1 morale while outnumbered 2:1 or more: `0.02 * dt` per second to join you |
| Heroics (yours) | Per defender killed in a compartment, each Active unit of yours there has `0.025` (`0.045` if a Heroic is already present) to become Heroic |
| Heroics (theirs) | Per unit of yours killed, each Active defender has `0.04 + 0.04 * existingHeroics` (cap 0.2) to become Heroic |

Prisoners go to your brig up to your ship's brig capacity (see [Pods](#pods-and-crew-return)). Surrendered and captured units no longer count as living.

Source: `DungeonSimulation.TrySurrenderInCombat`, `DungeonSimulation.CheckMassSurrender`, `DungeonSimulation.TryCaptureOutnumbered`, `DungeonSimulation.TryDefectInCombat`, `DungeonSimulation.TryGrantHeroicInCompartment`, `DungeonSimulation.TryGrantHeroicToDefenderInCompartment`.

## Level scaling of defenders

Defenders get an HP multiplier from the level gap `g = max(0, locationLevel - yourLevel)` (`dungeonLevel` in code):

```
f      = 2^(1 + min(g, 15)/5) * lerp(0.5, 1, min(g/10, 1))
hpMod  = sqrt(f)
```

| Gap | hpMod |
|---|---|
| 0 | 1.00 |
| 1 | 1.12 |
| 3 | 1.40 |
| 5 | 1.73 |
| 7 | 2.12 |
| 10 | 2.83 |
| 15 and above | 4.00 |

`hpMod` multiplies defender HP at spawn, the number of units in a combat ambush, and the HP damage of hazards on your crew. It does not multiply their power. Ships use your effective level; facilities use `GamePlayer.level`.

Gaps above zero also shift the defender mix toward Marines. The Marine weight moves from its base share toward 80% of total weight as `g / 15` rises, taking from Deckhand and Gunner weight (`ApplyMarineGapShift`). At a gap of 15, up to 80% of the defenders are Marines if there are enough Deckhands and Gunners to convert. Hazard event chances rise by `2^(g/10)`.

Source: `DungeonSimulation.ApplyLevelScaling`, `DungeonSimulation.ComputeLevelDifficultyMod`, `DungeonSimulation.ApplyMarineGapShift`, `DungeonFacilityEvents.ComputeEventWeightDifficultyMod`.

## Faction defender profiles

Ship defenders (and facility defenders from a faction) use `FactionBoardingProfile`. Unlisted factions use the default (all modifiers 1, surrender 5%, morale threshold 25%).

| Faction | Defense x | Surrender | Other |
|---|---|---|---|
| Default | 1.0 | 5% | morale threshold 0.25 |
| Corsair Syndicate (`marauders`) | 1.0 | 3% | scuttle 20%, counterattack +0.35, alert speed x1.3, brig crew useful |
| Meridia's Chosen (`fanatics`) | 1.0 | 0% | scuttle 100%, can vent airlocks, kamikaze 5%, alert x1.5, brig crew useful |
| Stellar Industries (`blue`) | 1.1 | 10% | side-switch 5%, morale threshold 0.35 |
| Kolyatov Collective (`red`) | 1.1 | 0% | counterattack +0.1 |
| Luminate Combine (`gold`) | 1.1 | 8% | drones (+15% defender power), kamikaze 3% |
| Canisec (Police Guild) | 1.3 | 5% | no mass surrender, brig crew useful |
| Omnitac Agency (Mercenary Guild) | 1.1 | 5% | can be bought out |
| Darkspace Compact (`darkspacers`) | 1.4 | 0% | counterattack +0.5, alert x1.3 |
| Void Drifters (Smugglers) | 1.2 | 8% | can be bought out |
| Malcyte (`amalgam`) | 1.5 | 0% | scuttle 100%, can vent airlocks, counterattack +0.5 |
| The Radiant (Holy Radicals) | 1.0 | 0% | kamikaze 8%, can vent airlocks |
| Mindus Holdings, Steel Vultures, Forge Industries (Mining, Salvage, Industrial Guilds) | 0.8 | 15% | none |
| Stranded | 1.2 | 5% | counterattack +0.15 |

Source: `FactionBoardingProfile.Build`.

## Compartments

### States

| State | Meaning |
|---|---|
| Unknown | Never entered (shown as unexplored) |
| Investigating | Your units are present, no hostiles, investigation not yet complete. Counts as friendly-controlled. The game labels it "Investigating", or "Securing" for a corridor |
| Contested | Both sides present. This is where fighting happens |
| Friendly | Cleared and investigation complete. The game labels it "Cleared" |
| Hostile | Defenders only |
| Destroyed | Gone (scuttle armory) |

A compartment's state is recomputed from who is standing in it. When both sides are present it is Contested. When only you are there it is Investigating or Friendly. When only defenders remain it is Hostile, but a room that was Unknown or Investigating stays that way if the simulation uses investigation.

Source: `DungeonSimulation.SyncCompartmentState`, `DungeonSimulation.GetFriendlyClearedState`.

### Capacity

Base capacity per type, multiplied by the ship size multiplier. Facilities always use multiplier 1.

| Compartment | Base capacity |
|---|---|
| Airlock, Reactor Room, Brig, Control Room, Workshop, Laboratory, Generator Room, Server Room, Security Office, Medical Bay | 4 |
| Corridor, Armory, Officers Lounge, Ore Processing, Excavation | 5 |
| Cargo Hold, Living Quarters, Mess Hall, Storage Bay, Debris Field | 6 |
| Bridge | 3 |

Ship size tiers 0-8 multiply by 1, 1, 1.2, 1.5, 1.8, 2.2, 2.8, 3.5, 5 (rounded down). Capacity applies to each side separately.

Source: `SimCompartmentData.BaseCapacity`, `SimCompartmentData.EffectiveCapacity`.

### Ship layouts

Every ship has an Airlock, Bridge and Cargo Hold. Larger sizes add rooms:

| Size tier | Rooms added |
|---|---|
| 2 | Corridor |
| 3 | Living Quarters, Reactor Room |
| 4 | Brig, Corridor |
| 5 | Mess Hall, Armory |
| 6 | Officers Lounge, Corridor, a second Airlock, Living Quarters, Cargo Hold, Mess Hall, Reactor Room |
| 7 | Living Quarters |
| 8 | A third Airlock, Cargo Hold, Living Quarters, 12 more Corridors |

Only the first Airlock is where your crew lands. Before overflow corridors are added, size 1 ships have 3 compartments, size 3 has 6, size 5 has 10, size 6 has 17 and size 8 has 33.

Ship integrity is `compartments * 1000`, and starts at `max * min(1, hullFraction + 0.2)` where `hullFraction` is the target's hull when you start. A ship you shot down to 20% hull starts at 40% integrity.

Defenders per ship: the ship's `crewData` (loaded at the fill rate if empty). Each crew type has a home room preference (Marine Armory 60%, Gunner Armory 45%, Deckhand Living Quarters 50%, Miner Cargo Hold 50%, Salvager Reactor Room 40%), then they fall back to a random room within capacity. The first `clamp(1 + sizeTier/3, 1, 3)` defenders are moved to guard the Airlock.

Source: `ShipCompartmentLayout.Build`, `ShipCompartmentLayout.CrewHomePreferences`, `DungeonSimulation.CreateForShipBoarding`, `DungeonSimulation.AssignAirlockGuards`.

### Facility layouts and sizes

The compartment count is a random whole number from `min + floor(n/2)` to `max + n`, where `n = min(floor(level/12), 8)`. Every facility has one Airlock, one to four Corridors and the rest from the type's weighted pool.

| Type | Compartments (min-max) | Layout | Speed bonus | Hostility |
|---|---|---|---|---|
| Mining Outpost | 6-12 | Tunnel, docking pad | 0.4 | None |
| Salvage Yard | 6-16 | Radial | 0.4 | None |
| Research Station | 8-14 | Grid | 0.4 | Low |
| Pirate Base | 10-18 | Radial | 0.25 | Medium |
| Relay Station | 5-10 | Tunnel | 0.4 | None |
| Industrial Facility | 10-16 | Grid | 0.4 | Low |
| Black Market Hub | 6-12 | Tunnel | 0.3 | Low |
| Military Outpost | 10-18 | Grid | 0.2 | High |
| Derelict Colony Ship | 14-20 | Radial | 0.4 | None |
| Ancient Ruin | 6-16 | Radial | diversity | Low |
| Hostile Ship | 4-16 | Ship | n/a | High |

Maximum facility integrity is `compartments * 3000`. Starting integrity is a random fraction:

| Facility | Starting integrity |
|---|---|
| Ancient Ruin, Derelict Colony Ship | 45-75% |
| Other abandoned | 55-80% |
| Staffed | 80-100% |

The fraction is stored on the location and carries over between visits. Compartments of type Control Room, Storage Bay, Workshop, Laboratory, Server Room or Security Office are locked 35% of the time.

Source: `DungeonDefinition` assets, `DungeonOperation.ApplyDefinitionToSim`, `DungeonOperation.InitialiseFacilityIntegrity`, `DungeonCompartmentLayout.Build`, `DungeonCompartmentLayout.LockableTypes`.

## Alert, investigation and movement

### Alert

Each compartment has an alert state: Dazed, Relaxed, Concerned, Worried, Alert. Every compartment starts Dazed. A compartment not yet in Alert advances only while it touches a compartment that is Contested or friendly-controlled. It accumulates `dt * noise * factionAlertSpeed * random(0.3, 1.7)` and leaves each state at these thresholds:

| From | Threshold |
|---|---|
| Dazed | 14 |
| Relaxed | 9 |
| Concerned | 7 |
| Worried | 5 |

That is 35 units from Dazed to Alert. At Balanced noise (0.5) a room next to your crew goes Alert after about 70 seconds on average. At Silent (0.1) it takes about 350 s and at Aggressive (1.0) about 35 s. Comms Down halves the noise. `factionAlertSpeed` is 1.3-1.5 for some factions.

An Alert room with more than half its original occupants sends one defender per second-ish (`0.3 * dt` chance per second) into an adjacent Contested room that is under capacity. Counterattacks also need Alert. So noise decides how many defenders end up joining a fight you started elsewhere.

Source: `DungeonSimulation.TickCompartmentAlert`, `DungeonSimulation.AlertThreshold`, `DungeonSimulation.GetAlertNoiseLevel`, `DungeonSimulation.TickDefenderMovement`.

### Investigation

Units in a Friendly-controlled but not finished room investigate it:

```
progress/s = (1 / investigationDuration) * (1 + 0.15 * (unitsPresent - 1)) * professionMod
professionMod = 1 + 0.2 * (distinctCrewTypes - 1)            (Ancient Ruin, diversity)
              = 1 + professionSpeedBonus                       (a specialist is present, and the room type is one the definition marks)
              = 1                                              (otherwise)
```

A room becomes Friendly when progress reaches 1 and all its events have fired. Base durations:

| Room | Seconds |
|---|---|
| Corridor | 5 |
| Medical Bay | 50 |
| Brig | 50 |
| Living Quarters | 45 |
| Storage Bay, Generator Room, Cargo Hold | 60 |
| Debris Field, Security Office | 70 |
| Workshop | 75 |
| Ore Processing, Armory, Server Room | 80 |
| Control Room, Laboratory | 90 |
| Excavation | 100 |
| Other / unlisted | 60 |

On ships only the Cargo Hold (20 s) and Armory (25 s) take investigation time unless the ship has hidden caches, in which case every non-airlock, non-corridor room takes 25 s. Other ship rooms resolve at 1 progress per second.

Adding crew to a room has strongly diminishing returns on investigation speed: 6 units give `1 + 0.15 * 5 = 1.75x`.

Source: `DungeonSimulation.TickCompartmentInvestigation`, `DungeonSimulation.CalculateInvestigationRate`, `DungeonSimulation.GetInvestigationProfessionMod`, `DungeonCompartmentLayout.BaseInvestigationTime`, `DungeonCompartmentLoot.InjectShipCargoHold`, `DungeonCompartmentLoot.InjectShipArmory`.

### Movement and orders

If automatic movement is on (the game's "Automatic movement" setting, `autoMove`), your units spread automatically: they push from friendly rooms into adjacent enemy rooms (strongest units first), fill under-staffed rooms that are investigating, flow toward contested front lines and explore unknown rooms. With it off, only directives and explicit orders move units.

Directives are the internal orders that pull units to a room. When two compete, the higher priority wins (`DirectivePriority`, lowest to highest):

| Priority | Created by |
|---|---|
| MissedLootRevisit | A profession discovery fired with no specialist present; sends a specialist back |
| ProfessionCallIn | A room with an unfired profession event has no specialist; sends one |
| CombatReinforcement | A room just became Contested (combat ambush) |
| PlayerMovementOrder | Your orders |

Your orders: select a compartment, then 1 sends Combat crew (highest boarding power first), 2 sends Specialists, 3 sends any. Hold the number and scroll to set the count. E (or Ctrl+click) sets the room as Priority: new crew are routed there first. R retreats units out of a compartment and clears orders there. G throws a grenade, B toggles a barricade. Orders can only draw from units that are idle, then investigating, then (if the target is contested) units in other contested rooms.

Moving from room to room takes 0.7 s in transit. Units have a 2 s dwell timer after arriving before they can be pulled elsewhere (1 s when retreating). Units in transit do not fight.

Source: `DungeonSimulation.TickAttackerFlow`, `DungeonSimulation.TickDirectives`, `DungeonSimulation.IssueMovementOrder`, `DungeonSimulation.RetreatFromCompartment`, `DirectivePriority`, `DungeonSimulation.StartTransit`, `DungeonSimulation.TickTransitPool`.

### Locks and lockdowns

A locked room needs a specialist standing in an adjacent room: a unit that `IsAdvantagedCrew` starts a 10-second unlock timer. At Ancient Ruin, where the diversity bonus replaces specialists, locks only open with 3 or more distinct crew types alive. The game routes a specialist toward a locked room automatically when one exists.

Locked rooms are not counted for the "all compartments explored" win condition. They carry bonus loot when opened, and special-loot chances in locked rooms are multiplied by 6.

A Lockdown event (Military Outpost control rooms) seals adjacent corridors: 15 seconds, or 10.5 s with a specialist in the room. Only specialists can move into a sealed room, and doing so cancels the lockdown.

Source: `DungeonSimulation.TryUnlockCompartment`, `DungeonSimulation.TickUnlockAttempts`, `DungeonSimulation.FireLockdownEvent`, `DungeonSimulation.MoveCrewTo`, `DungeonCompartmentLoot.InjectDungeonLoot`.

### Barricades and grenades

Only on ships and armed facilities (`hasDefenderPlacement`).

| Action | Rule |
|---|---|
| Barricade | Needs a Friendly-state room. Build rate `0.05 * (1 + 0.15 * (crew - 1))` per second, so 20 s with one unit. Pauses while the room is contested; cleared if defenders retake it. Complete: damage to your units there x0.65. Units in a barricaded room stay put instead of pushing forward |
| Grenade | Charges: `3 + sizeTier/2` (ship, integer division) or `3 + compartments/4` (armed facility). Cooldown 10 s. Target room must have a living defender and be yours, contested or adjacent to yours. Sets the room to Alert; every unit in it, both sides, takes 3 damage and is dazed 15 s (power x2/3); the structure takes 5% of its maximum |

A Deckhand (6 HP) survives one grenade and a Gunner (8 HP) two. Marines (13 HP) survive four, before the HP skill bonus for your units or the level multiplier for defenders. Everyone in the room also loses a third of their power for 15 seconds, and this hits your own crew in the room as much as theirs.

Source: `DungeonSimulation.ToggleBarricade`, `DungeonSimulation.CalculateBarricadeRate`, `DungeonSimulation.ThrowGrenade`, `DungeonSimulation.CanThrowGrenade`, `DungeonSimulation.CreateForShipBoarding`, `DungeonOperation.InitialiseWalkSimulation`.

## Specialists and the diversity bonus

Each facility definition lists one or more crew types as its specialists. A specialist does four things:

1. Unlocks locked rooms (10 s timer).
2. Collects profession discoveries. Loot that needs a specialist is lost for now if none is in the room when the event fires at its progress threshold. The room then asks for a specialist to come back (revisit).
3. Raises investigation speed in rooms where the definition has a profession event (the `professionSpeedBonus`).
4. Protects against bad events (see below). This applies in any room, as long as the specialist is standing in it.

Profession discoveries check for the first crew type in the definition's list specifically, not any specialist. The list comes from the asset data:

| Facility | Specialists (first one collects profession loot) | Profession loot rooms |
|---|---|---|
| Mining Outpost | Miner | Ore Processing (crystal vein), Excavation (geological data) |
| Salvage Yard | Salvager | Debris Field, Workshop |
| Research Station | Salvager | Laboratory, Workshop |
| Pirate Base | Marine, Gunner | none |
| Relay Station | Marine | Server Room |
| Industrial Facility | Salvager, Miner | Workshop, Ore Processing |
| Black Market Hub | Deckhand | Cargo Hold |
| Military Outpost | Marine | Armory |
| Derelict Colony Ship | Salvager, Deckhand | Living Quarters, Debris Field, Workshop |
| Ancient Ruin | Diversity bonus (no specialists) | none |

The speed bonus applies only in the "profession loot rooms" column. A Miner at an Industrial Facility opens locks and shields against events, but does not collect the Salvager's discoveries.

At an Ancient Ruin, `usesDiversityBonus` is set. Every unit counts as a specialist for event protection, investigation speed is `1 + 0.2 * (distinctCrewTypes - 1)` everywhere, and locks require three or more distinct types.

Protection against negative events (Hazard, Combat ambush, Lockdown), when the event fires:

```
needed = 1 + floor(levelGap / 3)
if specialistsInRoom >= needed:  33% chance the event is fully prevented
```

If it fires anyway:

| Event | Specialist effect |
|---|---|
| Hazard crew damage | `max(1, round(2 * hpMod * (1 - 0.125 * min(specialists, 4))))` damage per victim. Non-specialists take double and have a 20% instant kill chance |
| Hazard facility damage | Multiplied by `1 - 0.125 * min(specialists, 4)` |
| Combat ambush | Number of spawned defenders x0.7 (minimum 1) |
| Lockdown | Timer 10.5 s instead of 15 s |

`professionDiscoveryBonus` (0.25) exists in each definition but nothing in the code reads it.

Source: `DungeonDefinition` assets, `DungeonSimulation.ApplyDefinition`, `DungeonSimulation.IsAdvantagedCrew`, `DungeonSimulation.TryPreventNegativeEvent`, `DungeonSimulation.GetSpecialistsNeededToPrevent`, `DungeonSimulation.GetHazardDamageMultiplier`, `DungeonSimulation.FireDiscoveryEvent`, `DungeonOperation.BuildEvent`, `DungeonDefinition.GetAdvantagedCompartmentTypes`.

## Events inside a facility

Events trigger when a room's investigation progress crosses their threshold (0 means as soon as progress is above zero, so on entry).

### Per-room template events

Each non-airlock room rolls 0-3 events from its definition's weighted templates (`weight` per template). These are discoveries (loot), hazards, combat ambushes, lockdowns or interactions. Templates marked active-only never appear in abandoned facilities.

Combat ambush size: `randomRange(2, max(3, 3 + floor(compartments/5)))`, times `hpMod`, rounded; x0.7 with a specialist. Units come from the type's mix:

| Facility | Mix (weights) |
|---|---|
| Pirate Base | Marine 50, Gunner 30, Deckhand 20 |
| Military Outpost | Marine 70, Gunner 20, Salvager 10 |
| Mining Outpost | Miner 40, Marine 30, Salvager 30 |
| Industrial Facility | Salvager 40, Miner 20, Marine 30, Deckhand 10 |
| Relay Station | Marine 40, Salvager 30, Deckhand 30 |
| Research Station | Salvager 40, Marine 30, Deckhand 30 |
| Black Market Hub | Deckhand 50, Gunner 30, Marine 20 |
| Salvage Yard | Salvager 50, Marine 30, Deckhand 20 |
| Other | Marine 30, Gunner 25, Salvager 25, Deckhand 20 |

The templates' `hostileCount` and `hostilePowerPerUnit` fields are in the asset data but not read by the code.

### Facility hazard events

On top of the templates, `DungeonFacilityEvents` adds hazards that damage the facility. Each non-airlock, non-corridor room rolls each eligible one with chance `baseWeight * 2^(gap/10) * 0.5` (universal ones are x1.5 in abandoned facilities). They trigger at 20-79% room progress.

| Hazard | Damage to integrity | Base weight | Rooms / facility types |
|---|---|---|---|
| Fire | 4,500 | 0.25 | any |
| Pressure lost | 5,400 | 0.20 | any |
| Structural collapse | 4,500 | 0.20 | any |
| Power grid failure | 3,000 | 0.35 | Generator, Control, Server rooms |
| Booby trap | 4,500 | 0.4 | Pirate Base, Military Outpost, Black Market Hub |
| Explosive charge | 6,000 | 0.5 | Armory / Cargo Hold in Pirate Base, Military Outpost |
| Drilling accident | 4,500 | 0.5 | Excavation / Ore Processing |
| Pressure explosion | 6,000 | 0.45 | Workshop / Ore Processing in Industrial Facility, Mining Outpost |
| Chemical leak | 3,600 | 0.45 | Laboratory / Workshop in Industrial Facility, Research Station |
| Containment breach | 6,000 | 0.6 | Laboratory in a staffed Research Station |
| Energy pulse | 3,600 | 0.3 | Ancient Ruin |
| Deck collapse | 5,400 | 0.35 | Derelict Colony Ship |

At 3,000 integrity per compartment, a fire in a 12-compartment facility costs 4,500/36,000 = 12.5%.

Source: `DungeonOperation.GenerateEvents`, `DungeonOperation.GetTemplatesFor`, `DungeonSimulation.GetDungeonCombatComposition`, `DungeonSimulation.ComputeDungeonCombatUnitCount`, `DungeonSimulation.SpawnDungeonCombatUnits`, `DungeonFacilityEvents.InjectEvents`.

### Hazard effects on your crew

When a hazard fires (and is not prevented), `max(1, units / 2)` random units in that room are hit. Each non-specialist has a 20% chance to die outright; otherwise it takes the damage above. Every victim loses 0.15 morale, and the unit can become wounded.

Source: `DungeonSimulation.FireHazardEvent`, `DungeonSimulation.ApplyHazardCrewDamage`, `DungeonSimulation.ApplyHazardDamageToUnit`.

### Armed facilities

Facilities with hostility above None place defenders at start and use the same combat rules as ships:

```
defenders = max(2, round(compartments * fraction) + randomRange(-1, 1))   (fraction Low 0.3, Medium 0.5, High 0.8)
Low hostility: only 30% of runs have any defenders at all
```

Defenders go to random non-airlock rooms with spare capacity (corridors only if no other room has space). The structure takes `2.4 * dt` damage for every contested room each second, on top of hazards. Compared to the 3,000-per-room maximum this is small, but a long multi-room battle adds up.

Source: `DungeonSimulation.PlaceDefendersForDungeon`, `DungeonSimulation.ComputeDungeonDefenderCount`, `DungeonSimulation.DungeonDefenderFraction`, `DungeonOperation.ApplyDefinitionToSim`.

## Start events (ships)

When a ship boarding begins, each of these is rolled independently:

| Event | Chance | Effect |
|---|---|---|
| Comms Down | 15% | Alert noise x0.5 for the whole fight |
| Reactor Down | 8% | Removes the faction's reactor scuttle (see below) |
| Enemy Dazed | 12% | Sets every room to Dazed alert. Rooms already start Dazed, so this changes no number I could find; it still blocks Enemy Alerted |
| Enemy Alerted | 12% (only if Dazed did not roll) | Every room is set to a random alert state of Concerned, Worried or Alert (2-4). 20% to add an Enemy Reinforcements entry to the event list |
| Enemy Reinforcements | 10% | Schedules a defender reinforcement at 40% of estimated duration |
| Armory Damaged | 15% | `armoryDamageEfficiency` = 70-90% (random), multiplying every defender's power for the whole fight |
| Volatile Cargo | 12% | A hazard in the Cargo Hold fires on entry |

Also read from the code: the Enemy Alerted roll that adds Enemy Reinforcements happens inside the loop that applies start events over a copy of the list. As far as I can read it, the added entry shows up in the event list but does not set `_reinforcementsScheduled`, so only the straight 10% roll schedules reinforcements (not verified in play).

The Volatile Cargo hazard is a normal hazard (see above), so a specialist can prevent or soften it. On a ship there are no listed specialists unless the ship carries an investigation crew type, so it hurts a standard crew in full. `ship.shipNoScuttle` ships behave as if Reactor Down was rolled.

Estimated duration:

```
estimatedDuration = clamp(10 + 3 * compartments + defenderPower/attackerPower * 20, 10, 60)   seconds
```

Ship hazards that damage the hull on top of start events are added per non-airlock, non-corridor room with chance `baseWeight * 2^(gap/10) * 0.5`:

| Hazard | Damage | Base weight | Factions |
|---|---|---|---|
| Hull breach charges | 800 | 0.25 | Corsair Syndicate, Meridia's Chosen, The Radiant |
| Fire | 1,000 | 0.2 | any |
| Pressure lost | 800 | 0.15 | any |

Source: `DungeonSimulation.RollStartEvents`, `DungeonSimulation.ApplyStartEvents`, `DungeonSimulation.ApplyEnemyAlerted`, `DungeonSimulation.ApplyArmoryDamaged`, `DungeonSimulation.InjectVolatileCargoEvent`, `DungeonSimulation.FinalizeEstimatedDuration`, `DungeonFacilityEvents.InjectBoardingEvents`.

## What defenders do on ships

These run once per simulation step and only on ships and armed facilities.

| Behaviour | Conditions and numbers |
|---|---|
| Counterattack | Every 5-10 s. A room not held by you with 2 or more defenders, in Alert, next to one of your rooms where your power there does not exceed theirs: chance `0.5 + factionBonus` to send all but one defender (up to the target's spare capacity) into your room. Factions without a counterattack bonus only do this while none of your rooms is contested. With a bonus, moved defenders gain +0.2 morale |
| Scuttle | Ships only, while the fight is not won. Needs the reactor room or the armory still held by defenders, and you hold at least 55% of the ship. Warns once. Then `0.08 * canScuttleReactor` per second (0.016 per second for the Corsair Syndicate, 0.08 for Meridia's Chosen and the Malcyte). Outcome: if the armory is theirs and the reactor is yours, always an ammo explosion; if both are theirs, 40% ammo explosion and 60% reactor blast; if only the reactor is theirs, the reactor blast. Ammo explosion: 35% structure damage and the armory is destroyed. Reactor blast: the ship explodes in 5 s |
| Airlock vent | Factions that can vent. Needs the Airlock not held by you and a 30 s cooldown; `0.003` per second. Kills each unit of either side in the airlock with 100% chance, then in rooms further out with `max(0.1, 1 - 0.4 * depth)` (60%, 20%, 10%) for up to 3 rooms |
| Jump drive | Ships level 40 and up that can flee. After you hold 50% of the ship while defenders hold the Bridge: `0.05` per second to start a 120 s spool. Take the Bridge before it finishes or the ship jumps away (outcome Retreat, reason Target Ship Escaped) |
| Buy-out | Mercenary Guild and Smugglers. `0.008` per second after a 15 s cooldown, offers one unit for `1.25 * crewCost`. Accepting spends credits and the unit joins you (state Bought). "Auto accept buy-outs" accepts automatically |
| Side-switch | Blue (Stellar Industries) defenders: `0.05 * dt` per second once you control 60% of the ship. 1-3 defenders join you, 20 s cooldown |
| Kamikaze | Factions with `kamikazeChance`: units under the morale threshold die at `kamikazeChance * dt` per second and deal 1 damage to a random unit of yours in the room |
| Brig rescue | First time you take the Brig (ships of size 4 and up have one): 65% for factions with useful brig crew, otherwise 30%, to free between 1 and `max(2, compartments/2)` units of the ship's own crew types, who join you |

The 65% factions are the Corsair Syndicate, Meridia's Chosen and Canisec. Rescued units are newcomers: they are returned to your ship only if there is space in your crew quarters after your own crew.

Source: `DungeonSimulation.TryCounterattack`, `DungeonSimulation.TryScuttle`, `DungeonSimulation.FireScuttleOutcome`, `DungeonSimulation.TryAirlockVent`, `DungeonSimulation.ApplyVentDamage`, `DungeonSimulation.TryJumpDrive`, `DungeonSimulation.CanShipJumpAway`, `DungeonSimulation.TryBuyOut`, `DungeonSimulation.TrySideSwitch`, `DungeonSimulation.TryKamikaze`, `DungeonSimulation.OnBrigRescue`.

## Integrity and collapse

### Structure damage

| Source | Amount |
|---|---|
| Kills with AP / Standard / Hollow ammo | 2.5 / 2 / 1.5 per kill |
| Grenade | 5% of maximum |
| Armed-facility combat | 2.4 per second per contested room |
| Facility hazards | 3,000-6,000 each (see table) |
| Ship hazards | 800-1,000 each |
| Armory explosion | 35% of maximum |

### Ship collapse

At 0 integrity, without a won fight, the ship starts `structuralCollapse`: your crew begins retreating, 2-4 compartments (at most `max(1, compartments/4)`) suffer decompression right away (and one more with 6% per second), and at 15 seconds the ship explodes, whether or not your crew reached the Airlock. Vent kills in decompressed rooms: `max(0.1, 0.5 - 0.4 * depth)` per unit, spreading three rooms out. After victory, integrity reaching 0 does nothing.

A scuttle explosion is faster: 5 seconds, each living crew member outside the Airlock and staging has a `0.14 * dt` per second chance of death.

### Facility collapse

At 0 integrity a facility triggers a retreat with outcome Retreat, reason Structural Collapse. There is no explosion. Your crew walks to the Airlock and leaves with partial loot, and any mission to clear that facility (the game's "Clear Pirate Base" mission, type "Dungeon Assault") fails with "Mission failed - the base collapsed." The stored integrity stays near zero, so the facility stays closed (needs more than 10%).

Mission-protected locations (`stationIsInvincible`, `shipIsInvincible`) cannot drop below 15%.

Source: `DungeonSimulation.DamageFacility`, `DungeonSimulation.TriggerStructuralCollapse`, `DungeonSimulation.TickStructuralCollapse`, `DungeonSimulation.TriggerExplosion`, `DungeonSimulation.TickExplosion`, `DungeonSimulation.ApplyStructuralVentDamage`, `DungeonOperation.FailDungeonMissionsForCollapse`.

## How a run ends

`MissionOutcome` values (the result card's Outcome row uses the game's own words Extracted, Retreated, Crew Lost, Captured and Failed; I did not trace which value maps to which):

| Outcome | Reason code | When |
|---|---|---|
| FriendlyVictory | none | Facility: every room not locked is Friendly (none Unknown, Investigating or Contested). Ship: all defenders dead or surrendered, or mass surrender |
| FriendlyExtracted | Voluntary Retreat | You abandon the operation, or your crew breaks off |
| FriendlyExtracted | Morale Collapse | Ship boarding, average attacker morale under 0.15 (or autopilot) |
| FriendlyExtracted | Structural Collapse | Facility integrity reached 0 |
| FriendlyExtracted | Target Ship Escaped | Jump drive completed |
| HostileVictory | All Crew Killed | No living units on your side |
| ShipExploded | Structural Collapse / Reactor Scuttled | Ship collapse timer or scuttle |

A victory does not close the run immediately. The simulation then stays open until you request extraction. Crew walk back to the Airlock and into staging and then it completes. On ships the investigation speed rises tenfold after victory so remaining rooms are explored quickly. If you retreat instead, crew walk out without further fighting: while retreating, the simulation skips combat entirely, so retreating is the safe way to preserve survivors.

Consequences by outcome:

| Outcome | Ship | Facility |
|---|---|---|
| FriendlyVictory | You capture the ship: it becomes yours, its crew and commander are removed, ammo capped to one minute, modules damaged by `1 - hull fraction`, hull upgrades damaged. All loot goes to your cargo (2% chance of a booster). Leadership mastery XP gained | All loot to cargo, the internal `Dungeon Cleared` trigger fires (the game's "Base cleared" for a clear-the-base mission), matching missions are claimed |
| FriendlyExtracted | The ship recovers (at least 15% hull), becomes unboardable and flees. Each non-data loot item has a 50% chance to reach your cargo. Boarding missions fail | Same partial loot rule; data items always kept. Integrity stays reduced |
| HostileVictory | Crew lost, ship recovers and flees, boarding missions fail, a quarter of the usual XP | Crew lost |
| ShipExploded | Loot lost. Surviving attackers drop out as crew pods (see below), and the ship is gone | n/a |

Source: `DungeonSimulation.CheckWinLoss`, `DungeonSimulation.CheckHostilesWinConditions`, `DungeonSimulation.CheckExplorationWinCondition`, `DungeonSimulation.TickRetreating`, `DungeonSimulation.TickSimulationBody`, `DungeonSimulation.GetInvestigationSpeedMultiplier`, `DungeonOperation.HandlePodSimulationComplete`, `DungeonOperation.HandleWalkSimulationComplete`, `DungeonOperation.ApplyWalkOutcomeEffects`, `DungeonOperation.TransferPartialLootToCargo`, `BoardableUnit.FinalizeCapture`, `BoardableUnit.ConvertToPlayerShip`, `BoardableUnit.RecoverAndFlee`.

## Pods and crew return

Ships are boarded with pods. Facilities are entered by walking.

### Boarding pods

`BoardingPod` states are Docked, Launching, Attached, Returning, Arrived.

| Step | Rule |
|---|---|
| Creation | When you press Start, each crew selection is removed from your ship and split into pods of up to 10 units, as one Docked pod per 10 (`BatchCrewIntoPods`) |
| Approach | The ship flies to a standoff point. Within 5 units of the hull the docked pods launch together. Pods spawned later directly from a ship (reinforcements from close range, enemy pods) launch 0.35 s apart |
| Flight | Speed 6 units per second. Each pod attaches when it overlaps the target's hull collider or reaches the last known position (0.5 radius). If the target disappears, the pod turns around |
| Landing | Crew arrive in staging (outside the ship). The simulation starts once all launched pods have landed. Crew enter through the Airlock, up to its capacity at a time |
| Extraction | After the simulation ends, `TriggerPodReturn` sends each pod home carrying the survivors. The operation closes when all pods have arrived, or after 120 s regardless |
| Cancel | The X button on the target while approaching recalls pods; Docked pods return their crew instantly, Launching ones turn around |

### Returning crew and overflow

Survivors of the pods are returned to your ship's crew quarters (`AddCrew`), but only up to `getMaxGrunts - totalCrew`. Your original crew come first. Rescued, bought-out and defected units ("newcomers") are added only for the free space left. The rest is jettisoned as crew pods that float near your ship. Overflow prisoners go out as hostile pods.

Prisoners are added to your brig up to the ship's `brigCapacity` (a Leadership skill `BrigCapacityBonus` exists; I did not trace how it feeds `brigCapacity`).

### Walking into facilities

The ship docks at a facility pad or tunnel. If it docked at a pad, crew walk from your ship to the pad (logistics walkers), then the simulation starts. At the end, survivors walk back, with the same newcomer and overflow rule. If the walkers get stuck 15 s, they are forced aboard.

Source: `DungeonManager.BatchCrewIntoPods`, `DungeonOperation.CreateDockedPods`, `DungeonOperation.TickPodApproach`, `BoardingPod.UpdateLaunching`, `BoardingPod.Attach`, `DungeonOperation.HandlePodCrewLanded`, `DungeonOperation.StartPodSimulation`, `DungeonOperation.TriggerPodReturn`, `DungeonOperation.SelectReturningUnits`, `DungeonOperation.JettisonOverflowCrew`, `DungeonOperation.TickAwaitingPodReturn`, `SpaceShipData.AddCrew`, `SpaceShipData.AddPrisoners`, `LootManager.CreateCrewPodsBatched`.

## Reinforcements

### Enemy reinforcements

If the Enemy Reinforcements start event was rolled, `reinforcementRequested` turns on once `elapsed >= 0.4 * estimatedDuration`. With the estimated duration limits of 10-60 s, that is 4 to 24 seconds into the fight. Only pod boardings (ships) act on it. Then:

1. The game looks for an eligible ship within 200 units of the target. Eligible: same faction as the target, hostile to you, not the target and not your ship, not boardable, not leaving, and it has crew with boarding power above zero.
2. It takes crew from that ship: boarding-capable types only, up to the smaller of the target's spare defender capacity (total room capacity minus living defenders) and a wave cap of `clamp(shipSize, 3, 8)` units.
3. That ship flies to the target and fires pods. The pods attach and the units appear in the Airlock as defenders.

The request is consumed whether or not a ship is found. A ship that was destroyed or driven off before then means no reinforcements.

### Your reinforcements

In the boarding panel, once a simulation is running, the button reads Add Attackers (ships) or Call Reinforcements (facilities). It opens the crew sliders. Pressing Send takes the selected crew out of your flagship's crew right away.

| Case | What happens |
|---|---|
| Ship boarding, ship within 5 units of the hull | Pods launch at once from your ship |
| Ship boarding, farther away | Docked pods are created and your ship flies to the standoff point, then launches them |
| Facility | The crew are added straight to the simulation, placed in the Airlock up to its capacity and staging otherwise |

New units arrive Active in staging with full HP and morale. Crew sent with reinforcements that survive are returned like the original crew.

Source: `DungeonSimulation.TickReinforcementSchedule`, `DungeonOperation.CheckReinforcementRequest`, `DungeonOperation.FindEligibleReinforcer`, `DungeonOperation.IsEligibleReinforcer`, `DungeonOperation.CollectCombatCrew`, `DungeonOperation.GetSpareDefenderCapacity`, `DungeonOperation.GetReinforcementWaveCap`, `DungeonOperation.DispatchReinforcer`, `DungeonOperation.AddReinforcements`, `DungeonOperation.TickPendingReinforcements`, `DungeonSimulation.AddDefenders`, `DungeonPanel.AddReinforcements`.

## Skills and gear

| Source | Effect |
|---|---|
| Assault Doctrine (Leadership skill) | +10% boarding power per point, 3 points maximum, so +30%. Applies to your units only |
| Crew Conditioning | +10% crew resistance per point, 3 points maximum: raises your units' HP and lowers their wound chance |
| Combat Hardening | +15% crew resistance, 1 point. Needs 11 points spent in its tree |
| Equipment stat Boarding Power | Adds directly to the same multiplier as Assault Doctrine: `1 + AssaultDoctrine + BoardingPower`. Read live from your flagship |
| Equipment stat Boarding Suppression | Defender power x `(1 - value)` in every contested room. The unique effect "Shock Assault" gives 0.05 (5%) |
| Body Armor | One item in cargo, or found anywhere during the run, reduces your casualty hit rate by 10% |

I found no equipment item in the asset files that carries Boarding Power, only Shock Assault for Boarding Suppression. Suppression reads from your current flagship, not the operation's ship.

The Crew screen and the ship's General tab show `crew boarding power * (1 + Assault Doctrine)`. They do not include the equipment stat.

Source: `SkilltreeNode.BoardingPowerBonus`, `SkilltreeNode.CrewResistanceBonus`, `SkilltreeNode.BoardingEquipmentBonus`, `AbstractUnit.RecalculateStats`, `DungeonOperation.TickActive`, `DungeonSimulation.GetArmorDamageMultiplier`, `UniqueCombatBoardingSuppression` prefab, `CrewView`.

## Autopilot

Autonomous operations (the game's "Installation Operations" autopilot stat) run an `AutoDungeonDirector`: it forces a retreat when every unit is dead or the average morale falls under 0.15, and when every unexplored room is blocked by locks it logs a message and withdraws after 3 seconds. Autopilot also turns on automatic movement. A victory that ends with unfinished locked rooms sends the crew home, locking that facility to autonomous play.

Source: `AutoDungeonDirector.Tick`, `DungeonOperation.TickAutonomousVictoryExtractionDelay`.

## Practical advice

### Picking crew for a facility

- Count specialists first. The list is on the Specialist row. With a specialist in the room, ambushes are 30% smaller and hazard damage drops by up to 50%. Even one specialist gives a 33% chance to prevent each negative event outright at a level gap under 3. Specialists matter in every room, not just profession rooms.
- Bring the first-listed specialist for the loot. The profession loot is checked against that one type (Miner at Mining Outpost, Salvager at Salvage Yard, Research Station, Industrial Facility and Derelict, Marine at Relay and Military, Deckhand at Black Market). The second listed type opens locks and shields from events but misses profession loot.
- Locked rooms need a specialist adjacent. At 35% per lockable room, a 12-room facility often has two or three, and locked rooms hold the best loot. Winning does not require them.
- At Ancient Ruins, bring 3 or more distinct crew types. All the bonuses depend on it.
- Armed facilities use the combat rules. Bring Marines. A Marine has 4x a Deckhand's power and over twice the HP, and defenders on Military Outposts are 70% Marines.
- Do not overstack one room. Investigation speed is `1 + 0.15 * (n - 1)` and capacity is 3-6 per room. A group of six covers a room in 57% of the time of one unit, not 17%.
- Facility integrity is the clock. A facility has 3,000 per room and each hazard takes 3,000-6,000, so a few hazards can close it. Faster investigation (specialists, more units up to a point) means fewer events fire before you finish.

### Picking crew for a ship

- Compare the live Estimate with the pre-start one. The latter uses a flat 3 power per defender. Marine-heavy factions and high level gaps (more Marines, x HP) are stronger than it says.
- A level gap raises defender HP up to 4x at a gap of 15 and also shifts their mix to Marines. Hard-boarding factions are always 10 levels above you at minimum.
- Marines first. They have the best HP per slot, and compartment capacity caps how many units fight at once.
- Stealth choice: Silent stretches the time before rooms next to yours go Alert to about 350 s on average (Balanced about 70 s, Aggressive about 35 s) and cuts hits taken by 25%, in exchange for 20% less power. Use it on large ships where defenders are spread out and you clear room by room. Use Aggressive only when you are strong enough that speed matters more.
- Armor Piercing (AP) is the better ammo against Marine-heavy factions (1.2x) and does the most structural damage. Hollow reduces structure damage and wastes 20% against Marines and Gunners.
- Pods carry 10 units each. Sending 21 units launches three pods, the last with 1 unit. The simulation waits for all pods to land.

### When to call reinforcements

- New crew enter through the Airlock, up to its capacity, and wait in staging otherwise. Reinforcements only help if the Airlock and the rooms beside it have free capacity.
- The enemy's reinforcement wave lands in the Airlock, which is also your entry point. Defenders appear there even if you hold it, so a strong first wave in the Airlock room matters more than depth in the ship.
- Wounded units leave the fight on their own and recover in staging at about 1.3% of max HP per second, so a long fight lets staging heal your first wave.
- Crew you send are taken off your ship immediately and come back only if they survive and your ship has free crew space.

### What lowers your losses

- Put the Bridge on priority early on ships. Holding it gives +0.02 morale per second, cancels a jump drive, and is required for mass surrender.
- Keep your rooms connected to the Airlock. A room cut off from the Airlock by non-friendly rooms costs 0.05 morale per second.
- Barricade secured rooms next to a front: -35% damage taken there.
- Carry a Body Armor (-10% casualty rate).
- Take Assault Doctrine and Crew Conditioning if you board often.
- Throw grenades into rooms that hold defenders and none of your crew. Each grenade does 3 damage to everyone in the room, dazes them for 15 s (power x2/3) and sets the room to Alert, so a second wave arrives sooner. It also costs 5% of the structure.
- Do not let integrity reach zero on a ship. At zero the ship blows in 15 seconds and nothing stops it unless you already won. Avoid grenade spam (5% each) and expect 800-1,000 per hull hazard.
- For facilities, retreat while integrity is still above 10% if hazards keep firing. A retreat keeps your crew and half the loot, and the facility remains enterable.
- If a fight is lost, retreat rather than let morale break: retreating crew take no combat damage.

### What looks useful but is not

- Estimate percentages are not win probabilities. They are the power ratio mapped through `r/(r+1)`, so even 50% means equal power, not equal outcomes.
- The Enemy Dazed event changes no number in the code: every compartment already starts Dazed.
- Profession discovery bonus is dead data. Only `professionSpeedBonus` is read.
- Room `efficiencyModifier` (reduced for rooms cut off from the Airlock) is computed but nothing reads it.
- Cancelling an approach does not refund the reputation penalty.

## Not verified

- Whether `SpawnEscapeeCrewPods` and `BoardableUnit.SpawnExplosionEscapePods` both spawn pods after a ship explodes (the code calls both).
- Whether any generated equipment carries the Boarding Power stat; none of the prefab data does.
- The per-update chances in the morale code (panic 2-3%, mass surrender, `surrenderChance * 0.1`) are not scaled by elapsed time, so they depend on how often the simulation updates. I did not find a fixed tick rate.
- Which items carry the "Shock Assault" unique effect.
- Skill names and values come from the localization strings and prefab data. The `availableForCrew` flag on the three crew skills was not interpreted, and I did not check how the skill points are earned.
- Whether `MarkFactionPlayerHostile` has effects beyond turning the faction hostile at that location.
