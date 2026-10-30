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
    public void DeckButtonHasEditableTooltipAndIndependentVisualWithoutChangingItsToggle()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity", OpenSceneMode.Additive);
        try
        {
            DeckOverviewUI overview = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<DeckOverviewUI>(true)).Single();
            var overviewState = new SerializedObject(overview);
            var button = (UnityEngine.UI.Button)overviewState.FindProperty("toggleButton").objectReferenceValue;
            DeckPileHoverUI hover = button.GetComponent<DeckPileHoverUI>();
            Assert.That(hover, Is.Not.Null, "DeckButton needs the same hover and tooltip behaviour as the pile buttons.");
            var state = new SerializedObject(hover);
            var visual = (RectTransform)state.FindProperty("hoverVisual").objectReferenceValue;
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.parent, Is.EqualTo(button.transform));
            Assert.That(visual.GetComponent<UnityEngine.UI.Image>().sprite, Is.Not.Null);
            Assert.That(visual.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.False);
            Assert.That(button.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
            Assert.That(button.targetGraphic, Is.EqualTo(visual.GetComponent<UnityEngine.UI.Image>()));
            Assert.That(((RectTransform)button.transform).sizeDelta, Is.EqualTo(new Vector2(108, 108)));
            Assert.That(state.FindProperty("tooltipText").stringValue, Is.Not.Empty);
            Assert.That(state.FindProperty("tooltipPrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(state.FindProperty("allowWhileDeckOverviewOpen").boolValue, Is.True);
            Assert.That(Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Count(index =>
                button.onClick.GetPersistentTarget(index) == overview &&
                button.onClick.GetPersistentMethodName(index) == "Toggle"), Is.EqualTo(1));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest]
    public IEnumerator DeckButtonHoverWorksBeforeAndDuringOverviewAndResetsOnClickInspectionAndDisable()
    {
        yield return new EnterPlayMode();
        yield return VerifyDeckButton();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyDeckButton()
    {
        yield return LoadBattle();
        DeckOverviewUI overview = Object.FindFirstObjectByType<DeckOverviewUI>();
        var overviewState = new SerializedObject(overview);
        var button = (UnityEngine.UI.Button)overviewState.FindProperty("toggleButton").objectReferenceValue;
        DeckPileHoverUI hover = button.GetComponent<DeckPileHoverUI>();
        Assert.That(hover, Is.Not.Null);
        var state = new SerializedObject(hover);
        var visual = (RectTransform)state.FindProperty("hoverVisual").objectReferenceValue;
        var layer = (Canvas)state.FindProperty("hoverCanvas").objectReferenceValue;
        var rect = (RectTransform)button.transform;
        Vector3 position = rect.position;
        Vector2 size = rect.rect.size;
        Time.timeScale = 0;
        hover.OnPointerEnter(null);
        yield return Until(() => Tooltip() != null && Mathf.Abs(visual.localScale.x - 1.1f) < .001f && layer.overrideSorting);
        Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
        Assert.That(rect.position, Is.EqualTo(position));
        Assert.That(rect.rect.size, Is.EqualTo(size));
        Assert.That(Tooltip().GetComponentInChildren<TMP_Text>().text, Is.EqualTo(state.FindProperty("tooltipText").stringValue));
        Assert.That(Tooltip().GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True);
        var root = (RectTransform)button.GetComponentInParent<Canvas>().rootCanvas.transform;
        var corners = new Vector3[4];
        ((RectTransform)Tooltip().transform).GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 point = root.InverseTransformPoint(corner);
            Assert.That(point.x, Is.InRange(root.rect.xMin - .1f, root.rect.xMax + .1f));
            Assert.That(point.y, Is.InRange(root.rect.yMin - .1f, root.rect.yMax + .1f));
        }
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
        { position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position) }, hits);
        Assert.That(hits[0].gameObject, Is.EqualTo(button.gameObject));
        Time.timeScale = 1;
        button.onClick.Invoke();
        Assert.That(overview.IsOpen, Is.True);
        Assert.That(Tooltip(), Is.Null);
        Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        hover.OnPointerEnter(null);
        yield return Until(() => Tooltip() != null && Mathf.Abs(visual.localScale.x - 1.1f) < .001f);
        foreach (DeckPileButton pile in Object.FindObjectsByType<DeckPileButton>(FindObjectsSortMode.None))
        {
            Enter(pile);
            var pileState = new SerializedObject(pile.GetComponent<DeckPileHoverUI>());
            var pileVisual = (RectTransform)pileState.FindProperty("hoverVisual").objectReferenceValue;
            Assert.That(pileVisual.localScale, Is.EqualTo(Vector3.one), "Pile hover must remain blocked by the overview.");
        }
        hover.OnPointerExit(null);
        Assert.That(Tooltip(), Is.Null);
        yield return Until(() => visual.localScale == Vector3.one && !layer.overrideSorting);
        hover.OnPointerEnter(null);
        yield return Until(() => Tooltip() != null);
        var list = (DeckListUI)overviewState.FindProperty("dreamList").objectReferenceValue;
        var content = (Transform)new SerializedObject(list).FindProperty("content").objectReferenceValue;
        content.GetComponentInChildren<DeckCardUI>().OnPointerClick(new PointerEventData(EventSystem.current)
        { button = PointerEventData.InputButton.Right });
        CardInspectionUI inspection = Object.FindFirstObjectByType<CardInspectionUI>();
        Assert.That(inspection.IsOpen, Is.True);
        yield return Until(() => Tooltip() == null && visual.localScale == Vector3.one && !layer.overrideSorting);
        hover.OnPointerEnter(null);
        yield return Wait(.3f);
        Assert.That(Tooltip(), Is.Null);
        inspection.Close();
        yield return Until(() => !CardInspectionUI.BlocksGameplayInput);
        hover.OnPointerEnter(null);
        yield return Until(() => Tooltip() != null);
        button.onClick.Invoke();
        Assert.That(overview.IsOpen, Is.False);
        Assert.That(Tooltip(), Is.Null);
        Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        yield return null;
        hover.OnPointerEnter(null);
        yield return Until(() => Tooltip() != null);
        button.interactable = false;
        yield return Until(() => Tooltip() == null && visual.localScale == Vector3.one && !layer.overrideSorting);
        button.interactable = true;
        hover.OnPointerEnter(null);
        yield return Until(() => Tooltip() != null);
        button.gameObject.SetActive(false);
        Assert.That(Tooltip(), Is.Null);
        Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        // Inactive nested Canvases report root sorting; check restoration after re-enabling.
        button.gameObject.SetActive(true);
        yield return null;
        Assert.That(layer.overrideSorting, Is.False);
    }

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
