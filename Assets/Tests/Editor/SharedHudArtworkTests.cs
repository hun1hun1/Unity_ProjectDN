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

    [TestCase("Default")]
    [TestCase("Small")]
    [TestCase("Normal")]
    [TestCase("Large")]
    public void PartialHealthUsesSeparateArtworkLayersAndKeepsFrameAndCounterReadable(string size)
    {
        string path = size == "Default" ? "Assets/Prefabs/UI/CharacterInfoUI.prefab" :
            "Assets/Prefabs/UI/CharacterSizes/" + size + "CharacterInfoUI.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject root = Object.Instantiate(prefab);
        try
        {
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.sizeDelta = new Vector2(size == "Small" ? 288 : size == "Large" ? 360 : 320, 84);
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
            AssertHudSpacing(hud);
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
        Transform playerRow = player.GetComponent<BattleCharacterPresentation>().InfoUI.transform.Find("StatusContainer");
        Assert.That(playerRow.GetComponentInChildren<Image>(), Is.Null);
        var weakness = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/WeaknessExposure.asset");
        var rust = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/Rust.asset");
        player.StatusEffects.Apply(weakness, 2);
        enemy.StatusEffects.Apply(rust, 2);
        yield return null; Canvas.ForceUpdateCanvases();
        Image playerPanel = AssertStatusPanel(player);
        AssertStatusPanel(enemy);
        CheckLiveHud(player, .6625f); CheckLiveHud(enemy, .3125f);
        float singleIconWidth = playerPanel.rectTransform.rect.width;
        player.StatusEffects.Apply(rust, 12);
        yield return null;
        Assert.That(AssertStatusPanel(player), Is.SameAs(playerPanel));
        Assert.That(playerPanel.rectTransform.rect.width, Is.GreaterThan(singleIconWidth));
        Capture(battle, "soldier-slime-1920x1080", 1920, 1080);
        Capture(battle, "soldier-slime-2560x1080", 2560, 1080);
        player.StatusEffects.Clear();
        yield return null;
        Assert.That(playerPanel.gameObject.activeSelf, Is.False);
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
    [UnityTest, Category("RequiresGraphics")]
    public IEnumerator BlockGainFeedbackUsesTheActualAmountForBothSidesAndDelayedCards()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        var battle = Object.FindFirstObjectByType<BattleManager>();
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!battle.CanInspectCards && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(battle.CanInspectCards, Is.True);

        Player player = battle.GetPlayer();
        Enemy enemy = battle.GetTarget();
        var playerView = player.GetComponent<BattleCharacterPresentation>();
        var enemyView = enemy.GetComponent<BattleCharacterPresentation>();
        Transform Badge(BattleCharacterPresentation view) => view.InfoUI.transform.Find("Block");
        Image Shield(BattleCharacterPresentation view) => view.Visual.Find("BlockGainShield")?.GetComponent<Image>();
        TMP_Text Gain(BattleCharacterPresentation view) => Badge(view).Find("BlockGainAmount")?.GetComponent<TMP_Text>();
        void AssertGain(BattleCharacterPresentation view, string label)
        {
            Image shield = Shield(view);
            Assert.That(shield, Is.Not.Null);
            Assert.That(shield.gameObject.activeInHierarchy, Is.True);
            Assert.That(shield.sprite, Is.SameAs(Badge(view).GetComponent<Image>().sprite));
            Assert.That(shield.raycastTarget || Gain(view).raycastTarget, Is.False);
            Assert.That(Gain(view).text, Is.EqualTo(label));
        }

        player.ResetBlock();
        Assert.That(Shield(playerView), Is.Null);
        var broken = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/DefenseBreak.asset");
        player.StatusEffects.Apply(broken, 2);
        player.AddBlock(1);
        Assert.That(player.GetBlock(), Is.Zero);
        Assert.That(Shield(playerView), Is.Null);

        const string supplyPath = "Assets/CardData/Soldier/Basic/Reward_Pool/Dream/Skill/Uncommon/01.Air_Supply/Air_Supply.asset";
        var supply = AssetDatabase.LoadAssetAtPath<CardData>(supplyPath);
        var delayed = System.Array.Find(supply.effects, effect => effect is DelayEffect) as DelayEffect;
        var block = System.Array.Find(supply.effects, effect => effect is BlockEffect) as BlockEffect;
        Vector3 baseScale = Badge(playerView).localScale;
        block.Execute(battle);
        int gain = player.GetBlock();
        Assert.That(gain, Is.Positive);
        AssertGain(playerView, $"+{gain}");
        Image original = Shield(playerView);
        block.Execute(battle);
        AssertGain(playerView, $"+{gain * 2}");
        Assert.That(Shield(playerView), Is.SameAs(original));
        Assert.That(Badge(playerView).Find("BlockText").GetComponent<TMP_Text>().text, Is.EqualTo((gain * 2).ToString()));

        var enemyBlock = AssetDatabase.LoadAssetAtPath<EnemyBlockAction>("Assets/Enemy/Test/Block_Test.asset");
        enemyBlock.Execute(battle);
        AssertGain(enemyView, $"+{enemyBlock.GetBlock()}");

        yield return new WaitForSecondsRealtime(.1f);
        Capture(battle, "block-gain-1280x720", 1280, 720);
        deadline = Time.realtimeSinceStartup + 2f;
        while (original.gameObject.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(original.gameObject.activeSelf || Shield(enemyView).gameObject.activeSelf, Is.False);
        Assert.That(Gain(playerView).gameObject.activeSelf, Is.False);
        Assert.That(Badge(playerView).localScale, Is.EqualTo(baseScale));

        player.ResetBlock();
        player.StatusEffects.Clear();
        delayed.Execute(battle);
        Assert.That(original.gameObject.activeSelf, Is.False);
        battle.DelayedEffects.AdvanceTurn();
        battle.RunEffectRoutine(battle.DelayedEffects.ProcessDueEffectsRoutine(battle));
        deadline = Time.realtimeSinceStartup + 5f;
        while (player.GetBlock() == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(player.GetBlock(), Is.Positive);
        AssertGain(playerView, $"+{player.GetBlock()}");
        Assert.That(Shield(playerView), Is.SameAs(original));
    }

    private static void CheckLiveHud(BattleCharacter actor, float amount)
    {
        CharacterInfoUI hud = actor.GetComponent<BattleCharacterPresentation>().InfoUI;
        AssertArtwork(hud.transform.Find("HPBar/Fill").GetComponent<Image>(), "HpFill.png");
        AssertArtwork(hud.transform.Find("HPBar/Frame").GetComponent<Image>(), "HpFrame.png");
        AssertArtwork(hud.transform.Find("Block").GetComponent<Image>(), "Defense.png");
        Assert.That(hud.transform.Find("HPBar/Fill").GetComponent<Image>().fillAmount, Is.EqualTo(amount).Within(.00001f));
        Assert.That(hud.transform.Find("Block").gameObject.activeSelf, Is.True);
        AssertHudSpacing(hud);
    }

    private static void AssertHudSpacing(CharacterInfoUI hud)
    {
        Transform root = hud.transform;
        var hp = (RectTransform)root.Find("HPBar");
        var block = root.Find("Block").GetComponent<Image>();
        var status = (RectTransform)root.Find("StatusContainer");
        Rect box = block.rectTransform.rect;
        Rect sprite = block.sprite.rect;
        float scale = Mathf.Min(box.width / sprite.width, box.height / sprite.height);
        // Defense.png's drawn shield spans x=80..335 within the 416x294 sprite.
        float shieldRight = box.center.x + (336f - sprite.width / 2f) * scale;
        Vector2 shieldEdge = InHud(root, block.rectTransform, new Vector2(shieldRight, box.center.y));
        Vector2 barLeft = InHud(root, hp, new Vector2(hp.rect.xMin, hp.rect.center.y));
        Vector2 rowTop = InHud(root, status, new Vector2(status.rect.xMin, status.rect.yMax));
        float barBottom = InHud(root, hp, new Vector2(0, hp.rect.yMin)).y;
        Assert.That(sprite.height * scale, Is.GreaterThanOrEqualTo(40f));
        Assert.That(barLeft.x - shieldEdge.x, Is.InRange(1f, 5f));
        Assert.That(shieldEdge.y, Is.EqualTo(barLeft.y).Within(.01f));
        Assert.That(rowTop.x, Is.EqualTo(barLeft.x).Within(.01f));
        Assert.That(barBottom - rowTop.y, Is.EqualTo(6f).Within(.01f));
    }

    private static Image AssertStatusPanel(BattleCharacter actor)
    {
        Transform root = actor.GetComponent<BattleCharacterPresentation>().InfoUI.transform;
        var hp = (RectTransform)root.Find("HPBar");
        Transform row = root.Find("StatusContainer");
        Image panel = row.Find("StatusBackground")?.GetComponent<Image>();
        Assert.That(panel, Is.Not.Null, "The status icons should share one background.");
        Assert.That(panel.gameObject.activeSelf, Is.True);
        Assert.That(AssetDatabase.GetAssetPath(panel.sprite), Is.EqualTo("Assets/Images/UI/Tooltips/card-status-tooltip-frame.png"));
        Assert.That(panel.raycastTarget, Is.False);

        Vector2 barBottomLeft = InHud(root, hp, hp.rect.min);
        var panelRect = panel.rectTransform;
        Vector2 panelMin = InHud(root, panelRect, panelRect.rect.min);
        Vector2 panelMax = InHud(root, panelRect, panelRect.rect.max);
        Assert.That(panelMin.x, Is.EqualTo(barBottomLeft.x).Within(.01f));
        Assert.That(barBottomLeft.y - panelMax.y, Is.EqualTo(2f).Within(.01f));

        StatusIconUI[] icons = row.GetComponentsInChildren<StatusIconUI>();
        Assert.That(icons, Is.Not.Empty);
        foreach (StatusIconUI icon in icons)
        {
            var rect = (RectTransform)icon.transform;
            Assert.That(rect.rect.size, Is.EqualTo(new Vector2(32f, 32f)));
            Vector2 iconMin = InHud(root, rect, rect.rect.min);
            Vector2 iconMax = InHud(root, rect, rect.rect.max);
            Assert.That(iconMin.x - panelMin.x, Is.GreaterThanOrEqualTo(3.99f));
            Assert.That(iconMin.y - panelMin.y, Is.GreaterThanOrEqualTo(3.99f));
            Assert.That(panelMax.x - iconMax.x, Is.GreaterThanOrEqualTo(3.99f));
            Assert.That(panelMax.y - iconMax.y, Is.GreaterThanOrEqualTo(3.99f));
            Assert.That(panel.transform.GetSiblingIndex(), Is.LessThan(icon.transform.GetSiblingIndex()));
            TMP_Text number = icon.GetComponentInChildren<TMP_Text>();
            number.ForceMeshUpdate();
            Assert.That(number.color.grayscale, Is.GreaterThan(.85f));
            Assert.That(number.fontSharedMaterial.GetFloat("_OutlineWidth"), Is.GreaterThan(0f));
            Assert.That(number.isTextOverflowing, Is.False);
        }
        return panel;
    }

    private static Vector2 InHud(Transform root, RectTransform child, Vector2 point) =>
        root.InverseTransformPoint(child.TransformPoint(point));

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
