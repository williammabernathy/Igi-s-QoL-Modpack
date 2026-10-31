using System.Collections.Generic;
using Behaviour.Crew;
using Behaviour.Equipment.Module;
using Behaviour.Item;
using Behaviour.UI;
using Behaviour.UI.Spacestation;
using Behaviour.Unit;
using IgiQoL.Core;
using LightJson;
using Source.Galaxy.POI;
using Source.Item;
using Source.Personnel;
using Source.Player;
using Source.SpaceShip;
using Source.Util;
using PersonalHangarUI = Behaviour.UI.Spacestation.Location.PersonalHangar;
using CrewType = Behaviour.Crew.CrewType;

namespace IgiQoL.Features.Loadouts;

public sealed class ApplyReport
{
    public readonly List<string> done = new();
    public readonly List<string> problems = new();
}

// Applies a loadout at data level, in the order skills → equipment → officers → crew → drones → settings → ECHO.
// Skills go first because they set crew capacity and unlock drones; equipment before crew and drones because the
// fitted drone bay decides how many drone slots there are.
public static class LoadoutApply
{
    public static string CannotApplyReason(SpaceShipData ship)
    {
        var interior = SpaceStationInterior.instance;
        if (interior == null || interior.spacestation == null || !interior.spacestation.HasFacility(SpaceStationFacility.PersonalHangar))
            return "Dock at a station with a hangar to apply a loadout.";
        // The hangar's own refit rule (PersonalHangar.SelectShip); IsLentOut would also count your personal fleet.
        if (ship.IsCarrierAssigned || ship.IsWorkBayAssigned || ship.IsPatrolAssigned || ship.IsEmpireFleetAssigned)
            return ship.GetShipName() + " is assigned elsewhere; the hangar can't refit it either.";
        return null;
    }

    public static ApplyReport Apply(SpaceShipData ship, Loadout l)
    {
        var r = new ApplyReport();
        string reason = CannotApplyReason(ship);
        if (reason != null)
        {
            r.problems.Add(reason);
            return r;
        }
        if (l.hull != ship.shipClass.identifier)
        {
            r.problems.Add("This loadout is for a different hull.");
            return r;
        }
        Skills(ship, l, r);
        Equipment(ship, l, r);
        Officers(ship, l, r);
        Crew(ship, l, r);
        Drones(ship, l, r);
        ShipSettings.Restore(ship.guid, l.settings);
        if (l.echo != null)
        {
            if (ship == GamePlayer.current.currentSpaceShip)
            {
                if (Loadout.CaptureEcho().ToString() != l.echo.ToString()) r.done.Add("ECHO settings");
                RestoreEcho(l.echo);
            }
            else r.done.Add("ECHO settings: used when you next fly this ship");
        }
        LoadoutStore.SetLastApplied(ship.guid, l.name);

        ship.RecalculateLevel();
        ship.RebuildDerivedStatBoosts();
        GameplayManager.Instance.RefreshShipInSpace(ship);
        CrewData.NotifyCrewChanged();
        CrewData.NotifyOfficerChanged();
        if (PersonalHangarUI.current != null) PersonalHangarUI.current.RefreshIfOpen();
        if (InventoryInteractionManager.Instance != null) InventoryInteractionManager.Instance.ReloadUI();
        return r;
    }

