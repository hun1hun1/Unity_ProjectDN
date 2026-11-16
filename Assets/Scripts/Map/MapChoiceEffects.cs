using UnityEngine;
using UnityEngine.UI;

internal sealed class MapChoiceEffects
{
    private static readonly Color FogColor = new Color(.34f, .49f, .80f);
    private static readonly Color CoverColor = new Color(.035f, .065f, .13f);
    private static readonly Color Blue = new Color(.31f, .56f, 1f);
    private static readonly Color Gold = new Color(1f, .75f, .35f);
    private readonly Image artwork;
    private RectTransform root;
    private Image cover;
    private RawImage mist;
    private RawImage light;
    private RawImage lightCore;
    private Texture2D lightTexture;
    private Outline rim;

    internal MapChoiceEffects(Image artwork) => this.artwork = artwork;

    internal void SetMist(float coverAmount, float drift, float opacity, bool behindArtwork)
    {
        EnsureRoot();
        if (mist == null)
        {
            cover = AddLayer<Image>("UnknownCover");
            Stretch(cover.rectTransform);
            mist = AddLayer<RawImage>("UnknownMist");
            Stretch(mist.rectTransform);
            mist.texture = Resources.Load<Texture2D>("SceneTransitionFog");
        }

        SetLayerOrder(behindArtwork);
        cover.enabled = coverAmount > 0f;
        cover.color = Alpha(CoverColor, coverAmount);
        mist.enabled = mist.texture != null;
        mist.uvRect = new Rect(.13f + .43f * drift, .07f, .65f, .84f);
        mist.color = Alpha(FogColor, opacity);
    }

    internal void SetConduitRim(float strength)
    {
        if (rim == null && strength > 0f)
        {
            rim = artwork.gameObject.AddComponent<Outline>();
            rim.effectDistance = new Vector2(2.1f, -2.1f);
        }
        if (rim == null) return;
        rim.enabled = strength > 0f && artwork.enabled;
        rim.effectColor = Alpha(Gold, strength * .56f);
    }

    internal void SetConduitLight(float progress)
    {
        EnsureRoot();
        if (light == null)
        {
            lightTexture = MapNodeEntrance.CreateGlowTexture();
            light = AddLayer<RawImage>("ConduitLight");
            light.texture = lightTexture;
            lightCore = AddLayer<RawImage>("ConduitLightCore");
            lightCore.texture = lightTexture;
        }

        SetLayerOrder(false);
        float intensity = Mathf.Sin(progress * Mathf.PI);
        float travel = Mathf.SmoothStep(0f, 1f, progress);
        Vector2 point = Vector2.Lerp(new Vector2(.23f, .33f), new Vector2(.77f, .68f), travel);
        Color beam = Color.Lerp(Blue, Gold, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, .68f, travel)));
        DrawLight(light, point, new Vector2(156f, 53f), beam, intensity * .52f);
        DrawLight(lightCore, point + new Vector2(.065f, .037f), new Vector2(104f, 22f),
            Color.Lerp(beam, Color.white, .26f), intensity * .90f);
        SetConduitRim(intensity * Mathf.SmoothStep(.2f, 1f, travel));
    }

    private static void DrawLight(RawImage image, Vector2 point, Vector2 size, Color color, float opacity)
    {
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = point;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, 28f);
        rect.anchoredPosition = Vector2.zero;
        image.color = Alpha(color, opacity);
    }

    private void SetLayerOrder(bool behindArtwork)
    {
        int layerIndex = behindArtwork ? 0 : root.parent.childCount - 1;
        if (root.GetSiblingIndex() != layerIndex) root.SetSiblingIndex(layerIndex);
        if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
    }

    private void EnsureRoot()
    {
        if (root != null) return;
        root = new GameObject("NodeCardEffects", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        root.SetParent(artwork.transform.parent, false);
        Stretch(root);
    }

    private T AddLayer<T>(string name) where T : Graphic
    {
        T graphic = new GameObject(name, typeof(RectTransform), typeof(T)).GetComponent<T>();
        graphic.rectTransform.SetParent(root, false);
        graphic.raycastTarget = false;
        return graphic;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Color Alpha(Color color, float opacity)
    {
        color.a = opacity;
        return color;
    }

    internal void Clear()
    {
        if (root != null) root.gameObject.SetActive(false);
        if (cover != null) cover.enabled = false;
        if (mist != null) mist.enabled = false;
        if (light != null)
        {
            light.color = Color.clear;
            lightCore.color = Color.clear;
        }
        SetConduitRim(0f);
    }

    internal void Dispose()
    {
        if (root != null) Object.Destroy(root.gameObject);
        if (rim != null) Object.Destroy(rim);
        if (lightTexture != null) Object.Destroy(lightTexture);
    }
}
