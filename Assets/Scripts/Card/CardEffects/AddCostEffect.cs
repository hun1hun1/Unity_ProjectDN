using UnityEngine;

[CreateAssetMenu(fileName = "AddCostEffect", menuName = "Card/Effect/Add Cost")]
public class AddCostEffect : CardEffect
{
    [SerializeField] private int dreamCost;
    [SerializeField] private int nightmareCost;

    public override void Execute(BattleManager battleManager)
    {
        Player player = battleManager.GetPlayer();

        if (player == null)
            return;

        CostSystem costSystem = player.GetCostSystem();

        if (costSystem == null)
            return;

        costSystem.AddDreamCost(dreamCost);
        costSystem.AddNightmareCost(nightmareCost);
    }
    public override string GetEffectSummary(System.Collections.Generic.HashSet<CardEffect> visiting)
    {
        return $"꿈 코스트 {dreamCost}, 악몽 코스트 {nightmareCost} 획득";
    }
}
