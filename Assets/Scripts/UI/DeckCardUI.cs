using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DeckCardUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image artwork;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text typeText;

    [Header("Card Rarity Images")]
    [SerializeField] private UnityEngine.UI.Image rarityImage;
    [SerializeField] private Sprite commonImage;
    [SerializeField] private Sprite uncommonImage;
    [SerializeField] private Sprite rareImage;

    [SerializeField] private bool allowInspection = true;
    private CardData cardData;
    private BattleManager battleContext;
    private CardDescriptionBinding descriptionBinding;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (allowInspection && eventData.button == PointerEventData.InputButton.Right)
            CardInspectionUI.TryOpen(cardData, battleContext);
    }

    private void OnEnable() { descriptionBinding?.Enable(); }
    private void OnDisable() { descriptionBinding?.Disable(); }
    private void OnDestroy() { descriptionBinding?.Disable(); }

    private TMP_Text[] cardTexts;
    private float[] originalFontSizes;

    public void ResizeForList(float ratio)
    {
        if (cardTexts == null)
        {
            cardTexts = GetComponentsInChildren<TMP_Text>(true);
            originalFontSizes = new float[cardTexts.Length];
            for (int i = 0; i < cardTexts.Length; i++)
                originalFontSizes[i] = cardTexts[i].fontSize;
        }

        for (int i = 0; i < cardTexts.Length; i++)
            cardTexts[i].fontSize = originalFontSizes[i] * ratio;
    }

    public void SetCard(CardData data, BattleManager manager = null, bool applyStatusModifiers = true)
    {
        cardData = data;
        battleContext = manager;
        GetComponent<DeckCardHoverUI>()?.ResetHover();
        artwork.sprite = data.artwork;
        if (data.deckType == CardDeckType.TypeDream)
        {
            costText.text = data.CostLabel;
        }
        if (data.deckType == CardDeckType.TypeNightmare)
        {
            costText.text = data.CostLabel;
        }
        nameText.text = data.cardName;
        if (descriptionBinding == null)
            descriptionBinding = new CardDescriptionBinding(text =>
            {
                if (descriptionText != null) descriptionText.text = text;
            });
        descriptionBinding.Bind(data, manager, isActiveAndEnabled, applyStatusModifiers);
        typeText.text = data.type.ToString();
        if (rarityImage != null)
            rarityImage.sprite = data.rarity == CardRarity.Common ? commonImage
                : data.rarity == CardRarity.Uncommon ? uncommonImage : rareImage;
    }
}