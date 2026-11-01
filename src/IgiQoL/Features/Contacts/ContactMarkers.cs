using System;
using System.Collections.Generic;
using Behavior.UI.GalaxyMap;
using Behaviour.Equipment.Module;
using Behaviour.Salvage;
using Behaviour.Tractoring;
using Behaviour.UI.HUD;
using Behaviour.UI.Shared;
using Behaviour.UI.Spacestation;
using Behaviour.Unit;
using Behaviour.Weapons;
using IgiQoL.Game;
using IgiQoL.UI;
using Source.Util;
using Behaviour.Mining;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.Contacts;

// Edge-of-screen markers for the nearest boardable ships, wrecks and loot that are off screen, like the game's
// enemy indicators. The set is picked twice a second; positions follow the camera every frame. Click one to
// board, salvage or tractor it.
public sealed class ContactMarkers : MonoBehaviour
{
    public static ContactMarkers Instance;

    public const int MaxPerKind = 8;
    private const float Size = 22f;

    public enum Kind { Boardable, Wreck, Loot, Ore }

    private sealed class Marker
    {
        public GameObject go;
        public RectTransform rt;
        public Image icon;
        public TMP_Text dist;
        public Kind kind;
        public Component target;
        public bool shown;
        public int lastDist = -1;
    }

    private readonly List<Marker> markers = new();
    private static readonly Kind[] Kinds = (Kind[])Enum.GetValues(typeof(Kind));
    private readonly List<(float d, Component c)> scratch = new();
    private Comparison<(float d, Component c)> byDistance;
    private float nextCollect;

    public int ShownCount
    {
        get
        {
            int n = 0;
            foreach (var m in markers) if (m.shown) n++;
            return n;
        }
    }

    // Whether a shown marker points at this contact, and where on the screen edge (viewport 0..1).
    public bool Marks(Component target, out Vector2 anchor)
    {
        foreach (var m in markers)
        {
            if (!m.shown || m.target != target) continue;
            anchor = m.rt.anchorMin;
            return true;
        }
        anchor = Vector2.zero;
        return false;
    }

    internal static void Create()
    {
        if (Instance != null) return;
        var root = UiCards.NewChild("IgiQoL_ContactMarkers", HudManager.Instance.rootCanvasTransform);
        UiCards.Stretch(root);
        // First child: drawn under every HUD panel and window.
        root.SetAsFirstSibling();
        Instance = root.gameObject.AddComponent<ContactMarkers>();
        Instance.byDistance = (a, b) => a.d.CompareTo(b.d);
        foreach (var kind in Kinds)
            for (int i = 0; i < MaxPerKind; i++) Instance.markers.Add(Instance.NewMarker(root, kind));
    }

    internal static string IconKey(Kind kind) => kind switch
    {
        Kind.Boardable => Kit.Icon("Info", "Fleet.Raid", "Crew"),
        Kind.Wreck => Kit.Icon("Info", "Salvage"),
        Kind.Ore => Kit.Icon("Info", "Mining"),
        _ => Kit.Icon("Info", "Fleet.Tractor", "Cargo"),
    };

    internal static Color Tint(Kind kind) => kind switch
    {
        Kind.Boardable => ColorHelper.orange75,
        Kind.Wreck => ColorHelper.lightCyan,
        _ => ColorHelper.offWhite,
    };

    private Marker NewMarker(RectTransform root, Kind kind)
    {
        var rt = UiCards.NewChild("Marker" + kind, root);
        rt.sizeDelta = new Vector2(Size, Size + 12f);
        var icon = FleetBarParts.AddIcon(rt, IconKey(kind), Tint(kind), Size);
        var irt = (RectTransform)icon.transform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
        irt.pivot = new Vector2(0.5f, 1f);
        irt.anchoredPosition = Vector2.zero;
        icon.raycastTarget = true;
        var dist = FleetBarParts.AddLine(rt, "", 10f, ColorHelper.offWhite, TextAlignmentOptions.Center);
        var drt = (RectTransform)dist.transform;
        drt.anchorMin = new Vector2(0f, 0f);
        drt.anchorMax = new Vector2(1f, 0f);
        drt.pivot = new Vector2(0.5f, 0f);
        drt.sizeDelta = new Vector2(40f, 12f);
        drt.anchoredPosition = Vector2.zero;
        var m = new Marker { go = rt.gameObject, rt = rt, icon = icon, dist = dist, kind = kind };
        var button = icon.gameObject.AddComponent<Button>();
        button.targetGraphic = icon;
        button.onClick.AddListener(() => OnClick(m));
        FleetBarParts.SetTooltip(button, null, kind switch
        {
            Kind.Boardable => "Boardable ship off screen. Click to open boarding.",
            Kind.Wreck => "Wreck off screen. Click to salvage it.",
            Kind.Ore => "Asteroid with one of your preferred ores off screen. Click to mine it.",
            _ => "Loot off screen. Click to tractor it.",
        });
        rt.gameObject.SetActive(false);
        return m;
    }

    private static bool Enabled(Kind kind)
    {
        var c = Contacts.Instance;
        return kind switch
        {
            Kind.Boardable => c.MarkBoardable.Value,
            Kind.Wreck => c.MarkWrecks.Value,
            Kind.Ore => c.MarkOres.Value && OrePreference.OrePreference.Instance.Active && OrePreference.OrePreference.Ores.Count > 0,
            _ => c.MarkLoot.Value,
        };
    }

