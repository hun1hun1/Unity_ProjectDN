using UnityEngine;

[CreateAssetMenu(fileName = "DamageEffect", menuName = "Card/Effect/Damage")]
public class DamageEffect : CardEffect
{
    public int damage;

    public override void Execute(BattleManager battleManager)
    {
        battleManager.GetTarget().TakeDamage(damage);
    }
}