    public static void RestoreEcho(JsonObject echo)
    {
        if (echo["autopilot"].IsJsonObject)
        {
            // Copy into the live object: the Autopilot side tab keeps a reference to it.
            var fresh = AutopilotSettings.FromJson(echo["autopilot"]);
            var live = GamePlayer.current.autopilotSettings;
            if (live == null) GamePlayer.current.autopilotSettings = fresh;
            else
            {
                live.prioritizeHomestation = fresh.prioritizeHomestation;
                live.runMissions = fresh.runMissions;
                live.allowCourierMissions = fresh.allowCourierMissions;
                live.preferMissions = fresh.preferMissions;
                live.noTravel = fresh.noTravel;
                live.autoSell = fresh.autoSell;
                live.ammoMinutes = fresh.ammoMinutes;
                live.preferredGameplayType = fresh.preferredGameplayType;
                live.preferredTargetLayer = fresh.preferredTargetLayer;
                live.autonomousDungeonMode = fresh.autonomousDungeonMode;
                live.maxAutonomousDungeonHostility = fresh.maxAutonomousDungeonHostility;
            }
        }
        if (EchoPriority.EchoPriority.Instance.Active && echo["priority"].IsJsonArray)
        {
            var list = new List<EchoPriority.EchoActivity>();
            foreach (var v in echo["priority"].AsJsonArray)
                if (v.IsString && System.Enum.TryParse(v.AsString, out EchoPriority.EchoActivity a)) list.Add(a);
            EchoPriority.EchoPriority.SetList(list);
        }
    }

    private static void Skills(SpaceShipData ship, Loadout l, ApplyReport r)
    {
        if (l.skill == null) return;
        var captain = GamePlayer.current.commander;
        int index = captain.loadouts.FindIndex(x => x.name == l.skill);
        if (index < 0)
        {
            r.problems.Add($"Skill preset \"{l.skill}\" no longer exists.");
            return;
        }
        int need = 0;
        foreach (var tree in captain.loadouts[index].skills.Values)
            foreach (var node in tree) need += node.currentPoints;
        int have = captain.GetSkillPoints();
        if (need > have)
        {
            r.problems.Add($"Skill preset \"{l.skill}\" needs {need} skill points; you have {have}.");
            return;
        }
        if (ship.skillLoadout == l.skill) return;
        if (ship == GamePlayer.current.currentSpaceShip) captain.SetSelectedLoadout(index);
        else ship.skillLoadout = l.skill;
        r.done.Add($"skill preset \"{l.skill}\"");
    }

    private enum Kind { Hardpoint, Module, Booster }

    private sealed class Slot
    {
        public Kind kind;
        public int index;
        public EquipmentSlot module;
        public InventoryItemType current;
        public JsonValue want;
        public Candidate match;
        public bool substitute;
        public InventoryItemType result;

        public string Label => kind switch
        {
            Kind.Hardpoint => "weapon slot " + (index + 1),
            Kind.Booster => "booster slot " + (index + 1),
            _ => SettingRowsPretty(module.ToString()) + " slot",
        };
    }

    private static string SettingRowsPretty(string s) => IgiQoL.UI.SettingRows.Pretty(s).ToLowerInvariant();

    private sealed class Candidate
    {
        public InventoryItemType item;
        public Inventory inventory;
        public Slot fittedIn;
        public string identity;
        public string type;
        public string rarity;
        public int level;
        public int left = 1;
    }

