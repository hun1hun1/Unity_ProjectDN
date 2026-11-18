using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public partial class RunCheckpointRuntimeTests
{
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(RunSession.Instance!=null)Object.DestroyImmediate(RunSession.Instance.gameObject);
        if(Application.isPlaying)yield return new ExitPlayMode();
        RunSaves.UseStore(null);RunAssetCatalog.SetRuntimeCatalog(null);RunDeckInventory.ResetForNewGame();RunRelicInventory.ResetForNewGame();
    }
    private static void Isolate()
    {RunSaves.UseStore(new RunSaveStore(Path.GetFullPath(".utmp/run-save/runtime-"+Guid.NewGuid().ToString("N")+".json")));}
    private static IEnumerator StartNewAdventure(TitleMenuUI title)
    {
        title.StartGame();
        var selection = Ref<CharacterSelectionUI>(title, "characterSelection");
        yield return TitleMenuTests.WaitForCharacterSelection(selection);
        Ref<Toggle>(selection, "soldierToggle").OnSubmit(null);
        title.ConfirmAdventure();
    }

    [UnityTest]
    public IEnumerator ThornThicketGrantsThreePermanentThornsAtBattleStartAndAfterContinue()
    {
        yield return new EnterPlayMode();
        yield return VerifyThornThicket();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyThornThicket()
    {
        Isolate();
        yield return Load("TitleScene");
        var conduit = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/MapData/ThornThicket.asset");
        var definition = AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        Assert.That(conduit, Is.Not.Null);
        Assert.That(conduit.HudArtwork, Is.Not.Null);
        Assert.That(conduit.HudArtwork.rect.size, Is.EqualTo(new Vector2(256, 256)));
        RunSession session = RunSession.GetOrCreate();
        session.StartNewRun(definition, 41, new[] { MapContentType.Shop, MapContentType.Rest,
            MapContentType.Relic, MapContentType.Event }, conduit);
        yield return WaitMap();
        Assert.That(RunSaves.TryLoad(out RunSaveData saved, out string error), Is.True, error);
        RunRelicData grant = saved.inventory.relics.Single();
        Assert.That(grant.battleStartBuffPower, Is.EqualTo(3));
        Assert.That(RunAssetCatalog.Load().Resolve<StatusEffectData>(grant.battleStartBuff).Type,
            Is.EqualTo(StatusEffectType.Thorns));
        Assert.That(session.SelectOption(0), Is.True, session.SaveError);
        yield return WaitBattle();

        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Player player = battle.GetPlayer();
        Enemy enemy = battle.GetTarget();
        Assert.That(player.StatusEffects.GetBuffPower(StatusEffectType.Thorns), Is.EqualTo(3));
        Assert.That(enemy.StatusEffects.GetBuffPower(StatusEffectType.Thorns), Is.Zero);
        Assert.That(player.StatusEffects.ActiveEffects.Single().IsPermanent, Is.True);
        player.StatusEffects.ReduceRemainingTurns();
        int enemyHP = enemy.GetCurrentHP();
        player.TakeDamage(1, enemy);
        Assert.That(enemy.GetCurrentHP(), Is.EqualTo(enemyHP - 3));
        GameObject slot = GameObject.Find("ConduitArea").transform.Find(conduit.name).gameObject;
        Assert.That(slot.transform.Find("Artwork").GetComponent<Image>().sprite, Is.SameAs(conduit.HudArtwork));

        Assert.That(session.ReturnToTitle(), Is.True, session.SaveError);
        yield return LoadDone("TitleScene");
        Object.Destroy(RunSession.Instance.gameObject);
        yield return null;
        RunRelicInventory.ResetForNewGame();
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
        yield return WaitBattle();
        Assert.That(RunRelicInventory.Contains(conduit), Is.True);
        Assert.That(Object.FindFirstObjectByType<BattleManager>().GetPlayer().StatusEffects.GetBuffPower(StatusEffectType.Thorns),
            Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator AutomaticLoaderReloadsEveryPlayerTurnAndAfterContinue()
    {
        yield return new EnterPlayMode();
        yield return VerifyAutomaticLoader();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyAutomaticLoader()
    {
        Isolate();
        yield return Load("TitleScene");
        var conduit = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/MapData/AutomaticLoader.asset");
        var definition = AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        Assert.That(conduit, Is.Not.Null);
        Assert.That(conduit.Rarity, Is.EqualTo(ConduitRarity.Uncommon));
        Assert.That(conduit.HasEffect, Is.True, "A turn-start-only conduit must be acquirable.");
        Assert.That(conduit.HudArtwork, Is.Not.Null);
        Assert.That(conduit.HudArtwork.rect.size, Is.EqualTo(new Vector2(256, 256)));
        Assert.That(definition.floors[0].uncommonConduitRewards.conduits, Does.Contain(conduit));

        RunSession session = RunSession.GetOrCreate();
        session.StartNewRun(definition, 41, new[] { MapContentType.Shop, MapContentType.Rest,
            MapContentType.Relic, MapContentType.Event }, conduit);
        yield return WaitMap();
        Assert.That(conduit.Acquire(), Is.False);
        Assert.That(session.SelectOption(0), Is.True, session.SaveError);
        yield return WaitBattle();

        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        AmmoSystem ammo = battle.GetPlayer().GetAmmoSystem();
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(1), "The opening turn must load one round.");
        Assert.That(ammo.MaxAmmo, Is.EqualTo(ammo.BaseMaxAmmo));
        var artwork = GameObject.Find("ConduitArea").transform.Find(conduit.name + "/Artwork").GetComponent<Image>();
        Assert.That(artwork.sprite, Is.SameAs(conduit.HudArtwork));

        battle.EndPlayerTurn();
        yield return Until(() => battle.DelayedEffects.CurrentTurn == 1 && battle.CanInspectCards);
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(2));
        ammo.Load(ammo.MaxAmmo);
        battle.EndPlayerTurn();
        yield return Until(() => battle.DelayedEffects.CurrentTurn == 2 && battle.CanInspectCards);
        Assert.That(ammo.CurrentAmmo, Is.EqualTo(ammo.MaxAmmo));

        Assert.That(session.ReturnToTitle(), Is.True, session.SaveError);
        yield return LoadDone("TitleScene");
        Object.Destroy(RunSession.Instance.gameObject);
        yield return null;
        RunRelicInventory.ResetForNewGame();
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
        yield return WaitBattle();
        Assert.That(RunRelicInventory.Contains(conduit), Is.True);
        battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle.GetPlayer().GetAmmoSystem().CurrentAmmo, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator BarrierGeneratorGrantsOneWardPerBattleAndAfterContinue()
    {
        yield return new EnterPlayMode();
        yield return VerifyBarrierGenerator();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyBarrierGenerator()
    {
        Isolate();
        yield return Load("TitleScene");
        var conduit = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/MapData/BarrierGenerator.asset");
        var ward = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/Ward.asset");
        var definition = AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        Assert.That(conduit, Is.Not.Null);
        Assert.That(conduit.Rarity, Is.EqualTo(ConduitRarity.Uncommon));
        Assert.That(conduit.HasEffect, Is.True, "A conduit must be able to grant Ward at battle start.");
        Assert.That(conduit.HudArtwork.rect.size, Is.EqualTo(new Vector2(256, 256)));
        Assert.That(definition.floors[0].uncommonConduitRewards.conduits, Does.Contain(conduit));

        RunSession session = RunSession.GetOrCreate();
        session.StartNewRun(definition, 41, new[] { MapContentType.Shop, MapContentType.Rest,
            MapContentType.Relic, MapContentType.Event }, conduit);
        yield return WaitMap();
        Assert.That(RunSaves.TryLoad(out RunSaveData saved, out string error), Is.True, error);
        RunRelicData grant = saved.inventory.relics.Single();
        Assert.That(RunAssetCatalog.Load().Resolve<StatusEffectData>(grant.battleStartBuff), Is.SameAs(ward));
        Assert.That(grant.battleStartBuffPower, Is.EqualTo(1));
        Assert.That(session.SelectOption(0), Is.True, session.SaveError);
        yield return WaitBattle();

        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Player player = battle.GetPlayer();
        Enemy enemy = battle.GetTarget();
        Assert.That(player.StatusEffects.GetBuffPower(StatusEffectType.Ward), Is.EqualTo(1));
        Assert.That(enemy.StatusEffects.HasStatus(StatusEffectType.Ward), Is.False);
        var artwork = GameObject.Find("ConduitArea").transform.Find(conduit.name + "/Artwork").GetComponent<Image>();
        Assert.That(artwork.sprite, Is.SameAs(conduit.HudArtwork));

        int hp = player.GetCurrentHP();
        player.TakeDamage(7, enemy);
        Assert.That(player.GetCurrentHP(), Is.EqualTo(hp));
        Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Ward), Is.False);
        player.TakeDamage(7, enemy);
        Assert.That(player.GetCurrentHP(), Is.EqualTo(hp - 7));

        Assert.That(session.ReturnToTitle(), Is.True, session.SaveError);
        yield return LoadDone("TitleScene");
        Object.Destroy(RunSession.Instance.gameObject);
        yield return null;
        RunRelicInventory.ResetForNewGame();
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
        yield return WaitBattle();
        Assert.That(RunRelicInventory.Contains(conduit), Is.True);
        player = Object.FindFirstObjectByType<BattleManager>().GetPlayer();
        Assert.That(player.StatusEffects.GetBuffPower(StatusEffectType.Ward), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator FoldingCoverGrantsBlockOnlyOnTheFirstTurnAndAfterContinue()
    {
        yield return new EnterPlayMode();
        yield return VerifyFoldingCover();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyFoldingCover()
    {
        Isolate();
        yield return Load("TitleScene");
        var conduit = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/MapData/FoldingCover.asset");
        var definition = AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        Assert.That(conduit, Is.Not.Null);
        Assert.That(conduit.Rarity, Is.EqualTo(ConduitRarity.Common));
        Assert.That(conduit.HasEffect, Is.True, "A conduit that only grants starting block must be acquirable.");
        Assert.That(conduit.HudArtwork, Is.Not.Null);
        Assert.That(conduit.HudArtwork.rect.size, Is.EqualTo(new Vector2(256, 256)));
        Assert.That(definition.floors[0].commonConduitRewards.conduits, Does.Contain(conduit));

        RunSession session = RunSession.GetOrCreate();
        session.StartNewRun(definition, 41, new[] { MapContentType.Shop, MapContentType.Rest,
            MapContentType.Relic, MapContentType.Event }, conduit);
        yield return WaitMap();
        Assert.That(session.SelectOption(0), Is.True, session.SaveError);
        yield return WaitBattle();

        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Player player = battle.GetPlayer();
        Assert.That(player.GetBlock(), Is.EqualTo(10));
        var artwork = GameObject.Find("ConduitArea").transform.Find(conduit.name + "/Artwork").GetComponent<Image>();
        Assert.That(artwork.sprite, Is.SameAs(conduit.HudArtwork));
        int playerHP = player.GetCurrentHP();
        player.TakeDamage(7, battle.GetTarget());
        Assert.That(player.GetCurrentHP(), Is.EqualTo(playerHP));
        Assert.That(player.GetBlock(), Is.EqualTo(3));
        battle.EndPlayerTurn();
        yield return Until(() => battle.DelayedEffects.CurrentTurn == 1 && battle.CanInspectCards);
        Assert.That(player.GetBlock(), Is.Zero, "Later turns must not receive the opening block bonus.");

        Assert.That(session.ReturnToTitle(), Is.True, session.SaveError);
        yield return LoadDone("TitleScene");
        Object.Destroy(RunSession.Instance.gameObject);
        yield return null;
        RunRelicInventory.ResetForNewGame();
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
        yield return WaitBattle();
        Assert.That(RunRelicInventory.Contains(conduit), Is.True);
        Assert.That(Object.FindFirstObjectByType<BattleManager>().GetPlayer().GetBlock(), Is.EqualTo(10));
    }

    [UnityTest]
    public IEnumerator RustImmunityBlocksStatusApplicationsAndSurvivesContinue()
    {
        yield return new EnterPlayMode();
        yield return VerifyRustImmunity();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyRustImmunity()
    {
        Isolate();
        yield return Load("TitleScene");
        var asset = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/MapData/AntiRustOil.asset");
        var definition = AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        Assert.That(asset, Is.Not.Null);
        Assert.That(asset.Rarity, Is.EqualTo(ConduitRarity.Uncommon));
        Assert.That(asset.RustImmunity, Is.True);
        Assert.That(asset.HudArtwork, Is.Not.Null);
        Assert.That(asset.HudArtwork.rect.size, Is.EqualTo(new Vector2(256, 256)));
        Assert.That(definition.floors[0].uncommonConduitRewards.conduits, Does.Contain(asset));
        Assert.That(RunAssetCatalog.Load().Key(asset), Is.Not.Empty);
        var conduit = Object.Instantiate(asset);
        conduit.name = asset.name;
        var enemyAction = ScriptableObject.CreateInstance<EnemyApplyStatusAction>();
        var attackCard = ScriptableObject.CreateInstance<CardData>();
        var continuousCard = ScriptableObject.CreateInstance<CardData>();
        var damage = ScriptableObject.CreateInstance<DamageEffect>();
        RunAssetCatalog catalog = null;
        try
        {
            catalog = RunSaveTestCatalog.Register(conduit);
            var rust = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/Rust.asset");
            var rustCard = AssetDatabase.LoadAssetAtPath<CardData>("Assets/CardData/Status/Examples/Rust_Player.asset");
            var rustEffect = (ApplyStatusEffect)rustCard.effects.Single();
            var statusAction = new SerializedObject(enemyAction);
            statusAction.FindProperty("status").objectReferenceValue = rust;
            statusAction.ApplyModifiedPropertiesWithoutUndo();
            damage.damage = 10;
            var damageState = new SerializedObject(damage);
            damageState.FindProperty("target").enumValueIndex = (int)CardTarget.Enemy;
            damageState.ApplyModifiedPropertiesWithoutUndo();
            attackCard.description = "{damage:1}";
            attackCard.effects = new CardEffect[] { rustEffect, damage };
            continuousCard.type = CardType.Continuous;
            continuousCard.deckType = CardDeckType.TypeDream;
            continuousCard.effects = new CardEffect[] { rustEffect };

            RunSession session = RunSession.GetOrCreate();
            session.StartNewRun(definition, 41, new[] { MapContentType.Shop, MapContentType.Rest,
                MapContentType.Relic, MapContentType.Event }, conduit);
            yield return WaitMap();
            Assert.That(session.SelectOption(0), Is.True, session.SaveError);
            yield return WaitBattle();

            BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
            Player player = battle.GetPlayer();
            var artwork = GameObject.Find("ConduitArea").transform.Find(conduit.name + "/Artwork").GetComponent<Image>();
            Assert.That(artwork.sprite, Is.SameAs(asset.HudArtwork));
            Assert.That(enemyAction.GetActionDescription(battle), Is.EqualTo("녹슬음 · 면역"));
            enemyAction.Execute(battle);
            rustEffect.Execute(battle);
            Assert.That(player.ContinuousCards.Activate(continuousCard, battle, battle.GetDeckController()), Is.Not.Null);
            Assert.That(player.StatusEffects.HasStatus(StatusEffectType.Rust), Is.False);
            Assert.That(player.StatusEffects.GetRemainingTurns(StatusEffectType.Rust), Is.Zero);
            Assert.That(CardDescriptionBuilder.Build(attackCard, battle), Is.EqualTo("10"));
            Assert.That(battle.GetTarget().StatusEffects.Apply(rust, 2), Is.True);
            var burn = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/Burn.asset");
            Assert.That(player.StatusEffects.Apply(burn, 1), Is.True);

            Assert.That(session.ReturnToTitle(), Is.True, session.SaveError);
            yield return LoadDone("TitleScene");
            Object.Destroy(RunSession.Instance.gameObject);
            yield return null;
            RunRelicInventory.ResetForNewGame();
            var settings = new SerializedObject(conduit);
            settings.FindProperty("rustImmunity").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
            yield return WaitBattle();
            player = Object.FindFirstObjectByType<BattleManager>().GetPlayer();
            Assert.That(player.StatusEffects.Apply(rust, 2), Is.False, "The checkpoint must keep the acquired immunity.");
        }
        finally
        {
            RunAssetCatalog.SetRuntimeCatalog(null);
            Object.Destroy(catalog);
            Object.Destroy(damage);
            Object.Destroy(continuousCard);
            Object.Destroy(attackCard);
            Object.Destroy(enemyAction);
            Object.Destroy(conduit);
        }
    }

    [UnityTest]
    public IEnumerator HalfMoonChaliceFillsDreamGaugeAtBattleStartAndAfterContinue()
    {
        yield return new EnterPlayMode();
        yield return VerifyHalfMoonChalice();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyHalfMoonChalice()
    {
        Isolate();
        yield return Load("TitleScene");
        var chalice = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/MapData/HalfMoonChalice.asset");
        var definition = AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        Assert.That(chalice, Is.Not.Null, "The Half-Moon Chalice asset must exist.");
        Assert.That(chalice.Rarity, Is.EqualTo(ConduitRarity.Uncommon));
        Assert.That(chalice.HasEffect, Is.True);
        Assert.That(chalice.MaxAmmoBonus, Is.Zero);
        Assert.That(AssetDatabase.GetAssetPath(chalice.Artwork), Is.EqualTo("Assets/Images/UI/Conduits/half-moon-chalice.png"));
        Assert.That(definition.floors[0].uncommonConduitRewards.conduits, Does.Contain(chalice));

        RunSession session = RunSession.GetOrCreate();
        session.StartNewRun(definition, 41, new[] { MapContentType.Shop, MapContentType.Rest,
            MapContentType.Relic, MapContentType.Event }, chalice);
        yield return WaitMap();
        Assert.That(session.SelectOption(0), Is.True, session.SaveError);
        yield return WaitBattle();

        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle, Is.Not.Null);
        Assert.That(battle.GetPlayer(), Is.Not.Null);
        CostSystem cost = battle.GetPlayer().GetCostSystem();
        Assert.That(cost, Is.Not.Null);
        string GaugeText(string field)
        {
            var gauge = Ref<CostGaugeUI>(cost, field);
            Assert.That(gauge, Is.Not.Null, field);
            return Ref<TMP_Text>(gauge, "gaugeText").text;
        }
        Assert.That(cost.GetDreamCost(), Is.EqualTo(3));
        Assert.That(cost.GetNightmareCost(), Is.Zero);
        Assert.That(GaugeText("dreamGaugeUI"), Is.EqualTo("5 / 10"));
        Assert.That(GaugeText("nightmareGaugeUI"), Is.EqualTo("0 / 20"));
        var conduitArea = Object.FindFirstObjectByType<ConduitAreaUI>();
        Assert.That(conduitArea, Is.Not.Null);
        Transform artworkTransform = conduitArea.transform.Find(chalice.name + "/Artwork");
        Assert.That(artworkTransform, Is.Not.Null, "The battle HUD must include the chalice artwork.");
        var artwork = artworkTransform.GetComponent<Image>();
        Assert.That(artwork.sprite, Is.SameAs(chalice.HudArtwork));

        battle.EndPlayerTurn();
        yield return Until(() => battle.DelayedEffects.CurrentTurn == 1 && battle.CanInspectCards);
        Assert.That(GaugeText("dreamGaugeUI"), Is.EqualTo("8 / 10"));

        Assert.That(session.ReturnToTitle(), Is.True, session.SaveError);
        yield return LoadDone("TitleScene");
        Object.Destroy(RunSession.Instance.gameObject);
        yield return null;
        RunRelicInventory.ResetForNewGame();
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
        yield return WaitBattle();
        Assert.That(RunRelicInventory.Contains(chalice), Is.True);
        cost = Object.FindFirstObjectByType<BattleManager>().GetPlayer().GetCostSystem();
        Assert.That(GaugeText("dreamGaugeUI"), Is.EqualTo("5 / 10"));
        Assert.That(GaugeText("nightmareGaugeUI"), Is.EqualTo("0 / 20"));
    }

    [UnityTest]
    public IEnumerator DreamBookmarkExpandsOnlyDreamHandAcrossTurnsAndContinue()
    {
        yield return new EnterPlayMode();
        yield return VerifyHandSlotConduit("DreamBookmark", CardDeckType.TypeDream, ConduitRarity.Uncommon);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator NightmareBookmarkExpandsOnlyNightmareHandAcrossTurnsAndContinue()
    {
        yield return new EnterPlayMode();
        yield return VerifyHandSlotConduit("NightmareBookmark", CardDeckType.TypeNightmare, ConduitRarity.Common);
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyHandSlotConduit(string assetName, CardDeckType expandedDeck, ConduitRarity rarity)
    {
        Isolate();
        yield return Load("TitleScene");
        var conduit = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>($"Assets/MapData/{assetName}.asset");
        var definition = AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        Assert.That(conduit, Is.Not.Null);
        Assert.That(conduit.Rarity, Is.EqualTo(rarity));
        Assert.That(conduit.HasEffect, Is.True, "A hand-size-only conduit must be acquirable.");
        Assert.That(conduit.HudArtwork, Is.Not.Null);
        Assert.That(conduit.HudArtwork.rect.size, Is.EqualTo(new Vector2(256, 256)));
        FloorMapDefinition floor = definition.floors[0];
        ConduitRewardDatabase rewards = rarity == ConduitRarity.Common ? floor.commonConduitRewards : floor.uncommonConduitRewards;
        Assert.That(rewards.conduits, Does.Contain(conduit));

        RunSession session = RunSession.GetOrCreate();
        session.StartNewRun(definition, 41, new[] { MapContentType.Shop, MapContentType.Rest,
            MapContentType.Relic, MapContentType.Event }, conduit);
        yield return WaitMap();
        Assert.That(conduit.Acquire(), Is.False);
        Assert.That(session.SelectOption(0), Is.True, session.SaveError);
        yield return WaitBattle();

        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardDeckType otherDeck = expandedDeck == CardDeckType.TypeDream ? CardDeckType.TypeNightmare : CardDeckType.TypeDream;
        CardHandController expandedHand = battle.GetHand(expandedDeck);
        CardHandController otherHand = battle.GetHand(otherDeck);
        Assert.That(expandedHand.GetMaxHandSize(), Is.EqualTo(4));
        Assert.That(expandedHand.GetCurrentHandSize(), Is.EqualTo(4));
        Assert.That(otherHand.GetMaxHandSize(), Is.EqualTo(3));
        Assert.That(otherHand.GetCurrentHandSize(), Is.EqualTo(3));
        Assert.That(battle.DrawCard(expandedDeck, expandedHand), Is.False);
        var artwork = GameObject.Find("ConduitArea").transform.Find(conduit.name + "/Artwork").GetComponent<Image>();
        Assert.That(artwork.sprite, Is.SameAs(conduit.HudArtwork));

        battle.EndPlayerTurn();
        yield return Until(() => battle.DelayedEffects.CurrentTurn == 1 && battle.CanInspectCards);
        Assert.That(expandedHand.GetCurrentHandSize(), Is.EqualTo(4));
        Assert.That(otherHand.GetCurrentHandSize(), Is.EqualTo(3));

        Assert.That(session.ReturnToTitle(), Is.True, session.SaveError);
        yield return LoadDone("TitleScene");
        Object.Destroy(RunSession.Instance.gameObject);
        yield return null;
        RunRelicInventory.ResetForNewGame();
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
        yield return WaitBattle();
        Assert.That(RunRelicInventory.Contains(conduit), Is.True);
        battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle.GetHand(expandedDeck).GetCurrentHandSize(), Is.EqualTo(4));
        Assert.That(battle.GetHand(otherDeck).GetCurrentHandSize(), Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator WeaknessExposureBonusAffectsTimedPlayerApplicationsAndSurvivesContinue()
    {
        yield return new EnterPlayMode();
        yield return VerifyWeaknessExposureBonus();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyWeaknessExposureBonus()
    {
        Isolate();
        yield return Load("TitleScene");
        var conduit = ScriptableObject.CreateInstance<AmmoCapacityRelic>();
        var recurringCard = ScriptableObject.CreateInstance<CardData>();
        var enemyAction = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyApplyStatusAction>("Assets/Enemy/Examples/Debuff.asset"));
        RunAssetCatalog catalog = null;
        try
        {
            var settings = new SerializedObject(conduit);
            settings.FindProperty("maxAmmoBonus").intValue = 0;
            SerializedProperty bonus = settings.FindProperty("weaknessExposureBonusTurns");
            Assert.That(bonus, Is.Not.Null, "A conduit must be able to increase applied Weakness Exposure duration.");
            bonus.intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            catalog = RunSaveTestCatalog.Register(conduit);

            var definition = AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
            var playerCard = AssetDatabase.LoadAssetAtPath<CardData>(
                "Assets/CardData/Soldier/Basic/Starting/Nightmare/Skill/02.Card_Target_Tracking/Target_Tracking.asset");
            var selfEffect = AssetDatabase.LoadAssetAtPath<ApplyStatusEffect>(
                "Assets/CardData/Status/Examples/WeaknessExposure_PlayerEffect.asset");
            CardEffect weakness = playerCard.effects[0];
            Assert.That(CardDescriptionBuilder.Build(playerCard), Does.Contain("2턴"));
            recurringCard.type = CardType.Continuous;
            recurringCard.deckType = CardDeckType.TypeDream;
            recurringCard.continuousDurationType = ContinuousDurationType.FixedTurns;
            recurringCard.continuousTurns = 2;

            RunSession session = RunSession.GetOrCreate();
            session.StartNewRun(definition, 41, new[] { MapContentType.Shop, MapContentType.Rest,
                MapContentType.Relic, MapContentType.Event }, conduit);
            yield return WaitMap();
            Assert.That(session.SelectOption(0), Is.True, session.SaveError);
            yield return WaitBattle();

            BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
            Player player = battle.GetPlayer();
            Enemy enemy = battle.GetTarget();
            Assert.That(CardDescriptionBuilder.Build(playerCard, battle), Does.Contain("3턴"));
            Assert.That(CardDescriptionBuilder.Build(playerCard, battle, false), Does.Contain("2턴"));
            weakness.Execute(battle);
            Assert.That(enemy.StatusEffects.GetRemainingTurns(StatusEffectType.WeaknessExposure), Is.EqualTo(3));
            weakness.Execute(battle);
            Assert.That(enemy.StatusEffects.GetRemainingTurns(StatusEffectType.WeaknessExposure), Is.EqualTo(6));
            selfEffect.Execute(battle);
            Assert.That(player.StatusEffects.GetRemainingTurns(StatusEffectType.WeaknessExposure), Is.EqualTo(2));

            enemy.StatusEffects.Clear();
            var actionSettings = new SerializedObject(enemyAction);
            actionSettings.FindProperty("target").enumValueIndex = (int)EnemyTargetType.Self;
            actionSettings.ApplyModifiedPropertiesWithoutUndo();
            enemyAction.Execute(battle);
            Assert.That(enemy.StatusEffects.GetRemainingTurns(StatusEffectType.WeaknessExposure), Is.EqualTo(2));

            enemy.StatusEffects.Clear();
            recurringCard.effects = new[] { weakness };
            Assert.That(player.ContinuousCards.Activate(recurringCard, battle, battle.GetDeckController()), Is.Not.Null);
            Assert.That(enemy.StatusEffects.GetRemainingTurns(StatusEffectType.WeaknessExposure), Is.EqualTo(2));

            player.ContinuousCards.ResetForBattle();
            Assert.That(enemy.StatusEffects.HasStatus(StatusEffectType.WeaknessExposure), Is.False);
            recurringCard.effects = Array.Empty<CardEffect>();
            recurringCard.turnStartEffects = new[] { weakness };
            recurringCard.description = "{turnStatusTurns:0}턴";
            Assert.That(CardDescriptionBuilder.Build(recurringCard, battle), Is.EqualTo("3턴"));
            Assert.That(player.ContinuousCards.Activate(recurringCard, battle, battle.GetDeckController()), Is.Not.Null);
            battle.EndPlayerTurn();
            yield return Until(() => battle.DelayedEffects.CurrentTurn == 1 && battle.CanInspectCards);
            Assert.That(enemy.StatusEffects.GetRemainingTurns(StatusEffectType.WeaknessExposure), Is.EqualTo(3));

            Assert.That(session.ReturnToTitle(), Is.True, session.SaveError);
            yield return LoadDone("TitleScene");
            Object.Destroy(RunSession.Instance.gameObject);
            yield return null;
            RunRelicInventory.ResetForNewGame();
            settings.FindProperty("weaknessExposureBonusTurns").intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
            yield return WaitBattle();
            battle = Object.FindFirstObjectByType<BattleManager>();
            weakness.Execute(battle);
            Assert.That(battle.GetTarget().StatusEffects.GetRemainingTurns(StatusEffectType.WeaknessExposure), Is.EqualTo(3));
        }
        finally
        {
            RunAssetCatalog.SetRuntimeCatalog(null);
            Object.Destroy(catalog);
            Object.Destroy(enemyAction);
            Object.Destroy(recurringCard);
            Object.Destroy(conduit);
        }
    }

    [UnityTest] public IEnumerator ReturnFromBattleRestartsItsEntryStateAcrossSessionDestruction()
    {yield return new EnterPlayMode();yield return VerifyBattleReturn();yield return new ExitPlayMode();}
    private static IEnumerator VerifyBattleReturn()
    {
        Isolate();yield return Load("TitleScene");TitleMenuUI title=Object.FindFirstObjectByType<TitleMenuUI>();
        Assert.That(Ref<Button>(title,"continueButton").gameObject.activeSelf,Is.False);
        yield return StartNewAdventure(title);yield return WaitMap();Object.FindFirstObjectByType<MapController>().SelectOption(0);yield return WaitBattle();
        BattleManager battle=Object.FindFirstObjectByType<BattleManager>();int hp=battle.GetPlayer().GetCurrentHP();
        int savedGold = RunGoldInventory.Gold;
        string[] hand=battle.GetHand(CardDeckType.TypeDream).GetCardsSnapshot().Select(c=>c.GetCardData().name).ToArray();
        int count=battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count;
        battle.GetPlayer().TakeDamage(13);battle.GetDeckController().AddCardToDeck(battle.GetDeckController().GetDeck(CardDeckType.TypeDream)[0],CardDeckType.TypeDream);RunGoldInventory.Add(44);
        SettingsUI settings=Object.FindFirstObjectByType<SettingsUI>();settings.Toggle();Assert.That(settings.IsOpen,Is.True);
        settings.RequestQuit();settings.ConfirmQuit();settings.ConfirmQuit();yield return LoadDone("TitleScene");
        Assert.That(RunSession.Instance.Progress,Is.Null);title=Object.FindFirstObjectByType<TitleMenuUI>();
        Assert.That(Ref<Button>(title,"continueButton").gameObject.activeSelf,Is.True);
        Object.Destroy(RunSession.Instance.gameObject);yield return null;RunDeckInventory.ResetForNewGame();RunRelicInventory.ResetForNewGame();
        Ref<Button>(title,"continueButton").onClick.Invoke();yield return WaitBattle();battle=Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle.GetPlayer().GetCurrentHP(),Is.EqualTo(hp));
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(savedGold));
        Assert.That(battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count,Is.EqualTo(count));
        Assert.That(battle.GetHand(CardDeckType.TypeDream).GetCardsSnapshot().Select(c=>c.GetCardData().name),Is.EqualTo(hand));
        Assert.That(RunSession.Instance.Progress.Phase,Is.EqualTo(RunPhase.Battle));
    }
    [UnityTest]
    public IEnumerator EmergencyBandageHealsFourOnVictoryAndKeepsRecoveredHealthAcrossContinue()
    {
        yield return new EnterPlayMode();
        yield return VerifyEmergencyBandage();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyEmergencyBandage()
    {
        Isolate();
        yield return Load("TitleScene");
        var bandage = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/MapData/EmergencyBandage.asset");
        var definition = AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        Assert.That(bandage, Is.Not.Null, "The Emergency Bandage asset must exist.");
        Assert.That(bandage.Rarity, Is.EqualTo(ConduitRarity.Common));
        Assert.That(bandage.HasEffect, Is.True);
        Assert.That(bandage.MaxAmmoBonus, Is.Zero);
        Assert.That(AssetDatabase.GetAssetPath(bandage.Artwork), Is.EqualTo("Assets/Images/UI/Conduits/emergency-bandage.png"));
        Assert.That(definition.floors[0].commonConduitRewards.conduits, Does.Contain(bandage));

        RunSession session = RunSession.GetOrCreate();
        session.StartNewRun(definition, 41, new[] { MapContentType.Shop, MapContentType.Rest,
            MapContentType.Relic, MapContentType.Event }, bandage);
        yield return WaitMap();
        Assert.That(session.SelectOption(0), Is.True, session.SaveError);
        yield return WaitBattle();

        var battle = Object.FindFirstObjectByType<BattleManager>();
        Player player = battle.GetPlayer();
        int maxHP = player.GetMaxHP();
        player.TakeDamage(7);
        Assert.That(player.GetCurrentHP(), Is.EqualTo(maxHP - 7));
        battle.GetTarget().TakeDamage(100000);
        Assert.That(player.GetCurrentHP(), Is.EqualTo(maxHP - 3));
        Assert.That(session.Progress.CurrentHP, Is.EqualTo(maxHP - 3));
        Assert.That(RunSaves.TryLoad(out RunSaveData checkpoint, out _), Is.True);
        Assert.That(checkpoint.progress.hp, Is.EqualTo(maxHP - 3));

        Assert.That(session.ReturnToTitle(), Is.True, session.SaveError);
        yield return LoadDone("TitleScene");
        Object.Destroy(RunSession.Instance.gameObject);
        yield return null;
        RunRelicInventory.ResetForNewGame();
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
        yield return LoadDone("BattleScene");

        session = RunSession.Instance;
        battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(RunRelicInventory.Contains(bandage), Is.True);
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(maxHP - 3));
        var rewards = Object.FindFirstObjectByType<BattleRewardSystem>();
        using (var locked = new FileStream(RunSaves.Store.Path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            rewards.SkipRemaining();
            Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Rewards));
            Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(maxHP - 3));
        }
        Assert.That(rewards.SkipRemaining(), Is.True);
        yield return WaitMap();
        Assert.That(session.Progress.CurrentHP, Is.EqualTo(maxHP - 3));

        Assert.That(session.SelectOption(0), Is.True, session.SaveError);
        yield return WaitBattle();
        battle = Object.FindFirstObjectByType<BattleManager>();
        battle.GetTarget().TakeDamage(100000);
        Assert.That(battle.GetPlayer().GetCurrentHP(), Is.EqualTo(maxHP));
        Assert.That(session.Progress.CurrentHP, Is.EqualTo(maxHP));
    }

    [UnityTest]
    public IEnumerator MidasHandBoostsBattleGoldOnceAcrossContinue()
    {
        yield return new EnterPlayMode();
        yield return VerifyMidasHandBattleReward();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyMidasHandBattleReward()
    {
        Isolate();
        yield return Load("TitleScene");
        var midas = AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/MapData/MidasHand.asset");
        var definition = AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        Assert.That(midas, Is.Not.Null, "The Midas's Hand asset must exist.");
        Assert.That(midas.Rarity, Is.EqualTo(ConduitRarity.Uncommon));
        Assert.That(midas.HasEffect, Is.True);
        Assert.That(midas.MaxAmmoBonus, Is.Zero);
        Assert.That(AssetDatabase.GetAssetPath(midas.Artwork), Is.EqualTo("Assets/Images/UI/Conduits/midas-hand.png"));
        Assert.That(definition.floors[0].uncommonConduitRewards.conduits, Does.Contain(midas));

        RunSession session = RunSession.GetOrCreate();
        session.StartNewRun(definition, 41, new[] { MapContentType.Shop, MapContentType.Rest,
            MapContentType.Relic, MapContentType.Event }, midas);
        yield return WaitMap();
        int startingGold = definition.startingGold;
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(startingGold));
        Assert.That(session.SelectOption(0), Is.True, session.SaveError);
        yield return WaitBattle();

        var rewards = Object.FindFirstObjectByType<BattleRewardSystem>();
        int expected = (new System.Random(session.Progress.NodeSeed).Next(20, 26) + 1) * 3 / 2;
        Object.FindFirstObjectByType<BattleManager>().GetTarget().TakeDamage(100000);
        Assert.That(rewards.GoldReward, Is.EqualTo(expected));
        Assert.That(Ref<TMP_Text>(Object.FindFirstObjectByType<CardRewardUI>(), "goldText").text, Does.Contain(expected.ToString()));
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(startingGold));

        Assert.That(session.ReturnToTitle(), Is.True, session.SaveError);
        yield return LoadDone("TitleScene");
        Object.Destroy(RunSession.Instance.gameObject);
        yield return null;
        RunDeckInventory.ResetForNewGame();
        RunRelicInventory.ResetForNewGame();
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();
        yield return LoadDone("BattleScene");

        rewards = Object.FindFirstObjectByType<BattleRewardSystem>();
        Assert.That(RunRelicInventory.Contains(midas), Is.True);
        Assert.That(rewards.GoldReward, Is.EqualTo(expected));
        Assert.That(rewards.ClaimGold(), Is.True);
        Assert.That(rewards.ClaimGold(), Is.False);
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(startingGold + expected));
    }

    [UnityTest] public IEnumerator PartialRewardsAndFixedOffersSurviveResumeWithoutDuplicateClaims()
    {yield return new EnterPlayMode();yield return VerifyRewards();yield return new ExitPlayMode();}
    private static IEnumerator VerifyRewards()
    {
        Isolate();yield return Load("TitleScene");yield return StartNewAdventure(Object.FindFirstObjectByType<TitleMenuUI>());yield return WaitMap();
        Object.FindFirstObjectByType<MapController>().SelectOption(0);yield return WaitBattle();
        BattleManager battle=Object.FindFirstObjectByType<BattleManager>();battle.GetPlayer().TakeDamage(7);battle.GetTarget().TakeDamage(100000);
        BattleRewardSystem rewards=Object.FindFirstObjectByType<BattleRewardSystem>();Assert.That(rewards.ClaimGold(),Is.True);
        Assert.That(rewards.OpenCardReward(),Is.True);Assert.That(rewards.SelectDeck(CardDeckType.TypeDream),Is.True);
        string[] offers=rewards.Offers.Select(c=>c.name).ToArray();int gold=RunGoldInventory.Gold;
        SettingsUI settings=Object.FindFirstObjectByType<SettingsUI>();settings.Toggle();Assert.That(settings.IsOpen,Is.True,"Settings must open during reward selection.");
        settings.RequestQuit();settings.ConfirmQuit();Assert.That(rewards.ChooseCard(0),Is.False,"A departing scene must not grant another reward.");
        yield return LoadDone("TitleScene");Object.Destroy(RunSession.Instance.gameObject);yield return null;RunDeckInventory.ResetForNewGame();
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();yield return LoadDone("BattleScene");
        battle=Object.FindFirstObjectByType<BattleManager>();rewards=Object.FindFirstObjectByType<BattleRewardSystem>();
        Assert.That(battle.Result,Is.EqualTo(BattleResult.Victory));Assert.That(battle.GetHand(CardDeckType.TypeDream).GetCardsSnapshot(),Is.Empty);
        Assert.That(battle.GetPlayer().GetCurrentHP(),Is.EqualTo(73));Assert.That(rewards.State,Is.EqualTo(BattleRewardState.CardChoice));
        Assert.That(rewards.Offers.Select(c=>c.name),Is.EqualTo(offers));Assert.That(RunGoldInventory.Gold,Is.EqualTo(gold));Assert.That(rewards.ClaimGold(),Is.False);
        Assert.That(rewards.ChooseCard(0),Is.True);int deckCount=battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count;
        settings=Object.FindFirstObjectByType<SettingsUI>();settings.Toggle();settings.RequestQuit();settings.ConfirmQuit();yield return LoadDone("TitleScene");
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();yield return LoadDone("BattleScene");
        rewards=Object.FindFirstObjectByType<BattleRewardSystem>();battle=Object.FindFirstObjectByType<BattleManager>();
        Assert.That(rewards.CardAvailable,Is.False);Assert.That(rewards.ChooseCard(0),Is.False);Assert.That(rewards.ClaimGold(),Is.False);
        Assert.That(battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count,Is.EqualTo(deckCount));
        Assert.That(rewards.SkipRemaining(),Is.True);yield return WaitMap();Assert.That(RunSession.Instance.Progress.NodeIndex,Is.EqualTo(1));
        Assert.That(RunSaves.TryLoad(out RunSaveData save,out _),Is.True);Assert.That(save.progress.phase,Is.EqualTo(RunPhase.Map));Assert.That(save.progress.node,Is.EqualTo(1));
    }
    [UnityTest] public IEnumerator FailedTitleReturnKeepsConfirmationOpenAndPreservesTheCheckpoint()
    {yield return new EnterPlayMode();yield return VerifyWriteFailure();yield return new ExitPlayMode();}
    private static IEnumerator VerifyWriteFailure()
    {
        Isolate();yield return Load("TitleScene");yield return StartNewAdventure(Object.FindFirstObjectByType<TitleMenuUI>());yield return WaitMap();
        Object.FindFirstObjectByType<MapController>().SelectOption(0);yield return WaitBattle();
        var settings=Object.FindFirstObjectByType<SettingsUI>();settings.Toggle();settings.RequestQuit();
        string path=RunSaves.Store.Path;string before=File.ReadAllText(path);
        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            settings.ConfirmQuit();Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("BattleScene"));
            Assert.That(settings.IsOpen,Is.True);Assert.That(Ref<TMP_Text>(settings,"navigationError").text,Is.Not.Empty);
        }
        Assert.That(File.ReadAllText(path),Is.EqualTo(before));settings.ConfirmQuit();yield return LoadDone("TitleScene");
    }
    private static T Ref<T>(Object owner,string name) where T:Object=>(T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    private static IEnumerator Load(string name) {AsyncOperation load=SceneManager.LoadSceneAsync(name);yield return Until(()=>load.isDone);yield return null;yield return null;}
    private static IEnumerator LoadDone(string name) {yield return Until(()=>SceneManager.GetActiveScene().name==name&&RunSession.Instance!=null&&!RunSession.Instance.IsTransitioning);yield return null;yield return null;}
    private static IEnumerator WaitMap() {yield return LoadDone("MapScene");}
    private static IEnumerator WaitBattle() {yield return LoadDone("BattleScene");yield return Until(()=>Object.FindFirstObjectByType<BattleManager>()!=null&&Object.FindFirstObjectByType<BattleManager>().CanInspectCards);}
    private static IEnumerator Until(Func<bool> ready) {float end=Time.realtimeSinceStartup+20;while(!ready()){Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;}}
}
