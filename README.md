# Vanguard Galaxy Mods

My modding workspace for Vanguard Galaxy (Steam app 3471800), played on Linux through Proton. The main thing in it is Igi's QoL Modpack, one BepInEx plugin with 44 quality-of-life features. Around it are tools for keeping mods working across game updates and a template for starting new ones.

The modpack, the tools and the template were developed and tested on Linux only, with the game running under Proton. The game itself is a Windows build, so they should also work on Windows, but nobody has tried them there yet. Reports from Windows players are welcome.

![Igi's QoL Modpack: the mod window open on the ECHO tab](docs/images/overview.jpg)

## Documentation

The player-facing pages (game mechanics, the modpack's features and settings, BepInEx setup) are also published as a website: <https://williammabernathy.github.io/Igi-s-QoL-Modpack/>.

| If you want to | Read |
|---|---|
| Install and use the modpack | [docs/modpack/README.md](docs/modpack/README.md) |
| See what each feature does | [Window and HUD](docs/modpack/features/interface.md), [ECHO, the Autopilot](docs/modpack/features/echo.md), [Fleet and Combat](docs/modpack/features/fleet-combat.md), [Loot and Cargo](docs/modpack/features/loot-and-cargo.md), [Stations, Loadouts and Crafting](docs/modpack/features/stations.md), [Conquest](docs/modpack/features/conquest.md), [Logs, Reference and Other Mods](docs/modpack/features/logs-and-reference.md) |
| Look up a setting | [docs/modpack/settings.md](docs/modpack/settings.md) |
| Understand how game systems work (damage, scaling, boarding, conquest, faction economies), ordered by when the game opens them | [docs/index.md](docs/index.md) |
| Get BepInEx running on Windows or under Proton, find logs and saves | [docs/setup.md](docs/setup.md) |
| Build the mods, or update them after a game patch | [docs/building.md](docs/building.md) |
| Find which game method to patch for something | [docs/modding-reference.md](docs/modding-reference.md) |
| Work on the modpack's code | [docs/modpack/developing.md](docs/modpack/developing.md) |
| Run the in-game self-checks, dump game data, profile | [docs/modpack/dev-harness.md](docs/modpack/dev-harness.md) |
| Compare two game versions for broken patches | [tools/VersionDiff/README.md](tools/VersionDiff/README.md) |
| Start a new mod in its own repo | [docs/template.md](docs/template.md) |
| Understand the pipeline, releases and the documentation site | [docs/ci.md](docs/ci.md) |

## What's Here

| Path | What it is |
|---|---|
| `src/IgiQoL/` | Igi's QoL Modpack |
| `src/HelloVanguard/` | The smallest working plugin, which proves that BepInEx loads ([README](src/HelloVanguard/README.md)) |
| `tools/VersionDiff/` | Compares game versions: which patched methods changed, which game members a mod DLL uses that are gone, and what changed in the game's data |
| `tools/Harness/` | Runs the modpack's in-game self-checks on a copy of a save and reports the results ([dev-harness.md](docs/modpack/dev-harness.md#a-run)) |
| `templates/VgMod/` | `dotnet new` template for a mod in its own repo |
| `Directory.Build.props` | Shared build setup: game references, publicizer, deploy to the game, game-version snapshots |
| `.github/workflows/ci.yml` | Build, patch check and releases, on a runner on this PC |
| `.github/workflows/pages.yml`, `zensical.toml`, `tools/site/` | Builds `docs/` into the documentation site and publishes it to GitHub Pages |
| `docs/` | The documentation above. `docs/images/` holds the screenshots |
| `decompiled/`, `game-versions/` | The decompiled game and copies of its DLLs per version. Both are gitignored, because they are the game's copyrighted code |

## Quick Start

```fish
dotnet build -c Release
```

This builds everything against the installed game and copies the plugins into `BepInEx/plugins/`. You need the .NET 8 SDK and BepInEx 5 in the game folder. [docs/setup.md](docs/setup.md) and [docs/building.md](docs/building.md) have the details.

Releases of the modpack are on the [GitHub releases page](https://github.com/williammabernathy/Igi-s-QoL-Modpack/releases).
