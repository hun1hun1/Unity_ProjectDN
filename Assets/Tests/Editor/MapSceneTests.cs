using System.Collections;
using System.Collections.Generic;
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
using Object = UnityEngine.Object;

public class MapSceneTests
{
    [System.Serializable]
    private sealed class SavedShopStock
    {
        public string[] dream;
        public string[] nightmare;
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (RunSession.Instance != null)
        {
            if (Application.isPlaying) Object.Destroy(RunSession.Instance.gameObject);
            else Object.DestroyImmediate(RunSession.Instance.gameObject);
        }
        if (Application.isPlaying) { yield return null; yield return new ExitPlayMode(); }
        RunDeckInventory.ResetForNewGame(); RunRelicInventory.ResetForNewGame();
    }

    [Test] public void SerializedTemporaryRunOffersShopAndRestAlongsideBattles()
    {
        RunDefinition definition = AssetDatabase.LoadAssetAtPath<RunDefinition>(MapAssetSetup.RunPath);
        Assert.That(definition, Is.Not.Null);
        var state = new RunProgress(definition, 12);
        Assert.That(state.Nodes.Count, Is.EqualTo(5));
        Assert.That(state.Nodes.Select(n => n.Choices.Count), Is.EqualTo(new[] { 1, 1, 2, 2, 1 }));
        Assert.That(state.Nodes.All(n => n.Choices[0].Type == MapContentType.NormalBattle), Is.True);
        Assert.That(state.Nodes[2].Choices[1].Type, Is.EqualTo(MapContentType.Shop));
        Assert.That(state.Nodes[3].Choices[1].Type, Is.EqualTo(MapContentType.Rest));
        var battlesOnly = new RunProgress(definition, 12, new[] { MapContentType.NormalBattle });
        Assert.That(battlesOnly.Nodes.All(n => n.Choices.Count == 1), Is.True);
        Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo("Assets/Scenes/TitleScene.unity"));
        Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == MapAssetSetup.MapScenePath), Is.True);
        Assert.That(EditorBuildSettings.scenes[0].enabled, Is.True);
        Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/BattleScene.unity"), Is.True);
    }

    [UnityTest] public IEnumerator ShopPanelResumesAndLeavesOnlyAfterSuccessfulSave()
    {
        yield return new EnterPlayMode();
        string savePath = System.IO.Path.GetFullPath(".utmp/run-save/shop-" + System.Guid.NewGuid().ToString("N") + ".json");
        RunSaves.UseStore(new RunSaveStore(savePath));
        try
        {
            yield return SceneManager.LoadSceneAsync("TitleScene");
            yield return WaitForScene("TitleScene");
            var title = Object.FindFirstObjectByType<TitleMenuUI>();
            title.StartGame();
            var selection = Object.FindFirstObjectByType<CharacterSelectionUI>();
            selection.GetComponentInChildren<Toggle>().isOn = true;
            title.ConfirmAdventure();
            yield return WaitForMap();
            for (int i = 0; i < 2; i++)
            {
                Object.FindFirstObjectByType<MapController>().SelectOption(0);
                yield return WaitForScene("BattleScene");
                Object.FindFirstObjectByType<BattleManager>().GetTarget().TakeDamage(100000);
                Assert.That(Object.FindFirstObjectByType<BattleRewardSystem>().SkipRemaining(), Is.True);
                yield return WaitForMap();
            }

            var session = RunSession.Instance;
            ClickUtilityButton(GameObject.Find("Choice2").GetComponent<Button>());
            Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Content));
            var panel = GameObject.Find("ShopPanel").transform;
            Assert.That(panel.Find("Merchant").GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(panel.Find("Display").GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<MapController>().CanChoose(0), Is.False);
            var toolbar = FindMapCanvas().transform.Find("MapUtilityButtons");
            var wallet = (RectTransform)toolbar.Find("Wallet");
            var deckButton = toolbar.Find("DeckButton").GetComponent<Button>();
            var optionsButton = toolbar.Find("OptionsButton").GetComponent<Button>();
            Assert.That(wallet, Is.Not.Null);
            Assert.That(wallet.gameObject.activeInHierarchy, Is.True);
            Assert.That(wallet.GetComponentInChildren<TMP_Text>().text, Is.EqualTo($"보유 골드 {RunGoldInventory.Gold}"));
            var walletCorners = new Vector3[4];
            var deckCorners = new Vector3[4];
            wallet.GetWorldCorners(walletCorners);
            ((RectTransform)deckButton.transform).GetWorldCorners(deckCorners);
            Assert.That(walletCorners[2].x, Is.LessThan(deckCorners[0].x));
            Assert.That(wallet.InverseTransformPoint(deckButton.transform.position).y,
                Is.EqualTo(wallet.rect.center.y).Within(1f));
            var display = panel.Find("Display");
            Assert.That(display.Find("DreamSlots").GetComponentsInChildren<DeckCardUI>(), Has.Length.EqualTo(3));
            Assert.That(display.Find("NightmareSlots").GetComponentsInChildren<DeckCardUI>(), Is.Empty);
            Assert.That(display.Find("NightmareStockStatus").GetComponent<TMP_Text>().text, Is.EqualTo("입고 예정"));
            Assert.That(display.Find("NightmareStockStatus").gameObject.activeInHierarchy, Is.True);
            var shop = Object.FindFirstObjectByType<ShopHandler>();
            string stock = shop.CaptureState();
            Assert.That(session.SavedContentState, Is.EqualTo(stock).And.Not.Empty);
            CaptureMap("map-shop-stock.png");
            yield return null;

            var shopLeave = panel.Find("LeaveButton").GetComponent<Button>();
            var stockCard = display.Find("DreamSlots").GetComponentInChildren<DeckCardUI>();
            var inspection = Object.FindFirstObjectByType<CardInspectionUI>();
            ClickPointer(stockCard.gameObject, PointerEventData.InputButton.Right);
            Assert.That(inspection.IsOpen, Is.False);
            ClickPointer(stockCard.gameObject, PointerEventData.InputButton.Left);
            Assert.That(inspection.IsOpen, Is.True);
            var inspected = (DeckCardUI)new SerializedObject(inspection).FindProperty("cardView").objectReferenceValue;
            Assert.That(inspected.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Description").text,
                Is.EqualTo(stockCard.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Description").text));
            Assert.That(shopLeave.interactable, Is.False);
            CaptureMap("map-shop-inspection.png");
            inspection.Close();
            yield return null;
            yield return null;
            Assert.That(inspection.IsOpen, Is.False);
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            Assert.That(shopLeave.interactable, Is.True);
            Assert.That(shop.CaptureState(), Is.EqualTo(stock));

            var deck = Object.FindFirstObjectByType<DeckOverviewUI>();
            ClickUtilityButton(deckButton);
            Assert.That(deck.IsOpen, Is.True);
            shopLeave.onClick.Invoke();
            Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Content));
            var deckPanel = FindMapCanvas().transform.Find("DeckOverviewPanel");
            yield return null;
            ClickUtilityButton(deckPanel.Find("CloseButton").GetComponent<Button>());
            Assert.That(deck.IsOpen, Is.False);
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            yield return null;

            var settings = Object.FindFirstObjectByType<SettingsUI>();
            ClickUtilityButton(optionsButton);
            Assert.That(settings.IsOpen, Is.True);
            shopLeave.onClick.Invoke();
            Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Content));
            var settingsData = new SerializedObject(settings);
            yield return null;
            ClickUtilityButton((Button)settingsData.FindProperty("closeButton").objectReferenceValue);
            Assert.That(settings.IsOpen, Is.False);
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            yield return null;

            Assert.That(session.ReturnToTitle(), Is.True);
            yield return WaitForScene("TitleScene");
            Assert.That(RunSaves.TryLoad(out RunSaveData shopCheckpoint, out _), Is.True);
            var savedStock = JsonUtility.FromJson<SavedShopStock>(stock);
            System.Array.Reverse(savedStock.dream, 0, 2);
            shopCheckpoint.contentState = JsonUtility.ToJson(savedStock);
            Assert.That(shopCheckpoint.contentState, Is.Not.EqualTo(stock));
            Assert.That(RunSaves.Store.TryWrite(shopCheckpoint, out _), Is.True);
            Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
            yield return WaitForMap();

            panel = GameObject.Find("ShopPanel").transform;
            Assert.That(Object.FindFirstObjectByType<ShopHandler>().CaptureState(), Is.EqualTo(shopCheckpoint.contentState));
            var restoredCards = panel.Find("Display/DreamSlots").GetComponentsInChildren<DeckCardUI>();
            var catalog = RunAssetCatalog.Load();
            Assert.That(restoredCards.Select(card => ((TMP_Text)new SerializedObject(card).FindProperty("nameText")
                .objectReferenceValue).text), Is.EqualTo(savedStock.dream.Select(id => catalog.Resolve<CardData>(id).cardName)));
            var leave = panel.Find("LeaveButton").GetComponent<Button>();
            wallet = (RectTransform)FindMapCanvas().transform.Find("MapUtilityButtons/Wallet");
            Assert.That(wallet.gameObject.activeInHierarchy, Is.True);
            Assert.That(session.Progress.NodeIndex, Is.EqualTo(2));
            using (var locked = new System.IO.FileStream(savePath, System.IO.FileMode.Open,
                System.IO.FileAccess.Read, System.IO.FileShare.None))
            {
                ClickUtilityButton(leave);
                Assert.That(panel.gameObject.activeInHierarchy, Is.True);
                Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Content));
            }
            ClickUtilityButton(leave);
            Assert.That(panel.gameObject.activeInHierarchy, Is.False);
            Assert.That(wallet.gameObject.activeInHierarchy, Is.False);
            Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Map));
            Assert.That(session.Progress.NodeIndex, Is.EqualTo(3));
            Assert.That(RunSaves.TryLoad(out RunSaveData saved, out _), Is.True);
            Assert.That(saved.progress.node, Is.EqualTo(3));
        }
        finally { RunSaves.UseStore(null); }
        yield return new ExitPlayMode();
    }

    [UnityTest] public IEnumerator RestRecoveryHealsMissingHealthAndAdvancesOnlyAfterSaving()
    {
        yield return new EnterPlayMode();
        string savePath = System.IO.Path.GetFullPath(".utmp/run-save/rest-" + System.Guid.NewGuid().ToString("N") + ".json");
        RunSaves.UseStore(new RunSaveStore(savePath));
        try
        {
            yield return SceneManager.LoadSceneAsync("TitleScene");
            yield return WaitForScene("TitleScene");
            var title = Object.FindFirstObjectByType<TitleMenuUI>();
            title.StartGame();
            Object.FindFirstObjectByType<CharacterSelectionUI>().GetComponentInChildren<Toggle>().isOn = true;
            title.ConfirmAdventure();
            yield return WaitForMap();
            for (int i = 0; i < 3; i++)
            {
                Object.FindFirstObjectByType<MapController>().SelectOption(0);
                yield return WaitForScene("BattleScene");
                var battle = Object.FindFirstObjectByType<BattleManager>();
                battle.GetPlayer().TakeDamage(7);
                battle.GetTarget().TakeDamage(100000);
                Assert.That(Object.FindFirstObjectByType<BattleRewardSystem>().SkipRemaining(), Is.True);
                yield return WaitForMap();
            }

            var session = RunSession.Instance;
            Assert.That(session.Progress.CurrentHP, Is.EqualTo(59));
            Assert.That(Object.FindFirstObjectByType<MapController>().CanChoose(1), Is.True, "The fourth node offers rest.");
            ClickUtilityButton(GameObject.Find("Choice2").GetComponent<Button>());
            Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Content));
            Assert.That(session.ReturnToTitle(), Is.True);
            yield return WaitForScene("TitleScene");
            Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
            yield return WaitForMap();

            var panel = GameObject.Find("RestPanel");
            Assert.That(panel, Is.Not.Null);
            var recover = panel.GetComponentsInChildren<Button>().Single(button => button.name == "RecoverButton");
            Assert.That(panel.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Health").text,
                Is.EqualTo("HP 59 / 80"));
            CaptureMap("map-rest-recovery.png");
            using (var locked = new System.IO.FileStream(savePath, System.IO.FileMode.Open,
                System.IO.FileAccess.Read, System.IO.FileShare.None))
            {
                ClickUtilityButton(recover);
                Assert.That(panel.activeInHierarchy, Is.True);
                Assert.That(session.Progress.CurrentHP, Is.EqualTo(59));
                Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Content));
            }

            ClickUtilityButton(recover);
            recover.onClick.Invoke();
            Assert.That(panel.activeInHierarchy, Is.False);
            Assert.That(session.Progress.CurrentHP, Is.EqualTo(76));
            Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Map));
            Assert.That(session.Progress.NodeIndex, Is.EqualTo(4));
            Assert.That(RunSaves.TryLoad(out RunSaveData saved, out _), Is.True);
            Assert.That(saved.progress.hp, Is.EqualTo(76));
            Assert.That(saved.progress.node, Is.EqualTo(4));

            Object.FindFirstObjectByType<MapController>().SelectOption(0);
            yield return WaitForScene("BattleScene");
            Assert.That(Object.FindFirstObjectByType<BattleManager>().GetPlayer().GetCurrentHP(), Is.EqualTo(76));
        }
        finally { RunSaves.UseStore(null); }
        yield return new ExitPlayMode();
    }

    [Test] public void SavedMapSceneKeepsItsRunUIAndInputReferences()
    {
        Scene scene = SceneManager.GetSceneByPath(MapAssetSetup.MapScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(MapAssetSetup.MapScenePath, OpenSceneMode.Additive);
        try
        {
            MapController map = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapController>(true)).Single();
            Assert.That(map.Definition, Is.SameAs(AssetDatabase.LoadAssetAtPath<RunDefinition>(MapAssetSetup.RunPath)));
            var config = new SerializedObject(map);
            foreach (string field in new[] { "heading", "progressText", "resourcesText", "statusText", "nodeTemplate", "nodeContainer" })
                Assert.That(config.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            var buttons = config.FindProperty("choiceButtons"); Assert.That(buttons.arraySize, Is.EqualTo(3));
            for (int i = 0; i < buttons.arraySize; i++)
            {
                Button button = (Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(button, Is.Not.Null); Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(map));
            }
            Assert.That(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(true)).Single(), Is.Not.Null);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest] public IEnumerator FiveRealBattleRoundTripsPreserveHealthRewardsAndStop()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MapScene"); yield return WaitForMap();
        CaptureMap("map-start.png");
        RunSession session = RunSession.Instance;
        RunProgress progress = session.Progress;
        var finished = new FinishCounter(RunPhase.Completed); session.RunFinished += finished.OnFinished;
        AmmoCapacityRelic relic = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/CardData/Soldier/Examples/AmmoCapacityRelic.asset");
        Assert.That(relic.Acquire(), Is.True);
        int initialDeckCount = -1;
        int expectedTotalGold = progress.Definition.startingGold;
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(expectedTotalGold));
        for (int i = 0; i < 5; i++)
        {
            MapController map = Object.FindFirstObjectByType<MapController>();
            Assert.That(map.CanChoose(0), Is.True); Assert.That(map.CanChoose(1), Is.EqualTo(i == 2 || i == 3));
            var button = GameObject.Find("Choice1").GetComponent<Button>();
            Assert.That(button.interactable, Is.True);
            button.onClick.Invoke(); button.onClick.Invoke();
            Assert.That(progress.Phase, Is.EqualTo(RunPhase.Battle));
            yield return WaitForScene("BattleScene");
            BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
            Assert.That(Object.FindFirstObjectByType<RunBattleBridge>(), Is.Not.Null);
            Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(80 - i * 7));
            Assert.That(battle.GetPlayer().GetAmmoSystem().RunMaxAmmoBonus, Is.EqualTo(relic.MaxAmmoBonus));
            if (initialDeckCount < 0) initialDeckCount = battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count;
            Assert.That(battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count, Is.EqualTo(initialDeckCount + (i > 0 ? 1 : 0)));
            battle.GetPlayer().TakeDamage(7);
            battle.GetTarget().TakeDamage(100000);
            var rewards = Object.FindFirstObjectByType<BattleRewardSystem>();
            Assert.That(progress.Phase, Is.EqualTo(RunPhase.Rewards));
            Assert.That(progress.CompletedNodes, Is.EqualTo(i));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("BattleScene"));
            Assert.That(session.SelectOption(0), Is.False);
            int nodeBonus = i + 1;
            int goldReward = rewards.GoldReward;
            Assert.That(goldReward, Is.InRange(20 + nodeBonus, 25 + nodeBonus), $"Node {nodeBonus} gold reward");
            Assert.That(rewards.ClaimGold(), Is.True);
            expectedTotalGold += goldReward;
            if (i == 0)
            {
                Assert.That(rewards.OpenCardReward(), Is.True);
                Assert.That(rewards.SelectDeck(CardDeckType.TypeDream), Is.True);
                Assert.That(rewards.SkipRemaining(), Is.False);
                Assert.That(rewards.ChooseCard(0), Is.True);
            }
            Assert.That(rewards.SkipRemaining(), Is.True);
            Assert.That(rewards.SkipRemaining(), Is.False);
            yield return WaitForMap();
            Assert.That(session.Progress, Is.SameAs(progress));
            Assert.That(progress.CurrentHP, Is.EqualTo(80 - (i + 1) * 7));
            Assert.That(RunGoldInventory.Gold, Is.EqualTo(expectedTotalGold));
            Assert.That(RunRelicInventory.Contains(relic), Is.True);
            Assert.That(progress.CompletedNodes, Is.EqualTo(i + 1));
        }
        Assert.That(progress.Phase, Is.EqualTo(RunPhase.Completed)); Assert.That(finished.Count, Is.EqualTo(1));
        Assert.That(Object.FindFirstObjectByType<MapController>().CanChoose(0), Is.False);
        Assert.That(GameObject.Find("Heading").GetComponent<TMP_Text>().text, Is.EqualTo("런 완료"));
        Assert.That(GameObject.Find("Choices").GetComponentsInChildren<Button>().Any(b => b.interactable), Is.False);
        Assert.That(Object.FindFirstObjectByType<BattleManager>(), Is.Null);
        Assert.That(session.SelectOption(0), Is.False);
        CaptureMap("map-completed.png");
    }

    [UnityTest] public IEnumerator DefeatReturnsToTerminalMapWithoutAdvancingAndNewRunResetsAllInventories()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MapScene"); yield return WaitForMap();
        RunSession session = RunSession.Instance; RunDefinition definition = session.Progress.Definition;
        var finished = new FinishCounter(RunPhase.Defeated); session.RunFinished += finished.OnFinished;
        RunGoldInventory.Add(123);
        AmmoCapacityRelic relic = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/CardData/Soldier/Examples/AmmoCapacityRelic.asset");
        relic.Acquire(); session.SelectOption(0); yield return WaitForScene("BattleScene");
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardData extra = battle.GetDeckController().GetDeck(CardDeckType.TypeDream)[0];
        int initialDeck = battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count;
        battle.GetDeckController().AddCardToDeck(extra, CardDeckType.TypeDream);
        battle.GetPlayer().TakeDamage(100000);
        Assert.That(Object.FindFirstObjectByType<BattleRewardSystem>().State, Is.EqualTo(BattleRewardState.Closed));
        yield return WaitForMap();
        Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Defeated));
        Assert.That(session.Progress.CompletedNodes, Is.Zero); Assert.That(session.Progress.CurrentHP, Is.Zero);
        Assert.That(finished.Count, Is.EqualTo(1));
        Assert.That(Object.FindFirstObjectByType<MapController>().CanChoose(0), Is.False);
        RunProgress old = session.Progress;
        session.StartNewRun(definition, 81);
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(definition.startingGold));
        Assert.That(RunRelicInventory.Count, Is.Zero);
        Assert.That(session.Progress.CurrentHP, Is.EqualTo(80));
        Assert.That(session.CompleteContent(old, 0, 0), Is.False);
        session.SelectOption(0); yield return WaitForScene("BattleScene");
        Assert.That(Object.FindFirstObjectByType<BattleManager>().GetDeckController().GetDeck(CardDeckType.TypeDream).Count, Is.EqualTo(initialDeck));
    }

    [UnityTest] public IEnumerator CancelledBattleDoesNotGrantCompletionOrRewards()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MapScene"); yield return WaitForMap();
        RunSession.Instance.SelectOption(0); yield return WaitForScene("BattleScene");
        Object.FindFirstObjectByType<BattleManager>().EndBattle();
        Assert.That(Object.FindFirstObjectByType<BattleRewardSystem>().State, Is.EqualTo(BattleRewardState.Closed));
        yield return WaitForMap();
        Assert.That(RunSession.Instance.Progress.Phase, Is.EqualTo(RunPhase.Cancelled));
        Assert.That(RunSession.Instance.Progress.CompletedNodes, Is.Zero);
    }

    [UnityTest] public IEnumerator DirectBattleSceneStillStartsWithoutAnActiveRun()
    {
        yield return new EnterPlayMode();
        if (RunSession.Instance != null) Object.Destroy(RunSession.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("BattleScene"); yield return WaitForScene("BattleScene");
        Assert.That(RunSession.Instance, Is.Null);
        var topArea = GameObject.Find("TopArea").transform;
        Assert.That(topArea.Find("RunInfo/FloorNode").GetComponent<TMP_Text>().text, Is.EqualTo("—"));
        Assert.That(topArea.Find("RunInfo/GoldValue").GetComponent<TMP_Text>().text, Is.EqualTo("0"));
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(battle.GetPlayer().GetMaxHP()));
        battle.GetTarget().TakeDamage(100000);
        var rewards = Object.FindFirstObjectByType<BattleRewardSystem>();
        Assert.That(rewards.State, Is.EqualTo(BattleRewardState.Summary));
        rewards.SkipRemaining(); yield return null; yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("BattleScene"));
    }


    [UnityTest]
    public IEnumerator UtilityButtonsOpenRunDeckAndSettingsAndBlockMapChoices()
    {
        yield return new EnterPlayMode();
        var savePath = System.IO.Path.Combine(".utmp", "map-utility", System.Guid.NewGuid() + ".json");
        RunSaves.UseStore(new RunSaveStore(System.IO.Path.GetFullPath(savePath)));
        try
        {
            yield return SceneManager.LoadSceneAsync("MapScene");
            yield return WaitForMap();
            var map = Object.FindFirstObjectByType<MapController>();
            var canvas = FindMapCanvas();
            var toolbar = canvas.transform.Find("MapUtilityButtons");
            Assert.That(toolbar, Is.Not.Null);
            Assert.That(toolbar.GetComponent<Image>().sprite.name, Is.EqualTo("map-utility-buttons-backplate"));

            var deckButton = toolbar.Find("DeckButton").GetComponent<Button>();
            var deck = Object.FindFirstObjectByType<DeckOverviewUI>();
            var settings = Object.FindFirstObjectByType<SettingsUI>();
            Assert.That(deck.IsOpen || settings.IsOpen, Is.False);

            ClickUtilityButton(deckButton);
            yield return null;
            Assert.That(deck.IsOpen, Is.True);
            var lists = canvas.transform.Find("DeckOverviewPanel").GetComponentsInChildren<DeckListUI>();
            Assert.That(lists.Length, Is.EqualTo(2));
            Assert.That(lists.All(list => list.GetComponentsInChildren<DeckCardUI>().Length > 0), Is.True);
            var inspection = Object.FindFirstObjectByType<CardInspectionUI>();
            var card = lists[0].GetComponentInChildren<DeckCardUI>();
            card.OnPointerClick(new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Right });
            Assert.That(inspection.IsOpen, Is.True);
            inspection.Close();
            yield return null;
            yield return null;
            map.SelectOption(0);
            Assert.That(RunSession.Instance.Progress.Phase, Is.EqualTo(RunPhase.Map));

            int dreamCount = canvas.transform.Find("DeckOverviewPanel/DreamSection")
                .GetComponentsInChildren<DeckCardUI>().Length;
            canvas.transform.Find("DeckOverviewPanel/CloseButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(deck.IsOpen, Is.False);
            yield return null;

            map.SelectOption(0);
            yield return WaitForScene("BattleScene");
            Object.FindFirstObjectByType<BattleManager>().GetTarget().TakeDamage(100000);
            var rewards = Object.FindFirstObjectByType<BattleRewardSystem>();
            Assert.That(rewards.OpenCardReward(), Is.True);
            Assert.That(rewards.SelectDeck(CardDeckType.TypeDream), Is.True);
            Assert.That(rewards.ChooseCard(0), Is.True);
            Assert.That(rewards.SkipRemaining(), Is.True);
            yield return WaitForMap();

            map = Object.FindFirstObjectByType<MapController>();
            canvas = FindMapCanvas();
            toolbar = canvas.transform.Find("MapUtilityButtons");
            deck = Object.FindFirstObjectByType<DeckOverviewUI>();
            settings = Object.FindFirstObjectByType<SettingsUI>();
            ClickUtilityButton(toolbar.Find("DeckButton").GetComponent<Button>());
            Assert.That(canvas.transform.Find("DeckOverviewPanel/DreamSection")
                .GetComponentsInChildren<DeckCardUI>().Length, Is.EqualTo(dreamCount + 1));
            deck.Close();
            yield return null;

            ClickUtilityButton(toolbar.Find("OptionsButton").GetComponent<Button>());
            yield return null;
            Assert.That(settings.IsOpen, Is.True);
            map.SelectOption(0);
            Assert.That(RunSession.Instance.Progress.Phase, Is.EqualTo(RunPhase.Map));

            canvas.transform.Find("SettingsPanel/MainContent/Footer/QuitButton").GetComponent<Button>().onClick.Invoke();
            canvas.transform.Find("SettingsPanel/QuitConfirmation/Dialog/ConfirmButton").GetComponent<Button>().onClick.Invoke();
            yield return WaitForScene("TitleScene");
            Assert.That(RunSaves.TryLoad(out RunSaveData saved, out _), Is.True);
            Assert.That(saved.progress.phase, Is.EqualTo(RunPhase.Map));
            Assert.That(saved.progress.node, Is.EqualTo(1));
        }
        finally { RunSaves.UseStore(null); }
        yield return new ExitPlayMode();
    }

    [UnityTest] public IEnumerator BattleTopAreaTracksCurrentNodeAndClaimedGold()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MapScene");
        yield return WaitForMap();
        int startingGold = RunGoldInventory.Gold;

        Object.FindFirstObjectByType<MapController>().SelectOption(0);
        yield return WaitForScene("BattleScene");
        Transform hud = GameObject.Find("TopArea").transform.Find("RunInfo");
        Assert.That(hud, Is.Not.Null);
        var symbol = hud.Find("SoldierSymbol").GetComponent<Image>();
        Assert.That(symbol.sprite.name, Is.EqualTo("soldier-toparea-symbol"));
        Assert.That(symbol.raycastTarget, Is.False);
        var coin = hud.Find("GoldCoin").GetComponent<Image>().sprite;
        Assert.That(AssetDatabase.GetAssetPath(coin), Is.EqualTo("Assets/Images/UI/MapScene/gold-coin.png"));
        var node = hud.Find("FloorNode").GetComponent<TMP_Text>();
        var gold = hud.Find("GoldValue").GetComponent<TMP_Text>();
        Assert.That(node.text, Is.EqualTo("1층 · 1 / 5"));
        Assert.That(gold.text, Is.EqualTo(startingGold.ToString()));

        Object.FindFirstObjectByType<BattleManager>().GetTarget().TakeDamage(100000);
        var rewards = Object.FindFirstObjectByType<BattleRewardSystem>();
        Assert.That(gold.text, Is.EqualTo(startingGold.ToString()));
        using (var lockedSave = new System.IO.FileStream(RunSaves.Store.Path, System.IO.FileMode.Open,
            System.IO.FileAccess.Read, System.IO.FileShare.None))
        {
            Assert.That(rewards.ClaimGold(), Is.False);
            Assert.That(gold.text, Is.EqualTo(startingGold.ToString()));
        }
        Assert.That(rewards.ClaimGold(), Is.True);
        int claimedGold = startingGold + rewards.GoldReward;
        Assert.That(gold.text, Is.EqualTo(claimedGold.ToString()));

        Assert.That(rewards.SkipRemaining(), Is.True);
        yield return WaitForMap();
        Object.FindFirstObjectByType<MapController>().SelectOption(0);
        yield return WaitForScene("BattleScene");
        hud = GameObject.Find("TopArea").transform.Find("RunInfo");
        Assert.That(hud.Find("FloorNode").GetComponent<TMP_Text>().text, Is.EqualTo("1층 · 2 / 5"));
        Assert.That(hud.Find("GoldValue").GetComponent<TMP_Text>().text, Is.EqualTo(claimedGold.ToString()));
    }

    private static void ClickUtilityButton(Button button) =>
        ClickPointer(button.gameObject, PointerEventData.InputButton.Left);

    private static void ClickPointer(GameObject target, PointerEventData.InputButton button)
    {
        Canvas.ForceUpdateCanvases();
        var events = EventSystem.current;
        var rect = (RectTransform)target.transform;
        var pointer = new PointerEventData(events)
        {
            button = button,
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
        };
        var hits = new List<RaycastResult>();
        events.RaycastAll(pointer, hits);
        Assert.That(hits, Is.Not.Empty);
        Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(
            hits[0].gameObject), Is.EqualTo(target));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer,
            ExecuteEvents.pointerClickHandler);
    }

    private static IEnumerator WaitForMap()
    {
        yield return WaitForScene("MapScene");
        float until = Time.realtimeSinceStartup + 20;
        while ((RunSession.Instance == null || RunSession.Instance.IsTransitioning || RunSession.Instance.Progress == null) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(RunSession.Instance, Is.Not.Null);
        Assert.That(RunSession.Instance.IsTransitioning, Is.False);
        yield return null;
    }
    private static IEnumerator WaitForScene(string scene)
    {
        float until = Time.realtimeSinceStartup + 20;
        while ((SceneManager.GetActiveScene().name != scene || (RunSession.Instance != null && RunSession.Instance.IsTransitioning)) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(scene));
        yield return null; yield return null;
    }

    private static Canvas FindMapCanvas() =>
        Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(canvas => canvas.isRootCanvas);

    internal static void CaptureMap(string fileName)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Canvas canvas = FindMapCanvas();
        var cameraObject = new GameObject("Map capture camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .075f, .12f);
        var target = new RenderTexture(1280, 720, 24); camera.targetTexture = target;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            string folder = System.IO.Path.Combine(Application.dataPath, "../.utmp/map-system");
            System.IO.Directory.CreateDirectory(folder); System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, fileName), pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            camera.targetTexture = null; Object.Destroy(cameraObject); Object.Destroy(target); Object.Destroy(pixels);
        }
    }

    // Allocate after EnterPlayMode; iterator-generated captured-local display classes do not
    // survive the test runner's domain reload at that yield point.
    private sealed class FinishCounter
    {
        private readonly RunPhase expected;
        public int Count { get; private set; }
        public FinishCounter(RunPhase expected) { this.expected = expected; }
        public void OnFinished(RunPhase phase) { Assert.That(phase, Is.EqualTo(expected)); Count++; }
    }
}
