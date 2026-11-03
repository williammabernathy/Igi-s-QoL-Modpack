# Empire fleets

This page covers your own Empire Fleets and patrol fleets: slots and size, service levels, orders, stances, the outmatched warning, how your fleets show on the map and in the Defense number, and what the war looks like above level 60. Fleets need player level 10 and a held system; the war options on this page open with "Beyond the Front", which needs level 60 and one finished 30% sector-control mission ([gate](README.md)). Values are read from the game code of version 0.8.3.4 (the decompiled source, not in-game testing). Anything not confirmed in code is marked `(not verified)`.

Terms and abbreviations are explained in the [Beyond the Front glossary](glossary.md), the [conquest glossary](../conquest/glossary.md) and the [basics glossary](../basics/glossary.md). Buying and claiming systems, upkeep and your stations are in [your empire](your-empire.md).

## Slots and size

You get fleet slots by war-zone systems held, and each fleet's size depends on your level. A fleet slot is room for one Empire Fleet; the game shows "{n} / {m} fleet slots". The empire points and empire ships columns of the personal points table in [scaling](../basics/scaling.md#how-many-enemies-a-hostile-site-gets) are this budget; the full table is below.

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

In the code: [slots and size](#code-slots-and-size).

## Service levels and standard-issue copies

Rows of the fleet-level question table in [war](../conquest/war.md#fleet-levels) that belong to your own ships:

| Question | Answer |
|---|---|
| Level of a newly built player-faction ship | Level of the system it is built in |
| When is an Empire Fleet "due" | Fleet service level <= system level - 3 |
| What an Empire Fleet refit does | Raises service level to system level + 3 |

Patrol ships at your Defense stations use the same due rule with the station's system level (refit timer 300 s).

Your own ships in an Empire Fleet or on patrol have a service level, set to the system level when they join. When they are spawned into a battle they do not use your own loadout. The game generates a "standard issue" copy at that service level (see [Standard-issue gear](../basics/scaling.md#standard-issue-gear)), keeps the ship's ship rank and rarity, and gives it hull nodes as if it were an NPC of that level.

The standard-issue copies get the NPC ship HP curve (see [NPC ship HP curve](../basics/scaling.md#npc-ship-hp-curve)); your actual flagship does not. They do not get ship rank modifiers or escalation buffs.

Standard-issue equipment is used for your Empire Fleet and patrol ship copies in battle (at the ship's service level), as well as for hangar-bay fighters. Your own fitted gear does not carry over into those battles.

In the code: [service levels and standard-issue copies](#code-service-levels-and-standard-issue-copies).

## Orders

| Order | Rules |
|---|---|
| Move | Travel to a system and stay. |
| Attack | War-zone system only; not your own, not a headquarters, not a system whose hub is already gone, not a for-sale player-sector system, not free territory, not already fought over by a non-friendly faction, and its owner must be your enemy (or you must declare war). Arriving at an unowned system claims it. |
| Raid | Economic outpost (not a Defense station) in the war zone owned by a hostile faction. |
| Return | Home system, or your nearest held system if home is lost. |
| Refit | Nearest Construction Yard of yours. Refused if it would not raise the fleet's level. |
| Join siege | Needs a fight in that system with exactly one friendly side. |

The range is 30 jumps. You cannot order a fleet that is defending against another fleet in a fleet clash, and you cannot remove ships from a fleet that is in a battle. Attacking an ally or neutral lets you declare war from the order dialog. Giving a new order breaks off an attack in progress.

Join siege lets your fleet help whichever side is yours or allied: if the owner is friendly and the attacker is not, the fleet defends (and counts in the system's Defense); if the attacker is friendly and the owner is not, it joins as an attacker supporter. Both sides friendly, or neither, is refused.

In the code: [orders](#code-orders).

## Defense stances and other behavior

| Stance | Behavior |
|---|---|
| None | Fleet does not defend (counts only while on a Join siege order). |
| Defend system (the default) | While idle, joins the defense of the system it is in. |
| Defend sector | Also answers sieges and raids on your systems anywhere in the same sector as its post, traveling there with the Responding label. |

Clicking the active stance again returns it to None. An idle fleet counts toward Defense if it has a stance or a Join siege order, is in the system, and is not traveling. If a system with your defending fleets is captured, those fleets fall back to their home system (a Sector-stance fleet whose post is still held stays).

The fleet retreat threshold defaults to 0 and the setting is hidden in this version, so your fleets never break off on their own. A fleet only retreats if that value is changed. Your Empire Fleet ships and patrol ships are real ships: when they die in a simulated or live battle they are removed permanently.

The fleet mix bonus appears on the fleet card as "Fleet mix +x% off-screen strength" and applies to simulated rounds and to the Defense number, not to live combat.

In the code: [defense stances and other behavior](#code-defense-stances-and-other-behavior).

## Outmatched warning

When you give an Attack order and the fleet's points are below 1.15 x the target's next-battle strength (the threshold NPC fleet planning uses), the game warns: "{fleet} ({points} points) is outmatched here ({defense} defense) and will likely be wiped out." and offers "Attack anyway". The number in the warning is the next-battle strength, which can be lower than the full number in the Defense row when the system has Defense stations.

In the code: [outmatched warning](#code-outmatched-warning).

## Your fleets in battle and on the map

- In a live battle, ships of your own Empire Fleets and patrol fleets that die are removed permanently, as are NPC ships in their fleets.
- Hull rating of your ships in simulated battles (see [Hull edge](../conquest/war.md#hull-edge)): your ships use the larger of their spent upgrade points and an NPC-equivalent figure.
- "Withdrew before relief arrived" outcome (see [Outcomes](../conquest/war.md#outcomes)): the lead attacker is ordered away with no standing supporters; Empire Fleets only, in practice.
- Headquarters: your Attack order on one is refused ("A faction's headquarters can't be taken by a fleet.").
- The Fleets tab of the Empire window lists your own Empire Fleets.
- Cadence: Empire Fleet order check, every 5 s.

Label rows that only apply to your fleets. In the label order in [war](../conquest/war.md#fleet-labels-on-the-map), "Moving" also covers an Empire Fleet Move order, and this row sits between "Supporting" and "Defending an outpost from raiders":

| Label | When |
|---|---|
| Joining the siege of {system} | Your fleet is traveling on a Join siege order |

In the reason labels: "Attacking" is also set by your Attack order; "Responding to a distress call" is also set for your Sector-stance fleets going to a system under attack in their sector, and for a Join siege order that defends.

Your own fleets' map callout uses a different set: "Moving to {system}", "Holding at {system}", "Returning to {system}", "Attacking {system}", "Raiding {station}", "Refitting at {system}", "Joining the siege of {system}", and for stance "Defending {system}", "Defending sector {sector}" or "Not defending".

In the code: [your fleets in battle and on the map](#code-your-fleets-in-battle-and-on-the-map).

## Your fleets and patrols in the Defense number

Additions to the Defense formula and its explanation in [war](../conquest/war.md#what-the-defense-number-means).

A defending fleet also counts when it is an allied Empire Fleet defending an allied system. Only Empire Fleets have a fleet mix bonus.

In the code: [your fleets and patrols in the Defense number](#code-your-fleets-and-patrols-in-the-defense-number).

### Fleet mix bonus

Empire Level gives a fleet mix bonus on Empire Fleets with 2 or more distinct non-drone ship sizes. It counts toward the Defense row and battle strength in simulated battles. Empire Level also sets how many systems you may hold without upkeep ([Empire Level and upkeep](your-empire.md#empire-level-and-upkeep)).

```
bonus = number of distinct ship sizes x rate    (only with 2 or more distinct non-drone ship sizes)
rate = 1% per size at Empire Level 1-4, 2% at Empire Level 5-9, 3% at Empire Level 10+
```

In the code: [fleet mix bonus](#code-fleet-mix-bonus).

### Patrol ships

Patrol ships count only for your own systems: every ship assigned to a patrol fleet (ships you assign to guard one of your Defense stations) at one of your Defense stations in the system counts for its full points. There is no mix bonus.

Patrol fleet capacity by player Defense station tier:

| Tier | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| Patrol fleets | 1 | 1 | 2 | 2 | 3 |
| Points per fleet | 120 | 240 | 260 | 400 | 500 |

In the code: [patrol ships](#code-patrol-ships).

### Levers that raise your own Defense

These two rows extend the levers table in [war](../conquest/war.md#ways-to-lower-or-raise-defense), which lists the other ways to raise a system's Defense.

| Lever | Effect |
|---|---|
| To raise your own: stack fleets | Fleets with a defense stance count in full, plus the mix bonus. |
| To raise your own: patrol fleets | Each Defense station gives 1 to 3 patrol fleets of 120 to 500 points each by tier. |

## Above level 60, after "Beyond the Front"

What you could do before, at level 60 or below, is in [sector control](../conquest/sector-control.md#level-60-or-below-before-your-first-system). This section applies once you have accepted "Beyond the Front" ([gate](README.md)).

In the code: [above level 60](#code-above-level-60-after-beyond-the-front).

### What you can and cannot do

| You can | Notes |
|---|---|
| Buy systems in the player sector | Price and purchase rules are in [buying systems](your-empire.md#buying-systems-in-the-player-sector). The first one becomes your capital. |
| Claim unowned war-zone systems | Your flagship or an idle Empire Fleet in the system is enough. Not free territory, not player-sector systems. |
| Take systems by siege | Attack orders from Empire Fleets. Capture needs the conditions in [How a siege ends](../conquest/war.md#how-a-siege-ends). |
| Field Empire Fleets | Slots and budget by your level are in [slots and size](#slots-and-size): 2500 points and 12 ships at level 60, 4000 and 25 at level 100. |
| Build Defense stations and patrol fleets | 2 station slots per system (captured economic outposts use them too). Patrol fleets of 120 to 500 points, 1 to 3 per station by tier. |
| Raid, join sieges, set defense stances | Empire Fleet orders. |
| Level past 60 | Cap is 100 once the expansion is unlocked. |

| You still cannot | Why |
|---|---|
| Take a headquarters or your capital | They are flagged as untakeable. |
| Claim free territory | Independent hubs in the expansion are for NPC factions to settle. |
| Claim without presence | Flagship or an idle fleet must be in the system. |

The safe-sector rule and upkeep that follow from holding systems are in [your empire](your-empire.md#buying-systems-in-the-player-sector).

In the code: [what you can and cannot do](#code-what-you-can-and-cannot-do).

### What you face

- Systems: expansion systems are created at level 60 and rise toward your level by the rule in [How system levels rise](../conquest/war.md#how-system-levels-rise), so they sit near your level plus up to 4 once you are past 60 (worked out from the formulas). Their ore levels ramp from 60 to 100 across the expansion.
- NPC fleet ship level: still the higher of your effective level and the highest conquest system level, plus 6 ([Fleet levels](../conquest/war.md#fleet-levels)). With expansion systems in the list the highest system level is at least 60, so NPC ships are at least level 66 from the moment the expansion unlocks, and about your level plus 10 once you are above 60 (worked out from the formulas).
- Station turrets: every expansion system is level 60 or higher, so every generated conquest station gets the M2 or L1 turret layouts. Ara Martis stations switch to them when their system level reaches 45.
- Fleets: NPC fleets are 2000 points. The fleet caps once the expansion is open are in [fleet caps once the expansion war is open](your-empire.md#fleet-caps-once-the-expansion-war-is-open). Great Gate reinforcement fleets are 1500 points. Raid parties are 200 to 300.
- Hostility: taking a system sets your reputation with the former owner to -5000, and the game tells you "Factions hostile to you will attack this system".

In the code: [what you face](#code-what-you-face).

### Taking a system with Empire Fleets

Worked cases (worked out from the Defense formula in [war](../conquest/war.md#what-the-defense-number-means) and the patrol and mix terms above; station firepower with the sector station multiplier at 1, no war effort, system not settling). Next-battle strength is the figure NPC fleet planning compares attackers against; see [what NPC factions use instead of the Defense number](../conquest/war.md#what-npc-factions-use-instead-of-the-defense-number).

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

In the code: [taking a system with Empire Fleets](#code-taking-a-system-with-empire-fleets).

### Defending a system you hold

- Keep a fleet in the system with a defense stance. It counts in full for Defense, plus the mix bonus (2 or more ship sizes, up to 3% per size at Empire Level 10).
- Build Defense stations and assign patrol fleets. They count in full for Defense, and the hub siege goes straight to the hub rather than through station stages. Patrol ships lost in a siege are lost for good.
- Run war effort missions for the faction that owns the system, near that system. A +1.0 bonus doubles the station term, but it is worth at most 700 to 1400 of Defense. Whether this applies to systems you hold is `(not verified)` ([War effort on your own systems](your-empire.md#war-effort-on-your-own-systems)).
- Stay out of reach of the 1.15 threshold NPC fleet planning uses: NPC fleets attack when their points are at least 1.15 times the weakest-station strength plus any fleets already traveling to relieve it. A lone NPC fleet is 2000 points, so a Defense number above about 1740 stops single-fleet attacks. Assaults combine 2 or more fleets and can exceed that.
- A flagship or fleet of yours in the venue turns the battle live and freezes the simulation; the fight then depends on the ships alone, with the real station HP multiplied by x20 (x30 at a headquarters) and war effort scaling station weapons.
- A defending fleet or patrol in the system stops a raid from running unopposed. An undefended raid finishes at its 600 s minimum once the station structure (15% of normal hull) is broken.

In the code: [defending a system you hold](#code-defending-a-system-you-hold).

## Where the efficient choice differs

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

The 1.15 NPC attack margin also sets the outmatched warning for your Attack orders, and the 1.25 capture margin applies to your own fleets' captures. Both rows are in [sector control](../conquest/sector-control.md#numbers-to-watch).

## In the game code

This part holds the code names, call details and `Source:` lines behind the sections above. It comes from the decompiled game code of version 0.8.3.4; to check a claim, look up the class and method named in the `Source:` line.

### Code: Slots and size

The `EmpirePoints` and `EmpireShips` columns of the `PersonalPoints` table in [scaling](../basics/scaling.md#how-many-enemies-a-hostile-site-gets) are this budget.

Source: `EmpireFleets.FleetSlotSystems`, `EmpireFleets.MaxFleets`, `FleetLevelSteps`, `FleetLevelSteps.EmpirePoints`

### Code: Service levels and standard-issue copies

Rows of the `FleetLevels` question table that belong to your own ships (the table is in [war](../conquest/war.md#fleet-levels)).

`BuildStandardIssueCopy` generates a "standard issue" copy at service level, keeps the ship's ship rank and rarity, and gives it hull nodes as if it were an NPC of that level.

The standard-issue copies (faction player, `isPlayer` false) get the NPC ship HP curve (`ApplyNpcShipHpCurve`); your actual flagship does not. They do not get ship rank modifiers or escalation buffs, because those check the faction.

`LoadStandardIssueEquipment` is used for your Empire Fleet and patrol ship copies in battle (at the ship's service level), as well as for hangar-bay fighters.

Source: `FleetLevels.GetNewShipLevel`, `FleetLevels.IsDueRefit`, `FleetLevels.GetRefitLevel`, `ServiceLevels.Join`, `ServiceLevels.CompleteFleetRefit`, `ConquestCombat.BuildStandardIssueCopy`, `AbstractUnitData.LoadStandardIssueEquipment`

### Code: Orders

| Code name | Name in the game |
|---|---|
| `Hold` | Move |
| `Attack` | Attack |
| `Raid` | Raid |
| `Return` | Return |
| `Refit` | Refit |
| `JoinSiege` | Join siege |

The range is 30 jumps. You cannot order a fleet that is the defender in a `FleetClash`, and you cannot remove ships from a fleet that is in a battle. Attacking an ally or neutral lets you declare war from the order dialog. Giving a new order breaks off an attack in progress (`Withdrew`).

The Attack rule "not a system whose hub is already gone" is the code's "hubless" check.

Source: `EmpireFleets.TryOrder`, `EmpireFleets.GetRefusal`, `EmpireFleets.RefuseAttack`, `EmpireFleets.RefuseRaid`, `EmpireFleets.RefuseJoinSiege`, `EmpireFleets.GetJoinSide`, `EmpireFleets.DefendsAlly`, `EmpireFleets.MaxOrderHops`

### Code: Defense stances and other behavior

The fleet retreat threshold (`retreatFraction`) defaults to 0 and the setting is hidden (`RetreatSettingShown` false), so your fleets never break off on their own.

Source: `EmpireFleets.JoinsDefense`, `EmpireFleets.GuardSector`, `EmpireFleets.FallBack`, `EmpireFleets.CheckRetreat`, `EmpireFleetCommands`, `FleetMixBonus`

### Code: Outmatched warning

Source: `EmpireFleets.GetOutmatchingDefense`, `EmpireFleetCommands`

### Code: Your fleets in battle and on the map

Source: `ConquestFleetMapIcon.GetTaskLabel`, `MapFleetCallout`, `EmpireFleets.RefuseAttack`, `HullRating.GetHullPoints`, `BattleManager.TickLive`

### Code: Your fleets and patrols in the Defense number

The subsections below back the fleet mix and patrol rules above.

#### Code: Fleet mix bonus

```
FleetMixBonus = sizes * rate    (only if the fleet has 2 or more distinct non-drone ship sizes)
rate = 1% per size at Empire Level 1-4, 2% at Empire Level 5-9, 3% at Empire Level 10+
```

Source: `FleetMixBonus.GetBattleStrength`, `FleetMixBonus`, `EmpireLevel`

#### Code: Patrol ships

Source: `ConquestCombat.GetPatrolShips`, `PlayerStation.patrolFleetCount`, `PlayerStation.patrolFleetBudget`

### Code: Above level 60, after "Beyond the Front"

The subsections below back the claims in the sections of the same names.

#### Code: What you can and cannot do

The "Take a headquarters or your capital" row: the reason is `cannotBeTaken`.

Source: `SystemClaim`, `EmpireFleets.MaxFleets`, `FleetLevelSteps`, `PlayerStation.patrolFleetCount`, `StationSiteRules`

#### Code: What you face

Ore levels ramp from 60 to 100 across the expansion (`ExpansionLevels.FloorAt`). NPC fleet ship level is `max(your effective level, highest conquest system level) + 6` ([Fleet levels](../conquest/war.md#fleet-levels)).

Source: `ConquestWorld.CreateExpansionSystem`, `ExpansionLevels.FloorAt`, `Conquest.RaiseTowardPlayer`, `FleetLevels.GetNpcFleetLevel`, `CombatStationFactory`, `ConquestFleetPlanner.MaxFleetsFor`, `ConquestFleetPlanner.MaxTerritoryFleets`, `SystemClaim.ApplyReputationRule`

#### Code: Taking a system with Empire Fleets

The share-rule bullet's total count is not verified: `(share of all war-zone systems; total count not verified)`.

The worked-case table uses station firepower with `m = 1` (the sector station-defense multiplier).

Source: `ConquestCombat.GetNextBattleStrength`, `EmpireFleets.GetOutmatchingDefense`, `BattleManager.CanCaptureEarly`, `ConquestFleetOrders.ApplyPeace`, `ConquestWarTuning.PeaceShareThreshold`

#### Code: Defending a system you hold

Whether war effort applies to systems you hold is `(not verified)`.

Source: `EmpireFleets.JoinsDefense`
