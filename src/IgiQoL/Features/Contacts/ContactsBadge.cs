using System;
using System.Text;
using Behavior.UI.GalaxyMap;
using Behaviour.Salvage;
using Behaviour.UI.HUD;
using Behaviour.UI.Shared;
using Behaviour.Unit;
using IgiQoL.Features.Window;
using IgiQoL.Game;
using IgiQoL.UI;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.Contacts;

// A one-line counter above the Dock button (above the QoL button when that sits there too): what is left to board,
// salvage or tractor here. Click opens the Scanner tab.
public sealed class ContactsBadge : MonoBehaviour
{
    public static ContactsBadge Instance;

    private RectTransform rt;
    private Image bg;
    private TMP_Text text;
    private Button button;
    private float nextRefresh;
    private bool hudShown;
    private readonly StringBuilder sb = new();

    public string Text => text != null ? text.text : "";
    public bool Shown => bg != null && bg.enabled;

    internal static void Create()
    {
        if (Instance != null) return;
        var dock = (RectTransform)HudManager.Instance.dockButton.transform;
        var rt = UiCards.NewChild("IgiQoL_Contacts", dock.parent);
        rt.anchorMin = dock.anchorMin;
        rt.anchorMax = dock.anchorMax;
        rt.pivot = dock.pivot;
        rt.sizeDelta = new Vector2(260f, 18f);
        var bg = UiCards.AddBackground(rt.gameObject, ColorHelper.panelHeaderBlue);
        var text = FleetBarParts.AddLine(rt, "", 11f, ColorHelper.offWhite, TextAlignmentOptions.Center);
        UiCards.Stretch((RectTransform)text.transform);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(OnClick);
        FleetBarParts.SetTooltip(button, null, "What is left at this location. Click for the Scanner tab, or to find the Scanner window when it is open.");
        Instance = rt.gameObject.AddComponent<ContactsBadge>();
        Instance.rt = rt;
        Instance.bg = bg;
        Instance.text = text;
        Instance.button = button;
        Instance.Refresh();
    }

    private void Update()
    {
        if (!Contacts.Instance.Active)
        {
            // Failed: hide the counter and stop updating it.
            bg.enabled = text.enabled = button.interactable = false;
            enabled = false;
            return;
        }
        // Follows the HUD at once when it hides (docking), otherwise refreshes once per second.
        bool hud = HudManager.Instance != null && HudManager.Instance.showHud;
        if (hud == hudShown && Time.unscaledTime < nextRefresh) return;
        hudShown = hud;
        nextRefresh = Time.unscaledTime + 1f;
        try { Refresh(); }
        catch (Exception e) { Contacts.Instance.Fail(e, nameof(ContactsBadge)); }
    }

    private void Refresh()
    {
        int total = Contacts.CountHere(out int b, out int w, out int l);
        bool show = Contacts.Instance.HudCounter.Value && hudShown && Fleet.Flagship != null && total > 0;
        bg.enabled = text.enabled = button.interactable = show;
        if (!show) return;
        Follow();
        sb.Clear();
        if (b > 0) sb.Append(b).Append(" boardable");
        if (w > 0) { if (sb.Length > 0) sb.Append(" | "); sb.Append(w).Append(w == 1 ? " wreck" : " wrecks"); }
        if (l > 0) { if (sb.Length > 0) sb.Append(" | "); sb.Append(l).Append(" loot"); }
        string s = sb.ToString();
        if (text.text != s) text.text = s;
    }

    // Above the QoL button while it shares the Dock button's parent (its "Above the Dock button" place), else above Dock.
    private void Follow()
    {
        var dock = (RectTransform)HudManager.Instance.dockButton.transform;
        var qol = HudButton.Instance != null ? (RectTransform)HudButton.Instance.transform : null;
        var below = qol != null && qol.parent == rt.parent ? qol : dock;
        rt.anchoredPosition = below.anchoredPosition + new Vector2(0f, below.rect.height + 4f);
    }

    private static void OnClick()
    {
        var popout = Popouts.Get(Contacts.PopoutId);
        if (popout != null) popout.Flash();
        else ModWindow.Open("contacts");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
