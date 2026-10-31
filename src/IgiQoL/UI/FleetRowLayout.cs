using Behaviour.UI.HUD.Fleet;
using UnityEngine;

namespace IgiQoL.UI;

// Geometry shared by the mod's additions to a fleet panel row: FleetVitals' bars and FleetStrip's tiles.
// All row children use top anchors, so y values are negative offsets from the row's top edge.
public static class FleetRowLayout
{
    public const string StripName = "IgiQoL_Strip";
    public const string ShieldName = "IgiQoL_Shield";
    public const string ArmorName = "IgiQoL_Armor";

    public static float Top(RectTransform rt) => rt.anchoredPosition.y + rt.rect.height * (1f - rt.pivot.y);

    public static float Bottom(RectTransform rt) => Top(rt) - rt.rect.height;

    // The vanilla hull bar: the fill's parent when the fill sits inside its own frame, else the fill itself.
    public static RectTransform HullBar(FleetPanelRow row)
    {
        var fill = row.hullBarFill.transform;
        return (RectTransform)(fill.parent != row.transform ? fill.parent : fill);
    }

    // Lowest bar of the row: the armor bar when FleetVitals added one (even while hidden), else the hull bar.
    public static float BarsBottom(FleetPanelRow row)
    {
        var armor = row.transform.Find(ArmorName) as RectTransform;
        return Bottom(armor != null ? armor : HullBar(row));
    }
}
