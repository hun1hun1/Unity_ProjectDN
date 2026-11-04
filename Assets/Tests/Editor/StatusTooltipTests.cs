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
