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

    [UnityTest] public IEnumerator AdventureAndBattleUseTheSameFogAcrossSceneChanges()
    {
        yield return new EnterPlayMode();
        string savePath = System.IO.Path.GetFullPath(".utmp/run-save/fog-" + System.Guid.NewGuid().ToString("N") + ".json");
        RunSaves.UseStore(new RunSaveStore(savePath));
        try
        {
            yield return SceneManager.LoadSceneAsync("TitleScene");
            yield return WaitForScene("TitleScene");
            var title = Object.FindFirstObjectByType<TitleMenuUI>();
            title.StartGame();
            var selection = Object.FindFirstObjectByType<CharacterSelectionUI>();
            yield return TitleMenuTests.WaitForCharacterSelection(selection);
            selection.GetComponentInChildren<Toggle>().isOn = true;
            title.ConfirmAdventure();

            var fog = GameObject.Find("SceneFogTransition");
            Assert.That(fog, Is.Not.Null);
            yield return CheckFogSceneChange(fog, "TitleScene", "MapScene");

            var map = Object.FindFirstObjectByType<MapController>();
            var choice = GameObject.Find("Choice1").GetComponent<Button>();
            Color frameColor = choice.targetGraphic.color;
            RectTransform icon = GetChoiceArtwork(choice);
            Vector2 iconPosition = icon.anchoredPosition;
            Vector2 cardPosition = ((RectTransform)choice.transform).anchoredPosition;
            Vector2 cardSize = ((RectTransform)choice.transform).rect.size;

            yield return HoverChoice(choice, icon, iconPosition, checkExit: true);
            Assert.That(((RectTransform)choice.transform).anchoredPosition, Is.EqualTo(cardPosition));
            Assert.That(((RectTransform)choice.transform).rect.size, Is.EqualTo(cardSize));
            ClickUtilityButton(choice);
            Assert.That(icon.anchoredPosition, Is.EqualTo(iconPosition));
            Assert.That(GameObject.Find("SceneFogTransition"), Is.SameAs(fog));
            Assert.That(choice.targetGraphic.color, Is.Not.EqualTo(frameColor));
            Assert.That(map.CanChoose(0), Is.False);
            yield return CheckFogSceneChange(fog, "MapScene", "BattleScene");
            Assert.That(RunSaves.TryLoad(out RunSaveData checkpoint, out _), Is.True);
            Assert.That(checkpoint.progress.phase, Is.EqualTo(RunPhase.Battle));
        }
        finally { RunSaves.UseStore(null); }
        yield return new ExitPlayMode();
    }

    private static IEnumerator CheckFogSceneChange(GameObject fog, string source, string destination)
    {
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(source));
        Assert.That(RunSession.Instance.IsTransitioning, Is.True);
        var cover = fog.transform.Find("Cover").GetComponent<Image>();
        var layers = fog.GetComponentsInChildren<RawImage>();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/SceneTransitionFog.png");
        Assert.That(texture, Is.Not.Null);
        Assert.That(layers, Has.Length.EqualTo(2));
        Assert.That(layers.All(layer => layer.texture == texture), Is.True);
        Assert.That(texture.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
        Assert.That(texture.width, Is.EqualTo(2048));
        Assert.That(texture.height, Is.EqualTo(1024));
        Assert.That(cover.raycastTarget, Is.True);
        Assert.That(cover.color.a, Is.EqualTo(0f));

        float firstOffset = layers[0].uvRect.x;
        float? alphaAtLoad = null;
        void RecordCover(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == destination) alphaAtLoad = cover.color.a;
        }
        SceneManager.sceneLoaded += RecordCover;
        try { yield return WaitForScene(destination); }
        finally { SceneManager.sceneLoaded -= RecordCover; }

        Assert.That(alphaAtLoad, Is.EqualTo(1f));
        Assert.That(layers[0].uvRect.x, Is.Not.EqualTo(firstOffset));
        Assert.That(layers[0].uvRect.x, Is.Not.EqualTo(layers[1].uvRect.x));
        Assert.That(cover.color.a, Is.EqualTo(0f));
        Assert.That(fog.activeSelf, Is.False);
    }

    [UnityTest] public IEnumerator LoadingHintAppearsOnlyAfterTheDelayAndFadesWithTheFog()
    {
        yield return new EnterPlayMode();
        yield return VerifyLoadingHint();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyLoadingHint()
    {
        var owner = new GameObject("Loading hint test");
        try
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            System.Type type = typeof(RunSession).Assembly.GetType("SceneFogTransition", throwOnError: true);
            object transition = System.Activator.CreateInstance(type, flags, null, new object[] { owner.transform }, null);
            void Call(string method, params object[] args) => type.GetMethod(method, flags).Invoke(transition, args);

            Call("Show");
            Transform overlay = owner.transform.Find("SceneFogTransition");
            Transform hint = overlay.Find("SceneLoadingIndicator");
            Assert.That(hint, Is.Not.Null);
            Assert.That(hint.gameObject.activeSelf, Is.False);
            Call("SetCoverage", 1f);
            Call("UpdateLoading", .799d);
            Assert.That(hint.gameObject.activeSelf, Is.False);

            Call("UpdateLoading", .8d);
            Assert.That(hint.gameObject.activeInHierarchy, Is.True);
            var group = hint.GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(group.blocksRaycasts, Is.False);
            var moon = hint.GetComponentInChildren<Image>();
            Assert.That(AssetDatabase.GetAssetPath(moon.sprite), Is.EqualTo("Assets/Resources/SceneLoadingCrescent.png"));
            var label = hint.GetComponentInChildren<TMP_Text>();
            const string text = "불러오는 중…";
            Assert.That(label.text, Is.EqualTo(text));
            label.ForceMeshUpdate();
            Assert.That(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Select(info => info.character),
                Is.EqualTo(text.ToCharArray()));

            float firstOpacity = moon.color.a;
            CaptureMap("scene-loading-indicator.png", overlay.GetComponent<Canvas>());
            yield return new WaitForSecondsRealtime(.3f);
            Call("SetCoverage", .4f);
            Assert.That(group.alpha, Is.EqualTo(.4f));
            Assert.That(moon.color.a, Is.LessThan(firstOpacity));
            Call("SetCoverage", 0f);
            Assert.That(group.alpha, Is.Zero);

            Call("Hide");
            Assert.That(overlay.gameObject.activeSelf, Is.False);
            Call("Show");
            Call("UpdateLoading", .1d);
            Assert.That(hint.gameObject.activeSelf, Is.False);
        }
        finally { Object.Destroy(owner); }
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
            yield return TitleMenuTests.WaitForCharacterSelection(selection);
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
            var otherChoice = GameObject.Find("Choice1").GetComponent<Button>();
            var shopChoice = GameObject.Find("Choice2").GetComponent<Button>();
            Color originalFrameColor = shopChoice.targetGraphic.color;
            var choiceData = new SerializedObject(shopChoice.GetComponent<MapChoiceCardUI>());
            var coinPurse = (Image)choiceData.FindProperty("artwork").objectReferenceValue;
            Vector3 originalPurseScale = coinPurse.rectTransform.localScale;
            Vector2 pursePosition = coinPurse.rectTransform.anchoredPosition;
            yield return HoverChoice(shopChoice, coinPurse.rectTransform, pursePosition);
            ClickUtilityButton(shopChoice);
            Assert.That(coinPurse.rectTransform.anchoredPosition, Is.EqualTo(pursePosition));
            Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Content));
            var panel = GameObject.Find("ShopPanel").transform;
            var entrance = panel.GetComponent<CanvasGroup>();
            var shelf = (RectTransform)panel.Find("Display");
            Vector2 startPosition = shelf.anchoredPosition;
            Assert.That(entrance, Is.Not.Null);
            Assert.That(entrance.alpha, Is.Zero);
            Assert.That(entrance.interactable, Is.False);
            Assert.That(entrance.blocksRaycasts, Is.False);
            Assert.That(coinPurse.rectTransform.localScale.x, Is.GreaterThan(originalPurseScale.x));
            Assert.That(shopChoice.targetGraphic.color, Is.Not.EqualTo(originalFrameColor));
            Assert.That(otherChoice.GetComponent<CanvasGroup>().alpha, Is.LessThan(1f));
            panel.Find("LeaveButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Content));
            Assert.That(session.SavedContentState, Is.Not.Empty);

            float entranceDeadline = Time.realtimeSinceStartup + 3f;
            while (!entrance.interactable && Time.realtimeSinceStartup < entranceDeadline) yield return null;
            Assert.That(entrance.interactable, Is.True);
            Assert.That(entrance.blocksRaycasts, Is.True);
            Assert.That(coinPurse.rectTransform.localScale, Is.EqualTo(originalPurseScale));
            Assert.That(entrance.alpha, Is.EqualTo(1f));
            Assert.That(shelf.anchoredPosition.y - startPosition.y, Is.EqualTo(24f).Within(0.01f));
            Assert.That(shopChoice.targetGraphic.color, Is.EqualTo(originalFrameColor));
            Assert.That(otherChoice.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
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
            var catalog = RunAssetCatalog.Load();
            var savedStock = JsonUtility.FromJson<SavedShopStock>(stock);
            int[] originalPrices = ReadShopPrices(display, savedStock, catalog);
            yield return null;

            var shopLeave = panel.Find("LeaveButton").GetComponent<Button>();
            var stockCard = display.Find("DreamSlots").GetComponentInChildren<DeckCardUI>();
            var priceTag = GetShopPriceLabel(stockCard.transform.parent).rectTransform;
            var cardVisual = (RectTransform)stockCard.transform.Find("HoverVisual");
            Vector3 pricePosition = priceTag.position;
            var hover = stockCard.GetComponent<DeckCardHoverUI>();
            hover.OnPointerEnter(new PointerEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(cardVisual.localScale.x, Is.GreaterThan(1f));
            Assert.That(priceTag.position, Is.EqualTo(pricePosition));
            Assert.That(cardVisual.TransformPoint(new Vector3(0, cardVisual.rect.yMin)).y,
                Is.GreaterThan(priceTag.TransformPoint(new Vector3(0, priceTag.rect.yMax)).y));
            hover.OnPointerExit(new PointerEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(0.2f);

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
            System.Array.Reverse(savedStock.dream, 0, 2);
            shopCheckpoint.contentState = JsonUtility.ToJson(savedStock);
            Assert.That(shopCheckpoint.contentState, Is.Not.EqualTo(stock));
            Assert.That(RunSaves.Store.TryWrite(shopCheckpoint, out _), Is.True);
            Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
            yield return WaitForMap();

            panel = GameObject.Find("ShopPanel").transform;
            entrance = panel.GetComponent<CanvasGroup>();
            Assert.That(entrance == null || (entrance.interactable && Mathf.Approximately(entrance.alpha, 1f)), Is.True);
            var restoredStock = JsonUtility.FromJson<SavedShopStock>(Object.FindFirstObjectByType<ShopHandler>().CaptureState());
            Assert.That(restoredStock.dream, Is.EqualTo(savedStock.dream));
            Assert.That(restoredStock.nightmare, Is.EqualTo(savedStock.nightmare));
            var restoredCards = panel.Find("Display/DreamSlots").GetComponentsInChildren<DeckCardUI>();
            Assert.That(restoredCards.Select(card => ((TMP_Text)new SerializedObject(card).FindProperty("nameText")
                .objectReferenceValue).text), Is.EqualTo(savedStock.dream.Select(id => catalog.Resolve<CardData>(id).cardName)));
            Assert.That(ReadShopPrices(panel.Find("Display"), savedStock, catalog)[2], Is.EqualTo(originalPrices[2]));
            shop = Object.FindFirstObjectByType<ShopHandler>();
            shop.RestoreState(stock);
            Assert.That(ReadShopPrices(panel.Find("Display"), JsonUtility.FromJson<SavedShopStock>(stock), catalog),
                Is.EqualTo(originalPrices));
            shop.RestoreState(shopCheckpoint.contentState);

            Transform firstSlot = panel.Find("Display/DreamSlots").GetChild(0);
            Transform secondSlot = panel.Find("Display/DreamSlots").GetChild(1);
            int price = int.Parse(GetShopPriceLabel(firstSlot).text);
            string secondPrice = GetShopPriceLabel(secondSlot).text;
            CardData offeredCard = catalog.Resolve<CardData>(savedStock.dream[0]);
            var deckData = new SerializedObject(Object.FindFirstObjectByType<DeckOverviewUI>());
            var dream = (CardDatabase)deckData.FindProperty("dreamStartingDeckDatabase").objectReferenceValue;
            var nightmare = (CardDatabase)deckData.FindProperty("nightmareStartingDeckDatabase").objectReferenceValue;
            int CardCount() => RunDeckInventory.GetCardSelections(dream, nightmare, CardDeckType.TypeDream)
                .Count(selection => selection.Card == offeredCard);
            int beforePurchase = CardCount();
            RunGoldInventory.ResetForNewGame();
            RunGoldInventory.Add(price - 1);
            ClickPointer(firstSlot.GetComponentInChildren<DeckCardUI>().gameObject, PointerEventData.InputButton.Left);
            var buyObject = GameObject.Find("PurchaseButton");
            Assert.That(buyObject, Is.Not.Null);
            var buy = buyObject.GetComponent<Button>();
            Assert.That(buy.GetComponentInChildren<TMP_Text>().text, Is.EqualTo($"구매 · {price}골드"));
            yield return null;
            ClickUtilityButton(buy);
            Assert.That(GameObject.Find("PurchaseMessage").GetComponent<TMP_Text>().text, Is.EqualTo("골드가 부족합니다."));
            CaptureMap("map-shop-purchase.png");
            Assert.That(RunGoldInventory.Gold, Is.EqualTo(price - 1));
            Assert.That(CardCount(), Is.EqualTo(beforePurchase));
            Assert.That(firstSlot.GetComponentInChildren<DeckCardUI>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<CardInspectionUI>().IsOpen, Is.True);

            RunGoldInventory.Add(1);
            using (var locked = new System.IO.FileStream(savePath, System.IO.FileMode.Open,
                System.IO.FileAccess.Read, System.IO.FileShare.None))
            {
                ClickUtilityButton(buy);
                Assert.That(RunGoldInventory.Gold, Is.EqualTo(price));
                Assert.That(CardCount(), Is.EqualTo(beforePurchase));
                Assert.That(firstSlot.GetComponentInChildren<DeckCardUI>(), Is.Not.Null);
                Assert.That(Object.FindFirstObjectByType<CardInspectionUI>().IsOpen, Is.True);
            }

            ClickUtilityButton(buy);
            buy.onClick.Invoke();
            Assert.That(RunGoldInventory.Gold, Is.Zero);
            Assert.That(CardCount(), Is.EqualTo(beforePurchase + 1));
            AssertSoldOut(firstSlot);
            Assert.That(GetShopPriceLabel(secondSlot).text, Is.EqualTo(secondPrice));
            yield return null;
            yield return null;

            Assert.That(session.ReturnToTitle(), Is.True);
            yield return WaitForScene("TitleScene");
            Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
            yield return WaitForMap();
            panel = GameObject.Find("ShopPanel").transform;
            AssertSoldOut(panel.Find("Display/DreamSlots").GetChild(0));
            Assert.That(GetShopPriceLabel(panel.Find("Display/DreamSlots").GetChild(1)).text, Is.EqualTo(secondPrice));
            Assert.That(RunGoldInventory.Gold, Is.Zero);
            Assert.That(CardCount(), Is.EqualTo(beforePurchase + 1));
            CaptureMap("map-shop-sold-out.png");

            secondSlot = panel.Find("Display/DreamSlots").GetChild(1);
            int nextPrice = int.Parse(secondPrice);
            RunGoldInventory.Add(nextPrice + 7);
            int totalDreamCards = RunDeckInventory.GetCardSelections(dream, nightmare, CardDeckType.TypeDream).Count;
            ClickPointer(secondSlot.GetComponentInChildren<DeckCardUI>().gameObject, PointerEventData.InputButton.Left);
            yield return null;
            ClickUtilityButton(GameObject.Find("PurchaseButton").GetComponent<Button>());
            Assert.That(RunGoldInventory.Gold, Is.EqualTo(7));
            Assert.That(RunDeckInventory.GetCardSelections(dream, nightmare, CardDeckType.TypeDream).Count,
                Is.EqualTo(totalDreamCards + 1));
            AssertSoldOut(secondSlot);
            yield return null;
            yield return null;

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

    private static void AssertSoldOut(Transform slot)
    {
        Assert.That(slot.GetComponentInChildren<DeckCardUI>(), Is.Null);
        Assert.That(GetShopPriceLabel(slot), Is.Null);
        var marker = slot.Find("SoldOut")?.GetComponent<Image>();
        Assert.That(marker, Is.Not.Null);
        Assert.That(marker.gameObject.activeInHierarchy, Is.True);
        Assert.That(marker.sprite.name, Is.EqualTo("shop-sold-out"));
        Assert.That(marker.raycastTarget, Is.False);
    }

    private static TMP_Text GetShopPriceLabel(Transform slot) =>
        slot.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.transform.parent == slot);

    private static int[] ReadShopPrices(Transform display, SavedShopStock stock, RunAssetCatalog catalog)
    {
        Canvas.ForceUpdateCanvases();
        var dreamSlots = display.Find("DreamSlots");
        var nightmareSlots = display.Find("NightmareSlots");
        Assert.That(nightmareSlots.GetComponentsInChildren<TMP_Text>(), Is.Empty);
        var prices = new int[stock.dream.Length];
        var divider = (RectTransform)display.Find("ServiceDivider");
        float dividerTop = divider.TransformPoint(new Vector3(0, divider.rect.yMax)).y;
        for (int i = 0; i < prices.Length; i++)
        {
            Transform slot = dreamSlots.GetChild(i);
            TMP_Text label = GetShopPriceLabel(slot);
            Assert.That(label, Is.Not.Null);
            RectTransform priceTag = label.rectTransform;
            var coin = priceTag.GetComponentInChildren<Image>();
            Assert.That(coin.sprite, Is.Not.Null);
            Assert.That(label.raycastTarget || coin.raycastTarget, Is.False);
            Assert.That(label.font.name, Is.EqualTo("NotoSansKR-SemiBold SDF"));
            Assert.That(int.TryParse(label.text, out prices[i]), Is.True, label.text);
            CardData card = catalog.Resolve<CardData>(stock.dream[i]);
            var (min, max) = card.rarity switch
            {
                CardRarity.Common => (30, 40),
                CardRarity.Uncommon => (60, 70),
                _ => (90, 110)
            };
            Assert.That(prices[i], Is.InRange(min, max), card.cardName);
            var cardRect = (RectTransform)slot.GetComponentInChildren<DeckCardUI>().transform;
            Assert.That(priceTag.TransformPoint(new Vector3(0, priceTag.rect.yMin)).y, Is.GreaterThan(dividerTop));
            Assert.That(priceTag.TransformPoint(new Vector3(0, priceTag.rect.yMax)).y,
                Is.LessThan(cardRect.TransformPoint(new Vector3(0, cardRect.rect.yMin)).y));
        }
        return prices;
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
            var selection = Object.FindFirstObjectByType<CharacterSelectionUI>();
            yield return TitleMenuTests.WaitForCharacterSelection(selection);
            selection.GetComponentInChildren<Toggle>().isOn = true;
            title.ConfirmAdventure();
            yield return WaitForMap();
            for (int i = 0; i < 3; i++)
            {
                var map = Object.FindFirstObjectByType<MapController>();
                Assert.That(map.CanChoose(0), Is.True, $"Battle option unavailable before rest, node {i + 1}.");
                map.SelectOption(0);
                Assert.That(RunSession.Instance.Progress.Phase, Is.EqualTo(RunPhase.Battle),
                    $"Node {i + 1}: save error={RunSession.Instance.SaveError}, settings={SettingsUI.BlocksBattleInput}, " +
                    $"deck={DeckOverviewUI.BlocksBattleInput}, inspection={CardInspectionUI.BlocksGameplayInput}");
                yield return WaitForScene("BattleScene");
                var battle = Object.FindFirstObjectByType<BattleManager>();
                battle.GetPlayer().TakeDamage(7);
                battle.GetTarget().TakeDamage(100000);
                Assert.That(Object.FindFirstObjectByType<BattleRewardSystem>().SkipRemaining(), Is.True);
                Assert.That(RunSession.Instance.Progress.Phase, Is.EqualTo(RunPhase.Map), RunSession.Instance.SaveError);
                yield return WaitForMap();
            }

            var session = RunSession.Instance;
            Assert.That(session.Progress.CurrentHP, Is.EqualTo(59));
            Assert.That(Object.FindFirstObjectByType<MapController>().CanChoose(1), Is.True, "The fourth node offers rest.");
            var restChoice = GameObject.Find("Choice2").GetComponent<Button>();
            var battleChoice = GameObject.Find("Choice1").GetComponent<Button>();
            Color frameColor = restChoice.targetGraphic.color;
            var choiceData = new SerializedObject(restChoice.GetComponent<MapChoiceCardUI>());
            var campfire = (Image)choiceData.FindProperty("artwork").objectReferenceValue;
            Vector3 campfireScale = campfire.rectTransform.localScale;
            Vector2 campfirePosition = campfire.rectTransform.anchoredPosition;
            yield return HoverChoice(restChoice, campfire.rectTransform, campfirePosition);
            ClickUtilityButton(restChoice);
            Assert.That(campfire.rectTransform.anchoredPosition, Is.EqualTo(campfirePosition));
            Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Content));

            var enteringPanel = GameObject.Find("RestPanel");
            var entrance = enteringPanel.GetComponent<CanvasGroup>();
            Assert.That(entrance, Is.Not.Null);
            Assert.That(entrance.alpha, Is.Zero);
            Assert.That(entrance.interactable, Is.False);
            Assert.That(entrance.blocksRaycasts, Is.False);
            Assert.That(restChoice.targetGraphic.color, Is.Not.EqualTo(frameColor));
            Assert.That(battleChoice.GetComponent<CanvasGroup>().alpha, Is.LessThan(1f));

            var glow = campfire.rectTransform.parent.Find("CampfireGlow")?.GetComponent<RawImage>();
            Assert.That(glow, Is.Not.Null);
            Assert.That(glow.texture, Is.Not.Null);
            Assert.That(glow.color.r, Is.GreaterThan(glow.color.g));
            Assert.That(glow.raycastTarget, Is.False);
            Assert.That(glow.rectTransform.GetSiblingIndex(), Is.LessThan(campfire.rectTransform.GetSiblingIndex()));
            CaptureMap("map-rest-entrance-glow.png");
            enteringPanel.GetComponentsInChildren<Button>().Single(button => button.name == "RecoverButton").onClick.Invoke();
            Assert.That(session.Progress.CurrentHP, Is.EqualTo(59));
            Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Content));

            float entranceDeadline = Time.realtimeSinceStartup + 3f;
            while (!entrance.interactable && Time.realtimeSinceStartup < entranceDeadline) yield return null;
            Assert.That(entrance.interactable, Is.True);
            Assert.That(entrance.blocksRaycasts, Is.True);
            Assert.That(entrance.alpha, Is.EqualTo(1f));
            Assert.That(glow == null || !glow.gameObject.activeInHierarchy, Is.True);
            Assert.That(campfire.rectTransform.localScale, Is.EqualTo(campfireScale));
            Assert.That(restChoice.targetGraphic.color, Is.EqualTo(frameColor));
            Assert.That(battleChoice.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(session.ReturnToTitle(), Is.True);
            yield return WaitForScene("TitleScene");
            Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
            yield return WaitForMap();

            var panel = GameObject.Find("RestPanel");
            Assert.That(panel, Is.Not.Null);
            entrance = panel.GetComponent<CanvasGroup>();
            Assert.That(entrance == null || (entrance.interactable && Mathf.Approximately(entrance.alpha, 1f)), Is.True);
            var recover = panel.GetComponentsInChildren<Button>().Single(button => button.name == "RecoverButton");
            Assert.That(panel.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Health").text,
                Is.EqualTo("HP 59 / 80"));
            foreach (Button action in panel.GetComponentsInChildren<Button>())
                yield return CheckRestActionHover(action);
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

    private static IEnumerator CheckRestActionHover(Button button)
    {
        Assert.That(button.colors.fadeDuration, Is.EqualTo(.15f).Within(.001f), button.name);
        EventSystem.current.SetSelectedGameObject(button.gameObject);
        yield return new WaitForSecondsRealtime(.18f);

        var rect = (RectTransform)button.transform;
        Vector2 position = rect.anchoredPosition;
        Vector2 size = rect.rect.size;
        var labels = button.GetComponentsInChildren<TMP_Text>();
        Assert.That(labels.Length, Is.EqualTo(2), button.name);
        Color TextColor(TMP_Text label) => label.color * label.canvasRenderer.GetColor();
        Color imageBefore = button.targetGraphic.canvasRenderer.GetColor();
        Color[] textBefore = labels.Select(TextColor).ToArray();

        var pointer = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return new WaitForSecondsRealtime(.18f);
        Assert.That(button.targetGraphic.canvasRenderer.GetColor().grayscale, Is.GreaterThan(imageBefore.grayscale));
        for (int i = 0; i < labels.Length; i++)
            Assert.That(TextColor(labels[i]).grayscale, Is.GreaterThan(textBefore[i].grayscale), labels[i].name);
        Assert.That(rect.anchoredPosition, Is.EqualTo(position));
        Assert.That(rect.rect.size, Is.EqualTo(size));

        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        yield return new WaitForSecondsRealtime(.18f);
        Assert.That(button.targetGraphic.canvasRenderer.GetColor(), Is.EqualTo(imageBefore));
        Assert.That(labels.Select(TextColor), Is.EqualTo(textBefore));
    }

    [UnityTest]
    public IEnumerator RestUpgradeFiltersBothDecksAndCommitsOneCardWithTheNode()
    {
        yield return new EnterPlayMode();
        string savePath = System.IO.Path.GetFullPath(".utmp/run-save/upgrade-" + System.Guid.NewGuid().ToString("N") + ".json");
        RunSaves.UseStore(new RunSaveStore(savePath));
        try
        {
            yield return SceneManager.LoadSceneAsync("MapScene");
            yield return WaitForMap();
            var session = RunSession.Instance;
            var progress = session.Progress;
            for (int i = 0; i < 3; i++)
            {
                Assert.That(progress.TrySelectOption(0), Is.True);
                Assert.That(progress.RecordBattleResult(BattleResult.Victory), Is.True);
                Assert.That(progress.CompleteNode(progress.FloorIndex, progress.NodeIndex), Is.True);
            }
            var overview = Object.FindFirstObjectByType<DeckOverviewUI>();
            var view = new SerializedObject(overview);
            var dream = (CardDatabase)view.FindProperty("dreamStartingDeckDatabase").objectReferenceValue;
            var nightmare = (CardDatabase)view.FindProperty("nightmareStartingDeckDatabase").objectReferenceValue;
            CardData[] Cards(CardDeckType type) => RunDeckInventory.GetCardSelections(dream, nightmare, type)
                .Select(item => item.Card).ToArray();
            var dreamType = CardDeckType.TypeDream;
            var nightmareType = CardDeckType.TypeNightmare;
            var originalDream = Cards(dreamType);
            var originalNightmare = Cards(nightmareType);
            void ReplaceDeck(CardDeckType type, CardData[] cards)
            {
                for (int i = 0; i < cards.Length; i++)
                {
                    var selection = RunDeckInventory.GetCardSelections(dream, nightmare, type)[i];
                    if (selection.Card != cards[i])
                        Assert.That(RunDeckInventory.TryReplaceCard(selection, cards[i]), Is.True);
                }
            }

            Object.FindFirstObjectByType<MapController>().SelectOption(1);
            Assert.That(progress.Phase, Is.EqualTo(RunPhase.Content));
            var rest = GameObject.Find("RestPanel");
            var upgrade = rest.GetComponentsInChildren<Button>().Single(button => button.name == "UpgradeButton");
            Assert.That(upgrade.interactable, Is.True);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!rest.GetComponent<CanvasGroup>().interactable && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(rest.GetComponent<CanvasGroup>().interactable, Is.True);

            var listPanel = ((GameObject)view.FindProperty("panel").objectReferenceValue).transform;
            DeckCardUI[] ListCards(string section) => listPanel.Find(section).GetComponentsInChildren<DeckCardUI>();
            Button FindButton(string name) => GameObject.Find(name).GetComponent<Button>();
            Sprite ButtonArt(string button, string asset)
            {
                var image = FindButton(button).targetGraphic as Image;
                Assert.That(image, Is.Not.Null, button);
                Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo("Assets/Resources/" + asset + ".png"), button);
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), button);
                Assert.That(image.sprite.border.x, Is.GreaterThan(0f), button);
                return image.sprite;
            }
            var target = originalDream.First(card => card.upgradedCard != null);
            var replacement = target.upgradedCard;

            ReplaceDeck(dreamType, originalDream.Select(card => card.upgradedCard ?? card).ToArray());
            ClickUtilityButton(upgrade);
            yield return null;
            Assert.That(ListCards("DreamSection"), Is.Empty);
            Assert.That(ListCards("NightmareSection").Length, Is.GreaterThan(0));
            Sprite cancelArt = ButtonArt("UpgradeListCancel", "CardUpgradeCancel");
            ClickUtilityButton(FindButton("UpgradeListCancel"));
            yield return null;
            ReplaceDeck(nightmareType, originalNightmare.Select(card => card.upgradedCard ?? card).ToArray());
            ClickUtilityButton(upgrade);
            yield return null;
            Assert.That(ListCards("DreamSection"), Is.Empty);
            Assert.That(ListCards("NightmareSection"), Is.Empty);
            ClickUtilityButton(FindButton("UpgradeListCancel"));
            Assert.That(rest.activeInHierarchy && !overview.IsOpen, Is.True);
            yield return null;

            ReplaceDeck(dreamType, originalDream);
            ReplaceDeck(nightmareType, originalNightmare);
            Assert.That(originalDream.Count(card => card == target), Is.GreaterThan(1));
            ClickUtilityButton(upgrade);
            yield return null;
            CaptureMap("map-upgrade-list.png");
            yield return null;
            var cardField = typeof(DeckCardUI).GetField("cardData", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            CardData DisplayedCard(DeckCardUI card) => (CardData)cardField.GetValue(card);
            var nightmareItem = ListCards("NightmareSection").First();
            ClickUtilityButton(nightmareItem.GetComponent<Button>());
            yield return null;
            var nightmareOriginal = DisplayedCard(nightmareItem);
            Assert.That(GameObject.Find("UpgradeComparison").GetComponentsInChildren<DeckCardUI>().Select(DisplayedCard),
                Is.EqualTo(new[] { nightmareOriginal, nightmareOriginal.upgradedCard }));
            ClickUtilityButton(FindButton("UpgradeComparisonCancel"));
            yield return null;

            var dreamCards = ListCards("DreamSection");
            var dreamScroll = listPanel.Find("DreamSection").GetComponentInChildren<ScrollRect>();
            dreamScroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            EventSystem.current.SetSelectedGameObject(dreamCards[0].gameObject);
            var firstRect = (RectTransform)dreamCards[0].transform;
            float firstTop = dreamScroll.viewport.InverseTransformPoint(firstRect.TransformPoint(firstRect.rect.max)).y;
            Assert.That(firstTop, Is.LessThanOrEqualTo(dreamScroll.viewport.rect.yMax + .01f));

            dreamScroll.verticalNormalizedPosition = 1f;
            yield return null;
            var item = dreamCards.Last(card => DisplayedCard(card) == target);
            ClickUtilityButton(item.GetComponent<Button>());
            var comparison = GameObject.Find("UpgradeComparison");
            Assert.That(comparison.GetComponentsInChildren<DeckCardUI>().Select(DisplayedCard),
                Is.EqualTo(new[] { target, replacement }));
            Assert.That(Cards(dreamType), Is.EqualTo(originalDream));
            Assert.That(ButtonArt("UpgradeComparisonCancel", "CardUpgradeCancel"), Is.SameAs(cancelArt));
            Assert.That(ButtonArt("UpgradeConfirm", "CardUpgradeConfirm"), Is.Not.SameAs(cancelArt));
            yield return null;
            CaptureMap("map-upgrade-comparison.png");
            yield return null;
            ClickUtilityButton(FindButton("UpgradeComparisonCancel"));
            Assert.That(comparison.activeSelf, Is.False);
            Assert.That(overview.IsOpen, Is.True);
            yield return null;
            ClickUtilityButton(item.GetComponent<Button>());
            yield return null;
            var confirm = FindButton("UpgradeConfirm");
            int health = progress.CurrentHP;
            int gold = RunGoldInventory.Gold;
            using (var locked = new System.IO.FileStream(savePath, System.IO.FileMode.Open,
                System.IO.FileAccess.Read, System.IO.FileShare.None))
            {
                ClickUtilityButton(confirm);
                Assert.That(comparison.activeInHierarchy, Is.True);
                Assert.That(progress.Phase, Is.EqualTo(RunPhase.Content));
                Assert.That(Cards(dreamType), Is.EqualTo(originalDream));
            }
            ClickUtilityButton(confirm);
            confirm.onClick.Invoke();
            Assert.That(rest.activeInHierarchy || overview.IsOpen, Is.False);
            Assert.That(progress.Phase, Is.EqualTo(RunPhase.Map));
            Assert.That(progress.NodeIndex, Is.EqualTo(4));
            Assert.That(progress.CurrentHP, Is.EqualTo(health));
            Assert.That(RunGoldInventory.Gold, Is.EqualTo(gold));
            var expectedDream = (CardData[])originalDream.Clone();
            expectedDream[System.Array.LastIndexOf(originalDream, target)] = replacement;
            Assert.That(Cards(dreamType), Is.EqualTo(expectedDream));
            Assert.That(Cards(nightmareType), Is.EqualTo(originalNightmare));

            Assert.That(session.ReturnToTitle(), Is.True);
            yield return WaitForScene("TitleScene");
            Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
            yield return WaitForMap();
            Assert.That(session.Progress.NodeIndex, Is.EqualTo(4));
            Assert.That(Cards(dreamType).Count(card => card == replacement), Is.EqualTo(1));
            Object.FindFirstObjectByType<MapController>().SelectOption(0);
            yield return WaitForScene("BattleScene");
            Assert.That(Object.FindFirstObjectByType<DeckController>().GetDeck(dreamType), Does.Contain(replacement));
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
            var shopTitle = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                .SingleOrDefault(text => text.text == "매개체");
            Assert.That(shopTitle, Is.Not.Null);
            Assert.That("매개체".All(character => shopTitle.font.HasCharacter(character)), Is.True);
            var presentations = config.FindProperty("choicePresentations");
            var relicPresentation = Enumerable.Range(0, presentations.arraySize)
                .Select(presentations.GetArrayElementAtIndex)
                .Single(item => item.FindPropertyRelative("type").intValue == (int)MapContentType.Relic);
            Assert.That(relicPresentation.FindPropertyRelative("title").stringValue, Is.EqualTo("매개체"));
            Assert.That(relicPresentation.FindPropertyRelative("description").stringValue,
                Is.EqualTo("런에 도움을 주는\n매개체를 획득합니다."));
            Assert.That(MapChoiceCardUI.DefaultTitle(MapContentType.Relic), Is.EqualTo("매개체"));
            Assert.That(MapChoiceCardUI.DefaultDescription(MapContentType.Relic), Does.Contain("매개체를"));
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
            Assert.That(RunSession.Instance.Progress.Phase, Is.EqualTo(RunPhase.Battle),
                $"After deck close: save error={RunSession.Instance.SaveError}, settings={SettingsUI.BlocksBattleInput}, " +
                $"deck={DeckOverviewUI.BlocksBattleInput}, inspection={CardInspectionUI.BlocksGameplayInput}");
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

    private static RectTransform GetChoiceArtwork(Button button)
    {
        var card = new SerializedObject(button.GetComponent<MapChoiceCardUI>());
        return ((Image)card.FindProperty("artwork").objectReferenceValue).rectTransform;
    }

    private static IEnumerator HoverChoice(Button button, RectTransform icon, Vector2 restPosition, bool checkExit = false)
    {
        var pointer = new PointerEventData(EventSystem.current);
        Color normalTint = button.colors.normalColor;
        Vector2 raisedPosition = restPosition + Vector2.up * 5f;
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return WaitForChoiceArtwork(icon, raisedPosition);

        if (!checkExit) yield break;
        Assert.That(button.targetGraphic.canvasRenderer.GetColor().grayscale, Is.GreaterThan(normalTint.grayscale));
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        yield return WaitForChoiceArtwork(icon, restPosition);
        Assert.That(button.targetGraphic.canvasRenderer.GetColor().grayscale, Is.EqualTo(normalTint.grayscale).Within(0.01f));
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return WaitForChoiceArtwork(icon, raisedPosition);
    }

    private static IEnumerator WaitForChoiceArtwork(RectTransform icon, Vector2 position)
    {
        const float tolerance = 0.001f;
        float deadline = Time.realtimeSinceStartup + 2f;
        while (Vector2.Distance(icon.anchoredPosition, position) > tolerance && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(Vector2.Distance(icon.anchoredPosition, position), Is.LessThanOrEqualTo(tolerance));
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
            hits[0].gameObject), Is.EqualTo(target), $"First raycast hit {hits[0].gameObject.name} at {pointer.position}.");
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

    internal static void CaptureMap(string fileName, Canvas canvas = null)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        canvas ??= FindMapCanvas();
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
