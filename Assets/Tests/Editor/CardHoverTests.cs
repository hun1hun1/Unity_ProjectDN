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

public class CardHoverTests
{
    [Test]
    public void CardPrefabSeparatesHoverAnimationAndPointerArea()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Card.prefab");
        CardUI card = prefab.GetComponent<CardUI>();
        CardHoverUI hover = prefab.GetComponent<CardHoverUI>();
        Assert.That(hover, Is.Not.Null);
        RectTransform visual = (RectTransform)new SerializedObject(card).FindProperty("cardVisual").objectReferenceValue;
        Assert.That(visual.parent.name, Is.EqualTo("HoverVisual"));
        Assert.That(visual.parent.parent, Is.EqualTo(prefab.transform));
        Assert.That(new SerializedObject(hover).FindProperty("hoverVisual").objectReferenceValue, Is.EqualTo(visual.parent));
        UnityEngine.UI.Image hitArea = prefab.GetComponent<UnityEngine.UI.Image>();
        Assert.That(hitArea.raycastTarget, Is.True);
        Assert.That(hitArea.color.a, Is.Zero);
        foreach (UnityEngine.UI.Graphic graphic in visual.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            Assert.That(graphic.raycastTarget, Is.False, graphic.name);
        Assert.That(visual.parent.GetComponent<Canvas>().overrideSorting, Is.False);
    }

