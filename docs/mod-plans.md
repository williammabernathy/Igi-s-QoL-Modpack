# Mod plans

Feasibility and design notes for the mods we want to build. Nothing here is implemented yet. Everything comes from reading the decompiled code of game version 0.8.3.2 (October 2026), so line numbers will drift with game updates. Paths are relative to `decompiled-0.8.3.2/`; [`planning-brief.md`](planning-brief.md) has the 0.8.3.4 locations and what the update changed. Anything not read directly in the code is marked [INFERENCE].

All of these are now features of Igi's QoL Modpack, one plugin. The master plan, build order and open questions are in [`qol-modpack-plan.md`](qol-modpack-plan.md); this file keeps the detailed designs.

| Mod | Verdict | Biggest risk |
|---|---|---|
| Ship loadouts (skills + officers + grunt crew + drones + equipment + weapon rules + optional autopilot) | Medium to hard | Identifying the exact item to re-fit; refreshing the ship you're flying; crew stock varies by station |
| Preferred engagement distance | Easy to medium | Weapon ranges live in Unity asset data, and the game caps the distance at 30 |
| Ship separation (no more pile-ups) | Medium | Tuning, so ships don't wobble between the game's steering and ours |
| Missile evasion | Medium to build, hard to make effective | Missile speed and turn rate are asset data we haven't measured |
| Fleet target priority and conditions | Medium | Ships keep their current target until something resets it |
| Autopilot activity priority list and rotation | Medium | The autopilot has no concept of an activity "ending" |
| Autopilot cargo rules (per item, category and rarity; sell above N) | Medium | Fitting the rule editor into the mod window |

## Ground rules for every mod

### Save safety

Nothing may add enum values or change how vanilla reads a save. Mod data goes into extra JSON keys that vanilla ignores.

The vanilla readers only look up the keys they know (`SpaceShipData.ReadFleetFields`, `SpaceShipData.cs:1439-1452`; `AutopilotSettings.FromJson`, `Source.Player/AutopilotSettings.cs:68-105`). An extra key is ignored on load.

All mod data goes into one `igiQol` key on the player JSON, with per-ship records keyed by `SpaceShipData.guid` (details in `qol-modpack-plan.md`, "Core layer"). That's two patches (`GamePlayer.ToJson` and the load) instead of a pair for every data type.

I'd keep mod data inside the save rather than in a separate file. Autosaves rotate between three slots, and you can load an older save. A separate file keyed by ship would then describe a different state than the save you loaded. Data inside the save always matches it. If you remove the mod, vanilla ignores the key, drops it on its next save, and the game keeps working. You lose the mod settings, nothing else.

None of this has been tested yet. The save-data test in Phase 0 of `qol-modpack-plan.md` checks it before anything else depends on it.

### UI

Every control in the game's UI was placed in the Unity editor and wired to a field (`[SerializeField]`). Mods add controls by copying an existing one at runtime (`Object.Instantiate`) and attaching their own handlers. The layout containers are only visible in the running game, so each UI piece starts with a look in UnityExplorer.

### Performance

