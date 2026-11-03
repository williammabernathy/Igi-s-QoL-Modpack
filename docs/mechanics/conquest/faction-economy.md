# Faction economy and ship production

This page covers where an NPC faction's money comes from, what it spends it on, how it builds economic outposts and Construction Yards, how warships are paid for, built and delivered, and which of your actions slow that down. NPC means non-player character: a faction the game controls. Every value is read from the decompiled game code of version 0.8.3.4, not measured in play. Ship point values come from the ship prefabs in the game files. This page belongs to the conquest stage, which opens at level 28 with the Sector Control missions. Battles, fleet orders, raids and captures are in [war](war.md); stations under attack in the war are in [war stations](war-stations.md), and attacking stations in general is in [stations](../basics/stations.md).

Terms and abbreviations are explained in the [conquest glossary](glossary.md) and the [basics glossary](../basics/glossary.md). The code names behind the terms are in the last part of the page, [In the game code](#in-the-game-code).

A few terms used throughout:

- Wealth, shown as "Economic Power" on the Faction overview, is a single number per faction. Ships cost wealth only, no materials, so cutting ore or goods supply does not slow warship production.
- The war zone is the Conquest sector (Ara Martis). The Frontier, Staging and Darkspace regions are not part of it. The expansion sectors join it with Beyond the Front ([Your empire](../expansion/your-empire.md)).
- An economic outpost is a station that produces something: Mining, Salvage, Refinery, Forge, Production, Trading, Recruitment or Construction Yard. The Faction overview shows them as "Economic Outposts". A system holds two. This page writes "yard" for a Construction Yard.
- Points are a ship's fleet value. Fleet cards in the game call the total "strength"; this page writes "points".
- The planner is the part of the game that decides what an NPC faction builds and where its fleets go.
- A garrison is the warships an NPC Defense station holds.
- A fleet cap is how many fleet slots a faction may fill.

## What the Faction overview shows

The Factions tab of the galaxy map shows, for an NPC faction:

| Tile or row | Value |
|---|---|
| Economic Power | The faction's current wealth, with the change against the previous hourly sample |
| Combat Power | The faction's total fleet points over all its fleets, with the same change figure. This is not the Combat Power ship stat |
| Economic Outposts | Economic outposts plus economic outposts under construction, over 2 x systems held ("slots used") |
| Building | The first economic outpost under construction and its time remaining |
| Power and Power History graphs | The last 24 hourly samples of wealth and fleet points |

The card that lists the income and spending ledger, the Construction Yards count, active ship builds, deliveries and recent builds exists in the game files but is only enabled when running inside the Unity editor, so it does not appear in the released game. The ledger entries below are therefore only visible indirectly, as movement in the Economic Power tile.

In the code: [what the Faction overview shows](#code-what-the-faction-overview-shows).

## When things happen

| Cadence | What runs |
|---|---|
| Every story second | Economic outpost builds, warship builds, warship deliveries and work-bay ship orders all advance |
| Every 3600 s (1 hour), the hourly economy tick, not shown in the game | Taxes are paid, then each faction's planner runs: economic outposts, the warship queue with Defense stations and fleet orders, and the home subsidy with Great Gate reinforcement. Then Canisec patrols, then the ledger hour closes |
| Every 3600 s (1 hour), the conquest update, a separate timer | Recalculates Area Conquered, rebuilds damaged stations and counts down settling. The galaxy map shows it as the "Conquest Update" countdown card |
| Every 600 s (10 minutes) | The warship queue is refilled and fleet orders are reassigned |
| After battles | The factions in a battle are re-planned immediately |

A yard slot that frees up is therefore refilled within about ten minutes, not an hour. Economic outposts and taxes move on the hourly economy tick.

In the code: [when things happen](#code-when-things-happen).

## Where a faction's wealth comes from

NPC factions add each earning to their wealth directly. The ledger groups earnings under these labels ("Wealth (last 24h)"):

| Label in the game | Amount | When |
|---|---|---|
| Taxes | Per owned non-reserved system, a uniform roll of 5,000 to 15,000, doubled in a war-zone system, and 0 while the system is in unrest | Hourly (hourly economy tick) |
| Trade deliveries | 10% of the cargo value of a finished delivery (sum of item cost x amount), 14% when the destination is an embassy (a faction's neutral station in the Staging sector) | When a Production station's run is unloaded at a non-faction-station destination |
| Intertrade sales | 10% of the sale value | When a Production station's run is sold into an Intertrade market |
| Home subsidy | See [Catch-up mechanics](#catch-up-mechanics) | Hourly (hourly economy tick), plus one-off grants |
| Your embassy missions | The wealth reward of your embassy missions, see below | When you complete one |
| Raids | What a raid takes: wealth drained from the victim plus 10% of the value of the economic outposts' materials | When the faction's raid on an economic outpost completes |

A faction that holds N systems outside the war zone and W systems in it earns 10,000 x N + 20,000 x W per hour on average from taxes. Unrest is the game's unnamed state for the first hours after a system changes owner. A captured system starts with a settling count of 6 that drops by one per conquest update (hourly). It pays no taxes while the count is above 4, so it pays nothing for the first two conquest updates after capture.

Trade and sales only count for Production stations. A Production station gets its first route to the nearest Intertrade market within 10 jumps. If there is none it falls back to the nearest own-faction civilian station, preferring an embassy over any other even if the embassy is farther. Mining and Salvage stations route all materials to the nearest Refinery, and Refineries route canisters to the nearest Production station, each within 10 jumps. Those chain routes pay nothing themselves.

The ledger also has a "Lost to your raids" line. NPC factions never use it: it is defined and labelled, but nothing in the game records it.

In the code: [where a faction's wealth comes from](#code-where-a-factions-wealth-comes-from).

### Your embassy missions feed the faction

A mission taken at an embassy of a conquest faction carries a faction-wealth reward of 80,000 to 120,000 (halved for idle missions, which ECHO, your ship's AI companion, completes for you while you are away). On completion the amount is added to that faction's wealth under "Your embassy missions", and to your running embassy wealth with it (the "Wealth your missions brought" counter). This is one of the three things the game calls contribution; the other two are "Your Contribution" (your conquest standing) and the 10 contribution points of the first sector-control mission. The faction gets the wealth regardless of whether you later fight it.

In the code: [your embassy missions feed the faction](#code-your-embassy-missions-feed-the-faction).

## What factions spend wealth on

| Spending | Cost | Notes |
|---|---|---|
| Stations | 50,000 per new economic outpost; 100,000 per Defense station; upgrades 15,000 / 25,000 / 50,000 / 100,000 when going from tier 1 / 2 / 3 / 4 | Paid when the build or upgrade starts |
| Warships | Per hull, by size category (table below); half price while under the fleet cap | Paid when the build is queued, never refunded |
| Ships for station work bays | Same per-size price | Paid when the build starts at a free yard, and they occupy a yard slot |
| "Great Gate fleets" | Full roster cost of the Great Gate reinforcement | Only when the faction is paid-eligible, see below |
| Raided | Drained by a raid | See [Raids and the economy](#raids-and-the-economy) |

Raiders and Canisec pay for fleets under the warship line at full roster price.

In the code: [what factions spend wealth on](#code-what-factions-spend-wealth-on).

## How NPC factions build economic outposts

### Order of builds

Each hourly economy tick the planner first makes sure the supply routes exist, then takes up to 2 actions. One action is either a new economic outpost or an upgrade:

- If economic outposts plus those under construction are below 2 x systems held, it tries to build.
- Otherwise it upgrades one economic outpost instead.

A build is blocked unless wealth is at least 50,000 and a site exists. The type chosen:

1. A faction with no Mining or Salvage station builds a Mining station first.
2. With E extractors (Mining plus Salvage), the chain target is the larger of 1 and E / 6 rounded up, of each chain stage, built in the order Refinery, Production, Construction Yard. Refinery and Production are only built when the previous stage's storage is at least 3% full (Mining or Salvage for Refinery, Refinery for Production). The Construction Yard has no such gate.
3. Once the chain targets are met, a further extractor is built only when some extractor's storage is at least 3% full. It alternates Mining and Salvage, keeping them equal with Mining first.
4. A Production station picks its trade good from what the faction can make from its own refined materials. Near an Intertrade market within 10 jumps it takes the good with the lowest supply score, with a slight lean toward higher-priced goods, and avoids goods other Production stations already plan to make. With no market in range the pick is random. When a Production station is upgraded, its good changes only if the best alternative scores under half of the current one.

Mindus Holdings and Steel Vultures are guild factions with restricted chains: the Refinery first, then the Production station, then their one extractor type (Mindus only Mining, Steel Vultures only Salvage), then a Construction Yard. After that the same stage targets and 3% gates apply, but the yard is not a stage. They never build the other extractor type.

If a Construction Yard has no valid site, the planner builds an extractor instead.

An economic outpost takes 3600 s (1 hour) to build.

In the code: [order of builds](#code-order-of-builds).

### Where economic outposts go

A system is a valid site when all of these hold:

- It belongs to the faction and is not a reserved slot.
- It is in the Frontier or Conquest quadrant ("Quadrant" is the game's name for the large map regions).
- It holds fewer than 2 economic outposts (counting those already under construction by that faction).
- Pocket systems only accept economic outposts if they are a Motherlode or Derelict Fleet system.
- A Construction Yard is never built in a war-zone system. Every yard sits in home space, meaning outside the war zone.

Among valid systems the planner picks the one with the fewest economic outposts, then the highest total economic outpost prosperity.

In the code: [where economic outposts go](#code-where-economic-outposts-go).

### Upgrades and the starting layout

When economic outpost slots are full, the planner upgrades the economic outpost with the highest storage fullness among those below their maximum tier (5, or 4 for Production). It picks the target by fullness, not by type, so a Construction Yard is not preferred. It is never upgraded unless it happens to be the fullest.

New games and fresh war zones start pre-built: 80% of slots filled for each faction (100% in the war zone).

In the code: [upgrades and the starting layout](#code-upgrades-and-the-starting-layout).

### Defense stations

A faction builds one Defense station at a time, for 100,000 wealth and 3600 s (1 hour), when it holds fewer than the cap:

```
cap = the smaller of
      (war-zone systems the faction holds / 4, rounded up)
      (frontline systems x 0.5, rounded up)
```

The site is a frontline system (one of the faction's systems that touches an enemy or unowned war-zone system) with fewer than 2 economic outposts, preferring the one with the fewest of the faction's Defense stations already. A planner-built Defense station starts with an empty garrison; the warship queue fills it (see below).

In the code: [Defense stations](#code-defense-stations).

## Construction Yards

Every yard has a tier from 1 to 5. The tier sets how many ships build at once and how fast:

| Tier | Build slots | Build-time multiplier |
|---|---|---|
| 1 | 1 | 1.0 |
| 2 | 1 | 0.8 |
| 3 | 2 | 0.7 |
| 4 | 2 | 0.6 |
| 5 | 3 | 0.5 |

A yard starts at tier 1. The only way its tier rises here is the planner upgrade above (15,000 to 100,000 wealth, only when every economic outpost slot is used, and only if the yard is the fullest eligible one). Slots are also used by bay-ship builds. A ship build reserves its slot from the moment it is queued.

The number of yards scales with extractors: one yard per 6 extractors (rounded up). A faction with up to 6 extractors has one yard. If a war faction has none outside the war zone, the game builds one at a valid home-space site.

In the code: [Construction Yards](#code-construction-yards).

## How warships get built

### Cost

A hull's price is set by its size category, not by its point value: 50 wealth per price point.

| Size | Price points | Cost (wealth) | Cost while under fleet cap | Combat hulls: fleet point values | Wealth per fleet point |
|---|---|---|---|---|---|
| 1 | 15 | 750 | 375 | 15, 20, 25 | 30 to 50 |
| 2 | 30 | 1,500 | 750 | 40, 45 | 33 to 38 |
| 3 | 60 | 3,000 | 1,500 | 80, 100, 120, 140 | 21 to 38 |
| 4 | 150 | 7,500 | 3,750 | 180, 200, 210 | 36 to 42 |
| 5 | 300 | 15,000 | 7,500 | 300, 320 | 47 to 50 |
| 6 | 600 | 30,000 | 15,000 | 460, 480 | 62 to 65 |
| 7 | 1,200 | 60,000 | 30,000 | none among combat hulls | n/a |
| 8 | 2,400 | 120,000 | 60,000 | none among combat hulls | n/a |

Size 3 hulls are the cheapest per point and size 5 and 6 the dearest. The size 7 and 8 rows exist in the price table, but the only size-7 hulls in the ship prefabs are non-combat, and no size-8 hull was found. Bay-ship build times (60 s at size 1 up to 420 s at size 8) apply to work-bay ships only, not warships.

A fleet of 2000 points costs roughly 40,000 to 130,000 wealth at full price depending on its mix.

In the code: [cost](#code-cost).

### Build time

```
duration = (300 + 1.2 x hull points) / speed
speed    = (3 if under the fleet cap, else 1) / yard build-time multiplier
```

The speed is fixed when the build is queued, from the yard tier and cap state at that moment.

| Hull points | Tier 1, at cap | Tier 1, under cap | Tier 5, at cap | Tier 5, under cap |
|---|---|---|---|---|
| 45 | 354 s | 118 s | 177 s | 59 s |
| 100 | 420 s | 140 s | 210 s | 70 s |
| 200 | 540 s | 180 s | 270 s | 90 s |
| 320 | 684 s | 228 s | 342 s | 114 s |
| 480 | 876 s | 292 s | 438 s | 146 s |

Throughput of one yard is build slots divided by build time. A tier-1 yard at the cap turns out about 6.7 ships of 200 points per hour (about 1,330 points per hour). A tier-5 yard under the cap builds 3 ships at once at 90 s each: about 120 ships per hour, so wealth, not yard time, becomes the limit.

In the code: [build time](#code-build-time).

### The under-cap discount

While a faction has fewer fleet slots in use than its fleet cap, every ship it queues costs 50% and builds 3 times faster. This applies to every build queued while the faction is below its fleet cap, including garrison ships and top-ups of existing fleets. Each fleet uses one slot per 2000 points of its target size (rounded to the nearest whole number, at least one), so a combined task force occupies several slots.

In the code: [the under-cap discount](#code-the-under-cap-discount).

### Fleet caps

| Faction | Cap |
|---|---|
| Mindus Holdings, Steel Vultures, Stranded | 0 if no war-zone systems. Otherwise 3 below 1.5M embassy wealth, 4 from 2.25M, 5 at 3M |
| Corsair Syndicate while holding no war-zone system | 2 |
| Any faction outside the war-fielding list | 1 |
| Darkspace Compact | 5 |
| Kolyatov, Stellar, Luminate, Meridia's Chosen, Corsairs holding a war-zone system | The smaller of the territory cap and 5 + (war-zone systems held / 15, rounded down) |

The territory cap is 8. The Darkspace Compact's cap changes once the expansion war opens, which comes with Beyond the Front ([Your empire](../expansion/your-empire.md)). With 0 to 14 war-zone systems the formula gives 5, with 15 to 29 it gives 6, and so on, up to the territory cap. For the three guild factions (Mindus Holdings, Steel Vultures, Stranded) the value that counts is the embassy wealth you deliver through embassy missions with that faction (see [Joining a faction's war](#joining-a-factions-war)).

In the code: [fleet caps](#code-fleet-caps).

### What gets queued, in priority order

For each yard with a free slot, the planner takes the first of these that applies:

1. Garrison. The faction's Defense station with the biggest shortfall: wanted ships, minus ships in the garrison, minus ships in production for it, minus ships in flight to it.
2. Starved fleet. The fleet furthest below 35% of its target (2000 points) takes the ship.
3. Founding. Spare fleet slots times 2000 points, minus what is already queued, capped at 2000 points for the ship.
4. Top-up. The fleet furthest below 100% of its target.

Wealth must cover the price or nothing is queued. The ship type for items 2 to 4 is a weighted random pick of the faction's warships with a point value at or below the points needed (heavier hulls weigh more). The garrison pick is the biggest warship that fits the per-ship budget.

Defense station garrison size depends on the station's tier:

| Station tier | Wanted ships | Total budget | Per-ship budget |
|---|---|---|---|
| 1 | 3 | 270 | 90 |
| 2 | 4 | 405 | 101 |
| 3 | 5 | 540 | 108 |
| 4 | 6 | 720 | 120 |
| 5 | 8 | 900 | 112 |

A garrisoned ship is also refitted to the current NPC fleet level in 300 s per refit pass, unless the system is besieged. The refit only raises ship levels, it adds no ships. A separate way of refilling garrisons without a build exists, but it only receives anything from a system's combat strength, which the game never raises above 0, so in practice garrisons are refilled only by the yard queue.

In the code: [what gets queued, in priority order](#code-what-gets-queued-in-priority-order).

### New ship level

A ship leaves the yard at the NPC fleet level, which is the galaxy level + 6 (at least 1). Fleets that fall behind that level are refitted at a yard for 300 s, up to 3 refits at once.

In the code: [new ship level](#code-new-ship-level).

### Delivery

A finished ship is dispatched to its target:

```
travel time = 30 + 30 x jumps   (seconds)
```

Zero jumps, or no route, means it joins immediately. The destination is the rendezvous system of the fleet: the fleet's destination if it is travelling, otherwise where it is. A garrison ship flies to its Defense station's system and joins only if that garrison is still short when it arrives. The Faction overview card lists "{hull} flying to its fleet at {system}" while it flies, but that card is editor-only.

If the ship's own fleet is gone or full when the build finishes or the delivery arrives, it goes to:

1. The below-target fleet with the fewest points.
2. A new fleet founded in that system, if the faction has fleet slots free under its cap.
3. Otherwise it is scrapped ("scrapped, every fleet full"). The wealth is already spent.

In the code: [delivery](#code-delivery).

## Factions without yards

Corsair Syndicate (while it holds no war-zone system) builds no ships at yards. Each hourly economy tick it:

- Tops up every idle or retreating fleet that is at one of its own systems. The missing points are composed as a roster and paid at full price from wealth.
- If fleet slots are under the cap (2 for Corsairs), raises a new 2000-point fleet at its first owned system, also at full price.

Canisec has no yard either. Each hourly economy tick it pays itself a 200,000 home subsidy, raises a 800-point patrol in each Frontier sector that has none, and tops up idle patrols at home, all at full roster price.

None of these wait for yard time. A wealthy yardless faction rebuilds a whole fleet in one hourly economy tick, so the only limit is wealth. Corsairs earn wealth from taxes on systems they hold and from raids, so each raid they complete both funds their rebuilds and drains the victim.

In the code: [factions without yards](#code-factions-without-yards).

## Catch-up mechanics

Several rules push a weak faction back toward parity. They are the reason that making a faction smaller often makes its recovery faster. In these tables "Great Gate reinforcement" means the fleet that appears from the Great Gate map location for Kolyatov, Stellar or Luminate; the game's ledger line for a paid one is "Great Gate fleets".

| Mechanic | Condition | Effect |
|---|---|---|
| Under-cap build discount | Fewer fleet slots used than the fleet cap | Warships cost 50%, build 3 times faster |
| Home subsidy | Kolyatov, Stellar, Luminate and Darkspace, while a war zone exists | 120,000 x (1 - war-zone share / (1/3)), never below 0, per hour (hourly economy tick) |
| Great Gate reinforcement | Kolyatov, Stellar and Luminate, not sitting out, fleet slots used below half the fleet cap, cooldown over | A fleet of 1500 points appears at the war-zone system nearest the Great Gate; cooldown 12 hourly economy ticks |
| Canisec home subsidy | A Frontier sector exists | 200,000 per hour (hourly economy tick) |
| Darkspace starting top-up | New game | Darkspace wealth is raised to the average of the three Great Gate factions (Kolyatov, Stellar, Luminate) |

The war-zone share is the faction's war-zone systems over all war-zone systems. The home subsidy by share:

| Share | Home subsidy per hour |
|---|---|
| 0% | 120,000 |
| 10% | 84,000 |
| 20% | 48,000 |
| 33.3% or more | 0 |

A Great Gate reinforcement is a free Great Gate fleet when the faction has no fleets at all or holds under 10% of the war zone. Otherwise it is paid for at the roster's full price, and if wealth cannot cover it no fleet spawns and the cooldown does not start. The fleet appears instantly with no yard, no delivery and nothing to intercept.

Home subsidy does not count toward the amount a raid drains (see below).

In the code: [catch-up mechanics](#code-catch-up-mechanics).

## Joining a faction's war

Your embassy wealth with a faction (shown as "Wealth your missions brought") rises by the wealth reward of each embassy mission you finish with it. This is not "Your Contribution", the conquest standing that sets your conquest rank. At 1,500,000 you meet the embassy wealth part of the join requirement. As an alternative, paying 10,000,000 credits sets the embassy wealth to at least 1,500,000. That payment is removed from your credits and does not add to the faction's wealth.

When you join, the faction gets:

- 1,000,000 wealth as home subsidy.
- A yard at your joined system if it has none in home space (in that case it removes stations from the system until two slots are free).
- A Defense station with a provisioned garrison, if the system has a slot.
- Its fleets seeded to full at that system.

For Mindus, Steel Vultures and Stranded the same embassy wealth sets the fleet cap (3 to 5), so feeding them wealth raises their cap.

In the code: [joining a faction's war](#code-joining-a-factions-war).

## Intercepting deliveries and convoys

From the galaxy map you can click an icon in transit and chase it. You fly to an intercept zone (the game's "Interception" location) at its position, lock on within 1.5 map units of it, and the target freezes while the encounter runs. The encounter ships appear 12 units behind and 8 above you. Intercept is blocked on autopilot. When the encounter ends the target is released and that target cannot be intercepted again for 300 seconds (a per-target timer kept in memory).

| Target | Ships | What killing it does |
|---|---|---|
| Warship delivery | 1, built from the delivered hull at the delivery's level | The ship is removed for good. Wealth and build time are gone, nothing is refunded. It carries no cargo, so there is no loot. Your own ships in transit cannot be intercepted |
| Cargo run | 1 ship per 6,210 m3 of cargo (or per 150 passengers), 1 to 10 | Killing some ships removes their share of the cargo from the run, so the trade or sales income for the run drops in proportion. Killing every ship ends the run: no cargo is unloaded and no trade or sales income is booked. The route stays and starts its next run |
| Other | Fleets, work-bay ships, bay-ship orders | Each has its own target class. A killed bay-ship order is cancelled |

Every killed ship drops 40% to 60% of each non-ammo cargo stack as loot under your name.

Reputation is charged per ship. Reputation levels are bands of a number: Wary is -4,999 to -500, Hostile is -14,999 to -5,000, and lower levels are worse still.

| Event | Cost with that faction |
|---|---|
| Opening fire on a ship that was not hostile to you (a force-attack) | -1,000 |
| Destroying that ship afterwards, if your standing with the faction was Wary or better (reputation above -5,000) | -3,500 more |

A faction that is already your enemy, meaning at war with you or reputation below -500, cannot be force-attacked in the first place: its ships are already hostile, so a plain click targets them. Intercepting an enemy faction therefore costs nothing in reputation. Each convoy ship is checked separately. The kill penalty applies only while your standing is Wary or better when the ship dies; once earlier penalties push you to Hostile or worse (-5,000 or lower), a force-attacked kill no longer costs the extra -3,500. Whether shooting one ship flags its escorts is not verified.

A delivery is on the map for only 30 + 30 x jumps seconds, so catching it means being near the yard already. A cargo run lasts as long as the route's travel time.

In the code: [intercepting deliveries and convoys](#code-intercepting-deliveries-and-convoys).

## Raids and the economy

A raid completes when a raiding fleet wins its raid battle at an economic outpost (any station that is not a Defense station). It targets an economic outpost of a faction. The Empire Log shows the result as "raid succeeded" to the raider and "outpost raided" to the victim. A completed raid then does four things:

```
wealth drained   = the smaller of
                   the victim's wealth
                   1.5 x its average hourly income (not counting home subsidy)
materials taken  = half of every stack in the station's storage (for an NPC raider)
production pause = 3600 s (1 hour)
raid cooldown    = 14400 s (4 hours)
raider earns     = wealth drained + 10% of the value of the materials taken
```

The average hourly income is the sum of the last 24 closed hourly buckets plus the current hour, over all income kinds including trade and raids, divided by 24. The drain therefore equals one and a half average hours of income other than home subsidy, capped by the wealth the faction has.

The 3600 s pause stops the economic outpost's own jobs: production, work-bay and garrison refit, player construction jobs, prosperity growth, and every cargo route sourced there (so its runs, including trade deliveries that would pay income, stop advancing). The 14400 s cooldown means that economic outpost cannot be raided again until it ends.

What a raid does not touch:

- The faction's warship queue. It is kept apart from the economic outpost, so raiding a Construction Yard pauses its player-ordered builds and work bay, not NPC warship builds. The ships in the queue keep progressing and deliveries keep flying.
- Defense stations. They cannot be raided.
- Wealth beyond the drain. A faction with a large stockpile loses only about one and a half hours of income per raid.

Who launches raids and how they are won is in [war](war.md).

In the code: [raids and the economy](#code-raids-and-the-economy).

## What happens when you destroy a yard

Destroying a yard marks it wrecked, turns 10% of its storage into loot (the rest is cleared), refunds any player build jobs, and costs reputation:

| Your standing with the faction | Reputation cost |
|---|---|
| Wary or better (reputation above -5,000) | -5,000 |
| Hostile or worse (-5,000 or lower) | -1,000 |

The wreck is removed when you leave the system. Until then the planner still counts it as a yard.

The ships already in the faction's queue are not cancelled. With the yard gone, each queue entry counts the yard as having 1 slot, so only one entry per destroyed yard progresses at a time. The build speed was fixed when the ship was queued and stays. When the entries finish, the ships are delivered as normal. The queue is only emptied when a system is captured or its yard system is purged. War-zone yards do not exist, so captures do not reach yards in practice.

With no yard, nothing is queued at that site. The planner replaces the yard on its next hourly economy tick if the faction's yard count is below the chain target and wealth is at least 50,000. The replacement takes 3600 s (1 hour) and starts at tier 1. A destroyed tier-5 yard comes back with 1 slot and 1.0 build time instead of 3 slots and 0.5, which is about a sixth of the throughput until it is upgraded again. The replacement site is chosen fresh and need not be the same system.

Destroying a Defense station clears its garrison, so the faction must pay 100,000 and 3600 s for a new station, then rebuild the garrison ship by ship through the yard queue.

In the code: [what happens when you destroy a yard](#code-what-happens-when-you-destroy-a-yard).

## Ways to slow a faction

Ranked by how much production they remove per reputation spent. "Cut" is what the faction loses; "Rebuild" is how it recovers. Reputation bands are as in the section on intercepting above.

| # | Action | Reputation cost | Cut | Rebuild |
|---|---|---|---|---|
| 1 | Kill warship deliveries of a faction already your enemy (at war with you, or reputation below -500) | 0 | One ship's price (750 to 30,000 wealth) plus its yard slot time (118 s to 876 s per ship) | The planner re-queues the missing points at its next 600 s pass |
| 2 | Destroy its only Construction Yard | -5,000 (-1,000 if Hostile or worse) | New warship builds stop until a replacement exists (about 1 to 2 hours: next hourly economy tick plus 3600 s); the replacement returns at tier 1, so throughput can drop to a sixth | Needs 50,000 wealth and a valid site. Yard count is one per 6 extractors, so a faction with 7 or more extractors may have a second yard that keeps working |
| 3 | Intercept and kill deliveries of a faction that is not your enemy | -1,000 to open fire, -3,500 more per kill (4,500 per ship) | Same as row 1 | Same as row 1 |
| 4 | Stop doing embassy missions for it | 0 | Each mission is +40,000 to +120,000 wealth to the faction | Avoids feeding wealth |
| 5 | Kill cargo convoys of its Production stations | Same as rows 1 and 3 | The run's trade or sales income (10% of value, 14% for an embassy) | Next run goes out normally. Minor next to taxes |
| 6 | Destroy Defense stations | Same as the yard | Garrison ships are lost with the station; the faction pays 100,000 and 3600 s for a replacement and then new garrison ships | Capped at one Defense station under construction at a time |
| 7 | Capture war-zone systems (see [war](war.md)); the Empire Log shows "system captured" | Depends on the war | -20,000 wealth per hour for each system on average, and 0 taxes for 2 conquest updates after capture | A Great Gate faction (Kolyatov, Stellar, Luminate) below 1/3 of the war zone gets up to 120,000 per hour of home subsidy and, below 50% of its cap, a free or paid Great Gate reinforcement of 1500 points every 12 hours |

In the code: [ways to slow a faction](#code-ways-to-slow-a-faction).

### What does not work

- Destroying a yard does not cancel the ships already in its queue. They keep progressing, one at a time per destroyed yard, and deliver normally.
- Raiding a yard pauses its station jobs, not the NPC warship queue or deliveries.
- Attacking ore, canister or material supply does not slow warships. NPC warships cost wealth only.
- Wiping fleets speeds replacement. Once the faction's fleet slots in use fall under the cap, every queued ship is half price and three times as fast. For Kolyatov, Stellar and Luminate it also triggers Great Gate reinforcement once below half of the cap. Remove fleets only if you can keep removing them, or remove the yard first.
- Taking Kolyatov, Stellar, Luminate or Darkspace below a third of the war zone raises its home subsidy. For the first three, dropping below 10% also makes Great Gate reinforcements free. Territory loss does not drive them to zero income.
- Draining wealth with raids is capped at 1.5 hours of income, and only matters when wealth is the faction's limit. A faction at the cap with a full yard can be wealth-rich and yard-limited.
- Yardless Corsairs and Canisec do not queue anything, so destroying their stations does not slow them. Their rebuilds are only limited by wealth. Taking away their raid income and taxes is the only lever.

## Not verified

- Whether a system's combat strength is ever raised elsewhere (for example by a mod patch or an asset-driven path). In the decompiled code it is only reset to 0, which makes the separate garrison refill path dead.
- One size-1 combat hull in the prefab data carries a point value of 2,500. Its identity was not looked up and it is left out of the size table.
- Whether attacking one ship of an intercepted convoy flags the whole convoy as hostile, which would change the reputation cost per convoy.
- Whether story time runs while the game is paused or on a menu. Build times are in story seconds.

In the code: [not verified](#code-not-verified).

## In the game code

This part holds the code detail behind the player sections above: class and method names, field names, timers as written in the code, raw formulas and the `Source:` lines. The code is the decompiled game of version 0.8.3.4. To check a claim, find the named method in a decompiler (the `Source:` line of each section lists them) and read it. Ship point values come from the `SpaceShip.pointValue` field of the ship prefabs in `resources.assets`.

| Term | Code name |
|---|---|
| Wealth ("Economic Power") | `FactionEconomy.wealth` |
| Combat Power (fleet points) | `totalPoints` summed over the faction's fleets |
| Under-cap state | `fleetSlotsUsed < MaxFleetsFor(faction)` |

### Code: What the Faction overview shows

Combat Power is `totalPoints` summed over the faction's fleets. The Economic Outposts tile shows `economic outposts + economic outposts under construction` over `2 x systems held` ("slots used").

The card that lists the income and spending ledger, the Construction Yards count, active ship builds, deliveries and recent builds (`FactionOverviewContent.BuildShipyardSection`) exists in the code but is only enabled when running inside the Unity editor, so it does not appear in the released game. The ledger entries are therefore only visible indirectly, as movement in the Economic Power tile.

Source: `FactionOverviewContent.BuildShipyardSection`, `FactionOverviewContent.SetEconomicPowerTile`, `FactionOverviewContent.SetCombatPowerTile`, `FactionPowerHistory.Record`

### Code: When things happen

| Cadence | What runs |
|---|---|
| Every story second | `FactionEconomy.ProgressConstructions`: economic outpost builds, warship builds, warship deliveries, work-bay ship orders |
| Every 3600 s (hourly economy tick, not shown in the game) | `GrowWealth` pays taxes, then each faction's planner runs: `FactionEconomicPlanner.Tick` (economic outposts), `ConquestFleetPlanner.Tick` (warship queue, Defense stations, fleet orders), `GreatGateReinforcement.Tick` (home subsidy and Great Gate reinforcement). Then Canisec patrols, then the ledger hour closes |
| Every 3600 s (conquest update, a separate timer) | `Conquest.DoConquestTick`: recalculates Area Conquered, rebuilds damaged stations and counts down settling. The galaxy map shows it as the "Conquest Update" countdown card |
| Every 600 s | `ConquestFleetPlanner.PlanBetweenTicks`: the warship queue is refilled and fleet orders reassigned |
| After battles | The factions in a battle are re-planned immediately |

Source: `LivingUniverse.StoryUpdate`, `LivingUniverse.RunHourlyTick`, `LivingUniverse.TickMilitaryPlanning`, `LivingUniverse.RecordPowerSamples`, `FactionLedger.CloseHour`, `Conquest.DoConquestTick`

### Code: Where a faction's wealth comes from

Each earning is booked under a `LedgerEntry` kind. NPC factions add the amount to wealth directly.

| Kind | Label in the game |
|---|---|
| `Passive` | Taxes |
| `Trade` | Trade deliveries |
| `Sales` | Intertrade sales |
| `Subsidy` | Home subsidy |
| `PlayerSupport` | Your embassy missions |
| `Raids` | Raids |

In code, `isInUnrest` is true while `settlingTicks` is above 4. A captured system starts at 6 and loses one per conquest update (hourly), so it pays no taxes for the first two conquest updates after capture.

`PlayerRaids` ("Lost to your raids") is a ledger kind NPC factions never use: it is defined and labelled but nothing in the code records it.

Source: `FactionEconomicPlanner.GrowWealth`, `FactionEconomicPlanner.RollSystemWealth`, `FactionEconomicPlanner.ApplyDeliveryProsperity`, `FactionEconomicPlanner.ApplySaleProsperity`, `FactionEconomicPlanner.EnsureChainRoutes`, `FactionEconomicPlanner.EnsureDeliveryRoutes`, `CargoRoute.Deliver`, `CargoRoute.isSaleRoute`, `ConquestSystem.isInUnrest`, `Conquest.TickSettling`

#### Code: Your embassy missions feed the faction

On completion the amount is added to that faction's wealth under `PlayerSupport` ("Your embassy missions") and to your running embassy wealth with it (`joinWealth`, the "Wealth your missions brought" counter). This is one of the three things the game calls contribution.

Source: `MissionGenerator.RollEmbassyWealth`, `ConquestStrength.OnComplete`, `ConquestJoin.DeliverMissionWealth`

### Code: What factions spend wealth on

| Kind | Meaning |
|---|---|
| `Stations` | New economic outposts, Defense stations and upgrades |
| `Warships` | Warship hulls; raiders and Canisec also pay here at full roster price |
| `BayShips` | Ships for station work bays |
| `GreatGate` ("Great Gate fleets") | Full roster cost of the Great Gate reinforcement, only when the faction is paid-eligible |
| `Raided` | Drained by a raid |

Source: `FactionEconomicPlanner.TryBuildNext`, `FactionEconomicPlanner.TryUpgrade`, `FactionEconomicPlanner.GetUpgradeCost`, `GameMath.FactionOutpostUpgradeCost`, `ConquestFleetPlanner.TryBuildDefenseStation`, `ConquestFleetPlanner.TryQueueBuild`, `BayShipSupply.TryStartBuild`, `GreatGateReinforcement.TryPay`, `YardlessFleetSupply.TryPay`

### Code: How NPC factions build economic outposts

#### Code: Order of builds

Each hourly economy tick `FactionEconomicPlanner.Tick` first makes sure chain routes exist, then takes up to 2 actions. One action is either a new economic outpost or an upgrade: if `economic outposts + economic outposts under construction` is below `2 x systems held`, it tries to build; otherwise it upgrades one economic outpost instead.

With E extractors (Mining plus Salvage), the chain target is `max(1, ceil(E / 6))` of each chain stage, built in the order Refinery, Production, Construction Yard.

Source: `FactionEconomicPlanner.ChooseNextStationType`, `FactionEconomicPlanner.ChooseGuildStationType`, `FactionEconomicPlanner.FindFirstMissingGuildStation`, `FactionEconomicPlanner.ChooseRedundantExtractorType`, `FactionEconomicPlanner.IsChainStageBackingUp`, `FactionEconomicPlanner.ChooseBuild`, `ProductionGoodPicker.Choose`, `ProductionGoodPicker.Repick`, `FactionStationConstruction.BuildDuration`

#### Code: Where economic outposts go

A Construction Yard is never built in a war-zone system. The one exception, `CanHostHeadquartersYard`, is never true because `ConquestWarTuning.HeadquartersYardFactions` is empty. Every yard sits in home space, meaning outside the war zone.

Source: `FactionEconomicPlanner.IsValidSite`, `FactionEconomicPlanner.CompareSites`, `FactionEconomicPlanner.IsInScopeQuadrant`, `FactionEconomicPlanner.CanHostHeadquartersYard`, `ConquestWarTuning.HeadquartersYardFactions`

#### Code: Upgrades and the starting layout

Source: `FactionEconomicPlanner.TryUpgrade`, `FactionEconomicPlanner.ChooseUpgradeTarget`, `PlayerStation.GetMaxTier`, `LivingUniverse.SeedNewGameOutpostsStaged`, `ConquestWarSeeder.SeedWarFaction`

#### Code: Defense stations

```
cap = min( ceil(warZoneSystems / 4), ceil(frontlineSystems * 0.5) )
```

Source: `ConquestFleetPlanner.TryBuildDefenseStation`, `ConquestFleetPlanner.DefenseStationCap`, `ConquestFleetPlanner.ChooseDefenseSite`

### Code: Construction Yards

Source: `ConquestWarTuning.YardBuildSlots`, `ConquestWarTuning.YardBuildTimeMultiplier`, `ConstructionYardCapacity.GetSlots`, `ConstructionYardCapacity.HasFreeSlot`, `ConstructionYardCapacity.GetBuildTimeMultiplier`

If a war faction has no yard outside the war zone, `EnsureYardOutsideWarZone` builds one at a valid home-space site.

Source: `FactionEconomicPlanner.ChainStageTarget`, `ConquestWarSeeder.EnsureYardOutsideWarZone`

### Code: How warships get built

#### Code: Cost

```
cost = FactionShipPricePoints[size] * 50 wealth
```

The point values in the size table are read from the `SpaceShip.pointValue` field of the ship prefabs. The bay-ship build times in `FactionShipPricing` (60 s at size 1 up to 420 s at size 8) apply to work-bay ships only, not warships.

Source: `FactionShipPricing.GetCost`, `GameMath.FactionShipPricePoints`, `FactionShipPricing.GetBuildSeconds`; point values read from the `SpaceShip.pointValue` field of the ship prefabs

#### Code: Build time

```
duration = (300 + 1.2 * points) / speedMultiplier
speedMultiplier = (underCap ? 3 : 1) / yardBuildTimeMultiplier
```

`points` is the hull's point value. `speedMultiplier` is fixed when the build is queued, from the yard tier and cap state at that moment.

Throughput of one yard is `slots / duration`: build slots divided by the build time of one ship, which gives the ships-per-hour figures in the player section.

Source: `FleetShipConstruction.duration`, `ConquestFleetPlanner.TryQueueBuild`, `FleetShipConstruction.BaseBuildDuration`, `FleetShipConstruction.DurationPerPoint`

#### Code: The under-cap discount

```
underCap = fleetSlotsUsed < MaxFleetsFor(faction)
cost  x 0.5 and speed x 3 while underCap
```

`fleetSlotsUsed` is `sum(max(1, round(targetPoints / 2000)))` over its fleets.

Source: `ConquestFleetPlanner.TryQueueBuild`, `FactionEconomy.fleetSlotsUsed`, `ConquestFleet.fleetSlots`

#### Code: Fleet caps

| Faction | Cap (`MaxFleetsFor`) |
|---|---|
| Mindus Holdings, Steel Vultures, Stranded | 0 if no war-zone systems. Otherwise `3 + floor(InverseLerp(1.5M, 3M, embassy wealth) x 2)`: 3 below 1.5M embassy wealth, 4 from 2.25M, 5 at 3M |
| Corsair Syndicate while holding no war-zone system | 2 |
| Any faction outside the war-fielding list | 1 |
| Darkspace Compact | 5 |
| Kolyatov, Stellar, Luminate, Meridia's Chosen, Corsairs holding a war-zone system | `min(territoryCap, 5 + floor(warZoneSystems / 15))` |

`territoryCap` is 8. For the three guild factions the value that counts is the embassy wealth you deliver through embassy missions with that faction; the code calls it contribution.

Source: `ConquestFleetPlanner.MaxFleetsFor`, `ConquestFleetPlanner.MaxTerritoryFleets`, `ConquestJoin.GetContributionFleetCap`, `ConquestJoin.GetFleetCapForContribution`, `ConquestWarTuning.FleetFieldingFactions`, `ConquestWarTuning.ContributionFleetFactions`

#### Code: What gets queued, in priority order

For each yard with a free slot (`QueueWarshipBuilds`), `ChooseBuildOrder` takes the first of these that applies:

1. Garrison. The faction's Defense station with the biggest shortfall: `wanted - garrisonShips - in production for it - in flight to it`.
2. Starved fleet. The fleet furthest below 35% of its target (2000 points) takes the ship.
3. Founding. Spare fleet slots times 2000 points, minus what is already queued, capped at 2000 points for the ship.
4. Top-up. The fleet furthest below 100% of its target.

A separate path refills garrisons without a build (`FactionGarrison.Repair`), but it only receives anything from a system's `combatStrength`, which the code never raises above 0, so in practice garrisons are refilled only by the yard queue.

Source: `ConquestFleetPlanner.ChooseBuildOrder`, `ConquestFleetPlanner.ChooseGarrisonToSupply`, `ConquestFleetPlanner.ChooseFleetToSupply`, `ConquestFleetPlanner.GetFoundingPointsNeeded`, `FactionGarrison.GetWantedCount`, `FactionGarrison.PickShip`, `FactionGarrison.ProgressRefit`, `ConquestFleetComposer.PickReinforcement`, `FactionShipCollection.GetUnit`, `Conquest.DrainCombatStrengthIntoGarrisons`

#### Code: New ship level

A ship leaves the yard at `max(1, galaxyLevel + 6)` (`FleetLevels.GetNpcFleetLevel`). Fleets that fall behind that level are refitted at a yard for 300 s, up to 3 refits at once.

Source: `FleetLevels.GetNpcFleetLevel`, `ConquestWarTuning.RefitSeconds`, `ConquestWarTuning.MaxConcurrentRefits`

#### Code: Delivery

```
travel time = 30 + 30 * jumps   (seconds)
```

If the ship's own fleet is gone or full when the build finishes or the delivery arrives, it goes to:

1. The below-target fleet with the fewest points (`ChooseReinforceTarget`).
2. A new fleet founded in that system, if `fleetSlotsUsed < MaxFleetsFor`.
3. Otherwise it is scrapped ("scrapped, every fleet full"). The wealth is already spent.

Source: `WarshipDeliveries.Dispatch`, `WarshipDeliveries.DispatchToGarrison`, `WarshipDeliveries.TravelDuration`, `WarshipDeliveries.Arrive`, `WarshipDeliveries.TryJoinGarrison`, `ConquestFleetPlanner.SendBuiltShip`, `ConquestFleetPlanner.ChooseReinforceTarget`, `WarshipDeliveries.FoundFleetIfRoom`

### Code: Factions without yards

Each hourly economy tick `CorsairRaids.Tick` tops up idle or retreating Corsair fleets and raises a new 2000-point fleet if slots are under the cap. Each hourly economy tick `CanisecPatrols.Tick` pays itself a 200,000 home subsidy, raises a 800-point patrol in each Frontier sector that has none, and tops up idle patrols at home, all at full roster price. Corsairs earn wealth from taxes on systems they hold and from `Raids`.

Source: `CorsairRaids.Tick`, `CorsairRaids.TryRaiseFleet`, `YardlessFleetSupply.TryTopUp`, `YardlessFleetSupply.TryRaise`, `CanisecPatrols.Tick`, `CanisecPatrols.MaintainPatrol`, `ConquestJoin.IsRaider`, `ConquestWarTuning.CanisecPatrolPoints`

### Code: Catch-up mechanics

| Mechanic | Condition | Effect |
|---|---|---|
| Under-cap build discount | `fleetSlotsUsed < MaxFleetsFor` | Warships cost 50%, build 3 times faster |
| Home subsidy | Kolyatov, Stellar, Luminate and Darkspace, while a war zone exists | `120,000 x clamp01(1 - warZoneShare / (1/3))` per hour (hourly economy tick) |
| Great Gate reinforcement | Kolyatov, Stellar and Luminate, not sitting out, `fleetSlotsUsed < 0.5 x MaxFleetsFor`, cooldown over | A fleet of 1500 points appears at the war-zone system nearest the Great Gate; cooldown 12 hourly economy ticks |
| Canisec home subsidy | A Frontier sector exists | 200,000 per hour (hourly economy tick) |
| Darkspace starting top-up | New game | Darkspace wealth is raised to the average of the three Great Gate factions (Kolyatov, Stellar, Luminate) |

`warZoneShare` is the faction's war-zone systems over all war-zone systems.

Source: `GreatGateReinforcement.Tick`, `GreatGateReinforcement.NeedsReinforcement`, `GreatGateReinforcement.TrySendFleet`, `GreatGateReinforcement.TryPay`, `GreatGateReinforcement.GetSubsidy`, `GreatGateReinforcement.ChooseEntrySystem`, `ConquestWarTuning.GreatGateFactions`, `ConquestWarTuning.SubsidyFactions`, `ConquestScope.GetWarZoneShare`, `ConquestWarSeeder.GrantDarkspacerStartingWealth`

### Code: Joining a faction's war

Your embassy wealth with a faction (`joinWealth`, shown as "Wealth your missions brought") rises by the wealth reward of each embassy mission you finish with it. The 1,000,000 wealth the faction gets on joining is booked as `Subsidy`.

Source: `ConquestJoin.TryDonate`, `ConquestJoin.DeliverMissionWealth`, `ConquestJoin.HasJoinContribution`, `ConquestJoin.Apply`, `ConquestJoin.GetContributionFleetCap`, `ConquestMissions.OfferJoinContribution`

### Code: Intercepting deliveries and convoys

The target freezes (`interceptHeld`) while the encounter runs.

| Target | Class and rule |
|---|---|
| Warship delivery (`WarshipDeliveryInterceptTarget`) | `WarshipDeliveries.Lose` removes the ship for good |
| Cargo run (`CargoRouteInterceptTarget`) | `ConvoyShipsFor`: 1 ship per 6,210 m3 of cargo (or per 150 passengers), 1 to 10. Killing every ship ends the run: no `Trade` or `Sales` income is booked |

Reputation, per ship (`InterceptForceAttack`, `LootManager`). The no-penalty case for an enemy faction is read from `CanForceAttack`, which returns false for ships that are already player enemies.

A delivery is on the map for only `30 + 30 x jumps` seconds (the delivery travel time), so the intercept window is that long.

Source: `InterceptManager.TryIntercept`, `InterceptManager.LockOn`, `InterceptManager.ReleaseTarget`, `InterceptManager.OnShipDestroyed`, `WarshipDeliveryInterceptTarget.OnShipDestroyed`, `WarshipDeliveries.Lose`, `WarshipDeliveryMapIcon.isInterceptable`, `CargoRouteInterceptTarget.OnShipDestroyed`, `CargoRoute.LoseActiveRun`, `CargoRoute.ConvoyShipsFor`, `InterceptRules.RollCargoDrop`, `InterceptRules.AppliesKillPenalty`, `InterceptForceAttack.Attack`, `InterceptForceAttack.CanForceAttack`

### Code: Raids and the economy

The result is logged as `BattleOutcome.Raided`. `StationRaid.ApplyRaid` then does four things:

```
drain          = min(wealth, 1.5 * max(0, (income24h - subsidy24h) / 24))   // from an NPC owner
materials      = floor(count * share) of every stack in the station's storage
                 share = 0.5 for an NPC raider
station.productionPausedFor = 3600 s
station.raidCooldown        = 14400 s
raider earns   = drain + 0.1 * (value of the materials taken)
```

`income24h` is the sum of the last 24 closed hourly buckets plus the current hour, over all income kinds including trade and raids. Dividing by 24 gives an average hour.

The warship queue is in `FactionEconomy.shipConstructions`, not in the economic outpost. `CanRaid` is false until the cooldown ends.

Source: `StationRaid.ApplyRaid`, `StationRaid.DrainOwner`, `StationRaid.GetMaterialShare`, `StationRaid.CanRaid`, `SpaceStation.TickRaidRecovery`, `FactionStation.UpdateJobs`, `StationTrade.ProgressJobs`, `BattleManager.CompleteRaid`, `FactionLedger.GetRecentIncome`

### Code: What happens when you destroy a yard

`FactionStationDestruction.OnDestroyedByPlayer` marks the yard wrecked, turns 10% of its storage into loot (the rest is cleared), refunds any player build jobs, and costs reputation.

With the yard gone, each queue entry looks up a slot count for a missing yard, which falls back to 1. The queue is only emptied when a system is captured or its yard system is purged (`SystemCaptureCleanup`, `PurgeStaleShipConstructions`).

With no yard, `QueueWarshipBuilds` queues nothing at that site.

Source: `FactionStationDestruction.OnDestroyedByPlayer`, `FactionStationDestruction.GetDestroyReputationLoss`, `FactionStationDestruction.ApplyDestroyReputation`, `FactionEconomy.TakeFreeSlot`, `ConstructionYardCapacity.GetSlots`, `SystemCaptureCleanup.CancelFormerOwnerBuilds`, `LivingUniverse.PurgeStaleShipConstructions`, `TravelManager` (calls `CompleteWreck` on leaving)

### Code: Ways to slow a faction

Row 5 income is the `Trade` or `Sales` income of the run. The reputation cost of row 6 is the same as for a yard (`FactionStationDestruction`). Rows 1 and 3 use the intercept reputation costs from the intercept section. In "What does not work", the fleet slots in use are `fleetSlotsUsed`.

### Code: Not verified

- Whether `combatStrength` is ever raised elsewhere (for example by a mod patch or an asset-driven path). In the decompiled code it is only reset to 0, which makes `FactionGarrison.Repair` dead.
- One size-1 combat hull in the prefab data carries a point value of 2,500. Its identity was not looked up and it is left out of the size table.
- Whether attacking one ship of an intercepted convoy flags the whole convoy as hostile, which would change the reputation cost per convoy.
- Whether story time runs while the game is paused or on a menu. Build times are in story seconds.
