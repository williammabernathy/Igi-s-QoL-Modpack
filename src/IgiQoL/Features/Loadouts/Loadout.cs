using System;
using System.Collections.Generic;
using System.Text;
using Behaviour.Item;
using IgiQoL.Core;
using LightJson;
using Source.Item;
using Source.Player;
using Source.SpaceShip;
using Source.Util;

namespace IgiQoL.Features.Loadouts;

// One saved fit of a ship. Items are stored as the game's own item JSON (rolled stats included), so the
// exact piece can be found again; slots that are null were empty.
public sealed class Loadout
{
    public string name;
    public string hull;
    public string skill;
    public readonly List<string> officers = new();
    public readonly Dictionary<string, int> crew = new();
    public readonly List<string> drones = new();
    public readonly Dictionary<EquipmentSlot, JsonValue> modules = new();
    public JsonValue[] hardpoints = Array.Empty<JsonValue>();
    public JsonValue[] boosters = Array.Empty<JsonValue>();
    public JsonObject settings;
    // ECHO settings (game autopilot settings plus the mod's activity list); null when not included.
    public JsonObject echo;

    public static Loadout Capture(SpaceShipData ship, string name, bool includeEcho)
    {
        var l = new Loadout { name = name, hull = ship.shipClass.identifier, skill = string.IsNullOrEmpty(ship.skillLoadout) ? null : ship.skillLoadout };
        foreach (var o in ship.officers)
            if (o != null) l.officers.Add(o.guid);
        foreach (var kv in ship.crewData.crew)
            if (kv.Value > 0) l.crew[kv.Key] = kv.Value;
        foreach (var d in ship.droneSlots) l.drones.Add(d != null ? d.identifier : null);
        foreach (var slot in ship.shipClass.moduleSlots)
        {
            var item = ship.GetEquippedItem(slot.slot);
            if (item != null) l.modules[slot.slot] = item.ToJson();
        }
        l.hardpoints = new JsonValue[ship.hardpoints.Length];
        for (int i = 0; i < ship.hardpoints.Length; i++) l.hardpoints[i] = ship.hardpoints[i] != null ? ship.hardpoints[i].ToJson() : JsonValue.Null;
        l.boosters = new JsonValue[ship.boosters.Length];
        for (int i = 0; i < ship.boosters.Length; i++) l.boosters[i] = ship.boosters[i] != null ? ship.boosters[i].ToJson() : JsonValue.Null;
        l.settings = ShipSettings.Snapshot(ship.guid);
        if (includeEcho) l.echo = CaptureEcho();
        return l;
    }

    public static JsonObject CaptureEcho()
    {
        var o = new JsonObject { { "autopilot", GamePlayer.current.autopilotSettings.ToJson() } };
        if (EchoPriority.EchoPriority.Instance.Active)
        {
            var arr = new JsonArray();
            foreach (var a in EchoPriority.EchoPriority.List) arr.Add(a.ToString());
            o["priority"] = arr;
        }
        return o;
    }

    public string Summary()
    {
        int turrets = 0, boost = 0, drone = 0, grunts = 0;
        foreach (var h in hardpoints) if (!h.IsNull) turrets++;
        foreach (var b in boosters) if (!b.IsNull) boost++;
        foreach (var d in drones) if (d != null) drone++;
        foreach (var kv in crew) grunts += kv.Value;
        var sb = new StringBuilder();
        sb.Append(turrets).Append(" weapons, ").Append(modules.Count).Append(" modules, ").Append(boost).Append(" boosters");
        if (drone > 0) sb.Append(", ").Append(drone).Append(" drones");
        if (officers.Count > 0) sb.Append(", ").Append(officers.Count).Append(officers.Count == 1 ? " officer" : " officers");
        sb.Append(", ").Append(grunts).Append(" crew");
        if (skill != null) sb.Append(", skills \"").Append(skill).Append('"');
        if (echo != null) sb.Append(", ECHO settings");
        return sb.ToString();
    }

    // "Missile Launcher Mk.XVI (High Grade, level 61)" from stored item JSON.
    public static string Describe(JsonValue item)
    {
        if (item.IsString)
            return InventoryItemType.TryGet(item.AsString, out var t) ? Translation.Translate(t.displayName) : item.AsString;
        if (!item.IsJsonObject) return "?";
        var o = item.AsJsonObject;
        string name = o["displayName"].IsString ? Translation.Translate(o["displayName"].AsString) : o["equipmentType"].AsString ?? "?";
        string rarity = o["rarity"].IsString && Enum.TryParse(o["rarity"].AsString, out Rarity r) ? r.GetDisplayName() : null;
        string level = o["itemLevel"].IsNumber ? "level " + o["itemLevel"].AsInteger : null;
        if (rarity == null && level == null) return name;
        return name + " (" + (rarity != null && level != null ? rarity + ", " + level : rarity ?? level) + ")";
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject { { "name", name }, { "hull", hull } };
        if (skill != null) o["skill"] = skill;
        var off = new JsonArray();
        foreach (var g in officers) off.Add(g);
        o["officers"] = off;
        var c = new JsonObject();
        foreach (var kv in crew) c[kv.Key] = kv.Value;
        o["crew"] = c;
        var dr = new JsonArray();
        foreach (var d in drones) dr.Add(d);
        o["drones"] = dr;
        var m = new JsonObject();
        foreach (var kv in modules) m[kv.Key.ToString()] = kv.Value;
        o["modules"] = m;
        o["hardpoints"] = new JsonArray(hardpoints);
        o["boosters"] = new JsonArray(boosters);
        o["settings"] = settings ?? new JsonObject();
        if (echo != null) o["echo"] = echo;
        return o;
    }

    public static Loadout FromJson(JsonValue v)
    {
        if (!v.IsJsonObject || !v["name"].IsString) return null;
        var l = new Loadout { name = v["name"].AsString, hull = v["hull"].AsString, skill = v["skill"].IsString ? v["skill"].AsString : null };
        if (v["officers"].IsJsonArray)
            foreach (var g in v["officers"].AsJsonArray) if (g.IsString) l.officers.Add(g.AsString);
        if (v["crew"].IsJsonObject)
            foreach (var kv in v["crew"].AsJsonObject) if (kv.Value.IsNumber) l.crew[kv.Key] = kv.Value.AsInteger;
        if (v["drones"].IsJsonArray)
            foreach (var d in v["drones"].AsJsonArray) l.drones.Add(d.IsString ? d.AsString : null);
        if (v["modules"].IsJsonObject)
            foreach (var kv in v["modules"].AsJsonObject)
                if (Enum.TryParse(kv.Key, out EquipmentSlot slot)) l.modules[slot] = kv.Value;
        l.hardpoints = ToArray(v["hardpoints"]);
        l.boosters = ToArray(v["boosters"]);
        l.settings = v["settings"].IsJsonObject ? v["settings"].AsJsonObject : new JsonObject();
        l.echo = v["echo"].IsJsonObject ? v["echo"].AsJsonObject : null;
        return l;
    }

    private static JsonValue[] ToArray(JsonValue v)
    {
        if (!v.IsJsonArray) return Array.Empty<JsonValue>();
        var arr = v.AsJsonArray;
        var result = new JsonValue[arr.Count];
        for (int i = 0; i < arr.Count; i++) result[i] = arr[i];
        return result;
    }
}
