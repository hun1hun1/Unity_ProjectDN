using System.Collections.Generic;
using UnityEngine;

public class StatusEffectsUI : MonoBehaviour
{
    [SerializeField] private StatusIconUI iconPrefab;
    [SerializeField] private StatusTooltipUI tooltipPrefab;
    private StatusTooltipUI tooltip;
    private StatusEffectSystem source;
    private ContinuousCardSystem continuous;
    private readonly Dictionary<long, StatusIconUI> continuousIcons = new Dictionary<long, StatusIconUI>();
    private readonly Dictionary<StatusEffectType, StatusIconUI> icons = new Dictionary<StatusEffectType, StatusIconUI>();

    public void Bind(StatusEffectSystem status, ContinuousCardSystem continuousCards = null)
    {
        Unsubscribe();
        // IDs are local to a system, so icons must not survive rebinding to another owner.
        if (continuous != continuousCards)
        {
            foreach (StatusIconUI icon in continuousIcons.Values) RemoveIcon(icon);
            continuousIcons.Clear();
        }
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

    private void OnDisable() { Unsubscribe(); }
    private void Subscribe()
    {
        if (!isActiveAndEnabled) return;
        if (source != null) source.Changed += Refresh;
        if (continuous != null) continuous.Changed += Refresh;
    }

    private void Unsubscribe()
    {
        if (source != null) source.Changed -= Refresh;
        if (continuous != null) continuous.Changed -= Refresh;
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

    private void Refresh()
    {
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
        if (source == null || iconPrefab == null) return;
        foreach (ActiveStatusEffect status in source.ActiveEffects)
        {
            if (!icons.TryGetValue(status.Type, out StatusIconUI icon) || icon == null)
            {
                icon = Instantiate(iconPrefab, transform, false);
                icon.BindTooltip(GetOrCreateTooltip());
                icons[status.Type] = icon;
            }
            icon.SetStatus(status);
        }
    }
}
