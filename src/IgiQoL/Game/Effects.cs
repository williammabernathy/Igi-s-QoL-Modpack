using System.Collections.Generic;
using Behaviour.Ability;
using Behaviour.Unit;

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

    private static bool HasEffect(List<TemporaryEffect> effects, string abilityId)
    {
        foreach (var e in effects)
            if (e.abilityIdentifier == abilityId) return true;
        return false;
    }
}
