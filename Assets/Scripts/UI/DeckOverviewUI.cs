using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class DeckOverviewUI : MonoBehaviour
{
    [SerializeField] private DeckController deckController;
    [SerializeField] private BattleManager battle;
    [SerializeField] private CardDatabase dreamStartingDeckDatabase;
    [SerializeField] private CardDatabase nightmareStartingDeckDatabase;
    [SerializeField] private GameObject panel;
    [SerializeField] private DeckListUI dreamList;
    [SerializeField] private DeckListUI nightmareList;
    [SerializeField] private DeckListUI pileList;
    [SerializeField] private TMP_Text dreamTitle;
    [SerializeField] private TMP_Text nightmareTitle;
    [SerializeField] private UnityEngine.UI.Button toggleButton;
    [SerializeField] private UnityEngine.UI.Button closeButton;
    [SerializeField] private TMP_Text panelTitle;

    private CardUpgradeComparisonUI upgradeView;
    private Func<RunDeckInventory.CardSelection, CardData, bool> commitUpgrade;
    private Action onUpgradeClosed;
    private RunDeckInventory.CardSelection selectedUpgrade;
    private CardData upgradeResult;
    private string normalTitle;

    internal bool IsChoosingUpgrade => commitUpgrade != null;

    private static DeckOverviewUI instance;
    private static int blockedUntilFrame = -1;
    private bool opened;

    private bool IsMapOverview => dreamStartingDeckDatabase != null && nightmareStartingDeckDatabase != null;

    public bool IsOpen => isActiveAndEnabled && panel != null && panel.activeInHierarchy;
    public static bool BlocksBattleInput => (Application.isPlaying && Time.frameCount <= blockedUntilFrame) || (instance != null && instance.IsOpen);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; blockedUntilFrame = -1; }

    private void Awake() { if (panel != null) panel.SetActive(false); }

    private void OnEnable()
    {
        instance = this;
        if (battle != null) battle.BattleEnded += OnBattleEnded;
    }

    public void Toggle()
    {
        if (!isActiveAndEnabled || IsChoosingUpgrade || CardInspectionUI.BlocksGameplayInput || SettingsUI.BlocksBattleInput) return;
        if (IsOpen) { Close(); return; }
        if (Time.frameCount <= blockedUntilFrame || panel == null || dreamList == null || nightmareList == null) return;
        IReadOnlyList<CardData> dream;
        IReadOnlyList<CardData> nightmare;
        if (IsMapOverview)
        {
            RunSession session = RunSession.Instance;
            if (session == null || session.Progress == null || session.IsTransitioning) return;

            var runDeck = RunDeckInventory.Find(dreamStartingDeckDatabase, nightmareStartingDeckDatabase);
            dream = runDeck?.DreamView ?? dreamStartingDeckDatabase.GetAllCards();
            nightmare = runDeck?.NightmareView ?? nightmareStartingDeckDatabase.GetAllCards();
        }
        else
        {
            if (battle == null || !battle.CanInspectCards || deckController == null) return;
            dream = deckController.GetDeck(CardDeckType.TypeDream);
            nightmare = deckController.GetDeck(CardDeckType.TypeNightmare);
        }

        if (pileList != null) pileList.Close();
        foreach (CardHoverUI hover in FindObjectsByType<CardHoverUI>(FindObjectsSortMode.None)) hover.ResetHover();
        foreach (DeckPileHoverUI hover in FindObjectsByType<DeckPileHoverUI>(FindObjectsSortMode.None)) hover.ResetHover();
        ShowPanel();
        dreamList.ShowCards(dream, battle?.GetHand(CardDeckType.TypeDream));
        nightmareList.ShowCards(nightmare, battle?.GetHand(CardDeckType.TypeNightmare));
        if (IsMapOverview && closeButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        if (dreamTitle != null) dreamTitle.text = $"꿈 덱 · {dream.Count}장";
        if (nightmareTitle != null) nightmareTitle.text = $"악몽 덱 · {nightmare.Count}장";
        ResetScroll();
    }

    internal bool ShowUpgrades(Func<RunDeckInventory.CardSelection, CardData, bool> commit, Action closed)
    {
        if (!isActiveAndEnabled || !IsMapOverview || IsOpen || panelTitle == null) return false;

        if (upgradeView == null)
        {
            normalTitle = panelTitle.text;
            upgradeView = new CardUpgradeComparisonUI((RectTransform)panel.transform, panelTitle.font,
                dreamList.CardPrefab, Close, ReturnToUpgradeList, ConfirmUpgrade);
        }

        commitUpgrade = commit;
        onUpgradeClosed = closed;
        panelTitle.text = "카드 강화";
        closeButton.gameObject.SetActive(false);
        ShowPanel();
        int dreamCount = dreamList.ShowUpgradeChoices(
            RunDeckInventory.GetCardSelections(dreamStartingDeckDatabase, nightmareStartingDeckDatabase, CardDeckType.TypeDream),
            SelectUpgrade);
        int nightmareCount = nightmareList.ShowUpgradeChoices(
            RunDeckInventory.GetCardSelections(dreamStartingDeckDatabase, nightmareStartingDeckDatabase, CardDeckType.TypeNightmare),
            SelectUpgrade);
        dreamTitle.text = $"꿈 덱 · {dreamCount}장";
        nightmareTitle.text = $"악몽 덱 · {nightmareCount}장";
        ResetScroll();
        upgradeView.ShowList();
        SelectButton(upgradeView.ListCancel);
        return true;
    }

    private void SelectUpgrade(RunDeckInventory.CardSelection selection)
    {
        if (!IsChoosingUpgrade || upgradeView.IsComparing) return;
        CardData result = RunDeckInventory.GetUpgradeResult(selection.Card);
        if (result == null) return;

        selectedUpgrade = selection;
        upgradeResult = result;
        dreamList.gameObject.SetActive(false);
        nightmareList.gameObject.SetActive(false);
        upgradeView.ShowComparison(selection.Card, result);
        SelectButton(upgradeView.ComparisonCancel);
    }

    private void ReturnToUpgradeList()
    {
        if (!IsChoosingUpgrade) return;
        selectedUpgrade = default;
        upgradeResult = null;
        dreamList.gameObject.SetActive(true);
        nightmareList.gameObject.SetActive(true);
        upgradeView.ShowList();
        SelectButton(upgradeView.ListCancel);
    }

    private void ConfirmUpgrade()
    {
        if (!IsChoosingUpgrade || !upgradeView.IsComparing || upgradeResult == null) return;
        if (commitUpgrade(selectedUpgrade, upgradeResult)) ForceClose();
        else upgradeView.ShowFailure();
    }

    private static void SelectButton(UnityEngine.UI.Button button)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private void ShowPanel()
    {
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        opened = true;
    }

    private void ResetScroll()
    {
        foreach (UnityEngine.UI.ScrollRect scroll in panel.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
        {
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    public void Close()
    {
        if (CardInspectionUI.BlocksGameplayInput) return;
        ForceClose();
    }

    private void ForceClose()
    {
        bool wasOpen = opened;
        if (wasOpen) blockedUntilFrame = Time.frameCount;
        opened = false;
        if (dreamList != null) dreamList.HideAndClear();
        if (nightmareList != null) nightmareList.HideAndClear();
        if (panel != null) panel.SetActive(false);
        if (IsChoosingUpgrade)
        {
            Action closed = onUpgradeClosed;
            commitUpgrade = null;
            onUpgradeClosed = null;
            selectedUpgrade = default;
            upgradeResult = null;
            if (panel != null) upgradeView.Hide();
            if (panelTitle != null) panelTitle.text = normalTitle;
            if (closeButton != null) closeButton.gameObject.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            closed?.Invoke();
        }
        else if (wasOpen && IsMapOverview && toggleButton != null && toggleButton.isActiveAndEnabled)
            SelectButton(toggleButton);
    }

    private void LateUpdate()
    {
        if (opened && (!IsOpen || (!IsMapOverview && (battle == null || !battle.isActiveAndEnabled || !battle.CanContinueEffects))))
            ForceClose();
        else if (IsChoosingUpgrade && upgradeView.IsComparing)
            upgradeView.ResizeCards();
    }

    private void OnBattleEnded(BattleResult result) { ForceClose(); }

    private void OnDisable()
    {
        if (battle != null) battle.BattleEnded -= OnBattleEnded;
        ForceClose();
        if (instance == this) instance = null;
    }
}
