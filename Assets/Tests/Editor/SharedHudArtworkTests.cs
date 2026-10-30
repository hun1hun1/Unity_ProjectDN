using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class SharedHudArtworkTests
{
    private const string Art = "Assets/Images/UI/CharacterHUD/";
    [UnityTearDown] public IEnumerator ExitPlay()
    { if (Application.isPlaying) yield return new ExitPlayMode(); }

    [TestCase("Small")]
    [TestCase("Normal")]
    [TestCase("Large")]
    public void PartialHealthUsesSeparateArtworkLayersAndKeepsFrameAndCounterReadable(string size)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterSizes/" + size + "CharacterInfoUI.prefab");
        GameObject root = Object.Instantiate(prefab);
        try
        {
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.sizeDelta = new Vector2(size == "Small" ? 288 : size == "Normal" ? 320 : 360, 84);
            var hud = root.GetComponent<CharacterInfoUI>(); hud.ValidateBindings();
            RectTransform hp = (RectTransform)root.transform.Find("HPBar");
            Image fill = hp.Find("Fill").GetComponent<Image>();
            Image background = hp.Find("Background").GetComponent<Image>();
            Transform frameRoot = hp.Find("Frame");
            Assert.That(frameRoot, Is.Not.Null, "The frame must be independent of the shrinking HP fill.");
            Image frame = frameRoot.GetComponent<Image>();
            AssertArtwork(fill, "HpFill.png"); AssertArtwork(background, "HpTrack.png"); AssertArtwork(frame, "HpFrame.png");
            Assert.That(frame.transform.parent, Is.SameAs(hp));
            Assert.That(frame.transform.GetSiblingIndex(), Is.GreaterThan(fill.transform.GetSiblingIndex()));
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
            Assert.That(fill.fillOrigin, Is.Zero);
            Assert.That(fill.rectTransform.anchorMin.x, Is.EqualTo(.04477612f).Within(.00001f));
            Assert.That(fill.rectTransform.anchorMax.x, Is.EqualTo(.95522388f).Within(.00001f));
            Assert.That(fill.rectTransform.anchorMin.y, Is.EqualTo(.18333333f).Within(.00001f));
            Assert.That(fill.rectTransform.anchorMax.y, Is.EqualTo(.81666667f).Within(.00001f));
            RectTransform frameRect = frame.rectTransform;
            Vector2 frameSize = frameRect.sizeDelta;
            foreach (int health in new[] { 0, 37, 80 })
            {
                hud.SetHP(health, 80);
                Assert.That(fill.fillAmount, Is.EqualTo(health / 80f).Within(.00001f));
                Assert.That(frameRect.sizeDelta, Is.EqualTo(frameSize));
                Assert.That(frame.gameObject.activeInHierarchy, Is.True);
            }
            Assert.That(hp.Find("HPText").GetSiblingIndex(), Is.GreaterThan(frameRoot.GetSiblingIndex()));
            Image block = root.transform.Find("Block").GetComponent<Image>();
            AssertArtwork(block, "Defense.png"); Assert.That(block.preserveAspect, Is.True);
            hud.SetBlock(0); Assert.That(block.gameObject.activeSelf, Is.False);
            hud.SetBlock(123); Assert.That(block.gameObject.activeSelf, Is.True);
            TMP_Text number = block.GetComponentInChildren<TMP_Text>(); number.ForceMeshUpdate();
            Assert.That(number.text, Is.EqualTo("123"));
            Assert.That(number.enableAutoSizing, Is.True);
            Assert.That(number.isTextOverflowing, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [UnityTest, Category("RequiresGraphics")] public IEnumerator BattleSceneRendersSharedArtworkForBothSidesAndReplacementEnemies()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return null;
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Player player = battle.GetPlayer();
        player.RestoreHealth(53, 80); player.AddBlock(12);
        Enemy enemy = battle.GetTarget(); enemy.RestoreHealth(25, 80); enemy.AddBlock(7);
        yield return null; Canvas.ForceUpdateCanvases();
        CheckLiveHud(player, .6625f); CheckLiveHud(enemy, .3125f);
        Capture(battle, "soldier-slime-1920x1080", 1920, 1080);
        Capture(battle, "soldier-slime-2560x1080", 2560, 1080);
        var template = AssetDatabase.LoadAssetAtPath<BattleEncounterDefinition>("Assets/MapData/NormalEncounter.asset");
        foreach (string name in new[] { "Scarecrow", "Lizard" })
        {
            BattleEncounterDefinition encounter = Object.Instantiate(template);
            try
            {
                encounter.enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/" + name + ".prefab").GetComponent<Enemy>();
                battle.ConfigureEncounter(encounter); yield return null;
                enemy = battle.GetTarget(); enemy.RestoreHealth(40, 80); enemy.AddBlock(123);
                CheckLiveHud(enemy, .5f);
                enemy.ResetBlock();
                Assert.That(enemy.GetComponent<BattleCharacterPresentation>().InfoUI.transform.Find("Block").gameObject.activeSelf, Is.False);
            }
            finally { Object.Destroy(encounter); }
        }
    }
    private static void CheckLiveHud(BattleCharacter actor, float amount)
    {
        CharacterInfoUI hud = actor.GetComponent<BattleCharacterPresentation>().InfoUI;
        AssertArtwork(hud.transform.Find("HPBar/Fill").GetComponent<Image>(), "HpFill.png");
        AssertArtwork(hud.transform.Find("HPBar/Frame").GetComponent<Image>(), "HpFrame.png");
        AssertArtwork(hud.transform.Find("Block").GetComponent<Image>(), "Defense.png");
        Assert.That(hud.transform.Find("HPBar/Fill").GetComponent<Image>().fillAmount, Is.EqualTo(amount).Within(.00001f));
        Assert.That(hud.transform.Find("Block").gameObject.activeSelf, Is.True);
    }
    private static void AssertArtwork(Image image, string file)
    {
        Assert.That(image.sprite, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(Art + file));
        Assert.That(image.raycastTarget, Is.False);
    }
    private static void Capture(BattleManager battle, string name, int width, int height)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Fail("HUD artwork validation needs rendering.");
        Canvas canvas = battle.GetTarget().GetComponentInParent<Canvas>().rootCanvas;
        RenderMode mode = canvas.renderMode; Camera oldCamera = canvas.worldCamera; float oldDistance = canvas.planeDistance;
        GameObject root = new GameObject("HUD artwork capture", typeof(Camera));
        Camera camera = root.GetComponent<Camera>(); camera.orthographic = true;
        var target = new RenderTexture(width, height, 24); var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            foreach (BattleCharacterPresentation view in Object.FindObjectsByType<BattleCharacterPresentation>(FindObjectsSortMode.None)) view.ApplyProfile(view.Profile);
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            Directory.CreateDirectory(".utmp/shared-hud-artwork");
            File.WriteAllBytes(".utmp/shared-hud-artwork/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
            camera.targetTexture = null; Object.Destroy(root); Object.Destroy(target); Object.Destroy(pixels);
        }
    }
}
