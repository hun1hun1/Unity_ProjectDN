using TMPro;
using UnityEngine;

internal static class CardHoverSizing
{
    internal static float GetTargetScale(RectTransform visual, TMP_Text description, Canvas canvas,
        Vector3 baseScale, float configuredScale, float minimumFontPixels, RectTransform lowerBoundary = null)
    {
        float preferredScale = Mathf.Max(1f, configuredScale);
        if (visual == null || description == null || canvas == null || description.fontSize <= 0f ||
            Mathf.Approximately(visual.localScale.x, 0f) || Mathf.Approximately(visual.localScale.y, 0f))
            return preferredScale;

        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        float baselineX = Mathf.Abs(baseScale.x / visual.localScale.x);
        float baselineY = Mathf.Abs(baseScale.y / visual.localScale.y);
        float fontPixels = ScreenLength(description.transform, Vector3.up * description.fontSize, camera) * baselineY;
        float width = ScreenLength(visual, Vector3.right * visual.rect.width, camera) * baselineX;
        float height = ScreenLength(visual, Vector3.up * visual.rect.height, camera) * baselineY;
        if (fontPixels <= 0f || width <= 0f || height <= 0f) return preferredScale;

        RectTransform canvasRect = (RectTransform)canvas.transform;
        Rect bounds = GetBounds(canvas, lowerBoundary);
        float availableWidth = ScreenLength(canvasRect, Vector3.right * bounds.width, camera);
        float availableHeight = ScreenLength(canvasRect, Vector3.up * bounds.height, camera);
        const float screenPadding = 16f;
        float fitScale = Mathf.Min(Mathf.Max(1f, availableWidth - screenPadding * 2f) / width,
            Mathf.Max(1f, availableHeight - screenPadding * 2f) / height);
        float readingScale = Mathf.Max(preferredScale, minimumFontPixels / fontPixels);
        return Mathf.Max(1f, Mathf.Min(readingScale, fitScale));
    }

    internal static Rect GetBounds(Canvas canvas, RectTransform lowerBoundary = null)
    {
        RectTransform canvasRect = (RectTransform)canvas.transform;
        Rect bounds = canvasRect.rect;
        if (lowerBoundary != null && lowerBoundary.gameObject.activeInHierarchy)
        {
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float pixelsPerUnit = ScreenLength(canvasRect, Vector3.up, camera);
            float margin = pixelsPerUnit > 0f ? 16f / pixelsPerUnit : 0f;
            Vector3 top = lowerBoundary.TransformPoint(new Vector3(0f, lowerBoundary.rect.yMax));
            bounds.yMin = Mathf.Clamp(canvasRect.InverseTransformPoint(top).y + margin, bounds.yMin, bounds.yMax);
        }
        return bounds;
    }

    private static float ScreenLength(Transform transform, Vector3 localVector, Camera camera)
    {
        Vector2 start = RectTransformUtility.WorldToScreenPoint(camera, transform.position);
        Vector2 end = RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(localVector));
        return Vector2.Distance(start, end);
    }
}
