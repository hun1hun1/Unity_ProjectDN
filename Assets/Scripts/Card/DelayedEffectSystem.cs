using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// Battle-owned clock and queue. Shared card/effect assets never contain reservations.
public sealed class DelayedEffectSystem
{
    private readonly List<PendingDelayedEffect> pending = new List<PendingDelayedEffect>();
    private readonly ReadOnlyCollection<PendingDelayedEffect> view;
    private long nextId;
    private int generation;
    private bool processing;
    public long CurrentTurn { get; private set; }
    public IReadOnlyList<PendingDelayedEffect> PendingEffects => view;
    public event Action Changed;

    public DelayedEffectSystem() { view = pending.AsReadOnly(); }

    public PendingDelayedEffect Schedule(DelayEffect effect, CardData card, ActiveContinuousCard source = null)
    {
        if (effect == null || !effect.IsValid || source != null && !source.IsActive) return null;
        var entry = new PendingDelayedEffect(this, ++nextId, effect, card, source);
        pending.Add(entry);
        source?.RegisterDelay();
        RefreshSourceTurns();
        Changed?.Invoke();
        return entry;
    }

    public void AdvanceTurn()
    {
        CurrentTurn++;
        RefreshSourceTurns();
        Changed?.Invoke();
    }

    public IEnumerator ProcessDueEffectsRoutine(BattleManager battle)
    {
        if (battle == null || processing) yield break;
        int version = generation;
        processing = true;
        var due = pending.FindAll(entry => entry.DueTurn <= CurrentTurn).ToArray();
        try
        {
            foreach (PendingDelayedEffect entry in due)
            {
                if (version != generation || !battle.CanContinueEffects) yield break;
                if (!pending.Remove(entry)) continue;
                ActiveContinuousCard source = entry.ContinuousSource;
                if (source != null && source.CompleteDelay())
                    battle.GetPlayer()?.ContinuousCards.CompleteDelayedCard(source);
                Changed?.Invoke();
                if (version != generation || !battle.CanContinueEffects) yield break;
                yield return battle.ExecuteEffectsRoutine(entry.EffectSnapshot, sourceCard: entry.Card);
            }
        }
        finally { processing = false; }
    }

    private void RefreshSourceTurns()
    {
        var turns = new Dictionary<ActiveContinuousCard, int>();
        foreach (PendingDelayedEffect entry in pending)
        {
            if (entry.ContinuousSource == null) continue;
            int remaining = (int)Math.Min(int.MaxValue, entry.RemainingTurns);
            if (!turns.TryGetValue(entry.ContinuousSource, out int old) || remaining > old)
                turns[entry.ContinuousSource] = remaining;
        }
        foreach (var pair in turns) pair.Key.SetDelayedTurns(pair.Value);
    }

    public void Clear()
    {
        generation++;
        pending.Clear();
        CurrentTurn = 0;
        Changed?.Invoke();
    }
}
