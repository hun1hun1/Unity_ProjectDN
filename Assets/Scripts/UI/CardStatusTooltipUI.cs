using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class CardStatusTooltipUI : MonoBehaviour
{
    [SerializeField] private TMP_Text contentText;
    [SerializeField, Min(1f)] private float preferredWidth = 320f;
    [SerializeField, Min(0f)] private float padding = 12f;
    [SerializeField, Min(0f)] private float cardGap = 12f;
    private CardHoverUI owner;
    private RectTransform cardVisual;
    private Canvas rootCanvas;
    private float measuredWidth = -1f;
    private float preferredHeight;
    private readonly Vector3[] corners = new Vector3[4];

    public void Show(CardHoverUI source, RectTransform visual, Canvas canvas,
        IReadOnlyList<StatusEffectData> statuses, int cardSortingOrder)
    {
        if (source == null || visual == null || canvas == null || contentText == null ||
            statuses == null || statuses.Count == 0) return;
        owner = source;
        cardVisual = visual;
        rootCanvas = canvas;
        var text = new StringBuilder();
        foreach (StatusEffectData status in statuses)
        {
            if (status == null) continue;
            if (text.Length > 0) text.Append("\n\n");
            text.Append("<b>").Append(status.DisplayName).Append("</b>\n").Append(status.Description);
        }
        contentText.text = text.ToString();
        measuredWidth = -1f;
        Canvas layer = GetComponent<Canvas>();
        layer.overrideSorting = true;
        layer.sortingLayerID = canvas.sortingLayerID;
        layer.sortingOrder = Mathf.Clamp(cardSortingOrder + 1, -32768, 32767);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Move(source);
    }

    public void Hide(CardHoverUI source)
    {
        if (owner != source) return;
        owner = null;
        gameObject.SetActive(false);
    }

    private void OnDisable() { owner = null; }

    public void Move(CardHoverUI source)
    {
        if (owner != source || cardVisual == null || rootCanvas == null || !gameObject.activeSelf) return;
        RectTransform canvas = (RectTransform)rootCanvas.transform;
        Rect bounds = canvas.rect;
        cardVisual.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in corners)
        {
            Vector2 point = canvas.InverseTransformPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        float leftSpace = Mathf.Max(0f, min.x - bounds.xMin - cardGap);
        float rightSpace = Mathf.Max(0f, bounds.xMax - max.x - cardGap);
        bool useRight = rightSpace >= preferredWidth || (leftSpace < preferredWidth && rightSpace >= leftSpace);
        float width = Mathf.Min(preferredWidth, Mathf.Max(1f, useRight ? rightSpace : leftSpace));
        if (Mathf.Abs(width - measuredWidth) > 0.1f)
        {
            measuredWidth = width;
            preferredHeight = contentText.GetPreferredValues(contentText.text,
                Mathf.Max(1f, width - padding * 2f), Mathf.Infinity).y + padding * 2f;
        }
        float height = Mathf.Min(preferredHeight, bounds.height);
        RectTransform panel = (RectTransform)transform;
        panel.sizeDelta = new Vector2(width, height);
        float x = useRight ? max.x + cardGap : min.x - cardGap - width;
        float y = Mathf.Clamp(max.y, bounds.yMin + height, bounds.yMax);
        panel.localPosition = new Vector3(Mathf.Clamp(x, bounds.xMin, bounds.xMax - width), y, 0f);
    }
}
