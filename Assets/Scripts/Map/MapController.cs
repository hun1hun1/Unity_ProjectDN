using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MapController : MonoBehaviour
{
    [SerializeField] private RunDefinition runDefinition;
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text resourcesText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text nodeTemplate;
    [SerializeField] private Transform nodeContainer;
    [SerializeField] private UnityEngine.UI.Button[] choiceButtons;
    [SerializeField] private MapChoicePresentation[] choicePresentations = Array.Empty<MapChoicePresentation>();
    [SerializeField] private MapContentHandler[] contentHandlers = Array.Empty<MapContentHandler>();
    private readonly List<TMP_Text> nodeLabels = new List<TMP_Text>();
    private RunSession session;
    public RunDefinition Definition => runDefinition;

    private void Awake() => session = RunSession.GetOrCreate();
    private void OnEnable() { session.Changed += Refresh; }
    private void OnDisable() { if (session != null) session.Changed -= Refresh; }
    private void Start()
    {
        if (session.Progress == null)
        {
            var available = new List<MapContentType>();
            foreach (MapContentHandler handler in contentHandlers)
                if (handler != null && handler.isActiveAndEnabled) available.Add(handler.ContentType);
            session.StartNewRun(runDefinition, Environment.TickCount, available);
        }
        if(session.Progress.Phase==RunPhase.Content)
        {
            RunProgress expected=session.Progress;int floor=expected.FloorIndex,node=expected.NodeIndex;
            MapContentHandler handler=FindHandler(expected.CurrentNode.SelectedChoice.Type);
            if(handler!=null)
            {
                string saved=session.SavedContentState;
                handler.Begin(expected.CurrentNode.SelectedChoice,expected,()=>session.CompleteContent(expected,floor,node));
                handler.RestoreState(saved);
            }
            else statusText.text="저장된 노드를 처리할 기능이 연결되지 않았습니다.";
        }
        Refresh();
    }
    private MapContentHandler FindHandler(MapContentType type)
    {
        foreach (MapContentHandler handler in contentHandlers)
            if (handler != null && handler.isActiveAndEnabled && handler.ContentType == type) return handler;
        return null;
    }
    public bool CanChoose(int index)
    {
        RunProgress state = session.Progress;
        if (session.IsTransitioning || state == null || state.Phase != RunPhase.Map || state.CurrentNode == null ||
            index < 0 || index >= state.CurrentNode.Choices.Count) return false;
        MapChoice choice = state.CurrentNode.Choices[index];
        return choice.IsBattle ? Application.CanStreamedLevelBeLoaded(state.Definition.battleScene) : FindHandler(choice.Type) != null;
    }
    public void SelectOption(int index)
    {
        if (DeckOverviewUI.BlocksBattleInput || SettingsUI.BlocksBattleInput ||
            CardInspectionUI.BlocksGameplayInput || !CanChoose(index)) return;
        RunProgress expected = session.Progress;
        int floor = expected.FloorIndex, node = expected.NodeIndex;
        MapChoice choice = expected.CurrentNode.Choices[index];
        MapContentHandler handler = choice.IsBattle ? null : FindHandler(choice.Type);
        if (!session.SelectOption(index)) return;
        if (choice.IsBattle && index < choiceButtons.Length)
            StartCoroutine(HighlightBattleChoice(index));
        if (handler != null)
        {
            handler.Begin(choice, expected, () => session.CompleteContent(expected, floor, node));
            if (ReferenceEquals(expected, session.Progress) && expected.Phase == RunPhase.Content)
            {
                session.SaveCheckpoint(contentState: handler.CaptureState());
                if (index < choiceButtons.Length)
                    handler.PlayEntrance(choiceButtons, index);
            }
        }
    }
    private IEnumerator HighlightBattleChoice(int index)
    {
        var selection = new MapChoiceHighlight(choiceButtons, index, new Color(0.67f, 0.79f, 1f));
        try
        {
            for (float elapsed = 0f; elapsed < MapChoiceHighlight.Duration; elapsed += Time.unscaledDeltaTime)
            {
                selection.SetStrength(1f - elapsed / MapChoiceHighlight.Duration);
                yield return null;
            }
        }
        finally { selection.Restore(); }
    }

    private void Refresh()
    {
        RunProgress state = session.Progress;
        if (state == null) return;
        heading.text = state.Phase == RunPhase.Completed ? "런 완료" : state.IsFinished ? "런 종료" : "진행 경로";
        progressText.text = $"{state.FloorIndex + 1}층 · {Math.Min(state.NodeIndex + 1, state.Nodes.Count)} / {state.Nodes.Count}";
        string health = $"HP {state.CurrentHP} / {state.MaxHP}";
        string gold = $"골드 {RunGoldInventory.Gold}";
        resourcesText.text = goldText == null ? $"{health}     {gold}" : health;
        if (goldText != null) goldText.text = gold;
        statusText.text = state.Phase == RunPhase.Completed ? $"총 {state.CompletedNodes}개 노드를 완료했습니다." :
            !string.IsNullOrEmpty(session.SaveError) ? "진행 저장에 실패했습니다. 다시 시도해 주세요." :
            state.Phase == RunPhase.Defeated ? "전투에서 패배하여 런이 종료되었습니다." :
            state.Phase == RunPhase.Cancelled ? "전투가 중단되어 런이 종료되었습니다." :
            session.IsTransitioning ? "이동 중입니다." : state.Phase == RunPhase.Content ? "선택한 장소를 이용 중입니다." : "진행할 행동을 선택하세요.";
        var grid = nodeContainer.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        if (nodeLabels.Count != state.Nodes.Count)
        {
            foreach (TMP_Text label in nodeLabels)
            {
                // Deferred destruction must not leave old items participating in this frame's layout.
                label.gameObject.SetActive(false);
                Destroy(label.gameObject);
            }
            nodeLabels.Clear();
            if (grid != null) grid.constraintCount = Math.Min(10, state.Nodes.Count);
            for (int i = 0; i < state.Nodes.Count; i++)
            {
                TMP_Text label = Instantiate(nodeTemplate, nodeContainer);
                label.name = "Node" + (i + 1); label.gameObject.SetActive(true); nodeLabels.Add(label);
            }
        }
        int columns = grid != null ? Math.Max(1, grid.constraintCount) : Math.Max(1, nodeLabels.Count);
        for (int i = 0; i < nodeLabels.Count; i++)
        {
            bool done = state.Nodes[i].Status == MapNodeStatus.Completed;
            bool current = !state.IsFinished && i == state.NodeIndex;
            nodeLabels[i].text = $"{i + 1}\n{(done ? "완료" : current ? "현재" : "대기")}";
            nodeLabels[i].color = done ? new Color(.4f, .8f, .65f) : current ? new Color(1f, .83f, .45f) : new Color(.55f, .6f, .7f);
            if (nodeLabels[i].TryGetComponent(out MapRouteNodeUI node))
                node.SetState(done, current, i % columns != 0);
        }
        var progressScroll = nodeContainer.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        if (progressScroll != null && grid != null)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)nodeContainer);
            int current = Math.Min(state.NodeIndex, state.Nodes.Count - 1);
            float offset = current / columns * (grid.cellSize.y + grid.spacing.y);
            float maximum = Mathf.Max(0, progressScroll.content.rect.height - progressScroll.viewport.rect.height);
            progressScroll.verticalNormalizedPosition = maximum > 0 ? 1 - Mathf.Clamp(offset, 0, maximum) / maximum : 1;
        }
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool visible = !state.IsFinished && state.CurrentNode != null && i < state.CurrentNode.Choices.Count;
            choiceButtons[i].gameObject.SetActive(visible);
            if (!visible) continue;
            MapChoice choice = state.CurrentNode.Choices[i];
            bool supported = choice.IsBattle || FindHandler(choice.Type) != null;
            MapChoicePresentation presentation = FindPresentation(choice.Type);
            string title = presentation != null && !string.IsNullOrWhiteSpace(presentation.title)
                ? presentation.title : MapChoiceCardUI.DefaultTitle(choice.Type);
            string description = presentation != null && !string.IsNullOrWhiteSpace(presentation.description)
                ? presentation.description : MapChoiceCardUI.DefaultDescription(choice.Type);
            MapChoiceCardUI card = choiceButtons[i].GetComponent<MapChoiceCardUI>();
            if (card != null)
                card.SetContent(title, description, presentation?.artwork, supported, choice.Type == MapContentType.NormalBattle);
            else choiceButtons[i].GetComponentInChildren<TMP_Text>().text = title + (supported ? "" : " · 준비 중");
            bool canChoose = CanChoose(i);
            choiceButtons[i].interactable = canChoose;
            if (!canChoose && card != null) card.ResetHover();
        }
    }
    private MapChoicePresentation FindPresentation(MapContentType type)
    {
        foreach (MapChoicePresentation presentation in choicePresentations)
            if (presentation != null && presentation.type == type) return presentation;
        return null;
    }
}
