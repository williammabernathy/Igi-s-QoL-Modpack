# VersionDiff

VersionDiff compares two versions of Vanguard Galaxy from a mod's point of view. It has two commands:

- `patches` tells you which game methods your Harmony patches target have changed or disappeared, and which game members your compiled mod uses that no longer exist.
- `data` turns two data dumps from Igi's QoL Modpack dev harness into a changelog of items, ships, recipes and other content.

Neither command starts the game. `patches` reads DLLs on disk and takes from a fraction of a second to a few seconds.

## Requirements

- The .NET 8 SDK. The tool runs on Windows, Linux and macOS.
- Copies of the game's DLLs for each version you want to compare (see the next section). The game only keeps the current version, so a version you never saved can't be compared later.

Run it from the repo root:

```
dotnet run --project tools/VersionDiff -- patches
```

`dotnet run` builds the tool the first time. Anything after `--` goes to the tool.

## Game Version Snapshots

Every build of a mod in this repo (and of a mod made from the `templates/VgMod` template) runs the `SnapshotGameAssemblies` target before compiling. It copies the game DLLs the mod compiles against into:

```
game-versions/<game version>-<first 8 hex digits of the SHA-256 of Assembly-CSharp.dll>/
```

For example, `game-versions/0.8.3.5-4d10550b/` holds `Assembly-CSharp.dll`, `UnityEngine*.dll`, `Unity.InputSystem.dll`, `Unity.TextMeshPro.dll` (about 17 MB) and `managed-dir.txt`. The last file records the path of the game's `Managed` folder, which the tool uses to find the .NET framework DLLs and `BepInEx/core/0Harmony.dll`.

The game version comes from the game's `globalgamemanagers` file, and the hash comes from the DLL itself. A hotfix that keeps the version number therefore still gets its own folder, and a folder never mixes two versions. Because the copy runs before compiling, a build that fails against a new game version still saves that version.

The snapshots are the game's copyrighted code. `game-versions/` is in `.gitignore`, so don't commit or share them.

## `patches`

```
VersionDiff patches [--old <dir>] [--new <dir>] [--mod <dll>]... [--out <file>]
```

| Option | Meaning | Default |
|---|---|---|
| `--old <dir>` | The version your mod was written for: a snapshot folder or any folder with the game's DLLs | The newest snapshot that isn't `--new` |
| `--new <dir>` | The version to check against | The installed game if its `Assembly-CSharp.dll` matches no snapshot yet, otherwise the newest snapshot |
| `--mod <dll>` | A compiled mod to check. Repeat it for several mods | `src/IgiQoL/bin/Release/IgiQoL.dll` in this repo; required anywhere else |
| `--out <file>` | Write the Markdown report to a file | Print it to the console |

The defaults look for the nearest folder above the current directory that holds `game-versions/` or this repo's `VanguardGalaxyMods.sln`. Snapshots are ordered by game version, then by folder date.

If there's only one version on disk, the tool runs in manifest mode. It lists every target and checks it against that version, without diffs. This is also a quick check that the tool reads your mod's patches correctly.

CI runs it in manifest mode, because a fresh checkout has no `game-versions/`. The command `patches --new "$GAME_DIR/VanguardGalaxy_Data/Managed" --mod src/IgiQoL/bin/Release/IgiQoL.dll` checks the freshly built DLL against the installed game, and the workflow fails on exit code 1 ([docs/ci.md](../../docs/ci.md)).

### After a Game Update

1. Don't rebuild first. Run `dotnet run --project tools/VersionDiff -- patches --out patch-report.md`. This compares the old snapshot against the freshly installed game. The mod DLL is still the one built against the old version, so the reference check shows exactly what the shipped DLL would hit.
2. Read "Needs attention" and the diffs below it. Fix the patches.
3. Build. That saves the new version as a snapshot. Running the tool again now compares the two newest snapshots.
4. Run the mod's self-checks in the game. A method body that changed in a way the diff doesn't make obvious still needs a test in play.

You can rebuild first if you prefer. The target diffs come out the same. The reference check then runs against the newly built DLL, and the compiler has already reported those errors.

### What It Checks

Targets: the tool reads every `[HarmonyPatch]` attribute in the mod with Mono.Cecil and merges class-level and method-level attributes the way Harmony does. It understands target types, method names, argument types, `ArgumentType` (ref, out, pointer) and the `MethodType`s `Normal`, `Getter`, `Setter`, `Constructor` and `StaticConstructor`. Like Harmony, it only looks at members declared on the target type itself.

For each target, the tool compares the old and the new version on these points:

- Signature: whether the method is static, its return type, its parameter types and its parameter names. Names count because Harmony passes arguments to your patch by name.
- Body: both versions are decompiled with ICSharpCode.Decompiler (the engine behind ILSpy), and the text after the signature is compared. Lambdas, local functions and iterator bodies decompile into the method, so changes there count too. Access modifiers don't count, so a publicized DLL compares equal to the original.

Patch parameters: every parameter of your prefix, postfix or finalizer must match a parameter name of the target in the new version. For `___name` injections, it must match a field on the type or a base type. Harmony's own parameters (`__instance`, `__result`, `__state`, `__args`, `__originalMethod`, `__runOriginal`, `__exception`, `__0`, `__1`, ...) are skipped.

