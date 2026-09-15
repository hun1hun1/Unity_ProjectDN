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
        return $"АјАн {damage}";
    }

    public override void Execute(BattleManager battleManager)
    {
        battleManager.GetPlayer().TakeDamage(damage);
    }
}
