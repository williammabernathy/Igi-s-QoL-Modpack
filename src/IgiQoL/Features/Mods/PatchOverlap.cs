using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace IgiQoL.Features.Mods;

// Game methods that Harmony patches from more than one mod, and what each mod does to them.
internal static class PatchOverlap
{
    internal sealed class Shared
    {
        public string Method;
        // A mod rewrites the method (transpiler, IL manipulator) or has a prefix that can skip the original.
        public bool Risky;
        public readonly List<string> Mods = new();
    }

    private sealed class Use
    {
        public readonly List<string> Kinds = new();
        public readonly List<string> Features = new();
        public bool CanSkip, Rewrites;
    }

    internal static List<Shared> Find()
    {
        // Patches are attributed by the assembly that holds the patch method: Harmony ids can be random
        // (Harmony.CreateAndPatchAll without an id) or unrelated to the plugin GUID.
        var plugins = new Dictionary<Assembly, string>();
        // A DLL with several [BepInPlugin] classes is one entry named after all of them.
        foreach (var info in Chainloader.PluginInfos.Values)
        {
            if (info.Instance == null) continue;
            var assembly = info.Instance.GetType().Assembly;
            plugins[assembly] = plugins.TryGetValue(assembly, out var known) ? known + ", " + info.Metadata.Name : info.Metadata.Name;
        }

        var result = new List<Shared>();
        foreach (var method in Harmony.GetAllPatchedMethods())
        {
            var patches = Harmony.GetPatchInfo(method);
            if (patches == null) continue;
            var mods = new Dictionary<string, Use>();
            Add(mods, plugins, patches.Prefixes, "prefix");
            Add(mods, plugins, patches.Postfixes, "postfix");
            Add(mods, plugins, patches.Transpilers, "transpiler");
            Add(mods, plugins, patches.Finalizers, "finalizer");
            Add(mods, plugins, patches.ILManipulators, "IL manipulator");
            if (mods.Count < 2) continue;

            var shared = new Shared { Method = (method.DeclaringType != null ? method.DeclaringType.Name + "." : "") + method.Name };
            foreach (var kv in mods)
            {
                var use = kv.Value;
                shared.Risky |= use.CanSkip || use.Rewrites;
                string name = use.Features.Count > 0 ? kv.Key + " (" + string.Join(", ", use.Features) + ")" : kv.Key;
                shared.Mods.Add(name + ": " + string.Join(", ", use.Kinds) + (use.CanSkip ? " that can skip the game's code" : ""));
            }
            result.Add(shared);
        }
        result.Sort((a, b) => a.Risky != b.Risky ? (a.Risky ? -1 : 1) : string.CompareOrdinal(a.Method, b.Method));
        return result;
    }

    private static void Add(Dictionary<string, Use> mods, Dictionary<Assembly, string> plugins, IEnumerable<Patch> list, string kind)
    {
        foreach (var patch in list)
        {
            var assembly = patch.PatchMethod.DeclaringType?.Assembly;
            string mod = assembly != null && plugins.TryGetValue(assembly, out var name) ? name : patch.owner;
            if (!mods.TryGetValue(mod, out var use)) mods[mod] = use = new Use();
            if (!use.Kinds.Contains(kind)) use.Kinds.Add(kind);
            // This modpack's Harmony ids are "igi.qol.<feature id>".
            if (assembly == typeof(Plugin).Assembly && patch.owner.StartsWith("igi.qol."))
            {
                string feature = patch.owner.Substring("igi.qol.".Length);
                if (!use.Features.Contains(feature)) use.Features.Add(feature);
            }
            if (kind == "prefix" && patch.PatchMethod.ReturnType == typeof(bool)) use.CanSkip = true;
            if (kind == "transpiler" || kind == "IL manipulator") use.Rewrites = true;
        }
    }
}
