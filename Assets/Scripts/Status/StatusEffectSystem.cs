using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class ActiveStatusEffect
{
    internal int TimedTurns;
    internal readonly List<ActiveContinuousCard> ContinuousSources = new List<ActiveContinuousCard>();
    public StatusEffectType Type { get; }
    public StatusEffectData Definition { get; internal set; }
    public bool IsPermanent
    {
        get
        {
            foreach (ActiveContinuousCard source in ContinuousSources)
                if (source.IsActive && source.IsPermanent) return true;
            return false;
        }
    }
    public bool IsActive
    {
        get
        {
            if (TimedTurns > 0) return true;
            foreach (ActiveContinuousCard source in ContinuousSources)
                if (source.IsActive) return true;
            return false;
        }
    }
    public int RemainingTurns
    {
        get
        {
            int turns = TimedTurns;
            foreach (ActiveContinuousCard source in ContinuousSources)
                if (source.IsActive && !source.IsPermanent) turns = Math.Max(turns, source.RemainingTurns);
            return turns;
        }
    }

    internal ActiveStatusEffect(StatusEffectData definition, int turns)
    {
        Type = definition.Type;
        Definition = definition;
        TimedTurns = turns;
    }
}

// Each character owns its durations. Shared assets contain definitions only.
public sealed class StatusEffectSystem
{
    private readonly List<ActiveStatusEffect> active = new List<ActiveStatusEffect>();
    private readonly ReadOnlyCollection<ActiveStatusEffect> view;
    public IReadOnlyList<ActiveStatusEffect> ActiveEffects => view;
    public event Action Changed;

    public StatusEffectSystem() { view = active.AsReadOnly(); }

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

    // The owning continuous card is the only clock for these contributions.
    public Action ApplyContinuous(StatusEffectData definition, ActiveContinuousCard source)
    {
        if (definition == null || source == null || !source.IsActive ||
            !Enum.IsDefined(typeof(StatusEffectType), definition.Type)) return null;
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
            if (!entry.IsActive) active.Remove(entry);
            Changed?.Invoke();
        };
    }

    public bool Apply(StatusEffectData definition, int turns)
    {
        if (definition == null || turns <= 0 || !Enum.IsDefined(typeof(StatusEffectType), definition.Type))
            return false;
        foreach (ActiveStatusEffect effect in active)
        {
            if (effect.Type != definition.Type) continue;
            int duration = (int)Math.Min(int.MaxValue, (long)effect.TimedTurns + turns);
            bool changed = effect.TimedTurns != duration || effect.Definition != definition;
            effect.TimedTurns = duration;
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
            if (active[i].TimedTurns > 0) active[i].TimedTurns--;
            if (!active[i].IsActive) active.RemoveAt(i);
        }
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (active.Count == 0) return;
        active.Clear();
        Changed?.Invoke();
    }
}
