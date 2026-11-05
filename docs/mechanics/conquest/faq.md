# Common Questions

These are short answers to questions players ask about the Conquest stage (level 28 and up): the 30% sector-control mission, the war map, battles and captures, faction wealth and warships, and stations. Each answer ends with a link to the section that has the full numbers and how they were read from the game code. Values were read from game version 0.8.3.4, and the code behind each answer was compared with the 0.8.3.5 source. Terms are in the [conquest glossary](glossary.md) and the [basics glossary](../basics/glossary.md).

## The 30% Sector-Control Mission

### What Counts Toward the 30%?

Your chosen faction needs to own 30% of the systems in the Conquest sector (Ara Martis). Only systems owned by exactly that faction count, and its headquarters counts. Allies, rival factions and unowned systems do not. The total includes every system in the sector, owned or not, and every rival's headquarters. The Darkspace region, the staging sectors and the Frontier are not in it.

The world generator gives the sector 70 to 84 systems, but a save checked in play had 88, so your total may be higher. The number of systems the faction must own is:

| Total systems | 70 | 71 to 73 | 74 to 76 | 77 to 80 | 81 to 83 | 84 to 86 | 87 to 90 |
|---|---|---|---|---|---|---|---|
| Systems the faction must own | 21 | 22 | 23 | 24 | 25 | 26 | 27 |

To find your own total, divide the faction's system count on the galaxy map by its Area Conquered percentage.

