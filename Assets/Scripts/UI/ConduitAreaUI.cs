using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.HorizontalLayoutGroup))]
public sealed class ConduitAreaUI : MonoBehaviour
{
    private const float IconSize = 64f;
    private const float BackgroundPadding = 12f;
    private const float BackgroundHeight = 84f;
    private const float BackgroundOpacity = .30f;

    [SerializeField] private StatusTooltipUI tooltipPrefab;
    private readonly List<ConduitIconHoverUI> icons = new List<ConduitIconHoverUI>();
    private RectTransform background;

    private void OnEnable()
    {
        RunRelicInventory.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => RunRelicInventory.Changed -= Refresh;

    private void Refresh()
    {
        int visible = 0;
        foreach (AmmoCapacityRelic conduit in RunRelicInventory.InAcquisitionOrder)
        {
            if (conduit.HudArtwork == null) continue;

            ConduitIconHoverUI icon = visible < icons.Count ? icons[visible] : CreateIcon();
            icon.Bind(conduit);
            visible++;
        }

        for (int i = visible; i < icons.Count; i++)
            icons[i].Bind(null);

        UpdateBackground(visible);
    }

    private void UpdateBackground(int visible)
    {
        if (visible == 0)
        {
            if (background != null) background.gameObject.SetActive(false);
            return;
        }

        if (background == null)
        {
            var panel = new GameObject("ConduitBackground", typeof(RectTransform),
                typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
            panel.layer = gameObject.layer;
            panel.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            var image = panel.GetComponent<UnityEngine.UI.Image>();
            StatusEffectsUI.ApplyListBackgroundStyle(image, tooltipPrefab, BackgroundOpacity);

            background = image.rectTransform;
            background.SetParent(transform, false);
            background.SetAsFirstSibling();
            background.anchorMin = background.anchorMax = background.pivot = new Vector2(0f, .5f);
        }

        var layout = GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        float width = visible * IconSize + (visible - 1) * layout.spacing + 2f * BackgroundPadding;
        background.anchoredPosition = new Vector2(layout.padding.left - BackgroundPadding, 0f);
        background.sizeDelta = new Vector2(width, BackgroundHeight);
        background.gameObject.SetActive(true);
    }

    private ConduitIconHoverUI CreateIcon()
    {
        var slotObject = new GameObject("ConduitIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        slotObject.layer = gameObject.layer;
        var hitArea = slotObject.GetComponent<UnityEngine.UI.Image>();
        hitArea.rectTransform.SetParent(transform, false);
        hitArea.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);
        hitArea.color = Color.clear;

        var artworkObject = new GameObject("Artwork", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        artworkObject.layer = gameObject.layer;
        var artwork = artworkObject.GetComponent<UnityEngine.UI.Image>();
        var visual = artwork.rectTransform;
        visual.SetParent(hitArea.rectTransform, false);
        visual.anchorMin = Vector2.zero;
        visual.anchorMax = Vector2.one;
        visual.offsetMin = visual.offsetMax = Vector2.zero;
        artwork.preserveAspect = true;
        artwork.raycastTarget = false;

        var icon = slotObject.AddComponent<ConduitIconHoverUI>();
        icon.Initialize(artwork, tooltipPrefab);
        icons.Add(icon);
        return icon;
    }
}
