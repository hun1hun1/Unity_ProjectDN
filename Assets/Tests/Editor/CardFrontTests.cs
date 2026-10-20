using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class CardFrontTests
{
    private const string Dream = "Assets/Images/UI/SoldierCardFronts/dream-card-front-v1-200x260.png";
    private const string Nightmare = "Assets/Images/UI/SoldierCardFronts/nightmare-card-front-v1-200x260.png";

    [TestCase("Assets/Prefabs/UI/Card.prefab")]
    [TestCase("Assets/Prefabs/UI/DeckCard.prefab")]
    [TestCase("Assets/Prefabs/UI/CardInspection.prefab")]
    public void RebindingSelectsTheMatchingFrontWithoutReplacingArtworkOrBack(string path)
    {
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        CardData data = ScriptableObject.CreateInstance<CardData>();
        try { VerifyFront(FindView(instance), data); }
        finally { Object.DestroyImmediate(instance); Object.DestroyImmediate(data); }
    }

    [TestCase("Assets/Prefabs/UI/Card.prefab")]
    [TestCase("Assets/Prefabs/UI/DeckCard.prefab")]
    [TestCase("Assets/Prefabs/UI/CardInspection.prefab")]
    public void OptionalFrontBindingsPreserveLegacyViewsAndFallbackSprites(string path)
    {
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        CardData data = ScriptableObject.CreateInstance<CardData>();
        try
        {
            Component view = FindView(instance);
            SerializedObject fields = new SerializedObject(view);
            Image image = ImageField(fields, "frontImage");
            Sprite fallback = image.sprite;
            foreach (string field in new[] { "dreamFrontImage", "nightmareFrontImage" })
                fields.FindProperty(field).objectReferenceValue = null;
            fields.ApplyModifiedPropertiesWithoutUndo();
            foreach (CardDeckType deck in new[] { CardDeckType.TypeDream, CardDeckType.TypeNightmare })
            {
                data.deckType = deck;
                Assert.DoesNotThrow(() => Bind(view, data));
                Assert.That(image.sprite, Is.SameAs(fallback));
            }
            fields.Update(); fields.FindProperty("frontImage").objectReferenceValue = null;
            fields.ApplyModifiedPropertiesWithoutUndo();
            Assert.DoesNotThrow(() => Bind(view, data));
        }
        finally { Object.DestroyImmediate(instance); Object.DestroyImmediate(data); }
    }

    [Test] public void BattleSceneRewardAndInspectionCardsInheritFrontBindings()
    {
        const string path = "Assets/Scenes/BattleScene.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        CardData data = ScriptableObject.CreateInstance<CardData>();
        try
        {
            DeckCardUI[] cards = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DeckCardUI>(true)).ToArray();
            Assert.That(cards.Length, Is.GreaterThan(0));
            foreach (DeckCardUI card in cards) VerifyFront(card, data);
        }
        finally
        {
            Object.DestroyImmediate(data);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void VerifyFront(Component view, CardData data)
    {
        SerializedObject fields = new SerializedObject(view);
        Image front = ImageField(fields, "frontImage");
        Assert.That(front.name, Is.EqualTo("Background"));
        Assert.That(front.type, Is.EqualTo(Image.Type.Simple));
        Assert.That(front.raycastTarget, Is.False);
        Assert.That(front.color, Is.EqualTo(Color.white));
        data.artwork = AssetDatabase.LoadAllAssetsAtPath(Nightmare).OfType<Sprite>().Single();
        foreach (CardDeckType deck in new[] { CardDeckType.TypeDream, CardDeckType.TypeNightmare, CardDeckType.TypeDream })
        {
            Sprite expected = AssetDatabase.LoadAllAssetsAtPath(deck == CardDeckType.TypeDream ? Dream : Nightmare).OfType<Sprite>().Single();
            string field = deck == CardDeckType.TypeDream ? "dreamFrontImage" : "nightmareFrontImage";
            Assert.That(fields.FindProperty(field).objectReferenceValue, Is.EqualTo(expected));
            data.deckType = deck; Bind(view, data);
            Assert.That(front.sprite, Is.EqualTo(expected), view.name + ": " + deck);
            Assert.That(ImageField(fields, "artwork").sprite, Is.EqualTo(data.artwork));
            if (view is CardUI)
            {
                string back = deck == CardDeckType.TypeDream ? "dreamBackImage" : "nightmareBackImage";
                Assert.That(ImageField(fields, "backImage").sprite, Is.EqualTo(fields.FindProperty(back).objectReferenceValue));
            }
        }
    }

    private static Component FindView(GameObject root)
    {
        CardUI hand = root.GetComponentInChildren<CardUI>(true);
        return hand != null ? (Component)hand : root.GetComponentInChildren<DeckCardUI>(true);
    }
    private static Image ImageField(SerializedObject fields, string name)
    {
        SerializedProperty field = fields.FindProperty(name);
        Assert.That(field, Is.Not.Null, fields.targetObject.name + " needs " + name);
        Assert.That(field.objectReferenceValue, Is.Not.Null, name + " must be wired in the prefab.");
        return (Image)field.objectReferenceValue;
    }
    private static void Bind(Component view, CardData data)
    {
        if (view is CardUI hand) hand.SetCard(data, null);
        else ((DeckCardUI)view).SetCard(data);
    }
}
