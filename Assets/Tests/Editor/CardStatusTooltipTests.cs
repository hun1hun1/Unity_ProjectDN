using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class CardStatusTooltipTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        if (Application.isPlaying) Time.timeScale = 1f;
        foreach (Object value in created) if (value != null) Object.DestroyImmediate(value);
        created.Clear();
    }

    [UnityTest]
    public IEnumerator HoverWaitsOneSecondShowsDefinitionAndResetsOnExit()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForCards();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI card = Cards()[0];
        card.SetCard(AssetDatabase.LoadAssetAtPath<CardData>(
            "Assets/CardData/Soldier/Starting/Nightmare/Attack/2.Corrosion/Corrosion.asset"), battle);
        var pointer = new PointerEventData(EventSystem.current);
        Enter(card, pointer);
        Time.timeScale = 0f;
        yield return Wait(0.65f);
        Assert.That(VisibleTooltip(card), Is.Null);
        yield return Wait(0.5f);
        TMP_Text text = VisibleTooltip(card);
        Assert.That(text, Is.Not.Null, "A status card must show its tooltip after one second of hover.");
        Assert.That(text.text, Does.Contain("녹슬음").And.Contain("25%"));
        Assert.That(text.text, Does.Not.Contain("남은 턴"));
        Assert.That(battle.GetPlayer().StatusEffects.ActiveEffects, Is.Empty);
        Assert.That(battle.GetTarget().StatusEffects.ActiveEffects, Is.Empty);
        Exit(card, pointer);
        Assert.That(VisibleTooltip(card), Is.Null);
        Enter(card, pointer);
        yield return Wait(0.65f);
        Assert.That(VisibleTooltip(card), Is.Null, "Reentry must start a fresh delay.");
        yield return Wait(0.5f);
        Assert.That(VisibleTooltip(card), Is.Not.Null);
        card.MarkAsUsed();
        Assert.That(VisibleTooltip(card), Is.Null);
        Time.timeScale = 1f;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator NestedStatusesDeduplicateAndPreviousOwnerCannotHideNewTooltip()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForCards();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI[] cards = Cards();
        ApplyStatusEffect rust = Apply("Rust");
        ApplyStatusEffect exposure = Apply("WeaknessExposure");
        SpendAmmoBonusEffect nested = Asset<SpendAmmoBonusEffect>();
        SetField(nested, "bonusEffects", new CardEffect[] { rust, exposure, nested });
        CardData combined = Card(nested, rust);
        cards[0].SetCard(combined, battle);
        cards[1].SetCard(Card(rust), battle);
        var pointer = new PointerEventData(EventSystem.current);
        Enter(cards[0], pointer);
        yield return Wait(1.15f);
        TMP_Text first = VisibleTooltip(cards[0]);
        Assert.That(first, Is.Not.Null);
        Assert.That(Regex.Matches(first.text, "녹슬음").Count, Is.EqualTo(1));
        Assert.That(Regex.Matches(first.text, "약점 노출").Count, Is.EqualTo(1));
        Assert.That(battle.GetPlayer().GetAmmoSystem().CurrentAmmo, Is.Zero);
        Enter(cards[1], pointer);
        yield return Wait(1.15f);
        TMP_Text second = VisibleTooltip(cards[1]);
        Assert.That(second, Is.SameAs(first), "One panel must be shared by cards on the same Canvas.");
        Assert.That(second.text, Does.Contain("녹슬음").And.Not.Contain("약점 노출"));
        Exit(cards[0], pointer);
        Assert.That(VisibleTooltip(cards[1]), Is.Not.Null, "The previous owner cannot hide the new owner.");
        cards[1].gameObject.SetActive(false);
        Assert.That(VisibleTooltip(cards[0]), Is.Null);
        cards[1].gameObject.SetActive(true);
        Enter(cards[1], pointer);
        yield return Wait(1.15f);
        cards[1].SetCard(Card(), battle);
        Assert.That(VisibleTooltip(cards[1]), Is.Null, "Rebinding must clear the old tooltip and timer.");
        Enter(cards[1], pointer);
        yield return Wait(1.15f);
        Assert.That(VisibleTooltip(cards[1]), Is.Null, "A card without statuses must not show the panel.");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator TooltipFlipsAtCanvasEdgesAndDragClosesIt()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForCards();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI card = Cards()[0];
        card.SetCard(Card(Apply("Rust"), Apply("WeaknessExposure")), battle);
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        Enter(card, pointer);
        yield return Wait(1.15f);
        TMP_Text text = VisibleTooltip(card);
        Assert.That(text, Is.Not.Null);
        foreach (UnityEngine.UI.Graphic graphic in text.transform.parent.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            Assert.That(graphic.raycastTarget, Is.False);
        Assert.That(text.transform.parent.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        Assert.That(text.GetComponentInParent<Canvas>().sortingOrder,
            Is.GreaterThan(card.transform.Find("HoverVisual").GetComponent<Canvas>().sortingOrder));
        card.OnPointerDown(pointer);
        Assert.That(VisibleTooltip(card), Is.Null, "Drag must close immediately.");
        SetField(card, "isDragging", false);
        card.transform.SetParent(card.GetHandController().transform, true);
        Canvas canvas = card.GetComponentInParent<Canvas>().rootCanvas;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.localScale = Vector3.one;
        RectTransform bounds = (RectTransform)canvas.transform;
        bounds.sizeDelta = new Vector2(1920, 540);
        card.GetHandController().GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().enabled = false;
        yield return null;
        Canvas.ForceUpdateCanvases();
        RectTransform root = (RectTransform)card.transform;
        foreach (bool rightEdge in new[] { false, true })
        {
            Exit(card, pointer);
            root.position = bounds.TransformPoint(new Vector2(rightEdge ? bounds.rect.xMax - root.rect.width / 2
                : bounds.rect.xMin + root.rect.width / 2, bounds.rect.yMin + root.rect.height / 2));
            Enter(card, pointer);
            yield return Wait(1.15f);
            text = VisibleTooltip(card);
            Assert.That(text, Is.Not.Null);
            Rect tooltipRect = RectInCanvas((RectTransform)text.transform.parent, bounds);
            Rect cardRect = RectInCanvas((RectTransform)card.transform.Find("HoverVisual"), bounds);
            if (rightEdge) Assert.That(tooltipRect.xMax, Is.LessThan(cardRect.xMin));
            else Assert.That(tooltipRect.xMin, Is.GreaterThan(cardRect.xMax));
            Assert.That(tooltipRect.xMin, Is.GreaterThanOrEqualTo(bounds.rect.xMin - 0.1f));
            Assert.That(tooltipRect.xMax, Is.LessThanOrEqualTo(bounds.rect.xMax + 0.1f));
            Assert.That(tooltipRect.yMin, Is.GreaterThanOrEqualTo(bounds.rect.yMin - 0.1f));
            Assert.That(tooltipRect.yMax, Is.LessThanOrEqualTo(bounds.rect.yMax + 0.1f));
            Assert.That(text.transform.parent.localScale, Is.EqualTo(Vector3.one));
        }
        yield return new ExitPlayMode();
    }

    private static CardUI[] Cards() => Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None)
        .Where(card => card.CanHover).ToArray();

    private static IEnumerator WaitForCards()
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Cards().Length < 2 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Cards().Length, Is.GreaterThanOrEqualTo(2), "Wait for draw animation completion before testing hover.");
    }

    private static TMP_Text VisibleTooltip(CardUI card) => card.GetComponentInParent<Canvas>().rootCanvas
        .GetComponentsInChildren<TMP_Text>(true).SingleOrDefault(text =>
            text.transform.parent.name.StartsWith("CardStatusTooltip") && text.gameObject.activeInHierarchy);

    private static void Enter(CardUI card, PointerEventData pointer) =>
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
    private static void Exit(CardUI card, PointerEventData pointer) =>
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);

    private ApplyStatusEffect Apply(string name)
    {
        ApplyStatusEffect effect = Asset<ApplyStatusEffect>();
        SetField(effect, "status", AssetDatabase.LoadAssetAtPath<StatusEffectData>($"Assets/StatusData/{name}.asset"));
        return effect;
    }

    private CardData Card(params CardEffect[] effects)
    {
        CardData card = Asset<CardData>();
        card.effects = effects;
        card.description = "상태이상 카드";
        return card;
    }

    private T Asset<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        created.Add(value);
        return value;
    }

    private static void SetField(object value, string name, object fieldValue) => value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, fieldValue);

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.unscaledTime < end && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Time.unscaledTime, Is.GreaterThanOrEqualTo(end));
    }

    private static Rect RectInCanvas(RectTransform rect, RectTransform canvas)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in corners)
        {
            Vector2 point = canvas.InverseTransformPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
