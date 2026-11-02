# ECHO, the autopilot

ECHO is the game's autopilot: switched on, it picks missions, mines, salvages, fights, unloads at stations and hires crew without you. These features change how it decides, and most of their settings are on the ECHO tab of the mod window. The tab is there while any ECHO feature is on. Settings keys are listed in [settings.md](../settings.md).

Every setting here starts at the game's own behaviour, with one exception: ECHO no longer pays for crew that can't fit aboard (`HireOnlyWhatFits`, see [ECHO decisions](#echo-decisions)).

## ECHO activity priority

![The ECHO tab: activity list and decisions](../../images/tab-echo.jpg)

An ordered list of activities: Mining Surface, Mining Core, Salvage Surface, Salvage Core, Combat and Cargo, the same names as the Autopilot tab's dropdown. Rows have up, down and remove buttons; an "Add an activity" dropdown at the bottom adds the ones not listed. When ECHO looks for something to do (docked, or in empty space), it tries them from the top down and takes the first one your ship is equipped for and can find work for. Cargo has no search of its own (the game has none either), so it only matters at the top. If none of the listed activities fits the ship's loadout, the status line says there is no suitable loadout for the top one. The top entry is also the game's preferred activity: picking an activity in the Autopilot tab's dropdown moves it to the top here, and picking Autodetect empties the list. Putting Cargo at the top turns the Autopilot tab's missions switch on. An empty list is the game's behaviour, one preferred activity. The list is saved per save game.

## ECHO decisions

Thresholds ECHO checks before it acts. Each defaults to the game's value, except where noted.

| Setting | What it does |
|---|---|
| Pick up loot before leaving | Collect the loot floating nearby before flying on. Off leaves sooner |
| Raid factions at reputation or below (-500) | Raiding costs reputation, so ECHO only raids factions you're already at or below this with. A higher number such as 0 lets it raid factions you get on better with, and you lose that reputation. The Umbral transponder skips the check, as in the game |
| Crew needed to start an operation (3) | Pilots don't count |
| Crew needed to board a ship (0.5) | Share of the flagship's maximum crew |
| Hire crew when below (0.5) | ECHO flies off to hire when the flagship's crew drops below this share. 0 stops those trips |
| Fast cargo unloading (off) | Unload a whole stack per step (as much as storage has room for) instead of a few items every few seconds. A full hold empties in a few steps instead of minutes |
| Quick station steps (off) | At a friendly station, ECHO's next decision comes 2 s (the game's minimum) after it docks, after a decision that changed its missions, flagship crew, cargo or credits, and once the flagship is repaired, instead of its whole decision time (12 s, less with the Prompt Engineering skills). A visit that turns in a mission and takes the next one is about half a minute shorter. Waiting for a mission to come up keeps the normal time, so ECHO doesn't decide more often than it would anyway |
| Recruit willing prisoners before hiring (off) | When ECHO tops up the flagship's crew, willing prisoners of the missing types are recruited after the crew reserve and before hiring (flagship brig, fleet brigs, station brig) at 40% of the crew price, as on the brig screens. Needs ECHO's "buy crew" setting on |
| Hire only crew that fits aboard (on) | The game asks for a type's whole shortfall, charges for all of it and drops what doesn't fit. On: ECHO takes from the reserve and pays only for crew that fit |
| Make room for short crew types (off) | Before that top-up, ECHO moves crew above their share in the crew ratios to the reserve, as many as the short types can get right away. Types with no share set are never moved |

## ECHO missions

![ECHO missions settings](../../images/tab-echo-missions.jpg)

`MissionsAtOnce` ("Missions ECHO takes at once", 1-5, default 1) lets ECHO take several missions from a board in one visit. It only takes missions it would work on itself (not too risky for the flagship, loadout in place) and stops at the game's own mission limit. It flies from each finished mission location to the nearest open one in the system and turns them all in on one trip home, in one decision instead of one per 10 s. Finished missions at the station it's docked at are turned in before it picks a new one. It still goes home first when the hold is full, it has no ammo it could restock, crew is short, the flagship's hull or armour is below half, or (with [ECHO retreat](#echo-retreat) on) a ship is below its hull threshold. At 1, or with travel off in the Autopilot tab, it goes home after each mission as the game does.

Below that is one switch per mission kind ECHO can be given: Eliminate, Reclaim Salvage Rights, Reclaim Asteroid Belt, Clear Pirate Base, Capture Enemy Vessel, Recover (item), Salvage (material), Mine (ore), Collect (ore) and Support (ore) Mining. A kind that's off is never offered to ECHO. ECHO only gets missions for its preferred activity, so only that activity's kinds matter: with every kind of that activity off (and no mission held), ECHO skips missions and goes on to its activities, whatever the other activities' switches say. A flagship without a preferred activity skips missions only when every kind is off. Courier missions (the Cargo activity) have no switch here and stay on the Autopilot tab's own one.

Clear Pirate Base is an installation raid: 3-5 minutes, credits and items found aboard, some crew lost, which ECHO hires again at the station. In two 30-minute test runs these paid far more than the other combat missions. Clear Pirate Base and Capture Enemy Vessel are also ruled out by the Autopilot tab's operations setting.

## ECHO retreat

![ECHO retreat thresholds](../../images/tab-echo-retreat.jpg)

`FlagshipHullPercent` ("Retreat when the flagship's hull is below (%)") and `FleetHullPercent` ("Retreat when a fleet ship's hull is below (%)"), 0-90, both 0 = off. While ECHO is on and the flagship's hull, or that of a fleet ship at the same location, drops below its threshold, ECHO stops fighting, skips the loot pickup and flies the fleet to the nearest friendly station (the home station when it's in this system). There it docks, waits for the hangar's free auto-repair (about 200 s; [Station services](stations.md#station-services) "Pay for repairs when you dock" buys the 20 s repair instead) and carries on. The status line reads "Retreating to Tower I for repairs: Eclipse hull at 20%".

