using UnityEngine;

[CreateAssetMenu(fileName = "ApplyStatusEffect", menuName = "Card/Effect/Apply Status")]
public class ApplyStatusEffect : CardEffect
{
    [SerializeField] private StatusEffectData status;
    [Min(1)] [SerializeField] private int turns = 1;

    public override void Execute(BattleManager battleManager)
    {
        if (battleManager == null) return;
        BattleCharacter character;
        switch (target)
        {
            case CardTarget.Player: character = battleManager.GetPlayer(); break;
            case CardTarget.Enemy: character = battleManager.GetTarget(); break;
            default: return;
        }
        if (character != null) character.StatusEffects.Apply(status, turns);
    }
}
