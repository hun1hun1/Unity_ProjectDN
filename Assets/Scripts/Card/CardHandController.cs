using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardHandController : MonoBehaviour
{
    [SerializeField] private CardUI cardPrefab;

    [SerializeField] private int maxHandSize = 3;

    private List<CardUI> cards = new List<CardUI>();
    private bool handLayoutDirty = true;
    public BattleManager BattleContext { get; private set; }

    [SerializeField] private RectTransform drawStartPoint;
    [SerializeField] private RectTransform cardAnimationLayer;

    [Header("Discard Animation")]
    [SerializeField] private RectTransform discardPilePoint;

    [Header("Drag References")]
    [SerializeField] private RectTransform battleArea;

    private void OnEnable() => handLayoutDirty = true;

    private void OnRectTransformDimensionsChange() => handLayoutDirty = true;

    private void LateUpdate()
    {
        if (handLayoutDirty) RefreshCardSizes();
    }

    public void RefreshCardSizes()
    {
        handLayoutDirty = false;
        if (cardPrefab == null || cards.Count == 0) return;

        RectTransform handRect = (RectTransform)transform;
        float ratio = GetCardSizeRatio();
        foreach (CardUI card in cards)
        {
            // Detached cards keep their size until their animation or drag ends.
            if (card != null && !card.IsUsed && card.transform.parent == transform)
                card.ResizeForHand(ratio);
        }
        LayoutRebuilder.MarkLayoutForRebuild(handRect);
    }

    public Vector2 GetHandCardSize()
    {
        if (cardPrefab == null) return Vector2.zero;
        return ((RectTransform)cardPrefab.transform).rect.size * GetCardSizeRatio();
    }

    private float GetCardSizeRatio()
    {
        if (cardPrefab == null) return 1f;
        RectTransform prefabRect = (RectTransform)cardPrefab.transform;
        if (prefabRect.rect.height <= 0f) return 1f;

        HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
        float padding = layout != null ? layout.padding.vertical : 0f;
        float availableHeight = Mathf.Max(0f, ((RectTransform)transform).rect.height - padding);
        return Mathf.Clamp01(availableHeight / prefabRect.rect.height);
    }

    public bool IsFull()
    {
        return cards.Count >= maxHandSize;
    }

    public IEnumerator DiscardAllCards(System.Action<CardData> onDiscard)
    {
        // 애니메이션 중 목록이 변경될 수 있으므로 복사본 사용
        List<CardUI> cardsToDiscard = new List<CardUI>(cards);

        foreach (CardUI cardUI in cardsToDiscard)
        {
            if (cardUI == null)
                continue;

            CardData cardData = cardUI.GetCardData();

            if (cardData != null)
            {
                onDiscard?.Invoke(cardData);
            }

            // 카드가 이동하는 동안 Layout Group의 영향을 받지 않도록
            cardUI.transform.SetParent(cardAnimationLayer, true);

            yield return StartCoroutine(cardUI.DiscardAnimation(discardPilePoint));

            cards.Remove(cardUI);
            Destroy(cardUI.gameObject);
        }
    }

    public RectTransform GetBattleArea()
    {
        return battleArea;
    }

    public bool AddCard(CardData cardData, BattleManager battleManager)
    {
        BattleContext = battleManager;
        if (IsFull()) return false;

        CardUI cardUI = Instantiate(cardPrefab, transform);

        cardUI.SetCard(cardData, battleManager);
        cardUI.SetHandController(this);

        cards.Add(cardUI);

        Canvas.ForceUpdateCanvases();
        RefreshCardSizes();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);

        cardUI.PlayDrawAnimation(drawStartPoint);

        return true;
    }

    public CardUI[] GetCardsSnapshot() => cards.ToArray();

    public void RemoveCard(CardUI cardUI)
    {
        //if (!cards.Remove(cardUI))
        //    return;

        //Destroy(cardUI.gameObject);
        cards.Remove(cardUI);
    }

    public RectTransform GetCardAnimationLayer()
    {
        return cardAnimationLayer;
    }

    public RectTransform GetDiscardPilePoint()
    {
        return discardPilePoint;
    }

    public void IncreaseMaxHandSize(int amount)
    {
        maxHandSize += amount;
    }

    public int GetCurrentHandSize()
    {
        return cards.Count;
    }

    public int GetMaxHandSize()
    {
        return maxHandSize;
    }

    public List<CardData> RemoveAllCards()
    {
        List<CardData> removedCards = new List<CardData>();

        foreach (CardUI cardUI in cards)
        {
            CardData cardData = cardUI.GetCardData();

            if (cardData != null)
            {
                removedCards.Add(cardData);
            }

            Destroy(cardUI.gameObject);
        }

        cards.Clear();

        return removedCards;
    }
}
