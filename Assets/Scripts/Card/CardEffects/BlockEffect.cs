using UnityEngine;

[CreateAssetMenu(fileName = "BlockEffect", menuName = "Card/Effect/Block")]
public class BlockEffect : CardEffect
{
    public int block;

    public override void Preview(CardPreviewContext context, string path)
    {
        context.RecordBlock(path, block);
    }

    public override void Execute(BattleManager battleManager)
    {
        battleManager.GetPlayer().AddBlock(block);
    }
    public override string GetEffectSummary(System.Collections.Generic.HashSet<CardEffect> visiting)
    {
        return $"자신에게 기본 방어도 {block} 획득";
    }
}
