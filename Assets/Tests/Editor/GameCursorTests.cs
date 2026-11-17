using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class GameCursorTests
{
    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        RunSaves.UseStore(null);
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [TestCase("Default")]
    [TestCase("Hover")]
    public void CursorArtworkIsImportedAsAReadable32PixelCursor(string variant)
    {
        string path = $"Assets/Resources/Cursors/{variant}.png";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Assert.That(texture, Is.Not.Null, path);
        Assert.That(texture.width, Is.EqualTo(32));
        Assert.That(texture.height, Is.EqualTo(32));
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Cursor));
        Assert.That(importer.isReadable, Is.True);
    }

    [Test]
    public void BothTexturesShareTheirSilhouetteAndClickableTip()
    {
        Texture2D normal = Resources.Load<Texture2D>("Cursors/Default");
        Texture2D hover = Resources.Load<Texture2D>("Cursors/Hover");
        Color32[] normalPixels = normal.GetPixels32();
        Color32[] hoverPixels = hover.GetPixels32();
        Assert.That(normalPixels.Select(pixel => pixel.a), Is.EqualTo(hoverPixels.Select(pixel => pixel.a)));
        CollectionAssert.AreNotEqual(normalPixels, hoverPixels);
        var hotspot = (Vector2)typeof(GameCursor).GetField("Hotspot", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var tip = (normal.height - 1 - (int)hotspot.y) * normal.width + (int)hotspot.x;
        Assert.That(normalPixels[tip].a, Is.GreaterThan(127));
        Assert.That(hoverPixels[tip], Is.EqualTo(normalPixels[tip]));
    }

    [Test]
    public void HoverUsesTheHitControlsInteractability()
    {
        var root = new GameObject("Button", typeof(RectTransform), typeof(Button));
        var label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(root.transform, false);
        try
        {
            MethodInfo classify = typeof(GameCursor).GetMethod("IsHoverTarget", BindingFlags.NonPublic | BindingFlags.Static);
            var button = root.GetComponent<Button>();
            Assert.That(classify.Invoke(null, new object[] { label }), Is.True);
            button.interactable = false;
            Assert.That(classify.Invoke(null, new object[] { label }), Is.False);
            button.interactable = true;
            button.enabled = false;
            Assert.That(classify.Invoke(null, new object[] { label }), Is.False);
            Assert.That(classify.Invoke(null, new object[] { null }), Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator OneCursorServiceSurvivesTheTitleMapAndBattleScenes()
    {
        yield return new EnterPlayMode();
        RunSaves.UseStore(new RunSaveStore(Path.GetFullPath(".utmp/run-save/cursor-" + System.Guid.NewGuid().ToString("N") + ".json")));
        GameCursor cursor = Object.FindFirstObjectByType<GameCursor>();
        Assert.That(cursor, Is.Not.Null);
        yield return SceneManager.LoadSceneAsync("TitleScene");
        Assert.That(Object.FindFirstObjectByType<GameCursor>(), Is.SameAs(cursor));
        yield return SceneManager.LoadSceneAsync("MapScene");
        Assert.That(Object.FindFirstObjectByType<GameCursor>(), Is.SameAs(cursor));
        yield return SceneManager.LoadSceneAsync("BattleScene");
        Assert.That(Object.FindObjectsByType<GameCursor>(FindObjectsSortMode.None), Is.EqualTo(new[] { cursor }));
    }
}
