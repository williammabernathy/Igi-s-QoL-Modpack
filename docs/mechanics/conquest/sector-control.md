# Sector control

This page covers the 30% sector-control mission and what you can and cannot do in the war before you hold a system of your own. It applies from the level 28 story step into the Conquest sector ([Conquest](README.md)) until you accept "Beyond the Front" ([gate](../expansion/README.md)). Values are read from the game code of version 0.8.3.4 (the decompiled source, not in-game testing). Anything not confirmed in code is marked `(not verified)`. The last section, "In the game code", has the method names behind each claim.

Terms and abbreviations are explained in the [conquest glossary](glossary.md) and the [basics glossary](../basics/glossary.md). Battles, the Defense number and NPC fleet planning are in [war](war.md).

## The 30% sector-control mission

Three story missions ask you to push one faction to 30% of the systems in the Conquest sector: "Hostile Takeover" (Stellar Industries), "The People's Sector" (Kolyatov Collective) and "Harmony" (Luminate Combine). Finishing any one of them is required before "Beyond the Front" is offered (see [the gate](../expansion/README.md)). You cannot hold systems yourself at this point, so your play changes the outcome only by nudging a war that NPC (non-player character) factions fight on their own. This section covers how to get the mission, what its objective counts, where in the game you can read progress, how the war behaves without you, what moves the share, and a plan.

> In short
> 30% means your chosen faction owns 30% of the systems in the Conquest sector: 21 to 26 systems, depending on how many the galaxy has (70 to 84).
> Progress is the "Area Conquered" row for that faction (press E on the galaxy map, open the Factions tab, click the faction), and the mission's own objective line "Conquer for {faction} {current}/30.00%".
> The number refreshes once an hour, when the "Conquest Update" countdown on the galaxy map runs out. A capture between refreshes shows up late.
> To win, let the faction's own fleets take the easy systems, help on the hard ones with war effort missions or by destroying a hub next to your faction's idle fleets, and be standing at the faction's commander in its headquarters when the number reads 30% or more.

Some terms before the details. The war zone is every system where factions fight over ownership: the Conquest sector (Ara Martis, the original war zone). Only the Conquest sector counts for this mission. Every war-zone system has a hub, its main station (the map calls it "Space Station"), which carries the owner's flag; destroying the hub abandons the system. A faction's headquarters is the one system it cannot lose to a fleet; its hub has the faction's commander and shop. An ordinary owned system that fleets can besiege and capture is an outpost system.

