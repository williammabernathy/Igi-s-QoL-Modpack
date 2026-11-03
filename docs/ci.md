# CI and releases

`.github/workflows/ci.yml` runs on every push to `main` and `development`. Day-to-day work goes to `development`; a merge to `main` that touches the mod's sources publishes a release.

```mermaid
flowchart LR
    build --> patch-check
    patch-check -->|main only, src/IgiQoL or<br/>Directory.Build.props changed| release
```

1. `build` compiles the solution against the installed game and packs `artifacts/IgiQoL-<version>.zip`, laid out as `BepInEx/plugins/IgiQoL/IgiQoL.dll` (+ `.pdb`) so it extracts straight into the game folder. The DLL and zip are the run's `build` artifact.
2. `patch-check` runs `VersionDiff patches` in manifest mode on the built DLL. It fails if a Harmony target, a game member the DLL uses, or an `AccessTools`/`Traverse` string lookup doesn't resolve in the installed game. The report is the run's `patch-report` artifact.
3. On `main` only, and only when `src/IgiQoL/` or `Directory.Build.props` differs from the newest `v*` tag, `release` creates the tag `v<version>` and a GitHub release with the zip attached. Comparing with the last release, not the previous push, means a release that failed is tried again by the next push, and re-running a finished release finds nothing new. The release notes list the commit subjects since the previous tag, without merge commits. `release` fails instead of writing onto a tag that already exists.

Runs of one branch go one at a time (a workflow-level `concurrency` group per ref that never cancels a running run), so two close pushes can't both pick the same version.

The in-game self-checks ([dev-harness.md](modpack/dev-harness.md)) need the game running, so CI doesn't run them. Run them yourself before merging to `main`.

## Versions

`<Version>` in `src/IgiQoL/IgiQoL.csproj` gives major and minor. The patch is one above the newest `vMAJOR.MINOR.*` tag, or `<Version>`'s own patch for the first release of that line. To start 0.2, set `<Version>0.2.0</Version>`. `[BepInPlugin]` and the startup log line read the version through the generated `PluginInfo.Version`, so local builds report `<Version>` as written.

## Running the pipeline locally

The same two jobs run on the host without the runner. From the repo root, in bash:

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
G="/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy"

# build: the version the pipeline would give this commit
git fetch --tags --force --quiet origin
base=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/IgiQoL/IgiQoL.csproj)
last=$(git tag -l "v${base%.*}.*" --sort=-v:refname | head -n 1)
if [ -n "$last" ]; then VERSION="${base%.*}.$(( ${last##*.} + 1 ))"; else VERSION="$base"; fi
dotnet build VanguardGalaxyMods.sln -c Release -warnaserror -p:GameDir="$G" -p:DeployToGame=false -p:Version="$VERSION" -p:ReleaseZip=true

# patch-check
dotnet run --project tools/VersionDiff -c Release -- patches --new "$G/VanguardGalaxy_Data/Managed" \
  --mod src/IgiQoL/bin/Release/IgiQoL.dll --out patch-report.md
```

`build` runs `dotnet build` with `-warnaserror`, so it passes only with no warnings at all; `patch-check` passes when VersionDiff exits 0. The zip lands in `artifacts/` (gitignored). The `release` job needs the workflow's GitHub token and only makes sense in CI.

## Documentation site

`.github/workflows/pages.yml` builds `docs/` with [Zensical](https://zensical.org/) (`zensical.toml`) and publishes it to GitHub Pages at <https://williammabernathy.github.io/Igi-s-QoL-Modpack/>. It runs on pushes to `main` that change `docs/`, `zensical.toml`, `tools/site/` or the workflow, and by hand from the Actions tab. It needs nothing from the game, so it runs on a GitHub-hosted runner. The build is `--strict`: a link to a page the site doesn't have fails it.

The site has the mechanics pages, the modpack's user pages and `setup.md`. The developer pages under `exclude_docs` in `zensical.toml` stay on GitHub only, so a site page that links to one uses its full GitHub URL. A new page goes in `nav` in `zensical.toml` or under `exclude_docs`.

Mechanics pages follow two layout rules. First, they sit in a stage folder under `docs/mechanics/` (`basics`, `conquest`, `expansion`, `late-game`) by when the game opens the system. A page names a later stage only as a pointer to its gate, and late-game names stay inside `late-game/`. The nav tabs carry the stage's spoiler level (`Conquest (Level 28+ Spoilers)`), and a section that reveals a story mission, an unlock's effects or a story faction has its heading prefixed `Spoiler: `. Headings, page titles and file names never name a story faction, place or item (no "Malcyte", "Meridia" or "Umbral"): they show up in the sidebar, the "On this page" list and the address bar, so they use neutral wording ("The dormant raider faction") and the names stay in the body text. Second, each page has two parts. The player explanation comes first, with no code identifiers or `Source:` lines. A final `## In the game code` section follows, with one `Code: <heading>` subsection per section it backs (one heading level deeper; the `Spoiler: ` prefix is left out of the `Code:` heading). Each player section that has code notes ends with `In the code: [..](#code-...)`.

Each stage folder also has a `faq.md`, "Common questions": short answers to questions players ask, grouped under topic headings, one `###` question per answer so a forum post can link straight to it. Its answers only condense facts from the stage's own pages (or earlier stages), follow the same spoiler and heading rules, and end with `Details: [..](page.md#section)` pointing at the section with the full numbers. It has no code identifiers and no "In the game code" section, since the linked section already has the code notes. When a page's numbers change, update the matching answer.

To preview it, from the repo root:

```bash
python -m venv /tmp/site-venv && /tmp/site-venv/bin/pip install -r tools/site/requirements.txt
/tmp/site-venv/bin/zensical serve
```

It serves at `http://localhost:8000` and rebuilds on save. `zensical build` writes to `site/` (gitignored).

## The runner

The game's DLLs are copyrighted and can't go to GitHub, so `ci.yml`'s jobs run only on a self-hosted runner on this PC, labeled `vanguard-galaxy`. Runs wait as queued while it is off.

- Install: `~/.local/share/actions-runner` (the `actions/runner` release for linux-x64). `.runner` and `.credentials*` there hold its registration, keep them private.
- Service: `systemctl --user {status,restart} github-runner`, a user unit in `~/.config/systemd/user/`. It starts at login, not at boot.
- Jobs run directly on the host, in `_work/` under the install, not in a container. `.env` in the install sets `GAME_DIR` (the game folder), `DOTNET_ROOT` and a `PATH` that starts with `~/.dotnet`; the user unit loads it with `EnvironmentFile=`, so restart the service after editing it.
- The workflow only triggers on pushes, so pull requests from forks never run code on this PC. Keep it that way: don't add `pull_request` triggers while the repo is public.
- After moving the game, update `GAME_DIR` in `.env`. After losing the install, add a new runner under Settings > Actions > Runners > New self-hosted runner, then run its `config.sh` with `--labels vanguard-galaxy` and recreate `.env` and the user unit.
