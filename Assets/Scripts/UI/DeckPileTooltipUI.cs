using TMPro;
using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class DeckPileTooltipUI : MonoBehaviour
{
    [SerializeField] private TMP_Text contentText;
    [SerializeField, Min(1f)] private float preferredWidth = 320f;
    [SerializeField, Min(0f)] private float padding = 12f;
    [SerializeField, Min(0f)] private float pileGap = 12f;

    private DeckPileHoverUI owner;
    private RectTransform pileVisual;
    private Canvas rootCanvas;
    private float measuredWidth = -1f;
    private float preferredHeight;
    private readonly Vector3[] corners = new Vector3[4];

    public void Show(DeckPileHoverUI source, RectTransform visual, Canvas canvas, string explanation)
    {
        if (source == null || visual == null || canvas == null || contentText == null ||
            string.IsNullOrWhiteSpace(explanation)) return;
        owner = source;
        pileVisual = visual;
        rootCanvas = canvas;
        contentText.text = explanation;
        measuredWidth = -1f;
        Canvas layer = GetComponent<Canvas>();
        layer.overrideSorting = true;
        layer.sortingLayerID = canvas.sortingLayerID;
        layer.sortingOrder = Mathf.Clamp(canvas.sortingOrder + 11, -32768, 32767);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Move(source);
    }

    public void Hide(DeckPileHoverUI source)
    {
        if (owner != source) return;
        owner = null;
        gameObject.SetActive(false);
    }

    private void OnDisable() { owner = null; }

    private void LateUpdate()
    {
        if (owner == null || !owner.isActiveAndEnabled) gameObject.SetActive(false);
    }

    public void Move(DeckPileHoverUI source)
    {
        if (owner != source || pileVisual == null || rootCanvas == null || !gameObject.activeSelf) return;
        RectTransform canvas = (RectTransform)rootCanvas.transform;
        Rect bounds = canvas.rect;
        pileVisual.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in corners)
        {
            Vector2 point = canvas.InverseTransformPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        float width = Mathf.Min(preferredWidth, Mathf.Max(1f, bounds.width));
        if (Mathf.Abs(width - measuredWidth) > 0.1f)
        {
            measuredWidth = width;
            preferredHeight = contentText.GetPreferredValues(contentText.text,
                Mathf.Max(1f, width - padding * 2f), Mathf.Infinity).y + padding * 2f;
        }
        float height = Mathf.Min(preferredHeight, bounds.height);
        RectTransform panel = (RectTransform)transform;
        panel.sizeDelta = new Vector2(width, height);
        float x = (min.x + max.x - width) * 0.5f;
        float y = max.y + pileGap + height;
        if (y > bounds.yMax) y = min.y - pileGap;
        panel.localPosition = new Vector3(Mathf.Clamp(x, bounds.xMin, bounds.xMax - width),
            Mathf.Clamp(y, bounds.yMin + height, bounds.yMax), 0f);
    }
}
