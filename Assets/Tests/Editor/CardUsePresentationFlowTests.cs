using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class CardUsePresentationFlowTests
{
    private static List<Object> created = new List<Object>();

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        if (battle != null && Application.isPlaying) battle.EndBattle();
        foreach (Object value in created ?? new List<Object>())
            if (value != null) Object.DestroyImmediate(value);
        created?.Clear();
        RunDeckInventory.ResetForNewGame();
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [TestCase(0f, 10f)]
    [TestCase(10f, 0f)]
    public void UsedCardAndHandDiscardsUseIndependentMoveDurations(float usedDuration, float handDuration)
    {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Card.prefab");
        CardUI card = Track(Object.Instantiate(prefab)).GetComponent<CardUI>();
        var discardPoint = (RectTransform)Track(new GameObject("Discard point", typeof(RectTransform))).transform;
        var settings = new UnityEditor.SerializedObject(card);
        var usedMove = settings.FindProperty("usedCardDiscardMoveDuration");
        var handMove = settings.FindProperty("handDiscardMoveDuration");
        Assert.That(usedMove, Is.Not.Null, "A used card needs its own discard move duration.");
        Assert.That(handMove, Is.Not.Null, "A card discarded directly from the hand needs its own move duration.");
        usedMove.floatValue = usedDuration;
        handMove.floatValue = handDuration;
        settings.FindProperty("discardShrinkDuration").floatValue = 0f;
        settings.ApplyModifiedPropertiesWithoutUndo();

        IEnumerator afterUse = card.CompleteUsedCardAnimation(discardPoint);
        Assert.That(afterUse.MoveNext(), Is.True, "Used cards should start the discard animation.");
        var usedDiscard = (IEnumerator)afterUse.Current;
        Assert.That(usedDiscard.MoveNext(), Is.EqualTo(usedDuration > 0f));
        (afterUse as System.IDisposable)?.Dispose();

        IEnumerator fromHand = card.DiscardAnimation(discardPoint);
        Assert.That(fromHand.MoveNext(), Is.EqualTo(handDuration > 0f));
    }

    [UnityTest]
    public IEnumerator CardArrivesBeforeFireAndStaysThroughRecoveryBeforeDiscard()
    {
        yield return new EnterPlayMode();
        yield return Load();
        yield return VerifyUse(false);
    }

    [UnityTest]
    public IEnumerator XCostCardWaitsInUsingAreaAndSettlesOnceAfterAllEffects()
    {
        yield return new EnterPlayMode();
        yield return Load();
        yield return VerifyUse(true);
    }

    [UnityTest]
    public IEnumerator EndingBattleDuringFireSettlesOnceAndRestoresIdle()
    {
        yield return new EnterPlayMode();
        yield return Load();
        yield return VerifyBattleCancellation();
    }

    private static IEnumerator VerifyBattleCancellation()
    {
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardData card = Card(false, out UISpriteAnimation clip);
        CardUI ui = battle.GetHand(card.deckType).GetCardsSnapshot().First(c => c.CanMulligan);
        ui.SetCard(card, battle);
        UISpriteAnimator animator = battle.GetPlayer().GetComponent<BattleCharacterPresentation>().Visual.GetComponent<UISpriteAnimator>();
        UISpriteAnimation idle = animator.Animation;
        int block = battle.GetPlayer().GetBlock();
        Assert.That(battle.UseCard(ui), Is.True);
        yield return Until(() => animator.Animation == clip);
        battle.EndBattle();
        yield return Until(() => ui == null);
        Assert.That(ui == null, Is.True);
        Assert.That(animator.Animation, Is.SameAs(idle));
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(block));
        Assert.That(battle.GetDeckController().GetDiscardPile(card.deckType).Count(c => c == card), Is.EqualTo(1));
    }

    private static IEnumerator VerifyUse(bool xCost)
    {
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardData card = Card(xCost, out UISpriteAnimation clip);
        CardHandController hand = battle.GetHand(card.deckType);
        CardUI ui = hand.GetCardsSnapshot().First(c => c.CanMulligan);
        ui.SetCard(card, battle);
        RectTransform visual = (RectTransform)Get(ui, "cardVisual");
        RectTransform usingPoint = (RectTransform)Get(battle, "cardUsingPoint");
        UISpriteAnimator animator = battle.GetPlayer().GetComponent<BattleCharacterPresentation>().Visual.GetComponent<UISpriteAnimator>();
        int block = battle.GetPlayer().GetBlock();
        Assert.That(battle.UseCard(ui), Is.True);
        Assert.That(battle.BlocksCardInteraction, Is.True, "Input must stay locked while the card moves to the using area.");
        Assert.That(battle.UseCard(ui), Is.False);
        yield return Until(() => animator.Animation == clip);
        Assert.That(Vector3.Distance(visual.position, usingPoint.position), Is.LessThan(.1f), "The card must arrive before the firing animation starts.");
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(block));
        Assert.That(battle.GetDeckController().GetDiscardPile(card.deckType).Count(c => c == card), Is.Zero);
        Assert.That(battle.BlocksCardInteraction, Is.True);
        animator.Advance(1000f);
        yield return Until(() => battle.GetPlayer().GetBlock() > block);
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(block + (xCost ? 6 : 2)));
        Assert.That(Vector3.Distance(visual.position, usingPoint.position), Is.LessThan(.1f), "The card must stay in the using area during recovery.");
        Assert.That(battle.GetDeckController().GetDiscardPile(card.deckType).Count(c => c == card), Is.Zero);
        yield return Until(() => ui == null && !battle.HasOtherCardUses && !battle.HandsAreMoving);
        Assert.That(battle.GetDeckController().GetDiscardPile(card.deckType).Count(c => c == card), Is.EqualTo(1));
        Assert.That(hand.GetCurrentHandSize(), Is.EqualTo(3));
    }

    private static CardData Card(bool xCost, out UISpriteAnimation clip)
    {
        clip = Track(ScriptableObject.CreateInstance<UISpriteAnimation>());
        Texture2D texture = Track(new Texture2D(2, 2));
        Sprite frame = Track(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f));
        clip.frames = new[] { frame, frame };
        clip.frameDuration = 5f;
        PlaySpritePresentationStep fire = Track(ScriptableObject.CreateInstance<PlaySpritePresentationStep>());
        fire.animation = clip; fire.waitForCompletion = true;
        WaitPresentationStep recovery = Track(ScriptableObject.CreateInstance<WaitPresentationStep>());
        recovery.duration = .5f;
        CardPresentation presentation = Track(ScriptableObject.CreateInstance<CardPresentation>());
        presentation.beforeImpact = new CardPresentationStep[] { fire };
        presentation.afterImpact = new CardPresentationStep[] { recovery };
        BlockEffect block = Track(ScriptableObject.CreateInstance<BlockEffect>()); block.block = 2;
        CardData card = Track(ScriptableObject.CreateInstance<CardData>());
        card.cardName = "Presentation flow test";
        card.type = CardType.Attack; card.deckType = CardDeckType.TypeDream;
        card.cost = new Cost(0, 0); card.costMode = xCost ? CardCostMode.X : CardCostMode.Fixed;
        card.effects = new CardEffect[] { block }; card.presentation = presentation;
        return card;
    }

    private static T Track<T>(T value) where T : Object { (created ??= new List<Object>()).Add(value); return value; }
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return Until(() =>
        {
            BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
            return battle != null && battle.CanInspectCards && battle.GetPlayer() != null &&
                battle.GetPlayer().GetComponent<BattleCharacterPresentation>()?.Visual != null &&
                battle.GetHand(CardDeckType.TypeDream)?.GetCardsSnapshot().Count(c => c.CanMulligan) == 3 &&
                battle.GetHand(CardDeckType.TypeNightmare)?.GetCardsSnapshot().Count(c => c.CanMulligan) == 3;
        });
    }
    private static IEnumerator Until(System.Func<bool> ready)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!ready())
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Card use did not reach the expected phase.");
            yield return null;
        }
    }
}
