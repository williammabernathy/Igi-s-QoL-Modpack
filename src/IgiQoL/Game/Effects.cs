using System.Collections.Generic;
using Behaviour.Ability;
using Behaviour.Unit;
using Source.Item;

namespace IgiQoL.Game;

// A unit's active buffs and debuffs, collected the way the game's buff HUD does for your flagship (BuffHud):
// temporary effects marked for the HUD, then active abilities that have no effect of their own.
public static class Effects
{
    private static readonly List<TemporaryEffect> AllEffects = new();
    private static readonly List<ActivatedAbility> AllAbilities = new();

    public static void Collect(AbstractUnit unit, List<TemporaryEffect> effects, List<ActivatedAbility> abilities)
    {
        effects.Clear();
        abilities.Clear();
        unit.GetComponentsInChildren(AllEffects);
        foreach (var e in AllEffects)
            if (e.showInBuffHud) effects.Add(e);
        unit.GetComponentsInChildren(AllAbilities);
        foreach (var a in AllAbilities)
            if (a.showInBuffHud && (a.IsPayloadActive() || a.toggleActive) && !HasEffect(effects, a.identifier)) abilities.Add(a);
        AllEffects.Clear();
        AllAbilities.Clear();
    }

    // Debuffs are the effects the game marks as such (the red border in its buff HUD). Abilities are always buffs.
    public static int DebuffCount(AbstractUnit unit)
    {
        unit.GetComponentsInChildren(AllEffects);
        int n = 0;
        foreach (var e in AllEffects)
            if (e.showInBuffHud && e.isDebuff) n++;
        AllEffects.Clear();
        return n;
    }

    // DebuffCount plus the debuffs that have no buff-bar effect: stat boosts from outside the unit that lower a stat
    // (the painter drone's damage-reduction strip) and an armour paint strip. Boosts a buff-bar effect or one of
    // the unit's own abilities applied are left out, so an effect counts once and a self-buff's drawback not at all.
    public static int AllDebuffs(AbstractUnit unit)
    {
        int n = DebuffCount(unit);
        var boosts = unit.unitData != null ? unit.unitData.statBoosts : null;
        if (boosts != null && boosts.Count > 0)
        {
            unit.GetComponentsInChildren(AllEffects);
            unit.GetComponentsInChildren(OwnAbilities);
            foreach (var b in boosts)
                if (!b.derived && Lowers(b) && !FromUnit(b.sourceId)) n++;
            AllEffects.Clear();
            OwnAbilities.Clear();
        }
        var armor = unit.armorModule;
        if (armor != null && Resists.PaintStrip(armor) > 0f) n++;
        return n;
    }

    private static readonly List<AbstractAbility> OwnAbilities = new();

    // A longer shield recharge delay is worse; for every other stat, less is worse.
    private static bool Lowers(StatBoostEntry b)
    {
        bool down = b.amount < 0f || b.multiplier < 1f;
        bool up = b.amount > 0f || b.multiplier > 1f;
        return b.stat == EquipStat.ShieldRechargeDelay ? up : down;
    }

    // Effects and abilities name their boosts after themselves: "id" or "id:index".
    private static bool FromUnit(string sourceId)
    {
        if (sourceId == null) return false;
        foreach (var e in AllEffects)
            if (e.showInBuffHud && Named(sourceId, e.abilityIdentifier)) return true;
        foreach (var a in OwnAbilities)
            if (Named(sourceId, a.identifier)) return true;
        return false;
    }

    private static bool Named(string sourceId, string id) =>
        !string.IsNullOrEmpty(id) && sourceId.StartsWith(id, System.StringComparison.Ordinal)
        && (sourceId.Length == id.Length || sourceId[id.Length] == ':');

    private static bool HasEffect(List<TemporaryEffect> effects, string abilityId)
    {
        foreach (var e in effects)
            if (e.abilityIdentifier == abilityId) return true;
        return false;
    }
}
