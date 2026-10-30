using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class HandLayoutTests
{
    [UnityTest]
    public IEnumerator BothHandsFitCardsAndTextWhenResizedAndWhenNewCardsAreDrawn()
    {
        yield return new EnterPlayMode();
        SceneManager.LoadScene("BattleScene");
        yield return null;
        // HoverVisual has its own Canvas; resize the Canvas that actually owns the hand.
        Canvas canvas = Object.FindFirstObjectByType<CardHandController>().GetComponentInParent<Canvas>().rootCanvas;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.localScale = Vector3.one;
        RectTransform canvasRect = (RectTransform)canvas.transform;
        CardHandController[] hands = Object.FindObjectsByType<CardHandController>(FindObjectsSortMode.None);
        Assert.That(hands.Length, Is.EqualTo(2));

        // Logical Canvas heights for 16:9, 2560x1080, 32:9, and back to 16:9.
        float[] heights = { 1080f, 810f, 540f, 1080f };
        // Current hand padding is 40px above and 4px below the cards.
        float[] widths = { 173.84615f, 121.92308f, 70f, 173.84615f };
        float[] cardHeights = { 226f, 158.5f, 91f, 226f };
        float[] nameFontSizes = { 17.384615f, 12.192308f, 7f, 17.384615f };
        for (int i = 0; i < heights.Length; i++)
        {
            canvasRect.sizeDelta = new Vector2(1920f, heights[i]);
            yield return null;
            yield return WaitForPlayTime(1f);
            Canvas.ForceUpdateCanvases();
            foreach (CardHandController hand in hands)
            {
                CardUI[] cards = hand.GetComponentsInChildren<CardUI>();
                Assert.That(cards.Length, Is.EqualTo(3));
                foreach (CardUI card in cards)
                    AssertCardFits(hand, card, widths[i], cardHeights[i], nameFontSizes[i]);
            }

            if (i == 1)
            {
                // Drawing after the resize must use the same size as existing cards.
                CardHandController hand = hands[0];
                CardUI old = hand.GetComponentsInChildren<CardUI>()[0];
                CardData data = old.GetCardData();
                hand.RemoveCard(old);
                Object.Destroy(old.gameObject);
                yield return null;
                Assert.That(hand.AddCard(data, Object.FindFirstObjectByType<BattleManager>()), Is.True);
                yield return WaitForPlayTime(1f);
                foreach (CardUI card in hand.GetComponentsInChildren<CardUI>())
                    AssertCardFits(hand, card, 121.92308f, 158.5f, 12.192308f);
            }
        }

        // Runtime fitting must never change the source prefab's dimensions or fonts.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Card.prefab");
        Assert.That(((RectTransform)prefab.transform).rect.size, Is.EqualTo(new Vector2(200f, 260f)));
        Assert.That(prefab.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Name").fontSize,
            Is.EqualTo(20f));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ResponsiveCardSizeSurvivesDrawUseAndDiscardAnimations()
    {
        yield return new EnterPlayMode();
        SceneManager.LoadScene("BattleScene");
        yield return null;
        // HoverVisual has its own Canvas; resize the Canvas that actually owns the hand.
        Canvas canvas = Object.FindFirstObjectByType<CardHandController>().GetComponentInParent<Canvas>().rootCanvas;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.localScale = Vector3.one;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920f, 810f);
        yield return WaitForPlayTime(1f);
        CardHandController hand = Object.FindObjectsByType<CardHandController>(FindObjectsSortMode.None)[0];
        CardUI card = hand.GetComponentsInChildren<CardUI>()[0];
        AssertCardFits(hand, card, 121.92308f, 158.5f, 12.192308f);

        RectTransform drawPoint = (RectTransform)new SerializedObject(hand).FindProperty("drawStartPoint").objectReferenceValue;
        card.PlayDrawAnimation(drawPoint);
        yield return WaitForPlayTime(1f);
        AssertCardFits(hand, card, 121.92308f, 158.5f, 12.192308f);

        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        RectTransform usingPoint = (RectTransform)new SerializedObject(battle).FindProperty("cardUsingPoint").objectReferenceValue;
        hand.RemoveCard(card);
        card.transform.SetParent(hand.GetCardAnimationLayer(), true);
        yield return WaitForAnimation(card, card.UsedCardAnimation(usingPoint, hand.GetDiscardPilePoint()));
        Assert.That(((RectTransform)card.transform).rect.width, Is.EqualTo(121.92308f).Within(0.02f));
        Assert.That(((RectTransform)card.transform).rect.height, Is.EqualTo(158.5f).Within(0.02f));
        RectTransform visual = (RectTransform)new SerializedObject(card).FindProperty("cardVisual").objectReferenceValue;
        Assert.That(Vector3.Distance(visual.position, hand.GetDiscardPilePoint().position), Is.LessThan(0.02f));

        // The regular turn-end discard still removes all remaining hand cards.
        yield return DiscardRemainingHand(hand);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator CardReturningFromDragUsesTheResizedHandAndDoesNotChangeDuringDrag()
    {
        yield return new EnterPlayMode();
        SceneManager.LoadScene("BattleScene");
        yield return null;
        // HoverVisual has its own Canvas; resize the Canvas that actually owns the hand.
        Canvas canvas = Object.FindFirstObjectByType<CardHandController>().GetComponentInParent<Canvas>().rootCanvas;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.localScale = Vector3.one;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920f, 1080f);
        yield return WaitForPlayTime(1f);
        CardHandController hand = Object.FindObjectsByType<CardHandController>(FindObjectsSortMode.None)[0];
        CardUI card = hand.GetComponentsInChildren<CardUI>()[0];
        AssertCardFits(hand, card, 173.84615f, 226f, 17.384615f);
        Vector2 dragSize = ((RectTransform)card.transform).rect.size;
        card.OnPointerDown(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
        Assert.That(card.transform.parent, Is.SameAs(hand.GetCardAnimationLayer()));
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920f, 540f);
        yield return null;
        yield return null;
        Assert.That(((RectTransform)card.transform).rect.size, Is.EqualTo(dragSize));

        // EndDrag normally clears this flag before starting the return coroutine.
        typeof(CardUI).GetField("isDragging", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(card, false);
        typeof(CardUI).GetMethod("ReturnToHand", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(card, null);
        float deadline = Time.realtimeSinceStartup + 5f;
        while (card.transform.parent != hand.transform && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        Assert.That(card.transform.parent, Is.EqualTo(hand.transform));
        AssertCardFits(hand, card, 70f, 91f, 7f);
        yield return new ExitPlayMode();
    }

    private static void AssertCardFits(CardHandController hand, CardUI card, float width, float height, float fontSize)
    {
        RectTransform rect = (RectTransform)card.transform;
        Assert.That(rect.rect.width, Is.EqualTo(width).Within(0.02f), hand.name);
        Assert.That(rect.rect.height, Is.EqualTo(height).Within(0.02f), hand.name);
        RectTransform handRect = (RectTransform)hand.transform;
        RectOffset padding = hand.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().padding;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 local = handRect.InverseTransformPoint(corner);
            Assert.That(local.y, Is.InRange(handRect.rect.yMin + padding.bottom - 0.02f,
                handRect.rect.yMax - padding.top + 0.02f), hand.name);
            Assert.That(local.x, Is.InRange(handRect.rect.xMin - 0.02f, handRect.rect.xMax + 0.02f), hand.name);
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Card.prefab");
        var originalFonts = prefab.GetComponentsInChildren<TMP_Text>(true)
            .ToDictionary(text => text.name, text => text.fontSize);
        foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true))
            Assert.That(text.fontSize, Is.EqualTo(fontSize * originalFonts[text.name] / 20f).Within(0.02f), text.name);
        RectTransform visual = (RectTransform)new SerializedObject(card).FindProperty("cardVisual").objectReferenceValue;
        Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
    }

    private static IEnumerator WaitForPlayTime(float seconds)
    {
        // EditMode's runner does not wait on WaitForSeconds after EnterPlayMode.
        float target = Time.time + seconds;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.time < target && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(Time.time, Is.GreaterThanOrEqualTo(target));
    }

    private static IEnumerator WaitForAnimation(MonoBehaviour owner, IEnumerator animation)
    {
        bool completed = false;
        owner.StartCoroutine(CompleteAnimation(animation, () => completed = true));
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!completed && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(completed, Is.True, "The animation must finish before its result is checked.");
    }

    private static IEnumerator DiscardRemainingHand(CardHandController hand)
    {
        // Create this callback after EnterPlayMode's domain reload.
        int discarded = 0;
        yield return WaitForAnimation(hand, hand.DiscardAllCards(_ => discarded++));
        yield return null;
        Assert.That(discarded, Is.EqualTo(2));
        Assert.That(hand.GetCurrentHandSize(), Is.Zero);
    }

    private static IEnumerator CompleteAnimation(IEnumerator animation, System.Action complete)
    {
        yield return animation;
        complete();
    }
}
