using System.Collections.Generic;
using UnityEngine;

public class CardRewardSystem : MonoBehaviour
{
    [Header("Card Database")]
    [SerializeField]
    private CardDatabase cardDatabase;

    [Header("Rarity Chance")]
    [SerializeField]
    private float commonChance = 80f;

    [SerializeField]
    private float uncommonChance = 15f;

    [SerializeField]
    private float rareChance = 5f;

    [Header("Common Type Chance")]
    [SerializeField]
    private float commonAttackChance = 70f;

    [SerializeField]
    private float commonSkillChance = 30f;

    [Header("Uncommon Type Chance")]
    [SerializeField]
    private float uncommonAttackChance = 50f;

    [SerializeField]
    private float uncommonSkillChance = 50f;

    [Header("Rare Type Chance")]
    [SerializeField]
    private float rareAttackChance = 40f;

    [SerializeField]
    private float rareSkillChance = 40f;

    [SerializeField]
    private float rareContinuousChance = 20f;


    public CardData GetRandomCard(CardDeckType deckType)
    {
        if (cardDatabase == null)
        {
            Debug.LogError("CardDatabase가 설정되지 않았습니다.");
            return null;
        }

        CardRarity rarity = GetRandomRarity();
        CardType type = GetRandomCardType(rarity);

        List<CardData> candidates =
            GetCandidates(deckType, rarity, type);

        if (candidates.Count == 0)
        {
            Debug.LogWarning(
                $"조건에 맞는 카드가 없습니다. " +
                $"Deck: {deckType}, " +
                $"Rarity: {rarity}, " +
                $"Type: {type}"
            );

            return null;
        }

        int randomIndex =
            Random.Range(0, candidates.Count);

        return candidates[randomIndex];
    }


    private List<CardData> GetCandidates(
    CardDeckType deckType,
    CardRarity rarity,
    CardType type)
    {
        List<CardData> candidates =
            new List<CardData>();

        IReadOnlyList<CardData> allCards =
            cardDatabase.GetAllCards();

        foreach (CardData card in allCards)
        {
            if (card == null)
                continue;

            if (card.deckType != deckType)
                continue;

            if (card.rarity != rarity)
                continue;

            if (card.type != type)
                continue;

            candidates.Add(card);
        }

        return candidates;
    }

    private CardRarity GetRandomRarity()
    {
        float randomValue =
            Random.Range(0f, 100f);

        if (randomValue < commonChance)
        {
            return CardRarity.Common;
        }

        if (randomValue <
            commonChance + uncommonChance)
        {
            return CardRarity.Uncommon;
        }

        return CardRarity.Rare;
    }


    private CardType GetRandomCardType(
        CardRarity rarity)
    {
        float randomValue =
            Random.Range(0f, 100f);

        switch (rarity)
        {
            case CardRarity.Common:

                if (randomValue < commonAttackChance)
                {
                    return CardType.Attack;
                }

                return CardType.Skill;


            case CardRarity.Uncommon:

                if (randomValue < uncommonAttackChance)
                {
                    return CardType.Attack;
                }

                return CardType.Skill;


            case CardRarity.Rare:

                if (randomValue < rareAttackChance)
                {
                    return CardType.Attack;
                }

                if (randomValue <
                    rareAttackChance + rareSkillChance)
                {
                    return CardType.Skill;
                }

                return CardType.Continuous;
        }

        return CardType.Attack;
    }


    public List<CardData> GenerateRewards(
    CardDeckType deckType,
    int rewardCount)
    {
        List<CardData> rewards =
            new List<CardData>();

        for (int i = 0; i < rewardCount; i++)
        {
            CardData card =
                GetRandomCard(deckType);

            if (card != null)
            {
                rewards.Add(card);
            }
        }

        return rewards;
    }
}