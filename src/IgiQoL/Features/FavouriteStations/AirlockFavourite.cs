using System;
using Behaviour.UI.Shared;
using Behaviour.UI.Spacestation;
using Behaviour.UI.Tooltip;
using Source.Galaxy.POI;
using UnityEngine;

namespace IgiQoL.Features.FavouriteStations;

// A Favourite / Unfavourite button at the start of the airlock's text buttons (the row in the action card under the station
// characters), made like the airlock's own.
internal sealed class AirlockFavourite : MonoBehaviour
{
    public const string Name = "IgiFavouriteStation";

    private Airlock airlock;
    internal GameObject button;
    private float nextRefresh;
    private bool? shownFavourite;

    public static void Attach(Airlock airlock)
    {
        if (airlock.textButtons == null || airlock.GetComponent<AirlockFavourite>() != null) return;
        var c = airlock.gameObject.AddComponent<AirlockFavourite>();
        c.airlock = airlock;
        // Made like the game's own text buttons (height 30, font 12); first in the row, so the game's situational buttons and
        // Station services keep their places after it.
        var b = UiButton.Add(airlock.textButtons, "Favourite", c.Click, ButtonRole.Plain, 0f, null, null, 30f, 12f);
        b.name = Name;
        var go = b.gameObject;
        Airlock.IgnoreForEcho(go);
        go.transform.SetAsFirstSibling();
        c.button = go;
        c.Refresh();
        airlock.RefreshDivider();
    }

    private void Click()
    {
        var f = FavouriteStations.Instance;
        if (!f.Active || SpaceStation.current == null) return;
        try
        {
            FavouriteStations.Toggle(SpaceStation.current, notify: true);
            Refresh();
        }
        catch (Exception e) { f.Fail(e, nameof(AirlockFavourite)); }
    }

    private void Update()
    {
        if (button == null || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.5f;
        var f = FavouriteStations.Instance;
        if (!f.Active)
        {
            if (button.activeSelf)
            {
                button.SetActive(false);
                airlock.RefreshDivider();
            }
            return;
        }
        // The tab and the galaxy map can change the list while you're docked.
        try { Refresh(); }
        catch (Exception e) { f.Fail(e, nameof(AirlockFavourite)); }
    }

    internal void Refresh()
    {
        // The game puts its situational buttons (Build outpost) at the front of the row when they show up later.
        if (button.transform.GetSiblingIndex() != 0) button.transform.SetAsFirstSibling();
        bool favourite = FavouriteStations.IsFavourite(SpaceStation.current);
        if (shownFavourite == favourite) return;
        shownFavourite = favourite;
        string text = favourite ? "Unfavourite" : "Favourite";
        // Sizes the button to its label, as the game's own text buttons are.
        Airlock.SetButtonLabel(button, text);
        Airlock.SetButtonState(button, true, favourite
            ? "Takes this station off the favourites on the QoL window's Stations tab."
            : "Adds this station to the favourites on the QoL window's Stations tab, which shows what it has, your storage here, the jumps to it and a Travel to button.");
        var tip = button.GetComponent<TooltipSource>();
        if (tip != null) tip.Title = "Favourite station (QoL)";
        // The label's width moved every button after it; let the services row re-check that it still fits.
        var services = airlock.GetComponent<StationServices.AirlockServices>();
        var ss = StationServices.StationServices.Instance;
        if (services != null && ss.Active && ss.AirlockButtons.Value)
        {
            try { services.Refresh(); }
            catch (System.Exception e) { ss.Fail(e, nameof(StationServices.AirlockServices)); }
        }
    }
}
