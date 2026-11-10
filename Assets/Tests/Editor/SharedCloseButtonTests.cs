using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class SharedCloseButtonTests
{
    private const int Width = 960;
    private const int Height = 540;
    private const string SpritePath = "Assets/Images/UI/shared-close-button.png";
    private const string CaptureFolder = ".utmp/shared-close-button";

    [UnityTearDown]
    public IEnumerator ExitPlay()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest, Category("RequiresGraphics")]
    public IEnumerator SharedCloseButtonFitsAndClosesAllSixWindowsAtSmallResolution()
    {
        yield return new EnterPlayMode();
        Directory.CreateDirectory(CaptureFolder);
        var savePath = Path.GetFullPath(Path.Combine(CaptureFolder, Guid.NewGuid() + ".json"));
        RunSaves.UseStore(new RunSaveStore(savePath));
        try
        {
            yield return SceneManager.LoadSceneAsync("TitleScene");
            yield return null;
            yield return CheckSettings("Title");

            yield return SceneManager.LoadSceneAsync("BattleScene");
            yield return WaitFor(() => Object.FindFirstObjectByType<BattleManager>()?.CanInspectCards == true);
            DeckListUI pileList = Object.FindObjectsByType<DeckListUI>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Single(list => list.name == "DeckListPanel");
            Object.FindObjectsByType<DeckPileButton>(FindObjectsSortMode.None)
                .Single(pile => pile.name == "DreamDrawPileButton")
                .GetComponent<Button>().onClick.Invoke();
            Assert.That(pileList.gameObject.activeInHierarchy, Is.True);
            Assert.That(pileList.GetComponentInChildren<DeckCardUI>(), Is.Not.Null);
            yield return CheckCloseButton(pileList.transform.Find("CloseButton").GetComponent<Button>(),
                "Battle-CardPile", () => !pileList.gameObject.activeSelf);
            yield return CheckDeck("Battle");
            yield return CheckSettings("Battle");

            yield return SceneManager.LoadSceneAsync("MapScene");
            yield return WaitFor(() => RunSession.Instance != null && !RunSession.Instance.IsTransitioning &&
                Object.FindFirstObjectByType<MapController>() != null);
            yield return CheckDeck("Map");
            yield return CheckSettings("Map");
        }
        finally
        {
            RunSaves.UseStore(null);
        }
    }

    private static IEnumerator CheckDeck(string scene)
    {
        DeckOverviewUI deck = Object.FindFirstObjectByType<DeckOverviewUI>();
        deck.Toggle();
        Assert.That(deck.IsOpen, Is.True, scene);
        yield return CheckCloseButton(GetCloseButton(deck), scene + "-Deck", () => !deck.IsOpen);
    }

    private static IEnumerator CheckSettings(string scene)
    {
        SettingsUI settings = Object.FindFirstObjectByType<SettingsUI>();
        settings.Toggle();
        Assert.That(settings.IsOpen, Is.True, scene);
        yield return CheckCloseButton(GetCloseButton(settings), scene + "-Settings", () => !settings.IsOpen);
    }

    private static Button GetCloseButton(MonoBehaviour owner) =>
        (Button)new SerializedObject(owner).FindProperty("closeButton").objectReferenceValue;

    private static IEnumerator CheckCloseButton(Button button, string name, Func<bool> isClosed)
    {
        Image graphic = button.targetGraphic as Image;
        Assert.That(graphic, Is.Not.Null, name);
        Assert.That(AssetDatabase.GetAssetPath(graphic.sprite), Is.EqualTo(SpritePath), name);
        RectTransform rect = graphic.rectTransform;
        Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(56f, 56f)), name);
        Assert.That(rect.anchorMin, Is.EqualTo(Vector2.one), name);
        Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one), name);
        Assert.That(rect.pivot, Is.EqualTo(Vector2.one), name);
        Assert.That(rect.anchoredPosition.x, Is.LessThanOrEqualTo(-20f), name);
        Assert.That(rect.anchoredPosition.y, Is.LessThanOrEqualTo(-20f), name);

        Canvas canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        bool scalerEnabled = scaler.enabled;
        RenderMode originalMode = canvas.renderMode;
        Camera originalCamera = canvas.worldCamera;
        float originalDistance = canvas.planeDistance;
        var camera = new GameObject("Small close button test camera", typeof(Camera)).GetComponent<Camera>();
        camera.orthographic = true;
        var target = new RenderTexture(Width, Height, 24);
        try
        {
            camera.targetTexture = target;
            scaler.enabled = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.scaleFactor = Width / scaler.referenceResolution.x;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Capture(camera, target, name);

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, name);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),
                Is.SameAs(button.gameObject), name);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(isClosed(), Is.True, name);
        }
        finally
        {
            canvas.renderMode = originalMode;
            canvas.worldCamera = originalCamera;
            canvas.planeDistance = originalDistance;
            scaler.enabled = scalerEnabled;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(camera.gameObject);
        }
        yield return null;
    }

    private static void Capture(Camera camera, RenderTexture target, string name)
    {
        var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        RenderTexture original = RenderTexture.active;
        try
        {
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(CaptureFolder, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = original;
            Object.Destroy(pixels);
        }
    }

    private static IEnumerator WaitFor(Func<bool> ready)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(ready(), Is.True, "Scene did not become ready.");
    }
}
