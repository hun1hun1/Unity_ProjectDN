using TMPro;
using UnityEngine;

public class StatusTooltipUI : MonoBehaviour
{
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private float preferredWidth = 320f;
    [SerializeField] private float padding = 12f;
    [SerializeField] private Vector2 pointerOffset = new Vector2(16f, -16f);
    private StatusIconUI owner;
    private Canvas canvas;
    private Vector2 screenPosition;

    public void Show(StatusIconUI icon, ActiveStatusEffect effect, Vector2 position)
    {
        if (icon == null || effect == null || contentText == null) return;
        canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (canvas == null) return;
        owner = icon;
        screenPosition = position;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh(icon, effect);
    }

    public void Refresh(StatusIconUI icon, ActiveStatusEffect effect)
    {
        if (owner != icon || effect == null || contentText == null) return;
        string duration = effect.IsPermanent ? "전투 종료까지 지속" : $"남은 턴: {effect.RemainingTurns}";
        contentText.text = $"<b>{effect.Definition.DisplayName}</b>\n{effect.Definition.Description}\n{duration}";
        UpdatePosition();
    }

    public void ShowContinuous(StatusIconUI icon, ActiveContinuousCard card, Vector2 position)
    {
        if (icon == null || card == null || contentText == null) return;
        canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (canvas == null) return;
        owner = icon;
        screenPosition = position;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RefreshContinuous(icon, card);
    }

    public void RefreshContinuous(StatusIconUI icon, ActiveContinuousCard card)
    {
        if (owner != icon || card == null || contentText == null) return;
        string duration = card.IsPermanent ? "전투 종료까지 지속" : $"남은 턴: {card.RemainingTurns}";
        string location = card.IsAbsorbed ? (card.IsPermanent ? "흡수됨" : "흡수됨 · 만료 시 뽑을 카드 더미로 복귀") : "사용 시 소멸";
        contentText.text = $"<b>{card.Card.cardName}</b>\n{CardDescriptionBuilder.Build(card.Card, null, false)}\n{duration}\n{location}";
        UpdatePosition();
    }

    public void Move(StatusIconUI icon, Vector2 position)
    {
        if (owner != icon) return;
        screenPosition = position;
        UpdatePosition();
    }

    public void Hide(StatusIconUI icon)
    {
        if (owner != icon) return;
        owner = null;
        gameObject.SetActive(false);
    }

    private void OnDisable() { owner = null; }

    private void LateUpdate()
    {
        if (owner == null || !owner.isActiveAndEnabled)
        {
            gameObject.SetActive(false);
            return;
        }
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (canvas == null || contentText == null) return;
        RectTransform canvasRect = (RectTransform)canvas.transform;
        RectTransform panel = (RectTransform)transform;
        Rect bounds = canvasRect.rect;
        float width = Mathf.Min(preferredWidth, bounds.width);
        float textWidth = Mathf.Max(1f, width - padding * 2f);
        float height = Mathf.Min(contentText.GetPreferredValues(contentText.text, textWidth, Mathf.Infinity).y + padding * 2f, bounds.height);
        panel.sizeDelta = new Vector2(width, height);
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, camera, out Vector2 point)) return;
        Vector2 target = point + pointerOffset;
        if (target.x + width > bounds.xMax) target.x = point.x - pointerOffset.x - width;
        if (target.y - height < bounds.yMin) target.y = point.y - pointerOffset.y + height;
        target.x = Mathf.Clamp(target.x, bounds.xMin, bounds.xMax - width);
        target.y = Mathf.Clamp(target.y, bounds.yMin + height, bounds.yMax);
        // The prefab uses a top-left pivot and is a direct child of the root Canvas.
        panel.localPosition = new Vector3(target.x, target.y, 0f);
    }
}
