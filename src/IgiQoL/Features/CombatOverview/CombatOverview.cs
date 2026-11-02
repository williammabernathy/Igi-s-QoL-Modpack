using System;
using System.Collections;
using System.Collections.Generic;
using Behaviour.Equipment.Module;
using Behaviour.Equipment.Turret.CombatTurrets;
using Behaviour.UI;
using Behaviour.UI.HUD;
using Behaviour.Unit;
using BepInEx.Configuration;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.UI;
using Source.Combat;
using Source.Player;
using Source.UI.Tooltip;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Stats = IgiQoL.Features.ShipStats.ShipStats;

namespace IgiQoL.Features.CombatOverview;

public enum ShipsShown { All, Enemies, EnemiesAndFleet }

// A pop-out window listing every ship around you with its shields, armour and hull, buffs and debuffs and target;
// click a ship for its resistances, effects, weapons, threat list and attackers. The game shows this only for the
// ship you hover (and its buff bar only for your flagship).
public sealed class CombatOverview : Feature
{
    public static CombatOverview Instance;

    public CombatOverview() { Instance = this; }

    public const string PopoutId = "combat-overview";
    internal const string ShipsOption = "ships";
    internal const float DefaultWidth = 760f, DefaultHeight = 400f;

    public override string Id => "CombatOverview";
    public override string Title => "Combat overview";
    public override string Description => "A window of its own that lists every ship around you: shields, armour and hull, its buffs and debuffs and what it's targeting. Click a ship for its resistances after debuffs, every effect on it, its weapons, its threat list and who is attacking it. Open it with the hotkey below or the button on the DPS parser tab. By default it shows only while you're in combat; its Options button changes that, its opacity and whether clicks pass through it.";
    public override Type[] PatchClasses => Type.EmptyTypes;

    public ConfigEntry<Key> Hotkey;

    internal static readonly string[] ShipsNames = { "All ships", "Enemies only", "Enemies and your fleet" };

    public override void BindConfig(ConfigFile cfg)
    {
        base.BindConfig(cfg);
        Hotkey = cfg.Bind(Id, "Hotkey", Key.F7, SettingInfo.Describe("Hotkey",
            "Key that opens and closes the combat overview. Type a key name such as F7 or F8; None turns it off. Ignored while you type in a text field."));
    }

    public override void OnActivated()
    {
        Popouts.Register(new PopoutKind
        {
            Id = PopoutId,
            Title = Title,
            Owner = this,
            DefaultSize = new Vector2(DefaultWidth, DefaultHeight),
            MinSize = new Vector2(660f, 120f),
            DefaultShow = PopoutShow.WhenRelevant,
            RelevantLabel = "in combat",
            Relevant = InCombat,
            Build = (p, body) => OverviewList.Build(p, body),
            BuildOptions = BuildOptions,
        });
        Plugin.Instance.gameObject.AddComponent<CombatOverviewHotkey>();
    }

    private static void BuildOptions(Popout p, RectTransform column)
    {
        var row = Kit.Row(column, 8f, 26f);
        Kit.Label(row, "Ships shown");
        Kit.Dropdown(row, ShipsNames, (int)Shown(p.State), i =>
        {
            p.State.options[ShipsOption] = ((ShipsShown)i).ToString();
            p.State.Save(PopoutId);
        }, 200f);
    }

    internal static ShipsShown Shown(PopoutState s)
    {
        var v = s.options[ShipsOption];
        return v.IsString && Enum.TryParse(v.AsString, out ShipsShown shown) ? shown : ShipsShown.All;
    }

    // A hit within the DPS parser's fight gap, or any ship of yours holding a target, so the window shows before the
    // first hit lands. Without the DPS parser only the target test is left.
    public static bool InCombat()
    {
        var log = CombatLog.CombatLog.Instance;
        if (log != null && log.Active && CombatLog.CombatLog.Fights.Count > 0
            && Time.time - CombatLog.CombatLog.Fights[0].last <= log.FightGapSeconds.Value) return true;
        if (HasTarget(Fleet.Flagship)) return true;
        var player = GamePlayer.current;
        if (player == null) return false;
        foreach (var d in player.activeFleet)
            if (d != null && HasTarget(d.unit)) return true;
        return false;
    }

    private static bool HasTarget(AbstractUnit unit)
    {
        if (unit == null || unit.isDestroyed) return false;
        var module = unit.GetModule<CombatModule>();
        return module != null && module.priorityTarget != null;
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx) => CombatOverviewCheck.Run(ctx);
}

