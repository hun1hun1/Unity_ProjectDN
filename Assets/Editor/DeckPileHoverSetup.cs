using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DeckPileHoverSetup
{
    private const string ScenePath = "Assets/Scenes/BattleScene.unity";
    private const string TooltipPath = "Assets/Prefabs/UI/DeckPileTooltip.prefab";

    [MenuItem("ProjectDN/Setup Deck Pile Hover")]
    public static void CreateAssets()
    {
        DeckPileTooltipUI tooltip = AssetDatabase.LoadAssetAtPath<DeckPileTooltipUI>(TooltipPath);
        if (tooltip == null) tooltip = CreateTooltip();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            DeckPileButton[] piles = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<DeckPileButton>(true)).ToArray();
            foreach (DeckPileButton pile in piles) Configure(pile, tooltip);
            if (piles.Length != 4) throw new System.InvalidOperationException("Expected four deck pile buttons.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Configured hover visuals and tooltips for all four deck piles.");
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    private static DeckPileTooltipUI CreateTooltip()
    {
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CardStatusTooltip.prefab"));
        try
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "DeckPileTooltip";
            Object.DestroyImmediate(root.GetComponent<CardStatusTooltipUI>());
            DeckPileTooltipUI tooltip = root.AddComponent<DeckPileTooltipUI>();
            Bind(tooltip, "contentText", root.GetComponentInChildren<TMP_Text>(true));
            foreach (UnityEngine.UI.Graphic graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphic.raycastTarget = false;
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;
            root.SetActive(false);
            return PrefabUtility.SaveAsPrefabAsset(root, TooltipPath).GetComponent<DeckPileTooltipUI>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void Configure(DeckPileButton pile, DeckPileTooltipUI tooltip)
    {
        UnityEngine.UI.Image hitArea = pile.GetComponent<UnityEngine.UI.Image>();
        Transform child = pile.transform.Find("HoverVisual");
        RectTransform visual;
        if (child == null)
        {
            visual = new GameObject("HoverVisual", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image), typeof(Canvas)).GetComponent<RectTransform>();
            visual.gameObject.layer = pile.gameObject.layer;
            visual.SetParent(pile.transform, false);
            visual.SetAsFirstSibling();
            visual.anchorMin = Vector2.zero;
            visual.anchorMax = Vector2.one;
            visual.sizeDelta = Vector2.zero;
            visual.anchoredPosition = Vector2.zero;
            EditorUtility.CopySerialized(hitArea, visual.GetComponent<UnityEngine.UI.Image>());
            hitArea.color = Color.clear;
        }
        else visual = (RectTransform)child;
        UnityEngine.UI.Image image = visual.GetComponent<UnityEngine.UI.Image>();
        image.raycastTarget = false;
        hitArea.raycastTarget = true;
        pile.GetComponent<UnityEngine.UI.Button>().targetGraphic = image;
        DeckPileHoverUI hover = pile.GetComponent<DeckPileHoverUI>();
        bool isNew = hover == null;
        if (isNew) hover = pile.gameObject.AddComponent<DeckPileHoverUI>();
        Bind(hover, "hoverVisual", visual);
        Bind(hover, "hoverCanvas", visual.GetComponent<Canvas>());
        Bind(hover, "tooltipPrefab", tooltip);
        if (isNew)
        {
            var pileState = new SerializedObject(pile);
            bool dream = pileState.FindProperty("deckType").intValue == 0;
            bool discard = pileState.FindProperty("isDiscardPile").boolValue;
            var hoverState = new SerializedObject(hover);
            hoverState.FindProperty("tooltipText").stringValue = (dream ? "꿈" : "악몽") +
                (discard ? " 버린 카드 더미" : " 뽑을 카드 더미");
            hoverState.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void Bind(Object target, string name, Object value)
    {
        var state = new SerializedObject(target);
        state.FindProperty(name).objectReferenceValue = value;
        state.ApplyModifiedPropertiesWithoutUndo();
    }
}
