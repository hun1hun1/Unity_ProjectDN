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
    [Min(0)] [SerializeField] private int turnStartAmmo;
    [Min(0)] [SerializeField] private int battleStartBlock;
    [Min(0)] [SerializeField] private int battleVictoryHeal;
    [Min(0)] [SerializeField] private int restRecoveryBonus;
    [Min(0)] [SerializeField] private int dreamHandSizeBonus;
    [Min(0)] [SerializeField] private int nightmareHandSizeBonus;
    [Min(0)] [SerializeField] private int weaknessExposureBonusTurns;
    [Range(0, 100)] [SerializeField] private int shopDiscountPercent;
    [Min(0)] [SerializeField] private int goldGainBonusPercent;
    [SerializeField] private bool equalizeEliteConduitOdds;
    [SerializeField] private bool startWithHalfDreamGauge;
    [Tooltip("Prevents Rust from being applied to the player.")]
    [SerializeField] private bool rustImmunity;
    [Tooltip("Optional buff granted to the player at battle start. Supports battle-long buffs and Ward.")]
    [SerializeField] private StatusEffectData battleStartBuff;
    [Min(0)] [SerializeField] private int battleStartBuffPower;

    public string RelicName => relicName;
    public string Description => description;
    public Sprite Artwork => artwork;
    public Sprite HudArtwork => hudArtwork != null ? hudArtwork : artwork;
    public ConduitRarity Rarity => rarity;
    public int MaxAmmoBonus => Mathf.Max(0, maxAmmoBonus);
    public int BattleStartAmmo => Mathf.Max(0, battleStartAmmo);
    public int TurnStartAmmo => Mathf.Max(0, turnStartAmmo);
    public int BattleStartBlock => Mathf.Max(0, battleStartBlock);
    public int BattleVictoryHeal => Mathf.Max(0, battleVictoryHeal);
    public int RestRecoveryBonus => Mathf.Max(0, restRecoveryBonus);
    public int DreamHandSizeBonus => Mathf.Max(0, dreamHandSizeBonus);
    public int NightmareHandSizeBonus => Mathf.Max(0, nightmareHandSizeBonus);
    public int WeaknessExposureBonusTurns => Mathf.Max(0, weaknessExposureBonusTurns);
    public int ShopDiscountPercent => Mathf.Clamp(shopDiscountPercent, 0, 100);
    public int GoldGainBonusPercent => Mathf.Max(0, goldGainBonusPercent);
    public bool EqualizeEliteConduitOdds => equalizeEliteConduitOdds;
    public bool StartWithHalfDreamGauge => startWithHalfDreamGauge;
    public bool RustImmunity => rustImmunity;
    public StatusEffectData BattleStartBuff =>
        IsValidBattleStartBuff(battleStartBuff, battleStartBuffPower) ? battleStartBuff : null;
    public int BattleStartBuffPower => BattleStartBuff != null ? battleStartBuffPower : 0;
    public bool HasEffect => MaxAmmoBonus > 0 || BattleStartAmmo > 0 || TurnStartAmmo > 0 ||
        BattleStartBlock > 0 || BattleVictoryHeal > 0 || RestRecoveryBonus > 0 ||
        DreamHandSizeBonus > 0 || NightmareHandSizeBonus > 0 ||
        WeaknessExposureBonusTurns > 0 || ShopDiscountPercent > 0 || GoldGainBonusPercent > 0 ||
        EqualizeEliteConduitOdds || StartWithHalfDreamGauge || RustImmunity || BattleStartBuff != null;

    internal static bool IsValidBattleStartBuff(StatusEffectData status, int power) =>
        status != null && status.Category == StatusEffectCategory.Buff &&
        (status.Duration == BuffDuration.UntilBattleEnd || status.Type == StatusEffectType.Ward) &&
        status.CanApply(0, power);

    public bool Acquire()
    {
        return RunRelicInventory.Acquire(this);
    }
}
