using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class DeckCardHoverTests
{
    [UnityTest]
    public IEnumerator AllFourPilesEnlargeWithoutChangingLayoutOrBlockingOtherCards()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForHand();
        DeckController deck = Object.FindFirstObjectByType<DeckController>();
        foreach (CardDeckType type in new[] { CardDeckType.TypeDream, CardDeckType.TypeNightmare })
            deck.DiscardCard(deck.GetDeck(type)[0], type);
        DeckListUI list = Object.FindObjectsByType<DeckListUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)[0];
        Transform content = (Transform)new SerializedObject(list).FindProperty("content").objectReferenceValue;
        foreach (DeckPileButton button in Object.FindObjectsByType<DeckPileButton>(FindObjectsSortMode.None))
        {
            button.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            DeckCardUI[] cards = content.GetComponentsInChildren<DeckCardUI>();
            Assert.That(cards, Is.Not.Empty);
            RectTransform root = (RectTransform)cards[0].transform;
            RectTransform visual = (RectTransform)root.Find("HoverVisual");
            Assert.That(visual, Is.Not.Null, "Pile cards need a separate hover visual.");
            Vector2 size = root.rect.size;
            Vector3[] positions = cards.Select(card => card.transform.position).ToArray();
            var fonts = cards[0].GetComponentsInChildren<TMP_Text>().ToDictionary(text => text, text => text.fontSize);
            PointerEventData pointer = Pointer(root);
            AssertHit(pointer, root.gameObject);
            ExecuteEvents.Execute(root.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return Wait(0.25f);
            Assert.That(visual.localScale.x, Is.EqualTo(1.3f).Within(0.001f));
            Assert.That(root.localScale, Is.EqualTo(Vector3.one));
            Assert.That(root.rect.size, Is.EqualTo(size));
            for (int i = 0; i < cards.Length; i++)
            {
                Assert.That(cards[i].transform.position, Is.EqualTo(positions[i]));
                Assert.That(cards[i].transform.GetSiblingIndex(), Is.EqualTo(i));
            }
            foreach (var font in fonts) Assert.That(font.Key.fontSize, Is.EqualTo(font.Value));
            Assert.That(visual.GetComponent<Canvas>().overrideSorting, Is.True);
            Assert.That(visual.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(root.GetComponentInParent<Canvas>().sortingOrder));
            AssertHit(pointer, root.gameObject);
            foreach (Graphic graphic in visual.GetComponentsInChildren<Graphic>()) Assert.That(graphic.raycastTarget, Is.False);
            if (cards.Length > 1) AssertHit(Pointer((RectTransform)cards[1].transform), cards[1].gameObject);
            ExecuteEvents.Execute(root.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return Wait(0.25f);
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(visual.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(visual.GetComponent<Canvas>().overrideSorting, Is.False);
            ExecuteEvents.Execute(root.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return Wait(0.25f);
            list.Close();
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one), "Closing the list must reset enlargement immediately.");
            yield return null;
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator InspectorScaleWorksWhilePausedAndResizingRebindingAndDisablingRestoreTheVisual()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForHand();
        DeckListUI list = Object.FindObjectsByType<DeckListUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)[0];
        Transform content = (Transform)new SerializedObject(list).FindProperty("content").objectReferenceValue;
        list.ShowCards(Object.FindFirstObjectByType<DeckController>().GetDrawPile(CardDeckType.TypeDream));
        yield return null;
        DeckCardUI card = content.GetComponentInChildren<DeckCardUI>();
        RectTransform root = (RectTransform)card.transform;
        RectTransform visual = (RectTransform)root.Find("HoverVisual");
        Assert.That(visual, Is.Not.Null);
        var state = new SerializedObject(card.GetComponent("DeckCardHoverUI"));
        state.FindProperty("hoverScale").floatValue = 1.6f;
        state.ApplyModifiedPropertiesWithoutUndo();
        Time.timeScale = 0f;
        ExecuteEvents.Execute(root.gameObject, Pointer(root), ExecuteEvents.pointerEnterHandler);
        yield return Wait(0.25f);
        Assert.That(visual.localScale.x, Is.EqualTo(1.6f).Within(0.001f));
        Canvas canvas = root.GetComponentInParent<Canvas>().rootCanvas;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.localScale = Vector3.one;
        RectTransform canvasRect = (RectTransform)canvas.transform;
        canvasRect.sizeDelta = new Vector2(1920f, 540f);
        content.GetComponent<GridLayoutGroup>().enabled = false;
        foreach (Vector2 edge in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) })
        {
            root.position = canvasRect.TransformPoint(new Vector2(edge.x * (960f - root.rect.width / 2f), edge.y * (270f - root.rect.height / 2f)));
            yield return null;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            visual.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 point = canvasRect.InverseTransformPoint(corner);
                Assert.That(point.x, Is.InRange(-960.05f, 960.05f));
                Assert.That(point.y, Is.InRange(-270.05f, 270.05f));
            }
        }
        card.SetCard(AssetDatabase.LoadAssetAtPath<CardData>("Assets/CardData/Basic/Dream/Attack/1.Card_Attack/Attack.asset"));
        Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        Assert.That(visual.anchoredPosition, Is.EqualTo(Vector2.zero));
        ExecuteEvents.Execute(root.gameObject, Pointer(root), ExecuteEvents.pointerEnterHandler);
        yield return Wait(0.25f);
        root.sizeDelta = new Vector2(100f, 130f);
        card.ResizeForList(0.5f);
        yield return null;
        Assert.That(visual.localScale.x, Is.EqualTo(1.6f).Within(0.001f));
        Assert.That(visual.rect.size, Is.EqualTo(new Vector2(100f, 130f)), "The visual must follow the resized grid cell.");
        card.gameObject.SetActive(false);
        Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        card.gameObject.SetActive(true);
        yield return Wait(0.25f);
        Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        Assert.That(visual.GetComponent<Canvas>().overrideSorting, Is.False);
        Time.timeScale = 1f;
        list.Close();
        yield return new ExitPlayMode();
    }

    [TearDown]
    public void RestoreTime() { if (Application.isPlaying) Time.timeScale = 1f; }

    private static PointerEventData Pointer(RectTransform rect) => new PointerEventData(EventSystem.current)
    { position = RectTransformUtility.WorldToScreenPoint(null, rect.position) };

    private static void AssertHit(PointerEventData pointer, GameObject target)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits, Is.Not.Empty);
        Assert.That(hits[0].gameObject, Is.EqualTo(target));
    }

    private static IEnumerator WaitForHand()
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Any(card => card.CanHover) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Any(card => card.CanHover), Is.True);
    }

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.unscaledTime < end && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Time.unscaledTime, Is.GreaterThanOrEqualTo(end));
    }
}
