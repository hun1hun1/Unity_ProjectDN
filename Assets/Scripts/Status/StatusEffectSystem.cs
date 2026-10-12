using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class ActiveStatusEffect
{
    public StatusEffectType Type { get; }
    public StatusEffectData Definition { get; internal set; }
    public int RemainingTurns { get; internal set; }

    internal ActiveStatusEffect(StatusEffectData definition, int turns)
    {
        Type = definition.Type;
        Definition = definition;
        RemainingTurns = turns;
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

    public bool Apply(StatusEffectData definition, int turns)
    {
        if (definition == null || turns <= 0 || !Enum.IsDefined(typeof(StatusEffectType), definition.Type))
            return false;
        foreach (ActiveStatusEffect effect in active)
        {
            if (effect.Type != definition.Type) continue;
            int duration = (int)Math.Min(int.MaxValue, (long)effect.RemainingTurns + turns);
            bool changed = effect.RemainingTurns != duration || effect.Definition != definition;
            effect.RemainingTurns = duration;
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
            active[i].RemainingTurns--;
            if (active[i].RemainingTurns <= 0) active.RemoveAt(i);
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
