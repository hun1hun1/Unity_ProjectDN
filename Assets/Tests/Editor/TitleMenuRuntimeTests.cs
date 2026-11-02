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
    public IEnumerator StartCreatesOneFreshRunAndResetsOldResourcesBeforeLoadingMap()
    {yield return new EnterPlayMode();yield return VerifyNewRun();yield return new ExitPlayMode();}
    private static IEnumerator VerifyNewRun()
    {
        yield return LoadTitle();TitleMenuUI menu=Object.FindFirstObjectByType<TitleMenuUI>();
        menu.StartGame();menu.StartGame();
        yield return Until(()=>SceneManager.GetActiveScene().name=="MapScene"&&RunSession.Instance!=null&&!RunSession.Instance.IsTransitioning);
        RunSession session=RunSession.Instance;RunProgress old=session.Progress;
        Assert.That(old,Is.Not.Null);RunGoldInventory.Add(111);
        yield return LoadTitle();menu=Object.FindFirstObjectByType<TitleMenuUI>();
        uint version=(uint)typeof(RunDeckInventory).GetProperty("GameVersion",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
        Ref<Button>(menu,"startButton").onClick.Invoke();menu.StartGame();
        Assert.That(session.Progress,Is.Not.SameAs(old));Assert.That(RunGoldInventory.Gold,Is.Zero);
        Assert.That((uint)typeof(RunDeckInventory).GetProperty("GameVersion",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null),Is.EqualTo(version+1));
        yield return Until(()=>SceneManager.GetActiveScene().name=="MapScene"&&!session.IsTransitioning);
        Assert.That(session.Progress.Definition,Is.SameAs(Object.FindFirstObjectByType<MapController>().Definition));
        Assert.That(session.Progress.Phase,Is.EqualTo(RunPhase.Map));Assert.That(session.Progress.CurrentHP,Is.EqualTo(session.Progress.MaxHP));
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

    private static T Ref<T>(Object owner,string name) where T:Object=>(T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    private static IEnumerator LoadTitle()
    {AsyncOperation operation=SceneManager.LoadSceneAsync("TitleScene");yield return Until(()=>operation.isDone);yield return null;yield return null;}
    private static IEnumerator Until(Func<bool> predicate)
    {float end=Time.realtimeSinceStartup+20;while(!predicate()){Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;}}
}
