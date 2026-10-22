using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Explicit setup only: never runs on domain reload or silently replaces selected presentations.
public static class CardPresentationExamplesSetup
{
    private const string Root = "Assets/CardPresentations";
    [MenuItem("ProjectDN/Setup Card Presentation Examples")]
    public static void Setup()
    {
        Folder(Root); Folder(Root + "/Steps");
        var prepare = Ensure<MoveVisualPresentationStep>(Root + "/Steps/MeleePrepare.asset", step =>
        { step.offset = new Vector2(-.02f, 0); step.duration = .05f; });
        var lunge = Ensure<MoveVisualPresentationStep>(Root + "/Steps/MeleeLunge.asset", step =>
        { step.offset = new Vector2(.08f, 0); step.rotation = -3; step.duration = .08f; });
        var hold = Ensure<WaitPresentationStep>(Root + "/Steps/MeleeHold.asset", step => step.duration = .04f);
        var recover = Ensure<MoveVisualPresentationStep>(Root + "/Steps/MeleeRecover.asset", step =>
        { step.offset = Vector2.zero; step.rotation = 0; step.duration = .15f; });
        Ensure<CardPresentation>(Root + "/Melee.asset", asset =>
        {
            asset.beforeImpact = new CardPresentationStep[] { prepare, lunge };
            asset.afterImpact = new CardPresentationStep[] { hold, recover };
        });
        UISpriteAnimation fire = ImportFire();
        if (fire != null)
        {
            var play = Ensure<PlaySpritePresentationStep>(Root + "/Steps/FireSprite.asset", step =>
            { step.animation = fire; step.waitForCompletion = false; });
            var aim = Ensure<WaitPresentationStep>(Root + "/Steps/FireImpact.asset", step => step.duration = .1f);
            var finish = Ensure<WaitPresentationStep>(Root + "/Steps/FireFinish.asset", step => step.duration = .7f);
            CardPresentation shoot = Ensure<CardPresentation>(Root + "/SoldierFire.asset", asset =>
            {
                asset.beforeImpact = new CardPresentationStep[] { play, aim };
                asset.afterImpact = new CardPresentationStep[] { finish };
            });
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(
                "Assets/CardData/Soldier/Starting/Dream/Attack/01.Card_Attack/Attack.asset");
            if (card != null && card.presentation == null)
            {
                card.presentation = shoot; EditorUtility.SetDirty(card); AssetDatabase.SaveAssetIfDirty(card);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Card presentation examples are ready. Existing assigned presentations were preserved.");
    }
    private static UISpriteAnimation ImportFire()
    {
        string source = Path.Combine(Path.GetDirectoryName(Application.dataPath), "GeneratedArt/Soldier/Fire-v2/soldier-fire-spritesheet.png");
        if (!File.Exists(source)) { Debug.LogWarning("Soldier Fire-v2 sheet is absent; only the melee preset was created."); return null; }
        const string folder = "Assets/Images/Characters/Soldier";
        Folder(folder);
        const string texturePath = folder + "/Fire.png";
        if (!File.Exists(texturePath) || !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(texturePath)))
            File.Copy(source, texturePath, true);
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None; importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var factory = new SpriteDataProviderFactories(); factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var existing = provider.GetSpriteRects().ToDictionary(rect => rect.name);
        var rects = new SpriteRect[16];
        for (int i = 0; i < rects.Length; i++)
        {
            string name = "SoldierFire_" + i.ToString("00");
            rects[i] = new SpriteRect
            {
                name = name, rect = new Rect(i % 4 * 768, (3 - i / 4) * 768, 768, 768),
                alignment = SpriteAlignment.Custom, pivot = new Vector2(.5f, .0625f),
                spriteID = existing.TryGetValue(name, out SpriteRect previous) ? previous.spriteID : GUID.Generate()
            };
        }
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().OrderBy(frame => frame.name, StringComparer.Ordinal).ToArray();
        if (frames.Length != 16) throw new InvalidOperationException("Soldier Fire needs sixteen sprites.");
        return Ensure<UISpriteAnimation>(Root + "/SoldierFireClip.asset", clip =>
        { clip.frames = frames; clip.frameDuration = .05f; clip.groundPivot = new Vector2(.5f, .0625f); });
    }
    private static T Ensure<T>(string path, Action<T> initialize) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>(); initialize(asset);
        AssetDatabase.CreateAsset(asset, path); return asset;
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

