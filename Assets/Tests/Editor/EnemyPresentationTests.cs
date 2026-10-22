using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class EnemyPresentationTests
{
    private readonly List<Object> created = new List<Object>();
    [UnityTearDown] public IEnumerator ExitPlay()
    { if (Application.isPlaying) yield return new ExitPlayMode(); }
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }
    [Test] public void NoPresetKeepsImmediateDamageAndAdvancesIntent()
    {
        BattleManager battle = Battle(out BattleCharacterPresentation view);
        EnemyAttackAction attack = Attack();
        EnemyBlockAction block = Asset<EnemyBlockAction>();
        Configure(battle, attack, block);
        Drain(Turn(battle));
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(70));
        Assert.That(battle.GetTarget().NextAction, Is.SameAs(block));
    }
    [Test] public void DamageOccursAtLeftLungeAndNextIntentWaitsForRecovery()
    {
        BattleManager battle = Battle(out BattleCharacterPresentation view);
        EnemyAttackAction attack = Attack();
        EnemyPresentationGate before = Asset<EnemyPresentationGate>(), after = Asset<EnemyPresentationGate>();
        Present(attack, new CardPresentationStep[] { Move(-.08f), before }, new CardPresentationStep[] { after, Move(0) });
        EnemyBlockAction block = Asset<EnemyBlockAction>(); Configure(battle, attack, block);
        int hits = 0;
        battle.GetPlayer().VitalsChanged += () =>
        {
            hits++;
            Assert.That(view.PresentationOffset.x, Is.EqualTo(-view.Visual.rect.width * .08f).Within(.01f));
        };
        IEnumerator routine = Flat(Turn(battle));
        try
        {
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(80));
            before.open = true;
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(70));
            Assert.That(battle.GetTarget().NextAction, Is.SameAs(attack));
            after.open = true; Drain(routine);
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(view.PresentationOffset, Is.EqualTo(Vector2.zero));
            Assert.That(battle.GetTarget().NextAction, Is.SameAs(block));
        }
        finally { (routine as IDisposable)?.Dispose(); }
    }
    [TestCase(false)] [TestCase(true)]
    public void CancellationRestoresEnemyAndDoesNotApplyPendingDamage(bool disableEnemy)
    {
        BattleManager battle = Battle(out BattleCharacterPresentation view);
        EnemyAttackAction attack = Attack();
        Present(attack, new CardPresentationStep[] { Move(-.08f), Asset<EnemyPresentationGate>() }, Array.Empty<CardPresentationStep>());
        Configure(battle, attack);
        IEnumerator routine = Flat(Turn(battle));
        try
        {
            Assert.That(routine.MoveNext(), Is.True);
            if (disableEnemy) battle.GetTarget().enabled = false;
            else battle.EndBattle();
            Drain(routine);
            Assert.That(view.PresentationOffset, Is.EqualTo(Vector2.zero));
            Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(80));
        }
        finally { (routine as IDisposable)?.Dispose(); }
    }
    [Test] public void DisposingTurnRestoresPoseAndAllowsRetryWithoutDuplicateHit()
    {
        BattleManager battle = Battle(out BattleCharacterPresentation view);
        EnemyAttackAction attack = Attack();
        EnemyPresentationGate gate = Asset<EnemyPresentationGate>();
        Present(attack, new CardPresentationStep[] { Move(-.08f), gate }, Array.Empty<CardPresentationStep>());
        Configure(battle, attack);
        IEnumerator first = Flat(Turn(battle)); Assert.That(first.MoveNext(), Is.True);
        Drain(Turn(battle)); // Concurrent calls must not start a second action.
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(80));
        (first as IDisposable)?.Dispose();
        Assert.That(view.PresentationOffset, Is.EqualTo(Vector2.zero));
        gate.open = true; Drain(Turn(battle));
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(70));
    }
    [Test] public void CompositeWaitsForChildPresentationBeforeNextEffect()
    {
        BattleManager battle = Battle(out BattleCharacterPresentation view);
        EnemyAttackAction attack = Attack(); EnemyPresentationGate gate = Asset<EnemyPresentationGate>();
        Present(attack, new CardPresentationStep[] { gate }, Array.Empty<CardPresentationStep>());
        EnemyBlockAction block = Asset<EnemyBlockAction>(); Set(block, "block", 5);
        EnemyCompositeAction composite = Asset<EnemyCompositeAction>(); Set(composite, "actions", new EnemyAction[] { attack, block });
        Configure(battle, composite);
        IEnumerator routine = Flat(Turn(battle));
        try
        {
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(80));
            Assert.That(battle.GetTarget().GetBlock(), Is.Zero);
            gate.open = true; Drain(routine);
            Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(70));
            Assert.That(battle.GetTarget().GetBlock(), Is.EqualTo(5));
        }
        finally { (routine as IDisposable)?.Dispose(); }
    }
    [Test] public void ManagerDoesNotEndEnemyTurnUntilRecoveryCompletes()
    {
        BattleManager battle = Battle(out BattleCharacterPresentation view);
        EnemyAttackAction attack = Attack(); EnemyPresentationGate gate = Asset<EnemyPresentationGate>();
        Present(attack, new CardPresentationStep[] { Move(-.08f) }, new CardPresentationStep[] { gate });
        Configure(battle, attack);
        object result = typeof(BattleManager).GetMethod("StartEnemyTurn", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(battle, null);
        Assert.That(result, Is.InstanceOf<IEnumerator>(), "Enemy turn needs to await its presentation.");
        IEnumerator routine = Flat((IEnumerator)result);
        try
        {
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That((BattleState)typeof(BattleManager).GetField("currentState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(battle), Is.EqualTo(BattleState.EnemyTurn));
            gate.open = true; Drain(routine);
            Assert.That((BattleState)typeof(BattleManager).GetField("currentState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(battle), Is.EqualTo(BattleState.EnemyTurnEnd));
            Assert.That(view.PresentationOffset, Is.EqualTo(Vector2.zero));
        }
        finally { (routine as IDisposable)?.Dispose(); }
    }
    [TestCase(false)] [TestCase(true)]
    public void CompositeCancellationSkipsRemainingActionsWhenEnemyIsDisabledOrDestroyed(bool destroyEnemy)
    {
        BattleManager battle = Battle(out BattleCharacterPresentation view);
        EnemyAttackAction first = Attack(), second = Attack();
        Present(first, new CardPresentationStep[] { Move(-.08f), Asset<EnemyPresentationGate>() }, Array.Empty<CardPresentationStep>());
        EnemyCompositeAction composite = Asset<EnemyCompositeAction>();
        Set(composite, "actions", new EnemyAction[] { first, second }); Configure(battle, composite);
        IEnumerator routine = Flat(Turn(battle));
        try
        {
            Assert.That(routine.MoveNext(), Is.True);
            if (destroyEnemy) Object.DestroyImmediate(battle.GetTarget().gameObject);
            else battle.GetTarget().enabled = false;
            Assert.DoesNotThrow(() => Drain(routine));
            Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(80));
            if (!destroyEnemy) Assert.That(view.PresentationOffset, Is.EqualTo(Vector2.zero));
        }
        finally { (routine as IDisposable)?.Dispose(); }
    }
    [Test] public void EnemyMeleeExampleResolvesAllStepsAndTargetsEnemy()
    {
        CardPresentation preset = AssetDatabase.LoadAssetAtPath<CardPresentation>("Assets/CardPresentations/EnemyMelee.asset");
        Assert.That(preset, Is.Not.Null); preset.Validate();
        foreach (CardPresentationStep step in preset.beforeImpact)
        { Assert.That(step, Is.Not.Null); Assert.That(step.target, Is.EqualTo(PresentationTarget.Enemy)); }
        foreach (CardPresentationStep step in preset.afterImpact)
        { Assert.That(step, Is.Not.Null); Assert.That(step.target, Is.EqualTo(PresentationTarget.Enemy)); }
        Assert.That(((MoveVisualPresentationStep)preset.beforeImpact[1]).offset.x, Is.LessThan(0));
        EnemyAttackAction example = AssetDatabase.LoadAssetAtPath<EnemyAttackAction>("Assets/Enemy/Examples/MeleeAttack.asset");
        Assert.That(example, Is.Not.Null);
        Assert.That(typeof(EnemyAction).GetField("presentation").GetValue(example), Is.SameAs(preset));
    }
    [UnityTest] public IEnumerator SceneTurnWaitsForEnemyRecoveryBeforePlayerTurn()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        float deadline = Time.realtimeSinceStartup + 10;
        while ((State(battle) != BattleState.PlayerTurn || battle.BlocksCardInteraction || battle.HandsAreMoving) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(State(battle), Is.EqualTo(BattleState.PlayerTurn));
        Assert.That(battle.BlocksCardInteraction || battle.HandsAreMoving, Is.False);
        EnemyAttackAction attack = Attack();
        EnemyPresentationGate before = Asset<EnemyPresentationGate>(), after = Asset<EnemyPresentationGate>();
        Present(attack, new CardPresentationStep[] { Move(-.08f), before }, new CardPresentationStep[] { after, Move(0) });
        Configure(battle, attack);
        int hp = battle.GetPlayer().GetCurrentHP();
        battle.EndPlayerTurn();
        deadline = Time.realtimeSinceStartup + 10;
        while (State(battle) != BattleState.EnemyTurn && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(State(battle), Is.EqualTo(BattleState.EnemyTurn));
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(hp));
        before.open = true;
        yield return null; yield return null;
        Assert.That(State(battle), Is.EqualTo(BattleState.EnemyTurn));
        Assert.That(battle.BlocksCardInteraction, Is.True);
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(hp - 10));
        after.open = true;
        deadline = Time.realtimeSinceStartup + 10;
        while (State(battle) != BattleState.PlayerTurn && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(State(battle), Is.EqualTo(BattleState.PlayerTurn));
        Assert.That(battle.GetTarget().GetComponent<BattleCharacterPresentation>().PresentationOffset, Is.EqualTo(Vector2.zero));
    }
    private static BattleState State(BattleManager battle) =>
        (BattleState)typeof(BattleManager).GetField("currentState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(battle);
    private BattleManager Battle(out BattleCharacterPresentation view)
    {
        Player player = Track(new GameObject("Player")).AddComponent<Player>(); Set(player, "currentHP", 80);
        GameObject root = Track(new GameObject("Enemy", typeof(RectTransform)));
        Enemy enemy = root.AddComponent<Enemy>();
        RectTransform visual = (RectTransform)Track(new GameObject("Visual", typeof(RectTransform), typeof(Image))).transform;
        visual.SetParent(root.transform, false);
        view = root.AddComponent<BattleCharacterPresentation>();
        view.Configure(visual, visual.gameObject.AddComponent<UISpriteAnimator>(), null, AssetDatabase.LoadAssetAtPath<CharacterUIProfile>("Assets/CharacterPresentation/Profiles/Small.asset"));
        BattleManager battle = Track(new GameObject("Battle")).AddComponent<BattleManager>();
        Set(battle, "player", player); Set(battle, "target", enemy); enemy.BindBattle(battle);
        return battle;
    }
    private void Configure(BattleManager battle, params EnemyAction[] actions)
    {
        EnemyBehavior behavior = Asset<EnemyBehavior>(); Set(behavior, "actions", actions);
        BattleEncounterDefinition encounter = Asset<BattleEncounterDefinition>(); encounter.behavior = behavior;
        battle.GetTarget().ConfigureEncounter(encounter);
    }
    private EnemyAttackAction Attack() { var action = Asset<EnemyAttackAction>(); Set(action, "damage", 10); return action; }
    private MoveVisualPresentationStep Move(float x)
    { var step = Asset<MoveVisualPresentationStep>(); step.target = PresentationTarget.Enemy; step.offset = new Vector2(x, 0); step.duration = 0; return step; }
    private void Present(EnemyAction action, CardPresentationStep[] before, CardPresentationStep[] after)
    {
        FieldInfo field = typeof(EnemyAction).GetField("presentation"); Assert.That(field, Is.Not.Null, "Enemy actions need an optional presentation.");
        var preset = Asset<CardPresentation>(); preset.beforeImpact = before; preset.afterImpact = after; field.SetValue(action, preset);
    }
    private static IEnumerator Turn(BattleManager battle)
    {
        MethodInfo method = typeof(Enemy).GetMethod("EnemyTurnRoutine"); Assert.That(method, Is.Not.Null, "Enemy actions need an awaited turn routine.");
        return (IEnumerator)method.Invoke(battle.GetTarget(), new object[] { battle });
    }
    private T Asset<T>() where T : ScriptableObject => Track(ScriptableObject.CreateInstance<T>());
    private T Track<T>(T value) where T : Object { created.Add(value); return value; }
    private static void Set(object obj, string name, object value)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        { FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); if (field != null) { field.SetValue(obj, value); return; } }
        throw new MissingFieldException(name);
    }
    private static void Drain(IEnumerator routine)
    {
        IEnumerator flat = Flat(routine);
        try { int guard = 1000; while (flat.MoveNext()) if (--guard == 0) Assert.Fail("Unexpected wait."); }
        finally { (flat as IDisposable)?.Dispose(); }
    }
    private static IEnumerator Flat(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine);
        try
        {
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); (current as IDisposable)?.Dispose(); continue; }
                if (current.Current is IEnumerator nested) stack.Push(nested); else yield return current.Current;
            }
        }
        finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
    }
}
public class EnemyPresentationGate : CardPresentationStep
{
    public bool open;
    public override IEnumerator Play(CardPresentationContext context)
    { while (!open && context.CanContinue) yield return null; }
}
