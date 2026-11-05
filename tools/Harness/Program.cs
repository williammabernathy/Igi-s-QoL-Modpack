using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Harness;

// Runs the [Dev] harness of igi.qol.cfg on a copy of a save, once or several times, and reports the CHECK lines:
// counts, every FAIL, and with several runs the checks whose result changed between runs. The steps are the ones in
// docs/modpack/dev-harness.md; the [Dev] values it changes and the save copy are put back afterwards.
public static class Program
{
    private const string Usage = @"Usage:
  Harness --save <save> [--name <copy>] [--only <feature ids>] [--runs <n>] [--skips]
          [--game <dir>] [--saves <dir>] [--timeout <seconds>] [--keep]

  --save     save to copy, without extension (e.g. autosave-0)
  --name     name of the copy the harness loads (default igiqol-dev)
  --only     [Dev] OnlyChecks: comma-separated feature ids (default: every feature)
  --runs     run this many times and list checks whose result changed (default 1)
  --skips    also print every SKIP line
  --keep     keep the save copy afterwards
  --timeout  seconds to wait for one run (default 1200)";

    private const string AppId = "3471800";
    private static readonly Regex CheckLine = new(@"CHECK (\S+) (PASS|FAIL|SKIP)(?: - (.*))?$");
    private static readonly string[] DevKeys = { "Enabled", "AutoLoadSave", "SelfCheck", "OnlyChecks", "QuitAfterSeconds" };

    public static int Main(string[] args)
    {
        Options o;
        try { o = Options.Parse(args); }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            Console.Error.WriteLine(e.Message);
            o = null;
        }
        if (o == null)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }
        string cfg = Path.Combine(o.Game, "BepInEx", "config", "igi.qol.cfg");
        string log = Path.Combine(o.Game, "BepInEx", "LogOutput.log");
        string source = Path.Combine(o.Saves, o.Save + ".save");
        if (!File.Exists(cfg) || !File.Exists(source))
        {
            Console.Error.WriteLine($"Not found: {(File.Exists(cfg) ? source : cfg)}");
            return 2;
        }
        if (GameRunning())
        {
            Console.Error.WriteLine("The game is running; close it first.");
            return 2;
        }

        var saved = DevConfig.Read(cfg, DevKeys);
        var runs = new List<Dictionary<string, (string Result, string Detail)>>();
        int failed = 0;
        try
        {
            CopySave(o.Saves, o.Save, o.Name);
            DevConfig.Write(cfg, new Dictionary<string, string>
            {
                ["Enabled"] = "true", ["AutoLoadSave"] = o.Name, ["SelfCheck"] = "true", ["OnlyChecks"] = o.Only, ["QuitAfterSeconds"] = "10",
            });
            for (int i = 1; i <= o.Runs; i++)
            {
                Console.WriteLine($"Run {i} of {o.Runs}: {o.Save} as {o.Name}" + (o.Only.Length > 0 ? ", only " + o.Only : ""));
                var watch = Stopwatch.StartNew();
                string text = RunOnce(log, o.Timeout);
                string kept = KeepLog(text, o.Name, i);
                var checks = Parse(text);
                runs.Add(checks);
                int pass = checks.Values.Count(c => c.Result == "PASS"), fail = checks.Values.Count(c => c.Result == "FAIL"), skip = checks.Values.Count(c => c.Result == "SKIP");
                bool complete = text.Contains("Self-check complete");
                Console.WriteLine($"  {pass} pass, {fail} fail, {skip} skip in {watch.Elapsed.TotalSeconds:0} s{(complete ? "" : ", INCOMPLETE (no 'Self-check complete')")}; log {kept}");
                foreach (var c in checks.Where(c => c.Value.Result == "FAIL" || o.Skips && c.Value.Result == "SKIP"))
                    Console.WriteLine($"  {c.Value.Result} {c.Key}: {c.Value.Detail}");
                if (fail > 0 || !complete) failed++;
            }
        }
        finally
        {
            DevConfig.Write(cfg, saved);
            if (!o.Keep)
                foreach (var ext in new[] { ".save", ".meta" })
                    File.Delete(Path.Combine(o.Saves, o.Name + ext));
        }

        int unstable = 0;
        if (runs.Count > 1)
        {
            var names = runs.SelectMany(r => r.Keys).Distinct().OrderBy(n => n, StringComparer.Ordinal);
            foreach (var name in names)
            {
                var results = runs.Select(r => r.TryGetValue(name, out var c) ? c.Result : "none").ToList();
                if (results.Distinct().Count() == 1) continue;
                unstable++;
                Console.WriteLine($"  changed between runs: {name}: {string.Join(", ", results)}");
            }
            Console.WriteLine(unstable == 0 ? "  every check gave the same result in every run" : $"  {unstable} checks changed between runs");
        }
        return failed > 0 || unstable > 0 ? 1 : 0;
    }

    // Starts the game through Steam, waits for it to start and to quit (the harness quits it), and returns the log.
    private static string RunOnce(string log, int timeout)
    {
        var started = DateTime.UtcNow;
        if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo("steam://rungameid/" + AppId) { UseShellExecute = true });
        else Process.Start(new ProcessStartInfo("steam", "-applaunch " + AppId) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true });
        bool seen = false;
        while ((DateTime.UtcNow - started).TotalSeconds < timeout)
        {
            Thread.Sleep(2000);
            bool running = GameRunning();
            if (running) seen = true;
            else if (seen) break;
            else if ((DateTime.UtcNow - started).TotalSeconds > 180) throw new InvalidOperationException("The game didn't start within 180 s.");
        }
        if (GameRunning()) throw new InvalidOperationException($"The game still runs after {timeout} s; close it and look at {log}.");
        if (File.GetLastWriteTimeUtc(log) < started) throw new InvalidOperationException("The game quit without writing " + log);
        return File.ReadAllText(log);
    }

    private static bool GameRunning()
    {
        if (OperatingSystem.IsWindows()) return Process.GetProcessesByName("VanguardGalaxy").Length > 0;
        // Under Proton the game is a wine process; its command line names the exe.
        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            try
            {
                if (File.ReadAllText(Path.Combine(dir, "cmdline")).Contains("VanguardGalaxy.exe")) return true;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return false;
    }

    private static void CopySave(string saves, string save, string name)
    {
        File.Copy(Path.Combine(saves, save + ".save"), Path.Combine(saves, name + ".save"), true);
        // A save without a .meta of its own (a copy made by hand) gets the newest one, which names the same game version.
        string meta = Path.Combine(saves, save + ".meta"), target = Path.Combine(saves, name + ".meta");
        if (!File.Exists(meta))
            meta = new DirectoryInfo(saves).GetFiles("*.meta").Where(f => f.FullName != target).OrderByDescending(f => f.LastWriteTimeUtc).Select(f => f.FullName).FirstOrDefault();
        if (meta != null) File.Copy(meta, target, true);
    }

    private static string KeepLog(string text, string name, int run)
    {
        string dir = Path.Combine(RepoRoot(), "artifacts", "harness");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{name}-{DateTime.Now:yyyyMMdd-HHmmss}-{run}.log");
        File.WriteAllText(path, text);
        return path;
    }

    private static string RepoRoot()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "VanguardGalaxyMods.sln"))) return d.FullName;
        return Directory.GetCurrentDirectory();
    }

    // The last line of each check; a check name that shows twice keeps its last result.
    private static Dictionary<string, (string Result, string Detail)> Parse(string text)
    {
        var checks = new Dictionary<string, (string Result, string Detail)>();
        foreach (var line in text.Split('\n'))
        {
            var m = CheckLine.Match(line.TrimEnd('\r'));
            if (m.Success) checks[m.Groups[1].Value] = (m.Groups[2].Value, m.Groups[3].Value);
        }
        return checks;
    }
}

