using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class DeckCardRarityTests
{
    [TestCase("Assets/Prefabs/UI/DeckCard.prefab")]
    [TestCase("Assets/Prefabs/UI/CardInspection.prefab")]
    public void DeckCardsUseHandCardRaritySpritesAndRefreshWhenRebound(string prefabPath)
    {
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        CardData data = ScriptableObject.CreateInstance<CardData>();
        try
        {
            DeckCardUI card = instance.GetComponentInChildren<DeckCardUI>(true);
            Assert.That(card, Is.Not.Null);
            SerializedObject view = new SerializedObject(card);
            SerializedProperty rarityProperty = view.FindProperty("rarityImage");
            Assert.That(rarityProperty, Is.Not.Null, "Deck cards need an Inspector binding for the rarity image.");
            UnityEngine.UI.Image image = rarityProperty.objectReferenceValue as UnityEngine.UI.Image;
            Assert.That(image, Is.Not.Null, "The DeckCard and nested inspection prefab must have the image wired.");
            SerializedObject hand = new SerializedObject(AssetDatabase.LoadAssetAtPath<CardUI>("Assets/Prefabs/UI/Card.prefab"));
            foreach (CardRarity rarity in new[] { CardRarity.Rare, CardRarity.Common, CardRarity.Uncommon, CardRarity.Common })
            {
                string field = rarity == CardRarity.Common ? "commonImage" : rarity == CardRarity.Uncommon ? "uncommonImage" : "rareImage";
                Sprite expected = hand.FindProperty(field).objectReferenceValue as Sprite;
                Assert.That(expected, Is.Not.Null);
                SerializedProperty spriteProperty = view.FindProperty(field);
                Assert.That(spriteProperty, Is.Not.Null);
                Assert.That(spriteProperty.objectReferenceValue, Is.EqualTo(expected), "Deck and hand cards must use the same sprite assets.");
                data.rarity = rarity;
                card.SetCard(data);
                Assert.That(image.sprite, Is.EqualTo(expected), "Rebinding must update the displayed rarity: " + rarity);
                Assert.That(image.raycastTarget, Is.False, "The rarity decoration must not intercept card input.");
                Assert.That(image.color.a, Is.GreaterThan(0f));
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(data);
        }
    }
}
