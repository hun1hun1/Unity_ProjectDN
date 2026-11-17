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

public class StatusEffectTests
{
    private readonly List<Object> created = new List<Object>();

    [Test]
    public void ExistingStatusAssetsAreDebuffsAndThornsIsABuff()
    {
        foreach (string name in new[] { "WeaknessExposure", "Rust", "DefenseBreak", "Burn", "Thorns" })
        {
            var definition = AssetDatabase.LoadAssetAtPath<StatusEffectData>($"Assets/StatusData/{name}.asset");
            Assert.That(definition, Is.Not.Null, name);
            var category = new SerializedObject(definition).FindProperty("category");
            Assert.That(category, Is.Not.Null, name);
            Assert.That(category.enumNames[category.enumValueIndex], Is.EqualTo(name == "Thorns" ? "Buff" : "Debuff"));
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void WardAbsorbsOnlyTheNextIncomingAttackAndPreservesBlock(bool onPlayer)
    {
        CreateBattle(out Player player, out Enemy enemy);
        BattleCharacter defender = onPlayer ? (BattleCharacter)player : enemy;
        BattleCharacter attacker = onPlayer ? (BattleCharacter)enemy : player;
        var ward = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/Ward.asset");
        Assert.That(ward, Is.Not.Null);
        Assert.That(ward.Icon, Is.Not.Null);
        Assert.That(defender.StatusEffects.Apply(ward, 1), Is.True);
        Assert.That(defender.StatusEffects.Apply(ward, 1, 3), Is.True);
        defender.StatusEffects.ReduceRemainingTurns();
        Assert.That(defender.StatusEffects.HasStatus(ward.Type), Is.True);
        attacker.TakeDamage(1, defender);
        defender.TakeDamage(1);
        defender.TakeDamage(1, defender);
        Assert.That(defender.StatusEffects.HasStatus(ward.Type), Is.True);
        defender.AddBlock(4);

        defender.TakeDamage(100, attacker);
        Assert.That(defender.GetCurrentHP(), Is.EqualTo(78));
        Assert.That(defender.GetBlock(), Is.EqualTo(4));
        Assert.That(defender.StatusEffects.HasStatus(ward.Type), Is.False);

        defender.TakeDamage(10, attacker);
        Assert.That(defender.GetCurrentHP(), Is.EqualTo(72));
        Assert.That(defender.GetBlock(), Is.Zero);
    }

    [Test]
    public void WardPreviewsShowZeroOnlyForTheFirstHitWithoutConsumingTheLiveBuff()
    {
        BattleManager manager = CreateBattle(out Player player, out Enemy enemy);
        var playerGrant = AssetDatabase.LoadAssetAtPath<ApplyStatusEffect>("Assets/CardData/Status/Examples/Ward_PlayerEffect.asset");
        var enemyGrant = AssetDatabase.LoadAssetAtPath<EnemyApplyStatusAction>("Assets/Enemy/Examples/BuffWard.asset");
        Assert.That(playerGrant, Is.Not.Null);
        Assert.That(enemyGrant, Is.Not.Null);
        playerGrant.Execute(manager);
        enemyGrant.Execute(manager);
        Assert.That(enemyGrant.GetActionKinds(), Is.EqualTo(EnemyActionKinds.Buff));
        Assert.That(enemyGrant.GetActionIcon(), Is.Not.Null);

        var strike = CreateAsset<EnemyAttackAction>();
        SetField(strike, "damage", 6);
        var combo = CreateAsset<EnemyCompositeAction>();
        SetField(combo, "actions", new EnemyAction[] { strike, strike });
        Assert.That(combo.GetActionDescription(manager), Is.EqualTo("공격 0\n공격 6"));

        var damage = CreateAsset<DamageEffect>();
        damage.damage = 6;
        SetField(damage, "target", CardTarget.Enemy);
        var card = CreateAsset<CardData>();
        card.description = "{damage:0} / {damage:1}";
        card.effects = new CardEffect[] { damage, damage };
        Assert.That(CardDescriptionBuilder.Build(card, manager), Is.EqualTo("<color=#E57373>0</color> / 6"));
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Ward), Is.True);
        Assert.That(enemy.StatusEffects.HasStatus(StatusEffectType.Ward), Is.True);
    }

    [Test]
    public void WardHudUsesTheImportedIconAndRemovesItAfterBlockingAnAttack()
    {
        Player player = CreatePlayer();
        Player opponent = CreatePlayer();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterInfoUI.prefab");
        GameObject instance = Object.Instantiate(prefab);
        created.Add(instance);
        StatusEffectsUI ui = instance.GetComponentInChildren<StatusEffectsUI>();
        ui.Bind(player.StatusEffects);
        typeof(StatusEffectsUI).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
        var ward = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/Ward.asset");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/UI/StatusIcon/ward.png");