internal sealed class CombatOverviewHotkey : MonoBehaviour
{
    private void Update()
    {
        var feature = CombatOverview.Instance;
        if (!feature.Active) return;
        var key = feature.Hotkey.Value;
        var kb = Keyboard.current;
        if (kb == null || key <= Key.None || (int)key > Keyboard.KeyCount) return;
        try
        {
            if (!kb[key].wasPressedThisFrame) return;
            if (UIHelper.IsTypingInInputField() || AlertPopup.IsOpen || GamePlayer.current == null || HudManager.Instance == null) return;
            Popouts.Toggle(CombatOverview.PopoutId);
        }
        catch (Exception e) { feature.Fail(e, nameof(CombatOverviewHotkey)); }
    }
}

// One row per ship, rebuilt only when the set of ships changes; texts refresh twice a second and are assigned only
// when they change. Expanded ships keep their details open across rebuilds.
internal sealed class OverviewList : MonoBehaviour
{
    private const float RefreshSeconds = 0.5f;

    private sealed class Row
    {
        public SpaceShip ship;
        public TMP_Text name, level, shield, armor, hull, buffs, debuffs, target;
        public GameObject details;
        public TMP_Text resists, effects, weapons, aggro;
    }

    internal static OverviewList Instance;

    private Popout popout;
    private RectTransform column;
    private TMP_Text summary;
    private float grow;
    private readonly List<Behaviour.Ability.TemporaryEffect> effectList = new();
    private readonly List<Behaviour.Ability.ActivatedAbility> abilityList = new();
    private readonly List<SpaceShip> ships = new();
    private readonly HashSet<SpaceShip> keys = new();
    private readonly HashSet<SpaceShip> expanded = new();
    private readonly List<Row> rows = new();
    private readonly Dictionary<SpaceShip, float> distance = new();
    private readonly List<AbstractCombatTurret> turrets = new();
    private readonly int[] weaponsByType = new int[Resists.Types.Length];
    private Comparison<SpaceShip> byGroup;
    private float nextRefresh;

    internal int RowCount => rows.Count;

    // Level, shield, armour, hull, buffs, debuffs, target: fixed columns after the ship's name.
    private static readonly string[] Columns = { "Lv", "Shield", "Armour", "Hull", "Buffs", "Debuffs", "Targeting" };
    private static readonly float[] Widths = { 30f, 56f, 62f, 50f, 50f, 64f, 150f };
    private const float TargetGap = 8f;

    internal static void Build(Popout p, RectTransform body)
    {
        var list = Kit.Scroll(body);
        var self = list.gameObject.AddComponent<OverviewList>();
        self.popout = p;
        self.column = list;
        self.grow = Mathf.Max(1f, TextScale.TextScale.Factor);
        self.summary = Kit.Muted(body, "");
        // One short line fits the narrowest window; the longer explanation is on hover.
        Kit.Tooltip(self.summary, "Resistances", "A ship's resistances are the share of a hit it resists now. After the | comes the total on armour.");
        Instance = self;
    }

    // Inside the scrolled list, so the titles line up with the rows under them.
    private void AddHeader()
    {
        var header = Kit.Row(column, 4f, 18f);
        Kit.Label(header, "Ship (click for details)", 11f).color = ColorHelper.poiTooltipMuted;
        for (int i = 0; i < Columns.Length; i++)
            Kit.Fixed(header, Columns[i], Widths[i] * grow, i == Columns.Length - 1 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight, ColorHelper.poiTooltipMuted);
        // The last column is left-aligned right after a right-aligned one, so it needs a gap.
        var target = header.GetChild(header.childCount - 1).GetComponent<TMP_Text>();
        if (target != null) target.margin = new Vector4(TargetGap, 0f, 0f, 0f);
    }

