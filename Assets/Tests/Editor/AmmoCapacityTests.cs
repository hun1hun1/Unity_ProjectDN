using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class AmmoCapacityTests
{
    private readonly List<Object> created = new List<Object>();

    [SetUp]
    public void SetUp() => RunRelicInventory.ResetForNewGame();

    [TearDown]
    public void TearDown()
    {
        RunRelicInventory.ResetForNewGame();
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] == null) continue;
            if (!Application.isPlaying && created[i] is GameObject obj)
            {
                AmmoSystem ammo = obj.GetComponent<AmmoSystem>();
                if (ammo != null) InvokeAmmoLifecycle(ammo, "OnDisable");
            }
            Object.DestroyImmediate(created[i]);
        }
        created.Clear();
    }

    [Test]
    public void BattleCapacityIncreaseStacksAndExpiresAtBattleReset()
    {
        AmmoSystem ammo = CreateAmmo();
        Assert.That(ammo.IncreaseMaxForBattle(3), Is.EqualTo(3));
        ammo.IncreaseMaxForBattle(2);
        Assert.That(ammo.MaxAmmo, Is.EqualTo(15));
        Assert.That(ammo.BaseMaxAmmo, Is.EqualTo(10));
        Assert.That(ammo.BattleMaxAmmoBonus, Is.EqualTo(5));
        Assert.That(ammo.CurrentAmmo, Is.Zero);
        ammo.Load(99);
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(15));
        ammo.ResetForBattle();
        Assert.That(ammo.MaxAmmo, Is.EqualTo(10));
        Assert.That(ammo.BattleMaxAmmoBonus, Is.Zero);
        Assert.That(ammo.CurrentAmmo, Is.Zero);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonPositiveBattleCapacityChangesAreIgnored(int amount)
    {
        AmmoSystem ammo = CreateAmmo();
        int notifications = 0;
        ammo.AmmoChanged += (_, __) => notifications++;
        Assert.That(ammo.IncreaseMaxForBattle(amount), Is.Zero);
        Assert.That(ammo.MaxAmmo, Is.EqualTo(10));
        Assert.That(notifications, Is.Zero);
    }

    [Test]
    public void RelicCapacitySurvivesBattleResetAndPlayerReplacement()
    {
        AmmoSystem ammo = CreateAmmo();
        AmmoCapacityRelic relic = CreateRelic(4);
        Assert.That(relic.Acquire(), Is.True);
        ammo.IncreaseMaxForBattle(3);
        Assert.That(ammo.MaxAmmo, Is.EqualTo(17));
        ammo.Load(17);
        ammo.ResetForBattle();
        Assert.That(ammo.MaxAmmo, Is.EqualTo(14));
        Assert.That(ammo.CurrentAmmo, Is.Zero);
        InvokeAmmoLifecycle(ammo, "OnDisable");
        Object.DestroyImmediate(ammo.gameObject);
        AmmoSystem next = CreateAmmo();
        Assert.That(next.MaxAmmo, Is.EqualTo(14));
        Assert.That(next.RunMaxAmmoBonus, Is.EqualTo(4));
    }

    [Test]
    public void DifferentRelicsStackButTheSameRelicIsAcquiredOnlyOnce()
    {
        AmmoSystem ammo = CreateAmmo();
        AmmoCapacityRelic first = CreateRelic(2);
        AmmoCapacityRelic second = CreateRelic(3);
        Assert.That(RunRelicInventory.Acquire(first), Is.True);
        Assert.That(first.Acquire(), Is.False);
        Assert.That(second.Acquire(), Is.True);
        Assert.That(RunRelicInventory.Count, Is.EqualTo(2));
        Assert.That(RunRelicInventory.Contains(first), Is.True);
        Assert.That(RunRelicInventory.MaxAmmoBonus, Is.EqualTo(5));
        Assert.That(ammo.MaxAmmo, Is.EqualTo(15));
        Assert.That(ammo.CurrentAmmo, Is.Zero);
    }

    [TestCase(0)]
    [TestCase(-3)]
    public void InvalidRelicBonusesCannotEnterTheInventory(int amount)
    {
        AmmoCapacityRelic relic = CreateRelic(amount);
        Assert.That(relic.Acquire(), Is.False);
        Assert.That(RunRelicInventory.Acquire(null), Is.False);
        Assert.That(RunRelicInventory.Count, Is.Zero);
        Assert.That(RunRelicInventory.MaxAmmoBonus, Is.Zero);
    }

    [Test]
    public void NewGameClearsBothBonusesAndRestoresStartingAmmo()
    {
        AmmoSystem ammo = CreateAmmo(2);
        AmmoCapacityRelic relic = CreateRelic(4);
        relic.Acquire();
        ammo.IncreaseMaxForBattle(3);
        ammo.Load(99);
        RunRelicInventory.ResetForNewGame();
        Assert.That(ammo.MaxAmmo, Is.EqualTo(10));
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(2));
        Assert.That(ammo.BattleMaxAmmoBonus, Is.Zero);
        Assert.That(ammo.RunMaxAmmoBonus, Is.Zero);
        Assert.That(RunRelicInventory.Count, Is.Zero);
        Assert.That(relic.MaxAmmoBonus, Is.EqualTo(4), "Run state must not mutate shared assets.");
        Assert.That(relic.Acquire(), Is.True);
    }

    [Test]
    public void DisabledAmmoSystemReceivesNewGameResetWhenEnabledAgain()
    {
        AmmoSystem ammo = CreateAmmo();
        CreateRelic(4).Acquire();
        ammo.IncreaseMaxForBattle(3);
        ammo.Load(17);
        SetAmmoActive(ammo, false);
        RunRelicInventory.ResetForNewGame();
        SetAmmoActive(ammo, true);
        Assert.That(ammo.MaxAmmo, Is.EqualTo(10));
        Assert.That(ammo.CurrentAmmo, Is.Zero);
    }

    [Test]
    public void DisabledAmmoSystemReceivesRelicBonusWhenEnabledAgain()
    {
        AmmoSystem ammo = CreateAmmo();
        ammo.IncreaseMaxForBattle(2);
        SetAmmoActive(ammo, false);
        CreateRelic(4).Acquire();
        SetAmmoActive(ammo, true);
        Assert.That(ammo.MaxAmmo, Is.EqualTo(16));
        Assert.That(ammo.BattleMaxAmmoBonus, Is.EqualTo(2));
    }

    [Test]
    public void CapacityChangesNotifyEvenWhenCurrentAmmoIsUnchanged()
    {
        AmmoSystem ammo = CreateAmmo();
        var observations = new List<string>();
        ammo.AmmoChanged += (current, maximum) => observations.Add($"{current}/{maximum}");
        ammo.IncreaseMaxForBattle(2);
        CreateRelic(3).Acquire();
        ammo.ResetForBattle();
        RunRelicInventory.ResetForNewGame();
        Assert.That(observations, Is.EqualTo(new[] { "0/12", "0/15", "0/13", "0/10" }));
    }

    [Test]
    public void RelicBonusIsCapturedAtAcquisitionWithoutMutatingItsAsset()
    {
        AmmoCapacityRelic relic = CreateRelic(4);
        relic.Acquire();
        JsonUtility.FromJsonOverwrite("{\"maxAmmoBonus\":8}", relic);
        AmmoSystem ammo = CreateAmmo();
        Assert.That(ammo.MaxAmmo, Is.EqualTo(14));
        Assert.That(relic.Acquire(), Is.False);
    }

    [Test]
    public void VeryLargeCapacityBonusesCannotOverflowOrOverfillAmmo()
    {
        AmmoSystem ammo = CreateAmmo();
        ammo.IncreaseMaxForBattle(int.MaxValue);
        ammo.IncreaseMaxForBattle(int.MaxValue);
        CreateRelic(int.MaxValue).Acquire();
        CreateRelic(int.MaxValue).Acquire();
        Assert.That(ammo.MaxAmmo, Is.EqualTo(int.MaxValue));
        Assert.That(ammo.Load(int.MaxValue), Is.EqualTo(int.MaxValue));
        Assert.That(ammo.Load(1), Is.Zero);
        ammo.ResetForBattle();
        Assert.That(ammo.MaxAmmo, Is.EqualTo(int.MaxValue));
        Assert.That(ammo.CurrentAmmo, Is.Zero);
    }

    [Test]
    public void CapacityEffectExecutesWithExistingReloadEffectsInArrayOrder()
    {
        Soldier soldier = CreateObject("Soldier").AddComponent<Soldier>();
        AmmoSystem ammo = soldier.GetAmmoSystem();
        ammo.ResetForBattle();
        BattleManager manager = CreateObject("Battle").AddComponent<BattleManager>();
        SetField(manager, "player", soldier);
        CardData card = CreateCapacityCard(3, true);
        foreach (CardEffect effect in card.effects) effect.Execute(manager);
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(13));
        Assert.That(ammo.MaxAmmo, Is.EqualTo(13));
    }

    [Test]
    public void CapacityEffectIsSafeForDefaultPlayerAndMissingBattle()
    {
        IncreaseMaxAmmoEffect effect = CreateAsset<IncreaseMaxAmmoEffect>();
        Player player = CreateObject("Default Player").AddComponent<Player>();
        BattleManager manager = CreateObject("Battle").AddComponent<BattleManager>();
        SetField(manager, "player", player);
        Assert.DoesNotThrow(() => effect.Execute(manager));
        Assert.DoesNotThrow(() => effect.Execute(null));
        Assert.That(player.GetAmmoSystem(), Is.Null);
    }

    [Test]
    public void CapacityExamplesHaveValidSerializedBindings()
    {
        const string folder = "Assets/CardData/Soldier/Examples/";
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(folder + "IncreaseMaxAmmo.asset");
        AmmoCapacityRelic relic = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>(folder + "AmmoCapacityRelic.asset");
        Assert.That(card, Is.Not.Null);
        Assert.That(card.effects, Has.Length.EqualTo(1));
        Assert.That(card.effects[0], Is.InstanceOf<IncreaseMaxAmmoEffect>());
        Assert.That(new SerializedObject(card.effects[0]).FindProperty("amount").intValue, Is.EqualTo(1));
        Assert.That(relic, Is.Not.Null);
        Assert.That(relic.MaxAmmoBonus, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator CapacityCardAndRelicUpdateUiAndRelicSurvivesSceneReload()
    {
        yield return new EnterPlayMode();
        yield return VerifyCapacityCardAndRelicAcrossSceneReload();
        yield return new ExitPlayMode();
    }

    // Initialize captured locals after EnterPlayMode has completed its domain reload.
    private IEnumerator VerifyCapacityCardAndRelicAcrossSceneReload()
    {
        yield return LoadBattleScene();
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        Soldier soldier = manager.GetPlayer() as Soldier;
        Assert.That(soldier, Is.Not.Null);
        AmmoSystem ammo = soldier.GetAmmoSystem();
        TMP_Text label = Object.FindFirstObjectByType<AmmoUI>().GetComponentInChildren<TMP_Text>();
        CardHandController hand = (CardHandController)GetField(manager, "dreamCardHand");
        CardUI ui = ((List<CardUI>)GetField(hand, "cards"))[0];
        ui.SetCard(CreateCapacityCard(3, true), manager);
        Assert.That(manager.UseCard(ui), Is.True);
        Assert.That(manager.BlocksCardInteraction, Is.True);
        Assert.That(ammo.CurrentAmmo, Is.Zero, "The capacity and reload effects wait for the card to reach the using area.");
        yield return Until(() => ui == null && !manager.HasOtherCardUses && !manager.HandsAreMoving,
            "The capacity card did not finish resolving and refilling the hand.");
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(13));
        Assert.That(label.text, Is.EqualTo("총알 13 / 13"));
        Assert.That(soldier.GetCostSystem().GetDreamCost(), Is.EqualTo(2));
        AmmoCapacityRelic relic = CreateRelic(2);
        relic.Acquire();
        Assert.That(label.text, Is.EqualTo("총알 13 / 15"));

        yield return LoadBattleScene();
        manager = Object.FindFirstObjectByType<BattleManager>();
        soldier = manager.GetPlayer() as Soldier;
        Assert.That(soldier, Is.Not.Null);
        ammo = soldier.GetAmmoSystem();
        label = Object.FindFirstObjectByType<AmmoUI>().GetComponentInChildren<TMP_Text>();
        Assert.That(ammo.MaxAmmo, Is.EqualTo(12));
        Assert.That(ammo.CurrentAmmo, Is.Zero);
        Assert.That(label.text, Is.EqualTo("총알 0 / 12"));
        Assert.That(relic.Acquire(), Is.False);
        RunRelicInventory.ResetForNewGame();
        Assert.That(label.text, Is.EqualTo("총알 0 / 10"));
    }

    [UnityTest]
    public IEnumerator BattleManagerStartResetsReusedSoldierButKeepsRelics()
    {
        yield return new EnterPlayMode();
        SceneManager.LoadScene("BattleScene");
        yield return null;
        yield return new WaitForSeconds(0.8f);
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        Soldier soldier = manager.GetPlayer() as Soldier;
        Assert.That(soldier, Is.Not.Null);
        AmmoSystem ammo = soldier.GetAmmoSystem();
        CreateRelic(2).Acquire();
        ammo.IncreaseMaxForBattle(4);
        ammo.Load(16);
        typeof(BattleManager).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
        Assert.That(ammo.MaxAmmo, Is.EqualTo(12));
        Assert.That(ammo.CurrentAmmo, Is.Zero);
        Assert.That(Object.FindFirstObjectByType<AmmoUI>().GetComponentInChildren<TMP_Text>().text,
            Is.EqualTo("총알 0 / 12"));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DisabledSoldierSynchronizesNewGameAndRelicsInPlayMode()
    {
        yield return new EnterPlayMode();
        GameObject obj = new GameObject("Runtime Ammo");
        AmmoSystem ammo = obj.AddComponent<AmmoSystem>();
        CreateRelic(4).Acquire();
        ammo.IncreaseMaxForBattle(2);
        ammo.Load(16);
        Assert.That(ammo.MaxAmmo, Is.EqualTo(16));
        obj.SetActive(false);
        RunRelicInventory.ResetForNewGame();
        obj.SetActive(true);
        Assert.That(ammo.MaxAmmo, Is.EqualTo(10));
        Assert.That(ammo.CurrentAmmo, Is.Zero);
        obj.SetActive(false);
        CreateRelic(3).Acquire();
        obj.SetActive(true);
        Assert.That(ammo.MaxAmmo, Is.EqualTo(13));
        Assert.That(ammo.CurrentAmmo, Is.Zero);
        Object.Destroy(obj);
        yield return null;
        yield return new ExitPlayMode();
    }

    private static IEnumerator LoadBattleScene()
    {
        AsyncOperation loading = SceneManager.LoadSceneAsync("BattleScene");
        Assert.That(loading, Is.Not.Null);
        yield return Until(() => loading.isDone, "BattleScene did not complete loading.");
        yield return Until(() =>
        {
            BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
            return battle != null && battle.GetPlayer() != null && battle.CanInspectCards;
        }, "BattleScene did not finish initializing its player and hand animations.");
    }

    private static IEnumerator Until(System.Func<bool> ready, string message)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!ready())
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), message);
            yield return null;
        }
    }

    private AmmoSystem CreateAmmo(int starting = 0)
    {
        AmmoSystem ammo = CreateObject("Ammo").AddComponent<AmmoSystem>();
        JsonUtility.FromJsonOverwrite($"{{\"startingAmmo\":{starting}}}", ammo);
        ammo.ResetForBattle();
        // EditMode does not dispatch runtime MonoBehaviour enable callbacks.
        if (!Application.isPlaying) InvokeAmmoLifecycle(ammo, "OnEnable");
        return ammo;
    }

    private AmmoCapacityRelic CreateRelic(int bonus)
    {
        AmmoCapacityRelic relic = CreateAsset<AmmoCapacityRelic>();
        JsonUtility.FromJsonOverwrite($"{{\"maxAmmoBonus\":{bonus}}}", relic);
        return relic;
    }

    private CardData CreateCapacityCard(int amount, bool reload)
    {
        IncreaseMaxAmmoEffect effect = CreateAsset<IncreaseMaxAmmoEffect>();
        JsonUtility.FromJsonOverwrite($"{{\"amount\":{amount}}}", effect);
        CardData card = CreateAsset<CardData>();
        card.deckType = CardDeckType.TypeDream;
        card.cost = new Cost(1, 0);
        if (reload)
        {
            ReloadAmmoEffect loading = CreateAsset<ReloadAmmoEffect>();
            JsonUtility.FromJsonOverwrite("{\"amount\":99}", loading);
            card.effects = new CardEffect[] { effect, loading };
        }
        else card.effects = new CardEffect[] { effect };
        return card;
    }

    private GameObject CreateObject(string name)
    {
        GameObject obj = new GameObject(name);
        created.Add(obj);
        return obj;
    }

    private T CreateAsset<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        created.Add(asset);
        return asset;
    }

    private static object GetField(object obj, string name) =>
        obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);

    private static void SetField(object obj, string name, object value) =>
        obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);

    private static void SetAmmoActive(AmmoSystem ammo, bool active)
    {
        if (!active && !Application.isPlaying) InvokeAmmoLifecycle(ammo, "OnDisable");
        ammo.gameObject.SetActive(active);
        if (active && !Application.isPlaying) InvokeAmmoLifecycle(ammo, "OnEnable");
    }

    private static void InvokeAmmoLifecycle(AmmoSystem ammo, string method) =>
        typeof(AmmoSystem).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ammo, null);
}
