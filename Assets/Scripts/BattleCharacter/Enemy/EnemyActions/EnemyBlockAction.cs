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
        return $"¹æ¾î {block}";
    }

    public override void Execute(BattleManager battleManager)
    {
        battleManager.GetTarget().AddBlock(block);
    }
}
