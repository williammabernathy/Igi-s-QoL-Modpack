# Common questions

These are short answers to questions players ask about the Conquest stage (level 28 and up): the 30% sector-control mission, the war map, battles and captures, faction wealth and warships, and stations. Each answer ends with a link to the section that has the full numbers and how they were read from the game code. Values are for game version 0.8.3.4. Terms are in the [conquest glossary](glossary.md) and the [basics glossary](../basics/glossary.md).

## The 30% sector-control mission

### What counts toward the 30%?

Your chosen faction needs to own 30% of the systems in the Conquest sector (Ara Martis). Only systems owned by exactly that faction count, and its headquarters counts. Allies, rival factions and unowned systems do not. The total includes every system in the sector, owned or not, and every rival's headquarters. The Darkspace region, the staging sectors and the Frontier are not in it.

The sector has 70 to 84 systems, fixed when the galaxy is created. The number you need:

| Total systems | 70 | 71 to 73 | 74 to 76 | 77 to 80 | 81 to 83 | 84 |
|---|---|---|---|---|---|---|
| Systems the faction must own | 21 | 22 | 23 | 24 | 25 | 26 |

To find your own total, divide the faction's system count on the galaxy map by its Area Conquered percentage.

Details: [What the objective counts](sector-control.md#what-the-objective-counts).

### How often does the 30% progress update, and when do I have to be at 30%?

Once an hour. The percentage is stored when the "Conquest Update" countdown on the galaxy map runs out, so both the "Area Conquered" row (press E on the galaxy map, Factions tab, click the faction) and the mission's objective line lag the map by up to an hour. A capture between two updates shows up late.

The percentage can also go back down, because every update recomputes it from the current owners. Nothing latches it. It only has to read 30% or more at the moment you talk to the faction's commander in its headquarters. After that the mission completes and later drops do not matter. If it hit 30% at one update and is gone at the next, you still succeed if you are already standing at the commander.

Details: [What the objective counts](sector-control.md#what-the-objective-counts).

### What is the best way to finish the 30% mission?

Let your faction's own fleets take the easy systems, help on the hard ones, and time your hand-in. You cannot hold systems yet, so everything you do nudges a war that NPC (non-player character) factions fight on their own. A condensed plan:

1. Pick a faction that is not your enemy, has a headquarters, and borders Darkspace Compact systems.
2. Look at the neighbors of its frontline. A neighbor with no resident fleet and a Defense row under about 1,700 is a one-fleet job. The faction takes it on its own, so leave it.
3. For a neighbor with a resident 2000-point fleet, do 5 war effort missions at your faction's hub next to it within 3 hours, picking missions whose reward line names the target faction ("-10% for X next system"). That removes 145 to 400 attacker points. It does not remove the fleet.
4. For a hard neighbor next to your faction's idle fleets, with the owner already your enemy, fly in and kill the hub parts. The system is abandoned and your faction's nearby fleet claims it. Skip the Defense stations.
5. Time it against the Conquest Update card, then be at the commander in the headquarters as soon as the objective shows 30% or more.

Do not wipe a rival major's fleets. A wiped fleet frees a slot, which halves its build costs and triples build speed, and a major at 2 fleets or fewer gets a 1500-point Great Gate reinforcement.

Details: [A step-by-step plan](sector-control.md#a-step-by-step-plan).

### What are all the ways I can push my faction's share?

| Lever | Effect |
|---|---|
| Kill a resident fleet or garrison ships | Every point removed from the target cuts what attackers must bring by 1.15. A resident 2000-point fleet turns a target from assault-only into a one-fleet job. |
| War effort penalty on the target | Removes 125 to 350 from the target's station term. Needs 5 non-idle missions in 3 hours. |
| Kill the hub | Abandons the system; the faction's fleet claims it. Needs the owner to be your enemy already. |
| War effort bonus at your frontline hubs | Up to +250 to 700 of Defense, which protects systems your faction already has. Needs 10 missions at the system, or 20 at a neighbor, in 3 hours. |
| Embassy missions | 80,000 to 120,000 wealth each. Only helps rebuild a lost fleet; does not raise the fleet cap. |
| Kill enemy warship deliveries | Removes a ship the enemy already paid for. Rarely moves a system count. |

Details: [What moves your faction's share](sector-control.md#what-moves-your-factions-share).

### What can go wrong when I try to help?

- A hub kill with no besieger of your faction nearby gives the system to whoever arrives first. Unowned systems are the top target for every NPC faction, and a claim by an arriving fleet needs no siege and no wait.
- A hub kill during a rival's siege hands the system to that rival if the siege is in its hub stage.
- You cannot shoot a hub of a faction that is not your enemy. Making it one costs about 4,500 reputation (-1,000 for the force-attack, -3,500 for the kill), which is enough to push most factions under -500.
- Pushing a faction to 0 systems is only a delay. It sits out 5 conquest updates, then its fleets start claiming again.
- When your faction passes 10%, the smaller majors can start attacking it. Once it is larger than Darkspace, every NPC faction prefers it as a target.
- War effort needs a mission from a hub in a war-zone system. Embassy missions give wealth only, and a mission at another faction's hub strengthens that faction's stations.
- Wealth and fleets do not raise the fleet cap. It rises only with systems held: 6 at 15, 7 at 30.

Details: [Pitfalls](sector-control.md#pitfalls).

### Why is my faction's share not growing on its own?

The game has no growth rate to read. It only has limits on what NPC fleets will attack. A lone 2000-point fleet attacks only at 1.15 times a target's next-battle strength or better, so it can take anything up to about 1,739. Above that the faction needs an assault of at least 2 ready fleets, and it runs one assault at a time. Travel is 60 s plus 150 s per jump, a siege stage takes at least 600 s, and planning runs every 600 s.

A faction holding under 10% of the sector only targets unowned systems or owners at 10% or more. At the start none of the three majors is at 10%, so they ignore each other and go after Darkspace and unowned systems. A resident fleet in a system also pushes it above the single-fleet limit, so those systems wait for an assault.

Details: [What the war does without you](sector-control.md#what-the-war-does-without-you).

### Can I hold or claim systems before level 60?

No. The game refuses with "Claiming systems unlocks with Beyond the Front". Fielding your own fleets, building outposts or Defense stations and the empire level tab unlock with that mission too, which opens at level 60 once you have finished one 30% mission ([gate](../expansion/README.md)). Until then you can run Conquest missions, let ECHO run idle ones, attack stations and ships live, intercept fleets and deliveries, and join a smaller faction's war.

Details: [What you can and cannot do](sector-control.md#what-you-can-and-cannot-do).

## Starting the mission and choosing a faction

### Spoiler: How do I get the 30% mission?

1. Accept the story step at level 28, then find the three embassies and talk to their representatives. A representative offers to align you with that faction.
2. The faction's first mission has four steps: go to its embassy gate, talk to the commander at its headquarters, earn 10 contribution points by running missions for it, and talk to the commander again. A Conquest mission from its hub or embassy adds 3 to 6 points (1 to 3 if ECHO completes it idle), doubled when the mission's enemy is a conquest faction other than the two raider factions, plus a bonus from your conquest rank. That is usually 2 to 4 missions. It pays 2,000,000 credits and 60 Vanguard Marks.
3. The second mission has the 30% step and a report-back step. It cannot be abandoned and has no time limit. It pays 400 Vanguard Marks, 10,000,000 credits and 1 skill point. If the faction is already at 30%, the percentage step is done the moment the mission starts.

Details: [Getting the mission](sector-control.md#spoiler-getting-the-mission).

### Spoiler: Why does the representative refuse to align me?

The offer is refused if the faction is your enemy, or while the faction has no headquarters. The game counts a faction as your enemy when you are at war with it or your reputation with it is below -500, which is the Wary level or worse (Neutral covers -499 to 1,499). Raise your standing with that faction first, or pick another one.

Details: [Getting the mission](sector-control.md#spoiler-getting-the-mission).

### Spoiler: Is the faction choice permanent?

No. The first mission's description says "The choice of faction is not permanent". The game tracks your standing with all conquest factions all the time and accepts any of the three 30% missions as the requirement for what follows.

Details: [Getting the mission](sector-control.md#spoiler-getting-the-mission).

### Spoiler: Can I work on more than one faction's chain?

Yes. You can start the chain for more than one faction, and finishing any one of the three 30% missions is enough. The cheapest route is whichever of your factions the war favors. Check each faction's Area Conquered row on the Factions tab (press E on the galaxy map) before you commit.

Details: [Getting the mission](sector-control.md#spoiler-getting-the-mission), [A step-by-step plan](sector-control.md#a-step-by-step-plan).

### Spoiler: What are the three kinds of "contribution"?

The game uses the word for three separate counters. "Your Contribution" on the Faction overview is your conquest standing with a faction, and it sets your conquest rank. The 10 points in the first mission are a counter for that one step only. "Wealth your missions brought" is what your embassy missions delivered to a faction, and it matters for joining a smaller faction's war.

Details: [Getting the mission](sector-control.md#spoiler-getting-the-mission), [Joining a faction's war](faction-economy.md#joining-a-factions-war).

## The system tooltip, war effort and fleet labels

### What does the Defense number on a system tooltip mean?

It is the strength the defenders put into a siege round, in the same unit as ship points. An unowned system shows 0. The number is the sum of three terms:

```
Defense = station firepower term
        + garrison points of every Defense station in the system
        + points of every defending fleet in the system
```

The station term uses the hub and every Defense station of the owner. It has a floor of 250, and firepower above 700 does not count, so extra turret platforms past that only add hull. A Defense station here is a station type that holds a garrison, not the Defense number. Garrisons run 270 to 900 points per station by tier. A full NPC fleet is 2000 points. As an illustration: a medium hub (480), one tier-3 Defense station (about 540) and one full fleet give about 3,020.

NPC factions do not compare against this number. They use next-battle strength, which the game never shows: the weakest Defense station's strength if there are any, otherwise the full Defense number. The tooltip number is the upper bound.

Details: [What the Defense number means](war.md#what-the-defense-number-means), [What NPC factions use instead of the Defense number](war.md#what-npc-factions-use-instead-of-the-defense-number).

### How do I lower or raise a system's Defense?

To lower it:

- Apply a war effort penalty. At the maximum it halves the station term.
- Destroy defender ships or fleets. Their points come off one for one, and intercepting fleets still traveling removes the relief term.
- Destroy turret platforms or the core by hand. The parts must be fully destroyed.
- Attack within a few hours of a capture. The station profile is halved while the system is settling.
- A hub siege that breaks station structure lowers the station term toward the 250 floor.

To raise it, use a war effort bonus (up to x2 on the station term) or add turret platforms, which only helps up to 700 firepower.

Details: [Ways to lower or raise Defense](war.md#ways-to-lower-or-raise-defense).

### What is war effort and what does it do?

War effort is a multiplier on a system's station firepower, raised by Conquest missions you complete. The tooltip shows it as "Your war effort", green for a bonus and red for a penalty, with the time left.

```
multiplier = max(0, 1 + bonus - penalty)
```

Each non-idle mission adds 0.1. At the mission's system it adds to the bonus if the system is owned by the mission's faction. Each neighboring system owned by that faction gets half of that bonus. Each neighbor owned by the mission's enemy faction gets the same step as a penalty. The bonus caps at +1.0 (x2) and the penalty at 0.5 (x0.5). Each side has one timer of 3 hours, and every new step resets it. When it runs out, the whole bonus or penalty clears at once.

Idle missions done by ECHO use a step of 0.1 x (1 + 0.2 x points in ECHO The Conqueror). The multiplier applies to the station term of the Defense number and to the Combat Power, Drone Power and Torpedo Power of station parts while you are fighting at a hub or Defense station there.

Details: [War effort](war.md#war-effort), [War effort on station weapons](war-stations.md#war-effort-on-station-weapons).

### What does "Reinforcing" on a fleet mean?

It means the fleet is moving to a frontline system, or is retreating. An idle fleet that is not on the frontline gets the label when it is sent to the best-scoring frontline system. Every retreating fleet shows it too, and so do warship deliveries. A fleet retreats when it drops below 35% of its target points and shows "Reinforcing" until it is back to 75%.

Details: [Fleet labels on the map](war.md#fleet-labels-on-the-map), [Retreats, recovery and distress](war.md#retreats-recovery-and-distress).

### What do the other fleet labels mean?

| Label | Meaning |
|---|---|
| Attacking | Sent to take a system, or an assault launching, or a supporter joining a battle |
| Gathering for an assault | Fleets heading to or waiting at the staging system of an assault |
| Holding position | Idle on a frontline system; also the default after a battle or a finished refit |
| Guarding faction space | Leftover fleets spread over the faction's station systems |
| Responding to a distress call | Sent to a besieged or raided system the faction owns |
| Raiding an outpost | Sent to raid an economic outpost |
| Refitting - {time} left | Being refitted, 300 s per refit |

Details: [Fleet labels on the map](war.md#fleet-labels-on-the-map).

## Battles, sieges and captures

### How do battles and sieges resolve?

There are four kinds of fight: a fleet clash against a defending fleet (600 s cap), a siege of one Defense station (1800 s), a siege of the hub (1800 s) and a raid on an economic outpost (900 s). When you are not there, they run as simulated rounds every 60 s. Each side deals about 4% to 6% of its own strength per round, and damage is spent on that side's ships at random, with cheaper ships picked more often. Over a full siege each side deals about 1.5 times its own strength in total.

Each round the attacker's strength is also scaled by a hull edge of up to plus or minus 15%, from the gap between the two sides' average hull ratings.

If you are at the battle, the game spawns the real ships and the clock stops. The fight ends when one side has no ships left in the zone.

Details: [Types and stages](war.md#types-and-stages), [One round](war.md#one-round), [Hull edge](war.md#hull-edge).

### How is a system captured?

A hub siege ends in capture early if all of these hold: 600 s have passed, the system is not a headquarters, the station structure is at 0, no garrison ships or defending fleets are left, and the attackers have at least 1.25 times the current Defense number in points. At the 1800 s cap:

```
structure broken, capturable, points >= 1.25 x Defense  ->  Captured
points >= Defense                                       ->  Skirmish
otherwise                                               ->  Repelled
```

On capture, the defending NPC fleets are wiped, the old owner's Defense stations are destroyed, the hub and economic outposts flip to the attacker, war effort clears, and the settling timer starts.

Details: [How a siege ends](war.md#how-a-siege-ends), [Capture by siege](war.md#capture-by-siege).

### What happens in a system right after it changes hands?

For the first 2 conquest updates (1 to 2 hours) the system is in unrest and pays no taxes. For about 5 to 6 hours after the change, it is settling, and the Defense station profile (hull and firepower) is halved. The 250 floor is not halved. That makes the hours after a capture the easiest time to attack it again.

Details: [Settling and unrest](war.md#settling-and-unrest).

### What happens if I destroy a hub?

The system is abandoned and has no owner. The game then gives it to the lead attacker of a siege that is in its hub stage, if there is one. Otherwise the system is unowned and the first fleet to arrive claims it. Unowned systems are every NPC faction's first choice. When a fleet claims a system that a different faction abandoned, that faction's Defense stations in it are destroyed first. The code does not check for a headquarters here, so destroying a headquarters hub abandons it (not verified in play).

Hub parts take x20 hit points (x30 at a headquarters). Part kills cost 5 reputation each if you are at -500 or lower with the owner, and 100 each otherwise. A hub is not a faction station, so the -5,000 station penalty does not apply.

Details: [Unowned and abandoned systems](war.md#unowned-and-abandoned-systems), [What moves your faction's share](sector-control.md#what-moves-your-factions-share).

### How do NPC factions pick targets?

Idle fleets decide in a fixed order: retreat if below 35%, refit if due, answer a distress call, attack, join an assault, raid, then rally to the frontline and guard. A fleet attacks alone only at 1.15 times the target's next-battle strength or better. Among valid targets it prefers:

1. A target it is already standing in.
2. The owner with the most war-zone systems (unowned systems rank above any owner).
3. The lowest next-battle strength.
4. The fewest jumps.

Attacks reach up to 4 jumps from a frontline. A faction holding under 10% of the war zone only considers unowned targets or owners at 10% or more. When no single fleet is enough, 2 or more ready fleets gather at a staging system next to the target, merge and attack. A faction runs one assault at a time.

Details: [Choosing a target](war.md#choosing-a-target), [Assaults](war.md#assaults).

### How strong are the fleets and systems I face?

Conquest fleets target 2000 points each. NPC warships are built at your effective level (or the highest system level, if higher) plus 6. Systems rise toward your level every hour and settle at about your level plus 4. That puts a fleet at about your level plus 10, which from level 30 is about x1.5 damage and x1.6 to x2 HP compared with a fleet at your own level. Old fleets can lag up to 3 levels behind.

Details: [Fleet levels](war.md#fleet-levels), [What you face](sector-control.md#what-you-face).

## Faction wealth and warships

### Where does a faction's wealth come from?

| Source | Amount |
|---|---|
| Taxes | 5,000 to 15,000 per owned system per hour, doubled in the war zone, 0 while the system is in unrest |
| Trade deliveries | 10% of the cargo value of a finished delivery, 14% when it goes to an embassy |
| Intertrade sales | 10% of the sale value |
| Home subsidy | Kolyatov, Stellar, Luminate and Darkspace: 120,000 x (1 - share / (1/3)) per hour, never below 0 |
| Your embassy missions | 80,000 to 120,000 each (halved if idle) |
| Raids | What a completed raid takes from a victim |

The home subsidy is 120,000 at 0% of the war zone, 84,000 at 10%, 48,000 at 20% and 0 at 33.3% or more. On average a faction earns 10,000 per system per hour outside the war zone and 20,000 inside it.

Details: [Where a faction's wealth comes from](faction-economy.md#where-a-factions-wealth-comes-from), [Catch-up mechanics](faction-economy.md#catch-up-mechanics).

### What do factions spend wealth on?

| Spending | Cost |
|---|---|
| New economic outpost | 50,000 |
| Defense station | 100,000 |
| Station upgrades | 15,000 / 25,000 / 50,000 / 100,000, from tier 1 / 2 / 3 / 4 |
| Warships | 750 to 30,000 per hull by size (see below); half price while under the fleet cap |
| Great Gate fleets | Full roster price, when the faction is eligible to pay |

Ships cost wealth only, no materials. Cutting ore or goods supply does not slow warship production.

Details: [What factions spend wealth on](faction-economy.md#what-factions-spend-wealth-on).

### How is wealth affected by a raid?

A completed raid moves wealth from the victim to the raider:

```
wealth drained = the smaller of
                 the victim's wealth
                 1.5 x its average hourly income (home subsidy not counted)
raider earns   = wealth drained + 10% of the value of the materials taken
```

An NPC raider also takes half of every stack in the station's storage. The raided outpost pauses production for 3600 s and cannot be raided again for 14,400 s. A faction with a big stockpile loses only about one and a half hours of income per raid. Raids do not touch the warship queue, Defense stations or deliveries.

Details: [Raids and the economy](faction-economy.md#raids-and-the-economy).

### How is wealth affected by killing a warship delivery or a convoy?

Killing a warship delivery removes one ship for good. The faction paid for it when it was queued, nothing is refunded, and there is no loot because it carries no cargo. The planner re-queues the missing points at its next 600 s pass.

Killing a cargo convoy cuts the run's trade or sales income in proportion to the ships you kill. Killing every ship ends the run with no income booked, and the next run goes out normally. That is minor next to taxes. Each convoy ship drops 40% to 60% of each non-ammo cargo stack as loot under your name.

If the faction is not your enemy, opening fire costs -1,000 reputation per ship and each kill another -3,500 while you are Wary or better. If it is already your enemy, it costs nothing.

Details: [Intercepting deliveries and convoys](faction-economy.md#intercepting-deliveries-and-convoys).

### Do my embassy missions help a faction?

Yes. Each one adds 80,000 to 120,000 wealth to that faction (halved for idle ones), and the faction gets it whether or not you later fight it. A full 2000-point fleet costs roughly 40,000 to 130,000 at full price. The wealth helps rebuild a lost fleet at once. It does nothing for a faction whose fleets are already full, and does not raise the fleet cap. To slow a faction, stop doing its embassy missions.

Details: [Your embassy missions feed the faction](faction-economy.md#your-embassy-missions-feed-the-faction), [Ways to slow a faction](faction-economy.md#ways-to-slow-a-faction).

### How are warships built?

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
duration = (300 + 1.2 x hull points) / speed
speed    = (3 if under the fleet cap, else 1) / yard build-time multiplier
```

Yard tiers run 1 to 5. Tier 1 has 1 slot at x1.0 build time, tier 5 has 3 slots at x0.5. For each free slot the planner takes the first that applies: a garrison shortfall at a Defense station, the fleet furthest below 35% of its target, founding a new fleet, or topping up the fleet furthest below 100%. A finished ship flies for 30 s + 30 s per jump. A fleet of 2000 points costs roughly 40,000 to 130,000 at full price.

Details: [How warships get built](faction-economy.md#how-warships-get-built), [Construction Yards](faction-economy.md#construction-yards).

### How many fleets can a faction field?

For Kolyatov, Stellar, Luminate and the Corsair Syndicate when it holds a war-zone system, the cap is the smaller of 8 and 5 + (war-zone systems held / 15, rounded down). That is 5 below 15 systems, 6 at 15 to 29, and so on. The Darkspace Compact is fixed at 5. Wealth does not raise the cap, only systems held do. While a faction has fewer fleet slots in use than its cap, every ship costs half and builds three times faster.

Details: [Fleet caps](faction-economy.md#fleet-caps), [The under-cap discount](faction-economy.md#the-under-cap-discount).

### What happens when I destroy a Construction Yard?

It is wrecked, 10% of its storage becomes loot, and it costs reputation: -5,000 with the faction, or -1,000 if you are already Hostile or worse (-5,000 or lower). The ships already in its queue are not cancelled. Each queue entry counts the yard as having 1 slot, so only one entry per destroyed yard progresses at a time, and the ships deliver as normal.

The planner builds a replacement on its next hourly step if wealth is at least 50,000. It takes 3600 s and comes back at tier 1, so a destroyed tier-5 yard returns at about a sixth of the throughput. A faction with 7 or more extractors may have a second yard that keeps working.

Details: [What happens when you destroy a yard](faction-economy.md#what-happens-when-you-destroy-a-yard).

### What slows a faction down, and what does not work?

Ranked by production removed per reputation spent:

1. Kill warship deliveries of a faction that is already your enemy. No reputation cost.
2. Destroy its only Construction Yard. New builds stop until the replacement is done (about 1 to 2 hours) and throughput can drop to a sixth.
3. Destroy its Defense stations. The garrison is lost and the faction pays 100,000 and 3600 s for a replacement.
4. Capture war-zone systems. The faction loses about 20,000 wealth per hour per system, and a system pays no taxes for 2 conquest updates after capture.

What does not work: attacking ore or material supply (ships cost wealth only), raiding a yard (it pauses station jobs, not the warship queue), and destroying a yard while expecting its queue to stop. Wiping fleets makes replacement cheaper and faster. Taking Kolyatov, Stellar, Luminate or Darkspace below a third of the war zone raises its home subsidy. Yardless Corsairs and Canisec are limited only by wealth.

Details: [Ways to slow a faction](faction-economy.md#ways-to-slow-a-faction), [What does not work](faction-economy.md#what-does-not-work).

### How do I join a smaller faction's war?

A faction with an embassy and no headquarters can be joined once your embassy missions have brought it 1,500,000 wealth, or once you pay a 10,000,000 credit donation. The payment does not add to the faction's wealth. These are the Mining Guild, Salvage Guild, Corsair Syndicate and Stranded. On joining, the faction gets 1,000,000 wealth, fleets seeded to full at that system, a Defense station with a garrison if there is a slot, and a yard if it has none in home space.

Details: [Joining a faction's war](faction-economy.md#joining-a-factions-war).

## Stations, garrisons and reputation

### How tough are hubs and Defense stations?

They take much more damage than a mission station. The multiplier on part hit points depends on where the part is:

| Where | Multiplier |
|---|---|
| A hub in a headquarters system | x30 |
| A hub in any other war-zone system | x20 |
| A faction Defense station in a war-zone system | x20 |

At level 60 that is about x36.2 in a hub or Defense station and x54.3 in a headquarters hub. A faction Construction Yard or Refinery in a war-zone system has none of that, even though the Defense station beside it does.

Details: [How tough a hub or Defense station is](war-stations.md#how-tough-a-hub-or-defense-station-is).

### What do Defense stations and garrisons do?

A Defense station holds a garrison and fires on enemies. Garrison size follows its tier:

| Tier | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| Garrison ships | 3 | 4 | 5 | 6 | 8 |
| Garrison budget (points) | 270 | 405 | 540 | 720 | 900 |

The garrison counts in full toward a system's Defense number, summed across every Defense station there. In practice garrisons are refilled only by warships built at yards. A system holds at most 2 faction stations of any type. In a live fight at the station, the garrison ships spawn as real ships, and ships you kill are removed from it. A faction builds one Defense station at a time, at 100,000 wealth and 3600 s, on a frontline system.

Details: [Defense stations and garrisons](war-stations.md#defense-stations-and-garrisons), [Defense stations](faction-economy.md#defense-stations).

### What does destroying a station cost in reputation?

| Action | Reputation with that faction |
|---|---|
| Destroying a faction station (not a hub) while you hit it live | -5,000, or -1,000 if you are already Hostile or worse |
| Killing parts of a hub | 5 each if you are at -500 or lower with the owner, 100 each otherwise |
| Destroying a Construction Yard | -5,000, or -1,000 if already Hostile or worse |
| Force-attacking a ship of a faction that is not your enemy | -1,000 per ship, then -3,500 more per kill while you are Wary or better |

A station destroyed only by the simulation costs you nothing. A full Large conquest layout has 45 parts, so wrecking a friendly station can cost several thousand reputation in part kills before the 5,000 for the station itself.

Details: [Reputation cost of destroying stations](war.md#reputation-cost-of-destroying-stations), [Reputation and force attacks](../basics/stations.md#reputation-and-force-attacks).

### Does damage to a war-zone station last?

Not unless you take the hub. Stations rebuild from their seed at full HP: about an hour of game time after you leave, and every conquest update rebuilds every station except the one you are at and abandoned hulks. A hub is also rebuilt when it changes hands. Killing parts is therefore temporary unless the hub itself is lost.

Details: [How long damage lasts in a war zone](war-stations.md#how-long-damage-lasts-in-a-war-zone).
