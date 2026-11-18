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
    private SceneFogTransition fogTransition;
    internal bool BlocksSceneInput => fogTransition?.IsVisible == true;
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

    public void StartNewRun(RunDefinition definition, int seed, IReadOnlyCollection<MapContentType> supportedContent = null,
        AmmoCapacityRelic startingConduit = null)
    {
        if (IsTransitioning) throw new InvalidOperationException("Cannot replace a run during a scene transition.");
        // Validate/build first: a broken definition must not erase the previous run or inventory.
        var available = new List<MapContentType> { MapContentType.NormalBattle, MapContentType.EliteBattle, MapContentType.BossBattle };
        available.Add(MapContentType.Unknown);
        if (supportedContent != null) available.AddRange(supportedContent);
        var next = new RunProgress(definition, seed, available);
        int startingGold = Mathf.Max(0, definition.startingGold);
        if (startingConduit != null && !startingConduit.HasEffect)
            throw new InvalidOperationException("The starting conduit needs a valid effect.");
        if (Application.isPlaying && (!Application.CanStreamedLevelBeLoaded(definition.mapScene) || !Application.CanStreamedLevelBeLoaded(definition.battleScene)))
            throw new InvalidOperationException("Both run scenes must be enabled in Build Settings.");
        RunSaveData initial=null;
        if(Application.isPlaying)
        {
            RunAssetCatalog catalog = RunAssetCatalog.Load();
            var inventory = new RunInventoryData { gold = startingGold };
            if (startingConduit != null)
                inventory.relics = new[] { RunRelicInventory.CaptureStartingConduit(startingConduit, catalog) };
            initial = new RunSaveData { progress = next.Capture(catalog), inventory = inventory };
            RunSaveCodec.Validate(initial, catalog);
            if(!RunSaves.Store.TryWrite(initial,out string error))throw new InvalidOperationException("Could not save the new run: "+error);
        }
        RunDeckInventory.ResetForNewGame();
        RunRelicInventory.ResetForNewGame();
        if (startingConduit != null) startingConduit.Acquire();
        RunGoldInventory.Add(startingGold);
        Progress = next; finishedRaised = false;checkpoint=initial;SaveError=null;
        Publish();
        if (Application.isPlaying && SceneManager.GetActiveScene().name != definition.mapScene)
            LoadScene(definition.mapScene, useFog: true);
    }

    public bool SelectOption(int index) => SelectOption(index, null);

    internal bool SelectOption(int index, Func<MapContentType, bool> canDispatch,
        Func<MapChoice, IEnumerator> reveal = null, Action<MapChoice> openContent = null)
    {
        if (IsTransitioning || Progress == null || !Progress.TrySelectOption(index, Available)) return false;
        RunSaveData previous = checkpoint;
        if (!SaveCheckpoint()) { Progress.UndoNodeEntry(); checkpoint = previous; Publish(); return false; }
        MapNode selectedNode = Progress.CurrentNode;
        if (reveal != null && Application.isPlaying)
        {
            IsTransitioning = true;
            Publish();
            StartCoroutine(EnterAfterReveal(Progress, selectedNode, reveal, openContent));
        }
        else EnterSelectedNode(selectedNode.SelectedChoice, openContent);
        return true;

        bool Available(MapContentType type)
        {
            if (type == MapContentType.NormalBattle || type == MapContentType.EliteBattle || type == MapContentType.BossBattle)
                return Application.CanStreamedLevelBeLoaded(Progress.Definition.battleScene);
            return canDispatch?.Invoke(type) ?? true;
        }
    }

    private IEnumerator EnterAfterReveal(RunProgress expected, MapNode node, Func<MapChoice, IEnumerator> reveal,
        Action<MapChoice> openContent)
    {
        try
        {
            yield return reveal(node.SelectedChoice);
        }
        finally
        {
            IsTransitioning = false;
        }

        if (ReferenceEquals(Progress, expected) && ReferenceEquals(expected.CurrentNode, node))
            EnterSelectedNode(node.SelectedChoice, openContent);
        else
            Publish();
    }

    private void EnterSelectedNode(MapChoice choice, Action<MapChoice> openContent)
    {
        if (choice.IsBattle)
        {
            LoadScene(Progress.Definition.battleScene, useFog: true);
        }
        else
        {
            Publish();
            openContent?.Invoke(choice);
        }
    }

    public bool RecordBattleResult(RunProgress expected, int floor, int node, BattleResult result, int currentHP, int maxHP,
        bool keepBattleSceneOnDefeat = false)
    {
        if (Progress == null || !ReferenceEquals(expected, Progress) || Progress.Phase != RunPhase.Battle ||
            floor != Progress.FloorIndex || node != Progress.NodeIndex || result == BattleResult.None) return false;
        if (!Progress.RecordBattleResult(result)) return false;
        if (result == BattleResult.Victory)
            currentHP = (int)Math.Min(maxHP, (long)currentHP + RunRelicInventory.BattleVictoryHeal);
        Progress.CaptureHealth(currentHP, maxHP);
        if(Progress.IsFinished && !ClearCheckpoint()){Publish();return false;}
        Publish();
        if (Progress.IsFinished && !(keepBattleSceneOnDefeat && result == BattleResult.Defeat))
            LoadScene(Progress.Definition.mapScene);
        return true;
    }

    public bool CompleteBattle(RunProgress expected, int floor, int node, int currentHP, int maxHP)
    {
        if (Progress == null || !ReferenceEquals(expected, Progress) || Progress.Phase != RunPhase.Rewards || IsTransitioning ||
            floor != Progress.FloorIndex || node != Progress.NodeIndex) return false;
        RunAssetCatalog catalog = Application.isPlaying ? RunAssetCatalog.Load() : null;
        RunProgressData before = catalog != null ? Progress.Capture(catalog) : null;
        RunSaveData previous = checkpoint;
        AmmoCapacityRelic conduit = Progress.GetEliteVictoryConduit();
        RunRelicData[] relicsBefore = catalog != null && conduit != null ? RunRelicInventory.Capture(catalog) : null;
        Progress.CaptureHealth(currentHP, maxHP);
        if (!Progress.CompleteNode(floor, node, conduit)) return false;
        if (conduit != null) conduit.Acquire();
        if (!(Progress.IsFinished ? ClearCheckpoint() : SaveCheckpoint()))
        {
            if (before != null) Progress.RestoreCheckpointInPlace(before, catalog);
            if (relicsBefore != null) RunRelicInventory.Restore(relicsBefore, catalog);
            checkpoint = previous;
            Publish();
            return false;
        }
        Publish();
        LoadScene(Progress.Definition.mapScene);
        return true;
    }

    public bool CompleteContent(RunProgress expected, int floor, int node) =>
        CompleteContent(expected, floor, node, 0);

    internal static long GetRestFlatRecovery(int flatRecovery) =>
        (long)Mathf.Max(0, flatRecovery) + RunRelicInventory.RestRecoveryBonus;

    internal bool CompleteRest(RunNodeVisit visit, int recoveryPercent, int flatRecovery)
    {
        if (!IsRestVisit(visit)) return false;

        int missingHP = Progress.MaxHP - Progress.CurrentHP;
        int proportionalRecovery = (int)Math.Ceiling(missingHP * (decimal)Mathf.Clamp(recoveryPercent, 0, 100) / 100);
        int recovery = (int)Math.Min(missingHP, proportionalRecovery + GetRestFlatRecovery(flatRecovery));
        return CompleteContent(visit.Progress, visit.Floor, visit.Node, recovery);
    }

    internal bool CompleteRestUpgrade(RunNodeVisit visit, RunDeckInventory.CardSelection selection, CardData replacement)
    {
        if (!IsRestVisit(visit) || replacement == null || RunDeckInventory.GetUpgradeResult(selection.Card) != replacement)
            return false;

        bool completed = RunDeckInventory.TryReplaceCard(selection, replacement,
            () => SaveContentCompletion(visit.Progress, visit.Floor, visit.Node, 0));
        Publish();
        return completed;
    }

    internal bool CompleteContentWithAcquisition(RunNodeVisit visit, Action acquisition)
    {
        if (!IsContentVisit(visit) || acquisition == null) return false;

        var catalog = RunAssetCatalog.Load();
        RunInventoryData inventory = RunSaveCodec.CaptureInventory(catalog);
        bool completed;
        try
        {
            acquisition();
            completed = SaveContentCompletion(visit.Progress, visit.Floor, visit.Node, 0);
        }
        catch (Exception error)
        {
            SaveError = error.Message;
            completed = false;
        }
        if (!completed) RunSaveCodec.RestoreInventory(inventory, catalog);
        Publish();
        return completed;
    }

    private bool IsContentVisit(RunNodeVisit visit) =>
        visit != null && IsCurrentContent(visit.Progress, visit.Floor, visit.Node) &&
        visit.Handler != null && visit.Handler.isActiveAndEnabled &&
        visit.Handler.ContentType == Progress.CurrentNode.SelectedChoice.Type;

    private bool IsRestVisit(RunNodeVisit visit) =>
        CanCommit && visit != null && ReferenceEquals(visit.Progress, Progress) &&
        visit.Floor == Progress.FloorIndex && visit.Node == Progress.NodeIndex &&
        Progress.Phase == RunPhase.Content && Progress.CurrentNode.SelectedChoice.Type == MapContentType.Rest &&
        visit.Handler != null && visit.Handler.isActiveAndEnabled && visit.Handler.ContentType == MapContentType.Rest;

    private bool CompleteContent(RunProgress expected, int floor, int node, int recovery)
    {
        if (!IsCurrentContent(expected, floor, node)) return false;

        bool completed = SaveContentCompletion(expected, floor, node, recovery);
        Publish();
        return completed;
    }

    private bool IsCurrentContent(RunProgress expected, int floor, int node) =>
        CanCommit && ReferenceEquals(expected, Progress) && Progress.Phase == RunPhase.Content &&
        floor == Progress.FloorIndex && node == Progress.NodeIndex;

    private bool SaveContentCompletion(RunProgress expected, int floor, int node, int recovery)
    {
        if (!IsCurrentContent(expected, floor, node)) return false;

        RunAssetCatalog catalog = Application.isPlaying ? RunAssetCatalog.Load() : null;
        RunProgressData before = catalog != null ? Progress.Capture(catalog) : null;
        RunSaveData previous = checkpoint;
        if (!Progress.CompleteNode(floor, node)) return false;
        if (recovery > 0) Progress.CaptureHealth(Progress.CurrentHP + recovery, Progress.MaxHP);
        if (Progress.IsFinished ? ClearCheckpoint() : SaveCheckpoint()) return true;

        if (before != null) Progress.RestoreCheckpointInPlace(before, catalog);
        checkpoint = previous;
        return false;
    }

    public bool SaveCheckpoint(RunRewardData rewards = null, string contentState = "") =>
        !IsTransitioning && SaveCheckpointCore(rewards, contentState);

    // The destination has initialized the encounter and starting inventory under the fog.
    internal bool SaveBattleEntryCheckpoint(RunProgress expected) =>
        ReferenceEquals(Progress, expected) && expected?.Phase == RunPhase.Battle &&
        SaveCheckpointCore(null, "");

    private bool SaveCheckpointCore(RunRewardData rewards, string contentState)
    {
        if (Progress == null || Progress.IsFinished) return false;
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
    private void LoadScene(string scene, bool useFog = false)
    {
        if (!Application.isPlaying || IsTransitioning) return;
        if (!Application.CanStreamedLevelBeLoaded(scene)) throw new InvalidOperationException($"Run scene is not in Build Settings: {scene}");
        IsTransitioning = true;
        Publish();
        StartCoroutine(Transition(scene, useFog));
    }

    private IEnumerator Transition(string scene, bool useFog)
    {
        try
        {
            if (!useFog)
            {
                yield return SceneManager.LoadSceneAsync(scene);
                yield break;
            }

            fogTransition ??= new SceneFogTransition(transform);
            fogTransition.Show();
            yield return fogTransition.Fade(0f, 1f, 0.32f);
            // Present a fully covered frame before the scene activation can replace the canvas.
            yield return null;

            double loadStartedAt = Time.realtimeSinceStartupAsDouble;
            AsyncOperation load = SceneManager.LoadSceneAsync(scene);
            while (!load.isDone)
            {
                fogTransition.UpdateLoading(Time.realtimeSinceStartupAsDouble - loadStartedAt);
                yield return null;
            }

            yield return null;
            yield return fogTransition.Fade(1f, 0f, 0.38f);
        }
        finally
        {
            if (useFog) fogTransition?.Hide();
            IsTransitioning = false;
            Publish();
        }
    }
}
