# War stations

Conquest opens at level 28 (see the [Conquest stage](README.md)). This page covers the stations of that war: hubs, Defense stations and headquarters, the x20 and x30 hit point multipliers, war effort on station weapons, the conquest station layouts, the off-screen siege numbers and Defense station garrisons. Part stats, the stat formula, loot and reputation rules are on [basics/stations.md](../basics/stations.md). Values are read from the game code of version 0.8.3.4 (the decompiled source, not in-game testing).

Terms and abbreviations are explained in the [conquest glossary](glossary.md) and the [basics glossary](../basics/glossary.md).

Station-wide topics that live elsewhere: how the war map uses these numbers ([war.md](war.md)), how factions pay for and rebuild stations ([faction-economy.md](faction-economy.md)), what happens to a shot between the turret and the hull ([combat-damage.md](../basics/combat-damage.md)) and the general level and rarity curves ([scaling.md](../basics/scaling.md)). The last section of the page, "In the game code", holds the method names and raw formulas behind each claim.

## Which station is which

A war-zone system is one where factions fight over ownership: the Conquest sector (Ara Martis, the original war zone). The Frontier, Staging and Darkspace regions are not part of it. Expansion sectors count as war zones once they unlock with Beyond the Front ([Your empire](../expansion/your-empire.md)). The names of the other station kinds (economic outpost, Industrial Outpost) are in [basics/stations.md](../basics/stations.md#which-station-is-which).

| Name | What it is |
|---|---|
| hub | The main station of a war-zone system, shown as "Space Station" on the map. It carries the owner's flag. Destroying it abandons the system. |
| headquarters | The one system a faction cannot lose to a fleet. Its hub has the faction commander and shop. Destroying that hub still abandons the system, and the faction sets up a new headquarters in one of its other systems. |
| Defense station | A station type that holds a garrison and fires on enemies. Not the same as the Defense number on the system card. |
| economic outpost | A station that produces something: Mining, Salvage, Refinery, Forge, Production, Trading, Recruitment or Construction Yard. Raids target these. It is not a Defense station. |

In the code: [which station is which](#code-which-station-is-which).

## How tough a hub or Defense station is

The context multiplier in the [stat formula](../basics/stations.md#the-stat-formula) is chosen from where you are, not from who owns the part.

| Where the part is | Multiplier |
|---|---|
| A hub in a headquarters system | x30 |
| A hub in any other war-zone system | x20 |
| A faction Defense station in a war-zone system, including a headquarters system | x20 |

So a faction Construction Yard in a war-zone system has the same tiny HP a mission station has, while the Defense station next to it has twenty times as much.

The combined multiplier at escalation tier 0 is the "Combined" column of the table in [basics/stations.md](../basics/stations.md#the-stat-formula). With the context multiplier applied:

| Level | Combined | In a hub or Defense station (x20) | In a headquarters hub (x30) |
|---|---|---|---|
| 10 | 0.70 | 14.0 | 21.0 |
| 30 | 0.83 | 16.6 | 24.9 |
| 45 | 1.22 | 24.5 | 36.7 |
| 60 | 1.81 | 36.2 | 54.3 |
| 80 | 3.20 | 64.0 | 96.0 |
| 100 | 5.73 | 114.7 | 172.0 |

Worked example: a level 60 Station Core in a hub has a base hull term of 900, so about 900 x 36.2 = 32,600 hull before the Hull module's own HP is added (and before any Escalation multiplier). The part's tooltip "Hull" row shows the real current and maximum.

Station parts take this HP multiplier and the war-effort multiplier below inside the same hit pipeline ships use ([Combat damage](../basics/combat-damage.md#npc-and-station-modifiers)); that stat set is the only difference.

In the code: [how tough a hub or Defense station is](#code-how-tough-a-hub-or-defense-station-is).

## War effort on station weapons

In the same places as the x20 and x30 multipliers (a hub, or a Defense station in a war-zone system), the part's Combat Power, Drone Power and Torpedo Power are multiplied by that system's war effort multiplier, but only while the system has a war effort running. War effort is the firepower bonus that Conquest missions you complete give a system's stations; it lasts 3 hours from the last one.

```
multiplier = 1 + bonus - penalty      (never below 0)
```

Escalation and the level-based NPC multiplier scale the same three stats on top of that, which is why a high-level station hits much harder than a low-level one. How war effort is earned and decays is on [war.md](war.md).

In the code: [war effort on station weapons](#code-war-effort-on-station-weapons).

## Station layouts

Every station gets a seed made of a size and a digit from 0 to 6. The size comes from the number of facilities the station has: fewer than 4 is Small, 4 or 5 is Medium, 6 or more is Large. Faction stations are created with no facilities, so they are always Small.

| Seed | Layout | Parts |
|---|---|---|
| Small, digit 0 to 3 | Small 1 | 19 |
| Small, digit 4 to 6 | Small 2 | 18 |
| Medium, digit 0 to 3 | Medium 1 | 23 |
| Medium, digit 4 to 6 | Medium 2 | 27 |
| Large, digit 0 to 6 | Large 1 | 45 |

The layouts of mission, pirate, skirmish and escort stations, and the faction prefix that picks the artwork, are in [basics/stations.md](../basics/stations.md#station-layouts).

A full Large conquest layout has 45 parts, connectors included, so wrecking a friendly station can cost several thousand reputation in part kills before the 5000 for the station itself (see [reputation](../basics/stations.md#reputation-and-force-attacks)).

In the code: [station layouts](#code-station-layouts).

### Part lists

Turret entries marked "tier" follow the turret tier rule below. The other platform names are short forms of the game's internal names for fixed Defense Platforms (the game doesn't show them): S1 has one Small turret, S2_1C two Small turrets in the compact layout, S2_2C and S2_3C two Small turrets in the wider layout (their hull and shield are on [Combat stations](../basics/stations.md#every-station-part)). The drone bay is a platform with no weapon slots. Connector counts are in the last column.

| Layout | Core | Energy | Refinery | Cargo Dock | Docking Pad (Small / Medium) | Tunnel | Platforms | Connectors |
|---|---|---|---|---|---|---|---|---|
| Small 1 | 1 | 0 | 1 | 2 | 3 / 2 | 2 | 3 tier | 5 |
| Small 2 | 1 | 1 | 0 | 1 | 2 / 1 | 1 | 2 tier + S1 | 8 |
| Medium 1 | 1 | 1 | 0 | 2 | 4 / 2 | 2 | 6 tier | 5 |
| Medium 2 | 1 | 1 | 0 | 2 | 4 / 2 | 2 | 3 tier + 2 S2_3C + drone bay | 9 |
| Large 1 | 1 | 2 | 0 | 3 | 6 / 2 | 2 | 4 tier + 2 S2_2C + drone bay | 22 |

In the code: [part lists](#code-part-lists).

### Turret tier rules

In the Small, Medium and Large layouts each "tier" turret depends on the station's level:

- Below location level 45 it is always S2: two Small slots, hull 600 and shield 300 base.
- At level 45 or above it is M2 or L1 with a 50/50 roll. M2 is two Medium slots; L1 is one Large slot. Both have base hull 900 and base shield 900.
- The roll is fixed per station. In Medium 1 the first two tier turrets share one roll. Everywhere else each turret rolls on its own.

The fixed S1, S2_1C, S2_2C and S2_3C platforms never change with level.

In the code: [turret tier rules](#code-turret-tier-rules).

## How long damage lasts in a war zone

Damage and kills are saved, but stations are rebuilt from their seed, which restores every part at full HP. Besides the 3600 second rule that applies to space stations ([basics/stations.md](../basics/stations.md#how-long-damage-lasts)), two more situations rebuild war-zone stations:

- In every war-zone sector, the hourly conquest update (every 3600 seconds, 1 hour; the "Conquest Update" countdown card on the galaxy map) rebuilds every station except the one you are at and abandoned hulks.
- When a system is claimed (a fleet taking an abandoned or unowned system), the hub is rebuilt (after you leave, if you are there). A siege capture does not rebuild it; it waits for the next hourly rebuild.

For a war-zone system this means that killing parts is temporary unless the hub itself is lost.

In the code: [how long damage lasts in a war zone](#code-how-long-damage-lasts-in-a-war-zone).

## When the last part dies in a war zone

These steps come on top of the ones in [basics/stations.md](../basics/stations.md#when-the-last-part-dies). The station counts as destroyed when no part other than Connectors is left. They run after the mission trigger for a destroyed combat station and before the pocket achievement, and in this order:

1. The conquest side handles the station. If it is the hub of an owned war-zone system, the system is abandoned and handed on; what that does is on [war.md](war.md). A hub that is waiting to be rebuilt when you leave is skipped.
2. If the station is a faction station that is not already wrecked: if a part you hit was among those destroyed, it is wrecked on the spot ([wrecking](../basics/stations.md#wrecking-a-faction-station)). Then, if an active live siege of that Defense station exists, the siege ends with the attacker taking the station. Otherwise, and if it is not wrecked yet, the faction station is destroyed.

A Defense station that is not destroyed by your hand but is taken during a live siege (the Empire Log shows "Defense station destroyed" or "Defense station lost") goes through the same destruction step.

In the code: [when the last part dies in a war zone](#code-when-the-last-part-dies-in-a-war-zone).

## The station's off-screen siege numbers

The conquest map never reads the live HP of a station. It scores the station's part list instead, and a live fight does not write back into that score.

| Part | Structure (hull) score | Firepower score |
|---|---|---|
| Station Core | 200 | 0 |
| Connector | 5 | 0 |
| Every other non-platform part | 20 | 0 |
| Defense Platform | 60 + 15 x weapon size total | 40 x weapon size total |

The weapon size total adds up the size of each weapon slot on the platform, counting every slot as at least 1: Small 1, Medium 2, Large 3. So an S1 platform is 1, S2 is 2, M2 is 4, L1 is 3, and the drone bay (no weapon slots) is 0. Destroyed parts (HP 0 after damage) are skipped. Damaged parts count in full. A station you have never visited uses a fixed estimate: Small 560 hull and 240 firepower, Medium 940 and 480, Large 1390 and 560.

Totals per layout, with every tier turret S2 (below level 45), and the range when the tier turrets are all M2 or all L1:

| Layout | Hull | Firepower |
|---|---|---|
| Small 1 | 695 (740 to 785) | 240 (360 to 480) |
| Small 2 | 615 (645 to 675) | 200 (280 to 360) |
| Medium 1 | 985 (1075 to 1165) | 480 (720 to 960) |
| Medium 2 | 975 (1020 to 1065) | 400 (520 to 640) |
| Large 1 | 1210 (1270 to 1330) | 480 (640 to 800) |

These are summed over the system's hub and every un-wrecked same-faction Defense station, scaled by the sector's station-defense bonus, and halved while the system is settling (the 6 conquest updates after a system changes owner; the game has no name for this). A station siege uses this hull number as the "station structure" pool. A raid on an economic outpost (any non-Defense station) uses 15% of the hull and 50% of the firepower. How the Defense number, siege resolution and raids use these figures is on [war.md](war.md).

Because the score ignores partial damage and the live fight does not feed back, shooting a Defense Platform's hull down without killing it changes nothing off-screen. Killing parts does lower the score until the station is rebuilt.

In the code: [the station's off-screen siege numbers](#code-the-stations-off-screen-siege-numbers).

### The Energy Field's score

The Energy Field scores 20 hull and 0 firepower, like every other non-platform part that is not a Core or Connector. Its only roles here are this score and its loot table ([basics/stations.md](../basics/stations.md#the-energy-field)).

In the code: [the Energy Field's score](#code-the-energy-fields-score).

## Defense stations and garrisons

A Defense station is built with the Small layouts (Small 1 or Small 2), and has the x20 HP multiplier while you are in a war-zone system.

| Station tier | Garrison ships wanted | Point budget | Points per ship |
|---|---|---|---|
| 1 | 3 | 270 | 90 |
| 2 | 4 | 405 | 101 |
| 3 | 5 | 540 | 108 |
| 4 | 6 | 720 | 120 |
| 5 | 8 | 900 | 112 |

Each garrison ship is a warship chosen to fit its per-ship budget at the NPC fleet level. "Points" are the fleet value the game shows as "strength" on fleet cards. Ways a garrison is refilled:

- Warships built at faction yards and delivered to the station (priority is on [faction-economy.md](faction-economy.md)). In practice this is the only refill path.
- Each conquest update would add one ship per 10 combat strength, on a 900 s timer. It never fires, because nothing in the game raises a system's combat strength above 0 (see [war.md](war.md)).
- Newly seeded Defense stations are provisioned to their wanted count.

Refit: when the garrison's average level (weighted by ship points) is 3 or more levels below the current NPC fleet level and the system is not besieged, a 300 second refit starts and raises every ship below that level to it. It does not add ships. Refits stop while the station is in raid recovery, and a wrecked station runs no jobs at all.

When a Defense station siege is running and you are at that station, the garrison ships are spawned into the live fight as real ships at their stored level, hull rating and seed, along with the defending fleets in the system. When the siege targets the hub instead, the garrisons of every Defense station in the system spawn at the hub. Ships killed in the live fight are removed from the garrison. When the station is taken (live or at the siege cap) it is destroyed: if you are there the garrison is cleared and the station is marked wrecked, otherwise it is swapped for a wreck right away. A system holds at most 2 faction stations of any type.

In the code: [defense stations and garrisons](#code-defense-stations-and-garrisons).

## What to do with this

- Killing Connectors removes only 5 hull from a station's off-screen score each.
- Destroying a station in a war-zone system is undone within about an hour of game time after you leave, unless the hub itself is lost.
- A faction Construction Yard or Refinery (an economic outpost) in a war-zone system has none of the x20 HP of the Defense station beside it.

## Not verified

- How a faction station's tier rises (it sets the garrison size above). New Defense stations start at tier 1 (3 ships, 270 points). The faction economy can pay wealth to raise the tier of an owned station that is below its maximum, but whether it ever picks a Defense station is not verified.

## In the game code

Everything above is read from the decompiled game code of version 0.8.3.4, not from play. This part lists the method, field and prefab names behind each claim, the raw formulas and the source lines. To check a claim, search the decompiled source for the named method.

### Code: Which station is which

| Name | Code name |
|---|---|
| hub | `ConquestStation` |
| headquarters | `ConquestSystem.headquarters` |
| Defense station | `FactionStation` of type Defense |
| economic outpost | `FactionStation` that is not a Defense station |

### Code: How tough a hub or Defense station is

| Where the part is | Multiplier |
|---|---|
| A hub (`ConquestStation`) in a headquarters system | x30 |
| A hub in any other war-zone system | x20 |
| A faction Defense station (`FactionStation` of type Defense) in a war-zone system, including a headquarters system | x20 |

Combined multiplier at escalation tier 0 is `rankHP * HpBalance * NpcBase`, the "Combined" column of the table in [basics/stations.md](../basics/stations.md#the-stat-formula). The part's tooltip shows it in the `Hull` row.

Source: `CombatStationPart.GetStationHpMultiplier`, `CombatStationPart.GetConquestDefenseSystem`, `CombatStationPart.AddStatModifiers`, `AbstractUnit.ApplyNpcRankModifiers`, `BasePoiManager.AddToWorld`.

### Code: War effort on station weapons

The part's combat power, drone power and torpedo power are multiplied by that system's war effort multiplier, `max(0, 1 + warEffortBonus - warEffortPenalty)`, but only while the system has a war effort running. It applies to stat indices 21, 25 and 26 (`CombatPower`, `DronePower`, `TorpedoPower`). Escalation and `NpcBase` multiply the same three stats on top of that.

Source: `CombatStationPart.ApplyWarEffort`, `ConquestSystem.warEffortMultiplier`, `AbstractUnit.ApplyEscalationNpcModifiers`, `AbstractUnit.ApplyNpcRankModifiers`.

### Code: Station layouts

`SpaceStation.SetStationSeed` gives every `SpaceStation` a seed `{size}{digit}` where the digit is 0 to 6. The size comes from the number of facilities the station has: fewer than 4 is Small, 4 or 5 is Medium, 6 or more is Large. Faction stations (`FactionStation`) are created with no facilities, so they are always Small.

| Seed | Builder | Result |
|---|---|---|
| `Small0`..`Small3` | `CreateSmallConquestStation1` | 19 parts |
| `Small4`..`Small6` | `CreateSmallConquestStation2` | 18 parts |
| `Medium0`..`Medium3` | `CreateMediumConquestStation1` | 23 parts |
| `Medium4`..`Medium6` | `CreateMediumConquestStation2` | 27 parts |
| `Large0`..`Large6` | `CreateLargeConquestStation1` | 45 parts |

A full Large conquest layout is `Large1`.

Source: `CombatStationFactory.CreateStationVariant`, `CombatStationFactory.DispatchStationVariant`, `CombatStationFactory.ResolveSizeVariant`, `CombatStationFactory.CreateSmallConquestStation1`, `CreateSmallConquestStation2`, `CreateMediumConquestStation1`, `CreateMediumConquestStation2`, `CreateLargeConquestStation1`, `SpaceStation.SetStationSeed`, `SystemMapData.AddSpaceStation`, `MapPointOfInterest.BuildStationFromSeed`.

#### Code: Part lists

The part-list table uses the builder names: Small1 is `CreateSmallConquestStation1`, Small2 is `CreateSmallConquestStation2`, Medium1 and Medium2 are `CreateMediumConquestStation1` and `CreateMediumConquestStation2`, and Large1 is `CreateLargeConquestStation1`.

"Docking Pad" counts `Conq_DockingSmall_2C` and `Conq_DockingMedium_2C` in the conquest builders.

#### Code: Turret tier rules

In the Small, Medium and Large builders each "tier" turret is `DefenseTurret{S2|M2|L1}_1C`. The roll is seeded per station. In Medium1 the first two tier turrets share one roll.

### Code: How long damage lasts in a war zone

Stations are rebuilt from their seed. `DoConquestTick` runs every 3600 seconds and calls `Conquest.RepairConquestStations`, which rebuilds every station except the one you are at and abandoned hulks (`Conquest.IsAbandonedHulk`: a `ConquestStation` with no faction). When a system is claimed, `ConquestOwnership.Claim` calls `BuildOrRebuildHub`, which uses `ConquestOwnership.RebuildHub` (deferred with `rebuildOnLeave` while you are at the hub), and `ConquestOwnership.CompleteHubRebuild` finishes the job after you leave. A siege capture (`BattleManager.ApplyDecisiveStationCapture`) does not call `RebuildHub`. Part kills matter beyond the rebuild only if the hub is lost, which `OnStationDestroyed` handles on the conquest side.

Source: `Conquest.DoConquestTick`, `Conquest.RepairConquestStations`, `ConquestOwnership.RebuildHub`, `ConquestOwnership.CompleteHubRebuild`.

### Code: When the last part dies in a war zone

These steps run after the `CombatStationDestroyed` mission trigger and before the pocket achievement, in this order. `CombatStationPart.CheckCombatStationFullyDestroyed` fires them once no `CombatStationPart` other than a Connector is left in the station:

1. `ConquestOwnership.OnStationDestroyed` runs for the current station. If it is the hub of an owned war-zone system, the system is abandoned and handed on. A hub flagged `rebuildOnLeave` is skipped.
2. If the station is a faction station that is not already wrecked: if a part you hit was destroyed (`playerContributedToDestruction`), it is wrecked on the spot. Then, if an active live siege of that Defense station exists, the siege ends with the attacker taking the station. Otherwise, and if it is not wrecked yet, the faction station is destroyed.

A Defense station taken during a live siege (`DefenseStationDestroyed`, shown in the Empire Log as "Defense station destroyed" or "Defense station lost") goes through `FactionStationDestruction.Destroy` the same way.

Source: `ConquestOwnership.OnStationDestroyed`, `BattleManager.OnDefenseStationDestroyedLive`, `FactionStationDestruction.Destroy`.

### Code: The station's off-screen siege numbers

| Part | Structure (hull) score | Firepower score |
|---|---|---|
| Station Core | 200 | 0 |
| Connector | 5 | 0 |
| Every other non-platform part | 20 | 0 |
| Defense Platform | `60 + 15 * S` | `40 * S` |

`S` is the sum over the part's prefab weapon slots of `max(1, size)`: Small 1, Medium 2, Large 3. So S1 is 1, S2 is 2, M2 is 4, L1 is 3, and the drone bay (no weapon slots) is 0. Destroyed parts (HP 0 after damage) are skipped. Damaged parts count in full. `StationDefense.EstimateFromSize` is the fixed estimate for an ungenerated station (never visited): Small 560 hull and 240 firepower, Medium 940 and 480, Large 1390 and 560.

The totals per builder are summed over the system's hub and every un-wrecked same-faction Defense station by `StationDefense.GetDefendingStations`, scaled by the sector's station-defense bonus, and halved while the system is settling (`isSettling`: `settlingTicks` is set to 6 by `StartSettling` and drops by one per conquest update). The siege uses the hull number as the "station structure" pool (`BattleInstance.StartStationStructure`). A raid on an economic outpost (any non-Defense station) uses 15% of the hull and 50% of the firepower (`StationRaid.GetRaidProfile`).

Source: `StationDefense.ScorePart`, `StationDefense.ScoreParts`, `StationDefense.EstimateFromSize`, `StationDefense.GetStationProfile`, `StationDefense.GetProfile`, `StationDefense.GetDefendingStations`, `StationRaid.GetRaidProfile`, `BattleInstance.StartStationStructure`, `BattleManager.StartDefenseStationStage`.

#### Code: The Energy Field's score

The Energy Field scores 20 hull and 0 firepower, like every other non-platform part that is not a Core or Connector. Its only code-side roles are this score and its loot table ([basics/stations.md](../basics/stations.md#the-energy-field)).

### Code: Defense stations and garrisons

A Defense station is a `FactionStation` of type Defense. It is built with the Small layouts (Small1 or Small2), and has the x20 HP multiplier while you are in a war-zone system.

Garrison refill paths:

- Warships delivered by `WarshipDeliveries`; `ConquestFleetPlanner.ChooseGarrisonToSupply` picks the garrison. In practice this is the only refill path.
- `Conquest.DrainCombatStrengthIntoGarrisons` runs each conquest update and would add one ship per 10 combat strength through `FactionGarrison.Repair` (900 s timer). It never fires, because nothing in the code raises a system's `combatStrength` above 0 (see [war.md](war.md)).
- Newly seeded Defense stations are provisioned to their wanted count.

`FactionGarrison.GetWantedCount` gives the wanted ship count and `FactionGarrison.PickShip` the ship (budget per ship is `tierGarrisonBudget / tierGarrisonCount`, integer division). Refit (`FactionGarrison.ProgressRefit`, `FactionGarrison.CompleteRefit`) takes 300 seconds and starts when `FleetLevels.IsDueRefit` is true: the points-weighted average garrison level (`FleetLevels.GetGarrisonLevel`) is at most the NPC fleet level minus 3. A new `FactionStation` has `tier` 1; `FactionEconomicPlanner.TryUpgrade` raises tiers for wealth.

In a live siege, `FactionGarrison.SpawnInto` and `BattleManager.SpawnSiegeDefenders` spawn the garrison ships (`BattleManager.EnsureVenueUnits` for the venue). When the station is taken (live or at the siege cap) `BattleManager.TakeDefenseStation` sends it through `FactionStationDestruction.Destroy`. The cap of 2 is `FactionStation.MaxPerSystem`.

Source: `FactionGarrison.GetWantedCount`, `FactionGarrison.PickShip`, `FactionGarrison.Repair`, `FactionGarrison.ProgressRefit`, `FactionGarrison.CompleteRefit`, `FactionGarrison.SpawnInto`, `Conquest.DrainCombatStrengthIntoGarrisons`, `ConquestFleetPlanner.ChooseGarrisonToSupply`, `WarshipDeliveries`, `BattleManager.SpawnSiegeDefenders`, `BattleManager.EnsureVenueUnits`, `BattleManager.TakeDefenseStation`, `FactionStation.MaxPerSystem`.
