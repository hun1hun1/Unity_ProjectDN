using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectionUI : MonoBehaviour
{
    [SerializeField] private Toggle soldierToggle;
    [SerializeField] private Sprite soldierSelectedSprite;
    [SerializeField] private Button adventureButton;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject soldierPreview, soldierInfo;
    [SerializeField] private GameObject deckDetails;
    [SerializeField] private TMP_Text dreamSummary, nightmareSummary, dreamCards, nightmareCards, statusText;
    [SerializeField] private CardDatabase dreamDeck, nightmareDeck;

    private bool busy;

    public bool SoldierSelected => soldierToggle.isOn;

    private void OnEnable() => soldierToggle.onValueChanged.AddListener(RefreshSelection);

    private void OnDisable() => soldierToggle.onValueChanged.RemoveListener(RefreshSelection);

    public void Open()
    {
        soldierToggle.SetIsOnWithoutNotify(false);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        statusText.text = string.Empty;
        SetBusy(false);

        var dream = dreamDeck.GetAllCards();
        var nightmare = nightmareDeck.GetAllCards();
        dreamSummary.text = $"꿈 시작 덱 · {dream.Count}장";
        nightmareSummary.text = $"악몽 시작 덱 · {nightmare.Count}장";
        dreamCards.text = "꿈 · " + FormatDeck(dream);
        nightmareCards.text = "악몽 · " + FormatDeck(nightmare);
    }

    public void ToggleDeckDetails() => deckDetails.SetActive(!deckDetails.activeSelf);

    public void SetBusy(bool busy)
    {
        this.busy = busy;
        soldierToggle.interactable = !busy;
        backButton.interactable = !busy;
        RefreshSelection(SoldierSelected);
    }

    private void RefreshSelection(bool selected)
    {
        soldierToggle.image.overrideSprite = selected ? soldierSelectedSprite : null;
        adventureButton.interactable = selected && !busy;
        soldierPreview.SetActive(selected);
        soldierInfo.SetActive(selected);
        if (!selected)
            deckDetails.SetActive(false);
    }

    public void ShowError(string message)
    {
        statusText.text = message;
        SetBusy(false);
    }

    private static string FormatDeck(System.Collections.Generic.IReadOnlyList<CardData> cards)
    {
        return string.Join(" · ", cards.GroupBy(card => card).Select(group =>
            group.Count() == 1 ? group.Key.cardName : $"{group.Key.cardName} ×{group.Count()}"));
    }
}

