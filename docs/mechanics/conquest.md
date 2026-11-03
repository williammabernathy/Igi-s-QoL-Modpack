# Conquest

This page covers the Conquest Map: what the Defense number on a system tooltip is made of, how battles and sieges run and end, how NPC (non-player character) factions pick fights and raids, what each fleet label on the map means, what war effort does, how your own Empire Fleets take part, and how to finish the 30% sector-control mission. Values are read from the game code of version 0.8.3.4 (the decompiled source, not in-game testing). Anything not confirmed in code is marked `(not verified)`.

Terms and abbreviations are explained in the [glossary](glossary.md).

"Points" below are ship point values; the fleet cards in the game call the same unit "strength". How those values are derived is covered in [scaling](scaling.md). Your own fleets on the Conquest Map are called Empire Fleets. The hourly step the game shows as the "Conquest Update" card on the galaxy map is called the conquest update here (`Conquest.DoConquestTick`); a separate hourly step that runs the faction economy is the hourly economy tick, which the game does not show. Faction income, shipyards, warship delivery, the Great Gate and raid economics are in [faction-economy](faction-economy.md). Per-part station HP (hit points), loot and station layouts are in [stations](stations.md). Damage between live ships is in [combat-damage](combat-damage.md).

## The war zone and the kinds of system

The war zone is every system where factions fight over ownership: the Conquest sector (Ara Martis, the original war zone) and, once unlocked, the expansion sectors. In code it is every system whose sector is a conquest sector (the Ara Martis region) or sits in an expansion quadrant, excluding reserved slots. The Frontier, Staging and Darkspace regions are not part of it. Every war-zone system has a `ConquestSystem` that stores who owns it, war effort timers, settling state and umbral control, and a hub: its main station (`ConquestStation`, shown as "Space Station" on the map), which carries the owner's flag. Destroying the hub abandons the system. Systems outside the war zone are not part of fleet conquest.

| Kind | System card title or hint | What it means |
|---|---|---|
| Outpost system | "Conquest outpost" | Any owned war-zone system that is not a headquarters or your capital. Fleets can besiege and capture it. |
| Headquarters | "Conquest headquarters" | One system per conquest faction. It has the faction's commander and shop, and its hub takes x30 live hit points (HP) instead of x20. Fleets cannot take it: `cannotBeTaken` is true, NPC fleet target lists skip it, and your Attack order is refused ("A faction's headquarters can't be taken by a fleet."). |
| Your capital | "Your capital - no faction can take it" | The first system you hold becomes your capital. Same protection as a headquarters. Your economic outposts in it can still be raided. |
| Unowned | (section empty) | No faction owns it. The Defense number is 0 and no rows show, unless the hub is an abandoned hulk (then the Defense number is 0, and war effort and Umbral Reach rows can appear). |
| Abandoned | same as unowned; "Abandoned - the first faction to arrive claims this system" on the hub | A system whose hub was destroyed. It remembers `abandonedBy`, which matters when someone claims it (see below). |
| Free territory | (section empty); "Free territory is settled by the factions - you can only take a system from its owner" | Unowned system whose hub is Independent. You cannot claim it ("you can only take a system from its owner"); NPC factions can. |
| Player sector | "set aside for you - buy it at its hub" | One expansion sector is set aside for you. NPC factions cannot enter its unowned systems. You buy these at their hub. |
| Safe sector | "safe from invasions" | Sector with the SafeSector rule. Closed to every faction except you, and any NPC battle already running there is aborted. The player sector gets this rule. |
| Meridia | "There is nothing to besiege here" when you order an attack | The Meridia sector allows no stations (`AllowsStations` false), so Malcyte hold it without hubs. There is nothing to besiege, and fleets cannot attack it. |

While Meridia is closed, systems in the Meridia quadrant are open only to Malcyte, their war ally Meridia's Chosen, and (outside the Meridia sector itself) Darkspace Compact. That includes you. Jump gates into the quadrant, and into the Meridia sector from outside it, are closed. Nothing in the decompiled code outside the debug console calls `Conquest.OpenMeridia`, so how it opens in a normal game is `(not verified)`.

Source: `ConquestScope.IsWarZone`, `ConquestScope.IsOpenTo`, `ConquestScope.IsClosedMeridiaGate`, `ConquestScope.IsSafeSector`, `ConquestSystem.cannotBeTaken`, `ConquestSystem.isHubless`, `MapTooltipCardBuilder.GetConquestSectionTitleKey`, `SystemClaim.IsFreeTerritory`, `ConquestWorld.MarkPlayerSector`, `EmpireFleets.RefuseAttack`

## What the system tooltip shows

The conquest section of a war-zone system tooltip (the system card) has up to three kinds of row: the Defense row, "Your war effort" (green for a bonus, red for a penalty, with time left) and "Umbral Reach control". The localization key `SystemCardControl` ("Control") exists, but `BuildConquestSection` never adds it in 0.8.3.4, so no Control row appears. The `controlLevel` field (0, 1 or 2) is still tracked, and the section below on control level covers what it does.

Source: `MapTooltipCardBuilder.BuildConquestSection`, `MapTooltipCardBuilder.AddWarEffortRows`

### What the Defense number means

The Defense number, shown as the Defense row, is the strength the defenders put into a siege round, in the same unit as ship points. A Defense station is a station type that holds a garrison (warships stationed in it) or patrol fleets and fires on enemies; it is not the same thing as the Defense number. The number is the sum of four terms. An unowned system is 0.

```
Defense = StationFirepower
        + sum over faction Defense stations: garrison ship points
        + sum over defending fleets: totalPoints * (1 + FleetMixBonus)
        + (your systems only) sum of patrol ship points

StationFirepower = bound(profile.firepower) + sectorFlatBonus
bound(fp)        = max(250*m, min(fp, 700*m) * structureFraction) * warEffortMultiplier
```

`m` is the sector StationDefense multiplier and `sectorFlatBonus` the sector flat bonus. No code in 0.8.3.4 creates a StationDefense sector bonus (only SafeSector is created), so in a standard game `m = 1` and the flat bonus is 0 `(not verified for old saves)`.

The station term uses a profile summed over the system's hub and every non-wrecked Defense station of the same owner, each scaled by `m`. Parts score as follows; destroyed parts (HP at 0 and damage taken) are skipped:

| Part | Hull | Firepower |
|---|---|---|
| Defense platform | 60 + 15 x sizes | 40 x sizes |
| Core | 200 | 0 |
| Connector | 5 | 0 |
| Anything else | 20 | 0 |

`sizes` is the sum over the platform's turret slots of `max(1, size)` with Small = 1, Medium = 2, Large = 3. A platform with two medium turrets has sizes 4, so hull 120 and firepower 160. A station whose contents have not been generated yet uses a flat estimate:

| Station size | Hull | Firepower |
|---|---|---|
| Small | 560 | 240 |
| Medium | 940 | 480 |
| Large | 1390 | 560 |

Three things shape the station term:

- Firepower above 700 does not count. Past that cap, extra platforms only add hull.
- The floor of 250 applies even to a station with no weapons left.
- While a hub siege (the `StationSiege` stage) is running, `structureFraction` is the siege's remaining station hull fraction, so the term falls as the station breaks, down to the 250 floor. Outside a siege, or during a Defense station stage, the fraction is 1.

The whole profile is multiplied by 0.5 while the system is settling after a change of owner (see "Settling and unrest"). The 250 floor is not halved.

Each faction Defense station holds a garrison, a group of warships stationed in it. The number of ships and the point budget depend on its tier:

| Tier | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| Garrison ships | 3 | 4 | 5 | 6 | 8 |
| Garrison budget (points) | 270 | 405 | 540 | 720 | 900 |

The game picks each ship as budget divided by ships (90 to 120 points), so garrison strength is close to the budget when full. The garrison counts for a system's Defense in full, summed across every Defense station in it. Ships lost from a garrison are restocked by warships built at a Construction Yard (a faction station that builds warships); see [faction-economy](faction-economy.md). The older "combat strength drains into garrisons" path in `Conquest.DrainCombatStrengthIntoGarrisons` never fires because nothing in the code raises `combatStrength` above 0.

A defending fleet counts if it is of the owner's faction (or an allied Empire Fleet defending an allied system), is in the system, is not traveling, and "joins defense". NPC fleets always do. Only Empire Fleets have a fleet mix bonus:

```
FleetMixBonus = sizes * rate    (only if the fleet has 2 or more distinct non-drone ship sizes)
rate = 1% per size, 2% at empire level 5, 3% at empire level 10
```

Patrol ships count only for your own systems: every ship assigned to a patrol fleet (ships you assign to guard one of your Defense stations) at one of your Defense stations in the system counts for its full points. There is no mix bonus.

An illustrative case: a medium hub (480), one tier-3 Defense station (about 540), and one full NPC fleet of 2000 points give about 3020. That is a worked figure from the formulas, not a value read from the game.

Source: `ConquestCombat.GetDefenseStrength`, `ConquestCombat.GetStationFirepower`, `ConquestCombat.BoundStationFirepower`, `StationDefense.GetProfile`, `StationDefense.ScorePart`, `StationDefense.EstimateFromSize`, `FactionGarrison.GetStrength`, `FactionGarrison.PickShip`, `FleetMixBonus.GetBattleStrength`, `ConquestCombat.GetPatrolShips`

### What NPC factions use instead of the Defense number

