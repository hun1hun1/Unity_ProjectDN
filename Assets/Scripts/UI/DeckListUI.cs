using System.Collections.Generic;
using UnityEngine;

public class DeckListUI : MonoBehaviour
{
    [SerializeField]
    private Transform content;

    [SerializeField]
    private DeckCardUI cardPrefab;

    [SerializeField]
    private GameObject panel;

    public void ShowCards(IReadOnlyList<CardData> cards)
    {
        Clear();

        panel.SetActive(true);

        foreach (CardData cardData in cards)
        {
            DeckCardUI cardUI = Instantiate(cardPrefab, content);

            cardUI.SetCard(cardData);
        }
    }

    public void Close()
    {
        panel.SetActive(false);
        Clear();
    }

    private void Clear()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }
    }
}