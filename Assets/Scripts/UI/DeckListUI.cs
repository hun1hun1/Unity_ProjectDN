using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeckListUI : MonoBehaviour
{
    [SerializeField]
    private Transform content;

    [SerializeField]
    private DeckCardUI cardPrefab;

    [SerializeField]
    private GameObject panel;

    private readonly List<DeckCardUI> displayedCards = new List<DeckCardUI>();
    private CardHandController sourceHand;
    private GridLayoutGroup grid;
    private Vector2 originalSpacing;
    private Vector2 appliedCardSize = new Vector2(-1f, -1f);

    private void LateUpdate()
    {
        if (panel.activeInHierarchy) RefreshCardSizes();
    }

    public void ShowCards(IReadOnlyList<CardData> cards, CardHandController hand = null)
    {
        if (CardInspectionUI.BlocksGameplayInput) return;
        Clear();

        sourceHand = hand;
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

            cardUI.SetCard(cardData, sourceHand != null ? sourceHand.BattleContext : null);
            displayedCards.Add(cardUI);
        }

        Canvas.ForceUpdateCanvases();
        RefreshCardSizes(true);
    }

    private void RefreshCardSizes(bool force = false)
    {
        if (grid == null)
        {
            grid = content.GetComponent<GridLayoutGroup>();
            if (grid == null) return;
            originalSpacing = grid.spacing;
        }

        Vector2 prefabSize = ((RectTransform)cardPrefab.transform).rect.size;
        Vector2 size = sourceHand != null ? sourceHand.GetHandCardSize() : prefabSize;
        if (!force && size == appliedCardSize) return;

        appliedCardSize = size;
        float ratio = prefabSize.y > 0f ? size.y / prefabSize.y : 1f;
        grid.cellSize = size;
        grid.spacing = originalSpacing * ratio;
        foreach (DeckCardUI card in displayedCards)
            if (card != null) card.ResizeForList(ratio);

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);
    }

    public void Close()
    {
        if (CardInspectionUI.BlocksGameplayInput) return;
        panel.SetActive(false);
        Clear();
        sourceHand = null;
    }

    private void Clear()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            // Destroy is deferred; hide old entries before rebuilding the grid.
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
        displayedCards.Clear();
    }
}