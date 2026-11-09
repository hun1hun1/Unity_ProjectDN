using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyApplyStatusAction", menuName = "Enemy/Action/Apply Status")]
public class EnemyApplyStatusAction : EnemyAction
{
    [SerializeField] private StatusEffectData status;
    [Tooltip("Both sides tick at player-turn start. Two turns remain active during the next player turn.")]
    [Min(1)] [SerializeField] private int turns = 2;
    [SerializeField] private EnemyTargetType target = EnemyTargetType.Player;

    public override EnemyActionKinds GetActionKinds() => EnemyActionKinds.Debuff;
    internal override void Validate(HashSet<EnemyAction> visiting)
    {
        base.Validate(visiting);
        if (status == null || turns < 1 || !Enum.IsDefined(typeof(StatusEffectType), status.Type) ||
            !Enum.IsDefined(typeof(EnemyTargetType), target))
            throw new InvalidOperationException($"Status action '{name}' needs a valid status, target and positive duration.");
    }
    public override string GetActionDescription()
    {
        Validate();
        return $"{(target == EnemyTargetType.Player ? "플레이어" : "적 자신")}에게 {status.DisplayName} {turns}턴 부여";
    }
    public override string GetActionDescription(BattleManager battle) => Preview(new EnemyActionPreviewContext(battle));
    internal override string Preview(EnemyActionPreviewContext context)
    {
        string text = GetActionDescription();
        context.Apply(status, target, turns);
        return context.SummarizeStatuses ? "디버프" : text;
    }
    public override void Execute(BattleManager battle)
    {
        Validate();
        if (battle == null || !battle.CanContinueEffects) return;
        BattleCharacter recipient = target == EnemyTargetType.Player ? battle.GetPlayer() : battle.GetTarget();
        if (recipient != null) recipient.StatusEffects.Apply(status, turns);
    }
}