    private static void Equipment(SpaceShipData ship, Loadout l, ApplyReport r)
    {
        var slots = new List<Slot>();
        for (int i = 0; i < ship.hardpoints.Length; i++)
            slots.Add(new Slot { kind = Kind.Hardpoint, index = i, current = ship.hardpoints[i], want = i < l.hardpoints.Length ? l.hardpoints[i] : JsonValue.Null });
        foreach (var m in ship.shipClass.moduleSlots)
            slots.Add(new Slot { kind = Kind.Module, module = m.slot, current = ship.GetEquippedItem(m.slot), want = l.modules.TryGetValue(m.slot, out var v) ? v : JsonValue.Null });
        for (int i = 0; i < ship.boosters.Length; i++)
            slots.Add(new Slot { kind = Kind.Booster, index = i, current = ship.boosters[i], want = i < l.boosters.Length ? l.boosters[i] : JsonValue.Null });

        // Everything the fit can draw from: what's fitted now, the armory, and this ship's cargo.
        var pool = new List<Candidate>();
        foreach (var s in slots)
            if (s.current != null) pool.Add(NewCandidate(s.current, null, s, 1));
        AddInventory(pool, Inventory.global);
        AddInventory(pool, ship.cargo);

        // Exact piece first (keeping one that is already in its slot), then the same equipment type and rarity.
        foreach (var s in slots)
        {
            if (s.want.IsNull) continue;
            string identity = Identity(s.want);
            Candidate best = null;
            foreach (var c in pool)
            {
                if (c.left <= 0 || c.identity != identity || !Fits(ship, s, c.item)) continue;
                if (best == null || c.fittedIn == s) best = c;
            }
            Claim(s, best, false);
        }
        foreach (var s in slots)
        {
            if (s.want.IsNull || s.match != null) continue;
            var (type, rarity, _) = Key(s.want);
            if (type == null) continue;
            Candidate best = null;
            foreach (var c in pool)
            {
                if (c.left <= 0 || c.type != type || c.rarity != rarity || !Fits(ship, s, c.item)) continue;
                if (best == null || c.fittedIn == s || (best.fittedIn != s && c.level > best.level)) best = c;
            }
            Claim(s, best, true);
        }

        // Final contents: the match, or empty when the loadout has nothing there, or the current item when the wanted
        // one is missing (unless that item moves to another slot).
        // A fitted piece that another slot claims is moving out of its slot.
        var movedFrom = new HashSet<Slot>();
        foreach (var s in slots)
            if (s.match != null && s.match.fittedIn != null && s.match.fittedIn != s) movedFrom.Add(s.match.fittedIn);
        int changed = 0;
        foreach (var s in slots)
        {
            if (s.match != null) s.result = s.match.item;
            else if (s.want.IsNull) s.result = null;
            else
            {
                s.result = s.current != null && !movedFrom.Contains(s) ? s.current : null;
                r.problems.Add($"Missing for {s.Label}: {Loadout.Describe(s.want)}" + (s.result != null ? "; kept the fitted one." : "."));
            }
            if (s.substitute) r.problems.Add($"{s.Label}: the saved {Loadout.Describe(s.want)} is gone; fitted {Loadout.Describe(s.match.item.ToJson())} instead.");
            if (s.result != s.current) changed++;
        }
        if (changed == 0) return;

        int stored = 0;
        foreach (var s in slots)
        {
            if (s.match != null && s.match.inventory != null && s.result != s.current) s.match.inventory.Remove(s.result, 1);
        }
        foreach (var s in slots)
        {
            if (s.current == null || s.result == s.current || movedFrom.Contains(s)) continue;
            Inventory.global.Add(s.current, 1);
            stored++;
        }
        foreach (var s in slots)
        {
            if (s.result == s.current) continue;
            switch (s.kind)
            {
                case Kind.Hardpoint: ship.hardpoints[s.index] = s.result; break;
                case Kind.Booster: ship.EquipBooster(s.result, s.index); break;
                default: ship.EquipModule(s.result, s.module); break;
            }
        }
        r.done.Add(changed + (changed == 1 ? " slot refitted" : " slots refitted") + (stored > 0 ? $", {stored} removed {(stored == 1 ? "piece" : "pieces")} to the armory" : ""));
    }

    private static void Claim(Slot s, Candidate c, bool substitute)
    {
        if (c == null) return;
        c.left--;
        s.match = c;
        s.substitute = substitute;
    }

    private static Candidate NewCandidate(InventoryItemType item, Inventory inv, Slot fittedIn, int count)
    {
        var json = item.ToJson();
        var (type, rarity, level) = Key(json);
        return new Candidate { item = item, inventory = inv, fittedIn = fittedIn, identity = Identity(json), type = type ?? item.identifier, rarity = rarity ?? item.rarity.ToString(), level = level, left = count };
    }

