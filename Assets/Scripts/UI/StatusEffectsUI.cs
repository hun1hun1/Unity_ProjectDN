using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StatusEffectsUI : MonoBehaviour
{
    [SerializeField] private StatusIconUI iconPrefab;
    [SerializeField] private StatusTooltipUI tooltipPrefab;
    private static readonly Vector2 BackgroundPadding = new Vector2(4f, 4f);

    private StatusTooltipUI tooltip;
    private Image background;
    private bool backgroundLayoutDirty;
    private StatusEffectSystem source;
    private ContinuousCardSystem continuous;
    private DelayedEffectSystem delayed;
    private readonly Dictionary<long, StatusIconUI> delayedIcons = new Dictionary<long, StatusIconUI>();
    private readonly Dictionary<long, StatusIconUI> continuousIcons = new Dictionary<long, StatusIconUI>();
    private readonly Dictionary<StatusEffectType, StatusIconUI> icons = new Dictionary<StatusEffectType, StatusIconUI>();

    public void Bind(StatusEffectSystem status, ContinuousCardSystem continuousCards = null, DelayedEffectSystem delayedEffects = null)
    {
        Unsubscribe();
        // IDs are local to a system, so icons must not survive rebinding to another owner.
        if (continuous != continuousCards)
        {
            foreach (StatusIconUI icon in continuousIcons.Values) RemoveIcon(icon);
            continuousIcons.Clear();
        }
        if (delayed != delayedEffects)
        {
            foreach (StatusIconUI icon in delayedIcons.Values) RemoveIcon(icon);
            delayedIcons.Clear();
        }
        delayed = delayedEffects;
        source = status;
        continuous = continuousCards;
        Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        Unsubscribe();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
        foreach (StatusIconUI icon in delayedIcons.Values) RemoveIcon(icon);
        delayedIcons.Clear();
    }
    private void Subscribe()
    {
        if (!isActiveAndEnabled) return;
        if (source != null) source.Changed += Refresh;
        if (continuous != null) continuous.Changed += Refresh;
        if (delayed != null) delayed.Changed += Refresh;
    }

    private void Unsubscribe()
    {
        if (source != null) source.Changed -= Refresh;
        if (continuous != null) continuous.Changed -= Refresh;
        if (delayed != null) delayed.Changed -= Refresh;
    }

    private static void RemoveIcon(StatusIconUI icon)
    {
        if (icon == null) return;
        icon.CloseTooltip();
        icon.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(icon.gameObject);
        else DestroyImmediate(icon.gameObject);
    }

    private StatusTooltipUI GetOrCreateTooltip()
    {
        Canvas parent = GetComponentInParent<Canvas>();
        if (parent == null || tooltipPrefab == null) return null;
        Canvas root = parent.rootCanvas;
        if (tooltip != null && tooltip.transform.parent == root.transform) return tooltip;
        tooltip = root.GetComponentInChildren<StatusTooltipUI>(true);
        if (tooltip == null)
        {
            tooltip = Instantiate(tooltipPrefab, root.transform, false);
            tooltip.gameObject.SetActive(false);
        }
        return tooltip;
    }

    private void RefreshContinuousCards()
    {
        var present = new HashSet<long>();
        if (continuous != null && iconPrefab != null)
        {
            foreach (ActiveContinuousCard card in continuous.ActiveCards)
            {
                if (card.UsesDelayedLifetime && !card.HasMaintainedEffects && !card.HasTurnStartEffects) continue;
                present.Add(card.Id);
                if (!continuousIcons.TryGetValue(card.Id, out StatusIconUI icon) || icon == null)
                {
                    icon = Instantiate(iconPrefab, transform, false);
                    icon.BindTooltip(GetOrCreateTooltip());
                    continuousIcons[card.Id] = icon;
                }
                icon.SetContinuousCard(card);
            }
        }
        var expired = new List<long>();
        foreach (var entry in continuousIcons)
            if (!present.Contains(entry.Key)) expired.Add(entry.Key);
        foreach (long id in expired)
        {
            RemoveIcon(continuousIcons[id]);
            continuousIcons.Remove(id);
        }
    }

    private void RefreshDelayedEffects()
    {
        var present = new HashSet<long>();
        if (delayed != null && iconPrefab != null)
            foreach (PendingDelayedEffect entry in delayed.PendingEffects)
            {
                present.Add(entry.Id);
                if (!delayedIcons.TryGetValue(entry.Id, out StatusIconUI icon) || icon == null)
                {
                    icon = Instantiate(iconPrefab, transform, false);
                    icon.BindTooltip(GetOrCreateTooltip());
                    delayedIcons[entry.Id] = icon;
                }
                icon.SetDelayedEffect(entry);
            }
        var expired = new List<long>();
        foreach (var pair in delayedIcons) if (!present.Contains(pair.Key)) expired.Add(pair.Key);
        foreach (long id in expired) { RemoveIcon(delayedIcons[id]); delayedIcons.Remove(id); }
    }

    private void Refresh()
    {
        backgroundLayoutDirty = true;
        var expired = new List<StatusEffectType>();
        foreach (var entry in icons)
            if (source == null || !source.HasStatus(entry.Key)) expired.Add(entry.Key);
        foreach (StatusEffectType type in expired)
        {
            StatusIconUI icon = icons[type];
            icons.Remove(type);
            RemoveIcon(icon);
        }
        RefreshContinuousCards();
        RefreshDelayedEffects();
        if (source == null || iconPrefab == null) return;
        foreach (ActiveStatusEffect status in source.ActiveEffects)
        {
            if (status.Type == StatusEffectType.Burn) continue;

            if (!icons.TryGetValue(status.Type, out StatusIconUI icon) || icon == null)
            {
                icon = Instantiate(iconPrefab, transform, false);
                icon.BindTooltip(GetOrCreateTooltip());
                icons[status.Type] = icon;
            }
            icon.SetStatus(status);
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        backgroundLayoutDirty = true;
    }

    private void LateUpdate()
    {
        if (!backgroundLayoutDirty) return;
        backgroundLayoutDirty = false;
        if (transform is not RectTransform row) return;
        if (icons.Count == 0 && continuousIcons.Count == 0 && delayedIcons.Count == 0)
        {
            if (background != null) background.gameObject.SetActive(false);
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        Bounds? iconBounds = null;
        foreach (Transform child in row)
        {
            if (!child.gameObject.activeSelf || !child.TryGetComponent<StatusIconUI>(out _)) continue;

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(row, child);
            if (iconBounds.HasValue) bounds.Encapsulate(iconBounds.Value);
            iconBounds = bounds;
        }

        if (!iconBounds.HasValue)
        {
            if (background != null) background.gameObject.SetActive(false);
            return;
        }

        if (background == null) background = CreateBackground(row);
        RectTransform rect = background.rectTransform;
        rect.anchoredPosition = iconBounds.Value.center;
        rect.sizeDelta = (Vector2)iconBounds.Value.size + 2f * BackgroundPadding;
        background.gameObject.SetActive(true);
    }

    private Image CreateBackground(RectTransform row)
    {
        var panel = new GameObject("StatusBackground", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        panel.layer = gameObject.layer;
        panel.GetComponent<LayoutElement>().ignoreLayout = true;
        panel.transform.SetParent(row, false);
        panel.transform.SetAsFirstSibling();

        Image image = panel.GetComponent<Image>();
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = row.pivot;
        image.raycastTarget = false;
        if (tooltipPrefab != null && tooltipPrefab.TryGetComponent<Image>(out Image tooltipFrame))
            image.sprite = tooltipFrame.sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 8f;
        image.color = image.sprite != null
            ? new Color(1f, 1f, 1f, .94f)
            : new Color(.06f, .08f, .14f, .94f);
        return image;
    }
}
