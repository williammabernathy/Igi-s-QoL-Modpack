# Game and BepInEx Setup

Vanguard Galaxy (Steam app 3471800) is played here on Linux through Proton. These are the facts that decide the tooling:

| | |
|---|---|
| Install | `/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy` |
| Build | Windows x64, runs under Proton 11.0 |
| Engine | Unity 6000.4.7f1 (Unity 6.4) |
| Scripting backend | Mono (`VanguardGalaxy_Data/Managed/Assembly-CSharp.dll` exists), so BepInEx 5 works. IL2CPP games need BepInEx 6, but this one doesn't. |
| Game version | 0.8.3.5, open-beta branch (the Steam app manifest's beta key is `open-beta`, and a save written by the 0.8.3.5 build has `{"Version":"0.8.3.5"}` in its `.meta` file). `decompiled/` matches it, and `decompiled-0.8.3.4/` keeps the previous build for diffs. |
| Mod loader installed | BepInEx 5.4.23.6, `win_x64` build |

There is no official modding API. Every mod is a BepInEx plugin that uses Harmony to patch the game's C# code at runtime. The documentation is the game's own code, decompiled into `decompiled/` (gitignored, reference only).

## Installing BepInEx

On Windows, extract BepInEx 5 (`win_x64`) into the game folder and start the game once. That's all.

Under Proton there's one more step. BepInEx is copied into the game folder (`winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `BepInEx/`), but Proton normally loads Wine's built-in `winhttp.dll` instead of BepInEx's. On this machine, the game's Wine prefix has a DLL override that prefers the native one. It was added and verified on 2026-10-27:

- File: `/mnt/data/SteamLibrary/steamapps/compatdata/3471800/pfx/user.reg`
- Section `[Software\\Wine\\DllOverrides]`, line `"winhttp"="native,builtin"`
- The original file is saved next to it as `user.reg.pre-bepinex.bak`.

The Steam launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%` does the same thing. Either one works, and you don't need both. If you ever delete the Proton prefix, or Steam recreates it, the override is gone and BepInEx silently stops loading. Re-add the line above, or use the launch option instead.

Check that BepInEx ran:

```fish
cat "/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy/BepInEx/LogOutput.log"
```

On Windows, open `BepInEx\LogOutput.log` in the game folder (Steam > right-click the game > Manage > Browse local files). The first verified run showed `Chainloader started` and `Loading [Hello Vanguard 0.1.0]`. The test plugin then reported 0 items at plugin load and 393 items after `InventoryItemType.LoadAll` finished.

If `LogOutput.log` doesn't get rewritten when you start the game, BepInEx didn't load. Under Proton, the override isn't active. On Windows, the BepInEx files aren't directly in the game folder.

For a live log window, set `[Logging.Console] Enabled = true` in `BepInEx/config/BepInEx.cfg`.

## Using Mods

- Mod DLLs go in `BepInEx/plugins/`. A subfolder per mod is fine.
- Logs: `BepInEx/LogOutput.log` holds the output of BepInEx and the plugins and is rewritten at every start. Unity's own log is `Player.log`. On Windows it is at `%USERPROFILE%\AppData\LocalLow\Bat Roost Games\VanguardGalaxy\Player.log`. Under Proton, the same folder sits inside the prefix. On this machine that is
  `/mnt/data/SteamLibrary/steamapps/compatdata/3471800/pfx/drive_c/users/steamuser/AppData/LocalLow/Bat Roost Games/VanguardGalaxy/Player.log`.
- Per-mod settings appear in `BepInEx/config/<plugin guid>.cfg` after the first run.
- Play unmodded: set `enabled = false` in `doorstop_config.ini` in the game folder. BepInEx stays on disk but doesn't load.
- Uninstall: delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and `BepInEx/` from the game folder. Steam > Properties > Installed Files > Verify also restores a clean install, but it won't delete those extra files.
- Game updates can break mods that patch methods the developers renamed or changed. A broken patch shows up as an error in `LogOutput.log`, and the game itself usually still starts.

## Saves

Back up your saves before trying mods that touch save data. On Windows they live in `%USERPROFILE%\AppData\LocalLow\Bat Roost Games\VanguardGalaxy\Saves\`. Under Proton they are at
`.../compatdata/3471800/pfx/drive_c/users/steamuser/AppData/LocalLow/Bat Roost Games/VanguardGalaxy/Saves/`.

Each save is a `.save` file (gzip-compressed JSON; `zcat autosave-0.save | jq .` works) plus a `.meta` file that holds the game version. There are three rotating autosaves, written every 300 seconds. Steam Cloud syncs the folder, so a save you delete can come back after the next launch.

## The Built-In Developer Console

The game ships a dev console with about 140 commands (`Behaviour.UI.DebugScreen/ConsoleScreen.cs`). You don't need a mod for it. It's off unless a file named `console.enabled` exists in the game's working directory, which is the install folder:

```fish
touch "/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy/console.enabled"
```

On Windows, create an empty file with that name in the game folder, for example in PowerShell: `New-Item "C:\Program Files (x86)\Steam\steamapps\common\Vanguard Galaxy\console.enabled"`.

Then press the backtick key (`` ` ``) in game and type `help`. Commands include `AddCredits`/`ac`, `AddXp`/`axp`, `AddSkillPoints`/`asp`, `CreateShip`/`cs`, `SetHP`, `FillCrew`, shop refreshes, teleport and time scale (`ts`). While the console is enabled, holding Ctrl+Alt+Shift also lets you click map locations to teleport and open locked systems. Delete the file to turn the console off. A mod can add its own commands to `Singleton<ConsoleScreen>.Instance.commands`, which is a private `List<ConsoleCommand>` that the publicizer makes reachable.
