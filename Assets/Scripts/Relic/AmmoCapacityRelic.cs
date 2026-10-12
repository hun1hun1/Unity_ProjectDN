using UnityEngine;

[CreateAssetMenu(fileName = "AmmoCapacityRelic", menuName = "Relic/Ammo Capacity")]
public class AmmoCapacityRelic : ScriptableObject
{
    [SerializeField] private string relicName;
    [TextArea] [SerializeField] private string description;
    [Min(1)] [SerializeField] private int maxAmmoBonus = 1;

    public string RelicName => relicName;
    public string Description => description;
    public int MaxAmmoBonus => Mathf.Max(0, maxAmmoBonus);

    public bool Acquire()
    {
        return RunRelicInventory.Acquire(this);
    }
}
