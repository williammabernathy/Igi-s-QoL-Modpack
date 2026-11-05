using System;
using System.Collections;
using Behavior.UI.GalaxyMap;
using Behaviour.GalaxyMap;
using Behaviour.UI.Tooltip;
using HarmonyLib;
using IgiQoL.Core;
using IgiQoL.UI;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using Behaviour.UI;
using Source.Galaxy;
using Source.Galaxy.POI;
using Source.Simulation.Story;
using Source.UI.Tooltip;
using Source.Util;
using UnityEngine;

namespace IgiQoL.Features.ConquestDetails;

// Shows the numbers behind the Conquest war that the game keeps to itself: what a system's Defense row is made of, what
// it takes to capture it and what the player can do about it, what a fleet's order means and how close it is to
// retreating, the hidden faction ledger and fleet slots, and a War overview window with every faction's share and the
// targets its fleets pick from.
public sealed class ConquestDetails : Feature
{
    public static ConquestDetails Instance;

    public ConquestDetails() { Instance = this; }

    public const string PopoutId = "war-overview";

    public override string Id => "ConquestDetails";
    public override string Title => "Conquest Details";
    public override string Description => "Adds the numbers the Conquest war keeps hidden. A war-zone system's hover card on the galaxy map gets a short War block: what to do about the system (leave it, help, kill its hub, or skip it), the next-battle strength NPC fleets compare against, how many fleets an attack needs and what a capture needs. Hold a key for the full breakdown of the Defense row, the siege stages and how one full fleet would fare. Fleet cards say how close a fleet is to retreating and which of its three meanings \"Reinforcing\" has. Station cards show their firepower, garrison and reputation cost, and a hub's card says which faction would get the system if you destroyed it. The Faction overview gains each faction's live system count, what 30% needs, its fleet slots, home subsidy and Great Gate state, plus the game's own ledger card that the released game hides. The map's corner panel shows the hidden fleet orders and economy timers and a button for the War Overview window.";
    public override Type[] PatchClasses => new[] { typeof(ConquestDetailsPatches) };

    public ConfigEntry<SystemCardDetail> CardDetail;
    public ConfigEntry<Key> DetailKey;

    // Set while the War overview builds a system card: the window is where you study systems, so its cards are full.
    internal static bool ForceFull;

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        CardDetail = cfg.Bind(Id, "CardDetail", SystemCardDetail.Summary, SettingInfo.Describe("System card detail",
            "How much a war-zone system's hover card adds. Short card: a War block of a few rows with what to do, and the full breakdown while you hold the key below. Always the full breakdown: the whole breakdown at all times; a tall card may then fill most of the screen. The War Overview window always shows the full breakdown.",
            "Short card, full breakdown while the key is held", "Always the full breakdown"));
        DetailKey = cfg.Bind(Id, "DetailKey", Key.LeftShift, SettingInfo.Describe("Key for the full breakdown",
            "Hold this key while a war-zone system's card shows to see the full breakdown. Type a key name such as LeftShift, LeftCtrl or LeftAlt; either Shift, Ctrl or Alt key counts. Ignored while you type in a text field."));
    }

    // Whether a system card built now shows the full breakdown.
    internal bool WantFull() => ForceFull || CardDetail.Value == SystemCardDetail.Always || HeldKey.IsDown(DetailKey.Value);

    public override void OnActivated()
    {
        Plugin.Instance.gameObject.AddComponent<DetailKeyWatcher>();
        Popouts.Register(new PopoutKind
        {
            Id = PopoutId,
            Title = "War Overview",
            Owner = this,
            DefaultSize = new Vector2(900f, 480f),
            MinSize = new Vector2(760f, 160f),
            SortingOrder = 4,
            DefaultShow = PopoutShow.WhenRelevant,
            RelevantLabel = "the galaxy map is open",
            Relevant = AbstractGalaxyMapManager.IsShowing,
            Build = WarOverview.Build,
            BuildOptions = WarOverview.BuildOptions,
        });
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => ConquestDetailsCheck.Run(ctx);
}

// The rows the Faction overview's conquest section gains, and the one that counts down to the next conquest update.
internal sealed class FactionOverviewRows : MonoBehaviour
{
    internal FactionStatRow Countdown;

