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
    private const float HoverMistDuration = .21f;

    [SerializeField] private UnityEngine.UI.Image artwork;
    [SerializeField] private TMP_Text artworkPlaceholder;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text description;
    [SerializeField] private UnityEngine.UI.Image combatIcon;

    private UnityEngine.UI.Button button;
    private Vector2 artworkPosition;
    private bool pointerInside;
    private float hoverProgress;
    private float hoverMistElapsed = HoverMistDuration;
    private bool revealing;
    private MapContentType presentationType;
    private string placeholderText;
    private MapChoiceEffects effects;

    internal UnityEngine.UI.Image Artwork => artwork;

    private void Awake()
    {
        button = GetComponent<UnityEngine.UI.Button>();
        if (artwork != null) artworkPosition = artwork.rectTransform.anchoredPosition;
        placeholderText = artworkPlaceholder != null ? artworkPlaceholder.text : "";
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!pointerInside) hoverMistElapsed = 0f;
        pointerInside = true;
    }

    public void OnPointerExit(PointerEventData eventData) => pointerInside = false;

    private void LateUpdate()
    {
        if (revealing || (!pointerInside && hoverProgress == 0f)) return;

        if (!button.isActiveAndEnabled || !button.IsInteractable() || DeckOverviewUI.BlocksBattleInput ||
            SettingsUI.BlocksBattleInput || CardInspectionUI.BlocksGameplayInput)
        {
            SetHoverProgress(0f);
            hoverMistElapsed = HoverMistDuration;
            effects?.Clear();
            return;
        }

        float target = pointerInside ? 1f : 0f;
        SetHoverProgress(Mathf.MoveTowards(hoverProgress, target, Time.unscaledDeltaTime / HoverDuration));

        if (artwork == null) return;
        if (presentationType == MapContentType.Relic)
        {
            if (hoverProgress > 0f) Effects.SetConduitRim(hoverProgress);
            else effects?.Clear();
        }
        else if (presentationType == MapContentType.Unknown)
        {
            if (pointerInside && hoverMistElapsed < HoverMistDuration)
            {
                float progress = hoverMistElapsed / HoverMistDuration;
                Effects.SetMist(0f, progress, Mathf.Sin(progress * Mathf.PI) * .72f, behindArtwork: true);
                hoverMistElapsed += Time.unscaledDeltaTime;
            }
            else effects?.Clear();
        }
    }

    private MapChoiceEffects Effects => effects ??= new MapChoiceEffects(artwork);

    private void OnDisable()
    {
        revealing = false;
        ResetHover();
    }

    private void OnDestroy() => effects?.Dispose();

    internal void ResetHover()
    {
        pointerInside = false;
        hoverMistElapsed = HoverMistDuration;
        SetHoverProgress(0f);
        if (!revealing) effects?.Clear();
    }

    internal void BeginReveal()
    {
        ResetHover();
        revealing = true;
    }

    internal void ShowRevealMist(float cover, float drift, float opacity)
    {
        if (artwork != null) Effects.SetMist(cover, drift, opacity, behindArtwork: false);
    }

    internal void ShowConduitLight(float progress)
    {
        if (artwork != null) Effects.SetConduitLight(progress);
    }

    internal void EndReveal()
    {
        revealing = false;
        effects?.Clear();
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

    public void SetContent(string name, string details, Sprite sprite, bool supported, bool showCombatIcon = false,
        MapContentType type = MapContentType.NormalBattle, string resultMarker = null)
    {
        if (presentationType != type && !revealing) ResetHover();
        presentationType = type;
        title.text = name + (supported ? "" : " · 준비 중");
        description.text = details;
        artwork.sprite = sprite;
        artwork.enabled = sprite != null;
        placeholderText ??= artworkPlaceholder.text;
        artworkPlaceholder.text = resultMarker ?? placeholderText;
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
            case MapContentType.Unknown: return "미지";
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
            case MapContentType.Relic: return "매개체를 확인하고\n획득 여부를 선택합니다.";
            case MapContentType.Event: return "특별한 사건을 만나\n선택을 내립니다.";
            case MapContentType.Upgrade: return "보유한 카드를\n강화할 수 있습니다.";
            case MapContentType.Rest: return "휴식으로 체력을\n회복할 수 있습니다.";
            case MapContentType.Unknown: return "선택하면 결과가\n공개됩니다.";
            default: return "";
        }
    }
}
