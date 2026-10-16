using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class StartingDeckTests
{
    private const string DreamPath = "Assets/CardDatabase/Soldier/Dream/SoldierDreamStartingDeck.asset";
    private const string NightmarePath = "Assets/CardDatabase/Soldier/Nightmare/SoldierNightmareStartingDeck.asset";
    private readonly List<Object> created = new List<Object>();

    [SetUp]
    public void SetUp() => ResetRunIfAvailable();

    [TearDown]
    public void TearDown()
    {
        ResetRunIfAvailable();
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void SoldierPrefabReferencesBothStartingDatabases()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierAssetSetup.PrefabPath);
        SerializedObject state = new SerializedObject(prefab.GetComponent<DeckController>());
        AssertDatabase(state, "dreamStartingDeckDatabase", DreamPath);
        AssertDatabase(state, "nightmareStartingDeckDatabase", NightmarePath);
    }

    [Test]
    public void BattleSceneInheritsStartingDatabasesFromSoldierPrefabAndRetainsHudBindings()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity", OpenSceneMode.Additive);
        try
        {
            Soldier soldier = null;
            BattleManager manager = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (soldier == null) soldier = root.GetComponentInChildren<Soldier>(true);
                if (manager == null) manager = root.GetComponentInChildren<BattleManager>(true);
            }
            Assert.That(soldier, Is.Not.Null);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(soldier.gameObject),
                Is.EqualTo(SoldierAssetSetup.PrefabPath));
            DeckController deck = soldier.GetComponent<DeckController>();
            SerializedObject state = new SerializedObject(deck);
            AssertDatabase(state, "dreamStartingDeckDatabase", DreamPath);
            AssertDatabase(state, "nightmareStartingDeckDatabase", NightmarePath);
            Assert.That(state.FindProperty("dreamStartingDeckDatabase").prefabOverride, Is.False);
            Assert.That(state.FindProperty("nightmareStartingDeckDatabase").prefabOverride, Is.False);
            SerializedObject battle = new SerializedObject(manager);
            Assert.That(battle.FindProperty("player").objectReferenceValue, Is.SameAs(soldier));
            Assert.That(battle.FindProperty("deckController").objectReferenceValue, Is.SameAs(deck));
            SerializedObject cost = new SerializedObject(soldier.GetCostSystem());
            foreach (string field in new[] { "dreamCostUI", "nightmareCostUI", "dreamGaugeUI", "nightmareGaugeUI" })
                Assert.That(cost.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(soldier.gameObject), Is.Zero);

            AmmoUI hud = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (hud == null) hud = root.GetComponentInChildren<AmmoUI>(true);
            SerializedObject hudState = new SerializedObject(hud);
            RectTransform label = ((TMPro.TMP_Text)hudState.FindProperty("ammoText").objectReferenceValue).rectTransform;
            UnityEngine.UI.Image fill = (UnityEngine.UI.Image)hudState.FindProperty("ammoFill").objectReferenceValue;
            RectTransform bar = (RectTransform)fill.transform.parent;
            Vector2 labelMin = label.anchorMin, labelMax = label.anchorMax;
            Vector2 barMin = bar.anchorMin, barMax = bar.anchorMax;
            UnityEngine.UI.Image.FillMethod fillMethod = fill.fillMethod;
            SoldierAssetSetup.UseSoldierInBattleScene();
            Assert.That(label.anchorMin, Is.EqualTo(labelMin));
            Assert.That(label.anchorMax, Is.EqualTo(labelMax));
            Assert.That(bar.anchorMin, Is.EqualTo(barMin));
            Assert.That(bar.anchorMax, Is.EqualTo(barMax));
            Assert.That(fill.fillMethod, Is.EqualTo(fillMethod));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void StartingDecksCopyDatabaseContentsIncludingDuplicatesWithoutMutatingAssets()
    {
        CardData dream = CreateCard(CardDeckType.TypeDream);
        CardData nightmare = CreateCard(CardDeckType.TypeNightmare);
        CardDatabase dreamSource = CreateDatabase(dream, dream);
        CardDatabase nightmareSource = CreateDatabase(nightmare);
        DeckController deck = CreateController(dreamSource, nightmareSource);
        Assert.That(deck.GetDeck(CardDeckType.TypeDream), Is.EqualTo(new[] { dream, dream }));
        Assert.That(deck.GetDeck(CardDeckType.TypeNightmare), Is.EqualTo(new[] { nightmare }));
        Assert.That(deck.GetDeck(CardDeckType.TypeDream), Is.Not.InstanceOf<List<CardData>>());
        deck.AddCardToDeck(dream, CardDeckType.TypeDream);
        Assert.That(dreamSource.GetAllCards(), Has.Count.EqualTo(2));
        Assert.That(nightmareSource.GetAllCards(), Has.Count.EqualTo(1));
    }

    [Test]
    public void AddedCardUpdatesExistingDeckViewAndEntersDiscardPileForDrawing()
    {
        CardData first = CreateCard(CardDeckType.TypeDream);
        CardData added = CreateCard(CardDeckType.TypeDream);
        DeckController deck = CreateController(CreateDatabase(first), CreateDatabase());
        IReadOnlyList<CardData> view = deck.GetDeck(CardDeckType.TypeDream);
        Assert.That(deck.DrawCard(CardDeckType.TypeDream), Is.SameAs(first));
        deck.AddCardToDeck(added, CardDeckType.TypeDream);
        Assert.That(view, Is.EqualTo(new[] { first, added }));
        Assert.That(deck.GetDiscardPile(CardDeckType.TypeDream), Is.EqualTo(new[] { added }));
        Assert.That(deck.DrawCard(CardDeckType.TypeDream), Is.SameAs(added));
    }

    [Test]
    public void RecreatedControllerPreservesAddedAndRemovedCardsForBothDecks()
    {
        CardData dream = CreateCard(CardDeckType.TypeDream);
        CardData nightmare = CreateCard(CardDeckType.TypeNightmare);
        CardDatabase dreamSource = CreateDatabase(dream, dream);
        CardDatabase nightmareSource = CreateDatabase(nightmare);
        DeckController first = CreateController(dreamSource, nightmareSource);
        first.AddCardToDeck(nightmare, CardDeckType.TypeNightmare);
        Assert.That(first.RemoveCardFromDeck(dream, CardDeckType.TypeDream), Is.True);
        Object.DestroyImmediate(first.gameObject);
        DeckController second = CreateController(dreamSource, nightmareSource);
        Assert.That(second.GetDeck(CardDeckType.TypeDream), Is.EqualTo(new[] { dream }));
        Assert.That(second.GetDeck(CardDeckType.TypeNightmare), Is.EqualTo(new[] { nightmare, nightmare }));
        Assert.That(second.GetDrawPile(CardDeckType.TypeDream), Has.Count.EqualTo(1));
        Assert.That(second.GetDrawPile(CardDeckType.TypeNightmare), Has.Count.EqualTo(2));
    }

    [Test]
    public void BattleResetRebuildsPilesFromCurrentDeckRatherThanStartingDatabase()
    {
        CardData card = CreateCard(CardDeckType.TypeDream);
        DeckController deck = CreateController(CreateDatabase(card), CreateDatabase());
        deck.DrawCard(CardDeckType.TypeDream);
        deck.AddCardToDeck(card, CardDeckType.TypeDream);
        InvokeBattleReset(deck);
        Assert.That(deck.GetDeck(CardDeckType.TypeDream), Has.Count.EqualTo(2));
        Assert.That(deck.GetDrawPile(CardDeckType.TypeDream), Has.Count.EqualTo(2));
        Assert.That(deck.GetDiscardPile(CardDeckType.TypeDream), Is.Empty);
    }

    [Test]
    public void NewGameRestoresStartingDecksAndExistingControllerUsesFreshPiles()
    {
        CardData card = CreateCard(CardDeckType.TypeDream);
        CardDatabase source = CreateDatabase(card);
        DeckController deck = CreateController(source, CreateDatabase());
        deck.AddCardToDeck(card, CardDeckType.TypeDream);
        InvokeRunReset();
        Assert.That(deck.GetDeck(CardDeckType.TypeDream), Is.EqualTo(new[] { card }));
        Assert.That(deck.GetDrawPile(CardDeckType.TypeDream), Has.Count.EqualTo(1));
        Assert.That(deck.GetDiscardPile(CardDeckType.TypeDream), Is.Empty);
        Assert.That(source.GetAllCards(), Has.Count.EqualTo(1));
    }

    [Test]
    public void RemovingOneDuplicateAlsoRemovesOneCardFromTheBattlePiles()
    {
        CardData card = CreateCard(CardDeckType.TypeDream);
        DeckController deck = CreateController(CreateDatabase(card, card), CreateDatabase());
        Assert.That(deck.RemoveCardFromDeck(card, CardDeckType.TypeDream), Is.True);
        Assert.That(deck.GetDeck(CardDeckType.TypeDream), Has.Count.EqualTo(1));
        Assert.That(deck.GetDrawPile(CardDeckType.TypeDream), Has.Count.EqualTo(1));
    }

    [Test]
    public void CardCannotBeAddedToTheWrongDeck()
    {
        CardData nightmare = CreateCard(CardDeckType.TypeNightmare);
        DeckController deck = CreateController(CreateDatabase(), CreateDatabase());
        deck.AddCardToDeck(nightmare, CardDeckType.TypeDream);
        Assert.That(deck.GetDeck(CardDeckType.TypeDream), Is.Empty);
        Assert.That(deck.GetDiscardPile(CardDeckType.TypeDream), Is.Empty);
    }

    [UnityTest]
    public IEnumerator SceneReloadKeepsAcquiredCardsAndUsesThemInTheNextBattle()
    {
        yield return new EnterPlayMode();
        SceneManager.LoadScene("BattleScene");
        yield return null;
        yield return new WaitForSeconds(0.8f);
        Soldier soldier = Object.FindFirstObjectByType<Soldier>();
        DeckController first = soldier.GetComponent<DeckController>();
        CardDatabase source = AssetDatabase.LoadAssetAtPath<CardDatabase>(DreamPath);
        CardData added = source.GetAllCards()[0];
        int initialCount = source.GetAllCards().Count;
        first.AddCardToDeck(added, CardDeckType.TypeDream);
        Assert.That(first.GetDeck(CardDeckType.TypeDream), Has.Count.EqualTo(initialCount + 1));
        SceneManager.LoadScene("BattleScene");
        yield return null;
        yield return new WaitForSeconds(0.8f);
        DeckController next = Object.FindFirstObjectByType<Soldier>().GetComponent<DeckController>();
        Assert.That(next.GetDeck(CardDeckType.TypeDream), Has.Count.EqualTo(initialCount + 1));
        Assert.That(next.GetDrawPile(CardDeckType.TypeDream).Count, Is.EqualTo(initialCount + 1 - 3));
        Assert.That(next.GetDiscardPile(CardDeckType.TypeDream), Is.Empty);
        Assert.That(source.GetAllCards(), Has.Count.EqualTo(initialCount));
        Assert.That(Object.FindFirstObjectByType<AmmoUI>(), Is.Not.Null);
        yield return new ExitPlayMode();
    }

    private CardData CreateCard(CardDeckType type)
    {
        CardData card = ScriptableObject.CreateInstance<CardData>();
        card.deckType = type;
        created.Add(card);
        return card;
    }

    private CardDatabase CreateDatabase(params CardData[] cards)
    {
        CardDatabase database = ScriptableObject.CreateInstance<CardDatabase>();
        created.Add(database);
        SerializedObject state = new SerializedObject(database);
        SerializedProperty list = state.FindProperty("cards");
        list.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        state.ApplyModifiedPropertiesWithoutUndo();
        return database;
    }

    private DeckController CreateController(CardDatabase dream, CardDatabase nightmare)
    {
        GameObject root = new GameObject("TestDeck");
        root.SetActive(false);
        created.Add(root);
        DeckController deck = root.AddComponent<DeckController>();
        SerializedObject state = new SerializedObject(deck);
        SerializedProperty dreamField = state.FindProperty("dreamStartingDeckDatabase");
        Assert.That(dreamField, Is.Not.Null, "DeckController must reference starting databases.");
        dreamField.objectReferenceValue = dream;
        state.FindProperty("nightmareStartingDeckDatabase").objectReferenceValue = nightmare;
        state.ApplyModifiedPropertiesWithoutUndo();
        typeof(DeckController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(deck, null);
        return deck;
    }

    private static void AssertDatabase(SerializedObject state, string field, string path)
    {
        SerializedProperty property = state.FindProperty(field);
        Assert.That(property, Is.Not.Null, field);
        Assert.That(property.objectReferenceValue, Is.SameAs(AssetDatabase.LoadAssetAtPath<CardDatabase>(path)));
    }

    private static void InvokeBattleReset(DeckController deck)
    {
        MethodInfo reset = typeof(DeckController).GetMethod("ResetForBattle");
        Assert.That(reset, Is.Not.Null);
        reset.Invoke(deck, null);
    }

    private static void InvokeRunReset()
    {
        System.Type inventory = typeof(DeckController).Assembly.GetType("RunDeckInventory");
        Assert.That(inventory, Is.Not.Null);
        inventory.GetMethod("ResetForNewGame").Invoke(null, null);
    }

    private static void ResetRunIfAvailable()
    {
        System.Type inventory = typeof(DeckController).Assembly.GetType("RunDeckInventory");
        if (inventory != null) inventory.GetMethod("ResetForNewGame").Invoke(null, null);
    }
}
