using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.Outline))]
public class ScreenPixelOutline : MonoBehaviour
{
    [SerializeField, Min(1f)]
    private float thicknessPixels = 2f;

    private UnityEngine.UI.Outline outline;
    private Canvas rootCanvas;

    private void OnEnable()
    {
        outline = GetComponent<UnityEngine.UI.Outline>();

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        rootCanvas = parentCanvas != null
            ? parentCanvas.rootCanvas
            : null;
    }

    private void LateUpdate()
    {
        if (rootCanvas == null || rootCanvas.scaleFactor <= 0f)
            return;

        float distance = thicknessPixels / rootCanvas.scaleFactor;

        outline.effectDistance = new Vector2(distance, -distance);
    }
}