    [UnityTest]
    public IEnumerator BothHandsEnlargeOnlyTheVisualKeepRaycastsAndRestoreLayout()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForPlayTime(1f);
        CardHandController[] hands = Object.FindObjectsByType<CardHandController>(FindObjectsSortMode.None);
        foreach (CardHandController hand in hands)
        {
            CardUI card = hand.GetComponentsInChildren<CardUI>()[0];
            RectTransform root = (RectTransform)card.transform;
            RectTransform wrapper = (RectTransform)card.transform.Find("HoverVisual");
            RectTransform visual = (RectTransform)wrapper.Find("CardVisual");
            Assert.That(visual, Is.Not.Null);
            Vector2 size = root.rect.size;
            Vector3 position = root.position;
            int sibling = root.GetSiblingIndex();
            var fonts = new Dictionary<TMP_Text, float>();
            foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true)) fonts[text] = text.fontSize;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, root.position)
            };
            AssertRaycastCard(pointer, card);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return WaitForPlayTime(0.25f);
            Assert.That(wrapper.localScale.x, Is.EqualTo(ConfiguredHoverScale(card)).Within(0.001f));
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(root.rect.size, Is.EqualTo(size));
            Assert.That(root.position, Is.EqualTo(position));
            Assert.That(root.GetSiblingIndex(), Is.EqualTo(sibling));
            Assert.That(wrapper.GetComponent<Canvas>().overrideSorting, Is.True);
            foreach (var entry in fonts) Assert.That(entry.Key.fontSize, Is.EqualTo(entry.Value));
            AssertRaycastCard(pointer, card);
            AssertInsideCanvas(visual, root.GetComponentInParent<Canvas>().rootCanvas);
            // Interrupting and restarting hover must converge to the requested size.
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return WaitForPlayTime(0.04f);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return WaitForPlayTime(0.25f);
            Assert.That(wrapper.localScale.x, Is.EqualTo(ConfiguredHoverScale(card)).Within(0.001f));
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return WaitForPlayTime(0.25f);
            Assert.That(wrapper.localScale, Is.EqualTo(Vector3.one));
            Assert.That(wrapper.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(wrapper.GetComponent<Canvas>().overrideSorting, Is.False);
            Assert.That(root.rect.size, Is.EqualTo(size));
            Assert.That(root.GetSiblingIndex(), Is.EqualTo(sibling));

            // Edge placement in a wide Canvas must stay within its bounds after enlargement.
            Canvas canvas = root.GetComponentInParent<Canvas>().rootCanvas;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.transform.localScale = Vector3.one;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(1920, 540);
            yield return null;
            Canvas.ForceUpdateCanvases();
            hand.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().enabled = false;
            foreach (Vector2 edge in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) })
            {
                Vector2 center = new Vector2(
                    edge.x < 0 ? canvasRect.rect.xMin + root.rect.width / 2 : canvasRect.rect.xMax - root.rect.width / 2,
                    edge.y < 0 ? canvasRect.rect.yMin + root.rect.height / 2 : canvasRect.rect.yMax - root.rect.height / 2);
                root.position = canvasRect.TransformPoint(center);
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return WaitForPlayTime(0.25f);
                AssertInsideCanvas(visual, canvas);
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                yield return WaitForPlayTime(0.25f);
            }
            hand.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.ForceUpdateCanvases();
            yield return null;
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator HoverDoesNotMultiplyDragOrInterfereWithDrawReturnUseDiscardAndDisable()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForPlayTime(1f);
        CardHandController hand = Object.FindObjectsByType<CardHandController>(FindObjectsSortMode.None)[0];
        CardUI card = hand.GetComponentsInChildren<CardUI>()[0];
        RectTransform wrapper = (RectTransform)card.transform.Find("HoverVisual");
        RectTransform visual = (RectTransform)wrapper.Find("CardVisual");
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return WaitForPlayTime(0.25f);
        Assert.That(wrapper.localScale.x, Is.EqualTo(ConfiguredHoverScale(card)).Within(0.001f));
        card.OnPointerDown(pointer);
        Assert.That(wrapper.localScale, Is.EqualTo(Vector3.one));
        Assert.That(wrapper.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(card.transform.parent, Is.EqualTo(hand.GetCardAnimationLayer()));
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return WaitForPlayTime(0.2f);
        Assert.That(visual.localScale.x, Is.EqualTo(new SerializedObject(card).FindProperty("selectedScale").floatValue).Within(0.001f), "Hover must not compound the drag scale.");
        Assert.That(wrapper.localScale, Is.EqualTo(Vector3.one));
        typeof(CardUI).GetField("isDragging", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(card, false);
        typeof(CardUI).GetMethod("ReturnToHand", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(card, null);
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return WaitForPlayTime(0.05f);
        Assert.That(wrapper.localScale, Is.EqualTo(Vector3.one));
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        yield return WaitForPlayTime(0.3f);
        Assert.That(card.transform.parent, Is.EqualTo(hand.transform));
        Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return WaitForPlayTime(0.25f);
        card.gameObject.SetActive(false);
        Assert.That(wrapper.localScale, Is.EqualTo(Vector3.one));
        card.gameObject.SetActive(true);
        // Inactive nested Canvases report root sorting; check the active Canvas after LateUpdate.
        yield return null;
        Assert.That(wrapper.GetComponent<Canvas>().overrideSorting, Is.False);
        RectTransform start = (RectTransform)new SerializedObject(hand).FindProperty("drawStartPoint").objectReferenceValue;
        card.PlayDrawAnimation(start);
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return WaitForPlayTime(0.15f);
        Assert.That(wrapper.localScale, Is.EqualTo(Vector3.one));
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        yield return WaitForPlayTime(0.7f);
        Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return WaitForPlayTime(0.25f);
        card.MarkAsUsed();
        Assert.That(wrapper.localScale, Is.EqualTo(Vector3.one));
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return WaitForPlayTime(0.2f);
        Assert.That(wrapper.localScale, Is.EqualTo(Vector3.one));
        card.transform.SetParent(hand.GetCardAnimationLayer(), true);
        RectTransform usingPoint = (RectTransform)new SerializedObject(Object.FindFirstObjectByType<BattleManager>())
            .FindProperty("cardUsingPoint").objectReferenceValue;
        yield return WaitForAnimation(card, card.UsedCardAnimation(usingPoint, hand.GetDiscardPilePoint()));
        Assert.That(wrapper.localScale, Is.EqualTo(Vector3.one));
        Assert.That(Vector3.Distance(visual.position, hand.GetDiscardPilePoint().position), Is.LessThan(0.02f));
        yield return new ExitPlayMode();
    }

    private static float ConfiguredHoverScale(CardUI card) =>
        new SerializedObject(card.GetComponent<CardHoverUI>()).FindProperty("hoverScale").floatValue;

    private static void AssertRaycastCard(PointerEventData pointer, CardUI card)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits.Count, Is.GreaterThan(0));
        Assert.That(hits[0].gameObject, Is.EqualTo(card.gameObject));
    }

    private static void AssertInsideCanvas(RectTransform visual, Canvas canvas)
    {
        RectTransform root = (RectTransform)canvas.transform;
        var corners = new Vector3[4];
        visual.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 point = root.InverseTransformPoint(corner);
            Assert.That(point.x, Is.InRange(root.rect.xMin - 0.05f, root.rect.xMax + 0.05f));
            Assert.That(point.y, Is.InRange(root.rect.yMin - 0.05f, root.rect.yMax + 0.05f));
        }
    }

    private static IEnumerator WaitForPlayTime(float seconds)
    {
        float target = Time.unscaledTime + seconds;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.unscaledTime < target && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Time.unscaledTime, Is.GreaterThanOrEqualTo(target));
    }

    private static IEnumerator WaitForAnimation(MonoBehaviour owner, IEnumerator animation)
    {
        bool completed = false;
        owner.StartCoroutine(CompleteAnimation(animation, () => completed = true));
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!completed && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(completed, Is.True);
    }

    private static IEnumerator CompleteAnimation(IEnumerator animation, System.Action complete)
    {
        yield return animation;
        complete();
    }
}
