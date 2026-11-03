# Vanguard Galaxy mechanics

How Vanguard Galaxy's combat, economy and war systems work, with the numbers and formulas the game doesn't show you. Everything here was read from the game's decompiled code and asset files for game version 0.8.3.4, not measured in play. Anything the code didn't confirm is marked "(not verified)".

Each page has two parts. The first explains the system as you meet it in play: what happens, what the game shows, and the numbers that matter. The last section, "In the game code", backs that up with the game methods, fields and raw formulas each claim was read from, so you can check it yourself. An "In the code:" link at the end of a section jumps to its notes.

The pages are grouped by when the game opens each system, so you can read up to where you are without seeing what comes later. A page only names a later system as a pointer ("this unlocks with Beyond the Front"), never with its details. The last group covers the story lines and is all spoilers.

| Stage | When it opens | What's in it |
|---|---|---|
| [Basics](mechanics/basics/README.md) | From a new game | [Combat damage](mechanics/basics/combat-damage.md), [scaling](mechanics/basics/scaling.md) of enemies, gear and loot with level, [combat stations](mechanics/basics/stations.md), [boarding](mechanics/basics/boarding.md), and a [glossary](mechanics/basics/glossary.md) |
| [Conquest](mechanics/conquest/README.md) | Level 28, when the story sends you to the Conquest sector | [The war](mechanics/conquest/war.md) (the Defense number, sieges, raids, war effort, fleet labels), [the 30% sector-control mission](mechanics/conquest/sector-control.md#the-30-sector-control-mission), [war stations](mechanics/conquest/war-stations.md), [faction economy](mechanics/conquest/faction-economy.md), and a [glossary](mechanics/conquest/glossary.md) |
| [Beyond the Front](mechanics/expansion/README.md) | Level 60, after finishing a 30% sector-control mission | [Your empire](mechanics/expansion/your-empire.md) (buying and claiming systems, your stations), [Empire Fleets](mechanics/expansion/empire-fleets.md), and a [glossary](mechanics/expansion/glossary.md) |
| [Late game](mechanics/late-game/README.md) | Story lines that run through and after conquest | Story spoilers. Open it only if you want them |

Each stage also has a "Common questions" page with short answers to the questions players ask most, such as how damage is calculated or how to finish the 30% mission. Every answer links to the section that has the full numbers: [Basics](mechanics/basics/faq.md), [Conquest](mechanics/conquest/faq.md), [Beyond the Front](mechanics/expansion/faq.md) and [Late game](mechanics/late-game/faq.md) (spoilers).

A game update can change any of these numbers. The pages say which version they were checked against.

These pages were written alongside [Igi's QoL Modpack](modpack/README.md), a BepInEx plugin for the game. You don't need the modpack to use anything here.
