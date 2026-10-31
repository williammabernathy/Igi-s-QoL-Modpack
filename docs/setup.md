# Game and BepInEx setup

Vanguard Galaxy (Steam app 3471800) is played here on Linux through Proton. These are the facts that decide the tooling:

| | |
|---|---|
| Install | `/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy` |
| Build | Windows x64, runs under Proton 11.0 |
| Engine | Unity 6000.4.7f1 (Unity 6.4) |
| Scripting backend | Mono (`VanguardGalaxy_Data/Managed/Assembly-CSharp.dll` exists), so BepInEx 5 works. IL2CPP games need BepInEx 6; this one doesn't. |
| Game version | 0.8.3.4, open-beta branch (from the save `.meta` files). `decompiled/` matches it; `decompiled-0.8.3.2/` keeps the previous build for diffs. |
| Mod loader installed | BepInEx 5.4.23.6, `win_x64` build |

There is no official modding API. Every mod is a BepInEx plugin that uses Harmony to patch the game's C# code at runtime. The documentation is the game's own code, decompiled into `decompiled/` (gitignored, reference only).

## Installing BepInEx

On Windows, extract BepInEx 5 (`win_x64`) into the game folder and start the game once. That's all.

Under Proton there's one more step. BepInEx is copied into the game folder (`winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `BepInEx/`), but Proton normally loads Wine's built-in `winhttp.dll` instead of BepInEx's. On this machine the game's Wine prefix has a DLL override that prefers the native one (done and verified 2026-10-27):

- File: `/mnt/data/SteamLibrary/steamapps/compatdata/3471800/pfx/user.reg`
- Section `[Software\\Wine\\DllOverrides]`, line `"winhttp"="native,builtin"`
- The original file is saved next to it as `user.reg.pre-bepinex.bak`.

The Steam launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%` does the same thing. Either one works; you don't need both. If you ever delete the Proton prefix (or Steam recreates it), the override is gone and BepInEx silently stops loading. Re-add the line above, or use the launch option instead.

Check that BepInEx ran:

```fish
cat "/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy/BepInEx/LogOutput.log"
```

The first verified run showed `Chainloader started`, `Loading [Hello Vanguard 0.1.0]`, then the test plugin reporting 0 items at plugin load and 393 items after `InventoryItemType.LoadAll` finished. If `LogOutput.log` doesn't get rewritten when you start the game, the override isn't active.

For a live log window, set `[Logging.Console] Enabled = true` in `BepInEx/config/BepInEx.cfg`.

## Using mods

- Mod DLLs go in `BepInEx/plugins/` (a subfolder per mod is fine).
- Logs: `BepInEx/LogOutput.log` (BepInEx and plugins, rewritten at every start) and Unity's own log at
  `/mnt/data/SteamLibrary/steamapps/compatdata/3471800/pfx/drive_c/users/steamuser/AppData/LocalLow/Bat Roost Games/VanguardGalaxy/Player.log`.
- Per-mod settings appear in `BepInEx/config/<plugin guid>.cfg` after the first run.
- Play unmodded: set `enabled = false` in `doorstop_config.ini` in the game folder. BepInEx stays on disk but doesn't load.
- Uninstall: delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and `BepInEx/` from the game folder. Steam > Properties > Installed Files > Verify also restores a clean install, but it won't delete those extra files.
- Game updates can break mods that patch methods the developers renamed or changed. A broken patch shows up as an error in `LogOutput.log`; the game itself usually still starts.

## Saves

Back up your saves before trying mods that touch save data. They live at
`.../compatdata/3471800/pfx/drive_c/users/steamuser/AppData/LocalLow/Bat Roost Games/VanguardGalaxy/Saves/`.

Each save is a `.save` file (gzip-compressed JSON; `zcat autosave-0.save | jq .` works) plus a `.meta` file holding the game version. There are three rotating autosaves, written every 300 seconds. Steam Cloud syncs the folder, so a save you delete can come back after the next launch.

## The built-in developer console

The game ships a dev console with about 130 commands (`Behaviour.UI.DebugScreen/ConsoleScreen.cs`). You don't need a mod for it. It's off unless a file named `console.enabled` exists in the game's working directory, which is the install folder:

```fish
touch "/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy/console.enabled"
```

Then press the backtick key (`` ` ``) in game and type `help`. Commands include `AddCredits`/`ac`, `AddXp`/`axp`, `AddSkillPoints`/`asp`, `CreateShip`/`cs`, `SetHP`, `FillCrew`, shop refreshes, teleport, and time scale (`ts`). While it's enabled, holding Ctrl+Alt+Shift also lets you click map locations to teleport and open locked systems. Delete the file to turn it off. A mod can add its own commands to `Singleton<ConsoleScreen>.Instance.commands` (a private `List<ConsoleCommand>`, reachable through the publicizer).
