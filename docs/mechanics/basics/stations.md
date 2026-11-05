# Combat Stations

This page covers the physical stations you can shoot at from a new game: the parts they are built from, how tough each part is, what drops when it dies, what happens to the station and its owner when the last part goes, and the layouts of mission, pirate, skirmish, escort and boardable stations. Code was read from game version 0.8.3.4 (the decompiled source, not in-game testing) and re-checked against 0.8.3.5: the methods the code sections cite were re-read, and the 0.8.3.5 additions are marked. The base HP, module slots, weapon slots, tonnage and experience reward of each part come from the game's stored part templates (called prefabs). The decompiled source does not contain them, so they were re-read from the 0.8.3.5 data dumps and match what is listed here. Translation strings were not re-read for 0.8.3.5.

Terms and abbreviations are explained in the [glossary](glossary.md).

Other pages cover these station topics:

- What happens to a shot between the turret and the hull: [combat-damage.md](combat-damage.md).
- The general level and rarity curves: [scaling.md](scaling.md).
- Boarding a station's facility: [boarding.md](boarding.md).
- The stations of the faction war (hubs, Defense stations, the x20 and x30 hit point multipliers, the large conquest layouts and the off-screen siege numbers). These belong to the Conquest stage, which opens at level 28: [war-stations.md](../conquest/war-stations.md).

## Which Station Is Which

The game uses "station" and "outpost" for several different things, and the rules below differ by kind. This page uses these names:

