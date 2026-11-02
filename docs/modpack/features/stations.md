# Stations, loadouts and crafting

Things you do while docked, done for the whole fleet or in one click. Settings keys are listed in [settings.md](../settings.md).

## Station services

![The airlock screen with the Favourite button and the five service buttons in their short labels](../../images/station-services.jpg)

Five buttons at the end of the airlock's button row, after the game's own. The [Favourite button](#favourite-stations) comes first in the row. A service button shows up while it has work to do, or while it can't do it here (then it is greyed out, see below). Hovering one lists every ship and stack it would touch, what it costs and what it couldn't do.

| Button | What it does |
|---|---|
| Repair fleet N cr | Starts the hangar's paid repair (about 20 s, at the hangar's own price and reputation discount) on every damaged ship in your fleet. The free repair the station does by itself takes minutes |
| Restock ammo | Tops up every fleet ship's ammo to the "ammo minutes" target of ECHO's Autopilot tab: from the armory first, then bought from the station's credit shops. The game's ECHO only restocks the ship you fly |
| Sell by rules +N cr | Applies the [Item rules](loot-and-cargo.md#item-rules) Sell and Sell above N dock actions to your fleet's cargo, to your data inventory with [`SellFromData`](echo.md#echo-cargo-rules) on, to this station's material storage with [`SellFromStorage`](echo.md#echo-cargo-rules) on, and to the armory with [`SellFromArmory`](echo.md#echo-cargo-rules) on. Sell above N first stores up to N of the item in the armory or the station's storage, and sells the rest |
| Replace crew | Puts back the crew each ship lost to hull hits (see below) |
| Unload brigs | Moves the prisoners in your ships' brigs to the station brig, so the next boarding's captives fit aboard instead of floating off with "Brig full". Willing prisoners stay willing. The station brig's size grows with your level |

A button whose service can't be done here (no hangar, not enough credits, ammo not sold here, no storage that takes the item) is greyed out and the tooltip says why. When the row would run into the Undock button, the buttons switch to short labels (Repair, Ammo, Sell, Crew, Brig) until you leave the airlock screen, as in the picture. The game's own buttons and the Favourite button count towards that. Clicking a button shows a notification with what it did, plus the first problem it ran into.

Crew losses: every hull hit of 2% or more can kill one crew member, on every ship. The mod counts those deaths per ship and crew type in the save. Replace crew fills them from your crew reserve first, then willing prisoners of that type from the ship's brig and the station brig (at 40% of the hire price, the game's recruit cost), then hires at the station's personnel center at its price and stock. Crew of that type added to the ship any other way (by you, ECHO, a loadout) counts as a replacement, so nothing is replaced twice; crew coming back aboard from an operation doesn't count. The game's ECHO only hires for the ship you fly, so without this your fleet ships never get their losses back.

`FuelCells` (0-50, 0 = off) makes restocking also keep that many warp fuel cells in the ship you fly (the only ship that burns fuel): armory first, then bought, fastest kind first. A part-used cell counts as one, as the game counts it. The button reads "Restock fuel" when only fuel is missing.

