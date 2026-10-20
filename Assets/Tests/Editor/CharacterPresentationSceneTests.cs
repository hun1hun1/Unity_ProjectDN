using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class CharacterPresentationSceneTests
{
    [UnityTearDown] public IEnumerator ExitPlay()
    { if (Application.isPlaying) yield return new ExitPlayMode(); }

    [UnityTest] public IEnumerator BattlePlaybackAndEnemyReplacementKeepOwnedHudAndReadableSizes()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        Assert.That(Application.isPlaying, Is.True);
        yield return WaitForPlayTime(.4f);
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle, Is.Not.Null);
        var player = battle.GetPlayer().GetComponent<BattleCharacterPresentation>();
        var enemy = battle.GetTarget().GetComponent<BattleCharacterPresentation>();
        Assert.That(player, Is.Not.Null); Assert.That(enemy, Is.Not.Null);
        UISpriteAnimator playerAnimation = player.Visual.GetComponent<UISpriteAnimator>();
        UISpriteAnimator enemyAnimation = enemy.Visual.GetComponent<UISpriteAnimator>();
        int initialPlayer = playerAnimation.CurrentFrame, initialEnemy = enemyAnimation.CurrentFrame, initialTick = Time.frameCount;
        yield return WaitForPlayTime(.19f);
        Assert.That(Time.frameCount, Is.GreaterThan(initialTick + 1));
        Assert.That(playerAnimation.CurrentFrame, Is.Not.EqualTo(initialPlayer));
        Assert.That(enemyAnimation.CurrentFrame, Is.Not.EqualTo(initialEnemy));
        player.Visual.gameObject.SetActive(false); player.Visual.gameObject.SetActive(true);
        Assert.That(playerAnimation.CurrentFrame, Is.Zero, "Re-enabling the actual PlayMode component restarts its idle.");
        EnemyActionUI fallback = (EnemyActionUI)new SerializedObject(battle).FindProperty("enemyActionUI").objectReferenceValue;
        Assert.That(fallback.gameObject.activeSelf, Is.False);
        int health = battle.GetTarget().GetCurrentHP(); battle.GetTarget().AddBlock(7);
        foreach (string size in new[] { "Small", "Normal", "Large" })
        {
            CharacterUIProfile profile = AssetDatabase.LoadAssetAtPath<CharacterUIProfile>("Assets/CharacterPresentation/Profiles/" + size + ".asset");
            enemy.ApplyProfile(profile); yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(battle.GetTarget().GetCurrentHP(), Is.EqualTo(health));
            Assert.That(battle.GetTarget().GetBlock(), Is.EqualTo(7));
            Assert.That(enemy.InfoUI.gameObject.activeInHierarchy, Is.True);
            Assert.That(enemy.ActionUI.gameObject.activeInHierarchy, Is.True);
            AssertLayout(enemy);
        }
        enemy.ApplyProfile(AssetDatabase.LoadAssetAtPath<CharacterUIProfile>("Assets/CharacterPresentation/Profiles/Small.asset"));
        yield return null;
        Capture(battle, "soldier-slime-1920x1080", 1920, 1080);
        BattleEncounterDefinition template = AssetDatabase.LoadAssetAtPath<BattleEncounterDefinition>("Assets/MapData/NormalEncounter.asset");
        foreach (string name in new[] { "Scarecrow", "Lizard" })
        {
            BattleEncounterDefinition encounter = Object.Instantiate(template);
            try
            {
                encounter.enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/" + name + ".prefab").GetComponent<Enemy>();
                encounter.enemyMaxHP = 73;
                battle.ConfigureEncounter(encounter); yield return null; yield return WaitForPlayTime(.15f);
                enemy = battle.GetTarget().GetComponent<BattleCharacterPresentation>();
                Assert.That(battle.GetTarget().GetCurrentHP(), Is.EqualTo(73));
                Assert.That(fallback.gameObject.activeSelf, Is.False);
                Assert.That(enemy.ActionUI.transform.IsChildOf(enemy.transform), Is.True);
                AssertLayout(enemy);
                int first = enemy.Visual.GetComponent<UISpriteAnimator>().CurrentFrame;
                yield return WaitForPlayTime(.25f);
                Assert.That(enemy.Visual.GetComponent<UISpriteAnimator>().CurrentFrame, Is.Not.EqualTo(first));
                Capture(battle, name.ToLowerInvariant() + "-1920x1080", 1920, 1080);
                Capture(battle, name.ToLowerInvariant() + "-1600x1200", 1600, 1200);
            }
            finally { Object.Destroy(encounter); }
        }
    }

    private static IEnumerator WaitForPlayTime(float seconds)
    {
        // The EditMode runner does not wait on WaitForSeconds after EnterPlayMode.
        float target = Time.time + seconds;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.time < target && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Time.time, Is.GreaterThanOrEqualTo(target), "The real player loop must advance.");
    }

    private static void AssertLayout(BattleCharacterPresentation view)
    {
        Assert.That(view.Visual.pivot, Is.EqualTo(new Vector2(.5f, .0625f)));
        Assert.That(view.Visual.GetComponent<Image>().preserveAspect, Is.True);
        Assert.That(view.Visual.GetComponent<Image>().raycastTarget, Is.False);
        Assert.That(view.Visual.localScale, Is.EqualTo(Vector3.one));
        RectTransform hud = (RectTransform)view.InfoUI.transform;
        Assert.That(hud.localScale, Is.EqualTo(Vector3.one));
        Assert.That(hud.anchoredPosition.y, Is.LessThan(0));
        RectTransform intent = (RectTransform)view.ActionUI.transform;
        UISpriteAnimation clip = view.Visual.GetComponent<UISpriteAnimator>().Animation;
        float top = (clip.contentBounds.yMax - clip.groundPivot.y) * view.Visual.rect.height + view.Visual.anchoredPosition.y;
        Assert.That(intent.anchoredPosition.y, Is.GreaterThanOrEqualTo(top + view.Profile.actionGap - .01f));
        Assert.That(intent.anchoredPosition.y + intent.rect.height, Is.LessThanOrEqualTo(((RectTransform)view.transform).rect.height + .1f));
    }

    private static void Capture(BattleManager battle, string name, int width, int height)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Canvas canvas = battle.GetTarget().GetComponentInParent<Canvas>().rootCanvas;
        RenderMode mode = canvas.renderMode; Camera originalCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var root = new GameObject("Presentation capture", typeof(Camera));
        Camera camera = root.GetComponent<Camera>(); camera.orthographic = true;
        var target = new RenderTexture(width, height, 24);
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            foreach (BattleCharacterPresentation view in Object.FindObjectsByType<BattleCharacterPresentation>(FindObjectsSortMode.None))
                view.ApplyProfile(view.Profile);
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            Directory.CreateDirectory(".utmp/character-presentation");
            File.WriteAllBytes(".utmp/character-presentation/" + name + ".png", pixels.EncodeToPNG());
            AssertLayout(battle.GetTarget().GetComponent<BattleCharacterPresentation>());
        }
        finally
        {
            RenderTexture.active = previous; canvas.renderMode = mode; canvas.worldCamera = originalCamera; canvas.planeDistance = distance;
            camera.targetTexture = null; Object.Destroy(root); Object.Destroy(target); Object.Destroy(pixels);
        }
    }
}
