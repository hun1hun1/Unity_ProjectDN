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

    public string RelicName => relicName;
    public string Description => description;
    public Sprite Artwork => artwork;
    public Sprite HudArtwork => hudArtwork != null ? hudArtwork : artwork;
    public ConduitRarity Rarity => rarity;
    public int MaxAmmoBonus => Mathf.Max(0, maxAmmoBonus);
    public int BattleStartAmmo => Mathf.Max(0, battleStartAmmo);
    public bool HasEffect => MaxAmmoBonus > 0 || BattleStartAmmo > 0;

    public bool Acquire()
    {
        return RunRelicInventory.Acquire(this);
    }
}