    internal static void Add(FactionOverviewContent content, Conquest conquest, Faction faction)
    {
        int total = conquest.systems.Count;
        if (total == 0) return;
        int owned = WarMath.CountOwned(conquest, faction), need = WarMath.NeededFor30(total);
        Row(content, "Systems held now", owned + " of " + total + " (" + WarMath.Pct(WarMath.Share(owned, total)) + ")",
            "Counted live. Area Conquered above, and the 30% missions' objective, show the share stored at the last conquest update.");
        Row(content, "For 30%", owned >= need ? "reached (" + need + " systems)" : need + " systems, " + (need - owned) + " more",
            "Systems the faction must own for \"Hostile Takeover\", \"The People's Sector\" or \"Harmony\". The objective checks the stored share, so a capture counts from the next conquest update.");
        var self = content.GetComponent<FactionOverviewRows>();
        if (self == null) self = content.gameObject.AddComponent<FactionOverviewRows>();
        self.Countdown = Row(content, "Area Conquered refreshes in", WarMath.Timer(conquest.conquestTickTime),
            "The conquest update: the \"Conquest Update\" countdown on the galaxy map. Abandoning a system (a destroyed hub) also refreshes it at once.");

        var economy = WarMath.Universe?.PeekFactionEconomy(faction);
        if (economy == null) return;
        Row(content, "Fleet slots", economy.fleetSlotsUsed + " of " + ConquestFleetPlanner.MaxFleetsFor(faction),
            "Fleets fielded out of the faction's cap; a merged assault fleet takes several slots. While it is below the cap, every warship it builds costs half and builds three times faster.");
        float share = ConquestScope.GetWarZoneShare(faction);
        if (ConquestWarTuning.SubsidyFactions.Contains(faction))
            Row(content, "Home subsidy", GameMath.FormatNumber(GreatGateReinforcement.GetSubsidy(faction)) + " an hour",
                "Wealth paid every economy tick. It shrinks as the faction's share of the war zone grows and stops at a third.");
        if (ConquestWarTuning.GreatGateFactions.Contains(faction))
        {
            bool wanted = GreatGateReinforcement.NeedsReinforcement(economy);
            string when = !wanted ? "not needed"
                : economy.greatGateCooldown > 0f ? "in " + Mathf.CeilToInt(economy.greatGateCooldown) + " economy ticks"
                : "next economy tick";
            bool free = economy.fleets.Count == 0 || share < ConquestWarTuning.GreatGateFreeFleetShare;
            Row(content, "Great Gate fleet", wanted ? when + (free ? ", free" : ", paid") : when,
                "A 1,500-point fleet from the Great Gate, sent while fewer than half the fleet slots are filled, at most once every 13 economy ticks. Free under 10% of the war zone or with no fleets left, otherwise paid from wealth.");
        }
        if (share < ConquestWarTuning.PeaceShareThreshold)
            Row(content, "Under 10% of the war zone", "ignores factions under 10%",
                "A faction holding under 10% of the war zone attacks only unowned systems and factions holding 10% or more, unless that leaves it nothing to attack.");
    }

    internal static void Refresh(FactionOverviewContent content)
    {
        var self = content.GetComponent<FactionOverviewRows>();
        var conquest = WarMath.Conquest;
        if (self == null || self.Countdown == null || conquest == null) return;
        self.Countdown.SetValue("Area Conquered refreshes in", WarMath.Timer(conquest.conquestTickTime));
    }

    private static FactionStatRow Row(FactionOverviewContent content, string label, string value, string help)
    {
        var row = content.AddRow(content.conquestRowParent, content.conquestRows);
        row.SetValue(label, value);
        Kit.Tooltip(row.label, label, help);
        return row;
    }
}

public enum SystemCardDetail { Summary, Always }

// Rebuilds the shown system card when the detail key is pressed or let go, so the card swaps between the short and the
// full form without moving the mouse.
internal sealed class DetailKeyWatcher : MonoBehaviour
{
    private static UITooltip owner;
    private static bool shownFull;

    internal static void Built(bool full)
    {
        owner = UITooltip._current;
        shownFull = full;
    }

    private void Update()
    {
        // Unity's null check: the card is destroyed when the mouse leaves.
        if (owner == null) return;
        var f = ConquestDetails.Instance;
        if (!f.Active || f.CardDetail.Value != SystemCardDetail.Summary) return;
        bool held = HeldKey.IsDown(f.DetailKey.Value);
        if (held == shownFull) return;
        try
        {
            // Built() runs again inside the refresh and records the new state; the stale state stops a second pass.
            shownFull = held;
            if (UITooltip._current == owner && owner.Source != null) UITooltip.Refresh(owner.Source);
        }
        catch (Exception e) { f.Fail(e, nameof(DetailKeyWatcher)); }
    }
}

[HarmonyPatch]
internal static class ConquestDetailsPatches
{
    private static ConquestDetails F => ConquestDetails.Instance;

