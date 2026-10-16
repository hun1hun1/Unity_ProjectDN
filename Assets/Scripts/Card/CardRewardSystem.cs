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
}

public class CardRewardSystem : MonoBehaviour
{
    [SerializeField] private CardRewardPool[] pools = Array.Empty<CardRewardPool>();

    private List<CardData> Candidates(CardRewardPool pool, CardDeckType deck)
    {
        var result = new List<CardData>();
        if (pool == null || pool.deckType != deck || pool.weight <= 0 || pool.database == null) return result;
        foreach (CardData card in pool.database.GetAllCards())
            if (card != null && card.deckType == deck && card.type == pool.type && card.rarity == pool.rarity && !result.Contains(card))
                result.Add(card);
        return result;
    }

    public int GetCandidateCount(CardDeckType deck)
    {
        var unique = new HashSet<CardData>();
        foreach (CardRewardPool pool in pools) foreach (CardData card in Candidates(pool, deck)) unique.Add(card);
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
        var candidates = new List<CardData>[pools.Length];
        for (int i = 0; i < pools.Length; i++) candidates[i] = Candidates(pools[i], deckType);
        while (result.Count < rewardCount)
        {
            double total = 0;
            for (int i = 0; i < pools.Length; i++) if (candidates[i].Count > 0) total += pools[i].weight;
            double roll = (random != null ? random.NextDouble() : UnityEngine.Random.value) * total;
            int selected = -1;
            for (int i = 0; i < pools.Length; i++)
            {
                if (candidates[i].Count == 0) continue;
                selected = i;
                roll -= pools[i].weight;
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
