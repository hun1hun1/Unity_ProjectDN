using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class EnemyPrefabTests
{
    private const string PrefabPath = "Assets/Prefabs/Enemy/TestEnemy.prefab";
    private readonly List<Object> created = new List<Object>();

    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }
    [UnityTearDown] public IEnumerator ExitPlay()
    { if (Application.isPlaying) yield return new ExitPlayMode(); }

    [Test] public void EncounterStoresAnOptionalEnemyPrefabAndRejectsNonCanvasRoots()
    {
        BattleEncounterDefinition encounter = Encounter();
        SerializedProperty field = new SerializedObject(encounter).FindProperty("enemyPrefab");
        Assert.That(field, Is.Not.Null, "Encounter must expose Enemy Prefab.");
        Assert.DoesNotThrow(() => encounter.Validate(), "Legacy definitions without a prefab remain valid.");
        Enemy nonUI = Track(new GameObject("Non-UI enemy")).AddComponent<Enemy>();
        SetPrefab(encounter, nonUI);
        Assert.Throws<InvalidOperationException>(() => encounter.Validate());
    }

    [Test] public void SavedEnemyPrefabAndBattleSceneRetainTheirSerializedBindings()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null, "Existing enemy must be authored as a prefab.");
        Enemy enemy = prefab.GetComponent<Enemy>();
        Assert.That(enemy, Is.Not.Null);
        Assert.That(prefab.transform, Is.InstanceOf<RectTransform>());
        Assert.That(prefab.GetComponent<Image>().sprite, Is.Not.Null);
        Assert.That(prefab.GetComponent<Image>().preserveAspect, Is.True);
        var state = new SerializedObject(enemy);
        Assert.That(state.FindProperty("characterInfoUI").objectReferenceValue, Is.Not.Null);
        Assert.That(state.FindProperty("characterInfoUI").objectReferenceValue is Component hud && hud.transform.IsChildOf(prefab.transform), Is.True);
        Assert.That(state.FindProperty("actionUI").objectReferenceValue, Is.Null, "Prefab cannot retain a scene-only HUD reference.");
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/BattleScene.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity", OpenSceneMode.Additive);
        try
        {
            BattleManager battle = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<BattleManager>(true) != null) battle = root.GetComponentInChildren<BattleManager>(true);
            Assert.That(battle, Is.Not.Null);
            var config = new SerializedObject(battle);
            Component slot = (Component)config.FindProperty("enemySpawnRoot").objectReferenceValue;
            Assert.That(slot, Is.Not.Null);
            Assert.That(battle.GetTarget().transform.parent, Is.SameAs(slot.transform));
            Assert.That(config.FindProperty("enemyActionUI").objectReferenceValue, Is.Not.Null);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(battle.GetTarget()), Is.EqualTo(PrefabPath));
            BattleEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<BattleEncounterDefinition>("Assets/MapData/NormalEncounter.asset");
            Assert.That(new SerializedObject(encounter).FindProperty("enemyPrefab").objectReferenceValue, Is.SameAs(enemy));
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    [Test] public void SelectedPrefabReplacesTargetAndKeepsEncounterStatsAndDeathSubscriptions()
    {
        BattleManager battle = Battle();
        Enemy old = battle.GetTarget();
        Enemy template = Template("Selected enemy", Color.cyan);
        BattleEncounterDefinition encounter = Encounter(template);
        encounter.enemyMaxHP = 73;
        Configure(battle, encounter);
        Enemy spawned = battle.GetTarget();
        Assert.That(spawned, Is.Not.SameAs(old));
        Assert.That(spawned, Is.Not.SameAs(template));
        Assert.That(old == null || !old.gameObject.activeSelf, Is.True);
        Assert.That(spawned.GetMaxHP(), Is.EqualTo(73));
        Assert.That(spawned.GetCurrentHP(), Is.EqualTo(73));
        Assert.That(spawned.GetComponent<Image>().color, Is.EqualTo(Color.cyan));
        Assert.That(spawned.transform.parent, Is.SameAs(Get(battle, "enemySpawnRoot")));
        Assert.That(spawned.NextAction, Is.SameAs(encounter.behavior.GetAction(0)));
        if (old != null) old.TakeDamage(100000);
        Assert.That(battle.Result, Is.EqualTo(BattleResult.None));
        spawned.TakeDamage(100000);
        Assert.That(battle.Result, Is.EqualTo(BattleResult.Victory));
    }

    [Test] public void MissingSpawnSlotRejectsReplacementWithoutDestroyingCurrentEnemy()
    {
        BattleManager battle = Battle();
        Enemy old = battle.GetTarget();
        Set(battle, "enemySpawnRoot", null);
        Assert.Throws<InvalidOperationException>(() => Configure(battle, Encounter(Template("Enemy", Color.white))));
        Assert.That(battle.GetTarget(), Is.SameAs(old));
        Assert.That(old.gameObject.activeSelf, Is.True);
    }

    [Test] public void LegacyEncounterConfiguresTheExistingTargetWithoutSpawning()
    {
        BattleManager battle = Battle();
        Enemy old = battle.GetTarget();
        BattleEncounterDefinition encounter = Encounter(); encounter.enemyMaxHP = 62;
        Configure(battle, encounter);
        Assert.That(battle.GetTarget(), Is.SameAs(old));
        Assert.That(old.GetCurrentHP(), Is.EqualTo(62));
    }

    [UnityTest] public IEnumerator MapBattleSpawnsTheSelectedPrefabBeforeCombatAndKeepsRewardFlow()
    {
        yield return new EnterPlayMode();
        RunSession session = RunSession.GetOrCreate();
        RunDefinition source = AssetDatabase.LoadAssetAtPath<RunDefinition>(MapAssetSetup.RunPath);
        RunDefinition definition = Track(Object.Instantiate(source));
        FloorMapDefinition floor = Track(Object.Instantiate(source.floors[0]));
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null, "Existing enemy must be authored as a prefab.");
        BattleEncounterDefinition encounter = Encounter(prefab.GetComponent<Enemy>());
        encounter.enemyMaxHP = 67; encounter.goldReward = 29;
        floor.mode = FloorMapMode.NormalBattlesOnly; floor.nodeCount = 1; floor.normalBattles = new[] { encounter };
        definition.floors = new[] { floor };
        session.StartNewRun(definition, 21);
        yield return WaitForScene("MapScene");
        Assert.That(session.SelectOption(0), Is.True);
        yield return WaitForScene("BattleScene");
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle.GetTarget().GetCurrentHP(), Is.EqualTo(67));
        Assert.That(battle.GetTarget().name, Does.StartWith("TestEnemy"));
        Assert.That(Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        CharacterInfoUI hud = battle.GetTarget().GetComponentInChildren<CharacterInfoUI>();
        Assert.That(((TMP_Text)Get(hud, "hpText")).text, Is.EqualTo("67 / 67"));
        EnemyActionUI actionUI = Object.FindFirstObjectByType<EnemyActionUI>();
        Assert.That(((TMP_Text)Get(actionUI, "actionText")).text, Is.EqualTo(battle.GetTarget().NextAction.GetActionDescription(battle)));
        yield return new WaitForSeconds(.8f);
        CaptureBattle(battle);
        battle.GetTarget().TakeDamage(100000);
        Assert.That(battle.Result, Is.EqualTo(BattleResult.Victory));
        BattleRewardSystem rewards = Object.FindFirstObjectByType<BattleRewardSystem>();
        Assert.That(rewards.GoldReward, Is.EqualTo(29));
        rewards.SkipRemaining();
        yield return WaitForScene("MapScene");
        Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Completed));
        Object.Destroy(session.gameObject); yield return null;
    }

    [UnityTest] public IEnumerator PrefabAnimatorPlaysAndRepeatedSpawnsLeaveOneVisibleEnemy()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene"); yield return null;
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Enemy template = Template("Animated enemy", Color.white);
        AnimatorController controller = Track(new AnimatorController());
        controller.AddLayer("Base");
        AnimationClip clip = Track(new AnimationClip());
        clip.SetCurve("", typeof(Image), "m_Color.r", AnimationCurve.Linear(0, 0, 1, 1));
        AnimatorState state = controller.layers[0].stateMachine.AddState("Idle");
        state.motion = clip; controller.layers[0].stateMachine.defaultState = state;
        template.gameObject.AddComponent<Animator>().runtimeAnimatorController = controller;
        for (int i = 0; i < 2; i++)
        {
            Enemy previous = battle.GetTarget();
            Configure(battle, Encounter(template));
            Enemy spawned = battle.GetTarget();
            Assert.That(spawned.gameObject.activeInHierarchy, Is.True);
            Animator animator = spawned.GetComponent<Animator>();
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller));
            animator.Rebind(); animator.Update(0); animator.Update(.25f);
            Assert.That(spawned.GetComponent<Image>().color.r, Is.InRange(.20f, .30f));
            Assert.That(previous == null || !previous.gameObject.activeSelf, Is.True);
            yield return null;
            Assert.That(previous == null, Is.True);
            Assert.That(spawned.transform.parent.GetComponentsInChildren<Enemy>(), Has.Length.EqualTo(1));
            Vector3[] bounds = new Vector3[4];
            ((RectTransform)spawned.transform).GetWorldCorners(bounds);
            Assert.That(Vector3.Distance(bounds[0], bounds[2]), Is.GreaterThan(0));
        }
        Assert.That(template.GetComponent<Image>().color, Is.EqualTo(Color.white), "Animation must run on the instance, not the template.");
    }

    [UnityTest] public IEnumerator ActivePrefabDefaultsDoNotRunBeforeTheSelectedEncounterIsApplied()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene"); yield return null;
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Enemy template = Template("Active prefab", Color.white);
        template.ConfigureEncounter(Encounter());
        template.gameObject.SetActive(true);
        EnemyBehavior invalidDefault = Track(ScriptableObject.CreateInstance<EnemyBehavior>());
        var serialized = new SerializedObject(invalidDefault);
        serialized.FindProperty("selectionMode").enumValueIndex = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        // The template is already awake. Its next clone must receive the encounter's valid behavior before Awake.
        Set(template, "behavior", invalidDefault);
        BattleEncounterDefinition selected = Encounter(template);
        Configure(battle, selected);
        Assert.That(battle.GetTarget().NextAction, Is.SameAs(selected.behavior.GetAction(0)));
        yield return null;
    }

    private static IEnumerator WaitForScene(string name)
    {
        float until = Time.realtimeSinceStartup + 20;
        while ((SceneManager.GetActiveScene().name != name || RunSession.Instance != null && RunSession.Instance.IsTransitioning) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name));
        yield return null; yield return null;
    }
    private static void CaptureBattle(BattleManager battle)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Canvas canvas = battle.GetTarget().GetComponentInParent<Canvas>().rootCanvas;
        RenderMode previousMode = canvas.renderMode;
        Camera previousCamera = canvas.worldCamera;
        float previousDistance = canvas.planeDistance;
        GameObject cameraObject = new GameObject("Enemy prefab capture", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        RenderTexture texture = new RenderTexture(1280, 720, 24);
        Texture2D pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            string path = System.IO.Path.Combine(Application.dataPath, "../.utmp/enemy-prefabs/battle.png");
            System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousDistance;
            camera.targetTexture = null;
            Object.Destroy(cameraObject); Object.Destroy(texture); Object.Destroy(pixels);
        }
    }
    private BattleManager Battle()
    {
        GameObject root = Track(new GameObject("Prefab battle"));
        BattleManager battle = root.AddComponent<BattleManager>();
        Player player = Track(new GameObject("Player")).AddComponent<Player>();
        Enemy enemy = Template("Old enemy", Color.white); enemy.gameObject.SetActive(true);
        RectTransform slot = Track(new GameObject("Enemy slot", typeof(RectTransform))).GetComponent<RectTransform>();
        slot.sizeDelta = new Vector2(300, 300);
        enemy.transform.SetParent(slot, false);
        player.RestoreHealth(80, 80); enemy.RestoreHealth(50, 50);
        Set(battle, "player", player); Set(battle, "target", enemy);
        typeof(BattleManager).GetMethod("BindBattleCharacters", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(battle, null);
        Assert.That(Field(typeof(BattleManager), "enemySpawnRoot"), Is.Not.Null, "BattleManager needs an enemy spawn slot.");
        Set(battle, "enemySpawnRoot", slot);
        return battle;
    }
    private Enemy Template(string name, Color color)
    {
        GameObject root = Track(new GameObject(name, typeof(RectTransform), typeof(Image)));
        root.SetActive(false);
        root.GetComponent<Image>().color = color;
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
        return root.AddComponent<Enemy>();
    }
    private BattleEncounterDefinition Encounter(Enemy prefab = null)
    {
        BattleEncounterDefinition encounter = Track(ScriptableObject.CreateInstance<BattleEncounterDefinition>());
        EnemyBehavior behavior = Track(ScriptableObject.CreateInstance<EnemyBehavior>());
        EnemyAttackAction attack = Track(ScriptableObject.CreateInstance<EnemyAttackAction>());
        Set(attack, "damage", 2); Set(behavior, "actions", new EnemyAction[] { attack });
        encounter.behavior = behavior;
        if (prefab != null) SetPrefab(encounter, prefab);
        return encounter;
    }
    private static void SetPrefab(BattleEncounterDefinition encounter, Enemy prefab)
    {
        var serialized = new SerializedObject(encounter);
        SerializedProperty field = serialized.FindProperty("enemyPrefab");
        Assert.That(field, Is.Not.Null, "Encounter must expose Enemy Prefab.");
        field.objectReferenceValue = prefab; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Configure(BattleManager battle, BattleEncounterDefinition encounter)
    {
        MethodInfo method = typeof(BattleManager).GetMethod("ConfigureEncounter");
        Assert.That(method, Is.Not.Null, "BattleManager must instantiate the selected encounter prefab.");
        try { method.Invoke(battle, new object[] { encounter }); }
        catch (TargetInvocationException exception) { throw exception.InnerException; }
    }
    private T Track<T>(T value) where T : Object { created.Add(value); return value; }
    private static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static FieldInfo Field(Type type, string name)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
            type = type.BaseType;
        }
        return null;
    }
}
