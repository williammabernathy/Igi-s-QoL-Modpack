using System;
using System.Collections.Generic;
using Behavior.UI.GalaxyMap;
using Behaviour.UI.Shared;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.UI;
using LightJson;
using Source.Combat;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.CombatLog;

internal enum MeterNumber { Total, PerSecond, Share }

internal enum MeterColour { Role, Hull, Type, Picked }

// What one meter shows, read from its pop-out's options. Missing or unreadable values fall back to the defaults.
internal sealed class MeterSettings
{
    public View view = View.DealtByShip;
    public bool session;
    public int rows = 8;
    public MeterNumber number = MeterNumber.Total;
    public bool split;
    public MeterColour colour = MeterColour.Role;
    // Palette index + 1 by ship guid; 0 or missing means the role colour.
    public JsonObject picks;

    internal static MeterSettings Read(PopoutState state)
    {
        var o = state.options;
        var s = new MeterSettings();
        if (o["view"].IsString && Enum.TryParse(o["view"].AsString, out View view)) s.view = view;
        s.session = o["scope"].IsString && o["scope"].AsString == "Session";
        if (o["rows"].IsNumber) s.rows = Mathf.Clamp((int)o["rows"].AsNumber, 1, DpsMeter.RowChoices[DpsMeter.RowChoices.Length - 1]);
        if (o["number"].IsString && Enum.TryParse(o["number"].AsString, out MeterNumber number)) s.number = number;
        s.split = o["bar"].IsString && o["bar"].AsString == "Split";
        if (o["colour"].IsString && Enum.TryParse(o["colour"].AsString, out MeterColour colour)) s.colour = colour;
        s.picks = o["picks"].IsJsonObject ? o["picks"].AsJsonObject : null;
        return s;
    }

    internal int Pick(string guid)
    {
        if (picks == null || guid == null) return 0;
        var v = picks[guid];
        return v.IsNumber ? (int)v.AsNumber : 0;
    }
}

// Several bar meters of the DPS parser, each its own pop-out with its own view, scope and look.
internal static class DpsMeter
{
    internal const string KindId = "dps-meter";

    internal static readonly int[] RowChoices = { 5, 8, 10, 15, 25 };
    private static readonly string[] RowNames = { "5", "8", "10", "15", "25" };
    private static readonly string[] ScopeNames = { "Current fight", "Whole session" };
    private static readonly string[] NumberNames = { "Total", "Per second", "Share" };
    private static readonly string[] BarNames = { "Plain", "Split in 3" };
    private static readonly string[] ColourNames = { "By role", "By hull class", "By damage type", "Picked per ship" };
    internal static readonly string[] PresetNames = { "My fleet, dealt by ship", "My fleet, taken by ship", "Dealt by damage type" };

    private static readonly Color[] RoleColours =
    {
        new(0.85f, 0.38f, 0.34f), // other
        new(0.96f, 0.78f, 0.25f), // flagship
        new(0.30f, 0.62f, 0.95f), // fleet ship
        new(0.40f, 0.82f, 0.50f), // drone or fighter
        new(0.72f, 0.52f, 0.90f), // mercenary
    };

    // Hull size 1 and up; index 0 is a row that isn't a ship.
    private static readonly Color[] SizeColours =
    {
        new(0.55f, 0.58f, 0.62f),
        new(0.55f, 0.85f, 0.95f), new(0.40f, 0.80f, 0.80f), new(0.45f, 0.80f, 0.55f), new(0.70f, 0.85f, 0.40f),
        new(0.95f, 0.80f, 0.35f), new(0.95f, 0.60f, 0.30f), new(0.90f, 0.42f, 0.35f), new(0.80f, 0.35f, 0.65f),
    };

    internal static readonly Color[] Palette =
    {
        new(0.95f, 0.78f, 0.25f), new(0.30f, 0.62f, 0.95f), new(0.40f, 0.82f, 0.50f), new(0.72f, 0.52f, 0.90f),
        new(0.90f, 0.40f, 0.40f), new(0.30f, 0.80f, 0.80f), new(0.95f, 0.60f, 0.30f), new(0.90f, 0.90f, 0.90f),
    };

