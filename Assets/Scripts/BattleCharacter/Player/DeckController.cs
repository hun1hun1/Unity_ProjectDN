using System.Collections.Generic;
using UnityEngine;

public class DeckController : MonoBehaviour
{
    [Header("Starting Deck")]
    [SerializeField] private CardDatabase dreamStartingDeckDatabase;
    [SerializeField] private CardDatabase nightmareStartingDeckDatabase;

    private RunDeckState runDeck;
    private CardDatabase initializedDreamDatabase;
    private CardDatabase initializedNightmareDatabase;
    private uint gameVersion;

    private List<CardData> dreamDrawPile = new List<CardData>();
    private List<CardData> dreamDiscardPile = new List<CardData>();

    private List<CardData> nightmareDrawPile = new List<CardData>();
    private List<CardData> nightmareDiscardPile = new List<CardData>();
    private readonly List<CardData> dreamExhaustPile = new List<CardData>();
    private readonly List<CardData> nightmareExhaustPile = new List<CardData>();

    private void Awake()
    {
        ResetForBattle();
    }

    private void EnsureInitialized()
    {
        if (runDeck != null && gameVersion == RunDeckInventory.GameVersion &&
            initializedDreamDatabase == dreamStartingDeckDatabase &&
            initializedNightmareDatabase == nightmareStartingDeckDatabase)
            return;

        initializedDreamDatabase = dreamStartingDeckDatabase;
        initializedNightmareDatabase = nightmareStartingDeckDatabase;
        gameVersion = RunDeckInventory.GameVersion;
        runDeck = dreamStartingDeckDatabase != null || nightmareStartingDeckDatabase != null
            ? RunDeckInventory.GetOrCreate(dreamStartingDeckDatabase, nightmareStartingDeckDatabase)
            : new RunDeckState(null, null);
        ResetBattlePiles();
    }

    public void ResetForBattle()
    {
        EnsureInitialized();
        ResetBattlePiles();
    }

    private void ResetBattlePiles()
    {
        RunProgress progress = RunSession.Instance != null ? RunSession.Instance.Progress : null;
        shuffleRandom = progress != null && progress.Phase == RunPhase.Battle ? new System.Random(progress.NodeSeed) : null;
        dreamDrawPile.Clear();
        dreamDrawPile.AddRange(runDeck.Dream);
        nightmareDrawPile.Clear();
        nightmareDrawPile.AddRange(runDeck.Nightmare);
        dreamDiscardPile.Clear();
        nightmareDiscardPile.Clear();
        dreamExhaustPile.Clear();
        nightmareExhaustPile.Clear();
        Shuffle(dreamDrawPile);
        Shuffle(nightmareDrawPile);
    }

    private System.Random shuffleRandom;
    private void Shuffle(List<CardData> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int randomIndex = shuffleRandom != null ? shuffleRandom.Next(i + 1) : Random.Range(0, i + 1);

            CardData temp = cards[i];
            cards[i] = cards[randomIndex];
            cards[randomIndex] = temp;
        }
    }

    public IReadOnlyList<CardData> GetDeck(CardDeckType deckType)
    {
        EnsureInitialized();
        switch (deckType)
        {
            case CardDeckType.TypeDream: return runDeck.DreamView;
            case CardDeckType.TypeNightmare: return runDeck.NightmareView;
            default: return null;
        }
    }

    private List<CardData> GetMutableDeck(CardDeckType deckType)
    {
        EnsureInitialized();
        return runDeck.GetCards(deckType);
    }

    public CardData DrawCard(CardDeckType deckType)
    {
        EnsureInitialized();
        List<CardData> drawPile = deckType == CardDeckType.TypeDream ? dreamDrawPile :
            deckType == CardDeckType.TypeNightmare ? nightmareDrawPile : null;
        List<CardData> discardPile = deckType == CardDeckType.TypeDream ? dreamDiscardPile :
            deckType == CardDeckType.TypeNightmare ? nightmareDiscardPile : null;
        if (drawPile == null || discardPile == null) return null;

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

    public IReadOnlyList<CardData> GetDrawPile(CardDeckType deckType)
    {
        EnsureInitialized();
        switch (deckType)
        {
            case CardDeckType.TypeDream:
                return dreamDrawPile.AsReadOnly();

            case CardDeckType.TypeNightmare:
                return nightmareDrawPile.AsReadOnly();
        }

        return null;
    }

    public IReadOnlyList<CardData> GetDiscardPile(CardDeckType deckType)
    {
        EnsureInitialized();
        switch (deckType)
        {
            case CardDeckType.TypeDream:
                return dreamDiscardPile.AsReadOnly();

            case CardDeckType.TypeNightmare:
                return nightmareDiscardPile.AsReadOnly();
        }

        return null;
    }

    public void DiscardCard(CardData cardData, CardDeckType deckType)
    {
        if (cardData == null)
            return;

        EnsureInitialized();
        List<CardData> discardPile = deckType == CardDeckType.TypeDream ? dreamDiscardPile :
            deckType == CardDeckType.TypeNightmare ? nightmareDiscardPile : null;

        if (discardPile == null)
            return;

        discardPile.Add(cardData);
    }

    public IReadOnlyList<CardData> GetExhaustPile(CardDeckType deckType)
    {
        EnsureInitialized();
        switch (deckType)
        {
            case CardDeckType.TypeDream: return dreamExhaustPile.AsReadOnly();
            case CardDeckType.TypeNightmare: return nightmareExhaustPile.AsReadOnly();
            default: return null;
        }
    }

    public void ExhaustCard(CardData cardData, CardDeckType deckType)
    {
        if (cardData == null || cardData.deckType != deckType) return;
        EnsureInitialized();
        if (deckType == CardDeckType.TypeDream) dreamExhaustPile.Add(cardData);
        else if (deckType == CardDeckType.TypeNightmare) nightmareExhaustPile.Add(cardData);
    }

    public void ReturnToDrawPile(CardData cardData, CardDeckType deckType)
    {
        if (cardData == null || cardData.deckType != deckType) return;
        EnsureInitialized();
        List<CardData> pile = deckType == CardDeckType.TypeDream ? dreamDrawPile :
            deckType == CardDeckType.TypeNightmare ? nightmareDrawPile : null;
        if (pile != null) pile.Insert(Random.Range(0, pile.Count + 1), cardData);
    }

    public void AddCardToDeck(CardData cardData, CardDeckType deckType)
    {
        if (cardData == null)
            return;

        List<CardData> deck = GetMutableDeck(deckType);

        if (deck == null)
            return;

        if (cardData.deckType != deckType) return;
        deck.Add(cardData);
        runDeck.MarkChanged();
        DiscardCard(cardData, deckType);
    }

    public bool RemoveCardFromDeck(CardData cardData, CardDeckType deckType)
    {
        if (cardData == null)
            return false;

        List<CardData> deck = GetMutableDeck(deckType);

        if (deck == null)
            return false;

        if (!deck.Remove(cardData)) return false;
        runDeck.MarkChanged();
        List<CardData> drawPile = deckType == CardDeckType.TypeDream ? dreamDrawPile : nightmareDrawPile;
        List<CardData> discardPile = deckType == CardDeckType.TypeDream ? dreamDiscardPile : nightmareDiscardPile;
        if (!drawPile.Remove(cardData)) discardPile.Remove(cardData);
        return true;
    }
}