The same services can run every time you dock, ECHO included: `RepairOnDock`, `RestockOnDock`, `SellOnDock`, `CrewOnDock` and `BrigOnDock` (all off). With ECHO on, selling is left to [ECHO cargo rules](echo.md#echo-cargo-rules). With `SellOnDock` on, the game's fleet auto-deposit waits until the sale is done, so it can use the room the sale frees in station storage and the armory. A run on docking ends with a "Docked: ..." notification of what it did and one notice for each service that fell short, each shown once per station visit. `AirlockButtons` (on) shows the buttons.

Hired mercenaries are left out of every service. Repair skips ships that are away for repairs, since the game brings those back repaired for free. Favourites, mission items, usables the game keeps aboard, warp fuel and the ammo a ship fires are never sold. Selling from the armory also keeps warp fuel, every ammo type your fleet fires and the pieces your saved loadouts use.

## Ship loadouts

![The Loadouts tab while docked](../../images/tab-loadouts.jpg)

Save a ship's whole fit under a name and apply it later while docked at a station with a hangar. Open it from the Loadouts tab or the "L" button in the row of small buttons at the top right of the hangar's ship view, three places left of the game's "S" (skill preset) button. The button opens the tab on the ship the hangar shows.

![The hangar's L button, first of the small buttons above the ship](../../images/hangar-loadouts-button.jpg)

The tab has a ship dropdown (your fleet first, then your other ships), a name field with an "Include ECHO settings" tick and Save, and one row per saved loadout with Apply, Update, Rename and Delete. Saving under a name that exists asks before it replaces it. You can save and manage loadouts anywhere. Apply needs a docked station with a hangar and one of your own ships that the hangar can refit; when it can't apply, the tab says why under the ship dropdown and Apply is greyed out.

A loadout holds the exact weapons, modules and boosters (matched by their rolled stats, so socketing an aspect or a booster's unlock progress doesn't break the match), drones, officers, crew per type, the skill preset name, the ship's [per-ship settings](interface.md#ship-settings-popup) and, when ticked, ECHO's settings.

Applying runs skills, equipment, officers, crew, drones, then settings:

- Skills: the saved skill preset is selected, if it still exists and you have the skill points for it.
- Equipment comes from what's fitted, the armory and the ship's cargo; replaced pieces go to the armory. A piece that's gone is replaced by the best of the same type and rarity (flagged in the report; a favourited piece is never used for this), or the fitted one stays.
- Officers: the ship's officers that aren't in the loadout go back to your roster, and the saved ones are seated. One who is on another ship stays there and is reported.
- Surplus crew goes to your reserve, and shortfalls come from the reserve or are hired here.
- Drones are set per slot, and only drones you have unlocked.
- The "Last apply" list at the bottom of the tab says what was done and what wasn't.

Loadouts saved on your other ships of the same hull are listed under "Saved on your other ... hulls" and can be applied or copied here with "Copy here". Applying a loadout with ECHO settings to the ship you fly restores them at once; for another ship they are used when you next fly it. Switching to another ship restores the ECHO settings of the loadout last applied to it; refits and loading a save don't.

## Crafting roll-up

![A forge recipe with the Craft missing parts button](../../images/craft-rollup.jpg)

Forge recipes you can't craft yet get a "Craft missing parts" button with an amount field (1-999) in the "insufficient materials" strip. The strip shows the number of parts, the forge time (summed, as if run one after another), the number of ores to refine and their refinery time, the fees and, in orange after "Bring:", whatever is still missing. Hover it for every job. When nothing at the station makes what's missing, the strip says so in orange and the button stays greyed out.

When the station has a refinery, missing canisters are covered by refining ore from its materials storage (and your cargo when docked there). Per canister it picks the ore with the best yield, counting by-products and the refinery's carried-over fractions. Favourite ore, ore your missions need and ore the game won't auto-refine are left alone.

Starting a roll-up starts each refinery job and queues each part job as soon as its ingredients (or ore), a free slot and the credits are there, then the recipe itself. That's checked every second where you are and every 10 s at other stations, also while you're away. Parts and roll-up canisters go into the station's materials storage even with "deposit in cargo" on. A notification ("Roll-up finished: ...") says when the recipe is queued. Ingredients that no recipe makes (drops) still have to be at the station.

![The same recipe with a roll-up running](../../images/craft-rollup-running.jpg)

While a roll-up runs, the amount field is gone and the strip counts the forge jobs queued and refinery jobs started, and says what it is waiting for (an ingredient, credits, room in the materials storage or a free slot). Hover it to see each job marked started, queued or waiting. The button reads "Stop roll-up": it ends the roll-up, and jobs already queued keep running. Running roll-ups are saved with the game, and there is one per recipe and forge.

## Refinery filter

![The refinery filtered to Carbon: two ores left, best yield first](../../images/refinery-filter.jpg)

Left-click a material in the refinery's left column to show only the ore that refines into it, sorted by how much of that material one unit yields. The other materials dim while the filter is on, and the material's tooltip says what a click does. Click the material again to show all ore, or click another one to switch. When no ore at the station yields the material, the ore row says so. If the selected ore gets hidden, the first ore left is selected. Leaving the refinery clears the filter. It changes only what the screen shows.

## "Used in" tooltips

![The Titanium Plate tooltip listing the recipes that use it](../../images/used-in.jpg)

Hover a forge ingredient (a refined product, a canister, a signal fragment) and its tooltip lists the forge recipes that use it, with how many one craft takes, the same number the forge's ingredient list shows. Only recipes you can craft are listed, the ones the forge offers you, so a blueprint shows up as soon as you learn it. When you have a recipe in more than one rarity, its line has one count per rarity, in that rarity's colour. The list stops at 8 recipes and says how many more there are. The comparison tooltip for your fitted item doesn't get the list.

For the full list, open the Stat reference tab (under "More" at the default window width) and type part of the item's name into the search at the top, two letters or more. Each matching ingredient gets a row with its recipe count (up to 20 rows, then it asks for more of the name). Show opens every recipe that uses it, in the same format as the tooltip, with no cap, and Hide closes it again. A search with only one match opens by itself.

![The Stat reference tab's search with the Titanium Plate list open](../../images/used-in-search.jpg)

## Operations tab

![The Operations tab scrolled to its running operation, with the Abandon button](../../images/tab-operations.jpg)

Operations are the game's boarding and installation raids. The tab has four sections:

- Operations you start: the stealth, ammo and automatic-movement choices the operation panel starts with. The game remembers these between operations; changing them here is the same as changing them on the panel. `PlayerAutoBuyOut` ("Accept buyouts automatically", off) also ticks "Auto Accept Buyouts" on every new operation, which the game doesn't remember.
- Operations ECHO starts: see [ECHO's operations](echo.md#echos-operations).
- Priority compartment: the room your crew go for first, one choice for boardings of ships (`BoardingCompartment`, "Boardings") and one for installations, where the crew go in on foot (`InstallationCompartment`, "Installations"). When an operation starts, yours or ECHO's, the first compartment of that type in the layout becomes its priority, as if you had clicked it on the operation screen, and you can still click another one. A layout without that room gets no priority. The game keeps no priority between operations, and the rooms differ from ship to ship and site to site, so the choice is a kind of room, not a room number. Each list offers only the rooms that kind of operation can have, without the airlock and corridors. Both default to "None (game default)".
- Running operations: yours and ECHO's. Each line gives the site, who started it (You or ECHO), its phase, elapsed and estimated time, and the kills, crew lost and crew wounded so far, with that operation's own Stealth, Ammo, Auto movement and Auto buyouts. Changes apply to the running operation at once, and follow changes made on the game's panel. Each operation has the same exit button as the game's operation panel. After a victory it reads Extract, and your crew heads back to your ship with what it collected. Before that it reads Abandon: it asks first, then your crew retreats (or the approach is called off) and the operation ends. The button goes away once the crew is already leaving or the extraction has started.

![The confirmation that Abandon asks for](../../images/operations-abandon-confirm.jpg)

## Favourite stations

![The Stations tab with two favourites and the station search](../../images/tab-stations.jpg)

The Stations tab keeps a list of stations you pick, nearest first. Each one shows its system and the jumps to it from where you are ("this system", "N jumps", or "no known route"), the game's facility icons (hover one for its name), and your material storage there: space used out of the capacity, and the three biggest stacks (hover the line for all of them). Travel to closes the window and plots the course the way a click on the galaxy map does, and asks first when you're docked and the game has something to warn about. At the station you're docked at it reads "You're here" instead. The x takes the station off the list.

There are three ways to add a station or take it off again:

- Shift-click its marker on the galaxy map. The marker's tooltip says which of the two a Shift-click does.
- The Favourite button at the start of the airlock's button row while you're docked there. It reads Unfavourite on a favourite, and a notification says what changed.
- "Add a station" at the bottom of the tab: type part of a station or system name (two letters or more). It searches the stations the galaxy map shows, in systems you can reach, and lists up to 12 with an Add or Remove button each.

![The airlock with the Unfavourite button at the start of the row](../../images/airlock-favourite.jpg)

The search box above the list looks through the favourites' material storage: type part of an item name ("tungsten", two letters or more) and every favourite holding a matching item gets a row with the count and Travel to, up to 30 rows. Hover the item for its tooltip. The armory and the data inventory are the same at every station, so they aren't listed. Shop stock and mission boards only fill in while you're at a station, so the list doesn't show them.

The list is kept in the save. A favourite whose station no longer exists is left out of the list.