    internal static Color RoleColour(UnitRole role) => RoleColours[(int)role];

    internal static Color SizeColour(int size) => SizeColours[Mathf.Clamp(size, 0, SizeColours.Length - 1)];

    internal static void Register(Feature owner)
    {
        Popouts.Register(new PopoutKind
        {
            Id = KindId,
            Title = "DPS meter",
            Owner = owner,
            DefaultSize = new Vector2(340f, 240f),
            MinSize = new Vector2(240f, 100f),
            DefaultShow = PopoutShow.Always,
            RelevantLabel = "in combat",
            Relevant = CombatOverview.CombatOverview.InCombat,
            Multiple = true,
            DockTab = "combat",
            TitleOf = TitleOf,
            Build = BuildPlaced,
            BuildOptions = BuildOptions,
        });
    }

    // Before it has ever been placed a pop-out opens in the middle of the screen, which is where the combat overview
    // opens too. A meter starts beside the overview instead (each further meter a little lower), so neither hides the other.
    private static void BuildPlaced(Popout p, RectTransform body)
    {
        if (!p.State.placed)
        {
            int index = Mathf.Max(0, new List<string>(Popouts.Windows(KindId)).IndexOf(p.Id));
            p.State.x = CombatOverview.CombatOverview.DefaultWidth / 2f + 8f;
            p.State.y = CombatOverview.CombatOverview.DefaultHeight / 2f - 28f * index;
            p.State.placed = true;
        }
        MeterList.Build(p, body);
    }

    // Short, because it sits on the header and on the strip chip.
    internal static string ShortName(View v) => v switch
    {
        View.DealtByWeapon => "Dealt by weapon",
        View.DealtByType => "Dealt by type",
        View.DealtByShip => "Dealt by ship",
        View.DealtToTarget => "Dealt to enemy",
        View.TakenByWeapon => "Taken by weapon",
        View.TakenByType => "Taken by type",
        View.TakenFromShip => "Taken from enemy",
        _ => "Taken by ship",
    };

    private static string TitleOf(PopoutState s)
    {
        var settings = MeterSettings.Read(s);
        return "DPS: " + ShortName(settings.view) + (settings.session ? " (session)" : "");
    }

    // Starts a meter from one of PresetNames.
    internal static string AddPreset(int index) => Popouts.OpenNew(KindId, s =>
    {
        var o = s.options;
        o["scope"] = "Current";
        o["number"] = MeterNumber.Total.ToString();
        o["colour"] = MeterColour.Role.ToString();
        o["bar"] = "Plain";
        o["rows"] = 8;
        switch (index)
        {
            case 0:
                o["view"] = View.DealtByShip.ToString();
                break;
            case 1:
                o["view"] = View.TakenByShip.ToString();
                o["bar"] = "Split";
                break;
            default:
                o["view"] = View.DealtByType.ToString();
                o["colour"] = MeterColour.Type.ToString();
                break;
        }
    });

    // The tab's "Pop out meters": every saved meter again, or a first one when there are none.
    internal static void PopOutAll()
    {
        var ids = Popouts.Windows(KindId);
        if (ids.Count == 0)
        {
            AddPreset(0);
            return;
        }
        foreach (var id in ids) Popouts.Open(id);
    }

    private static void Change(Popout p, string key, JsonValue value, bool rebuild)
    {
        p.State.options[key] = value;
        p.State.Save(p.Id);
        // The title shows the view and scope, so those rebuild the window; the rest only redraws the bars.
        if (rebuild) Popouts.Rebuild(p.Id);
        else { var list = p.Body.GetComponentInChildren<MeterList>(); if (list != null) list.Apply(); }
    }

