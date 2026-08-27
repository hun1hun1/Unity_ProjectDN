using UnityEngine;

[CreateAssetMenu(fileName = "BlockEffect", menuName = "Card/Effect/Block")]
public class BlockEffect : CardEffect
{
    public int block;

    public override void Execute(BattleManager battleManager)
    {
        battleManager.GetPlayer().AddBlock(block);
    }
}
