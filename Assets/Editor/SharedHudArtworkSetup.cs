using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Explicit import/application only. Existing prefab identities and gameplay bindings are retained.
public static class SharedHudArtworkSetup
{
    private const string Art = "Assets/Images/UI/CharacterHUD";
    private static readonly string[] Prefabs =
    {
        "Assets/Prefabs/UI/CharacterInfoUI.prefab",
        "Assets/Prefabs/UI/CharacterSizes/SmallCharacterInfoUI.prefab",
        "Assets/Prefabs/UI/CharacterSizes/NormalCharacterInfoUI.prefab",
        "Assets/Prefabs/UI/CharacterSizes/LargeCharacterInfoUI.prefab",
        "Assets/Prefabs/Player/Soldier.prefab",
        "Assets/Prefabs/Enemy/Slime.prefab",
        "Assets/Prefabs/Enemy/Scarecrow.prefab",
        "Assets/Prefabs/Enemy/Lizard.prefab"
    };

    [MenuItem("ProjectDN/Apply Shared HUD Artwork")]
    public static void Apply()
    {
        Folder(Art);
        Sprite frame = Import("GeneratedArt/UI/HealthBar-v1/hp-frame.png", "HpFrame.png");
        Sprite track = Import("GeneratedArt/UI/HealthBar-v1/hp-track.png", "HpTrack.png");
        Sprite fill = Import("GeneratedArt/UI/HealthBar-v1/hp-fill.png", "HpFill.png");
        Sprite defense = Import("GeneratedArt/UI/Defense-v1/defense-slot-416x294.png", "Defense.png");
        foreach (string path in Prefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (CharacterInfoUI hud in root.GetComponentsInChildren<CharacterInfoUI>(true))
                    Apply(hud, frame, track, fill, defense);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Shared HP and defense artwork applied to all eight HUD/character prefabs.");
    }

    private static void Apply(CharacterInfoUI hud, Sprite frame, Sprite track, Sprite fill, Sprite defense)
    {
        RectTransform hp = Find(hud.transform, "HPBar");
        RectTransform background = Find(hp, "Background");
        RectTransform healthFill = Find(hp, "Fill");
        RectTransform hpText = Find(hp, "HPText");
        RectTransform block = Find(hud.transform, "Block");
        RectTransform blockText = Find(block, "BlockText");
        Clear(hud.GetComponent<UnityEngine.UI.Image>());
        Clear(hp.GetComponent<UnityEngine.UI.Image>());
        SetImage(background, track); SetImage(healthFill, fill);
        var fillImage = healthFill.GetComponent<UnityEngine.UI.Image>();
        fillImage.type = UnityEngine.UI.Image.Type.Filled;
        fillImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0; fillImage.fillAmount = 1;
        Vector2 innerMin = new Vector2(48f / 1072, 22f / 120);
        Vector2 innerMax = Vector2.one - innerMin;
        Stretch(background, innerMin, innerMax); Stretch(healthFill, innerMin, innerMax);

        RectTransform frameRect = hp.Find("Frame") as RectTransform;
        if (frameRect == null)
        {
            var item = new GameObject("Frame", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            item.layer = hp.gameObject.layer; frameRect = (RectTransform)item.transform; frameRect.SetParent(hp, false);
        }
        SetImage(frameRect, frame); Stretch(frameRect, Vector2.zero, Vector2.one);
        background.SetAsFirstSibling(); healthFill.SetSiblingIndex(1);
        frameRect.SetAsLastSibling(); hpText.SetAsLastSibling();
        Stretch(hpText, Vector2.zero, Vector2.one);
        SetImage(block, defense); block.GetComponent<UnityEngine.UI.Image>().preserveAspect = true;
        // Keep numbers inside the quiet plate, above the lower shield tip.
        Stretch(blockText, new Vector2(.24f, .30f), new Vector2(.76f, .80f));
        StyleText(hpText.GetComponent<TMPro.TMP_Text>(), 11);
        StyleText(blockText.GetComponent<TMPro.TMP_Text>(), 8);
        foreach (UnityEngine.UI.Graphic graphic in hud.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            graphic.raycastTarget = false;
        var state = new SerializedObject(hud);
        state.FindProperty("hpFill").objectReferenceValue = fillImage;
        state.FindProperty("hpText").objectReferenceValue = hpText.GetComponent<TMPro.TMP_Text>();
        state.FindProperty("blockText").objectReferenceValue = blockText.GetComponent<TMPro.TMP_Text>();
        state.ApplyModifiedPropertiesWithoutUndo();
        hud.ValidateBindings(); hud.SetBlock(0);
    }

    private static void StyleText(TMPro.TMP_Text text, float minimum)
    {
        float maximum = Mathf.Max(minimum, text.fontSize);
        text.enableAutoSizing = true; text.fontSizeMin = minimum; text.fontSizeMax = maximum;
        text.fontStyle |= TMPro.FontStyles.Bold;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.color = new Color32(255, 245, 223, 255);
        text.margin = Vector4.zero;
        text.overflowMode = TMPro.TextOverflowModes.Overflow;
    }
    private static void Clear(UnityEngine.UI.Image image)
    {
        if (image == null) return;
        image.sprite = null; image.color = Color.clear; image.raycastTarget = false;
    }
    private static void SetImage(RectTransform rect, Sprite sprite)
    {
        var image = rect.GetComponent<UnityEngine.UI.Image>();
        if (image == null) image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite; image.color = Color.white; image.type = UnityEngine.UI.Image.Type.Simple;
        image.preserveAspect = false; image.raycastTarget = false;
    }
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = Vector2.one * .5f;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }
    private static RectTransform Find(Transform parent, string name)
    {
        var item = parent.Find(name) as RectTransform;
        if (item == null) throw new InvalidOperationException(parent.name + " needs " + name);
        return item;
    }
    private static Sprite Import(string source, string name)
    {
        string project = Path.GetDirectoryName(Application.dataPath);
        string sourcePath = Path.Combine(project, source);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Generated HUD source is missing.", sourcePath);
        string path = Art + "/" + name;
        File.Copy(sourcePath, path, true);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None; importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("HUD sprite import failed: " + path);
        return sprite;
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
