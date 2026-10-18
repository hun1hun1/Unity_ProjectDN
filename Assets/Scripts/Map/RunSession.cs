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
        if (Application.isPlaying && (!Application.CanStreamedLevelBeLoaded(definition.mapScene) || !Application.CanStreamedLevelBeLoaded(definition.battleScene)))
            throw new InvalidOperationException("Both run scenes must be enabled in Build Settings.");
        RunDeckInventory.ResetForNewGame();
        RunRelicInventory.ResetForNewGame();
        Progress = next; finishedRaised = false;
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
        Publish();
        if (Progress.IsFinished) LoadScene(Progress.Definition.mapScene);
        return true;
    }

    public bool CompleteBattle(RunProgress expected, int floor, int node, int currentHP, int maxHP)
    {
        if (Progress == null || !ReferenceEquals(expected, Progress) || Progress.Phase != RunPhase.Rewards || IsTransitioning ||
            floor != Progress.FloorIndex || node != Progress.NodeIndex) return false;
        Progress.CaptureHealth(currentHP, maxHP);
        if (!Progress.CompleteNode(floor, node)) return false;
        Publish();
        LoadScene(Progress.Definition.mapScene);
        return true;
    }

    public bool CompleteContent(RunProgress expected, int floor, int node)
    {
        if (Progress == null || !ReferenceEquals(expected, Progress) || Progress.Phase != RunPhase.Content || IsTransitioning ||
            !Progress.CompleteNode(floor, node)) return false;
        Publish();
        return true;
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
