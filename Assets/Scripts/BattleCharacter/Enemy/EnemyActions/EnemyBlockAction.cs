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

    public override EnemyActionKinds GetActionKinds() => EnemyActionKinds.Block;

    public override string GetActionDescription(BattleManager battleManager) =>
        Preview(new EnemyActionPreviewContext(battleManager));

    internal override string Preview(EnemyActionPreviewContext context)
    {
        int amount = context.Block(block);
        context.AddIntent($"방어도를 {amount} 얻습니다");
        return $"방어 {amount}";
    }

    public override void Execute(BattleManager battleManager)
    {
        battleManager.GetTarget().AddBlock(block);
    }
}
