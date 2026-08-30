using UnityEngine;

[CreateAssetMenu(fileName = "ModifyCostEffect", menuName = "Card/Effect/Modify Cost"
)]
public class ModifyCostEffect : CardEffect
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

        costSystem.ModifyDreamCost(dreamCost);
        costSystem.ModifyNightmareCost(nightmareCost);
    }
}
