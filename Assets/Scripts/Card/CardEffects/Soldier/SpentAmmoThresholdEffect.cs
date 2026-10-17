using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpentAmmoThresholdEffect", menuName = "Card/Effect/Spent Ammo Threshold")]
public class SpentAmmoThresholdEffect : CardEffect
{
    [Min(1)] [SerializeField] private int minSpentAmmo = 1;
    [SerializeField] private CardEffect[] effects;

    private bool CanActivate(BattleManager battle) => minSpentAmmo > 0 && battle != null &&
        battle.CanContinueEffects && battle.CurrentSpentAmmo >= minSpentAmmo && !HasCyclicReferences(new HashSet<CardEffect>());
    public override void Execute(BattleManager battle)
    {
        if (battle != null) battle.RunEffectRoutine(ExecuteRoutine(battle));
    }
    public override IEnumerator ExecuteRoutine(BattleManager battle) => CanActivate(battle) ? battle.ExecuteEffectsRoutine(effects) : null;
    public override IEnumerator ActivateContinuousRoutine(BattleManager battle, ActiveContinuousCard source) =>
        source != null && CanActivate(battle) ? battle.ExecuteEffectsRoutine(effects, source) : null;
    public override System.Action ActivateContinuous(BattleManager battle, ActiveContinuousCard source)
    {
        if (battle != null) battle.RunEffectRoutine(ActivateContinuousRoutine(battle, source));
        return null;
    }
    public override void Preview(CardPreviewContext context, string path) => context.PreviewSpentAmmoThreshold(path, minSpentAmmo, effects,
        !HasCyclicReferences(new HashSet<CardEffect>()));
    public override void CollectReferencedStatuses(CardStatusCollector collector) => collector.CollectEffects(effects);
    public override bool ContainsDelay(HashSet<CardEffect> visiting)
    {
        if (!visiting.Add(this) || effects == null) return false;
        foreach (CardEffect effect in effects) if (effect != null && effect.ContainsDelay(visiting)) return true;
        return false;
    }
    public override bool HasCyclicReferences(HashSet<CardEffect> visiting)
    {
        if (!visiting.Add(this)) return true;
        try
        {
            if (effects != null) foreach (CardEffect effect in effects)
                if (effect != null && effect.HasCyclicReferences(visiting)) return true;
            return false;
        }
        finally { visiting.Remove(this); }
    }
    public override string GetEffectSummary(HashSet<CardEffect> visiting) =>
        $"총알 {minSpentAmmo}발 이상 소모 시 추가 발동:\n" + DelayedEffectDescriptionBuilder.Summarize(effects, visiting);
}
