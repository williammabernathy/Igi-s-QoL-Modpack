# Conquest

This stage covers the faction war in the Conquest sector (Ara Martis): the embassies, aligning with a faction, the war itself, and the 30% sector-control mission. It opens at player level 28. Values are read from the game code of version 0.8.3.4 (the decompiled source, not in-game testing). Anything not confirmed in code is marked `(not verified)`. The last section, "In the game code", has the mission ids and conditions behind the plain-language steps.

## Spoiler: How you get here

The stage opens at level 28. At that level the story step that sends you into the Conquest sector (Ara Martis) becomes available.

The chain that follows, in order:

1. The story mission "Sector Control" asks you to find the three embassies and talk to their representatives.
2. Talking to a representative offers to align you with that faction. The choice is not permanent.
3. The faction's first mission ("Internship" for Stellar, "For the Collective!" for Kolyatov, "Alignment" for Luminate) takes you to its headquarters and has you run missions for it.
4. Its second mission ("Hostile Takeover", "The People's Sector" or "Harmony") asks you to push that faction to 30% of the systems in the Conquest sector.

The details of each step and how to finish the 30% mission are in [sector control](sector-control.md).

In the code: [how you get here](#code-how-you-get-here).

## Level cap

Your level cap is 60 during this stage. It stays there until you accept "Beyond the Front", which is offered once you are level 60 and have finished one of the 30% missions ([gate](../expansion/README.md)).

In the code: [level cap](#code-level-cap).

## Pages

- [Common questions](faq.md): short answers to questions players ask about the war and the 30% mission, each linked to the section with the full numbers.
- [Glossary](glossary.md): terms and abbreviations used in the conquest pages. Earlier terms are in the [basics glossary](../basics/glossary.md).
- [War](war.md): the war zone, the Defense number on a system tooltip, war effort, battles and sieges, capture, how NPC factions pick fights, and fleet labels on the map.
- [Sector control](sector-control.md): the 30% sector-control mission and what you can do in the war before you hold a system.
- [War stations](war-stations.md): hubs, Defense stations, garrisons and conquest station layouts.
- [Faction economy](faction-economy.md): faction income, shipyards, warship delivery, the Great Gate and raid economics.

The next stage opens with the "Beyond the Front" mission: [Beyond the Front](../expansion/README.md).

## In the game code

This part is the decompiled code of version 0.8.3.4 behind the sections above: mission ids, conditions and method names. To check a claim, search the decompiled source for the name.

### Code: How you get here

| Gate | Condition in code | What it controls |
|---|---|---|
| Level 28 | `StoryMissionSkipToConquest` is available at `player.level >= 28` | The story step that sends you into the Conquest sector (Ara Martis). |

The chain that follows, in order:

1. `ConquestThreeFactions` ("Sector Control") asks you to find the three embassies and talk to their representatives.
2. Talking to a representative offers to align you with that faction (`FactionAlignmentChoice`). The choice is not permanent.
3. The faction's first mission (`ConquestStellar1`, `ConquestKolyatov1` or `ConquestLuminate1`) takes you to its headquarters and has you run missions for it.
4. Its second mission (`ConquestStellar2`, `ConquestKolyatov2` or `ConquestLuminate2`) asks you to push that faction to 30% of the systems in the Conquest sector.

### Code: Level cap

The level cap is `Conquest.maxPlayerLevel`, 60 during this stage.
