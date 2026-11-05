# Conquest

This stage covers the faction war in the Conquest sector (Ara Martis): the embassies, aligning with a faction, the war itself and the 30% sector-control mission. It opens at captain level 28. Values were read from the game code of version 0.8.3.4 (the decompiled source), not from testing in the game. The code was then compared with the 0.8.3.5 source, and every statement that changed there was rewritten. Anything the code does not confirm is marked `(not verified)`. The last section, "In the Game Code", lists the mission ids and conditions behind the plain-language steps.

## Spoiler: How You Get Here

The stage opens at level 28. At that level the story step "Gathering Storm", which sends you into the Conquest sector (Ara Martis), becomes available. Its last objective is to talk to the Canisec Commander. That conversation starts the first mission of the chain below.

The chain that follows, in order:

1. The story mission "Sector Control" asks you to find the three embassies and talk to their representatives.
2. Talking to a representative offers to align you with that faction. You answer a yes or no prompt first. A faction that is your enemy refuses. Whether you can change the choice later is `(not verified)` on this page.
3. The faction's first mission ("Internship" for Stellar, "For the Collective!" for Kolyatov, "Alignment" for Luminate) takes you to its headquarters and has you run missions for it.
4. Its second mission ("Hostile Takeover", "The People's Sector" or "Harmony") asks you to push that faction to 30% of the systems in the Conquest sector.

The details of each step and how to finish the 30% mission are in [sector control](sector-control.md).

In the code: [How You Get Here](#code-how-you-get-here).

## Level Cap

Your level cap is 60 during this stage. It stays there until you accept "Beyond the Front". The game offers that mission once you are level 60 and have finished one of the 30% missions (see the [Beyond the Front](../expansion/README.md) page for the requirements).

In the code: [Level Cap](#code-level-cap).

## Pages

- [Common Questions](faq.md) gives short answers to questions players ask about the war and the 30% mission. Each answer links to the section with the full numbers.
- [Glossary: Conquest](glossary.md) explains the terms and abbreviations used in the conquest pages. Earlier terms are in the [basics glossary](../basics/glossary.md).
- [War Zone and Battles](war.md) covers the war zone, the Defense number on a system tooltip, war effort, battles and sieges, capture, how NPC factions pick fights, and fleet labels on the map.
- [Sector Control](sector-control.md) covers the 30% sector-control mission and what you can do in the war before you hold a system.
- [War Stations](war-stations.md) covers hubs, Defense stations, garrisons and conquest station layouts.
- [Faction Economy and Ship Production](faction-economy.md) covers faction income, shipyards, warship delivery, the Great Gate and the economics of raids.

The next stage opens with the "Beyond the Front" mission. It is described on the [Beyond the Front](../expansion/README.md) page.

## In the Game Code

This part is the decompiled code behind the sections above: mission ids, conditions and method names. The names and conditions are the same in versions 0.8.3.4 and 0.8.3.5. To check a claim, search the decompiled source for the name.

### Code: How You Get Here

| Gate | Condition in code | What it controls |
|---|---|---|
| Level 28 | `StoryMissionSkipToConquest` is available at `player.level >= 28` | The story step that sends you into the Conquest sector (Ara Martis). Its last objective is `ConquestCanisec1`, and its reward starts `ConquestThreeFactions`. |

The chain that follows, in order:

1. `ConquestThreeFactions` ("Sector Control") asks you to find the three embassies and talk to their representatives.
2. Talking to a representative offers to align you with that faction (`Characters.AlignWithMajorFaction`, then `FactionAlignmentChoice`), after a yes/no prompt. A faction that is your enemy refuses. The code in this chain only starts the faction's first mission; whether the choice can be undone is not verified.
3. The faction's first mission (`ConquestStellar1`, `ConquestKolyatov1` or `ConquestLuminate1`) takes you to its headquarters and has you run missions for it.
4. Its second mission (`ConquestStellar2`, `ConquestKolyatov2` or `ConquestLuminate2`) asks you to push that faction to 30% of the systems in the Conquest sector.

### Code: Level Cap

The level cap is `Conquest.maxPlayerLevel`, 60 during this stage.
