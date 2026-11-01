using TMPro;
using UnityEngine;

public class DeckOverviewUI : MonoBehaviour
{
    [SerializeField] private DeckController deckController;
    [SerializeField] private BattleManager battle;
    [SerializeField] private GameObject panel;
    [SerializeField] private DeckListUI dreamList;
    [SerializeField] private DeckListUI nightmareList;
    [SerializeField] private DeckListUI pileList;
    [SerializeField] private TMP_Text dreamTitle;
    [SerializeField] private TMP_Text nightmareTitle;
    [SerializeField] private UnityEngine.UI.Button toggleButton;
    [SerializeField] private UnityEngine.UI.Button closeButton;

    private static DeckOverviewUI instance;
    private static int blockedUntilFrame = -1;
    private bool opened;

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
        if (!isActiveAndEnabled || CardInspectionUI.BlocksGameplayInput || SettingsUI.BlocksBattleInput) return;
        if (IsOpen) { Close(); return; }
        if (Time.frameCount <= blockedUntilFrame || battle == null || !battle.CanInspectCards ||
            deckController == null || panel == null || dreamList == null || nightmareList == null) return;

        if (pileList != null) pileList.Close();
        foreach (CardHoverUI hover in FindObjectsByType<CardHoverUI>(FindObjectsSortMode.None)) hover.ResetHover();
        foreach (DeckPileHoverUI hover in FindObjectsByType<DeckPileHoverUI>(FindObjectsSortMode.None)) hover.ResetHover();
        var dream = deckController.GetDeck(CardDeckType.TypeDream);
        var nightmare = deckController.GetDeck(CardDeckType.TypeNightmare);
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        opened = true;
        dreamList.ShowCards(dream, battle.GetHand(CardDeckType.TypeDream));
        nightmareList.ShowCards(nightmare, battle.GetHand(CardDeckType.TypeNightmare));
        if (dreamTitle != null) dreamTitle.text = $"꿈 덱 · {dream.Count}장";
        if (nightmareTitle != null) nightmareTitle.text = $"악몽 덱 · {nightmare.Count}장";
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
        if (opened) blockedUntilFrame = Time.frameCount;
        opened = false;
        if (dreamList != null) dreamList.HideAndClear();
        if (nightmareList != null) nightmareList.HideAndClear();
        if (panel != null) panel.SetActive(false);
    }

    private void LateUpdate()
    {
        if (opened && (!IsOpen || battle == null || !battle.isActiveAndEnabled || !battle.CanContinueEffects)) ForceClose();
    }

    private void OnBattleEnded(BattleResult result) { ForceClose(); }

    private void OnDisable()
    {
        if (battle != null) battle.BattleEnded -= OnBattleEnded;
        ForceClose();
        if (instance == this) instance = null;
    }
}
