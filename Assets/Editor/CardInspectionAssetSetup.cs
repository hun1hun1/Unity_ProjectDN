using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class CardInspectionAssetSetup
{
    public const string PrefabPath = "Assets/Prefabs/UI/CardInspection.prefab";

    [MenuItem("ProjectDN/Create Card Inspection UI")]
    public static void CreateAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) CreatePrefab(PrefabPath);
        const string path = "Assets/Scenes/BattleScene.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            Canvas canvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .First(value => value.name == "Canvas");
            if (canvas.GetComponentInChildren<CardInspectionUI>(true) == null)
            {
                GameObject window = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                window.transform.SetParent(canvas.transform, false);
                Stretch((RectTransform)window.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save card inspection UI.");
            }
            AssetDatabase.SaveAssets();
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        Debug.Log("Card inspection prefab and BattleScene bindings are ready.");
    }

    private static void CreatePrefab(string outputPath)
    {
        GameObject root = NewUI("CardInspectionCanvas", null, typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CardInspectionUI));
        try
        {
            // Avoid screen-space Canvas driving serialized RectTransforms while authoring the prefab.
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<Canvas>().overrideSorting = true;
            root.GetComponent<Canvas>().sortingOrder = 1000;
            Stretch((RectTransform)root.transform);
            GameObject backdrop = NewUI("Backdrop", root.transform, typeof(UnityEngine.UI.Image), typeof(CanvasGroup));
            Stretch((RectTransform)backdrop.transform);
            backdrop.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.7f);
            backdrop.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            GameObject card = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DeckCard.prefab"), backdrop.transform);
            card.name = "CardView";
            RectTransform cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            Object.DestroyImmediate(card.GetComponent<DeckCardHoverUI>());
            // Keep the inspection copy at its base presentation size.
            RectTransform visual = (RectTransform)card.transform.Find("HoverVisual");
            Stretch(visual);
            visual.localScale = Vector3.one;
            visual.localRotation = Quaternion.identity;
            visual.GetComponent<Canvas>().overrideSorting = false;
            foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true)) text.text = "";
            SerializedObject view = new SerializedObject(card.GetComponent<DeckCardUI>());
            view.FindProperty("allowInspection").boolValue = false;
            view.ApplyModifiedPropertiesWithoutUndo();
            GameObject status = NewUI("StatusDescriptions", backdrop.transform, typeof(UnityEngine.UI.Image));
            RectTransform statusRect = (RectTransform)status.transform;
            statusRect.anchorMin = statusRect.anchorMax = statusRect.pivot = new Vector2(0.5f, 0.5f);
            status.GetComponent<UnityEngine.UI.Image>().color = new Color(1f, 1f, 1f, 0.96f);
            status.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            GameObject label = NewUI("StatusText", status.transform, typeof(TextMeshProUGUI));
            RectTransform textRect = (RectTransform)label.transform;
            Stretch(textRect);
            textRect.offsetMin = new Vector2(16f, 16f);
            textRect.offsetMax = new Vector2(-16f, -16f);
            TMP_Text content = label.GetComponent<TMP_Text>();
            content.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-VF SDF.asset");
            content.text = "";
            content.fontSize = content.fontSizeMax = 24f;
            content.fontSizeMin = 14f;
            content.enableAutoSizing = true;
            content.color = Color.black;
            content.alignment = TextAlignmentOptions.TopLeft;
            content.overflowMode = TextOverflowModes.Ellipsis;
            content.raycastTarget = false;
            SerializedObject state = new SerializedObject(root.GetComponent<CardInspectionUI>());
            state.FindProperty("panel").objectReferenceValue = backdrop;
            state.FindProperty("cardView").objectReferenceValue = card.GetComponent<DeckCardUI>();
            state.FindProperty("statusPanel").objectReferenceValue = statusRect;
            state.FindProperty("statusText").objectReferenceValue = content;
            state.ApplyModifiedPropertiesWithoutUndo();
            backdrop.SetActive(false);
            if (PrefabUtility.SaveAsPrefabAsset(root, outputPath) == null)
                throw new InvalidOperationException("Could not save card inspection prefab.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static GameObject NewUI(string name, Transform parent, params Type[] components)
    {
        GameObject value = new GameObject(name, new[] { typeof(RectTransform) }.Concat(components).ToArray());
        value.layer = 5;
        if (parent != null) value.transform.SetParent(parent, false);
        return value;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
