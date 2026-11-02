# Logs, reference and other mods

Tabs for reading back what happened, looking numbers up, keeping notes and checking other mods. None of these change how the game plays. The DPS parser and Log tabs are on the mod window's tab bar; at the default width, Notes, Stat reference and Mods are in its "More" dropdown. Settings keys are listed in [settings.md](../settings.md).

## Event log

![The Log tab with pickups, an operation result, ECHO's status, event lines and notices](../../images/tab-log.jpg)

The Log tab collects, newest first and with your computer's clock time:

- the game's pop-up notifications and the event lines above the HUD (reputation, forge and refinery jobs finished, saves);
- ECHO's status whenever it changes while ECHO is running (its "Calculating..." between decisions left out), and ECHO switching on and off;
- arrivals of the ship you fly, with the system;
- one line per finished operation, from the game's own operation log: what it was (a boarding names its target, and ECHO's runs start with "ECHO:"), where, how it ended (succeeded, crew pulled out, crew lost or ship exploded), defenders killed and captured, crew killed and wounded, how many items it found and, separately, the credits found;
- items your ships pick up. Pickups of the same item within a minute share a line.

A line repeated within 10 s counts up "(x3)" instead of adding lines, and a long line wraps onto a second row. The line at the top sums the session's pickups with their sell value; it shows while the filter is Everything or Loot and something has been picked up, and hovering it lists the items. The filter offers Everything, Notifications, Event lines, ECHO, Arrivals, Loot and Operations. Clear empties the log and the pickup totals. The tab shows the newest 150 lines and says how many older ones it leaves out. The log keeps up to 1000 lines until you quit; nothing goes into the save. It's the place to see what ECHO did while you were away.