        Assert.That(player.StatusEffects.Apply(ward, 0), Is.True);
        var icon = ui.GetComponentInChildren<StatusIconUI>();
        Assert.That(icon.GetComponent<UnityEngine.UI.Image>().sprite, Is.SameAs(sprite).And.Not.Null);
        Assert.That(icon.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("1"));
        player.TakeDamage(10, opponent);
        Assert.That(ui.GetComponentInChildren<StatusIconUI>(), Is.Null);
    }

    [Test]
    public void BuffApplicationsHaveIndependentDurationsAndNextAttackIgnoresSelfDamage()
    {
        Player owner = CreatePlayer();
        Player opponent = CreatePlayer();
        var status = owner.StatusEffects;
        StatusEffectType type = StatusEffectType.Thorns;
        Assert.That(status.Apply(CreateBuff(BuffDuration.UntilBattleEnd), 0, 2), Is.True);
        Assert.That(status.Apply(CreateBuff(BuffDuration.FixedTurns), 2, 3), Is.True);
        Assert.That(status.Apply(CreateBuff(BuffDuration.NextTurn), 0, 5), Is.True);
        Assert.That(status.Apply(CreateBuff(BuffDuration.NextAttack), 0, 7), Is.True);
        Assert.That(status.ActiveEffects, Has.Count.EqualTo(1));
        Assert.That(status.GetBuffPower(type), Is.EqualTo(12));
        Assert.That(status.ActiveEffects[0].IsPending, Is.True);

        owner.TakeDamage(1, owner);
        opponent.TakeDamage(0, owner);
        Assert.That(status.GetBuffPower(type), Is.EqualTo(12));
        status.ReduceRemainingTurns();
        Assert.That(status.GetBuffPower(type), Is.EqualTo(17));
        Assert.That(status.ActiveEffects[0].IsPending, Is.False);

        opponent.TakeDamage(1, owner);
        Assert.That(status.GetBuffPower(type), Is.EqualTo(10));
        status.ReduceRemainingTurns();
        Assert.That(status.GetBuffPower(type), Is.EqualTo(2));
        Assert.That(status.ActiveEffects[0].IsPermanent, Is.True);
        status.Clear();
        Assert.That(status.ActiveEffects, Is.Empty);
    }

    [Test]
    public void ThornsRetaliatesOnBlockedAttacksWithoutConsumingBlockOrChaining()
    {
        BattleManager manager = CreateBattle(out Player player, out Enemy enemy);
        var grant = AssetDatabase.LoadAssetAtPath<ApplyStatusEffect>("Assets/CardData/Status/Examples/Thorns_PlayerEffect.asset");
        var enemyBuff = AssetDatabase.LoadAssetAtPath<EnemyApplyStatusAction>("Assets/Enemy/Examples/BuffThorns.asset");
        Assert.That(grant, Is.Not.Null);
        Assert.That(enemyBuff, Is.Not.Null);
        grant.Execute(manager);
        enemyBuff.Execute(manager);
        Assert.That(enemyBuff.GetActionKinds(), Is.EqualTo(EnemyActionKinds.Buff));
        Assert.That(enemyBuff.GetActionIcon(), Is.Not.Null);
        Assert.That(player.StatusEffects.GetBuffPower(StatusEffectType.Thorns), Is.EqualTo(3));
        Assert.That(enemy.StatusEffects.GetBuffPower(StatusEffectType.Thorns), Is.EqualTo(3));

        player.AddBlock(8);
        enemy.AddBlock(8);
        player.TakeDamage(5, enemy);
        Assert.That(player.GetCurrentHP(), Is.EqualTo(80));
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(77));
        Assert.That(enemy.GetBlock(), Is.EqualTo(8));

        player.TakeDamage(1);
        player.TakeDamage(1, player);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(77));
        Assert.That(player.StatusEffects.GetBuffPower(StatusEffectType.Thorns), Is.EqualTo(3));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FatalAttackOrRetaliationEndsTheCompositeWithoutAnotherHit(bool ownerDies)
    {
        BattleManager manager = CreateBattle(out Player player, out Enemy enemy);
        typeof(BattleManager).GetMethod("BindBattleCharacters", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, null);
        if (ownerDies) player.RestoreHealth(3, 80);
        else enemy.RestoreHealth(3, 80);
        player.StatusEffects.Apply(CreateBuff(BuffDuration.UntilBattleEnd), 0, 3);

        EnemyAttackAction attack = CreateAsset<EnemyAttackAction>();
        SetField(attack, "damage", 4);
        EnemyCompositeAction composite = CreateAsset<EnemyCompositeAction>();
        SetField(composite, "actions", new EnemyAction[] { attack, attack });
        Assert.That(composite.GetActionDescription(manager), Is.EqualTo("공격 4"));
        composite.Execute(manager);

        Assert.That(manager.Result, Is.EqualTo(ownerDies ? BattleResult.Defeat : BattleResult.Victory));
        Assert.That(player.GetCurrentHP(), Is.EqualTo(ownerDies ? 0 : 76));
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(ownerDies ? 80 : 0));
        Assert.That(player.StatusEffects.GetBuffPower(StatusEffectType.Thorns), Is.Zero);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [TestCase(10, false, false, 10)]
    [TestCase(10, true, false, 7)]
    [TestCase(10, false, true, 15)]
    [TestCase(10, true, true, 11)]
    [TestCase(12, true, true, 13)]
    [TestCase(1, true, false, 0)]
    [TestCase(-4, false, true, 0)]
    [TestCase(0, true, true, 0)]
    [TestCase(int.MaxValue, false, true, int.MaxValue)]
    public void DamageUsesAttackerAndDefenderAndRoundsOnce(int damage, bool rust, bool exposure, int expected)
    {
        Player attacker = CreatePlayer();
        Player defender = CreatePlayer();
        if (rust) attacker.StatusEffects.Apply(CreateStatus(StatusEffectType.Rust), 2);
        if (exposure) defender.StatusEffects.Apply(CreateStatus(StatusEffectType.WeaknessExposure), 2);
        Assert.That(DamageCalculator.Calculate(damage, attacker, defender), Is.EqualTo(expected));
        attacker.StatusEffects.Apply(CreateStatus(StatusEffectType.WeaknessExposure), 2);
        defender.StatusEffects.Apply(CreateStatus(StatusEffectType.Rust), 2);
        Assert.That(DamageCalculator.Calculate(damage, attacker, defender), Is.EqualTo(expected), "Opposite-role effects must not alter this attack.");
    }

    [Test]
    public void DurationsStackWithoutStackingPotencyAndOnlySelectedCharacterTicks()
    {
        Player first = CreatePlayer();
        Player second = CreatePlayer();
        StatusEffectData data = CreateStatus(StatusEffectType.Rust);
        first.StatusEffects.Apply(data, 2);
        first.StatusEffects.Apply(data, 3);
        second.StatusEffects.Apply(data, 1);
        first.StatusEffects.ReduceRemainingTurns();
        Assert.That(first.StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(4));
        Assert.That(second.StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(1));
        Assert.That(first.StatusEffects.ActiveEffects, Has.Count.EqualTo(1));
        Assert.That(DamageCalculator.Calculate(100, first, second), Is.EqualTo(75));
        second.StatusEffects.ReduceRemainingTurns();
        Assert.That(second.StatusEffects.ActiveEffects, Is.Empty);
        Assert.That(first.StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(4));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void InvalidStatusApplicationsHaveNoEffect(int turns)
    {
        var statuses = new StatusEffectSystem();
        int changes = 0;
        statuses.Changed += () => changes++;
        Assert.That(statuses.Apply(CreateStatus(StatusEffectType.Rust), turns), Is.False);
        Assert.That(statuses.Apply(null, 2), Is.False);
        Assert.That(statuses.Apply(CreateStatus((StatusEffectType)99), 2), Is.False);
        statuses.ReduceRemainingTurns();
        statuses.Clear();
        Assert.That(statuses.ActiveEffects, Is.Empty);
        Assert.That(changes, Is.Zero);
    }

    [Test]
    public void LargeDurationDoesNotOverflowAndClearNotifiesOnce()
    {
        var statuses = new StatusEffectSystem();
        StatusEffectData data = CreateStatus(StatusEffectType.Rust);
        statuses.Apply(data, int.MaxValue);
        statuses.Apply(data, int.MaxValue);
        Assert.That(statuses.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(int.MaxValue));
        int changes = 0;
        statuses.Changed += () => changes++;
        statuses.Clear();
        statuses.Clear();
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(statuses.ActiveEffects, Is.Empty);
    }

    [TestCase(3, 72, 0)]
    [TestCase(20, 80, 9)]
    public void BlockAbsorbsDamageAfterStatusMultipliers(int block, int hp, int remainingBlock)
    {
        Player attacker = CreatePlayer();
        Player defender = CreatePlayer();
        attacker.StatusEffects.Apply(CreateStatus(StatusEffectType.Rust), 1);
        defender.StatusEffects.Apply(CreateStatus(StatusEffectType.WeaknessExposure), 1);
        defender.AddBlock(block);
        defender.TakeDamage(10, attacker);
        Assert.That(defender.GetCurrentHP(), Is.EqualTo(hp));
        Assert.That(defender.GetBlock(), Is.EqualTo(remainingBlock));
    }

    [Test]
    public void DirectDamageAppliesReceivingStatusAndNegativeDamageDoesNotHeal()
    {
        Player player = CreatePlayer();
        player.StatusEffects.Apply(CreateStatus(StatusEffectType.WeaknessExposure), 2);
        player.TakeDamage(10);
        Assert.That(player.GetCurrentHP(), Is.EqualTo(65));
        player.AddBlock(3);
        player.TakeDamage(-5);
        Assert.That(player.GetCurrentHP(), Is.EqualTo(65));
        Assert.That(player.GetBlock(), Is.EqualTo(3));
    }

    [TestCase(CardTarget.Player, StatusEffectType.WeaknessExposure)]
    [TestCase(CardTarget.Enemy, StatusEffectType.WeaknessExposure)]
    [TestCase(CardTarget.Player, StatusEffectType.Rust)]
    [TestCase(CardTarget.Enemy, StatusEffectType.Rust)]
    public void CardEffectAppliesStatusToConfiguredTarget(CardTarget target, StatusEffectType type)
    {
        BattleManager manager = CreateBattle(out Player player, out Enemy enemy);
        ApplyStatusEffect effect = CreateEffect(CreateStatus(type), target, 3);
        effect.Execute(manager);
        Assert.That((target == CardTarget.Player ? player.StatusEffects : enemy.StatusEffects).GetRemainingTurns(type), Is.EqualTo(3));
        Assert.That((target == CardTarget.Player ? enemy.StatusEffects : player.StatusEffects).ActiveEffects, Is.Empty);
        Assert.DoesNotThrow(() => effect.Execute(null));
    }

    [Test]
    public void DamageCardAndEnemyActionUseTheSameCalculationAsPreview()
    {
        BattleManager manager = CreateBattle(out Player player, out Enemy enemy);
        player.StatusEffects.Apply(CreateStatus(StatusEffectType.Rust), 2);
        enemy.StatusEffects.Apply(CreateStatus(StatusEffectType.WeaknessExposure), 2);
        DamageEffect cardDamage = CreateAsset<DamageEffect>();
        cardDamage.damage = 10;
        SetField(cardDamage, "target", CardTarget.Enemy);
        cardDamage.Execute(manager);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(69));
        enemy.StatusEffects.Apply(CreateStatus(StatusEffectType.Rust), 2);
        player.StatusEffects.Apply(CreateStatus(StatusEffectType.WeaknessExposure), 2);
        EnemyAttackAction attack = CreateAsset<EnemyAttackAction>();
        SetField(attack, "damage", 10);
        Assert.That(attack.GetActionDescription(manager), Is.EqualTo("공격 11"));
        attack.Execute(manager);
        Assert.That(player.GetCurrentHP(), Is.EqualTo(69));
        Assert.That(attack.GetDamage(), Is.EqualTo(10), "Shared action data must remain unchanged.");
        Assert.That(attack.GetActionDescription(), Is.EqualTo("공격 10"));
    }

    [TestCase("WeaknessExposure", CardTarget.Player)]
    [TestCase("WeaknessExposure", CardTarget.Enemy)]
    [TestCase("Rust", CardTarget.Player)]
    [TestCase("Rust", CardTarget.Enemy)]
    public void ExampleCardsHaveValidStatusBindings(string name, CardTarget target)
    {
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>($"{StatusAssetSetup.ExampleFolder}/{name}_{target}.asset");
        Assert.That(card, Is.Not.Null);
        Assert.That(card.effects, Has.Length.EqualTo(1));
        Assert.That(card.effects[0], Is.InstanceOf<ApplyStatusEffect>());
        SerializedObject state = new SerializedObject(card.effects[0]);
        Assert.That(state.FindProperty("target").enumValueIndex, Is.EqualTo((int)target));
        Assert.That(state.FindProperty("turns").intValue, Is.EqualTo(2));
        Assert.That(state.FindProperty("status").objectReferenceValue, Is.Not.Null);
    }

    [Test]
    public void StatusHudDoesNotReceiveUpdatesWhileDisabledAfterEarlyBinding()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterInfoUI.prefab");
        GameObject instance = Object.Instantiate(prefab);
        created.Add(instance);
        StatusEffectsUI ui = instance.GetComponentInChildren<StatusEffectsUI>();
        Assert.That(ui, Is.Not.Null);
        var source = new StatusEffectSystem();
        StatusEffectData rust = CreateStatus(StatusEffectType.Rust);
        ui.Bind(source);
        typeof(StatusEffectsUI).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
        source.Apply(rust, 2);
        TMP_Text label = ui.GetComponentInChildren<StatusIconUI>().GetComponentInChildren<TMP_Text>();
        Assert.That(label.text, Is.EqualTo("2"));
        typeof(StatusEffectsUI).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
        source.Apply(rust, 3);
        Assert.That(label.text, Is.EqualTo("2"), "Each enable must create only one subscription.");
    }

    [UnityTest]
    public IEnumerator CardsShowStatusIconsAndBothSidesUpdateIntentAndDamageAtPlayerTurnStart()
    {
        yield return new EnterPlayMode();
        yield return VerifyCardStatusUpdatesAndTurns();
        yield return new ExitPlayMode();
    }

    private IEnumerator VerifyCardStatusUpdatesAndTurns()
    {
        AsyncOperation loading = SceneManager.LoadSceneAsync("BattleScene");
        Assert.That(loading, Is.Not.Null);
        float loadDeadline = Time.realtimeSinceStartup + 15f;
        while (!loading.isDone)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(loadDeadline), "BattleScene did not finish loading.");
            yield return null;
        }
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(manager, Is.Not.Null);
        yield return WaitForCards(manager);
        Player player = manager.GetPlayer();
        Enemy enemy = manager.GetTarget();
        EnemyAttackAction attack = CreateAsset<EnemyAttackAction>();
        SetField(attack, "damage", 10);
        EnemyBehavior behavior = CreateAsset<EnemyBehavior>();
        SetField(behavior, "actions", new EnemyAction[] { attack });
        SetField(enemy, "behavior", behavior);
        SetField(enemy, "currentActionIndex", 0);
        enemy.BindBattle(manager);
        TMP_Text intent = (TMP_Text)GetField(Object.FindFirstObjectByType<EnemyActionUI>(), "actionText");
        Assert.That(intent.text, Is.EqualTo("공격 10"));
        CardHandController hand = (CardHandController)GetField(manager, "dreamCardHand");
        UseCard(manager, hand, AssetDatabase.LoadAssetAtPath<CardData>($"{StatusAssetSetup.ExampleFolder}/WeaknessExposure_Player.asset"));
        yield return WaitForCards(manager);
        Assert.That(intent.text, Is.EqualTo("공격 15"));
        AssertIcon(player, StatusEffectType.WeaknessExposure, 2);
        CardData rust = CreateAsset<CardData>();
        rust.deckType = CardDeckType.TypeDream;
        rust.cost = new Cost(1, 0);
        rust.effects = new CardEffect[] { CreateEffect(CreateStatus(StatusEffectType.Rust), CardTarget.Enemy, 1) };
        UseCard(manager, hand, rust);
        yield return WaitForCards(manager);
        Assert.That(intent.text, Is.EqualTo("공격 11"));
        AssertIcon(enemy, StatusEffectType.Rust, 1);
        UseCard(manager, hand, AssetDatabase.LoadAssetAtPath<CardData>($"{StatusAssetSetup.ExampleFolder}/WeaknessExposure_Enemy.asset"));
        yield return WaitForCards(manager);
        Assert.That(enemy.GetComponentInChildren<StatusEffectsUI>().GetComponentsInChildren<StatusIconUI>(), Has.Length.EqualTo(2));
        Assert.That(intent.text, Is.EqualTo("공격 11"));
        int hp = player.GetCurrentHP();
        manager.EndPlayerTurn();
        Assert.That((BattleState)GetField(manager, "currentState"), Is.EqualTo(BattleState.PlayerTurnEnd));
        yield return WaitForPlayerTurn(manager);
        Assert.That(player.GetCurrentHP(), Is.EqualTo(hp - 11));
        AssertIcon(player, StatusEffectType.WeaknessExposure, 1);
        AssertIcon(enemy, StatusEffectType.WeaknessExposure, 1);
        Assert.That(enemy.StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.Zero);
        Assert.That(enemy.GetComponentInChildren<StatusEffectsUI>().GetComponentsInChildren<StatusIconUI>(), Has.Length.EqualTo(1));
        Assert.That(intent.text, Is.EqualTo("공격 15"));
        manager.EndPlayerTurn();
        Assert.That((BattleState)GetField(manager, "currentState"), Is.EqualTo(BattleState.PlayerTurnEnd));
        yield return WaitForPlayerTurn(manager);
        Assert.That(player.GetCurrentHP(), Is.EqualTo(hp - 26), "Player exposure remains active through the second enemy attack and expires at the next player turn start.");
        Assert.That(intent.text, Is.EqualTo("공격 10"));
        Assert.That(player.GetComponentInChildren<StatusIconUI>(), Is.Null);
        Assert.That(enemy.GetComponentInChildren<StatusIconUI>(), Is.Null);
        Assert.That(attack.GetDamage(), Is.EqualTo(10));
    }

    [UnityTest]
    public IEnumerator BothStatusesExpireBeforePlayerTurnStateBlockResetAndDraw()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return null;
        yield return new WaitForSeconds(0.8f);
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(manager, Is.Not.Null, "BattleScene must finish loading before checking turn order.");
        Player player = manager.GetPlayer();
        Enemy enemy = manager.GetTarget();
        EnemyAttackAction attack = CreateAsset<EnemyAttackAction>();
        SetField(attack, "damage", 10);
        EnemyBehavior behavior = CreateAsset<EnemyBehavior>();
        SetField(behavior, "actions", new EnemyAction[] { attack });
        SetField(enemy, "behavior", behavior);
        SetField(enemy, "currentActionIndex", 0);
        enemy.BindBattle(manager);
        player.StatusEffects.Apply(CreateStatus(StatusEffectType.WeaknessExposure), 1);
        enemy.StatusEffects.Apply(CreateStatus(StatusEffectType.Rust), 1);
        player.AddBlock(20);
        int hp = player.GetCurrentHP();
        CardHandController hand = (CardHandController)GetField(manager, "dreamCardHand");
        var observation = new StatusExpiryObservation(manager, player, enemy, hand);
        manager.EndPlayerTurn();
        yield return WaitForPlayerTurn(manager);
        observation.Stop();
        Assert.That(observation.States, Is.EqualTo(new[] { BattleState.EnemyTurnEnd, BattleState.EnemyTurnEnd }));
        Assert.That(observation.Blocks, Is.EqualTo(new[] { 9, 9 }), "Both one-turn effects must apply during the enemy attack, before the player block reset.");
        Assert.That(observation.CardCounts, Is.EqualTo(new[] { 0, 0 }), "Statuses must expire before drawing the next hand.");
        Assert.That(player.GetCurrentHP(), Is.EqualTo(hp));
        Assert.That(player.GetBlock(), Is.Zero);
        Assert.That(player.StatusEffects.ActiveEffects, Is.Empty);
        Assert.That(enemy.StatusEffects.ActiveEffects, Is.Empty);
        Assert.That(player.GetComponentInChildren<StatusIconUI>(), Is.Null);
        Assert.That(enemy.GetComponentInChildren<StatusIconUI>(), Is.Null);
        TMP_Text intent = (TMP_Text)GetField(Object.FindFirstObjectByType<EnemyActionUI>(), "actionText");
        Assert.That(intent.text, Is.EqualTo("공격 10"));
        Assert.That(((List<CardUI>)GetField(hand, "cards")).Count, Is.GreaterThan(0));
        yield return new ExitPlayMode();
    }

    // Construct after entering Play Mode: iterator-local closures cannot survive its domain reload.
    private sealed class StatusExpiryObservation
    {
        public readonly List<BattleState> States = new List<BattleState>();
        public readonly List<int> Blocks = new List<int>();
        public readonly List<int> CardCounts = new List<int>();
        private readonly BattleManager manager;
        private readonly Player player;
        private readonly Enemy enemy;
        private readonly CardHandController hand;

        public StatusExpiryObservation(BattleManager manager, Player player, Enemy enemy, CardHandController hand)
        {
            this.manager = manager;
            this.player = player;
            this.enemy = enemy;
            this.hand = hand;
            player.StatusEffects.Changed += Record;
            enemy.StatusEffects.Changed += Record;
        }

        private void Record()
        {
            States.Add((BattleState)GetField(manager, "currentState"));
            Blocks.Add(player.GetBlock());
            CardCounts.Add(((List<CardUI>)GetField(hand, "cards")).Count);
        }

        public void Stop()
        {
            player.StatusEffects.Changed -= Record;
            enemy.StatusEffects.Changed -= Record;
        }
    }

    [UnityTest]
    public IEnumerator StatusHudReactivationAndReapplicationReuseIconsAndRemoveExpiredOnes()
    {
        yield return new EnterPlayMode();
        GameObject canvas = new GameObject("Status Canvas", typeof(Canvas));
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterInfoUI.prefab");
        GameObject instance = Object.Instantiate(prefab, canvas.transform);
        StatusEffectsUI ui = instance.GetComponentInChildren<StatusEffectsUI>();
        var source = new StatusEffectSystem();
        ui.Bind(source);
        StatusEffectData rust = CreateStatus(StatusEffectType.Rust);
        source.Apply(rust, 2);
        StatusIconUI icon = ui.GetComponentInChildren<StatusIconUI>();
        TMP_Text text = icon.GetComponentInChildren<TMP_Text>();
        ui.gameObject.SetActive(false);
        source.Apply(rust, 1);
        Assert.That(text.text, Is.EqualTo("2"));
        ui.gameObject.SetActive(true);
        Assert.That(text.text, Is.EqualTo("3"));
        Assert.That(ui.GetComponentInChildren<StatusIconUI>(), Is.SameAs(icon));
        source.Apply(CreateStatus(StatusEffectType.WeaknessExposure), 1);
        Assert.That(ui.GetComponentsInChildren<StatusIconUI>(), Has.Length.EqualTo(2));
        source.ReduceRemainingTurns();
        Assert.That(text.text, Is.EqualTo("2"));
        Assert.That(ui.GetComponentsInChildren<StatusIconUI>(), Has.Length.EqualTo(1));
        source.ReduceRemainingTurns();
        source.ReduceRemainingTurns();
        Assert.That(ui.GetComponentsInChildren<StatusIconUI>(), Is.Empty);
        Object.Destroy(canvas);
        yield return null;
        yield return new ExitPlayMode();
    }

    private static StatusIconUI AssertIcon(BattleCharacter character, StatusEffectType type, int amount, Sprite expectedSprite = null)
    {
        StatusEffectsUI ui = character.GetComponentInChildren<StatusEffectsUI>();
        Assert.That(ui, Is.Not.Null);
        Assert.That(ui.name, Is.EqualTo("StatusContainer"));
        StatusIconUI icon = null;
        foreach (StatusIconUI item in ui.GetComponentsInChildren<StatusIconUI>()) if (item.StatusType == type) icon = item;
        Assert.That(icon, Is.Not.Null);
        TMP_Text label = icon.GetComponentInChildren<TMP_Text>();
        Assert.That(label.text, Is.EqualTo(amount.ToString()));
        Assert.That(label.rectTransform.anchorMin, Is.EqualTo(new Vector2(1, 0)));
        Assert.That(label.rectTransform.pivot, Is.EqualTo(new Vector2(1, 0)));
        Sprite sprite = icon.GetComponent<UnityEngine.UI.Image>().sprite;
        Assert.That(sprite, Is.Not.Null);
        if (expectedSprite != null) Assert.That(sprite, Is.SameAs(expectedSprite));
        return icon;
    }

    private static void UseCard(BattleManager manager, CardHandController hand, CardData data)
    {
        CardUI card = ((List<CardUI>)GetField(hand, "cards"))[0];
        card.SetCard(data, manager);
        Assert.That(manager.UseCard(card), Is.True);
    }

    [UnityTest]
    public IEnumerator BurnDamagesBothSidesOnceAtPlayerTurnEndAndExpires()
    {
        yield return new EnterPlayMode();
        yield return VerifyBurnDamageAndDecay();
        yield return new ExitPlayMode();
    }

    private IEnumerator VerifyBurnDamageAndDecay()
    {
        yield return LoadBattleForBurn();
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(manager, Is.Not.Null);
        yield return WaitForCards(manager);
        var burn = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/Burn.asset");
        Assert.That(burn, Is.Not.Null);
        Assert.That(burn.Type, Is.EqualTo(StatusEffectType.Burn));
        var burnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/UI/StatusIcon/burn.png");
        Assert.That(burnSprite, Is.Not.Null);
        Assert.That(burn.Icon, Is.SameAs(burnSprite));
        Player player = manager.GetPlayer();
        Enemy enemy = manager.GetTarget();
        EnemyBehavior behavior = CreateAsset<EnemyBehavior>();
        SetField(behavior, "actions", new EnemyAction[] { CreateAsset<EnemyBlockAction>() });
        SetField(enemy, "behavior", behavior);
        SetField(enemy, "currentActionIndex", 0);
        enemy.BindBattle(manager);

        CreateEffect(burn, CardTarget.Enemy, 4).Execute(manager);
        CreateEffect(burn, CardTarget.Enemy, 6).Execute(manager);
        player.StatusEffects.Apply(burn, 2);
        Assert.That(enemy.StatusEffects.ActiveEffects[0].StackCount, Is.EqualTo(10));
        Assert.That(enemy.StatusEffects.GetRemainingTurns(burn.Type), Is.Zero);
        StatusIconUI enemyBurnIcon = AssertIcon(enemy, burn.Type, 10, burnSprite);
        AssertIcon(player, burn.Type, 2, burnSprite);
        enemyBurnIcon.OnPointerEnter(new PointerEventData(EventSystem.current));
        var tooltip = (StatusTooltipUI)GetField(enemyBurnIcon, "tooltip");
        Assert.That(tooltip.gameObject.activeInHierarchy, Is.True);
        var tooltipText = (TMP_Text)GetField(tooltip, "contentText");
        Assert.That(tooltipText.text, Does.EndWith("중첩: 10"));
        enemyBurnIcon.CloseTooltip();
        enemy.StatusEffects.Apply(CreateStatus(StatusEffectType.WeaknessExposure), 1);
        enemy.AddBlock(20);
        int enemyHP = enemy.GetCurrentHP();
        int playerHP = player.GetCurrentHP();
        int enemyBlockAtTurnStart = -1;
        manager.OpeningChanged += () =>
        {
            if ((BattleState)GetField(manager, "currentState") == BattleState.EnemyTurn && enemyBlockAtTurnStart < 0)
                enemyBlockAtTurnStart = enemy.GetBlock();
        };

        var endTurn = GameObject.Find("TurnEndButton").GetComponent<UnityEngine.UI.Button>();
        endTurn.onClick.Invoke();
        endTurn.onClick.Invoke();
        yield return WaitForPlayerTurn(manager);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(enemyHP - 10));
        Assert.That(player.GetCurrentHP(), Is.EqualTo(playerHP - 2));
        Assert.That(enemyBlockAtTurnStart, Is.EqualTo(20));
        Assert.That(enemy.StatusEffects.ActiveEffects[0].StackCount, Is.EqualTo(9));
        Assert.That(player.StatusEffects.ActiveEffects[0].StackCount, Is.EqualTo(1));
        AssertIcon(enemy, burn.Type, 9, burnSprite);
        AssertIcon(player, burn.Type, 1, burnSprite);

        endTurn.onClick.Invoke();
        yield return WaitForPlayerTurn(manager);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(enemyHP - 19));
        Assert.That(enemy.StatusEffects.ActiveEffects[0].StackCount, Is.EqualTo(8));
        Assert.That(player.GetCurrentHP(), Is.EqualTo(playerHP - 3));
        Assert.That(player.StatusEffects.HasStatus(burn.Type), Is.False);
        Assert.That(player.GetComponentInChildren<StatusIconUI>(), Is.Null);
        AssertIcon(enemy, burn.Type, 8, burnSprite);
    }

    [UnityTest]
    public IEnumerator LethalBurnEndsBattleBeforeTheEnemyTurn()
    {
        yield return new EnterPlayMode();
        yield return VerifyLethalBurn();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyLethalBurn()
    {
        yield return LoadBattleForBurn();
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(manager, Is.Not.Null);
        yield return WaitForCards(manager);
        var burn = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/Burn.asset");
        Assert.That(burn, Is.Not.Null);
        Enemy enemy = manager.GetTarget();
        enemy.RestoreHealth(10, enemy.GetMaxHP());
        enemy.StatusEffects.Apply(burn, 10);
        bool enemyTurnStarted = false;
        manager.OpeningChanged += () => enemyTurnStarted |= (BattleState)GetField(manager, "currentState") == BattleState.EnemyTurn;
        GameObject.Find("TurnEndButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

        float deadline = Time.realtimeSinceStartup + 15f;
        while (manager.CanContinueEffects)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Lethal burn did not finish the battle.");
            yield return null;
        }
        Assert.That(enemy.GetCurrentHP(), Is.Zero);
        Assert.That(manager.Result, Is.EqualTo(BattleResult.Victory));
        Assert.That(enemyTurnStarted, Is.False);
    }

    private static IEnumerator LoadBattleForBurn()
    {
        AsyncOperation loading = SceneManager.LoadSceneAsync("BattleScene");
        Assert.That(loading, Is.Not.Null);
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!loading.isDone)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "BattleScene did not finish loading.");
            yield return null;
        }
    }

    private static IEnumerator WaitForCards(BattleManager manager)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!manager.CanInspectCards)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Card effects and hand animations did not finish.");
            yield return null;
        }
        Assert.That((int)GetField(manager, "activeCardUses"), Is.Zero);
    }

    private static IEnumerator WaitForPlayerTurn(BattleManager manager)
    {
        yield return WaitForCards(manager);
        Assert.That((BattleState)GetField(manager, "currentState"), Is.EqualTo(BattleState.PlayerTurn));
    }

    private Player CreatePlayer()
    {
        Player player = CreateObject("Player").AddComponent<Player>();
        SetField(player, "currentHP", 80);
        return player;
    }

    private BattleManager CreateBattle(out Player player, out Enemy enemy)
    {
        player = CreatePlayer();
        enemy = CreateObject("Enemy").AddComponent<Enemy>();
        SetField(enemy, "currentHP", 80);
        BattleManager manager = CreateObject("Battle").AddComponent<BattleManager>();
        SetField(manager, "player", player);
        SetField(manager, "target", enemy);
        return manager;
    }

    private StatusEffectData CreateStatus(StatusEffectType type)
    {
        StatusEffectData data = CreateAsset<StatusEffectData>();
        SetField(data, "statusType", type);
        return data;
    }

    private StatusEffectData CreateBuff(BuffDuration duration)
    {
        StatusEffectData data = CreateStatus(StatusEffectType.Thorns);
        SetField(data, "category", StatusEffectCategory.Buff);
        SetField(data, "buffDuration", duration);
        return data;
    }

    private ApplyStatusEffect CreateEffect(StatusEffectData status, CardTarget target, int turns)
    {
        ApplyStatusEffect effect = CreateAsset<ApplyStatusEffect>();
        SetField(effect, "status", status);
        SetField(effect, "target", target);
        SetField(effect, "turns", turns);
        return effect;
    }

    private GameObject CreateObject(string name)
    {
        GameObject obj = new GameObject(name);
        created.Add(obj);
        return obj;
    }

    private T CreateAsset<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        created.Add(asset);
        return asset;
    }

    private static FieldInfo Field(object obj, string name)
    {
        for (System.Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) return field;
        }
        throw new System.ArgumentException(name);
    }

    private static object GetField(object obj, string name) => Field(obj, name).GetValue(obj);
    private static void SetField(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
}
