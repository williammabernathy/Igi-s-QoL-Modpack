using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;

namespace VersionDiff;

// Compares what the mod patches between two game versions and checks the mod's compiled references.
public static class PatchDiff
{
    private sealed class Row
    {
        public Target Target;
        public Resolved New, Old;
        public string Status, Note, Diff, OldSource, OldSignature, NewSignature;
        public List<string> ParamProblems = new();
    }

    private sealed class LookupRow
    {
        public Lookup Lookup;
        public string OldStatus, OldDetail, NewStatus, NewDetail;
    }

    public static int Run(string[] args)
    {
        string oldDir = null, newDir = null, outFile = null;
        var mods = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (i + 1 >= args.Length) return Program.Fail($"{args[i]} needs a value");
            var value = args[i + 1];
            switch (args[i])
            {
                case "--old": oldDir = value; break;
                case "--new": newDir = value; break;
                case "--mod": mods.Add(value); break;
                case "--out": outFile = value; break;
                default: return Program.Fail("unknown argument " + args[i]);
            }
            i++;
        }

        var root = FindRoot();
        if (mods.Count == 0)
        {
            var igi = root == null ? null : Path.Combine(root, "src", "IgiQoL", "bin", "Release", "IgiQoL.dll");
            if (igi == null || !File.Exists(igi)) return Program.Fail("--mod <dll> is required outside this repo");
            mods.Add(igi);
        }
        foreach (var m in mods) if (!File.Exists(m)) return Program.Fail("mod DLL not found: " + m);

        var snapshots = root == null ? new List<string>() : ListSnapshots(root);
        // The game's Managed dir resolves framework assemblies and, through it, BepInEx/core/0Harmony.dll.
        var fallback = ManagedDir(snapshots.Count > 0 ? snapshots[^1] : null) ?? ManagedDir(newDir) ?? ManagedDir(oldDir);

        if (newDir == null)
        {
            if (fallback != null && File.Exists(Path.Combine(fallback, "Assembly-CSharp.dll")))
            {
                var hash = HashOf(fallback);
                newDir = snapshots.Any(s => Path.GetFileName(s).EndsWith("-" + hash, StringComparison.Ordinal)) ? snapshots[^1] : fallback;
            }
            else if (snapshots.Count > 0) newDir = snapshots[^1];
            else return Program.Fail("no game-versions snapshot and no --new given");
        }
        if (!Directory.Exists(newDir)) return Program.Fail("not a directory: " + newDir);
        newDir = Path.GetFullPath(newDir);
        if (oldDir == null) oldDir = snapshots.LastOrDefault(s => Path.GetFullPath(s) != newDir);
        else if (!Directory.Exists(oldDir)) return Program.Fail("not a directory: " + oldDir);
        if (oldDir != null) oldDir = Path.GetFullPath(oldDir);

        var vNew = new GameVersion(newDir, fallback ?? newDir, Label(newDir, snapshots));
        var vOld = oldDir == null ? null : new GameVersion(oldDir, fallback ?? newDir, Label(oldDir, snapshots));

        var targets = new Dictionary<string, Target>();
        var lookups = new List<Lookup>();
        var unresolved = new List<Unresolved>();
        foreach (var path in mods)
        {
            var mod = vNew.ReadMod(path);
            foreach (var t in PatchManifest.ReadTargets(mod))
            {
                if (targets.TryGetValue(t.Key, out var e)) e.PatchedBy.AddRange(t.PatchedBy);
                else targets[t.Key] = t;
            }
            lookups.AddRange(PatchManifest.ReadLookups(mod));
            unresolved.AddRange(PatchManifest.CheckReferences(mod, vNew));
        }

        var rows = targets.Values.OrderBy(t => t.TypeFull, StringComparer.Ordinal).ThenBy(t => t.Label, StringComparer.Ordinal).Select(t => Compare(t, vOld, vNew)).ToList();
        var lookupRows = lookups.Select(l =>
        {
            var r = new LookupRow { Lookup = l };
            (r.NewStatus, r.NewDetail) = PatchManifest.CheckLookup(vNew, l);
            if (vOld != null) (r.OldStatus, r.OldDetail) = PatchManifest.CheckLookup(vOld, l);
            return r;
        }).ToList();

