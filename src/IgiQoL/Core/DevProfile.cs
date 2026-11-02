using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Behaviour.Unit;
using BepInEx;
using HarmonyLib;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Core;

// [Dev] ProfileSeconds: spawns a fight next to the fleet and measures frame times, the time spent in every Harmony
// patch and MonoBehaviour update of this mod, and the game's own SimMarker sections. Report: dumps/profile.txt.
internal static class DevProfile
{
    private sealed class Stat
    {
        public string label;
        public long ticks, maxTicks;
        public int calls;
    }

    private static readonly Dictionary<MethodBase, Stat> stats = new();
    private static bool collecting;
    private static Harmony harmony;

    private static void Pre(out long __state) => __state = Stopwatch.GetTimestamp();

    private static void Post(long __state, MethodBase __originalMethod)
    {
        if (!collecting) return;
        long t = Stopwatch.GetTimestamp() - __state;
        if (!stats.TryGetValue(__originalMethod, out var s)) return;
        s.ticks += t;
        s.calls++;
        if (t > s.maxTicks) s.maxTicks = t;
    }

    private static void Instrument()
    {
        harmony = new Harmony("igi.qol.profile");
        var targets = new Dictionary<MethodBase, string>();
        foreach (var feature in FeatureRegistry.All)
        {
            if (feature.Harmony == null || !feature.Active) continue;
            foreach (var original in feature.Harmony.GetPatchedMethods())
            {
                var info = Harmony.GetPatchInfo(original);
                if (info == null) continue;
                void Add(IEnumerable<Patch> patches, string kind)
                {
                    foreach (var p in patches)
                    {
                        if (p.owner != feature.Harmony.Id || targets.ContainsKey(p.PatchMethod)) continue;
                        targets[p.PatchMethod] = $"{feature.Id} {kind} {original.DeclaringType?.Name}.{original.Name}";
                    }
                }
                Add(info.Prefixes, "pre");
                Add(info.Postfixes, "post");
                Add(info.Finalizers, "fin");
            }
        }
        foreach (var type in typeof(Plugin).Assembly.GetTypes())
        {
            if (!type.IsSubclassOf(typeof(MonoBehaviour)) || type == typeof(Plugin)) continue;
            foreach (var name in new[] { "Update", "LateUpdate", "FixedUpdate" })
            {
                var m = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (m != null) targets[m] = $"MonoBehaviour {type.Name}.{name}";
            }
        }
        var pre = new HarmonyMethod(typeof(DevProfile), nameof(Pre)) { priority = Priority.First };
        var post = new HarmonyMethod(typeof(DevProfile), nameof(Post)) { priority = Priority.Last };
        int ok = 0;
        foreach (var kv in targets)
        {
            try
            {
                harmony.Patch(kv.Key, pre, post);
                stats[kv.Key] = new Stat { label = kv.Value };
                ok++;
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[Dev] profile: can't time {kv.Value}: {e.Message}"); }
        }
        Plugin.Log.LogInfo($"[Dev] profile: timing {ok} of {targets.Count} mod methods");
    }

    public static IEnumerator Run(int seconds, int enemies)
    {
        yield return SelfCheckContext.EnsureInSpace();
        Instrument();
        if (enemies > 0 && !SelfCheckContext.SpawnEnemies(enemies)) Plugin.Log.LogInfo("[Dev] profile: no enemies spawned");
        float until = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < until && Hostiles() < Mathf.Min(enemies, 1)) yield return new WaitForSecondsRealtime(1f);
        yield return new WaitForSecondsRealtime(5f);

        var frames = new List<float>();
        var hostiles = new List<int>();
        var units = new List<int>();
        SimMarker.ResetTotals();
        SimMarker.collecting = true;
        collecting = true;
        float end = Time.realtimeSinceStartup + seconds, nextCensus = Time.realtimeSinceStartup + 1f;
        bool skip = true;
        int allFrames = 0;
        long frameStart = Stopwatch.GetTimestamp();
        double wallMs;
        // Always unpatch and clear the flags, even if the profile is abandoned part way through.
        try
        {
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                allFrames++;
                if (!skip) frames.Add(Time.unscaledDeltaTime * 1000f);
                skip = false;
                if (Time.realtimeSinceStartup >= nextCensus)
                {
                    hostiles.Add(Hostiles());
                    units.Add(UnityEngine.Object.FindObjectsByType<AbstractUnit>(FindObjectsInactive.Exclude).Length);
                    nextCensus = Time.realtimeSinceStartup + 1f;
                    skip = true;
                }
            }
            wallMs = (Stopwatch.GetTimestamp() - frameStart) * 1000.0 / Stopwatch.Frequency;
        }
        finally
        {
            collecting = false;
            SimMarker.collecting = false;
            harmony.UnpatchSelf();
        }
        Report(frames, allFrames, hostiles, units, wallMs);
    }

    private static int Hostiles()
    {
        int n = 0;
        foreach (var u in UnityEngine.Object.FindObjectsByType<AbstractUnit>(FindObjectsInactive.Exclude))
            if (u != null && !u.isDestroyed && u.IsPlayerEnemy() && u is not Drone) n++;
        return n;
    }

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private static void Report(List<float> frames, int allFrames, List<int> hostiles, List<int> units, double wallMs)
    {
        int totalFrames = frames.Count;
        double n = Mathf.Max(1, allFrames);
        var sorted = new List<float>(frames);
        sorted.Sort();
        float P(double q) => sorted.Count == 0 ? 0f : sorted[Mathf.Clamp((int)(q * (sorted.Count - 1)), 0, sorted.Count - 1)];
        double sum = 0;
        foreach (var f in frames) sum += f;
        double Avg(List<int> l) { double a = 0; foreach (var x in l) a += x; return l.Count > 0 ? a / l.Count : 0; }

        var sb = new StringBuilder();
        sb.AppendLine($"profile {DateTime.Now:yyyy-MM-dd HH:mm}, {SystemInfo.graphicsDeviceName}, {Screen.width}x{Screen.height}, vSync {QualitySettings.vSyncCount}, target fps {Application.targetFrameRate}");
        sb.AppendLine($"frames {allFrames} over {wallMs / 1000.0:0.0} s ({totalFrames} without the census frames): mean {sum / Mathf.Max(1, totalFrames):0.00} ms, p50 {P(0.5):0.00}, p95 {P(0.95):0.00}, p99 {P(0.99):0.00}, max {P(1):0.00}");
        sb.AppendLine($"hostile ships (mean) {Avg(hostiles):0.0}, all units (mean) {Avg(units):0.0}");
        double meanFrame = wallMs / n;
        var list = new List<Stat>(stats.Values);
        list.Sort((a, b) => b.ticks.CompareTo(a.ticks));
        long modTicks = 0;
        foreach (var s in list) modTicks += s.ticks;
        double modPerFrame = Ms(modTicks) / n;
        sb.AppendLine($"mod total {modPerFrame:0.000} ms / frame ({modPerFrame / Math.Max(meanFrame, 1e-6):P1} of the mean frame, census frames included); nested calls count twice");
        sb.AppendLine("mod methods (ms / frame, calls / frame, us / call, max us):");
        foreach (var s in list)
        {
            if (s.calls == 0) continue;
            sb.AppendLine($"  {s.label}: {Ms(s.ticks) / n:0.0000} / {s.calls / n:0.0} / {Ms(s.ticks) * 1000.0 / s.calls:0.0} / {Ms(s.maxTicks) * 1000.0:0}");
        }

        var game = new List<KeyValuePair<string, (long ticks, int calls)>>(SimMarker.Totals);
        game.Sort((a, b) => b.Value.ticks.CompareTo(a.Value.ticks));
        sb.AppendLine("game SimMarker sections (ms / frame, calls / frame; nested sections include their children):");
        foreach (var kv in game)
            sb.AppendLine($"  {kv.Key}: {Ms(kv.Value.ticks) / n:0.000} / {kv.Value.calls / n:0.0}");

        string dir = Paths.ConfigPath + "/IgiQoL/dumps";
        Directory.CreateDirectory(dir);
        string path = dir + "/profile.txt";
        File.WriteAllText(path, sb.ToString());
        Plugin.Log.LogInfo("[Dev] profile -> " + path + "\n" + sb);
    }
}
