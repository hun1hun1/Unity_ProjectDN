using UnityEngine;

[CreateAssetMenu(fileName = "EnemyAttackAction",
                 menuName = "Enemy/Action/Block")]
public class EnemyBlockAction : EnemyAction
{
    [SerializeField]
    private int block;

    public int GetBlock()
    {
        return block;
    }

    public override string GetActionDescription()
    {
        return $"방어 {block}";
    }

    public override string GetActionDescription(BattleManager battleManager)
    {
        if (battleManager == null) return GetActionDescription();
        return $"방어 {BlockCalculator.Calculate(block, battleManager.GetTarget())}";
    }

    public override void Execute(BattleManager battleManager)
    {
        battleManager.GetTarget().AddBlock(block);
    }
}
