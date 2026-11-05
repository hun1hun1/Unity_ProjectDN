using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(DeckCardUI))]
public class DeckCardHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform hoverVisual;
    [SerializeField] private Canvas hoverCanvas;
    [SerializeField, Min(1f)] private float hoverScale = 1.3f;
    [SerializeField, Min(0f)] private float hoverDuration = 0.12f;
    [SerializeField] private int sortingOrderOffset = 10;

    private Vector3 baseScale;
    private DeckCardUI card;
    private CardRewardUI rewardUI;
    private Vector2 basePosition;
    private bool initialized;
    private bool pointerInside;
    private float progress;
    private readonly Vector3[] corners = new Vector3[4];

    private void Awake() { Initialize(); }

    private void Initialize()
    {
        if (initialized) return;
        card = GetComponent<DeckCardUI>();
        rewardUI = GetComponentInParent<CardRewardUI>();
        if (hoverVisual != null)
        {
            baseScale = hoverVisual.localScale;
            basePosition = hoverVisual.anchoredPosition;
        }
        initialized = true;
    }

    public void OnPointerEnter(PointerEventData eventData) { if (!CardInspectionUI.BlocksGameplayInput && !SettingsUI.BlocksBattleInput) pointerInside = true; }
    public void OnPointerExit(PointerEventData eventData) { pointerInside = false; }

    public void ResetHover()
    {
        Initialize();
        pointerInside = false;
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
        if (CardInspectionUI.BlocksGameplayInput || SettingsUI.BlocksBattleInput) { ResetHover(); return; }
        if (hoverVisual == null) return;
        float target = pointerInside ? 1f : 0f;
        progress = hoverDuration > 0f
            ? Mathf.MoveTowards(progress, target, Time.unscaledDeltaTime / hoverDuration)
            : target;
        Canvas canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        RectTransform lowerBoundary = rewardUI != null ? rewardUI.HoverLowerBoundary : null;
        float targetScale = CardHoverSizing.GetTargetScale(hoverVisual, card.DescriptionText, canvas,
            baseScale, hoverScale, 16f, lowerBoundary);
        hoverVisual.localScale = baseScale * Mathf.Lerp(1f, targetScale, Mathf.SmoothStep(0f, 1f, progress));
        hoverVisual.anchoredPosition = basePosition;
        if (hoverCanvas != null) hoverCanvas.overrideSorting = progress > 0f;
        if (progress <= 0f) return;

        // Keep the grid's layout and hit area fixed; raise only the presentation.
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
        Rect bounds = CardHoverSizing.GetBounds(canvas, lowerBoundary);
        Vector2 offset = Vector2.zero;
        if (min.x < bounds.xMin) offset.x = bounds.xMin - min.x;
        else if (max.x > bounds.xMax) offset.x = bounds.xMax - max.x;
        if (min.y < bounds.yMin) offset.y = bounds.yMin - min.y;
        else if (max.y > bounds.yMax) offset.y = bounds.yMax - max.y;
        Vector3 localOffset = hoverVisual.parent.InverseTransformVector(canvasRect.TransformVector(offset));
        hoverVisual.anchoredPosition = basePosition + (Vector2)localOffset;
    }
}
