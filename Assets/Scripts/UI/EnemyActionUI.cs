using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EnemyActionUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public const float CompactHeight = 56f;
    private const float IconSize = 48f;
    private const float DamageFontSize = 24f;
    private static readonly Color32 OutlineColor = new Color32(18, 15, 13, 255);
    private const float TooltipWidth = 400f;

    [SerializeField] private Image actionIcon;
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private Image background;
    [SerializeField] private EnemyActionIconSet fallbackIcons;
    [SerializeField] private StatusTooltipUI tooltipPrefab;

    private readonly List<EnemyIntentEntry> preview = new List<EnemyIntentEntry>();
    private StatusTooltipUI tooltip;
    private string tooltipText = "";

    private void Start()
    {
        if (actionIcon != null)
        {
            if (!actionIcon.TryGetComponent(out Outline outline))
                outline = actionIcon.gameObject.AddComponent<Outline>();
            outline.effectColor = OutlineColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        if (actionText != null && actionText.fontSharedMaterial != null)
        {
            actionText.outlineColor = OutlineColor;
            actionText.outlineWidth = .22f;
            actionText.fontMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            actionText.extraPadding = true;
        }
    }

    public void ValidateBindings()
    {
        if (actionIcon == null || actionText == null)
            throw new System.InvalidOperationException("Enemy intent HUD needs icon and text references.");
    }

    public void SetAction(EnemyAction action) => SetAction(action, null);

    public void SetAction(EnemyAction action, BattleManager battleManager)
    {
        if (action == null)
        {
            ClearAction();
            return;
        }

        preview.Clear();
        action.Preview(new EnemyActionPreviewContext(battleManager, preview));
        if (preview.Count == 0)
        {
            ClearAction();
            return;
        }

        long? totalDamage = null;
        var description = new StringBuilder("이번 턴에 적이 ");
        for (int i = 0; i < preview.Count; i++)
        {
            EnemyIntentEntry entry = preview[i];
            if (entry.Damage.HasValue) totalDamage = (totalDamage ?? 0) + entry.Damage.Value;
            if (i > 0) description.Append("\n이어서 ");
            description.Append(entry.Description).Append('.');
        }

        tooltipText = description.ToString();
        Sprite sprite = action.GetActionIcon();
        if (sprite == null && fallbackIcons != null) sprite = fallbackIcons.GetIcon(action.GetActionKinds());
        UpdateBadge(sprite, totalDamage?.ToString() ?? "");
        if (tooltip != null) tooltip.RefreshText(this, tooltipText);
    }

    private void UpdateBadge(Sprite icon, string damage)
    {
        bool showDamage = damage.Length > 0;
        float hoverWidth = 80f;
        if (actionIcon != null)
        {
            actionIcon.sprite = icon;
            actionIcon.enabled = icon != null;
            actionIcon.preserveAspect = true;
            actionIcon.raycastTarget = false;
            Place(actionIcon.rectTransform, showDamage ? new Vector2(-8f, 2f) : Vector2.zero, IconSize, IconSize);
        }
        if (actionText != null)
        {
            actionText.text = damage;
            actionText.enabled = showDamage;
            if (showDamage)
            {
                actionText.raycastTarget = false;
                actionText.enableAutoSizing = false;
                actionText.fontSize = DamageFontSize;
                actionText.textWrappingMode = TextWrappingModes.NoWrap;
                actionText.alignment = TextAlignmentOptions.Center;
                actionText.margin = Vector4.zero;
                float width = Mathf.Ceil(actionText.GetPreferredValues(damage, Mathf.Infinity, Mathf.Infinity).x) + 8f;
                float left = icon != null ? 11f : -width * .5f;
                Place(actionText.rectTransform, new Vector2(left + width * .5f, -12f), width, 30f);
                hoverWidth = Mathf.Max(hoverWidth, (left + width + 6f) * 2f);
            }
        }
        if (background != null)
        {
            Place(background.rectTransform, Vector2.zero, hoverWidth, CompactHeight);
            background.color = Color.clear;
            background.raycastTarget = true;
            background.enabled = true;
        }
    }

    private static void Place(RectTransform rect, Vector2 position, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = position;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(tooltipText) || background == null) return;
        tooltip = StatusTooltipUI.GetOrCreate(GetComponentInParent<Canvas>(), tooltipPrefab);
        if (tooltip != null) tooltip.ShowText(this, tooltipText, background.rectTransform, TooltipWidth);
    }

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();
    private void OnDisable() => HideTooltip();

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.Hide(this);
    }

    public void ClearAction()
    {
        preview.Clear();
        tooltipText = "";
        HideTooltip();
        if (actionIcon != null) { actionIcon.sprite = null; actionIcon.enabled = false; }
        if (actionText != null) { actionText.text = ""; actionText.enabled = false; }
        if (background != null) background.enabled = false;
    }
}
