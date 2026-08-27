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

    public BattleManager battleManager;

    public void SetCard(CardData data)
    {
        artwork.sprite = data.artwork;
        costText.text = data.cost.ToString();
        nameText.text = data.cardName;
        descriptionText.text = data.description;
        typeText.text = data.type.ToString();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2)
        {
            UseCard();
        }
    }

    private void UseCard()
    {
        if (cardData == null) return;

        if (battleManager != null)
        {
            battleManager.UseCard(cardData);
        }
    }
}