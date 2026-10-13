using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class StatusIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [SerializeField] private UnityEngine.UI.Image icon;
    [SerializeField] private TMP_Text turnsText;
    public StatusEffectType StatusType { get; private set; }
    private StatusTooltipUI tooltip;
    private ActiveStatusEffect status;
    private bool hovered;

    public void BindTooltip(StatusTooltipUI value)
    {
        if (tooltip != null) tooltip.Hide(this);
        tooltip = value;
        hovered = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || status == null || tooltip == null) return;
        hovered = true;
        tooltip.Show(this, status, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (hovered && tooltip != null) tooltip.Move(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData) { CloseTooltip(); }
    private void OnDisable() { CloseTooltip(); }

    private void CloseTooltip()
    {
        hovered = false;
        if (tooltip != null) tooltip.Hide(this);
    }

    public void SetStatus(ActiveStatusEffect effect)
    {
        status = effect;
        StatusType = effect.Type;
        gameObject.name = effect.Definition.DisplayName;
        if (icon != null && effect.Definition.Icon != null) icon.sprite = effect.Definition.Icon;
        if (turnsText != null) turnsText.text = effect.RemainingTurns.ToString();
        if (hovered && tooltip != null) tooltip.Refresh(this, effect);
    }
}