NPC factions do not compare against the tooltip number. Their fleet planning uses next-battle strength (`GetNextBattleStrength`), which the game does not show anywhere. If the system has Defense stations, it is the strength of the weakest one (that station's own firepower, its garrison, and all defending fleets in the system); otherwise it is the full Defense number. It then adds the points of every same-faction fleet that is Traveling toward the system as relief. You can estimate it from what the game shows: with no Defense station in the system it equals the Defense row (plus any fleets already on the way); with Defense stations it is lower. The Defense row is the upper bound, before fleets on the way.

Source: `ConquestCombat.GetNextBattleStrength`, `ConquestCombat.ChooseNextDefenseStation`, `ConquestCombat.GetIncomingRelief`, `ConquestCombat.GetDefenseStationStrength`

### Ways to lower or raise Defense

| Lever | Effect |
|---|---|
| War effort penalty on the system | Station term x0.5 at the maximum penalty. See "War effort". |
| Destroy defender ships or fleets | Removes their points one for one. Intercepting fleets that are still traveling removes the relief term. |
| Destroy turret platforms or core by hand | Removes each platform's hull and firepower from the profile. Parts must be fully destroyed (HP 0 with damage taken). |
| Attack within a few hours of a capture | Station profile x0.5 while settling. |
| Siege progress | Each hub siege round that breaks station structure lowers the station term toward 250. |
| To raise your own: stack fleets in the system | Fleets with a defense stance count in full, plus the mix bonus. |
| To raise your own: patrol fleets | Each Defense station gives 1 to 3 patrol fleets of 120 to 500 points each by tier. |
| To raise your own: war effort bonus | Up to x2 on the station term. See "War effort". |
| To raise your own: more turret platforms | Helps only up to 700 firepower. |

Patrol fleet capacity by player Defense station tier:

| Tier | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| Patrol fleets | 1 | 1 | 2 | 2 | 3 |
| Points per fleet | 120 | 240 | 260 | 400 | 500 |

Source: `PlayerStation.patrolFleetCount`, `PlayerStation.patrolFleetBudget`, `StationDefense.GetDefendingStations`

### Umbral Reach control

The "Umbral Reach control" row shows `umbralControlLevel` as a percentage and appears only above 0. Each conquest update (every 3600 s) a system already above 0 rises by a random 0.05 to 0.1, clamped at 1. A system at 1.0 can seed one adjacent system at 0.05, but only with a 50% roll each update and at most one new system per update. The Umbral Hacking Tool starts infection by setting a system to 0.05 (0.7 for the "uber" variant); it needs a friendly station and an infected neighbor above 0.19 unless it is the uber variant. Missions at an Umbral station unlock at 5% station control and shops at 50%. This page does not cover what umbral control does beyond those thresholds.

Source: `Conquest.TickUmbralSpread`, `UmbralHackingTool`, `UmbralHackingBot`, `SpaceStationButton`

### Control level

`controlLevel` is 0 (unowned), 1 or 2. It rises by 1 each conquest update for an owned system with no battle in it and every neighboring system held by the same owner. The step from 1 to 2 in an NPC-owned system can inject a conquest dungeon. A hub siege that ends in Skirmish or Raided lowers it by 1 if above 0; capture sets it to 1. Beyond the dungeon, the only other readers in the code are the headquarters tie-break in `Conquest.EnsureSingleHeadquarters` (garrison strength, then resident fleet points, then control level) and a debug command.

Source: `Conquest.ApplyControlLevelUpgrades`, `Conquest.EnsureSingleHeadquarters`, `BattleManager.ApplyStationSiegeSkirmish`

## War effort

War effort is a per-system multiplier on station firepower, set by Conquest missions you complete (missions from a war-zone hub; the game shows them with a reward line such as "+10% Station firepower"). The tooltip shows it in the "Your war effort" row as "{percent} firepower ({time left})". Idle missions are the ones ECHO, your ship's AI companion, completes while you are away.

```
warEffortMultiplier = max(0, 1 + warEffortBonus - warEffortPenalty)
step                = 0.1 * (1 + 0.2 * ECHO_Conqueror_points)    for idle missions (done by ECHO)
step                = 0.1                                         otherwise
```

When you complete a mission whose source station is in a war-zone system (`ConquestWarEffort.Apply`):

| Where | Condition | Change |
|---|---|---|
| The mission's system | owned by the mission's source faction | bonus + step |
| Each neighboring system | owned by the source faction | bonus + step/2 |
| Each neighboring system | owned by the mission's targeted enemy faction | penalty + step |

The targeted faction is the mission's enemy faction, if it is a conquest faction and is not the employer. The mission reward line says so ("+10% Station firepower, -10% for X next system").

| Limit | Value |
|---|---|
| Bonus cap | +1.0 (multiplier x2) |
| Penalty cap | 0.5 (multiplier x0.5 if no bonus) |
| Duration | 10800 s (3 h) |

Each side (bonus and penalty) has one timer, and every new step on that side resets it to the full 10800 s. When the timer runs out the whole bonus or penalty clears at once; it does not decay gradually. The timers count down every game update, not once per conquest update. War effort is cleared on any change of owner, and when a system is abandoned.

The multiplier applies in two places:

- In the Defense formula, to the whole station firepower term, including the 250 floor. With `m = 1`, the maximum bonus (x2) puts the term between 500 (floor) and 1400 (cap); the maximum penalty (x0.5) puts it between 125 and 350.
- On live stations: while you are at a conquest hub or a faction Defense station in a war-zone system, the multiplier scales combat power, drone power and torpedo power (stat indices 21, 25 and 26) of that station's parts. See [stations](stations.md) for the live HP multipliers (x20, x30 at a headquarters).

"ECHO The Conqueror" (skill node `PromptEngineeringFleetStrengthBoost`) adds 20% of the base step per point, to idle Conquest missions only. Whether a mission gives war effort to your own empire systems depends on `mission.sourceFaction` matching the system owner; missions from your own stations are `(not verified)`.

Source: `ConquestWarEffort.GetStep`, `ConquestWarEffort.Apply`, `ConquestSystem.AddWarEffortBonus`, `ConquestSystem.AddWarEffortPenalty`, `ConquestSystem.TickWarEffort`, `LivingUniverse.StoryUpdate`, `ConquestStrength.OnComplete`, `CombatStationPart.ApplyWarEffort`, `SystemMapData.SetConquestFaction`

## Battles

### Types and stages

Every fight is a `BattleInstance` in one `BattleStage`:

| Stage | Who fights | Time cap | Ends with |
|---|---|---|---|
| `FleetClash` (a clash; the game calls it a Fleet Battle) | The arriving attackers against the first defending fleet in the system. A "Fleet Battle" location appears in the system. | 600 s | Winner goes on to a siege stage; otherwise the battle ends. |
| `DefenseStationSiege` | Attackers against one faction Defense station's garrison, its firepower and every defending fleet. | 1800 s | `DefenseStationDestroyed` (Empire Log: "Defense station destroyed") or another outcome. Starts with the weakest Defense station. |
| `StationSiege` | Attackers against the hub: all garrisons, fleets and patrols, plus station firepower. Used when no Defense station is left. | 1800 s | `Captured` ("system captured"), `Skirmish` ("inconclusive skirmish"), `Repelled` ("attack repelled") and so on. |
| `Raid` | A raider fleet against one economic outpost's raid profile and its defenders. | 900 s | `Raided` ("raid succeeded"), `Repelled`, `AttackerDestroyed` ("fleet destroyed"). |

Flow when an attacking fleet arrives at a hostile system (`OnFleetArrived`): unowned systems are claimed, not fought over (below). Otherwise, if any defending fleet is present, a `FleetClash` starts against the first one; if none, a siege starts. A fleet whose faction is already the attacker in an active non-raid fight at that system joins it as a supporter instead, bringing its points (`pointsAtJoin`). Allied NPC factions do not defend each other; defenders must be the same faction as the owner.

After a clash win the same battle becomes a siege. A Defense station falls in its own battle; the attacking faction's fleet planning then sends it again, and the next siege starts with the weakest remaining Defense station. Each stage has a fresh 1800 s timer. You may see several battles back to back in one system.

Source: `BattleManager.OnFleetArrived`, `BattleManager.StartEngagement`, `BattleManager.StartFleetClash`, `BattleManager.StartSieges`, `BattleManager.BeginNextSiegeStage`, `BattleManager.TransitionToStationSiege`, `BattleManager.TryJoinBattleOnArrival`, `BattleManager.StartRaid`

### One round

When you are not in the battle's venue, a battle runs as simulated rounds every 60 s. The cap timers above count down in the same 60 s steps (30 rounds for a siege, 10 for a clash, 15 for a raid).

```
pool(side)  = carriedDamage(side) + strength(side) * random(0.04, 0.06)
attackers:  strength = sum of (fleet totalPoints * (1 + FleetMixBonus)) * (1 + hullEdge)
defenders:  strength = (Defense, Defense-station strength, or raid defense) * (1 - hullEdge)
```

Each pool is spent on that side's own ships one at a time. The next victim is picked at random with weight `pointValue^-0.5` (`BattleTargetSizeBias` 0.5), so cheaper ships are picked more often. A ship dies only if the remaining pool covers its whole point value. If the picked ship costs more than what is left, spending stops and the remainder stays in `damageOnAttacker` or `damageOnDefender`, to be added to next round's pool. Fleets keep unspent damage after a battle as `carriedDamage` and repair it only while resting at home, 100 points per hour.

The defenders' pool can go past their last ship. That spill is the only damage the station structure takes: `stationHull` falls only after every garrison ship, defending fleet and patrol ship in the fight is dead.

Over a full 30-round siege each side deals roughly 30 x 0.05 = 1.5 times its own strength in total damage (derived from the constants, not read as one number). A 10-round clash deals about 0.5 times. A clash is therefore usually decided on points at its cap, not by wiping a fleet.

Attackers' strength in these rounds includes the fleet mix bonus. The capture checks below use plain `totalPoints` with no mix bonus.

Source: `BattleSimulation.RollDamage`, `BattleSimulation.SpendPool`, `BattleSimulation.PickTarget`, `BattleSimulation.TargetWeight`, `BattleSimulation.RunSiegeRound`, `BattleSimulation.RunDefenseStationRound`, `BattleSimulation.RunClashRound`, `BattleSimulation.RunRaidRound`, `BattleManager.TickOffscreen`, `ConquestWarTuning.BattleDamageShareMin`, `ConquestWarTuning.BattleTargetSizeBias`

### Hull edge

Each round the game computes an average hull rating for each side's remaining ships, weighted by point value, and gives the attacker a bonus or malus:

```
edge = clamp((attackerRating - defenderRating) * 0.005, -0.15, +0.15)
```

The attacker's strength is multiplied by `1 + edge` and the defender's by `1 - edge`. A gap of 30 rating points reaches the 15% cap. A side with no ships (only the station) has no rating, so the edge is 0. A ship's rating is its hull points clamped to 0 to 100. NPC ships start with hull points rolled from their level and ship rank (the Rookie to Legendary tier shown on an enemy ship's tooltip; max 85 at roll); your ships use the larger of their spent upgrade points and an NPC-equivalent figure. Winners gain hull XP (experience points):

| Event | XP |
|---|---|
| Battle ends in a win for that side (siege, or a clash the attacker lost) | 4 per surviving ship |
| Raid ends in a win for that side | 1 per surviving ship |

The next point costs `ceil(7 + 0.65 * points)` XP, and ships stop earning at 85 earned points. XP is paid when a battle ends, to every surviving ship of the winning side's fleets (and Defense garrisons). The winning side is the attacker for `Captured`, `Skirmish`, `Raided` and `DefenseStationDestroyed`, and the defender for `Repelled`, `AttackerDestroyed` and `Withdrew`. A clash the attackers win does not pay XP, because it continues into a siege instead of ending.

The Fleet card shows "Hull edge ... vs ... fleets nearby" for enemy fleets within 2 jumps.

Source: `HullRating.GetEdge`, `HullRating.GetSideRating`, `HullRating.GetHullPoints`, `HullXp.Award`, `HullXp.GetXpForNextPoint`, `BattleManager.AwardHullXp`, `BattleSimulation.UpdateHullEdge`

### When you are there: live battles

If your ship is in the battle's venue (the Fleet Battle zone, the besieged station, or the raided economic outpost), the game spawns the real ships of both sides there and the battle is live. In a live battle:

- Simulated rounds and the time cap stop. Nothing is resolved by the clock while you are present.
- Every 5 s the game checks which spawned ships died and removes them from their fleets (`SyncLiveLosses`).
- A clash ends when one side has no ships left in the zone. A siege ends early as `AttackerDestroyed` when the attackers have none left. If you destroy the hub, the system is abandoned; if you destroy a Defense station, the siege ends as `DefenseStationDestroyed`.
- Ships of your own Empire Fleets and patrol fleets that die are removed permanently, as are NPC ships in their fleets.

An undefended raid is simulated by rounds even if you are present; a raid with defenders runs live.

Source: `BattleManager.Tick`, `BattleManager.TickLive`, `BattleManager.EnsureVenueUnits`, `BattleManager.SyncLiveLosses`, `BattleManager.TryResolveLiveFleetClash`, `BattleManager.OnDefenseStationDestroyedLive`, `BattleManager.IsRaidLeftToRounds`, `CombatStationPart.CheckCombatStationFullyDestroyed`

### How a siege ends

Checks run after each simulated round, and again at the cap.

A hub siege (`StationSiege`) ends early if the lead attacker has no ships (`AttackerDestroyed`) or `CanCaptureEarly` holds. A Defense station siege uses the same rule against its own structure.

`CanCaptureEarly` requires all of:

- 600 s have passed in this stage.
- The system is capturable (not a headquarters or your capital).
- The station structure is at 0.
- There are no defenders left: no garrison ships, defending fleets or patrol ships.
- Attacker points (`totalPoints`, summed over attacker fleets still standing) are at least 1.25 x the current Defense number.

For a Defense station the checks are the same but compared against that station's strength with its structure fraction, and defenders means that garrison and the defending fleets.

At the cap (1800 s), a hub siege is judged like this:

```
structure broken, system capturable, points >= 1.25 * Defense  ->  Captured (Raided if the attacker cannot conquer)
points >= Defense                                               ->  Skirmish
otherwise                                                       ->  Repelled
```

