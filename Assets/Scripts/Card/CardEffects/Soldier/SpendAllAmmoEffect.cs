using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpendAllAmmoEffect", menuName = "Card/Effect/Spend All Ammo")]
public class SpendAllAmmoEffect : CardEffect
{
    [SerializeField] private CardEffect[] effects;

    public override void Execute(BattleManager battle)
    {
        if (battle != null) battle.RunEffectRoutine(ExecuteRoutine(battle));
    }
    public override IEnumerator ExecuteRoutine(BattleManager battle) => ExecuteChildren(battle, null);
    public override IEnumerator ActivateContinuousRoutine(BattleManager battle, ActiveContinuousCard source) =>
        source != null ? ExecuteChildren(battle, source) : null;
    public override System.Action ActivateContinuous(BattleManager battle, ActiveContinuousCard source)
    {
        if (battle != null) battle.RunEffectRoutine(ActivateContinuousRoutine(battle, source));
        return null;
    }

    private IEnumerator ExecuteChildren(BattleManager battle, ActiveContinuousCard source)
    {
        if (battle == null || !battle.CanContinueEffects || source != null && !source.IsActive ||
            effects == null || !System.Array.Exists(effects, e => e != null) || HasCyclicReferences(new HashSet<CardEffect>())) yield break;
        Player player = battle.GetPlayer();
        AmmoSystem ammo = player != null ? player.GetAmmoSystem() : null;
        if (ammo == null) yield break;
        int spent = ammo.SpendAll();
        if (spent == 0) yield break;
        yield return battle.ExecuteWithSpentAmmoRoutine(effects, spent, source);
    }

    public override void Preview(CardPreviewContext context, string path) => context.PreviewSpendAllAmmo(path, effects,
        effects != null && System.Array.Exists(effects, e => e != null) && !HasCyclicReferences(new HashSet<CardEffect>()));
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
        "총알을 모두 소모하여 발동 (1발 이상):\n" + DelayedEffectDescriptionBuilder.Summarize(effects, visiting);
}
