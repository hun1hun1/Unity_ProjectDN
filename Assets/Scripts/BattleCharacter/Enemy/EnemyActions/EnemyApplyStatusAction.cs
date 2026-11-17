using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyApplyStatusAction", menuName = "Enemy/Action/Apply Status")]
public class EnemyApplyStatusAction : EnemyAction
{
    [SerializeField] private StatusEffectData status;
    [Tooltip("Duration for timed statuses, or stacks to add for Burn.")]
    [Min(1)] [SerializeField] private int turns = 2;
    [Tooltip("Magnitude granted when the status is a buff.")]
    [Min(1)] [SerializeField] private int buffPower = 1;
    [SerializeField] private EnemyTargetType target = EnemyTargetType.Player;

    public override EnemyActionKinds GetActionKinds() =>
        status != null && status.Category == StatusEffectCategory.Buff ? EnemyActionKinds.Buff : EnemyActionKinds.Debuff;

    public override Sprite GetActionIcon() => base.GetActionIcon() ??
        (GetActionKinds() == EnemyActionKinds.Buff ? status.Icon : null);
    internal override void Validate(HashSet<EnemyAction> visiting)
    {
        base.Validate(visiting);
        if (status == null || !status.CanApply(turns, buffPower) ||
            !Enum.IsDefined(typeof(EnemyTargetType), target))
            throw new InvalidOperationException($"Status action '{name}' needs a valid status, target and application amount.");
    }
    public override string GetActionDescription()
    {
        Validate();
        return $"{(target == EnemyTargetType.Player ? "플레이어" : "적 자신")}에게 {status.DisplayName} {status.FormatApplication(turns, buffPower)} 부여";
    }
    public override string GetActionDescription(BattleManager battle) => Preview(new EnemyActionPreviewContext(battle));
    internal override string Preview(EnemyActionPreviewContext context)
    {
        string text = GetActionDescription();
        context.Apply(status, target, turns, buffPower);
        if (!context.SummarizeStatuses) return text;
        return status.Category == StatusEffectCategory.Buff ? $"{status.DisplayName} {buffPower} 부여" : "디버프";
    }
    public override void Execute(BattleManager battle)
    {
        Validate();
        if (battle == null || !battle.CanContinueEffects) return;
        BattleCharacter recipient = target == EnemyTargetType.Player ? battle.GetPlayer() : battle.GetTarget();
        if (recipient != null) recipient.StatusEffects.Apply(status, turns, buffPower);
    }
}