    // Every system hover card: the galaxy map, the sector view and the War overview's rows.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapTooltipCardBuilder), nameof(MapTooltipCardBuilder.BuildSystem))]
    private static void BuildSystemPostfix(SystemMapData system, TooltipCard __result)
    {
        if (!F.Active || __result == null) return;
        try
        {
            var cs = WarMath.Of(system);
            if (cs == null || WarMath.Universe == null) return;
            bool full = F.WantFull();
            WarCards.AddSystem(__result, cs, full);
            // A short card is rebuilt when the key is pressed or let go, and only a card the player asks for by hand
            // changes with the key: the War overview's cards and the always-full setting don't.
            if (!ConquestDetails.ForceFull && F.CardDetail.Value == SystemCardDetail.Summary) DetailKeyWatcher.Built(full);
        }
        catch (Exception e) { F.Fail(e, nameof(BuildSystemPostfix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ConquestFleetMapIcon), nameof(ConquestFleetMapIcon.DescribeTooltip))]
    private static void FleetTooltipPostfix(ConquestFleet ___fleet, TooltipCard __result)
    {
        if (!F.Active || __result == null || ___fleet == null) return;
        try { __result.sections.Add(WarCards.FleetSection(___fleet)); }
        catch (Exception e) { F.Fail(e, nameof(FleetTooltipPostfix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(WarshipDeliveryMapIcon), nameof(WarshipDeliveryMapIcon.DescribeTooltip))]
    private static void DeliveryTooltipPostfix(TooltipCard __result)
    {
        if (!F.Active || __result == null) return;
        try { __result.AddNote(WarCards.DeliveryNote, ColorHelper.lightCyan); }
        catch (Exception e) { F.Fail(e, nameof(DeliveryTooltipPostfix)); }
    }

    // Station hover cards in a war-zone system, on the map and in space.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PoiTooltipCardBuilder), nameof(PoiTooltipCardBuilder.Build))]
    private static void StationTooltipPostfix(MapPointOfInterest poi, TooltipCard __result)
    {
        if (!F.Active || __result == null || !(poi is SpaceStation station) || station.faction == null) return;
        try
        {
            var cs = WarMath.Of(station.system);
            if (cs == null || WarMath.Universe == null) return;
            var section = WarCards.StationSection(station, cs);
            if (section != null) __result.sections.Add(section);
        }
        catch (Exception e) { F.Fail(e, nameof(StationTooltipPostfix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactionOverviewContent), nameof(FactionOverviewContent.AddConquestRows))]
    private static void AddConquestRowsPostfix(FactionOverviewContent __instance, Conquest conquest, Faction targetFaction)
    {
        if (!F.Active) return;
        try { FactionOverviewRows.Add(__instance, conquest, targetFaction); }
        catch (Exception e) { F.Fail(e, nameof(AddConquestRowsPostfix)); }
    }

    // The ledger, yard count, builds, deliveries and build history card. The game builds it only inside the Unity
    // editor (shipyardCard.SetActive(Application.isEditor)); turned on, the game fills and refreshes it itself.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactionOverviewContent), nameof(FactionOverviewContent.SetFaction))]
    private static void SetFactionPostfix(FactionOverviewContent __instance, Faction ___faction, bool ___headerOnly)
    {
        if (!F.Active || ___headerOnly || __instance.shipyardCard == null || __instance.shipyardCard.activeSelf) return;
        try
        {
            __instance.shipyardCard.SetActive(true);
            __instance.BuildShipyardSection(FactionOverviewContent.GetEconomy(___faction));
            // The prefab's title says "(editor only)"; in this game it isn't.
            foreach (var text in __instance.shipyardCard.GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (text.text.Contains(" (editor only)")) text.text = text.text.Replace(" (editor only)", "");
        }
        catch (Exception e) { F.Fail(e, nameof(SetFactionPostfix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactionOverviewContent), nameof(FactionOverviewContent.RefreshLiveValues))]
    private static void RefreshLiveValuesPostfix(FactionOverviewContent __instance)
    {
        if (!F.Active) return;
        try { FactionOverviewRows.Refresh(__instance); }
        catch (Exception e) { F.Fail(e, nameof(RefreshLiveValuesPostfix)); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapCornerPanel), nameof(MapCornerPanel.Init))]
    private static void CornerPanelInitPostfix(MapCornerPanel __instance)
    {
        if (!F.Active) return;
        try { MapCornerExtras.Add(__instance); }
        catch (Exception e) { F.Fail(e, nameof(CornerPanelInitPostfix)); }
    }
}