    private static void BuildOptions(Popout p, RectTransform column)
    {
        var s = MeterSettings.Read(p.State);
        var views = new List<string>();
        foreach (View v in Enum.GetValues(typeof(View))) views.Add(CombatLog.ViewName(v));
        var row = Kit.Row(column, 8f, 26f);
        Kit.Label(row, "View");
        Kit.Dropdown(row, views, (int)s.view, i => Change(p, "view", ((View)i).ToString(), true), 220f);

        row = Kit.Row(column, 8f, 26f);
        Kit.Label(row, "Scope");
        Kit.Dropdown(row, ScopeNames, s.session ? 1 : 0, i => Change(p, "scope", i == 1 ? "Session" : "Current", true), 110f);
        Kit.Label(row, "Rows");
        Kit.Dropdown(row, RowNames, Mathf.Max(0, Array.IndexOf(RowChoices, s.rows)), i => Change(p, "rows", RowChoices[i], false), 60f);

        row = Kit.Row(column, 8f, 26f);
        Kit.Label(row, "Number");
        Kit.Dropdown(row, NumberNames, (int)s.number, i => Change(p, "number", ((MeterNumber)i).ToString(), false), 110f);
        Kit.Label(row, "Bar");
        Kit.Dropdown(row, BarNames, s.split ? 1 : 0, i => Change(p, "bar", i == 1 ? "Split" : "Plain", false), 90f);

        row = Kit.Row(column, 8f, 26f);
        Kit.Label(row, "Colour");
        var picks = Kit.Column(column, 2f);
        Kit.Dropdown(row, ColourNames, (int)s.colour, i =>
        {
            picks.gameObject.SetActive((MeterColour)i == MeterColour.Picked);
            Change(p, "colour", ((MeterColour)i).ToString(), false);
        }, 200f);
        BuildPicks(p, picks);
        picks.gameObject.SetActive(s.colour == MeterColour.Picked);

        row = Kit.Row(column, 8f, 26f);
        Kit.TextButton(row, "Remove this meter", () => Popouts.Remove(p.Id), 160f);
    }

    // One colour button per ship of yours; other units keep their role colour.
    private static void BuildPicks(Popout p, RectTransform column)
    {
        Kit.Muted(column, "Your ships' colours. Anything else uses its role colour.");
        foreach (var ship in Fleet.Ships(true))
        {
            string guid = ship.guid;
            var row = Kit.Row(column, 6f, 22f);
            Kit.Label(row, ship.GetShipName(), 11f);
            var swatch = UiCards.NewChild("Swatch", row);
            var image = UiCards.AddBackground(swatch.gameObject, ColorHelper.mapDockButton);
            image.raycastTarget = false;
            var le = swatch.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 24f;
            TMP_Text caption = null;
            var button = Kit.TextButton(row, "", () =>
            {
                var s = MeterSettings.Read(p.State);
                int next = (s.Pick(guid) + 1) % (Palette.Length + 1);
                if (!p.State.options["picks"].IsJsonObject) p.State.options["picks"] = new JsonObject();
                p.State.options["picks"].AsJsonObject[guid] = next;
                p.State.Save(p.Id);
                Paint(image, caption, next);
                var list = p.Body.GetComponentInChildren<MeterList>();
                if (list != null) list.Apply();
            }, 64f);
            caption = button.GetComponentInChildren<TMP_Text>();
            Paint(image, caption, MeterSettings.Read(p.State).Pick(guid));
        }
    }

    private static void Paint(Image swatch, TMP_Text caption, int pick)
    {
        swatch.color = pick > 0 ? Palette[pick - 1] : ColorHelper.mapDockButton;
        caption.text = pick > 0 ? "Next" : "Auto";
    }
}

// The bars of one meter. Rows are built once and pooled; a refresh changes only texts, colours and bar widths, once a
// second and only when something was recorded (or the setting changed).
internal sealed class MeterList : MonoBehaviour
{
    private sealed class Cells
    {
        public RectTransform root, fill, armour, shield;
        public Image fillImage, armourImage, shieldImage, swatch;
        public TMP_Text label, number;
        public float fillFraction = -1f, armourFraction = -1f, shieldFraction = -1f, numberValue = float.NaN;
        public bool split;
    }

    private static readonly Comparison<Stat> ByTotal = (a, b) => b.total.CompareTo(a.total);

    private static readonly Color HullColour = new(0.20f, 0.72f, 0.28f);

