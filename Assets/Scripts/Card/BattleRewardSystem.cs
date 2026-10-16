using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleResult { None, Victory, Defeat, Cancelled }
public enum BattleRewardState { Closed, Summary, DeckChoice, CardChoice, Completed }

public class BattleRewardSystem : MonoBehaviour
{
    [SerializeField] private BattleManager battle;
    [SerializeField] private DeckController deckController;
    [SerializeField] private CardRewardSystem cardRewards;
    [SerializeField, Min(0)] private int goldReward = 20;
    public BattleRewardState State { get; private set; }
    public int GoldReward => Mathf.Max(0, goldReward);
    public bool GoldAvailable { get; private set; }
    public bool CardAvailable { get; private set; }
    public IReadOnlyList<CardData> Offers => offers.AsReadOnly();
    public event Action Changed;
    public event Action Completed;
    private List<CardData> offers = new List<CardData>();
    private bool generated;

    private void OnEnable()
    {
        if (battle == null) return;
        battle.BattleEnded += OnBattleEnded;
        OnBattleEnded(battle.Result);
    }
    private void OnDisable() { if (battle != null) battle.BattleEnded -= OnBattleEnded; }
    private void OnBattleEnded(BattleResult result)
    {
        if (generated || result != BattleResult.Victory) return;
        generated = true;
        GoldAvailable = GoldReward > 0;
        CardAvailable = true;
        SetState(BattleRewardState.Summary);
    }
    private void SetState(BattleRewardState state) { State = state; Changed?.Invoke(); }
    public bool ClaimGold()
    {
        if (State != BattleRewardState.Summary || !GoldAvailable) return false;
        GoldAvailable = false;
        RunGoldInventory.Add(GoldReward);
        Changed?.Invoke();
        return true;
    }
    public bool OpenCardReward()
    {
        if (State != BattleRewardState.Summary || !CardAvailable) return false;
        SetState(BattleRewardState.DeckChoice); return true;
    }
    public bool CanSelectDeck(CardDeckType deck) => cardRewards != null && deckController != null &&
        cardRewards.GetCandidateCount(deck) >= (deck == CardDeckType.TypeDream ? 3 : 2);
    public bool SelectDeck(CardDeckType deck)
    {
        if (State != BattleRewardState.DeckChoice || !CanSelectDeck(deck)) return false;
        int count = deck == CardDeckType.TypeDream ? 3 : 2;
        List<CardData> selected = cardRewards.GenerateRewards(deck, count);
        if (selected.Count != count) return false;
        offers = selected;
        SetState(BattleRewardState.CardChoice); return true;
    }
    public bool ChooseCard(int index)
    {
        if (State != BattleRewardState.CardChoice || index < 0 || index >= offers.Count || offers[index] == null || deckController == null) return false;
        CardData card = offers[index];
        CardAvailable = false;
        State = BattleRewardState.Summary;
        deckController.AddCardToDeck(card, card.deckType);
        offers.Clear();
        Changed?.Invoke(); return true;
    }
    public bool SkipCard()
    {
        if (State != BattleRewardState.DeckChoice) return false;
        CardAvailable = false; SetState(BattleRewardState.Summary); return true;
    }
    public bool SkipRemaining()
    {
        if (State != BattleRewardState.Summary) return false;
        GoldAvailable = CardAvailable = false;
        SetState(BattleRewardState.Completed); Completed?.Invoke(); return true;
    }
}
