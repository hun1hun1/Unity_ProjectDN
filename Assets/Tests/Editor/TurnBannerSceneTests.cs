using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class TurnBannerSceneTests
{
    [UnityTearDown]
    public IEnumerator ExitBattle()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PlayerAndEnemyActOnlyAfterTheirAnnouncementsDisappear()
    {
        yield return new EnterPlayMode();
        yield return VerifyAnnouncements();
    }

    private static IEnumerator VerifyAnnouncements()
    {
        var loading = SceneManager.LoadSceneAsync("BattleScene");
        while (!loading.isDone) yield return null;
        yield return null;

        var battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle, Is.Not.Null);
        var banner = GameObject.Find("TurnBanner");
        Assert.That(banner, Is.Not.Null, "BattleScene needs a turn banner.");
        var group = banner.GetComponent<CanvasGroup>();
        Assert.That(group, Is.Not.Null);
        var image = banner.GetComponent<Image>();
        var label = banner.GetComponentInChildren<TextMeshProUGUI>();
        Assert.That(label, Is.Not.Null);
        Assert.That(label.font, Is.SameAs(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/Hahmlet-SemiBold SDF.asset")));
        var card = AssetDatabase.LoadAssetAtPath<CardData>(
            "Assets/CardData/Soldier/Basic/Starting/Dream/Attack/01.Card_Attack/Attack.asset");

        yield return Until(() => label.text == "내 차례 · 1턴" && group.alpha > 0f, () => $"Opening banner: {label.text}, alpha {group.alpha}, blocked {battle.BlocksCardInteraction}");
        label.ForceMeshUpdate();
        Assert.That(label.GetParsedText(), Is.EqualTo("내 차례 · 1턴"));
        Assert.That(battle.CanUseCard(card), Is.False);
        Assert.That(battle.CanInspectCards, Is.False);
        var playerSprite = image.sprite;
        Assert.That(playerSprite, Is.Not.Null);
        battle.EndPlayerTurn(); // A click through the opening announcement must be ignored.
        yield return Until(() => battle.CanInspectCards);
        Assert.That(group.alpha, Is.Zero);
        Assert.That(battle.CanUseCard(card), Is.True);

        battle.ConfigureEncounter(AssetDatabase.LoadAssetAtPath<BattleEncounterDefinition>(
            "Assets/MapData/NormalEncounter.asset"));
        var player = battle.GetPlayer();
        int startingHP = player.GetCurrentHP();
        float alphaAtEnemyAttack = -1f;
        player.HealthChanged += _ => alphaAtEnemyAttack = group.alpha;
        battle.EndPlayerTurn();

        yield return Until(() => label.text == "적 차례" && group.alpha > 0f, () => $"Enemy banner: {label.text}, alpha {group.alpha}");
        label.ForceMeshUpdate();
        Assert.That(label.GetParsedText(), Is.EqualTo("적 차례"));
        Assert.That(image.sprite, Is.Not.Null.And.Not.SameAs(playerSprite));
        Assert.That(player.GetCurrentHP(), Is.EqualTo(startingHP));
        Assert.That(battle.CanUseCard(card), Is.False);

        yield return Until(() => label.text == "내 차례 · 2턴" && group.alpha > 0f, () => $"Second turn banner: {label.text}, alpha {group.alpha}");
        Assert.That(player.GetCurrentHP(), Is.LessThan(startingHP));
        Assert.That(alphaAtEnemyAttack, Is.Zero);
        Assert.That(battle.CanUseCard(card), Is.False);
        yield return Until(() => battle.CanInspectCards);
        Assert.That(group.alpha, Is.Zero);
        Assert.That(battle.CanUseCard(card), Is.True);
    }

    [UnityTest]
    public IEnumerator EndingBattleWhileAnnouncementIsVisibleHidesItImmediately()
    {
        yield return new EnterPlayMode();
        yield return VerifyBattleEnd();
    }

    private static IEnumerator VerifyBattleEnd()
    {
        var loading = SceneManager.LoadSceneAsync("BattleScene");
        while (!loading.isDone) yield return null;
        yield return null;

        var battle = Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle, Is.Not.Null);
        var banner = GameObject.Find("TurnBanner");
        Assert.That(banner, Is.Not.Null);
        var group = banner.GetComponent<CanvasGroup>();
        Assert.That(group, Is.Not.Null);
        yield return Until(() => group.alpha > 0f, () => $"Banner alpha {group.alpha}, blocked {battle.BlocksCardInteraction}");
        battle.EndBattle();
        Assert.That(group.alpha, Is.Zero);
        yield return null;
        Assert.That(group.alpha, Is.Zero);
    }

    [UnityTest]
    public IEnumerator FirstTurnWaitsForTheBattleSceneTransition()
    {
        yield return new EnterPlayMode();
        yield return VerifySceneTransition();
    }

    private static IEnumerator VerifySceneTransition()
    {
        var previousStore = RunSaves.Store;
        RunSaves.UseStore(new RunSaveStore(Path.GetFullPath(
            $".utmp/turn-announcement/run-{Guid.NewGuid():N}.json")));
        var session = RunSession.GetOrCreate();
        try
        {
            var definition = AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
            session.StartNewRun(definition, 41, new[]
            {
                MapContentType.Shop, MapContentType.Rest, MapContentType.Relic, MapContentType.Event
            });
            yield return Until(() => SceneManager.GetActiveScene().name == "MapScene" && !session.IsTransitioning);
            Assert.That(session.SelectOption(0), Is.True, session.SaveError);
            yield return Until(() => SceneManager.GetActiveScene().name == "BattleScene");
            yield return null;

            var banner = GameObject.Find("TurnBanner");
            Assert.That(banner, Is.Not.Null);
            var group = banner.GetComponent<CanvasGroup>();
            var battle = Object.FindFirstObjectByType<BattleManager>();
            Assert.That(battle, Is.Not.Null);
            Assert.That(session.IsTransitioning, Is.True, "The battle should be active before the fog has cleared.");
            while (session.IsTransitioning)
            {
                Assert.That(group.alpha, Is.Zero);
                Assert.That(battle.BlocksCardInteraction, Is.True);
                yield return null;
            }

            var label = banner.GetComponentInChildren<TextMeshProUGUI>();
            yield return Until(() => group.alpha > 0f && label.text == "내 차례 · 1턴");
            Assert.That(battle.BlocksCardInteraction, Is.True);
            yield return Until(() => battle.CanInspectCards);
            Assert.That(group.alpha, Is.Zero);
        }
        finally
        {
            if (session != null) Object.DestroyImmediate(session.gameObject);
            RunSaves.UseStore(previousStore);
            RunDeckInventory.ResetForNewGame();
            RunRelicInventory.ResetForNewGame();
        }
    }

    private static IEnumerator Until(Func<bool> condition, Func<string> describe = null)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup >= deadline) Assert.Fail(describe?.Invoke() ?? "The expected turn phase did not arrive.");
            yield return null;
        }
    }
}
