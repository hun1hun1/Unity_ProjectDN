using UnityEngine;
using UnityEngine.UI;

public class DeckPileButton : MonoBehaviour
{
    [SerializeField]
    private DeckController deckController;

    [SerializeField]
    private DeckListUI deckListUI;

    [SerializeField]
    private CardDeckType deckType;

    [SerializeField]
    private bool isDiscardPile;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (isDiscardPile)
        {
            deckListUI.ShowCards(deckController.GetDiscardPile(deckType));
        }
        else
        {
            deckListUI.ShowCards(deckController.GetDrawPile(deckType));
        }
    }
}
