using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DelayEffect", menuName = "Card/Effect/Delay")]
public class DelayEffect : CardEffect
{
    [Min(1)] [SerializeField] private int delayTurns = 1;
    [SerializeField] private CardEffect[] effects;
    [TextArea] [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    public int DelayTurns => delayTurns;
    public CardEffect[] Effects => effects;
    public string Description => description;
    public Sprite Icon => icon;
    public bool IsValid
    {
        get
        {
            if (delayTurns < 1 || effects == null || HasCyclicReferences(new HashSet<CardEffect>())) return false;
            foreach (CardEffect effect in effects) if (effect != null && effect != this) return true;
            return false;
        }
    }

    public override void Execute(BattleManager battle)
    {
        if (battle != null && battle.CanContinueEffects)
        {
            battle.BindBattleCharacters();
            battle.DelayedEffects.Schedule(this, battle.CurrentEffectCard, spentAmmo: battle.CurrentSpentAmmo);
        }
    }

    public override IEnumerator ActivateContinuousRoutine(BattleManager battle, ActiveContinuousCard source)
    {
        if (battle != null && battle.CanContinueEffects)
        {
            battle.BindBattleCharacters();
            battle.DelayedEffects.Schedule(this, source?.Card ?? battle.CurrentEffectCard, source, battle.CurrentSpentAmmo);
        }
        return null;
    }

    public override System.Action ActivateContinuous(BattleManager battle, ActiveContinuousCard source)
    {
        ActivateContinuousRoutine(battle, source);
        return null;
    }

    public override void CollectReferencedStatuses(CardStatusCollector collector) => collector.CollectEffects(effects);
    // Future statuses and resource changes must not affect immediate effect previews.
    public override void Preview(CardPreviewContext context, string path) => context.PreviewDelayed(path, effects);
    public override bool ContainsDelay(HashSet<CardEffect> visiting) => IsValid;
    public override bool HasCyclicReferences(System.Collections.Generic.HashSet<CardEffect> visiting)
    {
        if (!visiting.Add(this)) return true;
        try
        {
            if (effects != null)
                foreach (CardEffect effect in effects)
                    if (effect != null && effect.HasCyclicReferences(visiting)) return true;
            return false;
        }
        finally { visiting.Remove(this); }
    }

    public override string GetEffectSummary(System.Collections.Generic.HashSet<CardEffect> visiting)
    {
        return $"{delayTurns}턴 뒤 발동:\n" + DelayedEffectDescriptionBuilder.Summarize(effects, visiting);
    }
}
