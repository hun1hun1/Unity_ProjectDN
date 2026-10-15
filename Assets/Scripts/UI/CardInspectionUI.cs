using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster))]
public class CardInspectionUI : MonoBehaviour, IPointerClickHandler, IScrollHandler
{
    [SerializeField] private GameObject panel;
    [SerializeField] private DeckCardUI cardView;
    [SerializeField] private RectTransform statusPanel;
    [SerializeField] private TMP_Text statusText;
    [SerializeField, Min(120f)] private float preferredCardHeight = 520f;
    [SerializeField, Min(120f)] private float statusWidth = 320f;
    [SerializeField, Min(0f)] private float cardGap = 24f;
    [SerializeField, Min(0f)] private float screenMargin = 24f;
    [SerializeField, Min(0f)] private float statusPadding = 16f;
    [SerializeField] private int sortingOrder = 1000;

    private static CardInspectionUI instance;
    private static int blockedUntilFrame = -1;
    private readonly Dictionary<UnityEngine.UI.Selectable, bool> previousInteractions = new Dictionary<UnityEngine.UI.Selectable, bool>();
    private GameObject previousSelection;
    private bool inputCaptured;
    private bool restorePending;
    private Vector2 baseCardSize;
    private float appliedHeight = -1f;
    private float measuredWidth = -1f;
    private float measuredPadding = -1f;
    private float preferredStatusHeight;

