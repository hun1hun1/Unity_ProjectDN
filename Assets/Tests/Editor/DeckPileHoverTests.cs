using System.Collections;
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

public class DeckPileHoverTests
{
    [Test]
    public void AllFourPilesHaveEditableTooltipAndIndependentVisual()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity", OpenSceneMode.Additive);
        try
        {
            DeckPileButton[] piles = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<DeckPileButton>(true)).ToArray();
            Assert.That(piles.Length, Is.EqualTo(4));
            foreach (DeckPileButton pile in piles)
            {
                Component hover = pile.GetComponent("DeckPileHoverUI");
                Assert.That(hover, Is.Not.Null, pile.name + " needs pile hover behaviour.");
                var state = new SerializedObject(hover);
                RectTransform visual = (RectTransform)state.FindProperty("hoverVisual").objectReferenceValue;
                Assert.That(visual, Is.Not.Null);
                Assert.That(visual.parent, Is.EqualTo(pile.transform));
                Assert.That(visual.GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(visual.GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(pile.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(pile.GetComponent<Button>().targetGraphic, Is.EqualTo(visual.GetComponent<Image>()));
                Assert.That(((RectTransform)pile.transform).sizeDelta, Is.EqualTo(new Vector2(128, 128)));
                Assert.That(state.FindProperty("tooltipText").stringValue, Is.Not.Empty);
                Component prefab = (Component)state.FindProperty("tooltipPrefab").objectReferenceValue;
                Assert.That(prefab, Is.Not.Null);
                Assert.That(prefab.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest]
    public IEnumerator EveryPileEnlargesShowsItsTextAndKeepsItsClickAction()
    {
        yield return new EnterPlayMode();
        yield return VerifyAllPiles();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyAllPiles()
    {
        yield return LoadBattle();
        DeckListUI list = Object.FindObjectsByType<DeckListUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(candidate => candidate.name == "DeckListPanel");
        foreach (DeckPileButton pile in Object.FindObjectsByType<DeckPileButton>(FindObjectsSortMode.None))
        {
            Component hover = pile.GetComponent("DeckPileHoverUI");
            Assert.That(hover, Is.Not.Null);
            var state = new SerializedObject(hover);
            RectTransform visual = (RectTransform)state.FindProperty("hoverVisual").objectReferenceValue;
            Vector3 position = pile.transform.position;
            Vector2 size = ((RectTransform)pile.transform).rect.size;
            Enter(pile);
            yield return Wait(0.4f);
            Assert.That(visual.localScale.x, Is.EqualTo(1.1f).Within(0.001f));
            Assert.That(pile.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(pile.transform.position, Is.EqualTo(position));
            Assert.That(((RectTransform)pile.transform).rect.size, Is.EqualTo(size));
            GameObject tooltip = Tooltip();
            Assert.That(tooltip, Is.Not.Null);
            Assert.That(tooltip.GetComponentInChildren<TMP_Text>().text,
                Is.EqualTo(state.FindProperty("tooltipText").stringValue));
            Assert.That(tooltip.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True);
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(Pointer(pile), hits);
            Assert.That(hits[0].gameObject, Is.EqualTo(pile.gameObject));
            pile.GetComponent<Button>().onClick.Invoke();
            Assert.That(Tooltip(), Is.Null, "Opening a pile must hide its tooltip.");
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(list.gameObject.activeInHierarchy, Is.True);
            list.Close();
            Enter(pile);
            yield return Wait(0.4f);
            Exit(pile);
            Assert.That(Tooltip(), Is.Null);
            yield return Wait(0.2f);
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        }
    }

    [UnityTest]
    public IEnumerator TooltipOwnershipPauseEmptyTextAndDisableAreSafe()
    {
        yield return new EnterPlayMode();
        yield return VerifyLifecycle();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyLifecycle()
    {
        yield return LoadBattle();
        DeckPileButton[] piles = Object.FindObjectsByType<DeckPileButton>(FindObjectsSortMode.None);
        Time.timeScale = 0;
        Enter(piles[0]);
        yield return Wait(0.4f);
        GameObject firstTooltip = Tooltip();
        Assert.That(firstTooltip, Is.Not.Null, "Hover must work while paused.");
        Enter(piles[1]);
        yield return Wait(0.4f);
        Exit(piles[0]);
        Assert.That(Tooltip(), Is.EqualTo(firstTooltip), "A stale exit must not hide another pile's tooltip.");
        Component hover = piles[1].GetComponent("DeckPileHoverUI");
        var state = new SerializedObject(hover);
        RectTransform visual = (RectTransform)state.FindProperty("hoverVisual").objectReferenceValue;
        RectTransform canvas = (RectTransform)piles[1].GetComponentInParent<Canvas>().rootCanvas.transform;
        Vector3[] corners = new Vector3[4];
        ((RectTransform)firstTooltip.transform).GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 point = canvas.InverseTransformPoint(corner);
            Assert.That(point.x, Is.InRange(canvas.rect.xMin - 0.1f, canvas.rect.xMax + 0.1f));
            Assert.That(point.y, Is.InRange(canvas.rect.yMin - 0.1f, canvas.rect.yMax + 0.1f));
        }
        piles[1].gameObject.SetActive(false);
        Assert.That(Tooltip(), Is.Null);
        Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        piles[1].gameObject.SetActive(true);
        state.Update();
        state.FindProperty("tooltipText").stringValue = "";
        state.ApplyModifiedPropertiesWithoutUndo();
        Enter(piles[1]);
        yield return Wait(0.4f);
        Assert.That(visual.localScale.x, Is.EqualTo(1.1f).Within(0.001f));
        Assert.That(Tooltip(), Is.Null, "Empty descriptions should not show an empty panel.");
        piles[1].GetComponent<Button>().interactable = false;
        yield return null;
        Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        Time.timeScale = 1;
    }

    [TearDown]
    public void RestoreTime() { if (Application.isPlaying) Time.timeScale = 1; }

    private static PointerEventData Pointer(DeckPileButton pile) => new PointerEventData(EventSystem.current)
    { position = RectTransformUtility.WorldToScreenPoint(null, pile.transform.position) };
    private static void Enter(DeckPileButton pile) => ExecuteEvents.Execute(pile.gameObject, Pointer(pile), ExecuteEvents.pointerEnterHandler);
    private static void Exit(DeckPileButton pile) => ExecuteEvents.Execute(pile.gameObject, Pointer(pile), ExecuteEvents.pointerExitHandler);
    private static GameObject Tooltip() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
        .FirstOrDefault(component => component.GetType().Name == "DeckPileTooltipUI")?.gameObject;

    private static IEnumerator LoadBattle()
    {
        AsyncOperation loading = SceneManager.LoadSceneAsync("BattleScene");
        yield return Until(() => loading.isDone);
        yield return Until(() => Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Any(card => card.CanHover));
    }
    private static IEnumerator Until(System.Func<bool> ready)
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
