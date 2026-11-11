using System.Collections;
using UnityEngine;

internal sealed class MapNodeEntrance
{
    private const float PanelDuration = 0.25f;
    private const float ShelfOffset = 24f;

    private readonly MonoBehaviour owner;
    private readonly CanvasGroup panelGroup;
    private readonly float panelAlpha;
    private readonly bool panelInteractable;
    private readonly bool panelBlocksRaycasts;
    private readonly RectTransform shelf;
    private readonly Vector2 shelfPosition;
    private readonly MapChoiceHighlight selection;
    private readonly UnityEngine.UI.RawImage glow;
    private readonly Texture2D glowTexture;
    private Coroutine animation;

    internal bool IsPlaying => animation != null;

    internal MapNodeEntrance(MonoBehaviour owner, GameObject panel, UnityEngine.UI.Button[] choices,
        int selectedIndex, Color highlight, RectTransform shelf, bool campfireGlow)
    {
        this.owner = owner;
        this.shelf = shelf;
        if (!panel.TryGetComponent(out CanvasGroup group)) group = panel.AddComponent<CanvasGroup>();
        panelGroup = group;
        panelAlpha = group.alpha;
        panelInteractable = group.interactable;
        panelBlocksRaycasts = group.blocksRaycasts;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        if (shelf != null)
        {
            shelfPosition = shelf.anchoredPosition;
            shelf.anchoredPosition = shelfPosition - Vector2.up * ShelfOffset;
        }

        selection = new MapChoiceHighlight(choices, selectedIndex, highlight);
        RectTransform icon = choices[selectedIndex].GetComponent<MapChoiceCardUI>()?.Artwork?.rectTransform;
        if (campfireGlow && icon != null)
        {
            glowTexture = CreateGlowTexture();
            var light = new GameObject("CampfireGlow", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            glow = light.GetComponent<UnityEngine.UI.RawImage>();
            glow.texture = glowTexture;
            glow.raycastTarget = false;
            RectTransform rect = glow.rectTransform;
            rect.SetParent(icon.parent, false);
            rect.SetSiblingIndex(icon.GetSiblingIndex());
            float diameter = Mathf.Max(icon.rect.width, icon.rect.height) * 1.35f;
            rect.sizeDelta = Vector2.one * diameter;
            rect.position = icon.TransformPoint(icon.rect.center + Vector2.up * icon.rect.height * 0.1f);
        }

        animation = owner.StartCoroutine(Animate());
    }

    internal void Stop()
    {
        if (!IsPlaying) return;
        owner.StopCoroutine(animation);
        Finish();
    }

    private IEnumerator Animate()
    {
        for (float elapsed = 0f; elapsed < MapChoiceHighlight.Duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = elapsed / MapChoiceHighlight.Duration;
            selection.SetStrength(1f - progress);
            SetGlow(Mathf.Lerp(0.62f, 1.15f, progress), Mathf.Lerp(0.5f, 0.88f, progress));
            yield return null;
        }

        for (float elapsed = 0f; elapsed < PanelDuration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / PanelDuration);
            panelGroup.alpha = panelAlpha * progress;
            if (shelf != null)
                shelf.anchoredPosition = shelfPosition - Vector2.up * (ShelfOffset * (1f - progress));
            SetGlow(Mathf.Lerp(1.15f, 1.5f, progress), 0.88f * (1f - progress));
            yield return null;
        }

        Finish();
    }

    private void SetGlow(float scale, float alpha)
    {
        if (glow == null) return;
        glow.rectTransform.localScale = Vector3.one * scale;
        glow.color = new Color(1f, 0.47f, 0.14f, alpha);
    }

    private void Finish()
    {
        selection.Restore();
        if (shelf != null) shelf.anchoredPosition = shelfPosition;
        if (panelGroup != null)
        {
            panelGroup.alpha = panelAlpha;
            panelGroup.interactable = panelInteractable;
            panelGroup.blocksRaycasts = panelBlocksRaycasts;
        }
        if (glow != null)
        {
            glow.gameObject.SetActive(false);
            Object.Destroy(glow.gameObject);
        }
        if (glowTexture != null) Object.Destroy(glowTexture);
        animation = null;
    }

    private static Texture2D CreateGlowTexture()
    {
        const int size = 48;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var offset = new Vector2(x, y) * (2f / (size - 1)) - Vector2.one;
                float falloff = Mathf.Clamp01(1f - offset.sqrMagnitude);
                pixels[y * size + x] = new Color(1f, 1f, 1f, falloff * falloff * falloff);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
