using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardRewardUI : MonoBehaviour
{
    private const float EntranceDuration = .22f;
    private const float EntranceDistance = 12f;

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
    [SerializeField] private Button confirmButton;
    [SerializeField] private GameObject[] selectionBorders;
    private int selectedCardIndex = -1;
    private Coroutine defeatWait;
    private RectTransform enteringSummary;
    private Vector2 summaryRestPosition;
    private float entranceStartedAt;
    internal RectTransform HoverLowerBoundary => confirmButton != null ? (RectTransform)confirmButton.transform : null;

    private void OnEnable()
    {
        if (rewards != null)
        {
            rewards.VictoryRewardsPrepared += ShowVictory;
            rewards.Changed += Refresh;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (rewards != null)
        {
            rewards.VictoryRewardsPrepared -= ShowVictory;
            rewards.Changed -= Refresh;
        }
        bool presentationInProgress = defeatWait != null || enteringSummary != null;
        StopWaitingForDefeat();
        if (enteringSummary != null) ResetEntrancePosition();
        if (presentationInProgress) SetVisibility(false);
        selectedCardIndex = -1;
        RefreshSelection();
    }

    private void ShowVictory(Enemy defeatedEnemy)
    {
        var presentation = defeatedEnemy != null ? defeatedEnemy.GetComponent<BattleCharacterPresentation>() : null;
        if (!Application.isPlaying || visibility == null || presentation == null || !presentation.HasDefeatMotion)
        {
            BeginEntrance();
            return;
        }

        SetVisibility(false);
        visibility.blocksRaycasts = true;
        defeatWait = StartCoroutine(ShowAfterDefeat(presentation));
    }

    private IEnumerator ShowAfterDefeat(BattleCharacterPresentation presentation)
    {
        // Allow the lethal action to release its sprite and motion overrides.
        yield return null;
        if (presentation != null)
        {
            presentation.PlayDefeat();
            while (presentation != null && presentation.IsPlayingDefeat) yield return null;
        }
        defeatWait = null;
        BeginEntrance();
    }

    private void StopWaitingForDefeat()
    {
        if (defeatWait == null) return;
        StopCoroutine(defeatWait);
        defeatWait = null;
    }

    private void BeginEntrance()
    {
        if (!Application.isPlaying || visibility == null || summaryPanel == null ||
            !(summaryPanel.transform is RectTransform panel)) return;

        enteringSummary = panel;
        summaryRestPosition = panel.anchoredPosition;
        entranceStartedAt = Time.unscaledTime;
        panel.anchoredPosition = summaryRestPosition + Vector2.down * EntranceDistance;
        visibility.alpha = 0f;
        visibility.interactable = false;
        visibility.blocksRaycasts = true;
    }

    private void Update()
    {
        if (enteringSummary == null) return;
        float progress = Mathf.Clamp01((Time.unscaledTime - entranceStartedAt) / EntranceDuration);
        visibility.alpha = progress;
        float remainingDistance = Mathf.SmoothStep(EntranceDistance, 0f, progress);
        enteringSummary.anchoredPosition = summaryRestPosition + Vector2.down * remainingDistance;
        if (progress < 1f) return;

        ResetEntrancePosition();
        SetVisibility(true);
    }

    private void ResetEntrancePosition()
    {
        enteringSummary.anchoredPosition = summaryRestPosition;
        enteringSummary = null;
    }

    private void SetVisibility(bool visible)
    {
        visibility.alpha = visible ? 1f : 0f;
        visibility.interactable = visibility.blocksRaycasts = visible;
    }

    private void Refresh()
    {
        if (rewards == null || visibility == null) return;
        selectedCardIndex = -1;
        BattleRewardState state = rewards.State;
        bool visible = state != BattleRewardState.Closed && state != BattleRewardState.Completed;
        if (state != BattleRewardState.Summary)
        {
            StopWaitingForDefeat();
            if (enteringSummary != null) ResetEntrancePosition();
        }
        if (defeatWait == null && enteringSummary == null) SetVisibility(visible);
        summaryPanel.SetActive(state == BattleRewardState.Summary);
        deckSelectPanel.SetActive(state == BattleRewardState.DeckChoice);
        cardRewardPanel.SetActive(state == BattleRewardState.CardChoice);
        goldButton.gameObject.SetActive(rewards.GoldAvailable);
        cardButton.gameObject.SetActive(rewards.CardAvailable);
        goldText.text = $"골드 {rewards.GoldReward} 받기";
        walletText.text = $"보유 골드: {RunGoldInventory.Gold}";
        if(RunSession.Instance!=null && !string.IsNullOrEmpty(RunSession.Instance.SaveError))
            walletText.text += "\n저장에 실패했습니다. 다시 시도해 주세요.";
        finishText.text = rewards.GoldAvailable || rewards.CardAvailable ? "남은 보상 건너뛰기" : "완료";
        dreamButton.interactable = rewards.CanSelectDeck(CardDeckType.TypeDream);
        nightmareButton.interactable = rewards.CanSelectDeck(CardDeckType.TypeNightmare);
        for (int i = 0; i < rewardCardSlots.Length; i++)
        {
            bool show = state == BattleRewardState.CardChoice && i < rewards.Offers.Count;
            rewardCardSlots[i].gameObject.SetActive(show);
            if (show) rewardCardSlots[i].SetCard(rewards.Offers[i], applyStatusModifiers: false);
        }
        RefreshSelection();
    }
    private void RefreshSelection()
    {
        bool choosing = rewards != null && rewards.State == BattleRewardState.CardChoice;
        if (selectionBorders != null)
            for (int i = 0; i < selectionBorders.Length; i++)
                if (selectionBorders[i] != null) selectionBorders[i].SetActive(choosing && i == selectedCardIndex);
        if (confirmButton != null)
            confirmButton.interactable = choosing && selectedCardIndex >= 0 && selectedCardIndex < rewards.Offers.Count;
    }
    private bool CanUseControls => isActiveAndEnabled && rewards != null && visibility != null &&
        visibility.interactable && !CardInspectionUI.BlocksGameplayInput;

    public void ClaimGold() { if (CanUseControls) rewards.ClaimGold(); }
    public void ShowReward() { if (CanUseControls) rewards.OpenCardReward(); }
    public void SelectDream() { if (CanUseControls) rewards.SelectDeck(CardDeckType.TypeDream); }
    public void SelectNightmare() { if (CanUseControls) rewards.SelectDeck(CardDeckType.TypeNightmare); }
    public void SkipCard() { if (CanUseControls) rewards.SkipCard(); }
    public void SkipRemaining() { if (CanUseControls) rewards.SkipRemaining(); }
    public void SelectCard(int index)
    {
        if (!CanUseControls || rewards.State != BattleRewardState.CardChoice ||
            index < 0 || index >= rewards.Offers.Count || index >= rewardCardSlots.Length || rewards.Offers[index] == null) return;
        selectedCardIndex = index;
        RefreshSelection();
    }
    public void ConfirmCard()
    {
        if (!CanUseControls || rewards.State != BattleRewardState.CardChoice || selectedCardIndex < 0) return;
        rewards.ChooseCard(selectedCardIndex);
    }
}
