using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class SpendAllAmmoTests
{
    private readonly List<Object> created = new List<Object>();
    private BattleManager battle;
    private Player player;
    private Enemy enemy;
    private AmmoSystem ammo;

    [SetUp] public void Setup()
    {
        GameObject root = Track(new GameObject("Spend all player"));
        player = root.AddComponent<Player>(); Set(player, "currentHP", 80);
        root.AddComponent<CostSystem>(); ammo = root.AddComponent<AmmoSystem>();
        DeckController deck = root.AddComponent<DeckController>();
        enemy = Track(new GameObject("Spend all enemy")).AddComponent<Enemy>(); Set(enemy, "currentHP", 80);
        battle = Track(new GameObject("Spend all battle")).AddComponent<BattleManager>();
        Set(battle, "player", player); Set(battle, "target", enemy); Set(battle, "deckController", deck);
    }

    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear(); RunDeckInventory.ResetForNewGame();
    }

    // These fail if spending is fixed, skipped, repeated, or confused with capacity.
    [TestCase(1, 78)] [TestCase(4, 72)] [TestCase(10, 60)]
    public void AllAmmoIsSpentOnceAndDamageUsesSpentCount(int count, int remainingHP)
    {
        ammo.Load(count); Run(All(Damage(2, true), Block(7)));
        Assert.That(ammo.CurrentAmmo, Is.Zero);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(remainingHP));
        Assert.That(player.GetBlock(), Is.EqualTo(7));
    }

    [Test] public void ZeroAmmoSkipsEvenFixedChildren()
    { Run(All(Block(12))); Assert.That(player.GetBlock(), Is.Zero); }

    [Test] public void DestroyedPlayerReferenceDoesNotExecuteOrThrow()
    {
        Object.DestroyImmediate(player.gameObject);
        Assert.DoesNotThrow(() => Run(All(Block(12))));
    }

    // Inclusive comparison and the captured cost are independent of later reloads.
    [TestCase(3, 0)] [TestCase(4, 11)] [TestCase(5, 11)]
    public void ThresholdIsInclusiveAndUsesSpentCountRatherThanCurrentAmmo(int count, int expected)
    {
        ammo.Load(count); Run(All(Reload(1), Threshold(4, Block(11))));
        Assert.That(player.GetBlock(), Is.EqualTo(expected)); Assert.That(ammo.CurrentAmmo, Is.EqualTo(1));
    }

    [Test] public void NestedSpendsRestoreOuterCountAndDoNotLeakIntoSiblingEffects()
    {
        ammo.Load(4);
        Run(All(Reload(2), All(Block(3, true)), Block(2, true)), Threshold(1, Block(50)), Block(9, true));
        Assert.That(player.GetBlock(), Is.EqualTo(14)); Assert.That(ammo.CurrentAmmo, Is.Zero);
    }

    [Test] public void SeparateUsesOfSharedAssetsCaptureIndependentCounts()
    {
        CardEffect effect = All(Block(2, true)); ammo.Load(2); Run(effect); ammo.Load(5); Run(effect);
        Assert.That(player.GetBlock(), Is.EqualTo(14));
    }

    [Test] public void DelayedThresholdAndScalingKeepTheOriginalSpentCountAcrossNestedReservations()
    {
        ammo.Load(4); Run(All(Delay(Delay(Threshold(4, Block(3, true))))));
        ammo.Load(1); Advance(); Assert.That(player.GetBlock(), Is.Zero);
        ammo.Load(2); Advance();
        Assert.That(player.GetBlock(), Is.EqualTo(12)); Assert.That(ammo.CurrentAmmo, Is.EqualTo(3));
        Run(Threshold(1, Block(100))); Assert.That(player.GetBlock(), Is.EqualTo(12));
    }

    [Test] public void DelayBeforeSpendConsumesAmmoAtActivationTime()
    {
        ammo.Load(1); Run(Delay(All(Block(2, true)))); ammo.Load(3); Advance();
        Assert.That(player.GetBlock(), Is.EqualTo(8)); Assert.That(ammo.CurrentAmmo, Is.Zero);
    }

    [Test] public void PreviewScalesThenAppliesStatusesWithoutSpendingLiveAmmo()
    {
        ammo.Load(4);
        StatusEffectData weak = Asset<StatusEffectData>(); Set(weak, "statusType", StatusEffectType.WeaknessExposure);
        enemy.StatusEffects.Apply(weak, 2);
        CardData card = Card(All(Damage(5, true), Threshold(4, Block(3, true))));
        card.description = "{damage:0.0}|{block:0.1.0}|{spentAmmo:0}|{spentAmmoCondition:0.1}";
        string preview = CardDescriptionBuilder.Build(card, battle);
        Assert.That(preview, Does.Contain("30")); Assert.That(preview, Does.Contain("12"));
        Assert.That(preview, Does.Contain("|4|")); Assert.That(preview, Does.Not.Contain("{spentAmmo"));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(4));
        string original = CardDescriptionBuilder.Build(card, battle, false);
        Assert.That(original, Does.Contain("5 × N")); Assert.That(original, Does.Not.Contain("30"));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(4));
    }

    [Test] public void FailedThresholdPreviewCannotLeakStatusesOrReloadsIntoLaterEffects()
    {
        ammo.Load(2);
        StatusEffectData weak = Asset<StatusEffectData>(); Set(weak, "statusType", StatusEffectType.WeaknessExposure);
        ApplyStatusEffect apply = Asset<ApplyStatusEffect>(); Set(apply, "status", weak); Set(apply, "target", CardTarget.Enemy);
        SpendAmmoBonusEffect bonus = Asset<SpendAmmoBonusEffect>(); Set(bonus, "ammoCost", 1);
        Set(bonus, "bonusEffects", new CardEffect[] { apply });
        CardData card = Card(All(Threshold(3, apply, Reload(5))), bonus, Damage(10));
        card.description = "{damage:2}";
        Assert.That(CardDescriptionBuilder.Build(card, battle), Is.EqualTo("10"));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(2)); Assert.That(enemy.StatusEffects.ActiveEffects, Is.Empty);
    }

    [Test] public void EarlierReloadAndCardAmmoPaymentAreIncludedInPreviewAndActualExecution()
    {
        ammo.Load(3); CardData card = Card(Reload(2), All(Block(4, true)));
        card.requiredAmmo = 1; card.consumeRequiredAmmo = true; card.description = "{block:1.0}";
        Assert.That(CardDescriptionBuilder.Build(card, battle), Is.EqualTo("16"));
        CardUI ui = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Card.prefab"))).GetComponent<CardUI>();
        ui.SetCard(card, battle); Assert.That(battle.UseCard(ui), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(16)); Assert.That(ammo.CurrentAmmo, Is.Zero);
    }

    [Test] public void EndingBattleStopsChildrenAndClearsTheCapturedCount()
    {
        ammo.Load(2); Run(All(Damage(80), Block(100)));
        Assert.That(player.GetBlock(), Is.Zero); Assert.That(battle.CanContinueEffects, Is.False);
        Assert.That(typeof(BattleManager).GetProperty("CurrentSpentAmmo").GetValue(battle), Is.EqualTo(0));
    }

    [Test] public void CyclesAreRejectedBeforeSpendingAndStatusMetadataTraversesThresholdChildren()
    {
        CardEffect all = All(); Set(all, "effects", new[] { all }); ammo.Load(3); Run(all);
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(3));
        Assert.That(CardStatusCollector.Collect(Card(all)), Is.Empty);
        StatusEffectData weak = Asset<StatusEffectData>(); Set(weak, "statusType", StatusEffectType.WeaknessExposure);
        ApplyStatusEffect apply = Asset<ApplyStatusEffect>(); Set(apply, "status", weak);
        Set(all, "effects", new[] { Threshold(2, apply) });
        Assert.That(CardStatusCollector.Collect(Card(all)), Has.Member(weak));
    }

    [Test] public void ScaledDamageIsOneHitRatherThanPerBulletRoundedHits()
    {
        ammo.Load(3); StatusEffectData rust = Asset<StatusEffectData>(); Set(rust, "statusType", StatusEffectType.Rust);
        player.StatusEffects.Apply(rust, 2); Run(All(Damage(1, true)));
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(78));
    }

    [Test] public void UnmetParentThresholdSuppressesNestedThresholdPreview()
    {
        ammo.Load(4); CardData card = Card(All(Threshold(5, Threshold(2, Block(8)))));
        card.description = "{spentAmmoCondition:0.0}|{spentAmmoCondition:0.0.0}";
        Assert.That(CardDescriptionBuilder.Build(card, battle), Does.Not.Contain("#81C784"));
    }

    [Test] public void RejectedCyclicSpendMustNotDrainPreviewAmmoNeededByLaterEffects()
    {
        ammo.Load(3); CardEffect all = All(); Set(all, "effects", new[] { all });
        StatusEffectData weak = Asset<StatusEffectData>(); Set(weak, "statusType", StatusEffectType.WeaknessExposure);
        ApplyStatusEffect apply = Asset<ApplyStatusEffect>(); Set(apply, "status", weak); Set(apply, "target", CardTarget.Enemy);
        SpendAmmoBonusEffect bonus = Asset<SpendAmmoBonusEffect>(); Set(bonus, "ammoCost", 1);
        Set(bonus, "bonusEffects", new CardEffect[] { apply }); CardData card = Card(all, bonus, Damage(10));
        card.description = "{damage:2}";
        Assert.That(CardDescriptionBuilder.Build(card, battle), Is.EqualTo("<color=#81C784>15</color>"));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(3));
    }

    [Test] public void DelayedDescriptionUsesCapturedCountWithoutPredictingFutureStatuses()
    {
        ammo.Load(4); DelayEffect delay = Delay(Block(3, true)); Set(delay, "description", "{block:0}");
        Run(All(delay)); ammo.Load(1);
        PendingDelayedEffect entry = battle.DelayedEffects.PendingEffects[0];
        int count = (int)entry.GetType().GetProperty("SpentAmmo").GetValue(entry);
        MethodInfo build = typeof(DelayedEffectDescriptionBuilder).GetMethod("Build", new[] { typeof(string), typeof(CardEffect[]), typeof(int?) });
        Assert.That(build.Invoke(null, new object[] { entry.Description, new CardEffect[] { Block(3, true) }, count }), Is.EqualTo("12"));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(1));
    }

    [Test] public void SeveralThresholdsCanAllTriggerAndDoNotSpendAgain()
    {
        ammo.Load(5); Run(All(Threshold(2, Block(3)), Threshold(5, Block(7)), Threshold(6, Block(100))));
        Assert.That(player.GetBlock(), Is.EqualTo(10)); Assert.That(ammo.CurrentAmmo, Is.Zero);
    }

    [Test] public void ContinuousThresholdMaintainedEffectReleasesWithItsOwnCard()
    {
        ammo.Load(4); StatusEffectData weak = Asset<StatusEffectData>(); Set(weak, "statusType", StatusEffectType.WeaknessExposure);
        ApplyStatusEffect apply = Asset<ApplyStatusEffect>(); Set(apply, "status", weak); Set(apply, "target", CardTarget.Enemy);
        CardData card = Card(All(Threshold(4, apply))); card.type = CardType.Continuous;
        DeckController deck = player.GetComponent<DeckController>();
        Drain(player.ContinuousCards.ActivateRoutine(card, battle, deck, null));
        Assert.That(enemy.StatusEffects.HasStatus(StatusEffectType.WeaknessExposure), Is.True);
        player.ContinuousCards.ResetForBattle();
        Assert.That(enemy.StatusEffects.HasStatus(StatusEffectType.WeaknessExposure), Is.False);
    }

    [Test] public void DisposalOfWaitingChildrenRestoresCountBeforeOtherExecutions()
    {
        ammo.Load(4); CardEffect all = All(Asset<SpendAmmoWaitTestEffect>(), Block(3, true));
        IEnumerator routine = Flatten(all.ExecuteRoutine(battle));
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(typeof(BattleManager).GetProperty("CurrentSpentAmmo").GetValue(battle), Is.EqualTo(4));
        (routine as IDisposable).Dispose(); Run(Threshold(1, Block(50)));
        Assert.That(player.GetBlock(), Is.Zero);
        Assert.That(typeof(BattleManager).GetProperty("CurrentSpentAmmo").GetValue(battle), Is.EqualTo(0));
    }

    private static IEnumerator Flatten(IEnumerator root)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(root);
        try
        {
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); (top as IDisposable)?.Dispose(); continue; }
                if (top.Current is IEnumerator nested) stack.Push(nested); else yield return top.Current;
            }
        }
        finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
    }

    private CardEffect All(params CardEffect[] effects) => Wrapper("SpendAllAmmoEffect", effects);
    private CardEffect Threshold(int minimum, params CardEffect[] effects)
    { CardEffect effect = Wrapper("SpentAmmoThresholdEffect", effects); Set(effect, "minSpentAmmo", minimum); return effect; }
    private CardEffect Wrapper(string name, CardEffect[] effects)
    {
        Type type = typeof(CardEffect).Assembly.GetType(name); Assert.That(type, Is.Not.Null, name + " must exist.");
        CardEffect effect = (CardEffect)ScriptableObject.CreateInstance(type); created.Add(effect); Set(effect, "effects", effects); return effect;
    }
    private BlockEffect Block(int value, bool scaled = false)
    { BlockEffect e = Asset<BlockEffect>(); e.block = value; if (scaled) Mode(e); return e; }
    private DamageEffect Damage(int value, bool scaled = false)
    { DamageEffect e = Asset<DamageEffect>(); e.damage = value; Set(e, "target", CardTarget.Enemy); if (scaled) Mode(e); return e; }
    private static void Mode(object effect)
    { FieldInfo f = Field(effect, "amountMode"); Assert.That(f, Is.Not.Null, "Effects must support spent-ammo scaling."); f.SetValue(effect, Enum.ToObject(f.FieldType, 1)); }
    private ReloadAmmoEffect Reload(int value) { ReloadAmmoEffect e = Asset<ReloadAmmoEffect>(); Set(e, "amount", value); return e; }
    private DelayEffect Delay(params CardEffect[] effects) { DelayEffect e = Asset<DelayEffect>(); Set(e, "effects", effects); Set(e, "delayTurns", 1); return e; }
    private CardData Card(params CardEffect[] effects) { CardData c = Asset<CardData>(); c.type = CardType.Skill; c.cost = new Cost(0, 0); c.effects = effects; return c; }
    private void Run(params CardEffect[] effects) => Drain(battle.ExecuteEffectsRoutine(effects));
    private void Advance() { battle.DelayedEffects.AdvanceTurn(); Drain(battle.DelayedEffects.ProcessDueEffectsRoutine(battle)); }
    private static void Drain(IEnumerator routine)
    { if (routine == null) return; try { while (routine.MoveNext()) if (routine.Current is IEnumerator nested) Drain(nested); } finally { (routine as IDisposable)?.Dispose(); } }
    private T Asset<T>() where T : ScriptableObject { T e = ScriptableObject.CreateInstance<T>(); created.Add(e); return e; }
    private GameObject Track(GameObject go) { created.Add(go); return go; }
    private static FieldInfo Field(object obj, string name)
    { for (Type t = obj.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f; } return null; }
    private static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
}

public class SpendAmmoWaitTestEffect : CardEffect
{
    public override void Execute(BattleManager battle) { }
    public override IEnumerator ExecuteRoutine(BattleManager battle) { yield return null; }
}