internal sealed class Options
{
    public string Save, Name = "igiqol-dev", Only = "", Game, Saves;
    public int Runs = 1, Timeout = 1200;
    public bool Skips, Keep;

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException(args[i] + " needs a value");
            switch (args[i])
            {
                case "--save": o.Save = Next(); break;
                case "--name": o.Name = Next(); break;
                case "--only": o.Only = Next(); break;
                case "--runs": o.Runs = int.Parse(Next()); break;
                case "--timeout": o.Timeout = int.Parse(Next()); break;
                case "--game": o.Game = Next(); break;
                case "--saves": o.Saves = Next(); break;
                case "--skips": o.Skips = true; break;
                case "--keep": o.Keep = true; break;
                default: return null;
            }
        }
        if (string.IsNullOrEmpty(o.Save) || o.Runs < 1 || o.Name == o.Save) return null;
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            o.Game ??= @"C:\Program Files (x86)\Steam\steamapps\common\Vanguard Galaxy";
            o.Saves ??= Path.Combine(home, "AppData", "LocalLow", "Bat Roost Games", "VanguardGalaxy", "Saves");
        }
        else
        {
            o.Game ??= "/mnt/data/SteamLibrary/steamapps/common/Vanguard Galaxy";
            o.Saves ??= "/mnt/data/SteamLibrary/steamapps/compatdata/3471800/pfx/drive_c/users/steamuser/AppData/LocalLow/Bat Roost Games/VanguardGalaxy/Saves";
        }
        return o;
    }
}

// Reads and writes keys of the [Dev] section of igi.qol.cfg, leaving every other line as it is.
internal static class DevConfig
{
    private static readonly Regex Key = new(@"^([A-Za-z0-9_]+) = (.*)$");

    public static Dictionary<string, string> Read(string path, string[] keys)
    {
        var values = new Dictionary<string, string>();
        string section = null;
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith('[')) section = line.Trim('[', ']', ' ');
            var m = Key.Match(line);
            if (section == "Dev" && m.Success && keys.Contains(m.Groups[1].Value)) values[m.Groups[1].Value] = m.Groups[2].Value;
        }
        return values;
    }

    public static void Write(string path, Dictionary<string, string> values)
    {
        var lines = File.ReadAllLines(path);
        string section = null;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('[')) section = lines[i].Trim('[', ']', ' ');
            var m = Key.Match(lines[i]);
            if (section == "Dev" && m.Success && values.TryGetValue(m.Groups[1].Value, out var v)) lines[i] = m.Groups[1].Value + " = " + v;
        }
        File.WriteAllLines(path, lines);
    }
}
