using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster))]
public class CardInspectionUI : MonoBehaviour, IPointerClickHandler, IScrollHandler
{
    [SerializeField] private GameObject panel;
    [SerializeField] private DeckOverviewUI mapDeckOverview;
    [SerializeField] private GameObject mapShopPanel;
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
    private RectTransform purchaseBar;
    private TMP_Text purchaseLabel;
    private TMP_Text purchaseMessage;
    private Func<string> confirmPurchase;

    public bool IsOpen => panel != null && panel.activeInHierarchy;
    public static bool BlocksGameplayInput => RunSession.Instance?.BlocksSceneInput == true ||
        MulliganSelectionUI.BlocksGameplayInput || Time.frameCount <= blockedUntilFrame ||
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
        if (instance == null || !instance.isActiveAndEnabled || data == null ||
            BlocksGameplayInput || SettingsUI.BlocksBattleInput)
            return false;
        if (battle == null)
        {
            bool deckOpen = instance.mapDeckOverview != null && instance.mapDeckOverview.IsOpen;
            bool shopOpen = instance.mapShopPanel != null && instance.mapShopPanel.activeInHierarchy;
            if (!deckOpen && !shopOpen) return false;
        }
        else if (battle.gameObject.scene != instance.gameObject.scene || !battle.CanInspectCards) return false;
        return instance.Open(data, battle);
    }

    internal static bool TryOpenShopOffer(CardData card, int price, TMP_FontAsset font, UnityEngine.UI.RawImage buttonArtwork,
        Func<string> confirm) => TryOpenShopAction(card, $"구매 · {price}골드", font, buttonArtwork, confirm);

    internal static bool TryOpenShopAction(CardData card, string label, TMP_FontAsset font,
        UnityEngine.UI.RawImage buttonArtwork, Func<string> confirm)
    {
        if (confirm == null || !TryOpen(card, null)) return false;
        instance.ShowPurchase(label, font, buttonArtwork, confirm);
        return true;
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

    private void ShowPurchase(string label, TMP_FontAsset font, UnityEngine.UI.RawImage buttonArtwork, Func<string> confirm)
    {
        if (purchaseBar == null)
        {
            var bar = new GameObject("ShopPurchase", typeof(RectTransform));
            bar.layer = panel.layer;
            purchaseBar = (RectTransform)bar.transform;
            purchaseBar.SetParent(panel.transform, false);
            purchaseBar.anchorMin = purchaseBar.anchorMax = new Vector2(0.5f, 0f);
            purchaseBar.pivot = new Vector2(0.5f, 0f);
            purchaseBar.sizeDelta = new Vector2(440f, 110f);
            purchaseBar.anchoredPosition = new Vector2(0f, screenMargin);

            var button = new GameObject("PurchaseButton", typeof(RectTransform), typeof(UnityEngine.UI.RawImage),
                typeof(UnityEngine.UI.Button));
            button.layer = panel.layer;
            var rect = (RectTransform)button.transform;
            rect.SetParent(purchaseBar, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240f, 54f);
            rect.anchoredPosition = new Vector2(0f, 20f);
            var image = button.GetComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = true;
            if (buttonArtwork != null && buttonArtwork.texture != null)
            {
                image.texture = buttonArtwork.texture;
                image.uvRect = buttonArtwork.uvRect;
            }
            else image.color = new Color(0.26f, 0.19f, 0.12f);
            var buy = button.GetComponent<UnityEngine.UI.Button>();
            buy.targetGraphic = image;
            buy.onClick.AddListener(Purchase);

            purchaseLabel = CreatePurchaseText("Label", rect, font, 25f, new Color(1f, 0.94f, 0.77f));
            purchaseLabel.rectTransform.anchorMin = Vector2.zero;
            purchaseLabel.rectTransform.anchorMax = Vector2.one;
            purchaseLabel.rectTransform.sizeDelta = Vector2.zero;

            purchaseMessage = CreatePurchaseText("PurchaseMessage", purchaseBar, font, 20f, new Color(1f, 0.74f, 0.58f));
            purchaseMessage.rectTransform.sizeDelta = new Vector2(440f, 32f);
            purchaseMessage.rectTransform.anchoredPosition = new Vector2(0f, -29f);
        }
        confirmPurchase = confirm;
        purchaseLabel.text = label;
        purchaseMessage.text = string.Empty;
        purchaseBar.gameObject.SetActive(true);
        RefreshLayout();
        Canvas.ForceUpdateCanvases();
    }

    private static TMP_Text CreatePurchaseText(string name, RectTransform parent, TMP_FontAsset font, float size, Color color)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.gameObject.layer = parent.gameObject.layer;
        label.rectTransform.SetParent(parent, false);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchoredPosition = Vector2.zero;
        label.font = font;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private void Purchase()
    {
        if (!IsOpen || confirmPurchase == null) return;
        string error = confirmPurchase();
        if (error == null) Close();
        else purchaseMessage.text = error;
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
        confirmPurchase = null;
        if (purchaseBar != null) purchaseBar.gameObject.SetActive(false);
        if (panel != null) panel.SetActive(false);
        blockedUntilFrame = Time.frameCount;
        restorePending = inputCaptured;
    }

    public static void CloseForBattleEnd()
    {
        if (instance == null) return;
        instance.Close();
        instance.RestoreInput();
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
        float purchaseSpace = confirmPurchase != null ? 124f : 0f;
        Vector2 contentOffset = Vector2.up * (purchaseSpace * 0.5f);
        float availableHeight = Mathf.Max(1f, bounds.height - screenMargin * 2f - purchaseSpace);
        float aspect = baseCardSize.y > 0f ? baseCardSize.x / baseCardSize.y : 200f / 260f;
        bool hasStatuses = statusPanel.gameObject.activeSelf;
        float width = Mathf.Min(statusWidth, availableWidth * 0.35f);
        float sideCardWidth = (bounds.width * 0.5f - screenMargin - width - cardGap) * 2f;
        bool below = hasStatuses && sideCardWidth < 140f;
        float height = Mathf.Min(preferredCardHeight, availableHeight, availableWidth / aspect);
        if (hasStatuses) height = below ? Mathf.Min(height, availableHeight * 0.42f)
            : Mathf.Min(height, Mathf.Max(1f, sideCardWidth) / aspect);
        RectTransform card = (RectTransform)cardView.transform;
        card.anchoredPosition = contentOffset;
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
        statusPanel.anchoredPosition = contentOffset + (below
            ? new Vector2(0f, -height * 0.5f - cardGap - statusHeight * 0.5f)
            : new Vector2(card.rect.width * 0.5f + cardGap + width * 0.5f, 0f));
    }
}
