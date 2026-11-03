# The control story line

This page covers the Umbral Reach: its control row and how it spreads, the Umbral Hacking Tool, the puppeteers' station, and every exception the Umbral transponder makes to the early rules. It is a spoiler for the Umbral story line, which opens as part of the reward for your faction's first Conquest mission (see the [late-game gates](README.md#spoiler-gates)). Values are read from the game code of version 0.8.3.4 (the decompiled source, not in-game testing). Anything not confirmed in code is marked "(not verified)".

Terms are in the [late-game glossary](glossary.md) and the [basics glossary](../basics/glossary.md).

## Spoiler: The control row and its spread

The "Umbral Reach control" row shows how far the Umbral Reach has spread into a system, as a percentage. It appears only above 0.

- **Growth.** Each conquest update (every 3600 s, 1 hour) a system already above 0 rises by a random 0.05 to 0.1, up to a maximum of 1 (100%).
- **Spread.** A system at 1.0 can seed one adjacent system at 0.05, but only with a 50% roll each update, and at most one new system per update.
- **Starting an infection.** The Umbral Hacking Tool starts it by setting a system to 0.05 (0.7 for the "uber" variant). It needs a friendly station and an infected neighbor above 0.19, unless it is the "uber" variant.
- **At an Umbral station.** Missions unlock at 5% station control and shops at 50%.

This page does not cover what Umbral control does beyond those thresholds.

In the code: [the control row and its spread](#code-the-control-row-and-its-spread).

### On the system tooltip and in system data

The conquest section of a war-zone system tooltip has a third kind of row, "Umbral Reach control" (the Defense row and "Your war effort" are the other two; see [What the system tooltip shows](../conquest/war.md#what-the-system-tooltip-shows)). An unowned system whose hub is an abandoned hulk can also show an Umbral Reach row (the Defense number is 0, and a war effort row can appear).

In the code: [on the system tooltip and in system data](#code-on-the-system-tooltip-and-in-system-data).

### The conquest update

The hourly conquest update also spreads the Umbral Reach. The [faction economy](../conquest/faction-economy.md#when-things-happen) page lists it as recalculating Area Conquered, rebuilding damaged stations and counting down settling.

In the code: [the conquest update](#code-the-conquest-update).

## Spoiler: The story faction's station

The puppeteers' station in quadrant 2 is not an economic outpost or an outpost system. It has 6 parts.

| Core | Energy | Refinery | Cargo Dock | Docking Pad (Small / Medium) | Tunnel | Platforms | Connectors |
|---|---|---|---|---|---|---|---|
| 1 | 0 | 1 | 0 | 1 / 0 | 1 | 2 tier (same roll) | 0 |

The columns are the same as in the part list tables on [Combat stations](../basics/stations.md#part-lists) and [War stations](../conquest/war-stations.md#part-lists).

Turret tier rule: the two "tier" turrets follow the same tier rule as the Small, Medium and Large conquest stations ([War stations](../conquest/war-stations.md#turret-tier-rules)). Below location level 45 they are S2. At level 45 or above they are M2 or L1 with a 50/50 roll. Here the roll is not tied to a per-station seed, and both turrets share one roll.

Off-screen siege numbers (scoring as in [War stations](../conquest/war-stations.md#the-stations-off-screen-siege-numbers)):

| Both tier turrets are | Hull | Firepower |
|---|---|---|
| S2 (below level 45) | 440 | 160 |
| both M2 or both L1 | 470 to 500 | 240 to 320 |

In the code: [the story faction's station](#code-the-story-factions-station).

## Stations with no reputation loss

Several mission stations and Umbral stations are flagged so that killing their parts costs no reputation with the owner. The flag overrides the per-part kill penalty (100 points a part while you are Neutral or better, 5 otherwise; see [Killing parts](../basics/stations.md#killing-parts)).

In the code: [stations with no reputation loss](#code-stations-with-no-reputation-loss).

## Spoiler: What the transponder changes

With the Umbral transponder, several reputation costs from the early pages are skipped. Each section below names the rule it changes and links to it.

### Entering a facility or boarding a ship

Changes [Reputation cost of entering](../basics/boarding.md#reputation-cost-of-entering). Having the Umbral transponder removes the reputation penalty for entering a facility or boarding a ship. That page also explains the ships that skip the penalty anyway.

In the code: [entering a facility or boarding a ship](#code-entering-a-facility-or-boarding-a-ship).

### Killing station parts

Changes [Killing parts](../basics/stations.md#killing-parts). If you have the Umbral transponder, every part you kill skips the owner's reputation hit.

In the code: [killing station parts](#code-killing-station-parts).

### Destroying a faction station

Changes [Wrecking a faction station](../basics/stations.md#wrecking-a-faction-station) and [Reputation cost of destroying stations](../conquest/war.md#reputation-cost-of-destroying-stations). Destroying a faction station while you hit it (live) costs -5000 reputation with its faction, or -1000 if your reputation with it is already at or below -5000 (Hostile or worse). With the Umbral transponder that loss is skipped.

In the code: [destroying a faction station](#code-destroying-a-faction-station).

### Destroying a yard

Changes [What happens when you destroy a yard](../conquest/faction-economy.md#what-happens-when-you-destroy-a-yard). The reputation cost for destroying a Construction Yard is 0 if you carry the Umbral transponder, whatever your standing with the faction.

| Your standing with the faction | Reputation cost |
|---|---|
| You carry the Umbral transponder | 0 |

In the code: [destroying a yard](#code-destroying-a-yard).

### Slowing a faction by destroying its only yard

Changes [Ways to slow a faction](../conquest/faction-economy.md#ways-to-slow-a-faction). In that table, row 2 (destroy its only Construction Yard) costs 0 reputation with the Umbral transponder, in addition to -5,000 (-1,000 if Hostile or worse).

## In the game code

This part holds the code names behind the sections above. It comes from the decompiled source of version 0.8.3.4. To check a claim, search the decompiled source for the method or field named here. Anything not confirmed in code is marked `(not verified)`.

### Code: The control row and its spread

The row shows `umbralControlLevel` as a percentage and appears only above 0. Each conquest update (every 3600 s) a system already above 0 rises by a random 0.05 to 0.1, clamped at 1. A system at 1.0 can seed one adjacent system at 0.05, but only with a 50% roll each update and at most one new system per update. The Umbral Hacking Tool starts infection by setting a system to 0.05 (0.7 for the "uber" variant); it needs a friendly station and an infected neighbor above 0.19 unless it is the uber variant. Missions at an Umbral station unlock at 5% station control and shops at 50%. This page does not cover what umbral control does beyond those thresholds.

The Umbral line is opened by `ConquestUmbral1` (see the [late-game gates](README.md#code-gates)).

Source: `Conquest.TickUmbralSpread`, `UmbralHackingTool`, `UmbralHackingBot`, `SpaceStationButton`

#### Code: On the system tooltip and in system data

Every war-zone system's `ConquestSystem` also stores umbral control, next to who owns it, war effort timers and settling state.

#### Code: The conquest update

`Conquest.DoConquestTick`, the hourly conquest update, also spreads the Umbral Reach.

Source: `Conquest.DoConquestTick`

### Code: The story faction's station

The puppeteers' station in quadrant 2 is built by `CreateUmbralOutpost`. "Umbral outpost" is the code builder name for it. It is not an economic outpost or an outpost system. It has 6 parts.

| Seed | Builder | Result |
|---|---|---|
| (puppeteers station, quadrant 2) | `CreateUmbralOutpost` | 6 parts |

Part list (columns as in the part list tables on [Combat stations](../basics/stations.md#part-lists) and [War stations](../conquest/war-stations.md#part-lists)):

| Builder | Core | Energy | Refinery | Cargo Dock | Docking Pad (Small / Medium) | Tunnel | Platforms | Connectors |
|---|---|---|---|---|---|---|---|---|
| Umbral outpost | 1 | 0 | 1 | 0 | 1 / 0 | 1 | 2 tier (same roll) | 0 |

Turret tier rule: the two "tier" turrets follow the same tier rule as the Small, Medium and Large conquest builders ([War stations](../conquest/war-stations.md#turret-tier-rules)): below location level 45 they are S2; at level 45 or above they are M2 or L1 with a 50/50 roll. In the Umbral outpost the roll uses the global random generator instead of a per-station seed, and both turrets share one roll.

Off-screen siege numbers, with both tier turrets S2 (below level 45) and, in brackets, the range when they are both M2 or both L1:

| Builder | Hull | Firepower |
|---|---|---|
| Umbral outpost | 440 (470 to 500) | 160 (240 to 320) |

Source: `CombatStationFactory.CreateUmbralOutpost`, `CombatStationFactory.CreateStationVariant`, `StationDefense.ScorePart`.

### Code: Stations with no reputation loss

The flag is `noReputationLoss`. Killing their parts costs no reputation with the owner, which overrides the per-part kill penalty.

Source: `LootManager.HandleKillReputation`, `LootManager.DropLoot`.

### Code: What the transponder changes

The transponder is the `hasUmbralTransponder` flag.

#### Code: Entering a facility or boarding a ship

The ships that skip the penalty anyway are the `noReputationLoss` ships.

Source: `DungeonManager.FriendlyEntryRepPenalty`, `DungeonManager.ApplyFriendlyFactionPenalty`, `DungeonManager.TriggerFriendlyFactionAggro` (the original page gave this Source line for the whole section; I did not separate which of the three checks the transponder).

#### Code: Killing station parts

Source: `LootManager.HandleKillReputation`, `LootManager.DropLoot`.

#### Code: Destroying a faction station

The code check is `hasUmbralTransponder`.

Source: `FactionStationDestruction.ApplyDestroyReputation`, `FactionStationDestruction.OnDestroyedByPlayer`, `FactionStationDestruction.GetDestroyReputationLoss`.

#### Code: Destroying a yard

Source: `FactionStationDestruction.GetDestroyReputationLoss`
