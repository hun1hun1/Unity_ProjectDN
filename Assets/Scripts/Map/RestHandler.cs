using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class RestHandler : MapContentHandler
{
    private static readonly Color CampfireOrange = new Color(1f, 0.63f, 0.31f);

    [SerializeField] private GameObject panel;
    [SerializeField] private Button recoverButton;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private DeckOverviewUI deckOverview;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text recoveryDescription;
    [SerializeField, Range(0, 100)] private int recoveryPercent = 30;
    [SerializeField, Min(0)] private int flatRecovery = 10;

    private RunNodeVisit visit;

    public override MapContentType ContentType => MapContentType.Rest;

    private void Awake()
    {
        recoverButton.onClick.AddListener(Recover);
        if (upgradeButton != null) upgradeButton.onClick.AddListener(OpenUpgrade);
    }

    private void OnDestroy()
    {
        if (recoverButton != null) recoverButton.onClick.RemoveListener(Recover);
        if (upgradeButton != null) upgradeButton.onClick.RemoveListener(OpenUpgrade);
    }

    public override void Begin(MapChoice choice, RunProgress progress, Func<bool> complete)
    {
        visit = CaptureVisit(progress);
        healthText.text = $"HP {progress.CurrentHP} / {progress.MaxHP}";
        recoveryDescription.text = $"잃은 체력의 {recoveryPercent}% + {flatRecovery}만큼\n회복합니다.";
        panel.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(recoverButton.gameObject);
    }

    internal override void PlayEntrance(UnityEngine.UI.Button[] choices, int selectedIndex) =>
        StartEntrance(panel, choices, selectedIndex, CampfireOrange, campfireGlow: true);

    private bool CanChooseRestAction => visit != null && panel.activeInHierarchy && !IsEntering &&
        !DeckOverviewUI.BlocksBattleInput && !SettingsUI.BlocksBattleInput && !CardInspectionUI.BlocksGameplayInput;

    private void OpenUpgrade()
    {
        if (!CanChooseRestAction || deckOverview == null) return;

        RunNodeVisit expected = visit;
        if (!deckOverview.ShowUpgrades(
            (selection, replacement) => Upgrade(expected, selection, replacement), ReturnFromUpgrade)) return;

        recoverButton.interactable = false;
        upgradeButton.interactable = false;
    }

    private void ReturnFromUpgrade()
    {
        if (this == null || recoverButton == null || upgradeButton == null) return;
        recoverButton.interactable = true;
        upgradeButton.interactable = true;
        if (upgradeButton.isActiveAndEnabled && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(upgradeButton.gameObject);
    }

    private bool Upgrade(RunNodeVisit expected, RunDeckInventory.CardSelection selection, CardData replacement)
    {
        RunSession session = RunSession.Instance;
        if (visit != expected || !panel.activeInHierarchy || IsEntering ||
            session == null || !session.CompleteRestUpgrade(expected, selection, replacement)) return false;
        Finish();
        return true;
    }

    private void Recover()
    {
        if (!CanChooseRestAction) return;

        RunSession session = RunSession.Instance;
        if (session == null || !session.CompleteRest(visit, recoveryPercent, flatRecovery))
        {
            recoveryDescription.text = "저장 실패\n다시 시도해 주세요.";
            return;
        }

        Finish();
    }

    private void Finish()
    {
        visit = null;
        panel.SetActive(false);
    }
}
