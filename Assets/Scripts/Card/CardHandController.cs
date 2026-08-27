using System.Collections.Generic;
using UnityEngine;

public class CardHandController : MonoBehaviour
{
    [SerializeField]
    private CardUI cardPrefab;

    [SerializeField]
    private int maxHandSize = 3;

    private List<CardUI> cards = new List<CardUI>();

    public bool IsFull()
    {
        return cards.Count >= maxHandSize;
    }

    public bool AddCard(CardData cardData, BattleManager battleManager)
    {
        if (IsFull())
            return false;

        CardUI cardUI =
            Instantiate(cardPrefab, transform);

        cardUI.SetCard(cardData, battleManager);

        cards.Add(cardUI);

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
}
