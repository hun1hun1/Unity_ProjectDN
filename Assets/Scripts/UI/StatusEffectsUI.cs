using System.Collections.Generic;
using UnityEngine;

public class StatusEffectsUI : MonoBehaviour
{
    [SerializeField] private StatusIconUI iconPrefab;
    [SerializeField] private StatusTooltipUI tooltipPrefab;
    private StatusTooltipUI tooltip;
    private StatusEffectSystem source;
    private readonly Dictionary<StatusEffectType, StatusIconUI> icons = new Dictionary<StatusEffectType, StatusIconUI>();

    public void Bind(StatusEffectSystem status)
    {
        Unsubscribe();
        source = status;
        if (isActiveAndEnabled && source != null) source.Changed += Refresh;
        Refresh();
    }

    private void OnEnable()
    {
        Unsubscribe();
        if (source != null) source.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() { Unsubscribe(); }
    private void Unsubscribe() { if (source != null) source.Changed -= Refresh; }

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

    private void Refresh()
    {
        var expired = new List<StatusEffectType>();
        foreach (var entry in icons)
            if (source == null || source.GetRemainingTurns(entry.Key) == 0) expired.Add(entry.Key);
        foreach (StatusEffectType type in expired)
        {
            StatusIconUI icon = icons[type];
            icons.Remove(type);
            if (icon == null) continue;
            icon.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(icon.gameObject);
            else DestroyImmediate(icon.gameObject);
        }
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
