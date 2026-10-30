using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class ContinuousTurnEffectTests
{
    private List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = (created?.Count ?? 0) - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created?.Clear();
        RunDeckInventory.ResetForNewGame();
    }

    [TestCase(false, CardDeckType.TypeDream)]
    [TestCase(true, CardDeckType.TypeDream)]
    [TestCase(false, CardDeckType.TypeNightmare)]
    [TestCase(true, CardDeckType.TypeNightmare)]
    public void OneTurnCardFiresOnFollowingStartBeforeStatusReleaseAndReturn(bool exhaust, CardDeckType deckType)
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        CardData card = Card(1, Damage(8));
        card.deckType = deckType;
        card.exhaustOnUse = exhaust;
        card.effects = new CardEffect[] { Rust(CardTarget.Player) };
        ActiveContinuousCard active = player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(80), "Recurring damage must not fire on use.");
        Assert.That(active.RemainingTurns, Is.EqualTo(1));
        bool expiredAfterDamage = false;
        player.ContinuousCards.Changed += () =>
        {
            if (!active.IsActive) expiredAfterDamage = enemy.GetCurrentHP() == 74;
        };
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(74), "Rust must still affect the final recurring hit.");
        Assert.That(expiredAfterDamage, Is.True);
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
        Assert.That(player.ContinuousCards.ActiveCards, Is.Empty);
        Assert.That(deck.GetDrawPile(deckType), Has.Count.EqualTo(exhaust ? 0 : 1));
        Assert.That(deck.GetExhaustPile(deckType), Has.Count.EqualTo(exhaust ? 1 : 0));
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(74), "Expired cards must not execute again.");
    }

    [Test]
    public void ThreeTurnCardFiresExactlyThreeFollowingStartsAndReturnsAfterTheLastHit()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        CardData card = Card(3, Damage(4));
        ActiveContinuousCard active = player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(80));
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(76));
        Assert.That(active.RemainingTurns, Is.EqualTo(2));
        Assert.That(deck.GetDrawPile(card.deckType), Is.Empty);
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(72));
        Assert.That(active.RemainingTurns, Is.EqualTo(1));
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(68));
        Assert.That(deck.GetDrawPile(card.deckType), Has.Count.EqualTo(1));
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(68));
        Assert.That(card.continuousTurns, Is.EqualTo(3));
    }

    [Test]
    public void PermanentCardStartsNextTurnAndKeepsFiringWithoutReturning()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        CardData card = Card(1, Damage(4));
        card.continuousDurationType = ContinuousDurationType.UntilBattleEnd;
        ActiveContinuousCard active = player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(80));
        for (int i = 0; i < 5; i++) StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(60));
        Assert.That(active.IsActive, Is.True);
        Assert.That(deck.GetDrawPile(card.deckType), Is.Empty);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void CardsWithoutNonNullTurnEffectsKeepTheirOriginalEarlyExpiry(int emptyMode)
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        CardData card = Card(1);
        ConfigureTurnEffects(card, emptyMode == 0 ? null : emptyMode == 1 ? new CardEffect[0] : new CardEffect[] { null });
        card.effects = new CardEffect[] { Rust(CardTarget.Player) };
        player.ContinuousCards.Activate(card, battle, deck);
        player.AddBlock(10);
        int blockAtExpiry = -1;
        player.ContinuousCards.Changed += () =>
        {
            if (player.ContinuousCards.ActiveCards.Count == 0) blockAtExpiry = player.GetBlock();
        };
        StartTurn(battle);
        Assert.That(blockAtExpiry, Is.EqualTo(10), "Non-recurring cards still expire before block reset.");
        Assert.That(player.GetBlock(), Is.Zero);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(80));
        Assert.That(deck.GetDrawPile(card.deckType), Has.Member(card));
    }

    [Test]
    public void RecurringBlockAndCostEffectsRunAfterResetsAndKeepGameplayInputLocked()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        BlockEffect block = Asset<BlockEffect>();
        block.block = 5;
        ModifyCostEffect cost = Asset<ModifyCostEffect>();
        SetField(cost, "dreamCost", -1);
        CardData card = Card(1, block, cost);
        player.ContinuousCards.Activate(card, battle, deck);
        player.AddBlock(20);
        SetField(player.GetCostSystem(), "currentDreamCost", 0);
        CardData ordinary = Asset<CardData>();
        ordinary.deckType = CardDeckType.TypeDream;
        bool inputWasBlocked = false;
        player.ContinuousCards.Changed += () => inputWasBlocked = !battle.CanUseCard(ordinary);
        StartTurn(battle);
        Assert.That(player.GetBlock(), Is.EqualTo(5));
        Assert.That(GetField(player.GetCostSystem(), "currentDreamCost"), Is.EqualTo(2));
        Assert.That(inputWasBlocked, Is.True);
        Assert.That(battle.CanUseCard(ordinary), Is.True);
    }

    [Test]
    public void MaintainedEffectsStayUntilAllRecurringEffectsFinishThenReleaseOnce()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        BlockEffect noBlock = Asset<BlockEffect>();
        CardData first = Card(1, noBlock);
        first.effects = new CardEffect[] { Rust(CardTarget.Player) };
        CardData second = Card(2, Damage(4));
        player.ContinuousCards.Activate(first, battle, deck);
        player.ContinuousCards.Activate(second, battle, deck);
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(77), "All recurring effects precede expiry cleanup.");
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(73));
        Assert.That(player.ContinuousCards.ActiveCards, Is.Empty);
    }

    [Test]
    public void DuplicatePeriodicCardsKeepTheirOwnRemainingExecutions()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        CardData card = Card(2, Damage(2));
        player.ContinuousCards.Activate(card, battle, deck);
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(78));
        player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(78));
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(74));
        Assert.That(player.ContinuousCards.ActiveCards, Has.Count.EqualTo(1));
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(72));
        Assert.That(deck.GetDrawPile(card.deckType), Has.Count.EqualTo(2));
    }

    [Test]
    public void InstantAndMaintainedEffectsDoNotRepeatAlongWithTurnEffects()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        IncreaseMaxAmmoEffect capacity = Asset<IncreaseMaxAmmoEffect>();
        SetField(capacity, "amount", 5);
        CardData card = Card(2, Damage(4));
        card.effects = new CardEffect[] { Damage(8), capacity };
        player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(72));
        Assert.That(player.GetAmmoSystem().MaxAmmo, Is.EqualTo(15));
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(68));
        Assert.That(player.GetAmmoSystem().MaxAmmo, Is.EqualTo(15));
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(64));
        Assert.That(player.GetAmmoSystem().MaxAmmo, Is.EqualTo(10));
    }

    [Test]
    public void RecurringConditionalEffectsCheckAndConsumeTheirOwnAmmoEachTurn()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        SpendAmmoBonusEffect bonus = Asset<SpendAmmoBonusEffect>();
        SetField(bonus, "bonusEffects", new CardEffect[] { Damage(4) });
        CardData card = Card(3, bonus);
        player.GetAmmoSystem().Load(2);
        player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(player.GetAmmoSystem().CurrentAmmo, Is.EqualTo(2));
        StartTurn(battle);
        Assert.That(player.GetAmmoSystem().CurrentAmmo, Is.EqualTo(1));
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(76));
        StartTurn(battle);
        Assert.That(player.GetAmmoSystem().CurrentAmmo, Is.Zero);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(72));
        StartTurn(battle);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(72));
        Assert.That(player.ContinuousCards.ActiveCards, Is.Empty);
    }

    [Test]
    public void RepeatedStatusApplicationUsesItsOwnDurationAndOccursAfterNormalCountdown()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        ApplyStatusEffect status = Rust(CardTarget.Enemy);
        SetField(status, "turns", 2);
        CardData card = Card(1, status);
        player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(enemy.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
        StartTurn(battle);
        Assert.That(player.ContinuousCards.ActiveCards, Is.Empty);
        Assert.That(enemy.StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(2));
        StartTurn(battle);
        Assert.That(enemy.StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(1));
        StartTurn(battle);
        Assert.That(enemy.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
    }

    [Test]
    public void TurnDamagePreviewUsesSeparateTokensAndCannotChangeLiveStateOrImmediatePreview()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        CardData card = Card(1, Rust(CardTarget.Player), Damage(8));
        card.effects = new CardEffect[] { Damage(10) };
        card.description = "Use {damage:0}; Turn {turnDamage:1}";
        Assert.That(CardDescriptionBuilder.Build(card, battle), Is.EqualTo("Use 10; Turn <color=#E57373>6</color>"));
        Assert.That(CardDescriptionBuilder.Build(card, battle, false), Is.EqualTo("Use 10; Turn 8"));
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(80));
        Assert.That(player.StatusEffects.ActiveEffects, Is.Empty);
    }

    [Test]
    public void TurnAmmoPreviewDoesNotChargeTheCardsUseTimeRequirementAgain()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        SpendAmmoBonusEffect bonus = Asset<SpendAmmoBonusEffect>();
        SetField(bonus, "bonusEffects", new CardEffect[] { Damage(8) });
        CardData card = Card(2, bonus);
        card.requiredAmmo = 2;
        card.consumeRequiredAmmo = true;
        card.description = "{turnAmmoCost:0}; {turnAmmoCondition:0}; {turnDamage:0.0}";
        player.GetAmmoSystem().Load(2);
        string description = CardDescriptionBuilder.Build(card, battle);
        Assert.That(description, Is.EqualTo("1; <color=#81C784>현재 발동 가능</color>; 8"));
        Assert.That(player.GetAmmoSystem().CurrentAmmo, Is.EqualTo(2));
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(80));
    }

    [Test]
    public void CardStatusTooltipsCollectNestedRecurringEffectsAndIgnoreThemOnOrdinaryCards()
    {
        SpendAmmoBonusEffect bonus = Asset<SpendAmmoBonusEffect>();
        ApplyStatusEffect rust = Rust(CardTarget.Enemy);
        SetField(bonus, "bonusEffects", new CardEffect[] { rust, rust });
        CardData card = Card(1, bonus);
        IReadOnlyList<StatusEffectData> statuses = CardStatusCollector.Collect(card);
        Assert.That(statuses, Has.Count.EqualTo(1));
        Assert.That(statuses[0].Type, Is.EqualTo(StatusEffectType.Rust));
        card.type = CardType.Skill;
        Assert.That(CardStatusCollector.Collect(card), Is.Empty);
    }

    [UnityTest]
    public IEnumerator BattleSceneFiresNextTurnAfterResetsThenReturnsBeforeDrawWithoutPayingUseCostsAgain()
    {
        yield return new EnterPlayMode();
        yield return VerifyNextTurnEffectsAndReturn();
        yield return new ExitPlayMode();
    }

    private IEnumerator VerifyNextTurnEffectsAndReturn()
    {
        AsyncOperation loading = SceneManager.LoadSceneAsync("BattleScene");
        Assert.That(loading, Is.Not.Null);
        yield return Until(() => loading.isDone, "BattleScene did not finish loading.");
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle, Is.Not.Null);
        yield return Until(() => battle.CanInspectCards, "The initial hand did not finish drawing.");
        Player player = battle.GetPlayer();
        Enemy enemy = battle.GetTarget();
        SetField(enemy, "behavior", null);
        enemy.BindBattle(battle);
        DeckController deck = (DeckController)GetField(battle, "deckController");
        CardHandController hand = (CardHandController)GetField(battle, "nightmareCardHand");
        while (deck.DrawCard(CardDeckType.TypeNightmare) != null) { }
        BlockEffect block = Asset<BlockEffect>();
        block.block = 5;
        CardData card = Card(1, Damage(4), block);
        card.deckType = CardDeckType.TypeNightmare;
        card.cost = new Cost(0, 1);
        card.requiredAmmo = 1;
        card.consumeRequiredAmmo = true;
        player.GetCostSystem().AddNightmareCost(1);
        player.GetAmmoSystem().Load(3);
        int previousHP = enemy.GetCurrentHP();
        CardUI ui = ((List<CardUI>)GetField(hand, "cards"))[0];
        ui.SetCard(card, battle);
        Assert.That(battle.UseCard(ui), Is.True);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(previousHP));
        Assert.That(player.GetBlock(), Is.Zero);
        Assert.That(player.GetAmmoSystem().CurrentAmmo, Is.EqualTo(2));
        Assert.That(battle.BlocksCardInteraction, Is.True);
        yield return Until(() => battle.CanInspectCards, "The continuous card did not finish activating.");
        Assert.That(player.ContinuousCards.ActiveCards, Has.Count.EqualTo(1));
        var observation = new PeriodicExpiryObservation(player, enemy, deck, hand, card, previousHP - 4);
        battle.EndPlayerTurn();
        Assert.That((BattleState)GetField(battle, "currentState"), Is.EqualTo(BattleState.PlayerTurnEnd),
            "The test must actually end the current turn before waiting for the next one.");
        yield return Until(() => battle.CanInspectCards, "The next player turn did not finish resolving and drawing.");
        observation.Stop();
        Assert.That((BattleState)GetField(battle, "currentState"), Is.EqualTo(BattleState.PlayerTurn));
        Assert.That(observation.FiredThenReturnedBeforeDraw, Is.True);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(previousHP - 4));
        Assert.That(player.GetBlock(), Is.EqualTo(5));
        Assert.That(player.GetAmmoSystem().CurrentAmmo, Is.EqualTo(2));
        Assert.That(GetField(player.GetCostSystem(), "currentNightmareCost"), Is.Zero);
        Assert.That(HandContains(hand, card), Is.True);
        Assert.That(player.ContinuousCards.ActiveCards, Is.Empty);
    }

    private static IEnumerator Until(Func<bool> ready, string message)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!ready())
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), message);
            yield return null;
        }
    }

    private sealed class PeriodicExpiryObservation
    {
        private readonly Player player;
        private readonly Enemy enemy;
        private readonly DeckController deck;
        private readonly CardHandController hand;
        private readonly CardData card;
        private readonly int expectedHP;
        public bool FiredThenReturnedBeforeDraw { get; private set; }

        public PeriodicExpiryObservation(Player player, Enemy enemy, DeckController deck, CardHandController hand, CardData card, int expectedHP)
        {
            this.player = player; this.enemy = enemy; this.deck = deck;
            this.hand = hand; this.card = card; this.expectedHP = expectedHP;
            player.ContinuousCards.Changed += Observe;
        }

        private void Observe()
        {
            if (player.ContinuousCards.ActiveCards.Count == 0)
                FiredThenReturnedBeforeDraw = enemy.GetCurrentHP() == expectedHP && player.GetBlock() == 5 &&
                    hand.GetCurrentHandSize() == 0 && deck.GetDrawPile(card.deckType).Count == 1;
        }

        public void Stop() => player.ContinuousCards.Changed -= Observe;
    }

    private static bool HandContains(CardHandController hand, CardData card)
    {
        foreach (CardUI ui in (List<CardUI>)GetField(hand, "cards"))
            if (ui.GetCardData() == card) return true;
        return false;
    }

    private BattleManager Battle(out Player player, out Enemy enemy, out DeckController deck)
    {
        GameObject root = Track(new GameObject("Player"));
        player = root.AddComponent<Player>();
        SetField(player, "currentHP", 80);
        root.AddComponent<CostSystem>().ResetForTurn();
        root.AddComponent<AmmoSystem>().ResetForBattle();
        deck = root.AddComponent<DeckController>();
        enemy = Track(new GameObject("Enemy")).AddComponent<Enemy>();
        SetField(enemy, "currentHP", 80);
        BattleManager battle = Track(new GameObject("Battle")).AddComponent<BattleManager>();
        SetField(battle, "player", player);
        SetField(battle, "target", enemy);
        SetField(battle, "deckController", deck);
        foreach (string field in new[] { "dreamCardHand", "nightmareCardHand" })
        {
            CardHandController hand = Track(new GameObject(field, typeof(RectTransform))).AddComponent<CardHandController>();
            SetField(hand, "maxHandSize", 0);
            SetField(battle, field, hand);
        }
        return battle;
    }

    private CardData Card(int turns, params CardEffect[] recurring)
    {
        CardData card = Asset<CardData>();
        card.type = CardType.Continuous;
        card.deckType = CardDeckType.TypeDream;
        card.continuousDurationType = ContinuousDurationType.FixedTurns;
        card.continuousTurns = turns;
        ConfigureTurnEffects(card, recurring);
        return card;
    }

    private DamageEffect Damage(int amount)
    {
        DamageEffect effect = Asset<DamageEffect>();
        effect.damage = amount;
        SetField(effect, "target", CardTarget.Enemy);
        return effect;
    }

    private ApplyStatusEffect Rust(CardTarget target)
    {
        StatusEffectData status = Asset<StatusEffectData>();
        SetField(status, "statusType", StatusEffectType.Rust);
        ApplyStatusEffect effect = Asset<ApplyStatusEffect>();
        SetField(effect, "status", status);
        SetField(effect, "target", target);
        return effect;
    }

    private static void ConfigureTurnEffects(CardData card, CardEffect[] effects) =>
        card.turnStartEffects = effects;

    private static void StartTurn(BattleManager battle) =>
        typeof(BattleManager).GetMethod("StartPlayerTurn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(battle, null);

    private T Asset<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        (created ??= new List<Object>()).Add(value);
        return value;
    }

    private GameObject Track(GameObject value) { (created ??= new List<Object>()).Add(value); return value; }

    private static FieldInfo Field(object value, string name)
    {
        for (Type type = value.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field;
        }
        throw new ArgumentException(name);
    }

    private static object GetField(object value, string name) => Field(value, name).GetValue(value);
    private static void SetField(object value, string name, object data) => Field(value, name).SetValue(value, data);
}
