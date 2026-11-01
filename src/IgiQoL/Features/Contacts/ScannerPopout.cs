using System.Collections;
using System.Collections.Generic;
using Behavior.UI.GalaxyMap;
using Behaviour.Managers;
using Behaviour.Mining;
using Behaviour.Item;
using Behaviour.Salvage;
using Behaviour.Tractoring;
using Behaviour.UI.Shared;
using Behaviour.Util;
using Behaviour.Unit;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.UI;
using Source.Data.Persistable;
using Source.Galaxy;
using Source.Item;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IgiQoL.Features.Contacts;

internal static class ScannerPopout
{
    internal const string ListCollapsed = "listCollapsed";

    internal static void Register(Contacts owner)
    {
        Popouts.Register(new PopoutKind
        {
            Id = Contacts.PopoutId,
            Title = "Scanner",
            Owner = owner,
            DefaultSize = new Vector2(360f, 480f),
            MinSize = new Vector2(280f, 260f),
            DefaultShow = PopoutShow.Always,
            RelevantLabel = "there's something here to board, salvage or pick up",
            Relevant = Contacts.HasContacts,
            DockTab = "contacts",
            Build = Build,
        });
    }

    // The minimap, then a header row with the list's +/- toggle, then the Scanner tab's own list.
    private static void Build(Popout p, RectTransform body)
    {
        var area = UiCards.NewChild("MapArea", body);
        var areaLayout = area.gameObject.AddComponent<LayoutElement>();
        areaLayout.preferredHeight = 240f;
        areaLayout.minHeight = 100f;
        areaLayout.flexibleWidth = 1f;
        var map = area.gameObject.AddComponent<ScannerMap>();

        var header = Kit.Row(body, 6f, 24f);
        var title = Kit.Label(header, "Contacts");
        title.color = ColorHelper.stationCardTitle;
        var column = Kit.Scroll(body);
        var scroll = column.parent.parent.gameObject;
        var list = column.gameObject.AddComponent<ContactsList>();
        list.KeepWindow = true;
        list.SelectRows = true;

        TMP_Text toggleText = null;
        void Apply()
        {
            bool collapsed = p.State.options[ListCollapsed].AsBoolean;
            scroll.SetActive(!collapsed);
            areaLayout.flexibleHeight = collapsed ? 1f : 0f;
            toggleText.text = collapsed ? "+" : "-";
        }
        var toggle = Kit.TextTile(header, "-", 22f, () =>
        {
            p.State.options[ListCollapsed] = !p.State.options[ListCollapsed].AsBoolean;
            p.State.Save(p.Id);
            Apply();
        });
        toggleText = toggle.GetComponentInChildren<TMP_Text>();
        Kit.Tooltip(toggleText, null, "Show or hide the contact list under the map.");
        Apply();

        map.Init(p, list, title);
    }
}

// The location drawn flat: its bounds scaled to fit the pop-out, a pooled dot per ship and contact, and the rectangle
// the game camera shows. Dot positions refresh every quarter second and the contact set every second, into dots that
// are made once and reused, so nothing is allocated per refresh.
internal sealed class ScannerMap : MonoBehaviour
{
    private const float PositionSeconds = 0.25f;
    private const float CollectSeconds = 1f;
    private const float ContactSize = 14f;
    private const float ShipSize = 6f;
    private const float FlagshipSize = 10f;
    private const float HaloGrow = 10f;

    internal static ScannerMap Instance;

    private sealed class Dot
    {
        public RectTransform rt;
        public GameObject halo;
        public Image icon;
        public Component target;
        public object key;
        public bool shown = true, selected;
        public Color tint;
    }

    private sealed class Pool
    {
        public readonly List<Dot> dots = new();
        public int used;
        public ContactMarkers.Kind kind;
        public bool ship;
        public Color color;
    }

    private static readonly Color HaloColor = new(1f, 1f, 1f, 0.55f);
    private static readonly Color ViewColor = new(1f, 1f, 1f, 0.45f);
    private static readonly Color FleetColor = new(0.4f, 0.85f, 1f);

    private Popout popout;
    private ContactsList list;
    private TMP_Text countLabel;
    private RectTransform area, map, view;
    private readonly Pool[] contactPools = new Pool[4];
    private Pool enemyPool, fleetPool;
    private Dot flagshipDot;
    private readonly List<Asteroid> asteroids = new();
    private Rect bounds;
    private Vector2 areaSize;
    private float nextPosition, nextCollect;
    private int contactCount = -1;
    private object appliedSelection;
    private bool grew;

