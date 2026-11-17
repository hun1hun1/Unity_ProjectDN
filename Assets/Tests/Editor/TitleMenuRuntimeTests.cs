using System;
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public partial class TitleMenuTests
{
    [UnityTest]
    public IEnumerator TutorialNavigationAndQuitDoNotChangeTheRunAndBlockUnderlyingMenu()
    {
        yield return new EnterPlayMode();yield return VerifyTutorialAndQuit();yield return new ExitPlayMode();
    }
    private static IEnumerator VerifyTutorialAndQuit()
    {
        yield return LoadTitle();TitleMenuUI menu=Object.FindFirstObjectByType<TitleMenuUI>();
        var platform=new FakeGameSettingsPlatform();menu.BindSettings(new GameSettingsService(platform));
        RunGoldInventory.Add(73);var session=RunSession.GetOrCreate();var before=session.Progress;
        Ref<Button>(menu,"tutorialButton").onClick.Invoke();
        Assert.That(Ref<GameObject>(menu,"tutorialPanel").activeSelf,Is.True);
        Assert.That(Ref<TMP_Text>(menu,"pageIndicator").text,Is.EqualTo("1 / 4"));
        Assert.That(Ref<Button>(menu,"previousButton").interactable,Is.False);
        menu.PreviousPage();menu.RequestQuit();menu.StartGame();
        Assert.That(Ref<GameObject>(menu,"quitPanel").activeSelf,Is.False);
        Assert.That(Ref<CanvasGroup>(menu,"mainMenu").interactable,Is.False);
        for(int i=0;i<5;i++)menu.NextPage();
        Assert.That(Ref<TMP_Text>(menu,"pageIndicator").text,Is.EqualTo("4 / 4"));
        Assert.That(Ref<Button>(menu,"nextButton").interactable,Is.False);
        menu.PreviousPage();Assert.That(Ref<TMP_Text>(menu,"pageIndicator").text,Is.EqualTo("3 / 4"));
        menu.HandleEscape();menu.StartGame();Assert.That(session.Progress,Is.SameAs(before));
        yield return null;yield return null;
        menu.ConfirmQuit();Assert.That(platform.QuitRequests,Is.Zero);
        Ref<Button>(menu,"quitButton").onClick.Invoke();menu.OpenTutorial();menu.OpenOptions();menu.StartGame();
        Assert.That(Ref<GameObject>(menu,"tutorialPanel").activeSelf,Is.False);
        Assert.That(Ref<SettingsUI>(menu,"settingsUI").IsOpen,Is.False);
        menu.HandleEscape();Assert.That(platform.QuitRequests,Is.Zero);
        yield return null;yield return null;
        menu.RequestQuit();Ref<Button>(menu,"confirmQuitButton").onClick.Invoke();menu.ConfirmQuit();
        Assert.That(platform.QuitRequests,Is.EqualTo(1));Assert.That(platform.Saves,Is.EqualTo(1));
        menu.CancelQuit();yield return null;yield return null;
        Assert.That(Ref<CanvasGroup>(menu,"mainMenu").interactable,Is.True);
        Assert.That(session.Progress,Is.SameAs(before));Assert.That(RunGoldInventory.Gold,Is.EqualTo(73));
    }

    [UnityTest]
    public IEnumerator TitleOptionsWorkWithoutBattleAndReturnToTheMenu()
    {yield return new EnterPlayMode();yield return VerifyOptions();yield return new ExitPlayMode();}
    private static IEnumerator VerifyOptions()
    {
        yield return LoadTitle();TitleMenuUI menu=Object.FindFirstObjectByType<TitleMenuUI>();
        var platform=new FakeGameSettingsPlatform();var service=new GameSettingsService(platform);menu.BindSettings(service);
        Ref<Button>(menu,"optionsButton").onClick.Invoke();SettingsUI options=Ref<SettingsUI>(menu,"settingsUI");
        yield return null;yield return null;
        Assert.That(options.IsOpen,Is.True);Assert.That(Object.FindFirstObjectByType<BattleManager>(),Is.Null);
        Assert.That(Ref<CanvasGroup>(menu,"mainMenu").interactable,Is.False);
        Ref<Slider>(options,"masterSlider").value=.35f;
        Ref<TMP_Dropdown>(options,"resolutionDropdown").value=0;
        Ref<Toggle>(options,"fullscreenToggle").isOn=true;Ref<Button>(options,"applyDisplayButton").onClick.Invoke();
        Assert.That(service.Current.masterVolume,Is.EqualTo(.35f).Within(.001f));
        Assert.That(platform.CurrentResolution,Is.EqualTo(new GameResolution(1280,720)));
        Assert.That(platform.Fullscreen,Is.True);
        Assert.That(Ref<Button>(options,"returnButton").GetComponentInChildren<TMP_Text>().text,Is.EqualTo("타이틀로 돌아가기"));
        menu.StartGame();Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("TitleScene"));
        Ref<Button>(options,"returnButton").onClick.Invoke();menu.StartGame();
        Assert.That(options.IsOpen,Is.False);Assert.That(RunSession.Instance==null||!RunSession.Instance.IsTransitioning,Is.True);
        yield return null;yield return null;
        Assert.That(Ref<CanvasGroup>(menu,"mainMenu").interactable,Is.True);
        menu.OpenOptions();Assert.That(options.IsOpen,Is.True);options.HandleEscape();yield return null;yield return null;
        Assert.That(Ref<CanvasGroup>(menu,"mainMenu").interactable,Is.True);
    }

    [UnityTest]
    public IEnumerator CharacterSelectionFadesOverTheMenuAndBlocksInputUntilVisible()
    {
        yield return new EnterPlayMode();
        yield return VerifyCharacterSelectionFade();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyCharacterSelectionFade()
    {
        yield return LoadTitle();
        var menu = Object.FindFirstObjectByType<TitleMenuUI>();
        var mainMenu = Ref<CanvasGroup>(menu, "mainMenu");
        menu.StartGame();
        var selection = Object.FindFirstObjectByType<CharacterSelectionUI>();
        Assert.That(selection, Is.Not.Null, "Starting the game must activate the character selection panel.");
        var soldier = Ref<Toggle>(selection, "soldierToggle");
        var panel = selection.GetComponent<CanvasGroup>();
        Assert.That(panel, Is.Not.Null);
        Assert.That(panel.alpha, Is.Zero);
        Assert.That(panel.interactable, Is.False);
        Assert.That(mainMenu.alpha, Is.EqualTo(1f));
        Assert.That(mainMenu.interactable || mainMenu.blocksRaycasts, Is.False);
        soldier.OnSubmit(null);
        Assert.That(soldier.isOn, Is.False);
        soldier.isOn = true;
        menu.ConfirmAdventure();
        Assert.That(RunSession.Instance, Is.Null);

        menu.HandleEscape();
        Assert.That(selection.gameObject.activeSelf, Is.False);
        Assert.That(mainMenu.alpha, Is.EqualTo(1f));
        menu.StartGame();
        Assert.That(selection.gameObject.activeSelf, Is.False, "Closing must block input in the same frame.");

        yield return null;
        menu.StartGame();
        Assert.That(panel.alpha, Is.Zero);
        Assert.That(soldier.isOn, Is.False);
        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        try
        {
            yield return Until(() => panel.alpha == 1f);
        }
        finally { Time.timeScale = previousTimeScale; }
        yield return null;

        Assert.That(mainMenu.alpha, Is.Zero);
        Assert.That(panel.interactable, Is.True);
        var info = Ref<GameObject>(selection, "soldierInfo").transform;
        var conduitRow = info.Find("StartingConduit");
        Assert.That(conduitRow, Is.Not.Null);
        Assert.That(conduitRow.gameObject.activeInHierarchy, Is.False);

        soldier.OnSubmit(null);
        Assert.That(soldier.isOn, Is.True);
        Assert.That(Ref<Button>(selection, "adventureButton").IsInteractable(), Is.True);
        Assert.That(conduitRow.gameObject.activeInHierarchy, Is.True);
        var conduit = Ref<AmmoCapacityRelic>(menu, "soldierStartingConduit");
        Assert.That(conduitRow.Find("Icon").GetComponent<Image>().sprite, Is.SameAs(conduit.HudArtwork));
        Assert.That(conduitRow.Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo("시작 매개체 · " + conduit.RelicName));
        Assert.That(conduitRow.Find("Effect").GetComponent<TMP_Text>().text, Is.EqualTo(conduit.Description));

        selection.ToggleDeckDetails();
        Canvas.ForceUpdateCanvases();
        Assert.That(Top(info.Find("Resource")), Is.GreaterThan(Top(conduitRow)));
        Assert.That(Bottom(info.Find("Resource")), Is.GreaterThan(Top(conduitRow)));
        Assert.That(Bottom(conduitRow), Is.GreaterThan(Top(info.Find("DreamDeck"))));
        Assert.That(Bottom(info.Find("DeckDetailsButton")), Is.GreaterThan(Top(info.Find("DeckDetails"))));
        Assert.That(Bottom(info.Find("DeckDetails")), Is.GreaterThan(Bottom(info)));

        soldier.OnSubmit(null);
        Assert.That(conduitRow.gameObject.activeInHierarchy, Is.False);
        Assert.That(Ref<GameObject>(selection, "deckDetails").activeSelf, Is.False);

        static float Top(Transform item) => item.TransformPoint(((RectTransform)item).rect.max).y;
        static float Bottom(Transform item) => item.TransformPoint(((RectTransform)item).rect.min).y;
    }

    [UnityTest]
    public IEnumerator StartCreatesOneFreshRunAndResetsOldResourcesBeforeLoadingMap()
    {yield return new EnterPlayMode();yield return VerifyNewRun();yield return new ExitPlayMode();}
    private static IEnumerator VerifyNewRun()
    {
        RunSaves.UseStore(new RunSaveStore(System.IO.Path.GetFullPath(".utmp/run-save/new-run-"+Guid.NewGuid().ToString("N")+".json")));
        yield return LoadTitle();TitleMenuUI menu=Object.FindFirstObjectByType<TitleMenuUI>();
        Ref<Button>(menu,"startButton").onClick.Invoke();
        Assert.That(RunSession.Instance, Is.Null, "Opening character selection must not create a run.");
        Assert.That(RunSaves.HasContinue, Is.False);
        var initialSelection = Ref<CharacterSelectionUI>(menu, "characterSelection");
        var initialAdventure = Ref<Button>(initialSelection, "adventureButton");
        Assert.That(initialSelection.SoldierSelected, Is.False);
        Assert.That(initialSelection.transform.Find("CharacterPreview").gameObject.activeInHierarchy, Is.False);
        Assert.That(initialSelection.transform.Find("CharacterInfo").gameObject.activeInHierarchy, Is.False);
        Assert.That(initialAdventure.IsInteractable(), Is.False);
        initialAdventure.onClick.Invoke();
        Assert.That(RunSession.Instance, Is.Null, "An unselected character must not start a run.");
        yield return WaitForCharacterSelection(initialSelection);
        ConfirmSelection(menu);
        yield return Until(()=>SceneManager.GetActiveScene().name=="MapScene"&&RunSession.Instance!=null&&!RunSession.Instance.IsTransitioning);
        RunSession session=RunSession.Instance;RunProgress old=session.Progress;
        Assert.That(old,Is.Not.Null);RunGoldInventory.Add(111);
        yield return LoadTitle();menu=Object.FindFirstObjectByType<TitleMenuUI>();
        uint version=(uint)typeof(RunDeckInventory).GetProperty("GameVersion",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
        string before = System.IO.File.ReadAllText(RunSaves.Store.Path);
        Ref<Button>(menu,"startButton").onClick.Invoke();
        var selection = Ref<CharacterSelectionUI>(menu, "characterSelection");
        Assert.That(selection.gameObject.activeSelf, Is.True);
        yield return WaitForCharacterSelection(selection);
        Assert.That(Ref<CanvasGroup>(menu, "mainMenu").alpha, Is.Zero);
        Assert.That(Ref<Toggle>(selection, "soldierToggle").isOn, Is.False);
        Assert.That(Ref<Button>(selection, "adventureButton").IsInteractable(), Is.False);
        Assert.That(selection.transform.Find("CharacterSlots/Locked2").GetComponent<Button>().IsInteractable(), Is.False);
        Assert.That(selection.transform.Find("CharacterSlots/Locked3").GetComponent<Button>().IsInteractable(), Is.False);
        menu.OpenOptions();
        Assert.That(Ref<SettingsUI>(menu, "settingsUI").IsOpen, Is.False);
        Ref<Toggle>(selection, "soldierToggle").OnSubmit(null);
        Assert.That(Ref<Button>(selection, "adventureButton").IsInteractable(), Is.True);
        var preview = selection.transform.Find("CharacterPreview").gameObject;
        var info = selection.transform.Find("CharacterInfo");
        Assert.That(preview.activeInHierarchy, Is.True);
        Assert.That(info.gameObject.activeInHierarchy, Is.True);
        var deckDetails = info.Find("DeckDetails").gameObject;
        selection.ToggleDeckDetails();
        Assert.That(deckDetails.activeInHierarchy, Is.True);

        var soldier = Ref<Toggle>(selection, "soldierToggle");
        soldier.OnSubmit(null);
        Assert.That(preview.activeInHierarchy, Is.False);
        Assert.That(info.gameObject.activeInHierarchy, Is.False);
        soldier.OnSubmit(null);
        Assert.That(preview.activeInHierarchy, Is.True);
        Assert.That(info.gameObject.activeInHierarchy, Is.True);
        Assert.That(deckDetails.activeInHierarchy, Is.False);
        menu.HandleEscape();
        Assert.That(selection.gameObject.activeSelf, Is.False);
        Assert.That(session.Progress, Is.SameAs(old));
        Assert.That(System.IO.File.ReadAllText(RunSaves.Store.Path), Is.EqualTo(before));
        yield return null;
        Ref<Button>(menu,"startButton").onClick.Invoke();
        Assert.That(Ref<Toggle>(selection, "soldierToggle").isOn, Is.False);
        yield return WaitForCharacterSelection(selection);
        ConfirmSelection(menu);
        ConfirmSelection(menu);
        Assert.That(session.Progress,Is.Not.SameAs(old));
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(100));
        Assert.That(RunSaves.TryLoad(out RunSaveData initialSave, out _), Is.True);
        Assert.That(initialSave.inventory.gold, Is.EqualTo(100));
        var casing = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/MapData/SoldierStartingCasing.asset");
        Assert.That(casing, Is.Not.Null);
        Assert.That(casing.Artwork, Is.Not.Null);
        Assert.That(RunRelicInventory.Contains(casing), Is.True);
        Assert.That(initialSave.inventory.relics.Length, Is.EqualTo(1));
        Assert.That(initialSave.inventory.relics[0].id, Is.EqualTo(RunAssetCatalog.Load().Key(casing)));
        Assert.That(initialSave.inventory.relics[0].battleStartAmmo, Is.EqualTo(2));
        Assert.That((uint)typeof(RunDeckInventory).GetProperty("GameVersion",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null),Is.EqualTo(version+1));
        yield return Until(()=>SceneManager.GetActiveScene().name=="MapScene"&&!session.IsTransitioning);
        Assert.That(session.Progress.Definition,Is.SameAs(Object.FindFirstObjectByType<MapController>().Definition));
        Assert.That(session.Progress.Nodes.Count, Is.EqualTo(13));
        Assert.That(session.Progress.Phase,Is.EqualTo(RunPhase.Map));Assert.That(session.Progress.CurrentHP,Is.EqualTo(session.Progress.MaxHP));
        Assert.That(session.SelectOption(0), Is.True);
        yield return Until(() => SceneManager.GetActiveScene().name == "BattleScene" && !session.IsTransitioning);
        Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Battle));
        Assert.That(session.Progress.CurrentNode.SelectedChoice.Encounter, Is.Not.Null);
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle.GetTarget(), Is.Not.Null);
        AmmoSystem ammo = battle.GetPlayer().GetAmmoSystem();
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(2));
        Assert.That(ammo.MaxAmmo, Is.EqualTo(ammo.BaseMaxAmmo));
        Assert.That(ammo.TrySpend(1), Is.True);
        AssertConduitIcons(casing);

        var later = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/MapData/MidpointEliteConduit.asset");
        var last = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/MapData/FirstFloorConduit.asset");
        Assert.That(later.Acquire(), Is.True);
        Assert.That(last.Acquire(), Is.True);
        Assert.That(later.Acquire(), Is.False);
        AssertConduitIcons(casing, later, last);
        Assert.That(session.SaveCheckpoint(), Is.True, session.SaveError);

        Assert.That(session.ReturnToTitle(), Is.True);
        yield return Until(() => SceneManager.GetActiveScene().name == "TitleScene" && !session.IsTransitioning);
        Object.Destroy(session.gameObject);
        yield return null;
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
        yield return Until(() => SceneManager.GetActiveScene().name == "BattleScene" &&
            RunSession.Instance != null && !RunSession.Instance.IsTransitioning);
        ammo = Object.FindFirstObjectByType<BattleManager>().GetPlayer().GetAmmoSystem();
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(2));
        AssertConduitIcons(casing, later, last);
        Assert.That(RunRelicInventory.Count, Is.EqualTo(3));
    }

    private static void AssertConduitIcons(params AmmoCapacityRelic[] expected)
    {
        var area = GameObject.Find("ConduitArea").GetComponent<RectTransform>();
        var group = area.GetComponent<HorizontalLayoutGroup>();
        Assert.That(group, Is.Not.Null);
        Assert.That(group.childAlignment, Is.EqualTo(TextAnchor.MiddleLeft));
        Assert.That(group.padding.left, Is.EqualTo(24));
        Assert.That(group.spacing, Is.EqualTo(12));
        Canvas.ForceUpdateCanvases();
        Assert.That(area.GetChild(0).name, Is.EqualTo("ConduitBackground"));
        Assert.That(area.childCount, Is.EqualTo(expected.Length + 1));
        for (int i = 0; i < expected.Length; i++)
        {
            var slot = (RectTransform)area.GetChild(i + 1);
            var icon = slot.Find("Artwork").GetComponent<UnityEngine.UI.Image>();
            Assert.That(slot.name, Is.EqualTo(expected[i].name));
            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(icon.sprite, Is.SameAs(expected[i].HudArtwork));
            Assert.That(icon.sprite.rect.size, Is.EqualTo(new Vector2(256, 256)));
            Assert.That(icon.preserveAspect, Is.True);
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(slot.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
            Assert.That(icon.rectTransform.rect.size, Is.EqualTo(new Vector2(64, 64)));
            Assert.That(slot.anchoredPosition.x, Is.EqualTo(56 + 76 * i).Within(.01f));
        }
    }

    [UnityTest]
    public IEnumerator MenuButtonsReturnToNormalAfterPointerExitWithoutKeepingSelection()
    {yield return new EnterPlayMode();yield return VerifyMouseOnlyMenu();yield return new ExitPlayMode();}
    private static IEnumerator VerifyMouseOnlyMenu()
    {
        RunSaves.UseStore(new RunSaveStore(System.IO.Path.GetFullPath(".utmp/title-hover/pointer-"+Guid.NewGuid().ToString("N")+".json")));
        yield return LoadTitle();
        TitleMenuUI menu=Object.FindFirstObjectByType<TitleMenuUI>();
        Assert.That(EventSystem.current.currentSelectedGameObject,Is.Null,"The title must not select its first button automatically.");
        foreach(string field in new[]{"continueButton","startButton","tutorialButton","optionsButton","quitButton"})
        {
            Button button=Ref<Button>(menu,field);
            Assert.That(button.navigation.mode,Is.EqualTo(Navigation.Mode.None),field);
            if(!button.gameObject.activeInHierarchy)continue;
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
            Assert.That(button.image.overrideSprite,Is.EqualTo(button.spriteState.highlightedSprite),field+" hover");
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            Assert.That(button.image.overrideSprite,Is.EqualTo(button.spriteState.pressedSprite),field+" press");
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.Null,field+" must not retain click selection");
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerExitHandler);
            Assert.That(button.image.overrideSprite,Is.EqualTo(button.image.sprite),field+" pointer exit");
        }
        menu.StartGame();
        yield return WaitForCharacterSelection(Ref<CharacterSelectionUI>(menu, "characterSelection"));
        ConfirmSelection(menu);
        yield return Until(()=>SceneManager.GetActiveScene().name=="MapScene"&&!RunSession.Instance.IsTransitioning);
        yield return LoadTitle();
        menu=Object.FindFirstObjectByType<TitleMenuUI>();
        Assert.That(Ref<Button>(menu,"continueButton").gameObject.activeSelf,Is.True);
        Assert.That(EventSystem.current.currentSelectedGameObject,Is.Null,"Continue must not be selected automatically either.");
    }

    [UnityTest]
    public IEnumerator ReturningFromTitlePopupsDoesNotSelectAMenuButton()
    {yield return new EnterPlayMode();yield return VerifyPopupSelectionClearing();yield return new ExitPlayMode();}
    private static IEnumerator VerifyPopupSelectionClearing()
    {
        yield return LoadTitle();TitleMenuUI menu=Object.FindFirstObjectByType<TitleMenuUI>();
        menu.BindSettings(new GameSettingsService(new FakeGameSettingsPlatform()));
        menu.OpenTutorial();menu.CloseTutorial();
        Assert.That(EventSystem.current.currentSelectedGameObject,Is.Null,"Closing the tutorial must clear selection.");
        yield return null;yield return null;
        menu.RequestQuit();menu.CancelQuit();
        Assert.That(EventSystem.current.currentSelectedGameObject,Is.Null,"Cancelling quit must clear selection.");
        yield return null;yield return null;
        EventSystem.current.SetSelectedGameObject(Ref<Button>(menu,"optionsButton").gameObject);
        menu.OpenOptions();SettingsUI options=Ref<SettingsUI>(menu,"settingsUI");
        Assert.That(options.IsOpen,Is.True);
        options.Close();
        Assert.That(EventSystem.current.currentSelectedGameObject,Is.Null,"Title settings must not restore an old menu selection.");
    }

    internal static IEnumerator WaitForCharacterSelection(CharacterSelectionUI selection)
    {
        var group = selection.GetComponent<CanvasGroup>();
        Assert.That(group, Is.Not.Null);
        yield return Until(() => group.interactable);
        yield return null;
    }

    private static void ConfirmSelection(TitleMenuUI menu)
    {
        var selection = Ref<CharacterSelectionUI>(menu, "characterSelection");
        var soldier = Ref<Toggle>(selection, "soldierToggle");
        if (!soldier.isOn) soldier.OnSubmit(null);
        Ref<Button>(selection, "adventureButton").onClick.Invoke();
    }

    private static T Ref<T>(Object owner,string name) where T:Object=>(T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    private static IEnumerator LoadTitle()
    {AsyncOperation operation=SceneManager.LoadSceneAsync("TitleScene");yield return Until(()=>operation.isDone);yield return null;yield return null;}
    private static IEnumerator Until(Func<bool> predicate)
    {float end=Time.realtimeSinceStartup+20;while(!predicate()){Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;}}
}
