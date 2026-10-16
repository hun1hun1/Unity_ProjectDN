using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class OpeningEffectTests
{
    private readonly List<Object> created = new List<Object>();
    private BattleManager battle;
    private Player player;
    private CostSystem costs;

    [SetUp] public void Setup()
    {
        GameObject root = Track(new GameObject("Opening player")); player = root.AddComponent<Player>();
        Set(player, "currentHP", 80); costs = root.AddComponent<CostSystem>(); costs.ResetForTurn();
        root.AddComponent<AmmoSystem>(); DeckController deck = root.AddComponent<DeckController>();
        Enemy enemy = Track(new GameObject("Opening enemy")).AddComponent<Enemy>(); Set(enemy, "currentHP", 80);
        battle = Track(new GameObject("Opening battle")).AddComponent<BattleManager>();
        Set(battle, "player", player); Set(battle, "target", enemy); Set(battle, "deckController", deck);
        foreach (string field in new[] { "dreamCardHand", "nightmareCardHand" })
        {
            CardHandController hand = Track(new GameObject(field, typeof(RectTransform))).AddComponent<CardHandController>();
            Set(hand, "maxHandSize", 0); Set(battle, field, hand);
        }
    }
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear(); RunDeckInventory.ResetForNewGame();
    }

    [TestCase(CardDeckType.TypeDream)] [TestCase(CardDeckType.TypeNightmare)]
    public void OnlyFirstSuccessfulCardGetsBonusAcrossBothDecks(CardDeckType firstDeck)
    {
        CardData first = Card(Block(2), Opening(Block(5))); first.deckType = firstDeck;
        CardData second = Card(Block(2), Opening(Block(5)));
        second.deckType = firstDeck == CardDeckType.TypeDream ? CardDeckType.TypeNightmare : CardDeckType.TypeDream;
        Assert.That(battle.UseCard(HandCard(first)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(7));
        Assert.That(battle.UseCard(HandCard(second)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(9));
    }

    [Test]
    public void OrdinaryCardConsumesOpeningAndSameAssetDoesNotRememberPreviousUse()
    {
        CardData card = Card(Block(1), Opening(Block(7)));
        Assert.That(battle.UseCard(HandCard(Card())), Is.True);
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(1));
        NextTurn();
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(8));
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(9));
    }

    [Test]
    public void FailedPaymentAndAmmoValidationPreserveOpening()
    {
        CardData expensive = Card(); expensive.cost = new Cost(99, 99);
        Assert.That(battle.UseCard(HandCard(expensive)), Is.False);
        CardData ammo = Card(); ammo.requiredAmmo = 1;
        Assert.That(battle.UseCard(HandCard(ammo)), Is.False);
        Assert.That(battle.UseCard(HandCard(Card(Opening(Block(6))))), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(6));
    }

    [Test]
    public void AmmoFailureAfterPaymentRefundDoesNotConsumeOpening()
    {
        player.GetAmmoSystem().Load(1);
        CardData card = Card(); card.cost = new Cost(1, 0); card.requiredAmmo = 1; card.consumeRequiredAmmo = true;
        Action spend = () => player.GetAmmoSystem().TrySpend(1);
        costs.Changed += spend;
        Assert.That(battle.UseCard(HandCard(card)), Is.False);
        costs.Changed -= spend;
        Assert.That(costs.GetDreamCost(), Is.EqualTo(3));
        Assert.That(battle.UseCard(HandCard(Card(Opening(Block(6))))), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(6));
    }

    [TestCase(0)] [TestCase(3)]
    public void XNeverGetsOpeningButConsumesFirstCardSlot(int amount)
    {
        costs.ModifyDreamCost(amount - 3);
        CardData card = Card(Block(1), Opening(Block(20))); card.costMode = CardCostMode.X;
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(amount));
        Assert.That(battle.UseCard(HandCard(Card(Opening(Block(6))))), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(amount));
    }

    [Test]
    public void ContinuousInitialUseGetsOpeningButAutomaticEffectsDoNot()
    {
        CardData card = Card(Opening(Block(4))); card.type = CardType.Continuous;
        card.turnStartEffects = new[] { Opening(Block(30)), Block(2) };
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(4));
        NextTurn();
        Assert.That(player.GetBlock(), Is.EqualTo(2));
        Assert.That(battle.UseCard(HandCard(Card(Opening(Block(7))))), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(9));
    }

    [Test]
    public void ContinuousOpeningStatusReleasesWhenItsSourceExpires()
    {
        StatusEffectData status = Asset<StatusEffectData>(); Set(status, "statusType", StatusEffectType.Rust);
        ApplyStatusEffect apply = Asset<ApplyStatusEffect>(); Set(apply, "status", status); Set(apply, "target", CardTarget.Player);
        CardData card = Card(Opening(apply)); card.type = CardType.Continuous;
        card.continuousDurationType = ContinuousDurationType.FixedTurns; card.continuousTurns = 1;
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.True);
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
        Assert.That(player.ContinuousCards.ActiveCards, Is.Empty);
    }

    [Test]
    public void OpeningCanScheduleDelayButDelayedAutomaticExecutionCannotClaimOpening()
    {
        DelayEffect delay = Asset<DelayEffect>(); Set(delay, "effects", new[] { Opening(Block(30)), Block(3) });
        CardData card = Card(Opening(delay));
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(battle.DelayedEffects.PendingEffects, Has.Count.EqualTo(1));
        Assert.That(player.GetBlock(), Is.Zero);
        NextTurn();
        Assert.That(player.GetBlock(), Is.EqualTo(3));
        Assert.That(battle.UseCard(HandCard(Card(Opening(Block(4))))), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(7));
    }

    [Test]
    public void PreviewRefreshesAfterUseAndTurnResetWithoutChangingLiveState()
    {
        CardData card = Card(Opening(Block(4))); card.description = "{openingCondition:0} / {block:0.0}";
        CardUI ui = HandCard(card);
        string initial = ((TMP_Text)Get(ui, "descriptionText")).text;
        Assert.That(initial, Does.Contain("개전 발동 가능").And.Contain("4").And.Not.Contain("{"));
        Assert.That(CardDescriptionBuilder.Build(card, battle, false), Is.EqualTo(" / 4"));
        Assert.That(battle.UseCard(HandCard(Card())), Is.True);
        Assert.That(((TMP_Text)Get(ui, "descriptionText")).text, Does.Contain("개전 발동 불가"));
        Assert.That(player.GetBlock(), Is.Zero);
        NextTurn();
        Assert.That(((TMP_Text)Get(ui, "descriptionText")).text, Does.Contain("개전 발동 가능"));
        Assert.That(battle.UseCard(ui), Is.True);
        Assert.That(((TMP_Text)Get(ui, "descriptionText")).text, Is.EqualTo(initial), "The resolving card keeps its pre-use description.");
        Assert.That(player.GetBlock(), Is.EqualTo(4));
    }

    [Test]
    public void UnavailablePreviewCannotLeakConditionalStatusesIntoLaterDamage()
    {
        StatusEffectData status = Asset<StatusEffectData>(); Set(status, "statusType", StatusEffectType.WeaknessExposure);
        ApplyStatusEffect apply = Asset<ApplyStatusEffect>(); Set(apply, "status", status); Set(apply, "target", CardTarget.Enemy);
        DamageEffect damage = Asset<DamageEffect>(); damage.damage = 20; Set(damage, "target", CardTarget.Enemy);
        CardData card = Card(Opening(apply), damage); card.description = "{damage:1}";
        Assert.That(CardDescriptionBuilder.Build(card, battle), Is.EqualTo("<color=#81C784>30</color>"));
        Assert.That(battle.GetTarget().StatusEffects.ActiveEffects, Is.Empty);
        Assert.That(battle.UseCard(HandCard(Card())), Is.True);
        Assert.That(CardDescriptionBuilder.Build(card, battle), Is.EqualTo("20"));
        Assert.That(CardDescriptionBuilder.Build(card, battle, false), Is.EqualTo("20"));
    }

    [Test]
    public void NestedAmmoBonusSharesOpeningSnapshotAndPaysOnlyOnFirstCard()
    {
        player.GetAmmoSystem().Load(2);
        SpendAmmoBonusEffect ammo = Asset<SpendAmmoBonusEffect>(); Set(ammo, "ammoCost", 1);
        Set(ammo, "bonusEffects", new[] { Opening(Block(5)) });
        CardData card = Card(Opening(ammo));
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(5));
        Assert.That(player.GetAmmoSystem().CurrentAmmo, Is.EqualTo(1));
    }

    [Test]
    public void InvalidCyclicOpeningDoesNotRecurseAndStatusMetadataStillTraversesChildren()
    {
        CardEffect opening = Opening(); Set(opening, "effects", new[] { opening });
        CardData card = Card(opening);
        Assert.That(battle.UseCard(HandCard(card)), Is.True);
        Assert.That(player.GetBlock(), Is.Zero);
        Assert.That(CardDescriptionBuilder.Build(card, battle), Is.Empty);
        Assert.That(CardStatusCollector.Collect(card), Is.Empty);
        DelayEffect delay = Asset<DelayEffect>(); Set(delay, "effects", new[] { opening });
        Assert.That(delay.IsValid, Is.False);
        StatusEffectData status = Asset<StatusEffectData>(); Set(status, "statusType", StatusEffectType.Rust);
        ApplyStatusEffect apply = Asset<ApplyStatusEffect>(); Set(apply, "status", status);
        Set(opening, "effects", new CardEffect[] { apply });
        Assert.That(CardStatusCollector.Collect(card), Has.Member(status));
    }

    [Test]
    public void EndingBattleStopsOpeningChildrenAndClearsExecutionContext()
    {
        DamageEffect hit = Asset<DamageEffect>(); hit.damage = 80; Set(hit, "target", CardTarget.Enemy);
        Assert.That(battle.UseCard(HandCard(Card(Opening(hit, Block(50))))), Is.True);
        Assert.That(player.GetBlock(), Is.Zero);
        Assert.That(battle.CanContinueEffects, Is.False);
        CardEffect opening = Opening(Block(8)); opening.Execute(battle);
        Assert.That(player.GetBlock(), Is.Zero);
    }

    private CardEffect Opening(params CardEffect[] effects)
    {
        Type type = typeof(CardEffect).Assembly.GetType("OpeningEffect");
        Assert.That(type, Is.Not.Null, "OpeningEffect must provide conditional card effects.");
        CardEffect effect = (CardEffect)ScriptableObject.CreateInstance(type); created.Add(effect); Set(effect, "effects", effects); return effect;
    }
    private CardData Card(params CardEffect[] effects)
    { CardData c = Asset<CardData>(); c.cardName = "Opening test"; c.type = CardType.Skill; c.cost = new Cost(0, 0); c.effects = effects; return c; }
    private CardUI HandCard(CardData card)
    { GameObject go = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Card.prefab"))); CardUI ui = go.GetComponent<CardUI>(); ui.SetCard(card, battle); return ui; }
    private void NextTurn() => Drain((IEnumerator)typeof(BattleManager).GetMethod("StartPlayerTurnRoutine", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(battle, null));
    private static void Drain(IEnumerator sequence)
    { try { while (sequence.MoveNext()) if (sequence.Current is IEnumerator child) Drain(child); } finally { (sequence as IDisposable)?.Dispose(); } }
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