The cap check does not require defenders to be dead; the defenders still alive are already in the Defense figure. Defense station siege at the cap: if the structure is broken and points are at least 1.25 x the station's strength, all defending fleets in the system are wiped and the station is destroyed (`DefenseStationDestroyed`, or `Raided` if the attacker cannot conquer); otherwise `Repelled`.

A clash at its cap (600 s) goes to the attackers if their combined points beat the defender fleet's points; they then continue to a siege. Otherwise they go idle and the outcome is `Repelled` (or `AttackerDestroyed` if the lead attacker is gone).

### Outcomes

| Outcome | Empire Log text (attacker's view / defender's view) | What triggers it | Result |
|---|---|---|---|
| `Captured` | "system captured" / "system lost" | Hub siege breakthrough by a faction that can conquer; a besieger that claims an abandoned system | System changes hands. See "Capture". |
| `Skirmish` | "inconclusive skirmish" | Hub siege at the cap with points at or above Defense but the capture conditions unmet (structure still standing, or points under 1.25x Defense) | Control level -1 if above 0. Attackers go idle. No re-attack block: the hub cap path does not call `BlockReattackAfterStalledSiege`. |
| `Repelled` | "attack repelled" | Attacker points below Defense at the cap; clash lost at the cap; raid broken off; Defense station siege at the cap not won | Attackers go idle. A Defense station siege that ends `Repelled` blocks re-attack for 3600 s. A hub siege `Repelled` at its cap, a lost clash and a broken-off raid do not. |
| `AttackerDestroyed` | "fleet destroyed" / "attackers destroyed" | No attacker ships left in any stage | Fleet dissolves if empty. |
| `Aborted` | no text of its own | The target or station vanished, the station was wrecked, the system became a safe sector, or the raided economic outpost changed hands | Attackers go idle. |
| `DefenseStationDestroyed` | "Defense station destroyed" / "Defense station lost" | A Defense station siege won as above, or you destroyed the station live | Station destroyed (a wreck for 3600 s that holds 10% of its stock); garrison counted as lost. No re-attack block. |
| `Withdrew` | "withdrew before relief arrived" / "attackers withdrew" | The lead attacker is ordered away with no standing supporters | Attackers go idle. Empire Fleets only, in practice. |
| `Raided` | "raid succeeded" / "outpost raided" | A raid succeeds; or a siege breakthrough by a raider faction (Corsair Syndicate while it holds no war-zone systems, and Malcyte) | In a hub siege: control -1, no capture. Re-attack is blocked for 3600 s when the siege ends early or is a Defense station siege, but not when a hub siege reaches its cap. For a Defense station siege the station survives and control is unchanged. In a raid: see below. |

The Empire Log shows the wording in the second column. The battle report text uses the English outcome name, plus losses, station structure left and the hull edge.

Source: `BattleManager.JudgeSiegeAtCap`, `BattleManager.CanCaptureEarly`, `BattleManager.TryEndSiegeEarly`, `BattleManager.ResolveDefenseStationSiegeAtCap`, `BattleManager.ResolveFleetClashAtCap`, `BattleManager.TryEndDefenseStationStageEarly`, `BattleManager.CanTakeDefenseStation`, `BattleManager.ApplySiegeOutcome`, `BattleManager.BlockReattackAfterStalledSiege`, `BattleManager.GetBreakthroughOutcome`, `ConquestJoin.IsRaider`, `BattleReport.Describe`, `ConquestWarTuning.SiegeCaptureMargin`

### Raids

A raid targets one economic outpost: a station that produces something (Mining, Salvage, Refinery, Forge, Production, Trading, Recruitment or Construction Yard) and is not a Defense station. It can be an NPC faction's economic outpost or one of yours. Structure and firepower come from the station's normal profile scaled to 15% hull and 50% firepower. Defenders are the owner's fleets and patrols in the system.

| Rule | Value |
|---|---|
| Cap | 900 s |
| Earliest success | 600 s elapsed, structure broken, and no defenders left |
| Breaks off (`Repelled`) | Raider has lost 50% or more of its starting points (not for frontier raid parties) |
| Any raider at the cap | Success if the outpost is taken (structure broken, no defenders). A raid party still facing defenders gets 60 more seconds, repeatedly. Otherwise `Repelled`. |

On success the station gets a 14400 s raid cooldown (`CanRaid` is false until it ends) and its production pauses 3600 s. Against an NPC owner the raider also takes 50% of stored materials (10% when the raider is you) and the owner loses wealth (the money a faction spends on warships and stations; the Faction overview shows it as "Economic Power"). Against one of your stations, no stock or wealth is taken; the station's production still stops. Details in [faction-economy](faction-economy.md).

Source: `StationRaid.GetRaidProfile`, `StationRaid.ApplyRaid`, `BattleManager.TryEndRaid`, `BattleManager.ResolveRaidAtCap`, `BattleManager.IsBreakingOff`, `BattleManager.StartRaid`

## Capture, abandonment and claiming

### Capture by siege

When a hub siege ends in `Captured`, `ApplyDecisiveStationCapture` runs in this order:

1. Every non-Empire defending fleet is wiped. If the loser is you, ships flee home.
2. Every Defense station of the old owner is destroyed (it becomes a wreck).
3. The system's owner becomes the attacker. The hub and economic outposts of the old owner flip to the new owner. War effort in the system clears.
4. `SystemCaptureCleanup` cancels cargo routes that now cross owners, removes the old owner's pending station constructions and queued warship constructions for yards in that system, and empties work bays.
5. The headquarters flag is removed, control level is set to 1, and the settling timer starts.
6. Your Empire Fleets that defended the system fall back (see below).
7. The old owner is checked: a faction with no systems left starts a 5-update cooldown (counted in conquest updates) before it can rejoin the war.

If the old owner was you, `PlayerStationCapture.HandOver` converts your stations to the captor's. 75% of stored materials go to a refund station at your capital (or home station), and the rest to the captor.

If you capture, your reputation with the former owner is set to the Hostile threshold (-5000) if it was higher. The in-game takeover dialog notes that a Decoy Transponder does not hide this. Captured economic outposts of the old owner become yours one tier lower (minimum 1), with 10% of their stock, and the system gets 2 slots for your own economic outposts. The first system you hold becomes your capital.

Source: `BattleManager.ApplyDecisiveStationCapture`, `BattleManager.DestroyDefenders`, `SystemCaptureCleanup.Apply`, `ConquestOwnership`, `SystemMapData.SetConquestFaction`, `SystemClaim.OnOwnerChanged`, `SystemClaim.ApplyReputationRule`, `PlayerStationCapture`, `Conquest.OnSystemLost`

### Settling and unrest

The game has no name for the two states below; "unrest" and "settling" are plain words used on this page. The conquest update runs every 3600 s (`Conquest.DoConquestTick`). Capture and claim set `settlingTicks` to 6, and each conquest update lowers it by 1.

| State | Condition | Effect |
|---|---|---|
| Unrest | `settlingTicks` above 4: the first 2 conquest updates | The system pays no taxes, the wealth a faction earns every hour from the systems it owns (the "Taxes" ledger line; see [faction-economy](faction-economy.md)). |
| Settling | `settlingTicks` above 0 | The Defense station profile is x0.5 (hull and firepower). |

Because the first conquest update can come at any time up to 3600 s after the capture, settling lasts between 5 and 6 hours of game time, and unrest between 1 and 2.

Source: `ConquestSystem.StartSettling`, `ConquestSystem.isInUnrest`, `Conquest.TickSettling`, `StationDefense.GetProfile`, `FactionEconomicPlanner`, `ConquestWarTuning.SettlingTicks`, `ConquestWarTuning.SettlingDefenseMultiplier`

### Unowned and abandoned systems

Destroying the conquest hub of an owned war-zone system with a live fight (you flying at the station) calls `ConquestOwnership.Abandon`. A simulated siege that ends in `Captured` does not go through this path. The system becomes unowned, control and war effort reset, and the headquarters flag drops. The code does not check whether the system is a headquarters here, so destroying a headquarters hub abandons it `(not verified in play)`. The code then does one of two things:

- In your player sector, the system returns to sale, and any battle there ends without a capture.
- Anywhere else, battles there are ended, and the claim goes to you if you can claim (see below), otherwise to the fleet that was besieging, if it can conquer.

A fleet that arrives at an unowned system claims it (`ConquestOwnership.Claim`) unless the system is waiting on a claim offer, is for sale in the player sector, or the fleet is yours and the system is free territory. If the system was abandoned by a different faction, that faction's Defense stations in it are destroyed first.

You can claim an unowned war-zone system yourself when: claiming is unlocked (the "Beyond the Front" milestone), the system is open to you, it is not a player-sector system or free territory, and your flagship or an idle Empire Fleet is in the system. An abandoned system you claim drops your reputation with the faction that abandoned it to Hostile, as a capture does.

Source: `ConquestOwnership.Abandon`, `ConquestOwnership.HandOverToClaimant`, `ConquestOwnership.Claim`, `BattleManager.OnFleetArrived`, `BattleManager.IsClaimableOnArrival`, `SystemClaim.GetRefusal`, `SystemClaim.OfferClaim`, `ConquestOwnership.OnStationDestroyed`

## How NPC factions pick fights

### Cadence

| What | Cadence |
|---|---|
| Conquest update (the "Conquest Update" card on the galaxy map): control level, settling, umbral spread, headquarters check | 3600 s |
| Hourly economy tick (not shown in the game): queues builds, assigns fleet orders (`ConquestFleetPlanner.Tick`) | 3600 s |
| Military planning between the hourly steps (`PlanBetweenTicks`) | every 600 s, plus immediately after any battle ends for each faction involved |
| Distress response check | 60 s, only while a battle is active |
| Empire Fleet order check | 5 s |
| Fleet travel time | 60 s + 150 s per jump |

NPC fleets target 2000 points (`FleetTargetPoints`). How many fleets a faction fields is in [faction-economy](faction-economy.md).

Source: `Conquest.DoConquestTick`, `LivingUniverse.TickMilitaryPlanning`, `LivingUniverse.RequestMilitaryPlanning`, `LivingUniverse.TickDistressResponses`, `ConquestFleetPlanner.TravelDuration`, `ConquestFleetPlanner.Tick`

### Frontline score

A faction's frontline is every war-zone system it owns that has at least one contested neighbor: a neighbor owned by a hostile owner and open to the faction, or (for non-raiders) an unowned war-zone neighbor. Each frontline system gets:

```
score = maxNeighborDefense + 0.6 * max(0, 2000 - minNeighborDefense) - 250 * coveringFleets
```

`maxNeighborDefense` and `minNeighborDefense` are the highest and lowest Defense numbers among contested neighbors. `coveringFleets` counts the faction's fleets in that system plus fleets traveling to it. A high score means a strong enemy neighbor, or a weak one worth taking, and few fleets assigned. Fleets rally to the highest score; every fleet already rallying there lowers it by 250 for the next. Hostile means any faction other than the owner or a war ally; against you, a faction must be your enemy (at war with you, or your reputation with it below -500).

Source: `ConquestFrontline.TryBuildFrontlineSystem`, `ConquestFrontline.GetContestedNeighbors`, `ConquestFrontline.IsHostileOwner`, `ConquestFleetOrders.ChooseRallyPoint`

### Choosing a target

Idle fleets that are not defending on site, in this order:

1. Retreat check (below).
2. Refit, if due and a yard is in reach (max 3 refits at once per faction).
3. Distress response.
4. Attack: with a frontline, the contested neighbors of frontline systems, up to 4 jumps away. Without one, any open, targetable war-zone system that is unowned or owned by a hostile faction (raiders skip unowned ones), up to 60 jumps away.
5. Assault planning with other fleets.
6. Raid.
7. Rally to the frontline, then guard.

