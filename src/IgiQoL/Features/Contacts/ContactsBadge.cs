using System;
using System.Collections;
using System.Text;
using Behavior.UI.GalaxyMap;
using Behaviour.Salvage;
using Behaviour.UI.Shared;
using Behaviour.Unit;
using IgiQoL.Features.Window;
using IgiQoL.UI;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.Contacts;

// A one-line counter above the QoL HUD button: what is left to board, salvage or tractor here. Click opens the Contacts tab.
public sealed class ContactsBadge : MonoBehaviour
{
    public static ContactsBadge Instance;

    private Image bg;
    private TMP_Text text;
    private Button button;
    private float nextRefresh;
    private bool hudShown;
    private readonly StringBuilder sb = new();

    public string Text => text != null ? text.text : "";
    public bool Shown => bg != null && bg.enabled;

    public static IEnumerator CreateWhenReady()
    {
        float deadline = Time.realtimeSinceStartup + 60f;
        while (HudButton.Instance == null)
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Plugin.Log.LogInfo("[Contacts] no QoL button; counter not shown");
                yield break;
            }
            yield return null;
        }
        if (Instance != null) yield break;
        try { Create(); }
        catch (Exception e) { Contacts.Instance.Fail(e, nameof(ContactsBadge)); }
    }

    private static void Create()
    {
        var qol = (RectTransform)HudButton.Instance.transform;
        var rt = UiCards.NewChild("IgiQoL_Contacts", qol.parent);
        rt.anchorMin = qol.anchorMin;
        rt.anchorMax = qol.anchorMax;
        rt.pivot = qol.pivot;
        rt.sizeDelta = new Vector2(260f, 18f);
        rt.anchoredPosition = qol.anchoredPosition + new Vector2(0f, qol.rect.height + 4f);
        var bg = UiCards.AddBackground(rt.gameObject, ColorHelper.panelHeaderBlue);
        var text = FleetBarParts.AddLine(rt, "", 11f, ColorHelper.offWhite, TextAlignmentOptions.Center);
        UiCards.Stretch((RectTransform)text.transform);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(() => ModWindow.Open("contacts"));
        FleetBarParts.SetTooltip(button, null, "What is left at this location. Click for the Contacts tab.");
        Instance = rt.gameObject.AddComponent<ContactsBadge>();
        Instance.bg = bg;
        Instance.text = text;
        Instance.button = button;
        Instance.Refresh();
    }

    private void Update()
    {
        if (!Contacts.Instance.Active) return;
        // Follows the QoL button at once when the HUD hides (docking), otherwise refreshes once per second.
        bool hud = HudButton.Instance != null && HudButton.Instance.activeSelf;
        if (hud == hudShown && Time.unscaledTime < nextRefresh) return;
        hudShown = hud;
        nextRefresh = Time.unscaledTime + 1f;
        try { Refresh(); }
        catch (Exception e) { Contacts.Instance.Fail(e, nameof(ContactsBadge)); }
    }

    private void Refresh()
    {
        int b = 0;
        foreach (var unit in BoardableUnit.all)
            if (unit != null && unit.gameObject.activeInHierarchy) b++;
        int w = SalvageContainer.all.Count;
        int l = Contacts.PruneLoot();
        bool show = Contacts.Instance.HudCounter.Value && hudShown && GameplayManager.Instance?.spaceShip != null && b + w + l > 0;
        bg.enabled = text.enabled = button.interactable = show;
        if (!show) return;
        sb.Clear();
        if (b > 0) sb.Append(b).Append(" boardable");
        if (w > 0) { if (sb.Length > 0) sb.Append(" | "); sb.Append(w).Append(w == 1 ? " wreck" : " wrecks"); }
        if (l > 0) { if (sb.Length > 0) sb.Append(" | "); sb.Append(l).Append(" loot"); }
        string s = sb.ToString();
        if (text.text != s) text.text = s;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
