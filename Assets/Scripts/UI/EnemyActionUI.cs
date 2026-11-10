using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyActionUI : MonoBehaviour
{
    private const float MinimumPanelHeight = 88f;
    private const float VerticalPadding = 12f;

    [SerializeField]
    private Image actionIcon;

    [SerializeField]
    private TMP_Text actionText;

    [SerializeField]
    private Image background;

    public void ValidateBindings()
    {
        if (actionIcon == null || actionText == null)
            throw new System.InvalidOperationException("Enemy intent HUD needs icon and text references.");
    }

    public void SetAction(EnemyAction action)
    {
        SetAction(action, null);
    }

    public void SetAction(EnemyAction action, BattleManager battleManager)
    {
        if (action == null)
        {
            ClearAction();
            return;
        }

        if (actionIcon != null)
        {
            actionIcon.sprite = action.GetActionIcon();
            actionIcon.enabled = actionIcon.sprite != null;
        }
        if (actionText != null) actionText.text = action.Preview(new EnemyActionPreviewContext(battleManager, summarizeStatuses: true));
        if (background != null)
        {
            background.enabled = true;
            if (actionText != null) UpdateBackgroundSize();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (background != null && background.enabled && actionText != null)
            UpdateBackgroundSize();
    }

    private void UpdateBackgroundSize()
    {
        RectTransform textRect = actionText.rectTransform;
        float textWidth = Mathf.Max(1, textRect.rect.width);
        if (actionIcon != null)
        {
            RectTransform iconRect = actionIcon.rectTransform;
            float panelWidth = background.rectTransform.rect.width;
            float iconWidth = iconRect.rect.width;
            float availableWidth = Mathf.Max(1, panelWidth - iconWidth - 40);
            float preferredWidth = actionText.GetPreferredValues(actionText.text, Mathf.Infinity, Mathf.Infinity).x;
            textWidth = Mathf.Min(availableWidth, Mathf.Max(1, preferredWidth + 1));
            float left = (panelWidth - iconWidth - 8 - textWidth) / 2;

            iconRect.anchoredPosition = new Vector2(left, iconRect.anchoredPosition.y);
            textRect.anchorMin = new Vector2(0, textRect.anchorMin.y);
            textRect.anchorMax = new Vector2(0, textRect.anchorMax.y);
            textRect.pivot = new Vector2(0, textRect.pivot.y);
            textRect.anchoredPosition = new Vector2(left + iconWidth + 8, textRect.anchoredPosition.y);
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
        }
        float preferredHeight = actionText.GetPreferredValues(actionText.text, textWidth, Mathf.Infinity).y;
        float textHeight = Mathf.Max(56, preferredHeight);
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
        float panelHeight = Mathf.Max(MinimumPanelHeight, textHeight + VerticalPadding * 2f);
        background.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
    }

    public void ClearAction()
    {
        if (actionIcon != null) { actionIcon.sprite = null; actionIcon.enabled = false; }
        if (actionText != null) actionText.text = "";
        if (background != null) background.enabled = false;
    }
}