A fleet attacks alone only if `totalPoints >= 1.15 * GetNextBattleStrength(target)`, that is, 1.15 times the target's next-battle strength. Among valid targets it prefers:

1. A target it is already standing in.
2. The owner with the most war-zone systems (unowned systems rank above any owner).
3. The lowest next-battle strength.
4. The fewest jumps.

Targets already claimed by another of the faction's fleets are skipped, as is a system the fleet was blocked from re-attacking for 3600 s. A faction holding under 10% of war-zone systems only considers unowned targets or owners that hold at least 10%, unless that leaves nothing.

Source: `ConquestFleetOrders.AssignOrders`, `ConquestFleetOrders.AssignFrontlineOrders`, `ConquestFleetOrders.TryAttack`, `ConquestFleetOrders.AttackTarget`, `ConquestFleetOrders.ApplyPeace`, `ConquestWarTuning.PeaceShareThreshold`, `ConquestFleet.IsReattackBlocked`

### Assaults

When no single fleet can beat a target, `PlanAssault` looks for 2 or more ready fleets whose combined points reach 1.15 x the target's next-battle strength. An assault is several of one faction's fleets meeting next to a target and merging to attack it.

1. The staging system is the first war-zone neighbor of the target owned by the faction (neighbor order is shuffled every conquest update).
2. The nearest ready fleets are chosen until the sum reaches the threshold; at least 2 are needed. They travel to staging with the order shown as "Gathering for an assault" (`Assembling`).
3. Once all are at staging, the sum is checked again. If it fell below the threshold, the assault disbands. Otherwise the fleets merge into one (the first fleet takes all ships and target points) and attack with the "Attacking" order.
4. If the staging system is lost, or a member stops being able to gather, the assault disbands.
5. A merged fleet above one fleet slot is split back into 2000-point fleets when idle.

A faction runs one assault at a time.

Source: `ConquestFleetOrders.PlanAssault`, `ConquestFleetOrders.TryPlanAssault`, `ConquestFleetOrders.AdvanceAssaults`, `ConquestFleetOrders.AdvanceAssault`, `ConquestFleetOrders.Launch`, `ConquestFleetOrders.SplitTaskForces`, `ConquestWarTuning.MinAssaultFleets`

### NPC raids

A fleet with no attack or assault gets a raid order if it is not below the retreat threshold. It picks the nearest (up to 4 jumps) raidable economic outpost among those owned by the factions on its frontline candidate list (plus you, if the faction is your enemy). An economic outpost qualifies if it is in the war zone, open to the raider, not a Defense station, not on raid cooldown, not already under raid or targeted, and has no defending fleet or patrol. Raiding fleets show the "Raiding an outpost" label.

Frontier raid parties are separate. Every hour, each Frontier sector with `systems / 5` slots tries to fill each free slot with a 25% roll. A party is 200 to 300 points, has 2 raids, and lives 3 hours; it leaves when its raids or time are gone, or when it drops below 50% of its starting points. Corsair Syndicate parties start from the nearest Corsair system. The two highest-level Frontier sectors can also spawn Meridia's Chosen parties. Targets are stations of Kolyatov Collective, Stellar Industries and Luminate Combine in the same sector (plus yours if the raider is your enemy), scored `stockValue / (1 + jumps)^2`. Canisec patrols (800 points) defend raided stations in Frontier sectors.

Source: `ConquestFleetOrders.TryOrderRaid`, `ConquestFleetOrders.ChooseRaidTarget`, `FrontierRaids.Tick`, `FrontierRaids.TrySpawnInSector`, `FrontierRaids.PlanParty`, `FrontierRaids.GetTargetScore`, `StationRaid.CanRaid`, `ConquestWarTuning.RaidPartyPointsMin`, `ConquestWarTuning.RaidPartyPointsMax`, `ConquestWarTuning.RaidsPerParty`, `ConquestWarTuning.RaidPartyLifetimeHours`, `ConquestWarTuning.RaidSpawnChance`, `CanisecPatrols`

### Retreats, recovery and distress

A fleet needs to retreat when it is below 35% of its target points. The check applies to idle fleets; a fleet defending a battle gets one 25% roll per battle to leave, and fleets with no roll left stay. The retreat destination is the nearest yard system within 4 jumps, otherwise the nearest safe owned war-zone system that is not on the frontline, otherwise any safe owned system. A retreating fleet is in state Retreating (shown as "Reinforcing") until it is back to 75% of its target points or it is waiting outside the war zone with no owned war-zone system within 4 jumps. Resting fleets repair 100 points of carried damage per hour; lost ships are rebuilt at yards.

A besieged system's owner can send a responder: the nearest available fleet that can arrive before the battle's remaining time runs out (`(timeRemaining - 60) / 150` jumps, max 60). That fleet shows "Responding to a distress call" and arrives as a defender. For raids, the owner's fleets answer too; war-zone fleets answer only if the outpost is in the war zone.

Source: `ConquestFleetOrders.TryRetreat`, `ConquestFleetOrders.NeedsToRetreat`, `ConquestFleetOrders.ChooseRetreatSite`, `ConquestFleetOrders.WinsDefenderRetreatRoll`, `ConquestFleetOrders.RefreshStaleOrder`, `ConquestFleetOrders.AssignDistressResponders`, `ConquestFleetOrders.MaxHopsWithin`, `LivingUniverse.RepairRestingFleets`, `ConquestWarTuning.RetreatThreshold`, `ConquestWarTuning.RetreatRecoveredThreshold`, `ConquestWarTuning.DefenderRetreatChance`

## Fleet labels on the map

`ConquestFleetMapIcon.GetTaskLabel` picks the first matching line:

| Order | Label | When |
|---|---|---|
| 1 | Refitting - {time} left | State Refitting (300 s per refit) |
| 2 | Flying to a construction yard to refit | Traveling with reason Refitting |
| 3 | Moving | Traveling with reason Loitering: an Empire Fleet Move order, or a raid party heading home |
| 4 | Supporting {fleet} at {system} | The fleet joined another fleet's battle as a supporter |
| 5 | Joining the siege of {system} | Your fleet is traveling on a Join siege order |
| 6 | Defending an outpost from raiders | Idle fleet in a system where its owner's economic outpost is being raided |
| 7 | Defending the system | Idle fleet in a system where its owner is besieged |
| 8 | By reason (below) | Otherwise |

Reason labels:

| Label | Reason | Set when |
|---|---|---|
| Attacking | `Attacking` | Sent to take a system (NPC `TryAttack`, an assault launching, your Attack order), or a supporter joining a battle. |
| Reinforcing | `Reinforcing` | An idle fleet that is not on the frontline is sent to the best-scoring frontline system; also every retreating fleet. Warship deliveries use it as well. |
| Responding to a distress call | `RespondingToDistress` | Sent to a besieged or raided system the faction owns; your Sector-stance fleets going to a system under attack in their sector; a Join siege order that defends. |
| Holding position | `Loitering` | Fleet is idle already on a frontline system, and the default after any battle, finished refit, or cancelled order. |
| Guarding faction space | `Guarding` | Fleets left over after rally. They spread over the faction's station systems, fewest covering fleets first, then nearest. |
| Gathering for an assault | `Assembling` | Fleets heading to or waiting at the staging system of an assault. |
| Refitting at a construction yard | `Refitting` | Reason shown outside the Refitting state. |
| Raiding an outpost | `Raiding` | Sent to raid, including raid parties. |

An NPC fleet that is Retreating still shows "Reinforcing", because the label comes from the reason and not the state.

Your own fleets' map callout uses a different set: "Moving to {system}", "Holding at {system}", "Returning to {system}", "Attacking {system}", "Raiding {station}", "Refitting at {system}", "Joining the siege of {system}", and for stance "Defending {system}", "Defending sector {sector}" or "Not defending".

Source: `ConquestFleetMapIcon.GetTaskLabel`, `ConquestFleetOrders.Rally`, `ConquestFleetOrders.Guard`, `ConquestFleetOrders.AdvanceAssault`, `ConquestFleetOrders.StartAssault`, `BattleManager.JoinAsSupporter`, `MapFleetCallout`

## Your Empire Fleets

### Slots and size

You get fleet slots by war-zone systems held, and each fleet's size depends on your level. A fleet slot is room for one Empire Fleet; the game shows "{n} / {m} fleet slots".

| Systems held | 1 | 5 | 10 | 20 | 40 | 80 | 160 |
|---|---|---|---|---|---|---|---|
| Fleet slots | 1 | 2 | 3 | 4 | 5 | 6 | 7 |

Fleets unlock at player level 10. The budget steps every 5 levels:

| Level | 10 | 15 | 20 | 25 | 30 | 35 | 40 | 45 | 50 | 55 |
|---|---|---|---|---|---|---|---|---|---|---|
| Points | 100 | 200 | 350 | 500 | 700 | 900 | 1150 | 1450 | 1800 | 2150 |
| Ships | 2 | 3 | 4 | 5 | 6 | 6 | 7 | 8 | 9 | 10 |

| Level | 60 | 65 | 70 | 75 | 80 | 85 | 90 | 95 | 100 |
|---|---|---|---|---|---|---|---|---|---|
| Points | 2500 | 2700 | 2875 | 3050 | 3250 | 3450 | 3625 | 3800 | 4000 |
| Ships | 12 | 14 | 15 | 17 | 18 | 20 | 21 | 23 | 25 |

Source: `EmpireFleets.FleetSlotSystems`, `EmpireFleets.MaxFleets`, `FleetLevelSteps`

### Orders

| Order | Name in the game | Rules |
|---|---|---|
| `Hold` | Move | Travel to a system and stay. |
| `Attack` | Attack | War-zone system only; not your own, not a headquarters, not hubless, not a for-sale player-sector system, not free territory, not already fought over by a non-friendly faction, and its owner must be your enemy (or you must declare war). Arriving at an unowned system claims it. |
| `Raid` | Raid | Economic outpost (not a Defense station) in the war zone owned by a hostile faction. |
| `Return` | Return | Home system, or your nearest held system if home is lost. |
| `Refit` | Refit | Nearest Construction Yard of yours. Refused if it would not raise the fleet's level. |
| `JoinSiege` | Join siege | Needs a fight in that system with exactly one friendly side. |

The range is 30 jumps. You cannot order a fleet that is the defender in a `FleetClash`, and you cannot remove ships from a fleet that is in a battle. Attacking an ally or neutral lets you declare war from the order dialog. Giving a new order breaks off an attack in progress (`Withdrew`).

