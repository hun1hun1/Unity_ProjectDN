using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class TitleMenuTests
{
    [Test]
    public void TitleSceneIsFirstAndHasFourWiredButtonsAndSharedSettings()
    {
        const string path="Assets/Scenes/TitleScene.unity";
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path),Is.Not.Null,"TitleScene must exist.");
        Assert.That(EditorBuildSettings.scenes.Where(s=>s.enabled).First().path,Is.EqualTo(path));
        Assert.That(EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path=="Assets/Scenes/MapScene.unity"),Is.True);
        Assert.That(EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path=="Assets/Scenes/BattleScene.unity"),Is.True);
        Scene scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            MonoBehaviour menu=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).Single(c=>c!=null&&c.GetType().Name=="TitleMenuUI");
            var state=new SerializedObject(menu);
            foreach(string field in new[]{"startButton","tutorialButton","optionsButton","quitButton"})
            {
                var button=(UnityEngine.UI.Button)state.FindProperty(field).objectReferenceValue;
                Assert.That(button,Is.Not.Null,field);
                Assert.That(button.onClick.GetPersistentEventCount(),Is.EqualTo(1));
                Assert.That(button.onClick.GetPersistentTarget(0),Is.EqualTo(menu));
            }
            SettingsUI settings=(SettingsUI)state.FindProperty("settingsUI").objectReferenceValue;
            var options=new SerializedObject(settings);
            Assert.That(options.FindProperty("requireBattle").boolValue,Is.False);
            var panel=(GameObject)options.FindProperty("panel").objectReferenceValue;
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(panel),Is.True);
            Assert.That(((RectTransform)panel.transform).anchorMax,Is.EqualTo(Vector2.one));
            Assert.That(panel.activeSelf,Is.False);
            Assert.That(state.FindProperty("runDefinition").objectReferenceValue,Is.Not.Null);
            Assert.That(state.FindProperty("tutorialPages").arraySize,Is.EqualTo(4));
        }
        finally{EditorSceneManager.CloseScene(scene,true);}
    }
}