    internal Vector2 MapSize => map.sizeDelta;

    internal void Init(Popout p, ContactsList contacts, TMP_Text label)
    {
        popout = p;
        list = contacts;
        countLabel = label;
        Instance = this;
        area = (RectTransform)transform;

        map = UiCards.NewChild("Map", area);
        map.anchorMin = map.anchorMax = map.pivot = new Vector2(0.5f, 0.5f);
        UiCards.AddBackground(map.gameObject, ColorHelper.panelHeaderBlue);
        map.gameObject.AddComponent<RectMask2D>();

        // The camera's view: four hairlines on a rect that follows the camera.
        view = UiCards.NewChild("View", map);
        view.anchorMin = view.anchorMax = Vector2.zero;
        view.pivot = new Vector2(0.5f, 0.5f);
        AddLine(view, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, 1f));
        AddLine(view, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f));
        AddLine(view, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(1f, 0f));
        AddLine(view, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(1f, 0f));

        enemyPool = new Pool { ship = true, color = ColorHelper.reddish };
        fleetPool = new Pool { ship = true, color = FleetColor };
        foreach (ContactMarkers.Kind kind in System.Enum.GetValues(typeof(ContactMarkers.Kind)))
            contactPools[(int)kind] = new Pool { kind = kind };
        flagshipDot = NewShipDot(Color.white, FlagshipSize);
        flagshipDot.rt.gameObject.name = "Flagship";
    }

    private static void AddLine(RectTransform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size)
    {
        var line = UiCards.NewChild("Line", parent);
        line.anchorMin = min;
        line.anchorMax = max;
        line.pivot = pivot;
        line.anchoredPosition = Vector2.zero;
        line.sizeDelta = size;
        UiCards.AddBackground(line.gameObject, ViewColor).raycastTarget = false;
    }

    private Dot NewShipDot(Color color, float size)
    {
        var rt = UiCards.NewChild("Ship", map);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var icon = UiCards.AddBackground(rt.gameObject, color);
        icon.raycastTarget = false;
        return new Dot { rt = rt, icon = icon, tint = color };
    }

    private Dot NewContactDot(ContactMarkers.Kind kind)
    {
        var rt = UiCards.NewChild("Dot" + kind, map);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(ContactSize, ContactSize);
        var halo = UiCards.NewChild("Halo", rt);
        halo.anchorMin = halo.anchorMax = halo.pivot = new Vector2(0.5f, 0.5f);
        halo.sizeDelta = new Vector2(ContactSize + HaloGrow, ContactSize + HaloGrow);
        UiCards.AddBackground(halo.gameObject, HaloColor).raycastTarget = false;
        halo.gameObject.SetActive(false);
        var tint = ContactMarkers.Tint(kind);
        var icon = FleetBarParts.AddIcon(rt, ContactMarkers.IconKey(kind), tint, ContactSize);
        icon.raycastTarget = true;
        var dot = new Dot { rt = rt, halo = halo.gameObject, icon = icon, tint = tint };
        var button = icon.gameObject.AddComponent<Button>();
        button.targetGraphic = icon;
        button.onClick.AddListener(() => ClickDot(dot));
        FleetBarParts.SetTooltip(button, null, kind switch
        {
            ContactMarkers.Kind.Boardable => "Boardable ship. Click to select it in the list.",
            ContactMarkers.Kind.Wreck => "Wreck. Click to select it in the list.",
            ContactMarkers.Kind.Ore => "Asteroid with ore. Click to select its ore in the list.",
            _ => "Loot. Click to select it in the list.",
        });
        return dot;
    }

    private void Update()
    {
        if (!Contacts.Instance.Active || !popout.Shown || Time.unscaledTime < nextPosition) return;
        nextPosition = Time.unscaledTime + PositionSeconds;
        try
        {
            if (!Fit()) return;
            if (Time.unscaledTime >= nextCollect)
            {
                nextCollect = Time.unscaledTime + CollectSeconds;
                Collect();
            }
            if (!object.Equals(list.Selected, appliedSelection)) ApplySelection();
            Place();
        }
        catch (System.Exception e)
        {
            Contacts.Instance.Fail(e, nameof(ScannerMap));
            Popouts.Close(popout.Id);
        }
    }

    // Sizes the map to the location's bounds inside the area; false while there is no location to draw.
    private bool Fit()
    {
        var poi = BasePoiManager.current;
        bool show = poi != null && Fleet.Flagship != null;
        if (map.gameObject.activeSelf != show) map.gameObject.SetActive(show);
        if (!show) return false;
        var b = poi.worldCoordinates;
        Vector2 a = area.rect.size;
        if (a.x < 4f || a.y < 4f || b.width <= 0f || b.height <= 0f) return false;
        if (b == bounds && a == areaSize) return true;
        bounds = b;
        areaSize = a;
        float scale = Mathf.Min(a.x / b.width, a.y / b.height);
        map.sizeDelta = new Vector2(b.width * scale, b.height * scale);
        return true;
    }

    private Vector2 ToMap(Vector3 world) =>
        new((world.x - bounds.xMin) / bounds.width * map.sizeDelta.x, (world.y - bounds.yMin) / bounds.height * map.sizeDelta.y);

    // Where the dot for a contact sits, in map units from the map's bottom-left corner; false when it has none.
    internal bool TryDot(Component target, out Vector2 position)
    {
        foreach (var pool in contactPools)
            for (int i = 0; i < pool.used; i++)
            {
                var d = pool.dots[i];
                if (d.target != target || !d.shown) continue;
                position = d.rt.anchoredPosition;
                return true;
            }
        position = Vector2.zero;
        return false;
    }

    internal Vector2 Expected(Vector3 world) => ToMap(world);

    internal bool IsSelected(Component target)
    {
        foreach (var pool in contactPools)
            for (int i = 0; i < pool.used; i++)
                if (pool.dots[i].target == target) return pool.dots[i].selected;
        return false;
    }

    internal bool ClickDot(Component target)
    {
        foreach (var pool in contactPools)
            for (int i = 0; i < pool.used; i++)
                if (pool.dots[i].target == target)
                {
                    ClickDot(pool.dots[i]);
                    return true;
                }
        return false;
    }

    private void ClickDot(Dot d)
    {
        if (d.key == null) return;
        list.Select(d.key, true);
        ApplySelection();
    }

    private void ApplySelection()
    {
        appliedSelection = list.Selected;
        foreach (var pool in contactPools)
            for (int i = 0; i < pool.used; i++) MarkSelected(pool.dots[i]);
    }

    private void MarkSelected(Dot d)
    {
        bool on = d.key != null && appliedSelection != null && object.Equals(d.key, appliedSelection);
        if (d.selected == on) return;
        d.selected = on;
        d.halo.SetActive(on);
    }

    private Dot Take(Pool pool)
    {
        if (pool.used == pool.dots.Count)
        {
            pool.dots.Add(pool.ship ? NewShipDot(pool.color, ShipSize) : NewContactDot(pool.kind));
            grew = true;
        }
        var d = pool.dots[pool.used++];
        if (!d.shown)
        {
            d.shown = true;
            d.rt.gameObject.SetActive(true);
        }
        return d;
    }

    private static void Hide(Dot d)
    {
        if (!d.shown) return;
        d.shown = false;
        d.rt.gameObject.SetActive(false);
    }

    private void Give(Pool pool, Component target, int scanLevel, Color? tint = null)
    {
        var d = Take(pool);
        d.target = target;
        if (pool.ship) return;
        d.key = ContactsList.KeyOf(target, scanLevel);
        if (tint.HasValue && d.tint != tint.Value)
        {
            d.tint = tint.Value;
            d.icon.color = tint.Value;
        }
        MarkSelected(d);
    }

    // The contact set, as ContactsList.Collect reads it (the asteroid walk is why this runs once a second, not every refresh).
    private void Collect()
    {
        grew = false;
        foreach (var pool in contactPools) pool.used = 0;
        enemyPool.used = fleetPool.used = 0;
        int scanLevel = Asteroid.GetTooltipScanLevel();

        foreach (var b in BoardableUnit.all)
            if (b != null && b.gameObject.activeInHierarchy) Give(contactPools[(int)ContactMarkers.Kind.Boardable], b, scanLevel);
        foreach (var c in SalvageContainer.all)
            if (c != null && c.gameObject.activeInHierarchy) Give(contactPools[(int)ContactMarkers.Kind.Wreck], c, scanLevel);
        Contacts.PruneLoot();
        foreach (var item in Contacts.Loot)
            if (item.data?.itemType != null) Give(contactPools[(int)ContactMarkers.Kind.Loot], item, scanLevel, item.data.itemType.rarity.GetColor());
        var poi = BasePoiManager.current;
        if (poi != null)
        {
            poi.GetComponentsInChildren(asteroids);
            foreach (var a in asteroids)
            {
                var data = a.asteroidData;
                if (data == null || data.isVisualOnly) continue;
                bool surface = data.surfaceAmount > 0 && data.surfaceItem != null;
                bool core = scanLevel > 0 && data.innerCoreAmount > 0 && data.innerCoreItem != null;
                if (!surface && !core) continue;
                Give(contactPools[(int)ContactMarkers.Kind.Ore], a, scanLevel, (surface ? data.surfaceItem : data.innerCoreItem).item.rarity.GetColor());
            }
            asteroids.Clear();
        }

        var flagship = Fleet.Flagship;
        foreach (var s in Object.FindObjectsByType<SpaceShip>(FindObjectsInactive.Exclude))
        {
            if (s == null || s == flagship || s.isDestroyed || s.isCarrierFighter || !s.gameObject.activeInHierarchy) continue;
            if (s.IsPlayerEnemy()) Give(enemyPool, s, scanLevel);
            else if (Fleet.IsPlayerShip(s)) Give(fleetPool, s, scanLevel);
        }

        int contacts = 0;
        foreach (var pool in contactPools) contacts += pool.used;
        if (contacts != contactCount)
        {
            contactCount = contacts;
            countLabel.text = "Contacts (" + contacts + ")";
        }
        foreach (var pool in contactPools) HideRest(pool);
        HideRest(enemyPool);
        HideRest(fleetPool);
        // Dots made since last time sit above the flagship's, which stays on top.
        if (grew) flagshipDot.rt.SetAsLastSibling();
        nextPosition = 0f;
    }

    private static void HideRest(Pool pool)
    {
        for (int i = pool.used; i < pool.dots.Count; i++)
        {
            Hide(pool.dots[i]);
            pool.dots[i].target = null;
            pool.dots[i].key = null;
        }
    }

    private void Place()
    {
        foreach (var pool in contactPools) PlacePool(pool);
        PlacePool(enemyPool);
        PlacePool(fleetPool);

        var ship = Fleet.Flagship;
        if (ship != null)
        {
            if (!flagshipDot.shown)
            {
                flagshipDot.shown = true;
                flagshipDot.rt.gameObject.SetActive(true);
            }
            flagshipDot.rt.anchoredPosition = ToMap(ship.transform.position);
        }
        else Hide(flagshipDot);

        var cam = GameplayManager.camera;
        if (cam != null)
        {
            float h = cam.orthographicSize * 2f, w = h * cam.aspect;
            view.anchoredPosition = ToMap(cam.transform.position);
            view.sizeDelta = new Vector2(w / bounds.width * map.sizeDelta.x, h / bounds.height * map.sizeDelta.y);
        }
    }

    private void PlacePool(Pool pool)
    {
        for (int i = 0; i < pool.used; i++)
        {
            var d = pool.dots[i];
            var t = d.target;
            // A contact taken or destroyed since the last collect: hide its dot now, the next collect drops it.
            if (t == null || !t.gameObject.activeInHierarchy || (pool.ship && ((SpaceShip)t).isDestroyed))
            {
                Hide(d);
                continue;
            }
            if (!d.shown)
            {
                d.shown = true;
                d.rt.gameObject.SetActive(true);
            }
            d.rt.anchoredPosition = ToMap(t.transform.position);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

internal static class ScannerPopoutCheck
{
    internal static IEnumerator Run(SelfCheckContext ctx)
    {
        const string id = Contacts.PopoutId;
        string pref = PlayerPrefs.GetString("IgiQoL.Popout." + id, "");
        bool wasOpen = Popouts.IsOpen(id);
        ctx.Cleanup(() =>
        {
            Popouts.Close(id);
            PlayerPrefs.SetString("IgiQoL.Popout." + id, pref);
            Popouts.ForgetState(id);
            if (wasOpen) Popouts.Open(id);
        });

        yield return SelfCheckContext.EnsureInSpace();
        var ship = Fleet.Flagship;
        if (ship == null || BasePoiManager.current == null || !Singleton<LootManager>.HasInstance)
        {
            ctx.Skip("scanner-popout", "not in space");
            yield break;
        }
        InventoryItemType ore = null;
        foreach (var t in InventoryItemType.all)
            if (t.itemCategory == ItemCategory.Ore) { ore = t; break; }
        if (ore == null)
        {
            ctx.Skip("scanner-popout", "no ore item type");
            yield break;
        }

        Popouts.Close(id);
        var state = Popouts.State(id);
        state.show = PopoutShow.Always;
        state.collapsed = false;
        state.clickThrough = true;
        state.opacity = 100;
        state.options[ScannerPopout.ListCollapsed] = false;
        state.placed = false;
        Popouts.Open(id);
        var p = Popouts.Get(id);
        if (p == null)
        {
            ctx.Fail("scanner-popout", "the pop-out did not open");
            yield break;
        }

        var item = Singleton<LootManager>.Instance.SpawnLootItem(new TractorableItemData
        {
            itemType = ore,
            itemAmount = 3,
            jettisoned = true,
            ownerFaction = Faction.player,
            position = (Vector2)ship.transform.position + new Vector2(30f, 20f),
        }, null);
        try
        {
            yield return new WaitForSeconds(1.6f);
            var map = ScannerMap.Instance;
            var list = p.GetComponentInChildren<ContactsList>();
            if (map == null || list == null)
            {
                ctx.Fail("scanner-popout", $"map={(map != null)} list={(list != null)}");
                yield break;
            }

            bool found = map.TryDot(item, out var at);
            Vector2 want = map.Expected(item.transform.position), size = map.MapSize;
            bool inside = at.x >= 0f && at.y >= 0f && at.x <= size.x && at.y <= size.y;
            string detail = $"dot={found} at=({at.x:0.0},{at.y:0.0}) expected=({want.x:0.0},{want.y:0.0}) map=({size.x:0},{size.y:0}) inside={inside}";
            if (found && inside && Vector2.Distance(at, want) < 1f) ctx.Pass("scanner-map-dot", detail);
            else ctx.Fail("scanner-map-dot", detail);

            var row = list.RowOf(item.data.itemType);
            if (row != null && list.KeepWindow) ctx.Pass("scanner-list-row", $"rows={list.RowCount} keepWindow={list.KeepWindow}");
            else ctx.Fail("scanner-list-row", $"row={(row != null)} rows={list.RowCount} keepWindow={list.KeepWindow}");

            // Dot to row: the click selects the row and lights the dot.
            bool clicked = map.ClickDot(item);
            var bg = row != null ? row.GetComponent<Image>() : null;
            bool rowLit = bg != null && bg.color.a > 0.05f;
            string dotDetail = $"clicked={clicked} selected={object.Equals(list.Selected, item.data.itemType)} rowLit={rowLit} dotLit={map.IsSelected(item)}";
            if (clicked && object.Equals(list.Selected, item.data.itemType) && rowLit && map.IsSelected(item)) ctx.Pass("scanner-select-from-dot", dotDetail);
            else ctx.Fail("scanner-select-from-dot", dotDetail);

            // Row to dot: the dot's highlight follows the list.
            list.Select(null, false);
            yield return new WaitForSeconds(0.5f);
            bool cleared = !map.IsSelected(item);
            var rowButton = row != null ? row.GetComponent<Button>() : null;
            rowButton?.onClick.Invoke();
            yield return new WaitForSeconds(0.5f);
            string rowDetail = $"cleared={cleared} lit={map.IsSelected(item)}";
            if (cleared && rowButton != null && map.IsSelected(item)) ctx.Pass("scanner-select-from-row", rowDetail);
            else ctx.Fail("scanner-select-from-row", rowDetail);
            yield return ctx.Screenshot("scanner-popout");

            // A row action leaves the pop-out open.
            Button tractor = null;
            if (row != null)
                foreach (var b in row.GetComponentsInChildren<Button>())
                {
                    var text = b.GetComponentInChildren<TMP_Text>();
                    if (text != null && text.text == "Tractor") tractor = b;
                }
            if (tractor == null) ctx.Skip("scanner-row-action", "no tractor beam on the flagship");
            else
            {
                tractor.onClick.Invoke();
                yield return null;
                string actionDetail = $"popOutOpen={Popouts.IsOpen(id)}";
                if (Popouts.IsOpen(id)) ctx.Pass("scanner-row-action", actionDetail);
                else ctx.Fail("scanner-row-action", actionDetail);
            }
        }
        finally
        {
            Popouts.Close(id);
            if (item != null) Object.DestroyImmediate(item.gameObject);
            Contacts.PruneLoot();
        }
    }
}
