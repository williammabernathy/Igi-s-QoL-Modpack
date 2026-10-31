# ECHO, the autopilot

ECHO is the game's autopilot: switched on, it picks missions, mines, salvages, fights, unloads at stations and hires crew without you. These features change how it decides, and most of their settings are on the ECHO tab of the mod window. The tab is there while any ECHO feature is on. Settings keys are listed in [settings.md](../settings.md).

Every setting here starts at the game's own behaviour, with one exception: ECHO no longer pays for crew that can't fit aboard (`HireOnlyWhatFits`, see [ECHO decisions](#echo-decisions)).

## ECHO activity priority

![The ECHO tab: activity list and decisions](../../images/tab-echo.jpg)

An ordered list of activities. When ECHO looks for something to do (docked, or in empty space), it tries them from the top down and takes the first one your ship is equipped for and can find work for. The top entry is also the game's preferred activity: picking an activity in the Autopilot tab's dropdown moves it to the top here, and picking Autodetect empties the list. An empty list is the game's behaviour, one preferred activity.

## ECHO decisions

Thresholds ECHO checks before it acts. Each defaults to the game's value.

| Setting | What it does |
|---|---|
| Pick up loot before leaving | Collect the loot floating nearby before flying on. Off leaves sooner |
| Raid factions at reputation or below (-500) | Raiding costs reputation, so ECHO only raids factions you're already at or below this with |
| Crew needed to start an operation (3) | Pilots don't count |
| Crew needed to board a ship (0.5) | Share of the flagship's maximum crew |
| Hire crew when below (0.5) | ECHO flies off to hire when the flagship's crew drops below this share. 0 stops those trips |
| Fast cargo unloading (off) | Unload a whole stack per step (as much as storage has room for) instead of a few items every few seconds. A full hold empties in a few steps instead of minutes |
| Quick station steps (off) | At a friendly station, ECHO's next decision comes 2 s (the game's minimum) after it docks, after a decision that changed its missions, flagship crew, cargo or credits, and once the flagship is repaired, instead of its whole decision time (12 s, less with the Prompt Engineering skills). A visit that turns in a mission and takes the next one is about half a minute shorter. Waiting for a mission to come up keeps the normal time, so ECHO doesn't decide more often than it would anyway |
| Recruit willing prisoners before hiring (off) | When ECHO tops up the flagship's crew, willing prisoners of the missing types are recruited first (flagship brig, fleet brigs, station brig) at 40% of the crew price, as on the brig screens. Needs ECHO's "buy crew" setting on |
| Hire only crew that fits aboard (on) | The game asks for a type's whole shortfall, charges for all of it and drops what doesn't fit. On: ECHO takes from the reserve and pays only for crew that fit |
| Make room for short crew types (off) | Before that top-up, ECHO moves crew above their share in the crew ratios to the reserve, as many as the short types can get right away. Types with no share set are never moved |

## ECHO missions

![ECHO missions settings](../../images/tab-echo-missions.jpg)

`MissionsAtOnce` (1-5, default 1) lets ECHO take several missions from a board in one visit. It flies from each finished mission location to the nearest open one in the system and turns them all in on one trip home, in one decision instead of one per 10 s. It still goes home first when the hold is full, it has no ammo it could restock, crew is short, the flagship's hull or armour is below half, or (with [ECHO retreat](#echo-retreat) on) a ship is below its hull threshold.

Below that is one switch per mission kind ECHO can be given: Eliminate, Reclaim Salvage Rights, Reclaim Asteroid Belt, Clear Pirate Base, Capture Enemy Vessel, Recover (item), Salvage (material), Mine (ore), Collect (ore) and Support (ore) Mining. A kind that's off is never offered to ECHO; with every kind off, ECHO skips missions and goes on to its activities. ECHO only gets missions for its preferred activity, so only that activity's kinds matter. Courier missions stay on the Autopilot tab's own switch.

Clear Pirate Base is an installation raid: 3-5 minutes, credits and items found aboard, some crew lost. In two 30-minute test runs these paid far more than the other combat missions.

## ECHO retreat

![ECHO retreat thresholds](../../images/tab-echo-retreat.jpg)

`FlagshipHullPercent` and `FleetHullPercent` (0-90, both 0 = off). While ECHO is on and the flagship's hull, or that of a fleet ship at the same location, drops below its threshold, ECHO stops fighting, skips the loot pickup and flies the fleet to the nearest friendly station (the home station when it's in this system). There it docks, waits for the hangar's free auto-repair (about 200 s; [Station services](stations.md#station-services) "Pay for repairs when you dock" buys the 20 s repair instead) and carries on. The status line reads "Retreating to Tower I for repairs: Eclipse hull at 20%".

