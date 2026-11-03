# Common questions

These are short answers to questions players ask about Beyond the Front: the level 60 gate, buying and claiming systems, upkeep, and your own Empire Fleets. Each answer ends with a link to the section with the full numbers and how they were read from the code. Values are for game version 0.8.3.4. Terms are in the [Beyond the Front glossary](glossary.md), the [Conquest glossary](../conquest/glossary.md) and the [basics glossary](../basics/glossary.md).

## Getting past level 60

### Spoiler: Why can't I level past 60?

You haven't accepted the "Beyond the Front" mission. The level cap is 60 until you do, and 100 afterwards. The game does not check your level when you take conquest actions, so the cap is the whole gate.

The mission is offered only once you are level 60 and have finished your faction's 30% sector-control mission. It has three travel steps. Accepting it unlocks the new sectors and the higher caps. Completing it unlocks claiming systems, buying systems and the Empire level tab.

Details: [What level 60 and "Beyond the Front" gate](README.md#spoiler-what-level-60-and-beyond-the-front-gate).

### Spoiler: What does accepting "Beyond the Front" change?

Accepting it generates the new sectors and raises three caps: the player level cap to 100, the loot box skill point cap from 10 to 30, and the reputation cap from Distinguished to Exalted. It also widens the fleet caps of two NPC factions.

Claiming and buying systems come later, when you complete the mission. Finishing it starts a follow-up mission that asks you to buy your first system.

Details: [Level cap and other changes](your-empire.md#spoiler-level-cap-and-other-changes).

### Why doesn't the "Beyond the Front" mission show up at level 60?

You also need one finished 30% sector-control mission for your faction. That mission checks the "Area Conquered" figure, so the war has to go your faction's way first. How to finish it is in the Conquest pages.

Details: [The 30% sector-control mission](../conquest/sector-control.md#the-30-sector-control-mission).

### Is there anything I can't do even after the expansion unlocks?

Yes. You can't take a headquarters or your own capital, you can't claim free territory, and you can't claim a system unless your flagship or an idle Empire Fleet is in it. Independent hubs in the new sectors are for NPC factions to settle.

Details: [What you can and cannot do](empire-fleets.md#what-you-can-and-cannot-do).

### How tough is the war above level 60?

NPC ships are at least level 66 from the moment the expansion unlocks, and about your level plus 10 once you are above 60. Expansion systems start at level 60 and rise toward your level, so they sit near your level plus up to 4. Every generated station there gets the M2 or L1 turret layouts. NPC fleets are 2000 points. Taking a system sets your reputation with its former owner to -5000, and that faction attacks you afterwards.

Details: [What you face](empire-fleets.md#what-you-face).

## Buying and claiming systems

### How do I buy a system, and what does it cost?

You buy unowned systems in your player sector at their hub, and you must be in the system. The price is 10,000,000 credits x 1.5^n, where n is the number of systems you have already bought. In Vanguard Marks, divide the credit price by 5000.

| Systems already bought | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| Credits | 10,000,000 | 15,000,000 | 22,500,000 | 33,750,000 | 50,625,000 | 75,937,500 |
| Vanguard Marks | 2,000 | 3,000 | 4,500 | 6,750 | 10,125 | 15,188 |

The first system you hold becomes your capital.

Details: [Buying systems in the player sector](your-empire.md#buying-systems-in-the-player-sector).

### Spoiler: Which systems are safe from raids and sieges?

Systems in your player sector, the one expansion sector set aside for you. NPC factions can't enter it, so no siege or raid reaches those systems, and any NPC battle already running there is aborted. Your capital can't be captured either, but raids can still hit its economic outposts. Systems you hold anywhere else are targets for any faction hostile to you.

Details: [The expanded war zone](your-empire.md#spoiler-the-expanded-war-zone) and [Buying systems in the player sector](your-empire.md#buying-systems-in-the-player-sector).

### How do I claim a system instead of buying it?

Put your flagship or an idle Empire Fleet in an unowned war-zone system. It can't be a player-sector system (those are bought) or free territory (only NPC factions can settle that). If the system's hub still stands, the game refuses the claim, so a destroyed hub is what makes a system claimable.

Details: [Claiming systems](your-empire.md#claiming-systems).

### What happens to my reputation when I claim or capture a system?

Capturing a system from a faction, or claiming one it abandoned, drops your reputation with the former owner to Hostile (-5000) if it was higher. A Decoy Transponder does not hide this. Claiming an unowned system that nobody abandoned costs nothing.

Details: [Reputation](your-empire.md#reputation).

### What do I get when I capture a system?

Economic outposts of the old owner become yours, one tier lower (minimum 1), with 10% of their stock. The system gets 2 slots for your own economic outposts. Your first held system becomes your capital.

Details: [When a system changes hands](your-empire.md#when-a-system-changes-hands-against-you-or-by-your-capture).

### What happens to my stations and ships if I lose a system?

Your stations there are handed to the captor. Build jobs are cancelled, serving ships are released and routes end. 75% of the stored materials go to a refund station at your capital (or home station), and the rest stays with the captor. Your Empire Fleets that defended the system fall back to their home system. Ships that die in the fight are gone for good.

Details: [When a system changes hands](your-empire.md#when-a-system-changes-hands-against-you-or-by-your-capture).

## Empire Level, upkeep and your stations

### What is Empire Level and what does it do?

Empire Level is your empire's own level. It is separate from your player level. It sets how many systems you can hold without paying upkeep, and it sets the fleet mix bonus on your Empire Fleets.

Details: [Empire Level and upkeep](your-empire.md#empire-level-and-upkeep).

### How many systems can I hold before I pay upkeep?

| Empire Level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Systems without upkeep | 10 | 14 | 19 | 25 | 32 | 41 | 52 | 65 | 81 | 101 |

Details: [Empire Level and upkeep](your-empire.md#empire-level-and-upkeep).

### How much is upkeep, and what if I can't pay it?

Past your capacity you pay every hour, in credits:

```
upkeep per hour = 2,000,000 x 2^(systems over capacity - 1) x cost multiplier at your level / cost multiplier at level 60
```

The cost multiplier is the one the game uses for item values, so it rises with your level and the level-60 value is the baseline. At level 60 that is 2,000,000 for one system over capacity and 4,000,000 for two. If you can't pay for 6 hours, the newest system revolts.

Details: [Empire Level and upkeep](your-empire.md#empire-level-and-upkeep).

### How do I raise my Empire Level?

Earn Empire XP. Each Empire Level needs 1000 x 1.65^(level - 1) XP. You gain 250 for a new system, 50 for building a station, 3 per station tier every hour, and 5 per enemy ship killed in a battle.

Details: [Empire Level and upkeep](your-empire.md#empire-level-and-upkeep).

### What can I build in my systems?

Economic outposts, Defense stations and patrols, and only in a system you own. A station you build has no facilities and uses a Small layout. A Defense station holds patrol fleets and fires on enemies. Each system has 2 station slots, and captured economic outposts use them too. Economic outposts also stake mining and salvage claims in their system. Your stations can't be force-attacked.

Removing a station leaves a "Remains of {station}" salvage site for 1 hour.

Details: [Player stations](your-empire.md#player-stations).

## Empire Fleets

### How many Empire Fleets can I have, and how big are they?

You need player level 10 and a held system. The number of fleet slots depends on the war-zone systems you hold, and the size of each fleet depends on your level.

| Systems held | 1 | 5 | 10 | 20 | 40 | 80 | 160 |
|---|---|---|---|---|---|---|---|
| Fleet slots | 1 | 2 | 3 | 4 | 5 | 6 | 7 |

The size budget steps every 5 levels. It is 100 points and 2 ships at level 10, 2500 points and 12 ships at level 60, and 4000 points and 25 ships at level 100. The full table is on the linked page.

Details: [Slots and size](empire-fleets.md#slots-and-size).

### What orders can I give a fleet?

| Order | What it does |
|---|---|
| Move | Travel to a system and stay. |
| Attack | Siege an enemy war-zone system. Arriving at an unowned system claims it. |
| Raid | Hit a hostile faction's economic outpost. |
| Return | Go to the home system, or your nearest held system if home is lost. |
| Refit | Go to your nearest Construction Yard. Refused if it would not raise the fleet's level. |
| Join siege | Help whichever side of a fight in that system is yours or allied. |

The range is 30 jumps. Giving a new order breaks off an attack in progress. You can't order a fleet that is defending against another fleet.

Details: [Orders](empire-fleets.md#orders).

### What do the defense stances do?

| Stance | Behavior |
|---|---|
| None | Doesn't defend (counts only while on a Join siege order). |
| Defend system (default) | While idle, joins the defense of the system it is in. |
| Defend sector | Also answers sieges and raids on your systems anywhere in its sector. |

Clicking the active stance again returns it to None. Your fleets never retreat on their own, because the retreat setting is hidden in this version and set to 0.

Details: [Defense stances and other behavior](empire-fleets.md#defense-stances-and-other-behavior).

### What level are my fleet ships, and does my own loadout count?

Your own loadout does not count. A ship in an Empire Fleet or on patrol has a service level, set to the system level when it joins. When it fights, the game builds a "standard issue" copy at that service level. The copy keeps the ship's rank and rarity, gets the NPC ship HP curve, and uses standard-issue equipment, as hangar-bay fighters do. It gets no ship rank modifiers and no escalation buffs.

A newly built player-faction ship takes the level of the system it is built in. An Empire Fleet is "due" for a refit when its service level is at or below the system level minus 3, and a refit raises it to the system level plus 3. Patrol ships use the same rule (refit timer 300 s).

Details: [Service levels and standard-issue copies](empire-fleets.md#service-levels-and-standard-issue-copies).

### Do I lose ships when a fleet loses a fight?

Yes. Ships in your Empire Fleets and patrol fleets that die, in simulated or live battles, are removed permanently.

Details: [Defense stances and other behavior](empire-fleets.md#defense-stances-and-other-behavior).

### What is the fleet mix bonus?

A bonus for an Empire Fleet with 2 or more distinct non-drone ship sizes:

```
bonus = number of distinct ship sizes x rate
rate = 1% per size at Empire Level 1-4, 2% at Empire Level 5-9, 3% at Empire Level 10+
```

It applies to simulated battles and to the Defense number, not to live combat. Patrol ships get no mix bonus.

Details: [Fleet mix bonus](empire-fleets.md#fleet-mix-bonus).

## Taking and defending systems

### What does the "outmatched" warning mean?

You gave an Attack order to a fleet whose points are below 1.15 x the target's next-battle strength. The game says the fleet "will likely be wiped out" and offers "Attack anyway". The number in the warning can be lower than the Defense row when the system has Defense stations.

Details: [Outmatched warning](empire-fleets.md#outmatched-warning).

### How strong must my fleet be to take a system?

To avoid the warning you need at least 1.15 times the next-battle strength. To capture you need at least 1.25 times the Defense at that moment.

| System | Defense number | Points to avoid the warning |
|---|---|---|
| Hub only, medium (480 firepower) | 480 | 553 |
| Hub plus one tier-5 Defense station | up to 1600 | up to 1840 |
| Same, plus one resident 2000-point fleet | up to 3600 | up to 4140 |

A level 60 fleet is 2500 points, so it clears the first two. The third needs a level 100 fleet (4000 points plus the mix bonus) or several fleets. Once the station is broken and every defender is dead, Defense falls to a floor of 250, so about 313 points is enough.

Details: [Taking a system with Empire Fleets](empire-fleets.md#taking-a-system-with-empire-fleets).

### Any tips for sieging a system?

- Attack right after a system changes hands. Its station firepower is halved for 5 to 6 hours and it pays no taxes for 1 to 2 hours.
- Station structure only takes damage after every defender ship is dead, so clear the fleets first. You can also intercept reinforcements while they are still traveling.
- A Defense station has to be won first. Each stage is its own battle with a 600 s minimum.
- Missions against the owner, done next to the target, lower its station firepower by up to half.
- Destroying a system's hub live abandons it and offers you the claim, with the reputation drop to Hostile.
- A flagship or fleet of yours in the system turns the battle live, and the fight then depends on the ships alone.

Details: [Taking a system with Empire Fleets](empire-fleets.md#taking-a-system-with-empire-fleets).

### How do my fleets and patrols count in the Defense number?

A fleet counts in full if it has a defense stance (or a Join siege order), is in the system, and is not traveling. Add the fleet mix bonus for Empire Fleets. Patrol ships count in full for their own system, with no mix bonus. Each Defense station you build gives patrol fleets by tier:

| Tier | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| Patrol fleets | 1 | 1 | 2 | 2 | 3 |
| Points per fleet | 120 | 240 | 260 | 400 | 500 |

Details: [Your fleets and patrols in the Defense number](empire-fleets.md#your-fleets-and-patrols-in-the-defense-number).

### How do I defend a system I hold?

- Keep a fleet in the system with a defense stance.
- Build Defense stations and assign patrol fleets. A siege then goes straight to the hub rather than through station stages.
- Keep the Defense number above about 1740. NPC fleets attack at 1.15 times the weakest-station strength, and a lone NPC fleet is 2000 points. Assaults combine 2 or more fleets and can exceed that.
- A defending fleet or patrol stops a raid from running unopposed.
- A flagship or fleet of yours in the system turns the battle live, and the fight then depends on the ships alone.
- War effort missions may help, but whether they apply to systems you hold is (not verified).

Details: [Defending a system you hold](empire-fleets.md#defending-a-system-you-hold).

### Will other factions attack me?

Any faction hostile to you can attack the systems you hold outside your player sector, but small factions hold back. A faction holding under 10% of war-zone systems only attacks unowned systems or owners at 10% or more, so while you hold under 10% the small factions leave you alone and the large ones don't. The 10% is a share of all war-zone systems; which systems the game counts in that total is (not verified).

Details: [Taking a system with Empire Fleets](empire-fleets.md#taking-a-system-with-empire-fleets).

## Raids

### What happens when my stations are raided?

Nothing is taken from you. A raid on your outpost only sets its production pause and cooldown. NPC raiders list you as a target if their faction is your enemy, and raid parties in the Frontier add your stations to their list if the raider is your enemy. Systems in your player sector can't be reached by raids.

Details: [Raids against your stations and by your Empire Fleets](your-empire.md#raids-against-your-stations-and-by-your-empire-fleets).

### Can I raid with my own fleets?

Yes. The Raid order targets an economic outpost (not a Defense station) in the war zone owned by a hostile faction. A raid pays you credits at 10 credits per wealth point. It also slows the faction: it cuts 1.5 average hours of its income other than the home subsidy and pauses that outpost for 3600 s, with a 4 hour cooldown. The reputation cost depends on the war.

Details: [Raids against your stations and by your Empire Fleets](your-empire.md#raids-against-your-stations-and-by-your-empire-fleets) and [Ways to slow a faction: Empire Fleet raids](your-empire.md#ways-to-slow-a-faction-empire-fleet-raids).

### Is war effort or my own fleet the better lever above level 60?

Your fleets. Below the unlock, interception, live hub kills and war effort bursts are most of what you can do. Once claiming is unlocked, attacking and defending with your own fleets is the main lever, and war effort is a small add-on worth at most 250 to 700 Defense at the cap.

Details: [Where the efficient choice differs](empire-fleets.md#where-the-efficient-choice-differs).
