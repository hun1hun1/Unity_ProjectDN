using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunCheckpointWiringTests
{
    [Test]
    public void TitleHasAContinueButtonBoundToTheMenu()
    {
        Scene scene=EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity",OpenSceneMode.Additive);
        try
        {
            TitleMenuUI menu=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TitleMenuUI>(true)).Single();
            var field=new SerializedObject(menu).FindProperty("continueButton");
            Assert.That(field,Is.Not.Null,"The title needs a conditional Continue button.");
            var button=(UnityEngine.UI.Button)field.objectReferenceValue;
            Assert.That(button,Is.Not.Null);
            Assert.That(button.GetComponentInChildren<TMP_Text>().text,Is.EqualTo("이어서 하기"));
            Assert.That(button.onClick.GetPersistentTarget(0),Is.EqualTo(menu));
            Assert.That(button.onClick.GetPersistentMethodName(0),Is.EqualTo("ContinueGame"));
        }
        finally{EditorSceneManager.CloseScene(scene,true);}
    }
    [Test]
    public void BattleSettingsReturnToTitleInsteadOfQuittingTheApplication()
    {
        Scene scene=EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity",OpenSceneMode.Additive);
        try
        {
            SettingsUI settings=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SettingsUI>(true)).Single();
            var state=new SerializedObject(settings);
            var button=(UnityEngine.UI.Button)state.FindProperty("quitButton").objectReferenceValue;
            Assert.That(button.GetComponentInChildren<TMP_Text>().text,Is.EqualTo("타이틀로 돌아가기"));
            Assert.That(state.FindProperty("returnToTitle").boolValue,Is.True);
            Assert.That(state.FindProperty("navigationError").objectReferenceValue,Is.Not.Null);
        }
        finally{EditorSceneManager.CloseScene(scene,true);}
    }
}
