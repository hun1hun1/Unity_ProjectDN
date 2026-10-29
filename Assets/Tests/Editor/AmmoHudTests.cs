using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class AmmoHudTests
{
    [UnityTest]
    public IEnumerator BattleAreaHudUsesTwentyRoundFillScaleAndDisplaysActualCapacity()
    {
        yield return new EnterPlayMode();
        SceneManager.LoadScene("BattleScene");
        yield return null;
        yield return new WaitForSeconds(0.8f);
        Soldier soldier = Object.FindFirstObjectByType<BattleManager>().GetPlayer() as Soldier;
        Assert.That(soldier, Is.Not.Null);
        AmmoSystem ammo = soldier.GetAmmoSystem();
        AmmoUI hud = Object.FindFirstObjectByType<AmmoUI>();
        Assert.That(hud, Is.Not.Null);
        Assert.That(soldier.GetComponentInChildren<AmmoUI>(true), Is.Null);
        Assert.That(hud.name, Is.EqualTo("PlayerSpecialAbility"));
        Assert.That(hud.transform.parent.name, Is.EqualTo("PlayerArea"));
        Assert.That(hud.transform.parent.parent.name, Is.EqualTo("BattleArea"));
        TMP_Text text = (TMP_Text)GetField(hud, "ammoText");
        UnityEngine.UI.Image fill = (UnityEngine.UI.Image)GetField(hud, "ammoFill");
        Assert.That(fill, Is.Not.Null);
        Assert.That(fill.type, Is.EqualTo(UnityEngine.UI.Image.Type.Filled));
        // Preserve BattleScene's existing vertical ammo bar.
        Assert.That(fill.fillMethod, Is.EqualTo(UnityEngine.UI.Image.FillMethod.Vertical));
        Assert.That(fill.sprite, Is.Not.Null, "Default UI sprite is needed for Image fill geometry.");
        Assert.That(fill.raycastTarget, Is.False);
        Assert.That(text.raycastTarget, Is.False);
        Assert.That(fill.rectTransform.rect.width, Is.GreaterThan(0));
        Assert.That(fill.rectTransform.rect.height, Is.GreaterThan(0));
        Assert.That(text.text, Is.EqualTo("총알 0 / 10"));
        Assert.That(fill.fillAmount, Is.Zero);
        Assert.That(GetFillVertexCount(fill), Is.Zero);
        // Fill uses twenty artwork slots; the label still shows actual capacity.
        ammo.Load(5);
        Assert.That(text.text, Is.EqualTo("총알 5 / 10"));
        Assert.That(fill.fillAmount, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(GetFillVertexCount(fill), Is.GreaterThan(0));
        ammo.Load(5);
        Assert.That(text.text, Is.EqualTo("총알 10 / 10"));
        Assert.That(fill.fillAmount, Is.EqualTo(0.5f).Within(0.0001f), "A full ten-round magazine fills only half of the twenty-slot artwork.");
        Assert.That(ammo.TrySpend(5), Is.True);
        ammo.IncreaseMaxForBattle(10);
        Assert.That(text.text, Is.EqualTo("총알 5 / 20"));
        Assert.That(fill.fillAmount, Is.EqualTo(0.25f).Within(0.0001f));
        hud.gameObject.SetActive(false);
        ammo.Load(5);
        Assert.That(text.text, Is.EqualTo("총알 5 / 20"));
        hud.gameObject.SetActive(true);
        Assert.That(text.text, Is.EqualTo("총알 10 / 20"));
        Assert.That(fill.fillAmount, Is.EqualTo(0.5f).Within(0.0001f));
        GameObject other = new GameObject("Other Ammo");
        AmmoSystem otherAmmo = other.AddComponent<AmmoSystem>();
        otherAmmo.Load(1);
        hud.Bind(otherAmmo);
        ammo.Load(99);
        Assert.That(text.text, Is.EqualTo("총알 1 / 10"), "Rebinding must detach the previous source.");
        Assert.That(fill.fillAmount, Is.EqualTo(0.05f).Within(0.0001f));
        hud.Bind(null);
        Assert.That(text.text, Is.EqualTo("총알 0 / 0"));
        Assert.That(fill.fillAmount, Is.Zero);
        hud.Bind(ammo);
        Object.Destroy(other);
        ammo.Load(99);
        Assert.That(fill.fillAmount, Is.EqualTo(1));
        ammo.TrySpend(20);
        Assert.That(fill.fillAmount, Is.Zero);
        SerializedObjectSetCapacity(ammo, 0);
        yield return null;
        Assert.That(text.text, Is.EqualTo("총알 0 / 10"), "Battle bonus remains during Inspector editing.");
        ammo.ResetForBattle();
        Assert.That(text.text, Is.EqualTo("총알 0 / 0"));
        Assert.That(fill.fillAmount, Is.Zero);
        yield return new ExitPlayMode();
    }

    private static void SerializedObjectSetCapacity(AmmoSystem ammo, int maximum)
    {
        var state = new UnityEditor.SerializedObject(ammo);
        state.FindProperty("maxAmmo").intValue = maximum;
        state.ApplyModifiedPropertiesWithoutUndo();
    }

    private static int GetFillVertexCount(UnityEngine.UI.Image fill)
    {
        using (var vertices = new UnityEngine.UI.VertexHelper())
        {
            typeof(UnityEngine.UI.Image).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(UnityEngine.UI.VertexHelper) }, null)
                .Invoke(fill, new object[] { vertices });
            return vertices.currentVertCount;
        }
    }

    private static object GetField(object obj, string name)
    {
        FieldInfo field = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        return field.GetValue(obj);
    }
}
