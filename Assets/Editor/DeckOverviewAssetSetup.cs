using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DeckOverviewAssetSetup
{
    private const string ScenePath = "Assets/Scenes/BattleScene.unity";

    [MenuItem("ProjectDN/Setup Deck Overview")]
    public static void CreateAssets()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            Transform canvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .First(candidate => candidate.isRootCanvas).transform;
            BattleManager battle = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BattleManager>(true)).Single();
            DeckController deck = battle.GetDeckController();
            // Serialized references are authoritative before BattleManager.Awake runs in the Editor.
            if (deck == null) deck = (DeckController)new SerializedObject(battle).FindProperty("deckController").objectReferenceValue;
            UnityEngine.UI.Button toggle = canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == "DeckButton");
            DeckListUI pileList = canvas.GetComponentsInChildren<DeckListUI>(true).Single(list => list.name == "DeckListPanel");
            DeckOverviewUI overview = canvas.GetComponentInChildren<DeckOverviewUI>(true);
            if (overview == null)
                overview = Rect(canvas, "DeckOverview", Vector2.zero, Vector2.zero).gameObject.AddComponent<DeckOverviewUI>();
            RectTransform panel = canvas.Find("DeckOverviewPanel") as RectTransform;
            if (panel == null)
            {
                panel = Rect(canvas, "DeckOverviewPanel", Vector2.zero, new Vector2(1, .9f));
                var panelImage = Background(panel, Color.white, true);
                panelImage.sprite = pileList.GetComponent<UnityEngine.UI.Image>().sprite;
                panelImage.type = UnityEngine.UI.Image.Type.Sliced;
                panelImage.pixelsPerUnitMultiplier = 1.5f;
                Label(panel, "Title", "전체 덱", new Color(.93f,.9f,.8f), 34,
                    new Vector2(0,1), Vector2.one, new Vector2(0,-72), Vector2.zero);
                RectTransform close = Rect(panel, "CloseButton", Vector2.one, Vector2.one);
                close.pivot = Vector2.one;
                close.anchoredPosition = new Vector2(-28, -20);
                close.sizeDelta = new Vector2(56, 56);
                Background(close, Color.white, true).sprite =
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/UI/shared-close-button.png");
                close.gameObject.AddComponent<UnityEngine.UI.Button>();
                CreateSection(panel, "DreamSection", true);
                CreateSection(panel, "NightmareSection", false);
            }
            DeckListUI dream = panel.Find("DreamSection").GetComponent<DeckListUI>();
            DeckListUI nightmare = panel.Find("NightmareSection").GetComponent<DeckListUI>();
            UnityEngine.UI.Button closeButton = panel.Find("CloseButton").GetComponent<UnityEngine.UI.Button>();
            closeButton.targetGraphic = closeButton.GetComponent<UnityEngine.UI.Image>();
            Bind(overview, "deckController", deck);
            Bind(overview, "battle", battle);
            Bind(overview, "panel", panel.gameObject);
            Bind(overview, "dreamList", dream);
            Bind(overview, "nightmareList", nightmare);
            Bind(overview, "pileList", pileList);
            Bind(overview, "dreamTitle", panel.Find("DreamSection/Title").GetComponent<TMP_Text>());
            Bind(overview, "nightmareTitle", panel.Find("NightmareSection/Title").GetComponent<TMP_Text>());
            Bind(overview, "toggleButton", toggle);
            Bind(overview, "closeButton", closeButton);
            for (int i = toggle.onClick.GetPersistentEventCount()-1; i >= 0; i--)
                if (toggle.onClick.GetPersistentTarget(i) == overview && toggle.onClick.GetPersistentMethodName(i) == "Toggle")
                    UnityEventTools.RemovePersistentListener(toggle.onClick, i);
            UnityEventTools.AddPersistentListener(toggle.onClick, overview.Toggle);
            for (int i = closeButton.onClick.GetPersistentEventCount()-1; i >= 0; i--)
                if (closeButton.onClick.GetPersistentTarget(i) == overview && closeButton.onClick.GetPersistentMethodName(i) == "Close")
                    UnityEventTools.RemovePersistentListener(closeButton.onClick, i);
            UnityEventTools.AddPersistentListener(closeButton.onClick, overview.Close);
            EditorUtility.SetDirty(toggle);
            EditorUtility.SetDirty(closeButton);
            panel.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Deck overview wired to the existing DeckButton with independent Dream/Nightmare lists.");
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    private static void CreateSection(RectTransform parent, string name, bool dream)
    {
        RectTransform section = Rect(parent, name, new Vector2(dream ? 0 : .5f,0), new Vector2(dream ? .5f : 1,1));
        section.offsetMin = new Vector2(dream ? 24 : 12,20);
        section.offsetMax = new Vector2(dream ? -12 : -24,-88);
        Background(section, dream ? new Color(.035f,.065f,.1f) : new Color(.105f,.035f,.045f), false);
        Label(section, "Title", dream ? "꿈 덱" : "악몽 덱", dream ? new Color(.62f,.82f,1f) : new Color(1f,.63f,.64f), 28,
            new Vector2(0,1), Vector2.one, new Vector2(0,-64), Vector2.zero);
        RectTransform scrollRoot = Rect(section, "ScrollView", Vector2.zero, Vector2.one);
        scrollRoot.offsetMin = new Vector2(12,12);
        scrollRoot.offsetMax = new Vector2(-12,-64);
        UnityEngine.UI.ScrollRect scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
        RectTransform viewport = Rect(scrollRoot, "Viewport", Vector2.zero, Vector2.one);
        Background(viewport, new Color(0,0,0,.001f), true);
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        RectTransform content = Rect(viewport, "Content", new Vector2(0,1), Vector2.one);
        content.pivot = new Vector2(.5f,1);
        UnityEngine.UI.GridLayoutGroup grid = content.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize = new Vector2(200,260);
        grid.spacing = new Vector2(50,30);
        grid.padding = new RectOffset(36,36,40,40);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        UnityEngine.UI.ContentSizeFitter fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;
        DeckListUI list = section.gameObject.AddComponent<DeckListUI>();
        Bind(list, "panel", section.gameObject);
        Bind(list, "content", content);
        Bind(list, "cardPrefab", AssetDatabase.LoadAssetAtPath<DeckCardUI>("Assets/Prefabs/UI/DeckCard.prefab"));
        var state = new SerializedObject(list);
        state.FindProperty("sortByName").boolValue = true;
        state.FindProperty("maxColumns").intValue = 3;
        state.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static UnityEngine.UI.Image Background(RectTransform rect, Color color, bool raycast)
    {
        UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static void Label(Transform parent, string name, string text, Color color, int size,
        Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = Rect(parent, name, min, max);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Bold SDF.asset");
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private static void Bind(Object target, string field, Object value)
    {
        var state = new SerializedObject(target);
        state.FindProperty(field).objectReferenceValue = value;
        state.ApplyModifiedPropertiesWithoutUndo();
    }
}
