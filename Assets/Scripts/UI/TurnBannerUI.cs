using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TurnBannerUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Sprite playerSprite;
    [SerializeField] private Sprite enemySprite;

    private const float FadeIn = .08f;
    private const float Hold = .30f;
    private const float FadeOut = .12f;

    internal bool IsShowing { get; private set; }

    private void Awake() => Hide();
    private void OnDisable() => Hide();

    internal IEnumerator ShowPlayerTurn(int turnNumber) => Show($"내 차례 · {turnNumber}턴", playerSprite);
    internal IEnumerator ShowEnemyTurn() => Show("적 차례", enemySprite);

    internal void Hide()
    {
        IsShowing = false;
        if (visibility != null) visibility.alpha = 0f;
    }

    private IEnumerator Show(string message, Sprite sprite)
    {
        if (!isActiveAndEnabled || IsShowing || visibility == null || background == null || label == null || sprite == null)
            yield break;

        background.sprite = sprite;
        label.text = message;
        IsShowing = true;
        const float duration = FadeIn + Hold + FadeOut;
        try
        {
            // A slow scene-loading frame must not skip the announcement entirely.
            for (float elapsed = 0f; IsShowing && elapsed < duration; elapsed += Mathf.Min(Time.unscaledDeltaTime, FadeIn))
            {
                visibility.alpha = Mathf.Clamp01(Mathf.Min(elapsed / FadeIn, (duration - elapsed) / FadeOut));
                yield return null;
            }
        }
        finally
        {
            Hide();
        }
    }
}
