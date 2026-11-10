using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class CardPresentationTests
{
    private readonly List<Object> created = new List<Object>();
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
        PresentationProbeEffect.OnExecute = null;
    }
    [Test] public void CardDataHasOptionalPresentation()
    {
        FieldInfo field = typeof(CardData).GetField("presentation");
        Assert.That(field, Is.Not.Null, "CardData needs an optional presentation asset.");
        Assert.That(field.GetValue(Asset<CardData>()), Is.Null);
    }
    [Test] public void MissingPresentationPreservesEffectRepeatCount()
    {
        BattleManager battle = Battle(out Player actor, out BattleCharacterPresentation view);
        BlockEffect block = Asset<BlockEffect>(); block.block = 3;
        CardData card = Asset<CardData>(); card.effects = new CardEffect[] { block };
        Drain(Present(battle, card, 3));
        Assert.That(actor.GetBlock(), Is.EqualTo(9));
    }
    [Test] public void ImpactRunsBetweenMotionAndRecoveryExactlyOnce()
    {
        BattleManager battle = Battle(out Player actor, out BattleCharacterPresentation view);
        CardData card = Card(new[] { Move(.08f) }, new[] { Move(0) });
        card.effects = new CardEffect[] { Asset<PresentationProbeEffect>() };
        int hits = 0;
        PresentationProbeEffect.OnExecute = _ =>
        {
            hits++;
            Assert.That(view.Visual.anchoredPosition.x, Is.EqualTo(view.Visual.rect.width * .08f).Within(.01));
        };
        Drain(Present(battle, card));
        Assert.That(hits, Is.EqualTo(1));
        Assert.That(view.Visual.anchoredPosition, Is.EqualTo(Vector2.zero));
    }
    [TestCase(false)] [TestCase(true)]
    public void DisposingDuringEitherPhaseRestoresMotionAndDoesNotRepeatDamage(bool afterImpact)
    {
        BattleManager battle = Battle(out Player actor, out BattleCharacterPresentation view);
        ScriptableObject wait = Step("WaitPresentationStep"); Set(wait, "duration", 10f);
        CardData card = Card(afterImpact ? new[] { Move(.08f) } : new[] { Move(.08f), wait },
            afterImpact ? new[] { wait } : Array.Empty<ScriptableObject>());
        BlockEffect block = Asset<BlockEffect>(); block.block = 4; card.effects = new CardEffect[] { block };
        IEnumerator flat = Flat(Present(battle, card));
        Assert.That(flat.MoveNext(), Is.True);
        Assert.That(actor.GetBlock(), Is.EqualTo(afterImpact ? 4 : 0));
        Assert.That(view.Visual.anchoredPosition.x, Is.GreaterThan(0));
        ((IDisposable)flat).Dispose();
        Assert.That(view.Visual.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(actor.GetBlock(), Is.EqualTo(afterImpact ? 4 : 0));
    }
    [Test] public void LayoutChangesPreserveActiveOffsetAndCleanupUsesNewLayout()
    {
        BattleManager battle = Battle(out Player actor, out BattleCharacterPresentation view);
        ScriptableObject wait = Step("WaitPresentationStep"); Set(wait, "duration", 10f);
        CardData card = Card(new[] { Move(.08f), wait }, Array.Empty<ScriptableObject>());
        IEnumerator flat = Flat(Present(battle, card)); Assert.That(flat.MoveNext(), Is.True);
        CharacterUIProfile profile = Object.Instantiate(view.Profile); created.Add(profile);
        profile.visualOffset = new Vector2(5, 7);
        view.ApplyProfile(profile);
        Assert.That(view.Visual.anchoredPosition.x, Is.GreaterThan(5));
        ((IDisposable)flat).Dispose();
        Assert.That(view.Visual.anchoredPosition, Is.EqualTo(profile.visualOffset));
    }
    [Test] public void OneShotAnimationStopsAndDefaultPlaybackStillLoops()
    {
        Battle(out Player actor, out BattleCharacterPresentation view);
        UISpriteAnimator animator = view.Visual.GetComponent<UISpriteAnimator>();
        UISpriteAnimation clip = Clip();
        MethodInfo play = typeof(UISpriteAnimator).GetMethod("Play");
        Assert.That(play, Is.Not.Null, "Sprite player needs explicit one-shot playback.");
        play.Invoke(animator, new object[] { clip, false });
        animator.Advance(1f);
        Assert.That(animator.CurrentFrame, Is.EqualTo(1));
        Assert.That((bool)typeof(UISpriteAnimator).GetProperty("IsComplete").GetValue(animator), Is.True);
        animator.SetAnimation(clip); animator.Advance(.25f);
        Assert.That(animator.CurrentFrame, Is.EqualTo(0));
    }
    [Test] public void SpriteStepWaitsForImpactAndRestoresIdleOnCompletion()
    {
        BattleManager battle = Battle(out Player actor, out BattleCharacterPresentation view);
        UISpriteAnimator animator = view.Visual.GetComponent<UISpriteAnimator>();
        UISpriteAnimation idle = animator.Animation;
        animator.Advance(.15f);
        int idleFrame = animator.CurrentFrame;
        ScriptableObject step = Step("PlaySpritePresentationStep");
        Set(step, "animation", Clip()); Set(step, "waitForCompletion", true);
        CardData card = Card(new[] { step }, Array.Empty<ScriptableObject>());
        BlockEffect block = Asset<BlockEffect>(); block.block = 6; card.effects = new CardEffect[] { block };
        IEnumerator flat = Flat(Present(battle, card)); Assert.That(flat.MoveNext(), Is.True);
        Assert.That(actor.GetBlock(), Is.Zero);
        animator.Advance(1f); while (flat.MoveNext()) { }
        ((IDisposable)flat).Dispose();
        Assert.That(actor.GetBlock(), Is.EqualTo(6));
        Assert.That(animator.Animation, Is.SameAs(idle));
        Assert.That(animator.CurrentFrame, Is.EqualTo(idleFrame));
    }
    [Test] public void InvalidStepFailsBeforeExecutingEffects()
    {
        BattleManager battle = Battle(out Player actor, out BattleCharacterPresentation view);
        ScriptableObject step = Step("WaitPresentationStep"); Set(step, "duration", -1f);
        CardData card = Card(new[] { step }, Array.Empty<ScriptableObject>());
        BlockEffect block = Asset<BlockEffect>(); block.block = 6; card.effects = new CardEffect[] { block };
        Assert.Throws<ArgumentOutOfRangeException>(() => Drain(Present(battle, card)));
        Assert.That(actor.GetBlock(), Is.Zero);
    }
    [Test] public void DisablingSpriteDuringWaitDoesNotHang()
    {
        BattleManager battle = Battle(out Player actor, out BattleCharacterPresentation view);
        ScriptableObject step = Step("PlaySpritePresentationStep");
        Set(step, "animation", Clip()); Set(step, "waitForCompletion", true);
        CardData card = Card(new[] { step }, Array.Empty<ScriptableObject>());
        IEnumerator flat = Flat(Present(battle, card)); Assert.That(flat.MoveNext(), Is.True);
        view.Visual.GetComponent<UISpriteAnimator>().enabled = false;
        Drain(flat);
    }
    [Test] public void TemporaryEffectsAreRemovedWhenCancelledBeforeImpact()
    {
        BattleManager battle = Battle(out Player actor, out BattleCharacterPresentation view);
        ScriptableObject spawn = Step("SpawnVfxPresentationStep");
        GameObject template = Track(new GameObject("Effect template", typeof(RectTransform), typeof(Image)));
        Set(spawn, "prefab", template); Set(spawn, "duration", 0f);
        ScriptableObject wait = Step("WaitPresentationStep"); Set(wait, "duration", 10f);
        CardData card = Card(new[] { spawn, wait }, Array.Empty<ScriptableObject>());
        int count = view.Visual.childCount;
        IEnumerator flat = Flat(Present(battle, card)); Assert.That(flat.MoveNext(), Is.True);
        Assert.That(view.Visual.childCount, Is.EqualTo(count + 1));
        Assert.That(view.Visual.GetChild(count).GetComponent<Image>().raycastTarget, Is.False);
        ((IDisposable)flat).Dispose();
        Assert.That(view.Visual.childCount, Is.EqualTo(count));
    }
    [Test] public void SharedDefinitionKeepsTwoPlaybackContextsIndependent()
    {
        BattleManager a = Battle(out Player actorA, out BattleCharacterPresentation viewA);
        BattleManager b = Battle(out Player actorB, out BattleCharacterPresentation viewB);
        ScriptableObject wait = Step("WaitPresentationStep"); Set(wait, "duration", 10f);
        ScriptableObject move = Move(.08f);
        CardData card = Card(new[] { move, wait }, Array.Empty<ScriptableObject>());
        IEnumerator first = Flat(Present(a, card)), second = Flat(Present(b, card));
        Assert.That(first.MoveNext(), Is.True); Assert.That(second.MoveNext(), Is.True);
        ((IDisposable)first).Dispose();
        Assert.That(viewA.Visual.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(viewB.Visual.anchoredPosition.x, Is.GreaterThan(0));
        ((IDisposable)second).Dispose();
        Assert.That(viewB.Visual.anchoredPosition, Is.EqualTo(Vector2.zero));
    }
    [Test] public void EndingBattleBeforeImpactSkipsEffectsAndRestoresPose()
    {
        BattleManager battle = Battle(out Player actor, out BattleCharacterPresentation view);
        ScriptableObject wait = Step("WaitPresentationStep"); Set(wait, "duration", 10f);
        CardData card = Card(new[] { Move(.08f), wait }, Array.Empty<ScriptableObject>());
        BlockEffect block = Asset<BlockEffect>(); block.block = 8; card.effects = new CardEffect[] { block };
        IEnumerator flat = Flat(Present(battle, card)); Assert.That(flat.MoveNext(), Is.True);
        battle.enabled = false;
        Drain(flat);
        Assert.That(actor.GetBlock(), Is.Zero);
        Assert.That(view.Visual.anchoredPosition, Is.EqualTo(Vector2.zero));
    }
    [Test] public void MissingVisualStillExecutesGameEffects()
    {
        BattleManager battle = Battle(out Player actor, out BattleCharacterPresentation view);
        Object.DestroyImmediate(view);
        ScriptableObject wait = Step("WaitPresentationStep"); Set(wait, "duration", 10f);
        CardData card = Card(new[] { Move(.08f), wait }, Array.Empty<ScriptableObject>());
        BlockEffect block = Asset<BlockEffect>(); block.block = 8; card.effects = new CardEffect[] { block };
        Drain(Present(battle, card));
        Assert.That(actor.GetBlock(), Is.EqualTo(8));
    }
    [Test] public void SoldierAttackHasValidReusableFireAndMeleeExamples()
    {
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>("Assets/CardData/Soldier/Basic/Starting/Dream/Attack/01.Card_Attack/Attack.asset");
        FieldInfo field = typeof(CardData).GetField("presentation"); Assert.That(field, Is.Not.Null);
        Object fire = (Object)field.GetValue(card);
        Assert.That(fire, Is.SameAs(AssetDatabase.LoadAssetAtPath<Object>("Assets/CardPresentations/SoldierFire.asset")));
        fire.GetType().GetMethod("Validate").Invoke(fire, null);
        Object melee = AssetDatabase.LoadAssetAtPath<Object>("Assets/CardPresentations/Melee.asset");
        Assert.That(melee, Is.Not.Null); melee.GetType().GetMethod("Validate").Invoke(melee, null);
        UISpriteAnimation clip = AssetDatabase.LoadAssetAtPath<UISpriteAnimation>("Assets/CardPresentations/SoldierFireClip.asset");
        Assert.That(clip.frames.Length, Is.EqualTo(16));
        Assert.That(clip.frames[0].rect.size, Is.EqualTo(new Vector2(768, 768)));
        Assert.That(clip.frameDuration, Is.EqualTo(.05f));
    }
    private CardData Card(ScriptableObject[] before, ScriptableObject[] after)
    {
        CardData card = Asset<CardData>(); card.effects = Array.Empty<CardEffect>();
        ScriptableObject presentation = Step("CardPresentation");
        SetArray(presentation, "beforeImpact", before); SetArray(presentation, "afterImpact", after);
        FieldInfo field = typeof(CardData).GetField("presentation"); Assert.That(field, Is.Not.Null);
        field.SetValue(card, presentation); return card;
    }
    private ScriptableObject Move(float x)
    {
        ScriptableObject step = Step("MoveVisualPresentationStep");
        Set(step, "offset", new Vector2(x, 0)); Set(step, "duration", 0f);
        return step;
    }
    private BattleManager Battle(out Player actor, out BattleCharacterPresentation view)
    {
        GameObject root = Track(new GameObject("Presentation player", typeof(RectTransform)));
        actor = root.AddComponent<Player>();
        RectTransform visual = (RectTransform)Track(new GameObject("Visual", typeof(RectTransform), typeof(Image))).transform;
        visual.SetParent(root.transform, false);
        UISpriteAnimator animator = visual.gameObject.AddComponent<UISpriteAnimator>();
        view = root.AddComponent<BattleCharacterPresentation>();
        view.Configure(visual, animator, Clip(), AssetDatabase.LoadAssetAtPath<CharacterUIProfile>("Assets/CharacterPresentation/Profiles/Small.asset"));
        GameObject owner = Track(new GameObject("Presentation battle"));
        BattleManager battle = owner.AddComponent<BattleManager>();
        Set(battle, "player", actor);
        return battle;
    }
    private UISpriteAnimation Clip()
    {
        UISpriteAnimation clip = Asset<UISpriteAnimation>();
        Texture2D texture = Track(new Texture2D(2, 2));
        clip.frames = new[] { Track(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f)),
            Track(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f)) };
        clip.frameDuration = .1f; return clip;
    }
    private IEnumerator Present(BattleManager battle, CardData card, int count = 1)
    {
        MethodInfo method = typeof(BattleManager).GetMethod("ExecutePresentedCardRoutine");
        Assert.That(method, Is.Not.Null, "Card use must offer an effect/presentation bridge.");
        return (IEnumerator)method.Invoke(battle, new object[] { card, battle.ExecuteCardEffectsRoutine(card, count) });
    }
    private ScriptableObject Step(string name)
    {
        Type type = typeof(CardData).Assembly.GetType(name); Assert.That(type, Is.Not.Null, name + " is missing.");
        return Track(ScriptableObject.CreateInstance(type));
    }
    private T Asset<T>() where T : ScriptableObject => Track(ScriptableObject.CreateInstance<T>());
    private T Track<T>(T value) where T : Object { created.Add(value); return value; }
    private static void Set(object obj, string name, object value)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) { field.SetValue(obj, value); return; }
        }
        throw new MissingFieldException(name);
    }
    private static void SetArray(object obj, string name, ScriptableObject[] items)
    {
        FieldInfo field = obj.GetType().GetField(name);
        Assert.That(field, Is.Not.Null);
        Array array = Array.CreateInstance(field.FieldType.GetElementType(), items.Length);
        for (int i = 0; i < items.Length; i++) array.SetValue(items[i], i);
        field.SetValue(obj, array);
    }
    private static void Drain(IEnumerator routine)
    {
        using (IEnumeratorDisposable flat = new IEnumeratorDisposable(Flat(routine)))
        { int guard = 1000; while (flat.Value.MoveNext()) if (--guard == 0) Assert.Fail("Unexpected timed wait."); }
    }
    private sealed class IEnumeratorDisposable : IDisposable
    {
        public readonly IEnumerator Value;
        public IEnumeratorDisposable(IEnumerator value) { Value = value; }
        public void Dispose() => (Value as IDisposable)?.Dispose();
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
public class PresentationProbeEffect : CardEffect
{
    public static Action<BattleManager> OnExecute;
    public override void Execute(BattleManager battleManager) => OnExecute?.Invoke(battleManager);
}