    public bool IsOpen => panel != null && panel.activeInHierarchy;
    public static bool BlocksGameplayInput => MulliganSelectionUI.BlocksGameplayInput || Time.frameCount <= blockedUntilFrame ||
        (instance != null && (instance.IsOpen || instance.inputCaptured));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; blockedUntilFrame = -1; }

    private void Awake()
    {
        instance = this;
        Canvas layer = GetComponent<Canvas>();
        if (layer != null)
        {
            layer.sortingLayerID = layer.rootCanvas.sortingLayerID;
            layer.overrideSorting = true;
            layer.sortingOrder = Mathf.Clamp(Mathf.Max(sortingOrder, layer.rootCanvas.sortingOrder + 100), -32768, 32767);
        }
        if (cardView != null) baseCardSize = ((RectTransform)cardView.transform).rect.size;
        if (panel != null) panel.SetActive(false);
    }

    public static bool TryOpen(CardData data, BattleManager battle)
    {
        if (instance == null || !instance.isActiveAndEnabled || data == null || battle == null ||
            BlocksGameplayInput || battle.gameObject.scene != instance.gameObject.scene || !battle.CanInspectCards)
            return false;
        return instance.Open(data, battle);
    }

    private bool Open(CardData data, BattleManager battle)
    {
        if (panel == null || cardView == null || statusPanel == null || statusText == null) return false;
        foreach (CardHoverUI hover in FindObjectsByType<CardHoverUI>(FindObjectsSortMode.None)) hover.ResetHover();
        foreach (DeckCardHoverUI hover in FindObjectsByType<DeckCardHoverUI>(FindObjectsSortMode.None)) hover.ResetHover();
        cardView.SetCard(data, battle, applyStatusModifiers: false);
        statusText.text = CardStatusDescriptionBuilder.BuildForCard(data);
        statusPanel.gameObject.SetActive(!string.IsNullOrEmpty(statusText.text));
        measuredWidth = -1f;
        panel.SetActive(true);
        transform.SetAsLastSibling();
        inputCaptured = true;
        restorePending = false;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        previousInteractions.Clear();
        foreach (UnityEngine.UI.Selectable selectable in FindObjectsByType<UnityEngine.UI.Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (selectable.transform.IsChildOf(transform)) continue;
            previousInteractions[selectable] = selectable.interactable;
            selectable.interactable = false;
        }
        Canvas.ForceUpdateCanvases();
        RefreshLayout();
        return true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsOpen) return;
        if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)cardView.transform,
            eventData.position, eventData.pressEventCamera)) return;
        Close();
    }

    // The backdrop consumes wheel input instead of scrolling the pile behind it.
    public void OnScroll(PointerEventData eventData) { }

    public void Close()
    {
        if ((!IsOpen && !inputCaptured) || restorePending) return;
        if (panel != null) panel.SetActive(false);
        blockedUntilFrame = Time.frameCount;
        restorePending = inputCaptured;
    }

    private void Update()
    {
        if (!IsOpen && inputCaptured && !restorePending) Close();
        if (restorePending && Time.frameCount > blockedUntilFrame) RestoreInput();
    }

    private void RestoreInput()
    {
        foreach (var previous in previousInteractions)
            if (previous.Key != null) previous.Key.interactable = previous.Value;
        previousInteractions.Clear();
        if (inputCaptured && EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
        {
            UnityEngine.UI.Selectable selectable = previousSelection.GetComponent<UnityEngine.UI.Selectable>();
            if (selectable == null || selectable.interactable) EventSystem.current.SetSelectedGameObject(previousSelection);
        }
        previousSelection = null;
        inputCaptured = restorePending = false;
    }

    private void OnDisable() { Close(); RestoreInput(); }
    private void OnDestroy() { RestoreInput(); if (instance == this) instance = null; }
    private void LateUpdate() { if (IsOpen) RefreshLayout(); }

    private void RefreshLayout()
    {
        Rect bounds = ((RectTransform)panel.transform).rect;
        float availableWidth = Mathf.Max(1f, bounds.width - screenMargin * 2f);
        float availableHeight = Mathf.Max(1f, bounds.height - screenMargin * 2f);
        float aspect = baseCardSize.y > 0f ? baseCardSize.x / baseCardSize.y : 200f / 260f;
        bool hasStatuses = statusPanel.gameObject.activeSelf;
        float width = Mathf.Min(statusWidth, availableWidth * 0.35f);
        float sideCardWidth = (bounds.width * 0.5f - screenMargin - width - cardGap) * 2f;
        bool below = hasStatuses && sideCardWidth < 140f;
        float height = Mathf.Min(preferredCardHeight, availableHeight, availableWidth / aspect);
        if (hasStatuses) height = below ? Mathf.Min(height, availableHeight * 0.42f)
            : Mathf.Min(height, Mathf.Max(1f, sideCardWidth) / aspect);
        RectTransform card = (RectTransform)cardView.transform;
        card.anchoredPosition = Vector2.zero;
        card.localScale = Vector3.one;
        card.sizeDelta = new Vector2(height * aspect, height);
        if (!Mathf.Approximately(height, appliedHeight))
        {
            appliedHeight = height;
            cardView.ResizeForList(baseCardSize.y > 0f ? height / baseCardSize.y : 1f);
        }
        if (!hasStatuses) return;
        if (below) width = Mathf.Min(statusWidth, availableWidth);
        if (!Mathf.Approximately(width, measuredWidth) || !Mathf.Approximately(statusPadding, measuredPadding))
        {
            measuredWidth = width;
            measuredPadding = statusPadding;
            statusText.rectTransform.offsetMin = new Vector2(statusPadding, statusPadding);
            statusText.rectTransform.offsetMax = new Vector2(-statusPadding, -statusPadding);
            statusText.fontSize = statusText.fontSizeMax;
            preferredStatusHeight = statusText.GetPreferredValues(statusText.text,
                Mathf.Max(1f, width - statusPadding * 2f), Mathf.Infinity).y + statusPadding * 2f;
        }
        float maxHeight = below ? availableHeight * 0.5f - height * 0.5f - cardGap : availableHeight;
        float statusHeight = Mathf.Min(preferredStatusHeight, Mathf.Max(1f, maxHeight));
        statusPanel.sizeDelta = new Vector2(width, statusHeight);
        statusPanel.anchoredPosition = below ? new Vector2(0f, -height * 0.5f - cardGap - statusHeight * 0.5f)
            : new Vector2(card.rect.width * 0.5f + cardGap + width * 0.5f, 0f);
    }
}
