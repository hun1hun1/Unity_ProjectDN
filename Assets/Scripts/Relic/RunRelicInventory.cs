using System;
using System.Collections.Generic;
using UnityEngine;

// Runtime state belongs to the current game, never to a shared relic asset.
public static class RunRelicInventory
{
    private static readonly Dictionary<AmmoCapacityRelic, (int Capacity, int StartAmmo)> ownedRelics =
        new Dictionary<AmmoCapacityRelic, (int Capacity, int StartAmmo)>();
    private static readonly List<AmmoCapacityRelic> acquisitionOrder = new List<AmmoCapacityRelic>();
    internal static IReadOnlyList<AmmoCapacityRelic> InAcquisitionOrder { get; } = acquisitionOrder.AsReadOnly();
    public static int Count => ownedRelics.Count;
    public static int MaxAmmoBonus { get; private set; }
    internal static int BattleStartAmmo { get; private set; }
    internal static uint GameVersion { get; private set; }
    internal static event Action Changed;

    public static bool Acquire(AmmoCapacityRelic relic)
    {
        if (relic == null || !relic.HasEffect || ownedRelics.ContainsKey(relic)) return false;
        // Capture the values on acquisition so editing the asset cannot rewrite run state.
        Add(relic, relic.MaxAmmoBonus, relic.BattleStartAmmo);
        Changed?.Invoke();
        return true;
    }

    public static bool Contains(AmmoCapacityRelic relic)
    {
        return relic != null && ownedRelics.ContainsKey(relic);
    }

    internal static RunRelicData[] Capture(RunAssetCatalog catalog)
    {
        var result = new RunRelicData[acquisitionOrder.Count];
        for (int i = 0; i < result.Length; i++)
        {
            AmmoCapacityRelic relic = acquisitionOrder[i];
            var effect = ownedRelics[relic];
            result[i] = new RunRelicData
            {
                id = catalog.Key(relic),
                bonus = effect.Capacity,
                battleStartAmmo = effect.StartAmmo
            };
        }
        return result;
    }
    internal static void Restore(RunRelicData[] data, RunAssetCatalog catalog)
    {
        Clear();
        foreach (RunRelicData entry in data)
            Add(catalog.Resolve<AmmoCapacityRelic>(entry.id), entry.bonus, entry.battleStartAmmo);
        unchecked { GameVersion++; }
        Changed?.Invoke();
    }
    private static void Add(AmmoCapacityRelic relic, int capacity, int startAmmo)
    {
        ownedRelics.Add(relic, (capacity, startAmmo));
        acquisitionOrder.Add(relic);
        MaxAmmoBonus = (int)Math.Min(int.MaxValue, (long)MaxAmmoBonus + capacity);
        BattleStartAmmo = (int)Math.Min(int.MaxValue, (long)BattleStartAmmo + startAmmo);
    }

    private static void Clear()
    {
        ownedRelics.Clear();
        acquisitionOrder.Clear();
        MaxAmmoBonus = 0;
        BattleStartAmmo = 0;
    }

    public static void ResetForNewGame()
    {
        Clear();
        unchecked { GameVersion++; }
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeRuntime()
    {
        // Also reset when entering Play Mode with domain reload disabled.
        Clear();
        unchecked { GameVersion++; }
        Changed = null;
    }
}
