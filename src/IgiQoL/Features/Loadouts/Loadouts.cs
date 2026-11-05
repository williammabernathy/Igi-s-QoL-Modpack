using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.UI.HUD;
using Behaviour.UI.Spacestation;
using Behaviour.Util;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using LightJson;
using Source.Galaxy.POI;
using Source.Item;
using Source.Player;
using Source.SpaceShip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using PersonalHangarUI = Behaviour.UI.Spacestation.Location.PersonalHangar;

namespace IgiQoL.Features.Loadouts;

public sealed class Loadouts : Feature
{
    public static Loadouts Instance;

    public Loadouts() { Instance = this; }

    public override string Id => "Loadouts";
    public override string Title => "Ship Loadouts";
    public override string Description => "Saves whole fits per ship (weapons, modules, boosters, drones, officers, crew, skill preset, the ship's QoL settings and optionally ECHO's settings) and applies them in the hangar in one click. Open it from the Loadouts tab or the L button next to the hangar's skill preset button.";
    public override Type[] PatchClasses => new[] { typeof(LoadoutPatches) };

    // Ship switches only count once a save is up; SetSpaceShipData also runs while a save loads.
    internal static bool ready;

    // Guid of the ship whose switch was last handled; refits re-run SetSpaceShipData for the same ship.
    internal static string flownGuid;

    public override void OnActivated()
    {
        ModWindow.AddTab(this, "loadouts", "Loadouts", 25, LoadoutsTab.Build);
        GameEvents.SaveLoaded += () =>
        {
            LoadoutsTab.Reset();
            ready = true;
            var flown = GamePlayer.current != null ? GamePlayer.current.currentSpaceShip : null;
            flownGuid = flown != null ? flown.guid : null;
        };
        GameEvents.MainMenuShown += () => { ready = false; flownGuid = null; };
    }

    // Flying another ship brings back the ECHO settings of the loadout last applied to it.
    internal static void OnFlownShipChanged(SpaceShipData ship)
    {
        string last = LoadoutStore.LastApplied(ship.guid);
        if (last == null) return;
        var l = LoadoutStore.Find(ship.guid, last);
        if (l?.echo == null) return;
        LoadoutApply.RestoreEcho(l.echo);
        Plugin.Log.LogInfo($"[Loadouts] {ship.GetShipName()}: ECHO settings from loadout \"{last}\"");
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => LoadoutsCheck.Run(ctx);
}

// Loadouts live in the ship's mod record: "loadouts" (array) and "lastLoadout" (name).
public static class LoadoutStore
{
    public static List<Loadout> For(string guid)
    {
        var list = new List<Loadout>();
        var record = SaveData.Ship(guid, false);
        if (record == null || !record["loadouts"].IsJsonArray) return list;
        foreach (var v in record["loadouts"].AsJsonArray)
        {
            var l = Loadout.FromJson(v);
            if (l != null) list.Add(l);
        }
        return list;
    }

    public static Loadout Find(string guid, string name) => For(guid).Find(l => l.name == name);

    public static void Write(string guid, List<Loadout> list)
    {
        var record = SaveData.Ship(guid, true);
        if (list.Count == 0)
        {
            record.Remove("loadouts");
            return;
        }
        var arr = new JsonArray();
        foreach (var l in list) arr.Add(l.ToJson());
        record["loadouts"] = arr;
    }

    public static void Put(string guid, Loadout loadout)
    {
        var list = For(guid);
        int i = list.FindIndex(l => l.name == loadout.name);
        if (i >= 0) list[i] = loadout;
        else list.Add(loadout);
        Write(guid, list);
    }

    public static void Delete(string guid, string name)
    {
        var list = For(guid);
        list.RemoveAll(l => l.name == name);
        Write(guid, list);
        if (LastApplied(guid) == name) SetLastApplied(guid, null);
    }

    public static void Rename(string guid, string from, string to)
    {
        var list = For(guid);
        var l = list.Find(x => x.name == from);
        if (l == null) return;
        l.name = to;
        Write(guid, list);
        if (LastApplied(guid) == from) SetLastApplied(guid, to);
    }

    public static string LastApplied(string guid)
    {
        var v = SaveData.Ship(guid, false)?["lastLoadout"];
        return v.HasValue && v.Value.IsString ? v.Value.AsString : null;
    }

    public static void SetLastApplied(string guid, string name)
    {
        var record = SaveData.Ship(guid, true);
        if (name == null) record.Remove("lastLoadout");
        else record["lastLoadout"] = name;
    }

    // Loadouts saved on your other ships of the same hull.
    public static List<(SpaceShipData owner, Loadout loadout)> SameHull(SpaceShipData ship)
    {
        var result = new List<(SpaceShipData, Loadout)>();
        foreach (var other in GamePlayer.current.spaceShips)
        {
            if (other == ship || other.shipClass != ship.shipClass) continue;
            foreach (var l in For(other.guid)) result.Add((other, l));
        }
        return result;
    }
}

// Keeps the hangar's L button out of the way of the decal "-" button that shares its spot in decal mode.
internal sealed class HangarLoadoutButton : MonoBehaviour
{
    public GameObject decalButton;
    public Button button;

    private void LateUpdate()
    {
        bool show = decalButton == null || !decalButton.activeSelf;
        if (button.gameObject.activeSelf != show) button.gameObject.SetActive(show);
    }
}

[HarmonyPatch]
internal static class LoadoutPatches
{
    private static Loadouts F => Loadouts.Instance;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PersonalHangarUI), "Start")]
    private static void HangarStartPostfix(PersonalHangarUI __instance)
    {
        if (!F.Active || PersonalHangarUI.arenaMode) return;
        try { AddHangarButton(__instance); }
        catch (Exception e) { F.Fail(e, nameof(LoadoutPatches)); }
    }

    // A copy of the hangar's "S" (skill preset) button, one step further left than the decal-mode buttons start.
    internal static Button AddHangarButton(PersonalHangarUI hangar)
    {
        var template = hangar.skillLoadoutButton;
        if (template == null) return null;
        var go = Object.Instantiate(template.gameObject, template.transform.parent);
        go.name = "IgiLoadoutsButton";
        foreach (var t in go.GetComponentsInChildren<Translatable>(true)) Object.Destroy(t);
        var button = go.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() => LoadoutsTab.Open(hangar.selectedShipData ?? GamePlayer.current.currentSpaceShip));
        var text = go.GetComponentInChildren<TMP_Text>();
        if (text != null) text.text = "L";
        var rt = (RectTransform)go.transform;
        var src = (RectTransform)template.transform;
        rt.anchoredPosition = new Vector2(src.anchoredPosition.x - 132f, src.anchoredPosition.y);
        go.SetActive(true);
        Behavior.UI.GalaxyMap.FleetBarParts.SetTooltip(button, "Loadouts (QoL)", "Save this ship's whole fit, or apply a saved one: weapons, modules, boosters, drones, officers, crew and skill preset.");
        var keeper = go.AddComponent<HangarLoadoutButton>();
        keeper.button = button;
        keeper.decalButton = hangar.removeDecalButton != null ? hangar.removeDecalButton.gameObject : null;
        return button;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GamePlayer), nameof(GamePlayer.SetSpaceShipData))]
    private static void SetSpaceShipDataPostfix(SpaceShipData data)
    {
        if (!F.Active || !Loadouts.ready || data == null || data.guid == Loadouts.flownGuid) return;
        Loadouts.flownGuid = data.guid;
        try { Loadouts.OnFlownShipChanged(data); }
        catch (Exception e) { F.Fail(e, nameof(LoadoutPatches)); }
    }
}
