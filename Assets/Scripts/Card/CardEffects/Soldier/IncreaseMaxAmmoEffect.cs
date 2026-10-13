using UnityEngine;

[CreateAssetMenu(fileName = "IncreaseMaxAmmoEffect", menuName = "Card/Effect/Increase Max Ammo")]
public class IncreaseMaxAmmoEffect : CardEffect
{
    [Min(0)] [SerializeField] private int amount = 1;

    public override void Preview(CardPreviewContext context, string path)
    {
        context.IncreaseMaxAmmo(amount);
    }

    public override void Execute(BattleManager battleManager)
    {
        Player player = battleManager != null ? battleManager.GetPlayer() : null;
        AmmoSystem ammo = player != null ? player.GetAmmoSystem() : null;
        if (ammo != null) ammo.IncreaseMaxForBattle(amount);
    }
}
