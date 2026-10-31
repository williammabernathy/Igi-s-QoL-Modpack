using BepInEx.Configuration;

namespace IgiQoL.Core;

// Display text for a config entry in the mod window, carried as a ConfigDescription tag.
// The config key stays stable (it is what igi.qol.cfg stores); Label and Choices are what players read.
public sealed class SettingInfo
{
    public readonly string Label;
    public readonly string[] Choices;

    private SettingInfo(string label, string[] choices)
    {
        Label = label;
        Choices = choices;
    }

    // choices: display names for an enum entry's values, in declaration order.
    public static ConfigDescription Describe(string label, string help, params string[] choices) =>
        new(help, null, new SettingInfo(label, choices.Length > 0 ? choices : null));

    // range: BepInEx clamps values outside it, from the cfg file and from the mod window.
    public static ConfigDescription Describe(string label, string help, AcceptableValueBase range) =>
        new(help, range, new SettingInfo(label, null));

    public static SettingInfo Of(ConfigEntryBase entry)
    {
        var tags = entry.Description?.Tags;
        if (tags == null) return null;
        foreach (var t in tags)
            if (t is SettingInfo info) return info;
        return null;
    }
}