    private void Update()
    {
        if (!Contacts.Instance.Active)
        {
            // Failed: park the markers and stop updating them.
            HideAll();
            enabled = false;
            return;
        }
        try
        {
            var ship = Fleet.Flagship;
            bool space = ship != null && HudManager.Instance.showHud && SpaceStationInterior.instance == null && GameplayManager.camera != null;
            if (!space)
            {
                HideAll();
                return;
            }
            if (Time.unscaledTime >= nextCollect)
            {
                nextCollect = Time.unscaledTime + 0.5f;
                Collect(ship.transform.position);
            }
            Place();
        }
        catch (Exception e) { Contacts.Instance.Fail(e, nameof(ContactMarkers)); }
    }

    private void HideAll()
    {
        foreach (var m in markers) Show(m, false);
    }

    private static void Show(Marker m, bool on)
    {
        if (m.shown == on) return;
        m.shown = on;
        m.go.SetActive(on);
    }

    // Picks the nearest contacts of each kind; markers without one are parked.
    private void Collect(Vector2 from)
    {
        int slot = 0;
        foreach (var kind in Kinds)
        {
            scratch.Clear();
            if (Enabled(kind)) Gather(kind, from);
            scratch.Sort(byDistance);
            for (int i = 0; i < MaxPerKind; i++, slot++)
            {
                var m = markers[slot];
                if (i < scratch.Count)
                {
                    m.target = scratch[i].c;
                    int d = Mathf.RoundToInt(scratch[i].d);
                    if (m.lastDist != d)
                    {
                        m.lastDist = d;
                        m.dist.text = d.ToString();
                    }
                    if (kind == Kind.Loot && m.target is TractorableItem loot && loot.data?.itemType != null)
                        m.icon.color = loot.data.itemType.rarity.GetColor();
                    else if (kind == Kind.Ore && m.target is Asteroid rock && rock.asteroidData != null)
                        m.icon.color = PreferredOre(rock.asteroidData).item.rarity.GetColor();
                }
                else
                {
                    m.target = null;
                    m.lastDist = -1;
                }
            }
        }
    }

    private void Gather(Kind kind, Vector2 from)
    {
        switch (kind)
        {
            case Kind.Boardable:
                foreach (var b in BoardableUnit.all)
                    if (b != null && b.gameObject.activeInHierarchy && !(b.data != null && b.data.isFullyDepleted))
                        scratch.Add((Vector2.Distance(from, b.transform.position), b));
                break;
            case Kind.Wreck:
                foreach (var c in SalvageContainer.all)
                {
                    if (c == null || !c.gameObject.activeInHierarchy) continue;
                    var data = c.data;
                    if (data != null && !data.HasItems() && !data.HasSalvage(TargetLayer.Surface) && !data.HasSalvage(TargetLayer.Core)) continue;
                    scratch.Add((Vector2.Distance(from, c.transform.position), c));
                }
                break;
            case Kind.Ore:
                foreach (var t in OrePreference.OrePreference.SceneAsteroids())
                    if (t is Asteroid a && a != null && a.gameObject.activeInHierarchy && OrePreference.OrePreference.HasPreferred(a.asteroidData, true, true))
                        scratch.Add((Vector2.Distance(from, a.transform.position), a));
                break;
            default:
                Contacts.PruneLoot();
                foreach (var item in Contacts.Loot)
                    if (item.data?.itemType != null)
                        scratch.Add((Vector2.Distance(from, item.transform.position), item));
                break;
        }
    }

    // The listed ore that made the asteroid count: the surface one when that's listed, else the core.
    private static Behaviour.Mining.OreItemData PreferredOre(Source.Data.Persistable.AsteroidData a) =>
        a.surfaceAmount > 0 && OrePreference.OrePreference.IsPreferred(a.surfaceItem) ? a.surfaceItem : a.innerCoreItem;

    private void Place()
    {
        var cam = GameplayManager.camera;
        foreach (var m in markers)
        {
            if (m.target == null || !m.target.gameObject.activeInHierarchy)
            {
                Show(m, false);
                continue;
            }
            Vector2 vp = cam.WorldToViewportPoint(m.target.transform.position);
            if (vp.x > -0.02f && vp.x <= 1.02f && vp.y > -0.02f && vp.y < 1.02f)
            {
                Show(m, false);
                continue;
            }
            Show(m, true);
            // The pivot sits on the same edge point, so the marker stays fully on screen.
            var edge = EdgeIndicator.IntersectViewportEdge(vp);
            m.rt.anchorMin = m.rt.anchorMax = m.rt.pivot = edge;
            m.rt.anchoredPosition = Vector2.zero;
        }
    }

    private static void OnClick(Marker m)
    {
        switch (m.target)
        {
            case BoardableUnit b: Contacts.Board(b); break;
            case SalvageContainer c: Contacts.Salvage(c); break;
            case Asteroid a: Contacts.Mine(a); break;
            case TractorableItem item:
                var ship = Fleet.Flagship;
                var tractor = ship != null ? ship.GetModule<TractorModule>() : null;
                if (tractor != null && item.canBeTractorAssisted) tractor.SetManualTarget(item);
                break;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
