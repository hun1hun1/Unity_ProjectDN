using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class MapChoiceLayoutTests
{
    private RunDefinition previewRun;
    private FloorMapDefinition previewFloor;
    private RunAssetCatalog previewCatalog;
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (RunSession.Instance != null) Object.Destroy(RunSession.Instance.gameObject);
        if (previewRun != null) Object.Destroy(previewRun);
        if (previewFloor != null) Object.Destroy(previewFloor);
        RunAssetCatalog.SetRuntimeCatalog(null);
        if(previewCatalog!=null)Object.Destroy(previewCatalog);
        if (Application.isPlaying) { yield return null; yield return new ExitPlayMode(); }
        RunDeckInventory.ResetForNewGame(); RunRelicInventory.ResetForNewGame();
    }

    [UnityTest] public IEnumerator ChoicesUseCenteredHorizontalCardsWithArtworkTitleAndDescription()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MapScene"); yield return null; yield return null;
        Transform row = GameObject.Find("Canvas/Content/Choices").transform;
        var horizontal = row.GetComponent<HorizontalLayoutGroup>();
        Assert.That(horizontal, Is.Not.Null, "Node choices must be laid out horizontally.");
        Assert.That(horizontal.childAlignment, Is.EqualTo(TextAnchor.MiddleCenter));
        Assert.That(row.GetComponent<VerticalLayoutGroup>(), Is.Null);
        Transform card = row.Find("Choice1");
        RectTransform rect = (RectTransform)card;
        Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(300));
        Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(380));
        Assert.That(card.Find("ArtworkFrame/Artwork").GetComponent<Image>(), Is.Not.Null);
        Assert.That(card.Find("Title").GetComponent<TMP_Text>().text, Is.EqualTo("일반 전투"));
        Assert.That(card.Find("Description").GetComponent<TMP_Text>().text, Is.Not.Empty);
        Assert.That(card.Find("ArtworkFrame/Artwork").GetComponent<Image>().raycastTarget, Is.False);
        Assert.That(card.GetComponent<Button>().interactable, Is.True);
        Vector3 center = rect.TransformPoint(rect.rect.center);
        RectTransform canvas = (RectTransform)row.GetComponentInParent<Canvas>().transform;
        Vector3 screenCenter = canvas.TransformPoint(canvas.rect.center);
        Assert.That(center.x, Is.EqualTo(screenCenter.x).Within(1));
        Assert.That(center.y, Is.EqualTo(screenCenter.y).Within(1));

        // Authoring data must drive separate labels and replace, rather than retain, old art.
        Image artwork = card.Find("ArtworkFrame/Artwork").GetComponent<Image>();
        Assert.That(artwork.sprite, Is.Not.Null);
        var config = new SerializedObject(Object.FindFirstObjectByType<MapController>());
        var presentation = config.FindProperty("choicePresentations").GetArrayElementAtIndex(0);
        presentation.FindPropertyRelative("title").stringValue = "검증용 노드";
        presentation.FindPropertyRelative("description").stringValue = "사용자 지정 설명";
        presentation.FindPropertyRelative("artwork").objectReferenceValue = null;
        config.ApplyModifiedPropertiesWithoutUndo();
        RunSession.Instance.StartNewRun(RunSession.Instance.Progress.Definition, 12);
        Assert.That(card.Find("Title").GetComponent<TMP_Text>().text, Is.EqualTo("검증용 노드"));
        Assert.That(card.Find("Description").GetComponent<TMP_Text>().text, Is.EqualTo("사용자 지정 설명"));
        Assert.That(artwork.sprite, Is.Null); Assert.That(artwork.enabled, Is.False);
        Assert.That(card.Find("ArtworkFrame/ArtworkPlaceholder").gameObject.activeSelf, Is.True);
    }

    [UnityTest] public IEnumerator OneTwoAndThreeChoicesStayCenteredAndDoNotOverlap()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MapScene"); yield return null; yield return null;
        RunSession session = RunSession.Instance;
        previewRun = Object.Instantiate(session.Progress.Definition);
        previewCatalog=RunSaveTestCatalog.Register(previewRun);
        previewFloor = Object.Instantiate(previewRun.floors[0]);
        previewRun.floors = new[] { previewFloor };
        previewRun.enabledContent = (MapContentType[])System.Enum.GetValues(typeof(MapContentType));
        previewFloor.mode = FloorMapMode.PlannedFloor; previewFloor.nodeCount = 6;
        previewFloor.eliteBattles = previewFloor.bossBattles = previewFloor.normalBattles;
        previewFloor.nonBattleContents = new[] { new WeightedMapContent { type = MapContentType.Shop, weight = 1 } };
        for (int count = 1; count <= 3; count++)
        {
            previewFloor.nonBattleChance = count >= 2 ? 1 : 0;
            previewFloor.eliteChance = count == 3 ? 1 : 0;
            session.StartNewRun(previewRun, 9, new[] { MapContentType.Relic, MapContentType.Shop });
            RunProgress progress = session.Progress;
            progress.TrySelectOption(0);
            session.RecordBattleResult(progress, 0, 0, BattleResult.Victory, 80, 80);
            session.CompleteBattle(progress, 0, 0, 80, 80);
            float until = Time.realtimeSinceStartup + 20;
            while (session.IsTransitioning && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(session.IsTransitioning, Is.False);
            yield return null; yield return null;
            Transform row = GameObject.Find("Canvas/Content/Choices").transform;
            Canvas.ForceUpdateCanvases();
            var rowCorners = new Vector3[4]; ((RectTransform)row).GetWorldCorners(rowCorners);
            float firstCenter = 0, lastCenter = 0, previousRight = float.NegativeInfinity;
            int active = 0;
            for (int i = 0; i < 3; i++)
            {
                Transform card = row.Find("Choice" + (i + 1));
                if (!card.gameObject.activeSelf) continue;
                active++;
                var corners = new Vector3[4]; ((RectTransform)card).GetWorldCorners(corners);
                Assert.That(corners[0].x, Is.GreaterThan(previousRight)); previousRight = corners[2].x;
                Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(rowCorners[0].x - 1));
                Assert.That(corners[2].x, Is.LessThanOrEqualTo(rowCorners[2].x + 1));
                float center = (corners[0].x + corners[2].x) / 2;
                if (i == 0) firstCenter = center;
                lastCenter = center;
                Assert.That(card.Find("Description").GetComponent<TMP_Text>().text, Is.Not.Empty);
            }
            Assert.That(active, Is.EqualTo(count));
            Assert.That((firstCenter + lastCenter) / 2, Is.EqualTo((rowCorners[0].x + rowCorners[2].x) / 2).Within(1));
            MapSceneTests.CaptureMap("map-card-" + count + ".png");
        }
    }

    [UnityTest] public IEnumerator LongerFloorsKeepProgressInsideItsViewportAndRevealTheCurrentNode()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MapScene"); yield return null; yield return null;
        RunSession session = RunSession.Instance;
        previewRun = Object.Instantiate(session.Progress.Definition);
        previewCatalog=RunSaveTestCatalog.Register(previewRun);
        previewFloor = Object.Instantiate(previewRun.floors[0]); previewRun.floors = new[] { previewFloor };
        for (int count = 12; count <= 21; count += 9)
        {
            previewFloor.nodeCount = count; session.StartNewRun(previewRun, 19);
            MapController map = Object.FindFirstObjectByType<MapController>();
            var config = new SerializedObject(map);
            Transform nodes = (Transform)config.FindProperty("nodeContainer").objectReferenceValue;
            ScrollRect scroll = nodes.GetComponentInParent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null, "Long floors need a bounded progress viewport.");
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            var clip = new Vector3[4]; scroll.viewport.GetWorldCorners(clip);
            var status = new Vector3[4]; ((RectTransform)GameObject.Find("Status").transform).GetWorldCorners(status);
            Assert.That(clip[2].y, Is.LessThan(status[0].y));
            RunProgress progress = session.Progress;
            for (int i = 0; i < count - 1; i++)
            {
                progress.TrySelectOption(0); progress.RecordBattleResult(BattleResult.Victory); progress.CompleteNode(0, i);
            }
            progress.TrySelectOption(0);
            session.RecordBattleResult(progress, 0, count - 1, BattleResult.Victory, 80, 80);
            Canvas.ForceUpdateCanvases();
            var current = (RectTransform)nodes.Find("Node" + count);
            Vector3 center = current.TransformPoint(current.rect.center);
            Assert.That(center.y, Is.InRange(clip[0].y, clip[2].y));
            MapSceneTests.CaptureMap("map-card-progress-" + count + ".png");
        }
    }
}
