using System.Collections;
using System.Text;
using Behaviour.UI.Spacestation;
using IgiQoL.Core;
using IgiQoL.UI;
using LightJson;
using Source.Galaxy.POI;
using Source.Item;
using Source.Player;
using Source.SpaceShip;
using TMPro;
using UnityEngine;
using PersonalHangarUI = Behaviour.UI.Spacestation.Location.PersonalHangar;

namespace IgiQoL.Features.Loadouts;

internal static class LoadoutsCheck
{
    private const string NameA = "igi-check-A", NameEcho = "igi-check-echo";

    // Fitted pieces (by roll identity), crew, drones and officers as one comparable string.
    private static string Fit(SpaceShipData ship)
    {
        var l = Loadout.Capture(ship, "", false);
        var sb = new StringBuilder();
        foreach (var h in l.hardpoints) sb.Append(LoadoutApply.Identity(h)).Append(';');
        foreach (var kv in l.modules) sb.Append(kv.Key).Append('=').Append(LoadoutApply.Identity(kv.Value)).Append(';');
        foreach (var b in l.boosters) sb.Append(LoadoutApply.Identity(b)).Append(';');
        var o = l.ToJson();
        sb.Append(o["crew"].ToString()).Append(o["drones"].ToString()).Append(o["officers"].ToString());
        return sb.ToString();
    }

    private static int ArmoryEquipment()
    {
        int n = 0;
        foreach (var e in Inventory.global.items)
            if (e?.item != null && e.item.equipmentDefinition != null) n += e.count;
        return n;
    }

    private static string Report(ApplyReport r) => $"done [{string.Join("; ", r.done)}] problems [{string.Join("; ", r.problems)}]";

