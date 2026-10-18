using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class MapSceneTests
{
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (RunSession.Instance != null)
        {
            if (Application.isPlaying) Object.Destroy(RunSession.Instance.gameObject);
            else Object.DestroyImmediate(RunSession.Instance.gameObject);
        }
        if (Application.isPlaying) { yield return null; yield return new ExitPlayMode(); }
        RunDeckInventory.ResetForNewGame(); RunRelicInventory.ResetForNewGame();
    }

    [Test] public void SerializedTemporaryRunContainsFiveNormalBattlesAndIsTheBuildEntry()
    {
        RunDefinition definition = AssetDatabase.LoadAssetAtPath<RunDefinition>(MapAssetSetup.RunPath);
        Assert.That(definition, Is.Not.Null);
        var state = new RunProgress(definition, 12);
        Assert.That(state.Nodes.Count, Is.EqualTo(5));
        Assert.That(state.Nodes.All(n => n.Choices.Count == 1 && n.Choices[0].Type == MapContentType.NormalBattle), Is.True);
        Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(MapAssetSetup.MapScenePath));
        Assert.That(EditorBuildSettings.scenes[0].enabled, Is.True);
        Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/BattleScene.unity"), Is.True);
    }

    [Test] public void SavedMapSceneKeepsItsRunUIAndInputReferences()
    {
        Scene scene = SceneManager.GetSceneByPath(MapAssetSetup.MapScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(MapAssetSetup.MapScenePath, OpenSceneMode.Additive);
        try
        {
            MapController map = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapController>(true)).Single();
            Assert.That(map.Definition, Is.SameAs(AssetDatabase.LoadAssetAtPath<RunDefinition>(MapAssetSetup.RunPath)));
            var config = new SerializedObject(map);
            foreach (string field in new[] { "heading", "progressText", "resourcesText", "statusText", "nodeTemplate", "nodeContainer" })
                Assert.That(config.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            var buttons = config.FindProperty("choiceButtons"); Assert.That(buttons.arraySize, Is.EqualTo(3));
            for (int i = 0; i < buttons.arraySize; i++)
            {
                Button button = (Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(button, Is.Not.Null); Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(map));
            }
            Assert.That(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(true)).Single(), Is.Not.Null);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest] public IEnumerator FiveRealBattleRoundTripsPreserveHealthRewardsAndStop()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MapScene"); yield return WaitForMap();
        CaptureMap("map-start.png");
        RunSession session = RunSession.Instance;
        RunProgress progress = session.Progress;
        var finished = new FinishCounter(RunPhase.Completed); session.RunFinished += finished.OnFinished;
        AmmoCapacityRelic relic = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/CardData/Soldier/Examples/AmmoCapacityRelic.asset");
        Assert.That(relic.Acquire(), Is.True);
        int initialDeckCount = -1;
        for (int i = 0; i < 5; i++)
        {
            MapController map = Object.FindFirstObjectByType<MapController>();
            Assert.That(map.CanChoose(0), Is.True); Assert.That(map.CanChoose(1), Is.False);
            var button = GameObject.Find("Choice1").GetComponent<Button>();
            Assert.That(button.interactable, Is.True);
            button.onClick.Invoke(); button.onClick.Invoke();
            Assert.That(progress.Phase, Is.EqualTo(RunPhase.Battle));
            yield return WaitForScene("BattleScene");
            BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
            Assert.That(Object.FindFirstObjectByType<RunBattleBridge>(), Is.Not.Null);
            Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(80 - i * 7));
            Assert.That(battle.GetPlayer().GetAmmoSystem().RunMaxAmmoBonus, Is.EqualTo(relic.MaxAmmoBonus));
            if (initialDeckCount < 0) initialDeckCount = battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count;
            Assert.That(battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count, Is.EqualTo(initialDeckCount + (i > 0 ? 1 : 0)));
            battle.GetPlayer().TakeDamage(7);
            battle.GetTarget().TakeDamage(100000);
            var rewards = Object.FindFirstObjectByType<BattleRewardSystem>();
            Assert.That(progress.Phase, Is.EqualTo(RunPhase.Rewards));
            Assert.That(progress.CompletedNodes, Is.EqualTo(i));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("BattleScene"));
            Assert.That(session.SelectOption(0), Is.False);
            Assert.That(rewards.ClaimGold(), Is.True);
            if (i == 0)
            {
                Assert.That(rewards.OpenCardReward(), Is.True);
                Assert.That(rewards.SelectDeck(CardDeckType.TypeDream), Is.True);
                Assert.That(rewards.SkipRemaining(), Is.False);
                Assert.That(rewards.ChooseCard(0), Is.True);
            }
            Assert.That(rewards.SkipRemaining(), Is.True);
            Assert.That(rewards.SkipRemaining(), Is.False);
            yield return WaitForMap();
            Assert.That(session.Progress, Is.SameAs(progress));
            Assert.That(progress.CurrentHP, Is.EqualTo(80 - (i + 1) * 7));
            Assert.That(RunGoldInventory.Gold, Is.EqualTo((i + 1) * 20));
            Assert.That(RunRelicInventory.Contains(relic), Is.True);
            Assert.That(progress.CompletedNodes, Is.EqualTo(i + 1));
        }
        Assert.That(progress.Phase, Is.EqualTo(RunPhase.Completed)); Assert.That(finished.Count, Is.EqualTo(1));
        Assert.That(Object.FindFirstObjectByType<MapController>().CanChoose(0), Is.False);
        Assert.That(GameObject.Find("Heading").GetComponent<TMP_Text>().text, Is.EqualTo("런 완료"));
        Assert.That(Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b => b.interactable), Is.False);
        Assert.That(Object.FindFirstObjectByType<BattleManager>(), Is.Null);
        Assert.That(session.SelectOption(0), Is.False);
        CaptureMap("map-completed.png");
    }

    [UnityTest] public IEnumerator DefeatReturnsToTerminalMapWithoutAdvancingAndNewRunResetsAllInventories()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MapScene"); yield return WaitForMap();
        RunSession session = RunSession.Instance; RunDefinition definition = session.Progress.Definition;
        var finished = new FinishCounter(RunPhase.Defeated); session.RunFinished += finished.OnFinished;
        RunGoldInventory.Add(123);
        AmmoCapacityRelic relic = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/CardData/Soldier/Examples/AmmoCapacityRelic.asset");
        relic.Acquire(); session.SelectOption(0); yield return WaitForScene("BattleScene");
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardData extra = battle.GetDeckController().GetDeck(CardDeckType.TypeDream)[0];
        int initialDeck = battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count;
        battle.GetDeckController().AddCardToDeck(extra, CardDeckType.TypeDream);
        battle.GetPlayer().TakeDamage(100000);
        Assert.That(Object.FindFirstObjectByType<BattleRewardSystem>().State, Is.EqualTo(BattleRewardState.Closed));
        yield return WaitForMap();
        Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Defeated));
        Assert.That(session.Progress.CompletedNodes, Is.Zero); Assert.That(session.Progress.CurrentHP, Is.Zero);
        Assert.That(finished.Count, Is.EqualTo(1));
        Assert.That(Object.FindFirstObjectByType<MapController>().CanChoose(0), Is.False);
        RunProgress old = session.Progress;
        session.StartNewRun(definition, 81);
        Assert.That(RunGoldInventory.Gold, Is.Zero); Assert.That(RunRelicInventory.Count, Is.Zero);
        Assert.That(session.Progress.CurrentHP, Is.EqualTo(80));
        Assert.That(session.CompleteContent(old, 0, 0), Is.False);
        session.SelectOption(0); yield return WaitForScene("BattleScene");
        Assert.That(Object.FindFirstObjectByType<BattleManager>().GetDeckController().GetDeck(CardDeckType.TypeDream).Count, Is.EqualTo(initialDeck));
    }

    [UnityTest] public IEnumerator CancelledBattleDoesNotGrantCompletionOrRewards()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MapScene"); yield return WaitForMap();
        RunSession.Instance.SelectOption(0); yield return WaitForScene("BattleScene");
        Object.FindFirstObjectByType<BattleManager>().EndBattle();
        Assert.That(Object.FindFirstObjectByType<BattleRewardSystem>().State, Is.EqualTo(BattleRewardState.Closed));
        yield return WaitForMap();
        Assert.That(RunSession.Instance.Progress.Phase, Is.EqualTo(RunPhase.Cancelled));
        Assert.That(RunSession.Instance.Progress.CompletedNodes, Is.Zero);
    }

    [UnityTest] public IEnumerator DirectBattleSceneStillStartsWithoutAnActiveRun()
    {
        yield return new EnterPlayMode();
        if (RunSession.Instance != null) Object.Destroy(RunSession.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("BattleScene"); yield return WaitForScene("BattleScene");
        Assert.That(RunSession.Instance, Is.Null);
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(battle.GetPlayer().GetMaxHP()));
        battle.GetTarget().TakeDamage(100000);
        var rewards = Object.FindFirstObjectByType<BattleRewardSystem>();
        Assert.That(rewards.State, Is.EqualTo(BattleRewardState.Summary));
        rewards.SkipRemaining(); yield return null; yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("BattleScene"));
    }

    private static IEnumerator WaitForMap()
    {
        yield return WaitForScene("MapScene");
        float until = Time.realtimeSinceStartup + 20;
        while ((RunSession.Instance == null || RunSession.Instance.IsTransitioning || RunSession.Instance.Progress == null) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(RunSession.Instance, Is.Not.Null);
        Assert.That(RunSession.Instance.IsTransitioning, Is.False);
        yield return null;
    }
    private static IEnumerator WaitForScene(string scene)
    {
        float until = Time.realtimeSinceStartup + 20;
        while ((SceneManager.GetActiveScene().name != scene || (RunSession.Instance != null && RunSession.Instance.IsTransitioning)) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(scene));
        yield return null; yield return null;
    }

    internal static void CaptureMap(string fileName)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        var cameraObject = new GameObject("Map capture camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .075f, .12f);
        var target = new RenderTexture(1280, 720, 24); camera.targetTexture = target;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            string folder = System.IO.Path.Combine(Application.dataPath, "../.utmp/map-system");
            System.IO.Directory.CreateDirectory(folder); System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, fileName), pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            camera.targetTexture = null; Object.Destroy(cameraObject); Object.Destroy(target); Object.Destroy(pixels);
        }
    }

    // Allocate after EnterPlayMode; iterator-generated captured-local display classes do not
    // survive the test runner's domain reload at that yield point.
    private sealed class FinishCounter
    {
        private readonly RunPhase expected;
        public int Count { get; private set; }
        public FinishCounter(RunPhase expected) { this.expected = expected; }
        public void OnFinished(RunPhase phase) { Assert.That(phase, Is.EqualTo(expected)); Count++; }
    }
}
