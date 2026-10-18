using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class MapAssetSetup
{
    public const string MapScenePath = "Assets/Scenes/MapScene.unity";
    public const string RunPath = "Assets/MapData/TemporaryRun.asset";
    private static TMP_FontAsset font;

    [MenuItem("ProjectDN/Setup Map System")]
    public static void CreateAssets()
    {
        if (!AssetDatabase.IsValidFolder("Assets/MapData")) AssetDatabase.CreateFolder("Assets", "MapData");
        Scene battleScene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity");
        BattleManager battle = UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        BattleRewardSystem rewards = UnityEngine.Object.FindFirstObjectByType<BattleRewardSystem>();
        if (battle == null || rewards == null) throw new InvalidOperationException("Battle scene needs its existing battle and reward systems.");

        BattleEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<BattleEncounterDefinition>("Assets/MapData/NormalEncounter.asset");
        if (encounter == null)
        {
            encounter = ScriptableObject.CreateInstance<BattleEncounterDefinition>();
            encounter.displayName = "일반 전투";
            encounter.behavior = (EnemyBehavior)new SerializedObject(battle.GetTarget()).FindProperty("behavior").objectReferenceValue;
            encounter.enemyMaxHP = battle.GetTarget().GetMaxHP(); encounter.goldReward = rewards.GoldReward;
            AssetDatabase.CreateAsset(encounter, "Assets/MapData/NormalEncounter.asset");
        }
        FloorMapDefinition floor = AssetDatabase.LoadAssetAtPath<FloorMapDefinition>("Assets/MapData/TemporaryFloor.asset");
        if (floor == null)
        {
            floor = ScriptableObject.CreateInstance<FloorMapDefinition>();
            floor.mode = FloorMapMode.NormalBattlesOnly; floor.nodeCount = 5; floor.normalBattles = new[] { encounter };
            AssetDatabase.CreateAsset(floor, "Assets/MapData/TemporaryFloor.asset");
        }
        RunDefinition run = AssetDatabase.LoadAssetAtPath<RunDefinition>(RunPath);
        if (run == null)
        {
            run = ScriptableObject.CreateInstance<RunDefinition>();
            run.startingMaxHP = battle.GetPlayer().GetMaxHP(); run.floors = new[] { floor };
            run.enabledContent = new[] { MapContentType.NormalBattle };
            AssetDatabase.CreateAsset(run, RunPath);
        }
        var bridge = battle.GetComponent<RunBattleBridge>();
        if (bridge == null) bridge = battle.gameObject.AddComponent<RunBattleBridge>();
        Bind(bridge, "battle", battle); Bind(bridge, "rewards", rewards);
        EditorSceneManager.MarkSceneDirty(battleScene); EditorSceneManager.SaveScene(battleScene);

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-VF SDF.asset");
        if (font == null) throw new InvalidOperationException("The existing Korean TMP font is required.");
        AssetDatabase.SaveAssets();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MapScenePath) == null) CreateMapScene();
        else
        {
            Scene mapScene = EditorSceneManager.OpenScene(MapScenePath);
            MapController map = UnityEngine.Object.FindFirstObjectByType<MapController>();
            if (map == null) throw new InvalidOperationException("Map scene needs a MapController.");
            if (new SerializedObject(map).FindProperty("runDefinition").objectReferenceValue == null)
            {
                Bind(map, "runDefinition", AssetDatabase.LoadAssetAtPath<RunDefinition>(RunPath));
                EditorSceneManager.MarkSceneDirty(mapScene); EditorSceneManager.SaveScene(mapScene);
            }
        }
        MapController mapController = UnityEngine.Object.FindFirstObjectByType<MapController>();
        MapChoiceCardSetup.Configure(mapController);
        EditorSceneManager.MarkSceneDirty(mapController.gameObject.scene);
        EditorSceneManager.SaveScene(mapController.gameObject.scene);
        // Keep other registered scenes and their enabled flags; only put the map before the battle.
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != MapScenePath && s.path != "Assets/Scenes/BattleScene.unity").ToList();
        scenes.Insert(0, new EditorBuildSettingsScene("Assets/Scenes/BattleScene.unity", true));
        scenes.Insert(0, new EditorBuildSettingsScene(MapScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Map system configured: five normal battles, then run completion.");
    }

    private static void CreateMapScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("MapController");
        MapController controller = root.AddComponent<MapController>();
        // Opening a scene can unload asset objects held only in local variables.
        Bind(controller, "runDefinition", AssetDatabase.LoadAssetAtPath<RunDefinition>(RunPath));

        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;

        RectTransform background = Rect(canvas.transform, "Background", new Vector2(1920,1080)); Stretch(background);
        background.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.055f, .075f, .12f, 1);
        RectTransform content = Rect(canvas.transform, "Content", new Vector2(1440, 880));
        content.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.085f, .11f, .17f, 1);
        var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(64, 64, 48, 48); layout.spacing = 20; layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

        TMP_Text heading = Label(content, "Heading", "진행 경로", 54, 84);
        TMP_Text progress = Label(content, "Progress", "1층 · 1 / 5", 30, 48);
        TMP_Text resources = Label(content, "Resources", "HP 80 / 80     골드 0", 28, 44);
        RectTransform nodes = Rect(content, "Nodes", new Vector2(1200, 200));
        Height(nodes, 200);
        var grid = nodes.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize = new Vector2(110, 86); grid.spacing = new Vector2(14, 14); grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
        TMP_Text template = Label(nodes, "NodeTemplate", "1\n현재", 26, 86); template.gameObject.SetActive(false);
        TMP_Text status = Label(content, "Status", "진행할 행동을 선택하세요.", 26, 58);

        RectTransform choices = Rect(content, "Choices", new Vector2(1200, 180)); Height(choices, 180);
        var choiceLayout = choices.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        choiceLayout.spacing = 12; choiceLayout.childAlignment = TextAnchor.MiddleCenter;
        choiceLayout.childControlWidth = choiceLayout.childControlHeight = true;
        choiceLayout.childForceExpandWidth = false; choiceLayout.childForceExpandHeight = false;
        var buttons = new UnityEngine.UI.Button[3];
        for (int i = 0; i < buttons.Length; i++)
        {
            RectTransform rect = Rect(choices, "Choice" + (i + 1), new Vector2(520, 64));
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.19f, .27f, .39f, 1);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1f, .88f, .65f); colors.disabledColor = new Color(.4f, .4f, .4f); button.colors = colors;
            var size = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); size.preferredWidth = 520; size.preferredHeight = 52;
            TMP_Text text = Label(rect, "Text", "일반 전투", 28, 52); Stretch((RectTransform)text.transform);
            UnityEventTools.AddIntPersistentListener(button.onClick, controller.SelectOption, i);
            buttons[i] = button;
            if (i > 0) rect.gameObject.SetActive(false);
        }
        Bind(controller, "heading", heading); Bind(controller, "progressText", progress);
        Bind(controller, "resourcesText", resources); Bind(controller, "statusText", status);
        Bind(controller, "nodeTemplate", template); Bind(controller, "nodeContainer", nodes);
        var config = new SerializedObject(controller); var options = config.FindProperty("choiceButtons"); options.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++) options.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        config.ApplyModifiedPropertiesWithoutUndo();

        var inputObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        inputObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        EditorSceneManager.SaveScene(scene, MapScenePath);
    }
    private static RectTransform Rect(Transform parent, string name, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = size;
        return rect;
    }
    private static TMP_Text Label(Transform parent, string name, string text, float size, float height)
    {
        RectTransform rect = Rect(parent, name, new Vector2(900, height));
        TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = size; label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(.9f, .93f, .98f); label.raycastTarget = false;
        Height(rect, height); return label;
    }
    private static void Height(RectTransform rect, float height)
    { var layout = rect.gameObject.GetComponent<UnityEngine.UI.LayoutElement>(); if (layout == null) layout = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); layout.preferredHeight = height; }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Bind(UnityEngine.Object target, string name, UnityEngine.Object value)
    { var config = new SerializedObject(target); config.FindProperty(name).objectReferenceValue = value; config.ApplyModifiedPropertiesWithoutUndo(); }
}
