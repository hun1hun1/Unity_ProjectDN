using System;
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

public class DefenseBreakTests
{
    private List<Object> created = new List<Object>();
    private const StatusEffectType DefenseBreak = StatusEffectType.DefenseBreak;

    [TearDown]
    public void TearDown()
    {
        for (int i = (created?.Count ?? 0) - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created?.Clear();
        RunDeckInventory.ResetForNewGame();
    }

    [TestCase(0, false, 0)]
    [TestCase(1, false, 1)]
    [TestCase(4, false, 4)]
    [TestCase(5, false, 5)]
    [TestCase(int.MaxValue, false, int.MaxValue)]
    [TestCase(0, true, 0)]
    [TestCase(1, true, 0)]
    [TestCase(4, true, 2)]
    [TestCase(5, true, 2)]
    [TestCase(int.MaxValue, true, 1073741823)]
    public void BlockGainHalvesOnlyWhileAffectedAndRoundsDown(int amount, bool affected, int expected)
    {
        Player player = Track(new GameObject("Player")).AddComponent<Player>();
        if (affected) player.StatusEffects.Apply(Status(DefenseBreak), 2);
        player.AddBlock(amount);
        Assert.That(player.GetBlock(), Is.EqualTo(expected));
    }

    [Test]
    public void ExistingBlockIsKeptAndRepeatedApplicationsDoNotMultiplyThePenalty()
    {
        Player player = Track(new GameObject("Player")).AddComponent<Player>();
        player.AddBlock(10);
        StatusEffectData status = Status(DefenseBreak);
        player.StatusEffects.Apply(status, 1);
        player.StatusEffects.Apply(status, 2);
        Assert.That(player.GetBlock(), Is.EqualTo(10));
        player.AddBlock(8);
        Assert.That(player.GetBlock(), Is.EqualTo(14));
        Assert.That(player.StatusEffects.GetRemainingTurns(DefenseBreak), Is.EqualTo(3));
        for (int i = 0; i < 3; i++) player.StatusEffects.ReduceRemainingTurns();
        player.AddBlock(8);
        Assert.That(player.GetBlock(), Is.EqualTo(22));
    }

    [Test]
    public void OnlyTheReceivingCharactersStatusChangesTheirGain()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        player.StatusEffects.Apply(Status(DefenseBreak), 2);
        player.AddBlock(8);
        enemy.AddBlock(8);
        Assert.That(player.GetBlock(), Is.EqualTo(4));
        Assert.That(enemy.GetBlock(), Is.EqualTo(8));
        enemy.StatusEffects.Apply(Status(DefenseBreak), 2);
        enemy.AddBlock(8);
        Assert.That(enemy.GetBlock(), Is.EqualTo(12));
    }

