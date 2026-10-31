using System;
using System.Collections.Generic;
using LightJson;

namespace IgiQoL.Core;

public enum RetargetMode { Off, WeaponTarget, Nearest }

public enum EngagementMode { Auto, MaxRange, Fixed }

public enum TargetRule { Nearest, LowestHull, HighestHull, MostDamage, AttackingFlagship, SmallestFirst, LargestFirst, LowestLevel, HighestLevel }

// Game = the game's own Free roam / Follow ship toggle decides.
public enum DroneMode { Game, Follow, Guard, ShipTarget }

// Per-ship overrides stored in the save (SaveData.Ship(guid)). Null / Auto means "use the feature's config value".
public sealed class ShipSettings
{
    public RetargetMode? missileRetarget;
    public bool? dronesOnArrival;
    public EngagementMode engagement = EngagementMode.Auto;
    public float engagementDistance = 10f;
    public TargetRule? targetRule;
    public float? repickSeconds;
    public int? finishBelow;
    public int maxLevel;
    public float leash;
    // Per-hardpoint target rule, keyed by SpaceShipHardpoint.index. weapon is the item identifier the rule was set
    // for, so the popup can flag a hardpoint that has been refitted since.
    public readonly Dictionary<int, WeaponRule> weaponRules = new();
    public DroneMode droneMode = DroneMode.Game;
    public float droneRadius = 10f;
    // Guid of another of your ships the drones stay with (follow, guard, its target); null = their own ship.
    public string droneAnchor;
    public bool? separation;

    public static readonly ShipSettings Default = new();

    private static readonly Dictionary<string, ShipSettings> cache = new();

    public static ShipSettings Get(string guid)
    {
        if (guid == null) return Default;
        if (cache.TryGetValue(guid, out var s)) return s;
        var record = SaveData.Ship(guid, false);
        s = record == null ? Default : Parse(record);
        cache[guid] = s;
        return s;
    }

    public static ShipSettings Edit(string guid)
    {
        var s = Get(guid);
        if (s != Default) return s;
        s = new ShipSettings();
        cache[guid] = s;
        return s;
    }

    public static void Save(string guid) => Write(Edit(guid), SaveData.Ship(guid, true));

    // The ship's settings as a standalone record (for loadouts), and back.
    public static JsonObject Snapshot(string guid)
    {
        var o = new JsonObject();
        Write(Get(guid), o);
        return o;
    }

    public static void Restore(string guid, JsonObject o)
    {
        cache[guid] = Parse(o);
        Save(guid);
    }

    private static void Write(ShipSettings s, JsonObject record)
    {
        if (s.missileRetarget.HasValue) record["missileRetarget"] = s.missileRetarget.Value.ToString();
        else record.Remove("missileRetarget");
        if (s.dronesOnArrival.HasValue) record["dronesOnArrival"] = s.dronesOnArrival.Value;
        else record.Remove("dronesOnArrival");
        if (s.engagement != EngagementMode.Auto) record["engagement"] = s.engagement.ToString();
        else record.Remove("engagement");
        if (s.engagementDistance != Default.engagementDistance) record["engagementDistance"] = s.engagementDistance;
        else record.Remove("engagementDistance");
        if (s.targetRule.HasValue) record["targetRule"] = s.targetRule.Value.ToString();
        else record.Remove("targetRule");
        if (s.repickSeconds.HasValue) record["repickSeconds"] = s.repickSeconds.Value;
        else record.Remove("repickSeconds");
        if (s.finishBelow.HasValue) record["finishBelow"] = s.finishBelow.Value;
        else record.Remove("finishBelow");
        if (s.maxLevel > 0) record["maxLevel"] = s.maxLevel;
        else record.Remove("maxLevel");
        if (s.leash > 0f) record["leash"] = s.leash;
        else record.Remove("leash");
        if (s.weaponRules.Count > 0)
        {
            var list = new JsonArray();
            foreach (var kv in s.weaponRules)
            {
                var o = new JsonObject();
                o["index"] = kv.Key;
                o["rule"] = kv.Value.rule.ToString();
                o["weapon"] = kv.Value.weapon;
                list.Add(o);
            }
            record["weaponRules"] = list;
        }
        else record.Remove("weaponRules");
        if (s.droneMode != DroneMode.Game) record["droneMode"] = s.droneMode.ToString();
        else record.Remove("droneMode");
        if (s.droneRadius != Default.droneRadius) record["droneRadius"] = s.droneRadius;
        else record.Remove("droneRadius");
        if (s.droneAnchor != null) record["droneAnchor"] = s.droneAnchor;
        else record.Remove("droneAnchor");
        if (s.separation.HasValue) record["separation"] = s.separation.Value;
        else record.Remove("separation");
    }

    public static void Invalidate() => cache.Clear();

    private static ShipSettings Parse(JsonObject record)
    {
        var s = new ShipSettings();
        var v = record["missileRetarget"];
        if (v.IsString && Enum.TryParse(v.AsString, out RetargetMode mode)) s.missileRetarget = mode;
        v = record["dronesOnArrival"];
        if (v.IsBoolean) s.dronesOnArrival = v.AsBoolean;
        v = record["engagement"];
        if (v.IsString && Enum.TryParse(v.AsString, out EngagementMode engagement)) s.engagement = engagement;
        v = record["engagementDistance"];
        if (v.IsNumber) s.engagementDistance = (float)v.AsNumber;
        v = record["targetRule"];
        if (v.IsString && Enum.TryParse(v.AsString, out TargetRule rule)) s.targetRule = rule;
        v = record["repickSeconds"];
        if (v.IsNumber) s.repickSeconds = (float)v.AsNumber;
        v = record["finishBelow"];
        if (v.IsNumber) s.finishBelow = (int)v.AsNumber;
        v = record["maxLevel"];
        if (v.IsNumber) s.maxLevel = (int)v.AsNumber;
        v = record["leash"];
        if (v.IsNumber) s.leash = (float)v.AsNumber;
        v = record["weaponRules"];
        if (v.IsJsonArray)
            foreach (var item in v.AsJsonArray)
            {
                if (!item.IsJsonObject) continue;
                var o = item.AsJsonObject;
                if (o["index"].IsNumber && o["rule"].IsString && Enum.TryParse(o["rule"].AsString, out TargetRule wr))
                    s.weaponRules[(int)o["index"].AsNumber] = new WeaponRule(wr, o["weapon"].IsString ? o["weapon"].AsString : null);
            }
        v = record["droneMode"];
        if (v.IsString && Enum.TryParse(v.AsString, out DroneMode drone)) s.droneMode = drone;
        v = record["droneRadius"];
        if (v.IsNumber) s.droneRadius = (float)v.AsNumber;
        v = record["droneAnchor"];
        if (v.IsString) s.droneAnchor = v.AsString;
        v = record["separation"];
        if (v.IsBoolean) s.separation = v.AsBoolean;
        return s;
    }
}

public sealed class WeaponRule
{
    public TargetRule rule;
    public string weapon;

    public WeaponRule(TargetRule rule, string weapon)
    {
        this.rule = rule;
        this.weapon = weapon;
    }
}
