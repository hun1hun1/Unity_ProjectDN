using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class ActiveStatusEffect
{
    private sealed class BuffApplication
    {
        internal readonly BuffDuration Duration;
        internal readonly int Power;
        internal int Turns;
        internal bool IsPending => Duration == BuffDuration.NextTurn && Turns > 1;

        internal BuffApplication(BuffDuration duration, int power, int turns)
        {
            Duration = duration;
            Power = power;
            Turns = duration == BuffDuration.NextTurn ? 2 : turns;
        }

        internal bool Tick()
        {
            return (Duration == BuffDuration.FixedTurns || Duration == BuffDuration.NextTurn) && --Turns == 0;
        }
    }

    // The amount is a duration for timed effects and a stack count for Burn.
    internal int Amount;
    private readonly List<BuffApplication> buffs = new List<BuffApplication>();
    internal readonly List<ActiveContinuousCard> ContinuousSources = new List<ActiveContinuousCard>();
    public StatusEffectType Type { get; }
    public StatusEffectData Definition { get; internal set; }
    public int StackCount => Type == StatusEffectType.Burn ? Amount : 0;
    public int BuffPower
    {
        get
        {
            long total = 0;
            foreach (BuffApplication buff in buffs)
                if (!buff.IsPending) total += buff.Power;
            return (int)Math.Min(int.MaxValue, total);
        }
    }
    public bool IsPending => buffs.Exists(buff => buff.IsPending);
    public bool HasNextAttackBuff => buffs.Exists(buff => buff.Duration == BuffDuration.NextAttack);
    public bool HasNextIncomingAttackBuff => buffs.Exists(buff => buff.Duration == BuffDuration.NextIncomingAttack);
    internal bool IsPresent => IsActive || IsPending;
    public bool IsPermanent
    {
        get
        {
            if (buffs.Exists(buff => buff.Duration == BuffDuration.UntilBattleEnd)) return true;
            foreach (ActiveContinuousCard source in ContinuousSources)
                if (source.IsActive && source.IsPermanent) return true;
            return false;
        }
    }
    public bool IsActive
    {
        get
        {
            if (Amount > 0 || buffs.Exists(buff => !buff.IsPending)) return true;
            foreach (ActiveContinuousCard source in ContinuousSources)
                if (source.IsActive) return true;
            return false;
        }
    }
    public int RemainingTurns
    {
        get
        {
            if (Type == StatusEffectType.Burn) return 0;
            int turns = Amount;
            foreach (BuffApplication buff in buffs)
                if (buff.Duration == BuffDuration.FixedTurns || buff.Duration == BuffDuration.NextTurn && !buff.IsPending)
                    turns = Math.Max(turns, buff.Turns);
            foreach (ActiveContinuousCard source in ContinuousSources)
                if (source.IsActive && !source.IsPermanent) turns = Math.Max(turns, source.RemainingTurns);
            return turns;
        }
    }

    internal void AddBuff(BuffDuration duration, int power, int turns)
    {
        if (Type == StatusEffectType.Ward)
        {
            // Ward always holds at most one charge.
            buffs.Clear();
            power = 1;
        }
        buffs.Add(new BuffApplication(duration, power, turns));
    }

    internal void TickBuffs() => buffs.RemoveAll(buff => buff.Tick());

    internal bool ConsumeBuffs(BuffDuration trigger) => buffs.RemoveAll(buff => buff.Duration == trigger) != 0;

    internal ActiveStatusEffect(StatusEffectData definition, int amount)
    {
        Type = definition.Type;
        Definition = definition;
        Amount = amount;
    }
}

// Each character owns its durations and stacks. Shared assets contain definitions only.
public sealed class StatusEffectSystem
{
    private readonly List<ActiveStatusEffect> active = new List<ActiveStatusEffect>();
    private readonly ReadOnlyCollection<ActiveStatusEffect> view;
    private bool rustImmunity;
    public IReadOnlyList<ActiveStatusEffect> ActiveEffects => view;
    public event Action Changed;

    public StatusEffectSystem() { view = active.AsReadOnly(); }

    internal bool IsImmune(StatusEffectType type) => type == StatusEffectType.Rust && rustImmunity;

    internal void SetRustImmunity(bool immune)
    {
        if (rustImmunity == immune) return;
        rustImmunity = immune;
        if (immune) active.RemoveAll(effect => effect.Type == StatusEffectType.Rust);
        Changed?.Invoke();
    }

