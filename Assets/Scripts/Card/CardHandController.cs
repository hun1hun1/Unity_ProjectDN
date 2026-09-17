using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardHandController : MonoBehaviour
{
    [SerializeField] private CardUI cardPrefab;

    [SerializeField] private int maxHandSize = 3;

    private List<CardUI> cards = new List<CardUI>();

    [SerializeField] private RectTransform drawStartPoint;
    [SerializeField] private RectTransform cardAnimationLayer;

    [Header("Discard Animation")]
    [SerializeField] private RectTransform discardPilePoint;

    public bool IsFull()
    {
        return cards.Count >= maxHandSize;
    }

    public IEnumerator DiscardAllCards()
    {
        // 애니메이션 중 목록이 변경될 수 있으므로 복사본 사용
        List<CardUI> cardsToDiscard = new List<CardUI>(cards);

        foreach (CardUI cardUI in cardsToDiscard)
        {
            if (cardUI == null)
                continue;

            // 카드가 이동하는 동안 Layout Group의 영향을 받지 않도록
            cardUI.transform.SetParent(cardAnimationLayer, true);

            yield return cardUI.DiscardAnimation(discardPilePoint);

            cards.Remove(cardUI);
            Destroy(cardUI.gameObject);
        }
    }

    public bool AddCard(CardData cardData, BattleManager battleManager)
    {
        if (IsFull()) return false;

        CardUI cardUI = Instantiate(cardPrefab, transform);

        cardUI.SetCard(cardData, battleManager);
        cardUI.SetHandController(this);

        cards.Add(cardUI);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);

        cardUI.PlayDrawAnimation(drawStartPoint);

        return true;
    }

    public void RemoveCard(CardUI cardUI)
    {
        if (!cards.Remove(cardUI))
            return;

        Destroy(cardUI.gameObject);
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
