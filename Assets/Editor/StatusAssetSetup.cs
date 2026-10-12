using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class StatusAssetSetup
{
    public const string DefinitionFolder = "Assets/StatusData";
    public const string ExampleFolder = "Assets/CardData/Status/Examples";
    public const string IconPath = "Assets/Prefabs/UI/StatusIcon.prefab";

    [MenuItem("ProjectDN/Create Status Assets")]
    public static void CreateAssets()
    {
        EnsureFolder(DefinitionFolder);
        EnsureFolder(ExampleFolder);
        CreateIcon();
        StatusEffectData exposure = CreateDefinition("WeaknessExposure", "약점 노출", StatusEffectType.WeaknessExposure);
        StatusEffectData rust = CreateDefinition("Rust", "녹슬음", StatusEffectType.Rust);
        foreach (CardTarget target in new[] { CardTarget.Player, CardTarget.Enemy })
        {
            CreateCard(exposure, "WeaknessExposure", target);
            CreateCard(rust, "Rust", target);
        }
        ConfigurePrefab("Assets/Prefabs/UI/CharacterInfoUI.prefab");
        ConfigurePrefab(SoldierAssetSetup.PrefabPath);
        const string path = "Assets/Scenes/BattleScene.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (CharacterInfoUI info in root.GetComponentsInChildren<CharacterInfoUI>(true)) ConfigureInfo(info);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save status UI bindings.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        Debug.Log("Status definitions, card examples and character status HUD bindings are ready.");
    }

    private static void CreateIcon()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(IconPath) != null) return;
        GameObject root = new GameObject("StatusIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(StatusIconUI));
        root.layer = 5;
        try
        {
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(32, 32);
            UnityEngine.UI.Image image = root.GetComponent<UnityEngine.UI.Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.raycastTarget = false;
            GameObject label = new GameObject("Turns", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.layer = 5;
            label.transform.SetParent(root.transform, false);
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(-1, 1);
            rect.sizeDelta = new Vector2(24, 20);
            TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-VF SDF.asset");
            text.fontSize = 16;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.BottomRight;
            text.raycastTarget = false;
            text.text = "1";
            SerializedObject state = new SerializedObject(root.GetComponent<StatusIconUI>());
            state.FindProperty("icon").objectReferenceValue = image;
            state.FindProperty("turnsText").objectReferenceValue = text;
            state.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.SaveAsPrefabAsset(root, IconPath) == null) throw new InvalidOperationException("Could not save status icon.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static StatusEffectData CreateDefinition(string file, string name, StatusEffectType type)
    {
        string path = $"{DefinitionFolder}/{file}.asset";
        StatusEffectData data = AssetDatabase.LoadAssetAtPath<StatusEffectData>(path);
        if (data != null) return data;
        data = ScriptableObject.CreateInstance<StatusEffectData>();
        SerializedObject state = new SerializedObject(data);
        state.FindProperty("statusType").enumValueIndex = (int)type;
        state.FindProperty("displayName").stringValue = name;
        state.FindProperty("icon").objectReferenceValue = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        state.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(data, path);
        return data;
    }

    private static void CreateCard(StatusEffectData status, string file, CardTarget target)
    {
        string name = $"{file}_{target}";
        string effectPath = $"{ExampleFolder}/{name}Effect.asset";
        ApplyStatusEffect effect = AssetDatabase.LoadAssetAtPath<ApplyStatusEffect>(effectPath);
        if (effect == null)
        {
            effect = ScriptableObject.CreateInstance<ApplyStatusEffect>();
            SerializedObject state = new SerializedObject(effect);
            state.FindProperty("status").objectReferenceValue = status;
            state.FindProperty("turns").intValue = 2;
            state.FindProperty("target").enumValueIndex = (int)target;
            state.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(effect, effectPath);
        }
        string cardPath = $"{ExampleFolder}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<CardData>(cardPath) != null) return;
        CardData template = AssetDatabase.LoadAssetAtPath<CardData>("Assets/CardData/Basic/Dream/Attack/1.Card_Attack/Attack.asset");
        if (template == null) throw new InvalidOperationException("Basic card template is missing.");
        CardData card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = $"{status.DisplayName} {(target == CardTarget.Player ? "자신" : "적")} 예시";
        card.description = $"{(target == CardTarget.Player ? "자신" : "적")}에게 {status.DisplayName}을 2턴 부여합니다.";
        card.cost = template.cost;
        card.deckType = template.deckType;
        card.type = CardType.Skill;
        card.rarity = template.rarity;
        card.artwork = template.artwork;
        card.effects = new CardEffect[] { effect };
        AssetDatabase.CreateAsset(card, cardPath);
    }

    private static void ConfigurePrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (CharacterInfoUI info in root.GetComponentsInChildren<CharacterInfoUI>(true)) ConfigureInfo(info);
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException("Could not save " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureInfo(CharacterInfoUI info)
    {
        Transform container = info.transform.Find("StatusContainer");
        if (container == null) container = info.transform.Find("StatusConatiner");
        if (container == null) throw new InvalidOperationException("CharacterInfoUI has no status container.");
        UnityEngine.UI.HorizontalLayoutGroup layout = container.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (layout == null) layout = container.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childAlignment = TextAnchor.MiddleLeft;
        StatusEffectsUI ui = container.GetComponent<StatusEffectsUI>();
        if (ui == null) ui = container.gameObject.AddComponent<StatusEffectsUI>();
        SerializedObject state = new SerializedObject(ui);
        state.FindProperty("iconPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(IconPath).GetComponent<StatusIconUI>();
        state.ApplyModifiedPropertiesWithoutUndo();
        state = new SerializedObject(info);
        state.FindProperty("statusUI").objectReferenceValue = ui;
        state.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string parent = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]);
            parent = next;
        }
    }
}
