using UnityEngine;
using UnityEngine.EventSystems;

internal sealed class ConduitIconHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float HoverScale = 1.10f;
    private const float HoverDuration = 0.12f;
    private const float TooltipDelay = 0.2f;

    private UnityEngine.UI.Image artwork;
    private StatusTooltipUI tooltipPrefab;
    private StatusTooltipUI tooltip;
    private AmmoCapacityRelic conduit;
    private bool pointerInside, tooltipShown;
    private float progress, enteredAt;

    internal void Initialize(UnityEngine.UI.Image image, StatusTooltipUI prefab)
    {
        artwork = image;
        tooltipPrefab = prefab;
    }

    internal void Bind(AmmoCapacityRelic value)
    {
        if (value != conduit) ResetHover();
        conduit = value;
        artwork.sprite = conduit != null ? conduit.HudArtwork : null;
        if (conduit != null) gameObject.name = conduit.name;
        gameObject.SetActive(conduit != null);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanHover()) return;
        pointerInside = true;
        enteredAt = Time.unscaledTime;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        HideTooltip();
    }

    private bool CanHover() => conduit != null && !CardInspectionUI.BlocksGameplayInput &&
        !SettingsUI.BlocksBattleInput && !DeckOverviewUI.BlocksBattleInput &&
        RunSession.Instance?.BlocksSceneInput != true;

    private void LateUpdate()
    {
        if (!pointerInside && progress <= 0f) return;
        if (!CanHover()) { ResetHover(); return; }

        progress = Mathf.MoveTowards(progress, pointerInside ? 1f : 0f, Time.unscaledDeltaTime / HoverDuration);
        artwork.rectTransform.localScale = Vector3.one * Mathf.SmoothStep(1f, HoverScale, progress);

        if (!pointerInside || tooltipShown || Time.unscaledTime - enteredAt < TooltipDelay) return;
        if (tooltip == null) tooltip = StatusTooltipUI.GetOrCreate(GetComponentInParent<Canvas>(), tooltipPrefab);
        if (tooltip == null) return;
        tooltip.ShowConduit(this, conduit, artwork.rectTransform);
        tooltipShown = true;
    }

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.Hide(this);
        tooltipShown = false;
    }

    private void ResetHover()
    {
        pointerInside = false;
        progress = 0f;
        if (artwork != null) artwork.rectTransform.localScale = Vector3.one;
        HideTooltip();
    }

    private void OnDisable() => ResetHover();
}