Combat code runs every physics step for every ship. New per-ship logic runs on a timer (0.25–0.5 s, like the game's own target and range updates), reuses lists the game already keeps, and doesn't allocate.

### Data we can't get from code

Weapon ranges, missile speed, thrust and turn rates are values typed into Unity prefabs (`AbstractTurret._range`, `Missile` thrust and speed). The code can't tell us whether railguns outrange other weapons. A logging mod or UnityExplorer can read the real numbers at runtime.

## 1. Ship loadouts

A loadout belongs to one ship and bundles its skill preset, officers, grunt crew, drones, equipment, weapon targeting rules, and optionally the autopilot settings. You can only save or apply one while docked, from the hangar.

### How the game works today

- Docking: the station you're at is `GamePlayer.current.currentPointOfInterest` (a `SpaceStation`). The interior screen is `SpaceStationInterior.instance` (`SpaceStationInterior.cs:181-184`). Every `SpaceStation` creates a personal hangar (`SpaceStation.cs:345`) [INFERENCE: nothing in the code hides it at some stations]. So the gate is "docked at a station, hangar screen open" rather than a per-station check. If you know of stations without a hangar in-game, this needs another look.
- Equipment slots: hardpoints (turrets), modules by `EquipmentSlot`, boosters, and drone slots on `AbstractUnitData` (`Source.Data/AbstractUnitData.cs:68,70,120,190-193`). The data-side writers work without the hangar UI: `EquipItem` (`:654`), `EquipModule` (`~:683`), `RemoveModuleOfType` (`~:700`), plus booster equipping. For the ship you're flying, the 3D ship also has to be rebuilt (`InitModules`, `LoadHardpoint`, then `GameplayManager.RefreshShipInSpace`, `GameplayManager.cs:475-486`).
- Item identity: rolled equipment never stacks (`InventoryItemType.CanStackWith`, `Behaviour.Item/InventoryItemType.cs:318-334`). Each turret or module with random rolls is a unique object, saved as its builder JSON (`InventoryItemType.ToJson`, `:655-678`). Plain items stack by name and level.
- Officers: `SpaceShipData.AssignOfficer` and `RemoveCrewMember` (`SpaceShipData.cs:1056-1089`). An officer can only sit on one of your ships (`:1062`, which uses `GamePlayer.GetOfficerShip`). Officers don't have player-allocated skill trees like the captain's [INFERENCE: `OfficerData` has profession-based skill nodes only], so a loadout covers which officers sit on the ship, not their skills.
- Captain skills belong to the captain, not to a ship. The game already has named skill presets (`Source.Personnel/CaptainData.cs` `loadouts`, `LoadoutData.cs`). Each ship remembers a preset name (`SpaceShipData.skillLoadout`, `:93`), and switching ships applies it (`GameplayManager.cs:293-296`, inside `ReinitPlayerSpaceshipRoutine`).
- Grunt crew are counts per crew type with no identity or experience (`Source.Personnel/CrewData.cs:13`). Hiring needs a station personnel center with stock (`Source.Galaxy.POI.Station/PersonnelCenter.cs:31`, `TryPurchaseCrew` `:292-304`) and costs `CrewType.cost` per head. Your reserve (`GamePlayer.unassignedCrew`, capped by `maxReserve`) holds unassigned crew for free. `SpaceShipData.AddCrew` clamps to capacity and returns the overflow (`SpaceShipData.cs:895-915`).
- Autopilot settings are one object for the whole save (`GamePlayer.autopilotSettings`, `GamePlayer.cs:111`). Only the ship you fly runs the autopilot (`IdleManager`).

### Design

A loadout stores:
- a name
- equipment per slot: the item's full saved JSON for rolled gear, name and level for plain items
- drone slots, by drone name
- officer IDs
- grunt crew count per crew type
- weapon targeting rules per hardpoint (see `qol-modpack-plan.md` #4)
- the name of a captain skill preset (it reuses the game's preset system instead of copying skills)
- optionally, a copy of the autopilot settings

There can be several loadouts per ship (for example "Mining" and "Combat" on the same hull).

Skills come first, because they change the caps the later steps check: crew capacity (`SpaceShipData.cs:786`), which drone types are unlocked (`GamePlayer.cs:3321-3330`), and the extra hangar bay slot (`HangarBayModule.cs:154`). `SpaceShipData.CheckDroneLoadout` (`:1709-1725`) also removes drones the active skills don't allow, so fitting drones before skills would lose them.

Applying does as much as it can and reports the rest:

1. Check the ship is fully docked and the skill preset fits your skill points. These two stop the apply; everything after this is best effort.
2. Apply the skill preset (`CaptainData.SetSelectedLoadout(int)`, which also writes the ship's preset name).
3. Unseat officers who aren't in the loadout, then seat the loadout's officers that are free.
4. Grunt crew: move surplus to the reserve, fill shortfalls from the reserve, then hire the rest at this station if it has a personnel center, stock and you have the credits. Crew is never sold (selling refunds 25%, `PersonnelCenter.cs:317-320`). Surplus that doesn't fit in the reserve stays on the ship.
5. Fit drones.
6. Unfit equipment the loadout replaces, back into cargo or the armory using the game's own routing rules, then fit modules, turrets and boosters that are available and fit the slot (`InventoryItemType.CanBeEquippedOn`, `:477`).
7. Restore weapon targeting rules.
8. Copy the autopilot settings, if the loadout has them. ECHO isn't switched on.
9. If this is the ship you're flying, rebuild it in space.

Afterwards a popup lists what couldn't be applied: "Missing: Railgun Mk3 (Epic, lvl 24)", "Officer Vance is on the Kestrel", "No Deckhands for hire here (need 4)", "Slot too small".

Flying a ship applies the autopilot settings of its last-applied loadout, through a postfix on `GamePlayer.SetSpaceShipData` (`GamePlayer.cs:3128`).

Nice-to-have, built last: the loadout list also offers loadouts saved on other ships of the same hull.

Gotchas the code needs to handle:
- `CaptainData.RefundAllSkills()` by default overwrites the active preset with the current tree (`CaptainData.cs:198-213`, `UpdateLoadout` at `:436`). Always call it with `updateLoadout: false`.
- Spending skill points only checks that you have more than zero left (`Source.Personnel/SkillNodeData.cs:33-34`), so a preset can overspend. Check the total first.
- `EquipItem` silently overwrites the first hardpoint when no slot is free (`AbstractUnitData.cs:654`). Pick the slot index explicitly.
- Preset names are matched case-sensitively in one place and case-insensitively in another (`CaptainData.cs:66-83` and `:397`). Store the exact name.

### UI

Add a "Loadouts" button in the hangar's ship panel next to the existing skill preset button (`Behaviour.UI.Spacestation.Location/PersonalHangar.cs`, `skillLoadoutButton` at `:51`, shown at `:646-655`). It opens a list with Save current / Apply / Rename / Delete. Naming reuses the game's text-input popup (`AlertPopup.ShowInput`).

### Verdict

Medium. Every write path exists and works without the UI. The work is in validation, item matching, and rebuilding the active ship.

## 2. Combat movement

### What actually happens today

- Ships don't take damage from ramming. The only collision handler on ships records salvage contact (`Behaviour.Unit/AbstractUnit.cs:3304-3312`). Your fleet ships don't even collide with each other (`SpaceShip.DisableCollisionWith`, `SpaceShip.cs:1162-1176`). The damage you see while ships pile up comes from what pile-ups do to positioning: ships shove each other off their firing positions and sit still while they're shot [INFERENCE].
- During combat, nothing steers ships around each other. The game has an obstacle-avoiding navigator, but it switches itself off whenever a ship is engaging a target (`Behaviour.Unit/ShipNavigator.cs:85-89`). Combat movement is a straight line toward a ring around the target.
- The ring: each ship's `approachDistance` is recalculated every 0.5 s from its turrets' ranges, roughly `clamp(5/6 × turret range, 6, 30)` (`Behaviour.Equipment.Module/AbstractTargetingModule.cs:116-141, 167-183`). A ship moves in when more than 20% outside the ring and backs off when more than 40% inside it (`AbstractUnit.cs:872-893`). The 30 is hard-coded (`AbstractTargetingModule.cs:140`).
- Missiles home in on their target after an ignition delay, and brake when they're more than 10° off course (`Behaviour.Equipment.Turret.Projectile/Missile.cs:112-141`). Nothing detects incoming missiles, and there's no point defence. The only evasion is reactive: after taking damage, a ship swings 45° to either side every 3.5 s for 3 s (`AbstractUnit.cs:895-910`, timer at `:2145`).
- Ships can strafe. The engine applies sideways thrust as well as forward thrust (`EngineThrustersModule.SideThrust`, `:253-278`).

### 2a. Preferred engagement distance (easy to medium)

A per-ship setting with three modes:
- Auto: vanilla behaviour.
- Max range: hold at about 90% of the longest-range weapon's actual range, for railgun boats.
- Fixed: hold at N units.

This is a postfix on `AbstractTargetingModule.UpdateApproachDistance` that replaces `approachDistance` for your ships. The game's existing move-in/back-off logic then holds the new ring. Under autopilot the ship you fly uses the same movement code. During manual flying the setting does nothing, because hold-position and manual thrust bypass it (`AbstractUnit.cs:827-830`).

Open: if railgun range times 5/6 is above 30, the hard cap blocks it, and we'd patch the cap too. That depends on numbers we have to read in-game first.

### 2b. Separation (medium)

A steering term that runs every 0.25–0.5 s for your ships while they're in combat. It pushes away from nearby ships, scaled by how close they are, and blends that with the game's own steering through sideways thrust. It reuses the navigator's neighbour list (`ShipNavigator.navigators`, nearest 16) instead of scanning every ship. It only applies to your ships, so enemy behaviour stays vanilla.

The risk is oscillation: our push-away and the game's "move toward the ring" can fight. That's a tuning problem, and why this comes after 2a.

### 2c. Missile evasion (medium to build, effectiveness unknown)

- A registry of live missiles and their targets, filled by patching `Missile.Initialize` (`Missile.cs:67-89`) and emptied when they're destroyed.
- Every ~0.2 s, each of your ships checks for missiles aimed at it within a threshold distance and makes a sideways dodge perpendicular to the missile's path.
- Missiles brake when they're more than 10° off course, so a late sideways jink might make them overshoot [INFERENCE].

Whether a dodge actually works depends on missile speed and turn rate against ship acceleration, which are asset values. Measure those before building this.

### Persistence

The per-ship distance mode and the separation/evasion toggles go in the ship's record in the mod's save key.

## 3. Fleet target priority and conditions

This builds on the four existing stances. It doesn't add new ones.

### How the game works today

Wingmen run `Source.SpaceShip.Auto/WingmanActions.cs`.
- Aggressive (I'm assuming this is the "attack or hunt anything" option in the UI) has no targeting logic of its own (`:299`). It takes whatever the ship's scanner picked, which is always the nearest valid enemy (`AbstractTargetingModule.UpdateAutoTarget`, `~:195-228`).
- Escort and Defensive copy the flagship's target. Defensive prefers whoever has damaged it the most (`:319-370`).
- Attack orders and targets you set manually always win over automatic picks (`priorityTarget` returns the manual target first, `AbstractTargetingModule.cs:~93-105`).
- Targets are sticky. Once a ship has an automatic target, it doesn't re-pick until that target is gone (`UpdateAutoTarget` returns early, `CombatModule` only rebuilds its candidate list when there's no target).
- Wingmen flee below 10% hull, checked only when they take damage (`WingmanActions.cs:471-481`).

### Design

A per-ship targeting rule used when the ship picks targets on its own (Aggressive, and Defensive when nobody is hitting it). Attack orders and manual targets keep priority.

Rules, each using data the game already tracks:
- Nearest (vanilla)
- Lowest hull % (`fractionHullHP`)
- Biggest threat: who has dealt the most damage to the fleet (`TargetableUnit.targetedBy`)
- Whoever is attacking the flagship (`candidate.targetProvider.priorityTarget == flagship`)
- Fighters and drones first (`isCarrierFighter`, `Drone`)
- By ship size or class (`shipRoleType`)
- Lowest level first

Conditions:
- Only engage within N units of the flagship. This reuses the Defensive leash math, which today only applies to Defensive (`IsPastDefensiveLeash`, `:373-389`).
- Ignore targets above level N.
- Disengage below Y% hull. This is checked on a timer, replacing the hard-coded 10% damage-event check.

Implementation: a prefix on the scanner's nearest-target pick (`AbstractTargetingModule.FindClosestFilteredTarget`) that only applies to your wingmen, plus a re-pick every 1–2 s so the rule takes effect mid-fight. A small bonus for the current target stops ships from flipping between two similar candidates.

### UI

The per-ship settings popup, opened from the settings button on the fleet row's command strip (`qol-modpack-plan.md` #13), holds the rule dropdown and the conditions. The dropdown is copied from the Autopilot tab's (`Behaviour.UI.Side_Menu.SideTabs/Autopilot.cs:233-301`). The fleet row can show the rule next to the stance name it already displays.

### Verdict

Medium. Every value needed to rank targets is already tracked. The stickiness reset and the re-pick cadence take the most care.

## 4. Autopilot

### How the game works today

- `IdleManager.FindActivity` (`Behaviour.Gameplay/IdleManager.cs:168`) re-decides what to do every 2–12 s from the current state. There are no activity objects and no durations.
- Docked, the order is: claim missions, drop cargo, wait for repairs, hire crew, go home if set, then `FindActivityForEquipment` (`:574`). That method picks missions, your preferred activity, dungeons, or "nothing to do".
- An activity ends when there's nothing left to mine, salvage or shoot, or the hold is full (`TriggerInventoryFull`, `:1381-1387`).
- If your fit can't do the preferred activity, `TryPreferredActivity` shows "no suitable loadout" and returns true (`:624-650`). That blocks every fallback, so the ship sits idle.
- Cargo handling on each dock (`TryDropCargoOf`, `:313-361`):
  - Materials (ore, salvage, junk, refined products, trade goods, crystals) go to that station's materials storage.
  - Armory items (ammo, turrets, modules, boosters, drones, torpedoes, jump gate passes, usables, currency, boss signals) go to the armory, which is shared across all stations (`Inventory.global`).
  - Favourites and the ammo your ship uses stay in cargo (`ShouldKeepInCargo`).
- So the autopilot already moves equipment to the armory on its own. What it doesn't do is let you choose.
- Auto-sell only runs when a station's materials storage is full. It then sells the cheapest stack at the item's flat sell value, not the market price (`SellItemFromMaterials`, `:463-474`; `InventoryInteractionManager.SellAmount`, `:987-1008`). It protects favourites and items needed for missions.
- Item categories: `Source.Item/ItemCategory.cs:3-22`. Where each one can go: `InventoryItemType.CanGoInArmory` (`:959`) and `CanGoInMaterials` (`:1010`).

### 4a. Activity priority list and rotation (medium)

You set an ordered list of activities, each with its own layer, for example Mining (Core) → Salvage (Surface) → Combat. ECHO uses the first one the current fit can do and that finds a target; the rest are fallbacks. Details and hooks are in `qol-modpack-plan.md` #16.

Optional switch rules on the same list turn it into a rotation:
- after N cargo drops,
- after M minutes.

The switch happens in `FindActivityForEquipment` while docked, after cargo is dropped and repairs are done, so a switch never strands cargo or skips a repair.

Rotating with a different fit per activity means the autopilot would apply a ship loadout at the station before each switch. That's hard, and it waits until loadouts (section 1) work.

### 4b + 4c. Cargo rules (medium)

Selling and stashing are the same decision, "what happens to this item when I dock?", so they're one setting: the dock action on the item rules described in `qol-modpack-plan.md` #3 and #15. Rules match a specific item, a category and rarity, or a category. Actions are Default, Keep, Stash, Sell, and Sell above N.

Example: trade goods set to Sell, ore set to Sell above 5000, Common ammo set to Sell, turrets set to Stash, torpedoes set to Keep.

The rules plug into `TryDropCargoOf` before the vanilla routing. Favourites, mission items, and the ammo your ship uses are always protected, as they are today. Market-price selling (through the station economy, `LocalEconomy.Trade`) is possible as a later option but changes station prices, so it isn't the default.

### UI

The activity list and switch rules go on the ECHO tab of the mod window, and the item rules on its Item rules tab (`qol-modpack-plan.md`, "Core layer"). The Autopilot tab's existing activity dropdown keeps working and edits the first entry of the list.

### Persistence

Stored in the mod's save key. Categories are stored by name, so no enum changes.

## Parked: transparent and Performant window modes on Linux

What you see on Wayland: both modes keep the window on top, but it's black instead of transparent.

What the modes do (`Behaviour.Transparency/TransparentWindow.cs`):
- Transparent clears the background to RGB(5,13,9) and tells Windows to make exactly that colour see-through (`SetLayeredWindowAttributes` with a colour key).
- Performant clears to alpha 0 and relies on the Windows desktop compositor (DWM) to use the alpha channel (`DwmExtendFrameIntoClientArea`).
- Both switch click-through on and off every frame and set always-on-top.

Both background colours are near-black, so a black window is what you'd get if Wine on XWayland applies neither the colour key nor the alpha channel [INFERENCE]. A mod can't add that compositing. The one workaround idea is to shrink the real window to the game's HUD strip and pin it to the screen edge (no transparency needed), but the UI assumes a full-screen canvas, so it's unproven.

## Build order and open decisions

Both moved to [`qol-modpack-plan.md`](qol-modpack-plan.md).
