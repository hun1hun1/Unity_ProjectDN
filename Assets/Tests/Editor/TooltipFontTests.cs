using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class TooltipFontTests
{
    private const string FontPath = "Assets/Fonts/NotoSansKR-VF SDF.asset";
    private const string CardFontPath = "Assets/Fonts/NotoSansKR-Bold SDF.asset";
    private const string PreviouslyMissing = "능증으며감남";
    private readonly List<TMP_FontAsset> fonts = new List<TMP_FontAsset>();
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject obj in objects) Object.DestroyImmediate(obj);
        objects.Clear();
        foreach (TMP_FontAsset font in fonts)
        {
            foreach (Texture2D texture in font.atlasTextures)
                if (texture != null) Object.DestroyImmediate(texture);
            if (font.material != null) Object.DestroyImmediate(font.material);
            Object.DestroyImmediate(font);
        }
        fonts.Clear();
    }

    [TestCase("Assets/Prefabs/UI/Card.prefab", CardFontPath)]
    [TestCase("Assets/Prefabs/UI/DeckCard.prefab", CardFontPath)]
    [TestCase("Assets/Prefabs/UI/StatusTooltip.prefab", CardFontPath)]
    [TestCase("Assets/Prefabs/UI/CardStatusTooltip.prefab", CardFontPath)]
    [TestCase("Assets/Prefabs/UI/DeckPileTooltip.prefab", CardFontPath)]
    public void DescriptionsUseTheirConfiguredBakedFontAndAtlas(string prefabPath, string fontPath)
    {
        TMP_FontAsset font = SourceFont(fontPath);
        Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
        Assert.That(font.atlasTextures, Is.Not.Empty);
        Assert.That(font.atlasTextureCount, Is.InRange(1, font.atlasTextures.Length));
        Assert.That(font.atlasTextures.Take(font.atlasTextureCount).All(texture => texture != null), Is.True);
        Assert.That(font.characterTable, Is.Not.Empty);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, prefabPath);
        TMP_Text description = prefab.GetComponentsInChildren<TMP_Text>(true)
            .Single(text => text.name == "Description" || text.name == "Content");
        Assert.That(description.font, Is.SameAs(font), prefabPath);
    }

    [TestCase(FontPath)]
    [TestCase(CardFontPath)]
    public void AllCardAndStatusTooltipTextRendersWithoutReplacementGlyphs(string fontPath)
    {
        // Static fonts render their baked glyphs without changing the saved atlas.
        TMP_FontAsset font = SourceFont(fontPath);
        TMP_Text label = Label(font);
        var strings = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:CardData", new[] { "Assets/CardData" }))
        {
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
            strings.Add(card.cardName);
            strings.Add(CardDescriptionBuilder.Build(card));
        }
        foreach (string guid in AssetDatabase.FindAssets("t:StatusEffectData", new[] { "Assets/StatusData" }))
        {
            StatusEffectData status = AssetDatabase.LoadAssetAtPath<StatusEffectData>(AssetDatabase.GUIDToAssetPath(guid));
            strings.Add($"<b>{status.DisplayName}</b>\n{status.Description}\n남은 턴: 2");
        }
        strings.Add("<color=#81C784>현재 발동 가능</color>\n<color=#808080>현재 총알 부족</color>");
        Assert.That(strings.Count, Is.GreaterThan(10));
        foreach (string text in strings) AssertRendered(label, text ?? "");
        foreach (char character in PreviouslyMissing)
            Assert.That(font.characterLookupTable.ContainsKey(character), Is.True, $"U+{(int)character:X4}");
    }

    [Test]
    public void MissingCharactersStillRenderAfterTheFirstAtlasFills()
    {
        TMP_FontAsset font = IsolatedDynamicFont();
        string stress = HangulStressText();
        Assert.That(font.TryAddCharacters(stress, out string missing), Is.True, missing);
        Assert.That(font.atlasTextureCount, Is.GreaterThan(1), "The test must actually exceed one atlas.");
        TMP_Text label = Label(font);
        AssertRendered(label, PreviouslyMissing + "\n현재 발동 가능\n받는 피해가 50% 증가합니다.\n남은 턴: 2");
        for (int i = 0; i < 20; i++)
        {
            label.GetPreferredValues(label.text, 320, Mathf.Infinity);
            label.ForceMeshUpdate();
        }
        AssertRendered(label, label.text);
    }

    [Test]
    public void SingleAtlasControlCannotFitTheSameStressText()
    {
        TMP_FontAsset font = IsolatedDynamicFont(false);
        Assert.That(font.TryAddCharacters(HangulStressText(), out string missing), Is.False,
            "The control must reproduce the capacity problem without changing the saved font.");
        Assert.That(missing, Is.Not.Empty);
        Assert.That(font.atlasTextureCount, Is.EqualTo(1));
    }

    private static TMP_FontAsset SourceFont(string fontPath = FontPath)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        Assert.That(font, Is.Not.Null);
        return font;
    }

    private TMP_FontAsset IsolatedDynamicFont(bool multiAtlas = true)
    {
        TMP_FontAsset source = SourceFont();
        // Baked font assets omit the runtime source reference; resolve the original Font in the Editor.
        string sourceGuid = new SerializedObject(source).FindProperty("m_SourceFontFileGUID").stringValue;
        Font sourceFile = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(sourceGuid));
        Assert.That(sourceFile, Is.Not.Null, "Dynamic stress tests need the original Font asset.");
        // Keep this capacity test independent of the saved static atlas and its dimensions.
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(sourceFile, (int)source.faceInfo.pointSize,
            source.atlasPadding, source.atlasRenderMode, 1024, 1024,
            AtlasPopulationMode.Dynamic, multiAtlas);
        Assert.That(font, Is.Not.Null);
        fonts.Add(font);
        return font;
    }

    private TMP_Text Label(TMP_FontAsset font)
    {
        GameObject canvas = new GameObject("FontVerificationCanvas", typeof(Canvas));
        objects.Add(canvas);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        GameObject root = new GameObject("FontVerificationText", typeof(RectTransform), typeof(TextMeshProUGUI));
        root.transform.SetParent(canvas.transform, false);
        TMP_Text label = root.GetComponent<TMP_Text>();
        label.font = font;
        label.rectTransform.sizeDelta = new Vector2(2000, 2000);
        label.fontSize = 20;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    private static void AssertRendered(TMP_Text label, string text)
    {
        label.text = text;
        label.ForceMeshUpdate(true);
        for (int i = 0; i < label.textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = label.textInfo.characterInfo[i];
            if (char.IsWhiteSpace(character.character)) continue;
            Assert.That(character.textElement, Is.Not.Null, text);
            Assert.That(character.textElement.unicode, Is.EqualTo((uint)character.character),
                $"Replacement glyph for U+{(int)character.character:X4}: {text}");
            Assert.That(character.textElement.glyph, Is.Not.Null, text);
        }
    }

    private static string HangulStressText()
    {
        var text = new StringBuilder();
        for (int i = 0; i < 320; i++) text.Append((char)(0xAC00 + i));
        return text.ToString();
    }
}