It doesn't leave while an operation or boarding is running, from next to a station, or on a trip it's already making. When the Autopilot tab's travel is off, or no friendly station can be reached, ECHO stays and the status line says so.

Without it the flagship fights until the emergency jump at 0 hull, and every heavy hull hit can kill a crew member; the 10 autopilot sessions one save kept had 50 emergency jumps in 48 hours. Fleet ships already leave by themselves at 10% hull for two minutes of repairs, so `FleetHullPercent` only matters above 10.

## ECHO abilities and deployables

![ECHO abilities and deployables](../../images/tab-echo-kit.jpg)

A rule per flagship ability and per deployable type (from the flagship's cargo or the armory): Off, In combat when ready, Shields / Armour / Hull below N %, Targeted by at least N enemies, or Whenever ready. Each rule has a number box next to it (1-100, default 50) for N. All rules start off. The rules only run while the flagship is in space and not travelling, and the ability rows show while you're in space.

- Every rule except Whenever ready only fires in a fight: damage in the last 10 s, or a target.
- Untargeted abilities fire as they are. Hostile-targeted ones go on ECHO's target, friendly-targeted ones on your most damaged ship, asteroid ones (Crackshot Drone) on the asteroid the flagship mines, and wreck ones (LB-RTR, SF-CRK) on the nearest wreck whose cargo box or credits are still there. Drone-targeted abilities aren't automated.
- Toggle abilities are switched on while the rule holds, and off again (once the ability is ready) only if the rule switched them on.
- Deployables (turrets, shield pylons, decoys, mines) are dropped from the flagship's cargo, at most one per 2 s and only when the game's deploy cooldown allows.

`PickUpTurrets` ("Pick deployed turrets back up", on) tractors the turrets these rules dropped back in, 5 s after the fight and before ECHO leaves the location (it waits for them as it does for its own loot, also with "Pick up loot before leaving" off); turrets you placed yourself stay. `AlsoWithoutEcho` ("Also while you fly", off) applies the rules while you fly the ship yourself.

## Preferred ores

![Preferred ores with two ores listed](../../images/tab-echo-ores.jpg)

A list of ores and a mode, saved per save game. The mode dropdown reads "Off (nearest asteroid, game default)", "Prefer these ores" or "Only these ores"; ores are added from an "Add an ore" dropdown, listed by name and level and coloured by rarity, and adding the first one switches the mode from Off to Prefer.

- Prefer: ECHO, your ships' own asteroid picks and their mining drones take the nearest asteroid with a listed ore first, and ECHO picks a mining location in the system that still has one. With none left anywhere, mining works as usual.
- Only: other asteroids are left alone, and ECHO moves on to its next activity when no listed ore is left.
- Off (default), or an empty list, is the game's behaviour.

Core ores count only once you have the Basic Scanning mining skill, the point where the game's asteroid tooltip names them. The [Scanner](loot-and-cargo.md#scanner) marks the listed ores and can point edge markers at them.

## ECHO's operations

The stealth mode, ammo type and buyout choice for operations ECHO starts on its own. They're on the [Operations tab](stations.md#operations-tab), next to your own defaults. "Same as yours" (the game's behaviour) uses whatever you last picked yourself. `AutoBuyOut` lets ECHO accept defenders' offers to switch sides for credits when you can pay; the game never accepts them for ECHO.

