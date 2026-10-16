using UnityEngine;

[CreateAssetMenu(fileName = "ReloadAmmoEffect", menuName = "Card/Effect/Reload Ammo")]
public class ReloadAmmoEffect : CardEffect
{
    [Min(0)] [SerializeField] private int amount = 1;

    public override void Preview(CardPreviewContext context, string path)
    {
        context.LoadAmmo(amount);
    }

    public override void Execute(BattleManager battleManager)
    {
        Player player = battleManager != null ? battleManager.GetPlayer() : null;
        AmmoSystem ammoSystem = player != null ? player.GetAmmoSystem() : null;
        if (ammoSystem != null) ammoSystem.Load(amount);
    }
    public override string GetEffectSummary(System.Collections.Generic.HashSet<CardEffect> visiting)
    {
        return $"총알 {amount} 장전";
    }
}
