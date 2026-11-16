using UnityEngine;

public enum ConduitRarity { Common, Uncommon, Rare }

[CreateAssetMenu(fileName = "AmmoCapacityRelic", menuName = "Conduit/Ammo Capacity")]
public class AmmoCapacityRelic : ScriptableObject
{
    [SerializeField] private string relicName;
    [TextArea] [SerializeField] private string description;
    [SerializeField] private Sprite artwork;
    [SerializeField] private ConduitRarity rarity;
    [Min(1)] [SerializeField] private int maxAmmoBonus = 1;

    public string RelicName => relicName;
    public string Description => description;
    public Sprite Artwork => artwork;
    public ConduitRarity Rarity => rarity;
    public int MaxAmmoBonus => Mathf.Max(0, maxAmmoBonus);

    public bool Acquire()
    {
        return RunRelicInventory.Acquire(this);
    }
}
