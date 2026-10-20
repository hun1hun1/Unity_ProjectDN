using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class GeneratedEnemyIconTests
{
    [TestCase("attack", 1, "8ea9ce74db1edc04796c404f24799207")]
    [TestCase("block", 2, "3d589e8b9355b734b8dcad657c569d87")]
    [TestCase("attackBlock", 3, "e2025647b4fc64e48b2b268705ba998d")]
    [TestCase("debuff", 4, "5693e4a60ffbd344da7992f29a3dbc55")]
    [TestCase("attackDebuff", 5, "ba6e8d5bffbda4540a5c39fe866bf709")]
    [TestCase("blockDebuff", 6, "b52de1f015a2f914da8f3531339fcc29")]
    [TestCase("attackBlockDebuff", 7, "55a7f52628f950343838b2ba08b5b52a")]
    public void GeneratedArtworkReplacesTheOldBadgeWithoutBreakingReferences(string name, int mask, string guid)
    {
        string target = "Assets/Images/UI/EnemyActions/" + name + ".png";
        bool single = mask == 1 || mask == 2 || mask == 4;
        string source = "GeneratedArt/EnemyActionIcons/" + (single ? name + "-v1-256x256.png" : "Composites/" + name + "-v1.png");
        Assert.That(File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(source)), Is.True,
            "The game icon must use the selected generated artwork: " + name);
        Assert.That(AssetDatabase.AssetPathToGUID(target), Is.EqualTo(guid), "Replacement must retain the original GUID.");
        var set = AssetDatabase.LoadAssetAtPath<EnemyActionIconSet>("Assets/Enemy/DefaultEnemyActionIcons.asset");
        Sprite sprite = set.GetIcon((EnemyActionKinds)mask);
        Assert.That(sprite, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(target));
        Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(256, 256)));
        var importer = (TextureImporter)AssetImporter.GetAtPath(target);
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.alphaIsTransparency, Is.True);
        Texture2D original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(original.LoadImage(File.ReadAllBytes(target)), Is.True);
            foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
                Assert.That(original.GetPixel((int)(corner.x * (original.width - 1)), (int)(corner.y * (original.height - 1))).a, Is.Zero);
        }
        finally { Object.DestroyImmediate(original); }
    }
}
