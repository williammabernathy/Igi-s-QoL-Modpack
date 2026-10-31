using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using Mono.Cecil;
using Mono.Cecil.Cil;
using AssemblyNameReference = Mono.Cecil.AssemblyNameReference;

namespace VersionDiff;

// One folder of game assemblies, plus the Cecil resolver and decompilers that read it.
internal sealed class GameVersion
{
    public readonly string Dir;
    public readonly string Label;
    private readonly string fallback;
    private readonly DefaultAssemblyResolver resolver = new();
    private readonly Dictionary<string, ModuleDefinition> modules = new();
    private readonly Dictionary<string, CSharpDecompiler> decompilers = new();

    private static readonly DecompilerSettings Settings = new() { ThrowOnAssemblyResolveErrors = false, UseDebugSymbols = false };

    public GameVersion(string dir, string fallback, string label)
    {
        Dir = dir;
        Label = label;
        this.fallback = fallback;
        // Cecil's default search path includes "." and "bin" relative to the working directory; that would pick up stray DLLs.
        resolver.RemoveSearchDirectory(".");
        resolver.RemoveSearchDirectory("bin");
        resolver.AddSearchDirectory(dir);
        if (fallback != null) resolver.AddSearchDirectory(fallback);
        // Cecil resolves the HarmonyLib enums in the mod's attribute arguments, so 0Harmony.dll must be findable.
        var harmonyDir = fallback == null ? null : Path.GetFullPath(Path.Combine(fallback, "..", "..", "BepInEx", "core"));
        if (harmonyDir != null && Directory.Exists(harmonyDir)) resolver.AddSearchDirectory(harmonyDir);
    }

    public bool InDir(string assemblyName) => File.Exists(Path.Combine(Dir, assemblyName + ".dll"));

    public ModuleDefinition ReadMod(string path) =>
        ModuleDefinition.ReadModule(path, new ReaderParameters { AssemblyResolver = resolver });

    public ModuleDefinition Module(string assemblyName)
    {
        if (modules.TryGetValue(assemblyName, out var m)) return m;
        try { m = resolver.Resolve(new AssemblyNameReference(assemblyName, null)).MainModule; }
        catch (AssemblyResolutionException) { m = null; }
        return modules[assemblyName] = m;
    }

    public TypeDefinition FindType(TypeReference t)
    {
        t = t.GetElementType();
        var scope = PatchManifest.ScopeName(t);
        return scope == null ? null : Module(scope)?.GetType(t.FullName);
    }

