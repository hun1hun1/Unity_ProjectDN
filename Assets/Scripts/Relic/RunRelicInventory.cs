using System;
using System.Collections.Generic;
using UnityEngine;

// Runtime state belongs to the current game, never to a shared relic asset.
public static class RunRelicInventory
{
    private static readonly HashSet<AmmoCapacityRelic> ownedRelics = new HashSet<AmmoCapacityRelic>();
    private static readonly Dictionary<AmmoCapacityRelic,int> capturedBonuses = new Dictionary<AmmoCapacityRelic,int>();
    public static int Count => ownedRelics.Count;
    public static int MaxAmmoBonus { get; private set; }
    internal static uint GameVersion { get; private set; }
    internal static event Action Changed;

    public static bool Acquire(AmmoCapacityRelic relic)
    {
        if (relic == null || relic.MaxAmmoBonus <= 0 || !ownedRelics.Add(relic)) return false;
        // Capture the bonus on acquisition; editing the asset cannot rewrite run state.
        capturedBonuses.Add(relic, relic.MaxAmmoBonus);
        MaxAmmoBonus = (int)Math.Min(int.MaxValue, (long)MaxAmmoBonus + relic.MaxAmmoBonus);
        Changed?.Invoke();
        return true;
    }

    public static bool Contains(AmmoCapacityRelic relic)
    {
        return relic != null && ownedRelics.Contains(relic);
    }

    internal static RunRelicData[] Capture(RunAssetCatalog catalog)
    {
        var result = new List<RunRelicData>();
        foreach (var item in capturedBonuses) result.Add(new RunRelicData { id=catalog.Key(item.Key), bonus=item.Value });
        return result.ToArray();
    }
    internal static void Restore(RunRelicData[] data, RunAssetCatalog catalog)
    {
        ownedRelics.Clear();capturedBonuses.Clear();MaxAmmoBonus=0;
        foreach (RunRelicData entry in data)
        {
            var relic=catalog.Resolve<AmmoCapacityRelic>(entry.id);
            ownedRelics.Add(relic);capturedBonuses.Add(relic,entry.bonus);MaxAmmoBonus+=entry.bonus;
        }
        unchecked { GameVersion++; }
        Changed?.Invoke();
    }
    public static void ResetForNewGame()
    {
        ownedRelics.Clear();capturedBonuses.Clear();
        MaxAmmoBonus = 0;
        unchecked { GameVersion++; }
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeRuntime()
    {
        // Also reset when entering Play Mode with domain reload disabled.
        ownedRelics.Clear();capturedBonuses.Clear();
        MaxAmmoBonus = 0;
        unchecked { GameVersion++; }
        Changed = null;
    }
}
