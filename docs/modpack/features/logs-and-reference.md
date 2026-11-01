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

Pop out puts the log in a [window of its own](interface.md#pop-out-windows) beside the game: the newest lines first, without the help text, with a filter of its own that doesn't change the tab's. Its Options set how many lines it shows (10 to 150, 25 by default). Clear stays on the tab. By default the window is always shown; its Options can limit it to while ECHO is running.

## DPS parser

![The DPS parser tab, dealt by weapon](../../images/tab-dps-parser.jpg)

Every hit between your side (ships, drones, fighters) and anyone else, as it landed: shield and armour absorbed plus hull lost, after resistances.

- Dealt: by weapon (per fitted piece and slot; drones per drone type), by damage type, by your ship (drones count for their ship), by enemy type.
- Taken: by enemy weapon type, by damage type, by enemy type, by your ship.
- Each row: damage, share, per second of fighting, hits, crit rate, biggest hit, shield/armour/hull split, kills.
- Scope: the current or last fight, the 9 before it, or the whole session.

`FightGapSeconds` (5-120, default 15) quiet seconds end a fight. `WriteFile` (off) appends each finished fight's tables to `BepInEx/config/IgiQoL/combat-log.txt`. Nothing is saved with the game.

### DPS meters

Bar meters that float over the game (see [pop-out windows](interface.md#pop-out-windows)), as many as you like. Each is its own window with its own settings, so one can show the damage your fleet deals, ship by ship, while another shows the damage it takes. On the DPS parser tab, "Add a meter..." starts one from a preset ("My fleet, dealt by ship", "My fleet, taken from ship" or "Dealt by damage type"), and "Pop out meters" opens the ones you've saved, or a first one when there are none. While meters are out the button reads "Dock back all". The Dock button in a meter's header and the x on its chip in the strip along the bottom of the QoL window bring it back to the tab; the header's own close button only closes it.

A meter shows one bar per row of the view it follows, longest first, the top bar full width and the rest scaled to it. The text on the bar is the row's name and one number. A meter keeps showing the last fight until the next one starts. Its Options button, under the pop-out's own settings, has:

- View: any of the 8 views of the tab. The meter's title and its chip in the strip name it.
- Scope: the current (or last) fight, or the whole session.
- Rows: 5, 8, 10, 15 or 25.
- Number: total damage, damage per second of fighting, or share of the view's total.
- Bar: plain, or split into shield, armour and hull as the damage was absorbed.
- Colour: by role (your flagship, a fleet ship, a drone or fighter, a hired mercenary, everyone else), by hull class, by damage type (a ship takes the colour of the type it dealt or took most), or picked per ship. Picking lists your flagship and fleet, each with a button that cycles through eight colours; other units keep their role colour. With a split bar the colour shows as a strip on the left edge.
- Remove this meter, which closes it and forgets its settings.

Changing the view or scope rebuilds the window, which closes its Options panel; the other settings redraw it in place. A row's role and hull class are noted when its first hit lands, so a ship destroyed later keeps its colour. A meter redraws once a second, and only when something was recorded. Layouts and settings are saved on your machine (`IgiQoL.Popout.dps-meter-N` in PlayerPrefs), not in the save.

## Stat reference

![The Stat reference tab](../../images/tab-stat-reference.jpg)

At the top, while ["Used in" tooltips](stations.md#used-in-tooltips) is on, a search lists every forge recipe you can craft that uses an item. Below it, read from the game's data and formulas, so it follows game updates:

- how a hit is reduced on its way to the hull: the damage roll, crits, type bonuses, the 80% resistance cap, shields, armour, paint strip;
- every damage type with the weapons that deal it;
- every armour type's resistance and weakness (in game version 0.8.3.4 there's one: Armor Plating, 15% resist, takes +25% from Corrosion);
- the ship you fly: tonnage, mass, acceleration, top speed, sidestep, crit chance and per-type damage reduction, with the game's own descriptions of those stats.

## Notes

Named note tabs for your own notes and links, kept in `BepInEx/config/IgiQoL/notes.json` rather than in a save, so they're the same in every save. You edit a note as plain text; View shows `#` headings, `**bold**`, `*italic*`, `- ` bullets and clickable links (`[text](url)` or a bare `https://` link).

Pop out shows the open note in a window of its own, read-only, as a checklist or reference while you play. You keep editing on the tab, and the window redraws when you save a change. Each note can have its own window; its Options pick another note. Dock back closes the window, and popping the same note out again brings it back where you left it. Deleting a note closes its windows. By default a note window is always shown; its Options can limit it to while you're docked.

## Other mods

![The Mods tab](../../images/tab-mods.jpg)

The Mods tab is about the other BepInEx plugins you have installed.

Compatibility, at the top:

- the game version the modpack was tested on;
- the plugins BepInEx refused to load (missing dependency, declared incompatibility, wrong BepInEx version);
- every game method that Harmony patches from more than one mod, with each mod's patch kinds. "Check" marks a method where a mod has a transpiler or IL manipulator, or a prefix that returns bool and can skip the original. Patches count for the plugin whose assembly holds the patch method, so mods that patch under random Harmony ids are still named.

The same list goes to `LogOutput.log` once at the main menu (`[Mods]` lines).

Below that, every other plugin's own config file as rows, with the same controls as the Settings tab. ConfigurationManager tags are honoured (`Browsable`, `Category`, `DispName`, `Description`, `Order`, `ReadOnly`), and so is its sort order. `[Flags]` enums and types without their own control get a text field. Changes write that plugin's `.cfg` at once; mods that read their settings only when the game starts need a restart.
