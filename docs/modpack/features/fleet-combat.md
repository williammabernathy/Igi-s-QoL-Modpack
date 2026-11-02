# Fleet and combat

How your ships pick targets, how close they fly, what their missiles and drones do. Most of these have a default on the Settings tab and a per-ship override in the [ship settings popup](interface.md#ship-settings-popup). Settings keys are listed in [settings.md](../settings.md).

Your own clicks on the flagship and attack orders always win over everything on this page.

## Target priority

Which enemy your ships attack when they pick a target on their own. `Rule`:

| Rule | Picks |
|---|---|
| Nearest | The closest enemy (the game's choice) |
| Lowest hull % / Highest hull % | The most damaged or the healthiest enemy |
| Most damage to my fleet | Whoever has hurt your ships most in the last few seconds |
| Attacking my flagship | Enemies shooting at or aiming at the ship you fly |
| Smallest first / Largest first | By hull class |
| Lowest level / Highest level | By enemy level |
| Weakest to my weapons | The enemy that takes the most damage from the ship's guns: each gun's expected DPS times what the enemy lets through of its damage type, on the layer a hit lands on now (shields have no per-type resistance, armour adds its own resistance or weakness, paint strip included) |
| Debuffed first | The enemy carrying the most debuffs: the ones the game's buff bar marks red, plus stat penalties applied from outside such as the painter drone's strip, plus an armour paint strip |

The two newest rules only prefer an enemy that leads by more than `WeakestGuardPercent` or `DebuffedGuardPercent` (0-200, default 25): "only when it takes more than 25% more damage than the other choice". A smaller lead counts as a tie, so a small resistance gap never drags a ship across the field. Any debuff beats none.

`RepickSeconds` (0-30, default 0) is how often a ship re-applies its rule. 0 keeps the game's stickiness: a ship stays on its target until it dies or leaves. Above 0, a ship switches when another enemy is clearly better (for example 10 points of hull, or 20% closer), so ships don't flicker between two similar targets. `FinishBelowPercent` (0-100, default 0) keeps a ship on a target below that hull %, so nearly dead enemies get finished. With any rule other than Nearest alone, ships also stop jumping to whoever just hit a nearby ally. "Attacking my flagship" is the exception for hits on the flagship: a ship whose first rule it is switches to whoever hits the ship you fly, even at `RepickSeconds` 0, unless it's already on an enemy at least as good by its rules, is finishing a target below `FinishBelowPercent`, or the attacker is past its "Only attack within" distance.

In the popup a ship can chain up to three rules: "Target choice", then two "Then by" rows, for example "Attacking my flagship, then Weakest to my weapons, then Nearest". Enemies are compared rule by rule. A difference within a rule's margin (10 points of hull, 20% of the distance, the lead above for the two newest rules, an exact tie for size, level and "Attacking my flagship") counts as a tie and the next rule decides; after the last rule the nearest enemy wins. So a rule placed first wins over the ones after it, and a rule placed later is a fallback. Re-checks walk the same chain.

"Attacking my flagship" only pulls a ship onto whoever hits your flagship when it's the chain's first rule; further down it breaks ties like any other rule. Weapon rules stay one rule each, and the Settings tab's `Rule` stays the one rule for ships without their own. Choosing "None" in a "Then by" row ends the chain there. Saves from before rule chains load each ship's rule as a one-rule chain.

The ship settings popup adds, per ship:

- its own rule chain, re-check interval and finish threshold;
- "Avoid enemies above level": enemies above it are only picked when nothing else is in range (0 = off);
- "Only attack within (units of the flagship)": fleet ships don't pick enemies further than this from the ship you fly, and drop a target that moves past it (0 = off);
- one rule per combat weapon (hardpoint). A weapon with a rule shoots the best enemy by that rule among those it can hit and has in range, and follows the ship's target otherwise. For example, one turret on drones while the rest follow the ship.

The Escort and Defensive stances pick the ship's target themselves (Defensive: whoever is hitting it, else your target; Escort: your target), so under them the chain, "Attacking my flagship" included, only decides when there is nothing to copy. New fleet ships start in the Defensive stance; set Aggressive for the chain to decide every pick. Weapon rules still apply under every stance.

## Engagement distance

How close each ship flies to its combat target, set per ship in the popup:

- Auto: the game decides (the default).
- Max range: stay as far back as the shortest-range weapon that can hit the target allows.
- Fixed distance: stay this many units away (6 to 30: the game never holds closer than 6 or further than 30).

It only changes combat targeting. Mining, salvage, repair and painter modules approach their targets as the game does.

## Keep distance in combat

In a fight the game flies every ship straight at a ring around its target and switches its obstacle-avoiding navigator off, so ships pile on top of each other. With this on, a fighting ship (has a target, no move order, not holding position) has its destination pushed away from every ship whose hull outline is closer than `Gap` (0.5-10 units, default 2). The push is measured again every 0.25 s and added on top of the game's own destination, so it doesn't build up.

`Default` (off) applies to your ships, and each ship can override it in the popup. `OtherShips` (off) makes enemy and allied ships that aren't yours do the same; it changes how enemies move, which is why it's off by default.

Measured in spawned 12-enemy, 4-ally brawls (4 rounds of 33 s each way): with both on, overlapping ship pairs dropped by 42%, and the share of turrets with their target in range dropped from 78% to 71%, because ships that slide apart sometimes slide out of reach.

## Retreat for repairs

In the game a fleet ship leaves a fight for repairs once a hit drops it below 10% hull: it flies out, repairs for 120 s and rejoins. Your fleet ships can't be destroyed, but every hull hit of 2% or more can kill crew aboard. The popup's "Retreat for repairs" makes a ship leave sooner: below 20%, 30%, 50% or 75% hull. "Game default" keeps the 10%.

The check runs when the ship takes a hit, as the game's does. It has no effect on the ship you fly, and a hired extra mercenary ship leaves for good, as it does at 10% in the game.

## Ship stats on hover

Hovering any ship, enemy, neutral or one of your fleet ships (the game shows no card for the ship you fly), adds a Resistances block to the game's hover card: one row per damage type with the share of a hit the ship resists right now. Buffs and debuffs are counted, so a ship hit by a resistance debuff shows the lower figure. This figure can't go above 80%, and it goes below 0% when debuffs push it there.

Ships with armour get a second figure per row, after the `|`: what a hit on the armour loses in total, the ship's resistance plus the armour's own resistance or weakness to that type (paint strip included). That figure applies only while the armour has HP left. A row turns red when either figure is below 0%, meaning that type hits harder than normal.

Below that, a Buffs and debuffs block lists what the game's buff bar would show if that ship were yours: one row per effect, with its stacks (`x3`), the time left and a bar that empties as it runs out. Buffs are blue and debuffs red, as in the game's buff bar, and the block's header counts both. An ability that's running without a timer of its own reads "active" (or "on" for a toggle). The block is left out when the ship has no effects.

The card also says what the ship is targeting right now ("Targeting", next to the Captain row) and adds two blocks when they have something to show:

- Threat: the ship's threat list, highest first, each as a share of the top entry, so you can see how close another ship is to pulling it away. The unit it's attacking has a red "target" badge. That isn't always the top entry, because the game skips units out of its weapon or chase range first. Only combat ships that aren't yours keep a threat list, and at a station location most ships follow the station's behaviour instead and have none.
- Attacked by: the units whose current target is this ship, on any ship, yours included. Units with the same name share a row ("Defense Platform x6"), and the header gives the total.

Each block shows 5 rows, then "+N more".

The card rebuilds twice a second while you hover it, so timers count down, the resistance figures follow debuffs as they land and expire, and the threat shares and attackers stay current.

## Combat overview

A pop-out window (see [pop-out windows](interface.md#pop-out-windows)) that lists every ship at your location, enemies first, then yours, then everyone else, nearest first within each. Open and close it with F7 (`Hotkey`) or the "Combat overview" button on the DPS parser tab. Carrier fighters and drones aren't listed.

Each ship gets one row: its level, shield, armour and hull as a share of the maximum, how many buffs and debuffs it carries (debuffs counted as for "Debuffed first", in red when there are any) and what it's targeting. Names are red for enemies, green for your ships. Click a ship's name for its details, the same figures the [hover card](#ship-stats-on-hover) shows:

- Resists: the share of a hit it resists by damage type, and after the `|` the total on armour.
- Effects: every buff and debuff with stacks and time left, plus the number of stat penalties that have no buff-bar entry.
- Weapons: its combat weapons by damage type and their range.
- Threat (enemy combat ships) and Attacked by.

The list refreshes twice a second while the window shows. It rebuilds only when a ship arrives or leaves, and a ship's details stay open until you close them. By default the window shows only while you're in combat: a hit in the last `FightGapSeconds` of the DPS parser, or any of your ships holding a target. Its Options button has "Show: Always", the opacity, click-through and "Ships shown" (all ships, enemies only, or enemies and your fleet).

## Missile retargeting

What your missiles do when their target is destroyed before they hit. `Mode`:

- Off (Fly on): the game's behaviour; the missile flies on and is wasted.
- WeaponTarget (Weapon's target): steer to whatever the launcher is aiming at now.
- Nearest (Nearest enemy): steer to the closest enemy the launcher could hit.

Each ship can override it in the popup.

## Drone modes

Drone behaviours beyond the game's Free roam and Follow ship:

| Mode | Combat and painter drones | Mining and salvage drones | Repair drones |
|---|---|---|---|
| Follow the ship | Without a target, come back once further than N units | Without a target, come back once further than N units | Without a target, come back once further than N units |
| Guard the ship | Only take targets whose hull is within N units of the ship, stay within N, drop targets that drift past 1.25 × N | Join the ship's asteroid or wreck when nothing free is within N | Fix the ship first when it's damaged, else anything within N |
| Ship's target only | Attack the ship's target, otherwise follow | Join the ship's asteroid or wreck when they can (a driller only if the asteroid has room), else take one within N | Fix the ship first when it's damaged, else anything within N |

Mining, salvage and repair drones switch at their next pick, so nothing is pulled off a job. "Drones stay with" in the popup makes "the ship" another ship of your fleet; while that ship is elsewhere (travelling, docked, destroyed), the drones stay with their own ship.

The HUD's drone button cycles Free roam → Follow ship → Follow → Guard → Ship's target for the flagship, with its label showing the mode. Every ship has the mode, distance and anchor in its popup. The game's own saved mode stays Free roam or Follow ship, so a save loads fine without the mod.

## Drones on arrival

`Default` (off): every ship of yours launches its docked drones as soon as it arrives somewhere that isn't a station. Drones you recalled yourself stay docked. Each ship can override it in the popup.
