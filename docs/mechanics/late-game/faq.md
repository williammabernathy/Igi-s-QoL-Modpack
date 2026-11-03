# Common questions

These are short answers to questions players ask about the story lines behind the war. Every answer here is a spoiler. Each one ends with a link to the section with the full numbers and how they were read from the code. Values are for game version 0.8.3.4.

Terms are in the [late-game glossary](glossary.md), the [Conquest glossary](../conquest/glossary.md) and the [basics glossary](../basics/glossary.md).

## Unlocking the story lines

### Spoiler: How do I unlock the first story line?

Finish the first mission of your faction's Conquest chain (Stellar, Kolyatov or Luminate, depending on your faction). Its reward is 2,000,000 credits, 60 Vanguard Marks, XP, the second mission and the Umbral line. So the Umbral line comes with the reward of that first mission.

Details: [Gates](README.md#spoiler-gates).

### Spoiler: What does the 30% sector-control mission unlock?

It starts the next story mission of the Darkspace line. I did not find that mission defined in the code, so I can't tell you what it asks for.

Details: [Gates](README.md#spoiler-gates).

### Spoiler: How do I wake the dormant raider faction or open the closed sector?

I don't know. In version 0.8.3.4 the only thing that wakes Malcyte or opens Meridia is the debug console, and I found no other caller in the code. How either one happens in a normal game is "(not verified)".

Details: [Gates](README.md#spoiler-gates).

## The control row and the story station

### Spoiler: What is the control row on the system tooltip?

The "Umbral Reach control" row shows how far the Umbral Reach has spread into that system, as a percentage. It only appears above 0. It is the third kind of conquest row on a war-zone system tooltip, next to Defense and "Your war effort". An unowned system whose hub is an abandoned hulk can also show it.

Details: [On the system tooltip and in system data](control-story.md#on-the-system-tooltip-and-in-system-data).

### Spoiler: How does the control row grow and spread?

It grows and spreads on the hourly conquest update (every 3600 s).

- A system already above 0 rises by a random 0.05 to 0.1 each update, up to 1 (100%).
- A system at 1.0 can seed one adjacent system at 0.05, with a 50% roll each update, and at most one new system per update.

Details: [The control row and its spread](control-story.md#spoiler-the-control-row-and-its-spread).

### Spoiler: How do I start the spread in a system?

Use the Umbral Hacking Tool. It sets the system's control to 0.05, or 0.7 for the "uber" variant. It needs a friendly station and an infected neighbor above 0.19, unless it is the "uber" variant.

Details: [The control row and its spread](control-story.md#spoiler-the-control-row-and-its-spread).

### Spoiler: What does control do at one of the story faction's stations?

At an Umbral station, missions unlock at 5% station control and shops at 50%. I did not cover what control does beyond those thresholds.

Details: [The control row and its spread](control-story.md#spoiler-the-control-row-and-its-spread).

### Spoiler: What is the station in quadrant 2 and how hard is it?

It is the puppeteers' station: 6 parts, and not an economic outpost or an outpost system. It has 1 Core, 1 Refinery, 1 Small Docking Pad, 1 Tunnel and 2 turrets that share one tier roll. Below location level 45 both turrets are S2. At level 45 or above both are M2 or both are L1, with a 50/50 roll.

Off-screen siege numbers:

| Both tier turrets are | Hull | Firepower |
|---|---|---|
| S2 (below level 45) | 440 | 160 |
| both M2 or both L1 | 470 to 500 | 240 to 320 |

Details: [The story faction's station](control-story.md#spoiler-the-story-factions-station).

## The transponder and reputation

### Spoiler: What does the story transponder change?

With the Umbral transponder, five reputation costs from the early pages are skipped:

- Entering a facility or boarding a ship.
- Each part you kill on a station.
- Destroying a faction station while you hit it. Normally this costs -5000 reputation, or -1000 if you are already at -5000 or below.
- Destroying a Construction Yard. The cost is 0 whatever your standing.
- Destroying a faction's only yard as a way to slow it. It costs 0 reputation instead of -5,000 (-1,000 if Hostile or worse).

Details: [What the transponder changes](control-story.md#spoiler-what-the-transponder-changes).

### Spoiler: Can I kill a station's parts without losing reputation?

Yes, in two cases. With the Umbral transponder, every part you kill skips the owner's reputation hit. Separately, several mission stations and Umbral stations are flagged so that killing their parts costs nothing with the owner. Normally each part costs 100 points while you are Neutral or better, and 5 otherwise.

Details: [Killing station parts](control-story.md#killing-station-parts) and [Stations with no reputation loss](control-story.md#stations-with-no-reputation-loss).

## The closed sector

### Spoiler: Why can't I attack or enter the closed sector?

You can't attack it because the Meridia sector allows no stations. Malcyte hold it without hubs, so there is nothing to besiege, and the game says "There is nothing to besiege here" when you order an attack.

You can't enter the quadrant around it either, while it is closed. It is open only to Malcyte, their war ally Meridia's Chosen and, outside the Meridia sector itself, Darkspace Compact. Jump gates into the quadrant, and into the Meridia sector from outside it, are closed. How it opens in a normal game is "(not verified)".

Details: [The closed quadrant](closed-sector-story.md#the-closed-quadrant) and [Your options in the closed sector](closed-sector-story.md#your-options-in-the-closed-sector).

### Spoiler: Can anyone build an economic outpost in the closed sector?

No. A Meridia system is not a valid site for an economic outpost, so no faction builds one there. That means Meridia's Chosen and Malcyte yards can't be in Meridia.

Details: [No economic outposts in the closed sector](closed-sector-story.md#no-economic-outposts-in-the-closed-sector).

## The dormant raider faction

### Spoiler: How big can the dormant faction's fleet get?

While dormant, its fleet cap is 0, and it earns nothing. Once awake, the cap is 5 plus 1 for every 15 war-zone systems (rounded down), never more than 8. That is the same limit as Kolyatov, Stellar and Luminate.

Details: [Fleet cap](closed-sector-story.md#fleet-cap).

### Spoiler: How does the dormant faction rebuild its fleets without yards?

It doesn't use yards. Each hourly economy tick it tops up every idle or retreating fleet that sits at one of its own systems, paying full price from wealth. If it has fleet slots under the cap, it raises a new 2000-point fleet at its first owned system, which is a Meridia system, also at full price.

Nothing waits for yard time, so a wealthy Malcyte can rebuild a whole fleet in one tick. Destroying its stations does not slow it. Its wealth comes from taxes on systems it holds and from the raids it completes, so taking away its raid income and taxes is the only lever.

Details: [Yardless fleets](closed-sector-story.md#yardless-fleets).

### Spoiler: Who counts as the dormant faction's war ally?

Meridia's Chosen. Both are admitted to the Meridia quadrant while it is closed. Meridia's Chosen is an ordinary faction in the earlier stages. Only this tie belongs to the story.

Details: [The dormant faction's war ally](closed-sector-story.md#spoiler-the-dormant-factions-war-ally).

### Spoiler: Why do siege breakthroughs by the dormant faction say "Raided"?

It is a raider faction, like the Corsair Syndicate while that faction holds no war-zone systems. A siege breakthrough by a raider gives the "Raided" outcome instead of "Captured".

Details: [As a raider](closed-sector-story.md#as-a-raider).

## Boarding the dormant faction

### Spoiler: How hard is it to board the dormant faction's ships?

Harder than usual. It is a "hard boarding" faction, like Meridia's Chosen, Canisec and The Radiant. Its ships are simulated at the higher of the ship's own level and your effective level + 10, unless the boarding comes from a capture mission. That puts the defenders at least 10 levels above you, which sets their HP multiplier to about 2.8.

| Faction | Defense x | Surrender | Other |
|---|---|---|---|
| Malcyte | 1.5 | 0% | scuttle 100%, can vent airlocks, counterattack +0.5 |

Details: [Hard boarding](closed-sector-story.md#hard-boarding) and [Defender profile](closed-sector-story.md#defender-profile).

### Spoiler: Will the defenders scuttle their ships?

Yes. They never surrender, and they scuttle at a rate of 0.08 per second, the same as Meridia's Chosen (the Corsair Syndicate scuttles at 0.016).

Details: [Scuttle rate](closed-sector-story.md#scuttle-rate).
