using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class RunSession : MonoBehaviour
{
    public static RunSession Instance { get; private set; }
    public RunProgress Progress { get; private set; }
    public bool IsTransitioning { get; private set; }
    public event Action Changed;
    public event Action<RunPhase> RunFinished;
    private bool finishedRaised;
    private RunSaveData checkpoint;
    public string SaveError { get; private set; }
    public RunRewardData SavedRewards => checkpoint?.rewards;
    public string SavedContentState => checkpoint?.contentState ?? "";
    public bool CanCommit => !IsTransitioning && Progress!=null && !Progress.IsFinished;

    public static RunSession GetOrCreate()
    {
        if (Instance == null) new GameObject("RunSession").AddComponent<RunSession>();
        return Instance;
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    public void StartNewRun(RunDefinition definition, int seed, IReadOnlyCollection<MapContentType> supportedContent = null)
    {
        if (IsTransitioning) throw new InvalidOperationException("Cannot replace a run during a scene transition.");
        // Validate/build first: a broken definition must not erase the previous run or inventory.
        var available = new List<MapContentType> { MapContentType.NormalBattle, MapContentType.EliteBattle, MapContentType.BossBattle };
        if (supportedContent != null) available.AddRange(supportedContent);
        var next = new RunProgress(definition, seed, available);
        int startingGold = Mathf.Max(0, definition.startingGold);
        if (Application.isPlaying && (!Application.CanStreamedLevelBeLoaded(definition.mapScene) || !Application.CanStreamedLevelBeLoaded(definition.battleScene)))
            throw new InvalidOperationException("Both run scenes must be enabled in Build Settings.");
        RunSaveData initial=null;
        if(Application.isPlaying)
        {
            initial = new RunSaveData
            {
                progress = next.Capture(RunAssetCatalog.Load()),
                inventory = new RunInventoryData { gold = startingGold }
            };
            RunSaveCodec.Validate(initial,RunAssetCatalog.Load());
            if(!RunSaves.Store.TryWrite(initial,out string error))throw new InvalidOperationException("Could not save the new run: "+error);
        }
        RunDeckInventory.ResetForNewGame();
        RunRelicInventory.ResetForNewGame();
        RunGoldInventory.Add(startingGold);
        Progress = next; finishedRaised = false;checkpoint=initial;SaveError=null;
        Publish();
        if (Application.isPlaying && SceneManager.GetActiveScene().name != definition.mapScene) LoadScene(definition.mapScene);
    }

    public bool SelectOption(int index)
    {
        if (IsTransitioning || Progress == null || Progress.Phase != RunPhase.Map || Progress.CurrentNode == null ||
            index < 0 || index >= Progress.CurrentNode.Choices.Count) return false;
        MapChoice choice = Progress.CurrentNode.Choices[index];
        if (choice.IsBattle && !Application.CanStreamedLevelBeLoaded(Progress.Definition.battleScene)) return false;
        if (!Progress.TrySelectOption(index)) return false;
        RunSaveData previous=checkpoint;
        if(!SaveCheckpoint()){Progress.UndoNodeEntry();checkpoint=previous;Publish();return false;}
        if (choice.IsBattle) LoadScene(Progress.Definition.battleScene);
        else Publish();
        return true;
    }

    public bool RecordBattleResult(RunProgress expected, int floor, int node, BattleResult result, int currentHP, int maxHP)
    {
        if (Progress == null || !ReferenceEquals(expected, Progress) || Progress.Phase != RunPhase.Battle ||
            floor != Progress.FloorIndex || node != Progress.NodeIndex || result == BattleResult.None) return false;
        if (!Progress.RecordBattleResult(result)) return false;
        Progress.CaptureHealth(currentHP, maxHP);
        if(Progress.IsFinished && !ClearCheckpoint()){Publish();return false;}
        Publish();
        if (Progress.IsFinished) LoadScene(Progress.Definition.mapScene);
        return true;
    }

    public bool CompleteBattle(RunProgress expected, int floor, int node, int currentHP, int maxHP)
    {
        if (Progress == null || !ReferenceEquals(expected, Progress) || Progress.Phase != RunPhase.Rewards || IsTransitioning ||
            floor != Progress.FloorIndex || node != Progress.NodeIndex) return false;
        RunProgressData before=Application.isPlaying?Progress.Capture(RunAssetCatalog.Load()):null;
        RunSaveData previous=checkpoint;
        Progress.CaptureHealth(currentHP, maxHP);
        if (!Progress.CompleteNode(floor, node)) return false;
        if(!(Progress.IsFinished?ClearCheckpoint():SaveCheckpoint()))
        {
            if(before!=null)Progress.RestoreCheckpointInPlace(before,RunAssetCatalog.Load());
            checkpoint=previous;Publish();return false;
        }
        Publish();
        LoadScene(Progress.Definition.mapScene);
        return true;
    }

    public bool CompleteContent(RunProgress expected, int floor, int node)
    {
        if (Progress == null || !ReferenceEquals(expected, Progress) || Progress.Phase != RunPhase.Content || IsTransitioning ||
            floor!=Progress.FloorIndex || node!=Progress.NodeIndex) return false;
        RunProgressData before=Application.isPlaying?Progress.Capture(RunAssetCatalog.Load()):null;RunSaveData previous=checkpoint;
        if(!Progress.CompleteNode(floor,node))return false;
        if(!(Progress.IsFinished?ClearCheckpoint():SaveCheckpoint()))
        {
            if(before!=null)Progress.RestoreCheckpointInPlace(before,RunAssetCatalog.Load());
            checkpoint=previous;Publish();return false;
        }
        Publish();
        return true;
    }

    public bool SaveCheckpoint(RunRewardData rewards=null,string contentState="")
    {
        if(Progress==null || Progress.IsFinished || IsTransitioning)return false;
        if(!Application.isPlaying)return true;
        try
        {
            var next=RunSaveCodec.Capture(Progress,RunAssetCatalog.Load(),rewards,contentState);
            RunSaveCodec.Validate(next,RunAssetCatalog.Load());
            checkpoint=next;
            if(!RunSaves.Store.TryWrite(next,out string error)){SaveError=error;return false;}
            SaveError=null;return true;
        }
        catch(Exception error) when(error is InvalidOperationException || error is ArgumentException)
        {SaveError=error.Message;return false;}
    }
    public bool SaveRewards(RunProgress expected,int floor,int node,RunRewardData rewards)
    {
        if(!CanCommit || !ReferenceEquals(expected,Progress) || Progress.Phase!=RunPhase.Rewards ||
            floor!=Progress.FloorIndex || node!=Progress.NodeIndex)return false;
        RunSaveData previous=checkpoint;
        if(SaveCheckpoint(rewards))return true;
        if(previous!=null && previous.hasRewards)
        {
            RunSaveCodec.RestoreInventory(previous.inventory,RunAssetCatalog.Load());
            Progress.RestoreCheckpointInPlace(previous.progress,RunAssetCatalog.Load());checkpoint=previous;
        }
        return false;
    }
    public bool CommitContent(RunNodeVisit visit,Action acquisition)
    {
        if(visit==null)return false;
        RunProgress expected=visit.Progress;int floor=visit.Floor,node=visit.Node;MapContentHandler handler=visit.Handler;
        if(!CanCommit || !ReferenceEquals(expected,Progress) || Progress.Phase!=RunPhase.Content ||
            floor!=Progress.FloorIndex || node!=Progress.NodeIndex || handler==null || !handler.isActiveAndEnabled ||
            handler.ContentType!=Progress.CurrentNode.SelectedChoice.Type || acquisition==null)return false;
        var catalog=RunAssetCatalog.Load();
        RunInventoryData before=RunSaveCodec.CaptureInventory(catalog);
        RunProgressData progressBefore=Progress.Capture(catalog);
        string contentBefore=handler.CaptureState();RunSaveData previous=checkpoint;
        try
        {
            acquisition();
            if(SaveCheckpoint(contentState:handler.CaptureState()))return true;
        }
        catch(Exception error){SaveError=error.Message;}
        // A failed commit restores both owned resources and the handler's receipt/stock state.
        RunSaveCodec.RestoreInventory(before,catalog);
        Progress.CaptureHealth(progressBefore.hp,progressBefore.maxHP);
        handler.RestoreState(contentBefore);checkpoint=previous;
        return false;
    }
    public bool ResumeSavedRun()
    {
        if(IsTransitioning)return false;
        if(!RunSaves.TryLoad(out RunSaveData saved,out string error)){SaveError=error??"No saved run.";return false;}
        try
        {
            var catalog=RunAssetCatalog.Load();var restored=RunProgress.Restore(saved.progress,catalog);
            string scene=restored.Phase==RunPhase.Battle || restored.Phase==RunPhase.Rewards?restored.Definition.battleScene:restored.Definition.mapScene;
            if(Application.isPlaying && !Application.CanStreamedLevelBeLoaded(scene))throw new InvalidOperationException("Saved scene is not enabled.");
            RunSaveCodec.RestoreInventory(saved.inventory,catalog);
            checkpoint=saved;Progress=restored;finishedRaised=false;SaveError=null;
            Publish();LoadScene(scene);return true;
        }
        catch(Exception exception) when(exception is InvalidOperationException || exception is ArgumentException)
        {SaveError=exception.Message;return false;}
    }
    public bool ReturnToTitle()
    {
        if(IsTransitioning)return false;
        if(Application.isPlaying && !Application.CanStreamedLevelBeLoaded("TitleScene")){SaveError="TitleScene is not enabled.";return false;}
        if(checkpoint!=null && !RunSaves.Store.TryWrite(checkpoint,out string error)){SaveError=error;return false;}
        if(Progress!=null && Progress.IsFinished && !ClearCheckpoint())return false;
        // Invalidate callbacks before disabling scene components or starting the transition.
        Progress=null;SaveError=null;
        BattleManager battle=FindFirstObjectByType<BattleManager>();if(battle!=null)battle.enabled=false;
        LoadScene("TitleScene");return true;
    }
    private bool ClearCheckpoint()
    {
        checkpoint=null;
        if(!Application.isPlaying)return true;
        bool deleted=RunSaves.Store.TryDelete(out string error);SaveError=error;return deleted;
    }

    private void Publish()
    {
        Changed?.Invoke();
        if (Progress == null || !Progress.IsFinished || finishedRaised) return;
        finishedRaised = true;
        RunFinished?.Invoke(Progress.Phase);
    }
    private void LoadScene(string scene)
    {
        if (!Application.isPlaying || IsTransitioning) return;
        if (!Application.CanStreamedLevelBeLoaded(scene)) throw new InvalidOperationException($"Run scene is not in Build Settings: {scene}");
        IsTransitioning = true;
        Publish();
        StartCoroutine(Transition(scene));
    }
    private IEnumerator Transition(string scene)
    {
        try { yield return SceneManager.LoadSceneAsync(scene); }
        finally { IsTransitioning = false; Publish(); }
    }
}
