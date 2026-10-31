using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgiQoL.Core;

// Generic reflection dump of static game data (content registries) into the flat format tools/VersionDiff `data` compares:
// header line, then `<element key>\t<path>\t<value>` sorted ordinally.
internal static class DevDump
{
    // Registries Bootstrapper.Init fills. Depth = how many object levels below an element are expanded.
    private const string GameData =
        "Behaviour.Item.InventoryItemType.allItems:4,Behaviour.Crafting.CraftingRecipe.allRecipes:2,Behaviour.Equipment.Aspect.EquipAspect.allAspects:3," +
        "Behaviour.Hazard.LocalHazard.allHazards:3,Behaviour.Crew.SkilltreeNode.crewNodes:3,Behaviour.Crew.Skilltree.allTrees:3," +
        "Behaviour.Equipment.Builder.EquipmentBuilder.allBuilders:2,Behaviour.Item.Builder.ItemBuilder.allBuilders:3," +
        "Behaviour.Unit.SpaceShip.allShips:3,Behaviour.Unit.Drone.allDrones:3,Behaviour.Unit.CombatStationPart.allParts:3," +
        "Behaviour.Equipment.Module.AbstractModule.defaultModules:3,Behaviour.Unit.DefensiveTurret.allDefensiveTurrets:3," +
        "Behaviour.Crew.CrewType.allCrew:3,Behaviour.Dungeon.DungeonDefinition.allDefinitions:3";

    private const int DefaultDepth = 3;
    private const int MaxElements = 64;
    private static readonly string[] KeyNames = { "identifier", "id", "guid", "name" };
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly Assembly GameAssembly = typeof(Behaviour.Item.InventoryItemType).Assembly;

    public static void Run(string spec, bool gameData)
    {
        var entries = new List<string>();
        if (gameData) entries.AddRange(GameData.Split(','));
        if (!string.IsNullOrEmpty(spec)) entries.AddRange(spec.Split(','));
        foreach (var raw in entries)
        {
            var entry = raw.Trim();
            if (entry.Length == 0) continue;
            try { DumpOne(entry); }
            catch (Exception e) { Plugin.Log.LogError($"[Dev] dump of {entry} failed: {e}"); }
        }
    }

    private static void DumpOne(string entry)
    {
        int depth = DefaultDepth;
        int colon = entry.LastIndexOf(':');
        if (colon >= 0)
        {
            if (!int.TryParse(entry.Substring(colon + 1), NumberStyles.Integer, Inv, out depth) || depth < 0)
            {
                Plugin.Log.LogWarning($"[Dev] bad depth in dump entry '{entry}'");
                return;
            }
            entry = entry.Substring(0, colon);
        }
        int dot = entry.LastIndexOf('.');
        if (dot <= 0 || dot == entry.Length - 1)
        {
            Plugin.Log.LogWarning($"[Dev] dump entry '{entry}' is not TypeName.member");
            return;
        }
        string typeName = entry.Substring(0, dot), memberName = entry.Substring(dot + 1);
        var type = AccessTools.TypeByName(typeName);
        if (type == null)
        {
            Plugin.Log.LogWarning($"[Dev] dump type '{typeName}' not found");
            return;
        }

        object value;
        var field = FindField(type, memberName);
        if (field != null && field.IsStatic) value = field.GetValue(null);
        else
        {
            var getter = FindProperty(type, memberName)?.GetGetMethod(true);
            if (getter == null || !getter.IsStatic)
            {
                Plugin.Log.LogWarning($"[Dev] {type.FullName}.{memberName} is not a static field or property");
                return;
            }
            value = getter.Invoke(null, null);
        }

        var rows = new List<Row>();
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);
        int elements = 0;
        if (value is IDictionary dict)
        {
            int i = 0;
            foreach (DictionaryEntry de in dict) { AddElement(de.Value, de.Key, i++, depth, usedKeys, rows); elements++; }
        }
        else if (value is IEnumerable seq && value is not string && value is not Object)
        {
            int i = 0;
            foreach (var item in seq) { AddElement(item, null, i++, depth, usedKeys, rows); elements++; }
        }
        else
        {
            AddElement(value, null, 0, depth, usedKeys, rows);
            elements = 1;
        }