        var report = Report(mods, vOld, vNew, rows, lookupRows, unresolved);
        if (outFile != null) File.WriteAllText(outFile, report);
        else Console.Out.Write(report);

        var broken = rows.Any(r => r.Status is "missing" or "ambiguous" || r.ParamProblems.Count > 0)
            || unresolved.Count > 0 || lookupRows.Any(l => l.NewStatus is "missing" or "type changed");
        return broken ? 1 : 0;
    }

    // Nearest folder holding game-versions/ (a mod project built with the snapshot target) or this repo's solution.
    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "game-versions")) || File.Exists(Path.Combine(d.FullName, "VanguardGalaxyMods.sln"))) return d.FullName;
        return null;
    }

    private static string ManagedDir(string dir)
    {
        var file = dir == null ? null : Path.Combine(dir, "managed-dir.txt");
        if (file == null || !File.Exists(file)) return null;
        var path = File.ReadAllText(file).Trim();
        return Directory.Exists(path) ? path : null;
    }

    // Folder names are <game version>-<hash8>; oldest first.
    private static List<string> ListSnapshots(string root)
    {
        var dir = Path.Combine(root, "game-versions");
        if (!Directory.Exists(dir)) return new List<string>();
        return Directory.GetDirectories(dir)
            .OrderBy(d => Version.TryParse(Path.GetFileName(d).Split('-')[0], out var v) ? v : new Version(0, 0))
            .ThenBy(Directory.GetLastWriteTimeUtc)
            .ToList();
    }

    private static string HashOf(string dir)
    {
        using var f = File.OpenRead(Path.Combine(dir, "Assembly-CSharp.dll"));
        return Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant().Substring(0, 8);
    }

    private static string Label(string dir, List<string> snapshots)
    {
        var full = Path.GetFullPath(dir);
        if (snapshots.Any(s => Path.GetFullPath(s) == full)) return "snapshot " + Path.GetFileName(full);
        return File.Exists(Path.Combine(full, "Assembly-CSharp.dll")) ? $"{full} (Assembly-CSharp {HashOf(full)})" : full;
    }

    // ---- comparison -----------------------------------------------------------------------------------

    private static string Body(string source)
    {
        var lines = source.Split('\n');
        var i = Array.FindIndex(lines, l => l.Trim() == "{");
        return i < 0 ? "" : string.Join("\n", lines.Skip(i));
    }

    private static string TryDecompile(GameVersion v, MethodDefinition m)
    {
        try { return v.Decompile(m); }
        catch (Exception e) { return "/* decompile failed: " + e.Message + " */"; }
    }

    private static Row Compare(Target t, GameVersion vOld, GameVersion vNew)
    {
        var row = new Row { Target = t };
        if (t.Dynamic) { row.Status = "dynamic"; row.Note = "check by hand"; return row; }
        if (t.Unsupported != null) { row.Status = "unsupported"; row.Note = t.Unsupported; return row; }

        row.New = PatchManifest.Resolve(vNew, t);
        row.Note = row.New.Note;
        row.Old = vOld == null ? null : PatchManifest.Resolve(vOld, t);
        if (row.New.Status != "found")
        {
            row.Status = row.New.Status;
            if (row.Old?.Method != null) row.OldSource = TryDecompile(vOld, row.Old.Method);
            return row;
        }
        row.ParamProblems = PatchManifest.CheckParameters(t, row.New);
        if (vOld == null) { row.Status = "resolved"; return row; }
        if (row.Old.Status != "found") { row.Status = "new"; row.Note = "not found in old (" + row.Old.Status + ")"; return row; }

        row.OldSignature = PatchManifest.Signature(row.Old.Method);
        row.NewSignature = PatchManifest.Signature(row.New.Method);
        var oldText = TryDecompile(vOld, row.Old.Method);
        var newText = TryDecompile(vNew, row.New.Method);
        var sigChanged = row.OldSignature != row.NewSignature;
        var bodyChanged = Body(oldText) != Body(newText);
        row.Status = sigChanged ? "signature changed" : bodyChanged ? "body changed" : "unchanged";
        if (sigChanged || bodyChanged) row.Diff = Unified(oldText, newText, 3);
        return row;
    }

    // ---- unified diff ---------------------------------------------------------------------------------

    private static string Unified(string a, string b, int context)
    {
        var x = a.Split('\n');
        var y = b.Split('\n');
        int pre = 0;
        while (pre < x.Length && pre < y.Length && x[pre] == y[pre]) pre++;
        int suf = 0;
        while (suf < x.Length - pre && suf < y.Length - pre && x[x.Length - 1 - suf] == y[y.Length - 1 - suf]) suf++;

        var ops = new List<(char op, string line)>();
        for (var i = 0; i < pre; i++) ops.Add((' ', x[i]));
        int n = x.Length - pre - suf, m = y.Length - pre - suf;
        if ((long)n * m > 30_000_000)
        {
            for (var i = 0; i < n; i++) ops.Add(('-', x[pre + i]));
            for (var j = 0; j < m; j++) ops.Add(('+', y[pre + j]));
        }
        else
        {
            var lcs = new int[n + 1, m + 1];
            for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                lcs[i, j] = x[pre + i] == y[pre + j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            int p = 0, q = 0;
            while (p < n || q < m)
            {
                if (p < n && q < m && x[pre + p] == y[pre + q]) { ops.Add((' ', x[pre + p])); p++; q++; }
                else if (q >= m || (p < n && lcs[p + 1, q] >= lcs[p, q + 1])) { ops.Add(('-', x[pre + p])); p++; }
                else { ops.Add(('+', y[pre + q])); q++; }
            }
        }
        for (var i = x.Length - suf; i < x.Length; i++) ops.Add((' ', x[i]));

        var sb = new StringBuilder();
        var changes = Enumerable.Range(0, ops.Count).Where(i => ops[i].op != ' ').ToList();
        var k = 0;
        while (k < changes.Count)
        {
            var first = changes[k];
            var last = first;
            while (k + 1 < changes.Count && changes[k + 1] - last <= 2 * context + 1) last = changes[++k];
            k++;
            var start = Math.Max(0, first - context);
            var end = Math.Min(ops.Count - 1, last + context);
            int oldLine = 1, newLine = 1;
            for (var i = 0; i < start; i++) { if (ops[i].op != '+') oldLine++; if (ops[i].op != '-') newLine++; }
            int oldCount = 0, newCount = 0;
            for (var i = start; i <= end; i++) { if (ops[i].op != '+') oldCount++; if (ops[i].op != '-') newCount++; }
            sb.Append($"@@ -{oldLine},{oldCount} +{newLine},{newCount} @@\n");
            for (var i = start; i <= end; i++) sb.Append(ops[i].op).Append(ops[i].line).Append('\n');
        }
        return sb.ToString();
    }

    // ---- report ---------------------------------------------------------------------------------------

    private static string Cell(string s) => s.Replace("|", "\\|").Replace("\n", " ");

    private static string PatchedBy(Target t) => string.Join("; ", t.PatchedBy.Select(p => $"{p.Kind} {p.Name}"));

    private static string Report(List<string> mods, GameVersion vOld, GameVersion vNew, List<Row> rows, List<LookupRow> lookups, List<Unresolved> unresolved)
    {
        var sb = new StringBuilder();
        sb.Append("# Patch target report\n\n");
        sb.Append($"- Old: {(vOld == null ? "none" : vOld.Label)}\n- New: {vNew.Label}\n");
        foreach (var m in mods) sb.Append($"- Mod: `{m}`\n");
        if (vOld == null) sb.Append("\nNo older game version found, so this is a manifest run: targets are resolved against the new version, references are checked against it, and there are no diffs.\n");

        var patchMethods = rows.SelectMany(r => r.Target.PatchedBy).Select(p => p.Method).Distinct().Count();
        var patchClasses = rows.SelectMany(r => r.Target.PatchedBy).Select(p => p.Class).Distinct().Count();
        sb.Append($"\n## Summary\n\n{rows.Count} targets on {rows.Select(r => r.Target.TypeFull).Distinct().Count()} types, from {patchMethods} patch methods in {patchClasses} classes.\n\n| Status | Count |\n|---|---|\n");
        foreach (var g in rows.GroupBy(r => r.Status).OrderBy(g => g.Key, StringComparer.Ordinal)) sb.Append($"| {g.Key} | {g.Count()} |\n");
        var badParams = rows.Sum(r => r.ParamProblems.Count);
        var badLookups = lookups.Count(l => l.NewStatus is "missing" or "type changed");
        sb.Append($"\n- Patch parameter problems: {badParams}\n- Unresolved references: {unresolved.Count}\n- String lookups: {lookups.Count} ({badLookups} broken, {lookups.Count(l => l.NewStatus == "dynamic")} dynamic)\n");

        var attention = rows.Where(r => r.Status is not ("unchanged" or "resolved")).ToList();
        sb.Append("\n## Needs attention\n\n");
        if (attention.Count == 0) sb.Append("Nothing.\n");
        else
        {
            sb.Append("| Status | Target | Patched by | Note |\n|---|---|---|---|\n");
            foreach (var r in attention) sb.Append($"| {r.Status} | `{Cell(r.Target.Label)}` | {Cell(PatchedBy(r.Target))} | {Cell(r.Note ?? "")} |\n");
        }

        var changed = rows.Where(r => r.Diff != null).ToList();
        if (changed.Count > 0)
        {
            sb.Append("\n## Changed targets\n");
            foreach (var r in changed)
            {
                sb.Append($"\n### `{r.Target.Label}` ({r.Status})\n\nPatched by: {PatchedBy(r.Target)}\n\n");
                if (r.OldSignature != r.NewSignature) sb.Append($"- Old signature: `{r.OldSignature}`\n- New signature: `{r.NewSignature}`\n\n");
                sb.Append("```diff\n").Append(r.Diff).Append("```\n");
            }
        }

        var missing = rows.Where(r => r.Status is "missing" or "ambiguous").ToList();
        if (missing.Count > 0)
        {
            sb.Append("\n## Missing and ambiguous targets\n");
            foreach (var r in missing)
            {
                sb.Append($"\n### `{r.Target.Label}` ({r.Status})\n\nPatched by: {PatchedBy(r.Target)}\n\n{r.Note}\n\n");
                if (r.OldSource != null) sb.Append("Old source:\n\n```csharp\n").Append(r.OldSource.TrimEnd('\n')).Append("\n```\n\n");
                else if (r.Old != null) sb.Append($"Not found in the old version either ({r.Old.Status}).\n\n");
                sb.Append(r.New.Overloads.Count == 0 ? "No same-name members remain in the new version.\n" : "Same-name members in the new version:\n\n");
                foreach (var o in r.New.Overloads) sb.Append($"- `{o.Name}` {PatchManifest.Signature(o)}\n");
            }
        }

        var problems = rows.Where(r => r.ParamProblems.Count > 0).ToList();
        sb.Append("\n## Patch parameter problems\n\n");
        if (problems.Count == 0) sb.Append("None.\n");
        foreach (var r in problems)
        {
            sb.Append($"- `{r.Target.Label}`\n");
            foreach (var p in r.ParamProblems) sb.Append($"  - {p}\n");
        }

        sb.Append("\n## Unresolved references\n\n");
        if (unresolved.Count == 0) sb.Append("None.\n");
        else
        {
            sb.Append("| Kind | Reference | Used by |\n|---|---|---|\n");
            foreach (var u in unresolved)
            {
                var sites = u.Sites.Count > 8 ? string.Join(", ", u.Sites.Take(8)) + $", and {u.Sites.Count - 8} more" : string.Join(", ", u.Sites);
                sb.Append($"| {u.Kind} | `{Cell(u.Name)}` | {Cell(sites)} |\n");
            }
        }

        sb.Append("\n## String lookups\n\n");
        if (lookups.Count == 0) sb.Append("None.\n");
        else
        {
            sb.Append(vOld == null ? "| New | Lookup | Used by | Detail |\n|---|---|---|---|\n" : "| Old | New | Lookup | Used by | Detail |\n|---|---|---|---|---|\n");
            foreach (var l in lookups.OrderBy(l => l.Lookup.Site, StringComparer.Ordinal).ThenBy(l => l.Lookup.Label, StringComparer.Ordinal))
                sb.Append((vOld == null ? "" : $"| {l.OldStatus} ") + $"| {l.NewStatus} | `{Cell(l.Lookup.Label)}` | {Cell(l.Lookup.Site)} | {Cell(l.NewDetail)} |\n");
        }

        sb.Append("\n## All targets\n\n| Status | Target | Patched by |\n|---|---|---|\n");
        foreach (var r in rows) sb.Append($"| {r.Status} | `{Cell(r.Target.Label)}` | {Cell(PatchedBy(r.Target))} |\n");
        return sb.ToString();
    }
}
