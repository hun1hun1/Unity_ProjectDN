using System;
using System.Collections.Generic;
using UnityEngine;

// Runtime state belongs to the current game, never to a shared relic asset.
public static class RunRelicInventory
{
    private readonly struct RelicEffects
    {
        public readonly int Capacity, StartAmmo, BuffPower;
        public readonly StatusEffectData StartBuff;

        public RelicEffects(int capacity, int startAmmo, StatusEffectData startBuff, int buffPower)
        {
            Capacity = capacity;
            StartAmmo = startAmmo;
            StartBuff = startBuff;
            BuffPower = buffPower;
        }

        public RelicEffects(AmmoCapacityRelic relic)
            : this(relic.MaxAmmoBonus, relic.BattleStartAmmo, relic.BattleStartBuff, relic.BattleStartBuffPower) { }

        public RunRelicData Capture(AmmoCapacityRelic relic, RunAssetCatalog catalog) => new RunRelicData
        {
            id = catalog.Key(relic),
            bonus = Capacity,
            battleStartAmmo = StartAmmo,
            battleStartBuff = catalog.Key(StartBuff),
            battleStartBuffPower = BuffPower
        };
    }

    private static readonly Dictionary<AmmoCapacityRelic, RelicEffects> ownedRelics =
        new Dictionary<AmmoCapacityRelic, RelicEffects>();
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
        Add(relic, new RelicEffects(relic));
        Changed?.Invoke();
        return true;
    }

    public static bool Contains(AmmoCapacityRelic relic)
    {
        return relic != null && ownedRelics.ContainsKey(relic);
    }

    internal static RunRelicData CaptureStartingConduit(AmmoCapacityRelic relic, RunAssetCatalog catalog) =>
        new RelicEffects(relic).Capture(relic, catalog);

    internal static void ApplyBattleStartBuffs(Player player)
    {
        foreach (AmmoCapacityRelic relic in acquisitionOrder)
        {
            RelicEffects effect = ownedRelics[relic];
            if (effect.StartBuff != null)
                player.StatusEffects.Apply(effect.StartBuff, 0, effect.BuffPower);
        }
    }

    internal static RunRelicData[] Capture(RunAssetCatalog catalog)
    {
        var result = new RunRelicData[acquisitionOrder.Count];
        for (int i = 0; i < result.Length; i++)
        {
            AmmoCapacityRelic relic = acquisitionOrder[i];
            result[i] = ownedRelics[relic].Capture(relic, catalog);
        }
        return result;
    }
    internal static void Restore(RunRelicData[] data, RunAssetCatalog catalog)
    {
        Clear();
        foreach (RunRelicData entry in data)
            Add(catalog.Resolve<AmmoCapacityRelic>(entry.id), new RelicEffects(entry.bonus, entry.battleStartAmmo,
                catalog.Resolve<StatusEffectData>(entry.battleStartBuff), entry.battleStartBuffPower));
        unchecked { GameVersion++; }
        Changed?.Invoke();
    }
    private static void Add(AmmoCapacityRelic relic, RelicEffects effect)
    {
        ownedRelics.Add(relic, effect);
        acquisitionOrder.Add(relic);
        MaxAmmoBonus = (int)Math.Min(int.MaxValue, (long)MaxAmmoBonus + effect.Capacity);
        BattleStartAmmo = (int)Math.Min(int.MaxValue, (long)BattleStartAmmo + effect.StartAmmo);
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
