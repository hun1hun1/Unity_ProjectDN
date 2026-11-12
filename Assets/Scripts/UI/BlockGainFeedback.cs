using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class BlockGainFeedback
{
    private const float Duration = .45f;
    private const float MergeWindow = .14f;
    private static readonly Color ShieldFlash = new Color(.72f, .91f, 1f);
    private static readonly Color GainColor = new Color(.82f, .94f, 1f);

    private readonly RectTransform visual;
    private readonly Image badge;
    private readonly Image shield;
    private readonly TMP_Text amountLabel;
    private Vector3 badgeScale;
    private float startedAt;
    private long displayedAmount;
    private float shieldWidthFraction;
    private bool playing;

    internal BlockGainFeedback(RectTransform visual, Image badge, TMP_Text number)
    {
        this.visual = visual;
        this.badge = badge;

        var shieldObject = new GameObject("BlockGainShield", typeof(RectTransform));
        shieldObject.layer = visual.gameObject.layer;
        shieldObject.transform.SetParent(visual, false);
        shield = shieldObject.AddComponent<Image>();
        shield.sprite = badge.sprite;
        shield.preserveAspect = true;
        shield.raycastTarget = false;
        var edge = shieldObject.AddComponent<Outline>();
        edge.effectColor = new Color(.38f, .74f, 1f, .6f);
        edge.effectDistance = new Vector2(2f, -2f);
        shieldObject.SetActive(false);

        var labelObject = new GameObject("BlockGainAmount", typeof(RectTransform));
        labelObject.layer = badge.gameObject.layer;
        labelObject.transform.SetParent(badge.transform, false);
        amountLabel = labelObject.AddComponent<TextMeshProUGUI>();
        amountLabel.font = number.font;
        amountLabel.fontSharedMaterial = number.fontSharedMaterial;
        amountLabel.fontSize = 24f;
        amountLabel.fontSizeMin = 15f;
        amountLabel.fontSizeMax = 24f;
        amountLabel.enableAutoSizing = true;
        amountLabel.alignment = TextAlignmentOptions.Center;
        amountLabel.raycastTarget = false;
        RectTransform labelRect = amountLabel.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(.5f, 1f);
        labelRect.pivot = new Vector2(.5f, 0f);
        labelRect.sizeDelta = new Vector2(92f, 28f);
        labelObject.SetActive(false);
    }

    internal bool Matches(RectTransform artwork, Image blockBadge) => visual == artwork && badge == blockBadge;

    internal void Play(int amount, Rect bounds)
    {
        float now = Time.unscaledTime;
        displayedAmount = playing && now - startedAt <= MergeWindow ? displayedAmount + amount : amount;
        if (!playing) badgeScale = badge.rectTransform.localScale;

        shield.rectTransform.anchorMin = shield.rectTransform.anchorMax = bounds.center;
        shield.rectTransform.anchoredPosition = Vector2.zero;
        shieldWidthFraction = Mathf.Min(bounds.width, bounds.height) * .78f;
        amountLabel.text = $"+{displayedAmount}";
        shield.gameObject.SetActive(true);
        amountLabel.gameObject.SetActive(true);
        startedAt = now;
        playing = true;
        Update();
    }

    internal void Update()
    {
        if (!playing) return;
        float elapsed = Time.unscaledTime - startedAt;
        if (elapsed >= Duration || shield == null || badge == null || amountLabel == null)
        {
            Hide();
            return;
        }

        float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .1f));
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - .25f) / .2f));
        float width = Mathf.Clamp(Mathf.Min(visual.rect.width, visual.rect.height) * shieldWidthFraction, 108f, 220f);
        shield.rectTransform.sizeDelta = new Vector2(width, width * shield.sprite.rect.height / shield.sprite.rect.width);
        shield.rectTransform.localScale = Vector3.one * Mathf.Lerp(.78f, 1f, rise);
        Color shieldColor = Color.Lerp(Color.white, ShieldFlash, Mathf.Clamp01(elapsed / .16f));
        shieldColor.a = Mathf.Lerp(.20f, .72f, rise) * fade;
        shield.color = shieldColor;

        float pulse = Mathf.Sin(Mathf.PI * Mathf.Clamp01(elapsed / .25f));
        badge.rectTransform.localScale = badgeScale * (1f + .13f * pulse);
        amountLabel.rectTransform.anchoredPosition = new Vector2(0f, 2f + 10f * elapsed / Duration);
        Color labelColor = GainColor;
        labelColor.a = fade;
        amountLabel.color = labelColor;
    }

    internal void Hide()
    {
        if (!playing) return;
        playing = false;
        if (badge != null) badge.rectTransform.localScale = badgeScale;
        if (shield != null) shield.gameObject.SetActive(false);
        if (amountLabel != null) amountLabel.gameObject.SetActive(false);
    }

    internal void Dispose()
    {
        Hide();
        if (shield != null) Object.Destroy(shield.gameObject);
        if (amountLabel != null) Object.Destroy(amountLabel.gameObject);
    }
}
