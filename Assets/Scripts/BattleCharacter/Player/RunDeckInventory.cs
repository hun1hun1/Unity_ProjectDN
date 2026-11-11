using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

// Starting databases are definitions; this state belongs to the current game.
internal sealed class RunDeckState
{
    internal readonly List<CardData> Dream;
    internal readonly List<CardData> Nightmare;
    internal readonly ReadOnlyCollection<CardData> DreamView;
    internal readonly ReadOnlyCollection<CardData> NightmareView;
    internal uint Revision { get; private set; }

    internal List<CardData> GetCards(CardDeckType deckType)
    {
        switch (deckType)
        {
            case CardDeckType.TypeDream: return Dream;
            case CardDeckType.TypeNightmare: return Nightmare;
            default: return null;
        }
    }

    internal void MarkChanged() => Revision++;

    internal RunDeckState(CardDatabase dream, CardDatabase nightmare)
    {
        Dream = dream != null ? new List<CardData>(dream.GetAllCards()) : new List<CardData>();
        Nightmare = nightmare != null ? new List<CardData>(nightmare.GetAllCards()) : new List<CardData>();
        DreamView = Dream.AsReadOnly();
        NightmareView = Nightmare.AsReadOnly();
    }
}

public static class RunDeckInventory
{
    private static readonly Dictionary<(CardDatabase, CardDatabase), RunDeckState> decks =
        new Dictionary<(CardDatabase, CardDatabase), RunDeckState>();
    internal static uint GameVersion { get; private set; }

    internal static RunDeckState Find(CardDatabase dream, CardDatabase nightmare)
    {
        decks.TryGetValue((dream, nightmare), out RunDeckState state);
        return state;
    }

    internal static RunDeckState GetOrCreate(CardDatabase dream, CardDatabase nightmare)
    {
        var key = (dream, nightmare);
        if (!decks.TryGetValue(key, out RunDeckState state))
        {
            state = new RunDeckState(dream, nightmare);
            decks.Add(key, state);
        }
        return state;
    }

    public readonly struct CardSelection
    {
        public CardData Card { get; }
        internal readonly RunDeckState State;
        internal readonly CardDeckType DeckType;
        internal readonly int Index;
        internal readonly uint DeckRevision;
        internal readonly uint RunVersion;

        internal CardSelection(RunDeckState state, CardDeckType deckType, int index, CardData card)
        {
            Card = card;
            State = state;
            DeckType = deckType;
            Index = index;
            DeckRevision = state.Revision;
            RunVersion = GameVersion;
        }
    }

    // Keep the selection with its UI item when sorting or filtering the displayed cards.
    public static IReadOnlyList<CardSelection> GetCardSelections(
        CardDatabase dream, CardDatabase nightmare, CardDeckType deckType)
    {
        if ((dream == null && nightmare == null) ||
            (deckType != CardDeckType.TypeDream && deckType != CardDeckType.TypeNightmare))
            return Array.Empty<CardSelection>();

        RunDeckState state = GetOrCreate(dream, nightmare);
        List<CardData> cards = state.GetCards(deckType);
        var selections = new CardSelection[cards.Count];
        for (int i = 0; i < cards.Count; i++)
            selections[i] = new CardSelection(state, deckType, i, cards[i]);
        return selections;
    }

    internal static CardData GetUpgradeResult(CardData card)
    {
        if (card == null || card.upgradedCard == null) return null;

        var visited = new HashSet<CardData> { card };
        for (CardData next = card.upgradedCard; next != null; next = next.upgradedCard)
            if (next.deckType != card.deckType || !visited.Add(next)) return null;
        return card.upgradedCard;
    }

    public static bool TryReplaceCard(CardSelection selection, CardData replacement) =>
        TryReplaceCard(selection, replacement, null);

    // A failed save restores the exact entry and leaves its selection valid for a retry.
    internal static bool TryReplaceCard(CardSelection selection, CardData replacement, Func<bool> commit)
    {
        RunDeckState state = selection.State;
        if (state == null || selection.RunVersion != GameVersion || selection.DeckRevision != state.Revision ||
            selection.Card == null || replacement == null || replacement == selection.Card ||
            selection.Card.deckType != selection.DeckType || replacement.deckType != selection.DeckType)
            return false;

        List<CardData> cards = state.GetCards(selection.DeckType);
        if (cards == null || selection.Index < 0 || selection.Index >= cards.Count ||
            cards[selection.Index] != selection.Card)
            return false;

        cards[selection.Index] = replacement;
        bool committed = false;
        try
        {
            committed = commit == null || commit();
            if (committed) state.MarkChanged();
            return committed;
        }
        finally
        {
            if (!committed) cards[selection.Index] = selection.Card;
        }
    }

    internal static bool TryAddCard(CardDatabase dream, CardDatabase nightmare, CardData card)
    {
        if (dream == null || nightmare == null || card == null ||
            (card.deckType != CardDeckType.TypeDream && card.deckType != CardDeckType.TypeNightmare))
            return false;
        RunDeckState state = GetOrCreate(dream, nightmare);
        state.GetCards(card.deckType).Add(card);
        state.MarkChanged();
        return true;
    }

    internal static RunDeckData[] Capture(RunAssetCatalog catalog)
    {
        var result = new List<RunDeckData>();
        foreach (var item in decks)
            result.Add(new RunDeckData { dreamDatabase = catalog.Key(item.Key.Item1), nightmareDatabase = catalog.Key(item.Key.Item2),
                dream = item.Value.Dream.ConvertAll(card => catalog.Key(card)).ToArray(),
                nightmare = item.Value.Nightmare.ConvertAll(card => catalog.Key(card)).ToArray() });
        return result.ToArray();
    }
    internal static void Restore(RunDeckData[] saved, RunAssetCatalog catalog)
    {
        decks.Clear();
        foreach (RunDeckData entry in saved)
        {
            var dream = catalog.Resolve<CardDatabase>(entry.dreamDatabase);
            var nightmare = catalog.Resolve<CardDatabase>(entry.nightmareDatabase);
            var state = new RunDeckState(null, null);
            foreach (string id in entry.dream) state.Dream.Add(catalog.Resolve<CardData>(id));
            foreach (string id in entry.nightmare) state.Nightmare.Add(catalog.Resolve<CardData>(id));
            decks.Add((dream, nightmare), state);
        }
        unchecked { GameVersion++; }
    }

    public static void ResetForNewGame()
    {
        decks.Clear();
        RunGoldInventory.ResetForNewGame();
        unchecked { GameVersion++; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeRuntime() => ResetForNewGame();
}
