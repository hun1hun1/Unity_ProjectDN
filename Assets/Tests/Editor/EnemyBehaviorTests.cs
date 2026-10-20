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
using Object = UnityEngine.Object;

public class EnemyBehaviorTests
{
    private readonly List<Object> created = new List<Object>();
    private UnityEngine.Random.State randomState;

    [SetUp] public void Setup() { randomState = UnityEngine.Random.state; UnityEngine.Random.InitState(7021); }
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
        UnityEngine.Random.state = randomState;
    }
    [UnityTearDown] public IEnumerator ExitPlay()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [Test] public void ExistingSerializedBehaviorDefaultsToSequentialAndKeepsItsOrder()
    {
        EnemyBehavior behavior = AssetDatabase.LoadAssetAtPath<EnemyBehavior>("Assets/Enemy/Test/Behavior_Test.asset");
        var serialized = new SerializedObject(behavior);
        SerializedProperty mode = serialized.FindProperty("selectionMode");
        Assert.That(mode, Is.Not.Null, "Behavior needs a per-asset selection mode.");
        Assert.That(mode.enumValueIndex, Is.Zero, "Old assets must keep sequential behavior.");
        BattleManager battle = CreateBattle(behavior);
        Enemy enemy = battle.GetTarget();
        for (int i = 0; i < 6; i++)
        {
            Assert.That(Next(enemy), Is.SameAs(behavior.GetAction(i % 2)));
            enemy.EnemyTurn(battle);
        }
    }

    [Test] public void FixedModeAllowsThreeIdenticalEntriesAndSingleAction()
    {
        EnemyAction a = Attack(1);
        foreach (EnemyAction[] actions in new[] { new[] { a }, new[] { a, a, a } })
        {
            Enemy enemy = CreateBattle(Behavior(false, actions)).GetTarget();
            for (int i = 0; i < 5; i++)
            {
                Assert.That(Next(enemy), Is.SameAs(a));
                enemy.EnemyTurn((BattleManager)Get(enemy, "battleContext"));
            }
        }
    }

    [Test] public void RandomModeNeverExecutesThreeIdenticalActionsAndIsNotFixedAlternation()
    {
        EnemyAction a = Attack(1), b = Attack(2), c = Block(3);
        BattleManager battle = CreateBattle(Behavior(true, a, b, c));
        Enemy enemy = battle.GetTarget();
        EnemyAction previous = null;
        int streak = 0;
        bool sawDouble = false;
        var seen = new HashSet<EnemyAction>();
        for (int i = 0; i < 200; i++)
        {
            EnemyAction advertised = Next(enemy);
            Assert.That(new[] { a, b, c }, Does.Contain(advertised));
            streak = advertised == previous ? streak + 1 : 1;
            Assert.That(streak, Is.LessThan(3));
            sawDouble |= streak == 2;
            seen.Add(advertised);
            int hp = battle.GetPlayer().GetCurrentHP(), block = enemy.GetBlock();
            enemy.EnemyTurn(battle);
            if (advertised is EnemyAttackAction attack)
                Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(hp - attack.GetDamage()));
            else Assert.That(enemy.GetBlock(), Is.EqualTo(block + 3));
            previous = advertised;
        }
        Assert.That(seen.Count, Is.EqualTo(3));
        Assert.That(sawDouble, Is.True, "Random mode allows doubles; it must not silently alternate.");
    }

    [Test] public void DuplicateReferencesAndNullsCannotBypassTheRepeatLimit()
    {
        EnemyAction a = Attack(1), b = Block(1);
        BattleManager battle = CreateBattle(Behavior(true, a, a, null, b, b));
        EnemyAction previous = null;
        int streak = 0;
        for (int i = 0; i < 80; i++)
        {
            EnemyAction next = Next(battle.GetTarget());
            Assert.That(next, Is.Not.Null);
            streak = next == previous ? streak + 1 : 1;
            Assert.That(streak, Is.LessThan(3));
            previous = next;
            battle.GetTarget().EnemyTurn(battle);
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void RandomModeRejectsFewerThanTwoDistinctActions(int entries)
    {
        EnemyAction a = Attack(1);
        EnemyBehavior behavior = Behavior(true, entries == 0 ? Array.Empty<EnemyAction>() :
            entries == 1 ? new[] { a } : new[] { a, a });
        BattleEncounterDefinition encounter = Encounter(behavior);
        Assert.Throws<InvalidOperationException>(() => encounter.Validate());
    }

    [Test] public void SharedBehaviorDoesNotShareSequenceOrRandomHistory()
    {
        foreach (bool random in new[] { false, true })
        {
            EnemyBehavior behavior = Behavior(random, Attack(1), Block(1));
            BattleManager first = CreateBattle(behavior), second = CreateBattle(behavior);
            EnemyAction secondIntent = Next(second.GetTarget());
            EnemyAction previous = null;
            int streak = 0;
            for (int i = 0; i < 60; i++)
            {
                EnemyAction action = Next(first.GetTarget());
                streak = action == previous ? streak + 1 : 1;
                if (random) Assert.That(streak, Is.LessThan(3));
                previous = action;
                first.GetTarget().EnemyTurn(first);
                Assert.That(Next(second.GetTarget()), Is.SameAs(secondIntent));
            }
            Assert.That(new SerializedObject(behavior).FindProperty("currentActionIndex"), Is.Null,
                "Shared definitions must not retain per-enemy sequence state.");
        }
    }

    [Test] public void ReconfiguringEncounterResetsFixedSequenceAndRandomHistory()
    {
        EnemyAction a = Attack(1), b = Block(1);
        BattleManager battle = CreateBattle(Behavior(false, a, b));
        Enemy enemy = battle.GetTarget();
        enemy.EnemyTurn(battle);
        Assert.That(Next(enemy), Is.SameAs(b));
        enemy.ConfigureEncounter(Encounter(Behavior(false, a, b)));
        Assert.That(Next(enemy), Is.SameAs(a));
        EnemyBehavior random = Behavior(true, a, b);
        enemy.ConfigureEncounter(Encounter(random));
        for (int i = 0; i < 12; i++) enemy.EnemyTurn(battle);
        enemy.ConfigureEncounter(Encounter(random));
        Assert.That(Get(enemy, "consecutiveCount"), Is.EqualTo(0));
        Assert.That(Get(enemy, "lastAction"), Is.Null);
    }

    [Test] public void StatusChangesAndRepeatedBindingDoNotRerollTheAdvertisedAction()
    {
        BattleManager battle = CreateBattle(Behavior(true, Attack(2), Block(1)));
        Enemy enemy = battle.GetTarget();
        EnemyAction advertised = Next(enemy);
        UnityEngine.Random.State state = UnityEngine.Random.state;
        StatusEffectData rust = Track(ScriptableObject.CreateInstance<StatusEffectData>());
        Set(rust, "statusType", StatusEffectType.Rust);
        for (int i = 0; i < 8; i++)
        {
            enemy.BindBattle(battle);
            enemy.StatusEffects.Apply(rust, 1);
            battle.GetPlayer().StatusEffects.Apply(rust, 1);
            Assert.That(Next(enemy), Is.SameAs(advertised));
        }
        Assert.That(UnityEngine.Random.state, Is.EqualTo(state), "Refreshing intent must not consume random draws.");
    }

    [UnityTest] public IEnumerator BattleSceneDisplaysAndExecutesTheSameRandomIntentAfterStatusChanges()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return null;
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Enemy enemy = battle.GetTarget();
        EnemyBehavior behavior = Behavior(true, Attack(2), Attack(3));
        enemy.ConfigureEncounter(Encounter(behavior));
        EnemyActionUI ui = Object.FindFirstObjectByType<EnemyActionUI>();
        TMP_Text text = (TMP_Text)Get(ui, "actionText");
        StatusEffectData rust = Track(ScriptableObject.CreateInstance<StatusEffectData>());
        Set(rust, "statusType", StatusEffectType.Rust);
        for (int i = 0; i < 6; i++)
        {
            EnemyAction advertised = Next(enemy);
            enemy.StatusEffects.Apply(rust, 1);
            Assert.That(Next(enemy), Is.SameAs(advertised));
            Assert.That(text.text, Is.EqualTo(advertised.GetActionDescription(battle)));
            int hp = battle.GetPlayer().GetCurrentHP();
            int damage = DamageCalculator.Calculate(((EnemyAttackAction)advertised).GetDamage(), enemy, battle.GetPlayer());
            enemy.EnemyTurn(battle);
            Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(hp - damage));
            Assert.That(text.text, Is.EqualTo(Next(enemy).GetActionDescription(battle)));
        }
        enemy.enabled = false;
        EnemyAction cached = Next(enemy);
        enemy.enabled = true;
        Assert.That(Next(enemy), Is.SameAs(cached));
    }

    private EnemyBehavior Behavior(bool random, params EnemyAction[] actions)
    {
        EnemyBehavior behavior = Track(ScriptableObject.CreateInstance<EnemyBehavior>());
        Set(behavior, "actions", actions);
        var serialized = new SerializedObject(behavior);
        SerializedProperty mode = serialized.FindProperty("selectionMode");
        Assert.That(mode, Is.Not.Null, "Behavior needs a per-asset selection mode.");
        mode.enumValueIndex = random ? 1 : 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return behavior;
    }
    private BattleEncounterDefinition Encounter(EnemyBehavior behavior)
    {
        BattleEncounterDefinition encounter = Track(ScriptableObject.CreateInstance<BattleEncounterDefinition>());
        encounter.behavior = behavior;
        return encounter;
    }
    private BattleManager CreateBattle(EnemyBehavior behavior)
    {
        Player player = Track(new GameObject("Pattern player")).AddComponent<Player>();
        Set(player, "currentHP", 100000);
        Enemy enemy = Track(new GameObject("Pattern enemy")).AddComponent<Enemy>();
        BattleManager battle = Track(new GameObject("Pattern battle")).AddComponent<BattleManager>();
        Set(battle, "player", player); Set(battle, "target", enemy);
        enemy.ConfigureEncounter(Encounter(behavior));
        enemy.BindBattle(battle);
        return battle;
    }
    private EnemyAttackAction Attack(int damage)
    { var action = Track(ScriptableObject.CreateInstance<EnemyAttackAction>()); Set(action, "damage", damage); return action; }
    private EnemyBlockAction Block(int block)
    { var action = Track(ScriptableObject.CreateInstance<EnemyBlockAction>()); Set(action, "block", block); return action; }
    private T Track<T>(T value) where T : Object { created.Add(value); return value; }
    private static EnemyAction Next(Enemy enemy)
    {
        PropertyInfo property = typeof(Enemy).GetProperty("NextAction");
        Assert.That(property, Is.Not.Null, "Enemy must expose its cached intent for inspection.");
        return (EnemyAction)property.GetValue(enemy);
    }
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
