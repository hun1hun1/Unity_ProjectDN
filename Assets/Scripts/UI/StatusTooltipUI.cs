using TMPro;
using UnityEngine;

public class StatusTooltipUI : MonoBehaviour
{
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private float preferredWidth = 320f;
    [SerializeField] private float padding = 12f;
    [SerializeField] private Vector2 pointerOffset = new Vector2(16f, -16f);
    private Behaviour owner;
    private Canvas canvas;
    private Vector2 screenPosition;
    private RectTransform anchor;

    internal static StatusTooltipUI GetOrCreate(Canvas parent, StatusTooltipUI prefab)
    {
        if (parent == null || prefab == null) return null;
        Canvas root = parent.rootCanvas;
        var tooltip = root.GetComponentInChildren<StatusTooltipUI>(true);
        if (tooltip != null) return tooltip;

        tooltip = Instantiate(prefab, root.transform, false);
        tooltip.gameObject.SetActive(false);
        return tooltip;
    }

    private bool Begin(Behaviour source, Vector2 position, RectTransform anchor = null)
    {
        if (source == null || contentText == null) return false;
        canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (canvas == null) return false;
        owner = source;
        screenPosition = position;
        this.anchor = anchor;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        return true;
    }

    internal void ShowConduit(ConduitIconHoverUI icon, ConduitData conduit, RectTransform anchor)
    {
        if (conduit == null || !Begin(icon, Vector2.zero, anchor)) return;
        contentText.text = $"{CardStatusDescriptionBuilder.FormatTitle(conduit.RelicName)}\n{conduit.Description}";
        UpdatePosition();
    }

    public void Show(StatusIconUI icon, ActiveStatusEffect effect, Vector2 position)
    {
        if (effect == null || !Begin(icon, position)) return;
        Refresh(icon, effect);
    }

    public void Refresh(StatusIconUI icon, ActiveStatusEffect effect)
    {
        if (owner != icon || effect == null || contentText == null) return;
        string statusAmount = effect.IsPermanent ? "전투 종료까지 지속" : $"남은 턴: {effect.RemainingTurns}";
        if (effect.Type == StatusEffectType.Burn) statusAmount = $"중첩: {effect.StackCount}";
        if (effect.Definition.Category == StatusEffectCategory.Buff)
        {
            if (effect.RemainingTurns == 0 && !effect.IsPermanent)
                statusAmount = "";
            if (effect.HasNextAttackBuff)
                statusAmount += (statusAmount.Length > 0 ? "\n" : "") + "다음 공격 시 소모";
            if (effect.HasNextIncomingAttackBuff)
                statusAmount += (statusAmount.Length > 0 ? "\n" : "") + "다음 공격을 받을 때 소모";
            if (effect.IsPending) statusAmount += (statusAmount.Length > 0 ? "\n" : "") + "다음 플레이어 턴에 발동";
            if (effect.IsActive && effect.Type != StatusEffectType.Ward)
                statusAmount = $"버프 수치: {effect.BuffPower}\n{statusAmount}";
        }
        contentText.text = $"{CardStatusDescriptionBuilder.FormatTitle(effect.Definition.DisplayName)}\n{effect.Definition.Description}\n{statusAmount}";
        UpdatePosition();
    }

    public void ShowContinuous(StatusIconUI icon, ActiveContinuousCard card, Vector2 position)
    {
        if (card == null || !Begin(icon, position)) return;
        RefreshContinuous(icon, card);
    }

    public void RefreshContinuous(StatusIconUI icon, ActiveContinuousCard card)
    {
        if (owner != icon || card == null || contentText == null) return;
        string duration = card.IsPermanent ? "전투 종료까지 지속" : $"남은 턴: {card.RemainingTurns}";
        string location = card.IsAbsorbed ? (card.UsesDelayedLifetime ? "흡수됨 · 마지막 지연 효과 발동 시 버림 더미로 복귀" : card.IsPermanent ? "흡수됨" : "흡수됨 · 만료 시 뽑을 카드 더미로 복귀") : "사용 시 소멸";
        contentText.text = $"{CardStatusDescriptionBuilder.FormatTitle(card.Card.cardName)}\n{CardDescriptionBuilder.Build(card.Card, null, false)}\n{duration}\n{location}";
        UpdatePosition();
    }

    public void ShowDelayed(StatusIconUI icon, PendingDelayedEffect entry, Vector2 position)
    {
        if (entry == null || !Begin(icon, position)) return;
        RefreshDelayed(icon, entry);
    }

    public void RefreshDelayed(StatusIconUI icon, PendingDelayedEffect entry)
    {
        if (owner != icon || entry == null || contentText == null) return;
        string title = entry.Card != null ? entry.Card.cardName : "지연 효과";
        string description = DelayedEffectDescriptionBuilder.Build(entry.Description, entry.EffectSnapshot, entry.SpentAmmo);
        contentText.text = $"{CardStatusDescriptionBuilder.FormatTitle(title)}\n{entry.RemainingTurns}턴 뒤 발동\n{description}\n실제 수치는 발동 당시 상태를 따릅니다.";
        UpdatePosition();
    }

    public void Move(StatusIconUI icon, Vector2 position)
    {
        if (owner != icon) return;
        screenPosition = position;
        UpdatePosition();
    }

    public void Hide(Behaviour icon)
    {
        if (owner != icon) return;
        owner = null;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        owner = null;
        anchor = null;
    }

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
        Vector2 target;
        if (anchor != null)
        {
            Vector3 bottomLeft = anchor.TransformPoint(anchor.rect.min);
            target = (Vector2)canvasRect.InverseTransformPoint(bottomLeft) + new Vector2(0f, -8f);
        }
        else
        {
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, camera, out Vector2 point)) return;
            target = point + pointerOffset;
            if (target.x + width > bounds.xMax) target.x = point.x - pointerOffset.x - width;
            if (target.y - height < bounds.yMin) target.y = point.y - pointerOffset.y + height;
        }
        target.x = Mathf.Clamp(target.x, bounds.xMin, bounds.xMax - width);
        target.y = Mathf.Clamp(target.y, bounds.yMin + height, bounds.yMax);
        // The prefab uses a top-left pivot and is a direct child of the root Canvas.
        panel.localPosition = new Vector3(target.x, target.y, 0f);
    }
}
