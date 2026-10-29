using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

public class DeckListLayoutTests
{
    [UnityTest]
    public IEnumerator AllFourPileButtonsUsePrefabSizeWithoutChangingThePile()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForPlayTime(1f);
        DeckController deck = Object.FindFirstObjectByType<DeckController>();
        foreach (CardDeckType type in new[] { CardDeckType.TypeDream, CardDeckType.TypeNightmare })
            deck.DiscardCard(deck.GetDeck(type)[0], type);
        DeckListUI list = Object.FindObjectsByType<DeckListUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(candidate => candidate.name == "DeckListPanel");
        Transform content = Content(list);
        Canvas canvas = content.GetComponentInParent<Canvas>().rootCanvas;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.localScale = Vector3.one;
        RectTransform canvasRect = (RectTransform)canvas.transform;
        DeckPileButton[] buttons = Object.FindObjectsByType<DeckPileButton>(FindObjectsSortMode.None);
        Assert.That(buttons, Has.Length.EqualTo(4));
        float[] heights = { 810f, 540f, 1080f };
        Vector2 expectedSize = new Vector2(200f, 260f);
        for (int i = 0; i < heights.Length; i++)
        {
            canvasRect.sizeDelta = new Vector2(1920f, heights[i]);
            yield return null;
            foreach (DeckPileButton button in buttons)
            {
                SerializedObject state = new SerializedObject(button);
                CardDeckType type = (CardDeckType)state.FindProperty("deckType").enumValueIndex;
                bool discard = state.FindProperty("isDiscardPile").boolValue;
                IReadOnlyList<CardData> pile = discard ? deck.GetDiscardPile(type) : deck.GetDrawPile(type);
                var before = new List<CardData>(pile);
                button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return null;
                Canvas.ForceUpdateCanvases();
                DeckCardUI[] cards = content.GetComponentsInChildren<DeckCardUI>();
                Assert.That(cards.Length, Is.EqualTo(before.Count), button.name);
                Assert.That(pile, Is.EqualTo(before), "Opening the viewer must not reorder or change the pile.");
                CollectionAssert.AreEquivalent(before.Select(card => card.cardName), cards.Select(card =>
                    ((TMP_Text)new SerializedObject(card).FindProperty("nameText").objectReferenceValue).text));
                AssertCardsAndGrid(content, expectedSize);
                CardHandController hand = Object.FindObjectsByType<CardHandController>(FindObjectsSortMode.None)
                    .Single(candidate => candidate.name == (type == CardDeckType.TypeDream ? "DreamCardContainer" : "NightmareCardContainer"));
                Vector2 handSize = ((RectTransform)hand.GetComponentInChildren<CardUI>().transform).rect.size;
                Assert.That(handSize, Is.EqualTo(hand.GetHandCardSize()));
                Assert.That(handSize.y, Is.LessThan(expectedSize.y), "Hand padding must not shrink the list cards.");
                list.Close();
                yield return null;
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator IndependentListScaleWorksWithAnEmptyHandAndReopeningDoesNotCompoundFontsOrDuplicateCards()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForPlayTime(1f);
        DeckPileButton button = Object.FindObjectsByType<DeckPileButton>(FindObjectsSortMode.None).Single(candidate =>
        {
            SerializedObject state = new SerializedObject(candidate);
            return state.FindProperty("deckType").enumValueIndex == (int)CardDeckType.TypeDream &&
                !state.FindProperty("isDiscardPile").boolValue;
        });
        CardHandController hand = Object.FindObjectsByType<CardHandController>(FindObjectsSortMode.None)
            .Single(candidate => candidate.name == "DreamCardContainer");
        hand.RemoveAllCards();
        yield return null;
        DeckListUI list = Object.FindObjectsByType<DeckListUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(candidate => candidate.name == "DeckListPanel");
        Transform content = Content(list);
        Canvas canvas = hand.GetComponentInParent<Canvas>().rootCanvas;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.localScale = Vector3.one;
        RectTransform canvasRect = (RectTransform)canvas.transform;
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);
        var listState = new SerializedObject(list);
        listState.FindProperty("cardScale").floatValue = 1.25f;
        listState.ApplyModifiedPropertiesWithoutUndo();
        button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return null;
        DeckCardUI[] originalCards = content.GetComponentsInChildren<DeckCardUI>();
        Assert.That(originalCards.Length, Is.GreaterThan(0));
        float[] heights = { 810f, 540f, 1080f };
        Vector2 expectedSize = new Vector2(250f, 325f);
        for (int i = 0; i < heights.Length; i++)
        {
            canvasRect.sizeDelta = new Vector2(1920f, heights[i]);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertCardsAndGrid(content, expectedSize);
            CollectionAssert.AreEqual(originalCards, content.GetComponentsInChildren<DeckCardUI>());
        }
        listState.Update();
        listState.FindProperty("cardScale").floatValue = 0.8f;
        listState.ApplyModifiedPropertiesWithoutUndo();
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        AssertCardsAndGrid(content, new Vector2(160f, 208f));
        button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        Assert.That(content.GetComponentsInChildren<DeckCardUI>().Length, Is.EqualTo(originalCards.Length));
        AssertCardsAndGrid(content, new Vector2(160f, 208f));
        list.Close();
        yield return new ExitPlayMode();
    }

    private static Transform Content(DeckListUI list) =>
        (Transform)new SerializedObject(list).FindProperty("content").objectReferenceValue;

    private static void AssertCardsAndGrid(Transform content, Vector2 size)
    {
        UnityEngine.UI.GridLayoutGroup grid = content.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        Assert.That(grid.cellSize.x, Is.EqualTo(size.x).Within(0.02f));
        Assert.That(grid.cellSize.y, Is.EqualTo(size.y).Within(0.02f));
        float ratio = size.y / 260f;
        Assert.That(grid.spacing.x, Is.EqualTo(50f * ratio).Within(0.02f));
        Assert.That(grid.spacing.y, Is.EqualTo(30f * ratio).Within(0.02f));
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DeckCard.prefab");
        Dictionary<string, float> fonts = prefab.GetComponentsInChildren<TMP_Text>(true)
            .ToDictionary(text => text.name, text => text.fontSize);
        foreach (DeckCardUI card in content.GetComponentsInChildren<DeckCardUI>())
        {
            RectTransform rect = (RectTransform)card.transform;
            Assert.That(rect.rect.width, Is.EqualTo(size.x).Within(0.02f));
            Assert.That(rect.rect.height, Is.EqualTo(size.y).Within(0.02f));
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
            foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true))
                Assert.That(text.fontSize, Is.EqualTo(fonts[text.name] * ratio).Within(0.02f), text.name);
        }
    }

    private static IEnumerator WaitForPlayTime(float seconds)
    {
        float target = Time.time + seconds;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.time < target && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Time.time, Is.GreaterThanOrEqualTo(target));
    }
}