    // What makes a rolled piece that piece: type, rarity and its rolled stats. The full JSON also carries state that
    // changes while the item is fitted (a resonant booster's unlock progress, socketed aspects), so it can't be compared.
    // Stats are floats written with 15 digits; reading them back as float gives the original value.
    public static string Identity(JsonValue json)
    {
        if (!json.IsJsonObject || !json["stats"].IsJsonArray) return json.ToString();
        var sb = new System.Text.StringBuilder();
        sb.Append(json["equipmentType"].AsString).Append('|').Append(json["rarity"].AsString);
        foreach (var stat in json["stats"].AsJsonArray)
            sb.Append('|').Append(stat["stat"].AsString).Append('=').Append(((float)stat["amount"].AsNumber).ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static void AddInventory(List<Candidate> pool, Inventory inv)
    {
        if (inv == null) return;
        foreach (var entry in new List<Inventory.InventoryItem>(inv.items))
        {
            if (entry?.item == null || entry.item.equipmentDefinition == null) continue;
            pool.Add(NewCandidate(entry.item, inv, null, entry.count));
        }
    }

    // Equipment type, rarity and level from item JSON: builder items carry all three, plain items only their identifier.
    private static (string type, string rarity, int level) Key(JsonValue json)
    {
        if (json.IsString) return (json.AsString, null, 0);
        if (!json.IsJsonObject) return (null, null, 0);
        var o = json.AsJsonObject;
        return (o["equipmentType"].IsString ? o["equipmentType"].AsString : null,
                o["rarity"].IsString ? o["rarity"].AsString : null,
                o["itemLevel"].IsNumber ? o["itemLevel"].AsInteger : 0);
    }

    // The hangar's own size rules (InventoryInteractionManager.EquipHardpoint / EquipModule / EquipBooster).
    private static bool Fits(SpaceShipData ship, Slot s, InventoryItemType item)
    {
        var eq = item.equipmentDefinition;
        if (eq == null) return false;
        switch (s.kind)
        {
            case Kind.Hardpoint:
                var hps = ship.shipClass.hardpointSlots;
                return eq.slot == EquipmentSlot.Hardpoint && s.index < hps.Length && hps[s.index].size >= eq.size;
            case Kind.Booster:
                var bs = ship.shipClass.boosterSlots;
                return eq.slot == EquipmentSlot.Booster && s.index < bs.Length && bs[s.index].size == eq.size;
            default:
                if (eq.slot != s.module) return false;
                foreach (var m in ship.shipClass.moduleSlots)
                    if (m.slot == s.module && m.size == eq.size) return true;
                return false;
        }
    }

    private static void Officers(SpaceShipData ship, Loadout l, ApplyReport r)
    {
        var want = new HashSet<string>(l.officers);
        int seated = 0, unseated = 0;
        foreach (var o in (OfficerData[])ship.officers.Clone())
        {
            if (o == null || want.Contains(o.guid)) continue;
            if (ship.RemoveCrewMember(o)) unseated++;
        }
        foreach (var guid in l.officers)
        {
            if (System.Array.Exists(ship.officers, o => o != null && o.guid == guid)) continue;
            var officer = GamePlayer.current.officers.Find(o => o.guid == guid);
            if (officer == null)
            {
                r.problems.Add("An officer of this loadout is no longer in your roster.");
                continue;
            }
            var elsewhere = GamePlayer.current.GetOfficerShip(officer);
            if (elsewhere != null && elsewhere != ship)
            {
                r.problems.Add($"{officer.GetFullName()} is on {elsewhere.GetShipName()}.");
                continue;
            }
            if (ship.AssignOfficer(officer)) seated++;
            else r.problems.Add($"No free officer seat for {officer.GetFullName()}.");
        }
        if (seated + unseated > 0) r.done.Add($"officers: {seated} seated, {unseated} to the roster");
    }

    private static void Crew(SpaceShipData ship, Loadout l, ApplyReport r)
    {
        var player = GamePlayer.current;
        var reserve = player.unassignedCrew;
        var types = new List<string>(l.crew.Keys);
        foreach (var kv in ship.crewData.crew)
            if (!types.Contains(kv.Key)) types.Add(kv.Key);
        int toReserve = 0, fromReserve = 0, hired = 0;
        long spent = 0;

        // Surplus first, so the ship has room for the shortfalls.
        foreach (var type in types)
        {
            int surplus = Count(ship.crewData, type) - Want(l, type);
            if (surplus <= 0) continue;
            int move = System.Math.Min(surplus, System.Math.Max(0, player.maxReserve - reserve.totalCrew));
            if (move > 0)
            {
                ship.RemoveCrew(type, move);
                reserve.AddCrew(type, move, false);
                toReserve += move;
            }
            if (move < surplus) r.problems.Add($"{surplus - move} {CrewName(type)} stayed aboard: the crew reserve is full.");
        }

        var center = SpaceStationInterior.instance?.spacestation?.personnelCenter;
        foreach (var type in types)
        {
            int shortBy = Want(l, type) - Count(ship.crewData, type);
            if (shortBy <= 0) continue;
            int take = System.Math.Min(shortBy, Count(reserve, type));
            if (take > 0)
            {
                reserve.RemoveCrew(type, take, false);
                int overflow = ship.AddCrew(type, take, false);
                if (overflow > 0) reserve.AddCrew(type, overflow, false);
                fromReserve += take - overflow;
                shortBy -= take - overflow;
            }
            if (shortBy > 0 && center != null && CrewType.allCrew.TryGetValue(type, out var crewType))
            {
                center.available.TryGetValue(type, out int stock);
                long price = crewType.cost;
                int buy = System.Math.Min(System.Math.Min(shortBy, stock), ship.GetFreeCrewSpace());
                if (price > 0) buy = (int)System.Math.Min(buy, player.credits / price);
                if (buy > 0 && center.TryPurchaseCrew(type, buy))
                {
                    player.RemoveCreditsForCrew(buy * price, type);
                    ship.AddCrew(type, buy, false);
                    hired += buy;
                    spent += buy * price;
                    shortBy -= buy;
                }
            }
            if (shortBy > 0) r.problems.Add($"Short {shortBy} {CrewName(type)}: not in your reserve or for hire here.");
        }
        var parts = new List<string>();
        if (toReserve > 0) parts.Add(toReserve + " to the reserve");
        if (fromReserve > 0) parts.Add(fromReserve + " from the reserve");
        if (hired > 0) parts.Add($"{hired} hired for {GameMath.FormatNumber(spent)} cr");
        if (parts.Count > 0) r.done.Add("crew: " + string.Join(", ", parts));
    }

    private static int Count(CrewData data, string type) => data.crew.TryGetValue(type, out int n) ? n : 0;

    private static int Want(Loadout l, string type) => l.crew.TryGetValue(type, out int n) ? n : 0;

    private static string CrewName(string type) =>
        CrewType.allCrew.TryGetValue(type, out var t) ? Translation.Translate(string.IsNullOrEmpty(t.displayNamePlural) ? t.displayName : t.displayNamePlural) : type;

    private static void Drones(SpaceShipData ship, Loadout l, ApplyReport r)
    {
        var bay = ship.GetEquippedItem(EquipmentSlot.DroneBay)?.GetComponent<DroneBayModule>();
        if (bay == null)
        {
            if (l.drones.Exists(d => d != null)) r.problems.Add("No drone bay fitted, so no drones.");
            return;
        }
        int amount = bay.GetDroneAmount(ship);
        var unlocked = SkilltreeNode.dronesUnlockDrones1.isActive ? null : GamePlayer.current.GetUnlockedDrones();
        int changed = 0;
        for (int i = 0; i < amount; i++)
        {
            string id = i < l.drones.Count ? l.drones[i] : null;
            Drone drone = null;
            if (id != null && !Drone.GetAll().TryGetValue(id, out drone)) r.problems.Add($"Drone {id} no longer exists.");
            if (drone != null && unlocked != null && !unlocked.Contains(drone))
            {
                r.problems.Add($"{Translation.Translate(drone.displayName)} isn't unlocked.");
                drone = null;
            }
            while (ship.droneSlots.Count <= i) ship.droneSlots.Add(null);
            if (ship.droneSlots[i] == drone) continue;
            ship.droneSlots[i] = drone;
            changed++;
        }
        if (changed > 0) r.done.Add(changed + (changed == 1 ? " drone slot" : " drone slots") + " changed");
    }
}
