# Hello Vanguard

The smallest working plugin in this repo, and the one that proved BepInEx loads under Proton. GUID `local.hellovanguard`, deployed to `BepInEx/plugins/HelloVanguard/`.

It logs two lines to `BepInEx/LogOutput.log`:

```
[Info   :Hello Vanguard] Hello Vanguard loaded. Items already registered at plugin load: 0
[Info   :Hello Vanguard] InventoryItemType.LoadAll finished: 393 items
```

The first line comes from `Awake`, the second from a postfix on `InventoryItemType.LoadAll`. The gap between 0 and 393 is why content mods edit the registries from a `LoadAll` postfix and not from `Awake` ([modding-reference.md](../../docs/modding-reference.md#startup-timing)).

To start another mod in this repo, copy this folder. Rename the folder, the `.csproj`, `AssemblyName` and the plugin GUID, then run `dotnet sln add src/<Name>/<Name>.csproj`. For a mod in its own repo, use the [starter template](../../docs/template.md) instead.

It shows up on the modpack's Mods tab as "Hello Vanguard 0.1.0" with no settings. Delete `BepInEx/plugins/HelloVanguard/` from the game folder if you don't want it loaded.
