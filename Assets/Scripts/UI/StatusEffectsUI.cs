using System.Collections.Generic;
using UnityEngine;

public class StatusEffectsUI : MonoBehaviour
{
    [SerializeField] private StatusIconUI iconPrefab;
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
                icons[status.Type] = icon;
            }
            icon.SetStatus(status);
        }
    }
}
