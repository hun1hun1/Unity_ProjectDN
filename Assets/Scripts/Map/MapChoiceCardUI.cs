using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[Serializable]
public sealed class MapChoicePresentation
{
    public MapContentType type;
    public Sprite artwork;
    public string title;
    [TextArea] public string description;
}

[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.Button))]
public class MapChoiceCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float HoverDuration = 0.15f;
    private const float HoverLift = 5f;

    [SerializeField] private UnityEngine.UI.Image artwork;
    [SerializeField] private TMP_Text artworkPlaceholder;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text description;
    [SerializeField] private UnityEngine.UI.Image combatIcon;

    private UnityEngine.UI.Button button;
    private Vector2 artworkPosition;
    private bool pointerInside;
    private float hoverProgress;

    internal UnityEngine.UI.Image Artwork => artwork;

    private void Awake()
    {
        button = GetComponent<UnityEngine.UI.Button>();
        if (artwork != null) artworkPosition = artwork.rectTransform.anchoredPosition;
    }

    public void OnPointerEnter(PointerEventData eventData) => pointerInside = true;

    public void OnPointerExit(PointerEventData eventData) => pointerInside = false;

    private void LateUpdate()
    {
        if (!pointerInside && hoverProgress == 0f) return;

        if (!button.isActiveAndEnabled || !button.IsInteractable() || DeckOverviewUI.BlocksBattleInput ||
            SettingsUI.BlocksBattleInput || CardInspectionUI.BlocksGameplayInput)
        {
            SetHoverProgress(0f);
            return;
        }

        float target = pointerInside ? 1f : 0f;
        SetHoverProgress(Mathf.MoveTowards(hoverProgress, target, Time.unscaledDeltaTime / HoverDuration));
    }

    private void OnDisable() => ResetHover();

    internal void ResetHover()
    {
        pointerInside = false;
        SetHoverProgress(0f);
    }

    private void SetHoverProgress(float progress)
    {
        if (hoverProgress == progress) return;
        hoverProgress = progress;
        if (artwork != null)
        {
            float offset = Mathf.SmoothStep(0f, HoverLift, progress);
            artwork.rectTransform.anchoredPosition = artworkPosition + Vector2.up * offset;
        }
    }

    public void SetContent(string name, string details, Sprite sprite, bool supported, bool showCombatIcon = false)
    {
        title.text = name + (supported ? "" : " · 준비 중");
        description.text = details;
        artwork.sprite = sprite;
        artwork.enabled = sprite != null;
        artworkPlaceholder.gameObject.SetActive(sprite == null);
        if (combatIcon != null) combatIcon.enabled = showCombatIcon;
    }

    public static string DefaultTitle(MapContentType type)
    {
        switch (type)
        {
            case MapContentType.NormalBattle: return "일반 전투";
            case MapContentType.EliteBattle: return "엘리트 전투";
            case MapContentType.BossBattle: return "보스 전투";
            case MapContentType.Shop: return "상점";
            case MapContentType.Relic: return "매개체";
            case MapContentType.Event: return "이벤트";
            case MapContentType.Upgrade: return "강화";
            case MapContentType.Rest: return "휴식";
            default: return type.ToString();
        }
    }

    public static string DefaultDescription(MapContentType type)
    {
        switch (type)
        {
            case MapContentType.NormalBattle: return "적과 전투하고\n승리 보상을 획득합니다.";
            case MapContentType.EliteBattle: return "강력한 적과의 전투에\n도전합니다.";
            case MapContentType.BossBattle: return "이 층의 보스에게\n도전합니다.";
            case MapContentType.Shop: return "골드로 필요한 물품을\n구매할 수 있습니다.";
            case MapContentType.Relic: return "런에 도움을 주는\n매개체를 획득합니다.";
            case MapContentType.Event: return "특별한 사건을 만나\n선택을 내립니다.";
            case MapContentType.Upgrade: return "보유한 카드를\n강화할 수 있습니다.";
            case MapContentType.Rest: return "휴식으로 체력을\n회복할 수 있습니다.";
            default: return "";
        }
    }
}
