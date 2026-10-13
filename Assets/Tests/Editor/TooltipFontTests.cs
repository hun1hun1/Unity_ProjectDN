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

    [Test]
    public void SharedTooltipFontIsDynamicAndAllowsAdditionalAtlases()
    {
        TMP_FontAsset font = SourceFont();
        Assert.That(font.sourceFontFile, Is.Not.Null);
        Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
        Assert.That(font.isMultiAtlasTexturesEnabled, Is.True);
        foreach (string path in new[] { "Assets/Prefabs/UI/Card.prefab", "Assets/Prefabs/UI/DeckCard.prefab",
            "Assets/Prefabs/UI/StatusTooltip.prefab" })
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            TMP_Text description = prefab.GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.name == "Description" || text.name == "Content");
            Assert.That(description.font, Is.SameAs(font), path);
        }
    }

    [Test]
    public void AllCardAndStatusTooltipTextRendersWithoutReplacementGlyphs()
    {
        TMP_FontAsset font = IsolatedFont();
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
        TMP_FontAsset font = IsolatedFont();
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
        TMP_FontAsset font = IsolatedFont(false);
        Assert.That(font.TryAddCharacters(HangulStressText(), out string missing), Is.False,
            "The control must reproduce the capacity problem without changing the saved font.");
        Assert.That(missing, Is.Not.Empty);
        Assert.That(font.atlasTextureCount, Is.EqualTo(1));
    }

    private static TMP_FontAsset SourceFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Assert.That(font, Is.Not.Null);
        return font;
    }

    private TMP_FontAsset IsolatedFont(bool? multiAtlas = null)
    {
        TMP_FontAsset source = SourceFont();
        // Generate independent textures/tables so verification cannot alter the user's saved font.
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source.sourceFontFile, (int)source.faceInfo.pointSize,
            source.atlasPadding, source.atlasRenderMode, source.atlasWidth, source.atlasHeight,
            source.atlasPopulationMode, multiAtlas ?? source.isMultiAtlasTexturesEnabled);
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