    private Popout popout;
    private RectTransform column;
    private MeterSettings settings;
    private TMP_Text empty;
    private int version = -1;
    private Fight fight;
    private bool dirty = true;
    private float next;
    private readonly List<Cells> pool = new();
    private readonly List<Stat> scratch = new();

    internal int RowCount { get; private set; }

    internal string TopNumber => RowCount > 0 ? pool[0].number.text : "";

    // The drawn width of row i's bar (the fill's right anchor), and the total behind it; -1 and 0 past the last row.
    internal float FillAt(int i) => i >= 0 && i < RowCount ? pool[i].fill.anchorMax.x : -1f;

    internal double TotalAt(int i) => i >= 0 && i < RowCount ? scratch[i].total : 0;

    internal static void Build(Popout p, RectTransform body)
    {
        var list = Kit.Scroll(body);
        var layout = list.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.spacing = 2f;
        }
        var self = list.gameObject.AddComponent<MeterList>();
        self.popout = p;
        self.column = list;
        self.settings = MeterSettings.Read(p.State);
    }

    // A setting changed: read them again and redraw at once.
    internal void Apply()
    {
        settings = MeterSettings.Read(popout.State);
        foreach (var c in pool) c.numberValue = float.NaN;
        dirty = true;
        next = 0f;
    }

    internal void Tick()
    {
        next = 0f;
        dirty = true;
        Update();
    }

    private void Update()
    {
        if (!CombatLog.Instance.Active || Time.unscaledTime < next) return;
        var current = settings.session ? CombatLog.Session : CombatLog.Fights.Count > 0 ? CombatLog.Fights[0] : null;
        if (!dirty && version == CombatLog.Version && current == fight) return;
        next = Time.unscaledTime + 1f;
        version = CombatLog.Version;
        fight = current;
        dirty = false;
        try { Refresh(); }
        catch (Exception e) { CombatLog.Instance.Fail(e, nameof(MeterList)); }
    }

    private void Refresh()
    {
        scratch.Clear();
        if (fight != null) scratch.AddRange(fight.views[(int)settings.view].Values);
        scratch.Sort(ByTotal);
        int shown = Mathf.Min(scratch.Count, settings.rows);
        RowCount = shown;

        if (shown == 0 && empty == null) empty = Kit.Muted(column, "Nothing recorded yet. Hits by and on your ships, drones and fighters show up here.");
        if (empty != null && empty.gameObject.activeSelf != (shown == 0)) empty.gameObject.SetActive(shown == 0);
        for (int i = shown; i < pool.Count; i++)
            if (pool[i].root.gameObject.activeSelf) pool[i].root.gameObject.SetActive(false);
        if (shown == 0) return;

        bool dealtView = settings.view <= View.DealtToTarget;
        double whole = dealtView ? fight.dealt : fight.taken;
        float secs = fight.Duration;
        double top = scratch[0].total;
        for (int i = 0; i < shown; i++)
        {
            var s = scratch[i];
            if (i == pool.Count) pool.Add(NewRow(column));
            var c = pool[i];
            if (!c.root.gameObject.activeSelf) c.root.gameObject.SetActive(true);
            Set(c.label, s.label);

            float value = settings.number switch
            {
                MeterNumber.PerSecond => (float)(s.total / secs),
                MeterNumber.Share => whole > 0 ? Mathf.Round((float)(100.0 * s.total / whole)) : 0f,
                _ => (float)s.total,
            };
            if (c.numberValue != value)
            {
                c.numberValue = value;
                c.number.text = settings.number switch
                {
                    MeterNumber.PerSecond => GameMath.FormatNumber(value) + "/s",
                    MeterNumber.Share => value.ToString("0") + "%",
                    _ => GameMath.FormatNumber(value),
                };
            }

            var colour = ColourOf(s);
            float fraction = top > 0 ? (float)(s.total / top) : 0f;
            if (c.split != settings.split) SetSplit(c, settings.split);
            if (settings.split)
            {
                SetColour(c.fillImage, Dim(HullColour));
                SetColour(c.armourImage, Dim(ColorHelper.armorColor));
                SetColour(c.shieldImage, Dim(ColorHelper.shieldColor));
                SetColour(c.swatch, colour);
                float all = (float)Math.Max(s.total, 1e-9);
                Fill(c.armour, ref c.armourFraction, fraction * (float)(s.shield + s.armor) / all);
                Fill(c.shield, ref c.shieldFraction, fraction * (float)s.shield / all);
            }
            else SetColour(c.fillImage, Dim(colour));
            Fill(c.fill, ref c.fillFraction, fraction);
        }
    }

    // Bars are drawn darker than their colour, so the white name and number on them stay readable on yellow and green.
    private static Color Dim(Color c) => new(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, c.a);

    private Color ColourOf(Stat s)
    {
        switch (settings.colour)
        {
            case MeterColour.Hull: return DpsMeter.SizeColour(s.size);
            case MeterColour.Type: return s.MainType().GetColor();
            case MeterColour.Picked:
                int pick = settings.Pick(s.guid);
                if (pick > 0) return DpsMeter.Palette[Mathf.Min(pick, DpsMeter.Palette.Length) - 1];
                break;
        }
        return DpsMeter.RoleColour(s.role);
    }

    // The bar, the text over it, and (for the split bar) two overlays and a colour swatch. The overlays and swatch
    // are built with every row so a setting change never has to rebuild the window.
    private static Cells NewRow(RectTransform column)
    {
        var c = new Cells();
        c.root = UiCards.NewChild("Meter", column);
        var le = c.root.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = 18f;

        c.fill = FleetBarParts.AddFillBar(c.root, HullColour, 18f);
        var bar = (RectTransform)c.fill.parent;
        UiCards.Stretch(bar);
        c.fillImage = c.fill.GetComponent<Image>();
        c.armour = Overlay(bar, "Armour", out c.armourImage);
        c.shield = Overlay(bar, "Shield", out c.shieldImage);
        c.armour.gameObject.SetActive(false);
        c.shield.gameObject.SetActive(false);

        var swatch = UiCards.NewChild("Swatch", c.root);
        c.swatch = UiCards.AddBackground(swatch.gameObject, Color.white);
        c.swatch.raycastTarget = false;
        swatch.anchorMin = Vector2.zero;
        swatch.anchorMax = new Vector2(0f, 1f);
        swatch.offsetMin = Vector2.zero;
        swatch.offsetMax = new Vector2(4f, 0f);
        swatch.gameObject.SetActive(false);

        c.label = FleetBarParts.AddLine(c.root, "", 11f, ColorHelper.offWhite, TextAlignmentOptions.MidlineLeft);
        c.label.overflowMode = TextOverflowModes.Ellipsis;
        Place(c.label.rectTransform, 6f, 70f);
        c.number = FleetBarParts.AddLine(c.root, "", 11f, ColorHelper.offWhite, TextAlignmentOptions.MidlineRight);
        Place(c.number.rectTransform, 0f, 4f);
        return c;
    }

    private static RectTransform Overlay(RectTransform bar, string name, out Image image)
    {
        var rt = UiCards.NewChild(name, bar);
        image = UiCards.AddBackground(rt.gameObject, Color.white);
        image.raycastTarget = false;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(0f, 1f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static void Place(RectTransform rt, float left, float right)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, 0f);
        rt.offsetMax = new Vector2(-right, 0f);
    }

    private static void SetSplit(Cells c, bool split)
    {
        c.split = split;
        c.armour.gameObject.SetActive(split);
        c.shield.gameObject.SetActive(split);
        c.swatch.gameObject.SetActive(split);
        c.armourFraction = c.shieldFraction = -1f;
    }

    private static void Fill(RectTransform fill, ref float last, float fraction)
    {
        if (last == fraction) return;
        last = fraction;
        FleetBarParts.SetFill(fill, fraction);
    }

    private static void SetColour(Image image, Color colour)
    {
        if (image.color != colour) image.color = colour;
    }

    private static void Set(TMP_Text t, string text)
    {
        if (t.text != text) t.text = text;
    }
}
