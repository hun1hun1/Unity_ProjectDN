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
