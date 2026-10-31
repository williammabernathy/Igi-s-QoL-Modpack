# Vanguard Galaxy mods

Personal modding workspace for Vanguard Galaxy (Steam app 3471800), played on Linux through Proton.

Game facts that decide the tooling:

| | |
|---|---|
| Install | `/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy` |
| Build | Windows x64, runs under Proton 11.0 |
| Engine | Unity 6000.4.7f1 (Unity 6.4) |
| Scripting backend | Mono (`VanguardGalaxy_Data/Managed/Assembly-CSharp.dll` exists), so BepInEx 5 works. IL2CPP games need BepInEx 6; this one doesn't. |
| Game version | 0.8.3.4, open-beta branch (from the save `.meta` files). `decompiled/` matches it; `decompiled-0.8.3.2/` keeps the previous build for diffs. |
| Mod loader installed | BepInEx 5.4.23.6, `win_x64` build |

There is no official modding API. Every mod is a BepInEx plugin that uses Harmony to patch the game's C# code at runtime. The "documentation" is the game's own code, decompiled into `decompiled/` (gitignored, reference only).

## How BepInEx is hooked in (done, verified 2026-10-27)

BepInEx is copied into the game folder (`winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `BepInEx/`). Proton normally loads Wine's built-in `winhttp.dll` instead of BepInEx's, so the game's Wine prefix has a DLL override that prefers the native one:

- File: `/mnt/data/SteamLibrary/steamapps/compatdata/3471800/pfx/user.reg`
- Section `[Software\\Wine\\DllOverrides]`, line `"winhttp"="native,builtin"`
- The original file is saved next to it as `user.reg.pre-bepinex.bak`.

This replaces the usual Steam launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%`, which does the same thing. Either one works; you don't need both. If you ever delete the Proton prefix (or Steam recreates it), the override is gone and BepInEx silently stops loading. Re-add the line above, or use the launch option instead.

Check that BepInEx ran:

```fish
cat "/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy/BepInEx/LogOutput.log"
```

The first verified run showed `Chainloader started`, `Loading [Hello Vanguard 0.1.0]`, then the test plugin reporting 0 items at plugin load and 393 items after `InventoryItemType.LoadAll` finished. If `LogOutput.log` doesn't get rewritten when you start the game, the override isn't active.

Optional: to get a live log window, set `[Logging.Console] Enabled = true` in `BepInEx/config/BepInEx.cfg`.

## Using mods

- Mod DLLs go in `BepInEx/plugins/` (a subfolder per mod is fine).
- Logs: `BepInEx/LogOutput.log` (BepInEx and plugins) and Unity's own log at
  `/mnt/data/SteamLibrary/steamapps/compatdata/3471800/pfx/drive_c/users/steamuser/AppData/LocalLow/Bat Roost Games/VanguardGalaxy/Player.log`.
- Per-mod settings appear in `BepInEx/config/<plugin guid>.cfg` after the first run.
- Play unmodded: set `enabled = false` in `doorstop_config.ini` in the game folder. BepInEx stays on disk but doesn't load.
- Uninstall: delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and `BepInEx/` from the game folder. Steam > Properties > Installed Files > Verify also restores a clean install, but it won't delete those extra files.
- Game updates can break mods that patch methods the developers renamed or changed. A broken patch shows up as an error in `LogOutput.log`; the game itself usually still starts.

Back up your saves before trying mods that touch save data. They live at
`.../compatdata/3471800/pfx/drive_c/users/steamuser/AppData/LocalLow/Bat Roost Games/VanguardGalaxy/Saves/`.
Each save is a `.save` file (gzip-compressed JSON; `zcat autosave-0.save | jq .` works) plus a `.meta` file holding the game version. There are three rotating autosaves, written every 300 seconds.

## Building mods

Installed for this:

- .NET 8 SDK in `~/.dotnet` (user-local; the system `/usr/bin/dotnet` has no SDK).
- `ilspycmd` 9.1 (decompiler) as a .NET global tool in `~/.dotnet/tools`.

Put them on your fish PATH once (prepends, so it wins over `/usr/bin/dotnet`):

```fish
set -Ux DOTNET_ROOT $HOME/.dotnet
fish_add_path $HOME/.dotnet $HOME/.dotnet/tools
```

Build everything and deploy into the game:

```fish
dotnet build -c Release
```

`Directory.Build.props` holds the shared setup for every project under `src/`:

- References `BepInEx.dll`, `0Harmony.dll`, the game's `Assembly-CSharp.dll`, and the Unity DLLs straight from the game folder. Override the path with `-p:GameDir=...`.
- Publicizes `Assembly-CSharp` (`BepInEx.AssemblyPublicizer.MSBuild`), so you can use the game's private fields and methods directly instead of reflection.
- Targets `net472`, which matches the game's Mono profile.
- After each build, copies the DLL to `BepInEx/plugins/<AssemblyName>/`. Turn this off with `-p:DeployToGame=false`.

New mod in this repo: copy `src/HelloVanguard`, rename the folder, `.csproj`, `AssemblyName`, and plugin GUID, then `dotnet sln add src/<Name>/<Name>.csproj`. Restart the game to load a rebuilt DLL; BepInEx 5 has no hot reload.

New mod in its own repo: `templates/VgMod` is a `dotnet new` template with the same build setup, a plugin that patches each patch class separately (a class broken by a game update is skipped and logged), and a trimmed dev harness (auto-load a save, run checks, log `CHECK` lines, quit). Its README covers the rest.

```fish
dotnet new install templates/VgMod
dotnet new vgmod -n MyMod --Author yourname --GameDir "/path/to/Vanguard Galaxy"
```

### After a game update

Every build first copies the game DLLs it compiles against (`Assembly-CSharp`, `UnityEngine*`, `Unity.InputSystem`, `Unity.TextMeshPro`, about 17 MB) into `game-versions/<game version>-<hash>/`, so the version you last built against is still there after Steam replaces it. The copy happens before compiling, so a build that fails against the new game still saves the new version. `game-versions/` is gitignored.

`tools/VersionDiff` compares two of those versions:

```fish
dotnet run --project tools/VersionDiff -- patches --out patch-report.md
```

With no options it compares the newest snapshot against the installed game, or against the second newest snapshot once you've rebuilt, using `src/IgiQoL/bin/Release/IgiQoL.dll` (`--old`, `--new` and `--mod` override each). For every method the mod patches it reports `unchanged`, `body changed` (with a diff of the decompiled method), `signature changed`, `missing` or `ambiguous`. It also reports:
- patch parameters whose names no longer match the target, including `___field` injections
- members the compiled DLL uses that are gone from the new game
- string lookups (`AccessTools.Method(typeof(X), "name")`, `FieldRefAccess`, `Traverse`) that no longer resolve

It exits 1 when any of those need attention. With only one version on disk it lists the targets without diffs. [`tools/VersionDiff/README.md`](tools/VersionDiff/README.md) has the full reference: options and defaults, every status and what to do about it, the report layout, exit codes and limits.

Then refresh the decompiled source, keeping the old one for wider diffs:

```fish
mv decompiled decompiled-<old version>
ilspycmd -p -o decompiled "/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy/VanguardGalaxy_Data/Managed/Assembly-CSharp.dll"
```

The folder layout follows the namespaces. The game uses both `Behaviour.*` and `Behavior.*`, so search for both spellings.

To see what an update changed in the game's data, you need a dump from each version. `[Dev] DumpGameData` (see the `[Dev]` section below) writes one to `BepInEx/config/IgiQoL/dumps/<game version>/`; the 0.8.3.4 dump is already there. Once the game has updated, dump again and compare:

```fish
set D "/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy/BepInEx/config/IgiQoL/dumps"
dotnet run --project tools/VersionDiff -- data $D/0.8.3.4 $D/<new version> --out data-changes.md
```

That lists added and removed items, ships, recipes and so on, and every changed value as `path: old → new`.

## CI and releases

`.gitlab-ci.yml` runs on every push to `main` and `development`:

1. `build` compiles the solution against the installed game and packs `artifacts/IgiQoL-<version>.zip`, laid out as `BepInEx/plugins/IgiQoL/IgiQoL.dll` (+ `.pdb`) so it extracts straight into the game folder.
2. `patch-check` runs `VersionDiff patches` in manifest mode on the built DLL. It fails if a Harmony target, a game member the DLL uses, or an `AccessTools`/`Traverse` string lookup doesn't resolve in the installed game. The report is a job artifact ("Patch report").
3. On `main` only, and only when `src/IgiQoL/` or `Directory.Build.props` changed in the push, `publish` uploads the zip to the project's generic package registry, and `release` creates the tag `v<version>` and a GitLab release linking the zip. The release notes list the commit subjects since the previous tag, without merge commits.

The version is `<Version>` in `src/IgiQoL/IgiQoL.csproj` for major and minor. The patch is one above the newest `vMAJOR.MINOR.*` tag, or `<Version>`'s own patch for the first release of that line. To start 0.2, set `<Version>0.2.0</Version>`. `[BepInPlugin]` and the startup log line read the version through the generated `PluginInfo.Version`, so local builds report `<Version>` as written. `dotnet build -c Release -p:ReleaseZip=true` builds the same zip locally.

The game's DLLs are copyrighted and can't go to GitLab, so jobs run only on a project runner on this PC, tagged `vanguard-galaxy`. Pipelines wait as pending while it is off.

- Binary: `~/.local/bin/gitlab-runner`, config `~/.gitlab-runner/config.toml` (holds the runner token, keep it private).
- Service: `systemctl --user {status,restart} gitlab-runner`, a user unit in `~/.config/systemd/user/`. It starts at login, not at boot.
- Executor: docker, through rootless podman's socket (`podman.socket` user unit, `/run/user/1000/podman/podman.sock`). Job containers get the game folder read-only at `/game` (`volumes` in the config).
- After moving the game, update the `volumes` path in the config. After losing the config, create a new runner under Settings > CI/CD > Runners with the tag `vanguard-galaxy` and "Run untagged jobs" off, then `gitlab-runner register` with the same executor, host and volume options.

The in-game self-checks (`[Dev]` harness) still need the game running and stay manual.

## Igi's QoL Modpack (`src/IgiQoL`)

One plugin (`igi.qol`, deployed to `BepInEx/plugins/IgiQoL/`). Every feature has a section in `BepInEx/config/igi.qol.cfg` with an `Enabled` switch; all defaults behave like vanilla except the always-visible HUD additions. `SESSION_LOG.md` (local only, not in git) records what each work session changed; read it before starting new work.

| Section | What it does |
|---|---|
| `TractorCaps` | Stops auto-tractoring an item once the ship holds the pickup limit set on the Item rules tab (saved per save game; a save without rules is seeded once from `BepInEx/config/IgiQoL/item-rules.json`). A limit of -1 means never: your ships and ECHO's loot-before-leaving leave the item alone (for example a "Turret / Standard" rule at -1 skips common turrets). `MinCreditsPerM3` (0 = off, on the Item rules tab): loot whose sell value per m3 is below this isn't pulled in either, by your ships or ECHO's loot-before-leaving. Items your missions need and ammo the ship uses are exempt from both. Manual tractoring is unaffected. |
| `JumpsRemaining` | Adds "Jumps: N" under the distance line of the travel panel. |
| `MissionDistance` | How far away a mission's next location is (the game's own route search, counting jump gates). Mission board and missions window rows get a jump gate icon with the number of jumps when the location is in another system (tooltip: "Zorix, 38 jumps"); the mission details and the HUD mission tracker add it after the location: "Location: Fractured Crossroads (Red Gate, 1 jump)", "(this system)" or "(here)". Board missions have so far always been in the station's own system, so the row tags mostly show up in the missions window, on missions you took elsewhere and on story missions. |
| `FleetVitals` | Each fleet row shows three solid bars: shield, hull (the game's bar, restyled), armor. A ship without shields or armor leaves that slot blank. `BarHeight` (2-12 px, default 4 = the game's hull bar) sets the height of all three and applies at once; each row grows by three times the difference. |
| `MissileRetarget` | `Mode = Off / WeaponTarget / Nearest`: what your missiles do when their target dies. |
| `DronesOnArrival` | `Default = true` deploys docked drones when a ship of yours arrives somewhere other than a station. |
| `EchoGates` | ECHO thresholds: loot before leaving, raid reputation, dungeon crew minimum, boarding fraction, crew-hire fraction, `FastUnloading` (whole stack per drop, clamped to free storage), `QuickStationSteps` (off): docked at a friendly station, ECHO's next decision comes 2 s (the game's own minimum decision time) after docking, after any decision that changed its missions, flagship crew, flagship cargo or credits (turn-in, hire, restock, unload, sell, take a mission), and once the flagship's hull and armour are full after it waited for repairs, instead of its full 8-12 s. Decisions that change nothing (no mission on the board) keep the game's time, so ECHO doesn't make more decisions per visit, only spends less time between them. `RecruitPrisoners` (off): when ECHO tops up the flagship's crew at a station, willing prisoners of the missing types are recruited first (the flagship's brig, then fleet ships' brigs, then the station brig) at 40% of the crew type's price, as on the brig screens; the personnel center hires only what's left. Needs ECHO's "buy crew" setting on. `HireOnlyWhatFits` (on; the game behaves as off): ECHO takes from the reserve and pays the personnel center only for crew that fit aboard. The game asks for a type's whole shortfall against the crew ratios, charges for all of it and drops what doesn't fit, which happens whenever other crew types fill the ship. `RebalanceCrew` (off): before that top-up, ECHO moves crew above their share in the crew ratios to the crew reserve, as many as the short types can get right away (reserve, willing prisoners, personnel center within your credits) minus the free places; types with no share set are never moved. |
| `EchoMissions` | On the ECHO tab. `MissionsAtOnce` (1-5, default 1 = the game's one mission per trip): ECHO takes that many missions from the board in one visit, flies from each finished mission location to the nearest open one in the system, and turns them all in on one trip home, in one decision instead of one per 10 s. It still goes home first when the hold is full, it has no ammo it could restock, crew is short, or the flagship's hull or armour is below half. One switch per mission kind ECHO can be given (all on = the game): Eliminate, Reclaim Salvage Rights, Reclaim Asteroid Belt, Clear Pirate Base, Capture Enemy Vessel, Recover (item), Salvage (material), Mine (ore), Collect (ore), Support (ore) Mining; courier missions stay on the Autopilot tab's own switch. A kind that is off is never offered to ECHO; with every kind off ECHO skips missions and goes on to its activities. |
| `EchoOperations` | Stealth mode, ammo type and auto buy-out for operations ECHO starts itself. |
| `Window` | The mod window, opened with `Hotkey` (default F6), the "QoL" button above the HUD's Dock button in space, or the "QoL" tab above the station's Airlock tab while docked. The HUD button hides whenever the game hides its HUD (docked, dialogues), so it never covers station screens. Tabs: ECHO, Operations, Notes, Settings. Each setting lives in one place: the ECHO tab has ECHO's decision settings, the Operations tab has your and ECHO's operation choices, and Settings has every feature's on/off switch plus the settings that have no tab of their own. Every row has a plain-language name with an explanation on hover; changes write `igi.qol.cfg` at once, and on/off switches apply at the next game start. The window can be dragged by its header and remembers its position and last tab. |
| `FleetStrip` | A second line of tiles under each fleet row: stop, hold, regroup, the four stances (fleet ships only), auto-tractor, deploy/recall drones, the ship's cargo window, and the per-ship settings popup (missile retarget, drones on arrival, engagement distance). Clicking a tile doesn't select the row. |
| `EngagementDistance` | Per-ship approach distance from the settings popup: Auto (vanilla), Max range (the shortest range among weapons that can hit the target) or Fixed (1 to 30 units). |
| `EchoPriority` | ECHO tab: an ordered activity list ECHO tries while idle. The first entry is also the game's preferred activity and follows the Autopilot tab's dropdown. An empty list is vanilla. |
| `Operations` | Operations tab: your stealth/ammo/auto-move defaults for new operations, `PlayerAutoBuyOut` (pre-ticks auto buy-out), ECHO's operation settings, and the running operations with live options. |
| `Notes` | Notes tab: named note tabs stored in `BepInEx/config/IgiQoL/notes.json`, edited as plain text and viewed with `#` headings, `**bold**`, `*italic*`, `- ` bullets and clickable links. |
| `TextScale` | `Percent` (50-200, default 100, applies at once): font size for all game and mod text, separate from the game's own Scale setting, which resizes panels and text together. Panels keep their size, so larger text can be cut off with "..." or wrap inside narrow labels (cargo stack counts wrap at 130%). Labels keep the lines they show at 100% and spill past their box instead of disappearing. |
| `TargetPriority` | Which enemy your ships pick on their own: `Rule` (Nearest = vanilla, lowest/highest hull %, most damage to your fleet, attacking your flagship, smallest/largest hull class, lowest/highest level), `RepickSeconds` (0 = vanilla stickiness; above 0 the ship switches when another enemy is clearly better), `FinishBelowPercent` (never switch away from a target under that hull %). The per-ship popup has per-ship overrides, a level cap, a flagship leash for fleet ships, and one rule per combat weapon (hardpoint): a weapon with a rule shoots the best enemy by that rule among those it can hit and has in range, and follows the ship's target otherwise. Clicks on the flagship and attack orders still win; with a non-Nearest rule, ships stop jumping to whoever just hit a nearby ally. |
| `DroneModes` | Drone behaviours beyond the game's Free roam / Follow ship: Follow (no target: come back past N units), Guard (only take targets whose hull is within N units of the ship, stay within N; combat and painter drones also drop targets that drift past 1.25 × N), Ship's target only (combat and painter drones attack the ship's target, otherwise follow). Mining and salvage drones in Ship's target join the ship's asteroid or wreck when they can (a driller only if the asteroid has room), else take one within N; in Guard they join it when nothing free is within N. Repair drones in Guard or Ship's target fix the ship first when it is damaged, else anything within N. Mining, salvage and repair drones get these at their next pick, so nothing is pulled off a job. "Drones stay with" in the popup makes "the ship" another ship of your fleet (falls back to the drones' own ship while that one is elsewhere). The HUD drone button cycles Free roam → Follow ship → Follow → Guard → Ship's target for the flagship, with its label showing the mode; every ship has the mode, distance and anchor in its popup. The game's saved mode stays Free roam / Follow ship, so saves load without the mod. |
| `Separation` | `Default` (off), `OtherShips` (off) and `Gap` (0.5-10 units, default 2): while a ship fights (has a target, no move order, not holding position), its destination is pushed away from every ship whose hull outline is closer than the gap. Your ships follow `Default` or their per-ship popup setting; `OtherShips` does the same for enemy and allied ships that aren't yours. The push is re-measured every 0.25 s and added on top of the game's own destination, so it doesn't build up. Vanilla switches the obstacle-avoiding navigator off in combat, which is why ships stack up. Measured in spawned 12-enemy, 4-ally brawls (4 rounds of 33 s each way): with `Default` and `OtherShips` on, overlapping ship pairs dropped by 42%, and the share of turrets with their target in range dropped from 78% to 71%, because ships that slide apart sometimes slide out of reach. |
| `Contacts` | Contacts tab: boardable ships, wrecks and loot at your location, nearest first, with Board / Salvage / Tractor buttons, and where there are asteroids an "Asteroid ores" list: one row per ore and layer (asteroid count, nearest distance, ores listed in `OrePreference` marked "preferred") with a Mine button that sends your mining ships to the nearest asteroid of that ore, as clicking it would. Core ores appear with the Basic Scanning mining skill and ore amounts with Advanced Scanning, as on the game's asteroid tooltips. `HudCounter` shows what's left above the QoL button. `MarkBoardable`, `MarkWrecks`, `MarkLoot`, `MarkOres` (all on, also switchable on the tab): markers at the screen edge for the 8 nearest off-screen contacts of each kind, with the distance under the icon (loot tinted by rarity, empty wrecks and depleted ships left out; ore markers only for asteroids with an ore from the Preferred ores list, tinted by that ore's rarity). Clicking a marker boards, salvages, tractors or mines it. |
| `CraftRollup` | Forge recipes you can't craft yet get a "Craft missing parts" button with an amount field in the "insufficient materials" strip. The strip shows the number of parts, the forge time (summed, as if run one after another), the ore to refine and its refinery time, the fees and, in orange, whatever is still missing; hovering lists every job. When the station has a refinery, missing canisters are covered by refining ore from its materials storage (and your cargo when docked there): per canister the ore with the best yield, counting by-products and the refinery's carried-over fractions; favourite ore, ore your missions need and ore the game won't auto-refine are left alone. Starting it starts each refinery job and queues each part job as soon as its ingredients (or ore), a free slot and the credits are there (checked every second where you are, every 10 s at other stations, also while you're away), then the recipe itself. Parts and roll-up canisters go into the station's materials storage even with "deposit in cargo" on. Running roll-ups are saved; the same button stops one (jobs already queued keep running). A notification says when the recipe is queued. |
| `Loadouts` | Loadouts tab (also the "L" button left of the hangar's skill-preset "S" button): save a ship's whole fit under a name and apply it later while docked at a station with a hangar. A loadout holds the exact weapons, modules and boosters (matched by their rolled stats, so socketing an aspect or a booster's unlock progress doesn't break the match), drones, officers, crew per type, the skill preset name, the ship's QoL settings (targeting, drones, distance) and, when ticked, ECHO's settings. Applying runs skills, equipment, officers, crew, drones, then settings: equipment comes from the armory and the ship's cargo, replaced pieces go to the armory, a piece that's gone is replaced by the best of the same type and rarity (flagged) or the fitted one stays, surplus crew goes to your reserve and shortfalls come from the reserve or are hired here. A report lists what was done and what wasn't. Loadouts saved on your other ships of the same hull can be applied or copied. Flying a ship restores the ECHO settings of the loadout last applied to it. |
| `CombatLog` | Combat tab: every hit between your side (ships, drones, fighters) and anyone else, as it landed (shield and armour absorbed plus hull lost, after resistances). Views: dealt by weapon (per fitted piece and slot; drones per drone type), by damage type, by your ship (drones count for their ship), by enemy type; taken by enemy weapon type, by damage type, by enemy type, by your ship. Each row: damage, share, per second of fighting, hits, crit rate, biggest hit, shield/armour/hull split, kills. Scope: the current or last fight, the 9 before it, or the whole session. `FightGapSeconds` (5-120, default 15) quiet seconds end a fight; `WriteFile` (off) appends each finished fight's tables to `BepInEx/config/IgiQoL/combat-log.txt`. Nothing is saved with the game. |
| `EventLog` | Log tab: the game's pop-up notifications, the event lines above the HUD (reputation, forge and refinery jobs finished, saves), ECHO's status whenever it changes (its "Calculating..." between decisions left out), ECHO on/off, arrivals of the ship you fly, one line per finished operation (outcome, defenders killed and captured, crew killed and wounded, items found and, separately, credits found, from the game's own operation log), and items your ships pick up (pickups of the same item within a minute share a line), each with your computer's clock time, newest first; a notice repeated within 10 s counts up "(x3)". A line at the top sums the session's pickups with their sell value (hover for the list). Filter by kind, Clear. Up to 1000 lines, kept until you quit; nothing goes into the save. |
| `FleetHolds` | `Overflow` (off): when one of your ships' tractor beams reaches for loot its hold can't take whole, the ship moves cargo to your other ships at the same location (the flagship first, then the one with the most room), biggest stacks first, until the stack plus a quarter of its hold is free; at most once per 2 s per ship. Favourites, ammo the ship uses, usable items, warp fuel and items your missions need stay put. Without it, any one full ship sets ECHO's "inventory full" and the whole fleet flies home even when the others have room. |
| `Guide` | Guide tab, read from the game's data and formulas: how a hit is reduced on its way to the hull (roll, crits, type bonuses, the 80% resistance cap, shields, armour, paint strip), every damage type with the turrets that deal it, every armour type's resistance and weakness (in this build one: Armor Plating, 15% resist, takes +25% from Corrosion), and the flown ship's tonnage, mass, acceleration, top speed, sidestep, crit chance and per-type damage reduction, plus the game's own descriptions of those stats. |
| `EchoKit` | On the ECHO tab: a rule per flagship ability and per deployable type (from the flagship's cargo or the armory): Off, In combat when ready, Shields / Armour / Hull below N %, Targeted by at least N enemies, or Whenever ready. Every rule but Whenever ready only fires in a fight (damage in the last 10 s or a target). Untargeted abilities fire as they are; hostile-targeted ones go on ECHO's target, friendly-targeted ones on your most damaged ship, asteroid ones (Crackshot Drone) on the asteroid the flagship mines, wreck ones (LB-RTR, SF-CRK) on the nearest wreck whose cargo box or credits are still there, the same test the HUD uses; drone-targeted abilities aren't automated. Toggle abilities are switched on while the rule holds and off again only if the rule switched them on. Deployables (turrets, shield pylons, decoys, mines) are dropped from the flagship's cargo, at most one per 2 s and only when the game's deploy cooldown allows. `PickUpTurrets` (on) tractors those back 5 s after the fight; `AlsoWithoutEcho` (off) applies the rules while you fly. All rules start off. |
| `StationServices` | Five buttons in the airlock's button row (after the game's own), each shown only while it has work: "Repair fleet N cr" starts the hangar's paid repair (about 20 s, at the hangar's own price and reputation discount) on every damaged ship in your fleet; "Restock ammo" tops up every fleet ship's ammo to the "ammo minutes" target of ECHO's Autopilot tab, from the armory first and then bought from the station's credit shops; "Sell by rules +N cr" applies the Item rules tab's Sell and Sell above N dock actions to your fleet's cargo; "Replace crew" puts back the crew each ship of yours lost to hull hits (every hit of 2% hull or more can kill one), counted per ship and crew type in the save, from your crew reserve first, then willing prisoners of that type from the ship's brig and the station brig (at 40% of the hire price, the game's recruit cost), then hired at the station's personnel center at its price and stock. Crew of that type added to the ship any other way (by you, ECHO, a loadout) counts as a replacement, so nothing is replaced twice; vanilla ECHO only hires for the flown ship, so without this fleet ships never get their losses back; "Unload brigs" moves the prisoners in your ships' brigs to the station brig (`GamePlayer.maxBrig`, grows with your level), willing ones still willing, so the next boarding's captives fit aboard instead of floating off with "Brig full". When the row would run into the Undock button, the buttons switch to short labels (Repair, Ammo, Sell, Crew, Brig) for the rest of that visit. Hovering lists every ship and stack; a button that has work but can't do it here (no hangar, no credits, not sold here) is greyed with the reason. `FuelCells` (0 = off, up to 50) makes restock also keep that many warp fuel cells in the flown ship (the only one that burns fuel): armory first, then bought, fastest kind first; the button reads "Restock fuel" when only fuel is missing. `RepairOnDock`, `RestockOnDock`, `SellOnDock`, `CrewOnDock`, `BrigOnDock` (all off) run the same services every time you dock, ECHO included (with ECHO on, selling is left to `EchoCargo`); `AirlockButtons` (on). Favourites, mission items, usables the game keeps aboard and ammo a ship uses are never sold. |
| `OrePreference` | On the ECHO tab: a list of ores and a mode. Prefer: ECHO, your ships' own asteroid picks and their mining drones take the nearest asteroid with a listed ore first, and ECHO picks a mining location in the system that still has one (its last one only if that still does); with none left anywhere, mining works as usual. Only: other asteroids are left alone, and ECHO moves on to its next activity when no listed ore is left. Off (default) or an empty list is vanilla. Core ores count only with the Basic Scanning mining skill, the point where the game's tooltip names them. Saved per save game. |
| `EchoRetreat` | On the ECHO tab: `FlagshipHullPercent` and `FleetHullPercent` (0-90, both 0 = off, the game's behaviour). While ECHO is on and the flagship's hull, or that of a fleet ship at the same location, drops below its threshold, ECHO stops fighting, skips the loot pickup and flies the fleet to the nearest friendly station (the home station when it is in this system). There it docks, waits for the hangar's free auto-repair (about 200 s; `StationServices` `RepairOnDock` buys the 20 s repair instead) and carries on as usual. Status line: "Retreating to Tower I for repairs: Eclipse hull at 20%". It doesn't leave while an operation or boarding is running, from next to a station, or when the Autopilot tab's travel is off. Without it the flagship fights until the emergency jump at 0 hull, and every heavy hull hit can kill a crew member; the 10 autopilot sessions your save keeps had 50 emergency jumps in 48 hours. Fleet ships leave by themselves at 10% for two minutes of repairs, so `FleetHullPercent` only matters above 10. |

Mod data goes into the save under a `Player.igiQol` key (per-ship records keyed by ship guid). A feature whose patch throws switches itself off, logs the stack trace, and the main menu lists it in a popup.

The `[Dev]` section is a test harness, off by default. With `Enabled = true` it suppresses autosaves, loads `AutoLoadSave` at the main menu, runs each feature's self-check (`CHECK <Feature>/<name> PASS|FAIL|SKIP` lines in `LogOutput.log`; `OnlyChecks` limits that to a comma-separated list of feature ids; an exception inside a check, also in a coroutine it yields, fails the check and the run goes on), dumps the UI hierarchy of the components named in `DumpHierarchy` to `BepInEx/config/IgiQoL/dumps/` (plus `icons.txt` with `DumpIcons`, `turret-ranges.txt` with `DumpTurretRanges`, `recipes.txt` with `DumpRecipes` and `missiles.txt` with `DumpMissiles`), writes `igiqol-screenshot.png` and the self-checks' `igiqol-<name>.png` next to the `Saves` folder, then with `ProfileSeconds` above 0 spawns `ProfileEnemies` (default 12) hostile ships next to the fleet and measures for that long: frame times (mean, p50, p95, p99), the time spent in each of this mod's Harmony patches and MonoBehaviour updates, and the game's own `SimMarker` sections, written to `dumps/profile.txt`; then with `EchoSoakSeconds` above 0 switches ECHO on for that long (a `[Dev] soak` status line every 15 s, the whole event log at the end), logs `Self-check complete`, and quits after `QuitAfterSeconds`. Point it at a copy of a save, never at a save you care about.

Two more dumps read any value the game holds, without new code. `DumpGameData = true` writes the 15 content registries (items, recipes, equipment and item builders, ships, drones, station parts, modules, defensive turrets, aspects, hazards, crew types, skill trees and nodes, dungeons) to `dumps/<game version>/`, about 9.5 MB, in under a second. `DumpStatics` takes your own comma-separated list of static members, `TypeName.member` with an optional `:depth` (default 3), for example `Faction.all:2`. Each file has one line per value: element key, field path, value, tab-separated, sorted. Grep it to find a number (`grep _range dumps/0.8.3.4/InventoryItemType.allItems.txt`) or compare two versions with `VersionDiff data`. The walk follows fields, not properties. It expands game components on prefabs, including sibling components (`definition.@LaserTurret._range`), lists the first 64 entries of each collection, and prints other Unity objects by name.

## What mods can do

This comes from reading the decompiled code. Paths are relative to `decompiled/`. "Postfix" and "prefix" are Harmony patches that run after or before a game method and can change its arguments or result.

All planned features ship as one plugin, Igi's QoL Modpack. The plan, build order and open questions are in `docs/qol-modpack-plan.md`; detailed designs for loadouts, combat movement, fleet targeting and the autopilot are in `docs/mod-plans.md`. Both are local planning notes and aren't in git.

### Startup timing

`Bootstrapper.Init` (`Behaviour.Bootstrap/Bootstrapper.cs:49`) runs as `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`, before the first scene. It fills every content registry in a fixed order: items, recipes, aspects, hazards, crew skills, skill trees, equipment builders, item builders, ships, drones, station parts, modules, defensive turrets, crew types, dungeons. Then it loads the main menu.

Plugins load before this runs. The test plugin saw 0 registered items in `Awake` and 393 once `InventoryItemType.LoadAll` finished. So a content mod should postfix the registry's `LoadAll()` and edit content there. Don't touch registries from `Awake`, because they're still empty then.

This also means content edits are in place before any save loads, which matters because saves refer to content by name.

### Gameplay numbers (easy)

| Goal | Hook |
|---|---|
| XP multiplier for kills, mining, salvage and missions together | postfix the 3-argument `Source.Util.GameMath.GetExperienceRewardValue(float, int, int)` (`Source.Util/GameMath.cs:329`); the 2-argument overload calls it |
| Commander XP only | prefix `GamePlayer.GainExperience(long)` (`Source.Player/GamePlayer.cs:3174`) |
| Crew XP | prefix `AbstractUnit.AddCrewExperience` and its `SpaceShip`/`Drone` overrides |
| Mission credit rewards | postfix `MissionGenerator.CreditReward` (`Source.MissionSystem/MissionGenerator.cs`, around line 238) |
| Cheaper or free spending | prefix `GamePlayer.RemoveCredits(double, string)` (`GamePlayer.cs:549`); the string is a spending category |
| Mining yield | prefix `Asteroid.SpawnSurfaceOre` / `SpawnInnerCoreOre` (`Behaviour.Mining/Asteroid.cs:513`) and scale `yield` |
| Salvage scrap | postfix `SalvageData.ComputeScrapAmount` (`Source.Data.Persistable/SalvageData.cs:609`) |
| Loot drops | `LootManager.DropLoot`, `DropRegularLoot`, `CreateLootItem` (`Behaviour.Managers/LootManager.cs`) |
| Damage dealt or taken | `DamageData.CalculateDamage` (`Behaviour.Weapons/DamageData.cs:136`) or `AbstractUnit.TakeDamage`; there is also a static `DamageData.OnDamageDealt` event you can subscribe to without patching |
| Market prices | `MarketPricing.GetTargetValue` and `Trade` (`Source.Simulation.Economy/MarketPricing.cs`) |
| Game speed | `GameTimeScale` (`Behaviour.Util/GameTimeScale.cs`) |
| Patrol intensity | `CombatManager.securityPatrolMultiplier` (`Behaviour.Combat/CombatManager.cs:14`) |

Some tuning values in `GameMath` are C# `const`s (`FastLaneTravelMultiplier`, `LootBoxOpenPrice`, and others). The compiler copies those into every caller, so patching the constant does nothing. Patch the methods that use them.

### Content stats (easy to medium)

Every content type is a Unity prefab or ScriptableObject loaded from `Resources` into a static dictionary keyed by asset name:

| Content | Registry | Lookup |
|---|---|---|
| Items | `InventoryItemType` | `Get`, `TryGet`, `all` |
| Crafting recipes | `CraftingRecipe` | `Get`, `TryGet`, `all` |
| Equipment templates | `EquipmentBuilder` | `Get`, `TryGet` |
| Item templates | `ItemBuilder` | `Get`, `TryGet` |
| Ship hulls | `SpaceShip.allShips` | `Get`, `GetAll` |
| Drones, defensive turrets, station parts | `Drone`, `DefensiveTurret`, `CombatStationPart` | `Get` |
| Crew types, skill trees, skill nodes | `CrewType`, `Skilltree`, `SkilltreeNode` | `Get` |
| Equipment aspects, hazards, dungeons | `EquipAspect`, `LocalHazard`, `DungeonDefinition` | `Get` |
| Factions | `Faction` (plain C# class, created lazily by `Faction.Get(id)`) | `Get`, `all` |

You can rebalance anything those objects expose: item value, rarity and level (`InventoryItemType.SetBaseValue`, `SetRarity`, `SetItemLevel`), equipment `capacityCost` and `stats`, hull warp speed and cost (`baseMaxWarpSpeed`, `costMultiplier`), recipe `craftingValueMultiplier` and `unlockedFromStart`, crew cost. Edit the templates before the game clones them, or items that already exist keep their old values. Recipe ingredient lists are private serialized fields. The publicizer exposes them, but there are no setters.

### New content (hard)

You can add a new item, recipe, equipment type or hull by cloning an existing one (`Object.Instantiate`), changing it, and adding it to the registry under a new name. Saves store items by builder or asset name. If your mod isn't loaded, or a name changes, the game won't find the content on load: missing items get dropped with a warning, and `ItemBuilder.Get`/`EquipmentBuilder.Get` throw. Removing a content mod can therefore damage a save that uses its content.

Cloning only reuses existing art. New sprites are fine (load a PNG into a `Texture2D` and call `Sprite.Create`). New prefabs, models, shaders or sounds need an AssetBundle built with the same Unity version (6000.4.7f1).

Text comes from `Resources/Language/*` and goes through `Translation.Translate("@Key")`. Postfix `Translation.Translate` to add your own keys or change existing strings. Item names are translated once at creation and saved as plain text, so a text mod won't rename items you already own.

### Saving your mod's own data (medium)

- Write: postfix `GamePlayer.ToJson()` (`Source.Player/GamePlayer.cs:1705`) and add your own key to the returned JSON object. The game uses the `LightJson` library, not Newtonsoft.
- Read: after a save loads, `GamePlayer.current` is set. Postfix `SceneLoader.LoadScenesOnStartGame` (`Behaviour.Bootstrap/SceneLoader.cs:170`) and read your key back.
- Version migrations for vanilla data run in `GamePlayer` around lines 2389-2514 (`GamePatch.IsBelowAndReport`).

Unknown keys are likely ignored when the game loads, so an unmodded game should still open the save. I haven't tested that.

### UI (medium)

The game uses uGUI and TextMeshPro, not UI Toolkit.

- Toast message, no patch needed: `Singleton<NotificationManager>.Instance.CreateNotification("text").WithCustomTime(4f).Show();`
- Yes/no or text-input dialogs: `AlertPopup.ShowQuery` / `ShowInput` (`Behaviour.UI/AlertPopup.cs`).
- Extra tooltip lines: postfix `TooltipSource.AddCustomContent(UITooltip)` and call `tooltip.AddTextLine(...)`. Ship, POI, crew, skill and drone tooltips have static card builders (`ShipTooltipCardBuilder.Build` and others) you can postfix to add rows.
- New windows: the side-menu tabs are a closed enum (`SidePanel.SideTabType`) built from prefabs, so a mod can't add a real tab. Build your own uGUI panel under `HudManager.Instance` or `UITooltip.tooltipParent`, or draw an IMGUI window from your plugin's `OnGUI` (the quickest option for tools and debug panels).
- Settings: `SettingsMenu` sub-pages can be postfixed to add rows. Mod settings normally live in the BepInEx `.cfg` file instead. You can also install ConfigurationManager to get an in-game settings window for every plugin.
- UI scale: the game's Scale setting is one `CanvasScaler.scaleFactor` for the whole canvas (`Behaviour.Transparency/CanvasScaleUpdater.cs`, `GameplayerPrefs.GetScaleFactor`), so text and panels always grow together and there's no separate text size. The HUD is laid out as a strip 1920 units wide and 324 tall, so Scale tops out at window width / 1920 × 100 (176 for a 3389 px wide window). A higher value only takes effect when the window is also tall enough for the stacked two-row layout (`ScreenSettings.DetermineStack`, `CalculateScale`). The `TextScale` feature scales fonts on top of that by multiplying the size TextMeshPro lays text out at (`TextMeshProUGUI.GenerateTextMesh`, `TMP_Text.CalculatePreferredValues`).
- Extra station buttons: `PersonalFleetButton.Create(tabPrefab, below, root, icon, windowPrefab, spec)` (`Behaviour.UI.Spacestation/PersonalFleetButton.cs`) makes a station tab that floats above another tab; set the result's `SpaceStationButton.clickOverride` to use it for something else. The QoL station tab is built this way.

### Input

The game uses Unity's new Input System through a generated `PlayerControls` class (`decompiled/PlayerControls.cs`). Some UI code also uses legacy `Input.GetKeyDown`. Both work from a mod. Create your own `InputAction` with a binding like `"<Keyboard>/f9"`, or check `UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame` in `Update()`. Skip the hotkey while a text field has focus (`UIHelper.IsTypingInInputField()`).

### Galaxy, missions, AI (hard)

- Galaxy generation is in `SandboxWorld.SetupWorld` (`Source.Simulation.World/SandboxWorld.cs:209`), seeded from the clock through `SeededRandom.Global` (a public static you can replace for repeatable galaxies). Sector count, jump-gate links, dungeons and side content all have their own methods in that file.
- New mission types: subclass `MissionGenerator` and implement `GenerateMission` and `GetMissionType`. How generators get registered in `MissionGenerator.generators` hasn't been traced yet. Missions are generated per station in `MissionBoard`.
- Ship AI is in `Source.SpaceShip.Auto/` (`CombatActions`, `ThreatTable`, miner and salvager actions). Fleet and faction AI is in `Source.Galaxy/` (`ConquestFleetPlanner`, `EmpireFleets`, raids). It runs on `LivingUniverse`'s hourly tick.

### Leave alone

- `SteamAchievement.Clear` resets all your achievements and stats. Don't call it, and don't unlock vanilla achievements from a mod.
- `SteamCommunication.supporterEdition` and the DLC checks gate paid content.

## The built-in developer console (no mods needed)

The game ships a dev console with about 130 commands (`Behaviour.UI.DebugScreen/ConsoleScreen.cs`). It's off unless a file named `console.enabled` exists in the game's working directory, which is the install folder:

```fish
touch "/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy/console.enabled"
```

Then press the backtick key (`` ` ``) in game and type `help`. Commands include `AddCredits`/`ac`, `AddXp`/`axp`, `AddSkillPoints`/`asp`, `CreateShip`/`cs`, `SetHP`, `FillCrew`, shop refreshes, teleport, and time scale (`ts`). While it's enabled, holding Ctrl+Alt+Shift also lets you click map locations to teleport and open locked systems. Delete the file to turn it off. A mod can add its own commands to `Singleton<ConsoleScreen>.Instance.commands` (a private `List<ConsoleCommand>`, reachable through the publicizer).
