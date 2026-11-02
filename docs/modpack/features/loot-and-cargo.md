# Loot and cargo

What your ships pick up, where it goes, and finding what's left at a location. Settings keys are listed in [settings.md](../settings.md).

## Item rules

![The Item rules tab](../../images/tab-item-rules.jpg)

The Item rules tab holds one table of rules, saved with the game. Each rule has two parts:

- Pickup limit: your ships stop auto-tractoring the item once they carry this many. 0 means no limit, -1 means never.
- At dock: what ECHO (and, if you want, [Station services](stations.md#station-services)) does with the item at a station. Game default, Keep aboard, Sell, or Sell above N: deposit until the station storage holds N, sell the rest. The part above N sells even when the storage is full.

The most specific rule wins: a specific item (optionally at one item level), then category + rarity, then category + rarity and below, then category, then any category + rarity and below, then the default. A "Turret / Standard" rule with a pickup limit of -1, for example, makes your ships and ECHO's loot-before-leaving skip common turrets.

A "rarity and below" rule covers the chosen rarity and every rarity under it, so "Turret / High Grade and below: Sell" replaces three rows (Standard, Enhanced, High Grade). Pick "Any category" to cover every item up to that rarity. When several of these rules cover an item, the one with the lowest rarity wins: with "Standard and below: Sell" and "Exotic and below: Keep aboard", Standard items are sold and Enhanced to Exotic items stay aboard. A category's own rules win over an "Any category" rule.

The pickup limit is applied by [Loot pickup limits](#loot-pickup-limits) and the dock action by [ECHO cargo rules](echo.md#echo-cargo-rules) and Station services. The same tab carries both features' settings.

### Starting rules for new saves

A save without rules is seeded once from `BepInEx/config/IgiQoL/item-rules.json`, if that file exists. After that the save's own copy is used, so editing the file doesn't change saves that already have rules. The format is the one the mod writes into the save:

```json
{
  "default": { "tractorCap": 0, "dockAction": "Default", "keepAmount": 0 },
  "categories": {
    "Ore": { "tractorCap": 500, "dockAction": "SellAbove", "keepAmount": 2000 }
  },
  "categoryRarity": {
    "Turret/Standard": { "tractorCap": -1 }
  },
  "categoryUpTo": {
    "Module/Enhanced": { "dockAction": "Sell" }
  },
  "rarityUpTo": {
    "Standard": { "tractorCap": -1 }
  },
  "items": {
    "CanisterOxide": { "dockAction": "Keep" },
    "ArtifactAnalysis@3": { "tractorCap": 2 }
  }
}
```

- `tractorCap`: the pickup limit, -1 or more.
- `dockAction`: `Default`, `Keep`, `Sell` or `SellAbove`.
- `keepAmount`: N for `SellAbove`.
- Categories: `Ore`, `Ammo`, `Turret`, `Module`, `Booster`, `Junk`, `Drone`, `RefinedProduct`, `Torpedo`, `JumpgatePass`, `TradeGoods`, `Usable`, `DefensiveTurret`, `Salvage`, `Currency`, `Crystal`, `Data`, `Blueprint`, `BossSignal`, `SignalFragment`.
- Rarities: `Standard`, `Enhanced`, `HighGrade`, `Exotic`, `Legendary`. A `categoryUpTo` or `rarityUpTo` key names the highest rarity the rule covers.
- Item keys are the game's item identifier (`CanisterOxide`, not the display name "Oxide Canister"), with `@level` for one item level. `[Dev] DumpGameData` lists every identifier in `InventoryItemType.allItems.txt` ([dev-harness.md](../dev-harness.md#data-dumps)).

An entry with an unknown category or rarity is skipped with a warning in `LogOutput.log`. Fields you leave out take the vanilla value (0, `Default`, 0).

## Loot pickup limits

Applies the Item rules pickup limits: your ships stop pulling in an item automatically once they carry the limit, or never pull it in with -1. `MinCreditsPerM3` (0 = off, set on the Item rules tab) also skips loot whose sell value per m3 of hold space is below that many credits, so cheap bulky items don't fill the hold. Both apply to your ships' auto-tractor and to ECHO's loot-before-leaving.

Items your missions need and ammo a ship fires are exempt from -1 and from the value floor (a numeric limit still applies to them), and clicking an item yourself always picks it up.

Loot none of your ships at the location will pick up says why, on its tooltip in space and on its [Scanner](#scanner) row: "limit 50 reached", "never pick up" or "below 12 cr/m3". When several ships with a tractor beam are held back for different reasons, the first one's reason is shown, starting with the ship you fly. The reason only covers these filters; loot the game itself won't tractor (your own jettisoned cargo, for example) has no line.

## Shared fleet holds

`Overflow` (off): when a ship's tractor beam reaches for loot its hold can't take whole, the ship moves cargo to your other ships at the same location (the flagship first, then the one with the most room), biggest stacks first, until the stack plus a quarter of its hold is free. It does this at most once per 2 s per ship. Favourites, ammo the ship fires, the usables the game keeps aboard, warp fuel, items your missions need and pinned stacks stay where they are.

Without it, a single full ship sets ECHO's "inventory full" and the whole fleet flies home even when the others have room. With it, ECHO heads home once every ship is full.

## Pinned cargo

The gold crown button of the fleet cargo window moves a fleet ship's whole hold to the flagship, including the ammo that ship fires. Pinned item types stay aboard; everything else still moves in one click. Two ways to pin:

- Hover over a stack in one of your ships' cargo (a fleet ship, or one parked in the hangar; the pin applies while the ship is in your fleet) and press `PinKey` (P), as F marks a favourite. Pinned stacks get a gold lock in the bottom left corner, and the item's tooltip says whether it's pinned.
- In the ship's [settings popup](interface.md#ship-settings-popup): "Keep the ammo this ship fires on board" keeps whatever ammo and torpedoes its current fit uses, and "Pin an item" pins or unpins an item aboard (or a pin for an item that's no longer aboard).

A pin covers every stack and level of the item type, on that ship only. Pins are saved with the ship's other settings, so a [loadout](stations.md#ship-loadouts) carries them; after a refit to another ammo type, a pin on the old ammo stays until you remove it, while the ammo switch follows the fit. The "flagship full" notice only counts stacks the button tried to move. Shared fleet holds leave pinned stacks aboard too; ECHO's unloading, the fleet auto-deposit and the Deposit button ignore pins.

## Scanner

![The Scanner tab at an asteroid field](../../images/tab-scanner.jpg)

The Scanner tab lists everything at your location you can board, salvage, tractor or mine, nearest first, refreshed every second:

- Boardable ships, wrecks and loot, each with a Board, Salvage or Tractor button.
- Where there are asteroids, "Asteroid ores": one row per ore and layer with the asteroid count and the nearest distance. Ores on your [Preferred ores](echo.md#preferred-ores) list are marked "preferred". The Mine button sends your mining ships to the nearest asteroid of that ore, as clicking it would. Core ores appear once you have the Basic Scanning mining skill and ore amounts with Advanced Scanning, the same as the game's asteroid tooltips.

`HudCounter` puts a one-line count above the Dock button (above the QoL button when that sits there too). Click it to open the Scanner tab, or, while the Scanner window below is open, to bring that window forward and flash it.

![The counter above the QoL and Dock buttons](../../images/scanner-counter.jpg)

`MarkBoardable`, `MarkWrecks`, `MarkLoot` and `MarkOres` (all on, also switchable on the tab) put markers at the screen edge for the 8 nearest off-screen contacts of each kind, with the distance under the icon. Loot markers are tinted by rarity; empty wrecks and depleted ships are left out. Ore markers only point at asteroids with an ore from the Preferred ores list, tinted by that ore's rarity, so nothing shows while the list is empty. Clicking a marker boards, salvages, tractors or mines its target.

### Scanner window

The Pop out button at the top of the Scanner tab opens the Scanner as a [window of its own](interface.md#pop-out-windows) that stays on screen while you play. Dock back on the tab closes it; Dock on the window's header closes it and opens the tab. By default it is always shown; its Options button can limit it to when there is something here to board, salvage or pick up, the same count the HUD counter shows.

- The map draws the location to scale, with your flagship as a white dot, your fleet in blue and enemies in red. Boardable ships, wrecks, loot and asteroids with ore use the same icons as the edge markers, and loot and ore are tinted by rarity. The thin rectangle is what the game camera shows now.
- Dots follow their targets four times a second, and the set of ships and contacts is read once a second.
- Under the map, the Contacts header's +/- button folds the list away so the map takes the whole window; that choice is remembered. The list is the tab's list without its settings and help text.
- Click a dot to select its row, scrolled into view and highlighted, or click a row to ring its dot. One contact is selected at a time. A loot row rings every stack of that item, and an ore row rings every asteroid listed under that ore (an asteroid with surface ore is listed under its surface ore; a core-ore row rings the asteroids whose only listed ore it is).
- Board, Salvage, Tractor and Mine work as in the tab, but leave the window and the QoL window as they are.
