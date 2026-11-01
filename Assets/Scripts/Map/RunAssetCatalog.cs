using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RunAssetCatalog : ScriptableObject
{
    [Serializable]
    public struct Entry { public string id; public ScriptableObject asset; }
    [SerializeField] private Entry[] entries=Array.Empty<Entry>();
    private Dictionary<string,ScriptableObject> byId;
    private Dictionary<ScriptableObject,string> byAsset;
    private static RunAssetCatalog runtimeCatalog;
    public static void SetRuntimeCatalog(RunAssetCatalog catalog) { runtimeCatalog=catalog; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeCatalog() { runtimeCatalog=null; }
    private void OnValidate() { byId=null;byAsset=null; }
    public static RunAssetCatalog Load()
    {
        if(runtimeCatalog!=null)return runtimeCatalog;
        var catalog=Resources.Load<RunAssetCatalog>("RunAssetCatalog");
        if(catalog==null)throw new InvalidOperationException("RunAssetCatalog is missing.");
        return catalog;
    }
    private void Initialize()
    {
        if(byId!=null)return;
        byId=new Dictionary<string,ScriptableObject>();byAsset=new Dictionary<ScriptableObject,string>();
        foreach(Entry entry in entries)
        {
            if(string.IsNullOrEmpty(entry.id)||entry.asset==null)continue;
            byId.Add(entry.id,entry.asset);byAsset.Add(entry.asset,entry.id);
        }
    }
    public string Key(ScriptableObject asset)
    {
        if(asset==null)return "";
        Initialize();
        if(!byAsset.TryGetValue(asset,out string id))throw new InvalidOperationException("Asset is not registered for run saves: "+asset.name);
        return id;
    }
    public T Resolve<T>(string id) where T:ScriptableObject
    {
        if(string.IsNullOrEmpty(id))return null;
        Initialize();
        if(!byId.TryGetValue(id,out ScriptableObject asset)||!(asset is T typed))
            throw new InvalidOperationException("Saved asset is missing or has a different type: "+id);
        return typed;
    }
}
