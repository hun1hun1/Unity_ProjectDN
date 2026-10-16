using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// Battle-local card lifetimes. CardData and effect assets never hold runtime state.
public sealed class ContinuousCardSystem
{
    private readonly List<ActiveContinuousCard> active = new List<ActiveContinuousCard>();
    private readonly ReadOnlyCollection<ActiveContinuousCard> view;
    private long nextId;
    public IReadOnlyList<ActiveContinuousCard> ActiveCards => view;
    public event Action Changed;

    public ContinuousCardSystem() { view = active.AsReadOnly(); }

    public static bool CanActivate(CardData card)
    {
        return card != null && card.type == CardType.Continuous &&
            (card.deckType == CardDeckType.TypeDream || card.deckType == CardDeckType.TypeNightmare) &&
            (card.continuousDurationType == ContinuousDurationType.UntilBattleEnd ||
             card.continuousDurationType == ContinuousDurationType.FixedTurns && card.continuousTurns > 0);
    }

    public ActiveContinuousCard Activate(CardData card, BattleManager battle, DeckController deck)
    {
        if (battle == null) return null;
        ActiveContinuousCard result = null;
        battle.RunEffectRoutine(ActivateRoutine(card, battle, deck, entry => result = entry));
        return result;
    }

    public IEnumerator ActivateRoutine(CardData card, BattleManager battle, DeckController deck, Action<ActiveContinuousCard> activated = null, int repeatCount = 1)
    {
        if (!CanActivate(card) || battle == null || deck == null || repeatCount < 1) yield break;
        var entry = new ActiveContinuousCard(++nextId, card, deck);
        active.Add(entry);
        activated?.Invoke(entry);
        bool complete = false;
        try
        {
            yield return battle.ExecuteCardEffectsRoutine(card, repeatCount, entry);
            complete = battle.CanContinueEffects;
        }
        finally
        {
            if (!complete)
            {
                active.Remove(entry); entry.ReleaseEffects(); Changed?.Invoke();
            }
        }
        if (!complete) yield break;
        if (!entry.IsAbsorbed) deck.ExhaustCard(card, entry.DeckType);
        Changed?.Invoke();
    }

    public void ReduceRemainingTurns()
    {
        if (active.Count == 0) return;
        // Non-recurring cards retain their original early-expiry timing.
        foreach (ActiveContinuousCard entry in active.ToArray())
            if (!entry.HasTurnStartEffects) AdvanceDuration(entry);
        Changed?.Invoke();
    }

    public void ProcessTurnStartEffects(BattleManager battle)
    {
        if (battle != null) battle.RunEffectRoutine(ProcessTurnStartEffectsRoutine(battle));
    }

    public IEnumerator ProcessTurnStartEffectsRoutine(BattleManager battle)
    {
        if (battle == null || active.Count == 0) yield break;
        ActiveContinuousCard[] current = active.ToArray();
        bool processed = false;
        foreach (ActiveContinuousCard entry in current)
        {
            if (!entry.IsActive || !entry.HasTurnStartEffects) continue;
            processed = true;
            yield return entry.ExecuteTurnStartEffectsRoutine(battle);
            if (!battle.CanContinueEffects) yield break;
        }
        // Finish all effects before expiring any maintained contributions.
        foreach (ActiveContinuousCard entry in current)
            if (entry.HasTurnStartEffects) AdvanceDuration(entry);
        if (processed) Changed?.Invoke();
    }

    private void AdvanceDuration(ActiveContinuousCard entry)
    {
        if (!entry.IsActive || entry.IsPermanent || entry.UsesDelayedLifetime) return;
        entry.RemainingTurns--;
        if (entry.RemainingTurns > 0)
        {
            entry.NotifyDurationChanged();
            return;
        }
        active.Remove(entry);
        entry.ReleaseEffects();
        if (entry.IsAbsorbed && entry.Deck != null)
            entry.Deck.ReturnToDrawPile(entry.Card, entry.DeckType);
    }

    public void CompleteDelayedCard(ActiveContinuousCard entry)
    {
        if (entry == null || !entry.IsActive || entry.HasPendingDelays || !active.Remove(entry)) return;
        entry.ReleaseEffects();
        if (entry.IsAbsorbed && entry.Deck != null) entry.Deck.DiscardCard(entry.Card, entry.DeckType);
        Changed?.Invoke();
    }

    // Initializing a battle must not return old absorbed cards into freshly reset piles.
    public void ResetForBattle()
    {
        if (active.Count == 0) return;
        ActiveContinuousCard[] previous = active.ToArray();
        active.Clear();
        foreach (ActiveContinuousCard entry in previous) entry.ReleaseEffects();
        Changed?.Invoke();
    }
}
