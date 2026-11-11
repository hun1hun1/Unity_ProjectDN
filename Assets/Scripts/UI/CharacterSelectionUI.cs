using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class CharacterSelectionUI : MonoBehaviour
{
    private const float OpeningDuration = .2f;

    [SerializeField] private Toggle soldierToggle;
    [SerializeField] private Sprite soldierSelectedSprite;
    [SerializeField] private Button adventureButton;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject soldierPreview, soldierInfo;
    [SerializeField] private GameObject deckDetails;
    [SerializeField] private TMP_Text dreamSummary, nightmareSummary, dreamCards, nightmareCards, statusText;
    [SerializeField] private CardDatabase dreamDeck, nightmareDeck;

    private CanvasGroup panelGroup;
    private float openingElapsed = OpeningDuration;
    private bool busy;

    internal bool IsOpening => openingElapsed < OpeningDuration;
    public bool SoldierSelected => soldierToggle.isOn;

    private void OnEnable() => soldierToggle.onValueChanged.AddListener(RefreshSelection);

    private void OnDisable()
    {
        soldierToggle.onValueChanged.RemoveListener(RefreshSelection);
        openingElapsed = OpeningDuration;
        if (panelGroup != null)
        {
            panelGroup.alpha = 1f;
            panelGroup.interactable = true;
        }
    }

    public void Open()
    {
        panelGroup ??= GetComponent<CanvasGroup>();
        openingElapsed = 0f;
        panelGroup.alpha = 0f;
        panelGroup.interactable = false;
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

    private void Update()
    {
        if (!IsOpening) return;

        openingElapsed = Mathf.Min(OpeningDuration, openingElapsed + Time.unscaledDeltaTime);
        float remaining = 1f - openingElapsed / OpeningDuration;
        panelGroup.alpha = 1f - remaining * remaining;
        if (!IsOpening) panelGroup.interactable = true;
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