    public int GetRemainingTurns(StatusEffectType type)
    {
        foreach (ActiveStatusEffect effect in active)
            if (effect.Type == type) return effect.RemainingTurns;
        return 0;
    }

    public bool HasStatus(StatusEffectType type)
    {
        foreach (ActiveStatusEffect effect in active)
            if (effect.Type == type && effect.IsActive) return true;
        return false;
    }

    public int GetBuffPower(StatusEffectType type) => active.Find(effect => effect.Type == type)?.BuffPower ?? 0;

    // The owning continuous card is the only clock for these contributions.
    public Action ApplyContinuous(StatusEffectData definition, ActiveContinuousCard source)
    {
        if (definition == null || definition.Category != StatusEffectCategory.Debuff ||
            definition.Type == StatusEffectType.Burn || IsImmune(definition.Type) || source == null || !source.IsActive ||
            !definition.CanApply(1, 1)) return null;
        ActiveStatusEffect entry = active.Find(effect => effect.Type == definition.Type);
        if (entry == null)
        {
            entry = new ActiveStatusEffect(definition, 0);
            active.Add(entry);
        }
        entry.Definition = definition;
        entry.ContinuousSources.Add(source);
        Action refreshDuration = () => { if (active.Contains(entry)) Changed?.Invoke(); };
        source.DurationChanged += refreshDuration;
        Changed?.Invoke();
        bool released = false;
        return () =>
        {
            if (released) return;
            released = true;
            source.DurationChanged -= refreshDuration;
            entry.ContinuousSources.Remove(source);
            if (!active.Contains(entry)) return;
            if (!entry.IsPresent) active.Remove(entry);
            Changed?.Invoke();
        };
    }

    // For Burn, turns is the number of stacks to add.
    public bool Apply(StatusEffectData definition, int turns, int buffPower = 1)
    {
        if (definition == null || IsImmune(definition.Type) || !definition.CanApply(turns, buffPower))
            return false;
        if (definition.Category == StatusEffectCategory.Buff)
        {
            ActiveStatusEffect entry = active.Find(effect => effect.Type == definition.Type);
            if (entry == null)
            {
                entry = new ActiveStatusEffect(definition, 0);
                active.Add(entry);
            }
            entry.Definition = definition;
            entry.AddBuff(definition.Duration, buffPower, turns);
            Changed?.Invoke();
            return true;
        }
        foreach (ActiveStatusEffect effect in active)
        {
            if (effect.Type != definition.Type) continue;
            int total = (int)Math.Min(int.MaxValue, (long)effect.Amount + turns);
            bool changed = effect.Amount != total || effect.Definition != definition;
            effect.Amount = total;
            effect.Definition = definition;
            if (changed) Changed?.Invoke();
            return true;
        }
        active.Add(new ActiveStatusEffect(definition, turns));
        Changed?.Invoke();
        return true;
    }

    public void ReduceRemainingTurns()
    {
        if (active.Count == 0) return;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i].Type == StatusEffectType.Burn) continue;
            if (active[i].Amount > 0) active[i].Amount--;
            active[i].TickBuffs();
            if (!active[i].IsPresent) active.RemoveAt(i);
        }
        Changed?.Invoke();
    }

    internal void ConsumeTriggeredBuffs(BuffDuration trigger)
    {
        bool changed = false;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (!active[i].ConsumeBuffs(trigger)) continue;
            changed = true;
            if (!active[i].IsPresent) active.RemoveAt(i);
        }
        if (changed) Changed?.Invoke();
    }

    internal void ProcessPlayerTurnEnd(BattleCharacter owner)
    {
        ActiveStatusEffect burn = active.Find(effect => effect.Type == StatusEffectType.Burn);
        if (burn == null) return;

        owner.TakeFixedDamage(burn.StackCount);
        // Damage can end the battle and notify listeners before we resume.
        if (!active.Contains(burn)) return;

        burn.Amount--;
        if (burn.Amount == 0) active.Remove(burn);
        Changed?.Invoke();
    }

    internal void ClearBuffs()
    {
        if (active.RemoveAll(effect => effect.Definition.Category == StatusEffectCategory.Buff) > 0)
            Changed?.Invoke();
    }

    public void Clear()
    {
        if (active.Count == 0) return;
        active.Clear();
        Changed?.Invoke();
    }
}