References: every type, field and method of the game that the compiled mod uses must still exist in the new version. The report lists each missing one with the mod methods that use it. Framework assemblies (`mscorlib`, `netstandard`, `System.*`) are skipped.

String lookups: calls into `AccessTools` and `Traverse` that name a member in a string are resolved in both versions, including base types. Examples are `AccessTools.Method(typeof(X), "Name")`, `AccessTools.FieldRefAccess<TMP_Text, float>("m_fontSize")` and `Traverse.Create(pane).Field("mission")`. The compiler can't check these, and they fail at runtime.

### Statuses

| Status | Meaning | What to do |
|---|---|---|
| `unchanged` | Same signature, same decompiled body | Nothing |
| `body changed` | Same signature, different body. The report has a diff | Read the diff. Check that your patch's assumptions still hold, especially for transpilers and prefixes that replace the method |
| `signature changed` | The return type, the parameter types or names, or whether the method is static changed. The report shows both signatures and the diff | Update the patch's parameters and the attribute's argument types |
| `missing` | Not declared on the target type in the new version. The note says whether it moved to a base type ("now inherited from X") | Find where the logic went (the report prints the old source and any same-name members left) and retarget |
| `ambiguous` | Several overloads match a patch without argument types; Harmony would refuse to patch | Add argument types to the `[HarmonyPatch]` attribute |
| `new` | Found in the new version but not the old one | Usually a target you added after the old snapshot |
| `resolved` | Manifest mode only: found in the single version | Nothing |
| `dynamic` | The patch class picks its target in `TargetMethod()`/`TargetMethods()` | Check it by hand |
| `unsupported` | A `MethodType` the tool doesn't handle | Check it by hand |

String lookups get `ok`, `missing`, `type changed` (a `FieldRefAccess` field whose type no longer matches), `dynamic` (the name isn't a constant, for example `AccessTools.TypeByName(variable)`) or `n/a` (the assembly isn't in the version folder).

### The Report

Markdown, in this order:

1. The two versions and the mod DLLs.
2. Summary: counts per status, parameter problems, unresolved references, broken string lookups.
3. Needs attention: every target that isn't `unchanged` or `resolved`, with the patch methods that use it.
4. Changed targets: a unified diff of each changed method.
5. Missing and ambiguous targets: the old source and what's left with the same name.
6. Patch parameter problems, unresolved references and string lookups.
7. All targets.

### Exit Codes

| Code | Meaning |
|---|---|
| 0 | Nothing is broken. `body changed` and `signature changed` alone still give 0, since the patch may well still work |
| 1 | At least one target is missing, ambiguous or unsupported, a patch parameter doesn't match, a reference is unresolved or a string lookup is broken |
| 2 | Bad arguments or missing files, including a `0Harmony.dll` the tool can't find next to the game (see Limits) |

### Limits

- Only the patched method itself is compared. If the game changes a method that yours depends on but doesn't patch, the reference check only notices when that method disappears or its signature changes.
- A getter or setter target decompiles as the whole property, so a change to the other accessor shows up too.
- The type behind `Traverse.Create(x)` is guessed from the static type of `x`, so a lookup on a variable typed as a base class is checked against that base class.
- `TargetMethod()`/`TargetMethods()` patches and patches applied with `harmony.Patch(...)` in code aren't analysed.
- Enum members that the mod stores by name (item categories, rarities and compartment types in the config) compile to constants, so a renamed member passes `patches`. After a game update, run `VersionDiff data` on the old and new dumps to see category and rarity names change.
- A `Traverse` held in a variable (`var t = Traverse.Create(x); t.Property("name")`) is reported as `dynamic` and never fails. Check those by hand.
- `unsupported` targets (no declaring type or method name to resolve) fail the run. So does a missing `0Harmony.dll`, because without it Cecil can't decode the mod's `MethodType` arguments and every such target would look unsupported. The tool looks for `BepInEx/core/0Harmony.dll` two folders above the game's `Managed` folder. That is the `Managed` folder recorded in `managed-dir.txt` by the newest `game-versions/` snapshot, or else the `--new` folder.

## `data`

```
VersionDiff data <old dump dir> <new dump dir> [--out <file>]
```

The input is two folders of dumps written by Igi's QoL Modpack in the game. `[Dev] DumpGameData = true` writes the game's 15 content registries, and `[Dev] DumpStatics` writes any static members you list. They land in `BepInEx/config/IgiQoL/dumps/<game version>/`. Each file starts with a `# <Type>.<member> game <version> depth <n>` line. After that comes one sorted value per line, in the form `element key<TAB>field path<TAB>value`.

Files with the same name in both folders are compared. The report lists, per file:

- elements added and removed (an item, ship or recipe by its identifier);
- for each changed element, each changed value as `path: old → new`, with `(added)` and `(removed)` for paths that appear or disappear.

The top of the report has the totals and the files that exist on one side only. Two identical folders give "No differences". The exit code is 0, or 2 for bad arguments.

You need a dump from each version, so dump before the game updates. Once Steam has replaced the game you can't dump the old version any more.

## Using It for Your Own Mod

A mod made from `templates/VgMod` writes snapshots to its own `game-versions/` folder. Run the tool from the mod's folder and pass the DLL:

```
dotnet run --project <path to this repo>/tools/VersionDiff -- patches --mod bin/Release/MyMod.dll
```

`--old` and `--new` default to that project's snapshots and the installed game. Outside a folder with `game-versions/`, pass all three options.
