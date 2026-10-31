using System;
using System.Collections.Generic;
using Behavior.UI.GalaxyMap;
using Behaviour;
using Behaviour.Item;
using Behaviour.UI;
using Behaviour.UI.HUD;
using Behaviour.UI.Shared;
using Behaviour.UI.Side_Menu.SideTabs.Captain;
using Behaviour.UI.Tooltip;
using Source.UI.Tooltip;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.UI;

// Thin layer over the game's code-built UI helpers (UiCards, FleetBarParts, CaptainCards) so mod UI looks like the game's.
public static class Kit
{
    public sealed class Choice
    {
        public GameObject root;
        public Func<int> GetIndex;
        public Action<int> SetIndex;
    }

    public sealed class ToggleState : MonoBehaviour
    {
        public bool on;
    }

    public static RectTransform Window(string name, float width, float height, Vector2 position)
    {
        var rt = UiCards.NewChild(name, HudManager.Instance.rootCanvasTransform);
        rt.pivot = rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = position;
        var go = rt.gameObject;
        UiCards.AddBackground(go, ColorHelper.panelBlue);
        WindowBorder.Add(rt);
        var canvas = go.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 3;
        go.AddComponent<GraphicRaycaster>();
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 4f;
        UiCards.SetColumn(layout);
        return rt;
    }

    public static RectTransform Row(RectTransform parent, float spacing = 6f, float height = 26f)
    {
        var row = FleetBarParts.AddRow(parent, "Row", spacing);
        CaptainCards.SetFixedSize(row, 0f, height);
        return row;
    }

    public static RectTransform Column(RectTransform parent, float spacing = 4f) =>
        FleetBarParts.AddColumn(parent, "Column", spacing, 0f);

    public static RectTransform Scroll(RectTransform parent)
    {
        var scroll = UiCards.NewChild("Scroll", parent);
        var le = scroll.gameObject.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;
        le.flexibleWidth = 1f;
        return UiScrollView.Build(scroll, 4f, new RectOffset(8, 8, 8, 8));
    }

    // Empty vertical space in a column, e.g. above a heading so it doesn't run into the rows before it.
    public static void Gap(RectTransform parent, float height) =>
        UiCards.NewChild("Gap", parent).gameObject.AddComponent<LayoutElement>().preferredHeight = height;

    public static TMP_Text Heading(RectTransform parent, string text) =>
        FleetBarParts.AddLine(parent, text, 14f, ColorHelper.stationCardTitle, TextAlignmentOptions.MidlineLeft);

    public static TMP_Text Label(RectTransform parent, string text, float size = 12f) =>
        FleetBarParts.AddFlexibleLine(parent, text, size, ColorHelper.offWhite);

