using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class BattleRewardSceneTests
{
    [TearDown] public void Cleanup()
    {
        RunDeckInventory.ResetForNewGame();
        typeof(MulliganSelectionUI).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,null);
        typeof(CardInspectionUI).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,null);
    }
    [Test] public void RewardCardsKeepTheirDeckCardPrefabConnection()
    {
        const string path = "Assets/Scenes/BattleScene.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            CardRewardUI ui = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (ui == null) ui = root.GetComponentInChildren<CardRewardUI>(true);
            Assert.That(ui, Is.Not.Null);
            var slots = new SerializedObject(ui).FindProperty("rewardCardSlots");
            Assert.That(slots.arraySize, Is.EqualTo(3));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DeckCard.prefab");
            for (int i = 0; i < slots.arraySize; i++)
            {
                var slot = (DeckCardUI)slots.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(slot, Is.Not.Null);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(slot),
                    Is.EqualTo(prefab.GetComponent<DeckCardUI>()), "Reward cards must inherit future DeckCard prefab changes.");
                var fields = new SerializedObject(slot);
                foreach (string name in new[] { "costImage", "dreamCostImage", "nightmareCostImage",
                    "commonImage", "uncommonImage", "rareImage" })
                {
                    Assert.That(fields.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
                    Assert.That(fields.FindProperty(name).prefabOverride, Is.False, name + " must inherit its prefab binding.");
                }
            }
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    [UnityTest] public IEnumerator EnemyDeathOpensRewardsAndOnlyConfirmAddsTheSelectedCardToRunDeck()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene"); yield return null; yield return null;
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        BattleRewardSystem flow = Object.FindFirstObjectByType<BattleRewardSystem>();
        CardRewardUI ui = Object.FindFirstObjectByType<CardRewardUI>();
        var visibility = ui.GetComponent<CanvasGroup>();
        var summary = (RectTransform)ui.transform.Find("SummaryPanel");
        Vector2 restingPosition = summary.anchoredPosition;
        Assert.That(visibility.blocksRaycasts, Is.False);
        battle.GetTarget().TakeDamage(100000);
        Assert.That(battle.Result, Is.EqualTo(BattleResult.Victory));
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.Summary));
        Assert.That(summary.gameObject.activeInHierarchy, Is.True);
        Assert.That(visibility.alpha, Is.Zero);
        Assert.That(visibility.interactable, Is.False);
        Assert.That(visibility.blocksRaycasts, Is.True);
        Assert.That(summary.anchoredPosition, Is.EqualTo(restingPosition),
            "Slime's defeat should finish before the reward entrance begins.");
        Click(summary.Find("GoldButton"));
        Click(summary.Find("CardButton"));
        Assert.That(flow.GoldAvailable, Is.True);
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.Summary));
        yield return WaitForRewardInput(ui);
        Assert.That(visibility.alpha, Is.EqualTo(1f));
        Assert.That(summary.anchoredPosition, Is.EqualTo(restingPosition));
        Capture(ui, "victory-summary.png");
        Assert.That(ui.transform.Find("SummaryPanel").GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
        Click(ui.transform.Find("SummaryPanel/GoldButton"));
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(20));
        battle.FinishBattle(BattleResult.Victory);
        Assert.That(flow.ClaimGold(), Is.False);
        Click(ui.transform.Find("SummaryPanel/CardButton"));
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.DeckChoice));
        Click(ui.transform.Find("DeckSelectPanel/DreamButton"));
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.CardChoice));
        Assert.That(ui.transform.Find("SummaryPanel").gameObject.activeInHierarchy, Is.False);
        Assert.That(ui.transform.Find("DeckSelectPanel").gameObject.activeInHierarchy, Is.False);
        int before = battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count;
        Assert.That(flow.Offers.Count, Is.EqualTo(3));
        ui.ConfirmCard();
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.CardChoice));
        Transform initialConfirm = ui.transform.Find("CardRewardPanel/ConfirmButton");
        Assert.That(initialConfirm, Is.Not.Null);
        Assert.That(initialConfirm.GetComponent<Button>().interactable, Is.False);
        Click(ui.transform.Find("CardRewardPanel/Options/RewardCard1"));
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.CardChoice), "Selecting a card must not claim the reward.");
        Assert.That(battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count, Is.EqualTo(before));
        Transform confirm = ui.transform.Find("CardRewardPanel/ConfirmButton");
        Assert.That(confirm, Is.Not.Null);
        Assert.That(confirm.GetComponent<Button>().interactable, Is.True);
        AssertSelected(ui, 0);
        var chosen = flow.Offers[1];
        Click(ui.transform.Find("CardRewardPanel/Options/RewardCard2"));
        AssertSelected(ui, 1);
        Click(ui.transform.Find("CardRewardPanel/Options/RewardCard2"));
        AssertSelected(ui, 1);
        Capture(ui, "dream-selected.png");
        Click(confirm);
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.Summary));
        Assert.That(visibility.alpha, Is.EqualTo(1f));
        Assert.That(visibility.interactable, Is.True);
        Assert.That(summary.anchoredPosition, Is.EqualTo(restingPosition));
        Assert.That(battle.GetDeckController().GetDeck(CardDeckType.TypeDream), Does.Contain(chosen));
        Click(confirm);
        Assert.That(battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count, Is.EqualTo(before + 1));
        Click(ui.transform.Find("SummaryPanel/SkipRemainingButton"));
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.Completed));
        Assert.That(ui.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTest] public IEnumerator RestoredRewardSummaryOpensWithoutAnEntrance()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        var battle = Object.FindFirstObjectByType<BattleManager>();
        var flow = Object.FindFirstObjectByType<BattleRewardSystem>();
        var ui = Object.FindFirstObjectByType<CardRewardUI>();
        var group = ui.GetComponent<CanvasGroup>();
        var summary = (RectTransform)ui.transform.Find("SummaryPanel");
        Vector2 restingPosition = summary.anchoredPosition;

        flow.RestoreCheckpoint(new RunRewardData
        {
            state = BattleRewardState.Summary,
            gold = 20,
            goldAvailable = true,
            cardAvailable = true,
            offers = System.Array.Empty<string>()
        }, RunAssetCatalog.Load());
        battle.FinishBattle(BattleResult.Victory);

        Assert.That(summary.gameObject.activeInHierarchy, Is.True);
        Assert.That(group.alpha, Is.EqualTo(1f));
        Assert.That(group.interactable, Is.True);
        Assert.That(summary.anchoredPosition, Is.EqualTo(restingPosition));
        yield return new ExitPlayMode();
    }

    [UnityTest] public IEnumerator EarlyEnemiesFinishTheirDefeatBeforeRewardsBecomeVisible()
    {
        yield return new EnterPlayMode();
        var floor = AssetDatabase.LoadAssetAtPath<FloorMapDefinition>("Assets/MapData/FirstFloor.asset");
        Assert.That(floor.normalBattles, Has.Length.EqualTo(4));
        yield return VerifyDefeatSheets(floor.normalBattles);
        yield return new ExitPlayMode();
    }

    [UnityTest] public IEnumerator LateEnemiesFinishTheirDefeatBeforeRewardsBecomeVisible()
    {
        yield return new EnterPlayMode();
        var floor = AssetDatabase.LoadAssetAtPath<FloorMapDefinition>("Assets/MapData/FirstFloor.asset");
        Assert.That(floor.lateNormalBattles, Has.Length.EqualTo(5));
        yield return VerifyDefeatSheets(floor.lateNormalBattles);
        yield return new ExitPlayMode();
    }

    [UnityTest] public IEnumerator ElitesAndBossFinishTheirDefeatBeforeRewardsBecomeVisible()
    {
        yield return new EnterPlayMode();
        var floor = AssetDatabase.LoadAssetAtPath<FloorMapDefinition>("Assets/MapData/FirstFloor.asset");
        Assert.That(floor.eliteBattles, Has.Length.EqualTo(3));
        Assert.That(floor.bossBattles, Has.Length.EqualTo(1));
        yield return VerifyDefeatSheets(floor.eliteBattles);
        yield return VerifyDefeatSheets(floor.bossBattles);
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyDefeatSheets(BattleEncounterDefinition[] encounters)
    {
        foreach (BattleEncounterDefinition encounter in encounters)
        {
            AsyncOperation loading = SceneManager.LoadSceneAsync("BattleScene");
            while (!loading.isDone) yield return null;
            var battle = Object.FindFirstObjectByType<BattleManager>();
            var rewards = Object.FindFirstObjectByType<BattleRewardSystem>();
            var ui = Object.FindFirstObjectByType<CardRewardUI>();
            Assert.That(battle, Is.Not.Null);
            Assert.That(rewards, Is.Not.Null);
            Assert.That(ui, Is.Not.Null);
            battle.ConfigureEncounter(encounter);
            var enemy = battle.GetTarget();
            var presentation = enemy.GetComponent<BattleCharacterPresentation>();
            RectTransform artwork = presentation.Visual;
            Image sprite = artwork.GetComponent<Image>();
            var animator = artwork.GetComponent<UISpriteAnimator>();
            var group = ui.GetComponent<CanvasGroup>();
            string enemyName = encounter.enemyPrefab.name;
            var defeat = AssetDatabase.LoadAssetAtPath<UISpriteAnimation>(
                $"Assets/CharacterPresentation/Animations/{enemyName}Defeat.asset");
            Assert.That(defeat, Is.Not.Null, encounter.displayName);
            Assert.That(defeat.frames, Has.Length.EqualTo(16), encounter.displayName);
            foreach (Sprite frame in defeat.frames)
                Assert.That(AssetDatabase.GetAssetPath(frame),
                    Is.EqualTo($"Assets/Images/Characters/{enemyName}/Defeat.png"), encounter.displayName);
            Canvas.ForceUpdateCanvases();
            yield return null; // Allow the spawned enemy's LateUpdate to fit the canvas.
            float idleScale = artwork.rect.width / animator.Animation.frames[0].bounds.size.x;
            Vector2 idleGroundPosition = artwork.anchoredPosition;

            enemy.TakeDamage(100000);
            Assert.That(battle.Result, Is.EqualTo(BattleResult.Victory), encounter.displayName);
            Assert.That(rewards.State, Is.EqualTo(BattleRewardState.Summary), encounter.displayName);
            Assert.That(group.alpha, Is.Zero, encounter.displayName);
            Assert.That(group.interactable, Is.False, encounter.displayName);

            float deadline = Time.realtimeSinceStartup + 3f;
            while (animator.Animation != defeat && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(group.alpha, Is.Zero, encounter.displayName);
                yield return null;
            }
            Assert.That(animator.Animation, Is.SameAs(defeat), encounter.displayName);

            // Capture temporarily changes the canvas dimensions, so check the scale first.
            Assert.That(artwork.rect.width / defeat.frames[0].bounds.size.x,
                Is.EqualTo(idleScale).Within(.01f), encounter.displayName);

            bool capturedPose = false;
            while (!animator.IsComplete && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(group.alpha, Is.Zero, encounter.displayName);
                if (!capturedPose && animator.Animation == defeat && animator.CurrentFrame >= 8)
                {
                    Capture(ui, $"defeat-{enemyName}.png");
                    capturedPose = true;
                }
                yield return null;
            }
            Assert.That(animator.Animation, Is.SameAs(defeat), encounter.displayName);
            Assert.That(animator.IsComplete, Is.True, encounter.displayName);
            Assert.That(animator.Loop, Is.False, encounter.displayName);
            Assert.That(sprite.sprite, Is.SameAs(defeat.frames[defeat.frames.Length - 1]), encounter.displayName);
            Assert.That(artwork.anchoredPosition, Is.EqualTo(idleGroundPosition), encounter.displayName);
            Assert.That(presentation.InfoUI.isActiveAndEnabled, Is.False, encounter.displayName);
            yield return WaitForRewardInput(ui);
        }
    }
    [UnityTest] public IEnumerator PlayerDeathDoesNotOpenRewards()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene"); yield return null; yield return null;
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        battle.GetPlayer().TakeDamage(100000); yield return null;
        Assert.That(battle.Result, Is.EqualTo(BattleResult.Defeat));
        Assert.That(Object.FindFirstObjectByType<BattleRewardSystem>().State, Is.EqualTo(BattleRewardState.Closed));
        Assert.That(Object.FindFirstObjectByType<CardRewardUI>().GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator NightmareChoiceResetsSelectionWhenReopenedAndConfirmsOnlyOnce()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene"); yield return null; yield return null;
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        BattleRewardSystem flow = Object.FindFirstObjectByType<BattleRewardSystem>();
        CardRewardUI ui = Object.FindFirstObjectByType<CardRewardUI>();
        DeckController deck = battle.GetDeckController();
        CardData first = Object.Instantiate(deck.GetDeck(CardDeckType.TypeNightmare)[0]);
        CardData second = Object.Instantiate(first);
        CardDatabase database = ScriptableObject.CreateInstance<CardDatabase>();
        var catalog = ScriptableObject.CreateInstance<CardRewardPoolCatalog>();
        try
        {
            typeof(CardDatabase).GetField("cards", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(database, new System.Collections.Generic.List<CardData> { first, second });
            var catalogFields = new SerializedObject(catalog);
            var pools = catalogFields.FindProperty("pools");
            pools.arraySize = 1;
            var pool = pools.GetArrayElementAtIndex(0);
            pool.FindPropertyRelative("deckType").intValue = (int)CardDeckType.TypeNightmare;
            pool.FindPropertyRelative("type").intValue = (int)first.type;
            pool.FindPropertyRelative("rarity").intValue = (int)first.rarity;
            pool.FindPropertyRelative("weight").intValue = 1;
            pool.FindPropertyRelative("database").objectReferenceValue = database;
            catalogFields.ApplyModifiedPropertiesWithoutUndo();
            var samplerFields = new SerializedObject(Object.FindFirstObjectByType<CardRewardSystem>());
            samplerFields.FindProperty("poolCatalog").objectReferenceValue = catalog;
            samplerFields.ApplyModifiedPropertiesWithoutUndo();
            battle.FinishBattle(BattleResult.Victory);
            var visibility = ui.GetComponent<CanvasGroup>();
            Assert.That(visibility.alpha, Is.Zero);
            Assert.That(((RectTransform)ui.transform.Find("SummaryPanel")).anchoredPosition.y, Is.LessThan(0f));
            yield return WaitForRewardInput(ui);
            Click(ui.transform.Find("SummaryPanel/CardButton"));
            Click(ui.transform.Find("DeckSelectPanel/NightmareButton"));
            Assert.That(flow.Offers.Count, Is.EqualTo(2));
            Assert.That(ui.transform.Find("CardRewardPanel/Options/RewardCard3").gameObject.activeSelf, Is.False);
            int before = deck.GetDeck(CardDeckType.TypeNightmare).Count;
            ui.ConfirmCard(); ui.SelectCard(-1); ui.SelectCard(99);
            AssertSelected(ui, -1);
            Assert.That(deck.GetDeck(CardDeckType.TypeNightmare).Count, Is.EqualTo(before));
            ui.SelectCard(0); AssertSelected(ui, 0);
            ui.gameObject.SetActive(false); ui.gameObject.SetActive(true);
            AssertSelected(ui, -1);
            var confirm = ui.transform.Find("CardRewardPanel/ConfirmButton").GetComponent<Button>();
            Assert.That(confirm.interactable, Is.False);
            ui.SelectCard(1); AssertSelected(ui, 1);
            var root = (RectTransform)ui.transform.Find("CardRewardPanel/Options/RewardCard2");
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            UnityEngine.EventSystems.ExecuteEvents.Execute(root.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(root.Find("HoverVisual").localScale.x, Is.GreaterThanOrEqualTo(1.3f - .001f));
            Canvas.ForceUpdateCanvases();
            var cardCorners = new Vector3[4]; var buttonCorners = new Vector3[4];
            ((RectTransform)root.Find("HoverVisual")).GetWorldCorners(cardCorners);
            ((RectTransform)confirm.transform).GetWorldCorners(buttonCorners);
            Assert.That(buttonCorners[1].y, Is.LessThan(cardCorners[0].y), "Confirm must sit below even the enlarged card.");
            Capture(ui, "nightmare-selected-hover.png");
            CardData chosen = flow.Offers[1];
            confirm.onClick.Invoke(); confirm.onClick.Invoke();
            Assert.That(flow.State, Is.EqualTo(BattleRewardState.Summary));
            Assert.That(deck.GetDeck(CardDeckType.TypeNightmare).Count, Is.EqualTo(before + 1));
            Assert.That(deck.GetDeck(CardDeckType.TypeNightmare), Does.Contain(chosen));
            AssertSelected(ui, -1);
            Assert.That(confirm.interactable, Is.False);
        }
        finally { Object.Destroy(first); Object.Destroy(second); Object.Destroy(database); Object.Destroy(catalog); }
        yield return new ExitPlayMode();
    }

    private static IEnumerator WaitForRewardInput(CardRewardUI ui)
    {
        var group = ui.GetComponent<CanvasGroup>();
        float deadline = Time.realtimeSinceStartup + 2f;
        while (!group.interactable && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(group.interactable, Is.True);
    }

    private static void Capture(CardRewardUI ui, string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Canvas canvas = ui.GetComponent<Canvas>().rootCanvas;
        RenderMode mode = canvas.renderMode; Camera originalCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var cameraObject = new GameObject("Reward capture camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        var target = new RenderTexture(1280, 720, 24); camera.targetTexture = target;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            string folder = System.IO.Path.Combine(Application.dataPath, "../.utmp/reward-confirm");
            System.IO.Directory.CreateDirectory(folder); System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name), pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; canvas.renderMode = mode; canvas.worldCamera = originalCamera; canvas.planeDistance = distance;
            cameraObject.SetActive(false); camera.targetTexture = null;
            Object.Destroy(cameraObject); Object.Destroy(target); Object.Destroy(pixels);
        }
    }

    private static void AssertSelected(CardRewardUI ui, int selected)
    {
        Transform row = ui.transform.Find("CardRewardPanel/Options");
        for (int i = 0; i < 3; i++)
        {
            Transform border = row.GetChild(i).Find("HoverVisual/RewardSelectionBorder");
            Assert.That(border, Is.Not.Null);
            Assert.That(border.gameObject.activeSelf, Is.EqualTo(i == selected));
            Assert.That(border.GetComponentsInChildren<Image>(true), Has.Length.EqualTo(4));
            foreach (Image edge in border.GetComponentsInChildren<Image>(true))
            {
                Assert.That(edge.color.g, Is.GreaterThan(.9f));
                Assert.That(edge.color.r, Is.LessThan(.3f));
                Assert.That(edge.raycastTarget, Is.False);
            }
        }
    }
    private static void Click(Transform button) { Assert.That(button, Is.Not.Null); button.GetComponent<Button>().onClick.Invoke(); }
}
