using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class CardDescriptionTests
{
    private readonly List<Object> created = new List<Object>();
    private Player player;
    private Enemy enemy;
    private BattleManager battle;
    private AmmoSystem ammo;

    [SetUp]
    public void SetUp()
    {
        player = NewObject("PreviewPlayer").AddComponent<Player>();
        ammo = player.gameObject.AddComponent<AmmoSystem>();
        enemy = NewObject("PreviewEnemy").AddComponent<Enemy>();
        SetField(player, "currentHP", 80);
        SetField(enemy, "currentHP", 80);
        GameObject manager = NewObject("PreviewBattle");
        manager.SetActive(false);
        battle = manager.AddComponent<BattleManager>();
        SetField(battle, "player", player);
        SetField(battle, "target", enemy);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [TestCase(false, false, "6")]
    [TestCase(true, false, "4")]
    [TestCase(false, true, "9")]
    [TestCase(true, true, "6")]
    public void CardDisplaysStatusAdjustedDamageWithoutChangingSharedData(bool rust, bool exposure, string expected)
    {
        if (rust) player.StatusEffects.Apply(Status(StatusEffectType.Rust), 2);
        if (exposure) enemy.StatusEffects.Apply(Status(StatusEffectType.WeaknessExposure), 2);
        DamageEffect damage = Damage(6, CardTarget.Enemy);
        CardData data = Card("피해 {damage:0}, 2턴, 총알 1알", damage);
        CardUI ui = HandCard(data);
        Assert.That(Description(ui), Is.EqualTo($"피해 {expected}, 2턴, 총알 1알"));
        Assert.That(data.description, Is.EqualTo("피해 {damage:0}, 2턴, 총알 1알"));
        Assert.That(damage.damage, Is.EqualTo(6));
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(80));
    }

    [Test]
    public void DisplayRefreshesOnApplicationExpiryAndClear()
    {
        CardUI ui = HandCard(Card("{damage:0}", Damage(6, CardTarget.Enemy)));
        Assert.That(Description(ui), Is.EqualTo("6"));
        player.StatusEffects.Apply(Status(StatusEffectType.Rust), 1);
        Assert.That(Description(ui), Is.EqualTo("4"));
        enemy.StatusEffects.Apply(Status(StatusEffectType.WeaknessExposure), 2);
        Assert.That(Description(ui), Is.EqualTo("6"));
        player.StatusEffects.ReduceRemainingTurns();
        Assert.That(Description(ui), Is.EqualTo("9"));
        enemy.StatusEffects.Clear();
        Assert.That(Description(ui), Is.EqualTo("6"));
    }

    [Test]
    public void SelfDamageUsesPlayerAsBothAttackerAndDefender()
    {
        player.StatusEffects.Apply(Status(StatusEffectType.Rust), 2);
        player.StatusEffects.Apply(Status(StatusEffectType.WeaknessExposure), 2);
        CardUI ui = HandCard(Card("적 {damage:0}, 자신 {damage:1}",
            Damage(5, CardTarget.Enemy), Damage(3, CardTarget.Player)));
        Assert.That(Description(ui), Is.EqualTo("적 3, 자신 3"));
    }

    [Test]
    public void EarlierStatusEffectChangesLaterDamageWithoutApplyingRealStatus()
    {
        ApplyStatusEffect apply = Asset<ApplyStatusEffect>();
        SetField(apply, "target", CardTarget.Enemy);
        SetField(apply, "status", Status(StatusEffectType.WeaknessExposure));
        SetField(apply, "turns", 2);
        CardUI ui = HandCard(Card("{damage:1}", apply, Damage(6, CardTarget.Enemy)));
        Assert.That(Description(ui), Is.EqualTo("9"));
        Assert.That(enemy.StatusEffects.ActiveEffects, Is.Empty);
    }

    [Test]
    public void ConditionalDamageKeepsItsAmountWhenUnavailableAndRefreshesOnAmmoChanges()
    {
        player.StatusEffects.Apply(Status(StatusEffectType.Rust), 2);
        CardUI ui = HandCard(Card("{damage:0}+{damage:1.0}, {ammoCost:1}알, {ammoCondition:1}",
            Damage(4, CardTarget.Enemy), Bonus(1, Damage(6, CardTarget.Enemy))));
        Assert.That(Description(ui), Is.EqualTo("3+4, 1알, 현재 총알 부족"));
        ammo.Load(1);
        Assert.That(Description(ui), Is.EqualTo("3+4, 1알, 현재 발동 가능"));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(1), "Rendering must never spend live ammo.");
        ammo.TrySpend(1);
        Assert.That(Description(ui), Does.EndWith("현재 총알 부족"));
    }

    [Test]
    public void ConditionalEffectsSpendSnapshotAmmoInOrderAndDoNotShareHypotheticalStatuses()
    {
        ammo.Load(1);
        ApplyStatusEffect apply = Asset<ApplyStatusEffect>();
        SetField(apply, "target", CardTarget.Enemy);
        SetField(apply, "status", Status(StatusEffectType.WeaknessExposure));
        SetField(apply, "turns", 2);
        CardUI ui = HandCard(Card("{ammoCondition:0}; {ammoCondition:1}; {damage:1.1}; {damage:2}",
            Bonus(1, Damage(4, CardTarget.Enemy)), Bonus(1, apply, Damage(6, CardTarget.Enemy)),
            Damage(6, CardTarget.Enemy)));
        Assert.That(Description(ui), Is.EqualTo("현재 발동 가능; 현재 총알 부족; 9; 6"));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(1));
        Assert.That(enemy.StatusEffects.ActiveEffects, Is.Empty);
    }

    [Test]
    public void RequiredAmmoIsSpentBeforeOptionalBonus()
    {
        ammo.Load(1);
        CardData data = Card("{ammoCondition:0}", Bonus(1, Damage(6, CardTarget.Enemy)));
        data.requiredAmmo = 1;
        data.consumeRequiredAmmo = true;
        Assert.That(Description(HandCard(data)), Is.EqualTo("현재 총알 부족"));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(1));
    }

    [Test]
    public void UsedCardKeepsItsDescriptionDuringEffectResolution()
    {
        ammo.Load(1);
        CardUI ui = HandCard(Card("{damage:0}+{damage:1.0}; {ammoCondition:1}",
            Damage(4, CardTarget.Enemy), Bonus(1, Damage(6, CardTarget.Enemy))));
        ui.MarkAsUsed();
        ammo.TrySpend(1);
        player.StatusEffects.Apply(Status(StatusEffectType.Rust), 2);
        Assert.That(Description(ui), Is.EqualTo("4+6; 현재 발동 가능"));
    }

    [Test]
    public void ReloadAndCapacityChangesArePreviewedInEffectOrder()
    {
        SetField(ammo, "maxAmmo", 1);
        IncreaseMaxAmmoEffect capacity = Asset<IncreaseMaxAmmoEffect>();
        SetField(capacity, "amount", 1);
        ReloadAmmoEffect reload = Asset<ReloadAmmoEffect>();
        SetField(reload, "amount", 5);
        CardUI ui = HandCard(Card("{ammoCondition:2}; {ammoCondition:3}", capacity, reload,
            Bonus(2, Damage(6, CardTarget.Enemy)), Bonus(1, Damage(6, CardTarget.Enemy))));
        Assert.That(Description(ui), Is.EqualTo("현재 발동 가능; 현재 총알 부족"));
        Assert.That(ammo.MaxAmmo, Is.EqualTo(1));
        Assert.That(ammo.CurrentAmmo, Is.Zero);
    }

    [UnityTest]
    public IEnumerator UsedCardPreservesPreviewBeforeRequiredAmmoPayment()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return new WaitForSeconds(1f);
        battle = Object.FindFirstObjectByType<BattleManager>();
        player = battle.GetPlayer();
        enemy = battle.GetTarget();
        ammo = player.GetAmmoSystem();
        ammo.Load(2);
        CardHandController hand = (CardHandController)new SerializedObject(battle)
            .FindProperty("dreamCardHand").objectReferenceValue;
        CardUI ui = hand.GetComponentsInChildren<CardUI>()[0];
        CardData data = Card("{damage:0.0}; {ammoCondition:0}", Bonus(1, Damage(6, CardTarget.Enemy)));
        data.requiredAmmo = 1;
        data.consumeRequiredAmmo = true;
        ui.SetCard(data, battle);
        Assert.That(Description(ui), Is.EqualTo("6; 현재 발동 가능"));
        int initialHP = enemy.GetCurrentHP();
        Assert.That(battle.UseCard(ui), Is.True);
        Assert.That(ammo.CurrentAmmo, Is.Zero);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(initialHP - 6));
        Assert.That(Description(ui), Is.EqualTo("6; 현재 발동 가능"));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DisabledCardStopsRefreshingAndReenabledCardUsesCurrentState()
    {
        yield return new EnterPlayMode();
        SetUp();
        CardUI ui = HandCard(Card("{damage:0}", Damage(6, CardTarget.Enemy)));
        ui.gameObject.SetActive(false);
        player.StatusEffects.Apply(Status(StatusEffectType.Rust), 2);
        Assert.That(Description(ui), Is.EqualTo("6"));
        ui.gameObject.SetActive(true);
        Assert.That(Description(ui), Is.EqualTo("4"));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator BattleSceneHandsAndOpenPileListRefreshWithoutChangingCombatState()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return new WaitForSeconds(1f);
        battle = Object.FindFirstObjectByType<BattleManager>();
        player = battle.GetPlayer();
        enemy = battle.GetTarget();
        ammo = player.GetAmmoSystem();
        int playerHP = player.GetCurrentHP();
        int enemyHP = enemy.GetCurrentHP();
        CardHandController dream = (CardHandController)new SerializedObject(battle)
            .FindProperty("dreamCardHand").objectReferenceValue;
        CardHandController nightmare = (CardHandController)new SerializedObject(battle)
            .FindProperty("nightmareCardHand").objectReferenceValue;
        CardData attack = AssetDatabase.LoadAssetAtPath<CardData>(
            "Assets/CardData/Soldier/Starting/Dream/Attack/01.Card_Attack/Attack.asset");
        CardData sword = AssetDatabase.LoadAssetAtPath<CardData>(
            "Assets/CardData/Soldier/Starting/Nightmare/Attack/01.Card_Double_Edged_Sword/Double_Edged_Sword.asset");
        CardUI dreamCard = dream.GetComponentsInChildren<CardUI>()[0];
        CardUI nightmareCard = nightmare.GetComponentsInChildren<CardUI>()[0];
        dreamCard.SetCard(attack, battle);
        nightmareCard.SetCard(sword, battle);
        player.StatusEffects.Apply(Status(StatusEffectType.Rust), 2);
        Assert.That(Description(dreamCard), Does.Contain("4의 피해"));
        Assert.That(Description(nightmareCard), Does.Contain("3의 피해").And.Contain("2의 피해"));

        DeckListUI list = Object.FindObjectsByType<DeckListUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)[0];
        list.ShowCards(new[] { attack }, dream);
        Transform content = (Transform)new SerializedObject(list).FindProperty("content").objectReferenceValue;
        DeckCardUI entry = content.GetComponentInChildren<DeckCardUI>();
        Assert.That(Description(entry), Does.Contain("4의 피해"));
        enemy.StatusEffects.Apply(Status(StatusEffectType.WeaknessExposure), 2);
        Assert.That(Description(dreamCard), Does.Contain("6의 피해"));
        Assert.That(Description(entry), Does.Contain("6의 피해"));
        player.StatusEffects.Clear();
        Assert.That(Description(entry), Does.Contain("9의 피해"));
        list.Close();

        CardData snipe = AssetDatabase.LoadAssetAtPath<CardData>(
            "Assets/CardData/Soldier/Starting/Dream/Attack/02.Card_Snipe/Snipe.asset");
        list.ShowCards(new[] { snipe }, dream);
        entry = content.GetComponentInChildren<DeckCardUI>();
        Assert.That(Description(entry), Does.Contain("현재 총알 부족"));
        ammo.Load(1);
        Assert.That(Description(entry), Does.Contain("현재 발동 가능"));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(1));
        Assert.That(player.GetCurrentHP(), Is.EqualTo(playerHP));
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(enemyHP));
        list.Close();
        yield return new ExitPlayMode();
    }

    [Test]
    public void RebindingCardUnsubscribesFromPreviousBattle()
    {
        CardData data = Card("{damage:0}", Damage(6, CardTarget.Enemy));
        CardUI ui = HandCard(data);
        Player next = NewObject("NextPlayer").AddComponent<Player>();
        GameObject root = NewObject("NextBattle");
        root.SetActive(false);
        BattleManager nextBattle = root.AddComponent<BattleManager>();
        SetField(nextBattle, "player", next);
        SetField(nextBattle, "target", enemy);
        ui.SetCard(data, nextBattle);
        next.StatusEffects.Apply(Status(StatusEffectType.Rust), 2);
        Assert.That(Description(ui), Is.EqualTo("4"));
        // Changing a shared effect detects a stale subscription even if both snapshots had the same damage.
        ((DamageEffect)data.effects[0]).damage = 20;
        player.StatusEffects.Apply(Status(StatusEffectType.Rust), 2);
        Assert.That(Description(ui), Is.EqualTo("4"));
    }

    [Test]
    public void DeckCardWithoutBattleUsesBaseNumbersAndPreservesUnknownTokens()
    {
        DeckCardUI ui = Object.Instantiate(AssetDatabase.LoadAssetAtPath<DeckCardUI>("Assets/Prefabs/UI/DeckCard.prefab"));
        created.Add(ui.gameObject);
        ui.SetCard(Card("{damage:0} / {damage:99} / {unrecognized}", Damage(6, CardTarget.Enemy)));
        Assert.That(Description(ui), Is.EqualTo("6 / {damage:99} / {unrecognized}"));
    }

    [Test]
    public void ExistingSoldierCardsDisplayCorrectEnemySelfAndBonusDamage()
    {
        player.StatusEffects.Apply(Status(StatusEffectType.Rust), 2);
        ammo.Load(1);
        CardData snipe = AssetDatabase.LoadAssetAtPath<CardData>(
            "Assets/CardData/Soldier/Starting/Dream/Attack/02.Card_Snipe/Snipe.asset");
        string description = Description(HandCard(snipe));
        Assert.That(description, Does.Contain("3의 피해").And.Contain("4의 피해"));
        Assert.That(description, Does.Not.Contain("{damage:"));
        CardData tracking = AssetDatabase.LoadAssetAtPath<CardData>(
            "Assets/CardData/Soldier/Starting/Nightmare/Skill/02.Target_Tracking/Target_Tracking.asset");
        Assert.That(Description(HandCard(tracking)), Does.Contain("3의 피해"));
    }

    private CardUI HandCard(CardData data)
    {
        CardUI ui = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CardUI>("Assets/Prefabs/UI/Card.prefab"));
        created.Add(ui.gameObject);
        ui.SetCard(data, battle);
        return ui;
    }

    private static string Description(Object ui)
    {
        TMP_Text label = (TMP_Text)new SerializedObject(ui).FindProperty("descriptionText").objectReferenceValue;
        return Regex.Replace(label.text, "<[^>]+>", "");
    }

    private CardData Card(string description, params CardEffect[] effects)
    {
        CardData data = Asset<CardData>();
        data.description = description;
        data.effects = effects;
        return data;
    }

    private DamageEffect Damage(int amount, CardTarget target)
    {
        DamageEffect effect = Asset<DamageEffect>();
        effect.damage = amount;
        SetField(effect, "target", target);
        return effect;
    }

    private SpendAmmoBonusEffect Bonus(int amount, params CardEffect[] effects)
    {
        SpendAmmoBonusEffect bonus = Asset<SpendAmmoBonusEffect>();
        SetField(bonus, "ammoCost", amount);
        SetField(bonus, "bonusEffects", effects);
        return bonus;
    }

    private StatusEffectData Status(StatusEffectType type)
    {
        StatusEffectData status = Asset<StatusEffectData>();
        SetField(status, "statusType", type);
        return status;
    }

    private T Asset<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        created.Add(asset);
        return asset;
    }

    private GameObject NewObject(string name)
    {
        GameObject obj = new GameObject(name);
        created.Add(obj);
        return obj;
    }

    private static void SetField(object value, string name, object fieldValue)
    {
        for (System.Type type = value.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) continue;
            field.SetValue(value, fieldValue);
            return;
        }
        Assert.Fail($"Missing field {name}");
    }
}
