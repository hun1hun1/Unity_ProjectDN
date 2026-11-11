using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class RestActionButton : Button
{
    private static readonly Color HoverTextColor = new Color(1f, .94f, .73f);

    [SerializeField] private TMP_Text[] labels;

    private Color[] normalTextColors;
    private bool pointerInside;

    protected override void Awake()
    {
        base.Awake();
        normalTextColors = new Color[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            normalTextColors[i] = labels[i].color;
            // Use the renderer tint for both states so the hover can brighten the original text.
            labels[i].color = Color.white;
        }
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        base.OnPointerEnter(eventData);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        base.OnPointerExit(eventData);
    }

    protected override void OnDisable()
    {
        pointerInside = false;
        base.OnDisable();
        FadeLabels(false, 0f);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        // A mouse hover still takes effect when this button has keyboard focus.
        if (state == SelectionState.Selected && pointerInside) state = SelectionState.Highlighted;
        base.DoStateTransition(state, instant);
        if (gameObject.activeInHierarchy)
            FadeLabels(state == SelectionState.Highlighted, instant ? 0f : colors.fadeDuration);
    }

    private void FadeLabels(bool highlighted, float duration)
    {
        if (normalTextColors == null) return;
        for (int i = 0; i < labels.Length; i++)
        {
            Color color = highlighted ? HoverTextColor : normalTextColors[i];
            labels[i].CrossFadeColor(color, duration, ignoreTimeScale: true, useAlpha: false);
        }
    }
}
