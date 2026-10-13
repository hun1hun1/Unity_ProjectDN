using UnityEngine;

[CreateAssetMenu(fileName = "DamageEffect", menuName = "Card/Effect/Damage")]
public class DamageEffect : CardEffect
{
    [SerializeField] public int damage;

    public override void Preview(CardPreviewContext context, string path)
    {
        context.RecordDamage(path, damage, target);
    }

    public override void Execute(BattleManager battleManager)
    {
        switch (target)
        {
            case CardTarget.Player:
                battleManager.GetPlayer().TakeDamage(damage, battleManager.GetPlayer());
                break;

            case CardTarget.Enemy:
                battleManager.GetTarget().TakeDamage(damage, battleManager.GetPlayer());
                break;
        }
    }
}
