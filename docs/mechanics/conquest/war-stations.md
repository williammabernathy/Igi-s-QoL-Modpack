# War Stations

Conquest opens at level 28 (see the [Conquest stage](README.md)). This page covers the stations of that war: hubs, Defense stations and headquarters, the x20 and x30 hit point multipliers, war footing in live battles, the conquest station layouts, the off-screen siege numbers and Defense station garrisons. Part stats, the stat formula, loot and reputation rules are on [basics/stations.md](../basics/stations.md). Values were read from the game code of version 0.8.3.4 (the decompiled source, not in-game testing). The code was then compared with the 0.8.3.5 source, and every statement that changed there was rewritten. The station part values (hit points, turret slots) come from the part prefabs, which were dumped from the running game in both versions and did not change. Version 0.8.3.7 removed the per-system war effort that used to scale station weapons; the section on it was rewritten from the 0.8.3.7 source.

Terms and abbreviations are explained in the [conquest glossary](glossary.md) and the [basics glossary](../basics/glossary.md).

Other pages cover these station topics:

- How the war map uses these numbers: [war.md](war.md).
- How factions pay for and rebuild stations: [faction-economy.md](faction-economy.md).
- What happens to a shot between the turret and the hull: [combat-damage.md](../basics/combat-damage.md).
- The general level and rarity curves: [scaling.md](../basics/scaling.md).

The last section of this page, "In the Game Code", holds the method names and raw formulas behind each claim.

## Which Station Is Which