It doesn't leave while an operation or boarding is running, from next to a station, or when the Autopilot tab's travel is off.

Without it the flagship fights until the emergency jump at 0 hull, and every heavy hull hit can kill a crew member; the 10 autopilot sessions one save kept had 50 emergency jumps in 48 hours. Fleet ships already leave by themselves at 10% hull for two minutes of repairs, so `FleetHullPercent` only matters above 10.

## ECHO abilities and deployables

![ECHO abilities and deployables](../../images/tab-echo-kit.jpg)

A rule per flagship ability and per deployable type (from the flagship's cargo or the armory): Off, In combat when ready, Shields / Armour / Hull below N %, Targeted by at least N enemies, or Whenever ready. All rules start off.

- Every rule except Whenever ready only fires in a fight: damage in the last 10 s, or a target.
- Untargeted abilities fire as they are. Hostile-targeted ones go on ECHO's target, friendly-targeted ones on your most damaged ship, asteroid ones (Crackshot Drone) on the asteroid the flagship mines, and wreck ones (LB-RTR, SF-CRK) on the nearest wreck whose cargo box or credits are still there. Drone-targeted abilities aren't automated.
- Toggle abilities are switched on while the rule holds, and off again only if the rule switched them on.
- Deployables (turrets, shield pylons, decoys, mines) are dropped from the flagship's cargo, at most one per 2 s and only when the game's deploy cooldown allows.

`PickUpTurrets` (on) tractors the turrets these rules dropped back in 5 s after the fight; turrets you placed yourself stay. `AlsoWithoutEcho` (off) applies the rules while you fly the ship yourself.

## Preferred ores

![Preferred ores with two ores listed](../../images/tab-echo-ores.jpg)

A list of ores and a mode, saved per save game.

- Prefer: ECHO, your ships' own asteroid picks and their mining drones take the nearest asteroid with a listed ore first, and ECHO picks a mining location in the system that still has one. With none left anywhere, mining works as usual.
- Only: other asteroids are left alone, and ECHO moves on to its next activity when no listed ore is left.
- Off (default), or an empty list, is the game's behaviour.

Core ores count only once you have the Basic Scanning mining skill, the point where the game's asteroid tooltip names them. The [Scanner](loot-and-cargo.md#scanner) marks the listed ores and can point edge markers at them.

## ECHO's operations

The stealth mode, ammo type and buyout choice for operations ECHO starts on its own. They're on the [Operations tab](stations.md#operations-tab), next to your own defaults. "Same as yours" (the game's behaviour) uses whatever you last picked yourself. `AutoBuyOut` lets ECHO accept defenders' offers to switch sides for credits when you can pay; the game never accepts them for ECHO.

## ECHO cargo rules

When ECHO unloads at a station, the dock action from the [Item rules tab](loot-and-cargo.md#item-rules) decides what happens to each stack: deposit as usual, keep it aboard, sell it, or deposit up to an amount and sell the rest. Favourite stacks, ammo the flagship uses and the usable items the game keeps aboard are never touched.

`ApplyToAutoDeposit` (on) covers the game's fleet auto-deposit too, which empties fleet holds the moment you dock: Keep aboard stays aboard, Sell stays aboard until ECHO unloads it, and Sell above N deposits up to N. When you dock yourself nothing is sold, unless Station services' "Sell by item rules when you dock" is on or you press the airlock's Sell by rules button. Off: the game deposits everything as usual, so the rules only matter for fleet ships with auto-deposit off.
