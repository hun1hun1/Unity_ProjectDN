using UnityEngine;

[CreateAssetMenu(fileName = "EnemyAttackAction",
                 menuName = "Enemy/Action/Attack")]
public class EnemyAttackAction : EnemyAction
{
    [SerializeField]
    private int damage;

    public int GetDamage()
    {
        return damage;
    }

    public override string GetActionDescription()
    {
        return $"공격 {damage}";
    }

    public override EnemyActionKinds GetActionKinds() => EnemyActionKinds.Attack;

    public override string GetActionDescription(BattleManager battleManager) =>
        Preview(new EnemyActionPreviewContext(battleManager));

    internal override string Preview(EnemyActionPreviewContext context)
    {
        int amount = context.Attack(damage);
        context.AddIntent($"플레이어에게 {amount}의 피해를 줍니다", amount);
        return $"공격 {amount}";
    }

    public override void Execute(BattleManager battleManager)
    {
        battleManager.GetPlayer().TakeDamage(damage, battleManager.GetTarget());
    }
}
