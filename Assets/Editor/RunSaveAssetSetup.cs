using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RunSaveAssetSetup : IPreprocessBuildWithReport
{
    public int callbackOrder=>0;
    public void OnPreprocessBuild(BuildReport report) { RefreshCatalog(); }
    [MenuItem("ProjectDN/Refresh Run Save Catalog")]
    public static void RefreshCatalog()
    {
        const string path="Assets/Resources/RunAssetCatalog.asset";
        if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
        var catalog=AssetDatabase.LoadAssetAtPath<RunAssetCatalog>(path);
        if(catalog==null){catalog=ScriptableObject.CreateInstance<RunAssetCatalog>();AssetDatabase.CreateAsset(catalog,path);}
        var content=new HashSet<ScriptableObject>();
        foreach(string guid in AssetDatabase.FindAssets("t:FloorMapDefinition"))
        {
            var floor=AssetDatabase.LoadAssetAtPath<FloorMapDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if(floor.guaranteedRelicData!=null)content.Add(floor.guaranteedRelicData);
            if(floor.nonBattleContents!=null)foreach(var item in floor.nonBattleContents)if(item?.contentData!=null)content.Add(item.contentData);
        }
        var assets=new List<(string key,ScriptableObject asset)>();
        foreach(string guid in AssetDatabase.FindAssets("t:ScriptableObject",new[]{"Assets"}))
            foreach(ScriptableObject asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<ScriptableObject>())
                if(asset is CardData || asset is CardDatabase || asset is RunDefinition || asset is BattleEncounterDefinition || asset is ConduitData || asset is MapEventDefinition || content.Contains(asset))
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,out string id,out long local);
                    assets.Add((id+":"+local,asset));
                    if (asset is ConduitData relic && relic.BattleStartBuff is StatusEffectData buff)
                    {
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(buff, out string buffId, out long buffLocal);
                        assets.Add((buffId + ":" + buffLocal, buff));
                    }
                }
        var state=new SerializedObject(catalog);var entries=state.FindProperty("entries");
        var sorted=assets.GroupBy(a=>a.key).Select(g=>g.First()).OrderBy(a=>a.key,StringComparer.Ordinal).ToArray();entries.arraySize=sorted.Length;
        for(int i=0;i<sorted.Length;i++)
        {entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue=sorted[i].key;entries.GetArrayElementAtIndex(i).FindPropertyRelative("asset").objectReferenceValue=sorted[i].asset;}
        state.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
    }

    [MenuItem("ProjectDN/Setup Run Checkpoints")]
    public static void CreateAssets()
    {
        RefreshCatalog();
        PatchScene("Assets/Scenes/TitleScene.unity",PatchTitle);
        PatchScene("Assets/Scenes/BattleScene.unity",PatchBattle);
    }
    private static void PatchScene(string path,Action<Scene> patch)
    {
        Scene scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try{patch(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
    private static void PatchTitle(Scene scene)
    {
        TitleMenuUI menu=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TitleMenuUI>(true)).Single();
        var state=new SerializedObject(menu);
        var start=(UnityEngine.UI.Button)state.FindProperty("startButton").objectReferenceValue;
        Transform existing=start.transform.parent.Find("ContinueButton");
        UnityEngine.UI.Button button=existing!=null?existing.GetComponent<UnityEngine.UI.Button>():UnityEngine.Object.Instantiate(start,start.transform.parent);
        button.name="ContinueButton";button.transform.SetSiblingIndex(start.transform.GetSiblingIndex());
        ((RectTransform)button.transform).anchoredPosition=((RectTransform)start.transform).anchoredPosition+new Vector2(0,88);
        button.GetComponentInChildren<TMP_Text>().text="이어서 하기";
        for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(button.onClick,i);
        UnityEventTools.AddPersistentListener(button.onClick,menu.ContinueGame);
        state.FindProperty("continueButton").objectReferenceValue=button;state.ApplyModifiedPropertiesWithoutUndo();
        button.gameObject.SetActive(false);EditorUtility.SetDirty(button);
    }
    private static void PatchBattle(Scene scene)
    {
        SettingsUI settings=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SettingsUI>(true)).Single();
        var state=new SerializedObject(settings);state.FindProperty("returnToTitle").boolValue=true;
        GameObject panel=(GameObject)state.FindProperty("panel").objectReferenceValue;
        SetText(panel.transform,"MainContent/Footer/QuitButton/Text","타이틀로 돌아가기",23);
        SetText(panel.transform,"QuitConfirmation/Dialog/Title","타이틀로 돌아가기",32);
        SetText(panel.transform,"QuitConfirmation/Dialog/Message","저장된 진행을 유지하고\n타이틀로 돌아가시겠습니까?",26);
        SetText(panel.transform,"QuitConfirmation/Dialog/ConfirmButton/Text","돌아가기",26);
        Transform dialog=panel.transform.Find("QuitConfirmation/Dialog");Transform existing=dialog.Find("NavigationError");
        TMP_Text error;
        if(existing!=null)error=existing.GetComponent<TMP_Text>();
        else
        {
            error=UnityEngine.Object.Instantiate(dialog.Find("Message").GetComponent<TMP_Text>(),dialog);error.name="NavigationError";
            RectTransform rect=(RectTransform)error.transform;rect.anchorMin=new Vector2(.04f,.015f);rect.anchorMax=new Vector2(.96f,.1f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        error.text="";error.fontSize=18;error.color=new Color(1,.65f,.45f);state.FindProperty("navigationError").objectReferenceValue=error;
        state.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetText(Transform root,string path,string value,int size)
    {
        TMP_Text text=root.Find(path).GetComponent<TMP_Text>();text.text=value;text.fontSize=size;
        EditorUtility.SetDirty(text);PrefabUtility.RecordPrefabInstancePropertyModifications(text);
    }
}

public sealed class RunSaveCatalogUpdates : AssetPostprocessor
{
    private static bool queued;
    private static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] oldPaths)
    {
        if(queued || !imported.Concat(deleted).Concat(moved).Any(path=>path.EndsWith(".asset",StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("Assets/Fonts/",StringComparison.OrdinalIgnoreCase) && path!="Assets/Resources/RunAssetCatalog.asset"))return;
        queued=true;
        EditorApplication.delayCall+=()=>{queued=false;if(!EditorApplication.isPlayingOrWillChangePlaymode)RunSaveAssetSetup.RefreshCatalog();};
    }
}
