using System;
using TMPro;
using UnityEngine;

[Serializable]
public sealed class MapChoicePresentation
{
    public MapContentType type;
    public Sprite artwork;
    public string title;
    [TextArea] public string description;
}

[DisallowMultipleComponent]
public class MapChoiceCardUI : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image artwork;
    [SerializeField] private TMP_Text artworkPlaceholder;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text description;

    public void SetContent(string name, string details, Sprite sprite, bool supported)
    {
        title.text = name + (supported ? "" : " · 준비 중");
        description.text = details;
        artwork.sprite = sprite;
        artwork.enabled = sprite != null;
        artworkPlaceholder.gameObject.SetActive(sprite == null);
    }

    public static string DefaultTitle(MapContentType type)
    {
        switch (type)
        {
            case MapContentType.NormalBattle: return "일반 전투";
            case MapContentType.EliteBattle: return "엘리트 전투";
            case MapContentType.BossBattle: return "보스 전투";
            case MapContentType.Shop: return "상점";
            case MapContentType.Relic: return "유물";
            case MapContentType.Event: return "이벤트";
            case MapContentType.Upgrade: return "강화";
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
            case MapContentType.Relic: return "런에 도움을 주는\n유물을 획득합니다.";
            case MapContentType.Event: return "특별한 사건을 만나\n선택을 내립니다.";
            case MapContentType.Upgrade: return "보유한 카드를\n강화할 수 있습니다.";
            default: return "";
        }
    }
}
