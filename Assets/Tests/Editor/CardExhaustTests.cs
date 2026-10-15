using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class CardExhaustTests
{
    private List<Object> created = new List<Object>();

    [TearDown]
    public void Cleanup()
    {
        foreach (Object value in created ?? new List<Object>())
            if (value != null) Object.DestroyImmediate(value);
        created?.Clear();
        RunDeckInventory.ResetForNewGame();
    }

    [Test]
    public void ExistingAttackAndSkillAssetsKeepExhaustDisabled()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:CardData", new[] { "Assets/CardData" }))
        {
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
            if (card.type != CardType.Continuous)
                Assert.That(card.exhaustOnUse, Is.False, AssetDatabase.GUIDToAssetPath(guid));
        }
    }

    [UnityTest]
    public IEnumerator AttackAndSkillExhaustOnlyTheUsedCopyInBothDecksAndDoNotReshuffleIt()
    {
        yield return new EnterPlayMode();
        foreach (CardDeckType deckType in new[] { CardDeckType.TypeDream, CardDeckType.TypeNightmare })
        foreach (CardType type in new[] { CardType.Attack, CardType.Skill })
        {
            yield return LoadBattle();
            BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
            DeckController deck = (DeckController)Get(battle, "deckController");
            CardHandController hand = Hand(battle, deckType);
            CardUI[] cards = hand.GetComponentsInChildren<CardUI>().Where(c => c.CanHover).ToArray();
            Assert.That(cards.Length, Is.GreaterThanOrEqualTo(2));
            CardData data = Card(type, deckType, true);
            BlockEffect block = Asset<BlockEffect>();
            block.block = 4;
            data.effects = new CardEffect[] { block };
            ClearPiles(deck, deckType);
            deck.AddCardToDeck(data, deckType);
            deck.AddCardToDeck(data, deckType);
            Assert.That(deck.DrawCard(deckType), Is.SameAs(data));
            Assert.That(deck.DrawCard(deckType), Is.SameAs(data));
            cards[0].SetCard(data, battle);
            cards[1].SetCard(data, battle);
            int before = battle.GetPlayer().GetBlock();
            Assert.That(battle.UseCard(cards[0]), Is.True);
            Assert.That(battle.UseCard(cards[0]), Is.False, "A second click must not resolve or exhaust again.");
            yield return WaitUntilIdle(battle);
            Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(before + 4));
            Assert.That(deck.GetExhaustPile(deckType).Count(c => c == data), Is.EqualTo(1));
            Assert.That(deck.GetDiscardPile(deckType), Is.Empty);
            Assert.That(cards[1].IsUsed, Is.False, "A shared CardData asset must not exhaust another copy in hand.");
            Assert.That(deck.GetDeck(deckType).Count(c => c == data), Is.EqualTo(2), "Exhaustion preserves ownership.");
            Assert.That(deck.GetExhaustPile(Other(deckType)), Is.Empty);
            CardData filler = Card(CardType.Skill, deckType, false);
            deck.DiscardCard(filler, deckType);
            Assert.That(deck.DrawCard(deckType), Is.SameAs(filler));
            Assert.That(deck.DrawCard(deckType), Is.Null, "Reshuffling cannot bring the exhausted card back.");
            Assert.That(battle.GetPlayer().ContinuousCards.ActiveCards, Is.Empty);
            deck.ResetForBattle();
            Assert.That(deck.GetExhaustPile(deckType), Is.Empty);
            Assert.That(deck.GetDrawPile(deckType).Count(c => c == data), Is.EqualTo(2), "Exclusion ends when a new battle initializes.");
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator OrdinaryUseStillDiscardsAndFailedUseDoesNotExhaustOrRunEffects()
    {
        yield return new EnterPlayMode();
        foreach (CardType type in new[] { CardType.Attack, CardType.Skill })
        {
            yield return LoadBattle();
            BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
            DeckController deck = (DeckController)Get(battle, "deckController");
            CardHandController hand = Hand(battle, CardDeckType.TypeDream);
            CardUI ui = hand.GetComponentsInChildren<CardUI>().First(c => c.CanHover);
            CardData data = Card(type, CardDeckType.TypeDream, true);
            BlockEffect block = Asset<BlockEffect>(); block.block = 4;
            data.effects = new CardEffect[] { block };
            data.requiredAmmo = int.MaxValue;
            ui.SetCard(data, battle);
            int before = battle.GetPlayer().GetBlock();
            Assert.That(battle.UseCard(ui), Is.False);
            Assert.That(ui.IsUsed, Is.False);
            Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(before));
            Assert.That(deck.GetExhaustPile(data.deckType), Is.Empty);
            data.requiredAmmo = 0;
            data.exhaustOnUse = false;
            ClearPiles(deck, data.deckType);
            Assert.That(battle.UseCard(ui), Is.True);
            // Prevent the replacement draw so the used card's destination can be observed.
            Set(battle, "currentState", BattleState.EnemyTurn);
            yield return WaitUntilIdle(battle);
            Assert.That(deck.GetDiscardPile(data.deckType), Has.Member(data));
            Assert.That(deck.GetExhaustPile(data.deckType), Is.Empty);
            Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(before + 4));
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator EndingTurnDuringUseExhaustsUsedCardsAndDiscardsUnusedExhaustCards()
    {
        yield return new EnterPlayMode();
        yield return LoadBattle();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle, Is.Not.Null);
        DeckController deck = (DeckController)Get(battle, "deckController");
        CardData unused = Card(CardType.Skill, CardDeckType.TypeDream, true);
        CardUI[] dreamCards = Hand(battle, unused.deckType).GetComponentsInChildren<CardUI>().Where(c => c.CanHover).ToArray();
        dreamCards[1].SetCard(unused, battle);
        var used = new List<CardData>();
        foreach (CardDeckType deckType in new[] { CardDeckType.TypeDream, CardDeckType.TypeNightmare })
        {
            CardData data = Card(deckType == CardDeckType.TypeDream ? CardType.Attack : CardType.Skill, deckType, true);
            used.Add(data);
            CardUI ui = Hand(battle, deckType).GetComponentsInChildren<CardUI>().First(c => c.CanHover);
            ui.SetCard(data, battle);
            Assert.That(battle.UseCard(ui), Is.True);
        }
        Assert.That((int)Get(battle, "activeCardUses"), Is.EqualTo(2));
        battle.EndPlayerTurn();
        float deadline = Time.realtimeSinceStartup + 20f;
        while ((BattleState)Get(battle, "currentState") != BattleState.PlayerTurn && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That((BattleState)Get(battle, "currentState"), Is.EqualTo(BattleState.PlayerTurn));
        foreach (CardData data in used)
        {
            Assert.That(deck.GetExhaustPile(data.deckType).Count(c => c == data), Is.EqualTo(1));
            Assert.That(deck.GetDrawPile(data.deckType), Has.No.Member(data));
            Assert.That(deck.GetDiscardPile(data.deckType), Has.No.Member(data));
            Assert.That(Hand(battle, data.deckType).GetComponentsInChildren<CardUI>().Any(c => c.GetCardData() == data), Is.False);
        }
        Assert.That(deck.GetExhaustPile(unused.deckType), Has.No.Member(unused), "An unused exhaust card must follow normal hand discard.");
        Assert.That(dreamCards[1] == null, Is.True, "The previous hand view must be discarded even if the card is drawn again.");
        bool unusedStillAvailable = deck.GetDrawPile(unused.deckType).Contains(unused) ||
            deck.GetDiscardPile(unused.deckType).Contains(unused) ||
            HandContains(Hand(battle, unused.deckType), unused);
        Assert.That(unusedStillAvailable, Is.True);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator HoverAndInspectionExplainExhaustAlongsideStatusesAndClearWhenRebound()
    {
        yield return new EnterPlayMode();
        yield return LoadBattle();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI ui = Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).First(c => c.CanHover);
        var pointer = new PointerEventData(EventSystem.current);
        foreach (CardType type in new[] { CardType.Attack, CardType.Skill, CardType.Continuous })
        {
            CardData data = Card(type, CardDeckType.TypeDream, true);
            data.continuousDurationType = ContinuousDurationType.FixedTurns;
            data.continuousTurns = 2;
            ApplyStatusEffect effect = Asset<ApplyStatusEffect>();
            Set(effect, "status", AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/Rust.asset"));
            data.effects = new CardEffect[] { effect };
            ui.SetCard(data, battle);
            ExecuteEvents.Execute(ui.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return Wait(1.2f);
            TMP_Text tooltip = VisibleTooltip(ui);
            Assert.That(tooltip, Is.Not.Null);
            Assert.That(tooltip.text, Does.Contain("<b>소멸</b>").And.Contain("이번 전투 동안 제외").And.Contain("녹슬음"));
            ExecuteEvents.Execute(ui.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            Assert.That(VisibleTooltip(ui), Is.Null);
            var rightClick = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right };
            ExecuteEvents.Execute(ui.gameObject, rightClick, ExecuteEvents.pointerClickHandler);
            CardInspectionUI inspection = Object.FindFirstObjectByType<CardInspectionUI>();
            Assert.That(inspection.IsOpen, Is.True);
            TMP_Text explanation = (TMP_Text)Get(inspection, "statusText");
            Assert.That(explanation.text, Does.Contain("<b>소멸</b>").And.Contain("이번 전투 동안 제외").And.Contain("녹슬음"));
            Assert.That(battle.GetPlayer().StatusEffects.ActiveEffects, Is.Empty, "A card trait is not a character status.");
            Assert.That(battle.GetTarget().StatusEffects.ActiveEffects, Is.Empty);
            inspection.Close();
            yield return null; yield return null;
        }
        CardData plain = Card(CardType.Skill, CardDeckType.TypeDream, false);
        ui.SetCard(plain, battle);
        ExecuteEvents.Execute(ui.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return Wait(1.2f);
        Assert.That(VisibleTooltip(ui), Is.Null, "Rebinding to a plain card must clear the previous trait.");
        CardData permanent = Card(CardType.Continuous, CardDeckType.TypeDream, true);
        permanent.continuousDurationType = ContinuousDurationType.UntilBattleEnd;
        ui.SetCard(permanent, battle);
        ExecuteEvents.Execute(ui.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return Wait(1.2f);
        Assert.That(VisibleTooltip(ui), Is.Null, "Permanent continuous cards absorb even with a stale exhaust flag.");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ExhaustOnlyCardWithoutStatusEffectsHasBothTooltipAndInspectionExplanation()
    {
        yield return new EnterPlayMode();
        yield return LoadBattle();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI ui = Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).First(c => c.CanHover);
        CardData card = Card(CardType.Skill, CardDeckType.TypeDream, true);
        ui.SetCard(card, battle);
        ExecuteEvents.Execute(ui.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
        yield return Wait(1.2f);
        Assert.That(VisibleTooltip(ui), Is.Not.Null, "An exhaust-only card needs a tooltip even without statuses.");
        Assert.That(VisibleTooltip(ui).text, Does.Contain("이번 전투 동안 제외"));
        Assert.That(CardInspectionUI.TryOpen(card, battle), Is.True);
        CardInspectionUI inspection = Object.FindFirstObjectByType<CardInspectionUI>();
        Assert.That(((RectTransform)Get(inspection, "statusPanel")).gameObject.activeSelf, Is.True);
        Assert.That(((TMP_Text)Get(inspection, "statusText")).text, Does.Contain("<b>소멸</b>"));
        inspection.Close();
        yield return new ExitPlayMode();
    }

    private CardData Card(CardType type, CardDeckType deck, bool exhaust)
    {
        CardData card = Asset<CardData>();
        card.cardName = "Exhaust Test"; card.type = type; card.deckType = deck;
        card.cost = new Cost(0, 0); card.exhaustOnUse = exhaust;
        return card;
    }

    private T Asset<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        (created ??= new List<Object>()).Add(value); return value;
    }

    private static CardDeckType Other(CardDeckType type) => type == CardDeckType.TypeDream ? CardDeckType.TypeNightmare : CardDeckType.TypeDream;
    private static CardHandController Hand(BattleManager battle, CardDeckType type) =>
        (CardHandController)Get(battle, type == CardDeckType.TypeDream ? "dreamCardHand" : "nightmareCardHand");
    private static bool HandContains(CardHandController hand, CardData card)
    {
        // Avoid capturing the iterator's pre-EnterPlayMode locals across the domain reload.
        foreach (CardUI ui in hand.GetComponentsInChildren<CardUI>())
            if (ui.GetCardData() == card) return true;
        return false;
    }
    private static void ClearPiles(DeckController deck, CardDeckType type)
    {
        string prefix = type == CardDeckType.TypeDream ? "dream" : "nightmare";
        ((List<CardData>)Get(deck, prefix + "DrawPile")).Clear();
        ((List<CardData>)Get(deck, prefix + "DiscardPile")).Clear();
    }
    private static TMP_Text VisibleTooltip(CardUI card) => card.GetComponentInParent<Canvas>().rootCanvas
        .GetComponentsInChildren<TMP_Text>(true).SingleOrDefault(text =>
            text.transform.parent.name.StartsWith("CardStatusTooltip") && text.gameObject.activeInHierarchy);
    private static IEnumerator LoadBattle()
    {
        yield return SceneManager.LoadSceneAsync("BattleScene");
        float deadline = Time.realtimeSinceStartup + 12f;
        while (Time.realtimeSinceStartup < deadline &&
            Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Count(c => c.CanHover) < 6) yield return null;
        Assert.That(Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Count(c => c.CanHover), Is.GreaterThanOrEqualTo(6));
    }
    private static IEnumerator WaitUntilIdle(BattleManager battle)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while ((int)Get(battle, "activeCardUses") > 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That((int)Get(battle, "activeCardUses"), Is.Zero);
    }
    private static IEnumerator Wait(float seconds)
    {
        float until = Time.unscaledTime + seconds;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.unscaledTime < until && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Time.unscaledTime, Is.GreaterThanOrEqualTo(until));
    }
    private static FieldInfo Field(object value, string name)
    {
        for (System.Type type = value.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field;
        }
        throw new System.ArgumentException(name);
    }
    private static object Get(object value, string name) => Field(value, name).GetValue(value);
    private static void Set(object value, string name, object data) => Field(value, name).SetValue(value, data);
}