        rows.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.Key, b.Key);
            return c != 0 ? c : string.CompareOrdinal(a.Path, b.Path);
        });

        var sb = new StringBuilder();
        sb.Append("# ").Append(type.Name).Append('.').Append(memberName).Append(" game ").Append(Application.version).Append(" depth ").Append(depth).Append('\n');
        foreach (var r in rows) sb.Append(r.Key).Append('\t').Append(r.Path).Append('\t').Append(r.Value).Append('\n');

        string dir = Paths.ConfigPath + "/IgiQoL/dumps/" + Application.version;
        Directory.CreateDirectory(dir);
        string path = dir + "/" + type.Name + "." + memberName + ".txt";
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        Plugin.Log.LogInfo($"[Dev] dumped {elements} elements ({rows.Count} lines) -> {path}");
    }

    private struct Row
    {
        public string Key, Path, Value;
    }

    private sealed class RefComparer : IEqualityComparer<object>
    {
        public static readonly RefComparer Instance = new();
        public new bool Equals(object a, object b) => ReferenceEquals(a, b);
        public int GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
    }

    private sealed class Walk
    {
        public string Key;
        public List<Row> Rows;
        public readonly HashSet<object> OnPath = new(RefComparer.Instance);
        public readonly HashSet<GameObject> Expanded = new();

        public void Emit(string path, string value) => Rows.Add(new Row { Key = Key, Path = path, Value = Escape(value) });
    }

    private static void AddElement(object element, object dictKey, int index, int depth, HashSet<string> usedKeys, List<Row> rows)
    {
        string key = KeyOf(element) ?? (dictKey != null ? SafeToString(dictKey) : null) ?? index.ToString(Inv);
        string unique = key;
        for (int n = 2; !usedKeys.Add(unique); n++) unique = key + "#" + n;
        var walk = new Walk { Key = Escape(unique), Rows = rows };
        Visit(walk, element, "value", depth, root: true);
    }

    private static string KeyOf(object element)
    {
        if (element == null) return null;
        var t = element.GetType();
        foreach (var name in KeyNames)
        {
            try
            {
                var f = FindField(t, name);
                if (f != null && f.FieldType == typeof(string) && !f.IsStatic && f.GetValue(element) is string fs && fs.Length > 0) return fs;
                var p = FindProperty(t, name);
                if (p != null && p.PropertyType == typeof(string) && p.GetGetMethod(true) is { IsStatic: false } g && g.GetParameters().Length == 0 && g.Invoke(element, null) is string ps && ps.Length > 0) return ps;
            }
            catch (Exception) { }
        }
        return null;
    }

    private const BindingFlags AllDeclared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static FieldInfo FindField(Type t, string name)
    {
        for (; t != null; t = t.BaseType)
        {
            var f = t.GetField(name, AllDeclared);
            if (f != null) return f;
        }
        return null;
    }

    private static PropertyInfo FindProperty(Type t, string name)
    {
        for (; t != null; t = t.BaseType)
        {
            var p = t.GetProperty(name, AllDeclared);
            if (p != null) return p;
        }
        return null;
    }

    private static string SafeToString(object o)
    {
        try { return o.ToString(); }
        catch (Exception) { return null; }
    }

    private static string Join(string path, string name) => path.Length == 0 ? name : path + "." + name;

    // `root` elements that are objects put their members directly under the element, leaf roots use the path "value".
    private static void Visit(Walk w, object v, string path, int depth, bool root = false)
    {
        if (v == null) { w.Emit(path, "null"); return; }
        var t = v.GetType();

        if (v is Object uo)
        {
            if (uo == null) { w.Emit(path, "null"); return; }
            VisitUnity(w, uo, root ? "" : path, depth, root);
            return;
        }

        if (v is IntPtr || v is UIntPtr || v is Delegate) return;

        if (v is string || v is char || v is bool || t.IsEnum || t.IsPrimitive || v is decimal)
        {
            if (v is IFormattable f && !t.IsEnum) w.Emit(path, f.ToString(null, Inv));
            else w.Emit(path, v.ToString());
            return;
        }
        if (IsEngineValue(t))
        {
            string s;
            try { s = v is IFormattable fmt ? fmt.ToString("R", Inv) : v.ToString(); }
            catch (Exception) { s = v.ToString(); }
            w.Emit(path, s);
            return;
        }

        if (t.Assembly == typeof(string).Assembly || t.Assembly.GetName().Name.StartsWith("System", StringComparison.Ordinal))
        {
            if (v is IEnumerable) { VisitCollection(w, (IEnumerable)v, path, depth); return; }
            w.Emit(path, t.IsValueType ? (v is IFormattable bf ? bf.ToString(null, Inv) : v.ToString()) : "<" + TypeLabel(t) + ">");
            return;
        }

        if (v is IEnumerable seq)
        {
            VisitCollection(w, seq, path, depth);
            return;
        }

        if (depth <= 0 && !root) { w.Emit(path, "<" + TypeLabel(t) + ">"); return; }
        if (t.IsValueType) { VisitFields(w, v, t, root ? "" : path, depth); return; }
        if (!w.OnPath.Add(v)) { w.Emit(path, "<cycle " + TypeLabel(t) + ">"); return; }
        try { VisitFields(w, v, t, root ? "" : path, depth); }
        finally { w.OnPath.Remove(v); }
    }

    private static void VisitCollection(Walk w, IEnumerable seq, string path, int depth)
    {
        if (!w.OnPath.Add(seq)) { w.Emit(path, "<cycle " + TypeLabel(seq.GetType()) + ">"); return; }
        try
        {
            var items = new List<KeyValuePair<string, object>>();
            int count = -1;
            if (seq is ICollection c) count = c.Count;
            int seen = 0;
            if (seq is IDictionary d)
            {
                foreach (DictionaryEntry de in d)
                {
                    if (seen++ >= MaxElements) break;
                    items.Add(new KeyValuePair<string, object>(SafeToString(de.Key) ?? seen.ToString(Inv), de.Value));
                }
            }
            else
            {
                foreach (var item in seq)
                {
                    if (seen >= MaxElements) { break; }
                    items.Add(new KeyValuePair<string, object>(seen.ToString(Inv), item));
                    seen++;
                }
            }
            if (count < 0) count = seen;
            w.Emit(Join(path, "Count"), count.ToString(Inv));
            foreach (var kv in items) Visit(w, kv.Value, path + "[" + kv.Key + "]", depth);
        }
        catch (Exception e) { w.Emit(path, "<error " + e.GetType().Name + ">"); }
        finally { w.OnPath.Remove(seq); }
    }

    private static void VisitFields(Walk w, object v, Type t, string path, int depth)
    {
        var members = new List<KeyValuePair<string, FieldInfo>>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var bt = t; bt != null && bt != typeof(object) && bt != typeof(ValueType) && !IsEngineAssembly(bt.Assembly); bt = bt.BaseType)
        {
            foreach (var f in bt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.FieldType.IsPointer || typeof(Delegate).IsAssignableFrom(f.FieldType)) continue;
                string name = DisplayName(f.Name);
                if (!names.Add(name)) name = bt.Name + "." + name;
                members.Add(new KeyValuePair<string, FieldInfo>(name, f));
            }
        }
        members.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        foreach (var m in members)
        {
            string p = Join(path, m.Key);
            object fv;
            try { fv = m.Value.GetValue(v); }
            catch (Exception e) { w.Emit(p, "<error " + e.GetType().Name + ">"); continue; }
            try { Visit(w, fv, p, depth - 1); }
            catch (Exception e) { w.Emit(p, "<error " + e.GetType().Name + ">"); }
        }
    }

    private static void VisitUnity(Walk w, Object uo, string path, int depth, bool root)
    {
        var t = uo.GetType();
        string label = path;
        GameObject go = uo as GameObject ?? (uo as Component)?.gameObject;
        bool expandable = uo is GameObject || t.Assembly == GameAssembly;
        if (!expandable || (depth <= 0 && !root))
        {
            w.Emit(root ? "value" : path, "<" + t.Name + ":" + uo.name + ">");
            return;
        }
        if (!w.OnPath.Add(uo)) { w.Emit(root ? "value" : path, "<cycle " + t.Name + ">"); return; }
        try
        {
            if (go != null && !w.Expanded.Add(go)) { w.Emit(root ? "value" : path, "<" + t.Name + ":" + uo.name + ">"); return; }
            if (uo is not GameObject) VisitFields(w, uo, t, label, depth);
            if (go == null) return;
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null || c == uo || c.GetType().Assembly != GameAssembly) continue;
                if (!w.OnPath.Add(c)) continue;
                try { VisitFields(w, c, c.GetType(), Join(label, "@" + c.GetType().Name), depth); }
                finally { w.OnPath.Remove(c); }
            }
        }
        finally { w.OnPath.Remove(uo); }
    }

    private static bool IsEngineAssembly(Assembly a)
    {
        string n = a.GetName().Name;
        return n.StartsWith("UnityEngine", StringComparison.Ordinal) || n == "mscorlib" || n.StartsWith("System", StringComparison.Ordinal);
    }

    // Vector*, Color, Rect, Quaternion & co print as one value instead of their fields.
    private static bool IsEngineValue(Type t) =>
        t.IsValueType && t.Assembly.GetName().Name.StartsWith("UnityEngine", StringComparison.Ordinal) && typeof(IFormattable).IsAssignableFrom(t);

    private static string DisplayName(string field)
    {
        if (field.Length > 0 && field[0] == '<')
        {
            int end = field.IndexOf('>');
            if (end > 1) return field.Substring(1, end - 1);
        }
        return field;
    }

    private static string TypeLabel(Type t)
    {
        if (!t.IsGenericType) return t.Name;
        var sb = new StringBuilder(t.Name.Substring(0, t.Name.IndexOf('`'))).Append('<');
        var args = t.GetGenericArguments();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(TypeLabel(args[i]));
        }
        return sb.Append('>').ToString();
    }

    private static string Escape(string s) =>
        s.IndexOfAny(new[] { '\t', '\n', '\r' }) < 0 ? s : s.Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
}
