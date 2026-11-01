# Stations, loadouts and crafting

Things you do while docked, done for the whole fleet or in one click. Settings keys are listed in [settings.md](../settings.md).

## Station services

![The airlock screen with the five service buttons after the game's Trade Routes button](../../images/station-services.jpg)

Five buttons in the airlock's button row, after the game's own. Each shows up only while it has work, and hovering it lists every ship and stack it would touch and what it costs.

| Button | What it does |
|---|---|
| Repair fleet N cr | Starts the hangar's paid repair (about 20 s, at the hangar's own price and reputation discount) on every damaged ship in your fleet. The free repair the station does by itself takes minutes |
| Restock ammo | Tops up every fleet ship's ammo to the "ammo minutes" target of ECHO's Autopilot tab: from the armory first, then bought from the station's credit shops. The game's ECHO only restocks the ship you fly |
| Sell by rules +N cr | Applies the [Item rules](loot-and-cargo.md#item-rules) Sell and Sell above N dock actions to your fleet's cargo, to your data inventory with [`SellFromData`](echo.md#echo-cargo-rules) on, to this station's material storage with [`SellFromStorage`](echo.md#echo-cargo-rules) on, and to the armory with [`SellFromArmory`](echo.md#echo-cargo-rules) on |
| Replace crew | Puts back the crew each ship lost to hull hits (see below) |
| Unload brigs | Moves the prisoners in your ships' brigs to the station brig, so the next boarding's captives fit aboard instead of floating off with "Brig full". Willing prisoners stay willing. The station brig's size grows with your level |

A button that has work but can't do it here (no hangar, not enough credits, not sold here) is greyed out with the reason. When the row would run into the Undock button, the buttons switch to short labels (Repair, Ammo, Sell, Crew, Brig) for the rest of that visit, as in the picture.

Crew losses: every hull hit of 2% or more can kill one crew member, on every ship. The mod counts those deaths per ship and crew type in the save. Replace crew fills them from your crew reserve first, then willing prisoners of that type from the ship's brig and the station brig (at 40% of the hire price, the game's recruit cost), then hires at the station's personnel center at its price and stock. Crew of that type added to the ship any other way (by you, ECHO, a loadout) counts as a replacement, so nothing is replaced twice. The game's ECHO only hires for the ship you fly, so without this your fleet ships never get their losses back.

`FuelCells` (0-50, 0 = off) makes restocking also keep that many warp fuel cells in the ship you fly (the only ship that burns fuel): armory first, then bought, fastest kind first. A part-used cell counts as one, as the game counts it. The button reads "Restock fuel" when only fuel is missing.

The same services can run every time you dock, ECHO included: `RepairOnDock`, `RestockOnDock`, `SellOnDock`, `CrewOnDock` and `BrigOnDock` (all off). With ECHO on, selling is left to [ECHO cargo rules](echo.md#echo-cargo-rules). `AirlockButtons` (on) shows the buttons.

Hired mercenaries are left out of every service. Repair skips ships that are away for repairs, since the game brings those back repaired for free. Favourites, mission items, usables the game keeps aboard and the ammo a ship uses are never sold. Selling from the armory also keeps warp fuel, every ammo type your fleet fires and the pieces your saved loadouts use.

## Ship loadouts

![The Loadouts tab while docked](../../images/tab-loadouts.jpg)

Save a ship's whole fit under a name and apply it later while docked at a station with a hangar. Open it from the Loadouts tab or the "L" button left of the hangar's skill-preset "S" button.

A loadout holds the exact weapons, modules and boosters (matched by their rolled stats, so socketing an aspect or a booster's unlock progress doesn't break the match), drones, officers, crew per type, the skill preset name, the ship's [per-ship settings](interface.md#ship-settings-popup) and, when ticked, ECHO's settings.

Applying runs skills, equipment, officers, crew, drones, then settings:

- Equipment comes from the armory and the ship's cargo; replaced pieces go to the armory. A piece that's gone is replaced by the best of the same type and rarity (flagged in the report), or the fitted one stays.
- Surplus crew goes to your reserve, and shortfalls come from the reserve or are hired here.
- A report lists what was done and what wasn't.

Loadouts saved on your other ships of the same hull can be applied or copied. Switching to another ship restores the ECHO settings of the loadout last applied to it; refits and loading a save don't.

## Crafting roll-up

![A forge recipe with the Craft missing parts button](../../images/craft-rollup.jpg)

Forge recipes you can't craft yet get a "Craft missing parts" button with an amount field in the "insufficient materials" strip. The strip shows the number of parts, the forge time (summed, as if run one after another), the ore to refine and its refinery time, the fees and, in orange, whatever is still missing. Hover it for every job.

When the station has a refinery, missing canisters are covered by refining ore from its materials storage (and your cargo when docked there). Per canister it picks the ore with the best yield, counting by-products and the refinery's carried-over fractions. Favourite ore, ore your missions need and ore the game won't auto-refine are left alone.

Starting a roll-up starts each refinery job and queues each part job as soon as its ingredients (or ore), a free slot and the credits are there, then the recipe itself. That's checked every second where you are and every 10 s at other stations, also while you're away. Parts and roll-up canisters go into the station's materials storage even with "deposit in cargo" on. Running roll-ups are saved; the same button stops one (jobs already queued keep running). A notification says when the recipe is queued. Ingredients that no recipe makes (drops) still have to be at the station.

## Refinery filter

![The refinery filtered to Carbon: two ores left, best yield first](../../images/refinery-filter.jpg)

Click a material in the refinery's left column to show only the ore that refines into it, sorted by how much of that material one unit yields. The other materials dim while the filter is on. Click the material again to show all ore, or click another one to switch. When no ore at the station yields the material, the ore row says so. If the selected ore gets hidden, the first ore left is selected. Leaving the refinery clears the filter. It changes only what the screen shows.

## "Used in" tooltips

![The Titanium Plate tooltip listing the recipes that use it](../../images/used-in.jpg)

Hover a forge ingredient (a refined product, a canister, a signal fragment) and its tooltip lists the forge recipes that use it, with how many one craft takes, the same number the forge's ingredient list shows. Only recipes you can craft are listed, the ones the forge offers you, so a blueprint shows up as soon as you learn it. When you have a recipe in more than one rarity, its line has one count per rarity, in that rarity's colour. The list stops at 8 recipes and says how many more there are. The comparison tooltip for your fitted item doesn't get the list.

For the full list, type part of the item's name into the search at the top of the Stat reference tab. Each matching ingredient gets a row with its recipe count. Show opens every recipe that uses it, in the same format as the tooltip, with no cap, and Hide closes it again. A search with only one match opens by itself.

## Operations tab

![The Operations tab](../../images/tab-operations.jpg)

Operations are the game's boarding and installation raids. The tab has three sections:

- Operations you start: the stealth, ammo and automatic-movement choices the operation panel starts with. The game remembers these between operations; changing them here is the same as changing them on the panel. `PlayerAutoBuyOut` (off) also ticks "Auto Accept Buyouts" on every new operation, which the game doesn't remember.
- Operations ECHO starts: see [ECHO's operations](echo.md#echos-operations).
- Running operations: yours and ECHO's, with their live options. Changes apply to the running operation at once.
