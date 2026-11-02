using System;
using Behaviour.UI.Spacestation;
using Behaviour.UI.Tooltip;
using Behaviour.Util;
using Source.Galaxy.POI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.FavouriteStations;

// A Favourite / Unfavourite button at the start of the airlock's button row, cloned from the airlock's own buttons.
internal sealed class AirlockFavourite : MonoBehaviour
{
    public const string Name = "IgiFavouriteStation";

    private Airlock airlock;
    internal GameObject button;
    private TMP_Text label;
    private float nextRefresh;
    private bool? shownFavourite;

    public static void Attach(Airlock airlock)
    {
        if (airlock.homeStationButton == null || airlock.GetComponent<AirlockFavourite>() != null) return;
        var c = airlock.gameObject.AddComponent<AirlockFavourite>();
        c.airlock = airlock;
        var template = airlock.homeStationButton;
        var go = Object.Instantiate(template, template.transform.parent);
        go.name = Name;
        foreach (var t in go.GetComponentsInChildren<Translatable>(true)) Object.Destroy(t);
        var b = go.GetComponent<Button>();
        b.onClick = new Button.ButtonClickedEvent();
        b.onClick.AddListener(c.Click);
        var rt = (RectTransform)go.transform;
        airlock.PlaceBelowCharacters(rt);
        // First in the row, so the game's situational buttons and Station services keep their places after it.
        airlock.buttonRow.Remove(rt);
        airlock.buttonRow.Insert(0, rt);
        go.SetActive(true);
        c.button = go;
        c.label = go.GetComponentInChildren<TMP_Text>();
        c.Refresh();
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
            if (button.activeSelf) button.SetActive(false);
            return;
        }
        // The tab and the galaxy map can change the list while you're docked.
        try { Refresh(); }
        catch (Exception e) { f.Fail(e, nameof(AirlockFavourite)); }
    }

    internal void Refresh()
    {
        bool favourite = FavouriteStations.IsFavourite(SpaceStation.current);
        if (shownFavourite == favourite) return;
        shownFavourite = favourite;
        string text = favourite ? "Unfavourite" : "Favourite";
        label.text = text;
        var rt = (RectTransform)button.transform;
        rt.sizeDelta = new Vector2(Mathf.Max(80f, label.GetPreferredValues(text).x + 24f), rt.sizeDelta.y);
        airlock.SetButtonState(button, true, airlock.GetBaseButtonColor(), favourite
            ? "Takes this station off the favourites on the QoL window's Stations tab."
            : "Adds this station to the favourites on the QoL window's Stations tab, which shows what it has, your storage here, the jumps to it and a Travel to button.");
        var tip = button.GetComponent<TooltipSource>();
        if (tip != null) tip.Title = "Favourite station (QoL)";
    }
}
