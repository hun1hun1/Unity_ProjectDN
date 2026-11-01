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
    private RunProgress owner;
    private int ownerFloor,ownerNode;
    private bool boundToRun;
    public void BindRun(RunProgress progress)
    { owner=progress;ownerFloor=progress.FloorIndex;ownerNode=progress.NodeIndex;boundToRun=true; }

    public void ConfigureGoldReward(int amount)
    {
        if (generated) throw new InvalidOperationException("Rewards have already been generated.");
        goldReward = Mathf.Max(0, amount);
    }

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
    public RunRewardData CaptureCheckpoint(RunAssetCatalog catalog)
    {
        return new RunRewardData { state=State, gold=GoldReward, goldAvailable=GoldAvailable, cardAvailable=CardAvailable,
            offers=offers.ConvertAll(card=>catalog.Key(card)).ToArray() };
    }
    public void RestoreCheckpoint(RunRewardData saved,RunAssetCatalog catalog)
    {
        if(saved==null)throw new ArgumentNullException(nameof(saved));
        generated=true;goldReward=saved.gold;GoldAvailable=saved.goldAvailable;CardAvailable=saved.cardAvailable;
        offers=new List<CardData>();foreach(string id in saved.offers)offers.Add(catalog.Resolve<CardData>(id));
        SetState(saved.state);
    }
    public void PrepareVictory() { OnBattleEnded(BattleResult.Victory); }
    private bool CanInteract => this!=null && isActiveAndEnabled && !SettingsUI.BlocksBattleInput &&
        (RunSession.Instance==null || !RunSession.Instance.IsTransitioning) && (!boundToRun ||
        (RunSession.Instance!=null && ReferenceEquals(RunSession.Instance.Progress,owner) && owner.Phase==RunPhase.Rewards &&
        owner.FloorIndex==ownerFloor && owner.NodeIndex==ownerNode));
    public bool ClaimGold()
    {
        if (!CanInteract || State != BattleRewardState.Summary || !GoldAvailable) return false;
        GoldAvailable = false;
        RunGoldInventory.Add(GoldReward);
        Changed?.Invoke();
        return !GoldAvailable;
    }
    public bool OpenCardReward()
    {
        if (!CanInteract || State != BattleRewardState.Summary || !CardAvailable) return false;
        SetState(BattleRewardState.DeckChoice); return true;
    }
    public bool CanSelectDeck(CardDeckType deck) => cardRewards != null && deckController != null &&
        cardRewards.GetCandidateCount(deck) >= (deck == CardDeckType.TypeDream ? 3 : 2);
    public bool SelectDeck(CardDeckType deck)
    {
        if (!CanInteract || State != BattleRewardState.DeckChoice || !CanSelectDeck(deck)) return false;
        int count = deck == CardDeckType.TypeDream ? 3 : 2;
        List<CardData> selected = cardRewards.GenerateRewards(deck, count);
        if (selected.Count != count) return false;
        offers = selected;
        SetState(BattleRewardState.CardChoice); return true;
    }
    public bool ChooseCard(int index)
    {
        if (!CanInteract || State != BattleRewardState.CardChoice || index < 0 || index >= offers.Count || offers[index] == null || deckController == null) return false;
        CardData card = offers[index];
        CardAvailable = false;
        State = BattleRewardState.Summary;
        deckController.AddCardToDeck(card, card.deckType);
        offers.Clear();
        Changed?.Invoke(); return !CardAvailable;
    }
    public bool SkipCard()
    {
        if (!CanInteract || State != BattleRewardState.DeckChoice) return false;
        CardAvailable = false; SetState(BattleRewardState.Summary); return true;
    }
    public bool SkipRemaining()
    {
        if (!CanInteract || State != BattleRewardState.Summary) return false;
        GoldAvailable = CardAvailable = false;
        SetState(BattleRewardState.Completed); Completed?.Invoke(); return true;
    }
}
