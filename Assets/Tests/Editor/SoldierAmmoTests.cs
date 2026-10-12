using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class SoldierAmmoTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void RequiredAmmoWithoutAmmoSystemRejectsBeforePayingOrExecutingEffects()
    {
        GameObject playerObject = CreateObject("Player");
        Player player = playerObject.AddComponent<Player>();
        CostSystem cost = playerObject.AddComponent<CostSystem>();
        cost.ResetForTurn();
        SetField(player, "costSystem", cost);

        BattleManager manager = CreateObject("BattleManager").AddComponent<BattleManager>();
        SetField(manager, "player", player);
        SetField(manager, "currentState", BattleState.PlayerTurn);

        CardData card = CreateAsset<CardData>();
        card.deckType = CardDeckType.TypeDream;
        card.cost = new Cost(1, 0);
        JsonUtility.FromJsonOverwrite("{\"requiredAmmo\":2,\"consumeRequiredAmmo\":true}", card);
        BlockEffect effect = CreateAsset<BlockEffect>();
        effect.block = 4;
        card.effects = new CardEffect[] { effect };

        CardUI ui = CreateObject("Card").AddComponent<CardUI>();
        SetField(ui, "cardData", card);

        Assert.That(manager.UseCard(ui), Is.False);
        Assert.That(cost.GetDreamCost(), Is.EqualTo(3));
        Assert.That(player.GetBlock(), Is.Zero);
    }

    [TestCase(0, 3, 3)]
    [TestCase(9, 3, 10)]
    [TestCase(10, 3, 10)]
    [TestCase(0, int.MaxValue, 10)]
    public void ReloadClampsToCapacity(int start, int amount, int expected)
    {
        AmmoSystem ammo = CreateAmmo(10, start);
        ammo.Load(amount);
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(expected));
    }

    [TestCase(0, 2, false, 0)]
    [TestCase(1, 2, false, 1)]
    [TestCase(2, 2, true, 0)]
    [TestCase(5, 2, true, 3)]
    [TestCase(5, -1, false, 5)]
    public void SpendRequiresTheWholeAmount(int start, int amount, bool accepted, int remaining)
    {
        AmmoSystem ammo = CreateAmmo(10, start);
        Assert.That(ammo.TrySpend(amount), Is.EqualTo(accepted));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(remaining));
    }

    [TestCase(4, 9, 4, 4)]
    [TestCase(-1, 5, 0, 0)]
    [TestCase(10, -3, 10, 0)]
    public void BattleInitializationNormalizesInspectorValues(int max, int start, int expectedMax, int expected)
    {
        AmmoSystem ammo = CreateAmmo(max, start);
        Assert.That(ammo.MaxAmmo, Is.EqualTo(expectedMax));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(expected));
    }

    [Test]
    public void AmmoChangesNotifyOnlyWhenTheValueChanges()
    {
        AmmoSystem ammo = CreateAmmo(10, 9);
        var observations = new List<int>();
        ammo.AmmoChanged += (current, maximum) => observations.Add(current);
        ammo.Load(5);
        ammo.Load(5);
        ammo.TrySpend(11);
        ammo.TrySpend(2);
        CollectionAssert.AreEqual(new[] { 10, 8 }, observations);
    }

    [TestCase(0, 1)]
    [TestCase(1, 1)]
    [TestCase(2, 8)]
    [TestCase(5, 8)]
    public void OptionalBonusExecutesAllEffectsWithOneAmmoPayment(int start, int expectedBlock)
    {
        Player player;
        AmmoSystem ammo;
        CostSystem cost;
        BattleManager manager = CreateBattle(start, out player, out ammo, out cost);
        CardData card = CreateCard(1);
        BlockEffect basic = CreateAsset<BlockEffect>();
        basic.block = 1;
        BlockEffect bonusOne = CreateAsset<BlockEffect>();
        bonusOne.block = 3;
        BlockEffect bonusTwo = CreateAsset<BlockEffect>();
        bonusTwo.block = 4;
        SpendAmmoBonusEffect bonus = CreateAsset<SpendAmmoBonusEffect>();
        SetField(bonus, "ammoCost", 2);
        SetField(bonus, "bonusEffects", new CardEffect[] { bonusOne, null, bonusTwo });
        card.effects = new CardEffect[] { basic, bonus };

        Assert.That(manager.UseCard(CreateCardUI(card)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(expectedBlock));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(start >= 2 ? start - 2 : start));
        Assert.That(cost.GetDreamCost(), Is.EqualTo(2));
    }

    [TestCase(false, 3)]
    [TestCase(true, 0)]
    public void RequiredAmmoCanCheckPossessionOrConsume(bool consume, int remaining)
    {
        Player player;
        AmmoSystem ammo;
        CostSystem cost;
        BattleManager manager = CreateBattle(3, out player, out ammo, out cost);
        CardData card = CreateBlockCard(1, 4);
        card.requiredAmmo = 3;
        card.consumeRequiredAmmo = consume;

        Assert.That(manager.CanUseCard(card), Is.True);
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(3), "Preview must not spend ammo.");
        Assert.That(manager.UseCard(CreateCardUI(card)), Is.True);
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(remaining));
        Assert.That(player.GetBlock(), Is.EqualTo(4));
        Assert.That(cost.GetDreamCost(), Is.EqualTo(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InsufficientRequiredAmmoPreservesEveryResourceAndTheCard(bool consume)
    {
        Player player;
        AmmoSystem ammo;
        CostSystem cost;
        BattleManager manager = CreateBattle(2, out player, out ammo, out cost);
        CardData card = CreateBlockCard(1, 4);
        card.requiredAmmo = 3;
        card.consumeRequiredAmmo = consume;
        CardUI ui = CreateCardUI(card);

        Assert.That(manager.CanUseCard(card), Is.False);
        Assert.That(manager.UseCard(ui), Is.False);
        Assert.That(ui.IsUsed, Is.False);
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(2));
        Assert.That(cost.GetDreamCost(), Is.EqualTo(3));
        Assert.That(player.GetBlock(), Is.Zero);
    }

    [Test]
    public void InsufficientCardCostDoesNotConsumeRequiredAmmo()
    {
        Player player;
        AmmoSystem ammo;
        CostSystem cost;
        BattleManager manager = CreateBattle(5, out player, out ammo, out cost);
        CardData card = CreateBlockCard(4, 4);
        card.requiredAmmo = 3;
        card.consumeRequiredAmmo = true;
        Assert.That(manager.UseCard(CreateCardUI(card)), Is.False);
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(5));
        Assert.That(cost.GetDreamCost(), Is.EqualTo(3));
        Assert.That(player.GetBlock(), Is.Zero);
    }

    [Test]
    public void RequiredSpendHappensBeforeReloadAndOptionalBonus()
    {
        Player player;
        AmmoSystem ammo;
        CostSystem cost;
        BattleManager manager = CreateBattle(3, out player, out ammo, out cost);
        CardData card = CreateCard(1);
        card.requiredAmmo = 3;
        card.consumeRequiredAmmo = true;
        ReloadAmmoEffect reload = CreateAsset<ReloadAmmoEffect>();
        SetField(reload, "amount", 2);
        BlockEffect block = CreateAsset<BlockEffect>();
        block.block = 4;
        SpendAmmoBonusEffect bonus = CreateAsset<SpendAmmoBonusEffect>();
        SetField(bonus, "ammoCost", 2);
        SetField(bonus, "bonusEffects", new CardEffect[] { block });
        card.effects = new CardEffect[] { reload, bonus };
        Assert.That(manager.UseCard(CreateCardUI(card)), Is.True);
        Assert.That(ammo.CurrentAmmo, Is.Zero);
        Assert.That(player.GetBlock(), Is.EqualTo(4));
    }

    [Test]
    public void ReloadCannotSatisfyItsOwnPreUseRequirement()
    {
        Player player;
        AmmoSystem ammo;
        CostSystem cost;
        BattleManager manager = CreateBattle(0, out player, out ammo, out cost);
        CardData card = CreateCard(1);
        card.requiredAmmo = 1;
        ReloadAmmoEffect reload = CreateAsset<ReloadAmmoEffect>();
        SetField(reload, "amount", 3);
        card.effects = new CardEffect[] { reload };
        Assert.That(manager.UseCard(CreateCardUI(card)), Is.False);
        Assert.That(ammo.CurrentAmmo, Is.Zero);
        Assert.That(cost.GetDreamCost(), Is.EqualTo(3));
    }

    [Test]
    public void UsedCardCannotPayOrExecuteAgain()
    {
        Player player;
        AmmoSystem ammo;
        CostSystem cost;
        BattleManager manager = CreateBattle(6, out player, out ammo, out cost);
        CardData card = CreateBlockCard(1, 4);
        card.requiredAmmo = 2;
        card.consumeRequiredAmmo = true;
        CardUI ui = CreateCardUI(card);
        Assert.That(manager.UseCard(ui), Is.True);
        Assert.That(manager.UseCard(ui), Is.False);
        Assert.That(cost.GetDreamCost(), Is.EqualTo(2));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(4));
        Assert.That(player.GetBlock(), Is.EqualTo(4));
    }

    [Test]
    public void LegacyCardWorksWithoutAmmoAndNullEffectsAreAllowed()
    {
        Player player;
        AmmoSystem ammo;
        CostSystem cost;
        BattleManager manager = CreateBattle(0, out player, out ammo, out cost, false);
        CardData card = CreateBlockCard(1, 4);
        Assert.That(manager.UseCard(CreateCardUI(card)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(4));
        card = CreateCard(1);
        card.effects = null;
        Assert.That(manager.UseCard(CreateCardUI(card)), Is.True);
        Assert.That(cost.GetDreamCost(), Is.EqualTo(1));
    }

    [Test]
    public void NonPlayerTurnCannotSpendAmmoOrCost()
    {
        Player player;
        AmmoSystem ammo;
        CostSystem cost;
        BattleManager manager = CreateBattle(5, out player, out ammo, out cost);
        SetField(manager, "currentState", BattleState.EnemyTurn);
        CardData card = CreateBlockCard(1, 4);
        card.requiredAmmo = 2;
        card.consumeRequiredAmmo = true;
        Assert.That(manager.CanUseCard(card), Is.False);
        Assert.That(manager.UseCard(CreateCardUI(card)), Is.False);
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(5));
        Assert.That(cost.GetDreamCost(), Is.EqualTo(3));
    }

    [Test]
    public void ReloadAtCapacityStillPaysCardCostAndExecutesOtherEffects()
    {
        Player player;
        AmmoSystem ammo;
        CostSystem cost;
        BattleManager manager = CreateBattle(10, out player, out ammo, out cost);
        ReloadAmmoEffect reload = CreateAsset<ReloadAmmoEffect>();
        SetField(reload, "amount", 3);
        BlockEffect block = CreateAsset<BlockEffect>();
        block.block = 4;
        CardData card = CreateCard(1);
        card.effects = new CardEffect[] { reload, block };
        Assert.That(manager.UseCard(CreateCardUI(card)), Is.True);
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(10));
        Assert.That(cost.GetDreamCost(), Is.EqualTo(2));
        Assert.That(player.GetBlock(), Is.EqualTo(4));
    }

    [Test]
    public void OptionalBonusWithoutAmmoSystemKeepsTheBasicEffect()
    {
        Player player;
        AmmoSystem ammo;
        CostSystem cost;
        BattleManager manager = CreateBattle(0, out player, out ammo, out cost, false);
        BlockEffect extra = CreateAsset<BlockEffect>();
        extra.block = 7;
        SpendAmmoBonusEffect bonus = CreateAsset<SpendAmmoBonusEffect>();
        SetField(bonus, "bonusEffects", new CardEffect[] { extra });
        CardData card = CreateBlockCard(1, 1);
        card.effects = new CardEffect[] { card.effects[0], bonus };
        Assert.That(manager.UseCard(CreateCardUI(card)), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(1));
        Assert.That(cost.GetDreamCost(), Is.EqualTo(2));
    }

    [Test]
    public void SharedBonusAssetUsesEachPlayersCurrentAmmo()
    {
        SpendAmmoBonusEffect bonus = CreateAsset<SpendAmmoBonusEffect>();
        BlockEffect block = CreateAsset<BlockEffect>();
        block.block = 4;
        SetField(bonus, "bonusEffects", new CardEffect[] { block });
        CardData card = CreateCard(0);
        card.effects = new CardEffect[] { bonus };
        Player firstPlayer;
        AmmoSystem firstAmmo;
        CostSystem firstCost;
        BattleManager first = CreateBattle(1, out firstPlayer, out firstAmmo, out firstCost);
        Player secondPlayer;
        AmmoSystem secondAmmo;
        CostSystem secondCost;
        BattleManager second = CreateBattle(0, out secondPlayer, out secondAmmo, out secondCost);
        Assert.That(first.UseCard(CreateCardUI(card)), Is.True);
        Assert.That(second.UseCard(CreateCardUI(card)), Is.True);
        Assert.That(firstPlayer.GetBlock(), Is.EqualTo(4));
        Assert.That(firstAmmo.CurrentAmmo, Is.Zero);
        Assert.That(secondPlayer.GetBlock(), Is.Zero);
        Assert.That(secondAmmo.CurrentAmmo, Is.Zero);
    }

    [TestCase(CardDeckType.TypeDream, -1)]
    [TestCase(CardDeckType.TypeNightmare, -1)]
    public void NegativeCardCostCannotGenerateResources(CardDeckType deckType, int amount)
    {
        Player player;
        AmmoSystem ammo;
        CostSystem cost;
        BattleManager manager = CreateBattle(5, out player, out ammo, out cost);
        CardData card = CreateCard(0);
        card.deckType = deckType;
        card.cost = new Cost(amount, amount);
        Assert.That(manager.UseCard(CreateCardUI(card)), Is.False);
        Assert.That(cost.GetDreamCost(), Is.EqualTo(3));
        Assert.That(cost.GetNightmareCost(), Is.Zero);
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(5));
    }

    [Test]
    public void NightmareCardPaysNightmareCostAndRequiredAmmo()
    {
        Player player;
        AmmoSystem ammo;
        CostSystem cost;
        BattleManager manager = CreateBattle(3, out player, out ammo, out cost);
        cost.AddNightmareCost(2);
        CardData card = CreateBlockCard(0, 4);
        card.deckType = CardDeckType.TypeNightmare;
        card.cost = new Cost(0, 2);
        card.requiredAmmo = 3;
        card.consumeRequiredAmmo = true;
        Assert.That(manager.UseCard(CreateCardUI(card)), Is.True);
        Assert.That(cost.GetDreamCost(), Is.EqualTo(3));
        Assert.That(cost.GetNightmareCost(), Is.Zero);
        Assert.That(ammo.CurrentAmmo, Is.Zero);
        Assert.That(player.GetBlock(), Is.EqualTo(4));
    }

    [Test]
    public void SoldierPrefabHasLocalPlayerAmmoAndNoBattleHud()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierAssetSetup.PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Soldier soldier = prefab.GetComponent<Soldier>();
        Assert.That(soldier, Is.Not.Null);
        Assert.That(prefab.GetComponents<Player>().Length, Is.EqualTo(1));
        AmmoSystem ammo = soldier.GetAmmoSystem();
        Assert.That(ammo.MaxAmmo, Is.EqualTo(10));
        SerializedObject state = new SerializedObject(ammo);
        Assert.That(state.FindProperty("startingAmmo").intValue, Is.Zero);
        Assert.That(soldier.GetCostSystem(), Is.SameAs(prefab.GetComponent<CostSystem>()));
        Assert.That(prefab.GetComponent<DeckController>(), Is.Not.Null);
        Assert.That(prefab.GetComponentInChildren<AmmoUI>(true), Is.Null);
        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab), Is.Zero);
    }

    [TestCase("Reload", 0, false)]
    [TestCase("AmmoBonus", 0, false)]
    [TestCase("RequireAmmo", 1, false)]
    [TestCase("ConsumeAmmo", 1, true)]
    public void ExampleCardsHaveValidEffectAndRequirementBindings(string name, int required, bool consume)
    {
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>($"Assets/CardData/Soldier/Examples/{name}.asset");
        Assert.That(card, Is.Not.Null);
        Assert.That(card.requiredAmmo, Is.EqualTo(required));
        Assert.That(card.consumeRequiredAmmo, Is.EqualTo(consume));
        Assert.That(card.effects, Is.Not.Empty);
        foreach (CardEffect effect in card.effects) Assert.That(effect, Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator SoldierPlaysInBattleSceneAndTurnEndWaitsForCardAnimation()
    {
        yield return new EnterPlayMode();
        SceneManager.LoadScene("BattleScene");
        yield return null;
        yield return new WaitForSeconds(0.8f);

        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        Soldier soldier = manager.GetPlayer() as Soldier;
        Assert.That(soldier, Is.Not.Null, "BattleScene must use Soldier directly.");
        Assert.That(Object.FindObjectsByType<Player>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(GetField(manager, "deckController"), Is.SameAs(soldier.GetComponent<DeckController>()));
        Assert.That(soldier.GetCostSystem(), Is.SameAs(soldier.GetComponent<CostSystem>()));
        SerializedObject costState = new SerializedObject(soldier.GetCostSystem());
        foreach (string field in new[] { "dreamCostUI", "nightmareCostUI", "dreamGaugeUI", "nightmareGaugeUI" })
            Assert.That(costState.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        Assert.That(soldier.GetAmmoSystem().MaxAmmo, Is.EqualTo(10));
        Assert.That(soldier.GetAmmoSystem().CurrentAmmo, Is.Zero);

        CardHandController hand = (CardHandController)GetField(manager, "dreamCardHand");
        List<CardUI> cards = (List<CardUI>)GetField(hand, "cards");
        CardUI ui = cards[0];
        CardData reload = AssetDatabase.LoadAssetAtPath<CardData>("Assets/CardData/Soldier/Examples/Reload.asset");
        ui.SetCard(reload, manager);
        Assert.That(manager.UseCard(ui), Is.True);
        Assert.That(manager.UseCard(ui), Is.False);
        Assert.That(soldier.GetAmmoSystem().CurrentAmmo, Is.EqualTo(1));
        Assert.That(hand.GetCurrentHandSize(), Is.EqualTo(2));

        manager.EndPlayerTurn();
        Assert.That((BattleState)GetField(manager, "currentState"), Is.EqualTo(BattleState.PlayerTurnEnd));
        Assert.That((int)GetField(manager, "activeCardUses"), Is.EqualTo(1));
        float deadline = Time.realtimeSinceStartup + 12;
        while ((BattleState)GetField(manager, "currentState") != BattleState.PlayerTurn &&
            Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That((BattleState)GetField(manager, "currentState"), Is.EqualTo(BattleState.PlayerTurn));
        Assert.That((int)GetField(manager, "activeCardUses"), Is.Zero);
        Assert.That(hand.GetCurrentHandSize(), Is.EqualTo(hand.GetMaxHandSize()));
        Assert.That(soldier.GetAmmoSystem().CurrentAmmo, Is.EqualTo(1), "Ammo survives a turn change.");

        cards = (List<CardUI>)GetField(hand, "cards");
        CardData consume = AssetDatabase.LoadAssetAtPath<CardData>("Assets/CardData/Soldier/Examples/ConsumeAmmo.asset");
        ui = cards[0];
        ui.SetCard(consume, manager);
        int targetHP = manager.GetTarget().GetCurrentHP();
        Assert.That(manager.UseCard(ui), Is.True);
        Assert.That(soldier.GetAmmoSystem().CurrentAmmo, Is.Zero);
        Assert.That(manager.GetTarget().GetCurrentHP(), Is.LessThan(targetHP));
        Assert.That(Object.FindFirstObjectByType<AmmoUI>().GetComponentInChildren<TMP_Text>().text, Is.EqualTo("총알 0 / 10"));
        deadline = Time.realtimeSinceStartup + 5;
        while ((int)GetField(manager, "activeCardUses") > 0 && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That((int)GetField(manager, "activeCardUses"), Is.Zero);
        Assert.That(hand.GetCurrentHandSize(), Is.EqualTo(hand.GetMaxHandSize()));
        Assert.That(manager.CanUseCard(consume), Is.False);

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator InspectorAmmoCapacityChangesRefreshTheRuntimeUi()
    {
        yield return new EnterPlayMode();
        SceneManager.LoadScene("BattleScene");
        yield return null;
        yield return new WaitForSeconds(0.8f);
        AmmoSystem ammo = Object.FindFirstObjectByType<BattleManager>().GetPlayer().GetAmmoSystem();
        TMP_Text label = Object.FindFirstObjectByType<AmmoUI>().GetComponentInChildren<TMP_Text>();
        Assert.That(label.text, Is.EqualTo("총알 0 / 10"));
        SerializedObject state = new SerializedObject(ammo);
        state.FindProperty("maxAmmo").intValue = 5;
        state.FindProperty("startingAmmo").intValue = 2;
        state.ApplyModifiedPropertiesWithoutUndo();
        yield return null;
        Assert.That(label.text, Is.EqualTo("총알 0 / 5"));
        Assert.That(ammo.CurrentAmmo, Is.Zero, "Changing starting ammo does not reset a running battle.");
        yield return new ExitPlayMode();
    }

    private AmmoSystem CreateAmmo(int max, int start)
    {
        AmmoSystem ammo = CreateObject("Ammo").AddComponent<AmmoSystem>();
        SetField(ammo, "maxAmmo", max);
        SetField(ammo, "startingAmmo", start);
        ammo.ResetForBattle();
        return ammo;
    }

    private BattleManager CreateBattle(int start, out Player player, out AmmoSystem ammo, out CostSystem cost, bool soldier = true)
    {
        GameObject playerObject = CreateObject("Player");
        player = soldier ? (Player)playerObject.AddComponent<Soldier>() : playerObject.AddComponent<Player>();
        ammo = player.GetAmmoSystem();
        if (ammo != null)
        {
            SetField(ammo, "startingAmmo", start);
            ammo.ResetForBattle();
        }
        cost = playerObject.GetComponent<CostSystem>();
        if (cost == null) cost = playerObject.AddComponent<CostSystem>();
        cost.ResetForTurn();
        SetField(player, "costSystem", cost);
        BattleManager manager = CreateObject("BattleManager").AddComponent<BattleManager>();
        SetField(manager, "player", player);
        SetField(manager, "currentState", BattleState.PlayerTurn);
        return manager;
    }

    private CardData CreateCard(int dreamCost)
    {
        CardData card = CreateAsset<CardData>();
        card.deckType = CardDeckType.TypeDream;
        card.cost = new Cost(dreamCost, 0);
        card.effects = new CardEffect[0];
        return card;
    }

    private CardData CreateBlockCard(int cost, int block)
    {
        CardData card = CreateCard(cost);
        BlockEffect effect = CreateAsset<BlockEffect>();
        effect.block = block;
        card.effects = new CardEffect[] { effect };
        return card;
    }

    private CardUI CreateCardUI(CardData card)
    {
        CardUI ui = CreateObject("Card").AddComponent<CardUI>();
        SetField(ui, "cardData", card);
        return ui;
    }

    private GameObject CreateObject(string name)
    {
        GameObject value = new GameObject(name);
        created.Add(value);
        return value;
    }

    private T CreateAsset<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        created.Add(value);
        return value;
    }

    private static void SetField(object instance, string name, object value)
    {
        Type type = instance.GetType();
        FieldInfo field = null;
        while (type != null && field == null)
        {
            field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            type = type.BaseType;
        }
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(instance, value);
    }

    private static object GetField(object instance, string name)
    {
        return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    }
}
