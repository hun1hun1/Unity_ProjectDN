using UnityEngine;

[CreateAssetMenu(fileName = "EnemyActionIcons", menuName = "Enemy/Action Icon Set")]
public class EnemyActionIconSet : ScriptableObject
{
    [SerializeField] private Sprite attack;
    [SerializeField] private Sprite block;
    [SerializeField] private Sprite debuff;
    [SerializeField] private Sprite attackBlock;
    [SerializeField] private Sprite attackDebuff;
    [SerializeField] private Sprite blockDebuff;
    [SerializeField] private Sprite attackBlockDebuff;

    public Sprite GetIcon(EnemyActionKinds kinds)
    {
        // Existing attack/block combinations remain available for actions that also grant a buff.
        kinds &= ~EnemyActionKinds.Buff;
        switch (kinds)
        {
            case EnemyActionKinds.Attack: return attack;
            case EnemyActionKinds.Block: return block;
            case EnemyActionKinds.Debuff: return debuff;
            case EnemyActionKinds.Attack | EnemyActionKinds.Block: return attackBlock;
            case EnemyActionKinds.Attack | EnemyActionKinds.Debuff: return attackDebuff;
            case EnemyActionKinds.Block | EnemyActionKinds.Debuff: return blockDebuff;
            case EnemyActionKinds.Attack | EnemyActionKinds.Block | EnemyActionKinds.Debuff: return attackBlockDebuff;
            default: return null;
        }
    }
}
