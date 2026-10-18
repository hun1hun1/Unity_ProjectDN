using System;
using NUnit.Framework;
using UnityEngine;

public class MapSystemTests
{
    [Test]
    public void RestoringRunHealthKeepsDamageAndClearsBattleBlock()
    {
        var root = new GameObject("Health restoration test");
        try
        {
            Player player = root.AddComponent<Player>(); player.AddBlock(12);
            player.RestoreHealth(31, 90);
            Assert.That(player.GetCurrentHP(), Is.EqualTo(31));
            Assert.That(player.GetMaxHP(), Is.EqualTo(90));
            Assert.That(player.GetBlock(), Is.Zero);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void InvalidHealthRestorationDoesNotChangeTheCharacter()
    {
        var root = new GameObject("Invalid health restoration test");
        try
        {
            Player player = root.AddComponent<Player>(); player.RestoreHealth(31, 90);
            Assert.Throws<ArgumentOutOfRangeException>(() => player.RestoreHealth(10, 0));
            Assert.That(player.GetCurrentHP(), Is.EqualTo(31)); Assert.That(player.GetMaxHP(), Is.EqualTo(90));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void RestoredHealthIsClampedToItsMaximum()
    {
        var root = new GameObject("Clamped health restoration test");
        try
        {
            Player player = root.AddComponent<Player>(); player.RestoreHealth(100, 90);
            Assert.That(player.GetCurrentHP(), Is.EqualTo(90));
            player.RestoreHealth(-1, 90); Assert.That(player.GetCurrentHP(), Is.Zero);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
