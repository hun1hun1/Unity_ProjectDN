using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class XCostTests
{
    private readonly List<Object> created = new List<Object>();
    private BattleManager battle;
    private Player player;
    private CostSystem costs;

    [SetUp] public void Setup()
    {
        GameObject root = Track(new GameObject("X player")); player = root.AddComponent<Player>();
        Set(player, "currentHP", 80); costs = root.AddComponent<CostSystem>(); costs.ResetForTurn();
        root.AddComponent<AmmoSystem>(); DeckController deck = root.AddComponent<DeckController>();
        Enemy enemy = Track(new GameObject("X enemy")).AddComponent<Enemy>(); Set(enemy, "currentHP", 80);
        battle = Track(new GameObject("X battle")).AddComponent<BattleManager>();
        Set(battle, "player", player); Set(battle, "target", enemy); Set(battle, "deckController", deck);
    }
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear(); RunDeckInventory.ResetForNewGame();
    }

    [TestCase(CardDeckType.TypeDream, 3)] [TestCase(CardDeckType.TypeNightmare, 2)]
    [TestCase(CardDeckType.TypeDream, 0)] [TestCase(CardDeckType.TypeNightmare, 0)]
    public void XConsumesOnlyItsOwnPoolAndRepeatsTheFullSequence(CardDeckType deck, int amount)
    {
        Set(costs, "currentDreamCost", deck == CardDeckType.TypeDream ? amount : 3);
        Set(costs, "currentNightmareCost", deck == CardDeckType.TypeNightmare ? amount : 2);
        CardData card = Card(deck, Block(4));
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(4 * amount));
        Assert.That(costs.GetDreamCost(), Is.EqualTo(deck == CardDeckType.TypeDream ? 0 : 3));
        Assert.That(costs.GetNightmareCost(), Is.EqualTo(deck == CardDeckType.TypeNightmare ? 0 : 2));
    }

    [Test]
    public void GrantedCostCannotIncreaseTheCapturedRepeatCount()
    {
        AddCostEffect gain = Asset<AddCostEffect>(); Set(gain, "dreamCost", 1);
        CardData card = Card(CardDeckType.TypeDream, Block(2), gain);
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(6));
        Assert.That(costs.GetDreamCost(), Is.EqualTo(3));
    }

    [Test]
    public void CardAmmoIsPaidOnceButConditionalAmmoIsCheckedEachRepetition()
    {
        AmmoSystem ammo = player.GetAmmoSystem(); ammo.Load(3);
        SpendAmmoBonusEffect bonus = Asset<SpendAmmoBonusEffect>(); Set(bonus, "ammoCost", 1);
        Set(bonus, "bonusEffects", new CardEffect[] { Block(5) });
        CardData card = Card(CardDeckType.TypeDream, bonus); card.requiredAmmo = 1; card.consumeRequiredAmmo = true;
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(ammo.CurrentAmmo, Is.Zero);
        Assert.That(player.GetBlock(), Is.EqualTo(10));
    }

    [Test]
    public void ZeroXContinuousCardCreatesNoActiveState()
    {
        costs.ModifyDreamCost(-3);
        CardData card = Card(CardDeckType.TypeDream, Block(10)); card.type = CardType.Continuous;
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(player.ContinuousCards.ActiveCards, Is.Empty);
        Assert.That(player.GetBlock(), Is.Zero);
    }

    [Test]
    public void PositiveXContinuousCreatesOneSourceAndRepeatsItsActivation()
    {
        CardData card = Card(CardDeckType.TypeDream, Block(3)); card.type = CardType.Continuous;
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(player.ContinuousCards.ActiveCards, Has.Count.EqualTo(1));
        Assert.That(player.GetBlock(), Is.EqualTo(9));
    }

    [Test]
    public void XTextAndLiveDescriptionRefreshWithoutSpendingResources()
    {
        CardData card = Card(CardDeckType.TypeDream, Block(4)); card.description = "{block:0} / {x}";
        CardUI ui = HandCard(card);
        Assert.That(((TMP_Text)Get(ui, "costText")).text, Is.EqualTo("X"));
        Assert.That(((TMP_Text)Get(ui, "descriptionText")).text, Is.EqualTo("4 / 3"));
        costs.ModifyDreamCost(-1);
        Assert.That(((TMP_Text)Get(ui, "descriptionText")).text, Is.EqualTo("4 / 2"));
        Assert.That(CardDescriptionBuilder.Build(card, battle, false), Is.EqualTo("4 / X"));
        Assert.That(player.GetBlock(), Is.Zero); Assert.That(costs.GetDreamCost(), Is.EqualTo(2));
        Assert.That(battle.UseCard(ui), Is.True);
        Assert.That(((TMP_Text)Get(ui, "descriptionText")).text, Is.EqualTo("4 / 2"), "Used-card preview freezes before payment.");
    }

    [Test]
    public void AmmoFailureAfterPaymentRefundsTheActualX()
    {
        Set(costs, "maxDreamCost", 10); Set(costs, "currentDreamCost", 2);
        AmmoSystem ammo = player.GetAmmoSystem(); ammo.Load(1);
        CardData card = Card(CardDeckType.TypeDream, Block(4)); card.cost = new Cost(7, 7);
        card.requiredAmmo = 1; card.consumeRequiredAmmo = true;
        EventInfo changed = typeof(CostSystem).GetEvent("Changed");
        Assert.That(changed, Is.Not.Null, "Descriptions need cost change notifications.");
        Action spend = () => ammo.TrySpend(1); changed.AddEventHandler(costs, spend);
        CardUI ui = HandCard(card);
        Assert.That(battle.UseCard(ui), Is.False);
        changed.RemoveEventHandler(costs, spend);
        Assert.That(costs.GetDreamCost(), Is.EqualTo(2));
        Assert.That(ui.IsUsed, Is.False); Assert.That(player.GetBlock(), Is.Zero);
    }

    [Test]
    public void DeathStopsTheRemainingRepetitions()
    {
        DamageEffect hit = Asset<DamageEffect>(); hit.damage = 80; Set(hit, "target", CardTarget.Enemy);
        CardData card = Card(CardDeckType.TypeDream, Block(1), hit, Block(100));
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(1));
        Assert.That(battle.CanContinueEffects, Is.False);
    }

    private CardData Card(CardDeckType deck, params CardEffect[] effects)
    {
        CardData card = Asset<CardData>(); card.cardName = "X Test"; card.type = CardType.Skill; card.deckType = deck;
        card.cost = new Cost(0, 0); card.effects = effects;
        FieldInfo mode = typeof(CardData).GetField("costMode");
        Assert.That(mode, Is.Not.Null, "CardData needs explicit Fixed / X cost modes.");
        mode.SetValue(card, Enum.Parse(mode.FieldType, "X"));
        return card;
    }
    private CardUI HandCard(CardData card)
    {
        GameObject go = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Card.prefab")));
        CardUI ui = go.GetComponent<CardUI>(); ui.SetCard(card, battle); return ui;
    }
    private BlockEffect Block(int amount) { BlockEffect e = Asset<BlockEffect>(); e.block = amount; return e; }
    private T Asset<T>() where T : ScriptableObject { T value = ScriptableObject.CreateInstance<T>(); created.Add(value); return value; }
    private GameObject Track(GameObject value) { created.Add(value); return value; }
    private static FieldInfo Field(object obj, string name)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        { FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f; }
        throw new ArgumentException(name);
    }
    private static object Get(object obj, string name) => Field(obj, name).GetValue(obj);
    private static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
}
