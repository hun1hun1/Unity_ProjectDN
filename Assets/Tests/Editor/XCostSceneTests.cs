using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class XCostSceneTests
{
    private List<Object> created = new List<Object>();
    [TearDown] public void Cleanup()
    {
        foreach (Object value in created ?? new List<Object>()) if (value != null) Object.DestroyImmediate(value);
        created?.Clear(); RunDeckInventory.ResetForNewGame();
    }

    [UnityTest]
    public IEnumerator PositiveAndZeroXSettleExactlyOneCardAndRefillBothDecks()
    {
        yield return new EnterPlayMode(); yield return Load();
        BattleManager b = Object.FindFirstObjectByType<BattleManager>();
        for (int deckIndex = 0; deckIndex < 2; deckIndex++)
            for (int amountIndex = 0; amountIndex < 2; amountIndex++)
                for (int exhaustIndex = 0; exhaustIndex < 2; exhaustIndex++)
                {
                    CardDeckType type = (CardDeckType)deckIndex;
                    int amount = amountIndex == 0 ? 3 : 0;
                    bool exhaust = exhaustIndex == 1;
                    FillDraw(b.GetDeckController(), type, 8);
                    b.GetPlayer().GetCostSystem().ResetForTurn();
                    if (type == CardDeckType.TypeDream) b.GetPlayer().GetCostSystem().ModifyDreamCost(amount - 3);
                    else b.GetPlayer().GetCostSystem().ModifyNightmareCost(amount);
                    CardData card = Card(type, Block(2)); card.exhaustOnUse = exhaust;
                    CardUI ui = b.GetHand(type).GetCardsSnapshot().First(c => c.CanMulligan); ui.SetCard(card, b);
                    int beforeBlock = b.GetPlayer().GetBlock();
                    Assert.That(b.UseCard(ui), Is.True); Assert.That(b.UseCard(ui), Is.False);
                    yield return Idle(b);
                    Assert.That(b.GetPlayer().GetBlock(), Is.EqualTo(beforeBlock + amount * 2));
                    Assert.That(b.GetHand(type).GetCurrentHandSize(), Is.EqualTo(3));
                    Assert.That(b.GetDeckController().GetDrawPile(type), Has.Count.EqualTo(7), "Only one replacement card is drawn.");
                    Assert.That(b.GetDeckController().GetExhaustPile(type).Count(c => c == card), Is.EqualTo(exhaust ? 1 : 0));
                    Assert.That(b.GetDeckController().GetDiscardPile(type).Count(c => c == card), Is.EqualTo(exhaust ? 0 : 1));
                }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ZeroXContinuousDiscardsOrExhaustsWithoutCreatingAnActiveSource()
    {
        yield return new EnterPlayMode(); yield return Load();
        BattleManager b = Object.FindFirstObjectByType<BattleManager>();
        for (int i = 0; i < 2; i++)
        {
            FillDraw(b.GetDeckController(), CardDeckType.TypeDream, 5);
            b.GetPlayer().GetCostSystem().ModifyDreamCost(-3);
            CardData card = Card(CardDeckType.TypeDream, Block(12)); card.type = CardType.Continuous;
            card.exhaustOnUse = i == 1;
            CardUI ui = b.GetHand(card.deckType).GetCardsSnapshot().First(c => c.CanMulligan); ui.SetCard(card, b);
            int before = b.GetPlayer().GetBlock();
            Assert.That(b.UseCard(ui), Is.True); yield return Idle(b);
            Assert.That(b.GetPlayer().GetBlock(), Is.EqualTo(before));
            Assert.That(b.GetPlayer().ContinuousCards.ActiveCards, Is.Empty);
            Assert.That(b.GetDeckController().GetDiscardPile(card.deckType).Count(c => c == card), Is.EqualTo(i == 0 ? 1 : 0));
            Assert.That(b.GetDeckController().GetExhaustPile(card.deckType).Count(c => c == card), Is.EqualTo(i == 1 ? 1 : 0));
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator XRepeatsWaitForEachMulliganAndPayCardAmmoOnlyOnce()
    {
        yield return new EnterPlayMode(); yield return Load();
        BattleManager b = Object.FindFirstObjectByType<BattleManager>();
        b.GetPlayer().GetCostSystem().ModifyDreamCost(-1); b.GetPlayer().GetAmmoSystem().Load(1);
        MulliganEffect swap = Asset<MulliganEffect>(); Set(swap, "handScope", MulliganHandScope.Nightmare);
        Set(swap, "countMode", MulliganCountMode.Fixed); Set(swap, "count", 1); Set(swap, "selectionMode", MulliganSelectionMode.Manual);
        CardData card = Card(CardDeckType.TypeDream, swap, Block(3)); card.requiredAmmo = 1; card.consumeRequiredAmmo = true;
        CardUI ui = b.GetHand(card.deckType).GetCardsSnapshot().First(c => c.CanMulligan); ui.SetCard(card, b);
        Assert.That(b.UseCard(ui), Is.True);
        for (int i = 0; i < 2; i++)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (!b.Mulligans.IsSelecting || b.GetPlayer().GetBlock() != i * 3)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            Assert.That(b.BlocksCardInteraction, Is.True);
            Assert.That(b.GetPlayer().GetCostSystem().GetDreamCost(), Is.Zero);
            Assert.That(b.GetPlayer().GetAmmoSystem().CurrentAmmo, Is.Zero);
            Assert.That(b.Mulligans.Toggle(b.GetHand(CardDeckType.TypeNightmare).GetCardsSnapshot().First(c => c.CanMulligan)), Is.True);
            Assert.That(b.Mulligans.Confirm(), Is.True);
        }
        yield return Idle(b);
        Assert.That(b.GetPlayer().GetBlock(), Is.EqualTo(6));
        Assert.That(b.Mulligans.IsSelecting, Is.False);
        Assert.That(b.GetHand(card.deckType).GetCurrentHandSize(), Is.EqualTo(3));
        yield return new ExitPlayMode();
    }

    private void FillDraw(DeckController deck, CardDeckType type, int count)
    {
        var draw = (List<CardData>)Get(deck, type == CardDeckType.TypeDream ? "dreamDrawPile" : "nightmareDrawPile");
        draw.Clear(); for (int i = 0; i < count; i++) draw.Add(Card(type));
    }
    private T Asset<T>() where T : ScriptableObject
    { T value = ScriptableObject.CreateInstance<T>(); (created ??= new List<Object>()).Add(value); return value; }
    private BlockEffect Block(int value) { BlockEffect e = Asset<BlockEffect>(); e.block = value; return e; }
    private CardData Card(CardDeckType type, params CardEffect[] effects)
    { CardData c = Asset<CardData>(); c.cardName = "X scene test"; c.type = CardType.Skill; c.deckType = type; c.costMode = CardCostMode.X; c.cost = new Cost(0, 0); c.effects = effects; return c; }
    private static IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("BattleScene");
        float deadline = Time.realtimeSinceStartup + 12;
        while (Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Count(c => c.CanMulligan) < 6 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Count(c => c.CanMulligan), Is.GreaterThanOrEqualTo(6));
    }
    private static IEnumerator Idle(BattleManager b)
    {
        float deadline = Time.realtimeSinceStartup + 15;
        while ((int)Get(b, "activeCardUses") > 0 || b.HandsAreMoving)
        { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
    }
    private static FieldInfo Field(object obj, string name)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        { FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f; }
        throw new ArgumentException(name);
    }
    private static object Get(object obj, string name) => Field(obj, name).GetValue(obj);
    private static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
}
