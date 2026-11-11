using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

internal sealed class CardUpgradeComparisonUI
{
    private static readonly Color Ivory = new Color(0.95f, 0.92f, 0.82f);
    private static readonly Color Gold = new Color(1f, 0.83f, 0.48f);

    private readonly RectTransform comparison;
    private readonly RectTransform originalSlot;
    private readonly RectTransform resultSlot;
    private readonly DeckCardUI original;
    private readonly DeckCardUI result;
    private readonly Vector2 cardSize;
    private readonly TMP_Text message;

    internal Button ListCancel { get; }
    internal Button ComparisonCancel { get; }
    internal bool IsComparing => comparison.gameObject.activeSelf;

    internal CardUpgradeComparisonUI(RectTransform panel, TMP_FontAsset font, DeckCardUI prefab,
        UnityAction close, UnityAction back, UnityAction confirm)
    {
        Sprite cancelArtwork = Resources.Load<Sprite>("CardUpgradeCancel");
        Sprite confirmArtwork = Resources.Load<Sprite>("CardUpgradeConfirm");
        var upperLeft = new Vector2(0f, 1f);
        ListCancel = MakeButton(panel, "UpgradeListCancel", "취소", font, upperLeft, upperLeft, cancelArtwork, close);
        var cancelRect = (RectTransform)ListCancel.transform;
        cancelRect.pivot = upperLeft;
        cancelRect.sizeDelta = new Vector2(132f, 52f);
        cancelRect.anchoredPosition = new Vector2(28f, -20f);

        comparison = MakeRect(panel, "UpgradeComparison", Vector2.zero, Vector2.one);
        comparison.gameObject.SetActive(false);
        originalSlot = MakeRect(comparison, "OriginalSlot", new Vector2(.1f, .18f), new Vector2(.46f, .8f));
        resultSlot = MakeRect(comparison, "ResultSlot", new Vector2(.54f, .18f), new Vector2(.9f, .8f));
        cardSize = ((RectTransform)prefab.transform).rect.size;
        original = MakePreview(originalSlot, prefab);
        result = MakePreview(resultSlot, prefab);

        MakeText(comparison, "OriginalTitle", "강화 전", font, 28f, Ivory, new Vector2(.1f, .84f), new Vector2(.46f, .92f));
        MakeText(comparison, "ResultTitle", "강화 후", font, 28f, Gold, new Vector2(.54f, .84f), new Vector2(.9f, .92f));
        MakeText(comparison, "Direction", ">", font, 64f, Gold, new Vector2(.46f, .18f), new Vector2(.54f, .8f));
        message = MakeText(comparison, "UpgradeMessage", "", font, 24f, new Color(1f, .71f, .55f),
            new Vector2(.14f, .127f), new Vector2(.86f, .178f));

        ComparisonCancel = MakeButton(comparison, "UpgradeComparisonCancel", "취소", font,
            new Vector2(.27f, .045f), new Vector2(.48f, .12f), cancelArtwork, back);
        MakeButton(comparison, "UpgradeConfirm", "강화하기", font,
            new Vector2(.52f, .045f), new Vector2(.73f, .12f), confirmArtwork, confirm);
        ListCancel.gameObject.SetActive(false);
    }

    internal void ShowComparison(CardData before, CardData after)
    {
        original.SetCard(before, applyStatusModifiers: false);
        result.SetCard(after, applyStatusModifiers: false);
        message.text = string.Empty;
        ListCancel.gameObject.SetActive(false);
        comparison.gameObject.SetActive(true);
        ResizeCards();
    }

    internal void ShowList()
    {
        comparison.gameObject.SetActive(false);
        ListCancel.gameObject.SetActive(true);
    }

    internal void Hide()
    {
        comparison.gameObject.SetActive(false);
        ListCancel.gameObject.SetActive(false);
    }

    internal void ShowFailure() => message.text = "강화에 실패했습니다. 다시 시도해 주세요.";

    internal void ResizeCards()
    {
        float scale = Mathf.Min(originalSlot.rect.width / cardSize.x, originalSlot.rect.height / cardSize.y);
        original.transform.localScale = result.transform.localScale = Vector3.one * scale;
    }

    private static DeckCardUI MakePreview(RectTransform slot, DeckCardUI prefab)
    {
        var card = Object.Instantiate(prefab, slot);
        var rect = (RectTransform)card.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = Vector2.zero;
        card.DisableInspection();
        if (card.TryGetComponent(out DeckCardHoverUI hover)) hover.enabled = false;
        return card;
    }

    private static RectTransform MakeRect(RectTransform parent, string name, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TMP_Text MakeText(RectTransform parent, string name, string text, TMP_FontAsset font,
        float size, Color color, Vector2 min, Vector2 max)
    {
        var label = MakeRect(parent, name, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static Button MakeButton(RectTransform parent, string name, string label, TMP_FontAsset font,
        Vector2 min, Vector2 max, Sprite artwork, UnityAction click)
    {
        var rect = MakeRect(parent, name, min, max);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = artwork;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 3f;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(click);
        MakeText(rect, "Label", label, font, 26f, Ivory, Vector2.zero, Vector2.one);
        return button;
    }
}
