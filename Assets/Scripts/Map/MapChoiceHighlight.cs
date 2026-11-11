using System.Collections.Generic;
using UnityEngine;

internal sealed class MapChoiceHighlight
{
    internal const float Duration = 0.15f;

    private readonly UnityEngine.UI.Graphic frame;
    private readonly Color frameColor;
    private readonly Color highlight;
    private readonly RectTransform icon;
    private readonly Vector3 iconScale;
    private readonly List<(CanvasGroup Group, float Alpha)> dimmed = new();

    internal MapChoiceHighlight(UnityEngine.UI.Button[] choices, int selectedIndex, Color highlight)
    {
        this.highlight = highlight;
        UnityEngine.UI.Button selected = choices[selectedIndex];
        frame = selected.targetGraphic;
        if (frame != null) frameColor = frame.color;
        icon = selected.GetComponent<MapChoiceCardUI>()?.Artwork?.rectTransform;
        if (icon != null) iconScale = icon.localScale;

        foreach (UnityEngine.UI.Button choice in choices)
        {
            if (choice == selected || !choice.gameObject.activeInHierarchy) continue;
            if (!choice.TryGetComponent(out CanvasGroup group))
                group = choice.gameObject.AddComponent<CanvasGroup>();
            dimmed.Add((group, group.alpha));
            group.alpha *= 0.65f;
        }
    }

    internal void SetStrength(float strength)
    {
        if (frame != null) frame.color = Color.Lerp(frameColor, highlight, strength);
        if (icon != null) icon.localScale = iconScale * (1f + 0.12f * strength);
    }

    internal void Restore()
    {
        if (frame != null) frame.color = frameColor;
        if (icon != null) icon.localScale = iconScale;
        foreach (var (group, alpha) in dimmed)
            if (group != null) group.alpha = alpha;
    }
}
