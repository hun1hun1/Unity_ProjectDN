using UnityEngine;

[CreateAssetMenu(fileName = "IncreaseMaxAmmoEffect", menuName = "Card/Effect/Increase Max Ammo")]
public class IncreaseMaxAmmoEffect : CardEffect
{
    [Min(0)] [SerializeField] private int amount = 1;

    public override void Preview(CardPreviewContext context, string path)
    {
        context.IncreaseMaxAmmo(amount);
    }

    public override System.Action ActivateContinuous(BattleManager battleManager, ActiveContinuousCard source)
    {
        Player player = battleManager != null ? battleManager.GetPlayer() : null;
        AmmoSystem ammo = player != null ? player.GetAmmoSystem() : null;
        return ammo != null ? ammo.AddContinuousMaxForBattle(amount) : null;
    }

    public override void Execute(BattleManager battleManager)
    {
        Player player = battleManager != null ? battleManager.GetPlayer() : null;
        AmmoSystem ammo = player != null ? player.GetAmmoSystem() : null;
        if (ammo != null) ammo.IncreaseMaxForBattle(amount);
    }
    public override string GetEffectSummary(System.Collections.Generic.HashSet<CardEffect> visiting)
    {
        return $"이번 전투 최대 탄약 {amount} 증가";
    }
}
