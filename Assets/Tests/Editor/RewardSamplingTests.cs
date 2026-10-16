using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class RewardSamplingTests
{
    private readonly List<Object> created = new List<Object>();
    private CardRewardSystem rewards;
    private readonly List<CardDatabase> databases = new List<CardDatabase>();
    [SetUp] public void Setup()
    {
        GameObject go = new GameObject("Reward sampling"); created.Add(go); rewards = go.AddComponent<CardRewardSystem>();
        FieldInfo pools = typeof(CardRewardSystem).GetField("pools", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(pools, Is.Not.Null, "Rewards must use the categorized databases.");
        Type entryType = pools.FieldType.GetElementType(); Array entries = Array.CreateInstance(entryType, 14);
        int[] weights = { 35, 35, 13, 12, 2, 2, 1 };
        CardType[] types = { CardType.Attack, CardType.Skill, CardType.Attack, CardType.Skill, CardType.Attack, CardType.Skill, CardType.Continuous };
        CardRarity[] rarities = { CardRarity.Common, CardRarity.Common, CardRarity.Uncommon, CardRarity.Uncommon, CardRarity.Rare, CardRarity.Rare, CardRarity.Rare };
        for (int deck = 0; deck < 2; deck++) for (int category = 0; category < 7; category++)
        {
            CardDatabase database = Asset<CardDatabase>(); databases.Add(database);
            var cards = new List<CardData>();
            for (int i = 0; i < 3; i++)
            {
                CardData card = Asset<CardData>(); card.deckType = (CardDeckType)deck;
                card.type = types[category]; card.rarity = rarities[category]; cards.Add(card);
            }
            Set(database, "cards", cards);
            object entry = Activator.CreateInstance(entryType);
            Set(entry, "deckType", (CardDeckType)deck); Set(entry, "type", types[category]);
            Set(entry, "rarity", rarities[category]); Set(entry, "weight", weights[category]); Set(entry, "database", database);
            entries.SetValue(entry, deck * 7 + category);
        }
        pools.SetValue(rewards, entries);
    }
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear(); databases.Clear(); RunDeckInventory.ResetForNewGame();
    }
    [TestCase(0.0, CardRarity.Common, CardType.Attack)]
    [TestCase(0.3499, CardRarity.Common, CardType.Attack)]
    [TestCase(0.3501, CardRarity.Common, CardType.Skill)]
    [TestCase(0.7001, CardRarity.Uncommon, CardType.Attack)]
    [TestCase(0.8301, CardRarity.Uncommon, CardType.Skill)]
    [TestCase(0.9501, CardRarity.Rare, CardType.Attack)]
    [TestCase(0.9701, CardRarity.Rare, CardType.Skill)]
    [TestCase(0.9901, CardRarity.Rare, CardType.Continuous)]
    public void SevenCategoryBoundariesMatchRequestedWeights(double roll, CardRarity rarity, CardType type)
    {
        foreach (CardDeckType deck in new[] { CardDeckType.TypeDream, CardDeckType.TypeNightmare })
        {
            MethodInfo method = typeof(CardRewardSystem).GetMethod("GenerateRewards", new[] { typeof(CardDeckType), typeof(int), typeof(System.Random) });
            Assert.That(method, Is.Not.Null);
            var result = (List<CardData>)method.Invoke(rewards, new object[] { deck, 1, new FixedRandom(roll) });
            Assert.That(result, Has.Count.EqualTo(1)); Assert.That(result[0].rarity, Is.EqualTo(rarity));
            Assert.That(result[0].type, Is.EqualTo(type)); Assert.That(result[0].deckType, Is.EqualTo(deck));
        }
    }
    [TestCase(CardDeckType.TypeDream, 3)] [TestCase(CardDeckType.TypeNightmare, 2)]
    public void OffersNeverRepeatOrMutateDatabases(CardDeckType deck, int count)
    {
        for (int i = 0; i < 300; i++)
        {
            List<CardData> offered = rewards.GenerateRewards(deck, count);
            Assert.That(offered, Has.Count.EqualTo(count)); Assert.That(offered.Distinct().Count(), Is.EqualTo(count));
            Assert.That(offered.All(c => c.deckType == deck), Is.True);
        }
        Assert.That(databases.All(d => d.GetAllCards().Count == 3), Is.True);
    }
    private BattleRewardSystem Flow(BattleResult result)
    {
        RunDeckInventory.ResetForNewGame();
        var go = new GameObject("Reward flow"); created.Add(go);
        var battle = go.AddComponent<BattleManager>();
        var deck = go.AddComponent<DeckController>();
        var flow = go.AddComponent<BattleRewardSystem>();
        Set(flow, "battle", battle); Set(flow, "deckController", deck); Set(flow, "cardRewards", rewards);
        typeof(BattleRewardSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow, null);
        battle.FinishBattle(result);
        return flow;
    }
    [Test] public void GoldIsClaimedOnceAndSkipForfeitsUnclaimedRewards()
    {
        var flow = Flow(BattleResult.Victory);
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.Summary));
        Assert.That(flow.ClaimGold(), Is.True); Assert.That(flow.ClaimGold(), Is.False);
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(20));
        Assert.That(flow.SkipRemaining(), Is.True);
        Assert.That(flow.OpenCardReward(), Is.False);
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(20));
        RunDeckInventory.ResetForNewGame();
        Assert.That(RunGoldInventory.Gold, Is.Zero);
    }
    [Test] public void SummaryCanSkipEverythingWithoutOpeningEntries()
    {
        var flow = Flow(BattleResult.Victory);
        Assert.That(flow.SkipRemaining(), Is.True);
        Assert.That(flow.ClaimGold(), Is.False); Assert.That(flow.OpenCardReward(), Is.False);
        Assert.That(RunGoldInventory.Gold, Is.Zero);
    }
    [TestCase(BattleResult.Defeat)] [TestCase(BattleResult.Cancelled)]
    public void NonVictoryNeverGrantsRewards(BattleResult result)
    {
        var flow = Flow(result);
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.Closed));
        Assert.That(flow.ClaimGold(), Is.False); Assert.That(flow.OpenCardReward(), Is.False);
    }
    [TestCase(CardDeckType.TypeDream,3)] [TestCase(CardDeckType.TypeNightmare,2)]
    public void DeckSelectionLocksOfferUntilOneCardIsTaken(CardDeckType type, int count)
    {
        var flow = Flow(BattleResult.Victory);
        var deck = flow.GetComponent<DeckController>();
        Assert.That(flow.OpenCardReward(), Is.True); Assert.That(flow.SelectDeck(type), Is.True);
        CardData[] offer = flow.Offers.ToArray(); Assert.That(offer, Has.Length.EqualTo(count));
        deck.AddCardToDeck(offer[0], type); // Owned cards remain eligible and can be acquired again.
        int before = deck.GetDeck(type).Count;
        Assert.That(flow.SkipRemaining(), Is.False); Assert.That(flow.SkipCard(), Is.False);
        Assert.That(flow.SelectDeck(type), Is.False); Assert.That(flow.OpenCardReward(), Is.False);
        Assert.That(flow.Offers, Is.EqualTo(offer)); Assert.That(flow.ChooseCard(-1), Is.False);
        Assert.That(flow.ChooseCard(0), Is.True); Assert.That(flow.ChooseCard(0), Is.False);
        Assert.That(deck.GetDeck(type).Count, Is.EqualTo(before + 1));
        Assert.That(deck.GetDeck(type).Count(c => c == offer[0]), Is.EqualTo(2));
        deck.ResetForBattle(); Assert.That(deck.GetDeck(type).Count, Is.EqualTo(before + 1));
        Assert.That(flow.ClaimGold(), Is.True);
    }
    [Test] public void SkippingDeckChoicePreservesGoldAndCannotReopenCardReward()
    {
        var flow = Flow(BattleResult.Victory);
        flow.OpenCardReward(); Assert.That(flow.SkipCard(), Is.True);
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.Summary));
        Assert.That(flow.OpenCardReward(), Is.False); Assert.That(flow.ClaimGold(), Is.True);
    }
    [Test] public void EmptyPoolsDoNotCreatePartialCompulsoryOffers()
    {
        Set(rewards, "pools", new CardRewardPool[0]);
        var flow = Flow(BattleResult.Victory); flow.OpenCardReward();
        Assert.That(flow.CanSelectDeck(CardDeckType.TypeDream), Is.False);
        Assert.That(flow.SelectDeck(CardDeckType.TypeDream), Is.False);
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.DeckChoice));
        Assert.That(flow.SkipCard(), Is.True);
    }
    private T Asset<T>() where T : ScriptableObject { T value = ScriptableObject.CreateInstance<T>(); created.Add(value); return value; }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);
    private sealed class FixedRandom : System.Random
    {
        private readonly double value;
        internal FixedRandom(double value) { this.value = value; }
        public override double NextDouble() => value;
        public override int Next(int maxValue) => 0;
    }
}
