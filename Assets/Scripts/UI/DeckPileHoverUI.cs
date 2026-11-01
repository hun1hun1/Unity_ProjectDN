using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(UnityEngine.UI.Button))]
public class DeckPileHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform hoverVisual;
    [SerializeField] private Canvas hoverCanvas;
    [SerializeField, Min(1f)] private float hoverScale = 1.1f;
    [SerializeField, Min(0f)] private float hoverDuration = 0.12f;
    [SerializeField, TextArea(3, 10)] private string tooltipText;
    [SerializeField] private DeckPileTooltipUI tooltipPrefab;
    [SerializeField, Min(0f)] private float tooltipDelay = 0.2f;
    [SerializeField] private bool allowWhileDeckOverviewOpen;

    private UnityEngine.UI.Button button;
    private DeckPileTooltipUI tooltip;
    private Vector3 baseScale;
    private bool pointerInside;
    private bool tooltipShown;
    private float enteredAt;
    private float progress;

    private void Awake()
    {
        button = GetComponent<UnityEngine.UI.Button>();
        baseScale = hoverVisual != null ? hoverVisual.localScale : Vector3.one;
    }

    private void OnEnable()
    {
        if (button == null) button = GetComponent<UnityEngine.UI.Button>();
        button.onClick.AddListener(ResetHover);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanHover()) return;
        pointerInside = true;
        enteredAt = Time.unscaledTime;
        tooltipShown = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        HideTooltip();
    }

    private bool CanHover() => isActiveAndEnabled && button != null && button.IsInteractable() &&
        !CardInspectionUI.BlocksGameplayInput && !SettingsUI.BlocksBattleInput && (allowWhileDeckOverviewOpen || !DeckOverviewUI.BlocksBattleInput);

    private void LateUpdate()
    {
        if (!CanHover()) { ResetHover(); return; }
        float target = pointerInside ? 1f : 0f;
        progress = hoverDuration > 0f
            ? Mathf.MoveTowards(progress, target, Time.unscaledDeltaTime / hoverDuration) : target;
        if (hoverVisual != null)
            hoverVisual.localScale = baseScale * Mathf.Lerp(1f, Mathf.Max(1f, hoverScale), Mathf.SmoothStep(0f, 1f, progress));

        Canvas root = GetComponentInParent<Canvas>()?.rootCanvas;
        if (hoverCanvas != null)
        {
            hoverCanvas.overrideSorting = progress > 0f;
            if (root != null)
            {
                hoverCanvas.sortingLayerID = root.sortingLayerID;
                hoverCanvas.sortingOrder = Mathf.Clamp(root.sortingOrder + 10, -32768, 32767);
            }
        }
        if (!pointerInside || root == null) return;
        if (!tooltipShown && Time.unscaledTime - enteredAt >= tooltipDelay &&
            !string.IsNullOrWhiteSpace(tooltipText) && tooltipPrefab != null)
        {
            if (tooltip == null || tooltip.transform.parent != root.transform)
            {
                tooltip = null;
                foreach (DeckPileTooltipUI candidate in root.GetComponentsInChildren<DeckPileTooltipUI>(true))
                {
                    tooltip = candidate;
                    break;
                }
                if (tooltip == null) tooltip = Instantiate(tooltipPrefab, root.transform, false);
            }
            tooltip.Show(this, hoverVisual != null ? hoverVisual : (RectTransform)transform, root, tooltipText);
            tooltipShown = true;
        }
        if (tooltipShown && tooltip != null) tooltip.Move(this);
    }

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.Hide(this);
        tooltipShown = false;
    }

    public void ResetHover()
    {
        pointerInside = false;
        progress = 0f;
        if (hoverVisual != null) hoverVisual.localScale = baseScale;
        if (hoverCanvas != null) hoverCanvas.overrideSorting = false;
        HideTooltip();
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(ResetHover);
        ResetHover();
    }
}
