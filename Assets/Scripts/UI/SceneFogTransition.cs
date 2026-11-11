using System.Collections;
using UnityEngine;

internal sealed class SceneFogTransition
{
    private const double LoadingHintDelay = 0.8;
    private const float CrescentPulseDuration = 1.2f;

    private static readonly Color CoverColor = new Color(0.035f, 0.048f, 0.09f);
    private static readonly Color BackFogColor = new Color(0.41f, 0.51f, 0.73f);
    private static readonly Color FrontFogColor = new Color(0.57f, 0.67f, 0.86f);

    private readonly GameObject root;
    private readonly UnityEngine.UI.Image cover;
    private readonly UnityEngine.UI.RawImage backFog;
    private readonly UnityEngine.UI.RawImage frontFog;
    private readonly CanvasGroup loadingHint;
    private readonly UnityEngine.UI.Image loadingCrescent;
    private float startTime;
    private float hintStartTime;

    internal bool IsVisible => root.activeInHierarchy;

    internal SceneFogTransition(Transform parent)
    {
        root = new GameObject("SceneFogTransition", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        root.transform.SetParent(parent, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        cover = AddLayer<UnityEngine.UI.Image>("Cover");
        cover.raycastTarget = true;
        Texture2D texture = Resources.Load<Texture2D>("SceneTransitionFog");
        backFog = AddFogLayer("BackFog", texture);
        frontFog = AddFogLayer("FrontFog", texture);

        var hintPrefab = Resources.Load<GameObject>("SceneLoadingIndicator");
        if (hintPrefab != null)
        {
            GameObject hint = Object.Instantiate(hintPrefab, root.transform, false);
            hint.name = hintPrefab.name;
            loadingHint = hint.GetComponent<CanvasGroup>();
            loadingCrescent = hint.GetComponentInChildren<UnityEngine.UI.Image>(true);
        }

        root.SetActive(false);
    }

    internal void Show()
    {
        startTime = Time.unscaledTime;
        if (loadingHint != null) loadingHint.gameObject.SetActive(false);
        SetCoverage(0f);
        root.SetActive(true);
    }

    internal void Hide() => root.SetActive(false);

    internal void UpdateLoading(double loadingSeconds)
    {
        if (loadingSeconds >= LoadingHintDelay && loadingHint != null && !loadingHint.gameObject.activeSelf)
        {
            hintStartTime = Time.unscaledTime;
            loadingHint.gameObject.SetActive(true);
        }
        SetCoverage(1f);
    }

    internal IEnumerator Fade(float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            SetCoverage(Mathf.SmoothStep(from, to, elapsed / duration));
            yield return null;
        }
        SetCoverage(to);
    }

    internal void SetCoverage(float coverage)
    {
        cover.color = WithAlpha(CoverColor, coverage);
        float elapsed = Time.unscaledTime - startTime;
        float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
        float tiles = aspect * 0.5f;
        backFog.uvRect = new Rect(Mathf.Repeat(0.17f - elapsed * 0.21f, 1f), 1f, tiles * 1.25f, -1f);
        frontFog.uvRect = new Rect(Mathf.Repeat(0.56f + elapsed * 0.36f, 1f), 0f, tiles, 1f);
        float opacity = Mathf.Sqrt(coverage);
        backFog.color = WithAlpha(BackFogColor, opacity * 0.62f);
        frontFog.color = WithAlpha(FrontFogColor, opacity * 0.82f);

        if (loadingHint != null && loadingHint.gameObject.activeSelf)
        {
            loadingHint.alpha = coverage;
            float pulseAngle = (Time.unscaledTime - hintStartTime) * (2f * Mathf.PI / CrescentPulseDuration);
            float pulse = (1f + Mathf.Cos(pulseAngle)) * 0.5f;
            loadingCrescent.color = WithAlpha(Color.white, Mathf.Lerp(0.55f, 1f, pulse));
        }
    }

    private UnityEngine.UI.RawImage AddFogLayer(string name, Texture2D texture)
    {
        var layer = AddLayer<UnityEngine.UI.RawImage>(name);
        layer.texture = texture;
        layer.raycastTarget = false;
        layer.enabled = texture != null;
        return layer;
    }

    private T AddLayer<T>(string name) where T : UnityEngine.UI.Graphic
    {
        var layer = new GameObject(name, typeof(RectTransform), typeof(T)).GetComponent<T>();
        RectTransform rect = layer.rectTransform;
        rect.SetParent(root.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return layer;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
