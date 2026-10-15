using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class MulliganTests
{
    private List<Object> created = new List<Object>();

    [TearDown]
    public void Cleanup()
    {
        foreach (Object value in created ?? new List<Object>()) if (value != null) Object.DestroyImmediate(value);
        created?.Clear(); RunDeckInventory.ResetForNewGame();
    }

    [UnityTest]
    public IEnumerator ManualClicksToggleGreenBordersCountAndRequireExplicitCompletion()
    {
        yield return new EnterPlayMode(); yield return LoadBattle();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardHandController dream = Hand(battle, CardDeckType.TypeDream);
        CardHandController nightmare = Hand(battle, CardDeckType.TypeNightmare);
        CardUI source = Cards(dream)[0];
        CardUI first = Cards(dream)[1], second = Cards(nightmare)[0], extra = Cards(nightmare)[1];
        DeckController deck = (DeckController)Get(battle, "deckController");
        FillDrawPile(deck, CardDeckType.TypeDream, 5); FillDrawPile(deck, CardDeckType.TypeNightmare, 5);
        CardData firstData = first.GetCardData(), secondData = second.GetCardData();
        BlockEffect after = Asset<BlockEffect>(); after.block = 7;
        CardData data = Card(CardDeckType.TypeDream, Mulligan(2, 1, 2, 0), after);
        data.exhaustOnUse = true; source.SetCard(data, battle);
        int before = battle.GetPlayer().GetBlock();
        Assert.That(battle.UseCard(source), Is.True);
        yield return WaitForSelection();
        TMP_Text count = SelectionCount(); UnityEngine.UI.Button complete = CompleteButton();
        Assert.That(count.text, Is.EqualTo("0/2")); Assert.That(complete.interactable, Is.False);
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(before), "Following effects must wait for confirmation.");
        ClickThroughRaycast(first); Assert.That(count.text, Is.EqualTo("1/2")); AssertGreen(first, true);
        Click(first); Assert.That(count.text, Is.EqualTo("0/2")); AssertGreen(first, false);
        Click(first); Click(second);
        Assert.That(count.text, Is.EqualTo("2/2")); Assert.That(complete.interactable, Is.True);
        AssertGreen(first, true); AssertGreen(second, true);
        Click(extra); Assert.That(count.text, Is.EqualTo("2/2")); AssertGreen(extra, false);
        Click(source); Assert.That(count.text, Is.EqualTo("2/2"));
        Assert.That(battle.UseCard(extra), Is.False);
        battle.EndPlayerTurn(); Assert.That(Get(battle, "currentState"), Is.EqualTo(BattleState.PlayerTurn));
        Assert.That(CardInspectionUI.TryOpen(firstData, battle), Is.False);
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(before), "Choosing enough cards is not confirmation.");
        complete.onClick.Invoke(); complete.onClick.Invoke();
        yield return WaitUntilIdle(battle);
        Assert.That(SelectionCount(), Is.Null);
        Assert.That(first == null && second == null, Is.True);
        Assert.That(deck.GetDiscardPile(CardDeckType.TypeDream), Has.Member(firstData));
        Assert.That(deck.GetDiscardPile(CardDeckType.TypeNightmare), Has.Member(secondData));
        Assert.That(deck.GetDrawPile(CardDeckType.TypeDream), Has.Count.EqualTo(3), "One replacement plus one normal source-card refill.");
        Assert.That(deck.GetDrawPile(CardDeckType.TypeNightmare), Has.Count.EqualTo(4));
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(before + 7));
        Assert.That(deck.GetExhaustPile(data.deckType), Has.Member(data));
        Assert.That(CardInspectionUI.BlocksGameplayInput, Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RandomAndAllRespectScopeInstanceCountAndNeverOpenSelection()
    {
        yield return new EnterPlayMode();
        foreach (int scope in new[] { 0, 1, 2 })
        foreach (int mode in new[] { 0, 1 })
        {
            yield return LoadBattle();
            BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
            DeckController deck = (DeckController)Get(battle, "deckController");
            CardUI source = Cards(Hand(battle, CardDeckType.TypeDream))[0];
            CardUI[] candidates = TargetCards(battle, scope, source);
            CardUI[] protectedCards = TargetCards(battle, scope == 0 ? 1 : 0, source);
            int expected = mode == 0 ? candidates.Length : Math.Min(2, candidates.Length);
            FillDrawPile(deck, CardDeckType.TypeDream, 10); FillDrawPile(deck, CardDeckType.TypeNightmare, 10);
            CardData data = Card(CardDeckType.TypeDream, Mulligan(scope, mode, 2, mode == 0 ? 0 : 1));
            data.exhaustOnUse = true; source.SetCard(data, battle);
            Assert.That(battle.UseCard(source), Is.True);
            yield return WaitUntilIdle(battle);
            Assert.That(SelectionCount(), Is.Null);
            Assert.That(candidates.Count(c => c == null), Is.EqualTo(expected));
            if (scope != 2) Assert.That(protectedCards.All(c => c != null), Is.True);
            Assert.That(deck.GetDiscardPile(CardDeckType.TypeDream).Count + deck.GetDiscardPile(CardDeckType.TypeNightmare).Count,
                Is.EqualTo(expected));
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SameAssetCopiesAreSeparateAndAvailableCountCapsManualSelection()
    {
        yield return new EnterPlayMode(); yield return LoadBattle();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI[] dream = Cards(Hand(battle, CardDeckType.TypeDream));
        CardData shared = Card(CardDeckType.TypeDream); shared.exhaustOnUse = true;
        dream[1].SetCard(shared, battle); dream[2].SetCard(shared, battle);
        CardData source = Card(CardDeckType.TypeDream, Mulligan(0, 1, 99, 0)); source.exhaustOnUse = true;
        dream[0].SetCard(source, battle);
        DeckController deck = (DeckController)Get(battle, "deckController"); FillDrawPile(deck, CardDeckType.TypeDream, 5);
        Assert.That(battle.UseCard(dream[0]), Is.True); yield return WaitForSelection();
        Assert.That(SelectionCount().text, Is.EqualTo("0/2"));
        Click(dream[1]); Click(dream[2]); Assert.That(SelectionCount().text, Is.EqualTo("2/2"));
        CompleteButton().onClick.Invoke(); yield return WaitUntilIdle(battle);
        Assert.That(CountCards(deck.GetDiscardPile(CardDeckType.TypeDream), shared), Is.EqualTo(2));
        Assert.That(deck.GetExhaustPile(CardDeckType.TypeDream), Has.No.Member(shared), "Mulligan is not use/exhaust.");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator NoCandidatesSkipsSelectionAndContinuesEffectsImmediately()
    {
        yield return new EnterPlayMode(); yield return LoadBattle();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardHandController dream = Hand(battle, CardDeckType.TypeDream);
        CardUI source = Cards(dream)[0];
        foreach (CardUI other in Cards(dream)) if (other != source) { dream.RemoveCard(other); Object.Destroy(other.gameObject); }
        BlockEffect block = Asset<BlockEffect>(); block.block = 7;
        source.SetCard(Card(CardDeckType.TypeDream, Mulligan(0, 1, 2, 0), block), battle);
        int before = battle.GetPlayer().GetBlock();
        Assert.That(battle.UseCard(source), Is.True); yield return WaitUntilIdle(battle);
        Assert.That(SelectionCount(), Is.Null); Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(before + 7));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator NestedAmmoBonusWaitsForSelectionAndPaysAmmoOnlyOnce()
    {
        yield return new EnterPlayMode(); yield return LoadBattle();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI[] dream = Cards(Hand(battle, CardDeckType.TypeDream));
        SpendAmmoBonusEffect nested = Asset<SpendAmmoBonusEffect>();
        BlockEffect block = Asset<BlockEffect>(); block.block = 7;
        Set(nested, "bonusEffects", new[] { Mulligan(0, 1, 1, 0), block });
        dream[0].SetCard(Card(CardDeckType.TypeDream, nested), battle);
        battle.GetPlayer().GetAmmoSystem().Load(2);
        int before = battle.GetPlayer().GetBlock();
        Assert.That(battle.UseCard(dream[0]), Is.True); yield return WaitForSelection();
        Assert.That(battle.GetPlayer().GetAmmoSystem().CurrentAmmo, Is.EqualTo(1));
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(before));
        Click(dream[1]); CompleteButton().onClick.Invoke(); yield return WaitUntilIdle(battle);
        Assert.That(battle.GetPlayer().GetAmmoSystem().CurrentAmmo, Is.EqualTo(1));
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(before + 7));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DisablingBattleClearsSelectionAndBordersWithoutExchangingCards()
    {
        yield return new EnterPlayMode(); yield return LoadBattle();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI[] dream = Cards(Hand(battle, CardDeckType.TypeDream));
        dream[0].SetCard(Card(CardDeckType.TypeDream, Mulligan(0, 1, 1, 0)), battle);
        Assert.That(battle.UseCard(dream[0]), Is.True); yield return WaitForSelection();
        Click(dream[1]); AssertGreen(dream[1], true);
        battle.enabled = false; yield return null;
        Assert.That(SelectionCount(), Is.Null); AssertGreen(dream[1], false);
        Assert.That(CardInspectionUI.BlocksGameplayInput, Is.False);
        Assert.That(dream[1].GetHandController().GetComponentsInChildren<CardUI>(), Has.Member(dream[1]));
        Assert.That(dream[0] == null, Is.True, "An interrupted used card cannot remain detached and busy.");
        battle.enabled = true;
        CardUI next = Cards(Hand(battle, CardDeckType.TypeDream))[0];
        next.SetCard(Card(CardDeckType.TypeDream, Mulligan(1, 1, 1, 0)), battle);
        Assert.That(battle.UseCard(next), Is.True); yield return WaitForSelection();
        CardUI target = Hand(battle, CardDeckType.TypeNightmare).GetComponentsInChildren<CardUI>()[0];
        Click(target); CompleteButton().onClick.Invoke(); yield return WaitUntilIdle(battle);
        Assert.That(CardInspectionUI.BlocksGameplayInput, Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator InterruptingCommittedExchangeCleansDetachedCardsAndPreservesCardData()
    {
        yield return new EnterPlayMode(); yield return LoadBattle();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI[] dream = Cards(Hand(battle, CardDeckType.TypeDream));
        DeckController deck = (DeckController)Get(battle, "deckController");
        FillDrawPile(deck, CardDeckType.TypeDream, 5);
        CardData replaced = dream[1].GetCardData();
        CardData source = Card(CardDeckType.TypeDream, Mulligan(0, 1, 1, 0)); source.exhaustOnUse = true;
        dream[0].SetCard(source, battle);
        Assert.That(battle.UseCard(dream[0]), Is.True); yield return WaitForSelection();
        Click(dream[1]); CompleteButton().onClick.Invoke(); yield return null;
        battle.enabled = false; yield return null;
        Assert.That(dream[1] == null && dream[0] == null, Is.True, "Committed animation objects must be disposed on disable.");
        Assert.That(deck.GetDiscardPile(CardDeckType.TypeDream), Has.Member(replaced));
        Assert.That(deck.GetExhaustPile(CardDeckType.TypeDream), Has.Member(source));
        Assert.That(CardInspectionUI.BlocksGameplayInput, Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ContinuousActivationWaitsForSelectionBeforeApplyingItsScopedStatus()
    {
        yield return new EnterPlayMode(); yield return LoadBattle();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI[] dream = Cards(Hand(battle, CardDeckType.TypeDream));
        ApplyStatusEffect status = Asset<ApplyStatusEffect>();
        Set(status, "status", UnityEditor.AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/Rust.asset"));
        Set(status, "target", (int)CardTarget.Player);
        CardData data = Card(CardDeckType.TypeDream, Mulligan(0, 1, 1, 0), status);
        data.type = CardType.Continuous; data.continuousDurationType = ContinuousDurationType.FixedTurns; data.continuousTurns = 1;
        dream[0].SetCard(data, battle);
        Assert.That(battle.UseCard(dream[0]), Is.True); yield return WaitForSelection();
        Assert.That(battle.GetPlayer().StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
        Click(dream[1]); CompleteButton().onClick.Invoke(); yield return WaitUntilIdle(battle);
        Assert.That(battle.GetPlayer().StatusEffects.HasStatus(StatusEffectType.Rust), Is.True);
        Assert.That(battle.GetPlayer().ContinuousCards.ActiveCards, Has.Count.EqualTo(1));
        battle.GetPlayer().ContinuousCards.ReduceRemainingTurns();
        Assert.That(battle.GetPlayer().StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
        Assert.That(battle.GetDeckController().GetDrawPile(data.deckType), Has.Member(data));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RecurringMulliganWaitsForConfirmationBeforeFollowingEffectAndExpiry()
    {
        yield return new EnterPlayMode(); yield return LoadBattle();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI target = Cards(Hand(battle, CardDeckType.TypeNightmare))[0];
        BlockEffect block = Asset<BlockEffect>(); block.block = 7;
        CardData data = Card(CardDeckType.TypeDream);
        data.type = CardType.Continuous; data.continuousDurationType = ContinuousDurationType.FixedTurns; data.continuousTurns = 1;
        data.turnStartEffects = new[] { Mulligan(1, 1, 1, 0), block };
        ActiveContinuousCard entry = battle.GetPlayer().ContinuousCards.Activate(data, battle, battle.GetDeckController());
        int before = battle.GetPlayer().GetBlock();
        battle.GetPlayer().ContinuousCards.ProcessTurnStartEffects(battle);
        yield return WaitForSelection();
        Assert.That(entry.RemainingTurns, Is.EqualTo(1)); Assert.That(entry.IsActive, Is.True);
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(before));
        Click(target); CompleteButton().onClick.Invoke();
        float deadline = Time.realtimeSinceStartup + 12;
        while (entry.IsActive && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(entry.IsActive, Is.False); Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(before + 7));
        Assert.That(battle.GetDeckController().GetDrawPile(data.deckType), Has.Member(data));
        Assert.That(CardInspectionUI.BlocksGameplayInput, Is.False);
        yield return new ExitPlayMode();
    }

    private CardEffect Mulligan(int scope, int mode, int count, int selection)
    {
        Type type = typeof(CardEffect).Assembly.GetType("MulliganEffect");
        Assert.That(type, Is.Not.Null, "A selectable/random/all MulliganEffect must be available.");
        var effect = (CardEffect)ScriptableObject.CreateInstance(type); (created ??= new List<Object>()).Add(effect);
        Set(effect, "handScope", scope); Set(effect, "countMode", mode); Set(effect, "count", count); Set(effect, "selectionMode", selection);
        return effect;
    }
    private CardData Card(CardDeckType deck, params CardEffect[] effects)
    {
        CardData card = Asset<CardData>(); card.cardName = "Mulligan Test"; card.type = CardType.Skill;
        card.deckType = deck; card.effects = effects; card.cost = new Cost(0, 0); return card;
    }
    private T Asset<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>(); (created ??= new List<Object>()).Add(value); return value;
    }
    private void FillDrawPile(DeckController deck, CardDeckType type, int count)
    {
        string prefix = type == CardDeckType.TypeDream ? "dream" : "nightmare";
        var pile = (List<CardData>)Get(deck, prefix + "DrawPile"); pile.Clear();
        ((List<CardData>)Get(deck, prefix + "DiscardPile")).Clear();
        for (int i = 0; i < count; i++) pile.Add(Card(type));
    }
    private static int CountCards(IReadOnlyList<CardData> cards, CardData target)
    {
        int count = 0; foreach (CardData card in cards) if (card == target) count++; return count;
    }
    private static CardUI[] TargetCards(BattleManager battle, int scope, CardUI source)
    {
        var result = new List<CardUI>();
        if (scope != 1) result.AddRange(Cards(Hand(battle, CardDeckType.TypeDream)));
        if (scope != 0) result.AddRange(Cards(Hand(battle, CardDeckType.TypeNightmare)));
        result.Remove(source); return result.ToArray();
    }
    private static CardUI[] Cards(CardHandController hand) => hand.GetComponentsInChildren<CardUI>().Where(c => c.CanHover).ToArray();
    private static CardHandController Hand(BattleManager battle, CardDeckType type) =>
        (CardHandController)Get(battle, type == CardDeckType.TypeDream ? "dreamCardHand" : "nightmareCardHand");
    private static void Click(CardUI card) => ExecuteEvents.Execute(card.gameObject,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    private static void ClickThroughRaycast(CardUI card)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform rect = (RectTransform)card.transform;
        Canvas canvas = card.GetComponentInParent<Canvas>().rootCanvas;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits, Is.Not.Empty, "The visible hand card must receive screen-space UI raycasts.");
        GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.That(receiver, Is.SameAs(card.gameObject), "The used card or selection UI must not obscure a candidate's hit area.");
        ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
    }
    private static TMP_Text SelectionCount() => Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
        .SingleOrDefault(t => t.name == "MulliganCount" && t.gameObject.activeInHierarchy);
    private static UnityEngine.UI.Button CompleteButton() => Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
        .Single(b => b.name == "MulliganComplete" && b.gameObject.activeInHierarchy);
    private static void AssertGreen(CardUI card, bool selected)
    {
        Transform border = card.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MulliganSelectionBorder");
        if (!selected) { Assert.That(border == null || !border.gameObject.activeSelf, Is.True); return; }
        Assert.That(border, Is.Not.Null); Assert.That(border.gameObject.activeSelf, Is.True);
        foreach (UnityEngine.UI.Image edge in border.GetComponentsInChildren<UnityEngine.UI.Image>())
        { Assert.That(edge.color.g, Is.GreaterThan(0.8f)); Assert.That(edge.color.r, Is.LessThan(0.3f)); Assert.That(edge.raycastTarget, Is.False); }
    }
    private static IEnumerator LoadBattle()
    {
        yield return SceneManager.LoadSceneAsync("BattleScene");
        float deadline = Time.realtimeSinceStartup + 12;
        while (Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Count(c => c.CanHover) < 6 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Count(c => c.CanHover), Is.GreaterThanOrEqualTo(6));
    }
    private static IEnumerator WaitForSelection()
    {
        float deadline = Time.realtimeSinceStartup + 12;
        while (SelectionCount() == null && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(SelectionCount(), Is.Not.Null);
    }
    private static IEnumerator WaitUntilIdle(BattleManager battle)
    {
        float deadline = Time.realtimeSinceStartup + 12;
        while ((int)Get(battle, "activeCardUses") > 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That((int)Get(battle, "activeCardUses"), Is.Zero);
    }
    private static FieldInfo Field(object value, string name)
    {
        for (Type type = value.GetType(); type != null; type = type.BaseType)
        { FieldInfo f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f; }
        throw new ArgumentException(name);
    }
    private static object Get(object value, string name) => Field(value, name).GetValue(value);
    private static void Set(object value, string name, object data)
    {
        FieldInfo field = Field(value, name); field.SetValue(value, field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, data) : data);
    }
}
