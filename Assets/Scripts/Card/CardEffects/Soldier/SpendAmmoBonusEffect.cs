using UnityEngine;

[CreateAssetMenu(fileName = "SpendAmmoBonusEffect", menuName = "Card/Effect/Spend Ammo Bonus")]
public class SpendAmmoBonusEffect : CardEffect
{
    [Min(1)] [SerializeField] private int ammoCost = 1;
    [SerializeField] private CardEffect[] bonusEffects;

    public override void Execute(BattleManager battleManager)
    {
        Player player = battleManager != null ? battleManager.GetPlayer() : null;
        AmmoSystem ammoSystem = player != null ? player.GetAmmoSystem() : null;
        if (ammoCost <= 0 || ammoSystem == null || !ammoSystem.TrySpend(ammoCost))
            return;

        if (bonusEffects == null) return;
        foreach (CardEffect effect in bonusEffects)
            if (effect != null) effect.Execute(battleManager);
    }
}
