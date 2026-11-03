using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public partial class SettingsWindowTests
{
    [UnityTest]
    public IEnumerator SettingsClosesOtherViewersAndBlocksBattleButRestoresInputOnClose()
    {
        yield return new EnterPlayMode();
        yield return VerifyInput();
        yield return new ExitPlayMode();
    }
    private static IEnumerator VerifyInput()
    {
        yield return LoadBattle();
        SettingsUI ui=Object.FindFirstObjectByType<SettingsUI>();
        var platform=new FakeGameSettingsPlatform();ui.BindSettings(new GameSettingsService(platform));
        BattleManager battle=Object.FindFirstObjectByType<BattleManager>();
        CardUI hand=Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).First(card=>battle.CanUseCard(card.GetCardData()));
        DeckOverviewUI overview=Object.FindFirstObjectByType<DeckOverviewUI>();overview.Toggle();Assert.That(overview.IsOpen,Is.True);
        GameObject settingButton=Ref<Button>(ui,"settingButton").gameObject;
        EventSystem.current.SetSelectedGameObject(settingButton);
        Ref<Button>(ui,"settingButton").onClick.Invoke();
        Assert.That(ui.IsOpen,Is.True);Assert.That(overview.IsOpen,Is.False);
        Assert.That(hand.CanHover,Is.False);Assert.That(battle.CanUseCard(hand.GetCardData()),Is.False);Assert.That(battle.UseCard(hand),Is.False);
        battle.EndPlayerTurn();yield return null;Assert.That(battle.CanInspectCards,Is.True);
        Assert.That(CardInspectionUI.TryOpen(hand.GetCardData(),battle),Is.False);
        overview.Toggle();Assert.That(overview.IsOpen,Is.False);
        foreach(DeckPileButton pile in Object.FindObjectsByType<DeckPileButton>(FindObjectsSortMode.None))pile.GetComponent<Button>().onClick.Invoke();
        Assert.That(Ref<DeckListUI>(ui,"pileList").gameObject.activeSelf,Is.False);
        Ref<Button>(ui,"returnButton").onClick.Invoke();Assert.That(ui.IsOpen,Is.False);
        Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(settingButton),"Battle settings must retain focus restoration.");
        Assert.That(battle.CanUseCard(hand.GetCardData()),Is.False,"The closing frame must not reach the battle.");
        yield return null;yield return null;
        Assert.That(hand.CanHover,Is.True);Assert.That(battle.CanUseCard(hand.GetCardData()),Is.True);
        Assert.That(platform.Saves,Is.Zero,"Opening/closing alone must not write preferences.");
    }

    [UnityTest]
    public IEnumerator SlidersApplyImmediatelyVideoIsStagedAndResetRestoresAllControls()
    {
        yield return new EnterPlayMode();
        yield return VerifyControls();
        yield return new ExitPlayMode();
    }
    private static IEnumerator VerifyControls()
    {
        yield return LoadBattle();SettingsUI ui=Object.FindFirstObjectByType<SettingsUI>();
        var platform=new FakeGameSettingsPlatform();var service=new GameSettingsService(platform);ui.BindSettings(service);ui.Toggle();
        Ref<Slider>(ui,"masterSlider").value=.4f;Ref<Slider>(ui,"musicSlider").value=.3f;Ref<Slider>(ui,"effectsSlider").value=.2f;
        Assert.That(platform.Saves,Is.EqualTo(3));Assert.That(platform.Master,Is.EqualTo(.4f));
        Assert.That(Ref<TMP_Text>(ui,"masterValue").text,Is.EqualTo("40%"));
        TMP_Dropdown dropdown=Ref<TMP_Dropdown>(ui,"resolutionDropdown");dropdown.value=0;
        Toggle fullscreen=Ref<Toggle>(ui,"fullscreenToggle");
        // Newly enabled graphics need their first canvas frame before pointer raycasts.
        yield return null;
        Canvas.ForceUpdateCanvases();
        Rect knob=Ref<Slider>(ui,"masterSlider").handleRect.rect;
        Assert.That(knob.width,Is.EqualTo(knob.height).Within(.01f),"Slider thumb geometry should preserve its round shape.");
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,fullscreen.transform.Find("Label").position)};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Assert.That(hits,Is.Not.Empty);
        Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(fullscreen.gameObject),string.Join(", ",hits.Select(hit=>hit.gameObject.name))+" at "+pointer.position+"; depth="+fullscreen.GetComponent<Image>().depth);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
        Assert.That(fullscreen.isOn,Is.True,"The full-screen label should toggle the checkbox too.");
        Assert.That(platform.DisplayChanges,Is.Zero);
        Ref<Slider>(ui,"musicSlider").value=.25f;
        Assert.That(dropdown.value,Is.Zero,"Audio changes must preserve the pending display choice.");
        Assert.That(Ref<Toggle>(ui,"fullscreenToggle").isOn,Is.True);
        Ref<Button>(ui,"applyDisplayButton").onClick.Invoke();
        Assert.That(platform.DisplayChanges,Is.EqualTo(1));Assert.That(platform.CurrentResolution,Is.EqualTo(new GameResolution(1280,720)));Assert.That(platform.Fullscreen,Is.True);
        ui.Close();yield return null;yield return null;ui.Toggle();
        Assert.That(Ref<Slider>(ui,"masterSlider").value,Is.EqualTo(.4f));Assert.That(dropdown.value,Is.Zero);
        Ref<Button>(ui,"resetButton").onClick.Invoke();
        Assert.That(service.Current.masterVolume,Is.EqualTo(1));Assert.That(service.Current.musicVolume,Is.EqualTo(1));Assert.That(service.Current.effectsVolume,Is.EqualTo(1));
        Assert.That(platform.CurrentResolution,Is.EqualTo(new GameResolution(1920,1080)));Assert.That(platform.Fullscreen,Is.False);
        Assert.That(dropdown.value,Is.EqualTo(service.Resolutions.Count-1));Assert.That(Ref<Slider>(ui,"effectsSlider").value,Is.EqualTo(1));ui.Close();
    }

    [UnityTest]
    public IEnumerator EscapeClosesOnlyTheFrontmostWindowAndQuitRequiresConfirmation()
    {
        yield return new EnterPlayMode();
        yield return VerifyEscapeAndQuit();
        yield return new ExitPlayMode();
    }
    private static IEnumerator VerifyEscapeAndQuit()
    {
        yield return LoadBattle();SettingsUI ui=Object.FindFirstObjectByType<SettingsUI>();
        var platform=new FakeGameSettingsPlatform();ui.BindSettings(new GameSettingsService(platform));ui.Toggle();ui.ConfirmQuit();Assert.That(platform.QuitRequests,Is.Zero);
        Ref<Button>(ui,"quitButton").onClick.Invoke();Assert.That(Ref<GameObject>(ui,"quitConfirmation").activeSelf,Is.True);Assert.That(Ref<CanvasGroup>(ui,"mainControls").interactable,Is.False);
        ui.HandleEscape();Assert.That(ui.IsOpen,Is.True);Assert.That(Ref<GameObject>(ui,"quitConfirmation").activeSelf,Is.False);Assert.That(Ref<CanvasGroup>(ui,"mainControls").interactable,Is.True);
        // TMP initializes its fade runner in Start after the hidden panel is first enabled.
        yield return null;
        TMP_Dropdown dropdown=Ref<TMP_Dropdown>(ui,"resolutionDropdown");dropdown.Show();yield return null;Assert.That(dropdown.IsExpanded,Is.True);
        ui.HandleEscape();yield return Wait(.3f);Assert.That(dropdown.IsExpanded,Is.False);Assert.That(ui.IsOpen,Is.True);
        ui.HandleEscape();Assert.That(ui.IsOpen,Is.False);yield return null;yield return null;
        ui.Toggle();Ref<Button>(ui,"quitButton").onClick.Invoke();Ref<Button>(ui,"confirmQuitButton").onClick.Invoke();
        yield return Until(()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="TitleScene");
        Assert.That(platform.QuitRequests,Is.Zero);Assert.That(platform.Saves,Is.Zero);
    }

    [UnityTest]
    public IEnumerator ExternalHideDisableAndBattleEndReleaseSettingsInput()
    {
        yield return new EnterPlayMode();
        yield return VerifyCleanup();
        yield return new ExitPlayMode();
    }
    private static IEnumerator VerifyCleanup()
    {
        yield return LoadBattle();SettingsUI ui=Object.FindFirstObjectByType<SettingsUI>();var platform=new FakeGameSettingsPlatform();ui.BindSettings(new GameSettingsService(platform));
        CardUI hand=Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).First(card=>card.CanHover);
        ui.Toggle();ui.RequestQuit();Ref<GameObject>(ui,"panel").SetActive(false);yield return null;yield return null;
        Assert.That(SettingsUI.BlocksBattleInput,Is.False);Assert.That(hand.CanHover,Is.True);
        ui.Toggle();ui.enabled=false;yield return null;yield return null;Assert.That(SettingsUI.BlocksBattleInput,Is.False);
        Ref<Slider>(ui,"masterSlider").value=.25f;Assert.That(platform.Saves,Is.Zero);
        ui.enabled=true;ui.Toggle();Assert.That(ui.IsOpen,Is.True);Assert.That(Ref<CanvasGroup>(ui,"mainControls").interactable,Is.True);
        Object.FindFirstObjectByType<BattleManager>().EndBattle();Assert.That(ui.IsOpen,Is.False);yield return null;yield return null;Assert.That(SettingsUI.BlocksBattleInput,Is.False);
    }

    [UnityTest]
    public IEnumerator OptionsIconRotatesOnHoverWithoutMovingItsHitAreaAndRestoresOnExit()
    {yield return new EnterPlayMode();yield return VerifyOptionsRotation();yield return new ExitPlayMode();}
    private static IEnumerator VerifyOptionsRotation()
    {
        yield return LoadBattle();
        SettingsUI settings=Object.FindFirstObjectByType<SettingsUI>();
        settings.BindSettings(new GameSettingsService(new FakeGameSettingsPlatform()));
        Button button=Ref<Button>(settings,"settingButton");
        Component hover=button.GetComponent("ButtonHoverRotation");
        Assert.That(hover,Is.Not.Null,"OptionsButton needs a hover rotation component.");
        RectTransform visual=Ref<RectTransform>(hover,"visual");
        Assert.That(visual,Is.Not.Null);
        Assert.That(visual.parent,Is.EqualTo(button.transform));
        Assert.That(button.targetGraphic,Is.EqualTo(visual.GetComponent<UnityEngine.UI.Image>()));
        Assert.That(button.GetComponent<UnityEngine.UI.Image>().color.a,Is.Zero);
        Assert.That(button.GetComponent<UnityEngine.UI.Image>().raycastTarget,Is.True);
        Assert.That(visual.GetComponent<UnityEngine.UI.Image>().raycastTarget,Is.False);
        Quaternion original=visual.localRotation;
        Quaternion hitRotation=button.transform.localRotation;
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
        float oldTimeScale=Time.timeScale;
        try
        {
            Time.timeScale=0;
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
            yield return Wait(.25f);
            Assert.That(Quaternion.Angle(visual.localRotation,original*Quaternion.Euler(0,0,45)),Is.LessThan(.1f));
            Assert.That(Quaternion.Angle(button.transform.localRotation,hitRotation),Is.LessThan(.01f));
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerExitHandler);
            yield return Wait(.25f);
            Assert.That(Quaternion.Angle(visual.localRotation,original),Is.LessThan(.1f));
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
            yield return Wait(.25f);
            button.onClick.Invoke();
            Assert.That(settings.IsOpen,Is.True,"Hover must preserve the existing settings callback.");
            yield return Wait(.25f);
            Assert.That(Quaternion.Angle(visual.localRotation,original),Is.LessThan(.1f));
            settings.Close();yield return null;yield return null;
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
            yield return Wait(.25f);
            button.gameObject.SetActive(false);
            Assert.That(Quaternion.Angle(visual.localRotation,original),Is.LessThan(.01f));
        }
        finally{Time.timeScale=oldTimeScale;}
    }

    private static T Ref<T>(Object owner,string field) where T:Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
    private static IEnumerator LoadBattle()
    {
        AsyncOperation load=SceneManager.LoadSceneAsync("BattleScene");yield return Until(()=>load.isDone);
        yield return Until(()=>Object.FindFirstObjectByType<BattleManager>()!=null && Object.FindFirstObjectByType<BattleManager>().CanInspectCards && Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Any(card=>card.CanHover));
    }
    private static IEnumerator Until(Func<bool> ready)
    {float deadline=Time.realtimeSinceStartup+15;while(!ready()){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}}
    private static IEnumerator Wait(float seconds)
    {float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
}
