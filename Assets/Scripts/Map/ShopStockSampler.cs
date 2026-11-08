using System;
using System.Collections.Generic;

public static class ShopStockSampler
{
    private static readonly (CardType, CardRarity)[] StandardCategories =
    {
        (CardType.Attack, CardRarity.Common),
        (CardType.Skill, CardRarity.Common),
        (CardType.Attack, CardRarity.Uncommon),
        (CardType.Skill, CardRarity.Uncommon)
    };

    private static readonly (CardType, CardRarity)[] PremiumCategories =
    {
        (CardType.Attack, CardRarity.Uncommon),
        (CardType.Skill, CardRarity.Uncommon),
        (CardType.Attack, CardRarity.Rare),
        (CardType.Skill, CardRarity.Rare),
        (CardType.Continuous, CardRarity.Rare)
    };

    public static CardData[] Generate(CardDeckType deck, IReadOnlyList<CardRewardPool> pools, Random random)
    {
        var stock = new CardData[deck == CardDeckType.TypeDream ? 3 : 2];
        var cardsByCategory = new Dictionary<(CardType, CardRarity), List<CardData>>();
        foreach (CardRewardPool pool in pools)
        {
            if (pool == null || pool.deckType != deck) continue;
            var category = (pool.type, pool.rarity);
            if (!cardsByCategory.TryGetValue(category, out List<CardData> cards))
            {
                cards = new List<CardData>();
                cardsByCategory.Add(category, cards);
            }
            pool.AddCandidates(deck, cards);
        }

        var available = new List<List<CardData>>(PremiumCategories.Length);
        for (int slot = 0; slot < stock.Length; slot++)
        {
            var categories = slot == stock.Length - 1 ? PremiumCategories : StandardCategories;
            available.Clear();
            foreach (var category in categories)
                if (cardsByCategory.TryGetValue(category, out List<CardData> cards) && cards.Count > 0)
                    available.Add(cards);
            if (available.Count == 0) continue;

            // Roll the category first so a larger database does not increase its odds.
            List<CardData> selected = available[(int)(random.NextDouble() * available.Count)];
            int cardIndex = random.Next(selected.Count);
            stock[slot] = selected[cardIndex];
            selected.RemoveAt(cardIndex);
        }
        return stock;
    }
}
