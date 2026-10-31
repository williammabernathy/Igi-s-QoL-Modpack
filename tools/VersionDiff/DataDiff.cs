using System.Text;

namespace VersionDiff;

// Compares two directories of dumps written by the in-game DumpStatics/DumpGameData harness.
public static class DataDiff
{
    private sealed class Dump
    {
        public string Header = "";
        public SortedDictionary<string, SortedDictionary<string, string>> Elements = new(StringComparer.Ordinal);
    }

    public static int Run(string[] args)
    {
        string oldDir = null, newDir = null, outFile = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--out")
            {
                if (++i >= args.Length) return Program.Fail("--out needs a file");
                outFile = args[i];
            }
            else if (args[i].StartsWith("--")) return Program.Fail("Unknown option " + args[i]);
            else if (oldDir == null) oldDir = args[i];
            else if (newDir == null) newDir = args[i];
            else return Program.Fail("Too many arguments");
        }
        if (oldDir == null || newDir == null) return Program.Fail("Both dump directories are required");
        if (!Directory.Exists(oldDir)) return Program.Fail("Not a directory: " + oldDir);
        if (!Directory.Exists(newDir)) return Program.Fail("Not a directory: " + newDir);

        var oldFiles = Names(oldDir);
        var newFiles = Names(newDir);
        var common = oldFiles.Intersect(newFiles).OrderBy(n => n, StringComparer.Ordinal).ToList();

        var sections = new StringBuilder();
        int totalAdded = 0, totalRemoved = 0, totalChanged = 0, changedFiles = 0;
        foreach (var name in common)
        {
            var a = Parse(Path.Combine(oldDir, name));
            var b = Parse(Path.Combine(newDir, name));
            var added = b.Elements.Keys.Where(k => !a.Elements.ContainsKey(k)).ToList();
            var removed = a.Elements.Keys.Where(k => !b.Elements.ContainsKey(k)).ToList();
            var changed = new List<(string Key, List<string> Lines)>();
            foreach (var (key, oldPaths) in a.Elements)
            {
                if (!b.Elements.TryGetValue(key, out var newPaths)) continue;
                var lines = new List<string>();
                foreach (var (path, oldValue) in oldPaths)
                {
                    if (!newPaths.TryGetValue(path, out var newValue)) lines.Add($"`{path}`: {Show(oldValue)} → (removed)");
                    else if (newValue != oldValue) lines.Add($"`{path}`: {Show(oldValue)} → {Show(newValue)}");
                }
                foreach (var (path, newValue) in newPaths)
                    if (!oldPaths.ContainsKey(path)) lines.Add($"`{path}`: (added) → {Show(newValue)}");
                if (lines.Count > 0) changed.Add((key, lines));
            }
            if (added.Count == 0 && removed.Count == 0 && changed.Count == 0) continue;

            changedFiles++;
            totalAdded += added.Count;
            totalRemoved += removed.Count;
            totalChanged += changed.Count;
            sections.AppendLine($"## {name}").AppendLine();
            if (a.Header != b.Header) sections.AppendLine($"`{a.Header}` → `{b.Header}`").AppendLine();
            if (added.Count > 0)
            {
                sections.AppendLine($"### Added ({added.Count})").AppendLine();
                foreach (var k in added) sections.AppendLine($"- `{k}`");
                sections.AppendLine();
            }
            if (removed.Count > 0)
            {
                sections.AppendLine($"### Removed ({removed.Count})").AppendLine();
                foreach (var k in removed) sections.AppendLine($"- `{k}`");
                sections.AppendLine();
            }
            if (changed.Count > 0)
            {
                sections.AppendLine($"### Changed ({changed.Count})").AppendLine();
                foreach (var (key, lines) in changed)
                {
                    sections.AppendLine($"- `{key}`");
                    foreach (var l in lines) sections.AppendLine("  - " + l);
                }
                sections.AppendLine();
            }
        }

        var report = new StringBuilder();
        report.AppendLine("# Data diff").AppendLine();
        report.AppendLine($"- Old: `{oldDir}`").AppendLine($"- New: `{newDir}`");
        report.AppendLine($"- Files compared: {common.Count}, with differences: {changedFiles}");
        report.AppendLine($"- Elements added: {totalAdded}, removed: {totalRemoved}, changed: {totalChanged}");
        var onlyOld = oldFiles.Except(newFiles).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var onlyNew = newFiles.Except(oldFiles).OrderBy(n => n, StringComparer.Ordinal).ToList();
        if (onlyOld.Count > 0) report.AppendLine("- Only in old: " + string.Join(", ", onlyOld.Select(n => $"`{n}`")));
        if (onlyNew.Count > 0) report.AppendLine("- Only in new: " + string.Join(", ", onlyNew.Select(n => $"`{n}`")));
        report.AppendLine();
        if (changedFiles == 0) report.AppendLine("No differences.");
        report.Append(sections);

        if (outFile != null)
        {
            File.WriteAllText(outFile, report.ToString());
            Console.WriteLine($"Wrote {outFile}");
        }
        else Console.Write(report);
        return 0;
    }

    private static List<string> Names(string dir) =>
        Directory.GetFiles(dir, "*.txt").Select(Path.GetFileName).ToList();

    private static string Show(string v) => v.Length == 0 ? "`(empty)`" : "`" + v.Replace("`", "'") + "`";

    private static Dump Parse(string file)
    {
        var dump = new Dump();
        foreach (var line in File.ReadLines(file, Encoding.UTF8))
        {
            if (line.StartsWith('#'))
            {
                if (dump.Header.Length == 0) dump.Header = line.TrimStart('#', ' ');
                continue;
            }
            var parts = line.Split('\t', 3);
            if (parts.Length < 3) continue;
            if (!dump.Elements.TryGetValue(parts[0], out var paths))
                dump.Elements[parts[0]] = paths = new SortedDictionary<string, string>(StringComparer.Ordinal);
            paths[parts[1]] = parts[2];
        }
        return dump;
    }
}
