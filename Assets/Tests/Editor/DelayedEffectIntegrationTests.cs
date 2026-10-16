using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public class DelayedEffectIntegrationTests
{
    private readonly List<Object> created = new List<Object>();
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear(); RunDeckInventory.ResetForNewGame();
    }

    [TestCase(CardDeckType.TypeDream, false)] [TestCase(CardDeckType.TypeNightmare, false)]
    [TestCase(CardDeckType.TypeDream, true)] [TestCase(CardDeckType.TypeNightmare, true)]
    public void ContinuousUsesLastDelayForLifetimeAndReturnsToCorrectDestination(CardDeckType type, bool exhaust)
    {
        BattleManager b = Battle();
        DelayEffect one = Delay(1, Block(2)), three = Delay(3, Block(5));
        CardData card = Card(type, one, three); card.type = CardType.Continuous;
        card.continuousDurationType = ContinuousDurationType.UntilBattleEnd; card.exhaustOnUse = exhaust;
        ActiveContinuousCard source = b.GetPlayer().ContinuousCards.Activate(card, b, b.GetDeckController());
        Assert.That(source.IsActive, Is.True);
        Assert.That(b.DelayedEffects.PendingEffects.Count, Is.EqualTo(2));
        Assert.That(b.GetDeckController().GetExhaustPile(type).Count, Is.EqualTo(exhaust ? 1 : 0));
        Advance(b); b.GetPlayer().ContinuousCards.ReduceRemainingTurns();
        Assert.That(source.IsActive, Is.True);
        Assert.That(b.GetPlayer().GetBlock(), Is.EqualTo(2));
        Advance(b); Advance(b);
        Assert.That(source.IsActive, Is.False);
        Assert.That(b.GetPlayer().GetBlock(), Is.EqualTo(7));
        Assert.That(b.GetDeckController().GetDiscardPile(type).Count, Is.EqualTo(exhaust ? 0 : 1));
        Assert.That(b.GetDeckController().GetExhaustPile(type).Count, Is.EqualTo(exhaust ? 1 : 0));
        Assert.That(b.GetDeckController().GetDrawPile(type), Is.Empty);
        Advance(b);
        Assert.That(b.GetPlayer().GetBlock(), Is.EqualTo(7));
    }

    [Test]
    public void DelayedContinuousStatusOutlivesTheSourceUsingItsOwnDuration()
    {
        BattleManager b = Battle();
        ApplyStatusEffect child = Status(StatusEffectType.Rust, 4);
        CardData card = Card(CardDeckType.TypeDream, Delay(1, child)); card.type = CardType.Continuous;
        card.continuousDurationType = ContinuousDurationType.FixedTurns; card.continuousTurns = 1;
        ActiveContinuousCard source = b.GetPlayer().ContinuousCards.Activate(card, b, b.GetDeckController());
        b.GetPlayer().ContinuousCards.ReduceRemainingTurns();
        Assert.That(source.IsActive, Is.True);
        Advance(b);
        Assert.That(source.IsActive, Is.False);
        Assert.That(b.GetPlayer().StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.EqualTo(4));
    }

    [Test]
    public void DuplicateContinuousUsesCannotReturnEachOthersCards()
    {
        BattleManager b = Battle(); CardData card = Card(CardDeckType.TypeDream, Delay(2, Block(1)));
        card.type = CardType.Continuous;
        ActiveContinuousCard first = b.GetPlayer().ContinuousCards.Activate(card, b, b.GetDeckController());
        Advance(b);
        ActiveContinuousCard second = b.GetPlayer().ContinuousCards.Activate(card, b, b.GetDeckController());
        Advance(b);
        Assert.That(first.IsActive, Is.False); Assert.That(second.IsActive, Is.True);
        Assert.That(b.GetDeckController().GetDiscardPile(card.deckType), Has.Count.EqualTo(1));
        Advance(b);
        Assert.That(second.IsActive, Is.False);
        Assert.That(b.GetDeckController().GetDiscardPile(card.deckType), Has.Count.EqualTo(2));
    }

    [Test]
    public void RecurringDelayDoesNotExtendTheContinuousSource()
    {
        BattleManager b = Battle(); CardData card = Card(CardDeckType.TypeDream);
        card.type = CardType.Continuous; card.continuousDurationType = ContinuousDurationType.FixedTurns; card.continuousTurns = 1;
        card.turnStartEffects = new CardEffect[] { Delay(1, Block(6)) };
        ActiveContinuousCard source = b.GetPlayer().ContinuousCards.Activate(card, b, b.GetDeckController());
        b.DelayedEffects.AdvanceTurn();
        Drain(b.GetPlayer().ContinuousCards.ProcessTurnStartEffectsRoutine(b));
        Drain(b.DelayedEffects.ProcessDueEffectsRoutine(b));
        Assert.That(source.IsActive, Is.False);
        Assert.That(b.GetPlayer().GetBlock(), Is.Zero);
        Advance(b);
        Assert.That(b.GetPlayer().GetBlock(), Is.EqualTo(6));
    }

    [Test]
    public void RegistrationSnapshotsTheChildArrayAndKeepsCardOrigin()
    {
        BattleManager b = Battle(); DelayEffect delay = Delay(1, Block(4));
        CardData card = Card(CardDeckType.TypeDream, delay);
        Drain(b.ExecuteEffectsRoutine(card.effects, sourceCard: card));
        Assert.That(b.DelayedEffects.PendingEffects[0].Card, Is.SameAs(card));
        delay.Effects[0] = Block(99);
        Advance(b);
        Assert.That(b.GetPlayer().GetBlock(), Is.EqualTo(4));
        Assert.That(b.CurrentEffectCard, Is.Null);
    }

    [Test]
    public void ConditionalDelayKeepsOriginAndEvaluatesChildAmmoAtDueTime()
    {
        BattleManager b = Battle(); AmmoSystem ammo = b.GetPlayer().GetAmmoSystem(); ammo.Load(2);
        SpendAmmoBonusEffect outer = Asset<SpendAmmoBonusEffect>(); Set(outer, "ammoCost", 1);
        SpendAmmoBonusEffect child = Asset<SpendAmmoBonusEffect>(); Set(child, "ammoCost", 1);
        Set(child, "bonusEffects", new CardEffect[] { Block(8) });
        Set(outer, "bonusEffects", new CardEffect[] { Delay(1, child) });
        CardData card = Card(CardDeckType.TypeDream, outer);
        Drain(b.ExecuteEffectsRoutine(card.effects, sourceCard: card));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(1));
        Assert.That(b.DelayedEffects.PendingEffects[0].Card, Is.SameAs(card));
        Advance(b);
        Assert.That(ammo.CurrentAmmo, Is.Zero); Assert.That(b.GetPlayer().GetBlock(), Is.EqualTo(8));
    }

    [Test]
    public void FutureStatusesAndAmmoCannotContaminateImmediatePreview()
    {
        BattleManager b = Battle(); DamageEffect damage = Asset<DamageEffect>(); damage.damage = 20;
        Set(damage, "target", CardTarget.Enemy);
        CardData card = Card(CardDeckType.TypeDream, Delay(1, Status(StatusEffectType.Rust, 3)), damage);
        card.description = "{damage:1}";
        Assert.That(CardDescriptionBuilder.Build(card, b), Is.EqualTo("20"));
        Assert.That(b.DelayedEffects.PendingEffects, Is.Empty);
        Assert.That(b.GetPlayer().StatusEffects.ActiveEffects, Is.Empty);
    }

    [Test]
    public void CurrentStatusesAreEvaluatedWhenTheReservationFires()
    {
        BattleManager b = Battle(); DamageEffect damage = Asset<DamageEffect>(); damage.damage = 20; Set(damage, "target", CardTarget.Enemy);
        Delay(1, damage).Execute(b);
        Status(StatusEffectType.Rust, 2).Execute(b);
        int hp = b.GetTarget().GetCurrentHP(); Advance(b);
        Assert.That(b.GetTarget().GetCurrentHP(), Is.EqualTo(hp - 15));
    }

    [Test]
    public void EndingBattleCancelsWithoutExecutingOrReturningCards()
    {
        BattleManager b = Battle(); CardData card = Card(CardDeckType.TypeDream, Delay(1, Block(5)));
        card.type = CardType.Continuous;
        b.GetPlayer().ContinuousCards.Activate(card, b, b.GetDeckController());
        MethodInfo end = typeof(BattleManager).GetMethod("EndBattle");
        Assert.That(end, Is.Not.Null, "Battle completion must cancel reservations.");
        end.Invoke(b, null); Advance(b);
        Assert.That(b.DelayedEffects.PendingEffects, Is.Empty);
        Assert.That(b.GetPlayer().GetBlock(), Is.Zero);
        Assert.That(b.GetDeckController().GetDiscardPile(card.deckType), Is.Empty);
        Assert.That(b.GetDeckController().GetDrawPile(card.deckType), Is.Empty);
        Assert.That(b.CanContinueEffects, Is.False);
    }

    [Test]
    public void HudShowsIndependentCountdownsAndRefreshesOpenTooltip()
    {
        BattleManager b = Battle();
        GameObject canvas = Track(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
        GameObject info = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterInfoUI.prefab"), canvas.transform));
        StatusEffectsUI hud = info.GetComponentInChildren<StatusEffectsUI>(true);
        MethodInfo bind = typeof(StatusEffectsUI).GetMethod("Bind");
        Assert.That(bind.GetParameters().Length, Is.EqualTo(3), "HUD must bind the battle's delayed reservations.");
        bind.Invoke(hud, new object[] { b.GetPlayer().StatusEffects, b.GetPlayer().ContinuousCards, b.DelayedEffects });
        DelayEffect delay = Delay(2, Block(7)); Set(delay, "description", "방어도 {block:0} 획득");
        CardData card = Card(CardDeckType.TypeDream, delay); card.cardName = "지연 방어";
        Drain(b.ExecuteEffectsRoutine(card.effects, sourceCard: card));
        Drain(b.ExecuteEffectsRoutine(card.effects, sourceCard: card));
        StatusIconUI[] icons = hud.GetComponentsInChildren<StatusIconUI>();
        Assert.That(icons, Has.Length.EqualTo(2));
        icons[0].OnPointerEnter(new PointerEventData(null) { position = Vector2.zero });
        StatusTooltipUI tooltip = canvas.GetComponentInChildren<StatusTooltipUI>(true);
        string text = tooltip.GetComponentInChildren<TMP_Text>(true).text;
        Assert.That(text, Does.Contain("지연 방어").And.Contain("2턴 뒤 발동").And.Contain("방어도 7 획득"));
        Advance(b);
        Assert.That(tooltip.GetComponentInChildren<TMP_Text>(true).text, Does.Contain("1턴 뒤 발동"));
        Advance(b);
        Assert.That(tooltip.gameObject.activeSelf, Is.False);
        Assert.That(hud.GetComponentsInChildren<StatusIconUI>(), Is.Empty);
    }

    [Test]
    public void DisablingHudComponentRemovesIconsAndRebindingUsesTheNewQueue()
    {
        BattleManager b = Battle();
        GameObject canvas = Track(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
        GameObject info = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterInfoUI.prefab"), canvas.transform));
        StatusEffectsUI hud = info.GetComponentInChildren<StatusEffectsUI>(true);
        hud.Bind(b.GetPlayer().StatusEffects, b.GetPlayer().ContinuousCards, b.DelayedEffects);
        Delay(2, Block(1)).Execute(b);
        Assert.That(hud.GetComponentsInChildren<StatusIconUI>(), Has.Length.EqualTo(1));
        hud.enabled = false; Lifecycle(hud, "OnDisable"); // EditMode objects do not receive play-mode lifecycle callbacks.
        Assert.That(hud.GetComponentsInChildren<StatusIconUI>(), Is.Empty);
        Advance(b);
        hud.enabled = true; Lifecycle(hud, "OnEnable");
        Assert.That(hud.GetComponentsInChildren<StatusIconUI>(), Has.Length.EqualTo(1));
        Assert.That(hud.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("1"));
        var next = new DelayedEffectSystem();
        next.Schedule(Delay(3, Block(2)), null);
        hud.Bind(b.GetPlayer().StatusEffects, b.GetPlayer().ContinuousCards, next);
        Assert.That(hud.GetComponentsInChildren<StatusIconUI>(), Has.Length.EqualTo(1));
        Assert.That(hud.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("3"));
    }

    [Test]
    public void CyclicDelayGraphsAreRejectedBeforeScheduling()
    {
        BattleManager b = Battle(); DelayEffect first = Delay(1, Block(7));
        Set(first, "effects", new CardEffect[] { first, Block(7) });
        Assert.That(first.IsValid, Is.False, "Self references would repeatedly schedule the same effect.");
        first.Execute(b); Assert.That(b.DelayedEffects.PendingEffects, Is.Empty);
        DelayEffect second = Delay(2, first);
        Set(first, "effects", new CardEffect[] { second, Block(7) });
        Assert.That(first.IsValid, Is.False); Assert.That(second.IsValid, Is.False);
        SpendAmmoBonusEffect conditional = Asset<SpendAmmoBonusEffect>();
        Set(conditional, "bonusEffects", new CardEffect[] { first });
        Set(first, "effects", new CardEffect[] { conditional });
        Assert.That(first.IsValid, Is.False);
    }

    [Test]
    public void DelayedContinuousTooltipDescribesDiscardAndKeepsRecurringPresentation()
    {
        BattleManager b = Battle();
        GameObject canvas = Track(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
        GameObject info = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterInfoUI.prefab"), canvas.transform));
        StatusEffectsUI hud = info.GetComponentInChildren<StatusEffectsUI>(true);
        hud.Bind(b.GetPlayer().StatusEffects, b.GetPlayer().ContinuousCards, b.DelayedEffects);
        CardData card = Card(CardDeckType.TypeDream, Status(StatusEffectType.Rust, 1), Delay(2, Block(1)));
        card.type = CardType.Continuous; card.description = "매 턴 방어도 획득";
        ActiveContinuousCard source = b.GetPlayer().ContinuousCards.Activate(card, b, b.GetDeckController());
        StatusIconUI icon = hud.GetComponentsInChildren<StatusIconUI>().Single(i => i.ContinuousCardId == source.Id);
        icon.OnPointerEnter(new PointerEventData(null));
        Assert.That(canvas.GetComponentInChildren<StatusTooltipUI>(true).GetComponentInChildren<TMP_Text>(true).text,
            Does.Contain("버림").And.Not.Contain("뽑을"));
        b.GetPlayer().ContinuousCards.ResetForBattle(); b.DelayedEffects.Clear(); b.GetPlayer().StatusEffects.Clear();
        card.effects = new CardEffect[] { Delay(2, Block(1)) }; card.turnStartEffects = new CardEffect[] { Block(3) };
        b.GetPlayer().ContinuousCards.Activate(card, b, b.GetDeckController());
        Assert.That(hud.GetComponentsInChildren<StatusIconUI>(), Has.Length.EqualTo(2), "Recurring effects still need their continuous icon.");
    }

    [Test]
    public void FailedConditionalDelayKeepsPermanentAbsorptionAndDisabledBattleCancelsTheQueue()
    {
        BattleManager b = Battle(); SpendAmmoBonusEffect bonus = Asset<SpendAmmoBonusEffect>();
        Set(bonus, "ammoCost", 1); Set(bonus, "bonusEffects", new CardEffect[] { Delay(2, Block(2)) });
        CardData card = Card(CardDeckType.TypeDream, bonus); card.type = CardType.Continuous;
        card.continuousDurationType = ContinuousDurationType.UntilBattleEnd; card.exhaustOnUse = true;
        ActiveContinuousCard source = b.GetPlayer().ContinuousCards.Activate(card, b, b.GetDeckController());
        Assert.That(source.IsPermanent, Is.True); Assert.That(source.IsAbsorbed, Is.True);
        Assert.That(b.GetDeckController().GetExhaustPile(card.deckType), Is.Empty);
        Delay(1, Block(1)).Execute(b); b.enabled = false; Lifecycle(b, "OnDisable"); b.enabled = true; Lifecycle(b, "OnEnable");
        Assert.That(b.DelayedEffects.PendingEffects, Is.Empty);
        Assert.That(source.IsActive, Is.False);
        Advance(b); Assert.That(b.GetPlayer().GetBlock(), Is.Zero);
    }

    [Test]
    public void ReservationsAtTheSameTurnExecuteInRegistrationOrder()
    {
        BattleManager b = Battle(); DamageEffect damage = Asset<DamageEffect>(); damage.damage = 10;
        Set(damage, "target", CardTarget.Player);
        Delay(1, damage).Execute(b); Delay(1, Block(10)).Execute(b);
        Advance(b);
        Assert.That(b.GetPlayer().GetCurrentHP(), Is.EqualTo(70));
        Assert.That(b.GetPlayer().GetBlock(), Is.EqualTo(10));
    }

    [Test]
    public void LateCharacterInitializationCannotDisconnectTheReservationHud()
    {
        BattleManager b = Battle();
        GameObject canvas = Track(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
        GameObject info = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterInfoUI.prefab"), canvas.transform));
        CharacterInfoUI characterInfo = info.GetComponent<CharacterInfoUI>();
        Set(b.GetPlayer(), "characterInfoUI", characterInfo);
        Lifecycle(b, "BindBattleCharacters");
        // Simulate the character Awake running after the manager OnEnable.
        characterInfo.BindStatusEffects(b.GetPlayer().StatusEffects, b.GetPlayer().ContinuousCards);
        Lifecycle(b, "BindBattleCharacters");
        Delay(2, Block(1)).Execute(b);
        Assert.That(info.GetComponentsInChildren<StatusIconUI>(), Has.Length.EqualTo(1));
    }

    [Test]
    public void EmptyDescriptionProvidesEffectSummaryAndCyclicMetadataTerminates()
    {
        BattleManager b = Battle(); DelayEffect delay = Delay(1, Block(7));
        Type builder = typeof(CardEffect).Assembly.GetType("DelayedEffectDescriptionBuilder");
        Assert.That(builder, Is.Not.Null);
        string text = (string)builder.GetMethod("Build", new[] { typeof(DelayEffect) }).Invoke(null, new object[] { delay });
        Assert.That(text, Does.Contain("7").And.Contain("방어"));
        SpendAmmoBonusEffect cycle = Asset<SpendAmmoBonusEffect>();
        Set(cycle, "bonusEffects", new CardEffect[] { delay });
        Set(delay, "effects", new CardEffect[] { cycle, Block(1) });
        CardData card = Card(CardDeckType.TypeDream, delay);
        Assert.DoesNotThrow(() => CardStatusCollector.Collect(card));
        Assert.DoesNotThrow(() => builder.GetMethod("Build", new[] { typeof(DelayEffect) }).Invoke(null, new object[] { delay }));
    }

    private BattleManager Battle()
    {
        GameObject root = Track(new GameObject("Delay player")); Player player = root.AddComponent<Player>();
        Set(player, "currentHP", 80); root.AddComponent<CostSystem>(); root.AddComponent<AmmoSystem>();
        DeckController deck = root.AddComponent<DeckController>();
        Enemy enemy = Track(new GameObject("Delay enemy")).AddComponent<Enemy>(); Set(enemy, "currentHP", 80);
        BattleManager battle = Track(new GameObject("Delay battle")).AddComponent<BattleManager>();
        Set(battle, "player", player); Set(battle, "target", enemy); Set(battle, "deckController", deck);
        return battle;
    }
    private GameObject Track(GameObject value) { created.Add(value); return value; }
    private T Asset<T>() where T : ScriptableObject { T value = ScriptableObject.CreateInstance<T>(); created.Add(value); return value; }
    private BlockEffect Block(int value) { BlockEffect e = Asset<BlockEffect>(); e.block = value; return e; }
    private DelayEffect Delay(int turns, params CardEffect[] effects)
    { DelayEffect e = Asset<DelayEffect>(); Set(e, "delayTurns", turns); Set(e, "effects", effects); return e; }
    private CardData Card(CardDeckType deck, params CardEffect[] effects)
    { CardData card = Asset<CardData>(); card.cardName = "Delay card"; card.deckType = deck; card.type = CardType.Skill; card.effects = effects; card.cost = new Cost(0, 0); return card; }
    private ApplyStatusEffect Status(StatusEffectType type, int turns)
    { StatusEffectData data = Asset<StatusEffectData>(); Set(data, "statusType", type); ApplyStatusEffect e = Asset<ApplyStatusEffect>(); Set(e, "status", data); Set(e, "turns", turns); Set(e, "target", CardTarget.Player); return e; }
    private static void Lifecycle(object value, string name) => value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, null);
    private static void Advance(BattleManager b) { b.DelayedEffects.AdvanceTurn(); Drain(b.DelayedEffects.ProcessDueEffectsRoutine(b)); }
    private static void Drain(IEnumerator routine) { while (routine.MoveNext()) if (routine.Current is IEnumerator nested) Drain(nested); }
    private static void Set(object obj, string name, object value)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        { FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) { f.SetValue(obj, value); return; } }
        throw new ArgumentException(name);
    }
}
