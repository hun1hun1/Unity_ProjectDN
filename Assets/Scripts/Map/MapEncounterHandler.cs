using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MapEncounterHandler : MapContentHandler
{
    [SerializeField] private MapContentType contentType = MapContentType.Event;
    [SerializeField] private RectTransform canvas;
    [SerializeField] private TMP_FontAsset font;

    private GameObject panel;
    private TMP_Text title;
    private TMP_Text description;
    private TMP_Text rewardName;
    private RectTransform rewardArtworkFrame;
    private Image rewardArtwork;
    private TMP_Text errorText;
    private Button acceptButton;
    private Button leaveButton;
    private TMP_Text acceptText;
    private Action reward;
    private RunNodeVisit visit;
    private Func<bool> complete;

    public override MapContentType ContentType => contentType;

    public override void Begin(MapChoice choice, RunProgress progress, Func<bool> complete)
    {
        if (panel == null) BuildPanel();
        visit = CaptureVisit(progress);
        this.complete = complete;
        reward = null;
        errorText.text = "";

        if (contentType == MapContentType.Relic)
        {
            rewardName.text = "";
            rewardArtworkFrame.gameObject.SetActive(false);
        }

        if (contentType == MapContentType.Event && choice.ContentData is MapEventDefinition mapEvent)
        {
            title.text = mapEvent.title;
            description.text = mapEvent.description;
            if (mapEvent.goldReward > 0)
            {
                int gold = RunGoldInventory.GetGoldReward(mapEvent.goldReward);
                acceptText.text = $"골드 {gold} 획득";
                reward = () => RunGoldInventory.Add(gold);
            }
        }
        else if (contentType == MapContentType.Relic && choice.ContentData is ConduitData relic)
        {
            title.text = "매개체 발견";
            rewardName.text = $"{relic.RelicName} · {RarityLabel(relic.Rarity)}";
            rewardArtwork.sprite = relic.Artwork;
            rewardArtworkFrame.gameObject.SetActive(relic.Artwork != null);
            bool owned = RunRelicInventory.Contains(relic);
            description.text = owned ? $"{relic.Description}\n이미 보유한 매개체입니다." : relic.Description;
            if (!owned)
            {
                acceptText.text = "매개체 획득";
                reward = () =>
                {
                    if (!relic.Acquire()) throw new InvalidOperationException("The relic could not be acquired.");
                };
            }
        }
        else
        {
            title.text = MapChoiceCardUI.DefaultTitle(contentType);
            description.text = "이곳에는 더 이상 남은 것이 없습니다.";
        }

        acceptButton.gameObject.SetActive(reward != null);
        if (contentType == MapContentType.Relic)
        {
            var leaveRect = (RectTransform)leaveButton.transform;
            leaveRect.anchoredPosition = new Vector2(reward == null ? 0 : 156, leaveRect.anchoredPosition.y);
        }
        panel.transform.SetAsLastSibling();
        panel.SetActive(true);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject((reward == null ? leaveButton : acceptButton).gameObject);
    }

    private static string RarityLabel(ConduitRarity rarity) => rarity switch
    {
        ConduitRarity.Uncommon => "고급",
        ConduitRarity.Rare => "희귀",
        _ => "일반"
    };

    private void Finish(bool accept)
    {
        if (complete == null || !panel.activeInHierarchy || DeckOverviewUI.BlocksBattleInput ||
            SettingsUI.BlocksBattleInput || CardInspectionUI.BlocksGameplayInput) return;

        bool success = accept ? RunSession.Instance.CompleteContentWithAcquisition(visit, reward) : complete();
        if (!success)
        {
            errorText.text = "진행 저장에 실패했습니다. 다시 시도해 주세요.";
            return;
        }

        complete = null;
        reward = null;
        panel.SetActive(false);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (panel != null) panel.SetActive(false);
    }

    private void BuildPanel()
    {
        RectTransform overlay = MakeRect(canvas, contentType + "EncounterPanel", Vector2.zero, Vector2.zero);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.gameObject.AddComponent<Image>().color = new Color(.03f, .04f, .07f, .82f);
        panel = overlay.gameObject;
        panel.SetActive(false);

        bool isConduit = contentType == MapContentType.Relic;
        RectTransform body = MakeRect(overlay, "Content", new Vector2(780, isConduit ? 660 : 460), Vector2.zero);
        body.gameObject.AddComponent<Image>().color = new Color(.105f, .14f, .20f);
        var border = body.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(.63f, .49f, .29f);
        border.effectDistance = new Vector2(2, -2);

        title = MakeText(body, "Title", new Vector2(700, 72), new Vector2(0, isConduit ? 265 : 155), 50);
        if (isConduit)
        {
            rewardArtworkFrame = MakeRect(body, "RewardArtwork", new Vector2(400, 232), new Vector2(0, 105));
            var artworkBackground = rewardArtworkFrame.gameObject.AddComponent<Image>();
            artworkBackground.color = new Color(.06f, .085f, .125f);
            artworkBackground.raycastTarget = false;
            var artworkBorder = rewardArtworkFrame.gameObject.AddComponent<Outline>();
            artworkBorder.effectColor = new Color(.48f, .38f, .24f);
            artworkBorder.effectDistance = new Vector2(1, -1);
            rewardArtwork = MakeRect(rewardArtworkFrame, "Artwork", new Vector2(386, 218), Vector2.zero)
                .gameObject.AddComponent<Image>();
            rewardArtwork.preserveAspect = true;
            rewardArtwork.raycastTarget = false;
            rewardName = MakeText(body, "RewardName", new Vector2(700, 54), new Vector2(0, -53), 38);
            rewardName.color = new Color(.97f, .83f, .55f);
        }

        description = MakeText(body, "Description", new Vector2(670, isConduit ? 86 : 150),
            new Vector2(0, isConduit ? -126 : 38), 30);
        errorText = MakeText(body, "Error", new Vector2(670, 46), new Vector2(0, isConduit ? -193 : -65), 24);
        errorText.color = new Color(1f, .63f, .53f);

        float buttonY = isConduit ? -254 : -151;
        acceptButton = MakeButton(body, "Accept", new Vector2(-156, buttonY), () => Finish(true), out acceptText);
        if (isConduit) acceptButton.targetGraphic.color = new Color(.46f, .32f, .16f);
        leaveButton = MakeButton(body, "Leave", new Vector2(156, buttonY), () => Finish(false), out TMP_Text leaveText);
        leaveText.text = "지나가기";
        acceptButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = leaveButton };
        leaveButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = acceptButton };
    }

    private Button MakeButton(RectTransform parent, string name, Vector2 position, Action click, out TMP_Text label)
    {
        RectTransform rect = MakeRect(parent, name, new Vector2(282, 76), position);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.24f, .30f, .40f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => click());
        label = MakeText(rect, "Label", new Vector2(264, 72), Vector2.zero, 31);
        return button;
    }

    private TMP_Text MakeText(RectTransform parent, string name, Vector2 size, Vector2 position, float fontSize)
    {
        var text = MakeRect(parent, name, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 20;
        text.fontSizeMax = fontSize;
        text.color = new Color(.96f, .90f, .79f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform MakeRect(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }
}
