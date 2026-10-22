using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class BlockVisibilityTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [TestCase("Small")]
    [TestCase("Normal")]
    [TestCase("Large")]
    public void SharedHudHidesZeroBlockAndCanShowItAgain(string size)
    {
        CharacterInfoUI hud = Hud(size);
        GameObject badge = hud.transform.Find("Block").gameObject;
        TMP_Text number = (TMP_Text)Field(hud, "blockText");
        Image hp = (Image)Field(hud, "hpFill");
        hud.SetBlock(0);
        Assert.That(badge.activeSelf, Is.False, "Zero block must hide the whole badge, including its image.");
        Assert.That(number.gameObject.activeInHierarchy, Is.False);
        Assert.That(hud.gameObject.activeSelf, Is.True);
        Assert.That(hp.gameObject.activeInHierarchy, Is.True);

        hud.SetBlock(12);
        Assert.That(badge.activeSelf, Is.True);
        Assert.That(number.gameObject.activeInHierarchy, Is.True);
        Assert.That(number.text, Is.EqualTo("12"));
        hud.SetBlock(0);
        Assert.That(badge.activeSelf, Is.False);
        hud.SetBlock(3);
        Assert.That(badge.activeSelf, Is.True);
        Assert.That(number.text, Is.EqualTo("3"));
    }

    [TestCase(typeof(Player))]
    [TestCase(typeof(Enemy))]
    public void CharacterBlockChangesDriveVisibilityAfterBindingDamageAndReset(Type characterType)
    {
        var root = new GameObject("Block visibility character"); created.Add(root);
        BattleCharacter actor = (BattleCharacter)root.AddComponent(characterType);
        actor.RestoreHealth(80, 80);
        CharacterInfoUI hud = Hud("Normal"); hud.transform.SetParent(root.transform, false);
        GameObject badge = hud.transform.Find("Block").gameObject;
        actor.BindCharacterInfoUI(hud);
        Assert.That(badge.activeSelf, Is.False, "Initial zero block must be hidden when the HUD binds.");
        actor.AddBlock(8);
        Assert.That(badge.activeSelf, Is.True);
        actor.TakeDamage(3);
        Assert.That(actor.GetBlock(), Is.EqualTo(5));
        Assert.That(badge.activeSelf, Is.True);
        actor.TakeDamage(5);
        Assert.That(actor.GetBlock(), Is.Zero);
        Assert.That(badge.activeSelf, Is.False);
        actor.AddBlock(2);
        Assert.That(badge.activeSelf, Is.True);
        actor.ResetBlock();
        Assert.That(badge.activeSelf, Is.False, "Turn-start block reset must hide the badge again.");
        actor.AddBlock(7);
        CharacterInfoUI replacement = Hud("Small"); replacement.transform.SetParent(root.transform, false);
        actor.BindCharacterInfoUI(replacement);
        Assert.That(replacement.transform.Find("Block").gameObject.activeSelf, Is.True);
        actor.RestoreHealth(80, 80);
        Assert.That(replacement.transform.Find("Block").gameObject.activeSelf, Is.False);
        Assert.That(replacement.gameObject.activeSelf, Is.True);
    }

    private CharacterInfoUI Hud(string size)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterSizes/" + size + "CharacterInfoUI.prefab");
        GameObject instance = Object.Instantiate(prefab); created.Add(instance); instance.SetActive(true);
        return instance.GetComponent<CharacterInfoUI>();
    }
    private static object Field(object owner, string name) =>
        typeof(CharacterInfoUI).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
}
