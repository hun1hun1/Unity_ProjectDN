using UnityEngine;

[CreateAssetMenu(fileName = "HealEffect", menuName = "Card/Effect/Heal")]
public class HealEffect : CardEffect
{
    [SerializeField] public int heal;

    public override void Execute(BattleManager battleManager)
    {
        switch (target)
        {
            case CardTarget.Player:
                battleManager.GetPlayer().Heal(heal);
                break;

            case CardTarget.Enemy:
                battleManager.GetTarget().Heal(heal);
                break;
        }
    }
    public override string GetEffectSummary(System.Collections.Generic.HashSet<CardEffect> visiting)
    {
        return $"{(target == CardTarget.Player ? "자신" : "적")}의 HP {heal} 회복";
    }
}