Join siege lets your fleet help whichever side is yours or allied: if the owner is friendly and the attacker is not, the fleet defends (and counts in the system's Defense); if the attacker is friendly and the owner is not, it joins as an attacker supporter. Both sides friendly, or neither, is refused.

Source: `EmpireFleets.TryOrder`, `EmpireFleets.GetRefusal`, `EmpireFleets.RefuseAttack`, `EmpireFleets.RefuseRaid`, `EmpireFleets.RefuseJoinSiege`, `EmpireFleets.GetJoinSide`, `EmpireFleets.DefendsAlly`, `EmpireFleets.MaxOrderHops`

### Defense stances and other behavior

| Stance | Behavior |
|---|---|
| None | Fleet does not defend (counts only while on a Join siege order). |
| Defend system (the default) | While idle, joins the defense of the system it is in. |
| Defend sector | Also answers sieges and raids on your systems anywhere in the same sector as its post, traveling there with the Responding label. |

Clicking the active stance again returns it to None. An idle fleet counts toward Defense if it has a stance or a Join siege order, is in the system, and is not traveling. If a system with your defending fleets is captured, those fleets fall back to their home system (a Sector-stance fleet whose post is still held stays).

The fleet retreat threshold (`retreatFraction`) defaults to 0 and the setting is hidden (`RetreatSettingShown` false), so your fleets never break off on their own. A fleet only retreats if that value is changed. Your Empire Fleet ships and patrol ships are real ships: when they die in a simulated or live battle they are removed permanently.

The fleet mix bonus appears on the fleet card as "Fleet mix +x% off-screen strength" and applies to simulated rounds and to the Defense number, not to live combat.

Source: `EmpireFleets.JoinsDefense`, `EmpireFleets.GuardSector`, `EmpireFleets.FallBack`, `EmpireFleets.CheckRetreat`, `EmpireFleetCommands`, `FleetMixBonus`

### Outmatched warning

When you give an Attack order and the fleet's points are below 1.15 x the target's next-battle strength (the threshold NPC fleet planning uses), the game warns: "{fleet} ({points} points) is outmatched here ({defense} defense) and will likely be wiped out." and offers "Attack anyway". The number in the warning is the next-battle strength, which can be lower than the full number in the Defense row when the system has Defense stations.

Source: `EmpireFleets.GetOutmatchingDefense`, `EmpireFleetCommands`

### Reputation

| Action | Reputation effect |
|---|---|
| Capturing a system from a faction, or claiming one it abandoned | Reputation with the former owner drops to Hostile (-5000) if above it. |
| Claiming an unowned system nobody abandoned | No drop. |
| Destroying a faction station while you hit it (live) | -5000 with its faction, or -1000 if your reputation with it is already at or below -5000 (Hostile or worse). No loss if `hasUmbralTransponder`. Hubs are not faction stations: killing a hub costs only the per-part penalty (see [stations](stations.md)). |
| Declaring war | Treated as enemy everywhere. |

A station destroyed only by the simulation does not cost you reputation.

Source: `SystemClaim.ApplyReputationRule`, `FactionStationDestruction.ApplyDestroyReputation`, `FactionStationDestruction.GetDestroyReputationLoss`, `CombatStationPart.OnFactionStationDestroyed`, `EmpireFleets.DeclareWar`

## The 30% sector-control mission

Three story missions ask you to push one faction to 30% of the systems in the Conquest sector: "Hostile Takeover" (Stellar Industries, `ConquestStellar2`), "The People's Sector" (Kolyatov Collective, `ConquestKolyatov2`) and "Harmony" (Luminate Combine, `ConquestLuminate2`). Finishing any one of them is required before "Beyond the Front" is offered (see [the gates](#what-level-60-and-beyond-the-front-gate)). You cannot hold systems yourself at this point, so your play changes the outcome only by nudging a war that NPC (non-player character) factions fight on their own. This section covers how to get the mission, what its objective counts, where in the game you can read progress, how the war behaves without you, what moves the share, and a plan.

> In short
> 30% means your chosen faction owns 30% of the systems in the Conquest sector: 21 to 26 systems, depending on how many the galaxy has (70 to 84).
> Progress is the "Area Conquered" row for that faction (press E on the galaxy map, open the Factions tab, click the faction), and the mission's own objective line "Conquer for {faction} {current}/30.00%".
> The number refreshes once an hour, when the "Conquest Update" countdown on the galaxy map runs out. A capture between refreshes shows up late.
> To win, let the faction's own fleets take the easy systems, help on the hard ones with war effort missions or by destroying a hub next to your faction's idle fleets, and be standing at the faction's commander in its headquarters when the number reads 30% or more.

Some terms before the details. The war zone is every system where factions fight over ownership: the Conquest sector (Ara Martis, the original war zone) and, once unlocked, the expansion sectors. Only the Conquest sector counts for this mission. Every war-zone system has a hub, its main station (the map calls it "Space Station"), which carries the owner's flag; destroying the hub abandons the system. A faction's headquarters is the one system it cannot lose to a fleet; its hub has the faction's commander and shop. An ordinary owned system that fleets can besiege and capture is an outpost system.

### Getting the mission

1. `ConquestThreeFactions` ("Sector Control") asks you to find the three embassies (each faction's neutral station in the Staging sector, outside the war zone) and talk to their representatives. Its description says "The choice of faction is not permanent".
2. Talking to a representative offers to align you with that faction. The offer is refused if the faction is your enemy, and refused while the faction has no headquarters. The game counts a faction as your enemy when you are at war with it or your reputation with it is below -500. Reputation is a number that the game shows as a level name; below -500 means the Wary level (except exactly -500) or any worse level, while Neutral covers -499 to 1,499 (all bands are in the [glossary](glossary.md#reputation)). Otherwise you are asked to confirm and the faction's first mission (`ConquestStellar1` "Internship", `ConquestKolyatov1` "For the Collective!", `ConquestLuminate1` "Alignment") is added.
3. The first mission has four steps: fly to the faction's embassy gate, go to its headquarters and talk to the commander, earn 10 contribution points by running missions for that faction ("Run missions for {faction}"), and talk to the commander again. The 10 points are a counter for this one step. They are not "Your Contribution" on the Faction overview (your conquest standing with the faction, which sets your conquest rank), and not the wealth your embassy missions bring. Each completed Conquest mission from that faction's hub or embassy (a mission on the mission board of a war-zone hub that raises war effort, or of an embassy that gives wealth) adds its contribution amount: 3 to 6 (1 to 3 for ECHO's idle missions, the ones ECHO, your ship's AI companion, completes while you are away), doubled when the mission's enemy is a conquest faction other than the Corsair Syndicate and Meridia's Chosen, plus a bonus from your conquest rank with that faction (`GetFleetStrengthRewardBonus`). That is usually 2 to 4 missions. The mission pays 2,000,000 credits, 60 Vanguard Marks (the currency used to buy ships and systems), XP (experience points), the second mission and the Umbral line.
4. The second mission (`ConquestStellar2` and its siblings) has the percentage step and the report-back step. It cannot be abandoned and has no time limit. It pays 400 Vanguard Marks, 10,000,000 credits, 1 skill point, XP, and starts the next story mission (`ConquestDarkspace1`, which I did not find defined in the code).

You can start the chain for more than one faction. Standings are tracked for all conquest factions all the time, and `HasCompletedSectorControl` accepts any of the three second missions. So the cheapest route is whichever of your factions the war favors. The percentage step is complete the moment the mission starts if the faction is already at 30%.

Reputation matters only for the alignment check and for docking at the headquarters. No other reputation threshold gates the mission.

Source: `ConquestMissions.FactionAlignmentChoice`, `ConquestMissions.CreateFactionHQIntroMission`, `ConquestMissions.CreateFactionConquestProgressMission`, `ConquestMissions.HasCompletedSectorControl`, `Characters.AlignWithMajorFaction`, `ConquestFleetStrength`, `ConquestStrength.OnComplete`, `MissionGenerator.AddRewards`, `FactionData.IsEnemy`

### What the objective counts

```
percentage = (conquest systems owned by the mission's faction) / (all conquest systems)
complete   = percentage >= 0.3
```

| Question | Answer |
|---|---|
| Numerator | Systems whose owner is exactly that faction (`sys.faction == faction`). Its headquarters counts. Allies do not count, other factions do not count, and a system with no owner does not count. You cannot hold a system yourself before claiming unlocks, and a system you held would not count for the faction anyway. |
| Denominator | Every war-zone system (`ConquestSystem`) in the Ara Martis conquest sectors, owned or not: unowned and abandoned systems, and every rival's headquarters, are in the total. The Darkspace region (Penumbra and its neighbors), the staging sectors and the Frontier are not. The expansion sectors are added only once the expansion is unlocked, which is after this mission. |
| How many systems that is | The Ara Martis area is seven draws of 10 to 12 systems (`CreateContinuousAraMartis`), so 70 to 84 systems in a normal galaxy, fixed when the game is created (derived; the exact total is not shown anywhere I found). |
| When it is computed | The game stores the percentage (`currentConqueredPercentage`) for every conquest faction at the start of every conquest update, the hourly step (3600 s of game time) whose countdown the galaxy map shows as the "Conquest Update" card. It is also stored when a system is abandoned (`Conquest.UpdateConquestArea`). A capture by siege does not refresh it. So the stored value, and the objective, lag the map by up to an hour. |
| What the interface shows | The objective reads "Conquer for {faction} {current}/{30.00%}", where current is the stored value with 2 decimals. The "Area Conquered" row on the Faction overview for any conquest faction is the same stored value. |
| Can it go backwards | Yes. Every conquest update recomputes it from the current owners. |
| Do you need to hold 30% | Only at the moment you talk to the commander. No latch exists on the objective. Mission steps are checked in order, and the first incomplete one is the current step. The final step ("Go to the {faction} HQ and talk to {commander}"; HQ is the game's short form of headquarters) only receives your talk event while the percentage step is complete. So the stored value must be at 30% or more when you talk to the commander in the headquarters. After that the mission completes and later drops do not matter. A value that reached 30% at one conquest update and is gone by the next is enough if you are already at the commander. |

Systems needed (derived from `count / total >= 0.3`):

| Total systems | 70 | 71 to 73 | 74 to 76 | 77 to 80 | 81 to 83 | 84 |
|---|---|---|---|---|---|---|
| Systems the faction must own | 21 | 22 | 23 | 24 | 25 | 26 |

To find your total, count the faction's systems in the Conquest sector on the galaxy map and divide by its Area Conquered percentage.

Source: `SystemsConquered.IsComplete`, `SystemsConquered.statusText`, `Conquest.UpdateConquestArea`, `Conquest.UpdateFactionStandings`, `Conquest.DoConquestTick`, `Conquest.RefreshStandingsAndGates`, `Conquest.PopulateSystems`, `Conquest.IsWarZoneSector`, `ConquestWorld.CreateContinuousAraMartis`, `Mission.currentStep`, `MissionObjective.Trigger`, `FactionOverviewContent.AddConquestRows`

### Where to read progress

Everything the plan below asks you to check is on screen somewhere. Where to look:

- The Faction overview. On the galaxy map press E (or click the Empire button in the map's corner panel, tooltip "Empire (E)"; the key does nothing while you type in a text field, and the window is locked during the prologue). The window is titled "Empire". Open its Factions tab, then click a faction in the list. For a conquest faction the detail shows three rows: Headquarters (the system's name), Area Conquered and Your Contribution. The Fleets tab of the same window lists your own Empire Fleets. The Log tab is the Empire Log, your record of battles, captures and claims (it says "system captured", "attack repelled" and so on, see "Outcomes" above).
- The Conquest Update card. In the corner panel of the galaxy map, a card titled "Conquest Update" has a countdown and a bar that drains over 3600 s. Its tooltip reads "Damaged stations are rebuilt and factions rethink their strategy". When the countdown ends, the conquest update runs and Area Conquered refreshes.
- A system's tooltip. Hover a war-zone system on the galaxy map. Its conquest section has the Defense row, the "Your war effort" row and, if present, an "Umbral Reach control" row. The Defense row (the Defense number) is the strength the defenders put into a siege round, counted in points (the game's fleet cards call the same unit "strength"). It adds up the hub's firepower, the garrisons (warships stationed in them) of the system's Defense stations (extra stations that fire on enemies and hold a garrison), and the fleets defending the system. War effort is a temporary multiplier on a system's station firepower that Conquest missions you complete raise, lasting 3 hours from the last one; the row is green for a bonus and red for a penalty and shows the time left.
- Next-battle strength. The game never shows this figure, which is what NPC fleets compare themselves against (see [what NPC factions use instead of the Defense number](#what-npc-factions-use-instead-of-the-defense-number)). You can estimate it from what you do see: with no Defense station in the system it equals the Defense row (plus any same-faction fleets already on the way); with Defense stations it is lower, the weakest Defense station's own firepower plus its garrison plus all defending fleets (plus fleets on the way). So the Defense row is the upper bound.
- Fleet icons and their labels. Fleets show as icons on the galaxy map; each has a label for its current order, for example "Attacking", "Gathering for an assault" or "Holding position". All labels are listed in [fleet labels on the map](#fleet-labels-on-the-map).
- The hub tooltip. A hub under siege shows "Under Attack! Holds {n} min at most" when you hover it.
- The mission board. Each Conquest mission lists its reward line, for example "+10% Station firepower, -10% for X next system". The part after the comma names the faction whose neighboring systems get the war effort penalty.

Source: `MapCornerPanel.BuildConquestCard` (strings `MapConquestUpdateTitle`, `MapConquestUpdateTooltip`), `MapCornerPanel.HandleHotkeys`, `EmpireWindow` (tabs, string `FactionOverviewTab` = "Factions", `EmpireTabLog`), `CommandTabUnlocks.IsEmpireWindowUnlocked`, `FactionOverviewContent.AddConquestRows`, `MapTooltipCardBuilder.BuildConquestSection`, `ConquestFleetMapIcon.GetTaskLabel`, `ConquestStationUnderAttack`, `MissionGenerator.AddRewards`

### What the war does without you

Start of the game. Kolyatov, Stellar and Luminate each get 3 systems, Meridia's Chosen get 3, and the Darkspace Compact gets everything else, because its starting claim limit is unlimited while the others stop at 3 (`ConquestWarSeeder.StartingClaimLimit`). With 70 to 84 systems that is about 58 to 72 Darkspace systems and 3.6% to 4.3% for each major (derived from the partition rules; the map shows the real start). Every war faction starts with fleets filled to its fleet cap, the number of fleets it may field (each one fills a fleet slot): 5 fleets of 2000 points, since the cap is `5 + floor(systems / 15)` up to 8 and Darkspace is fixed at 5 before the expansion.

What limits an NPC capture:

- Which target: unowned neighbors first, then the owner with the most war-zone systems (Darkspace early on), then the lowest next-battle strength, then the fewest jumps. A lone fleet attacks only at 1.15 times next-battle strength or better, so a 2000-point fleet can take anything with next-battle strength up to about 1739. Above that the faction needs an assault, several of its fleets meeting next to the target and merging to attack it: at least 2 ready fleets, one assault at a time per faction (see "Assaults").
- How long: a clash (the game's Fleet Battle) against a resident fleet runs up to 600 s, each Defense station is its own siege stage of at least 600 s, and the hub stage is at least 600 s. A 2000-point fleet breaks a medium hub's structure in about 10 rounds, so an undefended hub falls at that 600 s minimum. Travel is 60 s plus 150 s per jump. A stalled siege blocks that fleet from the same system for 3600 s. Nothing in the code caps captures per hour, so an unopposed fleet can in principle finish one every 11 minutes or so plus travel (derived).
- Planning cadence: orders are reassigned every 600 s and immediately after any battle ends.
- What a resident fleet does: a 2000-point fleet in a system pushes its next-battle strength above the single-fleet limit, so those systems wait for an assault.
- The peace rule: a faction holding under 10% of the war zone (fewer than 7 to 9 systems) only targets unowned systems or owners at 10% or more. At the start none of the three majors is at 10%, so they ignore each other and only go after Darkspace and unowned systems. Once your faction passes 10%, any major still below it treats you as a legal target, though Darkspace stays the preferred one while it is larger.

What helps a faction that is behind (see [faction-economy](faction-economy.md#catch-up-mechanics)). Wealth is the money a faction spends on warships and stations; the Faction overview shows it as "Economic Power". The home subsidy is wealth handed to Kolyatov, Stellar, Luminate and Darkspace every hour, larger the smaller their share of the war zone (the "Home subsidy" line of the faction ledger; it is not the taxes a faction earns from its systems). The Great Gate reinforcement is a 1500-point fleet that appears from the Great Gate (a map location) for the three majors when they have few fleets.

| Share | Home subsidy per hour (Kolyatov, Stellar, Luminate, Darkspace) | Great Gate reinforcement |
|---|---|---|
| Start (3.6% to 4.3%) | about 104,600 to 107,100 | Free, if it has 2 fleets or fewer |
| 10% | 84,000 | Paid from wealth, if 2 fleets or fewer |
| 20% | 48,000 | Paid |
| 30% | 12,000 | Paid |
| 33.3% or more | 0 | Paid |

The Great Gate reinforcement is 1500 points and appears when the faction has fewer than half its fleet cap (2 or fewer of 5), at most once every 12 hourly economy ticks (the separate hourly step that pays taxes and the home subsidy and plans builds; the game does not show it). Darkspace gets the home subsidy but no Great Gate reinforcements. A faction that is wiped to no systems sits out 5 conquest updates, then returns through the beachhead rule. Every build is half price and three times faster while a faction has fewer fleet slots than its cap.

Income gives the scale. A war-zone system pays 5,000 to 15,000 per hour in taxes (the "Taxes" ledger line), doubled in the war zone, and none while in unrest (the first 1 to 2 hours after a system changes owner). Three systems earn about 60,000 per hour on top of the home subsidy (derived).

Source: `ConquestWarSeeder.PartitionTerritory`, `ConquestWarSeeder.StartingClaimLimit`, `ConquestWarSeeder.SeedFleets`, `Conquest.AssignInitialSystems`, `ConquestFleetOrders.TryAttack`, `ConquestFleetOrders.ApplyPeace`, `ConquestFleetPlanner.MaxFleetsFor`, `LivingUniverse.TickMilitaryPlanning`, `GreatGateReinforcement.GetSubsidy`, `GreatGateReinforcement.NeedsReinforcement`, `GreatGateReinforcement.TryPay`, `ConquestFrontline.GetBeachheadCandidates`, `FactionEconomicPlanner.RollSystemWealth`

### What moves your faction's share

The frontline is a faction's systems that touch an enemy or unowned war-zone system; that is where its fleets push. In the table, "non-idle" missions are the ones you fly yourself, not the ones ECHO completes while you are away.

| Lever | What it changes | Cost and limits |
|---|---|---|
| Lower the next-battle strength of a hard target | Every point of next-battle strength you remove cuts what attackers must bring by 1.15. Killing a resident 2000-point fleet takes a target from assault-only to a one-fleet job. Killing garrison ships at its Defense station removes up to 900 (270 to 900 by tier). | Real combat against ships at about your level plus 10, near stations with x20 hit points (HP). Fleets can be intercepted from the map (`ConquestFleetInterceptTarget`). |
| War effort penalty on the target | Removes 125 to 350 from the target's station term at the cap, which is 145 to 400 attacker points. | 5 non-idle missions inside 3 hours, at your faction's hub in a system next to the target. The mission must name the target faction as its enemy (reward line "-10% for X next system"). |
| Kill the hub, not the Defense stations | The hub goes down, the system is abandoned, and the claim goes to a fleet of your faction (see below). On the claim `ConquestOwnership.Claim` also razes the old owner's Defense stations, so you skip them. | Hub parts are x20 HP. Needs the owner to be your enemy already (you cannot force-attack a station; a plain click only targets enemies). Part kills cost 5 reputation each if you are at -500 or lower with the owner, 100 each otherwise. A hub is not a `FactionStation`, so the -5000 station penalty does not apply. |
| Kill the Defense station live while your fleet besieges it | `OnDefenseStationDestroyedLive` ends that stage as `DefenseStationDestroyed` ("Defense station destroyed" in the Empire Log) at once. | -5000 reputation (-1000 if you are already Hostile or worse, at -5000 or lower), plus part kills. |
| War effort bonus at your frontline hubs | Up to +250 to 700 of Defense for the systems that rivals attack. Protects the share you have. | 10 missions at the system inside 3 hours, 20 at a neighbor. |
| Embassy missions | 80,000 to 120,000 wealth each (halved if idle), about 100,000, which is 40,000 to 130,000 for a full 2000-point fleet at full price. | Helps rebuild a lost fleet at once. Does nothing for a faction whose fleets are already full, and does not raise the fleet cap. |
| ECHO idle Conqueror missions | Step 0.1 x (1 + 0.2 x node points) per idle mission ("ECHO The Conqueror" skill). | Idle missions also pay a smaller share of contribution and wealth. |
| Intercepting enemy deliveries | A killed delivery is a ship their wealth already paid for. | -1000 per ship force-attacked and -3500 per kill if your reputation with the faction is Wary or better when it dies; nothing if it is already your enemy. Rarely moves a system count. |

The hub-kill conversion. When a hub dies, `ConquestOwnership.Abandon` runs. Because claiming is locked for you, `EndBattlesAt` hands the system to the lead attacker of a siege in the hub stage (`Captured`, "system captured" in the Empire Log). If the siege is in any other stage, or there is none, the system is unowned. Unowned systems are NPC factions' first choice: they are candidates for every faction's fleets with next-battle strength 0, and a fleet already in the system counts as "continuing". Your faction's idle fleets within 4 jumps will be sent at the next planning pass, within 600 s (immediately if a battle just ended there), arriving 60 s plus 150 s per jump later. So do it where your faction's fleets are idle within 1 or 2 jumps and where no rival borders the system.

Source: `ConquestOwnership.Abandon`, `ConquestOwnership.Claim`, `ConquestOwnership.HandOverToClaimant`, `BattleManager.EndBattlesAt`, `BattleManager.IsClaimableOnArrival`, `ConquestFrontline.GetContestedNeighbors`, `ConquestFleetOrders.TryAttack`, `ConquestFleetOrders.AttackTarget`, `ConquestWarEffort.Apply`, `MissionGenerator.GetConquestEnemyFactions`, `MissionGenerator.AddRewards`, `BattleManager.OnDefenseStationDestroyedLive`, `LootManager.HandleKillReputation`, `InterceptForceAttack.Attack`, `ConquestJoin.DeliverMissionWealth`

### Pitfalls

- A hub kill with no besieger of your faction nearby gives the system to whoever arrives first. Unowned systems are the top target for every NPC faction, so a rival or Darkspace can claim it. Claims by an arriving fleet need no siege and no wait.
- A hub kill during a rival's siege hands the system to that rival if the siege is in its hub stage.
- You cannot shoot a hub of a faction that is not your enemy. Making it an enemy costs reputation first: one force-attacked kill is -1000 and -3500, about -4,500, which is enough to make most factions enemies (under -500). Check the faction's reputation before you commit.
- Reputation with the Darkspace Compact matters later. The "Beyond the Front" questgiver stands at the Darkspace embassy. Whether bad reputation blocks docking there is `(not verified)`.
- Wiping a faction's fleets completely frees fleet slots. Below its cap every build is half price and three times faster. Kolyatov, Stellar and Luminate also get a 1500-point Great Gate reinforcement when they are at 2 fleets or fewer (free below 10% share). Darkspace gets only the cheaper rebuild. Killing ships out of a fleet that survives does not free a slot.
- Pushing a faction to 0 systems makes it sit out for 5 conquest updates, after which its fleets beachhead again. It is a delay, not a removal.
- When your faction passes 10%, the smaller majors can start attacking it. When it is larger than Darkspace, every NPC faction prefers it as a target, because the owner with the most systems is picked first.
- War effort needs a mission from a hub in a war-zone system. Embassy missions are outside the war zone and give wealth only. Missions at another faction's hub strengthen that faction's stations.
- The 30% value is stored hourly, so a capture an hour before you talk may not show yet. Watch the Conquest Update card.
- Wealth and fleets do not raise the fleet cap. The cap rises only with systems held: 6 at 15, 7 at 30.

### A step-by-step plan

1. Choose the faction. Press E on the galaxy map, open the Factions tab and click each major faction to read its Area Conquered and Headquarters rows. Pick one that is not your enemy, has a headquarters, and has a frontline with Darkspace Compact systems (its systems sit next to theirs on the map). Its second mission needs `ceil(0.3 x total)` systems (21 to 26, see the table above).
2. Finish the first mission: 10 contribution points. Take Conquest missions for that faction from its hub's mission board or from its embassy; both count. Prefer hub missions whose enemy is a conquest faction other than the Corsair Syndicate and Meridia's Chosen: their contribution is doubled. The objective line "Run missions for {faction}" shows your count.
3. Find the frontline on the galaxy map: your faction's systems next to Darkspace systems. Hover or click the fleet icons nearby. Fleets labelled "Attacking" and "Gathering for an assault" show where its next push goes.
4. Hover each neighboring system and read the Defense row. A neighbor with no resident fleet and a small garrison (Defense row under about 1,700) is a one-fleet job. The NPC faction will take it itself within a planning pass or two, so leave it alone. If it has Defense stations, its real next-battle strength is lower than the row.
5. Spend your time on the hard ones. For a neighbor with a resident 2000-point fleet, do 5 war effort missions at your faction's hub next to it within 3 hours, choosing missions on the hub's mission board whose reward line names that faction ("-10% for X next system"). That cuts 145 to 400 attacker points. It does not remove the fleet. The red "Your war effort" row in the target's tooltip shows that it worked and how long it lasts.
6. Hub-kill where your faction's fleets sit. For a hard neighbor next to your faction's idle fleets (icons labelled "Holding position" or "Guarding faction space"), with the owner already your enemy, fly to the system and kill the hub parts (x20 HP, 5 reputation each at -500 or lower). Do not bother with its Defense stations. Stay clear of rival borders. The Empire window's Log tab will show the capture ("system captured") once the faction's fleet arrives.
7. Watch the clock. The conquest update is hourly, and the Conquest Update card on the galaxy map counts it down, so you can time your captures against it. Finish your captures well before the countdown ends, then be docked at the headquarters (the "Gate to {faction} headquarters" at the faction's embassy leads there) and talk to the commander as soon as the Area Conquered row or the objective line shows 30% or more.
8. If your faction stalls below 10%, the Great Gate reinforcement and the home subsidy (about 100,000 per hour) already work for it. Do not try to wipe a rival major's fleets outright: a wiped fleet frees a slot, which halves its build costs and triples build speed, and a major at 2 fleets or fewer gets a 1500-point Great Gate reinforcement.

Not verified: how fast the three majors actually grow on their own (no rate exists in code, only the limits above); whether bad reputation with the Darkspace Compact blocks docking; the points of the "ECHO The Conqueror" node; whether idle ECHO missions choose the aligned faction's hubs; and the total number of systems in your galaxy (derived from the generator, not read from a save).

## Practical

What you can do in conquest depends on two unlocks: the player level cap and the "Beyond the Front" story mission. The first section says what those gates are in code. The two scenario sections after it cover what you can do, what you face and what is efficient on each side of them. Shared figures are at the end.

### What level 60 and "Beyond the Front" gate

Level 60 is a real code gate, but it works through the level cap, not through a check on conquest actions.

| Gate | Condition in code | What it controls |
|---|---|---|
| Level 28 | `StoryMissionSkipToConquest` is available at `player.level >= 28` | The story step that sends you into the Conquest sector (Ara Martis). |
| Level 10 | `FleetLevelSteps.UnlockLevel` | Empire Fleet budgets exist from level 10, but you also need a held system (below). |
| Level cap 60 | `Conquest.maxPlayerLevel` is 60 while `expansionUnlocked` is false, 100 after | You cannot pass level 60 until the expansion is unlocked. |
| "Beyond the Front" offered | `ExpansionMissions.IsGoToExpansionAvailable`: `player.level >= 60`, and one of `ConquestStellar2`, `ConquestKolyatov2` or `ConquestLuminate2` completed (the [30% sector-control missions](#the-30-sector-control-mission)), and the expansion not yet unlocked | The mission only appears once you are at the cap and have finished your faction's 30% sector-control mission. |
| Expansion unlock | Creating the mission (when you accept it from the questgiver) calls `Conquest.UnlockExpansion()` | Generates the expansion sectors, raises the level cap to 100, raises the loot box skill point cap from 10 to 30 and the reputation cap from Distinguished to Exalted, and widens the Darkspace Compact and Meridia's Chosen fleet caps (`Faction.ExpansionWarOpen`). |
| Claim unlock | Completing the mission pays `SystemClaimUnlock`, which sets `systemClaimUnlocked` | Claiming systems, buying player-sector systems and the Empire level tab. Empire Fleets, Defense stations and patrol fleets also need a held system. |

The mission has three travel steps (the Penumbra gate, the player sector gate, the home hub) and pays 1 skill point and +300 reputation with Darkspace Compact. It then starts `StoryMissionBuyFirstSystem`, which asks you to buy the first system.

So "level 60 or below" is simply everyone who has not accepted the mission, and "above level 60" is everyone who has. Between accepting and completing the mission you can already level past 60 and the expansion exists, but claiming is still locked. Your faction's 30% sector-control mission checks `currentConqueredPercentage` (the "Area Conquered" figure: that faction's systems divided by all Conquest sector systems), so before you can leave level 60 the war itself has to go your faction's way. That mission pays 400 Vanguard Marks, 10,000,000 credits and a skill point; how to finish it is in [the 30% sector-control mission](#the-30-sector-control-mission).

Other level 60 references in code, none of which gate conquest actions: `EmpireUpkeep.ReferenceLevel` (the level whose cost multiplier upkeep is divided by), Legendary loot (`LootRarity` adds it only at level 60 or higher, weight `(level - 60) / 2000`, so it is 0 at exactly 60), and the expansion's minimum system level (`ExpansionLevels.SeamLevel` 60). The empire fleet budget steps every 5 levels (2500 points and 12 ships at level 60, see [slots and size](#slots-and-size)); 60 is not special in that table.

Source: `ExpansionMissions.IsGoToExpansionAvailable`, `ExpansionMissions.BuildBeyondTheFront`, `ExpansionDialogues.GetTooEarlyLine`, `Conquest.maxPlayerLevel`, `Conquest.UnlockExpansion`, `Conquest.maxLootBoxSkillPoints`, `Conquest.maxReputation`, `SystemClaimUnlock`, `GamePlayer.UnlockSystemClaim`, `ConquestMissions.CreateFactionConquestProgressMission` (`SystemsConquered` at 0.3), `Conquest.UpdateConquestArea`, `LootRarity.GetRarityChances`, `EmpireUpkeep`, `ExpansionLevels`, `ConquestFleetPlanner.MaxFleetsFor`, `CommandTabUnlocks`

### Scenario 1: level 60 or below, before your first system

#### What you can and cannot do

| You can | Notes |
|---|---|
| Run Conquest missions | Each completed mission from a war-zone station adds war effort (see "War effort"). This needs no system of your own. |
| Let ECHO run idle Conquest missions | Larger step: `0.1 * (1 + 0.2 * points)` in the "ECHO The Conqueror" node. The node's maximum point count is in prefab data and not read here `(not verified)`. |
| Attack stations and ships live | Hubs take x20 HP (x30 at a headquarters); see [stations](stations.md). You can only target a station's parts with a plain click if its faction is already your enemy; force-attacking a `SpaceStation` is not possible. Destroying a faction station (not a hub) while you hit it costs -5000 reputation with that faction, or -1000 if you are already Hostile or worse (-5000 or lower). |
| Intercept fleets and warship deliveries | `ConquestFleetInterceptTarget` builds the fleet's real ships at the intercept zone; losses come off the fleet. If the faction is not your enemy, confirming the force attack costs -1000 reputation for each ship you first target (`InterceptForceAttack.Attack`), and each kill of a force-attacked ship costs another -3500 if your reputation with the faction is still Wary or better when the ship dies. Enemy ships cost nothing. |
| Join a smaller faction's war | A faction with an embassy and no headquarters of its own can be joined once your embassy missions have brought it 1,500,000 wealth (shown as "Wealth your missions brought"), or once you have paid a 10,000,000 credit donation. The player-populating factions are the Mining Guild, Salvage Guild, Corsair Syndicate and Stranded. Missions taken at an embassy pay wealth in. |
| Destroy a hub in a siege | Destroying a hub live ends the fight and abandons the system. With claiming locked, the besieger takes it if it was in the hub stage of a siege; otherwise the system is unowned and the next fleet to arrive claims it (see [the hub-kill conversion](#what-moves-your-factions-share)). |

| You cannot | Why |
|---|---|
| Claim, buy or hold a system | `SystemClaim.GetRefusal` and `PlayerSectorPurchase.GetRefusal` return "Claiming systems unlocks with Beyond the Front". |
| Field Empire Fleets | `EmpireFleets.MaxFleets` counts war-zone systems you hold against 1, 5, 10 and so on. With none it returns 0, and the game says "Claim your first system to field Empire Fleets". |
| Build economic outposts, Defense stations or patrols | `StationSiteRules.GetRefusal` requires `system.faction == Faction.player`. Other ways the game might give you a station before your first claim are `(not verified)`. |
| Order Attack, Raid or Join siege | Those are Empire Fleet orders. |
| Open the Empire level tab | `CommandTabUnlocks.IsEmpireLevelUnlocked` is `systemClaimUnlocked`. |

Source: `ConquestStrength.OnComplete`, `ConquestWarEffort.Apply`, `ConquestJoin.CanJoin`, `ConquestJoin.HasJoinContribution`, `ConquestJoin.TryDonate`, `InterceptRules`, `LootManager.TryApplyInterceptKillPenalty`, `ConquestFleetInterceptTarget`, `FactionStationDestruction.GetDestroyReputationLoss`, `ConquestOwnership.Abandon`, `ConquestOwnership.HandOverToClaimant`, `BattleManager.EndBattlesAt`, `SystemClaim.GetRefusal`, `PlayerSectorPurchase.GetRefusal`, `EmpireFleets.MaxFleets`, `StationSiteRules.GetRefusal`, `CommandTabUnlocks`

#### What you face

- Systems: Ara Martis systems are created at level 32 to 36. Each conquest update (hourly) `RaiseTowardPlayer` lifts every system by 2 if you are more than 3 levels above it, by 1 if you are 1 to 3 levels above it, and by 1 with a 50% roll if it is level with you or up to 3 levels above you. A system 4 or more levels above you does not rise, so systems settle around your level plus up to 4 (derived).
- NPC fleet ship level: `FleetLevels.GetNpcFleetLevel` is `max(your effective level, highest conquest system level) + 6`. With systems settled near your level plus 4, that is about your level plus 10 (derived). Fleet target size is 2000 points regardless of your level; the number of fleets per faction is in [faction-economy](faction-economy.md).
- Station turrets: generated conquest stations pick medium or large turrets (M2 or L1) only at system level 45 or higher and small ones (S2) below that. Below 45 a Defense platform scores sizes 2 (90 hull, 80 firepower); at 45 or higher with M2 it scores sizes 4 (120 hull, 160 firepower). See [stations](stations.md) for per-part data. Ungenerated stations use the flat estimates (240, 480 or 560 firepower).
- Garrisons: ship count and budget follow the station tier, not your level (3 to 8 ships, 270 to 900 points). Garrison ship levels follow the NPC fleet level above.
- Station hit points (HP): live hubs and Defense stations take x20 HP, headquarters hubs x30.

Source: `ConquestWorld` (system creation at `random.RandomRange(32, 37)`), `Conquest.RaiseTowardPlayer`, `FleetLevels.GetGalaxyLevel`, `FleetLevels.GetNpcFleetLevel`, `CombatStationFactory` (`poi.level >= 45` turret choice), `FactionGarrison`, `CombatStationPart.GetStationHpMultiplier`

#### Most efficient approach

What matters before your first system is that your own faction reaches 30% of the Conquest sector, because that opens "Beyond the Front". You cannot hold anything yourself, so every action here is a nudge to the war between NPC (non-player character) factions. The counting rule, the faction behavior you are pushing against, the ranked levers, the pitfalls and a step-by-step plan are in [The 30% sector-control mission](#the-30-sector-control-mission).

Embassy missions give the faction wealth in place of war effort (amounts are in the Embassy missions row of [the levers table](#what-moves-your-factions-share)). Faction wealth buys warships (see [faction-economy](faction-economy.md)).

Source: `ConquestWarEffort`, `ConquestSystem.AddWarEffortBonus`, `ConquestFleetOrders.PlanAssault`, `ConquestFleetOrders.TryAttack`, `BattleManager.EndBattlesAt`, `ConquestOwnership.HandOverToClaimant`, `InterceptRules.AppliesKillPenalty`

### Scenario 2: above level 60, after "Beyond the Front"

#### What you can and cannot do

| You can | Notes |
|---|---|
| Buy systems in the player sector | Price `10,000,000 * 1.5^n` credits and `price / 5000` Vanguard Marks, where `n` is the number of systems you have bought. You must be in the system. The first one becomes your capital. |
| Claim unowned war-zone systems | Your flagship or an idle Empire Fleet in the system is enough. Not free territory, not player-sector systems. |
| Take systems by siege | Attack orders from Empire Fleets. Capture needs the conditions in "How a siege ends". |
| Field Empire Fleets | 1 slot at 1 system held, then 2, 3, 4, 5, 6, 7 at 5, 10, 20, 40, 80, 160 systems. Budget by your level: 2500 points and 12 ships at level 60, 4000 and 25 at level 100. |
| Build Defense stations and patrol fleets | 2 station slots per system (captured economic outposts use them too). Patrol fleets of 120 to 500 points, 1 to 3 per station by tier. |
| Raid, join sieges, set defense stances | Empire Fleet orders. |
| Level past 60 | Cap is 100 once the expansion is unlocked. |

| You still cannot | Why |
|---|---|
| Take a headquarters or your capital | `cannotBeTaken`. |
| Claim free territory | Independent hubs in the expansion are for NPC factions to settle. |
| Touch Meridia | No stations to besiege. Closed to you while Meridia is closed. |
| Claim without presence | Flagship or an idle fleet must be in the system. |

First purchase prices (`n` = systems already bought):

| n | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| Credits | 10,000,000 | 15,000,000 | 22,500,000 | 33,750,000 | 50,625,000 | 75,937,500 |
| Vanguard Marks | 2,000 | 3,000 | 4,500 | 6,750 | 10,125 | 15,188 |

Systems in the player sector have the SafeSector rule: NPC factions cannot enter them, so no siege or raid reaches them. The capital is also uncapturable. Systems you hold elsewhere are targets for any faction hostile to you.

Holding systems costs nothing up to your Empire Level's capacity (10 systems at Empire Level 1, 14 at 2, 19 at 3, 25 at 4, 32 at 5, 41 at 6, 52 at 7, 65 at 8, 81 at 9, 101 at 10). Past capacity the hourly upkeep is `2,000,000 * 2^(over - 1) * CostMultiplier(level) / CostMultiplier(60)` credits. If you cannot pay for 6 hours the newest system revolts. Empire Level needs `1000 * 1.65^(level - 1)` Empire XP (experience points); gaining a system gives 250, building a station 50, each hour 3 per station tier, and winning a battle 5 per enemy ship killed.

Source: `PlayerSectorPurchase`, `SystemClaim`, `EmpireFleets.MaxFleets`, `FleetLevelSteps`, `PlayerStation.patrolFleetCount`, `StationSiteRules`, `EmpireLevel`, `EmpireExperience`, `EmpireUpkeep`, `ConquestScope.IsOpenTo`, `ConquestWorld.MarkPlayerSector`

#### What you face

- Systems: expansion systems are created at level 60 and rise toward your level by the same rule as before, so they sit near your level plus up to 4 once you are past 60 (derived). Their ore levels ramp from 60 to 100 across the expansion (`ExpansionLevels.FloorAt`).
- NPC fleet ship level: still `max(your effective level, highest conquest system level) + 6`. With expansion systems in the list the highest system level is at least 60, so NPC ships are at least level 66 from the moment the expansion unlocks, and about your level plus 10 once you are above 60 (derived).
- Station turrets: every expansion system is level 60 or higher, so every generated conquest station gets the M2 or L1 turret layouts. Ara Martis stations switch to them when their system level reaches 45.
- Fleets: NPC fleets are 2000 points. Once the expansion is open, the Darkspace Compact fleet cap rises from 5 toward 12 and Meridia's Chosen are capped at 6; other factions stay capped at 8 (each cap is also `5 + war-zone systems / 15`). Great Gate reinforcement fleets are 1500 points. Raid parties are 200 to 300.
- Hostility: taking a system sets your reputation with the former owner to -5000, and the game tells you "Factions hostile to you will attack this system".

Source: `ConquestWorld.CreateExpansionSystem`, `ExpansionLevels.FloorAt`, `Conquest.RaiseTowardPlayer`, `FleetLevels.GetNpcFleetLevel`, `CombatStationFactory`, `ConquestFleetPlanner.MaxFleetsFor`, `ConquestFleetPlanner.MaxTerritoryFleets`, `SystemClaim.ApplyReputationRule`

#### Taking a system with Empire Fleets

Worked cases (derived from the formulas above; station firepower with `m = 1`, no war effort, system not settling). Next-battle strength is the figure NPC fleet planning compares attackers against; see [what NPC factions use instead of the Defense number](#what-npc-factions-use-instead-of-the-defense-number).

| System | Defense number | Next-battle strength (NPC planning figure) | Points to avoid the outmatched warning |
|---|---|---|---|
| Hub only, medium (480 firepower) | 480 | 480 | 553 |
| Hub plus one tier-5 Defense station (garrison 900, firepower capped at 700) | up to 1600 | up to 1600 | up to 1840 |
| Same, plus one resident 2000-point fleet | up to 3600 | up to 3600 | up to 4140 |

A level 60 fleet is 2500 points, so it clears the outmatched threshold of the first two kinds. The third needs a level 100 fleet (4000, plus the mix bonus) or several fleets, because the 1.15 and 1.25 margins apply to the total. Capture needs 1.25 times the Defense at that moment. Once the structure is broken and every defender is dead, Defense falls to the 250 floor, so about 313 points is enough; while defenders are alive at the cap it is 1.25 times their points plus the station term.

- Read the Defense row, then work out which figure the warning uses (next-battle strength): the weakest Defense station plus fleets, or the full Defense number. Your Empire Fleet's points must be at least 1.15 times that to avoid the outmatched warning, and at least 1.25 times the current Defense number to capture.
- A fleet beating the defender fleet at the 10-minute cap passes to the siege. Remove fleets first by fighting them, or by intercepting reinforcements while they are still traveling, before they reach the system.
- Station structure only takes damage after every defender ship is dead. A siege with large defenders needs most of its 30 rounds just to clear them.
- Capture is allowed at 600 s once the station is broken and nothing defends. Staying on until the cap is not required if those conditions hold.
- Attack right after a system changes hands: its station profile is halved for 5 to 6 hours (settling), and it pays no taxes for the first 1 to 2 hours (unrest).
- Missions against the owner, done next to the target, lower the target's station firepower by up to half. Each mission adds one step and resets the 3-hour timer. A red "Your war effort" row shows it.
- A hull rating lead over the defenders is worth up to 15% of strength. Garrison hull ratings come from level and rank; your ships' upgrades raise yours.
- You can also take a system by destroying its hub live. That abandons the system and offers you the claim, with the reputation drop to Hostile.
- Damage converts to destroyed points one for one, so a fleet of many small ships and one of a few big ships lose points at the same rate. The 0.5 size bias only changes which ships go first. The ships you lose are removed permanently.
- A Defense station has to be won first. Each stage is its own battle with its own 600 s minimum, so a fortified system takes several battles and several NPC planning passes.
- Check the share rule before you expand: a faction holding under 10% of war-zone systems only attacks unowned systems or owners at 10% or more, so while you hold under 10% the small factions leave you alone, but the large ones do not `(share of all war-zone systems; total count not verified)`.

#### Defending a system you hold

- Keep a fleet in the system with a defense stance. It counts in full for Defense, plus the mix bonus (2 or more ship sizes, up to 3% per size at Empire level 10).
- Build Defense stations and assign patrol fleets. They count in full for Defense, and the hub siege goes straight to the hub rather than through station stages. Patrol ships lost in a siege are lost for good.
- Run war effort missions for the faction that owns the system, near that system. A +1.0 bonus doubles the station term, but it is worth at most 700 to 1400 of Defense. This applies to systems you hold only if the mission's source faction owns the system, so it is `(not verified)` for your own systems.
- Stay out of reach of the 1.15 threshold NPC fleet planning uses: NPC fleets attack when their points are at least 1.15 times the weakest-station strength plus any fleets already traveling to relieve it. A lone NPC fleet is 2000 points, so a Defense number above about 1740 stops single-fleet attacks. Assaults combine 2 or more fleets and can exceed that.
- A flagship or fleet of yours in the venue turns the battle live and freezes the simulation; the fight then depends on the ships alone, with the real station HP multiplied by x20 (x30 at a headquarters) and war effort scaling station weapons.
- A defending fleet or patrol in the system stops a raid from running unopposed. An undefended raid finishes at its 600 s minimum once the station structure (15% of normal hull) is broken.

Source: `ConquestCombat.GetNextBattleStrength`, `EmpireFleets.GetOutmatchingDefense`, `BattleManager.CanCaptureEarly`, `EmpireFleets.JoinsDefense`, `ConquestFleetOrders.ApplyPeace`, `ConquestWarTuning.PeaceShareThreshold`

### Where the efficient choice differs

| Question | Level 60 or below, no system | Above 60, claim unlocked |
|---|---|---|
| What can I hold? | Nothing. | Systems, with 1 fleet slot per threshold. |
| Main lever on a war | Interception, live hub kills at the right moment, war effort bursts. | Your own fleets: attack and defend. War effort is a small add-on. |
| War effort | Most of what you can do to the war. | A minor boost: at most 250 to 700 Defense at the cap. |
| Intercepting | Cheap to start; costs -1000 per ship force-attacked plus -3500 per kill with factions at Wary or better, nothing against factions that are already your enemies. | Same cost; a big fleet of yours can usually fight the real fight instead. |
| Hub kill | Hands the system to the besieger (or leaves it unowned). | Offers the claim to you, with a drop to Hostile with the old owner. |
| Reputation | Lose it station by station. | Lose it system by system (to -5000), and the owner attacks you afterwards. |
| Enemy ship level | Your level plus about 10. | At least 66, your level plus about 10 above 60. |
| Station turrets | Small below level 45, then M2 and L1. | M2 and L1 throughout the expansion. |

### Numbers to watch

| Number | Value | Why |
|---|---|---|
| 1.15 | NPC attack margin | Attackers need this ratio over next-battle strength. |
| 1.25 | Capture margin | Attackers need this ratio over current Defense to capture. |
| 600 s | Minimum siege time before capture | Applies per stage. |
| 1800 s / 600 s | Siege / clash cap | A stage's maximum length when simulated. |
| 60 s, 4%-6% | Round length, damage share | A fleet deals about 5% of its strength per round. |
| 250, 700 | Station firepower floor and cap | Weapon count beyond the cap adds nothing to Defense. |
| x0.5 | Settling and war effort floor | Settling halves the station profile; a full war effort penalty halves the station term. |
| 35% / 75% | Retreat / recovery thresholds | NPC fleets retreat below 35% and are ready again at 75%. |
| 3600 s / 10800 s | Re-attack block after a stalled siege / war effort duration per mission | Both are per-event timers, not a schedule. |
| 14400 s | Raid cooldown per station | Station cannot be raided again until it ends. |
| 30% | Faction share to finish the sector control mission | Needed before "Beyond the Front" is offered. |
