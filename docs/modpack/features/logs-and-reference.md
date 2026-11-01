# Logs, reference and other mods

Tabs for reading back what happened, looking numbers up, keeping notes and checking other mods. None of these change how the game plays. Settings keys are listed in [settings.md](../settings.md).

## Event log

![The Log tab](../../images/tab-log.jpg)

The Log tab collects, newest first and with your computer's clock time:

- the game's pop-up notifications and the event lines above the HUD (reputation, forge and refinery jobs finished, saves);
- ECHO's status whenever it changes (its "Calculating..." between decisions left out), and ECHO switching on and off;
- arrivals of the ship you fly;
- one line per finished operation: outcome, defenders killed and captured, crew killed and wounded, items found and, separately, credits found, from the game's own operation log;
- items your ships pick up. Pickups of the same item within a minute share a line.

A notice repeated within 10 s counts up "(x3)" instead of adding lines. The line at the top sums the session's pickups with their sell value; hover it for the list. Filter by kind, or Clear. The log keeps up to 1000 lines until you quit; nothing goes into the save. It's the place to see what ECHO did while you were away.

## DPS parser

![The DPS parser tab, dealt by weapon](../../images/tab-dps-parser.jpg)

Every hit between your side (ships, drones, fighters) and anyone else, as it landed: shield and armour absorbed plus hull lost, after resistances.

- Dealt: by weapon (per fitted piece and slot; drones per drone type), by damage type, by your ship (drones count for their ship), by enemy type.
- Taken: by enemy weapon type, by damage type, by enemy type, by your ship.
- Each row: damage, share, per second of fighting, hits, crit rate, biggest hit, shield/armour/hull split, kills.
- Scope: the current or last fight, the 9 before it, or the whole session.

`FightGapSeconds` (5-120, default 15) quiet seconds end a fight. `WriteFile` (off) appends each finished fight's tables to `BepInEx/config/IgiQoL/combat-log.txt`. Nothing is saved with the game.

## Stat reference

![The Stat reference tab](../../images/tab-stat-reference.jpg)

At the top, while ["Used in" tooltips](stations.md#used-in-tooltips) is on, a search lists every forge recipe you can craft that uses an item. Below it, read from the game's data and formulas, so it follows game updates:

- how a hit is reduced on its way to the hull: the damage roll, crits, type bonuses, the 80% resistance cap, shields, armour, paint strip;
- every damage type with the weapons that deal it;
- every armour type's resistance and weakness (in game version 0.8.3.4 there's one: Armor Plating, 15% resist, takes +25% from Corrosion);
- the ship you fly: tonnage, mass, acceleration, top speed, sidestep, crit chance and per-type damage reduction, with the game's own descriptions of those stats.

## Notes

Named note tabs for your own notes and links, kept in `BepInEx/config/IgiQoL/notes.json` rather than in a save, so they're the same in every save. You edit a note as plain text; View shows `#` headings, `**bold**`, `*italic*`, `- ` bullets and clickable links (`[text](url)` or a bare `https://` link).

## Other mods

![The Mods tab](../../images/tab-mods.jpg)

The Mods tab is about the other BepInEx plugins you have installed.

Compatibility, at the top:

- the game version the modpack was tested on;
- the plugins BepInEx refused to load (missing dependency, declared incompatibility, wrong BepInEx version);
- every game method that Harmony patches from more than one mod, with each mod's patch kinds. "Check" marks a method where a mod has a transpiler or IL manipulator, or a prefix that returns bool and can skip the original. Patches count for the plugin whose assembly holds the patch method, so mods that patch under random Harmony ids are still named.

The same list goes to `LogOutput.log` once at the main menu (`[Mods]` lines).

Below that, every other plugin's own config file as rows, with the same controls as the Settings tab. ConfigurationManager tags are honoured (`Browsable`, `Category`, `DispName`, `Description`, `Order`, `ReadOnly`), and so is its sort order. `[Flags]` enums and types without their own control get a text field. Changes write that plugin's `.cfg` at once; mods that read their settings only when the game starts need a restart.
