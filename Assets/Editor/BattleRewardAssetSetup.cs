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
        var config = new SerializedObject(sampler);
        SerializedProperty pools = config.FindProperty("pools"); pools.arraySize = 14;
        int[] weights = {35,35,13,12,2,2,1};
        CardType[] types = {CardType.Attack,CardType.Skill,CardType.Attack,CardType.Skill,CardType.Attack,CardType.Skill,CardType.Continuous};
        CardRarity[] rarities = {CardRarity.Common,CardRarity.Common,CardRarity.Uncommon,CardRarity.Uncommon,CardRarity.Rare,CardRarity.Rare,CardRarity.Rare};
        for (int d = 0; d < 2; d++) for (int i = 0; i < 7; i++)
        {
            string deckName = d == 0 ? "Dream" : "Nightmare";
            string name = i == 6 ? $"Soldier{deckName}ContinuousReward" : $"Soldier{deckName}{rarities[i]}{types[i]}Reward";
            string path = $"Assets/CardDatabase/Soldier/{deckName}/{types[i]}/{name}.asset";
            var entry = pools.GetArrayElementAtIndex(d * 7 + i);
            entry.FindPropertyRelative("deckType").intValue = d;
            entry.FindPropertyRelative("type").intValue = (int)types[i];
            entry.FindPropertyRelative("rarity").intValue = (int)rarities[i];
            entry.FindPropertyRelative("weight").intValue = weights[i];
            CardDatabase database = AssetDatabase.LoadAssetAtPath<CardDatabase>(path);
            if (database == null) throw new System.InvalidOperationException(path);
            entry.FindPropertyRelative("database").objectReferenceValue = database;
        }
        config.ApplyModifiedPropertiesWithoutUndo();
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
        RectTransform choices = (RectTransform)root.Find("DeckSelectPanel"); Center(choices,new Vector2(460,400)); Vertical(choices,20);
        ConfigureTitle(choices.Find("Title"), "카드 보상 선택");
        Button dream = ConfigureButton(choices.Find("DreamButton"), "꿈 · 3장 중 1장", ui.SelectDream);
        Button nightmare = ConfigureButton(choices.Find("NightmareButton"), "악몽 · 2장 중 1장", ui.SelectNightmare);
        ConfigureButton(choices.Find("SkipButton"), "카드 보상 건너뛰기", ui.SkipCard);
        Bind(ui,"dreamButton",dream); Bind(ui,"nightmareButton",nightmare);
        RectTransform options = (RectTransform)root.Find("CardRewardPanel"); Center(options,new Vector2(1000,520));
        Transform title = options.Find("Title"); ConfigureTitle(title,"카드를 한 장 선택하세요");
        Center((RectTransform)title,new Vector2(900,64)); ((RectTransform)title).anchoredPosition = new Vector2(0,220);
        Transform previous = options.Find("Options");
        RectTransform row = previous != null ? (RectTransform)previous : Panel(options,"Options",new Vector2(950,380));
        var horizontal = row.GetComponent<HorizontalLayoutGroup>(); if (horizontal == null) horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 30; horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = horizontal.childControlHeight = false;
        horizontal.childForceExpandWidth = horizontal.childForceExpandHeight = false;
        var uiConfig = new SerializedObject(ui); var slots = uiConfig.FindProperty("rewardCardSlots");
        for (int i = 0; i < slots.arraySize; i++)
        {
            DeckCardUI slot = (DeckCardUI)slots.GetArrayElementAtIndex(i).objectReferenceValue;
            GameObject oldSlot = slot.gameObject;
            GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DeckCard.prefab");
            GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, row);
            PrefabUtility.UnpackPrefabInstance(replacement, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            replacement.name = "RewardCard" + (i + 1);
            slot = replacement.GetComponent<DeckCardUI>();
            slots.GetArrayElementAtIndex(i).objectReferenceValue = slot;
            Object.DestroyImmediate(oldSlot);
            Center((RectTransform)slot.transform, new Vector2(260,338));
            slot.ResizeForList(1.3f);
            foreach (DeckCardHoverUI hover in slot.GetComponentsInChildren<DeckCardHoverUI>(true)) Object.DestroyImmediate(hover);
            foreach (Canvas childCanvas in slot.GetComponentsInChildren<Canvas>(true)) childCanvas.overrideSorting = false;
            var slotConfig = new SerializedObject(slot); slotConfig.FindProperty("allowInspection").boolValue = false; slotConfig.ApplyModifiedPropertiesWithoutUndo();
            Button button = slot.GetComponent<Button>(); if (button == null) button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = slot.GetComponentInChildren<Image>(true);
            button.onClick = new Button.ButtonClickedEvent(); UnityEventTools.AddIntPersistentListener(button.onClick,ui.SelectCard,i);
            slot.gameObject.SetActive(false);
        }
        uiConfig.ApplyModifiedPropertiesWithoutUndo();
        summary.gameObject.SetActive(false); choices.gameObject.SetActive(false); options.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Battle reward scene configured.");
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
