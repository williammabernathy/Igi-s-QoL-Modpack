# Common Questions

These are short answers to questions players ask about Beyond the Front: the level 60 gate, buying and claiming systems, upkeep, and your own Empire Fleets. Each answer ends with a link to the section that has the full numbers and how they were read from the code. Values were read from game version 0.8.3.4, and the code behind each answer was compared with the 0.8.3.5 source. Terms are explained in the [Beyond the Front glossary](glossary.md), the [Conquest glossary](../conquest/glossary.md) and the [basics glossary](../basics/glossary.md).

## Getting Past Level 60

### Spoiler: Why Can't I Level Past 60?

You haven't accepted the "Beyond the Front" mission. The level cap is 60 until you accept it, and 100 afterwards. The game does not check your level when you take conquest actions, so the cap is the whole gate.

The mission is offered only once you are level 60 and have finished your faction's 30% sector-control mission. It has three travel steps. Accepting it unlocks the new sectors and the higher caps. Completing it unlocks claiming systems, buying systems, and the Empire Level tile and line.

Details: [What Level 60 and "Beyond the Front" Gate](README.md#spoiler-what-level-60-and-beyond-the-front-gate).

### Spoiler: What Does Accepting "Beyond the Front" Change?

Accepting the mission generates the new sectors and raises four caps:

- The captain level cap goes to 100.
- The cap on skill points found in Locked Containers and facilities goes from 10 to 30.
- The bonus skill point cap goes from 61 to 101.
- The reputation cap goes from Distinguished to Exalted.

It also changes the fleet caps of two NPC factions. The Darkspace Compact goes from a flat 5 to up to 12. Meridia's Chosen goes down, from up to 8 to up to 6.

Claiming and buying systems come later, when you complete the mission. Completing it starts a follow-up mission that asks you to buy your first system.

Details: [Level Cap and Other Changes](your-empire.md#spoiler-level-cap-and-other-changes).

### Why Doesn't the "Beyond the Front" Mission Show Up at Level 60?

You also need one finished 30% sector-control mission for your faction. That mission checks the "Area Conquered" figure, so the war has to go your faction's way first. The Conquest pages explain how to finish it.

Details: [The 30% Sector-Control Mission](../conquest/sector-control.md#the-30-sector-control-mission).

### Is There Anything I Can't Do Even After the Expansion Unlocks?

Yes. You can't take a headquarters or your own capital. You can't claim free territory. You can't claim a system unless your flagship or an idle Empire Fleet is in it. Independent hubs in the new sectors are left for NPC factions to settle.

Details: [What You Can and Cannot Do](empire-fleets.md#what-you-can-and-cannot-do).

### How Tough Is the War Above Level 60?

NPC ships are at least level 66 from the moment the expansion unlocks. Once you are above level 60, they are about your level plus 10.

Expansion systems start at level 60 and rise toward your level, so they sit near your level plus up to 4. Every generated station there gets the M2 or L1 turret layouts. NPC fleets are 2000 points each.

Taking a system sets your reputation with its former owner to -5000, and that faction attacks you afterwards.

Details: [What You Face](empire-fleets.md#what-you-face).

## Buying and Claiming Systems

### How Do I Buy a System, and What Does It Cost?

You buy unowned systems that are for sale in your player sector. You buy them at their hub, and you must be in the system. You pay two prices:

- Credits: 10,000,000 x 1.5^n, where n is the number of systems you have already bought.
- Vanguard Marks: the credit price divided by 5000, rounded.

| Systems already bought | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| Credits | 10,000,000 | 15,000,000 | 22,500,000 | 33,750,000 | 50,625,000 | 75,937,500 |
| Vanguard Marks | 2,000 | 3,000 | 4,500 | 6,750 | 10,125 | 15,188 |

The first system you hold becomes your capital. A system you bought before and then lost is priced one step lower (n - 1, never below 0).

Details: [Buying Systems in the Player Sector](your-empire.md#buying-systems-in-the-player-sector).

### Spoiler: Which Systems Are Safe From Raids and Sieges?

Systems in your player sector, the one expansion sector set aside for you. NPC factions can't enter it, so no siege or raid reaches those systems. Any NPC battle already running there is aborted.

Your capital can't be captured either, but raids can still hit its economic outposts. Systems you hold anywhere else are targets for any faction hostile to you.

Details: [The Expanded War Zone](your-empire.md#spoiler-the-expanded-war-zone) and [Buying Systems in the Player Sector](your-empire.md#buying-systems-in-the-player-sector).

### How Do I Claim a System Instead of Buying It?

Put your flagship or an idle Empire Fleet in an unowned war-zone system. The system can't be in the player sector, because you buy those. It also can't be free territory, because only NPC factions settle that.

If the system's hub still stands, the game refuses the claim with "This system has an owner - destroy its hub first." A destroyed hub is what makes a system claimable. Claiming a system that another faction abandoned destroys that faction's Defense stations in it.

Details: [Claiming Systems](your-empire.md#claiming-systems).

### What Happens to My Reputation When I Claim or Capture a System?

If your reputation with the former owner is above Hostile, capturing a system from a faction, or claiming one it abandoned, drops it to Hostile (-5000). A Decoy Transponder does not hide this. Claiming an unowned system that nobody abandoned costs no reputation.

Details: [Reputation](your-empire.md#reputation).

### What Do I Get When I Capture a System?

The economic outposts of the old owner become yours, one tier lower (minimum 1), with 10% of their stock. The system gets 2 slots for your own economic outposts. If it is the first system you hold, it becomes your capital.

Details: [When a System Changes Hands Against You or by Your Capture](your-empire.md#when-a-system-changes-hands-against-you-or-by-your-capture).

### What Happens to My Stations and Ships if I Lose a System?

Your stations there are handed to the captor. Build jobs are cancelled, serving ships are released and routes end. 75% of the stored materials go to a refund station at your capital (or at your home station), and the captor keeps the rest.

Your Empire Fleets that defended the system fall back to their home system. Ships that die in the fight are gone for good.

Details: [When a System Changes Hands Against You or by Your Capture](your-empire.md#when-a-system-changes-hands-against-you-or-by-your-capture).

## Empire Level, Upkeep and Your Stations

### What Is Empire Level and What Does It Do?

Empire Level is your empire's own level. It is separate from your captain level, and the maximum is 20. It sets how many systems you can hold without paying upkeep, and it sets the fleet mix bonus on your Empire Fleets.

Details: [Empire Level and Upkeep](your-empire.md#empire-level-and-upkeep).

### How Many Systems Can I Hold Before I Pay Upkeep?

| Empire Level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Systems without upkeep | 10 | 14 | 19 | 25 | 32 | 41 | 52 | 65 | 81 | 101 |

| Empire Level | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 |
|---|---|---|---|---|---|---|---|---|---|---|
| Systems without upkeep | 125 | 155 | 191 | 235 | 289 | 355 | 436 | 534 | 654 | 800 |

Details: [Empire Level and Upkeep](your-empire.md#empire-level-and-upkeep).

### How Much Is Upkeep, and What if I Can't Pay It?

Once you hold more systems than your capacity, you pay upkeep every hour, in credits:

```
upkeep per hour = 2,000,000 x 2^(systems over capacity - 1) x cost multiplier at your effective level / cost multiplier at level 60
```

The cost multiplier is the one the game uses for item values, so it rises with level, and the level-60 value is the baseline. The game uses your effective level (level plus Vanguard level), not your plain level. Every system you hold counts toward capacity, including the capital. At level 60, the upkeep is 2,000,000 for one system over capacity and 4,000,000 for two.

If you can't pay for 6 hours in a row, the newest system revolts and goes independent. The capital never revolts. If the capital is the newest system, or the newest system is under siege or has a station that can't be removed, nothing revolts that hour. Paying, or dropping to capacity, resets the count.

Details: [Empire Level and Upkeep](your-empire.md#empire-level-and-upkeep).

### How Do I Raise My Empire Level?

Earn Empire XP (experience points). Each Empire Level needs 1000 x 1.65^(level - 1) XP. These are the sources:

- 250 for each new system you gain, paid once per system.
- 50 for each station you build, paid once per station.
- 3 for each station tier, every hour.
- 5 for each enemy ship killed in a battle you win.
- 1 for each work-bay trip, 3 for each trade run, and 0.5 for each route delivery once you have a capital.

Details: [Empire Level and Upkeep](your-empire.md#empire-level-and-upkeep).

### What Can I Build in My Systems?

You can build economic outposts, Defense stations and patrols, and only in a system you own. A station you build has no facilities and uses a Small layout. Each system has 2 station slots, and captured economic outposts use them too.

A Defense station holds patrol fleets and fires on enemies. Economic outposts also stake mining and salvage claims in their system. Your stations can't be force-attacked.

Removing a station leaves a "Remains of {station}" salvage site for 1 hour, but only if the station held materials, crew, build jobs or cargo in transit. An empty station leaves nothing.

Details: [Player Stations](your-empire.md#player-stations).

## Empire Fleets

### How Many Empire Fleets Can I Have, and How Big Are They?

You need captain level 10 and a held system. The number of fleet slots depends on the war-zone systems you hold, and the size of each fleet depends on your level.

| Systems held | 1 | 5 | 10 | 20 | 40 | 80 | 160 |
|---|---|---|---|---|---|---|---|
| Fleet slots | 1 | 2 | 3 | 4 | 5 | 6 | 7 |

The size budget steps every 5 levels. It is 100 points and 2 ships at level 10, 2500 points and 12 ships at level 60, and 4000 points and 25 ships at level 100. The full table is on the linked page.

Details: [Slots and Size](empire-fleets.md#slots-and-size).

### What Orders Can I Give a Fleet?

| Order | What it does |
|---|---|
| Move | Travels to a system and stays there. |
| Attack | Besieges an enemy war-zone system. If the system is unowned, arriving there claims it, unless it is for sale in your player sector or is free territory. |
| Raid | Hits a hostile faction's economic outpost. |
| Return | Goes to the home system, or to your nearest held system if the home system is lost. |
| Join siege | Helps whichever side of a fight in that system is yours or allied. |

The range is 30 jumps. Giving a fleet a new order breaks off an attack in progress. You can't order a fleet that is defending against another fleet.

Details: [Orders](empire-fleets.md#orders).

### What Do the Defense Stances Do?

| Stance | What the fleet does |
|---|---|
| None | Does not defend. It counts toward Defense only while it is on a Join siege order. |
| Defend system (the default) | While idle, joins the defense of the system it is in. |
| Defend sector | Does the same as Defend system, and also answers sieges and raids on your systems anywhere in its sector. |

Clicking the active stance again returns the fleet to None. Your fleets never retreat on their own, because the retreat setting is hidden in this version and is set to 0.

Details: [Defense Stances and Other Behaviour](empire-fleets.md#defense-stances-and-other-behaviour).

### What Level Are My Fleet Ships, and Does My Own Loadout Count?

Your own loadout does not count. A ship on patrol has a service level, which is set to the system level when the ship joins. A ship in an Empire Fleet always counts as your own level. When the ship fights, the game builds a "standard issue" copy of it at that level.

The copy keeps the ship's rank and rarity, gets the NPC ship HP curve, and uses standard-issue equipment, as hangar-bay fighters do. It gets no ship rank modifiers and no escalation buffs.

A newly built player-faction ship takes your level. Empire Fleets have no refit and follow your level as you gain it (changed in 0.8.3.5). A patrol is "due" for a refit when its service level is at or below the system level minus 3. A refit raises the service level to the system level plus 3, with a timer of 300 s.

Details: [Service Levels and Standard-Issue Copies](empire-fleets.md#service-levels-and-standard-issue-copies).

### Do I Lose Ships When a Fleet Loses a Fight?

Yes. Ships in your Empire Fleets and patrol fleets that die are removed permanently, in simulated and live battles alike.

Details: [Defense Stances and Other Behaviour](empire-fleets.md#defense-stances-and-other-behaviour).

### What Is the Fleet Mix Bonus?

It is a bonus for an Empire Fleet that has 2 or more distinct non-drone ship sizes:

```
bonus = number of distinct ship sizes x rate
rate = 1% per size at Empire Level 1-4, 2% at Empire Level 5-9, 3% at Empire Level 10+
```

The bonus applies to simulated battles and to the Defense number, not to live combat. Patrol ships get no mix bonus.

Details: [Fleet Mix Bonus](empire-fleets.md#fleet-mix-bonus).

## Taking and Defending Systems

### What Does the "Outmatched" Warning Mean?

You gave an Attack order to a fleet whose points are below 1.15 x the target's next-battle strength. The game says the fleet "will likely be wiped out" and offers "Attack anyway". When the system has Defense stations, the number in the warning can be lower than the Defense row.

Details: [Outmatched Warning](empire-fleets.md#outmatched-warning).

### How Strong Must My Fleet Be to Take a System?

To avoid the warning, your fleet needs at least 1.15 times the next-battle strength. To capture the system, it needs at least 1.25 times the Defense at that moment.

| System | Defense number | Points to avoid the warning |
|---|---|---|
| Hub only, medium (480 firepower) | 480 | 553 |
| Hub plus one tier-5 Defense station | up to 1600 | up to 1840 |
| The same, plus one resident 2000-point fleet | up to 3600 | up to 4140 |

A level 60 fleet is 2500 points, so it clears the first two. The third needs a level 100 fleet (4000 points plus the mix bonus) or several fleets. Once the station is broken and every defender is dead, Defense falls to a floor of 250, so about 313 points is enough.

Details: [Taking a System With Empire Fleets](empire-fleets.md#taking-a-system-with-empire-fleets).

### Any Tips for Sieging a System?

- Attack right after a system changes hands. For the first 5 to 6 hours its station firepower is halved, and for the first 1 to 2 hours it pays no taxes.
- Station structure takes damage only after every defender ship is dead, so clear the fleets first. A siege gets at most 29 rounds. You can also intercept reinforcements while they are still traveling.
- You have to win against a Defense station first. Each stage is its own battle with a 600 s minimum.
- Missions against the owner, done next to the target, lower its station firepower by up to half.
- Destroying a system's hub in a live battle abandons the system. Outside your player sector, with claiming unlocked and your flagship or an idle Empire Fleet there, you are offered the claim. Taking it drops your reputation to Hostile and destroys the old owner's Defense stations. In your player sector, the system goes back on sale instead.
- If a flagship or fleet of yours is in the system, the battle turns live, and the fight then depends on the ships alone.

Details: [Taking a System With Empire Fleets](empire-fleets.md#taking-a-system-with-empire-fleets).

### How Do My Fleets and Patrols Count in the Defense Number?

A fleet counts in full if it has a defense stance (or a Join siege order), is in the system, and is not traveling. Empire Fleets also add the fleet mix bonus. Patrol ships count in full for their own system, with no mix bonus.

Each Defense station you build gives patrol fleets according to its tier:

| Tier | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| Patrol fleets | 1 | 1 | 2 | 2 | 3 |
| Points per fleet | 120 | 240 | 260 | 400 | 500 |

Details: [Your Fleets and Patrols in the Defense Number](empire-fleets.md#your-fleets-and-patrols-in-the-defense-number).

### How Do I Defend a System I Hold?

- Keep a fleet in the system with a defense stance.
- Build Defense stations and assign patrol fleets. A siege then goes straight to the hub instead of through station stages.
- Keep the Defense number above about 1740. NPC fleets attack at 1.15 times the strength of the weakest station, and a lone NPC fleet is 2000 points. Assaults combine 2 or more fleets and can exceed that.
- A defending fleet or patrol stops a raid from running unopposed.
- If a flagship or fleet of yours is in the system, the battle turns live, and the fight then depends on the ships alone.
- War effort missions do not help here. They never apply to systems you hold.

Details: [Defending a System You Hold](empire-fleets.md#defending-a-system-you-hold).

### Will Other Factions Attack Me?

Any faction hostile to you can attack the systems you hold outside your player sector, but small factions hold back. A faction that holds under 10% of the war-zone systems attacks only unowned systems, or owners that hold 10% or more. While you hold under 10%, the small factions therefore leave you alone, and the large ones do not.

The 10% is the faction's war-zone systems divided by all war-zone systems, which means every system in a war-zone sector, owned or not. If this rule would leave a faction with no target at all, the faction considers every target.

Details: [Taking a System With Empire Fleets](empire-fleets.md#taking-a-system-with-empire-fleets).

## Raids

### What Happens When My Stations Are Raided?

Nothing is taken from you. A raid on your outpost only pauses its production and starts its cooldown. NPC raiders list you as a target if their faction is your enemy. Raid parties in the Frontier add your stations to their list if the raider is your enemy. Raids can't reach systems in your player sector.

Details: [Raids Against Your Stations and by Your Empire Fleets](your-empire.md#raids-against-your-stations-and-by-your-empire-fleets).

### Can I Raid With My Own Fleets?

Yes. The Raid order targets an economic outpost (not a Defense station) in the war zone that a hostile faction owns. A raid pays you credits at 10 credits per wealth point.

A raid also slows the faction. It cuts 1.5 average hours of the faction's income other than the home subsidy, and it pauses that outpost for 3600 s, followed by a 4 hour cooldown. The reputation cost depends on the war.

Details: [Raids Against Your Stations and by Your Empire Fleets](your-empire.md#raids-against-your-stations-and-by-your-empire-fleets) and [Ways to Slow a Faction: Empire Fleet Raids](your-empire.md#ways-to-slow-a-faction-empire-fleet-raids).

### Is War Effort or My Own Fleet the Better Lever Above Level 60?

Your fleets are. Before the unlock, interception, live hub kills and war effort bursts are most of what you can do. Once claiming is unlocked, attacking and defending with your own fleets is the main lever. War effort becomes a small add-on worth at most 250 to 700 Defense at the cap.

Details: [Where the Efficient Choice Differs](empire-fleets.md#where-the-efficient-choice-differs).
