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
    private ActiveContinuousCard continuousCard;
    public long ContinuousCardId => continuousCard != null ? continuousCard.Id : 0;
    private bool hovered;

    public void BindTooltip(StatusTooltipUI value)
    {
        if (tooltip != null) tooltip.Hide(this);
        tooltip = value;
        hovered = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || tooltip == null || (status == null && continuousCard == null)) return;
        hovered = true;
        if (continuousCard != null) tooltip.ShowContinuous(this, continuousCard, eventData.position);
        else tooltip.Show(this, status, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (hovered && tooltip != null) tooltip.Move(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData) { CloseTooltip(); }
    private void OnDisable() { CloseTooltip(); }

    public void CloseTooltip()
    {
        hovered = false;
        if (tooltip != null) tooltip.Hide(this);
    }

    public void SetContinuousCard(ActiveContinuousCard card)
    {
        status = null;
        continuousCard = card;
        gameObject.name = card.Card.cardName;
        if (icon != null)
        {
            Sprite sprite = card.Card.continuousIcon != null ? card.Card.continuousIcon : card.Card.artwork;
            if (sprite != null) icon.sprite = sprite;
        }
        if (turnsText != null) turnsText.text = card.IsPermanent ? "∞" : card.RemainingTurns.ToString();
        if (hovered && tooltip != null) tooltip.RefreshContinuous(this, card);
    }

    public void SetStatus(ActiveStatusEffect effect)
    {
        continuousCard = null;
        status = effect;
        StatusType = effect.Type;
        gameObject.name = effect.Definition.DisplayName;
        if (icon != null && effect.Definition.Icon != null) icon.sprite = effect.Definition.Icon;
        if (turnsText != null) turnsText.text = effect.IsPermanent ? "∞" : effect.RemainingTurns.ToString();
        if (hovered && tooltip != null) tooltip.Refresh(this, effect);
    }
}
