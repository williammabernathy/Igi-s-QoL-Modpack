using Behavior.UI.GalaxyMap;
using TMPro;
using UnityEngine;

namespace IgiQoL.UI;

// Game prefabs reused as templates for controls the code-built UI helpers do not offer.
public static class Templates
{
    private static TMP_Dropdown dropdown;
    private static bool dropdownLooked;

    public static TMP_Dropdown DropdownPrefab
    {
        get
        {
            if (dropdownLooked) return dropdown;
            dropdownLooked = true;
            var window = Resources.Load<TradeRouteWindow>("GalaxyMap/TradeRouteWindow");
            if (window != null && window.ruleRowPrefab != null && window.ruleRowPrefab.kindDropdown != null)
                dropdown = window.ruleRowPrefab.kindDropdown;
            else
                Plugin.Log.LogWarning("[Window] dropdown template missing; using cycle buttons");
            return dropdown;
        }
    }
}
