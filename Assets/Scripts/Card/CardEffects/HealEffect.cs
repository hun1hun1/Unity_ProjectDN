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
}
