using System.Collections.Generic;
using UnityEngine;

public class DeckController : MonoBehaviour
{
    [Header("Starting Deck")]
    [SerializeField] private List<CardData> dreamStartingDeck;
    [SerializeField] private List<CardData> nightmareStartingDeck;

    private List<CardData> dreamDrawPile = new List<CardData>();
    private List<CardData> dreamDiscardPile = new List<CardData>();

    private List<CardData> nightmareDrawPile = new List<CardData>();
    private List<CardData> nightmareDiscardPile = new List<CardData>();

    private void Awake()
    {
        InitializeDeck();
    }

    private void InitializeDeck()
    {
        dreamDrawPile = new List<CardData>(dreamStartingDeck);
        nightmareDrawPile = new List<CardData>(nightmareStartingDeck);

        dreamDiscardPile.Clear();
        nightmareDiscardPile.Clear();

        Shuffle(dreamDrawPile);
        Shuffle(nightmareDrawPile);
    }

    private void Shuffle(List<CardData> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

            CardData temp = cards[i];
            cards[i] = cards[randomIndex];
            cards[randomIndex] = temp;
        }
    }

    public CardData DrawCard(CardDeckType deckType)
    {
        List<CardData> drawPile = GetDrawPile(deckType);
        List<CardData> discardPile = GetDiscardPile(deckType);

        // 뽑을 카드가 없다면 버린 카드 더미를 다시 섞어서 사용
        if (drawPile.Count == 0)
        {
            if (discardPile.Count == 0)
                return null;

            drawPile.AddRange(discardPile);
            discardPile.Clear();

            Shuffle(drawPile);
        }

        int lastIndex = drawPile.Count - 1;

        CardData card = drawPile[lastIndex];

        drawPile.RemoveAt(lastIndex);

        return card;
    }

    private List<CardData> GetDrawPile(CardDeckType deckType)
    {
        switch (deckType)
        {
            case CardDeckType.TypeDream:
                return dreamDrawPile;

            case CardDeckType.TypeNightmare:
                return nightmareDrawPile;
        }

        return null;
    }

    private List<CardData> GetDiscardPile(CardDeckType deckType)
    {
        switch (deckType)
        {
            case CardDeckType.TypeDream:
                return dreamDiscardPile;

            case CardDeckType.TypeNightmare:
                return nightmareDiscardPile;
        }

        return null;
    }

    public void DiscardCard(CardData cardData, CardDeckType deckType)
    {
        if (cardData == null)
            return;

        List<CardData> discardPile = GetDiscardPile(deckType);

        if (discardPile == null)
            return;

        discardPile.Add(cardData);
    }
}