    [Test]
    public void EnemyBlockIntentMatchesItsReducedGainAndBaseActionStaysUnchanged()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        enemy.StatusEffects.Apply(Status(DefenseBreak), 2);
        EnemyBlockAction action = Asset<EnemyBlockAction>();
        SetField(action, "block", 9);
        Assert.That(action.GetActionDescription(battle), Does.EndWith("4"));
        Assert.That(action.GetActionDescription(), Does.EndWith("9"));
        action.Execute(battle);
        Assert.That(enemy.GetBlock(), Is.EqualTo(4));
        Assert.That(action.GetBlock(), Is.EqualTo(9));
    }

    [Test]
    public void BlockDescriptionRefreshesOnApplicationAndExpiryWhileInspectionKeepsBaseValues()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        BlockEffect block = Block(8);
        CardData card = Card("{block:0}", block);
        CardUI ui = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Card.prefab"))).GetComponent<CardUI>();
        ui.SetCard(card, battle);
        TMP_Text label = (TMP_Text)GetField(ui, "descriptionText");
        Assert.That(label.text, Is.EqualTo("8"));
        player.StatusEffects.Apply(Status(DefenseBreak), 1);
        Assert.That(label.text, Is.EqualTo("<color=#E57373>4</color>"));
        Assert.That(CardDescriptionBuilder.Build(card, battle, false), Is.EqualTo("8"));
        player.StatusEffects.ReduceRemainingTurns();
        Assert.That(label.text, Is.EqualTo("8"));
        Assert.That(block.block, Is.EqualTo(8));
        Assert.That(player.GetBlock(), Is.Zero);
    }

    [Test]
    public void EarlierStatusChangesLaterBlockInSnapshotWithoutApplyingLiveStatus()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        ApplyStatusEffect status = Apply(DefenseBreak, CardTarget.Player);
        CardData card = Card("{block:1}", status, Block(7));
        Assert.That(CardDescriptionBuilder.Build(card, battle), Is.EqualTo("<color=#E57373>3</color>"));
        Assert.That(CardDescriptionBuilder.Build(card, battle, false), Is.EqualTo("7"));
        Assert.That(player.StatusEffects.ActiveEffects, Is.Empty);
        Assert.That(player.GetBlock(), Is.Zero);
        card.effects = new CardEffect[] { Block(7), status };
        card.description = "{block:0}";
        Assert.That(CardDescriptionBuilder.Build(card, battle), Is.EqualTo("7"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NestedConditionalBlockUsesSnapshotStatusWithoutSpendingRealAmmo(bool hasAmmo)
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        player.StatusEffects.Apply(Status(DefenseBreak), 2);
        if (hasAmmo) player.GetAmmoSystem().Load(1);
        SpendAmmoBonusEffect bonus = Asset<SpendAmmoBonusEffect>();
        SetField(bonus, "bonusEffects", new CardEffect[] { Block(8) });
        CardData card = Card("{block:0.0}", bonus);
        Assert.That(CardDescriptionBuilder.Build(card, battle), Is.EqualTo("<color=#E57373>4</color>"));
        Assert.That(player.GetAmmoSystem().CurrentAmmo, Is.EqualTo(hasAmmo ? 1 : 0));
        Assert.That(player.GetBlock(), Is.Zero);
    }

    [Test]
    public void EnemyDefenseBreakDoesNotChangePlayerBlockDescription()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        enemy.StatusEffects.Apply(Status(DefenseBreak), 2);
        Assert.That(CardDescriptionBuilder.Build(Card("{block:0}", Block(8)), battle), Is.EqualTo("8"));
    }

    [Test]
    public void ContinuousSourceAffectsFinalRecurringBlockBeforeExpiryAndTurnPreviewSupportsBaseValues()
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        CardData card = Card("{turnBlock:0}", Apply(DefenseBreak, CardTarget.Player));
        card.type = CardType.Continuous;
        card.continuousDurationType = ContinuousDurationType.FixedTurns;
        card.continuousTurns = 1;
        card.turnStartEffects = new CardEffect[] { Block(8) };
        player.ContinuousCards.Activate(card, battle, deck);
        Assert.That(CardDescriptionBuilder.Build(card, battle), Is.EqualTo("<color=#E57373>4</color>"));
        Assert.That(CardDescriptionBuilder.Build(card, battle, false), Is.EqualTo("8"));
        Assert.That(player.GetBlock(), Is.Zero);
        player.ContinuousCards.ProcessTurnStartEffects(battle);
        Assert.That(player.GetBlock(), Is.EqualTo(4));
        Assert.That(player.StatusEffects.HasStatus(DefenseBreak), Is.False);
        player.AddBlock(8);
        Assert.That(player.GetBlock(), Is.EqualTo(12));
    }

    [TestCase("Assets/CardData/Basic/Dream/Skill/1.Card_Defense/Defense.asset")]
    [TestCase("Assets/CardData/Basic/Nightmare/Skill/1.Card_Cat_and_Mouse/Cat_and_Mouse.asset")]
    [TestCase("Assets/CardData/Soldier/Starting/Dream/Skill/01.Card_Defense/Defense.asset")]
    [TestCase("Assets/CardData/Soldier/Starting/Nightmare/Skill/01.Card_Cat_and_Mouse/Cat_and_Mouse.asset")]
    public void ExistingDefenseCardsDisplayActualGainAndRetainTheirOtherEffects(string path)
    {
        BattleManager battle = Battle(out Player player, out Enemy enemy, out DeckController deck);
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
        Assert.That(card, Is.Not.Null, path);
        player.StatusEffects.Apply(Status(DefenseBreak), 2);
        Assert.That(Plain(CardDescriptionBuilder.Build(card, battle)), Does.StartWith("방어도를 2"));
        Assert.That(Plain(CardDescriptionBuilder.Build(card, battle, false)), Does.StartWith("방어도를 4"));
        Assert.That(card.effects, Has.Length.EqualTo(2));
        foreach (CardEffect effect in card.effects) Assert.That(effect, Is.Not.Null);
    }

    [Test]
    public void SerializedStatusDefinitionHasTheNewTypeNameDescriptionAndIcon()
    {
        StatusEffectData status = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/DefenseBreak.asset");
        Assert.That(status, Is.Not.Null);
        Assert.That(status.Type, Is.EqualTo(DefenseBreak));
        Assert.That(status.DisplayName, Is.EqualTo("방어 파괴"));
        Assert.That(status.Description, Does.Contain("50%"));
        Assert.That(status.Icon, Is.Not.Null);
        Assert.That(Status(DefenseBreak).DisplayName, Is.EqualTo("방어 파괴"));
    }

    [UnityTest]
    public IEnumerator BattleSceneDefenseCardRefreshesItsDescriptionAndGainsTheDisplayedBlock()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return null;
        yield return new WaitForSeconds(0.8f);
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Player player = battle.GetPlayer();
        CardHandController hand = (CardHandController)GetField(battle, "dreamCardHand");
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>("Assets/CardData/Soldier/Starting/Dream/Skill/01.Card_Defense/Defense.asset");
        CardUI ui = ((List<CardUI>)GetField(hand, "cards"))[0];
        ui.SetCard(card, battle);
        TMP_Text label = (TMP_Text)GetField(ui, "descriptionText");
        Assert.That(Plain(label.text), Does.StartWith("방어도를 4"));
        StatusEffectData status = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/DefenseBreak.asset");
        Assert.That(status, Is.Not.Null);
        player.StatusEffects.Apply(status, 2);
        Assert.That(Plain(label.text), Does.StartWith("방어도를 2"));
        Assert.That(battle.UseCard(ui), Is.True);
        Assert.That(player.GetBlock(), Is.EqualTo(2));
        float deadline = Time.realtimeSinceStartup + 8;
        while ((int)GetField(battle, "activeCardUses") != 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That((int)GetField(battle, "activeCardUses"), Is.Zero);
        yield return new ExitPlayMode();
    }

    private static string Plain(string text) => Regex.Replace(text, "<.*?>", "");

    private BattleManager Battle(out Player player, out Enemy enemy, out DeckController deck)
    {
        GameObject root = Track(new GameObject("Player"));
        player = root.AddComponent<Player>();
        root.AddComponent<CostSystem>().ResetForTurn();
        root.AddComponent<AmmoSystem>().ResetForBattle();
        deck = root.AddComponent<DeckController>();
        enemy = Track(new GameObject("Enemy")).AddComponent<Enemy>();
        BattleManager battle = Track(new GameObject("Battle")).AddComponent<BattleManager>();
        SetField(battle, "player", player); SetField(battle, "target", enemy); SetField(battle, "deckController", deck);
        return battle;
    }

    private StatusEffectData Status(StatusEffectType type)
    {
        StatusEffectData status = Asset<StatusEffectData>();
        SetField(status, "statusType", type);
        return status;
    }

    private ApplyStatusEffect Apply(StatusEffectType type, CardTarget target)
    {
        ApplyStatusEffect effect = Asset<ApplyStatusEffect>();
        SetField(effect, "status", Status(type)); SetField(effect, "target", target); SetField(effect, "turns", 2);
        return effect;
    }

    private BlockEffect Block(int amount) { BlockEffect effect = Asset<BlockEffect>(); effect.block = amount; return effect; }
    private CardData Card(string description, params CardEffect[] effects)
    {
        CardData card = Asset<CardData>(); card.description = description; card.effects = effects; return card;
    }

    private T Asset<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>(); (created ??= new List<Object>()).Add(value); return value;
    }

    private GameObject Track(GameObject value) { (created ??= new List<Object>()).Add(value); return value; }
    private static FieldInfo Field(object value, string name)
    {
        for (Type type = value.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field;
        }
        throw new ArgumentException(name);
    }
    private static object GetField(object value, string name) => Field(value, name).GetValue(value);
    private static void SetField(object value, string name, object data) => Field(value, name).SetValue(value, data);
}