Details: [What the Objective Counts](sector-control.md#what-the-objective-counts).

### How Often Does the 30% Progress Update, and When Do I Have to Be at 30%?

The percentage updates once an hour. The game stores it when the "Conquest Update" countdown on the galaxy map runs out. It is stored again at once when a system is abandoned, which a hub kill does. Both the "Area Conquered" row and the mission's objective line therefore lag the map by up to an hour. To find the row, press E on the galaxy map, open the Factions tab and click the faction. A siege capture, or a fleet claiming an unowned system, shows up late.

The percentage can also go back down, because every update recomputes it from the current owners. Nothing locks it in once it reaches 30%. It only has to read 30% or more at the moment you talk to the faction's commander in its headquarters. After that the mission completes, and later drops do not matter. If it reached 30% at one update and has dropped by the next, you still succeed if you are already standing at the commander.

Details: [What the Objective Counts](sector-control.md#what-the-objective-counts).

### What Is the Best Way to Finish the 30% Mission?

Let your faction's own fleets take the easy systems, help on the harder ones, and time your hand-in. You cannot hold systems yourself yet, so everything you do nudges a war that NPC (non-player character) factions fight on their own. The three major factions are Kolyatov Collective, Stellar Industries and Luminate Combine. Here is a short plan:

1. Pick a faction that is not your enemy, has a headquarters, and borders Darkspace Compact systems.
2. Look at the neighbours of its frontline. A neighbour with no resident fleet and a Defense row of about 1,739 or less is a job for one fleet. A full 2000-point fleet starts an attack at 1.15 times the target's next-battle strength, a figure the game never shows (see [the glossary](glossary.md#conquest-map)). It captures the system once it has killed the defenders and broken the hub with about 313 points left. The faction takes such a system on its own, so leave it.
3. For a neighbour with a resident 2000-point fleet, do 5 war effort missions at your faction's hub next to it, each within 3 hours of the one before. Pick missions whose reward line names the target faction, for example "-10% for X next system". That lowers the points attackers need to start an attack by about 144 to 403, and the station fires at half strength through the siege. It does not remove the fleet.
4. Kill a hub only where the system can convert to your faction. The owner must already be your enemy, and the target must border one of your faction's systems. Your faction needs a free fleet within 4 jumps, meaning one that is idle and not heading to a refit, a distress call or an assault. The old owner must have no idle fleet inside the system, and no other faction's fleet may land first. Then fly in and kill the hub parts. The system is abandoned, and the first fleet to land claims it. Your faction's fleet claims it at its next planning pass (within 600 s). Skip the Defense stations.
5. Time it against the Conquest Update card. A hub kill shows in Area Conquered at once. A siege capture or a fleet's claim shows only at the next update, so finish captures well before the countdown ends. Be at the commander in the headquarters as soon as the objective shows 30% or more.

Do not wipe a rival major's fleets. A wiped fleet frees a slot, which halves its build costs and triples its build speed. A major with 2 fleets or fewer also gets a 1500-point Great Gate reinforcement.

Details: [A Step-by-Step Plan](sector-control.md#a-step-by-step-plan).

### What Are All the Ways I Can Push My Faction's Share?

ECHO is your ship's AI companion. In this table, a "non-idle" mission is one you fly yourself, not one ECHO completes while you are away.

| Lever | Effect |
|---|---|
| Kill a resident fleet or garrison ships | Every point removed from the target's next-battle strength cuts what attackers must bring by 1.15 points. Killing a resident 2000-point fleet turns a target that needed an assault into one that a single fleet can start. It also leaves 2000 fewer points of defenders to kill before the hub. |
| War effort penalty on the target | Removes 125 to 350 from the target's station term. That lowers the points attackers need to start an attack by about 144 to 403, and the station fires at half strength through the siege. It takes 5 non-idle missions, each within 3 hours of the one before. |
| Kill the hub | Abandons the system. A fleet of your faction takes it only if your faction borders it, has a free fleet within 4 jumps, and no other fleet lands or already stands there first. The owner must already be your enemy. |
| War effort bonus at your frontline hubs | Adds up to +250 to 700 of Defense, which protects systems your faction already has. It takes 10 non-idle missions at the system, or 20 at a neighbour, each within 3 hours of the one before. |
| Embassy missions | 80,000 to 120,000 wealth each. They only help rebuild a lost fleet and do not raise a major's fleet cap. |
| Kill enemy warship deliveries | Destroys a ship the enemy has already paid for. It rarely changes how many systems anyone holds. |

Details: [What Moves Your Faction's Share](sector-control.md#what-moves-your-factions-share).

### What Can Go Wrong When I Try to Help?

- A hub kill with no hub-stage siege by your faction gives the system to the first fleet that lands. Only factions that own a neighbouring system consider it. A faction with no frontline looks at any open system. A claim by an arriving fleet needs no siege and no wait. A fleet already flying in to attack the system, or a nearer fleet of another faction, lands before yours. The old owner's own idle fleet standing in the system claims it back at the next planning pass, so the hub kill is wasted there.
- A hub kill during a rival's siege hands the system to that rival at once if the siege is in its hub stage. If claiming is unlocked for you, you are offered the claim first.
- You cannot shoot a hub of a faction that is not your enemy. Making it one costs about 4,500 reputation (-1,000 for the force-attack and -3,500 for the kill), which is enough to push most factions under -500.
- Pushing a faction down to 0 systems only delays it. It sits out 5 conquest updates, then its fleets start claiming systems again.
- When your faction passes 10%, the smaller major factions that border it can start attacking it. Once it is larger than Darkspace, every NPC faction that borders it prefers it as a target.
- War effort needs a mission from a hub in a war-zone system. Embassy missions give wealth only, and a mission at another faction's hub strengthens that faction's stations.
- For Kolyatov, Stellar and Luminate, wealth and fleets do not raise the fleet cap. It rises only with systems held: to 6 at 15 systems and to 7 at 30.

Details: [Pitfalls](sector-control.md#pitfalls).

### Why Is My Faction's Share Not Growing on Its Own?

The game has no growth rate to read. It only has limits on what NPC fleets will attack. A single 2000-point fleet acting alone starts an attack only at 1.15 times a target's next-battle strength or better, so it can start on anything up to about 1,739. It captures the system once it has killed every defender and broken the hub's structure inside the 30-minute stage, with about 313 points still left. That is 1.25 times the 250 floor that the Defense number drops to. A siege that runs out of time first ends in a skirmish or a repelled attack. Above 1,739 the faction needs an assault of at least 2 ready fleets, and it runs one assault at a time. Travel takes 60 s plus 150 s per jump, a siege stage takes at least 600 s, and planning runs every 600 s.

A faction holding under 10% of the sector only targets unowned systems or owners at 10% or more. If that leaves no target at all, it targets everything. At the start, none of the three majors is at 10%, so they ignore each other and go after Darkspace and unowned systems. A resident fleet in a system also pushes it above the single-fleet limit, so those systems wait for an assault.

Details: [What the War Does Without You](sector-control.md#what-the-war-does-without-you).

### Can I Hold or Claim Systems Before Level 60?

No. The game refuses with "Claiming systems unlocks with Beyond the Front". Fielding your own fleets, building outposts or Defense stations, and the Empire Level tile on the Faction overview unlock with that mission too. The mission opens at level 60 once you have finished one 30% mission ([the gate](../expansion/README.md)). Until then you can run Conquest missions, let ECHO run idle ones, attack stations and ships live, intercept fleets and deliveries, and join a smaller faction's war.

Details: [What You Can and Cannot Do](sector-control.md#what-you-can-and-cannot-do).

## Starting the Mission and Choosing a Faction

### Spoiler: How Do I Get the 30% Mission?

1. Accept the story step at level 28. It ends with a talk to the Canisec Commander. Then find the three embassies and talk to their representatives. A representative offers to align you with that faction.
2. The faction's first mission has four steps: go to its embassy gate, talk to the commander at its headquarters, earn 10 contribution points by running missions for it, and talk to the commander again. The points count only while that third step is current, so missions finished before you talk to the commander do not count. A Conquest mission from its hub or embassy adds 3 to 6 points (1 to 3 if ECHO completes it idle). The amount is doubled when the mission's enemy is a conquest faction other than the Corsair Syndicate and Meridia's Chosen, and a bonus from your conquest rank is added. That is usually 2 to 4 missions, or 1 to 2 when the doubling applies. The first mission pays 2,000,000 credits and 60 Vanguard Marks.
3. The second mission has the 30% step and a report-back step. It cannot be abandoned and has no time limit. It pays 400 Vanguard Marks, 10,000,000 credits and 1 skill point. If the faction is already at 30%, the percentage step is done the moment the mission starts.

Details: [Spoiler: Getting the Mission](sector-control.md#spoiler-getting-the-mission).

### Spoiler: Why Does the Representative Refuse to Align Me?

The offer is refused if the faction is your enemy, or while the faction has no headquarters. The game counts a faction as your enemy when you are at war with it or your reputation with it is below -500. That is the Wary level or worse (Neutral covers -499 to 1,499). Raise your standing with that faction first, or pick another one.

Details: [Spoiler: Getting the Mission](sector-control.md#spoiler-getting-the-mission).

### Spoiler: Is the Faction Choice Permanent?

Not by the game's own words. The "Sector Control" mission description says "The choice of faction is not permanent", and the confirmation dialog says "You are not permanently bound to them and may work with others later." The game only shows a yes/no prompt, so how you would change the choice is not verified. The game always tracks your standing with all conquest factions and accepts any of the three 30% missions as the requirement for what follows.

Details: [Spoiler: Getting the Mission](sector-control.md#spoiler-getting-the-mission).

### Spoiler: Can I Work on More Than One Faction's Chain?

Yes. You can start the missions for more than one faction, and finishing any one of the three 30% missions is enough. The cheapest route is whichever of your factions the war favors. Check each faction's Area Conquered row on the Factions tab (press E on the galaxy map) before you commit.

Details: [Spoiler: Getting the Mission](sector-control.md#spoiler-getting-the-mission), [A Step-by-Step Plan](sector-control.md#a-step-by-step-plan).

### Spoiler: What Are the Three Kinds of "Contribution"?

The game uses the word for three separate counters. "Your Contribution" on the Faction overview is your conquest standing with a faction, and it sets your conquest rank. The 10 points in the first mission are a counter that runs for that one step only. The same mission amounts also add to "Your Contribution". "Wealth your missions brought" is what your embassy missions delivered to a faction, and it matters for joining a smaller faction's war.

Details: [Spoiler: Getting the Mission](sector-control.md#spoiler-getting-the-mission), [Joining a Faction's War](faction-economy.md#joining-a-factions-war).

## The System Tooltip, War Effort and Fleet Labels

### What Does the Defense Number on a System Tooltip Mean?

It is the strength the defenders put into a siege round, in the same unit as ship points. An unowned system shows 0. The number is the sum of three terms:

```
Defense = station firepower term
        + garrison points of every Defense station in the system
        + points of every defending fleet in the system
```

The station term uses the hub and every Defense station of the owner that is not wrecked. It has a floor of 250, and firepower above 700 does not count, so extra turret platforms past that only add hull. A Defense station is a type of station that holds a garrison. It is not the same thing as the Defense number. Garrisons run 270 to 900 points per station, depending on tier. A full NPC fleet is 2000 points.

For example, take a hub with 480 firepower, one tier-3 Defense station with a platform of two medium turrets (160 firepower, 540-point garrison) and one full fleet. The Defense number is min(480 + 160, 700) + 540 + 2000 = about 3,180.

NPC factions do not compare against this number. They use next-battle strength, which the game never shows. Next-battle strength is the weakest Defense station's strength if there are any, otherwise the full Defense number, plus same-faction fleets already on the way. The tooltip number is usually the upper bound. The exception is a system that is settling after a change of owner: the row halves the station profile, but a Defense station's own strength is not halved.

Details: [What the Defense Number Means](war.md#what-the-defense-number-means), [What NPC Factions Use Instead of the Defense Number](war.md#what-npc-factions-use-instead-of-the-defense-number).

### How Do I Lower or Raise a System's Defense?

To lower it:

- Apply a war effort penalty. At the maximum, it halves the station term.
- Destroy defender ships or fleets. Their points come off one for one. Intercepting fleets that are still traveling also removes the points they would add to the defense as relief.
- Destroy turret platforms or the core by hand. The parts must be fully destroyed.
- Attack within a few hours of a capture. The station profile (hub and Defense stations, hull and firepower) is halved while the system is settling.
- Break the station structure (the hull total the attackers wear down; see the [glossary](glossary.md)) in a hub siege. That lowers the station term toward the 250 floor.

To raise it, use a war effort bonus (up to double the station term) or add turret platforms, which only help up to 700 firepower.

Details: [Ways to Lower or Raise Defense](war.md#ways-to-lower-or-raise-defense).

### What Is War Effort and What Does It Do?

War effort is a multiplier on a system's station firepower, raised by Conquest missions you complete. The tooltip shows it as "Your war effort", green for a bonus and red for a penalty, with the time left.

```
multiplier = max(0, 1 + bonus - penalty)
```

Each non-idle mission adds a step of 0.1:

- At the mission's system, the step goes to the bonus if the mission's faction owns that system.
- Each neighbouring system owned by that faction gets half of that bonus.
- Each neighbouring system owned by the mission's enemy faction gets the same step as a penalty.

The bonus caps at +1.0 (a multiplier of 2) and the penalty at 0.5 (a multiplier of 0.5). Each side has one timer of 3 hours, and every new step resets it. When the timer runs out, the whole bonus or penalty clears at once.

Idle missions done by ECHO use a step of 0.1 x (1 + 0.2 x points in the "ECHO The Conqueror" skill). The multiplier applies to the station term of the Defense number. While you are fighting at a hub or Defense station, it also applies to the Combat Power, Drone Power and Torpedo Power of that station's parts.

Details: [War Effort](war.md#war-effort), [War Effort on Station Weapons](war-stations.md#war-effort-on-station-weapons).

### What Does "Reinforcing" on a Fleet Mean?

It means the fleet is moving to a frontline system, or is retreating. A frontline system is one of the faction's systems that touches an enemy or unowned war-zone system. An idle fleet that is not on the frontline gets the label when it is sent to the best-scoring frontline system. Every retreating fleet shows it too, both on the way to its retreat site and while it rests there. Warship deliveries show it as well. A fleet retreats when it drops below 35% of its target points and shows "Reinforcing" until it is back to 75%.

Details: [Fleet Labels on the Map](war.md#fleet-labels-on-the-map), [Retreats, Recovery and Distress](war.md#retreats-recovery-and-distress).

### What Do the Other Fleet Labels Mean?

| Label | Meaning |
|---|---|
| Attacking | The fleet was sent to take a system, an assault is launching, the fleet is joining a battle as a supporter, or it just started a clash or siege on arrival. |
| Gathering for an assault | The fleet is heading to, or waiting at, the staging system of an assault. |
| Holding position | The fleet is idle on a frontline system. It is also the label a fleet gets by default after a battle or a finished refit. |
| Guarding faction space | The faction's remaining fleets are spread over its station systems. |
| Responding to a distress call | The fleet was sent to a besieged or raided system that the faction owns. |
| Defending the system / Defending an outpost from raiders | The fleet is idle in a system where its owner is under a Defense station or hub siege (not a fleet clash), or where an outpost is being raided. |
| Raiding an outpost | The fleet was sent to raid an economic outpost. |
| Refitting - {time} left | The fleet is being refitted. A refit takes 300 s. |

Details: [Fleet Labels on the Map](war.md#fleet-labels-on-the-map).

## Battles, Sieges and Captures

### How Do Battles and Sieges Resolve?

There are four kinds of fight: a fleet clash against a defending fleet (600 s cap), a siege of one Defense station (1800 s), a siege of the hub (1800 s) and a raid on an economic outpost (900 s). When you are not there, they run as simulated rounds every 60 s. The fight is judged at the cap instead of running one more round, so a siege gets at most 29 rounds, a clash 9 and a raid 14. Each side deals about 4% to 6% of its own strength per round. Damage is spent on that side's ships at random, with cheaper ships picked more often. Over a full siege each side deals about 1.45 times its own strength in total.

Each round, the attacker's strength is also scaled by a hull edge of up to plus or minus 15%. The edge comes from the gap between the two sides' average hull ratings.

If you are at the battle, the game spawns the real ships and the clock stops. The fight ends when one side has no ships left in the zone.

Details: [Types and Stages](war.md#types-and-stages), [One Round](war.md#one-round), [Hull Edge](war.md#hull-edge).

### How Is a System Captured?

A hub siege ends in an early capture if all of these hold:

- 600 s have passed.
- The system is not a headquarters or your capital.
- The station structure is at 0.
- No garrison ships or defending fleets are left.
- The attackers have at least 1.25 times the current Defense number in points.

Otherwise the siege is judged at the 1800 s cap:

```
structure broken, capturable, points >= 1.25 x Defense  ->  Captured
points >= Defense                                       ->  Skirmish
otherwise                                               ->  Repelled
```

When a system is captured, the defending NPC fleets are wiped and the old owner's Defense stations are destroyed. The hub and economic outposts pass to the attacker. War effort clears, and the settling timer starts.

Details: [How a Siege Ends](war.md#how-a-siege-ends), [Capture by Siege](war.md#capture-by-siege).

### What Happens in a System Right After It Changes Hands?

For the first 2 conquest updates (1 to 2 hours) the system is in unrest and pays no taxes. For about 5 to 6 hours after the change, it is settling: the station profile of the hub and every Defense station (hull and firepower) is halved. That shows in the Defense number and in a hub siege, but not in a Defense station's own siege. The 250 floor is not halved. That makes the hours after a capture the easiest time to attack the hub again.

Details: [Settling and Unrest](war.md#settling-and-unrest).

### What Happens if I Destroy a Hub?

The system is abandoned and has no owner, and every fight there ends. Who gets it, in order:

1. If a siege is in its hub stage, its lead attacker takes the system at once, unless that attacker is a raider faction. Fights in any other stage just end.
2. With claiming unlocked, you are offered the claim first instead, and no fleet can claim the system while the offer is open. If you decline, the system goes to the hub siege's lead attacker if that fleet is still there. If you take it, the old owner's Defense stations are destroyed and your reputation with that faction drops to Hostile.
3. Otherwise the system stays unowned until a fleet lands. A fleet already flying in to attack it claims it on landing. In every other case, each faction that owns a neighbouring system sends the fleet that its planning picks at its next planning pass (within 600 s, or at once for a faction that was in a fight there), and the first to land wins. A fleet already standing in the system claims it at once, so the old owner's idle fleet there takes it straight back. A faction with no frontline looks at any open system.

When a fleet claims a system that a different faction abandoned, that faction's Defense stations in it are destroyed first. In your player sector the system goes back on sale instead. The game does not check for a headquarters here, so destroying a headquarters hub abandons it (not verified in play).

Hub parts have 20 times the normal hit points (30 times at a headquarters). Part kills cost 5 reputation each if you are at -500 or lower with the owner, and 100 each otherwise. A hub is not a faction station, so the -5,000 station penalty does not apply.

Details: [Unowned and Abandoned Systems](war.md#unowned-and-abandoned-systems), [What Moves Your Faction's Share](sector-control.md#what-moves-your-factions-share).

### How Do NPC Factions Pick Targets?

Idle fleets decide in a fixed order: retreat if below 35%, refit if due, answer a distress call, attack, join an assault, raid, then rally to the frontline and guard. A fleet attacks alone only at 1.15 times the target's next-battle strength or better. Among valid targets, it prefers these, in this order:

1. A target it is already standing in.
2. The owner with the most war-zone systems (unowned systems rank above any owner).
3. The lowest next-battle strength.
4. The fewest jumps.

Attacks go to the contested neighbours of a frontline system, up to 4 jumps away. A faction with no frontline looks at any open target up to 60 jumps away. A faction holding under 10% of the war zone only considers unowned targets or owners at 10% or more, unless that leaves nothing. When no single fleet is enough, 2 or more ready fleets gather at a staging system next to the target, merge and attack. A faction runs one assault at a time.

Fleets decide one after another in the order of the faction's fleet list, and each target is taken by one fleet. An unowned system counts as next-battle strength 0, so any fleet may go for it. A fleet already standing in a system picks that system first.

Details: [Choosing a Target](war.md#choosing-a-target), [Assaults](war.md#assaults).

### What Do the Clocks in the War Do?

There are three clocks, and the game shows only one of them:

- The "Conquest Update" card counts down 3600 s. Then it recalculates Area Conquered, raises each system's control level (an internal counter), counts down settling and rebuilds damaged stations.
- The hourly economy tick (not shown) pays taxes and the home subsidy, queues builds and assigns fleet orders.
- The planning pass (not shown) assigns fleet orders for every faction every 600 s, and at once for the factions of a battle that just ended.

The card's tooltip says factions "rethink their strategy", but the conquest update itself never assigns fleet orders. The three timers run on separate phases.

Details: [Cadence](war.md#cadence).

### How Strong Are the Fleets and Systems I Face?

NPC warships are built at the higher of your effective level and the highest system level, plus 6. At level 28, systems start at level 32 to 36, so the first fleets are up to level 42 (your level plus up to 14). Systems rise toward your level every hour and never drop. Once they sit about 4 above you, fleets are about your level plus 10.

At level 30, an enemy 5 levels above you has 1.56 times your damage and 1.57 times your HP. An enemy 10 levels above has 2.44 times the damage and 2.53 times the HP (see the [scaling table](../basics/scaling.md#how-much-tougher-is-an-enemy-five-or-ten-levels-higher)). A fleet is refitted once it is 3 or more levels behind, so old fleets mostly sit 0 to 2 levels behind between hourly updates, and further behind for a few updates after the NPC level rises. Fleets are 2000 points regardless of your level.

Details: [Fleet Levels](war.md#fleet-levels), [What You Face](sector-control.md#what-you-face).

## Faction Wealth and Warships

### Where Does a Faction's Wealth Come From?

| Source | Amount |
|---|---|
| Taxes | 5,000 to 15,000 per owned system per hour, doubled in the war zone, and none while the system is in unrest |
| Trade deliveries | 10% of the cargo value of a finished delivery, 14% when it goes to an embassy |
| Intertrade sales | 10% of the sale value |
| Home subsidy | Kolyatov, Stellar, Luminate and Darkspace: 120,000 x (1 - share / (1/3)) per hour, never below 0, where share is the faction's share of the war zone |
| Your embassy missions | 80,000 to 120,000 each (halved if idle) |
| Raids | Whatever a completed raid takes from its victim |

The home subsidy is 120,000 at 0% of the war zone, 84,000 at 10%, 48,000 at 20% and 0 at 33.3% or more. On average a faction earns 10,000 per system per hour outside the war zone and 20,000 inside it.

Details: [Where a Faction's Wealth Comes From](faction-economy.md#where-a-factions-wealth-comes-from), [Catch-Up Mechanics](faction-economy.md#catch-up-mechanics).

### What Do Factions Spend Wealth on?

| Spending | Cost |
|---|---|
| New economic outpost | 50,000 |
| Defense station | 100,000 |
| Station upgrades | 15,000 / 25,000 / 50,000 / 100,000, from tier 1 / 2 / 3 / 4 |
| Warships | 750 to 30,000 per hull, by size (see below); half price while under the fleet cap |
| Great Gate fleets | Full roster price, when the faction is eligible to pay |

Ships cost wealth only and need no materials, so cutting ore or goods supply does not slow warship production.

Details: [What Factions Spend Wealth On](faction-economy.md#what-factions-spend-wealth-on).

### How Is Wealth Affected by a Raid?

A completed raid moves wealth from the victim to the raider:

```
wealth drained = the smaller of
                 the victim's wealth
                 1.5 x its average hourly income (home subsidy not counted)
raider earns   = wealth drained + 10% of the value of the materials taken
```

An NPC raider also takes half of every stack in the station's storage, rounded down per stack, and those materials vanish. The raided outpost pauses production for 3600 s and cannot be raided again for 14,400 s. Defense stations cannot be raided. A faction with a big stockpile loses only about one and a half hours of income per raid. Raids do not touch the warship queue or deliveries.

Raids on your own stations work differently. An NPC raid drains no wealth and takes no materials from you, and the raider earns nothing, but the station still pauses and gets the cooldown. If your own empire fleet raids an NPC station (Beyond the Front), it takes 10% of each stack instead of half, and you are paid 10 credits per point of wealth the raid earns.

Details: [Raids and the Economy](faction-economy.md#raids-and-the-economy).

### How Is Wealth Affected by Killing a Warship Delivery or a Convoy?

Killing a warship delivery removes one ship for good. The faction paid for it when it was queued, nothing is refunded, and there is no loot because the ship carries no cargo. The faction queues the missing points again at its next 600 s planning pass.

Killing a cargo convoy cuts the run's trade or sales income in proportion to the ships you kill. Killing every ship ends the run with no income booked, and the next run goes out normally. That is minor next to taxes. Each convoy ship drops 40% to 60% of each non-ammo cargo stack as loot under your name.

If the faction is not your enemy, opening fire costs -1,000 reputation per ship and each kill another -3,500 while you are Wary or better. If it is already your enemy, it costs nothing.

Details: [Intercepting Deliveries and Convoys](faction-economy.md#intercepting-deliveries-and-convoys).

### Do My Embassy Missions Help a Faction?

Yes. Each one adds 80,000 to 120,000 wealth to that faction (halved for idle ones), and the faction gets it whether or not you later fight it. A full 2000-point fleet costs roughly 40,000 to 130,000 at full price (not verified). The wealth helps rebuild a lost fleet at once. It does nothing for a faction whose fleets are already full, and it does not raise the fleet cap of Kolyatov, Stellar or Luminate. To slow a faction, stop doing its embassy missions.

Details: [Your Embassy Missions Feed the Faction](faction-economy.md#your-embassy-missions-feed-the-faction), [Ways to Slow a Faction](faction-economy.md#ways-to-slow-a-faction).

### How Are Warships Built?

A faction queues a ship at a Construction Yard with a free slot, paying wealth up front. Yards are never built in war-zone systems. Each hull's price depends on its size, at 50 wealth per price point:

| Size | Cost | Cost while under fleet cap |
|---|---|---|
| 1 | 750 | 375 |
| 2 | 1,500 | 750 |
| 3 | 3,000 | 1,500 |
| 4 | 7,500 | 3,750 |
| 5 | 15,000 | 7,500 |
| 6 | 30,000 | 15,000 |

```
duration = (300 + 1.2 x ship points) / speed
speed    = (3 if under the fleet cap, else 1) / yard build-time multiplier
```

Yard tiers run 1 to 5. Tier 1 has 1 slot at 1.0 times the build time, and tier 5 has 3 slots at 0.5 times the build time. For each free slot, the faction picks the first of these that applies:

1. A garrison shortfall at a Defense station.
2. The fleet furthest below 35% of its target.
3. Founding a new fleet.
4. Topping up the fleet furthest below 100%.

A finished ship flies for 30 s plus 30 s per jump. A fleet of 2000 points costs roughly 40,000 to 130,000 at full price (not verified).

Details: [How Warships Get Built](faction-economy.md#how-warships-get-built), [Construction Yards](faction-economy.md#construction-yards).

### How Many Fleets Can a Faction Field?

For Kolyatov, Stellar, Luminate and the Corsair Syndicate (when it holds a war-zone system), the cap is the smaller of 8 and 5 + (war-zone systems held / 15, rounded down). That is 5 below 15 systems, 6 at 15 to 29, and so on. Other factions work differently:

- The Darkspace Compact is fixed at 5 until the expansion war opens ([the gate](../expansion/README.md)).
- The Corsair Syndicate with no war-zone system has 2.
- Mindus Holdings, Steel Vultures and Stranded field nothing without a war-zone system. With one, their cap is set by the embassy wealth you deliver: 3 below 2,250,000, 4 from 2,250,000 and 5 from 3,000,000.

For the factions whose cap follows systems held, wealth does not raise it. While a faction has fewer fleet slots in use than its cap, every ship costs half and builds three times faster.

Details: [Fleet Caps](faction-economy.md#fleet-caps), [The Under-Cap Discount](faction-economy.md#the-under-cap-discount).

### What Happens When I Destroy a Construction Yard?

The yard is wrecked, 10% of its storage becomes loot, and it costs reputation: -5,000 with the faction, or -1,000 if you are already Hostile or worse (-5,000 or lower). The wreck is removed when you leave the system or load the game. Until then, the faction still counts it as a yard. The ships already in its queue are not cancelled. Once the wreck is removed, each queue entry counts the yard as having 1 slot, so only one entry per destroyed yard progresses at a time, and the ships deliver as normal.

The faction builds a replacement at its next hourly economy tick if its wealth is at least 50,000. The replacement takes 3600 s and comes back at tier 1, so a destroyed tier-5 yard returns at about a sixth of the throughput. A faction with 7 or more extractors may have a second yard that keeps working.

Details: [What Happens When You Destroy a Yard](faction-economy.md#what-happens-when-you-destroy-a-yard).

### What Slows a Faction Down, and What Does Not Work?

Ranked by production removed per reputation spent:

1. Kill warship deliveries of a faction that is already your enemy. This costs no reputation.
2. Destroy its only Construction Yard. New builds stop until the replacement is done (about 1 to 2 hours), and throughput can drop to a sixth.
3. Destroy its Defense stations. The garrison is lost, and the faction pays 100,000 and 3600 s for a replacement.
4. Capture war-zone systems. The faction loses about 20,000 wealth per hour per system. A captured system also pays no taxes for its next one or two hourly tax payments, which is the 1 to 2 hours of unrest.

These do not work:

- Attacking ore or material supply. Ships cost wealth only.
- Raiding a yard. It pauses station jobs, not the warship queue.
- Destroying a yard and expecting its queue to stop.
- Wiping fleets, which makes replacement cheaper and faster.

Taking Kolyatov, Stellar, Luminate or Darkspace below a third of the war zone also raises its home subsidy. Corsairs and Canisec have no yards and are limited only by wealth.

Details: [Ways to Slow a Faction](faction-economy.md#ways-to-slow-a-faction), [What Does Not Work](faction-economy.md#what-does-not-work).

### How Do I Join a Smaller Faction's War?

A faction with an embassy and no headquarters can be joined once your embassy missions have brought it 1,500,000 wealth, or once you pay a 10,000,000 credit donation. The payment does not add to the faction's wealth. These factions are Mindus Holdings (the Mining Guild), Steel Vultures (the Salvage Guild), the Corsair Syndicate and Stranded. When you join, the faction gets 1,000,000 wealth, and its fleets are filled to full strength at that system. It also gets a Defense station with a garrison if there is a free slot, and a yard if it has none in home space. For Mindus Holdings, Steel Vultures and Stranded, the same embassy wealth sets their fleet cap (3 to 5), so feeding them wealth raises it.

Details: [Joining a Faction's War](faction-economy.md#joining-a-factions-war).

## Stations, Garrisons and Reputation

### How Tough Are Hubs and Defense Stations?

A hub or Defense station takes much more damage than a mission station. The multiplier on part hit points depends on where the part is:

| Where | Multiplier |
|---|---|
| A hub in a headquarters system | 30 times |
| A hub in any other war-zone system | 20 times |
| A faction Defense station in a war-zone system | 20 times |

At level 60 that is about 36.2 times in a hub or Defense station and 54.3 times in a headquarters hub. A faction Construction Yard or Refinery in a war-zone system has no multiplier, even though the Defense station beside it does.

Details: [How Tough a Hub or Defense Station Is](war-stations.md#how-tough-a-hub-or-defense-station-is).

### What Do Defense Stations and Garrisons Do?

A Defense station holds a garrison and fires on enemies. Garrison size follows its tier:

| Tier | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| Garrison ships | 3 | 4 | 5 | 6 | 8 |
| Garrison budget (points) | 270 | 405 | 540 | 720 | 900 |

The garrison counts in full toward a system's Defense number, summed across every Defense station there. In practice, garrisons are refilled only by warships built at yards. A system holds at most 2 faction stations of any type. In a live fight at the station, the garrison ships spawn as real ships, and ships you kill are removed from the garrison. A faction builds one Defense station at a time, on a frontline system, at a cost of 100,000 wealth and 3600 s.

Details: [Defense Stations and Garrisons](war-stations.md#defense-stations-and-garrisons), [Defense Stations](faction-economy.md#defense-stations).

### What Does Destroying a Station Cost in Reputation?

| Action | Reputation with that faction |
|---|---|
| Destroying a faction station (not a hub) while you hit it live, if a part you hit was among those destroyed | -5,000, or -1,000 if you are already Hostile or worse |
| Killing parts of a hub | 5 each if you are at -500 or lower with the owner, 100 each otherwise |
| Destroying a Construction Yard | -5,000, or -1,000 if already Hostile or worse |
| Force-attacking a ship of a faction that is not your enemy | -1,000 per ship, then -3,500 more per kill while you are Wary or better |

A station destroyed only by the simulation costs you nothing. No station or part cost applies while a Decoy Transponder is active ([the gate](../late-game/README.md)). A full Large conquest layout has 45 parts, so wrecking a friendly station can cost several thousand reputation in part kills before the 5,000 for the station itself.

Details: [Reputation Cost of Destroying Stations](war.md#reputation-cost-of-destroying-stations), [Reputation and Force Attacks](../basics/stations.md#reputation-and-force-attacks).

### Does Damage to a War-Zone Station Last?

Not unless you take the hub. Stations rebuild at full HP from the same generated layout. This happens about an hour of game time after you leave, and every conquest update rebuilds every station except the one you are at and abandoned hubs (hubs with no owner). A hub is also rebuilt when a system is claimed, but a siege capture does not rebuild it. It waits for the hourly rebuild. Killing parts is therefore temporary unless the hub itself is lost.

Details: [How Long Damage Lasts in a War Zone](war-stations.md#how-long-damage-lasts-in-a-war-zone).
