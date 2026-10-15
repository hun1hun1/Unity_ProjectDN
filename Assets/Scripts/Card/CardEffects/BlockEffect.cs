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
}
