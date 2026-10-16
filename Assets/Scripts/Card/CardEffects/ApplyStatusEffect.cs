using UnityEngine;

[CreateAssetMenu(fileName = "ApplyStatusEffect", menuName = "Card/Effect/Apply Status")]
public class ApplyStatusEffect : CardEffect
{
    [SerializeField] private StatusEffectData status;
    [Min(1)] [SerializeField] private int turns = 1;

    public override void CollectReferencedStatuses(CardStatusCollector collector)
    {
        if (turns > 0) collector.Add(status);
    }

    public override void Preview(CardPreviewContext context, string path)
    {
        context.ApplyStatus(status, turns, target);
    }

    public override System.Action ActivateContinuous(BattleManager battleManager, ActiveContinuousCard source)
    {
        if (battleManager == null || source == null) return null;
        BattleCharacter character = target == CardTarget.Player ? battleManager.GetPlayer() :
            target == CardTarget.Enemy ? battleManager.GetTarget() : null;
        return character != null ? character.StatusEffects.ApplyContinuous(status, source) : null;
    }

    public override void Execute(BattleManager battleManager)
    {
        if (battleManager == null) return;
        BattleCharacter character;
        switch (target)
        {
            case CardTarget.Player: character = battleManager.GetPlayer(); break;
            case CardTarget.Enemy: character = battleManager.GetTarget(); break;
            default: return;
        }
        if (character != null) character.StatusEffects.Apply(status, turns);
    }
    public override string GetEffectSummary(System.Collections.Generic.HashSet<CardEffect> visiting)
    {
        return status != null ? $"{(target == CardTarget.Player ? "자신" : "적")}에게 {status.DisplayName} {turns}턴 부여" : "상태이상 부여";
    }
}