    public static IEnumerator Run(SelfCheckContext ctx)
    {
        var player = GamePlayer.current;
        var ship = player.currentSpaceShip;
        string lastBefore = LoadoutStore.LastApplied(ship.guid);
        var savedBefore = LoadoutStore.For(ship.guid);
        ctx.Cleanup(() =>
        {
            LoadoutStore.Write(ship.guid, savedBefore);
            LoadoutStore.SetLastApplied(ship.guid, lastBefore);
        });

        var a = Loadout.Capture(ship, NameA, true);
        string aj = a.ToJson().ToString();
        string back = Loadout.FromJson(Json(aj)).ToJson().ToString();
        if (aj == back) ctx.Pass("roundtrip", a.Summary());
        else ctx.Fail("roundtrip", $"{aj.Length} vs {back.Length} chars");
        // Saved loadouts come back from parsed JSON, so the checks apply the parsed copy too.
        a = Loadout.FromJson(Json(aj));

        // The harness's Window check undocks; dock again (the dev save sits at a station) and leave afterwards.
        bool docked = false;
        if (SpaceStationInterior.instance == null && Behaviour.Managers.BasePoiManager.current is SpacestationExteriorManager exterior)
        {
            exterior.EnterSpacestation();
            float wait = Time.realtimeSinceStartup + 30f;
            while (SpaceStationInterior.instance == null && Time.realtimeSinceStartup < wait) yield return null;
            yield return new WaitForSeconds(3f);
            docked = SpaceStationInterior.instance != null;
        }
        if (docked) ctx.Cleanup(() => { if (SpaceStationInterior.instance != null) SpaceStationInterior.instance.ExitSpacestation(); });

        string reason = LoadoutApply.CannotApplyReason(ship);
        if (reason != null)
        {
            ctx.Skip("apply", reason);
            yield break;
        }

        string before = Fit(ship);
        int armory = ArmoryEquipment();
        var r = LoadoutApply.Apply(ship, a);
        yield return new WaitForSeconds(1f);
        string d = Report(r);
        if (Fit(ship) == before && ArmoryEquipment() == armory) ctx.Pass("apply-same", d);
        else ctx.Fail("apply-same", d);

        // Take a weapon and a booster off, send 5 crew to the reserve, then apply the saved fit.
        int h = System.Array.FindIndex(ship.hardpoints, x => x != null);
        int b = System.Array.FindIndex(ship.boosters, x => x != null);
        string crewType = null;
        foreach (var kv in ship.crewData.crew)
            if (kv.Value >= 5) { crewType = kv.Key; break; }
        var sb = new StringBuilder();
        if (h >= 0)
        {
            Inventory.global.Add(ship.hardpoints[h], 1);
            sb.Append($"took weapon {h + 1}; ");
            ship.hardpoints[h] = null;
        }
        if (b >= 0)
        {
            Inventory.global.Add(ship.boosters[b], 1);
            ship.EquipBooster(null, b);
            sb.Append($"took booster {b + 1}; ");
        }
        if (crewType != null && player.maxReserve - player.unassignedCrew.totalCrew >= 5)
        {
            ship.RemoveCrew(crewType, 5);
            player.unassignedCrew.AddCrew(crewType, 5, false);
            sb.Append($"5 {crewType} to reserve; ");
        }
        r = LoadoutApply.Apply(ship, a);
        yield return new WaitForSeconds(1f);
        d = sb + Report(r);
        if (Fit(ship) == before && ArmoryEquipment() == armory) ctx.Pass("refit", d);
        else ctx.Fail("refit", d + $" fitMatches={Fit(ship) == before} armory {armory}->{ArmoryEquipment()}");

        // Crew that is neither aboard nor in the reserve gets hired at this station's personnel center.
        var center = SpaceStationInterior.instance.spacestation.personnelCenter;
        string hireType = null;
        if (center != null)
            foreach (var kv in ship.crewData.crew)
                if (kv.Value >= 3 && center.available.TryGetValue(kv.Key, out int stock) && stock >= 3) { hireType = kv.Key; break; }
        if (hireType == null) ctx.Skip("hire", center == null ? "no personnel center here" : "no crew type aboard that the personnel center stocks 3 of");
        else
        {
            player.unassignedCrew.crew.TryGetValue(hireType, out int reserved);
            if (reserved > 0) player.unassignedCrew.RemoveCrew(hireType, reserved, false);
            ship.RemoveCrew(hireType, 3);
            long credits = player.credits;
            center.available.TryGetValue(hireType, out int stockBefore);
            long spent = 0;
            int stockAfter = stockBefore;
            try
            {
                r = LoadoutApply.Apply(ship, a);
                yield return new WaitForSeconds(1f);
                center.available.TryGetValue(hireType, out stockAfter);
                spent = credits - player.credits;
                d = $"{hireType}: stock {stockBefore}->{stockAfter}, spent {spent} cr; {Report(r)}";
                if (Fit(ship) == before && stockAfter == stockBefore - 3 && spent > 0) ctx.Pass("hire", d);
                else ctx.Fail("hire", d);
            }
            finally
            {
                // The check hired for real: put the reserve crew, the credits and the station's stock back.
                if (reserved > 0) player.unassignedCrew.AddCrew(hireType, reserved, false);
                player.credits = credits;
                if (stockBefore > 0) center.available[hireType] = stockBefore;
            }
        }

        // Swap two weapons of the same slot size, then put them back.
        int i = -1, j = -1;
        var hps = ship.shipClass.hardpointSlots;
        for (int x = 0; x < ship.hardpoints.Length && i < 0; x++)
            for (int y = x + 1; y < ship.hardpoints.Length; y++)
                if (ship.hardpoints[x] != null && ship.hardpoints[y] != null && hps[x].size == hps[y].size && LoadoutApply.Identity(a.hardpoints[x]) != LoadoutApply.Identity(a.hardpoints[y]))
                {
                    i = x;
                    j = y;
                    break;
                }
        if (i < 0) ctx.Skip("swap", "no two different weapons in same-size slots");
        else
        {
            var swapped = Loadout.FromJson(Json(aj));
            (swapped.hardpoints[i], swapped.hardpoints[j]) = (swapped.hardpoints[j], swapped.hardpoints[i]);
            r = LoadoutApply.Apply(ship, swapped);
            yield return new WaitForSeconds(1f);
            bool moved = LoadoutApply.Identity(ship.hardpoints[i].ToJson()) == LoadoutApply.Identity(a.hardpoints[j]) && LoadoutApply.Identity(ship.hardpoints[j].ToJson()) == LoadoutApply.Identity(a.hardpoints[i]);
            bool sameArmory = ArmoryEquipment() == armory;
            var r2 = LoadoutApply.Apply(ship, a);
            yield return new WaitForSeconds(1f);
            d = $"weapons {i + 1}<->{j + 1}: swapped={moved} armory unchanged={sameArmory}; back={Fit(ship) == before}; {Report(r)}";
            if (moved && sameArmory && Fit(ship) == before) ctx.Pass("swap", d);
            else ctx.Fail("swap", d + " / back: " + Report(r2));
        }

        if (h < 0) ctx.Skip("missing", "no weapon fitted");
        else
        {
            // A weapon that doesn't exist anywhere: reported, the fitted one stays.
            var missing = Loadout.FromJson(Json(aj));
            var w = missing.hardpoints[h].AsJsonObject;
            w["equipmentType"] = "IgiNoSuchWeapon";
            r = LoadoutApply.Apply(ship, missing);
            yield return new WaitForSeconds(1f);
            d = Report(r);
            if (Fit(ship) == before && r.problems.Exists(p => p.StartsWith($"Missing for weapon slot {h + 1}"))) ctx.Pass("missing", d);
            else ctx.Fail("missing", d);

            // The same weapon with a different roll: the closest piece of that type and rarity is fitted and flagged.
            var other = Loadout.FromJson(Json(aj));
            var stat = other.hardpoints[h].AsJsonObject["stats"].AsJsonArray[0].AsJsonObject;
            stat["amount"] = stat["amount"].AsNumber + 1;
            r = LoadoutApply.Apply(ship, other);
            yield return new WaitForSeconds(1f);
            d = Report(r);
            string fitted = ship.hardpoints[h]?.ToJson()["equipmentType"].AsString;
            if (fitted == a.hardpoints[h]["equipmentType"].AsString && r.problems.Exists(p => p.Contains("is gone; fitted"))) ctx.Pass("substitute", d);
            else ctx.Fail("substitute", d + $" fitted={fitted}");
            LoadoutApply.Apply(ship, a);
            yield return new WaitForSeconds(1f);
        }

        // Another skill preset: the captain re-invests and the ship remembers the new preset; then back.
        var captain = player.commander;
        var otherPreset = captain.loadouts.Find(p => p.name != ship.skillLoadout);
        if (otherPreset == null) ctx.Skip("skill-preset", "the captain has only one skill preset");
        else
        {
            string presetBefore = ship.skillLoadout;
            int investedBefore = captain.GetInvestedSkillPoints();
            var skills = Loadout.FromJson(Json(aj));
            skills.skill = otherPreset.name;
            r = LoadoutApply.Apply(ship, skills);
            yield return new WaitForSeconds(1f);
            string switched = ship.skillLoadout;
            int investedOther = captain.GetInvestedSkillPoints();
            var back2 = LoadoutApply.Apply(ship, a);
            yield return new WaitForSeconds(1f);
            d = $"\"{presetBefore}\" ({investedBefore} pts) -> \"{switched}\" ({investedOther} pts) -> \"{ship.skillLoadout}\" ({captain.GetInvestedSkillPoints()} pts); {Report(r)}";
            bool failedOnPoints = r.problems.Exists(p => p.Contains("skill points"));
            if (failedOnPoints) ctx.Skip("skill-preset", d);
            else if (switched == otherPreset.name && ship.skillLoadout == presetBefore && captain.GetInvestedSkillPoints() == investedBefore) ctx.Pass("skill-preset", d);
            else ctx.Fail("skill-preset", d + " / back: " + Report(back2));
        }

        // A fleet ship: take a weapon off and apply its own loadout.
        SpaceShipData wing = null;
        foreach (var s in player.activeFleet)
            if (s != null && s != ship && LoadoutApply.CannotApplyReason(s) == null && System.Array.Exists(s.hardpoints, x => x != null)) { wing = s; break; }
        if (wing == null)
        {
            var why = new StringBuilder();
            foreach (var s in player.activeFleet)
                if (s != null) why.Append($"{s.GetShipName()}: reason={LoadoutApply.CannotApplyReason(s) ?? "-"} weapons={System.Array.FindAll(s.hardpoints, x => x != null).Length}/{s.hardpoints.Length}; ");
            ctx.Skip("fleet-ship", "no fleet ship with a weapon: " + why);
        }
        else
        {
            var wl = Loadout.FromJson(Json(Loadout.Capture(wing, "igi-check-wing", false).ToJson().ToString()));
            string wingBefore = Fit(wing);
            int wh = System.Array.FindIndex(wing.hardpoints, x => x != null);
            Inventory.global.Add(wing.hardpoints[wh], 1);
            wing.hardpoints[wh] = null;
            r = LoadoutApply.Apply(wing, wl);
            yield return new WaitForSeconds(1f);
            d = $"{wing.GetShipName()} weapon {wh + 1}: {Report(r)}";
            if (Fit(wing) == wingBefore && ArmoryEquipment() == armory) ctx.Pass("fleet-ship", d);
            else ctx.Fail("fleet-ship", d);
        }

        // Flying a ship restores the ECHO settings of the loadout last applied to it.
        var original = player.autopilotSettings.ToJson();
        var echo = Loadout.FromJson(Json(aj));
        echo.name = NameEcho;
        int minutes = player.autopilotSettings.ammoMinutes + 7;
        echo.echo["autopilot"].AsJsonObject["ammoMinutes"] = minutes;
        LoadoutStore.Put(ship.guid, echo);
        LoadoutStore.SetLastApplied(ship.guid, NameEcho);
        Loadouts.OnFlownShipChanged(ship);
        int got = player.autopilotSettings.ammoMinutes;
        player.autopilotSettings = AutopilotSettings.FromJson(original);
        if (got == minutes) ctx.Pass("echo-on-switch", $"ammoMinutes -> {got}");
        else ctx.Fail("echo-on-switch", $"ammoMinutes {got}, expected {minutes}");

        LoadoutStore.Put(ship.guid, a);
        LoadoutStore.SetLastApplied(ship.guid, NameA);
        LoadoutsTab.Open(ship);
        yield return null;
        bool row = false;
        if (ModWindow.IsOpen)
            foreach (var t in ModWindow.Content.GetComponentsInChildren<TMP_Text>())
                row |= t.text.StartsWith(NameA + "  (last applied)");
        if (ModWindow.CurrentTab == "loadouts" && row) ctx.Pass("tab", $"tab={ModWindow.CurrentTab}");
        else ctx.Fail("tab", $"tab={ModWindow.CurrentTab} row={row}");
        yield return ctx.Screenshot("window-loadouts");
        ModWindow.Close();

        SpaceStationInterior.instance.GoToLocation(SpaceStationFacility.PersonalHangar);
        float until = Time.realtimeSinceStartup + 10f;
        while (PersonalHangarUI.current == null && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSeconds(1f);
        var hangar = PersonalHangarUI.current;
        var button = hangar != null ? hangar.skillLoadoutButton.transform.parent.Find("IgiLoadoutsButton") : null;
        if (hangar == null) ctx.Skip("hangar-button", "hangar screen didn't open");
        else if (button != null && button.gameObject.activeInHierarchy) ctx.Pass("hangar-button", $"at {((RectTransform)button).anchoredPosition}");
        else ctx.Fail("hangar-button", button == null ? "no button" : "button hidden");
        yield return ctx.Screenshot("hangar-loadouts");

        // Hired mercenaries aren't offered, and applying to one is refused.
        bool listed = LoadoutsTab.Ships().Exists(s => s.commanderData is Source.Personnel.Mercenary);
        if (!listed) ctx.Pass("no-mercenary", "no hired ship in the Loadouts list");
        else ctx.Fail("no-mercenary", "a hired ship is in the Loadouts list");
        var merc = player.activeFleet.Find(s => s != null && s.commanderData is Source.Personnel.Mercenary);
        if (merc == null) ctx.Skip("no-mercenary-reason", "no mercenary hired");
        else
        {
            string why = LoadoutApply.CannotApplyReason(merc);
            if (why != null && why.Contains("hired ship")) ctx.Pass("no-mercenary-reason", why);
            else ctx.Fail("no-mercenary-reason", why ?? "no reason given");
        }
    }

    private static JsonValue Json(string text) => LightJson.Serialization.JsonReader.Parse(text);
}