    public static TMP_Text Muted(RectTransform parent, string text)
    {
        var t = FleetBarParts.AddFlexibleLine(parent, text, 11f, ColorHelper.poiTooltipMuted);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    // A wrapping, rich-text line in body colour (Label is one line, Muted is grey).
    public static TMP_Text Text(RectTransform parent, string text)
    {
        var t = Muted(parent, text);
        t.color = ColorHelper.offWhite;
        t.fontSize = 12f;
        t.richText = true;
        return t;
    }

    // Tooltips need a raycast target; game labels are created with raycastTarget = false.
    public static void Tooltip(Graphic target, string title, string body)
    {
        target.raycastTarget = true;
        FleetBarParts.SetTooltip(target, title, body);
    }

    public static Button TextButton(RectTransform parent, string text, UnityAction onClick, float width = 90f) =>
        CaptainCards.AddButton(parent, text, onClick, width).GetComponent<Button>();

    public static Button Tile(RectTransform parent, string iconKey, Color tint, float size, UnityAction onClick, string tooltip = null)
    {
        var tile = FleetBarParts.AddTile(parent, Icon("Info", iconKey), tint, FleetTileState.Normal, size, Mathf.Round(size * 0.65f), onClick);
        if (tooltip != null) FleetBarParts.SetTooltip(tile, null, tooltip);
        return tile;
    }

    public static Button TextTile(RectTransform parent, string text, float size, UnityAction onClick) =>
        FleetBarParts.AddTextTile(parent, text, size, onClick);

    public static Button Toggle(RectTransform parent, string label, bool isOn, Action<bool> onChanged)
    {
        var row = Row(parent, 6f, 22f);
        Button tile = null;
        tile = Tile(row, "Ok", ColorHelper.offWhite, 20f, () =>
        {
            var state = tile.GetComponent<ToggleState>();
            SetToggle(tile, !state.on);
            onChanged(state.on);
        });
        tile.gameObject.AddComponent<ToggleState>();
        SetToggle(tile, isOn);
        // Without a label the toggle is a 20 px control inside someone else's row; with one it takes the free width.
        if (string.IsNullOrEmpty(label)) CaptainCards.SetFixedSize(row, 20f, 22f);
        else
        {
            Label(row, label);
            CaptainCards.SetFlexibleWidth(row);
        }
        return tile;
    }

    public static void SetToggle(Button tile, bool on)
    {
        tile.GetComponent<ToggleState>().on = on;
        FleetBarParts.SetTileState(tile, on ? FleetTileState.ToggledOn : FleetTileState.Normal);
        var icon = tile.transform.Find("Icon");
        if (icon != null) icon.GetComponent<Image>().enabled = on;
    }

    public static Choice Dropdown(RectTransform parent, IList<string> options, int index, Action<int> onChanged, float width = 160f)
    {
        var prefab = Templates.DropdownPrefab;
        if (prefab != null)
        {
            var dd = Object.Instantiate(prefab, parent);
            dd.name = "Dropdown";
            dd.onValueChanged = new TMP_Dropdown.DropdownEvent();
            // The template's texts wrap, so long options broke onto a second line outside the box.
            FitOneLine(dd.captionText);
            FitOneLine(dd.itemText);
            dd.ClearOptions();
            dd.AddOptions(new List<string>(options));
            dd.SetValueWithoutNotify(index);
            dd.onValueChanged.AddListener(i => onChanged(i));
            CaptainCards.SetFixedSize((RectTransform)dd.transform, width, 24f);
            return new Choice { root = dd.gameObject, GetIndex = () => dd.value, SetIndex = dd.SetValueWithoutNotify };
        }

        int current = index;
        TMP_Text text = null;
        Button button = TextButton(parent, options[index], () =>
        {
            int count = options.Count;
            current = GlobalControls.modifierShift ? (current + count - 1) % count : (current + 1) % count;
            text.text = options[current];
            onChanged(current);
        }, width);
        text = button.GetComponentInChildren<TMP_Text>();
        return new Choice
        {
            root = button.gameObject,
            GetIndex = () => current,
            SetIndex = i => { current = i; text.text = options[i]; },
        };
    }

    // One line, shrinking the font (down to 8) instead of wrapping or spilling out of the box.
    private static void FitOneLine(TMP_Text text)
    {
        if (text == null) return;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.fontSizeMax = text.fontSize;
        text.fontSizeMin = 8f;
        text.enableAutoSizing = true;
    }

    public static TMP_InputField Input(RectTransform parent, string text, TMP_InputField.ContentType type, Action<string> onEndEdit, float width = 160f)
    {
        var field = UiSearchField.Build(parent, "", ColorHelper.panelHeaderBlue, 12f);
        field.contentType = type;
        field.SetTextWithoutNotify(text);
        field.onEndEdit.AddListener(v => onEndEdit(v));
        CaptainCards.SetFixedSize((RectTransform)field.transform, width, 24f);
        return field;
    }

    public static TMP_InputField Search(RectTransform parent, string placeholder, Action<string> onChanged, float width = 300f)
    {
        var field = UiSearchField.Build(parent, placeholder, ColorHelper.panelHeaderBlue, 12f);
        UiSearchField.AddLens(field, 12f, 22f);
        field.onValueChanged.AddListener(v => onChanged(v));
        CaptainCards.SetFixedSize((RectTransform)field.transform, width, 24f);
        return field;
    }

    // A one-line label of fixed width, for table columns.
    public static TMP_Text Fixed(RectTransform parent, string text, float width, TextAlignmentOptions align, Color color)
    {
        var t = FleetBarParts.AddLine(parent, text, 12f, color, align);
        CaptainCards.SetFixedSize((RectTransform)t.transform, width, 22f);
        return t;
    }

    // The game's item hover card on any graphic; no-op when the HUD has no item tooltip prefab.
    public static void ItemTooltip(Graphic target, InventoryItemType item)
    {
        if (UITooltipParent.ItemTooltipPrefab == null) return;
        target.raycastTarget = true;
        var src = target.GetComponent<ItemTooltipSource>();
        if (src == null) src = target.gameObject.AddComponent<ItemTooltipSource>();
        src.SetItem(item, 1, false, ItemTooltipContext.InInventory);
    }

    public static string Icon(string fallback, params string[] candidates)
    {
        var lib = PoiIconLibrary.Instance;
        foreach (var key in candidates)
            if (key != null && lib.Has(key)) return key;
        return fallback;
    }

    public static Sprite CloseSprite => PoiIconLibrary.Instance.Get(PoiIconKeys.Fleet("Close"));
}
