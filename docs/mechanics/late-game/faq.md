# Common Questions

These are short answers to questions players ask about the story lines behind the war. Every answer here is a spoiler. Each answer ends with a link to the section with the full numbers and how they were read from the code. Values are for game version 0.8.3.4, and the code behind each answer was compared with the 0.8.3.5 and 0.8.3.7 sources.

Terms are in the [late-game glossary](glossary.md), the [Conquest glossary](../conquest/glossary.md) and the [basics glossary](../basics/glossary.md).

## Unlocking the Story Lines

### Spoiler: How Do I Unlock the First Story Line?

Finish the first mission of your faction's Conquest chain (Stellar, Kolyatov or Luminate, depending on your faction). Its reward is 2,000,000 credits, 60 Vanguard Marks, XP, the second mission and the Umbral line. So the Umbral line comes with the reward of that first mission.

Details: [Gates](README.md#spoiler-gates).

### Spoiler: What Does the 30% Sector-Control Mission Unlock?

It starts the first mission of the Darkspace line. That mission is one travel step to a point of interest in a Conquest staging system. It pays XP and 200 reputation with the Darkspace Compact, and it starts the next Darkspace mission. The 30% mission itself pays 10,000,000 credits, 400 Vanguard Marks, 1 skill point and XP.

Details: [Gates](README.md#spoiler-gates).

### Spoiler: How Do I Wake the Dormant Raider Faction or Open the Closed Sector?

You can't, in a normal game. In versions 0.8.3.4, 0.8.3.5 and 0.8.3.7 the only thing that wakes Malcyte or opens Meridia is the debug console, and it works only once the expansion is unlocked. No story mission, dialogue, save load or game asset does either, so Malcyte stays dormant and Meridia stays closed.

Details: [Gates](README.md#spoiler-gates).

## The Control Row and the Story Station

### Spoiler: What Is the Control Row on the System Tooltip?

The "Umbral Reach control" row shows how far the Umbral Reach has spread into that system, as a percentage. It only appears above 0. It is the second kind of conquest row on a war-zone system tooltip, next to Defense. An unowned system whose hub is an abandoned hulk can also show it.

Details: [On the system tooltip and in system data](control-story.md#on-the-system-tooltip-and-in-system-data).

### Spoiler: How Does the Control Row Grow and Spread?

Control grows and spreads on the hourly conquest update (every 3600 s).

- A system already above 0 rises by a random 0.05 to 0.1 each update, up to 1 (100%).
- Each update makes one 50% roll in total. When it passes, the first system found at 1.0 that has a neighbour with no control sets that neighbour to 0.05. That is at most one new system per update.

Details: [The control row and its spread](control-story.md#spoiler-the-control-row-and-its-spread).

### Spoiler: How Do I Start the Spread in a System?

Use the Umbral Hacking Tool, which an Umbral shop sells. It sets the system's control to 0.05. The stronger variant, the Umbral Hacking Multitool (called "uber" in the code), sets it to 0.7. Use either at the conquest station (the hub) of a system where you are friendly with the owner and the system has no control yet. Unless you use the Multitool, the hub of a neighbouring system must also have control above 0.19. The game's message says "at least 20%".

Details: [The control row and its spread](control-story.md#spoiler-the-control-row-and-its-spread).

### Spoiler: What Does Control Do at One of the Story Faction's Stations?

At an Umbral station, the mission board gets an Umbral mission button at 5% station control, and shops get an Umbral shop slot at 50%. The puppeteers' station in quadrant 2 always counts as 100%. This page does not cover what control does beyond those thresholds.

Details: [The control row and its spread](control-story.md#spoiler-the-control-row-and-its-spread).

### Spoiler: What Is the Station in Quadrant 2 and How Hard Is It?

It is the puppeteers' station. It has 6 parts and is not an economic outpost or an outpost system. The parts are 1 Core, 1 Refinery, 1 Small Docking Pad, 1 Tunnel and 2 turrets that share one tier roll. Below location level 45, both turrets are Small Turret Mk II. At level 45 or above, both are Medium Turret Mk II or both are Large Turret Mk I, with a 50/50 roll.

The game works out the station's off-screen siege numbers like this. Hull is the structure score of the station's parts added together, and Firepower is the firepower score of its platforms added together:

| Both tier turrets are | Hull | Firepower |
|---|---|---|
| Small Turret Mk II (below level 45) | 440 | 160 |
| Both Medium Turret Mk II or both Large Turret Mk I (level 45 and above) | 470 to 500 | 240 to 320 |

Details: [The story faction's station](control-story.md#spoiler-the-story-factions-station).

## The Transponder and Reputation

### Spoiler: What Does the Decoy Transponder Change?

The item is called the Decoy Transponder in the game. The item text says it prevents reputation loss and reputation gains from combat, and it lasts until you next dock at a station. While it is active, the game skips five reputation costs from the early pages:

- Entering a facility or boarding a ship.
- Each unit you kill, whether a station part or a ship.
- Destroying a faction station while you hit it. Normally this costs -5000 reputation, or -1000 if you are already at -5000 or below.
- Destroying a Construction Yard. The cost is 0 whatever your standing.
- Destroying a faction's only yard to slow that faction. It costs 0 reputation instead of -5,000 (-1,000 if you are Hostile or worse).

Details: [What the transponder changes](control-story.md#spoiler-what-the-transponder-changes).

### Spoiler: Can I Kill a Station's Parts Without Losing Reputation?

Yes, in two cases. With the Decoy Transponder active, every unit you kill skips the owner's reputation hit, whether it is a station part or a ship. Separately, several mission stations and Umbral stations are flagged so that killing their parts costs no reputation with the owner. Normally each part costs 100 points while you are Neutral or better, and 5 points otherwise.

Details: [Killing station parts](control-story.md#killing-station-parts) and [Stations with no reputation loss](control-story.md#stations-with-no-reputation-loss).

## The Closed Sector

### Spoiler: Why Can't I Attack or Enter the Closed Sector?

You can't attack it because the Meridia sector allows no stations. Malcyte hold it without hubs, so there is nothing to besiege. Once the quadrant is open to you, the game says "There is nothing to besiege here" when you order an attack. Fleets cannot attack it.

You also can't claim or order anything in the quadrant around it while it is closed. The game answers "This system is closed to you for now." That answer also comes before the "nothing to besiege" one. The quadrant is open only to Malcyte, their war ally Meridia's Chosen and, outside the Meridia sector itself, the Darkspace Compact.

The game also treats jump gates into the quadrant, and into the Meridia sector from outside it, as closed, and it sells no Jump Gate Pass for them. Those gates are locked from the start and do stop your travel. In a normal game nothing opens the quadrant, so it stays closed.

Details: [The closed quadrant](closed-sector-story.md#the-closed-quadrant) and [Your options in the closed sector](closed-sector-story.md#your-options-in-the-closed-sector).

### Spoiler: Can Anyone Build an Economic Outpost in the Closed Sector?

No. A Meridia system is not a valid site for an economic outpost, so no faction builds one there. That means Malcyte, which owns only Meridia systems, has no yard. Meridia's Chosen also owns systems outside Meridia, so its yard can stand there.

Details: [No economic outposts in the closed sector](closed-sector-story.md#no-economic-outposts-in-the-closed-sector).

## The Dormant Raider Faction

### Spoiler: How Big Can the Dormant Faction's Fleet Get?

While dormant, Malcyte earns no system taxes, fields no fleets and has a fleet cap of 0. Once awake, the cap is 5 plus 1 for every 15 war-zone systems (rounded down), and never more than 8. That is the same limit as Kolyatov, Stellar and Luminate.

Details: [Fleet cap](closed-sector-story.md#fleet-cap).

### Spoiler: How Does the Dormant Faction Rebuild Its Fleets Without Yards?

It doesn't use yards. Each hourly economy tick, it refills the missing ship points of every idle or retreating fleet that sits at one of its own systems, paying full price from its wealth. If it has fleet slots under the cap, it also raises a new 2000-point fleet at its first owned system, which is a Meridia system, again at full price.

Nothing waits for yard time, so a wealthy Malcyte can rebuild a whole fleet in one hourly economy tick. Destroying its stations does not slow it. Its wealth comes from taxes on systems it holds and from the raids it completes, so the only way to slow it is to take away its raid income and taxes.

Details: [Yardless fleets](closed-sector-story.md#yardless-fleets).

### Spoiler: Who Counts as the Dormant Faction's War Ally?

Meridia's Chosen. Both are admitted to the Meridia quadrant while it is closed. Meridia's Chosen is an ordinary faction in the earlier stages, and only this tie belongs to the story.

Details: [The dormant faction's war ally](closed-sector-story.md#spoiler-the-dormant-factions-war-ally).

### Spoiler: Why Do Siege Breakthroughs by the Dormant Faction Say "Raided"?

Malcyte is a raider faction, like the Corsair Syndicate while that faction holds no war-zone systems. A siege breakthrough by a raider gives the "Raided" outcome instead of "Captured".

Details: [As a raider](closed-sector-story.md#as-a-raider).

## Boarding the Dormant Faction

### Spoiler: How Hard Is It to Board the Dormant Faction's Ships?

Harder than usual. Malcyte is one of the factions that are harder to board, like Meridia's Chosen, Canisec and The Radiant. Its ships are simulated at the higher of the ship's own level and your effective level plus 10, unless the boarding comes from a capture mission. That puts the defenders at least 10 levels above you, which sets their HP multiplier to at least about 2.8. Each Malcyte defender also fights with 1.5 times the boarding power of a default defender, and its counterattack chance is 100% (see the explanation under the [faction defender profiles](../basics/boarding.md#faction-defender-profiles)).

| Faction | Defender power multiplier | Surrender chance | Other effects |
|---|---|---|---|
| Malcyte | 1.5 | 0% | Scuttle chance 100%, can vent airlocks, counterattack chance 100% |

Details: [Harder to board](closed-sector-story.md#harder-to-board) and [Defender profile](closed-sector-story.md#defender-profile).

### Spoiler: Will the Defenders Scuttle Their Ships?

Yes. They never surrender, and they scuttle at a rate of 0.08 per second, the same as Meridia's Chosen. The Corsair Syndicate scuttles at 0.016.

Details: [Scuttle rate](closed-sector-story.md#scuttle-rate).