    // AccessTools.TypeByName searches every loaded assembly; here that is every assembly in the folder.
    public TypeDefinition FindTypeByName(string name)
    {
        name = name.Replace('+', '/');
        foreach (var file in Directory.EnumerateFiles(Dir, "*.dll").OrderBy(f => f, StringComparer.Ordinal))
        {
            var m = Module(Path.GetFileNameWithoutExtension(file));
            var t = m?.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    public string Decompile(MethodDefinition m)
    {
        var path = m.Module.FileName;
        if (!decompilers.TryGetValue(path, out var d))
        {
            string framework;
            using (var pe = new PEFile(path)) framework = pe.DetectTargetFrameworkId();
            var asm = new UniversalAssemblyResolver(path, false, framework);
            asm.AddSearchDirectory(Dir);
            if (fallback != null) asm.AddSearchDirectory(fallback);
            decompilers[path] = d = new CSharpDecompiler(path, asm, Settings);
        }
        return d.DecompileAsString(new[] { MetadataTokens.EntityHandle(m.MetadataToken.ToInt32()) }).Replace("\r\n", "\n");
    }
}

internal sealed class PatchRef
{
    public string Kind;
    public TypeDefinition Class;
    public MethodDefinition Method;
    public string Name => PatchManifest.Pretty(Class.FullName) + "." + Method.Name;
}

internal sealed class Target
{
    public TypeReference TypeRef;
    public string TypeName;
    public string Method;
    public int MType;
    public string[] ArgTypes;
    public string Unsupported;
    public bool Dynamic;
    public readonly List<PatchRef> PatchedBy = new();

    public string TypeFull => TypeRef != null ? PatchManifest.Pretty(TypeRef.GetElementType().FullName) : TypeName ?? "?";
    public string Key => $"{TypeFull}|{Method}|{MType}|{(ArgTypes == null ? "*" : string.Join(",", ArgTypes))}";
    public string Label => Dynamic ? TypeFull + " (TargetMethod)" : TypeFull + "::" + MethodDisplay
        + (ArgTypes == null ? "" : "(" + string.Join(", ", ArgTypes.Select(PatchManifest.Pretty)) + ")");
    private string MethodDisplay => MType switch { 1 => "get_" + Method, 2 => "set_" + Method, 3 => ".ctor", 4 => ".cctor", _ => Method };
}

internal sealed class Resolved
{
    public string Status;
    public MethodDefinition Method;
    public TypeDefinition Type;
    public string Note;
    public List<MethodDefinition> Overloads = new();
}

internal sealed class Lookup
{
    public string Site;
    public string Api;
    public string Name;
    public TypeReference TypeRef;
    public string TypeName;
    public string DynamicReason;
    public string ExpectedFieldType;
    public string Label => Api + "(" + (DynamicReason != null ? "?" : (TypeRef != null ? PatchManifest.Pretty(TypeRef.GetElementType().FullName) + ", " : TypeName != null && Name == null ? "" : TypeName != null ? TypeName + ", " : "") + (Name == null ? "" : "\"" + Name + "\"")) + ")";
}

internal sealed class Unresolved
{
    public string Kind;
    public string Name;
    public readonly SortedSet<string> Sites = new(StringComparer.Ordinal);
}

internal static class PatchManifest
{
    private static readonly string[] MethodTypes = { "Normal", "Getter", "Setter", "Constructor", "StaticConstructor", "Enumerator", "Async" };
    private static readonly HashSet<string> Conventional = new() { "Prefix", "Postfix", "Transpiler", "Finalizer" };
    private static readonly Dictionary<string, string> KindAttributes = new()
    {
        ["HarmonyLib.HarmonyPrefix"] = "Prefix", ["HarmonyLib.HarmonyPostfix"] = "Postfix",
        ["HarmonyLib.HarmonyTranspiler"] = "Transpiler", ["HarmonyLib.HarmonyFinalizer"] = "Finalizer",
        ["HarmonyLib.HarmonyReversePatch"] = "ReversePatch",
    };
    private static readonly HashSet<string> HelperNames = new() { "TargetMethod", "TargetMethods", "Prepare", "Cleanup" };

    public static string Pretty(string fullName) => fullName.Replace('/', '.');

    public static string MethodTypeName(int t) => t >= 0 && t < MethodTypes.Length ? MethodTypes[t] : "#" + t;

    public static string ScopeName(TypeReference t)
    {
        while (t.DeclaringType != null) t = t.DeclaringType;
        return t.Scope switch
        {
            AssemblyNameReference a => a.Name,
            ModuleDefinition m => m.Assembly.Name.Name,
            _ => null,
        };
    }

    public static bool IsFramework(string assembly) =>
        assembly == "mscorlib" || assembly == "netstandard" || assembly == "System" || assembly.StartsWith("System.", StringComparison.Ordinal);

    // Mirrors HarmonyMethod: class attributes give defaults, method attributes override field by field.
    private sealed class Info
    {
        public TypeReference Type;
        public string TypeName, Method;
        public int? MType;
        public TypeReference[] Args;
        public int[] Variations;

        public Info Clone() => (Info)MemberwiseClone();

        public void Merge(Info o)
        {
            if (o.Type != null) { Type = o.Type; TypeName = null; }
            if (o.TypeName != null) { TypeName = o.TypeName; Type = null; }
            Method = o.Method ?? Method;
            MType = o.MType ?? MType;
            Args = o.Args ?? Args;
            Variations = o.Variations ?? Variations;
        }
    }

    private static Info ParseAttribute(CustomAttribute a)
    {
        var info = new Info();
        var strings = new List<string>();
        foreach (var arg in a.ConstructorArguments)
        {
            var typeName = arg.Type.FullName;
            if (arg.Value is TypeReference t) info.Type = t;
            else if (arg.Value is string s) strings.Add(s);
            else if (typeName == "HarmonyLib.MethodType") info.MType = Convert.ToInt32(arg.Value);
            else if (arg.Value is CustomAttributeArgument[] arr)
            {
                if (typeName == "System.Type[]") info.Args = arr.Select(x => (TypeReference)x.Value).ToArray();
                else if (typeName == "HarmonyLib.ArgumentType[]") info.Variations = arr.Select(x => Convert.ToInt32(x.Value)).ToArray();
            }
        }
        if (info.Type != null) info.Method = strings.FirstOrDefault();
        else if (strings.Count >= 2) { info.TypeName = strings[0]; info.Method = strings[1]; }
        else if (strings.Count == 1) info.Method = strings[0];
        return info;
    }

    private static Info MergeAll(IEnumerable<CustomAttribute> attrs, Info start = null)
    {
        var info = start?.Clone() ?? new Info();
        foreach (var a in attrs.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch")) info.Merge(ParseAttribute(a));
        return info;
    }

    private static string PatchKind(MethodDefinition m)
    {
        foreach (var a in m.CustomAttributes)
            if (KindAttributes.TryGetValue(a.AttributeType.FullName, out var k)) return k;
        return Conventional.Contains(m.Name) ? m.Name : null;
    }

    public static List<Target> ReadTargets(ModuleDefinition mod)
    {
        var targets = new Dictionary<string, Target>();
        foreach (var type in mod.GetTypes())
        {
            bool HasPatchAttr(ICustomAttributeProvider p) => p.CustomAttributes.Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch");
            var classHasAttr = HasPatchAttr(type);
            var methods = type.Methods.Where(m => !m.IsConstructor && !HelperNames.Contains(m.Name)
                && (HasPatchAttr(m) || m.CustomAttributes.Any(a => KindAttributes.ContainsKey(a.AttributeType.FullName)) || (classHasAttr && Conventional.Contains(m.Name)))).ToList();
            if (methods.Count == 0 && !classHasAttr) continue;
            var classInfo = MergeAll(type.CustomAttributes);

            if (type.Methods.Any(m => m.Name == "TargetMethod" || m.Name == "TargetMethods"))
            {
                var dyn = new Target { TypeName = Pretty(type.FullName), Dynamic = true, Method = "TargetMethod(s)", MType = 0 };
                foreach (var m in methods) dyn.PatchedBy.Add(new PatchRef { Kind = PatchKind(m) ?? "?", Class = type, Method = m });
                targets[dyn.Key] = dyn;
                continue;
            }

            foreach (var m in methods)
            {
                var info = MergeAll(m.CustomAttributes, classInfo);
                var t = new Target { TypeRef = info.Type, TypeName = info.TypeName, Method = info.Method, MType = info.MType ?? 0 };
                if (t.TypeRef == null && t.TypeName == null) t.Unsupported = "no declaring type";
                else if (t.MType > 4) t.Unsupported = "unsupported MethodType " + MethodTypeName(t.MType);
                else if (t.Method == null && t.MType <= 2) t.Unsupported = "no method name";
                if (info.Args != null)
                {
                    t.ArgTypes = info.Args.Select((a, i) => a.FullName + (info.Variations != null && i < info.Variations.Length
                        ? info.Variations[i] switch { 1 or 2 => "&", 3 => "*", _ => "" } : "")).ToArray();
                }
                if (t.Unsupported != null) t.Method ??= "?";
                if (t.Unsupported != null && t.TypeRef == null && t.TypeName == null) t.TypeName = Pretty(type.FullName);
                var key = t.Key + (t.Unsupported != null ? "|" + type.FullName : "");
                if (!targets.TryGetValue(key, out var existing)) targets[key] = existing = t;
                existing.PatchedBy.Add(new PatchRef { Kind = PatchKind(m) ?? "?", Class = type, Method = m });
            }
        }
        return targets.Values.OrderBy(t => t.TypeFull, StringComparer.Ordinal).ThenBy(t => t.Method, StringComparer.Ordinal).ToList();
    }

    private static string ParamName(ParameterDefinition p) => p.ParameterType.FullName;

    // AccessTools.DeclaredMethod and friends: declared members only.
    public static Resolved Resolve(GameVersion v, Target t)
    {
        var type = t.TypeRef != null ? v.FindType(t.TypeRef) : v.FindTypeByName(t.TypeName);
        if (type == null) return new Resolved { Status = "missing", Note = "type not found" };
        var r = new Resolved { Type = type };

        IEnumerable<MethodDefinition> Candidates(TypeDefinition ty) => t.MType switch
        {
            1 => ty.Properties.Where(p => p.Name == t.Method).Select(p => p.GetMethod).Where(m => m != null),
            2 => ty.Properties.Where(p => p.Name == t.Method).Select(p => p.SetMethod).Where(m => m != null),
            3 => ty.Methods.Where(m => m.Name == ".ctor"),
            4 => ty.Methods.Where(m => m.Name == ".cctor"),
            _ => ty.Methods.Where(m => m.Name == t.Method),
        };
        // Constructors resolve with Type.EmptyTypes when no argument types are given.
        bool ArgsMatch(MethodDefinition m) => t.ArgTypes != null
            ? m.Parameters.Select(ParamName).SequenceEqual(t.ArgTypes)
            : t.MType < 3 || m.Parameters.Count == 0;

        var found = Candidates(type).Where(ArgsMatch).ToList();
        if (found.Count == 1) { r.Status = "found"; r.Method = found[0]; return r; }
        r.Overloads = t.MType == 0 ? type.Methods.Where(m => m.Name == t.Method).ToList() : Candidates(type).ToList();
        if (found.Count > 1) { r.Status = "ambiguous"; r.Note = found.Count + " overloads match; add argument types"; return r; }
        r.Status = "missing";
        r.Note = "not declared on " + Pretty(type.FullName);
        for (var bt = type.BaseType; bt != null;)
        {
            TypeDefinition b;
            try { b = bt.Resolve(); } catch (AssemblyResolutionException) { break; }
            if (b == null) break;
            if (Candidates(b).Any(ArgsMatch)) { r.Note = "now inherited from " + Pretty(b.FullName); break; }
            bt = b.BaseType;
        }
        return r;
    }

    public static string Signature(MethodDefinition m) =>
        (m.IsStatic ? "static " : "") + m.ReturnType.FullName + " (" + string.Join(", ", m.Parameters.Select(p => p.ParameterType.FullName + " " + p.Name)) + ")";

    private static readonly HashSet<string> Specials = new() { "__instance", "__result", "__state", "__args", "__originalMethod", "__runOriginal", "__exception" };

    public static List<string> CheckParameters(Target t, Resolved r)
    {
        var problems = new List<string>();
        foreach (var p in t.PatchedBy)
        {
            if (p.Kind == "Transpiler" || p.Kind == "ReversePatch") continue;
            foreach (var prm in p.Method.Parameters)
            {
                var n = prm.Name;
                if (Specials.Contains(n) || (n.StartsWith("__", StringComparison.Ordinal) && n.Length > 2 && n.Skip(2).All(char.IsDigit))) continue;
                if (n.StartsWith("___", StringComparison.Ordinal))
                {
                    if (!HasField(r.Type, n.Substring(3))) problems.Add($"{p.Name}: `{n}` matches no field on {Pretty(r.Type.FullName)} or its base types");
                }
                else if (!r.Method.Parameters.Any(x => x.Name == n))
                    problems.Add($"{p.Name}: parameter `{n}` matches no parameter of the target ({string.Join(", ", r.Method.Parameters.Select(x => x.Name))})");
            }
        }
        return problems;
    }

    private static bool HasField(TypeDefinition t, string name)
    {
        for (; t != null; t = t.BaseType == null ? null : SafeResolve(t.BaseType))
            if (t.Fields.Any(f => f.Name == name)) return true;
        return false;
    }

    private static TypeDefinition SafeResolve(TypeReference t)
    {
        try { return t.Resolve(); } catch (AssemblyResolutionException) { return null; }
    }

    // ---- string lookups -------------------------------------------------------------------------------

    public static string SiteName(MethodDefinition m)
    {
        var name = m.Name;
        var type = m.DeclaringType;
        while (type.DeclaringType != null)
        {
            var sm = System.Text.RegularExpressions.Regex.Match(type.Name, @"^<(.+)>d__\d+$");
            if (sm.Success) name = sm.Groups[1].Value;
            type = type.DeclaringType;
        }
        var lm = System.Text.RegularExpressions.Regex.Match(name, @"^<(.+)>[bgd]__");
        if (lm.Success) name = lm.Groups[1].Value;
        else if (name.StartsWith("<>", StringComparison.Ordinal)) name = "lambda";
        return type.Name + "::" + name;
    }

    private static bool IsGetTypeFromHandle(Instruction i) =>
        i.Operand is MethodReference m && m.Name == "GetTypeFromHandle" && m.DeclaringType.FullName == "System.Type";

    private static bool Barrier(Instruction i)
    {
        var c = i.OpCode.Code;
        if (c == Code.Call || c == Code.Callvirt || c == Code.Newobj) return !IsGetTypeFromHandle(i);
        return i.OpCode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch or FlowControl.Return or FlowControl.Throw;
    }

    private static TypeReference StaticType(MethodDefinition owner, Instruction i)
    {
        var body = owner.Body;
        switch (i.OpCode.Code)
        {
            case Code.Ldloc_0: return body.Variables.Count > 0 ? body.Variables[0].VariableType : null;
            case Code.Ldloc_1: return body.Variables.Count > 1 ? body.Variables[1].VariableType : null;
            case Code.Ldloc_2: return body.Variables.Count > 2 ? body.Variables[2].VariableType : null;
            case Code.Ldloc_3: return body.Variables.Count > 3 ? body.Variables[3].VariableType : null;
            case Code.Ldloc or Code.Ldloc_S or Code.Ldloca or Code.Ldloca_S: return (i.Operand as VariableDefinition)?.VariableType;
            case Code.Ldarg_0 or Code.Ldarg_1 or Code.Ldarg_2 or Code.Ldarg_3:
            {
                var idx = i.OpCode.Code - Code.Ldarg_0;
                if (owner.HasThis) return idx == 0 ? owner.DeclaringType : idx - 1 < owner.Parameters.Count ? owner.Parameters[idx - 1].ParameterType : null;
                return idx < owner.Parameters.Count ? owner.Parameters[idx].ParameterType : null;
            }
            case Code.Ldarg or Code.Ldarg_S or Code.Ldarga or Code.Ldarga_S:
                return i.Operand is ParameterDefinition pd ? pd.ParameterType : owner.DeclaringType;
            case Code.Ldfld or Code.Ldsfld or Code.Ldflda or Code.Ldsflda: return ((FieldReference)i.Operand).FieldType;
            case Code.Call or Code.Callvirt: return ((MethodReference)i.Operand).ReturnType;
            case Code.Castclass or Code.Isinst: return (TypeReference)i.Operand;
            case Code.Newobj: return ((MethodReference)i.Operand).DeclaringType;
            default: return null;
        }
    }

    public static List<Lookup> ReadLookups(ModuleDefinition mod)
    {
        var result = new List<Lookup>();
        foreach (var type in mod.GetTypes())
        foreach (var m in type.Methods)
        {
            if (!m.HasBody) continue;
            var ins = m.Body.Instructions;
            for (var c = 0; c < ins.Count; c++)
            {
                if (ins[c].OpCode.Code is not (Code.Call or Code.Callvirt)) continue;
                var mr = (MethodReference)ins[c].Operand;
                var owner = mr.DeclaringType.GetElementType().FullName;
                var isTools = owner == "HarmonyLib.AccessTools";
                var isTraverse = owner is "HarmonyLib.Traverse" or "HarmonyLib.Traverse`1";
                if (!isTools && !isTraverse) continue;
                var strIdx = mr.Parameters.ToList().FindIndex(p => p.ParameterType.FullName == "System.String");
                if (strIdx < 0) continue;

                var lk = new Lookup { Site = SiteName(m), Api = (isTools ? "AccessTools." : "Traverse.") + mr.Name };
                result.Add(lk);
                var onlyArg = mr.Parameters.Count == 1;
                var s = c - 1;
                if (onlyArg) { if (ins[s].OpCode.Code != Code.Ldstr) { lk.DynamicReason = "name is not a constant"; continue; } }
                else
                {
                    for (var n = 0; s >= 0 && n < 40 && ins[s].OpCode.Code != Code.Ldstr; s--, n++)
                        if (Barrier(ins[s])) { s = -1; break; }
                    if (s < 0 || ins[s].OpCode.Code != Code.Ldstr) { lk.DynamicReason = "name is not a constant"; continue; }
                }
                lk.Name = (string)ins[s].Operand;

                if (isTools && mr.Name == "TypeByName") { lk.TypeName = lk.Name; lk.Name = null; continue; }

                if (isTools)
                {
                    if (mr is GenericInstanceMethod g && mr.Name.Contains("FieldRef") && g.GenericArguments.Count >= 2)
                    {
                        lk.TypeRef = g.GenericArguments[0];
                        lk.ExpectedFieldType = g.GenericArguments[1].FullName;
                    }
                    else if (mr.Parameters[0].ParameterType.FullName == "System.Type")
                    {
                        if (s >= 2 && IsGetTypeFromHandle(ins[s - 1]) && ins[s - 2].Operand is TypeReference tr) lk.TypeRef = tr;
                        else lk.DynamicReason = "type is not a constant";
                    }
                    else if (lk.Name.Contains(':'))
                    {
                        var parts = lk.Name.Split(':');
                        lk.TypeName = parts[0];
                        lk.Name = parts[1];
                    }
                    else lk.DynamicReason = "no type for the name";
                }
                else
                {
                    // Traverse.Create(x).Field("name"): the owner type is the static type of x.
                    if (s >= 1 && ins[s - 1].Operand is MethodReference cr && cr.Name == "Create" && cr.DeclaringType.FullName == "HarmonyLib.Traverse")
                    {
                        var create = s - 1;
                        if (cr is GenericInstanceMethod cg) lk.TypeRef = cg.GenericArguments[0];
                        else if (cr.Parameters[0].ParameterType.FullName == "System.Type")
                        {
                            if (create >= 2 && IsGetTypeFromHandle(ins[create - 1]) && ins[create - 2].Operand is TypeReference tr) lk.TypeRef = tr;
                            else lk.DynamicReason = "type is not a constant";
                        }
                        else
                        {
                            var st = create >= 1 ? StaticType(m, ins[create - 1]) : null;
                            if (st == null || st.FullName == "System.Object" || st is GenericParameter) lk.DynamicReason = "static type of the Traverse target is unknown";
                            else lk.TypeRef = st;
                        }
                    }
                    else lk.DynamicReason = "Traverse is chained or held in a variable";
                }
            }
        }
        return result;
    }

    // Returns (status, detail). Statuses: ok, missing, type changed, dynamic, n/a.
    public static (string, string) CheckLookup(GameVersion v, Lookup l)
    {
        if (l.DynamicReason != null) return ("dynamic", l.DynamicReason);
        TypeDefinition type;
        if (l.TypeRef != null)
        {
            var scope = ScopeName(l.TypeRef.GetElementType());
            if (scope != null && !v.InDir(scope) && !IsFramework(scope) && v.Module(scope) == null) return ("n/a", "assembly " + scope + " not found");
            type = v.FindType(l.TypeRef);
            if (type == null) return ("missing", "type " + Pretty(l.TypeRef.FullName) + " not found");
        }
        else
        {
            type = v.FindTypeByName(l.TypeName);
            if (type == null) return ("missing", "type " + l.TypeName + " not found");
            if (l.Name == null) return ("ok", "type exists");
        }
        for (var t = type; t != null; t = t.BaseType == null ? null : SafeResolve(t.BaseType))
        {
            var f = t.Fields.FirstOrDefault(x => x.Name == l.Name);
            if (f != null)
            {
                if (l.ExpectedFieldType != null && f.FieldType.FullName != l.ExpectedFieldType)
                    return ("type changed", $"field is {f.FieldType.FullName}, mod expects {l.ExpectedFieldType}");
                return ("ok", "field on " + Pretty(t.FullName));
            }
            if (t.Properties.Any(x => x.Name == l.Name)) return ("ok", "property on " + Pretty(t.FullName));
            if (t.Methods.Any(x => x.Name == l.Name)) return ("ok", "method on " + Pretty(t.FullName));
        }
        return ("missing", $"no field, property or method `{l.Name}` on {Pretty(type.FullName)} or its base types");
    }

    // ---- reference check ------------------------------------------------------------------------------

    private static void Touch(TypeReference t, HashSet<string> keys)
    {
        while (t is TypeSpecification ts)
        {
            if (t is GenericInstanceType g) foreach (var a in g.GenericArguments) Touch(a, keys);
            t = ts.ElementType;
        }
        if (t != null && t is not GenericParameter) keys.Add(t.FullName);
    }

    private static void Touch(MemberReference m, HashSet<string> keys)
    {
        if (m is GenericInstanceMethod g) { Touch(g.ElementMethod, keys); foreach (var a in g.GenericArguments) Touch(a, keys); return; }
        keys.Add(m.FullName);
        Touch(m.DeclaringType, keys);
    }

    public static List<Unresolved> CheckReferences(ModuleDefinition mod, GameVersion v)
    {
        bool Checked(TypeReference t)
        {
            var scope = ScopeName(t.GetElementType());
            return scope != null && !IsFramework(scope) && v.InDir(scope);
        }

        var bad = new Dictionary<string, Unresolved>();
        foreach (var tr in mod.GetTypeReferences())
        {
            if (!Checked(tr)) continue;
            bool ok;
            try { ok = tr.Resolve() != null; } catch (Exception) { ok = false; }
            if (!ok) bad[tr.FullName] = new Unresolved { Kind = "type", Name = tr.FullName };
        }
        foreach (var mr in mod.GetMemberReferences())
        {
            if (mr.DeclaringType is ArrayType || !Checked(mr.DeclaringType) || bad.ContainsKey(mr.DeclaringType.GetElementType().FullName)) continue;
            bool ok;
            try { ok = mr switch { MethodReference m => m.Resolve() != null, FieldReference f => f.Resolve() != null, _ => true }; }
            catch (Exception) { ok = false; }
            if (!ok) bad[mr.FullName] = new Unresolved { Kind = mr is FieldReference ? "field" : "method", Name = mr.FullName };
        }
        if (bad.Count == 0) return new List<Unresolved>();

        void Visit(string site, HashSet<string> keys)
        {
            foreach (var k in keys) if (bad.TryGetValue(k, out var u)) u.Sites.Add(site);
        }
        foreach (var type in mod.GetTypes())
        {
            var typeKeys = new HashSet<string>();
            if (type.BaseType != null) Touch(type.BaseType, typeKeys);
            foreach (var i in type.Interfaces) Touch(i.InterfaceType, typeKeys);
            foreach (var f in type.Fields) Touch(f.FieldType, typeKeys);
            Visit(Pretty(type.FullName), typeKeys);
            foreach (var m in type.Methods)
            {
                var keys = new HashSet<string>();
                Touch(m.ReturnType, keys);
                foreach (var p in m.Parameters) Touch(p.ParameterType, keys);
                if (m.HasBody)
                {
                    foreach (var l in m.Body.Variables) Touch(l.VariableType, keys);
                    foreach (var i in m.Body.Instructions)
                    {
                        if (i.Operand is MemberReference mem) Touch(mem, keys);
                        else if (i.Operand is TypeReference t) Touch(t, keys);
                    }
                }
                Visit(SiteName(m), keys);
            }
        }
        return bad.Values.OrderBy(u => u.Kind, StringComparer.Ordinal).ThenBy(u => u.Name, StringComparer.Ordinal).ToList();
    }
}