War-zone systems are the systems where factions fight over ownership. They are in the Conquest sector (Ara Martis, the original war zone). The Frontier, Staging and Darkspace regions are not part of the war zone. Expansion sectors count as war zones once they unlock with Beyond the Front ([Your Empire](../expansion/your-empire.md)). The names of the other station kinds (economic outpost, Industrial Outpost) are in [basics/stations.md](../basics/stations.md#which-station-is-which).

| Name | What it is |
|---|---|
| hub | The main station of a war-zone system, shown as "Space Station" on the map. It carries the owner's flag. Destroying it abandons the system. |
| headquarters | The one system a faction cannot lose to a fleet. Its hub has the faction commander and shop. Destroying that hub still abandons the system, and the faction sets up a new headquarters in one of its other systems. |
| Defense station | A station type that holds a garrison and fires on enemies. It is not the same as the Defense number on the system card. |
| economic outpost | A station that produces something: Mining, Salvage, Refinery, Forge, Production, Trading, Recruitment or Construction Yard. Raids target these. It is not a Defense station. |

In the code: [Which Station Is Which](#code-which-station-is-which).

## How Tough a Hub or Defense Station Is

The context multiplier in the [stat formula](../basics/stations.md#the-stat-formula) is chosen from where you are, not from who owns the part.

| Where the part is | Multiplier |
|---|---|
| A hub in a headquarters system | x30 |
| A hub in any other war-zone system | x20 |
| A faction Defense station in a war-zone system, including a headquarters system | x20 |

So a faction Construction Yard in a war-zone system has the same tiny HP a mission station has, while the Defense station next to it has twenty times as much.

At Escalation tier 0, the combined multiplier is the "Combined" column of the table in [basics/stations.md](../basics/stations.md#the-stat-formula). With the context multiplier applied, it becomes:

| Level | Combined | In a hub or Defense station (x20) | In a headquarters hub (x30) |
|---|---|---|---|
| 10 | 0.70 | 14.0 | 21.0 |
| 30 | 0.83 | 16.6 | 24.9 |
| 45 | 1.22 | 24.5 | 36.7 |
| 60 | 1.81 | 36.2 | 54.3 |
| 80 | 3.20 | 64.0 | 96.0 |
| 100 | 5.73 | 114.7 | 172.0 |

Worked example: a level 60 Station Core in a hub has a base hull of 900. Its Small Hull module multiplies that by 1 + u * 1920 (u is rolled between 0.010 and 0.014, and 1920 is the level 60 power level of 1600 times the High Grade factor 1.2), so by x20.2 to x27.9, about x24.0 for a mid-range roll. With the hub multiplier that is about 900 x 24.0 x 36.2 = 783,000 hull, before any Escalation multiplier. The roll and the module's aspect change it, so the part's tooltip "Hull" row shows the real current and maximum. The module formula is on [basics/stations.md](../basics/stations.md#how-tough-a-part-is). Each stat has one module (Hull, Shield Generator or Armor), so the only way two multipliers meet on the same stat is an aspect on any equipped module, such as a x1.05 hull aspect. They multiply: `AbstractUnitData.AggregateStatLines` runs `ApplyStatSourceLines` for every source, which multiplies `statMultipliers[stat]` by each line's multiplier and adds each line's flat amount to `calcedStats[stat]`.

Station parts use the same hit pipeline as ships ([Combat Damage](../basics/combat-damage.md#npc-and-station-modifiers)). Only the stat set differs: it includes the HP multiplier above.

In the code: [How Tough a Hub or Defense Station Is](#code-how-tough-a-hub-or-defense-station-is).

## War Footing in Live Battles

Until 0.8.3.5 a system's war effort multiplied the Combat Power, Drone Power and Torpedo Power of its hub's and Defense stations' parts. Version 0.8.3.7 removed that: a station part's weapons no longer have a war effort multiplier of their own, and a station part does not change with the missions you complete.

War footing, the faction-wide bonus that replaced it, multiplies those same three stats for the ships of a live conquest battle (a unit whose generation identity starts with "conquestbattle:"): garrison ships, defending fleets and attacking fleets of that faction. The multiplier is 1 plus the faction's footing bonus, up to x1.25. Station parts have the identity "stationpart" plus a number, so a hub's or Defense station's own turrets never get it. See [war.md](war.md#war-footing-and-war-fleets) for how footing is earned and what else it scales.

Escalation and the level-based NPC multiplier scale the same three stats on top of that, which is why a high-level station hits much harder than a low-level one.

In the code: [War Footing in Live Battles](#code-war-footing-in-live-battles).

## Station Layouts

Every station gets a layout seed made of a size and a digit from 0 to 6. The size comes from the number of facilities the station has: fewer than 4 is Small, 4 or 5 is Medium, 6 or more is Large. Faction stations are created with no facilities, so they are always Small.

| Seed | Layout | Parts |
|---|---|---|
| Small, digit 0 to 3 | Small 1 | 19 |
| Small, digit 4 to 6 | Small 2 | 18 |
| Medium, digit 0 to 3 | Medium 1 | 23 |
| Medium, digit 4 to 6 | Medium 2 | 27 |
| Large, digit 0 to 6 | Large 1 | 45 |

The layouts of mission, pirate, skirmish and escort stations, and the faction prefix that picks the artwork, are in [basics/stations.md](../basics/stations.md#station-layouts).

A full Large conquest layout has 45 parts, Connectors included. Wrecking a friendly station can therefore cost several thousand reputation in part kills, on top of the 5000 for the station itself (see [Reputation and Force Attacks](../basics/stations.md#reputation-and-force-attacks)).

In the code: [Station Layouts](#code-station-layouts).

### Part Lists

Platform entries marked "tier" follow the turret tier rules below. The other platforms are fixed Defense Platforms. The table writes them with short codes that are the endings of the game's internal part names. The game shows its own names for them:

- S1: Small Turret Mk I, with one Small turret.
- S2_1C: Small Turret Mk II, with two Small turrets in the compact layout.
- S2_2C: Small Turret Mk II (2-Way), with two Small turrets in the wider layout.
- S2_3C: Small Turret Mk II (3-Way), with two Small turrets in the wider layout.
- Drone bay: Small Drone Bay, a platform with no weapon slots.

The hull and shield of these platforms are in [Combat Stations](../basics/stations.md#every-station-part). Connector counts are in the last column.

| Layout | Core | Energy | Refinery | Cargo Dock | Docking Pad (Small / Medium) | Tunnel | Platforms | Connectors |
|---|---|---|---|---|---|---|---|---|
| Small 1 | 1 | 0 | 1 | 2 | 3 / 2 | 2 | 3 tier | 5 |
| Small 2 | 1 | 1 | 0 | 1 | 2 / 1 | 1 | 2 tier + S1 | 8 |
| Medium 1 | 1 | 1 | 0 | 2 | 4 / 2 | 2 | 6 tier | 5 |
| Medium 2 | 1 | 1 | 0 | 2 | 4 / 2 | 2 | 3 tier + 2 S2_3C + drone bay | 9 |
| Large 1 | 1 | 2 | 0 | 3 | 6 / 2 | 2 | 4 tier + 2 S2_2C + drone bay | 22 |

In the code: [Part Lists](#code-part-lists).

### Turret Tier Rules

In the Small, Medium and Large layouts, each "tier" turret depends on the station's level:

- Below location level 45 it is always S2 (a compact Small Turret Mk II): two Small slots, base hull 600 and base shield 300.
- At level 45 or above it is M2 (Medium Turret Mk II) or L1 (Large Turret Mk I) on a 50/50 roll. M2 has two Medium slots and L1 has one Large slot. Both have base hull 900 and base shield 900.
- The roll is fixed per station. In Medium 1 the first two tier turrets share one roll. Everywhere else each turret rolls on its own.

The fixed S1, S2_1C, S2_2C and S2_3C platforms never change with level.

In the code: [Turret Tier Rules](#code-turret-tier-rules).

## How Long Damage Lasts in a War Zone

Damage and kills are saved, but stations are rebuilt from their seed, which restores every part at full HP. Besides the 3600 second rule that applies to space stations ([basics/stations.md](../basics/stations.md#how-long-damage-lasts)), two more situations rebuild war-zone stations:

- In every war-zone sector, the hourly conquest update rebuilds every station except the one you are at and abandoned hubs (hubs with no owner, left behind by a system nobody has claimed yet; see [war.md](war.md)). The update runs every 3600 seconds (1 hour), and the galaxy map shows a "Conquest Update" countdown card for it.
- When a system is claimed (a fleet takes an abandoned or unowned system), the hub is rebuilt. If you are at the hub, the rebuild waits until you leave. The claim also destroys the Defense stations of the faction that abandoned the system, unless that faction is the one claiming it back. A siege capture does not rebuild the hub, which waits for the next hourly rebuild.

For a war-zone system this means that killing parts is temporary unless the hub itself is lost.

In the code: [How Long Damage Lasts in a War Zone](#code-how-long-damage-lasts-in-a-war-zone).

## When the Last Part Dies in a War Zone

These steps come on top of the ones in [basics/stations.md](../basics/stations.md#when-the-last-part-dies). The station counts as destroyed when no part other than a Connector is left. The steps run after the mission objectives for a destroyed combat station complete and before the pocket achievement, in this order:

1. The conquest code handles the station first. If it is the hub of an owned war-zone system, the system is abandoned: it loses its owner, and the first fleet to arrive can claim it. In your own sector it goes back on sale instead ([war.md](war.md) covers what that does). A hub that is waiting to be rebuilt when you leave is skipped.
2. If the station is a faction station that is not already wrecked, it is handled next. If a part you hit was among those destroyed, the station is wrecked on the spot ([Wrecking a Faction Station](../basics/stations.md#wrecking-a-faction-station)). Then, if an active live siege of that Defense station exists, the siege ends with the attacker taking the station. Otherwise, if the station is not wrecked yet, it is destroyed.

A Defense station that you did not destroy but that is taken during a live siege goes through the same destruction step. The Empire Log then shows "Defense station destroyed" or "Defense station lost".

In the code: [When the Last Part Dies in a War Zone](#code-when-the-last-part-dies-in-a-war-zone).

## The Station's Off-Screen Siege Numbers

The conquest map never reads the live HP of a station. It scores the station's part list instead, and a live fight does not write back into that score.

| Part | Structure (hull) score | Firepower score |
|---|---|---|
| Station Core | 200 | 0 |
| Connector | 5 | 0 |
| Every other non-platform part | 20 | 0 |
| Defense Platform | 60 + 15 x weapon size total | 40 x weapon size total |

The weapon size total adds up the size of each weapon slot on the platform, counting every slot as at least 1: Small 1, Medium 2, Large 3. So an S1 platform is 1, S2 is 2, M2 is 4, L1 is 3, and the drone bay (no weapon slots) is 0.

Destroyed parts (HP 0 after damage) are skipped. Damaged parts count in full.

A station you have never visited uses a fixed estimate: Small 560 hull and 240 firepower, Medium 940 and 480, Large 1390 and 560.

The table shows totals per layout. The first number assumes that every tier turret is S2 (below level 45). The numbers in brackets give the range when the tier turrets are all M2 or all L1:

| Layout | Hull | Firepower |
|---|---|---|
| Small 1 | 695 (740 to 785) | 240 (360 to 480) |
| Small 2 | 615 (645 to 675) | 200 (280 to 360) |
| Medium 1 | 985 (1075 to 1165) | 480 (720 to 960) |
| Medium 2 | 975 (1020 to 1065) | 400 (520 to 640) |
| Large 1 | 1210 (1270 to 1330) | 480 (640 to 800) |

The totals are summed over the system's hub and every same-faction Defense station that is not wrecked. They are then scaled by the sector's station-defense bonus. They are halved while the system is settling, which means during the 6 conquest updates after a system changes owner (the game has no name for this state).

A station siege uses this hull number as the "station structure" pool: the amount of hull the attackers must wear down before the station counts as broken. The game gives this pool no name, so "station structure" is this wiki's term (`stationHull` in the code). A raid on an economic outpost (any station that is not a Defense station) uses 15% of the hull and 50% of the firepower. [war.md](war.md) explains how the Defense number, siege resolution and raids use these figures.

Because the score ignores partial damage and the live fight does not feed back, shooting a Defense Platform's hull down without killing it changes nothing off-screen. Killing parts does lower the score until the station is rebuilt.

In the code: [The Station's Off-Screen Siege Numbers](#code-the-stations-off-screen-siege-numbers).

### The Energy Field's Score

The Energy Field scores 20 hull and 0 firepower, like every other part that is not a platform, a Core or a Connector. Its only roles here are this score and its loot table ([basics/stations.md](../basics/stations.md#the-energy-field)).

In the code: [The Energy Field's Score](#code-the-energy-fields-score).

## Defense Stations and Garrisons

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
- On paper, each conquest update adds one ship per 10 combat strength, on a 900 second timer. This never happens, because nothing in the game raises a system's combat strength above 0 (see [war.md](war.md)).
- Newly created Defense stations start with their full wanted number of ships.

A station's tier rises in two ways. New Defense stations start at tier 1, and the Darkspace Compact's gate fortresses, which the game places when the expansion opens, start at tier 5. After that, the faction economy can raise the tier of any station it owns that is below tier 5. It does so once all of the faction's station slots are used, at most twice per hourly planner run, and each step costs wealth (see [faction-economy.md](faction-economy.md#upgrades-and-the-starting-layout)). Defense stations are not excluded. The planner prefers the station with the fullest storage, and a Defense station stores nothing, so one is raised only when no other eligible station holds any goods and it comes first in the list, or when no other station is below its maximum tier.

Refit works as follows. When the garrison's average level (weighted by ship points) is 3 or more levels below the current NPC fleet level and the system is not besieged, a 300 second refit starts. It raises every ship below that level to it. A refit does not add ships. Refits stop while the station is in raid recovery, and a wrecked station runs no jobs at all.

When a Defense station siege is running and you are at that station, the garrison ships are spawned into the live fight as real ships at their stored level, hull rating and seed, along with the defending fleets in the system. When the siege targets the hub instead, the garrisons of every Defense station in the system spawn at the hub. Ships killed in the live fight are removed from the garrison. These ships get the owner's war footing on their weapons. Off-screen, the garrison counts at its points times 1 + the owner's war footing bonus.

When the station is taken (in a live fight, or in the simulated siege either early or when the siege reaches its time limit), it is destroyed. If you are there, the garrison is cleared and the station is marked wrecked. Otherwise the station is swapped for a wreck right away.

A system holds at most 2 faction stations of any type.

In the code: [Defense Stations and Garrisons](#code-defense-stations-and-garrisons).

## What to Do With This

- Each Connector you kill removes only 5 hull from a station's off-screen score.
- A station you destroy in a war-zone system is rebuilt within about an hour of game time after you leave, unless the hub itself is lost.
- A faction Construction Yard or Refinery (an economic outpost) in a war-zone system does not get the x20 HP multiplier that the Defense station beside it has.

## In the Game Code

Everything above was read from the decompiled game code of version 0.8.3.4, not from play. The code was then compared with the 0.8.3.5 source, and every statement that changed there was rewritten. The part prefab values (turret slot sizes, hull and shield scales) were read from runtime dumps of both versions, which match. This part lists the method, field and prefab names behind each claim, the raw formulas and the source lines. To check a claim, search the decompiled source for the named method.

### Code: Which Station Is Which

| Name | Code name |
|---|---|
| hub | `ConquestStation` |
| headquarters | `ConquestSystem.headquarters` |
| Defense station | `FactionStation` of type Defense |
| economic outpost | `FactionStation` that is not a Defense station |

### Code: How Tough a Hub or Defense Station Is

| Where the part is | Multiplier |
|---|---|
| A hub (`ConquestStation`) in a headquarters system | x30 |
| A hub in any other war-zone system | x20 |
| A faction Defense station (`FactionStation` of type Defense) in a war-zone system, including a headquarters system | x20 |

Combined multiplier at escalation tier 0 is `rankHP * HpBalance * NpcBase`, the "Combined" column of the table in [basics/stations.md](../basics/stations.md#the-stat-formula). The part's tooltip shows it in the `Hull` row. It multiplies the part's base HP times its module multiplier: `AbstractUnit.GetStatRaw` is `(base + calcedStats[stat]) * statMultipliers[stat]`, the Hull, Shield Generator and Armor main stats are multiplier lines (`1 + u * s * powerLevel * 1.2`, see [basics/stations.md](../basics/stations.md#code-module-gear-and-weapons)), and station gear never rolls flat HP, so `calcedStats` stays 0 for the HP stats. Several multiplier lines on one stat multiply (`AbstractUnitData.AggregateStatLines`, `ApplyStatSourceLines`).

Source: `CombatStationPart.GetStationHpMultiplier`, `CombatStationPart.GetConquestDefenseSystem`, `CombatStationPart.AddStatModifiers`, `AbstractUnit.ApplyNpcRankModifiers`, `AbstractUnit.GetStatRaw`, `AbstractUnitData.AggregateStatLines`, `AbstractUnitData.ApplyStatSourceLines`, `EquipmentBuilder.BuildInitialStats`, `BasePoiManager.AddToWorld`.

### Code: War Footing in Live Battles

`AbstractUnit.ApplyWarFooting` calls `MultiplyDamageStats(WarFooting.GetMultiplier(faction))` when `BattleUnitKey.IsBattleIdentity(unitData.generationIdentity)` is true (identity starting with `conquestbattle:`). Battle ships get that identity in `ConquestCombat.SpawnShip` and `BattleUnitDescriptor.Build`. Station parts get `stationpart` plus an index in `CombatStationData.AddPart`, so they are not scaled. `MultiplyDamageStats` multiplies stat indices 21, 25 and 26 (`CombatPower`, `DronePower`, `TorpedoPower`) of `statMultipliers`. Escalation and `NpcBase` multiply the same three stats through `MultiplyDamageStats` as well. `CombatStationPart.ApplyWarEffort` and `ConquestSystem.warEffortMultiplier` no longer exist in 0.8.3.7.

Source: `AbstractUnit.ApplyWarFooting`, `AbstractUnit.MultiplyDamageStats`, `WarFooting.GetMultiplier`, `BattleUnitKey.IsBattleIdentity`, `AbstractUnit.ApplyEscalationNpcModifiers`, `AbstractUnit.ApplyNpcRankModifiers`.

### Code: Station Layouts

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

#### Code: Part Lists

The part-list table uses the builder names: Small1 is `CreateSmallConquestStation1`, Small2 is `CreateSmallConquestStation2`, Medium1 and Medium2 are `CreateMediumConquestStation1` and `CreateMediumConquestStation2`, and Large1 is `CreateLargeConquestStation1`.

"Docking Pad" counts `Conq_DockingSmall_2C` and `Conq_DockingMedium_2C` in the conquest builders.

#### Code: Turret Tier Rules

In the Small, Medium and Large builders each "tier" turret is `DefenseTurret{S2|M2|L1}_1C`. The roll is seeded per station. In Medium1 the first two tier turrets share one roll.

### Code: How Long Damage Lasts in a War Zone

Stations are rebuilt from their seed. `DoConquestTick` runs every 3600 seconds and calls `Conquest.RepairConquestStations`, which rebuilds every station except the one you are at and abandoned hubs (`Conquest.IsAbandonedHulk`: a `ConquestStation` with no faction, which is the hub of an abandoned system, not a salvage wreck). When a system is claimed, `ConquestOwnership.Claim` calls `BuildOrRebuildHub`, which uses `ConquestOwnership.RebuildHub` (deferred with `rebuildOnLeave` while you are at the hub), and `ConquestOwnership.CompleteHubRebuild` finishes the job after you leave. A siege capture (`BattleManager.ApplyDecisiveStationCapture`) does not call `RebuildHub`. Part kills matter beyond the rebuild only if the hub is lost, which `OnStationDestroyed` handles on the conquest side.

`ConquestOwnership.Claim` also calls `RazeDefenseStations` (`FactionStationDestruction.Destroy` on each Defense station of `abandonedBy`) when the claimant is not the faction that abandoned the system.

Source: `Conquest.DoConquestTick`, `Conquest.RepairConquestStations`, `ConquestOwnership.RebuildHub`, `ConquestOwnership.CompleteHubRebuild`.

### Code: When the Last Part Dies in a War Zone

These steps run after the `CombatStationDestroyed` mission trigger and before the pocket achievement, in this order. `CombatStationPart.CheckCombatStationFullyDestroyed` fires them once no `CombatStationPart` other than a Connector is left in the station:

1. `ConquestOwnership.OnStationDestroyed` runs for the current station. If it is the hub of an owned war-zone system, `ConquestOwnership.Abandon` runs: it clears the owner and control level. Outside the player sector `HandOverToClaimant` then offers the claim or gives it to the fleet there. In the player sector `ReturnToSale` puts the system back on sale. A hub flagged `rebuildOnLeave` is skipped.
2. If the station is a faction station that is not already wrecked: if a part you hit was destroyed (`playerContributedToDestruction`), it is wrecked on the spot. Then, if an active live siege of that Defense station exists, the siege ends with the attacker taking the station. Otherwise, and if it is not wrecked yet, the faction station is destroyed.

A Defense station taken during a live siege (`DefenseStationDestroyed`, shown in the Empire Log as "Defense station destroyed" or "Defense station lost") goes through `FactionStationDestruction.Destroy` the same way.

Source: `ConquestOwnership.OnStationDestroyed`, `BattleManager.OnDefenseStationDestroyedLive`, `FactionStationDestruction.Destroy`.

### Code: The Station's Off-Screen Siege Numbers

| Part | Structure (hull) score | Firepower score |
|---|---|---|
| Station Core | 200 | 0 |
| Connector | 5 | 0 |
| Every other non-platform part | 20 | 0 |
| Defense Platform | `60 + 15 * S` | `40 * S` |

`S` is the sum over the part's prefab weapon slots of `max(1, size)`: Small 1, Medium 2, Large 3. So S1 is 1, S2 is 2, M2 is 4, L1 is 3, and the drone bay (no weapon slots) is 0. Destroyed parts (HP 0 after damage) are skipped. Damaged parts count in full. `StationDefense.EstimateFromSize` is the fixed estimate for an ungenerated station (never visited): Small 560 hull and 240 firepower, Medium 940 and 480, Large 1390 and 560.

The totals per builder are summed over the system's hub and every un-wrecked same-faction Defense station by `StationDefense.GetDefendingStations`, scaled by the sector's station-defense bonus, and halved while the system is settling (`isSettling`: `settlingTicks` is set to 6 by `StartSettling` and drops by one per conquest update). The siege uses the hull number as its "station structure" pool (`BattleInstance.StartStationStructure` sets `stationHull` and `stationHullMax`; "station structure" is this wiki's name, not a game string). A raid on an economic outpost (any non-Defense station) uses 15% of the hull and 50% of the firepower (`StationRaid.GetRaidProfile`).

Source: `StationDefense.ScorePart`, `StationDefense.ScoreParts`, `StationDefense.EstimateFromSize`, `StationDefense.GetStationProfile`, `StationDefense.GetProfile`, `StationDefense.GetDefendingStations`, `StationRaid.GetRaidProfile`, `BattleInstance.StartStationStructure`, `BattleManager.StartDefenseStationStage`.

#### Code: The Energy Field's Score

The Energy Field scores 20 hull and 0 firepower, like every other non-platform part that is not a Core or Connector. Its only code-side roles are this score and its loot table ([basics/stations.md](../basics/stations.md#the-energy-field)).

### Code: Defense Stations and Garrisons

A Defense station is a `FactionStation` of type Defense. It is built with the Small layouts (Small1 or Small2), and has the x20 HP multiplier while you are in a war-zone system.

Garrison refill paths:

- Warships delivered by `WarshipDeliveries`; `ConquestFleetPlanner.ChooseGarrisonToSupply` picks the garrison. In practice this is the only refill path.
- `Conquest.DrainCombatStrengthIntoGarrisons` runs each conquest update and would add one ship per 10 combat strength through `FactionGarrison.Repair` (900 s timer). It never fires, because nothing in the code raises a system's `combatStrength` above 0 (see [war.md](war.md)).
- Newly seeded Defense stations are provisioned to their wanted count.

`FactionGarrison.GetWantedCount` gives the wanted ship count and `FactionGarrison.PickShip` the ship (budget per ship is `tierGarrisonBudget / tierGarrisonCount`, integer division). Refit (`FactionGarrison.ProgressRefit`, `FactionGarrison.CompleteRefit`) takes 300 seconds and starts when `FleetLevels.IsDueRefit` is true: the points-weighted average garrison level (`FleetLevels.GetGarrisonLevel`) is at most the NPC fleet level minus 3. A new `FactionStation` has `tier` 1. Tiers are set in three places: `FactionEconomicPlanner.TryUpgrade` adds one tier for wealth, `ExpansionEconomySeeder.SeedGateFortresses` sets the Darkspace gate fortress Defense stations to tier 5 and provisions them, and a loaded save restores the saved value.

`FactionEconomicPlanner.TryTakeAction` calls `TryUpgrade` when `FactionStation.GetAllOwnedBy(faction).Count + economy.constructions.Count >= economy.outpostSlots`, up to twice per `FactionEconomicPlanner.Tick`. `ChooseUpgradeTarget` looks at every owned `FactionStation` with `tier < maxTier` (`PlayerStation.GetMaxTier`: 5 for every type except Production, which has 4) and keeps the one with the highest `GetStorageFullness()`, the first in the list on a tie. Nothing in it excludes `PlayerStationType.Defense`. A Defense station's `factionStorage` stays empty, because `StationProduction.Roll` returns null for it, so its fullness is 0. `FactionEconomicPlanner` never builds a Defense station; `ConquestFleetPlanner.TryBuildDefenseStation` does.

In a live siege, `FactionGarrison.SpawnInto` and `BattleManager.SpawnSiegeDefenders` spawn the garrison ships (`BattleManager.EnsureVenueUnits` for the venue). When the station is taken (live, early after 600 s of siege in `TryEndDefenseStationStageEarly`, or when the stage's timer runs out in `ResolveDefenseStationSiegeAtCap`) `BattleManager.TakeDefenseStation` sends it through `FactionStationDestruction.Destroy`. `FactionGarrison.GetStrength` returns the garrison points times `WarFooting.GetMultiplier(station.faction)`. The cap of 2 is `FactionStation.MaxPerSystem`.

Source: `FactionGarrison.GetWantedCount`, `FactionGarrison.PickShip`, `FactionGarrison.Repair`, `FactionGarrison.ProgressRefit`, `FactionGarrison.CompleteRefit`, `FactionGarrison.SpawnInto`, `Conquest.DrainCombatStrengthIntoGarrisons`, `ConquestFleetPlanner.ChooseGarrisonToSupply`, `FactionEconomicPlanner.TryTakeAction`, `FactionEconomicPlanner.TryUpgrade`, `FactionEconomicPlanner.ChooseUpgradeTarget`, `PlayerStation.GetMaxTier`, `ExpansionEconomySeeder.SeedGateFortresses`, `StationProduction.Roll`, `WarshipDeliveries`, `BattleManager.SpawnSiegeDefenders`, `BattleManager.EnsureVenueUnits`, `BattleManager.TakeDefenseStation`, `FactionStation.MaxPerSystem`.
