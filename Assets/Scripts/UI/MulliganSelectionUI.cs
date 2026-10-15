using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class MulliganSelectionUI : MonoBehaviour
{
    private static MulliganSelectionUI current;
    private static int blockedUntilFrame = -1;
    public static bool BlocksGameplayInput => Time.frameCount <= blockedUntilFrame ||
        (current != null && current.gameObject.activeInHierarchy);
    private MulliganSystem owner;
    private TMP_Text countText;
    private UnityEngine.UI.Button complete;
    private readonly Dictionary<UnityEngine.UI.Selectable, bool> previous = new Dictionary<UnityEngine.UI.Selectable, bool>();
    private GameObject previousSelection;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { current = null; blockedUntilFrame = -1; }

    public static MulliganSelectionUI Create(Canvas canvas)
    {
        GameObject root = NewUI("MulliganSelectionCanvas", canvas.transform);
        Stretch((RectTransform)root.transform);
        Canvas layer = root.AddComponent<Canvas>(); layer.overrideSorting = true;
        layer.sortingLayerID = canvas.sortingLayerID; layer.sortingOrder = Mathf.Min(32767, canvas.sortingOrder + 200);
        root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        MulliganSelectionUI result = root.AddComponent<MulliganSelectionUI>();
        TMP_FontAsset font = null;
        foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>(true))
            if (label.font != null) { font = label.font; break; }
        GameObject panel = NewUI("MulliganPrompt", root.transform);
        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = new Vector2(0.15f, 1); rect.anchorMax = new Vector2(0.85f, 1);
        rect.pivot = new Vector2(0.5f, 1); rect.offsetMin = new Vector2(0, -112); rect.offsetMax = new Vector2(0, -24);
        var background = panel.AddComponent<UnityEngine.UI.Image>(); background.color = new Color(0.06f, 0.09f, 0.12f, 0.96f);
        TMP_Text title = Label("MulliganTitle", panel.transform, font, "멀리건할 카드를 선택하세요");
        Place(title.rectTransform, new Vector2(0, 0), new Vector2(0.58f, 1), new Vector2(14, 8), new Vector2(-6, -8));
        result.countText = Label("MulliganCount", panel.transform, font, "0/0");
        Place(result.countText.rectTransform, new Vector2(0.58f, 0), new Vector2(0.75f, 1), new Vector2(4, 8), new Vector2(-4, -8));
        GameObject button = NewUI("MulliganComplete", panel.transform);
        Place((RectTransform)button.transform, new Vector2(0.75f, 0), Vector2.one, new Vector2(8, 14), new Vector2(-14, -14));
        var image = button.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(0.15f, 0.7f, 0.3f);
        result.complete = button.AddComponent<UnityEngine.UI.Button>(); result.complete.targetGraphic = image;
        result.complete.onClick.AddListener(() => result.owner?.Confirm());
        TMP_Text caption = Label("MulliganCompleteText", button.transform, font, "완료"); Stretch(caption.rectTransform);
        root.SetActive(false);
        return result;
    }

    public void Show(MulliganSystem system)
    {
        owner = system; current = this;
        gameObject.SetActive(true); transform.SetAsLastSibling();
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        previous.Clear();
        foreach (UnityEngine.UI.Selectable control in GetComponentInParent<Canvas>().rootCanvas.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
        {
            if (control.transform.IsChildOf(transform)) continue;
            previous[control] = control.interactable; control.interactable = false;
        }
        Refresh();
    }

    public void Refresh()
    {
        if (owner == null) return;
        countText.text = $"{owner.SelectedCount}/{owner.RequiredCount}";
        complete.interactable = owner.SelectedCount == owner.RequiredCount;
    }

    public void Hide()
    {
        if (current == this) { current = null; blockedUntilFrame = Time.frameCount; }
        owner = null; RestoreInput(); gameObject.SetActive(false);
    }

    private void RestoreInput()
    {
        foreach (var entry in previous) if (entry.Key != null) entry.Key.interactable = entry.Value;
        previous.Clear();
        if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    private void OnDisable()
    {
        MulliganSystem request = owner; owner = null;
        if (current == this) current = null;
        RestoreInput(); request?.Cancel();
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var result = new GameObject(name, typeof(RectTransform)); result.layer = 5;
        result.transform.SetParent(parent, false); return result;
    }
    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text)
    {
        TMP_Text label = NewUI(name, parent).AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = label.fontSizeMax = 26;
        label.fontSizeMin = 14; label.enableAutoSizing = true; label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        return label;
    }
    private static void Stretch(RectTransform rect) => Place(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }
}
