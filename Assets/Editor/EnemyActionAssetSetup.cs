using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Authors reusable action assets without replacing the encounter's existing behavior or balance.
public static class EnemyActionAssetSetup
{
    private const string Root = "Assets/Enemy/Examples";
    private const string IconRoot = "Assets/Images/UI/EnemyActions";
    public const string IconsPath = "Assets/Enemy/DefaultEnemyActionIcons.asset";

    [MenuItem("ProjectDN/Setup Enemy Actions")]
    public static void CreateAssets()
    {
        Folder(Root); Folder(IconRoot);
        EnemyActionIconSet icons = Ensure<EnemyActionIconSet>(IconsPath, out _);
        string[] fields = { "attack", "block", "attackBlock", "debuff", "attackDebuff", "blockDebuff", "attackBlockDebuff" };
        for (int mask = 1; mask <= 7; mask++)
            Fill(icons, fields[mask - 1], Icon(fields[mask - 1]));

        EnemyAction attack = AssetDatabase.LoadAssetAtPath<EnemyAction>("Assets/Enemy/Test/Attack_Test.asset");
        EnemyAction block = AssetDatabase.LoadAssetAtPath<EnemyAction>("Assets/Enemy/Test/Block_Test.asset");
        if (attack == null || block == null) throw new InvalidOperationException("Existing attack and block assets are required.");
        Fill(attack, "iconSet", icons); Fill(block, "iconSet", icons);

        var debuff = Ensure<EnemyApplyStatusAction>(Root + "/Debuff.asset", out bool newDebuff);
        if (newDebuff)
        {
            Bind(debuff, "status", AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/WeaknessExposure.asset"));
            var state = new SerializedObject(debuff);
            state.FindProperty("actionName").stringValue = "약점 노출";
            state.ApplyModifiedPropertiesWithoutUndo();
        }
        Fill(debuff, "iconSet", icons);
        EnemyCompositeAction debuffAttack = Composite("DebuffAttack", icons, debuff, attack);
        EnemyCompositeAction blockAttack = Composite("BlockAttack", icons, block, attack);
        Composite("AllActions", icons, block, debuffAttack);
        var behavior = Ensure<EnemyBehavior>(Root + "/ExpandedBehavior.asset", out bool newBehavior);
        if (newBehavior) BindActions(behavior, new EnemyAction[] { debuff, debuffAttack, blockAttack });
        behavior.Validate();
        AssetDatabase.SaveAssets();
        ConfigureIntentBindings(icons);
        Debug.Log("Enemy actions, example behavior and seven combination icons are ready. Existing encounter patterns are unchanged.");
    }

    private static void ConfigureIntentBindings(EnemyActionIconSet icons)
    {
        const string path = "Assets/Scenes/BattleScene.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var tooltip = AssetDatabase.LoadAssetAtPath<StatusTooltipUI>("Assets/Prefabs/UI/StatusTooltip.prefab");
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (EnemyActionUI ui in root.GetComponentsInChildren<EnemyActionUI>(true))
                {
                    var binding = new SerializedObject(ui);
                    binding.FindProperty("fallbackIcons").objectReferenceValue = icons;
                    binding.FindProperty("tooltipPrefab").objectReferenceValue = tooltip;
                    binding.ApplyModifiedPropertiesWithoutUndo();
                }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save enemy intent layout.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static EnemyCompositeAction Composite(string name, EnemyActionIconSet icons, params EnemyAction[] actions)
    {
        var asset = Ensure<EnemyCompositeAction>(Root + "/" + name + ".asset", out bool added);
        if (added) BindActions(asset, actions);
        Fill(asset, "iconSet", icons);
        asset.Validate();
        return asset;
    }
    private static void BindActions(UnityEngine.Object asset, EnemyAction[] actions)
    {
        var state = new SerializedObject(asset);
        SerializedProperty list = state.FindProperty("actions");
        list.arraySize = actions.Length;
        for (int i = 0; i < actions.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = actions[i];
        state.ApplyModifiedPropertiesWithoutUndo();
    }
    private static T Ensure<T>(string path, out bool added) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        added = asset == null;
        if (added) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
    private static void Fill(UnityEngine.Object asset, string field, UnityEngine.Object value)
    {
        if (new SerializedObject(asset).FindProperty(field).objectReferenceValue == null) Bind(asset, field, value);
    }
    private static void Bind(UnityEngine.Object asset, string field, UnityEngine.Object value)
    {
        var state = new SerializedObject(asset);
        state.FindProperty(field).objectReferenceValue = value;
        state.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("ProjectDN/Import Generated Enemy Action Icons")]
    public static void ImportGeneratedIcons()
    {
        string[] names = { "attack", "block", "attackBlock", "debuff", "attackDebuff", "blockDebuff", "attackBlockDebuff" };
        // Check all sources before replacing any project asset.
        foreach (string name in names)
            if (!File.Exists(GeneratedIconSource(name)))
                throw new FileNotFoundException("Generated enemy icon source is missing.", GeneratedIconSource(name));
        Folder(IconRoot);
        foreach (string name in names)
        {
            string path = IconRoot + "/" + name + ".png";
            File.Copy(GeneratedIconSource(name), path, true);
            ImportIcon(path);
        }
        Debug.Log("Imported seven painted enemy icons with centered overlapping combinations. Existing GUIDs and references are retained.");
    }

    private static string GeneratedIconSource(string name)
    {
        string relative = name == "attack" || name == "block" || name == "debuff"
            ? name + "-v1-256x256.png" : "Composites/" + name + "-v1.png";
        return Path.Combine(Path.GetDirectoryName(Application.dataPath), "GeneratedArt/EnemyActionIcons", relative);
    }

    private static Sprite Icon(string name)
    {
        string path = IconRoot + "/" + name + ".png";
        if (!File.Exists(path))
        {
            File.Copy(GeneratedIconSource(name), path, false);
            return ImportIcon(path);
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite ImportIcon(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 256;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