## ECHO cargo rules

When ECHO unloads at a station, the dock action from the [Item rules tab](loot-and-cargo.md#item-rules) decides what happens to each stack: deposit as usual, keep it aboard, sell it, or deposit up to an amount and sell the rest. Favourite stacks are never touched, and neither are the ammo a ship fires and the usable items the game keeps aboard while ECHO unloads; the fleet auto-deposit below deposits those as the game does. Sell and Sell above N both leave the items your missions still need aboard.

ECHO sells before it deposits. Each unload step first looks for something to sell on the flagship and every fleet cargo ship, and only deposits once nothing is left to sell. For Sell above N, the part above N is sold right away and only N goes to storage. At a station without a shop nothing can be sold, so the game's own unload takes over once storage holds N and the rest is deposited chunk by chunk past N. A full material storage or armory then holds up only the deposits: the game's ECHO stops at the first stack that doesn't fit and never reaches the stacks behind it.

`ApplyToAutoDeposit` (on) covers the game's fleet auto-deposit too, which empties fleet holds the moment you dock: Keep aboard stays aboard, Sell stays aboard until ECHO unloads it, and Sell above N deposits up to N. When you dock yourself nothing is sold, unless Station services' "Sell by item rules when you dock" is on or you press the airlock's Sell by rules button. With that setting on, the fleet auto-deposit runs after the sale instead of at docking. Off: the game deposits everything as usual, so the rules only matter for fleet ships with auto-deposit off.

Data items (intel and records, blueprint pieces, jumpgate passes, claims) never sit in cargo: the game puts them straight into your data inventory. With `SellFromData` (on), a Sell or Sell above N rule that names the item or its category sells them from there, one stack per unload step, after the cargo sales and before any deposit. Sell above N keeps N in the data inventory. The default rule and the any-category "rarity and below" rules never sell from the data inventory, so a catch-all rule can't sell your blueprint pieces. Favourites and the items your missions need stay. The setting applies to Station services' selling too.

Material storage is kept per station. With `SellFromStorage` (off by default), the same rules (only those that name the item or its category) also sell what is already stored at the station you're docked at, one stack per unload step, after the data inventory and before any deposit. Sell above N keeps N in that station's storage, so a storage that already holds more than N is trimmed back to N. Favourites and the items your missions need stay. It's off by default because it sells stock you stored on purpose. The setting applies to Station services' selling too.

The armory holds the weapons, modules, boosters, ammo and usables you or ECHO stored, for every station at once. With `SellFromArmory` (off by default), the same rules (only those that name the item or its category) also sell from the armory, one stack per unload step, last: after cargo, the data inventory and station storage, before any deposit. Sell above N keeps N in the armory. On top of favourites and the items your missions need, the armory keeps the usables the game keeps aboard, warp fuel, every ammo type a ship of your fleet fires, and the exact pieces any saved [loadout](stations.md#ship-loadouts) uses. A piece of the same type and rarity that no loadout names can still be sold. It's off by default because it sells gear you stored on purpose. The setting applies to Station services' selling too.
