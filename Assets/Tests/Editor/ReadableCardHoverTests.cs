using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class ReadableCardHoverTests
{
    private Camera testCamera;
    private RenderTexture target;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (testCamera != null) testCamera.targetTexture = null;
        if (target != null)
        {
            target.Release();
            Object.DestroyImmediate(target);
        }
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator HandHoverMaintainsReadableTextWhileResolutionChanges()
    {
        yield return new EnterPlayMode();
        yield return OpenBattle();
        yield return WaitFor(() => Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Any(candidate => candidate.CanHover));
        CardUI card = Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).First(candidate => candidate.CanHover);
        yield return CheckResolutions(card, card.GetComponent<CardHoverUI>());
    }

    [UnityTest]
    public IEnumerator PileHoverMaintainsReadableTextWhileResolutionChanges()
    {
        yield return new EnterPlayMode();
        yield return OpenBattle();
        DeckListUI list = Object.FindObjectsByType<DeckListUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(candidate => candidate.name == "DeckListPanel");
        DeckController deck = Object.FindFirstObjectByType<DeckController>();
        list.ShowCards(deck.GetDrawPile(CardDeckType.TypeDream));
        yield return null;
        DeckCardUI card = list.GetComponentInChildren<DeckCardUI>();
        Assert.That(card, Is.Not.Null);
        yield return CheckResolutions(card, card.GetComponent<DeckCardHoverUI>());
        list.Close();
    }

    [UnityTest]
    public IEnumerator RewardHoverKeepsItsConfirmButtonVisibleAt720p()
    {
        yield return new EnterPlayMode();
        yield return VerifyRewardHover();
    }

    private IEnumerator VerifyRewardHover()
    {
        yield return OpenBattle();
        CardRewardUI rewards = Object.FindFirstObjectByType<CardRewardUI>();
        BattleRewardSystem flow = Object.FindFirstObjectByType<BattleRewardSystem>();
        Assert.That(rewards, Is.Not.Null);
        Assert.That(flow, Is.Not.Null);
        Object.FindFirstObjectByType<BattleManager>().GetTarget().TakeDamage(100000);
        CanvasGroup visibility = rewards.GetComponent<CanvasGroup>();
        yield return WaitFor(() => flow.State == BattleRewardState.Summary && visibility.interactable);
        rewards.ShowReward();
        rewards.SelectDream();
        yield return WaitFor(() => rewards.GetComponentsInChildren<DeckCardUI>().Any(candidate =>
            candidate != null && candidate.transform.Find("HoverVisual") != null));
        DeckCardUI card = rewards.GetComponentsInChildren<DeckCardUI>().First();
        RectTransform visual = (RectTransform)card.transform.Find("HoverVisual");
        TMP_Text description = card.GetComponentsInChildren<TMP_Text>(true).Single(text => text.name == "Description");
        Canvas canvas = card.GetComponentInParent<Canvas>().rootCanvas;
        ConfigureCanvas(canvas);
        SetResolution(canvas, 1280, 720);
        yield return null;
        yield return null;
        ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
        yield return WaitFor(() => FontPixels(description) >= 15.9f);
        var corners = new Vector3[4];
        visual.GetWorldCorners(corners);
        float bottom = testCamera.WorldToScreenPoint(corners[0]).y;
        Button confirmButton = (Button)new SerializedObject(rewards).FindProperty("confirmButton").objectReferenceValue;
        RectTransform confirm = (RectTransform)confirmButton.transform;
        confirm.GetWorldCorners(corners);
        Assert.That(bottom, Is.GreaterThan(testCamera.WorldToScreenPoint(corners[1]).y));
        AssertInsideScreen(visual, 1280, 720);
        Capture("RewardCard-1280x720");
    }

    [Test]
    public void CurrentDescriptionsFitTheCardBody()
    {
        GameObject canvas = new GameObject("DescriptionMeasurement", typeof(Canvas));
        try
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Card.prefab");
            CardUI card = Object.Instantiate(prefab, canvas.transform).GetComponent<CardUI>();
            TMP_Text description = (TMP_Text)new SerializedObject(card).FindProperty("descriptionText").objectReferenceValue;
            Canvas.ForceUpdateCanvases();
            var overflow = new System.Collections.Generic.List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:CardData", new[] { "Assets/CardData" }))
            {
                CardData data = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
                description.text = CardDescriptionBuilder.Build(data);
                description.ForceMeshUpdate(true);
                if (description.isTextOverflowing)
                    overflow.Add(data.cardName + " (" + AssetDatabase.GUIDToAssetPath(guid) + ")");
            }
            Assert.That(overflow, Is.Empty, "Descriptions overflowing before enlargement: " + string.Join(", ", overflow));
        }
        finally { Object.DestroyImmediate(canvas); }
    }

    private IEnumerator OpenBattle()
    {
        AsyncOperation loading = SceneManager.LoadSceneAsync("BattleScene");
        while (!loading.isDone) yield return null;
        yield return WaitFor(() => Object.FindFirstObjectByType<BattleManager>()?.CanInspectCards == true);
        EventSystem.current.enabled = false;
    }

    private IEnumerator CheckResolutions(MonoBehaviour card, MonoBehaviour hover)
    {
        RectTransform root = (RectTransform)card.transform;
        RectTransform visual = (RectTransform)root.Find("HoverVisual");
        TMP_Text description = (TMP_Text)new SerializedObject(card).FindProperty("descriptionText").objectReferenceValue;
        Canvas canvas = root.GetComponentInParent<Canvas>().rootCanvas;
        ConfigureCanvas(canvas);
        var pointer = new PointerEventData(EventSystem.current);
        float originalFontSize = 0f;
        int sibling = root.GetSiblingIndex();
        var widths = new[] { 1280, 1600, 1920, 1280 };
        var heights = new[] { 720, 900, 1080, 720 };

        for (int i = 0; i < widths.Length; i++)
        {
            SetResolution(canvas, widths[i], heights[i]);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Vector2 originalSize = root.rect.size;
            if (i == 0)
            {
                originalFontSize = description.fontSize;
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            }
            yield return WaitFor(() => FontPixels(description) >= 15.9f);
            Assert.That(FontPixels(description), Is.InRange(15.9f, 16.2f), $"{widths[i]}x{heights[i]}");
            Assert.That(description.fontSize, Is.EqualTo(originalFontSize));
            Assert.That(root.rect.size, Is.EqualTo(originalSize));
            Assert.That(root.GetSiblingIndex(), Is.EqualTo(sibling));
            AssertInsideScreen(visual, widths[i], heights[i]);
            if (i < 3) Capture(card.GetType().Name + "-" + widths[i] + "x" + heights[i]);
        }

        // Preserve the configured enlargement when it already exceeds the reading target.
        var settings = new SerializedObject(hover);
        settings.FindProperty("hoverScale").floatValue = 3f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        yield return WaitFor(() => visual.localScale.x >= 2.99f);
        Assert.That(visual.localScale.x, Is.EqualTo(3f).Within(0.01f));

        // A viewport too small for the reading target still keeps the entire card visible.
        SetResolution(canvas, 320, 180);
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        AssertInsideScreen(visual, 320, 180);

        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        yield return WaitFor(() => visual.localScale == Vector3.one && !visual.GetComponent<Canvas>().overrideSorting);
        Assert.That(visual.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(visual.GetComponent<Canvas>().overrideSorting, Is.False);
    }

    private void ConfigureCanvas(Canvas canvas)
    {
        canvas.GetComponent<CanvasScaler>().enabled = false;
        testCamera = new GameObject("ReadableHoverCamera", typeof(Camera)).GetComponent<Camera>();
        testCamera.orthographic = true;
        testCamera.orthographicSize = 5;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = testCamera;
        canvas.planeDistance = 1;
    }

    private void SetResolution(Canvas canvas, int width, int height)
    {
        if (target != null)
        {
            testCamera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
        }
        target = new RenderTexture(width, height, 24);
        target.Create();
        testCamera.targetTexture = target;
        canvas.scaleFactor = width / 1920f;
    }

    private float FontPixels(TMP_Text text)
    {
        Vector3 origin = text.transform.position;
        Vector3 top = text.transform.TransformPoint(Vector3.up * text.fontSize);
        return Vector2.Distance(testCamera.WorldToScreenPoint(origin), testCamera.WorldToScreenPoint(top));
    }

    private void Capture(string name)
    {
        testCamera.Render();
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            string folder = System.IO.Path.GetFullPath(".utmp/readable-card-hover");
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(pixels);
        }
    }

    private void AssertInsideScreen(RectTransform visual, int width, int height)
    {
        var corners = new Vector3[4];
        visual.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 point = testCamera.WorldToScreenPoint(corner);
            Assert.That(point.x, Is.InRange(-0.1f, width + 0.1f));
            Assert.That(point.y, Is.InRange(-0.1f, height + 0.1f));
        }
    }

    private static IEnumerator WaitFor(System.Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        do { yield return null; }
        while (!condition() && Time.realtimeSinceStartup < deadline);
        Assert.That(condition(), Is.True, "The card must reach the requested readable hover state.");
    }
}
