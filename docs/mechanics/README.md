# Game mechanics

How Vanguard Galaxy's systems work under the hood, for deciding how to approach a fight, a siege or a faction war. The game shows few of these numbers. Everything here was read from the decompiled code of game version 0.8.3.4 and from the game's asset files, not measured in play. A game update can change any of it; re-check the cited methods in `decompiled/` after a patch.

New to the game's war systems? Start with the [glossary](glossary.md). It lists every term and abbreviation these pages use, what the game itself shows for each, and where the game uses one word for different things.

| Page | Answers |
|---|---|
| [Conquest](conquest.md) | What the Defense number means, how sieges and raids play out and end, what each fleet label on the Conquest Map means, what war effort does, [how to finish the 30% sector-control mission](conquest.md#the-30-sector-control-mission), and how your options change before and after "Beyond the Front" |
| [Glossary](glossary.md) | What every term and abbreviation means (outpost, hub, headquarters, Defense, wealth, home subsidy, war effort, Great Gate, reputation levels and more), the exact text the game shows for each, and the code name |
| [Faction economy](faction-economy.md) | Where a faction's money and warships come from, how fast it rebuilds, and which actions slow its production |
| [Combat stations](stations.md) | What each station part does, how many hit points it has, what it drops, what destroying one costs in reputation, and which parts are worth killing first |
| [Combat damage](combat-damage.md) | How a shot becomes hull damage: stats, damage types, crits, shields, armor and resistances |
| [Scaling](scaling.md) | How enemy HP and damage grow with level and rank, and what loot rarity to expect at a level |
| [Boarding](boarding.md) | How boarding and facility operations are simulated, and what the numbers on their tooltips mean |

Each section ends with a `Source:` line naming the game classes and methods it was read from, so you can check a claim or follow it up in `decompiled/`. Anything that couldn't be confirmed in code is marked "(not verified)".
