using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CharacterPresentationAssetSetup
{
    private const string Data = "Assets/CharacterPresentation";
    private const string HudRoot = "Assets/Prefabs/UI/CharacterSizes";

    [MenuItem("ProjectDN/Setup Character Presentation")]
    public static void CreateAssets()
    {
        Folder(Data + "/Animations"); Folder(Data + "/Profiles"); Folder(HudRoot);
        var animations = new Dictionary<string, UISpriteAnimation>();
        foreach (string name in new[] { "Soldier", "Slime", "Scarecrow", "Lizard" })
            animations[name] = ImportAnimation(name);
        const string scenePath = "Assets/Scenes/BattleScene.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            BattleManager manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleManager>(true)).First();
            var managerState = new SerializedObject(manager);
            EnemyActionUI legacy = managerState.FindProperty("enemyActionUI").objectReferenceValue as EnemyActionUI;
            if (legacy == null) throw new InvalidOperationException("The battle scene needs its legacy intent fallback.");
            EnemyActionUI intent = CreateIntentPrefab(legacy);
            CharacterUIProfile small = CreateProfile("Small", 352, 288, intent);
            CharacterUIProfile normal = CreateProfile("Normal", 384, 320, intent);
            CharacterUIProfile large = CreateProfile("Large", 480, 360, intent);
            ConfigurePrefab("Assets/Prefabs/Player/Soldier.prefab", normal, animations["Soldier"]);
            ConfigurePrefab("Assets/Prefabs/Enemy/TestEnemy.prefab", small, animations["Slime"]);
            CreateEnemy("Scarecrow", normal, animations["Scarecrow"]);
            CreateEnemy("Lizard", large, animations["Lizard"]);
            legacy.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save BattleScene.");
            AssetDatabase.SaveAssets();
            Debug.Log("Character presentation assets, size profiles and four idle sequences are ready.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static UISpriteAnimation ImportAnimation(string name)
    {
        string lower = name.ToLowerInvariant();
        string version = name == "Soldier" ? "v2" : "v1";
        string generated = Path.Combine(Path.GetDirectoryName(Application.dataPath), "GeneratedArt", name, "Idle-" + version);
        string source = Path.Combine(generated, lower + "-idle-spritesheet.png");
        string folder = "Assets/Images/Characters/" + name;
        Folder(folder);
        string target = folder + "/Idle.png";
        if (!File.Exists(target) || !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(target))) File.Copy(source, target, true);
        AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(target);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        TextureImporterSettings settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.spritePixelsPerUnit = 100;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 8192;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
        SpriteDataProviderFactories factory = new SpriteDataProviderFactories(); factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        Dictionary<string, SpriteRect> existing = provider.GetSpriteRects().ToDictionary(r => r.name);
        var rects = new List<SpriteRect>();
        for (int i = 0; i < 24; i++)
        {
            string frameName = name + "_" + i.ToString("00");
            rects.Add(new SpriteRect
            {
                name = frameName, rect = new Rect((i % 6) * 768, (3 - i / 6) * 768, 768, 768),
                alignment = SpriteAlignment.Custom, pivot = new Vector2(0.5f, 0.0625f),
                spriteID = existing.TryGetValue(frameName, out SpriteRect previous) ? previous.spriteID : GUID.Generate()
            });
        }
        provider.SetSpriteRects(rects.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(target).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        if (frames.Length != 24) throw new InvalidOperationException("Expected 24 frames for " + name);
        UISpriteAnimation animation = Ensure<UISpriteAnimation>(Data + "/Animations/" + name + "Idle.asset", out bool added);
        animation.frames = frames;
        if (added || animation.setupVersion < 1)
        {
            animation.frameDuration = name == "Soldier" ? 0.06f : name == "Slime" ? 0.08f : name == "Scarecrow" ? 0.10f : 0.12f;
            animation.setupVersion = 1;
        }
        animation.groundPivot = new Vector2(0.5f, 0.0625f);
        animation.contentBounds = ReadBounds(Path.Combine(generated, "frames"));
        animation.Validate(); EditorUtility.SetDirty(animation);
        AssetDatabase.SaveAssetIfDirty(animation);
        return animation;
    }

    private static Rect ReadBounds(string directory)
    {
        int minX = 768, minY = 768, maxX = 0, maxY = 0;
        foreach (string frame in Directory.GetFiles(directory, "*.png"))
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(frame))) throw new InvalidOperationException("Could not read " + frame);
                if (texture.width != 768 || texture.height != 768) throw new InvalidOperationException("Invalid frame cell size.");
                Color32[] pixels = texture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                    if (pixels[i].a > 8)
                    {
                        int x = i % 768, y = i / 768;
                        minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x + 1); maxY = Math.Max(maxY, y + 1);
                    }
            }
            finally { Object.DestroyImmediate(texture); }
        }
        return new Rect(minX / 768f, minY / 768f, (maxX - minX) / 768f, (maxY - minY) / 768f);
    }

    private static CharacterUIProfile CreateProfile(string name, float visualSize, float hudWidth, EnemyActionUI intent)
    {
        CharacterInfoUI hud = CreateHudPrefab(name);
        CharacterUIProfile profile = Ensure<CharacterUIProfile>(Data + "/Profiles/" + name + ".asset", out bool added);
        if (added || profile.setupVersion < 1)
        {
            profile.visualSize = Vector2.one * visualSize;
            profile.infoSize = new Vector2(hudWidth, 84);
            profile.infoOffset = new Vector2(0, -8);
            profile.actionSize = new Vector2(240, 112); profile.actionGap = 12;
            profile.setupVersion = 1;
        }
        if (profile.infoPrefab == null) profile.infoPrefab = hud;
        if (profile.actionPrefab == null) profile.actionPrefab = intent;
        profile.Validate(); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile); return profile;
    }

    private static CharacterInfoUI CreateHudPrefab(string name)
    {
        string path = HudRoot + "/" + name + "CharacterInfoUI.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            CharacterInfoUI saved = existing.GetComponent<CharacterInfoUI>();
            var savedState = new SerializedObject(saved);
            if (new[] { "hpFill", "hpText", "blockText", "statusUI" }.All(f => savedState.FindProperty(f).objectReferenceValue != null)) return saved;
            GameObject repair = PrefabUtility.LoadPrefabContents(path);
            try { BindHudFields(repair); return PrefabUtility.SaveAsPrefabAsset(repair, path).GetComponent<CharacterInfoUI>(); }
            finally { PrefabUtility.UnloadPrefabContents(repair); }
        }
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/CharacterInfoUI.prefab");
        try
        {
            root.name = name + "CharacterInfoUI";
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            UnityEngine.UI.Image panel = root.GetComponent<UnityEngine.UI.Image>(); if (panel != null) panel.color = Color.clear;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) { text.fontSize = 18; text.enableAutoSizing = false; }
            RectTransform hp = Find(root, "HPBar"); Stretch(hp, new Vector2(.14f, .65f), new Vector2(.96f, 1));
            RectTransform block = Find(root, "Block"); Stretch(block, new Vector2(.01f, .65f), new Vector2(.14f, 1));
            RectTransform status = Find(root, "StatusContainer"); Stretch(status, new Vector2(.14f, .05f), new Vector2(.96f, .50f));
            HorizontalLayoutGroup row = status.GetComponent<HorizontalLayoutGroup>(); if (row != null) Object.DestroyImmediate(row);
            GridLayoutGroup grid = status.GetComponent<GridLayoutGroup>(); if (grid == null) grid = status.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(28, 28); grid.spacing = new Vector2(4, 4);
            grid.childAlignment = TextAnchor.UpperLeft; grid.constraint = GridLayoutGroup.Constraint.Flexible;
            BindHudFields(root);
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<CharacterInfoUI>();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void BindHudFields(GameObject root)
    {
        var state = new SerializedObject(root.GetComponent<CharacterInfoUI>());
        state.FindProperty("hpFill").objectReferenceValue = Find(root, "Fill").GetComponent<UnityEngine.UI.Image>();
        state.FindProperty("hpText").objectReferenceValue = Find(root, "HPText").GetComponent<TMP_Text>();
        state.FindProperty("blockText").objectReferenceValue = Find(root, "BlockText").GetComponent<TMP_Text>();
        state.FindProperty("statusUI").objectReferenceValue = Find(root, "StatusContainer").GetComponent<StatusEffectsUI>();
        state.ApplyModifiedPropertiesWithoutUndo();
    }

    private static EnemyActionUI CreateIntentPrefab(EnemyActionUI source)
    {
        string path = HudRoot + "/EnemyIntentUI.prefab";
        GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (saved != null) return saved.GetComponent<EnemyActionUI>();
        GameObject root = Object.Instantiate(source.gameObject); root.name = "EnemyIntentUI"; root.SetActive(true);
        try
        {
            EnemyActionUI ui = root.GetComponent<EnemyActionUI>(); var state = new SerializedObject(ui);
            UnityEngine.UI.Image icon = (UnityEngine.UI.Image)state.FindProperty("actionIcon").objectReferenceValue;
            TMP_Text text = (TMP_Text)state.FindProperty("actionText").objectReferenceValue;
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            UnityEngine.UI.Image panel = root.GetComponent<UnityEngine.UI.Image>(); if (panel != null) panel.color = Color.clear;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, .5f); iconRect.pivot = new Vector2(0, .5f);
            iconRect.anchoredPosition = Vector2.zero; iconRect.sizeDelta = new Vector2(56, 56);
            icon.sprite = null; icon.enabled = false; icon.preserveAspect = true;
            Stretch(text.rectTransform, new Vector2(.27f, 0), Vector2.one);
            text.text = ""; text.color = new Color(.1f, .1f, .1f, 1);
            text.fontSize = 18; text.enableAutoSizing = true; text.fontSizeMin = 14; text.fontSizeMax = 18;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.Normal;
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<EnemyActionUI>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void CreateEnemy(string name, CharacterUIProfile profile, UISpriteAnimation animation)
    {
        string path = "Assets/Prefabs/Enemy/" + name + ".prefab";
        bool added = AssetDatabase.LoadAssetAtPath<GameObject>(path) == null;
        if (added)
        {
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Enemy/TestEnemy.prefab");
            try { root.name = name; PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        ConfigurePrefab(path, profile, animation, added);
    }

    private static void ConfigurePrefab(string path, CharacterUIProfile profile, UISpriteAnimation animation, bool resetSelection = false)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            BattleCharacter owner = root.GetComponent<BattleCharacter>();
            SerializedObject state = new SerializedObject(owner);
            CharacterInfoUI previous = state.FindProperty("characterInfoUI").objectReferenceValue as CharacterInfoUI;
            Transform child = root.transform.Find("Visual");
            RectTransform visual = child != null ? (RectTransform)child : new GameObject("Visual", typeof(RectTransform)).GetComponent<RectTransform>();
            visual.SetParent(root.transform, false); visual.SetAsFirstSibling();
            UnityEngine.UI.Image image = visual.GetComponent<UnityEngine.UI.Image>();
            if (image == null) image = visual.gameObject.AddComponent<UnityEngine.UI.Image>();
            UISpriteAnimator animator = visual.GetComponent<UISpriteAnimator>(); if (animator == null) animator = visual.gameObject.AddComponent<UISpriteAnimator>();
            BattleCharacterPresentation view = root.GetComponent<BattleCharacterPresentation>(); if (view == null) view = root.AddComponent<BattleCharacterPresentation>();
            if (!resetSelection)
            {
                if (view.Profile != null) profile = view.Profile;
                if (view.IdleAnimation != null) animation = view.IdleAnimation;
            }
            view.Configure(visual, animator, animation, profile);
            if (previous != null && previous != view.InfoUI) previous.gameObject.SetActive(false);
            UnityEngine.UI.Image legacy = root.GetComponent<UnityEngine.UI.Image>();
            if (legacy != null) { legacy.color = Color.clear; legacy.preserveAspect = true; }
            if (owner is Enemy)
            {
                state.Update(); state.FindProperty("actionUI").objectReferenceValue = null;
                state.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(view);
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException("Could not save " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static RectTransform Find(GameObject root, string name) => root.GetComponentsInChildren<RectTransform>(true).First(t => t.name == name);
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static T Ensure<T>(string path, out bool added) where T : ScriptableObject
    {
        T value = AssetDatabase.LoadAssetAtPath<T>(path); added = value == null;
        if (added) { value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); }
        return value;
    }
}
