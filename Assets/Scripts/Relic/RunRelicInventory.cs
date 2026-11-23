using System;
using System.Collections.Generic;
using UnityEngine;

// Runtime state belongs to the current game, never to a shared relic asset.
public static class RunRelicInventory
{
    private readonly struct RelicEffects
    {
        public readonly int Capacity, StartAmmo, TurnStartAmmo, StartBlock, VictoryHeal, BuffPower;
        public readonly int DreamHandSizeBonus, NightmareHandSizeBonus, WeaknessExposureBonusTurns, ShopDiscountPercent;
        public readonly int GoldGainBonusPercent, RestRecoveryBonus;
        public readonly bool EqualizeEliteConduitOdds, StartWithHalfDreamGauge, RustImmunity;
        public readonly StatusEffectData StartBuff;

        public RelicEffects(int capacity, int startAmmo, int turnStartAmmo, int startBlock, int victoryHeal, int restRecoveryBonus,
            int dreamHandSizeBonus, int nightmareHandSizeBonus, int weaknessExposureBonusTurns,
            int shopDiscountPercent, int goldGainBonusPercent, bool equalizeEliteConduitOdds,
            bool startWithHalfDreamGauge, bool rustImmunity,
            StatusEffectData startBuff, int buffPower)
        {
            Capacity = capacity;
            StartAmmo = startAmmo;
            TurnStartAmmo = turnStartAmmo;
            StartBlock = startBlock;
            VictoryHeal = victoryHeal;
            RestRecoveryBonus = restRecoveryBonus;
            DreamHandSizeBonus = dreamHandSizeBonus;
            NightmareHandSizeBonus = nightmareHandSizeBonus;
            WeaknessExposureBonusTurns = weaknessExposureBonusTurns;
            ShopDiscountPercent = shopDiscountPercent;
            GoldGainBonusPercent = goldGainBonusPercent;
            EqualizeEliteConduitOdds = equalizeEliteConduitOdds;
            StartWithHalfDreamGauge = startWithHalfDreamGauge;
            RustImmunity = rustImmunity;
            StartBuff = startBuff;
            BuffPower = buffPower;
        }

        public RelicEffects(ConduitData relic)
            : this(relic.MaxAmmoBonus, relic.BattleStartAmmo, relic.TurnStartAmmo,
                relic.BattleStartBlock, relic.BattleVictoryHeal, relic.RestRecoveryBonus,
                relic.DreamHandSizeBonus, relic.NightmareHandSizeBonus,
                relic.WeaknessExposureBonusTurns, relic.ShopDiscountPercent, relic.GoldGainBonusPercent,
                relic.EqualizeEliteConduitOdds, relic.StartWithHalfDreamGauge, relic.RustImmunity,
                relic.BattleStartBuff, relic.BattleStartBuffPower) { }

        public RunRelicData Capture(ConduitData relic, RunAssetCatalog catalog) => new RunRelicData
        {
            id = catalog.Key(relic),
            bonus = Capacity,
            battleStartAmmo = StartAmmo,
            turnStartAmmo = TurnStartAmmo,
            battleStartBlock = StartBlock,
            battleVictoryHeal = VictoryHeal,
            restRecoveryBonus = RestRecoveryBonus,
            dreamHandSizeBonus = DreamHandSizeBonus,
            nightmareHandSizeBonus = NightmareHandSizeBonus,
            weaknessExposureBonusTurns = WeaknessExposureBonusTurns,
            shopDiscountPercent = ShopDiscountPercent,
            goldGainBonusPercent = GoldGainBonusPercent,
            equalizeEliteConduitOdds = EqualizeEliteConduitOdds,
            startWithHalfDreamGauge = StartWithHalfDreamGauge,
            rustImmunity = RustImmunity,
            battleStartBuff = catalog.Key(StartBuff),
            battleStartBuffPower = BuffPower
        };
    }

    private static readonly Dictionary<ConduitData, RelicEffects> ownedRelics =
        new Dictionary<ConduitData, RelicEffects>();
    private static readonly List<ConduitData> acquisitionOrder = new List<ConduitData>();
    internal static IReadOnlyList<ConduitData> InAcquisitionOrder { get; } = acquisitionOrder.AsReadOnly();
    public static int Count => ownedRelics.Count;
    public static int MaxAmmoBonus { get; private set; }
    internal static int BattleStartAmmo { get; private set; }
    internal static int TurnStartAmmo { get; private set; }
    internal static int BattleStartBlock { get; private set; }
    internal static int BattleVictoryHeal { get; private set; }
    internal static int RestRecoveryBonus { get; private set; }
    internal static int DreamHandSizeBonus { get; private set; }
    internal static int NightmareHandSizeBonus { get; private set; }
    internal static int WeaknessExposureBonusTurns { get; private set; }
    internal static int ShopDiscountPercent { get; private set; }
    internal static int GoldGainBonusPercent { get; private set; }
    internal static bool EqualizeEliteConduitOdds { get; private set; }
    internal static bool StartWithHalfDreamGauge { get; private set; }
    internal static bool RustImmunity { get; private set; }
    internal static uint GameVersion { get; private set; }
    internal static event Action Changed;

