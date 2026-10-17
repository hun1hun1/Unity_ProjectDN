using UnityEngine;

[CreateAssetMenu(fileName = "DamageEffect", menuName = "Card/Effect/Damage")]
public class DamageEffect : CardEffect
{
    [SerializeField] public int damage;

    [SerializeField] private EffectAmountMode amountMode;

    public override void Preview(CardPreviewContext context, string path)
    {
        context.RecordDamage(path, damage, target, amountMode);
    }

    public override void Execute(BattleManager battleManager)
    {
        int amount = EffectAmount.Resolve(damage, amountMode, battleManager.CurrentSpentAmmo);
        switch (target)
        {
            case CardTarget.Player:
                battleManager.GetPlayer().TakeDamage(amount, battleManager.GetPlayer());
                break;

            case CardTarget.Enemy:
                battleManager.GetTarget().TakeDamage(amount, battleManager.GetPlayer());
                break;
        }
    }
    public override string GetEffectSummary(System.Collections.Generic.HashSet<CardEffect> visiting)
    {
        return $"{(target == CardTarget.Player ? "자신" : "적")}에게 기본 피해 {EffectAmount.Describe(damage, amountMode)}";
    }
}
