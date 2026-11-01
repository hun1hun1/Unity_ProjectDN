using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class SettingsWindowTests
{
    [Test]
    public void SceneHasLargeSettingsWindowAndExistingButtonCallback()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity", OpenSceneMode.Additive);
        try
        {
            MonoBehaviour settings = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .FirstOrDefault(component => component != null && component.GetType().Name == "SettingsUI");
            Assert.That(settings, Is.Not.Null, "BattleScene needs the large settings window and SettingButton wiring.");
            var state = new SerializedObject(settings);
            GameObject panel = (GameObject)state.FindProperty("panel").objectReferenceValue;
            RectTransform rect = (RectTransform)panel.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(1,.9f)));
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(panel), Is.True);
            foreach (string field in new[] {"mainControls","masterSlider","musicSlider","effectsSlider","fullscreenToggle","resolutionDropdown","applyDisplayButton","resetButton","returnButton","quitButton","closeButton","quitConfirmation"})
                Assert.That(state.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            TMPro.TMP_Dropdown dropdown=(TMPro.TMP_Dropdown)state.FindProperty("resolutionDropdown").objectReferenceValue;
            UnityEngine.UI.Toggle item=dropdown.itemText.GetComponentInParent<UnityEngine.UI.Toggle>(true);
            foreach(Color tint in new[]{item.colors.normalColor,item.colors.highlightedColor,item.colors.selectedColor})
            {
                float background=(item.targetGraphic.color*tint).linear.grayscale;
                float foreground=dropdown.itemText.color.linear.grayscale;
                float contrast=(Mathf.Max(background,foreground)+.05f)/(Mathf.Min(background,foreground)+.05f);
                Assert.That(contrast,Is.GreaterThanOrEqualTo(4.5f),"Resolution choices need readable text in every interactive state.");
            }
            UnityEngine.UI.Button button = (UnityEngine.UI.Button)state.FindProperty("settingButton").objectReferenceValue;
            Assert.That(button.name, Is.EqualTo("SettingButton"));
            Assert.That(Enumerable.Range(0,button.onClick.GetPersistentEventCount()).Any(index =>
                button.onClick.GetPersistentTarget(index)==settings && button.onClick.GetPersistentMethodName(index)=="Toggle"), Is.True);
        }
        finally { EditorSceneManager.CloseScene(scene,true); }
    }
}
