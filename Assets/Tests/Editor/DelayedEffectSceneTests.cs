using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class DelayedEffectSceneTests
{
    private List<Object> created = new List<Object>();
    [TearDown] public void Cleanup()
    {
        foreach (Object value in created ?? new List<Object>()) if (value != null) Object.DestroyImmediate(value);
        created?.Clear(); RunDeckInventory.ResetForNewGame();
    }

    [UnityTest]
    public IEnumerator AttackAndExhaustedSkillUsePayOnceRefillAndFireAtExactlyTwoTurns()
    {
        yield return new EnterPlayMode();
        yield return Load();
        BattleManager b = Object.FindFirstObjectByType<BattleManager>();
        Set(b.GetPlayer(), "currentHP", 1000); Set(b.GetTarget(), "currentHP", 1000);
        for (int i = 0; i < 2; i++)
        {
            CardData card = Card(2, Block(9)); card.type = i == 0 ? CardType.Attack : CardType.Skill;
            card.exhaustOnUse = i == 1; card.requiredAmmo = 1; card.consumeRequiredAmmo = true;
            b.GetPlayer().GetAmmoSystem().Load(1);
            CardUI ui = b.GetHand(CardDeckType.TypeDream).GetCardsSnapshot().First(c => c.CanMulligan);
            ui.SetCard(card, b);
            Assert.That(b.UseCard(ui), Is.True);
            Assert.That(b.UseCard(ui), Is.False);
            yield return Idle(b);
            Assert.That(b.GetHand(card.deckType).GetCurrentHandSize(), Is.EqualTo(3));
            Assert.That(b.GetPlayer().GetAmmoSystem().CurrentAmmo, Is.Zero);
            Assert.That(b.DelayedEffects.PendingEffects, Has.Count.EqualTo(1));
            if (card.exhaustOnUse) Assert.That(b.GetDeckController().GetExhaustPile(card.deckType), Has.Member(card));
            yield return Turn(b);
            Assert.That(b.GetPlayer().GetBlock(), Is.Zero);
            Assert.That(b.DelayedEffects.PendingEffects[0].RemainingTurns, Is.EqualTo(1));
            yield return Turn(b);
            Assert.That(b.GetPlayer().GetBlock(), Is.EqualTo(9));
            Assert.That(b.DelayedEffects.PendingEffects, Is.Empty);
            Assert.That(b.GetPlayer().GetAmmoSystem().CurrentAmmo, Is.Zero);
            yield return Turn(b);
            Assert.That(b.GetPlayer().GetBlock(), Is.Zero);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DeathDuringDelayCancelsLaterEffectsAndNeverRestartsPlayerTurn()
    {
        yield return new EnterPlayMode(); yield return Load();
        BattleManager b = Object.FindFirstObjectByType<BattleManager>();
        DamageEffect kill = Asset<DamageEffect>(); kill.damage = 10000; Set(kill, "target", CardTarget.Enemy);
        CardData card = Card(1, kill, Block(7));
        b.RunEffectRoutine(b.ExecuteEffectsRoutine(card.effects, sourceCard: card));
        CardData later = Card(2, Block(20)); b.RunEffectRoutine(b.ExecuteEffectsRoutine(later.effects, sourceCard: later));
        b.EndPlayerTurn();
        float deadline = Time.realtimeSinceStartup + 12;
        while (b.CanContinueEffects && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(b.CanContinueEffects, Is.False);
        Assert.That(b.DelayedEffects.PendingEffects, Is.Empty);
        Assert.That(b.GetPlayer().GetBlock(), Is.Zero);
        yield return null; Assert.That(b.CanContinueEffects, Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DelayedMulliganWaitsForInputAndEndingBattleCancelsTheRemainingEffect()
    {
        yield return new EnterPlayMode(); yield return Load();
        BattleManager b = Object.FindFirstObjectByType<BattleManager>();
        MulliganEffect mulligan = Asset<MulliganEffect>();
        Set(mulligan, "handScope", MulliganHandScope.Both); Set(mulligan, "countMode", MulliganCountMode.Fixed);
        Set(mulligan, "count", 1); Set(mulligan, "selectionMode", MulliganSelectionMode.Manual);
        CardData card = Card(1, mulligan, Block(11));
        b.RunEffectRoutine(b.ExecuteEffectsRoutine(card.effects, sourceCard: card));
        b.EndPlayerTurn();
        float deadline = Time.realtimeSinceStartup + 12;
        while (b.DelayedEffects.CurrentTurn == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        // Turn-start occurs before draw: with empty hands no selection should open.
        Assert.That(b.GetPlayer().GetBlock(), Is.EqualTo(11));
        Assert.That(b.DelayedEffects.PendingEffects, Is.Empty);
        yield return Idle(b);
        b.RunEffectRoutine(b.ExecuteEffectsRoutine(card.effects, sourceCard: card));
        b.DelayedEffects.AdvanceTurn();
        b.RunEffectRoutine(b.DelayedEffects.ProcessDueEffectsRoutine(b));
        deadline = Time.realtimeSinceStartup + 12;
        while (!b.Mulligans.IsSelecting && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(b.Mulligans.IsSelecting, Is.True);
        Assert.That(b.BlocksCardInteraction, Is.True);
        int before = b.GetPlayer().GetBlock();
        b.EndBattle(); yield return null;
        Assert.That(b.Mulligans.IsSelecting, Is.False);
        Assert.That(b.GetPlayer().GetBlock(), Is.EqualTo(before));
        Assert.That(b.DelayedEffects.PendingEffects, Is.Empty);
        Assert.That(Object.FindObjectsByType<MulliganSelectionUI>(FindObjectsSortMode.None).All(v => !v.gameObject.activeInHierarchy), Is.True);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator EndingBattleDuringDiscardCannotRunTheEnemyOrRestartTheTurn()
    {
        yield return new EnterPlayMode(); yield return Load();
        BattleManager b = Object.FindFirstObjectByType<BattleManager>();
        int hp = b.GetPlayer().GetCurrentHP();
        b.EndPlayerTurn();
        b.EndBattle();
        float deadline = Time.realtimeSinceStartup + 10;
        while (!b.CanContinueEffects && (b.GetHand(CardDeckType.TypeDream).GetCurrentHandSize() > 0 || b.GetHand(CardDeckType.TypeNightmare).GetCurrentHandSize() > 0))
        { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
        yield return null; yield return null;
        Assert.That(b.CanContinueEffects, Is.False, "Discard completion must not leave BattleEnd.");
        Assert.That(b.GetPlayer().GetCurrentHP(), Is.EqualTo(hp));
        Assert.That(b.DelayedEffects.CurrentTurn, Is.Zero);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ReenabledBattleStillObservesCharacterDeath()
    {
        yield return new EnterPlayMode(); yield return Load();
        BattleManager b = Object.FindFirstObjectByType<BattleManager>();
        Set(b.GetPlayer(), "currentHP", 1);
        b.enabled = false; b.enabled = true;
        b.EndPlayerTurn();
        float deadline = Time.realtimeSinceStartup + 8;
        while (b.GetPlayer().GetCurrentHP() > 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(b.GetPlayer().GetCurrentHP(), Is.Zero);
        Assert.That(b.CanContinueEffects, Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DisablingTheLiveHudRemovesIconsAndRestoresTheirCurrentCountdown()
    {
        yield return new EnterPlayMode(); yield return Load();
        BattleManager b = Object.FindFirstObjectByType<BattleManager>();
        CardData card = Card(2, Block(5)); b.RunEffectRoutine(b.ExecuteEffectsRoutine(card.effects, sourceCard: card));
        StatusEffectsUI hud = FindReservationHud(b);
        Assert.That(hud.GetComponentsInChildren<StatusIconUI>(), Has.Length.EqualTo(1));
        hud.enabled = false;
        Assert.That(hud.GetComponentsInChildren<StatusIconUI>(), Is.Empty);
        b.DelayedEffects.AdvanceTurn();
        hud.enabled = true;
        Assert.That(hud.GetComponentsInChildren<StatusIconUI>(), Has.Length.EqualTo(1));
        Assert.That(hud.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("1"));
        b.EndBattle();
        Assert.That(hud.GetComponentsInChildren<StatusIconUI>(), Is.Empty);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator EnemyLethalAttackEndsBattleAndClearsTheQueue()
    {
        yield return new EnterPlayMode(); yield return Load();
        BattleManager b = Object.FindFirstObjectByType<BattleManager>();
        CardData card = Card(2, Block(5)); b.RunEffectRoutine(b.ExecuteEffectsRoutine(card.effects, sourceCard: card));
        Set(b.GetPlayer(), "currentHP", 1);
        b.EndPlayerTurn();
        float deadline = Time.realtimeSinceStartup + 12;
        while (b.CanContinueEffects && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(b.CanContinueEffects, Is.False);
        Assert.That(b.DelayedEffects.PendingEffects, Is.Empty);
        yield return null; Assert.That(b.CanContinueEffects, Is.False);
        yield return new ExitPlayMode();
    }

    private static StatusEffectsUI FindReservationHud(BattleManager battle) => Object.FindObjectsByType<StatusEffectsUI>(FindObjectsSortMode.None)
        .Single(h => ReferenceEquals(Get(h, "delayed"), battle.DelayedEffects));

    private static IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("BattleScene");
        float deadline = Time.realtimeSinceStartup + 12;
        while (Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Count(c => c.CanMulligan) < 6 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None).Count(c => c.CanMulligan), Is.GreaterThanOrEqualTo(6));
    }
    private static IEnumerator Idle(BattleManager b)
    {
        float deadline = Time.realtimeSinceStartup + 12;
        while ((int)Get(b, "activeCardUses") > 0 || b.HandsAreMoving)
        { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
    }
    private static IEnumerator Turn(BattleManager b)
    {
        long turn = b.DelayedEffects.CurrentTurn; b.EndPlayerTurn();
        float deadline = Time.realtimeSinceStartup + 12;
        while (b.DelayedEffects.CurrentTurn == turn || b.HandsAreMoving || b.BlocksCardInteraction)
        { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
    }
    private T Asset<T>() where T : ScriptableObject
    { T value = ScriptableObject.CreateInstance<T>(); (created ??= new List<Object>()).Add(value); return value; }
    private BlockEffect Block(int value) { BlockEffect e = Asset<BlockEffect>(); e.block = value; return e; }
    private CardData Card(int turns, params CardEffect[] effects)
    {
        DelayEffect delay = Asset<DelayEffect>(); Set(delay, "delayTurns", turns); Set(delay, "effects", effects);
        CardData card = Asset<CardData>(); card.cardName = "Delay scene test"; card.deckType = CardDeckType.TypeDream;
        card.type = CardType.Skill; card.cost = new Cost(0, 0); card.effects = new CardEffect[] { delay }; return card;
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
