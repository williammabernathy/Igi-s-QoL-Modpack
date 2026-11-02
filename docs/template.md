# Starter template (`templates/VgMod`)

`templates/VgMod` is a `dotnet new` template for a Vanguard Galaxy mod in its own repo. It has the same build setup as this repo (game references, publicized `Assembly-CSharp`, deploy to the game, game-version snapshots) plus two things a new mod would otherwise grow later: a plugin that patches each patch class separately, and a trimmed version of the modpack's dev harness.

## Install and create a mod

```fish
dotnet new install templates/VgMod
dotnet new vgmod -n MyMod --Author yourname --GameDir "/path/to/Vanguard Galaxy"
cd MyMod
dotnet build -c Release
```

| Parameter | Meaning | Default |
|---|---|---|
| `-n`, `--name` | Mod name: project, assembly, namespace and plugin folder. Its lowercase form is the second half of the plugin GUID | the output folder's name |
| `--Author` | First half of the plugin GUID (`author.modname`, lowercased) | `yourname` |
| `--GameDir` | Game folder, written into the new project's `Directory.Build.props` as the default `GameDir` | `C:\Program Files (x86)\Steam\steamapps\common\Vanguard Galaxy` |

With the example above the plugin GUID is `yourname.mymod` and the build deploys to `BepInEx/plugins/MyMod/`.

After editing the template, reinstall it: `dotnet new uninstall <absolute path to templates/VgMod>`, then `dotnet new install templates/VgMod`. `dotnet new uninstall` with no argument lists what's installed.

## What you get

| File | Contents |
|---|---|
| `Plugin.cs` | The BepInEx plugin. Logs the game version and warns when it differs from `TestedGameVersion`; patches each class in `PatchClasses` on its own, so a class broken by a game update is skipped with one error line and the rest keeps working. Has one sample postfix on `MainMenuUI.Start` |
| `DevHarness.cs` | Off by default. Auto-loads a save, stops autosaves, runs your checks and logs `CHECK <name> PASS/FAIL/SKIP` lines, quits after a delay |
| `Directory.Build.props` | Game references, publicizer, `net472`, deploy to `BepInEx/plugins/<name>/`, game-version snapshots into `game-versions/` |
| `.gitignore` | `bin/`, `obj/` and `game-versions/` (the snapshots are the game's copyrighted DLLs) |
| `README.md` | Setup, the dev harness, and what to do after a game update. It's written for the new mod and ends up in its folder |

## How it differs from Igi's QoL Modpack

The modpack has a feature registry, per-feature Harmony instances, save data, a mod window and about 290 self-checks ([modpack/developing.md](modpack/developing.md)). The template has none of that. It's one plugin class and a harness of under 200 lines, so a small mod starts small. The patch-per-class loading, the `CHECK` log format and the game-version snapshots are the same, so [VersionDiff](../tools/VersionDiff/README.md) works on a template mod as it does here:

```fish
dotnet run --project <path to this repo>/tools/VersionDiff -- patches --mod bin/Release/MyMod.dll --out patch-report.md
```
