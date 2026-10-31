using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using IgiQoL.Core;
using TMPro;
using UnityEngine;

namespace IgiQoL.UI;

// One row per BepInEx config entry of a section, with a control chosen by the entry type.
public static class SettingRows
{
    public enum Rows { All, EnabledOnly, Options }

    public const string EnabledLabel = "Feature on (applies after a game restart)";

    public static void AddSection(RectTransform column, Feature feature) => AddSection(column, feature.Title, feature.Description);

    public static void AddSection(RectTransform column, string title, string description)
    {
        // A gap above each heading so a section's rows don't run into the next feature's title.
        Kit.Gap(column, 6f);
        Kit.Heading(column, title);
        Kit.Muted(column, description);
    }

    public static int AddEntries(RectTransform column, string section, Rows rows = Rows.All)
    {
        int count = 0;
        foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> kv in Plugin.Instance.Config)
        {
            if (kv.Key.Section != section) continue;
            bool enabled = kv.Key.Key == "Enabled";
            if (rows == Rows.EnabledOnly && !enabled) continue;
            if (rows == Rows.Options && enabled) continue;
            AddEntry(column, kv.Value, enabled);
            count++;
        }
        return count;
    }

    private static void AddEntry(RectTransform column, ConfigEntryBase entry, bool enabled)
    {
        var info = SettingInfo.Of(entry);
        string label = enabled ? EnabledLabel : info?.Label ?? Pretty(entry.Definition.Key);
        string help = enabled ? "Turns this whole feature on or off. The game only checks this when it starts." : entry.Description?.Description ?? "";
        AddEntry(column, entry, label, help, info);
    }

    // One row for any plugin's entry. info (optional) supplies display names for an enum's values.
    public static void AddEntry(RectTransform column, ConfigEntryBase entry, string label, string help, SettingInfo info = null)
    {
        var row = Kit.Row(column);
        var text = Kit.Label(row, label);
        if (help.Length > 0) Kit.Tooltip(text, label, help);

        var type = entry.SettingType;
        if (type == typeof(bool))
        {
            Kit.Toggle(row, null, (bool)entry.BoxedValue, v => entry.BoxedValue = v);
        }
        // Flags enums can hold several values at once, which a dropdown can't show; they get the text field.
        else if (type.IsEnum && Enum.GetNames(type).Length <= 12 && !type.IsDefined(typeof(FlagsAttribute), false))
        {
            var names = Enum.GetNames(type);
            int index = Array.IndexOf(names, TomlTypeConverter.ConvertToString(entry.BoxedValue, type));
            Kit.Dropdown(row, ChoiceNames(names, info), index < 0 ? 0 : index, i => entry.BoxedValue = Enum.GetValues(type).GetValue(i), 200f);
        }
        else
        {
            var contentType = type == typeof(int) ? TMP_InputField.ContentType.IntegerNumber
                : type == typeof(float) || type == typeof(double) ? TMP_InputField.ContentType.DecimalNumber
                : TMP_InputField.ContentType.Standard;
            TMP_InputField field = null;
            field = Kit.Input(row, entry.GetSerializedValue(), contentType, value =>
            {
                try { entry.BoxedValue = TomlTypeConverter.ConvertToValue(value, type); }
                catch (Exception) { }
                field.SetTextWithoutNotify(entry.GetSerializedValue());
            }, 200f);
        }
    }

    private static string[] ChoiceNames(string[] names, SettingInfo info)
    {
        if (info?.Choices != null && info.Choices.Length == names.Length) return info.Choices;
        var result = new string[names.Length];
        for (int i = 0; i < names.Length; i++) result[i] = Pretty(names[i]);
        return result;
    }

    // "ArmourPiercing" -> "Armour piercing", for names that have no display text of their own.
    public static string Pretty(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                sb.Append(' ').Append(char.ToLowerInvariant(c));
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
