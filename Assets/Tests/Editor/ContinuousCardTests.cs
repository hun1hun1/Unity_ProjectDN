using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class ContinuousCardTests
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

    [TestCase(CardDeckType.TypeDream)]
    [TestCase(CardDeckType.TypeNightmare)]
    public void TimedAbsorbedCardReturnsOnlyOnceToItsOriginalDrawPile(CardDeckType deckType)
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        CardData card = CreateCard(2, false, deckType);
        deck.AddCardToDeck(card, deckType);
        Assert.That(deck.DrawCard(deckType), Is.SameAs(card));
        ActiveContinuousCard entry = player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(entry.IsAbsorbed, Is.True);
        Assert.That(deck.GetDrawPile(deckType), Is.Empty);
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(entry.RemainingTurns, Is.EqualTo(1));
        Assert.That(deck.GetDrawPile(deckType), Is.Empty);
        player.ContinuousCards.ReduceRemainingTurns();
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(player.ContinuousCards.ActiveCards, Is.Empty);
        Assert.That(deck.GetDrawPile(deckType), Has.Count.EqualTo(1));
        Assert.That(deck.DrawCard(deckType), Is.SameAs(card));
        Assert.That(deck.DrawCard(deckType), Is.Null);
        Assert.That(deck.GetDiscardPile(deckType), Is.Empty);
        Assert.That(deck.GetExhaustPile(deckType), Is.Empty);
        Assert.That(deck.GetDrawPile(deckType == CardDeckType.TypeDream ? CardDeckType.TypeNightmare : CardDeckType.TypeDream), Is.Empty);
        Assert.That(deck.GetDeck(deckType), Has.Count.EqualTo(1), "Battle movement must preserve ownership.");
        Assert.That(card.continuousTurns, Is.EqualTo(2), "Runtime countdown must not mutate the shared asset.");
    }

    [TestCase(CardDeckType.TypeDream)]
    [TestCase(CardDeckType.TypeNightmare)]
    public void ExhaustedCardIsUnavailableImmediatelyButItsEffectLastsUntilExpiry(CardDeckType deckType)
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        CardData card = CreateCard(2, true, deckType);
        card.effects = new CardEffect[] { CreateStatusEffect(CardTarget.Player) };
        deck.AddCardToDeck(card, deckType);
        deck.DrawCard(deckType);
        ActiveContinuousCard entry = player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(entry.IsAbsorbed, Is.False);
        Assert.That(deck.GetExhaustPile(deckType), Has.Member(card));
        Assert.That(deck.DrawCard(deckType), Is.Null);
        Assert.That(DamageCalculator.Calculate(100, player, null), Is.EqualTo(75));
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.True);
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
        Assert.That(deck.GetExhaustPile(deckType), Has.Count.EqualTo(1));
        Assert.That(deck.DrawCard(deckType), Is.Null);
        Assert.That(deck.GetDeck(deckType), Has.Count.EqualTo(1));
    }

    [Test]
    public void DuplicateUsesKeepSeparateClocksAndDoNotRemoveEachOthersStatus()
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        CardData card = CreateCard(2);
        card.effects = new CardEffect[] { CreateStatusEffect(CardTarget.Player) };
        ActiveContinuousCard first = player.ContinuousCards.Activate(card, battle, deck);
        player.ContinuousCards.ReduceRemainingTurns();
        ActiveContinuousCard second = player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(second.Id, Is.Not.EqualTo(first.Id));
        Assert.That(player.StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(2));
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(first.IsActive, Is.False);
        Assert.That(second.IsActive, Is.True);
        Assert.That(second.RemainingTurns, Is.EqualTo(1));
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.True);
        Assert.That(deck.GetDrawPile(card.deckType), Has.Count.EqualTo(1));
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
        Assert.That(deck.GetDrawPile(card.deckType), Has.Count.EqualTo(2));
    }

    [Test]
    public void OrdinaryStatusDurationRemainsIndependentOfContinuousSources()
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        ApplyStatusEffect effect = CreateStatusEffect(CardTarget.Player, 4);
        effect.Execute(battle);
        CardData card = CreateCard(1);
        card.effects = new CardEffect[] { effect };
        player.ContinuousCards.Activate(card, battle, deck);
        player.StatusEffects.ReduceRemainingTurns();
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(player.StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(3));
        Assert.That(DamageCalculator.Calculate(100, player, null), Is.EqualTo(75));
        for (int i = 0; i < 3; i++) player.StatusEffects.ReduceRemainingTurns();
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
    }

    [Test]
    public void PermanentAndTimedSourcesOfTheSameStatusCoexistWithoutASecondClock()
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        ApplyStatusEffect effect = CreateStatusEffect(CardTarget.Player);
        CardData permanent = CreateCard(1);
        permanent.continuousDurationType = ContinuousDurationType.UntilBattleEnd;
        permanent.exhaustOnUse = true; // Permanent cards always absorb, even with this stale field.
        permanent.effects = new CardEffect[] { effect };
        CardData timed = CreateCard(1);
        timed.effects = new CardEffect[] { effect };
        ActiveContinuousCard entry = player.ContinuousCards.Activate(permanent, battle, deck);
        player.ContinuousCards.Activate(timed, battle, deck);
        for (int i = 0; i < 5; i++)
        {
            player.StatusEffects.ReduceRemainingTurns();
            player.ContinuousCards.ReduceRemainingTurns();
        }
        Assert.That(entry.IsAbsorbed, Is.True);
        Assert.That(player.ContinuousCards.ActiveCards, Has.Count.EqualTo(1));
        Assert.That(player.StatusEffects.ActiveEffects[0].IsPermanent, Is.True);
        Assert.That(DamageCalculator.Calculate(100, player, null), Is.EqualTo(75));
        Assert.That(deck.GetExhaustPile(permanent.deckType), Is.Empty);
        Assert.That(deck.GetDrawPile(permanent.deckType), Has.Count.EqualTo(1));
    }

    [Test]
    public void ImmediateEffectsExecuteOnceInOrderAndPermanentStatusAppearsInDamagePreview()
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        Enemy enemy = (Enemy)GetField(battle, "target");
        DamageEffect damage = Asset<DamageEffect>();
        damage.damage = 10;
        SetField(damage, "target", CardTarget.Enemy);
        CardData card = CreateCard(1);
        card.continuousDurationType = ContinuousDurationType.UntilBattleEnd;
        card.description = "Damage {damage:1}";
        card.effects = new CardEffect[] { CreateStatusEffect(CardTarget.Player), damage };
        Assert.That(CardDescriptionBuilder.Build(card, battle), Does.Contain("7"));
        Assert.That(CardDescriptionBuilder.Build(card, battle, false), Is.EqualTo("Damage 10"));
        player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(73));
        for (int i = 0; i < 4; i++) player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(73), "Damage must not repeat each turn.");
        CardData attack = Asset<CardData>();
        attack.description = "{damage:0}";
        attack.effects = new CardEffect[] { damage };
        Assert.That(CardDescriptionBuilder.Build(attack, battle), Does.Contain("7"));
        Assert.That(CardDescriptionBuilder.Build(attack, battle, false), Is.EqualTo("10"));
    }

    [Test]
    public void EnemyStatusFromContinuousCardExpiresWithItsPlayerOwnedSource()
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        Enemy enemy = (Enemy)GetField(battle, "target");
        CardData card = CreateCard(1);
        card.effects = new CardEffect[] { CreateStatusEffect(CardTarget.Enemy, 8) };
        player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(enemy.StatusEffects.HasStatus(StatusEffectType.Rust), Is.True);
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
        enemy.StatusEffects.ReduceRemainingTurns();
        Assert.That(enemy.StatusEffects.HasStatus(StatusEffectType.Rust), Is.True);
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(enemy.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
    }

    [Test]
    public void AmmoCapacitySourcesExpireSeparatelyAndClampCurrentAmmoWithoutUndoingOtherBonuses()
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        AmmoSystem ammo = player.GetAmmoSystem();
        ammo.IncreaseMaxForBattle(2);
        CardData first = CreateCard(1);
        CardData second = CreateCard(2);
        IncreaseMaxAmmoEffect firstEffect = Asset<IncreaseMaxAmmoEffect>();
        IncreaseMaxAmmoEffect secondEffect = Asset<IncreaseMaxAmmoEffect>();
        SetField(firstEffect, "amount", 3);
        SetField(secondEffect, "amount", 4);
        first.effects = new CardEffect[] { firstEffect };
        second.effects = new CardEffect[] { secondEffect };
        player.ContinuousCards.Activate(first, battle, deck);
        player.ContinuousCards.Activate(second, battle, deck);
        Assert.That(ammo.MaxAmmo, Is.EqualTo(19));
        ammo.Load(100);
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(ammo.MaxAmmo, Is.EqualTo(16));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(16));
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(ammo.MaxAmmo, Is.EqualTo(12));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(12));
        player.ContinuousCards.ResetForBattle();
        Assert.That(ammo.MaxAmmo, Is.EqualTo(12));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ConditionalBonusEffectsInheritTheContinuousCardLifetime(bool hasAmmo)
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        AmmoSystem ammo = player.GetAmmoSystem();
        if (hasAmmo) ammo.Load(1);
        ApplyStatusEffect status = CreateStatusEffect(CardTarget.Player, 8);
        IncreaseMaxAmmoEffect capacity = Asset<IncreaseMaxAmmoEffect>();
        SetField(capacity, "amount", 5);
        SpendAmmoBonusEffect bonus = Asset<SpendAmmoBonusEffect>();
        SetField(bonus, "bonusEffects", new CardEffect[] { status, capacity });
        CardData card = CreateCard(1);
        card.effects = new CardEffect[] { bonus };
        player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(ammo.CurrentAmmo, Is.Zero);
        Assert.That(ammo.MaxAmmo, Is.EqualTo(hasAmmo ? 15 : 10));
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.EqualTo(hasAmmo));
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(ammo.MaxAmmo, Is.EqualTo(10), "Nested capacity bonuses must be released with the card.");
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
    }

    [Test]
    public void ResetReleasesSourcesWithoutReturningCardsOrRemovingOrdinaryEffects()
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        ApplyStatusEffect status = CreateStatusEffect(CardTarget.Player, 3);
        status.Execute(battle);
        CardData card = CreateCard(2);
        IncreaseMaxAmmoEffect ammo = Asset<IncreaseMaxAmmoEffect>();
        SetField(ammo, "amount", 5);
        card.effects = new CardEffect[] { status, ammo };
        ActiveContinuousCard entry = player.ContinuousCards.Activate(card, battle, deck);
        player.ContinuousCards.ResetForBattle();
        player.ContinuousCards.ResetForBattle();
        Assert.That(entry.IsActive, Is.False);
        Assert.That(player.ContinuousCards.ActiveCards, Is.Empty);
        Assert.That(player.StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(3));
        Assert.That(player.GetAmmoSystem().MaxAmmo, Is.EqualTo(10));
        Assert.That(deck.GetDrawPile(card.deckType), Is.Empty);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void InvalidTimedDurationIsRejectedBeforeCostsOrAmmoAreSpent(int turns)
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        CardData card = CreateCard(turns);
        card.cost = new Cost(1, 0);
        card.requiredAmmo = 1;
        card.consumeRequiredAmmo = true;
        player.GetAmmoSystem().Load(4);
        CostSystem costs = player.GetCostSystem();
        int currentCost = (int)GetField(costs, "currentDreamCost");
        Assert.That(battle.CanUseCard(card), Is.False);
        Assert.That(player.ContinuousCards.Activate(card, battle, deck), Is.Null);
        Assert.That(GetField(costs, "currentDreamCost"), Is.EqualTo(currentCost));
        Assert.That(player.GetAmmoSystem().CurrentAmmo, Is.EqualTo(4));
        Assert.That(player.ContinuousCards.ActiveCards, Is.Empty);
    }

    [Test]
    public void InvalidDurationKindAndNonContinuousCardsCannotActivate()
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        CardData card = CreateCard(2);
        card.continuousDurationType = (ContinuousDurationType)9;
        Assert.That(battle.CanUseCard(card), Is.False);
        Assert.That(player.ContinuousCards.Activate(card, battle, deck), Is.Null);
        card.continuousDurationType = ContinuousDurationType.FixedTurns;
        card.type = CardType.Skill;
        Assert.That(player.ContinuousCards.Activate(card, battle, deck), Is.Null);
        Assert.That(battle.CanUseCard(card), Is.True);
    }

    [Test]
    public void HudKeepsDuplicateCardIconsSeparateAndClosesExpiredTooltip()
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        GameObject canvas = Track(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
        GameObject info = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterInfoUI.prefab"), canvas.transform));
        StatusEffectsUI hud = info.GetComponentInChildren<StatusEffectsUI>(true);
        hud.Bind(player.StatusEffects, player.ContinuousCards);
        CardData card = CreateCard(2);
        card.cardName = "Timed continuous";
        card.description = "A lasting effect";
        ActiveContinuousCard first = player.ContinuousCards.Activate(card, battle, deck);
        player.ContinuousCards.ReduceRemainingTurns();
        ActiveContinuousCard second = player.ContinuousCards.Activate(card, battle, deck);
        StatusIconUI[] icons = hud.GetComponentsInChildren<StatusIconUI>();
        Assert.That(icons, Has.Length.EqualTo(2));
        StatusIconUI firstIcon = System.Array.Find(icons, icon => icon.ContinuousCardId == first.Id);
        StatusIconUI secondIcon = System.Array.Find(icons, icon => icon.ContinuousCardId == second.Id);
        Assert.That(firstIcon.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("1"));
        Assert.That(secondIcon.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("2"));
        firstIcon.OnPointerEnter(new PointerEventData(null) { position = Vector2.zero });
        StatusTooltipUI tooltip = canvas.GetComponentInChildren<StatusTooltipUI>(true);
        Assert.That(tooltip.gameObject.activeSelf, Is.True);
        Assert.That(tooltip.GetComponentInChildren<TMP_Text>(true).text, Does.Contain("남은 턴: 1"));
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(tooltip.gameObject.activeSelf, Is.False);
        Assert.That(hud.GetComponentsInChildren<StatusIconUI>(), Has.Length.EqualTo(1));
        hud.gameObject.SetActive(false);
        player.ContinuousCards.ReduceRemainingTurns();
        hud.gameObject.SetActive(true);
        Assert.That(hud.GetComponentsInChildren<StatusIconUI>(), Is.Empty);
    }

    [Test]
    public void PermanentHudUsesArtworkFallbackAndRebindingDoesNotKeepOldCardIcons()
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        GameObject canvas = Track(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
        GameObject info = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterInfoUI.prefab"), canvas.transform));
        StatusEffectsUI hud = info.GetComponentInChildren<StatusEffectsUI>(true);
        hud.Bind(player.StatusEffects, player.ContinuousCards);
        CardData card = CreateCard(1);
        card.continuousDurationType = ContinuousDurationType.UntilBattleEnd;
        card.artwork = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/Rust.asset").Icon;
        player.ContinuousCards.Activate(card, battle, deck);
        StatusIconUI icon = hud.GetComponentInChildren<StatusIconUI>();
        Assert.That(icon.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("∞"));
        Assert.That(icon.GetComponent<UnityEngine.UI.Image>().sprite, Is.SameAs(card.artwork));
        icon.OnPointerEnter(new PointerEventData(null));
        Assert.That(canvas.GetComponentInChildren<StatusTooltipUI>(true).GetComponentInChildren<TMP_Text>(true).text,
            Does.Contain("전투 종료까지 지속"));
        hud.Bind(new StatusEffectSystem(), new ContinuousCardSystem());
        Assert.That(hud.GetComponentsInChildren<StatusIconUI>(), Is.Empty);
        Assert.That(canvas.GetComponentInChildren<StatusTooltipUI>(true).gameObject.activeSelf, Is.False);
    }

    [Test]
    public void EnemyHudAndOpenTooltipRefreshWhenThePlayersContinuousSourceTicks()
    {
        BattleManager battle = CreateBattle(out Player player, out DeckController deck);
        Enemy enemy = (Enemy)GetField(battle, "target");
        GameObject canvas = Track(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
        GameObject info = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterInfoUI.prefab"), canvas.transform));
        StatusEffectsUI hud = info.GetComponentInChildren<StatusEffectsUI>(true);
        hud.Bind(enemy.StatusEffects, enemy.ContinuousCards);
        CardData card = CreateCard(3);
        card.effects = new CardEffect[] { CreateStatusEffect(CardTarget.Enemy) };
        player.ContinuousCards.Activate(card, battle, deck);
        StatusIconUI icon = hud.GetComponentInChildren<StatusIconUI>();
        icon.OnPointerEnter(new PointerEventData(null));
        enemy.StatusEffects.ReduceRemainingTurns();
        player.ContinuousCards.ReduceRemainingTurns();
        Assert.That(enemy.StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(2));
        Assert.That(icon.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("2"));
        Assert.That(canvas.GetComponentInChildren<StatusTooltipUI>(true).GetComponentInChildren<TMP_Text>(true).text,
            Does.Contain("남은 턴: 2"));
    }

    [UnityTest]
    public IEnumerator BattleSceneReturnsExpiredCardBeforeDrawEvenWhenTurnEndsDuringUseAnimation()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return null;
        yield return new WaitForSeconds(0.8f);
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle, Is.Not.Null, "BattleScene must finish loading.");
        Player player = battle.GetPlayer();
        DeckController deck = (DeckController)GetField(battle, "deckController");
        CardHandController hand = (CardHandController)GetField(battle, "nightmareCardHand");
        while (deck.DrawCard(CardDeckType.TypeNightmare) != null) { }
        CardData card = CreateCard(1, false, CardDeckType.TypeNightmare);
        card.effects = new CardEffect[] { CreateStatusEffect(CardTarget.Player) };
        CardUI ui = ((List<CardUI>)GetField(hand, "cards"))[0];
        ui.SetCard(card, battle);
        Assert.That(battle.UseCard(ui), Is.True);
        Assert.That(player.ContinuousCards.ActiveCards, Has.Count.EqualTo(1));
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.True);
        var observation = new ReturnObservation(battle, player, deck, hand, card);
        battle.EndPlayerTurn();
        float deadline = Time.realtimeSinceStartup + 15;
        while ((BattleState)GetField(battle, "currentState") != BattleState.PlayerTurn && Time.realtimeSinceStartup < deadline)
            yield return null;
        observation.Stop();
        Assert.That((BattleState)GetField(battle, "currentState"), Is.EqualTo(BattleState.PlayerTurn));
        Assert.That(observation.ReturnedBeforeDraw, Is.True);
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
        Assert.That(HandContains(hand, card), Is.True);
        Assert.That(deck.GetExhaustPile(card.deckType), Is.Empty);
        Assert.That((int)GetField(battle, "activeCardUses"), Is.Zero);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator BattleSceneExhaustsOnUseAndDoesNotRedrawTheCardWhenItsEffectExpires()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return null;
        yield return new WaitForSeconds(0.8f);
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle, Is.Not.Null, "BattleScene must finish loading.");
        Player player = battle.GetPlayer();
        DeckController deck = (DeckController)GetField(battle, "deckController");
        CardHandController hand = (CardHandController)GetField(battle, "dreamCardHand");
        CardData card = CreateCard(1, true);
        card.effects = new CardEffect[] { CreateStatusEffect(CardTarget.Player) };
        CardUI ui = ((List<CardUI>)GetField(hand, "cards"))[0];
        ui.SetCard(card, battle);
        Assert.That(battle.UseCard(ui), Is.True);
        Assert.That(deck.GetExhaustPile(card.deckType), Has.Member(card), "Exhaustion happens before animation completes.");
        Assert.That(battle.UseCard(ui), Is.False);
        battle.EndPlayerTurn();
        float deadline = Time.realtimeSinceStartup + 15;
        while ((BattleState)GetField(battle, "currentState") != BattleState.PlayerTurn && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That((BattleState)GetField(battle, "currentState"), Is.EqualTo(BattleState.PlayerTurn));
        Assert.That(player.ContinuousCards.ActiveCards, Is.Empty);
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
        Assert.That(deck.GetExhaustPile(card.deckType), Has.Count.EqualTo(1));
        Assert.That(deck.GetDiscardPile(card.deckType), Has.No.Member(card));
        Assert.That(deck.GetDrawPile(card.deckType), Has.No.Member(card));
        Assert.That(HandContains(hand, card), Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PermanentCardIsAbsorbedAndItsStatusDoesNotExpireAfterOrdinaryDuration()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return null;
        yield return new WaitForSeconds(0.8f);
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Player player = battle.GetPlayer();
        DeckController deck = (DeckController)GetField(battle, "deckController");
        CardHandController hand = (CardHandController)GetField(battle, "dreamCardHand");
        StatusEffectData status = ScriptableObject.CreateInstance<StatusEffectData>();
        SetField(status, "statusType", StatusEffectType.Rust);
        ApplyStatusEffect effect = ScriptableObject.CreateInstance<ApplyStatusEffect>();
        SetField(effect, "status", status);
        SetField(effect, "turns", 1);
        SetField(effect, "target", CardTarget.Player);
        CardData card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "Permanent Test";
        card.type = CardType.Continuous;
        card.deckType = CardDeckType.TypeDream;
        card.effects = new CardEffect[] { effect };
        CardUI ui = ((List<CardUI>)GetField(hand, "cards"))[0];
        ui.SetCard(card, battle);
        Assert.That(battle.UseCard(ui), Is.True);
        float deadline = Time.realtimeSinceStartup + 8;
        while ((int)GetField(battle, "activeCardUses") > 0 && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(deck.GetDiscardPile(CardDeckType.TypeDream), Has.No.Member(card),
            "A permanent card must be absorbed instead of discarded.");
        for (int i = 0; i < 5; i++) player.StatusEffects.ReduceRemainingTurns();
        Assert.That(DamageCalculator.Calculate(100, player, null), Is.EqualTo(75),
            "A permanent card's status follows the card lifetime, not ApplyStatusEffect.turns.");
        Object.Destroy(card);
        Object.Destroy(effect);
        Object.Destroy(status);
        yield return new ExitPlayMode();
    }

    private static FieldInfo Field(object value, string name)
    {
        for (System.Type type = value.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field;
        }
        throw new System.ArgumentException(name);
    }

    private static bool HandContains(CardHandController hand, CardData card)
    {
        foreach (CardUI ui in (List<CardUI>)GetField(hand, "cards"))
            if (ui.GetCardData() == card) return true;
        return false;
    }

    // Iterator-local closures are not preserved by EnterPlayMode's domain reload.
    private sealed class ReturnObservation
    {
        private readonly BattleManager battle;
        private readonly Player player;
        private readonly DeckController deck;
        private readonly CardHandController hand;
        private readonly CardData card;
        public bool ReturnedBeforeDraw { get; private set; }

        public ReturnObservation(BattleManager battle, Player player, DeckController deck, CardHandController hand, CardData card)
        {
            this.battle = battle;
            this.player = player;
            this.deck = deck;
            this.hand = hand;
            this.card = card;
            player.ContinuousCards.Changed += Observe;
        }

        private void Observe()
        {
            if (player.ContinuousCards.ActiveCards.Count == 0)
                ReturnedBeforeDraw = deck.GetDrawPile(card.deckType).Count == 1 && hand.GetCurrentHandSize() == 0 &&
                    (BattleState)GetField(battle, "currentState") == BattleState.EnemyTurnEnd;
        }

        public void Stop() => player.ContinuousCards.Changed -= Observe;
    }

    private static object GetField(object value, string name) => Field(value, name).GetValue(value);
    private static void SetField(object value, string name, object content) => Field(value, name).SetValue(value, content);

    private GameObject Track(GameObject value) { (created ??= new List<Object>()).Add(value); return value; }

    private T Asset<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        (created ??= new List<Object>()).Add(value);
        return value;
    }

    private BattleManager CreateBattle(out Player player, out DeckController deck)
    {
        GameObject root = Track(new GameObject("Player"));
        player = root.AddComponent<Player>();
        root.AddComponent<CostSystem>().ResetForTurn();
        root.AddComponent<AmmoSystem>().ResetForBattle();
        deck = root.AddComponent<DeckController>();
        Enemy enemy = Track(new GameObject("Enemy")).AddComponent<Enemy>();
        SetField(enemy, "currentHP", 80);
        BattleManager battle = Track(new GameObject("Battle")).AddComponent<BattleManager>();
        SetField(battle, "player", player);
        SetField(battle, "target", enemy);
        SetField(battle, "deckController", deck);
        return battle;
    }

    private CardData CreateCard(int turns, bool exhaust = false, CardDeckType deckType = CardDeckType.TypeDream)
    {
        CardData card = Asset<CardData>();
        card.cardName = "Continuous Test";
        card.type = CardType.Continuous;
        card.deckType = deckType;
        card.continuousDurationType = ContinuousDurationType.FixedTurns;
        card.continuousTurns = turns;
        card.exhaustOnUse = exhaust;
        return card;
    }

    private ApplyStatusEffect CreateStatusEffect(CardTarget target, int turns = 1)
    {
        StatusEffectData status = Asset<StatusEffectData>();
        SetField(status, "statusType", StatusEffectType.Rust);
        ApplyStatusEffect effect = Asset<ApplyStatusEffect>();
        SetField(effect, "target", target);
        SetField(effect, "status", status);
        SetField(effect, "turns", turns);
        return effect;
    }
}
