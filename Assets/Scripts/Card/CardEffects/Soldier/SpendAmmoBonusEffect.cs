using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "SpendAmmoBonusEffect", menuName = "Card/Effect/Spend Ammo Bonus")]
public class SpendAmmoBonusEffect : CardEffect
{
    [Min(1)] [SerializeField] private int ammoCost = 1;
    [SerializeField] private CardEffect[] bonusEffects;

    public override void CollectReferencedStatuses(CardStatusCollector collector)
    {
        collector.CollectEffects(bonusEffects);
    }

    public override void Preview(CardPreviewContext context, string path)
    {
        context.PreviewAmmoBonus(path, ammoCost, bonusEffects);
    }

    public override System.Action ActivateContinuous(BattleManager battle, ActiveContinuousCard source)
    {
        if (battle != null) battle.RunEffectRoutine(ActivateContinuousRoutine(battle, source));
        return null;
    }

    public override IEnumerator ActivateContinuousRoutine(BattleManager battle, ActiveContinuousCard source)
    {
        if (source == null || !TrySpend(battle)) return null;
        return battle.ExecuteEffectsRoutine(bonusEffects, source);
    }

    public override void Execute(BattleManager battle)
    {
        if (battle != null) battle.RunEffectRoutine(ExecuteRoutine(battle));
    }

    public override IEnumerator ExecuteRoutine(BattleManager battle)
    {
        return TrySpend(battle) ? battle.ExecuteEffectsRoutine(bonusEffects) : null;
    }

    private bool TrySpend(BattleManager battle)
    {
        Player player = battle != null ? battle.GetPlayer() : null;
        AmmoSystem ammo = player != null ? player.GetAmmoSystem() : null;
        return ammoCost > 0 && ammo != null && ammo.TrySpend(ammoCost);
    }
}
