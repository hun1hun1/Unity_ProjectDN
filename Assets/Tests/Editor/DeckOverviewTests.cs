using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class DeckOverviewTests
{
    [Test]
    public void SceneWiresExistingDeckButtonToTwoIndependentPrefabLists()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity", OpenSceneMode.Additive);
        try
        {
            MonoBehaviour overview = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .FirstOrDefault(component => component != null && component.GetType().Name == "DeckOverviewUI");
            Assert.That(overview, Is.Not.Null, "BattleScene needs the dual deck overview and button wiring.");
            Button button = Reference<Button>(overview, "toggleButton");
            Assert.That(button.name, Is.EqualTo("DeckButton"));
            Assert.That(Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(index =>
                button.onClick.GetPersistentTarget(index) == overview && button.onClick.GetPersistentMethodName(index) == "Toggle"), Is.True);
            Assert.That(Reference<GameObject>(overview, "panel").activeSelf, Is.False);
            foreach (DeckListUI list in Lists(overview))
            {
                Assert.That(Reference<DeckCardUI>(list, "cardPrefab"), Is.EqualTo(
                    AssetDatabase.LoadAssetAtPath<DeckCardUI>("Assets/Prefabs/UI/DeckCard.prefab")));
                Assert.That(new SerializedObject(list).FindProperty("sortByName").boolValue, Is.True);
                Transform content = Content(list);
                Assert.That(content.GetComponent<GridLayoutGroup>().constraintCount, Is.EqualTo(3));
                ScrollRect scroll = content.GetComponentInParent<ScrollRect>(true);
                Assert.That(scroll.content, Is.EqualTo(content));
                Assert.That(scroll.vertical, Is.True);
                Assert.That(scroll.horizontal, Is.False);
                Assert.That(content.GetComponent<ContentSizeFitter>().verticalFit, Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest]
    public IEnumerator BothOwnedDecksIncludeAllZonesDuplicatesAndRefreshWithoutMutatingDecksOrRandom()
    {
        yield return new EnterPlayMode();
        yield return VerifyOwnedDecks();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyOwnedDecks()
    {
        yield return LoadBattle();
        MonoBehaviour overview = Overview();
        DeckController deck = Object.FindFirstObjectByType<DeckController>();
        foreach (CardDeckType type in new[] { CardDeckType.TypeDream, CardDeckType.TypeNightmare })
        {
            CardData discarded = deck.DrawCard(type);
            deck.DiscardCard(discarded, type);
            deck.ExhaustCard(deck.DrawCard(type), type);
            deck.AddCardToDeck(deck.GetDeck(type)[0], type);
        }
        var dream = deck.GetDeck(CardDeckType.TypeDream).ToArray();
        var nightmare = deck.GetDeck(CardDeckType.TypeNightmare).ToArray();
        var draw = deck.GetDrawPile(CardDeckType.TypeDream).ToArray();
        var discard = deck.GetDiscardPile(CardDeckType.TypeDream).ToArray();
        var exhaust = deck.GetExhaustPile(CardDeckType.TypeDream).ToArray();
        UnityEngine.Random.State random = UnityEngine.Random.state;
        Reference<Button>(overview, "toggleButton").onClick.Invoke();
        Assert.That(IsOpen(overview), Is.True);
        Assert.That(UnityEngine.Random.state, Is.EqualTo(random), "Browsing must not advance battle randomness.");
        AssertList(Lists(overview)[0], dream);
        AssertList(Lists(overview)[1], nightmare);
        Assert.That(Reference<TMP_Text>(overview, "dreamTitle").text, Does.Contain(dream.Length.ToString()));
        Assert.That(Reference<TMP_Text>(overview, "nightmareTitle").text, Does.Contain(nightmare.Length.ToString()));
        CollectionAssert.AreEqual(dream, deck.GetDeck(CardDeckType.TypeDream));
        CollectionAssert.AreEqual(nightmare, deck.GetDeck(CardDeckType.TypeNightmare));
        CollectionAssert.AreEqual(draw, deck.GetDrawPile(CardDeckType.TypeDream));
        CollectionAssert.AreEqual(discard, deck.GetDiscardPile(CardDeckType.TypeDream));
        CollectionAssert.AreEqual(exhaust, deck.GetExhaustPile(CardDeckType.TypeDream));
        Reference<Button>(overview, "toggleButton").onClick.Invoke();
        Assert.That(IsOpen(overview), Is.False);
        foreach (DeckListUI list in Lists(overview)) Assert.That(Content(list).GetComponentsInChildren<DeckCardUI>(), Is.Empty);
        yield return null;
        deck.RemoveCardFromDeck(dream[0], CardDeckType.TypeDream);
        Reference<Button>(overview, "toggleButton").onClick.Invoke();
        AssertList(Lists(overview)[0], deck.GetDeck(CardDeckType.TypeDream));
        overview.SendMessage("Close");
    }

    [UnityTest]
    public IEnumerator OverviewBlocksBattleButAllowsInspectionAndRestoresInputAfterClosing()
    {
        yield return new EnterPlayMode();
        yield return VerifyInputAndInspection();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyInputAndInspection()
    {
        yield return LoadBattle();
        MonoBehaviour overview = Overview();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI hand = Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).First(card => battle.CanUseCard(card.GetCardData()));
        Reference<Button>(overview, "toggleButton").onClick.Invoke();
        Assert.That(hand.CanHover, Is.False);
        Assert.That(battle.CanUseCard(hand.GetCardData()), Is.False);
        Assert.That(battle.UseCard(hand), Is.False);
        battle.EndPlayerTurn();
        yield return null;
        Assert.That(battle.CanInspectCards, Is.True, "End turn must be blocked while viewing owned decks.");
        foreach (DeckPileButton pile in Object.FindObjectsByType<DeckPileButton>(FindObjectsSortMode.None))
            pile.GetComponent<Button>().onClick.Invoke();
        Assert.That(Reference<DeckListUI>(overview, "pileList").gameObject.activeSelf, Is.False);
        DeckCardUI cardView = Content(Lists(overview)[0]).GetComponentInChildren<DeckCardUI>();
        cardView.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right });
        CardInspectionUI inspection = Object.FindFirstObjectByType<CardInspectionUI>();
        Assert.That(inspection.IsOpen, Is.True);
        overview.SendMessage("Toggle");
        overview.SendMessage("Close");
        Assert.That(IsOpen(overview), Is.True, "Inspection must retain the overview behind it.");
        inspection.Close();
        yield return Until(() => !CardInspectionUI.BlocksGameplayInput);
        Assert.That(hand.CanHover, Is.False);
        Reference<Button>(overview, "closeButton").onClick.Invoke();
        Assert.That(IsOpen(overview), Is.False);
        Assert.That(battle.CanUseCard(hand.GetCardData()), Is.False, "The closing click must not reach the battle in the same frame.");
        yield return null;
        yield return null;
        Assert.That(hand.CanHover, Is.True);
        Assert.That(battle.CanUseCard(hand.GetCardData()), Is.True);
    }

    [UnityTest]
    public IEnumerator IndependentScrollingHoverAndScaleWorkAtBothCanvasSizes()
    {
        yield return new EnterPlayMode();
        yield return VerifyLayout();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyLayout()
    {
        yield return LoadBattle();
        MonoBehaviour overview = Overview();
        DeckController deck = Object.FindFirstObjectByType<DeckController>();
        foreach (CardDeckType type in new[] { CardDeckType.TypeDream, CardDeckType.TypeNightmare })
            for (int i = 0; i < 20; i++) deck.AddCardToDeck(deck.GetDeck(type)[0], type);
        overview.SendMessage("Toggle");
        DeckListUI[] lists = Lists(overview);
        ScrollRect left = Content(lists[0]).GetComponentInParent<ScrollRect>();
        ScrollRect right = Content(lists[1]).GetComponentInParent<ScrollRect>();
        Canvas canvas = overview.GetComponentInParent<Canvas>().rootCanvas;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.localScale = Vector3.one;
        foreach (Vector2 resolution in new[] { new Vector2(1920, 1080), new Vector2(1280, 720) })
        {
            ((RectTransform)canvas.transform).sizeDelta = resolution;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(left.content.rect.height, Is.GreaterThan(left.viewport.rect.height));
            Vector2 rightPosition = right.content.anchoredPosition;
            left.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            Assert.That(left.content.anchoredPosition.y, Is.GreaterThan(0));
            Assert.That(right.content.anchoredPosition, Is.EqualTo(rightPosition));
            left.verticalNormalizedPosition = 1;
            DeckCardUI card = Content(lists[0]).GetComponentInChildren<DeckCardUI>();
            Vector3 rootScale = card.transform.localScale;
            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return Wait(.25f);
            Assert.That(card.transform.Find("HoverVisual").localScale.x, Is.GreaterThanOrEqualTo(1.3f - .001f));
            Assert.That(card.transform.localScale, Is.EqualTo(rootScale));
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        }
        var state = new SerializedObject(lists[0]);
        state.FindProperty("cardScale").floatValue = .8f;
        state.ApplyModifiedPropertiesWithoutUndo();
        yield return null;
        yield return null;
        Assert.That(left.content.GetComponent<GridLayoutGroup>().cellSize, Is.EqualTo(new Vector2(160, 208)));
        Assert.That(right.content.GetComponent<GridLayoutGroup>().cellSize, Is.EqualTo(new Vector2(200, 260)));
        overview.SendMessage("Close");
    }

    [UnityTest]
    public IEnumerator ExternalHideDisableAndBattleEndReleaseTheOverview()
    {
        yield return new EnterPlayMode();
        yield return VerifyCleanup();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyCleanup()
    {
        yield return LoadBattle();
        MonoBehaviour overview = Overview();
        CardUI hand = Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).First(card => card.CanHover);
        overview.SendMessage("Toggle");
        Reference<GameObject>(overview, "panel").SetActive(false);
        yield return null;
        yield return null;
        Assert.That(hand.CanHover, Is.True);
        overview.SendMessage("Toggle");
        overview.enabled = false;
        yield return null;
        yield return null;
        Assert.That(IsOpen(overview), Is.False);
        Assert.That(hand.CanHover, Is.True);
        overview.enabled = true;
        overview.SendMessage("Toggle");
        Object.FindFirstObjectByType<BattleManager>().EndBattle();
        Assert.That(IsOpen(overview), Is.False);
        foreach (DeckListUI list in Lists(overview)) Assert.That(Content(list).GetComponentsInChildren<DeckCardUI>(), Is.Empty);
    }

    private static MonoBehaviour Overview()
    {
        MonoBehaviour value = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .FirstOrDefault(component => component.GetType().Name == "DeckOverviewUI");
        Assert.That(value, Is.Not.Null);
        return value;
    }
    private static bool IsOpen(MonoBehaviour overview) => (bool)overview.GetType().GetProperty("IsOpen").GetValue(overview);
    private static T Reference<T>(Object owner, string field) where T : Object =>
        (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
    private static DeckListUI[] Lists(MonoBehaviour overview) => new[] { Reference<DeckListUI>(overview, "dreamList"), Reference<DeckListUI>(overview, "nightmareList") };
    private static Transform Content(DeckListUI list) => Reference<Transform>(list, "content");
    private static void AssertList(DeckListUI list, IReadOnlyList<CardData> expected)
    {
        DeckCardUI[] cards = Content(list).GetComponentsInChildren<DeckCardUI>();
        Assert.That(cards.Length, Is.EqualTo(expected.Count));
        string[] names = cards.Select(card => Reference<TMP_Text>(card, "nameText").text).ToArray();
        CollectionAssert.AreEqual(expected.Select(card => card.cardName).OrderBy(name => name, StringComparer.Ordinal), names);
        foreach (DeckCardUI card in cards)
        {
            Assert.That(card.GetComponent<DeckCardHoverUI>(), Is.Not.Null);
            Assert.That(((RectTransform)card.transform).rect.size, Is.EqualTo(new Vector2(200,260)));
        }
    }
    private static IEnumerator LoadBattle()
    {
        AsyncOperation loading = SceneManager.LoadSceneAsync("BattleScene");
        yield return Until(() => loading.isDone);
        yield return Until(() => Object.FindFirstObjectByType<BattleManager>() != null &&
            Object.FindFirstObjectByType<BattleManager>().CanInspectCards &&
            Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Any(card => card.CanHover));
    }
    private static IEnumerator Until(Func<bool> ready)
    {
        float deadline = Time.realtimeSinceStartup + 15;
        while (!ready()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
    }
    private static IEnumerator Wait(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }
}
