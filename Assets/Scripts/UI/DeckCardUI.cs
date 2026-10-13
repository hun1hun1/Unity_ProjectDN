using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckCardUI : MonoBehaviour
{
    [SerializeField] private Image artwork;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text typeText;

    private CardDescriptionBinding descriptionBinding;

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

    public void SetCard(CardData data, BattleManager manager = null)
    {
        GetComponent<DeckCardHoverUI>()?.ResetHover();
        artwork.sprite = data.artwork;
        if (data.deckType == CardDeckType.TypeDream)
        {
            costText.text = data.cost.dreamCost.ToString();
        }
        if (data.deckType == CardDeckType.TypeNightmare)
        {
            costText.text = data.cost.nightmareCost.ToString();
        }
        nameText.text = data.cardName;
        if (descriptionBinding == null)
            descriptionBinding = new CardDescriptionBinding(text =>
            {
                if (descriptionText != null) descriptionText.text = text;
            });
        descriptionBinding.Bind(data, manager, isActiveAndEnabled);
        typeText.text = data.type.ToString();
    }
}