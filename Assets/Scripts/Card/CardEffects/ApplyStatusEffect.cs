using UnityEngine;

[CreateAssetMenu(fileName = "ApplyStatusEffect", menuName = "Card/Effect/Apply Status")]
public class ApplyStatusEffect : CardEffect
{
    [SerializeField] private StatusEffectData status;
    [Tooltip("Duration for timed statuses, or stacks to add for Burn.")]
    [Min(1)] [SerializeField] private int turns = 1;
    [Tooltip("Magnitude granted when the status is a buff.")]
    [Min(1)] [SerializeField] private int buffPower = 1;

    public override void CollectReferencedStatuses(CardStatusCollector collector)
    {
        if (status != null && status.CanApply(turns, buffPower)) collector.Add(status);
    }

    public override void Preview(CardPreviewContext context, string path)
    {
        context.ApplyStatus(status, turns, target, buffPower);
    }

    public override System.Action ActivateContinuous(BattleManager battleManager, ActiveContinuousCard source)
    {
        if (battleManager == null || source == null) return null;
        if (status != null && (status.Type == StatusEffectType.Burn || status.Category == StatusEffectCategory.Buff))
            return base.ActivateContinuous(battleManager, source);

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
        if (character != null) character.StatusEffects.Apply(status, turns, buffPower);
    }
    public override string GetEffectSummary(System.Collections.Generic.HashSet<CardEffect> visiting)
    {
        return status != null ? $"{(target == CardTarget.Player ? "자신" : "적")}에게 {status.DisplayName} {status.FormatApplication(turns, buffPower)} 부여" : "상태이상 부여";
    }
}