    private void Awake()
    {
        byGroup = (a, b) =>
        {
            int ga = Group(a), gb = Group(b);
            return ga != gb ? ga.CompareTo(gb) : distance[a].CompareTo(distance[b]);
        };
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!CombatOverview.Instance.Active || !popout.Shown || Time.unscaledTime < nextRefresh) return;
        Tick();
    }

    internal void Tick()
    {
        nextRefresh = Time.unscaledTime + RefreshSeconds;
        try
        {
            Collect();
            if (Changed()) Rebuild();
            else Refresh();
        }
        catch (Exception e)
        {
            CombatOverview.Instance.Fail(e, nameof(OverviewList));
            Popouts.Close(CombatOverview.PopoutId);
        }
    }

    // Enemies first, then your ships, then everyone else; nearest first within each.
    private static int Group(SpaceShip s) => s.IsPlayerEnemy() ? 0 : Fleet.IsPlayerShip(s) ? 1 : 2;

    private void Collect()
    {
        ships.Clear();
        distance.Clear();
        var shown = CombatOverview.Shown(popout.State);
        var flagship = Fleet.Flagship;
        Vector2 from = flagship != null ? (Vector2)flagship.transform.position : Vector2.zero;
        // Every ship at the location; carrier fighters and drones aren't listed, stations and turrets aren't ships, and
        // the copy the hangar and shipyard show of a ship (inShipYard) isn't in space.
        foreach (var s in Object.FindObjectsByType<SpaceShip>(FindObjectsInactive.Exclude))
        {
            if (s == null || s.isDestroyed || s.isCarrierFighter || s.inShipYard || !s.gameObject.activeInHierarchy) continue;
            bool enemy = s.IsPlayerEnemy();
            if (shown == ShipsShown.Enemies && !enemy) continue;
            if (shown == ShipsShown.EnemiesAndFleet && !enemy && !Fleet.IsPlayerShip(s)) continue;
            ships.Add(s);
            distance[s] = Vector2.Distance(from, s.transform.position);
        }
    }

    private bool Changed()
    {
        if (keys.Count != ships.Count) return true;
        foreach (var s in ships)
            if (!keys.Contains(s)) return true;
        return false;
    }

    private void Rebuild()
    {
        for (int i = column.childCount - 1; i >= 0; i--)
        {
            var child = column.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        rows.Clear();
        keys.Clear();
        expanded.RemoveWhere(s => s == null || !distance.ContainsKey(s));
        ships.Sort(byGroup);
        AddHeader();
        foreach (var s in ships)
        {
            keys.Add(s);
            var ship = s;
            var line = Kit.Row(column, 4f, 20f);
            var r = new Row { ship = s, name = Kit.Label(line, "") };
            r.name.color = Group(s) switch { 0 => ColorHelper.red90, 1 => ColorHelper.greenishLight, _ => ColorHelper.offWhite };
            r.name.raycastTarget = true;
            var button = r.name.gameObject.AddComponent<Button>();
            button.targetGraphic = r.name;
            button.onClick.AddListener(() => ToggleDetails(ship));
            r.level = Value(line, 0);
            r.shield = Value(line, 1);
            r.armor = Value(line, 2);
            r.hull = Value(line, 3);
            r.buffs = Value(line, 4);
            r.debuffs = Value(line, 5);
            r.target = Value(line, 6);
            r.target.alignment = TextAlignmentOptions.MidlineLeft;
            r.target.margin = new Vector4(TargetGap, 0f, 0f, 0f);
            r.target.overflowMode = TextOverflowModes.Ellipsis;

            var details = Kit.Column(column, 2f);
            details.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(14, 4, 0, 4);
            r.resists = Kit.Text(details, "");
            r.effects = Kit.Text(details, "");
            r.weapons = Kit.Text(details, "");
            r.aggro = Kit.Text(details, "");
            r.details = details.gameObject;
            r.details.SetActive(expanded.Contains(s));
            rows.Add(r);
        }
        if (ships.Count == 0) Kit.Muted(column, "No ships here.");
        Refresh();
    }

    private TMP_Text Value(RectTransform row, int index) =>
        Kit.Fixed(row, "", Widths[index] * grow, TextAlignmentOptions.MidlineRight, ColorHelper.offWhite);

    internal void ToggleDetails(SpaceShip ship)
    {
        if (!expanded.Remove(ship)) expanded.Add(ship);
        foreach (var r in rows)
            if (r.ship == ship)
            {
                r.details.SetActive(expanded.Contains(ship));
                Refresh(r);
            }
    }

    internal string Details(SpaceShip ship)
    {
        foreach (var r in rows)
            if (r.ship == ship) return r.resists.text + "\n" + r.effects.text + "\n" + r.weapons.text + "\n" + r.aggro.text;
        return null;
    }

    private void Refresh()
    {
        int enemies = 0;
        foreach (var r in rows)
        {
            if (r.ship == null || r.ship.isDestroyed) continue;
            if (r.ship.IsPlayerEnemy()) enemies++;
            Refresh(r);
        }
        Set(summary, rows.Count + (rows.Count == 1 ? " ship" : " ships") + ", " + enemies + (enemies == 1 ? " enemy" : " enemies") + ". Click a ship for its resistances, effects, weapons and attackers.");
    }

    private void Refresh(Row r)
    {
        var s = r.ship;
        if (s == null || s.isDestroyed) return;
        string expand = r.details.activeSelf ? "- " : "+ ";
        Set(r.name, expand + Aggro.Name(s));
        Set(r.level, s.level.ToString());
        Set(r.shield, Share(s.currentShieldHP, s.maxShieldHP));
        Set(r.armor, Share(s.currentArmorHP, s.maxArmorHP));
        Set(r.hull, Share(s.currentHullHP, s.maxHullHP));
        int debuffs = Effects.AllDebuffs(s);
        Effects.Collect(s, effectList, abilityList);
        int buffs = abilityList.Count;
        int shownDebuffs = 0;
        foreach (var e in effectList)
        {
            if (e.isDebuff) shownDebuffs++;
            else buffs++;
        }
        Set(r.buffs, buffs.ToString());
        Set(r.debuffs, debuffs.ToString());
        var debuffColor = debuffs > 0 ? ColorHelper.red90 : ColorHelper.offWhite;
        if (r.debuffs.color != debuffColor) r.debuffs.color = debuffColor;
        var target = Aggro.Target(s);
        Set(r.target, target != null ? Aggro.Name(target) : "-");
        if (!r.details.activeSelf)
        {
            effectList.Clear();
            abilityList.Clear();
            return;
        }

        Set(r.resists, "<color=#8FA3B8>Resists</color>  " + Join(Stats.Section(s), true));
        int hidden = debuffs - shownDebuffs;
        var effects = Stats.EffectsSection(effectList, abilityList);
        effectList.Clear();
        abilityList.Clear();
        Set(r.effects, "<color=#8FA3B8>Effects</color>  " + (effects != null ? Join(effects, false) : "none")
                       + (hidden > 0 ? " | <color=#E64D4D>" + hidden + (hidden == 1 ? " stat debuff" : " stat debuffs") + "</color>" : ""));
        Set(r.weapons, "<color=#8FA3B8>Weapons</color>  " + Weapons(s));
        var threat = Stats.ThreatSection(s);
        var attackers = Stats.AttackersSection(s);
        Set(r.aggro, "<color=#8FA3B8>Threat</color>  " + (threat != null ? Join(threat, false) : "none")
                     + "    <color=#8FA3B8>Attacked by</color>  " + (attackers != null ? Join(attackers, false) : "nobody"));
    }

    // The hover card's rows on one line: a coloured name (by damage type or buff/debuff), then the value and badge.
    private static string Join(TooltipSection section, bool colourValues)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var row in section.rows)
        {
            if (sb.Length > 0) sb.Append(" | ");
            if (row.dotColor.HasValue) sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(row.dotColor.Value)).Append('>').Append(row.label).Append("</color>");
            else sb.Append(row.label);
            if (!string.IsNullOrEmpty(row.value))
            {
                sb.Append(' ');
                if (colourValues && row.valueColor.HasValue) sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(row.valueColor.Value)).Append('>').Append(row.value).Append("</color>");
                else sb.Append(row.value);
            }
            if (row.badge != null) sb.Append(" (").Append(row.badge).Append(')');
        }
        return sb.ToString();
    }

    // Combat weapons by damage type, and their range.
    private string Weapons(SpaceShip s)
    {
        s.GetComponentsInChildren(turrets);
        if (turrets.Count == 0) return "none";
        Array.Clear(weaponsByType, 0, weaponsByType.Length);
        float min = float.MaxValue, max = 0f;
        foreach (var t in turrets)
        {
            int type = (int)t.damageType;
            if (type >= 0 && type < weaponsByType.Length) weaponsByType[type]++;
            min = Mathf.Min(min, t.range);
            max = Mathf.Max(max, t.range);
        }
        turrets.Clear();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < weaponsByType.Length; i++)
        {
            if (weaponsByType[i] == 0) continue;
            if (sb.Length > 0) sb.Append(", ");
            var type = Resists.Types[i];
            sb.Append(weaponsByType[i]).Append(" <color=#").Append(ColorUtility.ToHtmlStringRGB(type.GetColor())).Append('>').Append(type.GetDisplayName()).Append("</color>");
        }
        sb.Append(" | range ").Append(min.ToString("0")).Append(max - min >= 0.5f ? " to " + max.ToString("0") : "");
        return sb.ToString();
    }

    private static string Share(float current, float max) => max > 0f ? Mathf.RoundToInt(Mathf.Clamp01(current / max) * 100f) + "%" : "-";

    private static void Set(TMP_Text t, string text)
    {
        if (t.text != text) t.text = text;
    }
}
