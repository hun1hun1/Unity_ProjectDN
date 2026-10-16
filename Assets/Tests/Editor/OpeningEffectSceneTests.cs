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

public class OpeningEffectSceneTests
{
    private List<Object> created = new List<Object>();
    [TearDown] public void Cleanup()
    {
        foreach (Object value in created ?? new List<Object>()) if (value != null) Object.DestroyImmediate(value);
        created?.Clear(); RunDeckInventory.ResetForNewGame();
        // PlayMode's last-frame modal guard must not block subsequent EditMode tests.
        typeof(MulliganSelectionUI).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
    }

    [UnityTest]
    public IEnumerator OpeningSnapshotSurvivesManualSelectionAndSettlesOnce()
    {
        yield return new EnterPlayMode(); yield return Load();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        MulliganEffect swap = Asset<MulliganEffect>(); Set(swap, "handScope", MulliganHandScope.Nightmare);
        Set(swap, "countMode", MulliganCountMode.Fixed); Set(swap, "count", 1);
        Set(swap, "selectionMode", MulliganSelectionMode.Manual);
        CardData card = Card(Block(2), Opening(swap, Block(5))); card.exhaustOnUse = true;
        card.description = "개전: 방어도 {block:1.1}. {openingCondition:1}";
        CardUI ui = ReadyCard(battle, card);
        string before = Description(ui);
        Assert.That(before, Does.Contain("개전 발동 가능"));
        Assert.That(battle.UseCard(ui), Is.True);
        Assert.That(battle.UseCard(ui), Is.False);
        float deadline = Time.realtimeSinceStartup + 15;
        while (!battle.Mulligans.IsSelecting)
        { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
        yield return null; yield return null;
        Assert.That(battle.BlocksCardInteraction, Is.True);
        Assert.That(battle.CurrentCardUse.IsOpening, Is.True);
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(2));
        Assert.That(Description(ui), Is.EqualTo(before));
        Assert.That(battle.Mulligans.Toggle(battle.GetHand(CardDeckType.TypeNightmare).GetCardsSnapshot().First(c => c.CanMulligan)), Is.True);
        Assert.That(battle.Mulligans.Confirm(), Is.True);
        yield return Idle(battle);
        Assert.That(battle.CurrentCardUse, Is.Null);
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(7));
        Assert.That(CountCard(battle.GetDeckController().GetExhaustPile(card.deckType), card), Is.EqualTo(1));
        Assert.That(battle.GetHand(card.deckType).GetCurrentHandSize(), Is.EqualTo(3));
        CardData second = Card(Block(1), Opening(Block(20))); second.deckType = CardDeckType.TypeNightmare;
        Assert.That(battle.UseCard(ReadyCard(battle, second)), Is.True);
        yield return Idle(battle);
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(8));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RealTurnTransitionRefreshesOpeningAndDisableClearsSuspendedContext()
    {
        yield return new EnterPlayMode(); yield return Load();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Set(battle.GetPlayer(), "currentHP", 1000); Set(battle.GetTarget(), "currentHP", 1000);
        CardData card = Card(Opening(Block(4))); card.description = "{openingCondition:0}";
        Assert.That(battle.UseCard(ReadyCard(battle, card)), Is.True);
        yield return Idle(battle);
        Assert.That(battle.OpeningAvailable, Is.False);
        battle.EndPlayerTurn();
        float deadline = Time.realtimeSinceStartup + 20;
        while (!battle.CanUseCard(card) || !battle.OpeningAvailable || battle.HandsAreMoving)
        { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
        CardUI next = ReadyCard(battle, card);
        Assert.That(Description(next), Does.Contain("개전 발동 가능"));
        Assert.That(battle.UseCard(next), Is.True);
        yield return Idle(battle);
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(4));

        // Suspend a normal second use: it must retain its false snapshot and clear it on disable.
        MulliganEffect swap = Asset<MulliganEffect>(); Set(swap, "handScope", MulliganHandScope.Nightmare);
        Set(swap, "countMode", MulliganCountMode.Fixed); Set(swap, "count", 1);
        Set(swap, "selectionMode", MulliganSelectionMode.Manual);
        Assert.That(battle.UseCard(ReadyCard(battle, Card(swap, Opening(Block(50))))), Is.True);
        deadline = Time.realtimeSinceStartup + 15;
        while (!battle.Mulligans.IsSelecting)
        { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
        Assert.That(battle.CurrentCardUse.IsOpening, Is.False);
        battle.enabled = false;
        Assert.That(battle.CurrentCardUse, Is.Null);
        Assert.That(battle.Mulligans.IsSelecting, Is.False);
        Assert.That(battle.GetPlayer().GetBlock(), Is.EqualTo(4));
        yield return new ExitPlayMode();
    }

    private T Asset<T>() where T : ScriptableObject
    { T value = ScriptableObject.CreateInstance<T>(); (created ??= new List<Object>()).Add(value); return value; }
    private BlockEffect Block(int amount) { BlockEffect effect = Asset<BlockEffect>(); effect.block = amount; return effect; }
    private OpeningEffect Opening(params CardEffect[] effects)
    { OpeningEffect effect = Asset<OpeningEffect>(); Set(effect, "effects", effects); return effect; }
    private CardData Card(params CardEffect[] effects)
    { CardData card = Asset<CardData>(); card.cardName = "Opening scene test"; card.type = CardType.Skill; card.cost = new Cost(0, 0); card.effects = effects; return card; }
    private static CardUI ReadyCard(BattleManager battle, CardData card)
    { CardUI ui = battle.GetHand(card.deckType).GetCardsSnapshot().First(c => c.CanMulligan); ui.SetCard(card, battle); return ui; }
    private static string Description(CardUI ui) => ((TMP_Text)Get(ui, "descriptionText")).text;
    private static int CountCard(IReadOnlyList<CardData> pile, CardData card) => pile.Count(c => c == card);
    private static IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("BattleScene");
        float deadline = Time.realtimeSinceStartup + 12;
        while (Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Count(c => c.CanMulligan) < 6 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Count(c => c.CanMulligan), Is.GreaterThanOrEqualTo(6));
    }
    private static IEnumerator Idle(BattleManager battle)
    {
        float deadline = Time.realtimeSinceStartup + 15;
        while ((int)Get(battle, "activeCardUses") > 0 || battle.HandsAreMoving)
        { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
    }
    private static FieldInfo Field(object obj, string name)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        { FieldInfo field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (field != null) return field; }
        throw new ArgumentException(name);
    }
    private static object Get(object obj, string name) => Field(obj, name).GetValue(obj);
    private static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
}