    public static bool Acquire(ConduitData relic)
    {
        if (relic == null || !relic.HasEffect || ownedRelics.ContainsKey(relic)) return false;
        // Capture the values on acquisition so editing the asset cannot rewrite run state.
        Add(relic, new RelicEffects(relic));
        Changed?.Invoke();
        return true;
    }

    public static bool Contains(ConduitData relic)
    {
        return relic != null && ownedRelics.ContainsKey(relic);
    }

    internal static RunRelicData CaptureStartingConduit(ConduitData relic, RunAssetCatalog catalog) =>
        new RelicEffects(relic).Capture(relic, catalog);

    internal static void ApplyBattleStartBuffs(Player player)
    {
        foreach (ConduitData relic in acquisitionOrder)
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
            ConduitData relic = acquisitionOrder[i];
            result[i] = ownedRelics[relic].Capture(relic, catalog);
        }
        return result;
    }
    internal static void Restore(RunRelicData[] data, RunAssetCatalog catalog)
    {
        Clear();
        foreach (RunRelicData entry in data)
            Add(catalog.Resolve<ConduitData>(entry.id), new RelicEffects(entry.bonus, entry.battleStartAmmo,
                entry.turnStartAmmo, entry.battleStartBlock, entry.battleVictoryHeal, entry.restRecoveryBonus,
                entry.dreamHandSizeBonus, entry.nightmareHandSizeBonus,
                entry.weaknessExposureBonusTurns, entry.shopDiscountPercent, entry.goldGainBonusPercent,
                entry.equalizeEliteConduitOdds, entry.startWithHalfDreamGauge, entry.rustImmunity,
                catalog.Resolve<StatusEffectData>(entry.battleStartBuff), entry.battleStartBuffPower));
        unchecked { GameVersion++; }
        Changed?.Invoke();
    }
    private static void Add(ConduitData relic, RelicEffects effect)
    {
        ownedRelics.Add(relic, effect);
        acquisitionOrder.Add(relic);
        MaxAmmoBonus = (int)Math.Min(int.MaxValue, (long)MaxAmmoBonus + effect.Capacity);
        BattleStartAmmo = (int)Math.Min(int.MaxValue, (long)BattleStartAmmo + effect.StartAmmo);
        TurnStartAmmo = (int)Math.Min(int.MaxValue, (long)TurnStartAmmo + effect.TurnStartAmmo);
        BattleStartBlock = (int)Math.Min(int.MaxValue, (long)BattleStartBlock + effect.StartBlock);
        BattleVictoryHeal = (int)Math.Min(int.MaxValue, (long)BattleVictoryHeal + effect.VictoryHeal);
        RestRecoveryBonus = (int)Math.Min(int.MaxValue, (long)RestRecoveryBonus + effect.RestRecoveryBonus);
        DreamHandSizeBonus = (int)Math.Min(int.MaxValue, (long)DreamHandSizeBonus + effect.DreamHandSizeBonus);
        NightmareHandSizeBonus = (int)Math.Min(int.MaxValue, (long)NightmareHandSizeBonus + effect.NightmareHandSizeBonus);
        WeaknessExposureBonusTurns = (int)Math.Min(int.MaxValue,
            (long)WeaknessExposureBonusTurns + effect.WeaknessExposureBonusTurns);
        ShopDiscountPercent = Math.Min(100, ShopDiscountPercent + effect.ShopDiscountPercent);
        GoldGainBonusPercent = (int)Math.Min(int.MaxValue, (long)GoldGainBonusPercent + effect.GoldGainBonusPercent);
        EqualizeEliteConduitOdds |= effect.EqualizeEliteConduitOdds;
        StartWithHalfDreamGauge |= effect.StartWithHalfDreamGauge;
        RustImmunity |= effect.RustImmunity;
    }

    private static void Clear()
    {
        ownedRelics.Clear();
        acquisitionOrder.Clear();
        MaxAmmoBonus = 0;
        BattleStartAmmo = 0;
        TurnStartAmmo = 0;
        BattleStartBlock = 0;
        BattleVictoryHeal = 0;
        RestRecoveryBonus = 0;
        DreamHandSizeBonus = 0;
        NightmareHandSizeBonus = 0;
        WeaknessExposureBonusTurns = 0;
        ShopDiscountPercent = 0;
        GoldGainBonusPercent = 0;
        EqualizeEliteConduitOdds = false;
        StartWithHalfDreamGauge = false;
        RustImmunity = false;
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
