using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
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

    [Test] public void IntentShowsOnlyAttackDamageAndKeepsDetailedCompositeTooltip()
    {
        BattleManager battle = Battle();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterSizes/EnemyIntentUI.prefab");
        GameObject instance = Track(Object.Instantiate(prefab));
        EnemyActionUI ui = instance.GetComponent<EnemyActionUI>();
        TMP_Text text = (TMP_Text)Get(ui, "actionText");
        EnemyAction debuff = Debuff(StatusEffectType.WeaknessExposure);

        ui.SetAction(debuff, battle);
        Assert.That(text.text, Is.Empty);
        Assert.That(text.enabled, Is.False);
        Assert.That(Get(ui, "tooltipText"), Is.EqualTo("이번 턴에 적이 플레이어에게 약점 노출 효과를 2턴 동안 부여합니다."));

        ui.SetAction(Composite(Block(8), debuff), battle);
        Assert.That(text.enabled, Is.False);
        Assert.That(Get(ui, "tooltipText"), Does.Contain("방어도를 8 얻습니다."));

        EnemyAction composite = Composite(Block(8), Composite(debuff, Attack(10)));
        ui.SetAction(composite, battle);
        Assert.That(text.text, Is.EqualTo("15"));
        Assert.That(Get(ui, "tooltipText"), Is.EqualTo("이번 턴에 적이 방어도를 8 얻습니다.\n이어서 플레이어에게 약점 노출 효과를 2턴 동안 부여합니다.\n이어서 플레이어에게 15의 피해를 줍니다."));

        ui.SetAction(Composite(Attack(6), Attack(6)), battle);
        Assert.That(text.text, Is.EqualTo("12"));
        Assert.That(Get(ui, "tooltipText"), Is.EqualTo("이번 턴에 적이 플레이어에게 6의 피해를 줍니다.\n이어서 플레이어에게 6의 피해를 줍니다."));

        ui.SetAction(Composite(Attack(100), debuff), battle);
        Assert.That(text.text, Is.EqualTo("100"));
        Assert.That(Get(ui, "tooltipText"), Is.EqualTo("이번 턴에 적이 플레이어에게 100의 피해를 줍니다."));
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
        Image icon = (Image)Get(ui, "actionIcon");
        Assert.That(text.text, Is.EqualTo("15"));
        Assert.That(icon.sprite, Is.SameAs(action.GetActionIcon()));
        EnemyAction next = battle.GetTarget().NextAction;
        battle.GetTarget().StatusEffects.Apply(Status(StatusEffectType.Rust), 2);
        Assert.That(battle.GetTarget().NextAction, Is.SameAs(next));
        // The pending debuff and the current Rust status both affect this hit.
        Assert.That(text.text, Is.EqualTo("11"));
        Assert.That(Get(ui, "tooltipText"), Does.Contain("11의 피해"));
        int hp = battle.GetPlayer().GetCurrentHP();
        yield return battle.GetTarget().EnemyTurnRoutine(battle);
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
        Assert.That(text.text, Is.EqualTo("10"));
        Assert.That(Get(ui, "tooltipText"), Does.Not.Contain("방어도"));
        EnemyAction cached = enemy.NextAction;
        var random = UnityEngine.Random.state;
        player.AddBlock(5);
        Assert.That(Get(ui, "tooltipText"), Does.Contain("방어도를 8"), "Gaining block can make the advertised attack nonlethal.");
        player.ResetBlock();
        Assert.That(Get(ui, "tooltipText"), Does.Not.Contain("방어도"));
        player.Heal(5);
        Assert.That(Get(ui, "tooltipText"), Does.Contain("방어도를 8"));
        player.TakeDamage(6);
        Assert.That(Get(ui, "tooltipText"), Does.Not.Contain("방어도"));
        player.RestoreHealth(20, 80);
        Assert.That(Get(ui, "tooltipText"), Does.Contain("방어도를 8"));
        Assert.That(enemy.NextAction, Is.SameAs(cached));
        Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
    }

    [UnityTest] public IEnumerator CompactIntentIsReadableWithoutAFrameAndStillShowsTheTooltip()
    {
        yield return new EnterPlayMode();
        AsyncOperation loading = SceneManager.LoadSceneAsync("BattleScene");
        while (!loading.isDone) yield return null;
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle, Is.Not.Null);
        var presentation = battle.GetTarget().GetComponent<BattleCharacterPresentation>();
        EnemyActionUI ui = presentation.ActionUI;
        Assert.That(ui, Is.Not.Null);
        RectTransform root = (RectTransform)ui.transform;
        Assert.That(root.rect.height, Is.EqualTo(56f).Within(.01f));
        Image background = (Image)Get(ui, "background");
        Assert.That(background, Is.Not.Null);
        Assert.That(background.color.a, Is.Zero);
        foreach (string name in new[] { "Debuff", "DebuffAttack", "BlockAttack", "AllActions" })
        {
            EnemyAction action = AssetDatabase.LoadAssetAtPath<EnemyAction>("Assets/Enemy/Examples/" + name + ".asset");
            ui.SetAction(action, battle);
            Canvas.ForceUpdateCanvases();
            Capture(battle, name);
            Assert.That(background.enabled && background.raycastTarget, Is.True, name);
            Assert.That(background.rectTransform.rect.height, Is.EqualTo(56f).Within(.01f), name);
            Assert.That(background.rectTransform.rect.width, Is.GreaterThanOrEqualTo(80f), name);
            Assert.That(background.rectTransform.rect.width, Is.LessThanOrEqualTo(root.rect.width + .01f), name);
            var icon = (Image)Get(ui, "actionIcon");
            Assert.That(icon.enabled, Is.True, name);
            Assert.That(icon.sprite, Is.SameAs(action.GetActionIcon()), name);
            Assert.That(icon.rectTransform.rect.size, Is.EqualTo(new Vector2(48f, 48f)), name);
            Assert.That(icon.GetComponent<Outline>(), Is.Not.Null, name);
            var text = (TMP_Text)Get(ui, "actionText");
            Assert.That(text.enabled, Is.EqualTo(name != "Debuff"), name);
            if (text.enabled)
            {
                text.ForceMeshUpdate();
                Assert.That(text.fontSize, Is.EqualTo(24f), name);
                Assert.That(text.outlineWidth, Is.GreaterThan(0f), name);
                Assert.That(text.isTextOverflowing, Is.False, name);
                Assert.That(text.rectTransform.anchoredPosition.x, Is.GreaterThan(icon.rectTransform.anchoredPosition.x), name);
                Assert.That(text.rectTransform.anchoredPosition.y, Is.LessThan(icon.rectTransform.anchoredPosition.y), name);
            }
        }

        Assert.That(EventSystem.current, Is.Not.Null);
        Canvas canvas = ui.GetComponentInParent<Canvas>().rootCanvas;
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(eventCamera, background.rectTransform.position)
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        RaycastResult target = default;
        foreach (RaycastResult hit in hits)
        {
            if (hit.gameObject != background.gameObject) continue;
            target = hit;
            break;
        }
        Assert.That(target.isValid, Is.True, "The transparent area must still receive pointer events.");
        ExecuteEvents.ExecuteHierarchy(target.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        var tooltip = (StatusTooltipUI)Get(ui, "tooltip");
        Assert.That(tooltip, Is.Not.Null);
        Assert.That(tooltip.gameObject.activeSelf, Is.True);
        TMP_Text tooltipLabel = (TMP_Text)Get(tooltip, "contentText");
        Assert.That(tooltipLabel.text, Does.StartWith("이번 턴에 적이 ").And.Contain("약점 노출"));
        Capture(battle, "IntentTooltip", background.gameObject);

        ui.SetAction(Attack(7), battle);
        Assert.That(tooltipLabel.text, Is.EqualTo("이번 턴에 적이 플레이어에게 7의 피해를 줍니다."));
        ExecuteEvents.ExecuteHierarchy(background.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        Assert.That(tooltip.gameObject.activeSelf, Is.False);
        ExecuteEvents.ExecuteHierarchy(background.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        Assert.That(tooltip.gameObject.activeSelf, Is.True);
        ui.ClearAction();
        Assert.That(background.enabled, Is.False);
        Assert.That(tooltip.gameObject.activeSelf, Is.False);
    }

    private static void Capture(BattleManager battle, string name, GameObject hoverTarget = null)
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
            Canvas.ForceUpdateCanvases();
            if (hoverTarget != null)
                ExecuteEvents.ExecuteHierarchy(hoverTarget, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            camera.Render(); RenderTexture.active = target;
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
