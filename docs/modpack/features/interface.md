# Window and HUD

The mod window, the per-ship settings popup, and the small additions to the game's own HUD. Settings keys are listed in [settings.md](../settings.md).

## Mod window

Every tab of the modpack lives in one window. Open and close it with F6 (`Hotkey`, any key name; ignored while you type in a text field), with the QoL button in space, or with the QoL tab next to the game's Fleet button above the station's tabs while docked. Hovering either button names the hotkey. The tabs are Scanner, DPS parser, Log, ECHO, Item rules, Operations, Stations, Loadouts, Notes, Stat reference, Mods and Settings; a tab whose feature is switched off isn't there. The window opens on the tab you last used.

It starts 760 px wide and 520 px tall (wider at a larger text size) and never goes below 480 px by 300 px. Drag it by its header and resize it from the corner at the bottom right; it remembers its position, size and tab. The tab bar shows the tabs that fit: at the default width that is Scanner to Loadouts, and the rest (Notes, Stat reference, Mods, Settings) wait in the "More" dropdown at the right end of the bar. Make the window wider and more tabs move into the bar. "More" names the open tab when it's one of the hidden ones. The window is never narrower than the open tab's rows need, so a tab with wide rows widens it while it's open and the window goes back to your width on the next tab. A position or size saved at a larger resolution or text size comes back inside the screen.

![The Settings tab](../../images/tab-settings.jpg)

Settings has every feature's on/off switch plus the settings that have no tab of their own. The switches are labelled "Feature on (applies after a game restart)". A feature with its own tab (ECHO, Operations, Item rules) keeps its settings there, so each setting lives in one place, and its section on Settings says which tab. If the feature that owns a tab is switched off, the settings that would have been on it show up on Settings instead. Every row has a plain-language name; hover it for the full explanation. Switches are check boxes, lists of choices are dropdowns and numbers are text fields; a number outside its range is pulled into it. Changes are written to `BepInEx/config/igi.qol.cfg` at once, and on/off switches apply at the next game start. The Window section ends with a "QoL button position" row with a Reset button (see below).

The QoL button in space sits on top of the side menu's button column at the right by default and moves with the column when the side panel opens and closes. `ButtonPlace` = "Above the Dock button" puts it above the HUD's Dock button at the bottom middle instead. You can drag the button away from either place: a drag never opens the window, and a drop off screen is pulled back on. The offset is kept per place, and "Reset" under the Window settings puts the button back. The button hides whenever the game hides its HUD (docked, dialogues), so it never covers a station screen.

![The QoL button on top of the side menu column](../../images/qol-button-side-menu.jpg)

![The QoL button above the Dock button](../../images/qol-button-dock.jpg)

Docked, the same toggle is a QoL tab floating above the station's tabs, beside the game's Fleet button:

![The QoL tab above the station's Airlock tab](../../images/qol-station-tab.jpg)

## Pop-out windows

Some views float on their own, outside the mod window: the [combat overview](fleet-combat.md#combat-overview), [DPS meters](logs-and-reference.md#dps-meters), the [Log](logs-and-reference.md#event-log), [notes](logs-and-reference.md#notes) and the [Scanner](loot-and-cargo.md#scanner-window). Each one works like the game's fleet panel:

- Drag it by its header. It stays on screen, and a layout saved at a larger resolution or text size comes back inside the screen.
- The header has Options, `-` and `+` to collapse it to the header and open it again, Dock (views that come from a tab) and `x` to close it. The corner at the bottom right resizes it, down to a minimum size that depends on the view.
- Its Options button sets when it shows ("Always", or "Only when ..." its own condition: in combat for the combat overview and DPS meters, ECHO is running for the Log, you're docked for notes, something here to board, salvage or pick up for the Scanner), its opacity (20-100%, in steps of 10), click-through, and resets its position and size. The combat overview starts on "only when in combat", the others on "Always". A window hidden by its show condition keeps its place and comes back when the condition holds; it's checked four times a second.
- Click-through is on by default: clicks on the window's body reach the game under it, and the header keeps working. Hold `PopoutInteractKey` (Ctrl by default; type LeftCtrl, LeftShift, LeftAlt or any other key name; either Ctrl, Shift or Alt key counts) to use the body, or switch click-through off in Options.
- A view that comes from a tab (all but the combat overview) has a Pop out button on that tab ("Pop out meters" for the DPS meters), which reads "Dock back" while the view is out ("Dock back all" for the DPS meters), and a Dock button on its own header that closes it and opens the tab. The tab stays as it was. DPS meters and notes can have several windows at once, each with its own layout and settings.

Each window keeps its position, size, collapsed state, opacity and options per machine, in Unity's PlayerPrefs next to the mod window's position, so they don't travel with a save. An open window comes back after loading a save and after a restart, and it closes by itself if the feature behind it is switched off. Pop-outs sit under the mod window, so one never covers a tab. While one is open, the mod window shows a "Pop-out windows" strip along its bottom with a chip for it: click the chip to bring the window back on screen and flash its header, or `x` to close it (to dock it back, for a view that has a tab). A chip is greyed while its window is hidden by its show condition, and its tooltip says what it's waiting for. A small window grows while its Options panel is open so the whole panel fits.

![The mod window with its Pop-out windows strip, the combat overview at the left and a DPS meter at the top right](../../images/pop-out-strip.jpg)

## Ship settings popup

Each of your ships has its own settings: missile retargeting, drones on arrival, drone mode, distance and which ship they stay with, engagement distance, keeping distance from other ships, the hull % at which it retreats for repairs, target choice with up to two "Then by" rules, how often it re-checks its target, finishing weak targets, a level to avoid, how far from the flagship it attacks, one target rule per weapon, and which cargo stays aboard when the fleet cargo moves everything to the flagship ([pinned cargo](loot-and-cargo.md#pinned-cargo)). Open the popup with the last button on the ship's row in the fleet panel. One popup is open at a time, above the mod window; Close or the x shuts it. "Default" means the ship follows the setting on the mod window's Settings tab. A row whose feature is switched off is left out (a one-line note says so for missile retargeting, drones on arrival, engagement distance and target priority). These per-ship settings are saved with the game.

![Per-ship settings for the Hurricane: target choice with two "Then by" rules, and a rule row per weapon](../../images/ship-settings-popup.jpg)

The features behind these rows are described in [fleet-combat.md](fleet-combat.md) and, for pinned cargo, [loot-and-cargo.md](loot-and-cargo.md#pinned-cargo).

## Fleet command strip

A second row of buttons under each ship in the fleet panel (bottom left): stop, hold position, regroup and the four combat stances (fleet ships only; the stance the ship has is lit), auto-tractor (ships you manage that have a tractor), deploy or recall drones (ships with a drone bay; lit while the drones are out), the ship's cargo window (ships you manage, not the flagship), and the ship settings popup, which every ship has. Clicking a button doesn't select the row.

## Fleet shield and armor bars

Each fleet row shows three bars: shield above, the game's hull bar (restyled) in the middle, armor below. A ship without shields or armor leaves that slot blank. `BarHeight` (2-12 px, default 4, the height of the game's hull bar) sets all three and applies at once; each row grows by three times the difference.

![The fleet panel with the command strip and the three bars](../../images/fleet-panel.jpg)

## Text size

`Percent` (50-200, default 100) draws all game and mod text bigger or smaller, separate from the game's own Scale setting, which resizes panels and text together. It applies at once; the mod window and any open pop-outs are rebuilt for the new size. Panels keep their size, so larger text can be cut off with "..." or wrap inside narrow labels (cargo stack counts wrap at 130%). Labels keep the lines they show at 100% and spill past their box instead of disappearing.

![The mod window and HUD at 130% text size](../../images/text-size-130.jpg)

## Jumps remaining

Adds "Jumps: N" under the distance line of the travel panel while you travel. N is the number of jumps left on your route; the line is hidden when none are.

## Mission distance

Shows how far away a mission's next location is, counted with the game's own route search (jump gates included):

- Mission board and missions window rows get a jump gate icon with the number of jumps when the location is in another system. Its tooltip is titled with the location and reads "Zorix, 40 jumps": the system, then the jumps.
- The mission details and the HUD mission tracker add it after the location: "Location: Fractured Crossroads (Red Gate, 1 jump)", "(this system)" or "(here)".

Most board missions are in the station's own system and get no row icon. The icons show up on board missions that send you to another system (a delivery one jump away, in the picture below), and in the missions window on missions you took elsewhere and on story missions.

![Mission board: the details give the location as "Ring I (here)", and a delivery row has a jump chip](../../images/mission-board-distance.jpg)

![Missions window: a story mission 40 jumps away, with the system and jumps in the details](../../images/missions-window-distance.jpg)

## Mission sort

The station mission board gets a sort button in its header, left of the refresh timer. Each click moves to the next order and re-sorts the board, keeping the mission you had selected (when none is selected any more, the first row shown is):

- Game order: as the game lists them (the default).
- Type: story, combat, mining, salvage, cargo, repair, then generic missions.
- Level: highest first. Each row gets a level chip ("L62") while this order is on, since the game shows the level nowhere on the row.
- Difficulty: hardest first (Insane, Hard, Challenging, Normal, Easy), then faction and story missions.

Each section of the board (ready to turn in, in progress, available) is sorted on its own, and missions that tie keep the game's order. The button and `Board` ("Mission board order") in the Settings tab are the same setting, so the order holds between visits.

![The mission board sorted by level, with an L62 chip on each row](../../images/mission-board-sort.jpg)

The missions window (the side panel's Missions tab) has its own sort button under its list, above the "N / 20 slots used" line, saved as `Window` ("Missions window order"). It has the same four orders plus one more:

- Distance: nearest first. The location you're at comes first, then this system, then other systems by jumps. Missions without a location or a known route go last.

The window sorts "ready to turn in" and "in progress" separately. Under Level, a row's level chip takes the place of its jump count, because the 190 px list has no room for both beside the progress bar. Some story missions name no location at all, so they have no level: they get no level chip and sort last. When the mission you had selected is gone, the window selects the first row shown instead of the first one in the game's order.

![The missions window sorted by level, with an L11 chip where the jump count was](../../images/missions-window-sort.jpg)

## Mission fit

Greys out station mission board offers that none of your ships has the equipment for. A typical case is a mining mission when no ship carries a mining turret, mining drones or a hangar bay that mines. The offer's icon, name and chips are dimmed, and a red "No fit" chip is added. Its tooltip, titled "No fit in your fleet", names the missing work, for example "Mining (Core)" or "Salvage (Structural)". The offer keeps its place in the list, and you can still pick and accept it. Only the mission board is marked; the missions window is left alone.

![Mission board offers for mining and salvage work greyed out with a No fit chip](../../images/mission-board-fit.jpg)

The test is the game's own (`SpaceShipData.HasLoadout`): a turret, drone bay, torpedo bay or hangar bay for the mission's work, on the layer the mission asks for. Generated mining and salvage missions ask for surface or core. Your flagship and every ship in your active fleet count, even while they're away from you. Hired mercenaries don't count, because their contracts end. Cargo and delivery missions are never greyed out, and neither are missions you already hold.

ECHO checks only the flagship before it takes a mission, so it can still answer "no suitable loadout" for an offer that isn't greyed out, when the gear for it is on a fleet ship.
