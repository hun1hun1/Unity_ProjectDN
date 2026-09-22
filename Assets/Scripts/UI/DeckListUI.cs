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

        // 원본 DrawPile은 건드리지 않고 복사
        List<CardData> shuffledCards = new List<CardData>(cards);

        // Fisher-Yates Shuffle
        for (int i = shuffledCards.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

            CardData temp = shuffledCards[i];
            shuffledCards[i] = shuffledCards[randomIndex];
            shuffledCards[randomIndex] = temp;
        }

        foreach (CardData cardData in shuffledCards)
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