Pop out puts the log in a [window of its own](interface.md#pop-out-windows) beside the game: the newest lines first, without the help text, with a filter of its own that doesn't change the tab's. Its Options set how many lines it shows (10, 25, 50, 100 or 150; 25 by default). Clear stays on the tab. By default the window is always shown; its Options can limit it to while ECHO is running.

## DPS parser

![The DPS parser tab, dealt by weapon](../../images/tab-dps-parser.jpg)

Every hit between your side (ships, drones, fighters) and anyone else, as it landed: shield and armour absorbed plus hull lost, after resistances. Damage beyond the hull a target had left isn't counted.

- Dealt: by weapon (per fitted piece and slot; drones per drone type), by damage type, by your ship (drones count for their ship), by enemy type.
- Taken: by enemy weapon type, by damage type, by enemy type, by your ship.
- Each row: damage, share, per second of fighting, hits, crit rate, biggest hit, the percent of its damage that shields, armour and hull took, and, in the dealt views, kills (counted by weapon, by ship and by enemy type). The 25 biggest rows show, with a line counting the rest.
- Scope: the current or last fight, the 9 before it, or the whole session.

Per-second figures use the time spent fighting; quiet stretches longer than the fight gap don't count, which matters for the session total. Clear, or loading a save, starts the parser over. While the [combat overview](fleet-combat.md#combat-overview) is on, the tab has a button that opens it.

Two settings sit at the top of the tab. `FightGapSeconds` (5-120, default 15) quiet seconds end a fight. `WriteFile` (off) appends each finished fight's tables to `BepInEx/config/IgiQoL/combat-log.txt`. Nothing is saved with the game.

### DPS meters

Bar meters that float over the game (see [pop-out windows](interface.md#pop-out-windows)), as many as you like. Each is its own window with its own settings, so one can show the damage your fleet deals, ship by ship, while another shows the damage it takes.

![Two DPS meters: damage dealt by ship, and damage taken by ship with a split bar](../../images/dps-meters.jpg)

On the DPS parser tab, "Add a meter..." starts one from a preset: "My fleet, dealt by ship" (the Dealt, by your ship view), "My fleet, taken by ship" (the Taken, by your ship view, with a split bar) or "Dealt by damage type" (coloured by damage type). "Pop out meters" opens the ones you've saved, or a first one, the dealt-by-ship preset, when there are none. While meters are out the button reads "Dock back all". The Dock button in a meter's header and the x on its chip in the strip along the bottom of the QoL window bring it back to the tab; the header's own close button only closes it. A meter you haven't moved opens beside the combat overview, each further one a little lower, so they don't cover each other. By default a meter is always shown; its Options can limit it to while you're in combat.

A meter shows one bar per row of the view it follows, longest first, the top bar full width and the rest scaled to it. The text on the bar is the row's name and one number. The title reads "DPS: " and the view's short name, with "(session)" added when it follows the whole session. A meter keeps showing the last fight until the next one starts. Its Options button, under the pop-out's own settings, has:

- View: any of the 8 views of the tab. The meter's title and its chip in the strip name it.
- Scope: Current fight (the last one until a new one starts) or Whole session.
- Rows: 5, 8 (the default), 10, 15 or 25.
- Number: "Total" damage (the default), "Per second" of fighting, or "Share" of the view's total.
- Bar: Plain (the default), or Split in 3: shield, armour and hull as the damage was absorbed.
- Colour: By role (the default: your flagship, a fleet ship, a drone or fighter, a hired mercenary, everyone else), By hull class, By damage type (a ship takes the colour of the type it dealt or took most), or Picked per ship. Picking lists your flagship and fleet, each with a button that reads "Auto" until you pick a colour and "Next" after; it steps through eight colours, then back to Auto. Other units keep their role colour. With a split bar the colour shows as a strip on the left edge.
- Remove this meter, which closes it and forgets its settings.

Changing the view or scope rebuilds the window, which closes its Options panel; the other settings redraw it in place. A row's role and hull class are noted when its first hit lands, so a ship destroyed later keeps its colour. A meter redraws once a second, and only when something was recorded. Layouts and settings are saved on your machine (`IgiQoL.Popout.dps-meter-N` in PlayerPrefs), not in the save.

## Stat reference

![The Stat reference tab](../../images/tab-stat-reference.jpg)

At the top, while the ["Used in" tooltips](stations.md#used-in-tooltips) feature is on, [a search](#recipe-search) lists every forge recipe you can craft that uses an item. Below it, read from the game's data and formulas, so it follows game updates:

- how a hit is reduced on its way to the hull: the damage roll, crits, type bonuses, the 80% resistance cap, shields, armour, paint strip;
- every damage type with the weapons that deal it;
- every armour type's resistance and weakness (in game version 0.8.3.4 there's one: Armor Plating, 15% resist, takes +25% from Corrosion);
- the ship you fly (shown while you're in space): tonnage, mass, acceleration, top speed, sidestep, crit chance and per-type damage reduction, with the game's own descriptions of those stats.

### Recipe search

![The recipe search on the Stat reference tab, with Titanium Plate opened](../../images/used-in-search.jpg)

Type at least two letters of an item's name. The list shows up to 20 matching items, sorted by name (a levelled item carries its level, like "Titanium Plate L5"), each with how many of your craftable recipes use it and a Show or Hide button. A single match opens by itself. Hover an item's name for its tooltip.

An open item lists one line per recipe with how many one craft takes, as the [item tooltip](stations.md#used-in-tooltips) does, but without the tooltip's cap of 8 lines. A recipe you have in several rarities shows each rarity's amount in that rarity's colour. If more than 20 items match, the list says so and asks for more of the name; if none do, it says no match was found among the items your craftable recipes use. The search and the open item stay when you switch tabs.

## Notes

Named note tabs for your own notes and links, kept in `BepInEx/config/IgiQoL/notes.json` rather than in a save, so they're the same in every save. The bar at the top has a button per note, "+" for a new one (named "Note 2" and so on), "<" and ">" to move the open note along the bar, Rename, Delete (not while it's your only note; it asks first), Edit or View, and Pop out. A note that is empty opens for editing; one with text opens in View. You edit as plain text, and the note saves when you leave the field or switch notes. View shows `#` and `##` headings, `**bold**`, `*italic*`, `- ` bullets and clickable links (`[text](url)` or a bare `https://` link). The note you had open comes back after a restart.

If `notes.json` can't be parsed, it's renamed to `notes.json.broken-` plus a number and you start with an empty note; the tab says so. If it can't be read at all (locked by another program), it's left alone, nothing is written over it and edits aren't saved until you fix it and restart.

Pop out shows the open note in a window of its own, read-only, as a checklist or reference while you play.

![A note in its own window, rendered from the note's text](../../images/note-popout.jpg)

You keep editing on the tab, and the window redraws when you save a change. Each note can have its own window; its Options pick another note. Dock back closes the window, and popping the same note out again brings it back where you left it. Renaming a note retitles its windows, and deleting a note closes them. By default a note window is always shown; its Options can limit it to while you're docked.

## Other mods

![The Mods tab](../../images/tab-mods.jpg)

The Mods tab (in the "More" dropdown at the default width) is about the other BepInEx plugins you have installed.

Compatibility, at the top:

- the game version the modpack was tested on, or, if you run a different one, a warning that a feature that breaks switches itself off and is listed at the main menu;
- the plugins BepInEx refused to load (missing dependency, declared incompatibility, wrong BepInEx version);
- every game method that Harmony patches from more than one mod, with each mod's patch kinds (prefix, postfix, transpiler, finalizer, IL manipulator). "Check" marks a method where a mod has a transpiler or IL manipulator, or a prefix that returns bool and can skip the original. Patches count for the plugin whose assembly holds the patch method, so mods that patch under random Harmony ids are still named; a DLL with several plugins is listed once under all their names. This modpack's own patches are listed with the feature that made them.

The same list goes to `LogOutput.log` once at the main menu (`[Mods]` lines).

Below that, every other plugin as a heading (name, version, GUID, DLL file and what it needs) followed by its own config file as rows, with the same controls as the Settings tab. ConfigurationManager tags are honoured (`Browsable`, `Category`, `DispName`, `Description`, `Order`, `ReadOnly`), and so is its sort order: by section, then `Order` with the highest first, then name. A `ReadOnly` entry shows its value as text. `[Flags]` enums and types without their own control get a text field. Changes write that plugin's `.cfg` at once; mods that read their settings only when the game starts need a restart.
