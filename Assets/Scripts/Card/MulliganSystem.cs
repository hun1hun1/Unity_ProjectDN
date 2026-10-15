using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Runtime requests belong to a battle, never to shared effect/card assets.
public sealed class MulliganSystem
{
    private readonly List<CardUI> candidates = new List<CardUI>();
    private readonly List<CardUI> selected = new List<CardUI>();
    private MulliganSelectionUI view;
    private bool resolving;
    private bool confirmed;
    private bool cancelled;
    public bool IsSelecting { get; private set; }
    public int RequiredCount { get; private set; }
    public int SelectedCount => selected.Count;

    public bool Toggle(CardUI card)
    {
        if (!IsSelecting || confirmed || card == null || !card.CanMulligan || !candidates.Contains(card)) return false;
        if (selected.Remove(card)) card.SetMulliganSelected(false);
        else
        {
            if (selected.Count >= RequiredCount) return false;
            selected.Add(card); card.SetMulliganSelected(true);
        }
        view?.Refresh();
        return true;
    }

    public bool Confirm()
    {
        if (!IsSelecting || confirmed || selected.Count != RequiredCount) return false;
        foreach (CardUI card in selected) if (card == null || !card.CanMulligan) return false;
        confirmed = true;
        return true;
    }

    public void Cancel()
    {
        cancelled = true;
        IsSelecting = false;
        foreach (CardUI card in candidates) if (card != null) card.SetMulliganSelected(false);
        if (view != null) view.Hide();
    }

    public IEnumerator Resolve(MulliganEffect effect, BattleManager battle)
    {
        if (resolving || effect == null || !effect.IsValid || battle == null) yield break;
        resolving = true; cancelled = false; confirmed = false;
        var exchanged = new List<CardUI>();
        try
        {
            while (battle.HasOtherCardUses || battle.HandsAreMoving)
            {
                if (cancelled || !battle.CanContinueEffects) yield break;
                yield return null;
            }
            if (!battle.CanContinueEffects) yield break;
            candidates.Clear(); selected.Clear();
            if (effect.HandScope != MulliganHandScope.Nightmare) AddCandidates(battle.GetHand(CardDeckType.TypeDream));
            if (effect.HandScope != MulliganHandScope.Dream) AddCandidates(battle.GetHand(CardDeckType.TypeNightmare));
            RequiredCount = effect.CountMode == MulliganCountMode.All ? candidates.Count : Mathf.Min(effect.Count, candidates.Count);
            if (RequiredCount == 0) yield break;
            battle.PresentResolvingCard();
            if (effect.CountMode == MulliganCountMode.All) selected.AddRange(candidates);
            else if (effect.SelectionMode == MulliganSelectionMode.Random)
            {
                for (int i = 0; i < RequiredCount; i++)
                {
                    int next = Random.Range(i, candidates.Count);
                    CardUI swap = candidates[i]; candidates[i] = candidates[next]; candidates[next] = swap;
                    selected.Add(candidates[i]);
                }
            }
            else
            {
                IsSelecting = true;
                Canvas canvas = candidates[0].GetComponentInParent<Canvas>()?.rootCanvas;
                if (canvas == null) yield break;
                view = canvas.GetComponentInChildren<MulliganSelectionUI>(true);
                if (view == null) view = MulliganSelectionUI.Create(canvas);
                view.Show(this);
                while (!confirmed)
                {
                    if (cancelled || !battle.CanContinueEffects) yield break;
                    // External destruction must not leave an impossible selection on screen.
                    for (int i = candidates.Count - 1; i >= 0; i--)
                        if (candidates[i] == null || !candidates[i].CanMulligan)
                        {
                            CardUI invalid = candidates[i]; candidates.RemoveAt(i); selected.Remove(invalid);
                            if (invalid != null) invalid.SetMulliganSelected(false);
                        }
                    RequiredCount = Mathf.Min(RequiredCount, candidates.Count);
                    if (RequiredCount == 0) yield break;
                    view.Refresh();
                    yield return null;
                }
                IsSelecting = false;
                view.Hide();
            }
            if (cancelled || !battle.CanContinueEffects) yield break;
            int dreamCount = 0, nightmareCount = 0;
            // Commit the whole exchange before yielding: interrupted animation cannot lose cards.
            foreach (CardUI card in selected)
            {
                if (card == null || !card.CanMulligan) continue;
                CardData data = card.GetCardData();
                CardHandController hand = card.GetHandController();
                if (hand != battle.GetHand(data.deckType)) continue;
                card.SetMulliganSelected(false);
                hand.RemoveCard(card);
                card.transform.SetParent(hand.GetCardAnimationLayer(), true);
                battle.GetDeckController().DiscardCard(data, data.deckType);
                exchanged.Add(card);
                if (data.deckType == CardDeckType.TypeDream) dreamCount++; else nightmareCount++;
            }
            Draw(battle, CardDeckType.TypeDream, dreamCount);
            Draw(battle, CardDeckType.TypeNightmare, nightmareCount);
            foreach (CardUI card in exchanged)
            {
                if (card == null) continue;
                yield return card.DiscardAnimation(card.GetHandController().GetDiscardPilePoint());
                if (card != null) Object.Destroy(card.gameObject);
            }
        }
        finally
        {
            Cancel();
            foreach (CardUI card in exchanged) if (card != null) Object.Destroy(card.gameObject);
            candidates.Clear(); selected.Clear(); RequiredCount = 0; resolving = false;
        }
    }

    private void AddCandidates(CardHandController hand)
    {
        if (hand == null) return;
        foreach (CardUI card in hand.GetCardsSnapshot())
            if (card != null && card.CanMulligan) candidates.Add(card);
    }

    private static void Draw(BattleManager battle, CardDeckType type, int count)
    {
        CardHandController hand = battle.GetHand(type);
        for (int i = 0; i < count; i++) if (hand == null || !battle.DrawCard(type, hand)) break;
    }
}
