using System.Collections.Generic;
using Behaviour.Item;
using Behaviour.Mining;
using IgiQoL.UI;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.OrePreference;

// The preferred-ore rows on the ECHO tab: mode, the chosen ores, and a dropdown to add one.
public static class OrePreferenceSection
{
    public static void Build(RectTransform content)
    {
        Kit.Muted(content, "Prefer: asteroids with these ores are mined first, and ECHO picks a mining location in the system that still has them; with none left anywhere, everything is mined as usual. Only: other asteroids are left alone, and ECHO moves on to its next activity when none of these ores is left. Applies to ECHO, to your ships' own asteroid picks and to their mining drones. Core ores count once you have the Basic Scanning mining skill, which is when the game names them.");

        var modeRow = Kit.Row(content, 6f, 26f);
        Kit.Label(modeRow, "Preferred ores");
        Kit.Dropdown(modeRow, OrePreference.ModeNames, (int)OrePreference.Mode, i => Edit((OreMode)i, null), 240f);

        var chosen = new List<string>(OrePreference.Ores);
        foreach (var id in chosen)
        {
            string ore = id;
            var row = Kit.Row(content, 6f, 26f);
            var label = Kit.Label(row, InventoryItemType.TryGet(ore, out var type) ? Name(type) : ore + " (not in this game version)");
            if (type != null)
            {
                label.color = type.rarity.GetColor();
                var data = type.GetComponent<OreItemData>();
                if (data != null) Kit.Tooltip(label, Name(type), data.GetDescription());
            }
            Kit.Tooltip(Kit.TextTile(row, "x", 22f, () => Edit(OrePreference.Mode, l => l.Remove(ore))).targetGraphic, null, "Remove from the list");
        }
        if (chosen.Count == 0) Kit.Muted(content, "No ores chosen yet, so nothing changes.");

        var missing = new List<OreItemData>();
        foreach (var ore in OrePreference.KnownOres())
            if (!chosen.Contains(ore.item.identifier)) missing.Add(ore);
        if (missing.Count == 0) return;
        var names = new List<string>(missing.Count);
        foreach (var ore in missing) names.Add(Name(ore.item));
        var addRow = Kit.Row(content, 6f, 26f);
        Kit.Label(addRow, "Add an ore");
        var choice = Kit.Dropdown(addRow, names, 0, _ => { }, 240f);
        Kit.TextButton(addRow, "Add", () =>
        {
            int pick = choice.GetIndex();
            if (pick < 0 || pick >= missing.Count) return;
            string id = missing[pick].item.identifier;
            Edit(OrePreference.Mode == OreMode.Off ? OreMode.Prefer : OrePreference.Mode, l => l.Add(id));
        }, 60f);
    }

    // Ore names repeat across levels, so the level goes with the name.
    public static string Name(InventoryItemType ore) => Translation.Translate(ore.displayName) + " (level " + ore.itemLevel + ")";

    private static void Edit(OreMode mode, System.Action<List<string>> change)
    {
        var list = new List<string>(OrePreference.Ores);
        change?.Invoke(list);
        OrePreference.Set(mode, list);
        ModWindow.Show("echo");
    }
}
