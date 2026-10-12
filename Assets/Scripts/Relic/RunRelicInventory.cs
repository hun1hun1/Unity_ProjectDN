using System;
using System.Collections.Generic;
using UnityEngine;

// Runtime state belongs to the current game, never to a shared relic asset.
public static class RunRelicInventory
{
    private static readonly HashSet<AmmoCapacityRelic> ownedRelics = new HashSet<AmmoCapacityRelic>();
    public static int Count => ownedRelics.Count;
    public static int MaxAmmoBonus { get; private set; }
    internal static uint GameVersion { get; private set; }
    internal static event Action Changed;

    public static bool Acquire(AmmoCapacityRelic relic)
    {
        if (relic == null || relic.MaxAmmoBonus <= 0 || !ownedRelics.Add(relic)) return false;
        // Capture the bonus on acquisition; editing the asset cannot rewrite run state.
        MaxAmmoBonus = (int)Math.Min(int.MaxValue, (long)MaxAmmoBonus + relic.MaxAmmoBonus);
        Changed?.Invoke();
        return true;
    }

    public static bool Contains(AmmoCapacityRelic relic)
    {
        return relic != null && ownedRelics.Contains(relic);
    }

    public static void ResetForNewGame()
    {
        ownedRelics.Clear();
        MaxAmmoBonus = 0;
        unchecked { GameVersion++; }
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeRuntime()
    {
        // Also reset when entering Play Mode with domain reload disabled.
        ownedRelics.Clear();
        MaxAmmoBonus = 0;
        unchecked { GameVersion++; }
        Changed = null;
    }
}
