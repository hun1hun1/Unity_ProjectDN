using UnityEngine;

[CreateAssetMenu(fileName = "BlockEffect", menuName = "Card/Effect/Block")]
public class BlockEffect : CardEffect
{
    public int block;

    [SerializeField] private EffectAmountMode amountMode;

    public override void Preview(CardPreviewContext context, string path)
    {
        context.RecordBlock(path, block, amountMode);
    }

    public override void Execute(BattleManager battleManager)
    {
        int amount = EffectAmount.Resolve(block, amountMode, battleManager.CurrentSpentAmmo);
        battleManager.GetPlayer().AddBlock(amount);
    }
    public override string GetEffectSummary(System.Collections.Generic.HashSet<CardEffect> visiting)
    {
        return $"자신에게 기본 방어도 {EffectAmount.Describe(block, amountMode)} 획득";
    }
}