In the code: [the 30% sector-control mission](#code-the-30-sector-control-mission).

### Getting the mission

1. The story mission "Sector Control" asks you to find the three embassies (each faction's neutral station in the Staging sector, outside the war zone) and talk to their representatives. Its description says "The choice of faction is not permanent".
2. Talking to a representative offers to align you with that faction. The offer is refused if the faction is your enemy, and refused while the faction has no headquarters. The game counts a faction as your enemy when you are at war with it or your reputation with it is below -500. Reputation is a number that the game shows as a level name; below -500 means the Wary level (except exactly -500) or any worse level, while Neutral covers -499 to 1,499 (all bands are in the [glossary](../basics/glossary.md#reputation)). Otherwise you are asked to confirm and the faction's first mission ("Internship" for Stellar, "For the Collective!" for Kolyatov, "Alignment" for Luminate) is added.
3. The first mission has four steps: fly to the faction's embassy gate, go to its headquarters and talk to the commander, earn 10 contribution points by running missions for that faction ("Run missions for {faction}"), and talk to the commander again. The 10 points are a counter for this one step. They are not "Your Contribution" on the Faction overview (your conquest standing with the faction, which sets your conquest rank), and not the wealth your embassy missions bring. Each completed Conquest mission from that faction's hub or embassy (a mission on the mission board of a war-zone hub that raises war effort, or of an embassy that gives wealth) adds its contribution amount: 3 to 6 (1 to 3 for ECHO's idle missions, the ones ECHO, your ship's AI companion, completes while you are away), doubled when the mission's enemy is a conquest faction other than the Corsair Syndicate and Meridia's Chosen, plus a bonus from your conquest rank with that faction. That is usually 2 to 4 missions. The mission pays 2,000,000 credits, 60 Vanguard Marks (the currency used to buy ships and systems), XP (experience points) and the second mission.
4. The second mission ("Hostile Takeover", "The People's Sector" or "Harmony") has the percentage step and the report-back step. It cannot be abandoned and has no time limit. It pays 400 Vanguard Marks, 10,000,000 credits, 1 skill point, XP.

You can start the chain for more than one faction. Standings are tracked for all conquest factions all the time, and the game accepts any of the three second missions as the requirement. So the cheapest route is whichever of your factions the war favors. The percentage step is complete the moment the mission starts if the faction is already at 30%.

Reputation matters only for the alignment check and for docking at the headquarters. No other reputation threshold gates the mission.

In the code: [getting the mission](#code-getting-the-mission).

### What the objective counts

```
percentage = (systems owned by the mission's faction) / (all systems in the Conquest sector)
complete   = percentage >= 30%
```

| Question | Answer |
|---|---|
| Which systems count | Systems owned by exactly that faction. Its headquarters counts. Allies do not count, other factions do not count, and a system with no owner does not count. You cannot hold a system yourself before claiming unlocks, and a system you held would not count for the faction anyway. |
| What the total is | Every war-zone system in the Ara Martis part of the Conquest sector, owned or not: unowned and abandoned systems, and every rival's headquarters, are in the total. The Darkspace region (Penumbra and its neighbors), the staging sectors and the Frontier are not. |
| How many systems that is | The Ara Martis area is built from seven batches of 10 to 12 systems, so 70 to 84 systems in a normal galaxy, fixed when the game is created (derived; the exact total is not shown anywhere I found). |
| When it is computed | The game stores the percentage for every conquest faction at the start of every conquest update, the hourly step (3600 s of game time, 1 hour) whose countdown the galaxy map shows as the "Conquest Update" card. It is also stored when a system is abandoned. A capture by siege does not refresh it. So the stored value, and the objective, lag the map by up to an hour. |
| What the interface shows | The objective reads "Conquer for {faction} {current}/{30.00%}", where current is the stored value with 2 decimals. The "Area Conquered" row on the Faction overview for any conquest faction is the same stored value. |
| Can it go backwards | Yes. Every conquest update recomputes it from the current owners. |
| Do you need to hold 30% | Only at the moment you talk to the commander. Nothing latches the objective once it reads 30%. Mission steps are checked in order, and the first incomplete one is the current step. The final step ("Go to the {faction} HQ and talk to {commander}"; HQ is the game's short form of headquarters) only counts your conversation with the commander while the percentage step is complete. So the stored value must be at 30% or more when you talk to the commander in the headquarters. After that the mission completes and later drops do not matter. A value that reached 30% at one conquest update and is gone by the next is enough if you are already at the commander. |

Systems needed (derived from systems / total >= 0.3):

| Total systems | 70 | 71 to 73 | 74 to 76 | 77 to 80 | 81 to 83 | 84 |
|---|---|---|---|---|---|---|
| Systems the faction must own | 21 | 22 | 23 | 24 | 25 | 26 |

To find your total, count the faction's systems in the Conquest sector on the galaxy map and divide by its Area Conquered percentage.

In the code: [what the objective counts](#code-what-the-objective-counts).

### Where to read progress

Everything the plan below asks you to check is on screen somewhere. Where to look:

- The Faction overview. On the galaxy map press E (or click the Empire button in the map's corner panel, tooltip "Empire (E)"; the key does nothing while you type in a text field, and the window is locked during the prologue). The window is titled "Empire". Open its Factions tab, then click a faction in the list. For a conquest faction the detail shows three rows: Headquarters (the system's name), Area Conquered and Your Contribution. The Log tab is the Empire Log, your record of battles, captures and claims (it says "system captured", "attack repelled" and so on, see [Outcomes](war.md#outcomes)).
- The Conquest Update card. In the corner panel of the galaxy map, a card titled "Conquest Update" has a countdown and a bar that drains over 3600 s. Its tooltip reads "Damaged stations are rebuilt and factions rethink their strategy". When the countdown ends, the conquest update runs and Area Conquered refreshes.
- A system's tooltip. Hover a war-zone system on the galaxy map. Its conquest section has the Defense row and the "Your war effort" row. The Defense row (the Defense number) is the strength the defenders put into a siege round, counted in points (the game's fleet cards call the same unit "strength"). It adds up the hub's firepower, the garrisons (warships stationed in them) of the system's Defense stations (extra stations that fire on enemies and hold a garrison), and the fleets defending the system. War effort is a temporary multiplier on a system's station firepower that Conquest missions you complete raise, lasting 3 hours from the last one; the row is green for a bonus and red for a penalty and shows the time left.
- Next-battle strength. The game never shows this figure, which is what NPC fleets compare themselves against (see [what NPC factions use instead of the Defense number](war.md#what-npc-factions-use-instead-of-the-defense-number)). You can estimate it from what you do see: with no Defense station in the system it equals the Defense row (plus any same-faction fleets already on the way); with Defense stations it is lower, the weakest Defense station's own firepower plus its garrison plus all defending fleets (plus fleets on the way). So the Defense row is the upper bound.
- Fleet icons and their labels. Fleets show as icons on the galaxy map; each has a label for its current order, for example "Attacking", "Gathering for an assault" or "Holding position". All labels are listed in [fleet labels on the map](war.md#fleet-labels-on-the-map).
- The hub tooltip. A hub under siege shows "Under Attack! Holds {n} min at most" when you hover it.
- The mission board. Each Conquest mission lists its reward line, for example "+10% Station firepower, -10% for X next system". The part after the comma names the faction whose neighboring systems get the war effort penalty.

In the code: [where to read progress](#code-where-to-read-progress).

### What the war does without you

Start of the game. Kolyatov, Stellar and Luminate each get 3 systems, Meridia's Chosen get 3, and the Darkspace Compact gets everything else, because its starting claim limit is unlimited while the others stop at 3. With 70 to 84 systems that is about 58 to 72 Darkspace systems and 3.6% to 4.3% for each major (derived from the partition rules; the map shows the real start). Every war faction starts with fleets filled to its fleet cap, the number of fleets it may field (each one fills a fleet slot): 5 fleets of 2000 points, since the cap is 5 fleets plus 1 for every 15 systems held, up to 8, and Darkspace is fixed at 5.

What limits an NPC capture:

- Which target: unowned neighbors first, then the owner with the most war-zone systems (Darkspace early on), then the lowest next-battle strength, then the fewest jumps. A lone fleet attacks only at 1.15 times next-battle strength or better, so a 2000-point fleet can take anything with next-battle strength up to about 1739. Above that the faction needs an assault, several of its fleets meeting next to the target and merging to attack it: at least 2 ready fleets, one assault at a time per faction (see [Assaults](war.md#assaults)).
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

The Great Gate reinforcement is 1500 points and appears when the faction has fewer than half its fleet cap (2 or fewer of 5), at most once every 12 hourly economy ticks (the separate hourly step that pays taxes and the home subsidy and plans builds; the game does not show it). Darkspace gets the home subsidy but no Great Gate reinforcements. A faction that is wiped to no systems sits out 5 conquest updates, then its fleets start claiming systems again. Every build is half price and three times faster while a faction has fewer fleet slots than its cap.

Income gives the scale. A war-zone system pays 5,000 to 15,000 per hour in taxes (the "Taxes" ledger line), doubled in the war zone, and none while in unrest (the first 1 to 2 hours after a system changes owner). Three systems earn about 60,000 per hour on top of the home subsidy (derived).

In the code: [what the war does without you](#code-what-the-war-does-without-you).

### What moves your faction's share

The frontline is a faction's systems that touch an enemy or unowned war-zone system; that is where its fleets push. In the table, "non-idle" missions are the ones you fly yourself, not the ones ECHO completes while you are away.

| Lever | What it changes | Cost and limits |
|---|---|---|
| Lower the next-battle strength of a hard target | Every point of next-battle strength you remove cuts what attackers must bring by 1.15. Killing a resident 2000-point fleet takes a target from assault-only to a one-fleet job. Killing garrison ships at its Defense station removes up to 900 (270 to 900 by tier). | Real combat against ships at about your level plus 10, near stations with x20 hit points (HP). Fleets can be intercepted from the map. |
| War effort penalty on the target | Removes 125 to 350 from the target's station term at the cap, which is 145 to 400 attacker points. | 5 non-idle missions inside 3 hours, at your faction's hub in a system next to the target. The mission must name the target faction as its enemy (reward line "-10% for X next system"). |
| Kill the hub, not the Defense stations | The hub goes down, the system is abandoned, and the claim goes to a fleet of your faction (see below). When the system is claimed, the old owner's Defense stations are razed too, so you skip them. | Hub parts are x20 HP. Needs the owner to be your enemy already (you cannot force-attack a station; a plain click only targets enemies). Part kills cost 5 reputation each if you are at -500 or lower with the owner, 100 each otherwise. A hub does not count as a faction station, so the -5000 station penalty does not apply. |
| Kill the Defense station live while your fleet besieges it | The siege stage ends at once ("Defense station destroyed" in the Empire Log). | -5000 reputation (-1000 if you are already Hostile or worse, at -5000 or lower), plus part kills. |
| War effort bonus at your frontline hubs | Up to +250 to 700 of Defense for the systems that rivals attack. Protects the share you have. | 10 missions at the system inside 3 hours, 20 at a neighbor. |
| Embassy missions | 80,000 to 120,000 wealth each (halved if idle), about 100,000, which is 40,000 to 130,000 for a full 2000-point fleet at full price. | Helps rebuild a lost fleet at once. Does nothing for a faction whose fleets are already full, and does not raise the fleet cap. |
| ECHO idle Conqueror missions | Step 0.1 x (1 + 0.2 x node points) per idle mission ("ECHO The Conqueror" skill). | Idle missions also pay a smaller share of contribution and wealth. |
| Intercepting enemy deliveries | A killed delivery is a ship their wealth already paid for. | -1000 per ship force-attacked and -3500 per kill if your reputation with the faction is Wary or better when it dies; nothing if it is already your enemy. Rarely moves a system count. |

The hub-kill conversion. When a hub dies, the system is abandoned. Because claiming is locked for you, the game hands the system to the lead attacker of a siege that is in its hub stage ("system captured" in the Empire Log). If the siege is in any other stage, or there is none, the system is unowned. Unowned systems are NPC factions' first choice: they are candidates for every faction's fleets with next-battle strength 0, and a fleet already in the system counts as continuing there. Your faction's idle fleets within 4 jumps will be sent at the next planning pass, within 600 s (immediately if a battle just ended there), arriving 60 s plus 150 s per jump later. So do it where your faction's fleets are idle within 1 or 2 jumps and where no rival borders the system.

In the code: [what moves your faction's share](#code-what-moves-your-factions-share).

### Pitfalls

- A hub kill with no besieger of your faction nearby gives the system to whoever arrives first. Unowned systems are the top target for every NPC faction, so a rival or Darkspace can claim it. Claims by an arriving fleet need no siege and no wait.
- A hub kill during a rival's siege hands the system to that rival if the siege is in its hub stage.
- You cannot shoot a hub of a faction that is not your enemy. Making it an enemy costs reputation first: one force-attacked kill is -1000 and -3500, about -4,500, which is enough to make most factions enemies (under -500). Check the faction's reputation before you commit.
- Reputation with the Darkspace Compact matters later. The "Beyond the Front" questgiver stands at the Darkspace embassy ([gate](../expansion/README.md)). Whether bad reputation blocks docking there is `(not verified)`.
- Wiping a faction's fleets completely frees fleet slots. Below its cap every build is half price and three times faster. Kolyatov, Stellar and Luminate also get a 1500-point Great Gate reinforcement when they are at 2 fleets or fewer (free below 10% share). Darkspace gets only the cheaper rebuild. Killing ships out of a fleet that survives does not free a slot.
- Pushing a faction to 0 systems makes it sit out for 5 conquest updates, after which its fleets start claiming systems again. It is a delay, not a removal.
- When your faction passes 10%, the smaller majors can start attacking it. When it is larger than Darkspace, every NPC faction prefers it as a target, because the owner with the most systems is picked first.
- War effort needs a mission from a hub in a war-zone system. Embassy missions are outside the war zone and give wealth only. Missions at another faction's hub strengthen that faction's stations.
- The 30% value is stored hourly, so a capture an hour before you talk may not show yet. Watch the Conquest Update card.
- Wealth and fleets do not raise the fleet cap. The cap rises only with systems held: 6 at 15, 7 at 30.

### A step-by-step plan

1. Choose the faction. Press E on the galaxy map, open the Factions tab and click each major faction to read its Area Conquered and Headquarters rows. Pick one that is not your enemy, has a headquarters, and has a frontline with Darkspace Compact systems (its systems sit next to theirs on the map). Its second mission needs 30% of the total, rounded up (21 to 26, see the table above).
2. Finish the first mission: 10 contribution points. Take Conquest missions for that faction from its hub's mission board or from its embassy; both count. Prefer hub missions whose enemy is a conquest faction other than the Corsair Syndicate and Meridia's Chosen: their contribution is doubled. The objective line "Run missions for {faction}" shows your count.
3. Find the frontline on the galaxy map: your faction's systems next to Darkspace systems. Hover or click the fleet icons nearby. Fleets labelled "Attacking" and "Gathering for an assault" show where its next push goes.
4. Hover each neighboring system and read the Defense row. A neighbor with no resident fleet and a small garrison (Defense row under about 1,700) is a one-fleet job. The NPC faction will take it itself within a planning pass or two, so leave it alone. If it has Defense stations, its real next-battle strength is lower than the row.
5. Spend your time on the hard ones. For a neighbor with a resident 2000-point fleet, do 5 war effort missions at your faction's hub next to it within 3 hours, choosing missions on the hub's mission board whose reward line names that faction ("-10% for X next system"). That cuts 145 to 400 attacker points. It does not remove the fleet. The red "Your war effort" row in the target's tooltip shows that it worked and how long it lasts.
6. Hub-kill where your faction's fleets sit. For a hard neighbor next to your faction's idle fleets (icons labelled "Holding position" or "Guarding faction space"), with the owner already your enemy, fly to the system and kill the hub parts (x20 HP, 5 reputation each at -500 or lower). Do not bother with its Defense stations. Stay clear of rival borders. The Empire window's Log tab will show the capture ("system captured") once the faction's fleet arrives.
7. Watch the clock. The conquest update is hourly, and the Conquest Update card on the galaxy map counts it down, so you can time your captures against it. Finish your captures well before the countdown ends, then be docked at the headquarters (the "Gate to {faction} headquarters" at the faction's embassy leads there) and talk to the commander as soon as the Area Conquered row or the objective line shows 30% or more.
8. If your faction stalls below 10%, the Great Gate reinforcement and the home subsidy (about 100,000 per hour) already work for it. Do not try to wipe a rival major's fleets outright: a wiped fleet frees a slot, which halves its build costs and triples build speed, and a major at 2 fleets or fewer gets a 1500-point Great Gate reinforcement.

Not verified: how fast the three majors actually grow on their own (no rate exists in code, only the limits above); whether bad reputation with the Darkspace Compact blocks docking; the points of the "ECHO The Conqueror" node; whether idle ECHO missions choose the aligned faction's hubs; and the total number of systems in your galaxy (derived from the generator, not read from a save).

In the code: [a step-by-step plan](#code-a-step-by-step-plan).

## Level 60 or below, before your first system

This section applies until you accept "Beyond the Front" ([gate](../expansion/README.md)).

### What you can and cannot do

| You can | Notes |
|---|---|
| Run Conquest missions | Each completed mission from a war-zone station adds war effort (see [War effort](war.md#war-effort)). This needs no system of your own. |
| Let ECHO run idle Conquest missions | Larger step: 0.1 x (1 + 0.2 x points) in the "ECHO The Conqueror" node. The node's maximum point count is in game data and not read here `(not verified)`. |
| Attack stations and ships live | Hubs take x20 HP (x30 at a headquarters); see [war-stations](war-stations.md). You can only target a station's parts with a plain click if its faction is already your enemy; force-attacking a station is not possible. Destroying a faction station (not a hub) while you hit it costs -5000 reputation with that faction, or -1000 if you are already Hostile or worse (-5000 or lower). |
| Intercept fleets and warship deliveries | Intercepting a fleet builds its real ships at the intercept zone; losses come off the fleet. If the faction is not your enemy, confirming the force attack costs -1000 reputation for each ship you first target, and each kill of a force-attacked ship costs another -3500 if your reputation with the faction is still Wary or better when the ship dies. Enemy ships cost nothing. |
| Join a smaller faction's war | A faction with an embassy and no headquarters of its own can be joined once your embassy missions have brought it 1,500,000 wealth, or once you have paid a 10,000,000 credit donation. The player-populating factions are the Mining Guild, Salvage Guild, Corsair Syndicate and Stranded. The requirement and what the faction gets are in [faction economy](faction-economy.md#joining-a-factions-war). |
| Destroy a hub in a siege | Destroying a hub live ends the fight and abandons the system. With claiming locked, the besieger takes it if it was in the hub stage of a siege; otherwise the system is unowned and the next fleet to arrive claims it (see [the hub-kill conversion](#what-moves-your-factions-share)). |

| You cannot | Why |
|---|---|
| Claim, buy or hold a system | The game refuses with "Claiming systems unlocks with Beyond the Front". |
| Anything that needs a system of your own | Fielding Empire Fleets, building economic outposts, Defense stations or patrols, and the Empire level tab all unlock with Beyond the Front ([Your empire](../expansion/your-empire.md), [Empire fleets](../expansion/empire-fleets.md)). Other ways the game might give you a station before your first claim are `(not verified)`. |

In the code: [what you can and cannot do](#code-what-you-can-and-cannot-do).

### What you face

- Systems: Ara Martis systems are created at level 32 to 36. Each conquest update (hourly) lifts every system by 2 if you are more than 3 levels above it, by 1 if you are 1 to 3 levels above it, and by 1 with a 50% roll if it is level with you or up to 3 levels above you. A system 4 or more levels above you does not rise, so systems settle around your level plus up to 4 (derived).
- NPC fleet ship level: the higher of your effective level and the highest conquest system level, plus 6. With systems settled near your level plus 4, that is about your level plus 10 (derived). Fleet target size is 2000 points regardless of your level; the number of fleets per faction is in [faction-economy](faction-economy.md).
- Station turrets: generated conquest stations pick medium or large turrets (M2 or L1) only at system level 45 or higher and small ones (S2) below that. Below 45 a Defense platform scores sizes 2 (90 hull, 80 firepower); at 45 or higher with M2 it scores sizes 4 (120 hull, 160 firepower). See [war-stations](war-stations.md) for per-part data. Ungenerated stations use the flat estimates (240, 480 or 560 firepower).
- Garrisons: ship count and budget follow the station tier, not your level (3 to 8 ships, 270 to 900 points). Garrison ship levels follow the NPC fleet level above.
- Station hit points (HP): live hubs and Defense stations take x20 HP, headquarters hubs x30.

In the code: [what you face](#code-what-you-face).

### Most efficient approach

What matters before your first system is that your own faction reaches 30% of the Conquest sector, because that opens "Beyond the Front". You cannot hold anything yourself, so every action here is a nudge to the war between NPC (non-player character) factions. The counting rule, the faction behavior you are pushing against, the ranked levers, the pitfalls and a step-by-step plan are in [The 30% sector-control mission](#the-30-sector-control-mission).

Embassy missions give the faction wealth in place of war effort (amounts are in the Embassy missions row of [the levers table](#what-moves-your-factions-share)). Faction wealth buys warships (see [faction-economy](faction-economy.md)).

In the code: [most efficient approach](#code-most-efficient-approach).

## Numbers to watch

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

## In the game code

This part is the decompiled code of version 0.8.3.4 behind the sections above: class and method names, formulas as the code writes them, and the `Source:` lines. To check a claim, search the decompiled source for the method name. Anything not confirmed in code is marked `(not verified)`.

### Code: The 30% sector-control mission

| Mission | Faction | Id |
|---|---|---|
| "Hostile Takeover" | Stellar Industries | `ConquestStellar2` |
| "The People's Sector" | Kolyatov Collective | `ConquestKolyatov2` |
| "Harmony" | Luminate Combine | `ConquestLuminate2` |

The war zone is the set of `ConquestSystem` objects; the original war zone is the Ara Martis conquest sectors.

#### Code: Getting the mission

| Step | Mission id |
|---|---|
| Find the three embassies | `ConquestThreeFactions` ("Sector Control") |
| First mission | `ConquestStellar1` "Internship", `ConquestKolyatov1` "For the Collective!", `ConquestLuminate1` "Alignment" |
| Second mission | `ConquestStellar2`, `ConquestKolyatov2`, `ConquestLuminate2` |

Alignment: `ConquestMissions.FactionAlignmentChoice` makes the offer and `FactionData.IsEnemy` supplies the enemy test. The conquest rank bonus on each contribution amount is `GetFleetStrengthRewardBonus`. The percentage step and the report-back step are built by `ConquestMissions.CreateFactionConquestProgressMission`, the first mission by `ConquestMissions.CreateFactionHQIntroMission`. `HasCompletedSectorControl` accepts any of the three second missions.

Source: `ConquestMissions.FactionAlignmentChoice`, `ConquestMissions.CreateFactionHQIntroMission`, `ConquestMissions.CreateFactionConquestProgressMission`, `ConquestMissions.HasCompletedSectorControl`, `Characters.AlignWithMajorFaction`, `ConquestFleetStrength`, `ConquestStrength.OnComplete`, `MissionGenerator.AddRewards`, `FactionData.IsEnemy`

#### Code: What the objective counts

```
percentage = (conquest systems owned by the mission's faction) / (all conquest systems)
complete   = percentage >= 0.3
```

| Question | Answer |
|---|---|
| Numerator | Systems whose owner is exactly that faction (`sys.faction == faction`). |
| Denominator | Every war-zone system (`ConquestSystem`) in the Ara Martis conquest sectors. |
| How many systems that is | The Ara Martis area is seven draws of 10 to 12 systems (`CreateContinuousAraMartis`), so 70 to 84 systems in a normal galaxy, fixed when the game is created. |
| When it is computed | The game stores the percentage (`currentConqueredPercentage`) for every conquest faction at the start of every conquest update, the hourly step (3600 s of game time). It is also stored when a system is abandoned (`Conquest.UpdateConquestArea`). |
| Do you need to hold 30% | No latch exists on the objective. Mission steps are checked in order (`Mission.currentStep`), and the first incomplete one is the current step. The final step only receives your talk event (`MissionObjective.Trigger`) while the percentage step is complete. |

Systems needed: `count / total >= 0.3`.

Source: `SystemsConquered.IsComplete`, `SystemsConquered.statusText`, `Conquest.UpdateConquestArea`, `Conquest.UpdateFactionStandings`, `Conquest.DoConquestTick`, `Conquest.RefreshStandingsAndGates`, `Conquest.PopulateSystems`, `Conquest.IsWarZoneSector`, `ConquestWorld.CreateContinuousAraMartis`, `Mission.currentStep`, `MissionObjective.Trigger`, `FactionOverviewContent.AddConquestRows`

#### Code: Where to read progress

The conquest card is built by `MapCornerPanel.BuildConquestCard` (strings `MapConquestUpdateTitle`, `MapConquestUpdateTooltip`), the E hotkey is handled in `MapCornerPanel.HandleHotkeys`, and the Empire window is `EmpireWindow` (tab string `FactionOverviewTab` = "Factions", log tab `EmpireTabLog`). `CommandTabUnlocks.IsEmpireWindowUnlocked` is the prologue lock. The three conquest rows come from `FactionOverviewContent.AddConquestRows`, the tooltip rows from `MapTooltipCardBuilder.BuildConquestSection`, fleet labels from `ConquestFleetMapIcon.GetTaskLabel`, the siege tooltip from `ConquestStationUnderAttack`, and the reward line from `MissionGenerator.AddRewards`.

Source: `MapCornerPanel.BuildConquestCard` (strings `MapConquestUpdateTitle`, `MapConquestUpdateTooltip`), `MapCornerPanel.HandleHotkeys`, `EmpireWindow` (tabs, string `FactionOverviewTab` = "Factions", `EmpireTabLog`), `CommandTabUnlocks.IsEmpireWindowUnlocked`, `FactionOverviewContent.AddConquestRows`, `MapTooltipCardBuilder.BuildConquestSection`, `ConquestFleetMapIcon.GetTaskLabel`, `ConquestStationUnderAttack`, `MissionGenerator.AddRewards`

#### Code: What the war does without you

The three majors and Meridia's Chosen each stop at 3 systems and Darkspace gets the rest because of `ConquestWarSeeder.StartingClaimLimit` (unlimited for Darkspace, 3 for the others). The fleet cap is `5 + floor(systems / 15)` up to 8 (`ConquestFleetPlanner.MaxFleetsFor`) and Darkspace is fixed at 5; the initial fleets come from `ConquestWarSeeder.SeedFleets`. Target choice and the 1.15 margin are in `ConquestFleetOrders.TryAttack`, the peace rule in `ConquestFleetOrders.ApplyPeace`, the 600 s planning pass in `LivingUniverse.TickMilitaryPlanning`, the Great Gate reinforcement in `GreatGateReinforcement.GetSubsidy`, `GreatGateReinforcement.NeedsReinforcement` and `GreatGateReinforcement.TryPay`, the wiped-faction return (the "beachhead rule") in `ConquestFrontline.GetBeachheadCandidates`, and the system tax roll in `FactionEconomicPlanner.RollSystemWealth`.

Source: `ConquestWarSeeder.PartitionTerritory`, `ConquestWarSeeder.StartingClaimLimit`, `ConquestWarSeeder.SeedFleets`, `Conquest.AssignInitialSystems`, `ConquestFleetOrders.TryAttack`, `ConquestFleetOrders.ApplyPeace`, `ConquestFleetPlanner.MaxFleetsFor`, `LivingUniverse.TickMilitaryPlanning`, `GreatGateReinforcement.GetSubsidy`, `GreatGateReinforcement.NeedsReinforcement`, `GreatGateReinforcement.TryPay`, `ConquestFrontline.GetBeachheadCandidates`, `FactionEconomicPlanner.RollSystemWealth`

#### Code: What moves your faction's share

| Lever | Code detail |
|---|---|
| Lower the next-battle strength of a hard target | Fleets can be intercepted from the map (`ConquestFleetInterceptTarget`). |
| Kill the hub, not the Defense stations | On the claim `ConquestOwnership.Claim` also razes the old owner's Defense stations, so you skip them. A hub is not a `FactionStation`, so the -5000 station penalty does not apply. |
| Kill the Defense station live while your fleet besieges it | `OnDefenseStationDestroyedLive` ends that stage as `DefenseStationDestroyed` ("Defense station destroyed" in the Empire Log) at once. |

The hub-kill conversion. When a hub dies, `ConquestOwnership.Abandon` runs. Because claiming is locked for you, `EndBattlesAt` hands the system to the lead attacker of a siege in the hub stage (`Captured`, "system captured" in the Empire Log). If the siege is in any other stage, or there is none, the system is unowned. A fleet already in the system counts as "continuing".

Source: `ConquestOwnership.Abandon`, `ConquestOwnership.Claim`, `ConquestOwnership.HandOverToClaimant`, `BattleManager.EndBattlesAt`, `BattleManager.IsClaimableOnArrival`, `ConquestFrontline.GetContestedNeighbors`, `ConquestFleetOrders.TryAttack`, `ConquestFleetOrders.AttackTarget`, `ConquestWarEffort.Apply`, `MissionGenerator.GetConquestEnemyFactions`, `MissionGenerator.AddRewards`, `BattleManager.OnDefenseStationDestroyedLive`, `LootManager.HandleKillReputation`, `InterceptForceAttack.Attack`, `ConquestJoin.DeliverMissionWealth`

#### Code: A step-by-step plan

The systems the second mission needs are `ceil(0.3 x total)`.

### Code: Level 60 or below, before your first system

#### Code: What you can and cannot do

- Run Conquest missions: war effort comes from `ConquestWarEffort.Apply` when the mission completes.
- ECHO idle missions: the step is `0.1 * (1 + 0.2 * points)` in the "ECHO The Conqueror" node. The node's maximum point count is in prefab data and not read here `(not verified)`.
- Attack stations: force-attacking a `SpaceStation` is not possible. The reputation loss for a faction station is `FactionStationDestruction.GetDestroyReputationLoss`.
- Intercept: `ConquestFleetInterceptTarget` builds the fleet's real ships at the intercept zone; losses come off the fleet. The -1000 per ship is charged by `InterceptForceAttack.Attack` and the -3500 per kill by `LootManager.TryApplyInterceptKillPenalty`; the rules live in `InterceptRules`.
- Join: `ConquestJoin.CanJoin`, `ConquestJoin.HasJoinContribution`, `ConquestJoin.TryDonate`.
- Hub destroyed in a siege: `ConquestOwnership.Abandon`, then `BattleManager.EndBattlesAt` and `ConquestOwnership.HandOverToClaimant`.
- Refusals: `SystemClaim.GetRefusal` and `PlayerSectorPurchase.GetRefusal` return "Claiming systems unlocks with Beyond the Front".

Source: `ConquestStrength.OnComplete`, `ConquestWarEffort.Apply`, `ConquestJoin.CanJoin`, `ConquestJoin.HasJoinContribution`, `ConquestJoin.TryDonate`, `InterceptRules`, `LootManager.TryApplyInterceptKillPenalty`, `ConquestFleetInterceptTarget`, `FactionStationDestruction.GetDestroyReputationLoss`, `ConquestOwnership.Abandon`, `ConquestOwnership.HandOverToClaimant`, `BattleManager.EndBattlesAt`, `SystemClaim.GetRefusal`, `PlayerSectorPurchase.GetRefusal`

#### Code: What you face

- Systems: Ara Martis systems are created at level 32 to 36 (`random.RandomRange(32, 37)` in `ConquestWorld`). Each conquest update `RaiseTowardPlayer` lifts every system by 2 if you are more than 3 levels above it, by 1 if you are 1 to 3 levels above it, and by 1 with a 50% roll if it is level with you or up to 3 levels above you. A system 4 or more levels above you does not rise.
- NPC fleet ship level: `FleetLevels.GetNpcFleetLevel` is `max(your effective level, highest conquest system level) + 6`. The galaxy level comes from `FleetLevels.GetGalaxyLevel`.
- Station turrets: `CombatStationFactory` picks the turret by `poi.level >= 45`.
- Garrisons: sized by `FactionGarrison`.
- Station HP: the multiplier is `CombatStationPart.GetStationHpMultiplier`.

Source: `ConquestWorld` (system creation at `random.RandomRange(32, 37)`), `Conquest.RaiseTowardPlayer`, `FleetLevels.GetGalaxyLevel`, `FleetLevels.GetNpcFleetLevel`, `CombatStationFactory` (`poi.level >= 45` turret choice), `FactionGarrison`, `CombatStationPart.GetStationHpMultiplier`

#### Code: Most efficient approach

Source: `ConquestWarEffort`, `ConquestSystem.AddWarEffortBonus`, `ConquestFleetOrders.PlanAssault`, `ConquestFleetOrders.TryAttack`, `BattleManager.EndBattlesAt`, `ConquestOwnership.HandOverToClaimant`, `InterceptRules.AppliesKillPenalty`
