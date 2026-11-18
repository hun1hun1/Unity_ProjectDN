using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameOverUI : MonoBehaviour
{
    [SerializeField] private UISpriteAnimation defeatAnimation;
    [SerializeField] private CanvasGroup overlay;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text location;
    [SerializeField] private TMP_Text encounter;
    [SerializeField] private TMP_Text navigationError;
    [SerializeField] private Button returnButton;

    private void Awake() => returnButton.onClick.AddListener(ReturnToTitle);

    internal void Show(Player player, RunProgress progress)
    {
        if (gameObject.activeSelf) return;

        location.transform.parent.gameObject.SetActive(progress != null);
        if (progress != null)
            location.text = $"{progress.FloorIndex + 1}층 · {progress.NodeIndex + 1}번째 노드";
        BattleEncounterDefinition lastEncounter = progress?.CurrentNode?.SelectedChoice?.Encounter;
        encounter.text = lastEncounter != null ? lastEncounter.displayName : "";
        encounter.transform.parent.gameObject.SetActive(!string.IsNullOrWhiteSpace(encounter.text));
        navigationError.text = "";
        panel.SetActive(false);
        overlay.alpha = 0;
        overlay.interactable = false;
        returnButton.interactable = false;
        gameObject.SetActive(true);
        StartCoroutine(ShowAfterFall(player));
    }

    private IEnumerator ShowAfterFall(Player player)
    {
        // Let the final action release its sprite and motion overrides before taking over the visual.
        yield return null;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        BattleCharacterPresentation presentation = player != null
            ? player.GetComponent<BattleCharacterPresentation>() : null;
        RectTransform visual = presentation != null ? presentation.Visual : null;
        if (defeatAnimation != null && visual != null &&
            visual.TryGetComponent(out UISpriteAnimator animator) && animator.isActiveAndEnabled)
        {
            animator.Play(defeatAnimation, false);
            while (animator != null && animator.isActiveAndEnabled &&
                animator.Animation == defeatAnimation && !animator.IsComplete)
                yield return null;
        }

        panel.SetActive(true);
        const float fadeDuration = 0.22f;
        for (float elapsed = 0; elapsed < fadeDuration; elapsed += Time.unscaledDeltaTime)
        {
            overlay.alpha = elapsed / fadeDuration;
            yield return null;
        }
        overlay.alpha = 1;
        overlay.interactable = true;
        returnButton.interactable = true;
        if (EventSystem.current != null) returnButton.Select();
    }

    private void ReturnToTitle()
    {
        if (!returnButton.interactable) return;

        returnButton.interactable = false;
        if (RunSession.GetOrCreate().ReturnToTitle()) return;

        navigationError.text = "저장 또는 이동에 실패했습니다. 다시 시도해 주세요.";
        returnButton.interactable = true;
        if (EventSystem.current != null) returnButton.Select();
    }
}
