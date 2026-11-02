using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.Button))]
public class ButtonHoverRotation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform visual;
    [SerializeField] private float hoverAngle = 60f;
    [SerializeField, Min(0f)] private float duration = 0.15f;

    private UnityEngine.UI.Button button;
    private Quaternion originalRotation;
    private bool pointerInside;
    private float progress;

    private void Awake()
    {
        button = GetComponent<UnityEngine.UI.Button>();
        if (visual != null) originalRotation = visual.localRotation;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = button != null && button.IsInteractable();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
    }

    private void LateUpdate()
    {
        if (visual == null) return;
        if (button == null || !button.IsInteractable() ||
            SettingsUI.BlocksBattleInput || CardInspectionUI.BlocksGameplayInput)
            pointerInside = false;

        float target = pointerInside ? 1f : 0f;
        progress = duration > 0f
            ? Mathf.MoveTowards(progress, target, Time.unscaledDeltaTime / duration)
            : target;
        float angle = hoverAngle * Mathf.SmoothStep(0f, 1f, progress);
        visual.localRotation = originalRotation * Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDisable()
    {
        pointerInside = false;
        progress = 0f;
        if (visual != null) visual.localRotation = originalRotation;
    }
}
