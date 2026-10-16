using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardRewardUI : MonoBehaviour
{
    [SerializeField] private BattleRewardSystem rewards;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private GameObject summaryPanel;
    [SerializeField] private GameObject deckSelectPanel;
    [SerializeField] private GameObject cardRewardPanel;
    [SerializeField] private DeckCardUI[] rewardCardSlots;
    [SerializeField] private Button goldButton;
    [SerializeField] private Button cardButton;
    [SerializeField] private Button dreamButton;
    [SerializeField] private Button nightmareButton;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text walletText;
    [SerializeField] private TMP_Text finishText;

    private void OnEnable() { if (rewards != null) rewards.Changed += Refresh; Refresh(); }
    private void OnDisable() { if (rewards != null) rewards.Changed -= Refresh; }
    private void Refresh()
    {
        if (rewards == null || visibility == null) return;
        BattleRewardState state = rewards.State;
        bool visible = state != BattleRewardState.Closed && state != BattleRewardState.Completed;
        visibility.alpha = visible ? 1 : 0;
        visibility.interactable = visibility.blocksRaycasts = visible;
        summaryPanel.SetActive(state == BattleRewardState.Summary);
        deckSelectPanel.SetActive(state == BattleRewardState.DeckChoice);
        cardRewardPanel.SetActive(state == BattleRewardState.CardChoice);
        goldButton.gameObject.SetActive(rewards.GoldAvailable);
        cardButton.gameObject.SetActive(rewards.CardAvailable);
        goldText.text = $"골드 {rewards.GoldReward} 받기";
        walletText.text = $"보유 골드: {RunGoldInventory.Gold}";
        finishText.text = rewards.GoldAvailable || rewards.CardAvailable ? "남은 보상 건너뛰기" : "완료";
        dreamButton.interactable = rewards.CanSelectDeck(CardDeckType.TypeDream);
        nightmareButton.interactable = rewards.CanSelectDeck(CardDeckType.TypeNightmare);
        for (int i = 0; i < rewardCardSlots.Length; i++)
        {
            bool show = state == BattleRewardState.CardChoice && i < rewards.Offers.Count;
            rewardCardSlots[i].gameObject.SetActive(show);
            if (show) rewardCardSlots[i].SetCard(rewards.Offers[i], applyStatusModifiers: false);
        }
    }
    public void ClaimGold() { if (!CardInspectionUI.BlocksGameplayInput) rewards.ClaimGold(); }
    public void ShowReward() { if (!CardInspectionUI.BlocksGameplayInput) rewards.OpenCardReward(); }
    public void SelectDream() { if (!CardInspectionUI.BlocksGameplayInput) rewards.SelectDeck(CardDeckType.TypeDream); }
    public void SelectNightmare() { if (!CardInspectionUI.BlocksGameplayInput) rewards.SelectDeck(CardDeckType.TypeNightmare); }
    public void SkipCard() { if (!CardInspectionUI.BlocksGameplayInput) rewards.SkipCard(); }
    public void SkipRemaining() { if (!CardInspectionUI.BlocksGameplayInput) rewards.SkipRemaining(); }
    public void SelectCard(int index) { if (!CardInspectionUI.BlocksGameplayInput) rewards.ChooseCard(index); }
}
