using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CardRewardPool
{
    public CardDeckType deckType;
    public CardType type;
    public CardRarity rarity;
    [Min(0)] public int weight;
    public CardDatabase database;

    internal void AddCandidates(CardDeckType deck, List<CardData> result)
    {
        if (deckType != deck || database == null) return;
        foreach (CardData card in database.GetAllCards())
            if (card != null && card.deckType == deck && card.type == type && card.rarity == rarity && !result.Contains(card))
                result.Add(card);
    }
}

public class CardRewardSystem : MonoBehaviour
{
    [SerializeField] private CardRewardPoolCatalog poolCatalog;
    [SerializeField, HideInInspector] private CardRewardPool[] pools = Array.Empty<CardRewardPool>();

    private IReadOnlyList<CardRewardPool> Pools => poolCatalog != null ? poolCatalog.Pools : pools;

    private List<CardData> Candidates(CardRewardPool pool, CardDeckType deck)
    {
        var result = new List<CardData>();
        if (pool != null && pool.weight > 0) pool.AddCandidates(deck, result);
        return result;
    }

    public int GetCandidateCount(CardDeckType deck)
    {
        var unique = new HashSet<CardData>();
        foreach (CardRewardPool pool in Pools) foreach (CardData card in Candidates(pool, deck)) unique.Add(card);
        return unique.Count;
    }

    public CardData GetRandomCard(CardDeckType deckType)
    {
        List<CardData> result = GenerateRewards(deckType, 1);
        return result.Count == 0 ? null : result[0];
    }

    public List<CardData> GenerateRewards(CardDeckType deckType, int rewardCount) => GenerateRewards(deckType, rewardCount, null);

    public List<CardData> GenerateRewards(CardDeckType deckType, int rewardCount, System.Random random)
    {
        var result = new List<CardData>();
        if (rewardCount <= 0 || GetCandidateCount(deckType) < rewardCount) return result;
        IReadOnlyList<CardRewardPool> rewardPools = Pools;
        var candidates = new List<CardData>[rewardPools.Count];
        for (int i = 0; i < candidates.Length; i++) candidates[i] = Candidates(rewardPools[i], deckType);
        while (result.Count < rewardCount)
        {
            double total = 0;
            for (int i = 0; i < candidates.Length; i++) if (candidates[i].Count > 0) total += rewardPools[i].weight;
            double roll = (random != null ? random.NextDouble() : UnityEngine.Random.value) * total;
            int selected = -1;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].Count == 0) continue;
                selected = i;
                roll -= rewardPools[i].weight;
                if (roll < 0) break;
            }
            List<CardData> category = candidates[selected];
            CardData card = category[random != null ? random.Next(category.Count) : UnityEngine.Random.Range(0, category.Count)];
            result.Add(card);
            foreach (List<CardData> list in candidates) list.Remove(card);
        }
        return result;
    }
}
