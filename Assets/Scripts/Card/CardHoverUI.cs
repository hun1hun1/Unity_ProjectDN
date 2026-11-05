using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CardUI))]
public class CardHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform hoverVisual;
    [SerializeField] private Canvas hoverCanvas;
    [SerializeField, Min(1f)] private float hoverScale = 1.2f;
    [SerializeField, Min(0f)] private float hoverDuration = 0.12f;
    [SerializeField] private int sortingOrderOffset = 10;
    [Header("Status Tooltip")]
    [SerializeField] private CardStatusTooltipUI statusTooltipPrefab;
    [SerializeField, Min(0f)] private float tooltipDelay = 1f;
    private CardStatusTooltipUI tooltip;
    private float hoverTime;
    private float hoverStartedAt = -1f;
    private bool tooltipShown;
    private CardUI card;
    private Vector3 baseScale;
    private Vector2 basePosition;
    private bool pointerInside;
    private bool initialized;
    private float progress;
    private readonly Vector3[] corners = new Vector3[4];

    private void Awake() { Initialize(); }

    private void Initialize()
    {
        if (initialized) return;
        card = GetComponent<CardUI>();
        if (hoverVisual != null)
        {
            baseScale = hoverVisual.localScale;
            basePosition = hoverVisual.anchoredPosition;
        }
        initialized = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        CloseTooltip();
        pointerInside = true;
        if (card != null && card.CanHover) hoverStartedAt = Time.unscaledTime;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        CloseTooltip();
    }

    public void ResetHover()
    {
        Initialize();
        pointerInside = false;
        CloseTooltip();
        progress = 0f;
        if (hoverVisual != null)
        {
            hoverVisual.localScale = baseScale;
            hoverVisual.anchoredPosition = basePosition;
        }
        if (hoverCanvas != null) hoverCanvas.overrideSorting = false;
    }

    private void OnDisable() { ResetHover(); }

    private void LateUpdate()
    {
        if (hoverVisual == null || card == null) return;
        bool available = card.CanHover;
        if (pointerInside && available)
        {
            if (hoverStartedAt < 0f) hoverStartedAt = Time.unscaledTime;
            hoverTime = Time.unscaledTime - hoverStartedAt;
        }
        else CloseTooltip();
        float target = pointerInside && available ? 1f : 0f;
        progress = available && hoverDuration > 0f
            ? Mathf.MoveTowards(progress, target, Time.unscaledDeltaTime / hoverDuration)
            : target;
        Canvas canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        float targetScale = CardHoverSizing.GetTargetScale(hoverVisual, card.DescriptionText, canvas,
            baseScale, hoverScale, 16f);
        float scale = Mathf.Lerp(1f, targetScale, Mathf.SmoothStep(0f, 1f, progress));
        hoverVisual.localScale = baseScale * scale;
        hoverVisual.anchoredPosition = basePosition;
        if (hoverCanvas != null) hoverCanvas.overrideSorting = progress > 0f;
        if (progress <= 0f) return;

        // Presentation stays in the hand; sorting changes never reorder layout children.
        if (canvas == null) return;
        if (hoverCanvas != null)
        {
            hoverCanvas.sortingLayerID = canvas.sortingLayerID;
            hoverCanvas.sortingOrder = Mathf.Clamp(canvas.sortingOrder + sortingOrderOffset, -32768, 32767);
        }
        RectTransform canvasRect = (RectTransform)canvas.transform;
        hoverVisual.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in corners)
        {
            Vector2 point = canvasRect.InverseTransformPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        Rect bounds = canvasRect.rect;
        Vector2 offset = Vector2.zero;
        if (min.x < bounds.xMin) offset.x = bounds.xMin - min.x;
        else if (max.x > bounds.xMax) offset.x = bounds.xMax - max.x;
        if (min.y < bounds.yMin) offset.y = bounds.yMin - min.y;
        else if (max.y > bounds.yMax) offset.y = bounds.yMax - max.y;
        Vector3 localOffset = hoverVisual.parent.InverseTransformVector(canvasRect.TransformVector(offset));
        hoverVisual.anchoredPosition = basePosition + (Vector2)localOffset;
        UpdateTooltip(canvas);
    }

    private void CloseTooltip()
    {
        hoverTime = 0f;
        hoverStartedAt = -1f;
        tooltipShown = false;
        if (tooltip != null) tooltip.Hide(this);
    }

    private void UpdateTooltip(Canvas canvas)
    {
        if (!pointerInside || !card.CanHover || hoverTime < tooltipDelay || progress < 1f) return;
        if (!tooltipShown)
        {
            string explanation = CardStatusDescriptionBuilder.BuildForCard(card.GetCardData());
            // Empty results are also cached until hover ends or the card is rebound.
            tooltipShown = true;
            if (string.IsNullOrEmpty(explanation) || statusTooltipPrefab == null) return;
            if (tooltip == null || tooltip.transform.parent != canvas.transform)
            {
                tooltip = canvas.GetComponentInChildren<CardStatusTooltipUI>(true);
                if (tooltip == null) tooltip = Instantiate(statusTooltipPrefab, canvas.transform, false);
            }
            tooltip.Show(this, hoverVisual, canvas, explanation,
                hoverCanvas != null ? hoverCanvas.sortingOrder : canvas.sortingOrder);
        }
        if (tooltip != null) tooltip.Move(this);
    }
}
