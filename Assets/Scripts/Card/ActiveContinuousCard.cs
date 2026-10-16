using System;
using System.Collections;
using System.Collections.Generic;

public enum ContinuousDurationType
{
    UntilBattleEnd,
    FixedTurns
}

// One record per use, even when several cards share the same CardData asset.
public sealed class ActiveContinuousCard
{
    private readonly List<Action> releases = new List<Action>();
    private readonly CardEffect[] turnStartEffects;
    public bool HasTurnStartEffects { get; }
    internal DeckController Deck { get; }
    public long Id { get; }
    public CardData Card { get; }
    public CardDeckType DeckType { get; }
    private readonly bool permanent;
    private int pendingDelays;
    public bool UsesDelayedLifetime { get; private set; }
    public bool HasPendingDelays => pendingDelays > 0;
    public bool HasMaintainedEffects => releases.Count > 0;
    public bool ExhaustsOnUse => Card.exhaustOnUse && (UsesDelayedLifetime || Card.ExhaustsOnUse);
    public bool IsPermanent => permanent && !UsesDelayedLifetime;
    public bool IsAbsorbed => !ExhaustsOnUse;
    public bool IsActive { get; internal set; } = true;
    public int RemainingTurns { get; internal set; }
    internal event Action DurationChanged;

    internal ActiveContinuousCard(long id, CardData card, DeckController deck)
    {
        Id = id;
        Card = card;
        Deck = deck;
        turnStartEffects = card.turnStartEffects != null ? (CardEffect[])card.turnStartEffects.Clone() : new CardEffect[0];
        foreach (CardEffect effect in turnStartEffects)
            if (effect != null) { HasTurnStartEffects = true; break; }
        DeckType = card.deckType;
        permanent = card.continuousDurationType == ContinuousDurationType.UntilBattleEnd;
        RemainingTurns = IsPermanent ? 0 : card.continuousTurns;
    }

    internal IEnumerator ExecuteTurnStartEffectsRoutine(BattleManager battle)
    {
        yield return battle.ExecuteEffectsRoutine(turnStartEffects, sourceCard: Card);
    }

    internal void RegisterDelay() { pendingDelays++; UsesDelayedLifetime = true; }
    internal bool CompleteDelay() { pendingDelays = Math.Max(0, pendingDelays - 1); return pendingDelays == 0; }
    internal void SetDelayedTurns(int turns) { RemainingTurns = turns; NotifyDurationChanged(); }

    internal void AddRelease(Action release)
    {
        if (release != null) releases.Add(release);
    }

    internal void NotifyDurationChanged() => DurationChanged?.Invoke();

    internal void ReleaseEffects()
    {
        IsActive = false;
        for (int i = releases.Count - 1; i >= 0; i--)
        {
            try { releases[i](); }
            catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
        }
        releases.Clear();
    }
}
