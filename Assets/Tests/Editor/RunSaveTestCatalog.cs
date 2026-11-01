using UnityEditor;
using UnityEngine;

public static class RunSaveTestCatalog
{
    public static RunAssetCatalog Register(ScriptableObject asset)
    {
        var catalog=Object.Instantiate(RunAssetCatalog.Load());
        var state=new SerializedObject(catalog);var entries=state.FindProperty("entries");int slot=entries.arraySize++;
        entries.GetArrayElementAtIndex(slot).FindPropertyRelative("id").stringValue="fixture:"+asset.GetInstanceID();
        entries.GetArrayElementAtIndex(slot).FindPropertyRelative("asset").objectReferenceValue=asset;
        state.ApplyModifiedPropertiesWithoutUndo();RunAssetCatalog.SetRuntimeCatalog(catalog);return catalog;
    }
}
