using UnityEngine;

public enum ConduitRarity { Common, Uncommon, Rare }

[CreateAssetMenu(fileName = "AmmoCapacityRelic", menuName = "Conduit/Ammo Capacity")]
public class AmmoCapacityRelic : ScriptableObject
{
    [SerializeField] private string relicName;
    [TextArea] [SerializeField] private string description;
    [SerializeField] private Sprite artwork;
    [Tooltip("Optional transparent icon for the battle HUD.")]
    [SerializeField] private Sprite hudArtwork;
    [SerializeField] private ConduitRarity rarity;
    [Min(0)] [SerializeField] private int maxAmmoBonus = 1;
    [Min(0)] [SerializeField] private int battleStartAmmo;
    [Tooltip("Optional buff granted to the player at battle start. It must last until the battle ends.")]
    [SerializeField] private StatusEffectData battleStartBuff;
    [Min(0)] [SerializeField] private int battleStartBuffPower;

    public string RelicName => relicName;
    public string Description => description;
    public Sprite Artwork => artwork;
    public Sprite HudArtwork => hudArtwork != null ? hudArtwork : artwork;
    public ConduitRarity Rarity => rarity;
    public int MaxAmmoBonus => Mathf.Max(0, maxAmmoBonus);
    public int BattleStartAmmo => Mathf.Max(0, battleStartAmmo);
    public StatusEffectData BattleStartBuff =>
        IsValidBattleStartBuff(battleStartBuff, battleStartBuffPower) ? battleStartBuff : null;
    public int BattleStartBuffPower => BattleStartBuff != null ? battleStartBuffPower : 0;
    public bool HasEffect => MaxAmmoBonus > 0 || BattleStartAmmo > 0 || BattleStartBuff != null;

    internal static bool IsValidBattleStartBuff(StatusEffectData status, int power) =>
        status != null && status.Category == StatusEffectCategory.Buff &&
        status.Duration == BuffDuration.UntilBattleEnd && status.CanApply(0, power);

    public bool Acquire()
    {
        return RunRelicInventory.Acquire(this);
    }
}
