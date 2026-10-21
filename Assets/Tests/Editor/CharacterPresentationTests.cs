using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class CharacterPresentationTests
{
    private readonly List<Object> created = new List<Object>();
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test] public void SpriteSequenceUsesItsOwnTimingAndWrapsAfterLongSteps()
    {
        GameObject root = Track(new GameObject("Animated image", typeof(RectTransform), typeof(Image)));
        Component player = root.AddComponent(Type("UISpriteAnimator"));
        ScriptableObject clip = Clip();
        Call(player, "SetAnimation", clip);
        Call(player, "Advance", 0.25f);
        Assert.That(Get<int>(player, "CurrentFrame"), Is.EqualTo(2));
        Call(player, "Advance", 0.20f);
        Assert.That(Get<int>(player, "CurrentFrame"), Is.EqualTo(1));
        Assert.That(root.GetComponent<Image>().sprite, Is.SameAs(((Sprite[])Field(clip, "frames"))[1]));
        Assert.That(Call(player, "Advance", 3f), Is.Null);
        Assert.That(Get<int>(player, "CurrentFrame"), Is.EqualTo(1));
    }

    [Test] public void InvalidSequenceDoesNotReplaceTheCurrentAnimation()
    {
        GameObject root = Track(new GameObject("Animated image", typeof(RectTransform), typeof(Image)));
        Component player = root.AddComponent(Type("UISpriteAnimator"));
        ScriptableObject valid = Clip(); Call(player, "SetAnimation", valid);
        Sprite original = root.GetComponent<Image>().sprite;
        ScriptableObject invalid = Clip(); Set(invalid, "frameDuration", 0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => Call(player, "SetAnimation", invalid));
        Assert.That(root.GetComponent<Image>().sprite, Is.SameAs(original));
    }

    [Test] public void ChangingSizeAndHudPrefabPreservesVitalsStatusesAndAnimationProgress()
    {
        GameObject root = Track(new GameObject("Enemy", typeof(RectTransform)));
        ((RectTransform)root.transform).sizeDelta = new Vector2(240, 800);
        Enemy enemy = root.AddComponent<Enemy>(); enemy.RestoreHealth(31, 73); enemy.AddBlock(8);
        StatusEffectData status = Track(ScriptableObject.CreateInstance<StatusEffectData>());
        SetSerialized(status, "statusType", (int)StatusEffectType.Rust);
        enemy.StatusEffects.Apply(status, 2);
        DelayedEffectSystem delayed = new DelayedEffectSystem(); enemy.BindDelayedEffects(delayed);
        RectTransform visual = Child(root.transform, "Visual"); Image image = visual.gameObject.AddComponent<Image>();
        Component animator = visual.gameObject.AddComponent(Type("UISpriteAnimator"));
        Component view = root.AddComponent(Type("BattleCharacterPresentation"));
        ScriptableObject small = Profile(HudTemplate("Small HUD"), 320);
        ScriptableObject large = Profile(HudTemplate("Large HUD"), 480);
        Call(view, "Configure", visual, animator, Clip(), small);
        Call(animator, "Advance", 0.25f);
        CharacterInfoUI previous = Get<CharacterInfoUI>(view, "InfoUI");
        Call(view, "ApplyProfile", large);
        CharacterInfoUI current = Get<CharacterInfoUI>(view, "InfoUI");
        Assert.That(current, Is.Not.SameAs(previous));
        Assert.That(visual.sizeDelta, Is.EqualTo(new Vector2(480, 480)));
        Assert.That(visual.anchorMin, Is.EqualTo(new Vector2(0.5f, 0)));
        Assert.That(visual.pivot, Is.EqualTo(new Vector2(0.5f, 0.0625f)));
        Assert.That(visual.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(31)); Assert.That(enemy.GetMaxHP(), Is.EqualTo(73));
        Assert.That(enemy.GetBlock(), Is.EqualTo(8));
        Assert.That(enemy.StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(2));
        Assert.That(((TMP_Text)Field(current, "hpText")).text, Is.EqualTo("31 / 73"));
        Assert.That(((TMP_Text)Field(current, "hpText")).fontSize, Is.EqualTo(18));
        Assert.That(Get<int>(animator, "CurrentFrame"), Is.EqualTo(2));
        Assert.That(Field((StatusEffectsUI)Field(current, "statusUI"), "delayed"), Is.SameAs(delayed));
        Assert.That(image.preserveAspect, Is.True); Assert.That(image.raycastTarget, Is.False);
    }

    [Test] public void InvalidProfileDoesNotPartiallyChangeTheCurrentPresentation()
    {
        ScriptableObject invalid = Profile(HudTemplate("HUD"), 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => Call(invalid, "Validate"));
    }

    [Test] public void HudProfileRejectsMissingVitalReferencesBeforeItCanBeApplied()
    {
        GameObject broken = Track(new GameObject("Incomplete HUD", typeof(RectTransform)));
        CharacterInfoUI hud = broken.AddComponent<CharacterInfoUI>();
        ScriptableObject profile = Profile(hud, 384);
        Assert.Throws<InvalidOperationException>(() => Call(profile, "Validate"));
    }

    [Test] public void IntentProfileRejectsUnboundImagesAndText()
    {
        ScriptableObject profile = Profile(HudTemplate("HUD"), 384);
        GameObject broken = Track(new GameObject("Incomplete intent", typeof(RectTransform)));
        Set(profile, "actionPrefab", broken.AddComponent<EnemyActionUI>());
        Assert.Throws<InvalidOperationException>(() => Call(profile, "Validate"));
    }

    [Test] public void ShortDisplayAreaShrinksOnlyArtworkAndKeepsIntentAboveIt()
    {
        GameObject root = Track(new GameObject("Short enemy slot", typeof(RectTransform)));
        ((RectTransform)root.transform).sizeDelta = new Vector2(240, 240);
        root.AddComponent<Enemy>();
        RectTransform visual = Child(root.transform, "Visual"); visual.gameObject.AddComponent<Image>();
        Component animator = visual.gameObject.AddComponent(Type("UISpriteAnimator"));
        Component view = root.AddComponent(Type("BattleCharacterPresentation"));
        ScriptableObject profile = Profile(HudTemplate("HUD"), 480);
        Set(profile, "actionPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterSizes/EnemyIntentUI.prefab").GetComponent<EnemyActionUI>());
        Call(view, "Configure", visual, animator, Clip(), profile);
        RectTransform intent = (RectTransform)Get<EnemyActionUI>(view, "ActionUI").transform;
        RectTransform hud = (RectTransform)Get<CharacterInfoUI>(view, "InfoUI").transform;
        Assert.That(visual.rect.height, Is.LessThan(480));
        Assert.That(intent.anchoredPosition.y + intent.rect.height, Is.LessThanOrEqualTo(240.01f));
        Assert.That(hud.sizeDelta, Is.EqualTo((Vector2)Field(profile, "infoSize")));
        Assert.That(((TMP_Text)Field(Get<CharacterInfoUI>(view, "InfoUI"), "hpText")).fontSize, Is.EqualTo(18));
    }

    [Test] public void ImportedAnimationsAndEnemyPresetsAreWiredWithoutChangingExistingCombatData()
    {
        foreach (string name in new[] { "Soldier", "Slime", "Scarecrow", "Lizard" })
        {
            Object clip = AssetDatabase.LoadAssetAtPath<Object>("Assets/CharacterPresentation/Animations/" + name + "Idle.asset");
            Assert.That(clip, Is.Not.Null, name + " animation is not imported.");
            float duration = name == "Soldier" ? .06f : name == "Slime" ? .08f : name == "Scarecrow" ? .10f : .12f;
            Assert.That((float)Field(clip, "frameDuration"), Is.EqualTo(duration).Within(.00001f));
            Sprite[] frames = (Sprite[])Field(clip, "frames"); Assert.That(frames.Length, Is.EqualTo(24));
            foreach (Sprite frame in frames) Assert.That(frame.rect.size, Is.EqualTo(new Vector2(768, 768)));
        }
        foreach (string name in new[] { "Small", "Normal", "Large" })
        {
            Object profile = AssetDatabase.LoadAssetAtPath<Object>("Assets/CharacterPresentation/Profiles/" + name + ".asset");
            Assert.That(profile, Is.Not.Null); Assert.That(Field(profile, "infoPrefab"), Is.Not.Null);
            Assert.That(Field(profile, "actionPrefab"), Is.Not.Null);
            float size = name == "Small" ? 352 : name == "Normal" ? 384 : 480;
            float width = name == "Small" ? 288 : name == "Normal" ? 320 : 360;
            Assert.That((Vector2)Field(profile, "visualSize"), Is.EqualTo(Vector2.one * size));
            Assert.That((Vector2)Field(profile, "infoSize"), Is.EqualTo(new Vector2(width, 84)));
        }
        foreach (string name in new[] { "Slime", "Scarecrow", "Lizard" })
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/" + name + ".prefab");
            Assert.That(prefab, Is.Not.Null); Assert.That(prefab.GetComponent(Type("BattleCharacterPresentation")), Is.Not.Null);
            Assert.That(new SerializedObject(prefab.GetComponent<Enemy>()).FindProperty("maxHP").intValue, Is.EqualTo(50));
            Assert.That(prefab.transform.Find("Visual").GetComponent<Image>().sprite, Is.Not.Null);
        }
        GameObject soldier = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Soldier.prefab");
        Assert.That(soldier.GetComponent(Type("BattleCharacterPresentation")), Is.Not.Null);
    }

    [Test] public void SavedHudAndIntentRemainLinkedToTheirEditablePrefabSources()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Slime.prefab");
        var view = prefab.GetComponent<BattleCharacterPresentation>();
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(view.InfoUI), Is.Not.Null);
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(view.ActionUI), Is.Not.Null);
    }

    [Test] public void ReconfiguringAnExistingPrefabKeepsItsSelectedSizeAndIdle()
    {
        const string temporary = "Assets/Tests/Editor/CharacterPresentationSetupTest.prefab";
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(temporary), Is.Null);
        try
        {
            Assert.That(AssetDatabase.CopyAsset("Assets/Prefabs/Enemy/Slime.prefab", temporary), Is.True);
            CharacterUIProfile large = AssetDatabase.LoadAssetAtPath<CharacterUIProfile>("Assets/CharacterPresentation/Profiles/Large.asset");
            UISpriteAnimation lizard = AssetDatabase.LoadAssetAtPath<UISpriteAnimation>("Assets/CharacterPresentation/Animations/LizardIdle.asset");
            GameObject root = PrefabUtility.LoadPrefabContents(temporary);
            try
            {
                var view = root.GetComponent<BattleCharacterPresentation>();
                view.Configure(view.Visual, view.Visual.GetComponent<UISpriteAnimator>(), lizard, large);
                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, temporary), Is.Not.Null);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            typeof(CharacterPresentationAssetSetup).GetMethod("ConfigurePrefab", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { temporary,
                    AssetDatabase.LoadAssetAtPath<CharacterUIProfile>("Assets/CharacterPresentation/Profiles/Small.asset"),
                    AssetDatabase.LoadAssetAtPath<UISpriteAnimation>("Assets/CharacterPresentation/Animations/SlimeIdle.asset"), false });
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(temporary).GetComponent<BattleCharacterPresentation>();
            Assert.That(saved.Profile, Is.SameAs(large));
            Assert.That(saved.IdleAnimation, Is.SameAs(lizard));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(saved.InfoUI), Is.SameAs(large.infoPrefab));
        }
        finally { AssetDatabase.DeleteAsset(temporary); }
    }

    private Type Type(string name)
    {
        Type type = typeof(Enemy).Assembly.GetType(name); Assert.That(type, Is.Not.Null, name + " must exist."); return type;
    }
    private ScriptableObject Clip()
    {
        ScriptableObject clip = Track(ScriptableObject.CreateInstance(Type("UISpriteAnimation")));
        Texture2D texture = Track(new Texture2D(8, 8)); Sprite[] sprites = new Sprite[3];
        for (int i = 0; i < sprites.Length; i++) sprites[i] = Track(Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f));
        Set(clip, "frames", sprites); Set(clip, "frameDuration", 0.1f); return clip;
    }
    private ScriptableObject Profile(CharacterInfoUI hud, float size)
    {
        ScriptableObject profile = Track(ScriptableObject.CreateInstance(Type("CharacterUIProfile")));
        Set(profile, "visualSize", new Vector2(size, size)); Set(profile, "infoPrefab", hud); return profile;
    }
    private CharacterInfoUI HudTemplate(string name)
    {
        GameObject root = Track(new GameObject(name, typeof(RectTransform))); root.SetActive(false);
        CharacterInfoUI hud = root.AddComponent<CharacterInfoUI>();
        Image fill = Child(root.transform, "Fill").gameObject.AddComponent<Image>();
        TMP_Text hp = Child(root.transform, "HP").gameObject.AddComponent<TextMeshProUGUI>(); hp.fontSize = 18;
        TMP_Text block = Child(root.transform, "Block").gameObject.AddComponent<TextMeshProUGUI>();
        StatusEffectsUI statuses = Child(root.transform, "Statuses").gameObject.AddComponent<StatusEffectsUI>();
        Set(hud, "statusUI", statuses);
        Set(hud, "hpFill", fill); Set(hud, "hpText", hp); Set(hud, "blockText", block); return hud;
    }
    private RectTransform Child(Transform parent, string name)
    { var child = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); child.SetParent(parent, false); return child; }
    private T Track<T>(T value) where T : Object { created.Add(value); return value; }
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    private static T Get<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
    private static object Call(object target, string name, params object[] args)
    {
        try { return target.GetType().GetMethod(name).Invoke(target, args); }
        catch (TargetInvocationException ex) { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
    private static void SetSerialized(Object target, string name, int value)
    { var state = new SerializedObject(target); state.FindProperty(name).intValue = value; state.ApplyModifiedPropertiesWithoutUndo(); }
}
