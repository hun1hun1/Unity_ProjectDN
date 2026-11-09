using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class EnemyActionExpansionTests
{
    private readonly List<Object> created = new List<Object>();
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }
    [UnityTearDown] public IEnumerator ExitPlay()
    { if (Application.isPlaying) yield return new ExitPlayMode(); }

    [Test] public void StatusOnlyActionAffectsItsConfiguredTargetWithoutDamage()
    {
        BattleManager battle = Battle();
        EnemyAction action = Debuff(StatusEffectType.Rust);
        action.Execute(battle);
        Assert.That(battle.GetPlayer().StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(2));
        Assert.That(battle.GetTarget().StatusEffects.ActiveEffects, Is.Empty);
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(80));
        Set(action, "target", EnemyTargetType.Self);
        action.Execute(battle);
        Assert.That(battle.GetTarget().StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(2));
    }

    [Test] public void EnemyDebuffUsesExistingDurationAndReapplicationRules()
    {
        BattleManager battle = Battle();
        EnemyAction action = Debuff(StatusEffectType.Rust);
        action.Execute(battle);
        battle.GetPlayer().StatusEffects.ReduceRemainingTurns();
        Assert.That(battle.GetPlayer().StatusEffects.HasStatus(StatusEffectType.Rust), Is.True);
        action.Execute(battle);
        Assert.That(battle.GetPlayer().StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(3));
    }

    [Test] public void DebuffBeforeAttackPreviewsItsDamageWithoutChangingCombatOrRandomState()
    {
        BattleManager battle = Battle();
        EnemyAction action = Composite(Debuff(StatusEffectType.WeaknessExposure), Attack(10));
        int changes = 0;
        battle.GetPlayer().StatusEffects.Changed += () => changes++;
        var random = UnityEngine.Random.state;
        for (int i = 0; i < 3; i++)
        {
            Assert.That(action.GetActionDescription(battle), Does.Contain("공격 15"));
            Assert.That(action.GetActionDescription(battle), Does.Contain("약점 노출 2턴"));
        }
        Assert.That(changes, Is.Zero);
        Assert.That(battle.GetPlayer().StatusEffects.ActiveEffects, Is.Empty);
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(80));
        Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
        action.Execute(battle);
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(65));
    }

    [Test] public void IntentPanelSummarizesDebuffsAndKeepsCompositeDamagePrediction()
    {
        BattleManager battle = Battle();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterSizes/EnemyIntentUI.prefab");
        GameObject instance = Track(Object.Instantiate(prefab));
        EnemyActionUI ui = instance.GetComponent<EnemyActionUI>();
        TMP_Text text = (TMP_Text)Get(ui, "actionText");
        EnemyAction debuff = Debuff(StatusEffectType.WeaknessExposure);

        ui.SetAction(debuff, battle);
        Assert.That(text.text, Is.EqualTo("디버프"));
        Assert.That(text.font.HasCharacter('디', tryAddCharacter: true), Is.True);
        Assert.That(text.font.HasCharacter('프', tryAddCharacter: true), Is.True);

        EnemyAction composite = Composite(Block(8), Composite(debuff, Attack(10)));
        ui.SetAction(composite, battle);
        Assert.That(text.text, Is.EqualTo("방어 8\n디버프\n공격 15"));
        Assert.That(composite.GetActionDescription(battle), Does.Contain("약점 노출 2턴"));

        ui.SetAction(Composite(Attack(100), debuff), battle);
        Assert.That(text.text, Is.EqualTo("공격 100"));
        Assert.That(battle.GetPlayer().StatusEffects.ActiveEffects, Is.Empty);
    }

    [Test] public void AttackBeforeDebuffDoesNotApplyFutureModifiersToTheEarlierAttack()
    {
        BattleManager battle = Battle();
        EnemyAction action = Composite(Attack(10), Debuff(StatusEffectType.WeaknessExposure));
        Assert.That(action.GetActionDescription(battle), Does.Contain("공격 10"));
        action.Execute(battle);
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(70));
        Assert.That(battle.GetPlayer().StatusEffects.HasStatus(StatusEffectType.WeaknessExposure), Is.True);
    }

    [Test] public void NestedPreviewAppliesBothSidesStatusesAndPreservesRepeatedChildren()
    {
        BattleManager battle = Battle();
        EnemyAction rust = Debuff(StatusEffectType.Rust);
        Set(rust, "target", EnemyTargetType.Self);
        EnemyAction attack = Attack(20);
        EnemyAction action = Composite(rust, Composite(Debuff(StatusEffectType.WeaknessExposure), attack, attack));
        Assert.That(action.GetActionDescription(battle), Does.Contain("공격 22\n공격 22"));
        Assert.That(Kinds(action), Is.EqualTo(5));
        action.Execute(battle);
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(36));
    }

    [Test] public void BlockAndAttackUseTheExistingStatusCalculators()
    {
        BattleManager battle = Battle();
        battle.GetTarget().StatusEffects.Apply(Status(StatusEffectType.DefenseBreak), 2);
        EnemyAction action = Composite(Block(8), Attack(10));
        Assert.That(action.GetActionDescription(battle), Is.EqualTo("방어 4\n공격 10"));
        action.Execute(battle);
        Assert.That(battle.GetTarget().GetBlock(), Is.EqualTo(4));
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(70));
    }

    [Test] public void CompositeStopsAfterLethalDamageAndPreviewDoesNotAdvertiseLaterSteps()
    {
        BattleManager battle = Battle();
        typeof(BattleManager).GetMethod("BindBattleCharacters", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(battle, null);
        battle.GetPlayer().AddBlock(5);
        EnemyAction action = Composite(Attack(100), Block(8), Debuff(StatusEffectType.Rust));
        Assert.That(action.GetActionDescription(battle), Is.EqualTo("공격 100"));
        action.Execute(battle);
        Assert.That(battle.Result, Is.EqualTo(BattleResult.Defeat));
        Assert.That(battle.GetTarget().GetBlock(), Is.Zero);
        Assert.That(battle.GetPlayer().StatusEffects.ActiveEffects, Is.Empty);
    }

    [Test] public void NullChildrenAreSkippedAndSharedSubtreesAreAllowed()
    {
        BattleManager battle = Battle();
        EnemyAction shared = Composite(Attack(2));
        EnemyAction action = Composite(null, shared, shared);
        Assert.DoesNotThrow(() => Validate(action));
        action.Execute(battle);
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(76));
    }

    [Test] public void CyclesAreRejectedBeforeAnyPartialExecutionOrRecursivePresentation()
    {
        BattleManager battle = Battle();
        EnemyAction a = Composite(Attack(10)), b = Composite(a);
        Set(a, "actions", new[] { Attack(10), b });
        Assert.Throws<InvalidOperationException>(() => Validate(a));
        Assert.Throws<InvalidOperationException>(() => a.Execute(battle));
        Assert.Throws<InvalidOperationException>(() => a.GetActionDescription(battle));
        Assert.Throws<InvalidOperationException>(() => a.GetActionIcon());
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(80));
        EnemyBehavior behavior = Track(ScriptableObject.CreateInstance<EnemyBehavior>());
        Set(behavior, "actions", new[] { a });
        Assert.Throws<InvalidOperationException>(() => behavior.Validate());
    }

    [Test] public void InvalidStatusAndEmptyCompositeAreConfigurationErrors()
    {
        EnemyAction status = Debuff(StatusEffectType.Rust);
        Set(status, "turns", 0);
        Assert.Throws<InvalidOperationException>(() => Validate(status));
        Set(status, "turns", 2);
        Set(status, "status", null);
        Assert.Throws<InvalidOperationException>(() => Validate(status));
        Assert.Throws<InvalidOperationException>(() => Validate(Composite(null, null)));
    }

    [Test] public void EveryActionCombinationResolvesItsOwnIconIncludingNestedActions()
    {
        ScriptableObject icons = New("EnemyActionIconSet");
        string[] fields = { "attack", "block", "attackBlock", "debuff", "attackDebuff", "blockDebuff", "attackBlockDebuff" };
        EnemyAction attack = Attack(1), block = Block(1), debuff = Debuff(StatusEffectType.Rust);
        for (int mask = 1; mask <= 7; mask++)
        {
            Sprite sprite = Icon();
            Set(icons, fields[mask - 1], sprite);
            var children = new List<EnemyAction>();
            if ((mask & 1) != 0) children.Add(Composite(attack, attack));
            if ((mask & 2) != 0) children.Add(block);
            if ((mask & 4) != 0) children.Add(debuff);
            EnemyAction composite = Composite(children.ToArray());
            Set(composite, "iconSet", icons);
            Assert.That(Kinds(composite), Is.EqualTo(mask));
            Assert.That(composite.GetActionIcon(), Is.SameAs(sprite));
        }
    }

    [Test] public void MissingCombinationUsesTheConfiguredFallbackAndRecomputesAfterEditingChildren()
    {
        EnemyAction action = Composite(Attack(1), Block(1));
        Sprite fallback = Icon();
        Set(action, "actionIcon", fallback);
        Set(action, "iconSet", New("EnemyActionIconSet"));
        Assert.That(action.GetActionIcon(), Is.SameAs(fallback));
        Set(action, "actions", new[] { Debuff(StatusEffectType.Rust) });
        Assert.That(Kinds(action), Is.EqualTo(4));
        Assert.That(action.GetActionIcon(), Is.SameAs(fallback));
        EnemyAction legacy = Attack(1);
        Set(legacy, "actionIcon", fallback);
        Assert.That(legacy.GetActionIcon(), Is.SameAs(fallback));
    }

    [Test] public void AuthoredExamplesHaveDistinctIconsValidReferencesAndKeepTheDefaultPattern()
    {
        var seen = new HashSet<Sprite>();
        foreach (string name in new[] { "Debuff", "DebuffAttack", "BlockAttack", "AllActions" })
        {
            EnemyAction action = AssetDatabase.LoadAssetAtPath<EnemyAction>("Assets/Enemy/Examples/" + name + ".asset");
            Assert.That(action, Is.Not.Null, name);
            Assert.DoesNotThrow(() => Validate(action));
            Assert.That(action.GetActionIcon(), Is.Not.Null, name);
            Assert.That(seen.Add(action.GetActionIcon()), Is.True, name);
        }
        EnemyBehavior original = AssetDatabase.LoadAssetAtPath<EnemyBehavior>("Assets/Enemy/Test/Behavior_Test.asset");
        Assert.That(original.GetActionCount(), Is.EqualTo(2));
        Assert.That(original.GetAction(0), Is.TypeOf<EnemyAttackAction>());
        Assert.That(original.GetAction(1), Is.TypeOf<EnemyBlockAction>());
        Assert.That(original.SelectionMode, Is.EqualTo(EnemyActionSelectionMode.Sequential));
    }

    [UnityTest] public IEnumerator BattleSceneShowsTheCompositeIconAndExecutesItsCachedIntent()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return null;
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        EnemyAction action = AssetDatabase.LoadAssetAtPath<EnemyAction>("Assets/Enemy/Examples/DebuffAttack.asset");
        Assert.That(action, Is.Not.Null);
        EnemyBehavior behavior = Track(ScriptableObject.CreateInstance<EnemyBehavior>());
        Set(behavior, "actions", new[] { action });
        BattleEncounterDefinition encounter = Track(ScriptableObject.CreateInstance<BattleEncounterDefinition>());
        encounter.behavior = behavior;
        battle.GetTarget().ConfigureEncounter(encounter);
        EnemyActionUI ui = Object.FindFirstObjectByType<EnemyActionUI>();
        TMP_Text text = (TMP_Text)Get(ui, "actionText");
        Image image = (Image)Get(ui, "actionIcon");
        Assert.That(text.text, Is.EqualTo("디버프\n공격 15"));
        Assert.That(image.sprite, Is.SameAs(action.GetActionIcon()));
        EnemyAction next = battle.GetTarget().NextAction;
        battle.GetTarget().StatusEffects.Apply(Status(StatusEffectType.Rust), 2);
        Assert.That(battle.GetTarget().NextAction, Is.SameAs(next));
        Assert.That(text.text, Is.EqualTo("디버프\n공격 11"));
        // The example applies Weakness Exposure before attacking, so calculate using a snapshot result: 10*.75*1.5 = 11.
        Assert.That(text.text, Does.Contain("공격 11"));
        int hp = battle.GetPlayer().GetCurrentHP();
        battle.GetTarget().EnemyTurn(battle);
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(hp - 11));
        Assert.That((int)Get(battle.GetTarget(), "consecutiveCount"), Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator HealthAndBlockChangesRefreshTheCachedCompositeWithoutRerolling()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene"); yield return null;
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Player player = battle.GetPlayer();
        player.RestoreHealth(8, 80);
        EnemyBehavior behavior = Track(ScriptableObject.CreateInstance<EnemyBehavior>());
        Set(behavior, "actions", new[] { Composite(Attack(10), Block(8)), Composite(Attack(10), Block(8)) });
        Set(behavior, "selectionMode", EnemyActionSelectionMode.RandomNoThreeConsecutive);
        BattleEncounterDefinition encounter = Track(ScriptableObject.CreateInstance<BattleEncounterDefinition>());
        encounter.behavior = behavior;
        Enemy enemy = battle.GetTarget(); enemy.ConfigureEncounter(encounter);
        EnemyActionUI ui = Object.FindFirstObjectByType<EnemyActionUI>();
        TMP_Text text = (TMP_Text)Get(ui, "actionText");
        Assert.That(text.text, Is.EqualTo("공격 10"));
        EnemyAction cached = enemy.NextAction;
        var random = UnityEngine.Random.state;
        player.AddBlock(5);
        Assert.That(text.text, Does.Contain("방어 8"), "Gaining block can make the advertised attack nonlethal.");
        player.ResetBlock();
        Assert.That(text.text, Is.EqualTo("공격 10"));
        player.Heal(5);
        Assert.That(text.text, Does.Contain("방어 8"));
        player.TakeDamage(6);
        Assert.That(text.text, Is.EqualTo("공격 10"));
        player.RestoreHealth(20, 80);
        Assert.That(text.text, Does.Contain("방어 8"));
        Assert.That(enemy.NextAction, Is.SameAs(cached));
        Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
    }

    [UnityTest] public IEnumerator ExpandedIntentFitsItsTextAreaInTheRealBattleScene()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return new WaitForSeconds(.5f);
        EnemyActionUI ui = Object.FindFirstObjectByType<EnemyActionUI>();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        TMP_Text text = (TMP_Text)Get(ui, "actionText");
        foreach (string name in new[] { "Debuff", "DebuffAttack", "BlockAttack", "AllActions" })
        {
            EnemyAction action = AssetDatabase.LoadAssetAtPath<EnemyAction>("Assets/Enemy/Examples/" + name + ".asset");
            ui.SetAction(action, battle);
            Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate();
            Capture(battle, name);
            Assert.That(text.isTextOverflowing, Is.False, name + " intent must fit without clipping or overlapping other HUD elements.");
            Assert.That(text.textInfo.lineCount, Is.EqualTo(name == "Debuff" ? 1 : name == "AllActions" ? 3 : 2), name);
            Image background = (Image)Get(ui, "background");
            Assert.That(background, Is.Not.Null);
            Assert.That(background.enabled, Is.True);
            Assert.That(background.raycastTarget, Is.False);
            Assert.That(background.rectTransform.rect.height, Is.GreaterThanOrEqualTo(text.preferredHeight + 16));
            RectTransform root = (RectTransform)ui.transform;
            RectTransform iconRect = ((Image)Get(ui, "actionIcon")).rectTransform;
            Bounds backgroundBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, background.rectTransform);
            Bounds iconBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, iconRect);
            Bounds textBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, text.rectTransform);
            Assert.That(iconBounds.min.x, Is.GreaterThanOrEqualTo(backgroundBounds.min.x + 15));
            Assert.That(iconBounds.max.x, Is.LessThanOrEqualTo(textBounds.min.x));
            Assert.That(textBounds.max.x, Is.LessThanOrEqualTo(backgroundBounds.max.x - 15));
            float visibleTextWidth = Mathf.Min(text.preferredWidth, textBounds.size.x);
            float contentCenter = (iconBounds.min.x + textBounds.min.x + visibleTextWidth) / 2;
            Assert.That(contentCenter, Is.EqualTo(backgroundBounds.center.x).Within(1f), name);
            Assert.That(iconBounds.center.x, Is.LessThan(backgroundBounds.center.x), name);
            Assert.That(textBounds.center.x, Is.GreaterThan(backgroundBounds.center.x), name);
        }
        ui.ClearAction();
        Assert.That(((Image)Get(ui, "background")).enabled, Is.False);
    }

    private static void Capture(BattleManager battle, string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Canvas canvas = battle.GetTarget().GetComponentInParent<Canvas>().rootCanvas;
        RenderMode mode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        float distance = canvas.planeDistance;
        var cameraObject = new GameObject("Action capture", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
        var target = new RenderTexture(1280, 720, 24);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            System.IO.Directory.CreateDirectory(".utmp/enemy-actions");
            System.IO.File.WriteAllBytes(".utmp/enemy-actions/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
            camera.targetTexture = null;
            Object.Destroy(cameraObject); Object.Destroy(target); Object.Destroy(pixels);
        }
    }

    private BattleManager Battle()
    {
        Player player = Track(new GameObject("Action player")).AddComponent<Player>();
        Enemy enemy = Track(new GameObject("Action enemy")).AddComponent<Enemy>();
        Set(player, "currentHP", 80); Set(enemy, "currentHP", 80);
        BattleManager battle = Track(new GameObject("Action battle")).AddComponent<BattleManager>();
        Set(battle, "player", player); Set(battle, "target", enemy);
        return battle;
    }
    private EnemyAction Composite(params EnemyAction[] actions)
    { EnemyAction value = (EnemyAction)New("EnemyCompositeAction"); Set(value, "actions", actions); return value; }
    private EnemyAction Debuff(StatusEffectType type)
    { EnemyAction value = (EnemyAction)New("EnemyApplyStatusAction"); Set(value, "status", Status(type)); return value; }
    private EnemyAttackAction Attack(int amount)
    { var value = Track(ScriptableObject.CreateInstance<EnemyAttackAction>()); Set(value, "damage", amount); return value; }
    private EnemyBlockAction Block(int amount)
    { var value = Track(ScriptableObject.CreateInstance<EnemyBlockAction>()); Set(value, "block", amount); return value; }
    private StatusEffectData Status(StatusEffectType type)
    { var value = Track(ScriptableObject.CreateInstance<StatusEffectData>()); Set(value, "statusType", type); return value; }
    private ScriptableObject New(string name)
    {
        Type type = typeof(EnemyAction).Assembly.GetType(name);
        Assert.That(type, Is.Not.Null, name + " must be implemented.");
        return Track(ScriptableObject.CreateInstance(type));
    }
    private Sprite Icon()
    {
        Texture2D texture = Track(new Texture2D(4, 4));
        return Track(Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f));
    }
    private static int Kinds(EnemyAction action)
    {
        MethodInfo method = typeof(EnemyAction).GetMethod("GetActionKinds");
        Assert.That(method, Is.Not.Null);
        return Convert.ToInt32(method.Invoke(action, null));
    }
    private static void Validate(EnemyAction action)
    {
        MethodInfo method = typeof(EnemyAction).GetMethod("Validate", Type.EmptyTypes);
        Assert.That(method, Is.Not.Null);
        try { method.Invoke(action, null); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }
    private T Track<T>(T value) where T : Object { created.Add(value); return value; }
    private static object Get(object target, string field) => Field(target.GetType(), field).GetValue(target);
    private static void Set(object target, string field, object value) => Field(target.GetType(), field).SetValue(target, value);
    private static FieldInfo Field(Type type, string name)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field != null) return field;
            type = type.BaseType;
        }
        throw new MissingFieldException(name);
    }
}
