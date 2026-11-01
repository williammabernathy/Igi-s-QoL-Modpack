# Igi's QoL Modpack

One BepInEx plugin (`igi.qol`, in `BepInEx/plugins/IgiQoL/`) with 34 quality-of-life features for Vanguard Galaxy: a mod window with its own tabs, more say over ECHO's decisions, fleet-wide station services, ship loadouts, per-ship combat settings, logs and a damage parser. Every feature can be switched off on its own. The defaults play like the unmodded game, apart from the added HUD elements (the QoL button, the Scanner counter and markers, the fleet bars and command strip, the jump and mission distance lines) and one ECHO fix: it no longer pays for crew that can't fit aboard. Tested on game version 0.8.3.4.

The modpack was developed and tested on Linux only, with the game running under Proton. Proton runs the game's Windows build, so the mod should work the same on Windows, but nobody has tested it there yet.

![The mod window open on the ECHO tab, in space](../images/overview.jpg)

## Install

1. Install BepInEx 5 (`win_x64`, version 5.4.23 or later) into the game folder and start the game once. On Linux under Proton, BepInEx also needs a DLL override; [setup.md](../setup.md#installing-bepinex) explains it.
2. Download `IgiQoL-<version>.zip` from the [releases page](https://github.com/williammabernathy/Igi-s-QoL-Modpack/releases) and extract it into the game folder. It adds `BepInEx/plugins/IgiQoL/`.
3. Start the game. `BepInEx/LogOutput.log` should have a line like `IgiQoL startup: 35 active, 1 disabled, 0 failed` (the disabled one is the developer harness).

To update, extract the new zip over the old one. To remove the mod, delete `BepInEx/plugins/IgiQoL/` (and `BepInEx/config/igi.qol.cfg` and `BepInEx/config/IgiQoL/` if you want its settings and files gone too).

## Using it

Press F6, click the QoL button in space, or click the QoL tab above the Airlock tab when docked. Every feature's switch and setting is in that window; see [Window and HUD](features/interface.md#mod-window). Your ships' own settings are in the popup behind the last button on each fleet row.

The features, by area:

| Area | Features |
|---|---|
| [Window and HUD](features/interface.md) | Mod window, ship settings popup, fleet command strip, fleet shield and armor bars, text size, jumps remaining, mission distance |
| [ECHO, the autopilot](features/echo.md) | Activity priority, ECHO decisions, missions, retreat, abilities and deployables, preferred ores, ECHO's operations, cargo rules |
| [Fleet and combat](features/fleet-combat.md) | Target priority, engagement distance, keep distance in combat, missile retargeting, drone modes, drones on arrival |
| [Loot and cargo](features/loot-and-cargo.md) | Item rules, loot pickup limits, shared fleet holds, Scanner |
| [Stations, loadouts and crafting](features/stations.md) | Station services, ship loadouts, crafting roll-up, refinery filter, Operations tab |
| [Logs, reference and other mods](features/logs-and-reference.md) | Event log, DPS parser, stat reference, notes, other mods |

[settings.md](settings.md) lists every setting with its default and range.

## Files it writes

| Where | What |
|---|---|
| `BepInEx/config/igi.qol.cfg` | Every setting, one section per feature |
| The save game, key `Player.igiQol` | Per-ship settings and loadouts, item rules, preferred ores, ECHO's activity list and ability rules, running crafting roll-ups and crew losses |
| `BepInEx/config/IgiQoL/item-rules.json` | Optional. Starting item rules for saves that have none ([format](features/loot-and-cargo.md#starting-rules-for-new-saves)) |
| `BepInEx/config/IgiQoL/notes.json` | The Notes tab |
| `BepInEx/config/IgiQoL/combat-log.txt` | Only with the DPS parser's `WriteFile` on |
| `BepInEx/config/IgiQoL/dumps/` | Only from the developer harness |
| Next to the game's `Saves` folder | `igiqol-*.png` screenshots, only from the developer harness |

The mod keeps its own data under that one key and leaves the game's values in their vanilla form (drone modes, for example, are saved as the game's Free roam or Follow ship), so a save should still load without the mod and only lose the mod's data. Loading such a save in an unmodded game hasn't been tested yet; keep a backup.

## When something breaks

A feature whose patch or save-load handler throws switches itself off, logs the stack trace to `BepInEx/LogOutput.log` (`[<Feature>] switched off at ...`), and the main menu lists it in a popup. The other features and the mod's save data carry on. After a game update, a patch the update broke shows up the same way, or as a Harmony error at startup.

The Mods tab ([Other mods](features/logs-and-reference.md#other-mods)) shows which game methods another mod also patches, which is where to look when two mods fight.

## For developers

- [developing.md](developing.md): how the code is laid out, the conventions every feature follows, and how to add a feature.
- [dev-harness.md](dev-harness.md): the in-game test harness, self-checks, data dumps and profiling.
- [../building.md](../building.md) and [../ci.md](../ci.md): building, game updates, CI and releases.