| Name | What it is |
|---|---|
| economic outpost | A faction station that produces something: Mining, Salvage, Refinery, Forge, Production, Trading, Recruitment or Construction Yard. It is not a Defense station. |
| Industrial Outpost | A separate neutral map location type, shown by the industry board. It has nothing to do with economic outposts. See [Industrial Outpost stations](#industrial-outpost-stations). |

The war stations (hub, headquarters, Defense station) are named in [war-stations.md](../conquest/war-stations.md). NPC (non-player character) factions are the factions the game controls. A "location" below is anything you can fly to inside a system: a station, gate or asteroid field.

In the code: [Which Station Is Which](#code-which-station-is-which).

## Every Station Part

A station is a set of parts joined by physics joints. The tooltip title is the part type name, followed by the level ("Lv. N"), the owner, and a single "Hull" row. Each part also has a more specific name in the game's name list, such as "Small Turret Mk II (3-Way)", "Small Landing Pad" or "Energy Field Generator". A part without an entry in that list is named "{type} ({N}-Way)", where N is the number of connectors the part has. Where that longer name is shown is not verified.

"Base HP" (hit points; the game shows "Hull HP", "Armor HP" and "Shield HP") below is the unscaled starting value for each part. Shield and armor base HP only count when the part has a Shield Generator or Armor module equipped (every module slot is filled when the part spawns). A part's stored shield and armor values therefore do not always apply (see the notes under the table). Modules add their own HP on top before the multipliers in the next section.

| In-game name | Variant | Base HP | Module slots | Weapon slots |
|---|---|---|---|---|
| Station Core | | hull 900 | Hull, Scanner (Small) | none |
| Energy Field | | hull 1350 | Hull, Scanner (Small) | none |
| Refinery | ordinary | hull 1350 | Hull, Scanner (Small) | none |
| Refinery | escort mission | hull 1500 | Hull, Scanner (Small) | none |
| Cargo Dock | | hull 900 | Hull, Scanner (Small) | none |
| Docking Pad | Small and Medium pads | hull 900 | Hull, Scanner (Small) | none |
| Docking Tunnel | | hull 900 | Hull, Scanner (Small) | none |
| Connector | smaller | hull 315 | Hull, Scanner (Small) | none |
| Connector | larger | hull 735 | Hull, Scanner (Small) | none |
| Defense Platform | 1 Small turret | hull 450, shield 150 | Reactor, Hull, Scanner, Shield Generator (Small) | 1 Small |
| Defense Platform | 2 Small turrets, compact | hull 600, shield 300 | same slots, Small size | 2 Small |
| Defense Platform | 2 Small turrets, wider | hull 750, shield 420 | same slots, Small size | 2 Small |
| Defense Platform | 2 Medium turrets | hull 900, shield 900 | same slots, Medium size | 2 Medium |
| Defense Platform | 1 Large turret | hull 900, shield 900 | same slots, Large size | 1 Large |
| Defense Platform | drone bay | hull 600, armor 600 | Reactor, Hull, Scanner, Armor, Drone Bay (Small) | none (drones) |

Notes on the table:

- The Core has an armor value in its prefab, but it has no Armor slot, and base armor is only added when an armor module is equipped. The Core therefore has no armor of its own.
- The drone bay platform is classified as a Defense Platform by the game, so it shows that name even though it has no turret slots. Its armor is the only armor base HP on any part.
- Some parts (Core, Energy Field, the smaller Connector) have mounting points in the model, but no turret is ever equipped there.
- Each faction's stations use one of four art styles. Stellar Industries, Luminate Combine and Corsair Syndicate have their own style, and the Kolyatov Collective and every faction without its own use the generic one. In the 0.8.3.5 data dumps each style has the same 23 parts. The part type, hull, shield and armor values, module slots, weapon slot sizes, tonnage, experience reward and in-game name are identical across all four. Only the images and the connector links differ (read earlier from the prefabs, because the dumps do not list them). The art style changes how a station looks and nothing else.

In the code: [Every Station Part](#code-every-station-part).

### What Each Part Does in a Live Fight

Only Defense Platforms shoot. A part gets turret behaviour only when its prefab lists weapon slots. The Core, Energy Field, Refinery, Cargo Dock, Docking Pads, Docking Tunnels and Connectors are targets and nothing else. Every other part is only hull for you to destroy.

| Part | In a fight |
|---|---|
| Station Core | Always drops credits. Counts as a required part for station destruction, like any part that is not a Connector. In Station Defense missions it is the part the mission asks you to protect. |
| Energy Field | Has hull and drops loot only (see [The Energy Field](#the-energy-field)). |
| Refinery | Has hull and drops loot (ore, canisters, blueprints). |
| Cargo Dock | Has hull and drops loot (credits and refined parts). |
| Docking Pad, Docking Tunnel | Have hull and drop loot. They hold the docking points ships use, which matters only for boardable facilities. |
| Connector | Has hull and drops a little loot. It is never required for station destruction. |
| Defense Platform (turret) | Shoots, has the only shields on any part, and can drop one of its turrets. |
| Defense Platform (drone bay) | Carries a Drone Bay module and has no turret slots. It drops nothing from the platform loot table. This page does not cover how it launches or targets drones. |

In the code: [What Each Part Does in a Live Fight](#code-what-each-part-does-in-a-live-fight).

### The Energy Field

The Energy Field does nothing special. It is a 1350 base hull part with no weapons, no aura and no effect on the other parts. It drops warp fuel and, rarely, Energy Crystals. The part's stored data lists a hidden object that gives +0.75 weapon range (not verified, prefab data). The code never switches that object on for a station part: a part's stats come only from equipped modules and turrets (and saved stat boosts), and the range bonus the game does add is for ships only. The Energy Field does not extend turret range for any part.

In the code: [The Energy Field](#code-the-energy-field).

## How Tough a Part Is

### The Stat Formula

Each of hull, armor and shield HP is built the same way:

```
HP = (base HP + HP of equipped modules) * M
M  = rank * HP balance curve * NPC base curve * Escalation * context
```

- Base HP: the numbers in the table above.
- Rank: 0.7, because station parts always count as the lowest ship rank (Rookie), whose HP multiplier is 0.7.
- HP balance curve, for part level L: (1 + 0.28 * a) * (1 + 0.6 * b^1.5), where a = (L - 10) / 30 limited to the range 0 to 1, and b = (L - 40) / 60 limited to 0 or above. The game never shows this curve or gives it a name on screen. It is the extra HP every NPC unit gets as its level rises.
- NPC base curve: 4 raised to c, where c = (L - 30) / 70 limited to the range 0 to 1 (so the result is 1 at level 30 or below and 4 at level 100 or above). The game does not show this curve on screen either. It also multiplies the part's combat power (see "War Effort on Station Weapons" on [war-stations.md](../conquest/war-stations.md)).
- Escalation: the Escalation tier multiplier, which is 1 / 1.15 / 1.35 / 1.6 / 2.8 / 3.8 / 5 / 6.2 / 8 for tiers 0 to 8, times the per-save override (1 to 4). Damage uses its own table: 1 / 1.08 / 1.18 / 1.3 / 1.5 / 2 / 4 / 6 / 10.
- Context: x1, x10, x20 or x30 (see [Context Multiplier](#context-multiplier)).

The separate HP curve for NPC ships is only applied to ships, not to station parts. The general level curves are on [scaling.md](scaling.md).

L is the part's own level. When the part spawns, its equipment is built at the location's level (items roll within one level of it). The part level is then (rounded average item level + highest item level) / 2, rounded down, taken over the equipped modules and turrets. It therefore sits at or very near the location's level.

The combined multiplier at Escalation tier 0 (rank * HP balance curve * NPC base curve), before the context multiplier, is:

| Level | HP balance curve | NPC base curve | Combined |
|---|---|---|---|
| 10 | 1.00 | 1.00 | 0.70 |
| 30 | 1.19 | 1.00 | 0.83 |
| 45 | 1.30 | 1.35 | 1.22 |
| 60 | 1.43 | 1.81 | 1.81 |
| 80 | 1.70 | 2.69 | 3.20 |
| 100 | 2.05 | 4.00 | 5.73 |

The x20 and x30 columns and a worked example are on [war-stations.md](../conquest/war-stations.md).

The HP that equipped modules add is not listed because it comes from equipment rolls that I did not read. Treat the tooltip number as the truth and the base terms above as the minimum.

In the code: [The Stat Formula](#code-the-stat-formula).

### Context Multiplier

The context multiplier is chosen from where you are, not from who owns the part. It is looked up from the location you are at (or flying to) when stats are calculated.

| Where the part is | Multiplier |
|---|---|
| A hub or a faction Defense station in a war-zone system | x20, or x30 for a hub in a headquarters system (see [war-stations.md](../conquest/war-stations.md)) |
| A boardable facility (dungeon station) | x10 |
| Everything else: other faction stations (economic outposts such as Construction Yards and Refineries), neutral guild stations, skirmish and pirate stations, mission stations, Industrial Outpost stations | x1 |

In the code: [Context Multiplier](#code-context-multiplier).

### Module Gear and Weapons

Modules on a part are built at the location's level (the part's level is worked out from them afterwards), with the rarity fixed at High Grade. This overrides the normal rarity roll, so every station part has High Grade modules whatever the level. Two rules apply to everything the part equips (modules and turrets):

- Optional bonus stats can never roll Hull, Armor or Shield HP on that gear.
- The size scaling for HP stats is capped at Medium. A Large module's HP stat scales like a Medium one (x1.5) instead of x2.25. Other stats on the same Large module still use the full size scale.

For NPC parts, each module is chosen at random, with equal chance, among all modules of that slot and size. The module's level is not restricted.

Turrets are chosen by the weapon slot size:

- Each slot gets a combat turret of that slot's size, chosen at random with equal chance from turrets of any level.
- A part with two or more weapon slots has a 25% chance that one random slot is a Laser Painter (a support turret) instead of a gun, when a Laser Painter exists for that size.
- Turret rarity is set by a random number u between 0 and 0.4 + L/100, where L is the location's level. Below 0.2 is Standard, below 0.5 is Enhanced, below 0.8 is High Grade, below 0.95 is Exotic, and anything higher is Legendary.

| Location level | Standard | Enhanced | High Grade | Exotic | Legendary |
|---|---|---|---|---|---|
| 10 | 40% | 60% | 0% | 0% | 0% |
| 30 | 29% | 43% | 29% | 0% | 0% |
| 45 | 24% | 35% | 35% | 6% | 0% |
| 60 | 20% | 30% | 30% | 15% | 5% |
| 80 | 17% | 25% | 25% | 12% | 21% |
| 100 | 14% | 21% | 21% | 11% | 32% |

A given station always rolls the same gear. Damage-dealt and fire-rate scaling for the turrets is on [combat-damage.md](combat-damage.md).

In the code: [Module Gear and Weapons](#code-module-gear-and-weapons).

## What Drops When a Part Dies

Loot is rolled when the part is destroyed and differs on every kill. Level L is the part's level. In the amounts below, random(a, b) is a random whole number from a up to but not including b (if a equals b the result is a). A credit factor is a number the game turns into credits using the part's level L; a higher factor pays more. While Autopilot is on (the ECHO system, the "Toggle Autopilot" key), each non-fixed item has a 50% chance to be skipped.

| Part | Drop |
|---|---|
| Connector | 70% chance of 1 refined part (Titanium Plate, Bulkhead, Hull Beam or Structural Component). Above level 25, there is a 50% chance of a second one. Each amount is 1 + random(round(L/12), round(L/8)), halved (minimum 1) for Connectors. |
| Docking Pad, Docking Tunnel | The same refined-part roll as Connectors, without the halving. |
| Cargo Dock | The refined-part roll, plus a 70% chance of credits at a credit factor of 4.5 to 5.25. |
| Energy Field | 50% chance of High Grade warp fuel, in an amount of 1 + random(round(L/20), round(L/16)). Above level 25, a 4% chance of Energy Crystals, in an amount of 1 + random(round(L/45), round(L/20)). |
| Refinery | 50% chance of ore of this system: a surface ore in an amount of 1 + random(round(L/6), round(L/4)) and a core ore at half that amount (minimum 1). Within that 50%, there is another 50% chance of two further entries (see the note below the table). |
| Refinery and Core | Both also have a 30% chance of a blueprint of level L. 98.5% of the time, Exotic and higher blueprints are excluded. |
| Station Core | Always credits at a credit factor of 13.5 to 15.25. |
| Defense Platform with turrets | 40% chance of one of its equipped turrets (only turret types that players can use), rebuilt at its exact level and rarity. 55% of the time, the choice is limited to the lowest-rarity turrets it carries. Separately, a 20% chance of a stack of one of its ammo types worth about a credit factor of 1 or 2, capped at the amount it holds. |
| Defense Platform with a drone bay | Nothing from this table. Fixed loot can still be placed on it. |
| Fixed loot on the part | Always dropped, and never skipped while Autopilot is on. Station missions and some skirmish and pirate stations add a pair of gear drops and a skill-point item here (see [Station Layouts](#station-layouts)). |

Games using the sandbox storyteller add these drops to the Core: an 80% chance of 1 to 2 Locked Container Keys and, in a conquest system, also a Locked Container of the part's level.

Note on Refineries: I could not tell whether the two extra entries are canisters or bulk core ore (not verified). The decompiled code gives core ore in both, which may be a decompiler artifact. Treat them as "bulk core ore, possibly canisters" until they are checked in play.

Killing a part also pays kill experience: 50 * level-gap factor * 2^(effective level / 9), rounded up. The 50 is 5 x the part's reward of 10, and the level-gap factor is the usual penalty or bonus for the level gap (see [scaling.md](scaling.md)). The Rookie rank (the lowest ship rank) adds no extra multiplier. Industrial Outpost stations give no loot or experience and cause no reputation change.

In the code: [What Drops When a Part Dies](#code-what-drops-when-a-part-dies).

## What Counts as Destroyed

- A part is destroyed when its hull HP reaches 0. Shields and armor only soak damage first; they do not remove the part (the exact order is on [combat-damage.md](combat-damage.md)).
- A destroyed part is removed from the station's saved data until the station is rebuilt (see [How Long Damage Lasts](#how-long-damage-lasts)). Partial damage is saved and carries over to your next visit.
- There is no cascade. Killing a part does not kill, detach or weaken any neighbour. A destroyed part's joints go with it, so what hung off it is released and can drift. That is a physics effect, not a destruction rule.
- When a part is destroyed by damage (not removed some other way), it releases a shockwave. Every unit within 8 units (ships, drones and other station parts) is pushed. The force is (8 - distance) * 0.6 * 1500, with the distance limited to between 1 and 8, so it is 6300 at 1 unit or closer and 0 at 8 units. The push arrives 0.62 seconds later and does not damage those units. Mines and loot containers in range are different: anyone can damage them, and each takes 10 points of damage from the shockwave. Since 0.8.3.5 the part also plays a shockwave explosion effect, which is visual only.
- The station counts as destroyed when every part that is not a Connector is gone. Connectors can survive, and the station still counts as destroyed.
- If you hit a part at any point before it dies, the station counts as having your contribution once that part dies. See the next section for what that changes.

In the code: [What Counts as Destroyed](#code-what-counts-as-destroyed).

### When the Last Part Dies

All of this runs once for the whole station:

1. Mission objectives such as "Destroy the enemy station" complete.
2. If the station is a faction station that is not already wrecked and you contributed, it is wrecked on the spot (next section).
3. In a pocket system, the achievement for a pirate or warzone pocket is awarded.

The extra steps that run when the station is the hub of a war-zone system, or a Defense station under a live siege, are on [war-stations.md](../conquest/war-stations.md).

```mermaid
---
config:
  flowchart:
    wrappingWidth: 420
---
flowchart TD
    A["Last non-Connector part dies"]
    B["Mission objectives complete"]
    C["Faction station?<br/>not a hub or guild station"]
    D["Station is not wrecked"]
    E["Did you contribute<br/>to the kill?"]
    F["Wrecked on the spot<br/>Builds are refunded and 10% of storage is dropped<br/>Reputation drops by 5000 (by 1000 if already Hostile)"]
    G["Station is removed<br/>No loot drops and no reputation cost<br/>(a salvage site can still remain)"]
    H["When you leave, the station location is removed"]
    I["Salvage site<br/>Wreckage of the station for 3600 s<br/>(only if materials or drops remain)"]
    A --> B --> C
    C -->|"no"| D
    C -->|"yes"| E
    E -->|"yes"| F --> H --> I
    E -->|"no, for example a siege"| G
```

In the code: [When the Last Part Dies](#code-when-the-last-part-dies).

### Wrecking a Faction Station

Wrecking applies to faction stations (Defense stations and economic outposts such as Construction Yards, refineries and production stations), not to hubs or guild stations.

- If you contributed, the wrecking steps run when the last part dies. Player builds queued at that station are refunded. 10% of each item stack in the station's faction storage (rounded down per stack) is dropped as loot at the destroyed part's position, and the storage is emptied. The garrison is cleared and the station is marked wrecked. Your reputation with the owner drops by 5000, or by 1000 if you are already Hostile or worse with the owner (reputation -5000 or lower). The event log reads "Station destroyed! -N {faction name} reputation."
- Wrecked stations stop running jobs. When you leave, the station location is removed. If any recoverable materials (10%) or dropped items remain, a "Wreckage of {station}" salvage site is created that lasts 3600 seconds (1 hour).
- Without your contribution (for example when a conquest siege destroys it), the same removal happens another way. Builds are refunded, an Empire Log entry is posted, and the station is removed or marked wrecked depending on whether you are there. Nothing is dropped at the part's position and there is no reputation cost. The storage is not emptied, so when the station is removed it can still leave the same 3600 second salvage site holding 10% of what it stored.
- Destroying a Construction Yard does not cancel the ships its faction has already queued. That is covered on [faction-economy.md](../conquest/faction-economy.md) (Conquest stage).

In the code: [Wrecking a Faction Station](#code-wrecking-a-faction-station).

## Reputation and Force Attacks

### Killing Parts

Every part you kill hits the owner's reputation, unless the part is exempt (several mission stations are). This applies to every kind of station: hubs, Defense stations and economic outposts alike. Reputation bands (Wary, Neutral and so on) are listed in the [glossary](glossary.md#reputation).

- Each part has a base reward of 10, so the base penalty is ceil(10 * 0.5) = 5 (10 * 0.5, rounded up).
- The penalty depends on your reputation with the owner:
  - Above -500 (Neutral or better), the penalty is multiplied by 20. Each part then costs 100 reputation, and the event log reads "Betrayal! -100 {faction name} reputation." (for example "Betrayal! -100 Luminate Combine reputation.").
  - At -500 or below (Wary or worse), each part costs 5.
  - Below -500 (Wary, except exactly -500, and everything worse), the faction also counts as your enemy, as it does when you are at war with it. Its parts can then be targeted with a plain click.
- Reputation also moves with a second faction. If the faction that owns the system you are in is an enemy of the part's owner, and your reputation with that system's faction is Neutral (above -500 and below 1500), your reputation with that system's faction rises by 1 for each part you kill. The 1 is the base penalty of 5 times 0.2, rounded up. It stays 1 even when the part's owner costs you 100, because the game works it out before the x20.
- A part counts as your kill if you hit it at any point before it died, even if something else landed the finishing hit.
- One late-game item cancels this loss and the wreck loss below while it is active (until you next dock at a space station). It is covered in the [late-game pages](../late-game/README.md).

In the code: [Killing Parts](#code-killing-parts).

### Ctrl-Click on a Friendly Station

Left-clicking a hostile part sets it as your manual target. Ctrl + left-click on a part whose faction is not hostile to you (the tooltip hint "force attack") works only when all of these are true:

- The part is not already hostile to you.
- You are in a subsector of the Conquest quadrant or of a quadrant beyond it. The Frontier quadrant and the prologue never allow it. The galaxy map button names the neighbouring quadrant, for example "< Frontier" and "Conquest >".
- The location you are in is not one of the space station kinds.

Faction stations (Defense stations and economic outposts), hubs, embassy stations, player stations, Industrial Outposts and every guild or neutral station are space station kinds, so you cannot force attack any of them. The rule lets you force attack non-hostile parts at combat-type locations (mission, skirmish and pirate stations, and boardable facilities). Embassy station parts are also always friendly, so normal targeting does not reach them either.

```mermaid
---
config:
  flowchart:
    wrappingWidth: 260
---
flowchart TD
    A["Ctrl + left-click a part"]
    B["Already hostile<br/>to you?"]
    C["Plain click already works"]
    D["Sector in a quadrant<br/>above 1?"]
    E["Location is a space<br/>station kind?"]
    F["No force attack"]
    G["Part becomes hostile to you<br/>and kills cost reputation"]
    A --> B
    B -->|"yes"| C
    B -->|"no"| D
    D -->|"no"| F
    D -->|"yes"| E
    E -->|"yes"| F
    E -->|"no"| G
```

When you do attack a non-hostile part, it becomes hostile to you. Any damage you deal to a unit whose faction is not your enemy also marks every unit of that faction at that location hostile to you. The reputation cost is the kill penalty above. A part that is already hostile to you (a faction at war with you, or a flagged unit) can be targeted with a plain click.

Since 0.8.3.5 a plain left-click on a part docks you at the station hub when the part's faction owns the station you are at, docking is possible right now and the station has a hub to dock at (the tooltip then shows a "dock" hint). Boardable facility parts open the facility panel instead.

In the code: [Ctrl-Click on a Friendly Station](#code-ctrl-click-on-a-friendly-station).

## Station Layouts

Which layout a station gets depends on how it was created. This page lists the layouts of the stations you meet outside the war. The Small, Medium and Large conquest layouts (hubs and faction Defense stations) and the rule that picks them are on [war-stations.md](../conquest/war-stations.md).

| Layout | Used for | Parts |
|---|---|---|
| Medium station | Solo medium stations, faction skirmish locations, Normal Station Offense and Defense missions | 14 parts |
| Large station | Pirate hideouts, the pirate station in the conquest world, Hard to Insane Station Offense and Defense missions | 18 parts |
| Easy 1 | Easy Station Offense and Defense missions | 9 parts |
| Easy 2 | Easy Station Offense and Defense missions | 10 parts |
| Escort location | Escort locations | 6 parts |
| Gun platform | Decorative platforms placed at locations ("window dressing" in the code), Industrial Outpost stations | 3 or 4 parts |

The faction picks the artwork: Luminate Combine, Stellar Industries and Corsair Syndicate each have their own, and every other faction uses the generic one.

In the code: [Station Layouts](#code-station-layouts).

### Part Lists

Connector counts are in the last column. "Platforms" counts Defense Platforms, drone bay platforms included.

| Layout | Core | Energy | Refinery | Cargo Dock | Docking Pad (Small) | Tunnel | Platforms | Connectors |
|---|---|---|---|---|---|---|---|---|
| Medium station | 1 | 1 | 0 | 0 | 1 | 0 | 3 | 8 |
| Large station | 1 | 1 | 0 | 0 | 2 | 0 | 6 | 8 |
| Easy 1 | 1 | 1 | 0 | 0 | 0 | 0 | 2 | 5 |
| Easy 2 | 1 | 1 | 0 | 0 | 1 | 0 | 2 (one is a drone bay) | 5 |
| Escort location | 0 | 0 | 1 (Escort Refinery) | 0 | 0 | 0 | 2 (one is a drone bay) | 3 |
| Gun platform, variant 1 | none | none | none | none | none | none | 1 | 2 |
| Gun platform, variant 2 | none | none | none | none | none | none | 3 (one is a drone bay) | 0 |
| Gun platform, variant 3 | none | none | none | none | none | none | 2 | 2 |

The gun platform variant is chosen at random: a roll above 0.666 gives variant 1, a roll between 0.333 and 0.666 gives variant 2, and a roll below 0.333 gives variant 3. The turrets in these layouts are fixed platforms and never change with level.

In the code: [Part Lists](#code-part-lists).

### Extra Loot on Mission and Skirmish Stations

- Faction skirmish locations (medium station): one random piece of equipment that is not a weapon or booster, and one random turret. Both are at the location's level, with rarity rolled from the level and never lower than High Grade. The two items go on random parts other than Connectors. The Core also gets a bonus skill point item.
- Pirate hideouts, the pirate station in the conquest world, and Hard to Insane Station Offense and Defense missions (large station): the same two items plus the skill point item, but the rarity is High Grade. Above level 20 there is a 5% chance of Exotic. Above level 30, a further 10% of the remaining chance becomes Exotic, for a total of 14.5% at level 31 or higher.
- Station Offense and Defense missions pick the layout from the difficulty: Easy gets Easy 1 or 2, Normal the Medium station, Hard and above the Large station. In a Defense mission the station is player-friendly and its Core is the protected subject.

In the code: [Extra Loot on Mission and Skirmish Stations](#code-extra-loot-on-mission-and-skirmish-stations).

## How Long Damage Lasts

Damage and kills are saved, but stations are rebuilt from their seed in some situations, which restores every part at full HP. The one that applies everywhere:

- When more than 3600 seconds (1 hour) of game time have passed since you last visited a given space station (a faction station, hub, embassy, player station, Industrial Outpost or guild station), that station is rebuilt the next time the game saves. Combat locations such as mission, skirmish and pirate stations do not use this rule; part damage on them is not reset by it.

Two more rebuild triggers apply to war-zone stations only and are on [war-stations.md](../conquest/war-stations.md). Guard ships at combat locations are regenerated after 7200 seconds away, which does not rebuild the station parts.

In the code: [How Long Damage Lasts](#code-how-long-damage-lasts).

## Industrial Outpost Stations

An Industrial Outpost (the map location type shown by the industry board) is a separate, neutral location. It is not an economic outpost: economic outposts are the faction production stations. The Industrial Outpost is built as the Easy 1 layout plus a variant 1 gun platform. Its parts get no context HP multiplier and give no loot, kill experience or reputation change. The physics engine treats them as fixed in place (kinematic), so they cannot be pushed. The mission fails when the Core is destroyed.

The part tooltip adds an "Industrial Outpost" section:

| Row | Meaning |
|---|---|
| Repair packs | The repair pool as a percentage of one pack. It starts at 100%, and each Repair Pack from storage adds 100%. One pack is 6000 HP of repair, scaled by the damage multiplier of the outpost's starting level. It is shown in red below 10%. |
| Ammo stockpile | The ammo reserve. It starts at 100%, and each Ammo Pack adds 100%. Every enemy wave drains 10 points plus 5 points per filled turret slot. At 0% all turrets stop firing. It is shown in red below 25%. |
| Turret slots | The number of filled turret slots, out of 4. |

Every 0.2 seconds (one tick), the Industrial Outpost repairs the armor, then the hull, of each part by up to 10% of that part's maximum, paid from the repair pool. Each empty turret slot (at the offsets (20,-5), (20,5), (14,-10) and (14,10) from the location) pulls one Defensive Turret Pack from storage and spawns a gun platform, one per tick. A new wave of marauders (or fanatics) spawns when no enemies are left or 120 seconds have passed. Wave strength is 0.7 + 0.1 * the wave number. The first wave is delayed by 45 seconds and later waves by 15 seconds.

In the code: [Industrial Outpost Stations](#code-industrial-outpost-stations).

## Boardable Facilities

A facility station (called a dungeon in the code) is built from Cores, Refineries, Energy Fields, Cargo Docks, Docking Pads, a Docking Tunnel and Connectors, with no weapons. Its parts have the x10 HP multiplier, but projectiles, rail cannons, torpedoes and manual drone targeting all ignore them, and your combat modules do not pick them as targets. I did not find a weapon path that damages them. Their numbers are on [boarding.md](boarding.md). The rules that concern the parts are:

- Facility integrity is the share of live non-connector parts (live parts / total parts), not a share of HP.
- You can enter a facility only if no docking part has been destroyed for good and its integrity is above 10%.
- When the last Docking Pad or Docking Tunnel dies, the facility can no longer be entered. Losing the docking point of an active operation aborts the operation.
- When a saved facility with integrity below 100% loads, each part other than the first has a chance of 1 - integrity of being removed. The same facility always loses the same parts, and the remaining parts show the same damage each time.
- Damage from the boarding fight is visual only. It breaks the part's sprite and does not change HP.

In the code: [Boardable Facilities](#code-boardable-facilities).

## What to Do With This

Connectors, Docking Pads, Docking Tunnels, Cargo Docks, Energy Fields, Refineries and the Core are pure HP: they never shoot. Their only reasons to die are loot and the final destruction check.

If you have to win the fight, kill the parts in this order:

1. Defense Platforms, because they are the only source of damage on the station. Platforms with Small turrets have the thinnest shields and go down fastest. Platforms with Medium or Large turrets carry 900 base shield, three times the 300 of a compact two-Small-turret platform. That shield regenerates, so burst these platforms down instead of sniping at them.
2. Among the platforms, kill the ones with the strongest gear first when you can tell. From level 80, a third or more of the turrets are Exotic or Legendary.
3. Everything else, in whichever order is convenient. You never have to kill Connectors.

If the goal is loot, shoot these parts:

| Goal | What to shoot |
|---|---|
| Credits | The Core (always), then Cargo Docks (70% chance). |
| Turrets | Defense Platforms (40% chance of one of their turrets). |
| Blueprints | Cores and Refineries (30% chance each). |
| Ore and canisters | Refineries. |
| Warp fuel | Energy Fields. |
| Crafting parts (refined parts) | Connectors, Docking Pads, Docking Tunnels and Cargo Docks (70% chance each). |

If the goal is to keep your reputation, do not kill the last part that is not a Connector on a faction station once any part you hit has died on it.

The 5000 point loss and the wreck happen when the last part dies and at least one part you hit has been destroyed on that station. Leaving one part alive avoids the 5000 point loss. The per-part penalty applies to every part you kill either way. It is 100 points a part while you are Neutral or better with the owner. If you are already Hostile or worse with the owner, the final penalty is 1000 and each part costs 5.

Two things do not work the way they look:

- Killing Connectors does not count toward destroying the station. It can release the parts hanging off that Connector to drift, which is a physics effect and not damage.
- The Energy Field is not an energy source for the station's weapons or a range booster. It is only a part with 1350 base hull.

## Not Verified

- The HP added by equipped modules (Hull, Shield Generator, Armor) and the exact turret statistics. They come from equipment rolls that I did not read.
- Whether the extra two Refinery drops are canisters or core ore (the decompiled output yields core ore).
- Whether station parts are included in the list of units that get marked hostile when you attack a friendly part.
- Whether drone-bay platforms launch drones at all. A part only gets combat behaviour if its prefab lists weapon slots or the part has set auto actions, and the drone-bay platform has neither (not verified), so its damage is unknown.
- Which quadrant the game numbers 3 belongs to. The code names it the test arena, and no map button for it was checked.
- The range bonus object on the Energy Field. It is stored in the prefab, and neither the C# source nor the 0.8.3.5 data dumps list it.
- Where the longer part names (such as "Small Turret Mk II (3-Way)") are shown in the interface.

In the code: [Not Verified](#code-not-verified).

## In the Game Code

Code was read from the decompiled source of version 0.8.3.4 and re-checked against 0.8.3.5 (the cited methods that changed were re-read and the page was corrected). The part values come from the prefab data, which the 0.8.3.5 data dumps list. Translation strings were not re-read for 0.8.3.5. The sections below give the class and method names, the raw expressions and the prefab names behind each claim, in the same order as the page. To check a claim, find the method in a decompiler (the `Source:` lines name where to look) and read it.

### Code: Which Station Is Which

| Name | Code name |
|---|---|
| economic outpost | `FactionStation` that is not a Defense station |
| Industrial Outpost | `IndustryStation` |

A "location" is the code's point of interest, POI.

### Code: Every Station Part

A station is a set of `CombatStationPart` objects joined by physics joints. The tooltip title is the part type name (`@StationPart<type>`), followed by the level (`Lv. N`), the owner, and a single `Hull` row. `CombatStationPart.GetDisplayName` looks up `@StationPartName_<prefab key>` and falls back to `{type} ({N}-Way)`, where N is the number of connectors the part has; every prefab on this page has an entry in the translation table.

`Base HP` is the unscaled starting term: hull is `hullHPScale * 150`, shield is `shieldHPScale * 300`, armor is `armorHPScale * 300` (`AbstractUnit.baseHullHP` and the related properties). The scale values themselves are prefab data, read from the 0.8.3.5 data dump (`CombatStationPart.allParts`): hull scale 6 for the Core, Cargo Dock, Docking Pads and Docking Tunnel, 9 for the Energy Field and Refinery, 10 for the escort Refinery, 2.1 and 4.9 for the Connectors, 3 to 6 for the Defense Platforms with turrets, and 4 for the drone bay platform. Shield scale is 0.5, 1, 1.4 and 3, and the only armor scales are 3 (Core) and 2 (drone bay platform). Shield and armor base HP only count when a Shield Generator or Armor module is equipped (`AbstractUnit.GetStatRaw`), so a prefab's shield and armor scale is not always live (see the notes below). Prefab names, without the faction prefix:

| Part | Prefab | Variant on this page |
|---|---|---|
| Station Core | `Core_4C` | |
| Energy Field | `Energy1_4C` | |
| Refinery | `Refinery1_2C` | ordinary |
| Refinery | `EscortPartRefinery1_2C` | escort mission |
| Cargo Dock | `Conq_CargoDock_4C` | |
| Docking Pad | `DockingSmall_2C`, `Conq_DockingSmall_2C`, `Conq_DockingMedium_2C` | Small and Medium pads |
| Docking Tunnel | `DockingTunnel_1C` | |
| Connector | `Connector_1C`, `Connector_1CSmall` | smaller |
| Connector | `Connector_2C`, `_2CSmall`, `_3C`, `_4C` | larger |
| Defense Platform | `DefenseTurretS1_1C` | 1 Small turret |
| Defense Platform | `DefenseTurretS2_1C` | 2 Small turrets, compact |
| Defense Platform | `DefenseTurretS2_2C`, `_2CSlim`, `_3C` | 2 Small turrets, wider |
| Defense Platform | `DefenseTurretM2_1C` | 2 Medium turrets |
| Defense Platform | `DefenseTurretL1_1C` | 1 Large turret |
| Defense Platform | `DefenseDronebaySmall_2C` | drone bay |

Notes on the table:

- The Core prefab has `armorHPScale` 3, but it has no Armor slot, and the base armor term is only added when an armor module is equipped. The Core therefore has no armor of its own.
- The `DefenseDronebaySmall_2C` prefab is typed as a Defense Platform in game, so it shows that name even though it has no turret slots. Its armor is the only armor base HP on any part.
- Some prefabs (Core, Energy Field, `Connector_1C`) have child objects named "Hardpoint" in the prefab. They are not listed in the part's `hardpointSlots`, so no turret is ever equipped there.
- The number before the `C` in a prefab name is not reliable as a connector count. The serialized connector list is rebuilt from the child objects at load, and for example `Energy1_4C` has four.
- In 0.8.3.5 the dedicated prefixes are `Generic_` (Kolyatov Collective and every faction without its own prefix), `Stellar_` (Stellar Industries), `Luminate_` (Luminate Combine) and `Corsair_` (Corsair Syndicate). Each has the same 23 prefabs in the `CombatStationPart.allParts` dump. The part type, hull, shield and armor scale, module slots, weapon slot sizes, tonnage, experience reward and display name are identical across all four prefixes. The sprites and connector references were found to differ when the prefabs were read earlier; the dump does not list them. The prefix changes how a station looks and nothing else.

Source: `CombatStationFactory.GetFactionPrefix`, `AbstractUnit.baseHullHP`, `AbstractUnit.baseShieldHP`, `AbstractUnit.baseArmorHP`, `AbstractUnit.GetStatRaw`, `CombatStationPart.BuildTooltipCard`, `CombatStationPart.GetDisplayName`, prefab data for `*_Core_4C` and the other `StationParts`.

#### Code: What Each Part Does in a Live Fight

A part gets turret behaviour (`CombatActions`) only when its prefab lists weapon slots. In Station Defense missions the Core is set up as the "defense subject" the mission protects.

Source: see [the Energy Field](#code-the-energy-field).

#### Code: The Energy Field

Its code-side role is the loot table (warp fuel, rarely Energy Crystals). The prefab data lists a child object called `RangeBonus` with a `BoostStat` of +0.75 on `WeaponRange` (not verified, prefab data). Nothing registers it as a stat source for a station part: a part's stats come only from equipped modules and turrets (and saved stat boosts), because only equipment and ship upgrades register themselves as stat sources. `AbstractUnit.AddRangeBonus` only adds a range badge to `SpaceShip` units. It does not extend turret range for any part.

Source: `CombatStationPart.CreateAutoActions`, `CombatStationPartData.CreateLootTable`, `AbstractUnit.RegisterStatSource`, `AbstractEquipment` (the only registrant), `BoostStat`.

### Code: How Tough a Part Is

#### Code: The Stat Formula

```
HP = (base term + sum of equipped module HP) * M
M  = rankHP * HpBalance(level) * NpcBase(level) * escalationHP * stationContext
```

- `base term`: the numbers in the table above.
- `rankHP`: 0.7. Station parts are never given a rank in code, so they keep the default rank (Rookie), whose HP multiplier is 0.7.
- `HpBalance(level)`: `GameMath.HpBalanceMultiplier`, `(1 + 0.28 * clamp01((L-10)/30)) * (1 + 0.6 * max(0,(L-40)/60)^1.5)`. It is the "HP balance curve" of the player text. No localization key names it.
- `NpcBase(level)`: `EscalationHelper.GetNpcBaseMultiplier`, `4 ^ clamp01((L-30)/70)`. The peak of 4 is fixed: `EscalationHelper.basePeakOverride` is never set. It also multiplies the part's combat power. It is the "NPC base curve" of the player text. No localization key names it.
- `escalationHP`: the Escalation tier multiplier, 1 / 1.15 / 1.35 / 1.6 / 2.8 / 3.8 / 5 / 6.2 / 8 for tiers 0 to 8, times the per-save override (1 to 4). Damage uses its own table, 1 / 1.08 / 1.18 / 1.3 / 1.5 / 2 / 4 / 6 / 10.
- `stationContext`: x1, x10, x20 or x30.

The NPC ship HP curve (`NpcShipHpCurve`) is only applied to ships, not to station parts.

`L` is the part's own level. At spawn the part's equipment is built at the location's level (`BasePoiManager.AddToWorld`, items roll `level + random(-1, 2)`), and the part level is then `(round(average item level) + highest item level) / 2` with whole-number division, via `AbstractUnitData.RecalculateLevel`. Rank, NPC scaling and escalation are skipped for player-faction units; station parts carry their location's faction.

Since 0.8.3.5, when a system changes owner, `SystemMapData.UpdateStationPartFactions` also moves the saved station parts (`CombatStationPartData`) of the old owner to the new owner.

The "Combined" column of the level table is `rankHP * HpBalance * NpcBase` at escalation tier 0, before the context multiplier.

Source: see [context multiplier](#code-context-multiplier).

#### Code: Context Multiplier

`CombatStationPart.GetStationHpMultiplier` returns the multiplier and `CombatStationPart.AddStatModifiers` applies it to the NPC health stats. `CombatStationPart.GetConquestDefenseSystem` finds the war-zone system for the x20 and x30 cases, and a boardable facility (`dungeonLocationData` set) gets x10. The location it checks is `MapPointOfInterest.currentOrNext`.

Source: `CombatStationPart.GetStationHpMultiplier`, `CombatStationPart.GetConquestDefenseSystem`, `CombatStationPart.AddStatModifiers`, `AbstractUnit.RecalculateStats`, `AbstractUnit.ApplyNpcRankModifiers`, `AbstractUnit.ApplyHpBalanceBonus`, `AbstractUnit.ApplyEscalationNpcModifiers`, `GameMath.HpBalanceMultiplier`, `EscalationHelper.GetNpcBaseMultiplier`, `UnitRankHelper.GetHpMultiplier`, `AbstractUnitData.RecalculateLevel`, `BasePoiManager.AddToWorld`.

#### Code: Module Gear and Weapons

Station module rarity is `CombatStationPartData.fixedModuleRarity` (HighGrade), built at the location's level with `exactLevel` false. Two rules in `StationGearRollRules` apply to everything the part equips (modules and turrets): `excludedBonusStats` is the hull, armor and shield HP set, and `hpStatSizeCap` is Medium (`EquipmentBuilder.GetStatSize`, size multipliers 1.5 for Medium and 2.25 for Large).

For NPC parts the module is a uniform random pick among every builder of that slot and size, with no level gate (`EquipmentBuilder.GetRandom` only filters by level for player-owned units). Each turret slot gets a uniform random combat turret builder of that slot's size that is not a support turret. The Laser Painter swap comes from `DetermineSupportSlotIndex`: 25% for a part with at least two hardpoints, then a random slot.

Turret rarity is rolled from `u = random(0, 0.4 + level/100)`, where `level` is the location's level passed to `LoadDefaultEquipment`: below 0.2 Standard, below 0.5 Enhanced, below 0.8 HighGrade, below 0.95 Exotic, else Legendary.

The equipment seed is the location id, the station seed and the part's position in the build order, so a given station always rolls the same gear.

Source: `CombatStationPartData.fixedModuleRarity`, `CombatStationPartData.StationGearRollRules`, `AbstractUnitData.LoadDefaultEquipment`, `AbstractUnitData.EquipDefaultModuleSlots`, `AbstractUnitData.EquipDefaultHardpoints`, `AbstractUnitData.GetSupportTurretFilter`, `AbstractUnitData.DetermineSupportSlotIndex`, `AbstractUnitData.GetDefaultEquipmentRarity`, `EquipmentBuilder.GetRandom`, `EquipmentBuilder.GetStatSize`, `CombatStationData.GetEquipmentSeed`.

### Code: What Drops When a Part Dies

Loot is rolled when the part is destroyed, using the global random generator (`SeededRandom.Global` inside `CreateLootTable`), so it differs on every kill. Integer random ranges are upper-bound exclusive and return the lower bound when both ends are equal (`SeededRandom.RandomRange(int, int)`). The Autopilot skip roll (`GamePlayer.autoPlay`) in `LootManager.DropRegularLoot` uses a different generator, seeded from the location and the part (`GetKillLootSeed`), so for a given part and item position its outcome repeats. Raw expressions (L is the part's level):

| Part | Expression |
|---|---|
| Connector, Docking Pad, Docking Tunnel (refined part amount) | `1 + random(round(L/12), round(L/8))`, halved (minimum 1) for Connectors |
| Cargo Dock (credits) | `GetCreditsValue(random(4.5, 5.25), L)` |
| Energy Field (warp fuel) | `1 + random(round(L/20), round(L/16))` |
| Energy Field (Energy Crystals) | `1 + random(round(L/45), round(L/20))` |
| Refinery (surface ore) | `1 + random(round(L/6), round(L/4))` |
| Station Core (credits) | `GetCreditsValue(random(13.5, 15.25), L)` |
| Defense Platform (ammo stack) | `GetCreditsValue(1 or 2, L)` |

Kill experience is `ceil(50 * gapFactor * 2^(effectiveLevel/9))`, where the base is 5 x the part's reward of 10 (`AbstractUnit.baseExperienceReward`, default 10), the gap factor is the usual level-gap penalty or bonus, and `UnitRankHelper.GetAdjustedPoints` is 1 for the default Rookie rank. Only turrets whose builder passes `EquipmentBuilder.AvailableForPlayer` can be picked as the dropped turret. Loot and kill rewards (experience, reputation) are skipped at an Industrial Outpost (`LootManager.DropLoot`) and the reward part only runs when `hitByPlayer` is set.

Note on Refineries: the decompiled code looks up the canister item for the surface and core ore and then yields the core ore item (x4 and x2 the rolled amount) in both extra entries, not the canisters. This may be a decompiler artifact. Treat the extra two entries as "bulk core ore, possibly canisters" until checked in play.

Source: `CombatStationPartData.CreateLootTable`, `AbstractUnitData.CreateLootTable`, `AbstractUnitData.FilterLoot`, `LootRarity.SelectDroppableEquipment`, `LootManager.DropLoot`, `LootManager.DropRegularLoot`, `LootManager.AddKillExperience`, `GameMath.GetExperienceRewardValue`.

### Code: What Counts as Destroyed

Parts are joined by joints that sit on the parent part; a destroyed part's joints go with it, so what hung off it is released and can drift. That is a physics effect read from the joint wiring, not a destruction rule. The shockwave force is `(8 - clamp(distance, 1, 8)) * 0.6 * 1500` on every `AbstractUnit` within `explosionImpactRange` 8. Other `TargetableUnit` objects with `damageableByAll` take a flat `damageAmount` of 10 (`PhysicsInteraction.ApplyShockwaveToNearbyShips`). Only `Mine` and `LootContainer` override `damageableByAll` to true, so those are the objects meant by "anyone can damage". It runs only when hull HP is at or below 0 and `damageTaken` is above 0. `RecordPlayerContribution` sets the station's `playerContributedToDestruction` flag when a part with `hitByPlayer` dies at a `FactionStation`.

Since 0.8.3.5, `CombatStationPart.OnDestroy` also calls `PlayDestroyedShockwave`, which plays `EffectManager.PlayShockwaveExplosionEffect` with size `max(0.6, GetBoundsX() / 8)`. This is a visual effect only. The push still passes the scale 0.6 to `ApplyShockwaveToNearbyShips` (the new constant `DestroyedPushScale` is declared with the same value), and `PhysicsInteraction` is unchanged.

Source: `CombatStationPart.OnDestroy`, `CombatStationPart.CheckCombatStationFullyDestroyed`, `CombatStationPart.RecordPlayerContribution`, `CombatStationData.CleanupParts`, `CombatStationData.ConnectPartPhysics`, `PhysicsInteraction.ApplyShockwaveToNearbyShips`.

#### Code: When the Last Part Dies

1. The mission trigger `CombatStationDestroyed` fires, which completes "Destroy the enemy station" objectives.
2. If the station is a faction station that is not already wrecked and you contributed, it is wrecked on the spot.
3. In a pocket system, the achievement for a pirate or warzone pocket is awarded.

#### Code: Wrecking a Faction Station

Wrecking applies to `FactionStation`, not to hubs or guild stations. If you contributed, `FactionStationDestruction.OnDestroyedByPlayer` runs when the last part dies. Without player contribution (for example a conquest siege kills it), the same removal happens through `FactionStationDestruction.Destroy`.

Source: `CombatStationPart.OnFactionStationDestroyed`, `CombatStationPart.WreckFactionStation`, `FactionStationDestruction.Destroy`, `FactionStationDestruction.OnDestroyedByPlayer`, `FactionStationDestruction.GetDestroyReputationLoss`, `FactionStationDestruction.SwapForWreck`, `StationDestruction.GetRecoverableMaterials`.

### Code: Reputation and Force Attacks

#### Code: Killing Parts

The exemption is the `noReputationLoss` flag on the part. The base penalty is `num = ceil(baseExperienceReward * 0.5) = 5`. If the owner's reputation is above -500, `num` is multiplied by 20 and the event log (type `rep`) gets `@DestroyedFriendlyTarget`, "Betrayal! -{0} {1} reputation.", with `{0}` the multiplied amount and `{1}` the owner's faction name. The penalty is then `ChangePlayerReputation(-num)`. The enemy test is `FactionData.IsEnemy` (at war, or reputation below -500). The rival-faction gain is `ceil(num * 0.2)` taken before the x20, so `ceil(5 * 0.2) = 1`. It goes to `SystemMapData.current.faction`, the faction owning the system you are in, through `ChangePlayerReputation`, only when that faction `IsEnemy` of the part's owner and your reputation with it is above -500 and below 1500. A late-game player flag cancels the kill penalty in `LootManager.HandleKillReputation` and the wreck penalty in `FactionStationDestruction.ApplyDestroyReputation`. `FactionStationDestruction.GetDestroyReputationLoss` returns 1000 when the reputation level is below Wary (Hostile, reputation -5000, or worse) and 5000 otherwise.

#### Code: Ctrl-Click on a Friendly Station

The third condition is that the location you are in is not a `SpaceStation`. The second condition is `SystemMapData.current.sector.quadrant > 1` in `AbstractUnit.CanBeForceFired`. The `SectorMapData` constants are `quadrantPrologue` 0, `quadrantFrontier` 1, `quadrantConquest` 2, `quadrantTestArena` 3 and `quadrantExpansion` 4 (Meridia) to 7 (north 5, south 6, middle 7). The tooltips call a `SectorMapData` a "Subsector" (`@MapStaticSector`) and the map button for a neighbouring quadrant reads "< Frontier" (`@MapFrontier`), "Conquest >" (`@MapConquest`) or the quadrant name with a ">" (`@MapQuadrantNext`, `MapWidget.GetQuadrantName`). `FactionStation` (Defense stations and economic outposts), `ConquestStation` (hubs), `EmbassyStation`, `PlayerStation`, `IndustryStation` (Industrial Outposts) and every guild or neutral station are `SpaceStation`s. When you attack a non-hostile part, the part's `playerHostile` flag is set.

In 0.8.3.5, `CombatStationPart.OnMouseUpAsButton` gained a last branch: when the part is not a facility part, `MapPointOfInterest.current` is a `SpaceStation` with the same faction as the part, and `HudManager.CanDockAtStationHub` is true (`IsDockableHubPart`), the click calls `HudManager.DockAtStationHub`. `CombatStationPart.BuildTooltipCard` adds the `@TipHintDock` hint in the same case.

Source: `LootManager.HandleKillReputation`, `LootManager.DropLoot`, `AbstractUnit.CanBeForceFired`, `AbstractUnit.TakeDamage`, `CombatStationPart.OnMouseUpAsButton`, `BasePoiManager.MarkFactionPlayerHostile`, `AbstractUnitData.IsEnemy`, `MapPointOfInterest.FinishSeedBuiltStation`.

### Code: Station Layouts

| Seed | Builder | Result |
|---|---|---|
| `SoloMediumStation`, `MediumSkirmish` | `CreateMediumStation` | 14 parts |
| `PirateLarge` | `CreateLargeStation` | 18 parts |
| `EasyStation1` | `CreateEasyStation1` | 9 parts |
| `EasyStation2` | `CreateEasyStation2` | 10 parts |
| `EscortLocation` | `CreateEscortLocation` | 6 parts |
| (window dressing, Industrial Outpost stations) | `CreateGunPlatform` | 3 or 4 parts |

The faction prefix picks the artwork: Luminate Combine uses `Luminate_`, Stellar Industries `Stellar_`, Corsair Syndicate `Corsair_`, and every other faction `Generic_`.

#### Code: Part Lists

Connector counts are in the last column.

| Builder | Core | Energy | Refinery | Cargo Dock | Docking Pad (Small) | Tunnel | Platforms | Connectors |
|---|---|---|---|---|---|---|---|---|
| Medium station | 1 | 1 | 0 | 0 | 1 | 0 | S1_1C, S2_1C, S2_3C | 8 |
| Large station | 1 | 1 | 0 | 0 | 2 | 0 | S1_1C, 2 S2_2C, 2 S2_2CSlim, S2_1C | 8 |
| Easy 1 | 1 | 1 | 0 | 0 | 0 | 0 | 2 S1_1C | 5 |
| Easy 2 | 1 | 1 | 0 | 0 | 1 | 0 | S1_1C + drone bay | 5 |
| Escort location | 0 | 0 | 1 (Escort Refinery) | 0 | 0 | 0 | S2_3C + drone bay | 3 |
| Gun platform variant 1 | none | none | none | none | none | none | S2_2C | 2 |
| Gun platform variant 2 | none | none | none | none | none | none | 2 S1_1C + drone bay | 0 |
| Gun platform variant 3 | none | none | none | none | none | none | S2_3C + S1_1C | 2 |

The gun platform variant is chosen at random (a roll above 0.666, between 0.333 and 0.666, and below 0.333 give variants 1, 2 and 3) unless the caller forces one. "Docking Pad" counts `DockingSmall_2C` in these builders. The turrets in these builders are fixed S1, S2_1C, S2_2C and S2_3C platforms and never change with level.

#### Code: Extra Loot on Mission and Skirmish Stations

- `MediumSkirmish` (faction skirmish locations): one random non-weapon, non-booster piece of equipment and one random turret. The Core also gets a bonus skill point template.
- `PirateLarge` (pirate hideouts, the pirate station in the conquest world, Hard to Insane Station Offense and Defense missions): same two items plus the skill point template.
- Station Offense and Defense missions pick the builder from the difficulty: Easy gets Easy 1 or 2, Normal the Medium station, Hard and above the Large station.

Source: `CombatStationFactory.CreateStationVariant`, `CombatStationFactory.DispatchStationVariant`, `CombatStationFactory.ResolveSizeVariant`, `CreateMediumStation`, `CreateLargeStation`, `CreateEasyStation1`, `CreateEasyStation2`, `CreateEscortLocation`, `CreateGunPlatform`, `CreateMediumSkirmishStation`, `CreatePirateHideoutStation`, `MapPointOfInterest.BuildStationFromSeed`, `StationBattle.GenerateMission`, `PoiWindowDressingHelper.AddGunPlatform`.

### Code: How Long Damage Lasts

`GamePlayer.CleanupStaleSpaceStation` runs for each `SpaceStation` that is not an abandoned conquest hub (`Conquest.IsAbandonedHulk`: a `ConquestStation` with no faction). When the time since the last visit is above 3600 s it calls `MapPointOfInterest.ClearStationBuilding`, which makes the station rebuild from its seed. `CleanupStaleCombatPoi` handles `Combat` locations (which include `CombatStation`) after 7200 s and only resets guard units and payloads, not the `CombatStationData`.

Source: `GamePlayer.CleanupStaleSpaceStation`, `GamePlayer.ToJson`, `MapPointOfInterest.ClearStationBuilding`.

### Code: Industrial Outpost Stations

The map location type is `IndustryStation`. It is built from `CreateEasyStation1` plus a variant 1 gun platform. A repair pack is `6000 * DamageMultiplier(startLevel)` HP of repair. Wave strength is `0.7 + 0.1 * wave`.

Source: `CombatStationPart.BuildIndustrySection`, `IndustrialOutpost.UpdateActive`, `IndustrialOutpost.repairMax`, `IndustryBoard.GenerateIndustryMissions`, `LootManager.DropLoot`.

### Code: Boardable Facilities

A facility (dungeon) station is built by `DungeonStationFactory`. Projectiles, rail cannons, torpedoes and manual drone targeting all ignore its parts (`IsProtectedFromWeaponFire`). Facility integrity is `live parts / total parts`. When a saved facility with integrity below 100% loads, each part other than the first is removed with probability `1 - integrity`. When the last Docking Pad or Docking Tunnel dies, the facility's `dockingDestroyed` flag is set.

Source: `CombatStationPart.HandleDockingPartDestroyed`, `CombatStationPart.UpdateFacilityIntegrityFromCombat`, `CombatStationPart.CheckDungeonStationFullyDestroyed`, `DungeonData.isEnterable`, `DungeonLocationData.CollectOrDamagePart`, `DungeonStationFactory.Generate`, `TargetableUnit.IsProtectedFromWeaponFire`, `AbstractUnit.ApplyFacilityDamage`.

### Code: Not Verified

- Whether station parts are included in the unit list used by `MarkFactionPlayerHostile`.
- `CombatStationFactory.CreateFromLayout` has no caller in the decompiled code.
