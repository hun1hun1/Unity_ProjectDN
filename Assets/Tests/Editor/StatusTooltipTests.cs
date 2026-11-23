using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class StatusTooltipTests
{
    [Test]
    public void PrefabsAndDefinitionsSupportHoverWithoutBlockingThePointer()
    {
        GameObject icon = AssetDatabase.LoadAssetAtPath<GameObject>(StatusAssetSetup.IconPath);
        Assert.That(icon.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
        GameObject tooltip = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/StatusTooltip.prefab");
        Assert.That(tooltip.GetComponent<StatusTooltipUI>(), Is.Not.Null);
        Assert.That(tooltip.activeSelf, Is.False);
        Assert.That(tooltip.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        foreach (UnityEngine.UI.Graphic graphic in tooltip.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            Assert.That(graphic.raycastTarget, Is.False);
        Assert.That(tooltip.GetComponentInChildren<TMP_Text>(true).font, Is.Not.Null);
        foreach (string path in new[] { "Assets/Prefabs/Player/Soldier.prefab", "Assets/Prefabs/UI/CharacterInfoUI.prefab" })
        {
            StatusEffectsUI ui = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<StatusEffectsUI>(true);
            Assert.That(new SerializedObject(ui).FindProperty("tooltipPrefab").objectReferenceValue,
                Is.EqualTo(tooltip.GetComponent<StatusTooltipUI>()));
        }
        Assert.That(Definition("Rust").Description, Does.Contain("25%"));
        Assert.That(Definition("WeaknessExposure").Description, Does.Contain("50%"));
    }

    [UnityTest]
    public IEnumerator BattleSceneHoverUpdatesTransfersAndClosesOnExitExpiryAndDisable()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return null;
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Player player = battle.GetPlayer();
        Enemy enemy = battle.GetTarget();
        player.StatusEffects.Apply(Definition("Rust"), 2);
        enemy.StatusEffects.Apply(Definition("WeaknessExposure"), 1);
        Canvas.ForceUpdateCanvases();
        yield return null; // Allow the newly spawned graphics to receive their Canvas render depth.
        StatusIconUI playerIcon = player.GetComponentInChildren<StatusIconUI>();
        StatusIconUI enemyIcon = enemy.GetComponentInChildren<StatusIconUI>();
        Canvas canvas = playerIcon.GetComponentInParent<Canvas>().rootCanvas;
        StatusTooltipUI[] tooltips = canvas.GetComponentsInChildren<StatusTooltipUI>(true);
        Assert.That(tooltips, Has.Length.EqualTo(1), "Both characters must share one tooltip.");
        StatusTooltipUI tooltip = tooltips[0];
        TMP_Text text = tooltip.GetComponentInChildren<TMP_Text>(true);
        Assert.That(tooltip.gameObject.activeSelf, Is.False);
        var pointer = new PointerEventData(EventSystem.current);
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, playerIcon.transform.position);
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits.Count, Is.GreaterThan(0));
        Assert.That(hits[0].gameObject, Is.EqualTo(playerIcon.gameObject),
            $"Icon depth: {playerIcon.GetComponent<UnityEngine.UI.Image>().depth}, pointer: {pointer.position}");
        ExecuteEvents.Execute(playerIcon.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        Assert.That(tooltip.gameObject.activeSelf, Is.True);
        Assert.That(text.text, Does.StartWith("<color=#F3D58A>녹슬음</color>\n").And.Contain("25%").And.Contain("남은 턴: 2"));
        player.StatusEffects.Apply(Definition("Rust"), 1);
        Assert.That(text.text, Does.Contain("남은 턴: 3"));
        player.StatusEffects.ReduceRemainingTurns();
        Assert.That(text.text, Does.Contain("남은 턴: 2"));
        hits.Clear();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits[0].gameObject, Is.EqualTo(playerIcon.gameObject), "Tooltip must not intercept hover.");

        // Moving near every corner must keep the tooltip inside the scaled Canvas.
        foreach (Vector2 position in new[] { Vector2.zero, new Vector2(Screen.width, 0),
            new Vector2(0, Screen.height), new Vector2(Screen.width, Screen.height) })
        {
            pointer.position = position;
            ExecuteEvents.Execute(playerIcon.gameObject, pointer, ExecuteEvents.pointerMoveHandler);
            Canvas.ForceUpdateCanvases();
            AssertInsideCanvas(tooltip, canvas);
        }
        ExecuteEvents.Execute(enemyIcon.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(playerIcon.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        Assert.That(tooltip.gameObject.activeSelf, Is.True, "Previous owner's exit must not hide the new tooltip.");
        Assert.That(text.text, Does.Contain("약점 노출").And.Contain("50%"));
        enemy.StatusEffects.ReduceRemainingTurns();
        Assert.That(tooltip.gameObject.activeSelf, Is.False, "Expiry must close the hovered tooltip.");
        ExecuteEvents.Execute(playerIcon.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(playerIcon.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        Assert.That(tooltip.gameObject.activeSelf, Is.False);
        ExecuteEvents.Execute(playerIcon.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        playerIcon.GetComponentInParent<StatusEffectsUI>().gameObject.SetActive(false);
        Assert.That(tooltip.gameObject.activeSelf, Is.False, "Disabling the HUD must close its tooltip.");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ConduitHoverEnlargesOnlyArtAndSharesTheStatusTooltip()
    {
        yield return new EnterPlayMode();
        AsyncOperation load = SceneManager.LoadSceneAsync("BattleScene");
        while (!load.isDone) yield return null;
        yield return null;

        var area = GameObject.Find("ConduitArea").transform;
        Assert.That(area.childCount, Is.Zero);
        var casing = AssetDatabase.LoadAssetAtPath<ConduitData>("Assets/MapData/SoldierStartingCasing.asset");
        Assert.That(casing.Acquire(), Is.True);
        var background = area.Find("ConduitBackground");
        Assert.That(background, Is.Not.Null);
        Assert.That(background.GetSiblingIndex(), Is.Zero);
        Assert.That(background.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout, Is.True);
        var frame = background.GetComponent<UnityEngine.UI.Image>();
        var statusFrame = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/StatusTooltip.prefab").GetComponent<UnityEngine.UI.Image>();
        Assert.That(frame.sprite, Is.SameAs(statusFrame.sprite));
        Assert.That(frame.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced));
        Assert.That(frame.pixelsPerUnitMultiplier, Is.EqualTo(8f));
        Assert.That(frame.color.a, Is.EqualTo(.30f));
        Assert.That(frame.raycastTarget, Is.False);
        Assert.That(frame.rectTransform.rect.size, Is.EqualTo(new Vector2(88, 84)));
        Assert.That(frame.rectTransform.anchoredPosition.x, Is.EqualTo(12));
        var second = AssetDatabase.LoadAssetAtPath<ConduitData>("Assets/MapData/WeaknessEngraver.asset");
        var third = AssetDatabase.LoadAssetAtPath<ConduitData>("Assets/MapData/MerchantGuildToken.asset");
        Assert.That(second.Acquire(), Is.True);
        Assert.That(third.Acquire(), Is.True);
        Assert.That(frame.rectTransform.rect.size, Is.EqualTo(new Vector2(240, 84)));
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        Player player = battle.GetPlayer();
        player.StatusEffects.Apply(Definition("Rust"), 2);
        StatusIconUI status = player.GetComponentInChildren<StatusIconUI>();
        var slot = (RectTransform)area.Find(casing.name);
        var artwork = (RectTransform)slot.Find("Artwork");
        Assert.That(artwork, Is.Not.Null);
        Canvas canvas = slot.GetComponentInParent<Canvas>().rootCanvas;
        StatusTooltipUI tooltip = canvas.GetComponentInChildren<StatusTooltipUI>(true);
        TMP_Text text = tooltip.GetComponentInChildren<TMP_Text>(true);
        Canvas.ForceUpdateCanvases();
        yield return null;

        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, slot.position)
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits.Count, Is.GreaterThan(0));
        Assert.That(hits[0].gameObject, Is.SameAs(slot.gameObject));

        ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        Assert.That(tooltip.gameObject.activeSelf, Is.False);
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!tooltip.gameObject.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(tooltip.gameObject.activeSelf, Is.True);
        Assert.That(canvas.GetComponentsInChildren<StatusTooltipUI>(true), Has.Length.EqualTo(1));
        Assert.That(text.text, Does.Contain(casing.RelicName).And.Contain(casing.Description));
        Assert.That(artwork.localScale.x, Is.EqualTo(1.10f).Within(.001f));
        Assert.That(slot.localScale, Is.EqualTo(Vector3.one));
        Assert.That(slot.rect.size, Is.EqualTo(new Vector2(64, 64)));
        var corners = new Vector3[4];
        slot.GetWorldCorners(corners);
        Assert.That(tooltip.transform.position.y, Is.LessThan(corners[0].y));
        AssertInsideCanvas(tooltip, canvas);

        ExecuteEvents.Execute(status.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        Assert.That(tooltip.gameObject.activeSelf, Is.True);
        Assert.That(text.text, Does.Contain("녹슬음"));
        deadline = Time.realtimeSinceStartup + 5f;
        while (!artwork.localScale.Equals(Vector3.one) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(artwork.localScale, Is.EqualTo(Vector3.one));

        ExecuteEvents.Execute(status.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        deadline = Time.realtimeSinceStartup + 5f;
        while (!tooltip.gameObject.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(tooltip.gameObject.activeSelf, Is.True);
        RunRelicInventory.ResetForNewGame();
        Assert.That(background.gameObject.activeSelf, Is.False);
        Assert.That(tooltip.gameObject.activeSelf, Is.False);
        Assert.That(artwork.localScale, Is.EqualTo(Vector3.one));
        yield return new ExitPlayMode();
    }

    private static StatusEffectData Definition(string name) =>
        AssetDatabase.LoadAssetAtPath<StatusEffectData>($"Assets/StatusData/{name}.asset");

    private static void AssertInsideCanvas(StatusTooltipUI tooltip, Canvas canvas)
    {
        RectTransform root = (RectTransform)canvas.transform;
        var corners = new Vector3[4];
        ((RectTransform)tooltip.transform).GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 local = root.InverseTransformPoint(corner);
            Assert.That(local.x, Is.InRange(root.rect.xMin - 0.1f, root.rect.xMax + 0.1f));
            Assert.That(local.y, Is.InRange(root.rect.yMin - 0.1f, root.rect.yMax + 0.1f));
        }
    }
}
