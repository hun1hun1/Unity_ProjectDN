using TMPro;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image artwork;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text typeText;

    private CardData cardData;
    private BattleManager battleManager;
    private CardHandController handController;

    public void SetCard(CardData data, BattleManager manager)
    {
        cardData = data;
        battleManager = manager;

        artwork.sprite = data.artwork;
        costText.text = data.cost.ToString();
        nameText.text = data.cardName;
        descriptionText.text = data.description;
        typeText.text = data.type.ToString();
    }

    public void SetHandController(CardHandController controller)
    {
        handController = controller;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"Card Clicked: {eventData.clickCount}");

        if (eventData.clickCount == 2)
        {
            Debug.Log("더블클릭 감지");
            UseCard();
        }
    }

    private void UseCard()
    {
        Debug.Log("UseCard 호출");

        if (cardData == null)
        {
            Debug.LogError("cardData가 null입니다.");
            return;
        }

        if (battleManager == null)
        {
            Debug.LogError("battleManager가 null입니다.");
            return;
        }

        Debug.Log($"카드 사용 요청: {cardData.cardName}");

        battleManager.UseCard(this);
    }

    public CardData GetCardData()
    {
        return cardData;
    }

    public CardHandController GetHandController()
    {
        return handController;
    }
}
