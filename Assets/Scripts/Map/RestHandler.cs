using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class RestHandler : MapContentHandler
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button recoverButton;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text recoveryDescription;
    [SerializeField, Range(0, 100)] private int recoveryPercent = 30;
    [SerializeField, Min(0)] private int flatRecovery = 10;

    private RunNodeVisit visit;

    public override MapContentType ContentType => MapContentType.Rest;

    private void Awake() => recoverButton.onClick.AddListener(Recover);

    private void OnDestroy()
    {
        if (recoverButton != null) recoverButton.onClick.RemoveListener(Recover);
    }

    public override void Begin(MapChoice choice, RunProgress progress, Func<bool> complete)
    {
        visit = CaptureVisit(progress);
        healthText.text = $"HP {progress.CurrentHP} / {progress.MaxHP}";
        recoveryDescription.text = $"잃은 체력의 {recoveryPercent}% + {flatRecovery}만큼\n회복합니다.";
        panel.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(recoverButton.gameObject);
    }

    private void Recover()
    {
        if (visit == null || DeckOverviewUI.BlocksBattleInput || SettingsUI.BlocksBattleInput ||
            CardInspectionUI.BlocksGameplayInput) return;

        RunSession session = RunSession.Instance;
        if (session == null || !session.CompleteRest(visit, recoveryPercent, flatRecovery))
        {
            recoveryDescription.text = "저장 실패\n다시 시도해 주세요.";
            return;
        }

        visit = null;
        panel.SetActive(false);
    }
}
