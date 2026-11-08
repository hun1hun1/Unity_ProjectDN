using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class BattleRewardAssetSetup
{
    private static TMP_FontAsset font;
    [MenuItem("ProjectDN/Setup Battle Rewards")]
    public static void CreateAssets()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-VF SDF.asset");
        CardRewardSystem sampler = Object.FindFirstObjectByType<CardRewardSystem>(FindObjectsInactive.Include);
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        DeckController deck = Object.FindFirstObjectByType<DeckController>();
        BattleRewardSystem flow = sampler.GetComponent<BattleRewardSystem>();
        if (flow == null) flow = sampler.gameObject.AddComponent<BattleRewardSystem>();
        const string catalogPath = "Assets/CardDatabase/Soldier/BattleRewardPools.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<CardRewardPoolCatalog>(catalogPath);
        if (catalog == null) throw new System.InvalidOperationException(catalogPath);
        Bind(sampler, "poolCatalog", catalog);
        Bind(flow, "battle", battle); Bind(flow, "deckController", deck); Bind(flow, "cardRewards", sampler);
        var flowConfig = new SerializedObject(flow); flowConfig.FindProperty("goldReward").intValue = 20; flowConfig.ApplyModifiedPropertiesWithoutUndo();
        CardRewardUI ui = Object.FindFirstObjectByType<CardRewardUI>(FindObjectsInactive.Include);
        ui.gameObject.SetActive(true);
        RectTransform root = (RectTransform)ui.transform; Stretch(root);
        root.SetAsLastSibling();
        Canvas canvas = ui.GetComponent<Canvas>(); if (canvas == null) canvas = ui.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true; canvas.sortingOrder = 2000;
        if (ui.GetComponent<GraphicRaycaster>() == null) ui.gameObject.AddComponent<GraphicRaycaster>();
        CanvasGroup group = ui.GetComponent<CanvasGroup>(); if (group == null) group = ui.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0; group.interactable = group.blocksRaycasts = false;
        Bind(ui, "visibility", group); Bind(ui, "rewards", flow);
        Transform oldSummary = root.Find("SummaryPanel"); if (oldSummary != null) Object.DestroyImmediate(oldSummary.gameObject);
        RectTransform summary = Panel(root, "SummaryPanel", new Vector2(460,440));
        Vertical(summary, 18);
        Label(summary, "Title", "전투 승리", 42, 64);
        TMP_Text wallet = Label(summary, "Wallet", "보유 골드: 0", 26, 44);
        Button gold = Button(summary, "GoldButton", "골드 20 받기", ui.ClaimGold);
        Button card = Button(summary, "CardButton", "카드 보상", ui.ShowReward);
        Button finish = Button(summary, "SkipRemainingButton", "남은 보상 건너뛰기", ui.SkipRemaining);
        Bind(ui,"summaryPanel",summary.gameObject); Bind(ui,"goldButton",gold); Bind(ui,"cardButton",card);
        Bind(ui,"goldText",gold.GetComponentInChildren<TMP_Text>()); Bind(ui,"walletText",wallet); Bind(ui,"finishText",finish.GetComponentInChildren<TMP_Text>());
        RectTransform choices = (RectTransform)root.Find("DeckSelectPanel");
        ConfigureTitle(choices.Find("Title"), "카드 보상 선택");
        Button dream = ConfigureButton(choices.Find("DreamButton"), "꿈 · 3장 중 1장", ui.SelectDream);
        Button nightmare = ConfigureButton(choices.Find("NightmareButton"), "악몽 · 2장 중 1장", ui.SelectNightmare);
        ConfigureButton(choices.Find("SkipButton"), "카드 보상 건너뛰기", ui.SkipCard);
        Bind(ui,"dreamButton",dream); Bind(ui,"nightmareButton",nightmare);
        ConfigureDeckChoiceLayout(choices);
        RectTransform options = (RectTransform)root.Find("CardRewardPanel"); Center(options,new Vector2(1000,520));
        Transform title = options.Find("Title"); ConfigureTitle(title,"카드를 한 장 선택하세요");
        Center((RectTransform)title,new Vector2(900,64)); ((RectTransform)title).anchoredPosition = new Vector2(0,220);
        Transform previous = options.Find("Options");
        RectTransform row = previous != null ? (RectTransform)previous : Panel(options,"Options",new Vector2(950,380));
        var horizontal = row.GetComponent<HorizontalLayoutGroup>(); if (horizontal == null) horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 30; horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = horizontal.childControlHeight = false;
        horizontal.childForceExpandWidth = horizontal.childForceExpandHeight = false;
        ReplaceRewardCardSlots(ui);
        ConfigureCardConfirmation(ui);
        summary.gameObject.SetActive(false); choices.gameObject.SetActive(false); options.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Battle reward scene configured.");
    }
    [MenuItem("ProjectDN/Connect Reward Card Prefabs")]
    public static void ConnectRewardCardPrefabs()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity");
        ReplaceRewardCardSlots(Object.FindFirstObjectByType<CardRewardUI>(FindObjectsInactive.Include));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Reward cards connected to the DeckCard prefab.");
    }

    private static void ReplaceRewardCardSlots(CardRewardUI ui)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DeckCard.prefab");
        var config = new SerializedObject(ui);
        var slots = config.FindProperty("rewardCardSlots");
        for (int i = 0; i < slots.arraySize; i++)
        {
            var previous = (DeckCardUI)slots.GetArrayElementAtIndex(i).objectReferenceValue;
            if (PrefabUtility.GetCorrespondingObjectFromSource(previous) == prefab.GetComponent<DeckCardUI>())
                continue;

            GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previous.transform.parent);
            replacement.name = previous.name;
            replacement.transform.SetSiblingIndex(previous.transform.GetSiblingIndex());
            var card = replacement.GetComponent<DeckCardUI>();
            Center((RectTransform)card.transform, new Vector2(260, 338));
            card.ResizeForList(1.3f);
            foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            foreach (Canvas canvas in card.GetComponentsInChildren<Canvas>(true))
                canvas.overrideSorting = false;
            ConfigureRewardCardHover(card);
            var fields = new SerializedObject(card);
            fields.FindProperty("allowInspection").boolValue = false;
            fields.ApplyModifiedPropertiesWithoutUndo();
            Button button = card.GetComponent<Button>();
            if (button == null) button = replacement.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<UnityEngine.UI.Image>();
            button.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddIntPersistentListener(button.onClick, ui.SelectCard, i);
            EditorUtility.SetDirty(button);

            // Keep the existing selection border and its CardRewardUI reference.
            Transform border = previous.transform.Find("HoverVisual/RewardSelectionBorder");
            if (border != null) border.SetParent(card.transform.Find("HoverVisual"), false);
            replacement.SetActive(previous.gameObject.activeSelf);
            PrefabUtility.RecordPrefabInstancePropertyModifications(replacement);
            PrefabUtility.RecordPrefabInstancePropertyModifications(card.transform);
            slots.GetArrayElementAtIndex(i).objectReferenceValue = card;
            Object.DestroyImmediate(previous.gameObject);
        }
        config.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("ProjectDN/Setup Reward Card Confirmation")]
    public static void SetupCardConfirmation()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-VF SDF.asset");
        ConfigureCardConfirmation(Object.FindFirstObjectByType<CardRewardUI>(FindObjectsInactive.Include));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Reward card selection borders and confirmation button connected.");
    }

    private static void ConfigureCardConfirmation(CardRewardUI ui)
    {
        var config = new SerializedObject(ui);
        var slots = config.FindProperty("rewardCardSlots");
        var borders = config.FindProperty("selectionBorders");
        borders.arraySize = slots.arraySize;
        for (int i = 0; i < slots.arraySize; i++)
        {
            var card = (DeckCardUI)slots.GetArrayElementAtIndex(i).objectReferenceValue;
            Transform visual = card.transform.Find("HoverVisual");
            Transform border = visual.Find("RewardSelectionBorder");
            if (border == null)
            {
                RectTransform rect = Panel(visual, "RewardSelectionBorder", Vector2.zero);
                Stretch(rect);
                AddSelectionEdge(rect, "Top", new Vector2(0, 1), Vector2.one, new Vector2(0, 4), new Vector2(0, -2));
                AddSelectionEdge(rect, "Bottom", Vector2.zero, new Vector2(1, 0), new Vector2(0, 4), new Vector2(0, 2));
                AddSelectionEdge(rect, "Left", Vector2.zero, new Vector2(0, 1), new Vector2(4, 0), new Vector2(2, 0));
                AddSelectionEdge(rect, "Right", new Vector2(1, 0), Vector2.one, new Vector2(4, 0), new Vector2(-2, 0));
                border = rect;
            }
            border.gameObject.SetActive(false);
            borders.GetArrayElementAtIndex(i).objectReferenceValue = border.gameObject;
        }
        Transform panel = ui.transform.Find("CardRewardPanel");
        Transform existing = panel.Find("ConfirmButton");
        Button confirm;
        if (existing != null) confirm = ConfigureButton(existing, "확인", ui.ConfirmCard);
        else
        {
            confirm = Button(panel, "ConfirmButton", "확인", ui.ConfirmCard);
            Center((RectTransform)confirm.transform, new Vector2(220, 60));
            ((RectTransform)confirm.transform).anchoredPosition =
                ((RectTransform)panel.Find("Options")).anchoredPosition + new Vector2(0, -280);
        }
        confirm.interactable = false;
        config.FindProperty("confirmButton").objectReferenceValue = confirm;
        config.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddSelectionEdge(RectTransform border, string name, Vector2 min, Vector2 max, Vector2 size, Vector2 position)
    {
        RectTransform edge = Panel(border, name, size);
        edge.anchorMin = min; edge.anchorMax = max; edge.sizeDelta = size; edge.anchoredPosition = position;
        var image = edge.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(.1f, 1f, .2f, 1f);
        image.raycastTarget = false;
    }

    [MenuItem("ProjectDN/Connect Reward Card Hover")]
    public static void ConnectRewardCardHover()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity");
        CardRewardUI ui = Object.FindFirstObjectByType<CardRewardUI>(FindObjectsInactive.Include);
        var slots = new SerializedObject(ui).FindProperty("rewardCardSlots");
        for (int i = 0; i < slots.arraySize; i++)
            ConfigureRewardCardHover((DeckCardUI)slots.GetArrayElementAtIndex(i).objectReferenceValue);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Reward card hover connected without changing card layout.");
    }

    private static void ConfigureRewardCardHover(DeckCardUI card)
    {
        var visual = (RectTransform)card.transform.Find("HoverVisual");
        if (visual == null) throw new System.InvalidOperationException("Reward card is missing HoverVisual.");
        Canvas visualCanvas = visual.GetComponent<Canvas>();
        Canvas rewardCanvas = card.GetComponentInParent<CardRewardUI>(true).GetComponent<Canvas>();
        DeckCardHoverUI hover = card.GetComponent<DeckCardHoverUI>();
        if (hover == null) hover = card.gameObject.AddComponent<DeckCardHoverUI>();
        var settings = new SerializedObject(hover);
        settings.FindProperty("hoverVisual").objectReferenceValue = visual;
        settings.FindProperty("hoverCanvas").objectReferenceValue = visualCanvas;
        // DeckCardHoverUI adds its offset to the root Canvas; the reward overlay sorts independently.
        settings.FindProperty("sortingOrderOffset").intValue =
            rewardCanvas.sortingOrder - rewardCanvas.rootCanvas.sortingOrder + 10;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("ProjectDN/Restore Reward Deck Choice Layout")]
    public static void RestoreDeckChoiceLayout()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity");
        CardRewardUI ui = Object.FindFirstObjectByType<CardRewardUI>(FindObjectsInactive.Include);
        ConfigureDeckChoiceLayout((RectTransform)ui.transform.Find("DeckSelectPanel"));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Reward deck choices restored to the original left/right layout.");
    }

    private static void ConfigureDeckChoiceLayout(RectTransform choices)
    {
        var layout = choices.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (layout != null) Object.DestroyImmediate(layout);
        SetAnchors(choices, new Vector2(.2f, .35f), new Vector2(.8f, .85f));
        SetChoiceAnchors(choices.Find("Title"), new Vector2(.1f, .85f), new Vector2(.9f, .95f));
        SetChoiceAnchors(choices.Find("DreamButton"), new Vector2(.1f, .4f), new Vector2(.4f, .7f));
        SetChoiceAnchors(choices.Find("NightmareButton"), new Vector2(.6f, .4f), new Vector2(.9f, .7f));
        SetChoiceAnchors(choices.Find("SkipButton"), new Vector2(.35f, .1f), new Vector2(.65f, .3f));
    }

    private static void SetChoiceAnchors(Transform child, Vector2 min, Vector2 max)
    {
        var element = child.GetComponent<UnityEngine.UI.LayoutElement>();
        if (element != null) Object.DestroyImmediate(element);
        SetAnchors((RectTransform)child, min, max);
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void Bind(Object target,string field,Object value)
    { var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; }
    private static void Center(RectTransform rect, Vector2 size) { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f); rect.sizeDelta = size; rect.anchoredPosition = Vector2.zero; rect.localScale = Vector3.one; }
    private static RectTransform Panel(Transform parent,string name,Vector2 size)
    { var go = new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); var rect = (RectTransform)go.transform; Center(rect,size); return rect; }
    private static void Vertical(RectTransform rect,float spacing)
    { var layout = rect.GetComponent<VerticalLayoutGroup>(); if (layout == null) layout = rect.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = spacing; layout.childAlignment = TextAnchor.MiddleCenter; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.childForceExpandWidth = true; }
    private static TMP_Text Label(Transform parent,string name,string text,float size,float height)
    { var rect = Panel(parent,name,new Vector2(440,height)); var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.text = text; label.fontSize = size; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; var layout = rect.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = height; return label; }
    private static Button Button(Transform parent,string name,string text,UnityAction action)
    { var rect = Panel(parent,name,new Vector2(440,64)); rect.gameObject.AddComponent<Image>().color = new Color(.18f,.23f,.32f,1); rect.gameObject.AddComponent<UnityEngine.UI.Button>(); Label(rect,"Text",text,28,64); Stretch((RectTransform)rect.GetChild(0)); return ConfigureButton(rect,text,action); }
    private static Button ConfigureButton(Transform root,string text,UnityAction action)
    { var button = root.GetComponent<Button>(); button.onClick = new Button.ButtonClickedEvent(); UnityEventTools.AddPersistentListener(button.onClick,action); var label = root.GetComponentInChildren<TMP_Text>(true); label.font = font; label.text = text; label.fontSize = 28; label.alignment = TextAlignmentOptions.Center; var layout = root.GetComponent<LayoutElement>(); if (layout == null) layout = root.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 64; return button; }
    private static void ConfigureTitle(Transform root,string text)
    { var label = root.GetComponent<TMP_Text>(); label.font = font; label.text = text; label.fontSize = 36; label.alignment = TextAlignmentOptions.Center; var layout = root.GetComponent<LayoutElement>(); if (layout == null) layout = root.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 64; }
}
