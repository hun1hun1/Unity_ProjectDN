using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class UIButtonSoundTests
{
    [TestCase("TitleScene")]
    [TestCase("MapScene")]
    [TestCase("BattleScene")]
    public void EachSceneRoutesOnlyItsMenuAndSettingsButtons(string sceneName)
    {
        Scene scene = EditorSceneManager.OpenScene($"Assets/Scenes/{sceneName}.unity", OpenSceneMode.Additive);
        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            var settings = roots.SelectMany(root => root.GetComponentsInChildren<SettingsUI>(true)).Single();
            var panel = (GameObject)new SerializedObject(settings).FindProperty("panel").objectReferenceValue;
            var settingsButtons = panel.GetComponentsInChildren<Button>(true);
            Assert.That(settingsButtons.Length, Is.GreaterThan(0));
            foreach (Button button in settingsButtons)
                AssertClips(button, "button-hover", "button-click");

            Button[] titleButtons = System.Array.Empty<Button>();
            if (sceneName == "TitleScene")
            {
                var title = roots.SelectMany(root => root.GetComponentsInChildren<TitleMenuUI>(true)).Single();
                var menu = new SerializedObject(title);
                titleButtons = new[] { "startButton", "continueButton", "tutorialButton", "optionsButton",
                    "quitButton", "cancelQuitButton", "confirmQuitButton" }
                    .Select(field => (Button)menu.FindProperty(field).objectReferenceValue).ToArray();
                foreach (Button button in titleButtons)
                    AssertClips(button, "title-button-hover", "title-button-click");
            }

            var otherButtons = roots.SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .Except(settingsButtons).Except(titleButtons);
            foreach (Button button in otherButtons)
                Assert.That(button.GetComponent("UIButtonSound"), Is.Null, button.name);

            Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<AudioListener>())
                .Count(listener => listener.isActiveAndEnabled), Is.EqualTo(1));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest]
    public IEnumerator HoverAndClosingClickPlayThroughTheEffectsSource()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("TitleScene");
        var title = Object.FindFirstObjectByType<TitleMenuUI>();
        var settings = Object.FindFirstObjectByType<SettingsUI>();
        var close = (Button)new SerializedObject(settings).FindProperty("closeButton").objectReferenceValue;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        title.OpenOptions();
        Assert.That(settings.IsOpen, Is.True);

        close.interactable = false;
        ExecuteEvents.Execute(close.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        Assert.That(Object.FindFirstObjectByType<SettingsAudioSource>(), Is.Null);
        close.interactable = true;
        close.enabled = false;
        ExecuteEvents.Execute(close.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        Assert.That(Object.FindFirstObjectByType<SettingsAudioSource>(), Is.Null);
        close.enabled = true;
        ExecuteEvents.Execute(close.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        var routing = Object.FindFirstObjectByType<SettingsAudioSource>();
        Assert.That(routing, Is.Not.Null, "Hovering an enabled option button should start the sound.");
        var source = routing.GetComponent<AudioSource>();
        Assert.That(source.isPlaying, Is.True);

        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(source.isPlaying, Is.False);
        ExecuteEvents.Execute(close.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Assert.That(settings.IsOpen, Is.False);
        Assert.That(source.isPlaying, Is.True, "Closing the window must not stop or skip its click.");

        var service = new GameSettingsService(new FakeGameSettingsPlatform());
        routing.BindSettings(service);
        service.SetVolumes(.8f, .6f, .25f);
        Assert.That(source.volume, Is.EqualTo(.25f).Within(.0001f));
        service.SetVolumes(.8f, .6f, 0f);
        Assert.That(source.volume, Is.Zero);
        yield return new ExitPlayMode();
    }

    private static void AssertClips(Button button, string hoverName, string clickName)
    {
        Assert.That(button, Is.Not.Null);
        var sound = button.GetComponent("UIButtonSound");
        Assert.That(sound, Is.Not.Null, $"{button.name} needs button sounds.");
        var clips = new SerializedObject(sound);
        Assert.That(clips.FindProperty("hoverClip").objectReferenceValue?.name, Is.EqualTo(hoverName), button.name);
        Assert.That(clips.FindProperty("clickClip").objectReferenceValue?.name, Is.EqualTo(clickName), button.name);
    }
}
