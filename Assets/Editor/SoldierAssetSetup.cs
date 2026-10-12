using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class SoldierAssetSetup
{
    private const string ExampleFolder = "Assets/CardData/Soldier/Examples";
    public const string PrefabPath = "Assets/Prefabs/Player/Soldier.prefab";

    [MenuItem("ProjectDN/Create Soldier Assets")]
    public static void CreateAssets()
    {
        EnsureFolder(ExampleFolder);
        EnsureFolder("Assets/Prefabs/Player");
        CreatePrefab();
        RemovePrefabAmmoUI();
        CreateExampleCards();
        AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(PrefabPath));
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { ExampleFolder }))
            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
        Debug.Log("Soldier prefab and ammo example cards are ready. BattleScene was not modified.");
    }

    [MenuItem("ProjectDN/Use Soldier In BattleScene")]
    public static void UseSoldierInBattleScene()
    {
        const string path = "Assets/Scenes/BattleScene.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            Player source = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                source = root.GetComponentInChildren<Player>(true);
                if (source != null) break;
            }
            if (source == null) throw new InvalidOperationException("BattleScene has no Player.");
            GameObject obj = source.gameObject;
            if (!(source is Soldier))
            {
                // Replace the script in place to retain the component fileID and all scene references.
                SerializedObject state = new SerializedObject(source);
                state.FindProperty("m_Script").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MonoScript>(
                    "Assets/Scripts/BattleCharacter/Player/Soldier.cs");
                state.ApplyModifiedPropertiesWithoutUndo();
            }
            Soldier soldier = obj.GetComponent<Soldier>();
            if (soldier == null) throw new InvalidOperationException("Player script replacement failed.");
            obj.name = "Soldier";
            AmmoSystem ammo = obj.GetComponent<AmmoSystem>();
            if (ammo == null)
            {
                ammo = obj.AddComponent<AmmoSystem>();
                SetInt(ammo, "maxAmmo", 10);
                SetInt(ammo, "startingAmmo", 0);
            }
            ConfigureAmmoHud(scene, ammo);
            RemovePrefabAmmoUI();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save BattleScene.");
            Debug.Log("BattleScene now uses Soldier with its existing deck and HUD references.");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void CreatePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;

        const string scenePath = "Assets/Scenes/BattleScene.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        GameObject clone = null;
        try
        {
            Player source = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                source = root.GetComponentInChildren<Player>(true);
                if (source != null) break;
            }
            if (source == null) throw new InvalidOperationException("BattleScene has no Player.");

            clone = Object.Instantiate(source.gameObject);
            clone.name = "Soldier";
            Player oldPlayer = clone.GetComponent<Player>();
            SerializedObject oldState = new SerializedObject(oldPlayer);
            int maxHP = oldState.FindProperty("maxHP").intValue;
            Object characterInfo = oldState.FindProperty("characterInfoUI").objectReferenceValue;
            Object.DestroyImmediate(oldPlayer);

            Soldier soldier = clone.AddComponent<Soldier>();
            CostSystem cost = clone.GetComponent<CostSystem>();
            SerializedObject playerState = new SerializedObject(soldier);
            playerState.FindProperty("maxHP").intValue = maxHP;
            playerState.FindProperty("currentHP").intValue = 0;
            playerState.FindProperty("block").intValue = 0;
            playerState.FindProperty("characterInfoUI").objectReferenceValue = characterInfo;
            playerState.FindProperty("costSystem").objectReferenceValue = cost;
            playerState.ApplyModifiedPropertiesWithoutUndo();

            // Scene HUD references cannot be stored in a reusable prefab.
            SerializedObject costState = new SerializedObject(cost);
            foreach (string field in new[] { "dreamCostUI", "nightmareCostUI", "dreamGaugeUI", "nightmareGaugeUI" })
                costState.FindProperty(field).objectReferenceValue = null;
            costState.ApplyModifiedPropertiesWithoutUndo();

            AmmoSystem ammo = clone.GetComponent<AmmoSystem>();
            SetInt(ammo, "maxAmmo", 10);
            SetInt(ammo, "startingAmmo", 0);
            foreach (AmmoUI ui in clone.GetComponentsInChildren<AmmoUI>(true))
                Object.DestroyImmediate(ui.gameObject);

            if (PrefabUtility.SaveAsPrefabAsset(clone, PrefabPath) == null)
                throw new InvalidOperationException("Could not save Soldier prefab.");
        }
        finally
        {
            if (clone != null) Object.DestroyImmediate(clone);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void RemovePrefabAmmoUI()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return;
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            AmmoUI[] widgets = root.GetComponentsInChildren<AmmoUI>(true);
            if (widgets.Length == 0) return;
            foreach (AmmoUI widget in widgets)
            {
                if (widget.gameObject == root) Object.DestroyImmediate(widget);
                else Object.DestroyImmediate(widget.gameObject);
            }
            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                throw new InvalidOperationException("Could not remove prefab ammo UI.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureAmmoHud(Scene scene, AmmoSystem ammo)
    {
        Transform area = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "PlayerSpecialAbility" || child.name == "PlayerSpecialAbillity")
                {
                    area = child;
                    break;
                }
            if (area != null) break;
        }
        if (area == null) throw new InvalidOperationException("PlayerSpecialAbility is missing.");

        AmmoUI previous = ammo.GetComponentInChildren<AmmoUI>(true);
        TMP_Text text = area.GetComponentInChildren<TMP_Text>(true);
        if (text == null && previous != null)
        {
            text = previous.GetComponent<TMP_Text>();
            if (text != null) text.transform.SetParent(area, false);
        }
        if (text == null)
        {
            GameObject label = new GameObject("AmmoText", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.layer = area.gameObject.layer;
            label.transform.SetParent(area, false);
            text = label.GetComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-VF SDF.asset");
            text.fontSize = 24;
            text.color = Color.black;
        }
        text.name = "AmmoText";
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = $"총알 {ammo.CurrentAmmo} / {ammo.MaxAmmo}";
        SetAnchors(text.rectTransform, new Vector2(0.08f, 0.56f), new Vector2(0.92f, 0.74f));

        UnityEngine.UI.Image background = EnsureImage(area, "AmmoBarBackground");
        SetAnchors(background.rectTransform, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.5f));
        background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        background.color = new Color(0.2f, 0.2f, 0.2f, 1);
        background.type = UnityEngine.UI.Image.Type.Simple;
        background.raycastTarget = false;
        UnityEngine.UI.Image fill = EnsureImage(background.transform, "Fill");
        SetAnchors(fill.rectTransform, Vector2.zero, Vector2.one);
        fill.sprite = background.sprite;
        fill.color = new Color(0.95f, 0.72f, 0.25f, 1);
        fill.type = UnityEngine.UI.Image.Type.Filled;
        fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = ammo.MaxAmmo > 0 ? (float)ammo.CurrentAmmo / ammo.MaxAmmo : 0;
        fill.raycastTarget = false;

        AmmoUI hud = area.GetComponent<AmmoUI>();
        if (hud == null) hud = area.gameObject.AddComponent<AmmoUI>();
        SerializedObject state = new SerializedObject(hud);
        state.FindProperty("ammoSystem").objectReferenceValue = ammo;
        state.FindProperty("ammoText").objectReferenceValue = text;
        state.FindProperty("ammoFill").objectReferenceValue = fill;
        state.ApplyModifiedPropertiesWithoutUndo();
        if (previous != null && previous != hud) Object.DestroyImmediate(previous);
    }

    private static UnityEngine.UI.Image EnsureImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            UnityEngine.UI.Image image = existing.GetComponent<UnityEngine.UI.Image>();
            if (image == null) image = existing.gameObject.AddComponent<UnityEngine.UI.Image>();
            return image;
        }
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<UnityEngine.UI.Image>();
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void CreateExampleCards()
    {
        CardData basicAttack = AssetDatabase.LoadAssetAtPath<CardData>(
            "Assets/CardData/Basic/Dream/Attack/1.Card_Attack/Attack.asset");
        DamageEffect damage = AssetDatabase.LoadAssetAtPath<DamageEffect>(
            "Assets/CardData/Basic/Dream/Attack/1.Card_Attack/Attack_DamageEffect.asset");
        if (basicAttack == null || damage == null)
            throw new InvalidOperationException("Basic attack assets are missing.");

        ReloadAmmoEffect reload = EnsureAsset<ReloadAmmoEffect>("ReloadEffect", value => SetInt(value, "amount", 1));
        SpendAmmoBonusEffect bonus = EnsureAsset<SpendAmmoBonusEffect>("BonusEffect", value =>
        {
            SetInt(value, "ammoCost", 1);
            SerializedObject state = new SerializedObject(value);
            SerializedProperty effects = state.FindProperty("bonusEffects");
            effects.arraySize = 1;
            effects.GetArrayElementAtIndex(0).objectReferenceValue = damage;
            state.ApplyModifiedPropertiesWithoutUndo();
        });

        EnsureAsset<CardData>("Reload", card =>
        {
            Configure(card, basicAttack, "장전 예시", "총알을 1알 장전합니다. 최대치에서는 장전 효과가 없습니다.");
            card.type = CardType.Skill;
            card.effects = new CardEffect[] { reload };
        });
        EnsureAsset<CardData>("AmmoBonus", card =>
        {
            Configure(card, basicAttack, "총알 강화 예시", "기본 공격 효과. 총알이 1알 이상이면 반드시 1알을 소모하여 추가로 6의 피해를 줍니다.");
            var effects = new List<CardEffect>(basicAttack.effects);
            effects.Add(bonus);
            card.effects = effects.ToArray();
        });
        EnsureAsset<CardData>("RequireAmmo", card =>
        {
            Configure(card, basicAttack, "총알 보유 조건 예시", "총알이 1알 이상 있어야 사용할 수 있습니다. 총알은 소모하지 않습니다.");
            card.requiredAmmo = 1;
        });
        EnsureAsset<CardData>("ConsumeAmmo", card =>
        {
            Configure(card, basicAttack, "총알 소모 조건 예시", "총알이 1알 이상 있어야 사용할 수 있으며, 사용 시 1알을 소모합니다.");
            card.requiredAmmo = 1;
            card.consumeRequiredAmmo = true;
        });

        IncreaseMaxAmmoEffect capacity = EnsureAsset<IncreaseMaxAmmoEffect>("IncreaseMaxAmmoEffect",
            value => SetInt(value, "amount", 1));
        EnsureAsset<CardData>("IncreaseMaxAmmo", card =>
        {
            Configure(card, basicAttack, "탄창 확장 예시", "이번 전투 동안 최대 총알이 1알 증가합니다. 총알을 장전하지는 않습니다.");
            card.type = CardType.Skill;
            card.effects = new CardEffect[] { capacity };
        });
        EnsureAsset<AmmoCapacityRelic>("AmmoCapacityRelic", relic =>
        {
            SerializedObject state = new SerializedObject(relic);
            state.FindProperty("relicName").stringValue = "확장 탄창 예시";
            state.FindProperty("description").stringValue = "이번 게임 동안 최대 총알이 1알 증가합니다.";
            state.FindProperty("maxAmmoBonus").intValue = 1;
            state.ApplyModifiedPropertiesWithoutUndo();
        });
    }

    private static void Configure(CardData card, CardData template, string name, string description)
    {
        card.cardName = name;
        card.description = description;
        card.cost = template.cost;
        card.deckType = template.deckType;
        card.type = template.type;
        card.rarity = template.rarity;
        card.artwork = template.artwork;
        card.effects = template.effects;
    }

    private static T EnsureAsset<T>(string name, Action<T> configure) where T : ScriptableObject
    {
        string path = $"{ExampleFolder}/{name}.asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        configure(asset);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void SetInt(Object target, string name, int value)
    {
        SerializedObject state = new SerializedObject(target);
        state.FindProperty(name).intValue = value;
        state.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string parent = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string current = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(current)) AssetDatabase.CreateFolder(parent, parts[i]);
            parent = current;
        }
    }
}
