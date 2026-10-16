using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class DelayedEffectTests
{
    private readonly List<Object> created = new List<Object>();
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void OneTurnDelayFiresAtNextStartOnlyOnce()
    {
        BattleManager battle = Battle(out Player player);
        BlockEffect block = Asset<BlockEffect>(); block.block = 7;
        CardEffect delay = Delay(1, block);
        delay.Execute(battle);
        Assert.That(player.GetBlock(), Is.Zero, "Scheduling must not execute the child effect.");
        object system = Property(battle, "DelayedEffects");
        Assert.That(PendingCount(system), Is.EqualTo(1));
        Advance(system, battle);
        Assert.That(player.GetBlock(), Is.EqualTo(7));
        Assert.That(PendingCount(system), Is.Zero);
        Advance(system, battle);
        Assert.That(player.GetBlock(), Is.EqualTo(7));
    }

    [Test]
    public void ThreeTurnDelayAndDuplicateUsesRemainIndependent()
    {
        BattleManager battle = Battle(out Player player);
        BlockEffect block = Asset<BlockEffect>(); block.block = 3;
        CardEffect delay = Delay(3, block);
        delay.Execute(battle);
        object system = Property(battle, "DelayedEffects");
        Advance(system, battle);
        delay.Execute(battle);
        Advance(system, battle);
        Assert.That(player.GetBlock(), Is.Zero);
        Advance(system, battle);
        Assert.That(player.GetBlock(), Is.EqualTo(3));
        Assert.That(PendingCount(system), Is.EqualTo(1));
        Advance(system, battle);
        Assert.That(player.GetBlock(), Is.EqualTo(6));
        Assert.That(PendingCount(system), Is.Zero);
    }

    [Test]
    public void NestedDelayWaitsForItsOwnFollowingTurn()
    {
        BattleManager battle = Battle(out Player player);
        BlockEffect block = Asset<BlockEffect>(); block.block = 5;
        CardEffect outer = Delay(1, Delay(1, block));
        outer.Execute(battle);
        object system = Property(battle, "DelayedEffects");
        Advance(system, battle);
        Assert.That(player.GetBlock(), Is.Zero);
        Assert.That(PendingCount(system), Is.EqualTo(1));
        Advance(system, battle);
        Assert.That(player.GetBlock(), Is.EqualTo(5));
        Assert.That(PendingCount(system), Is.Zero);
    }

    [TestCase(0)] [TestCase(-1)]
    public void InvalidDelayDoesNotSchedule(int turns)
    {
        BattleManager battle = Battle(out Player player);
        Delay(turns, Asset<BlockEffect>()).Execute(battle);
        Assert.That(PendingCount(Property(battle, "DelayedEffects")), Is.Zero);
    }

    private CardEffect Delay(int turns, params CardEffect[] effects)
    {
        Type type = typeof(CardEffect).Assembly.GetType("DelayEffect");
        Assert.That(type, Is.Not.Null, "DelayEffect must expose turn-based scheduling.");
        CardEffect effect = (CardEffect)ScriptableObject.CreateInstance(type); created.Add(effect);
        Field(effect, "delayTurns").SetValue(effect, turns);
        Field(effect, "effects").SetValue(effect, effects);
        return effect;
    }
    private BattleManager Battle(out Player player)
    {
        GameObject root = new GameObject("Delay player"); created.Add(root);
        player = root.AddComponent<Player>();
        root.AddComponent<CostSystem>(); root.AddComponent<AmmoSystem>();
        DeckController deck = root.AddComponent<DeckController>();
        GameObject owner = new GameObject("Delay battle"); created.Add(owner);
        BattleManager battle = owner.AddComponent<BattleManager>();
        Field(battle, "player").SetValue(battle, player);
        Field(battle, "deckController").SetValue(battle, deck);
        return battle;
    }
    private T Asset<T>() where T : ScriptableObject { T value = ScriptableObject.CreateInstance<T>(); created.Add(value); return value; }
    private static FieldInfo Field(object obj, string name)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        { FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f; }
        throw new ArgumentException(name);
    }
    private static object Property(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
    private static int PendingCount(object system) => (int)Property(Property(system, "PendingEffects"), "Count");
    private static void Advance(object system, BattleManager battle)
    {
        system.GetType().GetMethod("AdvanceTurn").Invoke(system, null);
        Drain((IEnumerator)system.GetType().GetMethod("ProcessDueEffectsRoutine").Invoke(system, new object[] { battle }));
    }
    private static void Drain(IEnumerator routine)
    {
        while (routine.MoveNext()) if (routine.Current is IEnumerator nested) Drain(nested);
    }
}
