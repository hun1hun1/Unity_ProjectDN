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

    public override string GetActionDescription(BattleManager battleManager)
    {
        int amount = DamageCalculator.Calculate(damage,
            battleManager != null ? battleManager.GetTarget() : null,
            battleManager != null ? battleManager.GetPlayer() : null);
        return $"공격 {amount}";
    }

    public override void Execute(BattleManager battleManager)
    {
        battleManager.GetPlayer().TakeDamage(damage, battleManager.GetTarget());
    }
}
