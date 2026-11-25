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
        string recipient = target == EnemyTargetType.Player ? "플레이어" : "자신";
        if (context.IsImmune(target, status.Type))
        {
            context.AddIntent($"{recipient}에게 {status.DisplayName} 효과를 부여하려 하지만 면역으로 적용되지 않습니다");
            return $"{status.DisplayName} · 면역";
        }

        context.Apply(status, target, turns, buffPower);
        string detail = status.Type == StatusEffectType.Burn
            ? $"{recipient}에게 {status.DisplayName} {turns}중첩을 부여합니다"
            : $"{recipient}에게 {status.DisplayName} 효과를 {turns}턴 동안 부여합니다";
        if (status.Category == StatusEffectCategory.Buff)
            detail = $"{recipient}에게 {status.DisplayName} {status.FormatApplication(turns, buffPower)} 효과를 부여합니다";
        context.AddIntent(detail);
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
