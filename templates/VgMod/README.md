# VgMod

A BepInEx 5 plugin for Vanguard Galaxy. It uses Harmony to patch the game at runtime.

## Set up

1. Install BepInEx 5 (x64, Mono) into the game folder and start the game once. That creates `BepInEx/LogOutput.log`.
2. Set the game folder. Either edit the default `GameDir` in `Directory.Build.props`, or pass it on every build:
   `dotnet build -p:GameDir="/path/to/Vanguard Galaxy"`
3. You need the .NET SDK. The project targets net472, so it also builds on Linux and macOS.

## Build and deploy

```
dotnet build -c Release
```

The build compiles against the game's DLLs and copies the plugin to `BepInEx/plugins/VgMod/` in the game folder. To build without copying, add `-p:DeployToGame=false`.

The game's private members are open to your code. The build strips their access modifiers from `Assembly-CSharp` at compile time.

## Read the log

Open `BepInEx/LogOutput.log` in the game folder after a run. Look for lines from `VgMod`. The plugin logs the game version, a warning if it differs from `TestedGameVersion`, and one error per patch class that failed to apply. A broken patch class is skipped. The rest of the plugin keeps working.

Each patch class in `Plugin.cs` is patched on its own. Add yours to the `PatchClasses` array. Wrap every patch body in try/catch so a bug in your code cannot crash the game.

## Dev harness

`DevHarness.cs` runs the game without clicking. It is off by default. Turn it on in `BepInEx/config/AUTHOR_TOKEN.MODID_TOKEN.cfg`, section `[Dev]`:

- `Enabled`: turns the harness on. It also stops autosaves, so a test run cannot overwrite your saves.
- `AutoLoadSave`: save name (no extension) to load at the main menu.
- `SelfCheck`: run the checks once the save is loaded.
- `QuitAfterSeconds`: quit the game this long after the checks end.

Add checks to `RunChecks`. Each check calls `Pass`, `Fail` or `Skip`. The log gets one line per check:

```
CHECK sample-patch PASS - MainMenuUI.Start postfix ran
Self-check complete: 1 pass, 0 fail, 0 skip
```

Search the log for `CHECK` and `Self-check complete`.

## After a game update

Every build copies the game DLLs it compiled against into `game-versions/<game version>-<hash>/`. When Steam updates the game, the old copy stays there. Those folders are in `.gitignore`. Do not commit them.

To see what changed between two versions, use the VersionDiff tool from https://github.com/williammabernathy/Igi-s-QoL-Modpack (`tools/VersionDiff`, documented in its README). Its `patches` command shows which methods your patches target have changed or disappeared, and which game members your DLL uses that are gone.

After an update:

1. Before rebuilding, run it from this folder: `dotnet run --project <path to that repo>/tools/VersionDiff -- patches --mod bin/Release/VgMod.dll --out patch-report.md`. It compares your last snapshot with the installed game.
2. Fix the patches it flags, then build. The build saves the new snapshot, even if it fails.
3. Run the dev harness, then update `TestedGameVersion`.
