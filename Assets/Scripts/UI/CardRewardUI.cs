using System.Collections.Generic;
using UnityEngine;

public class CardRewardUI : MonoBehaviour
{
    [Header("Reward System")]
    [SerializeField]
    private CardRewardSystem rewardSystem;

    [Header("Panels")]
    [SerializeField]
    private GameObject deckSelectPanel;

    [SerializeField]
    private GameObject cardRewardPanel;

    [Header("Reward Cards")]
    [SerializeField]
    private DeckCardUI[] rewardCardSlots;

    public void ShowReward()
    {
        if (CardInspectionUI.BlocksGameplayInput) return;
        deckSelectPanel.SetActive(true);
        cardRewardPanel.SetActive(false);
    }

    public void SelectDream()
    {
        if (CardInspectionUI.BlocksGameplayInput) return;
        ShowCardRewards(CardDeckType.TypeDream);
    }

    public void SelectNightmare()
    {
        if (CardInspectionUI.BlocksGameplayInput) return;
        ShowCardRewards(CardDeckType.TypeNightmare);
    }

    private void ShowCardRewards(CardDeckType deckType)
    {
        deckSelectPanel.SetActive(false);
        cardRewardPanel.SetActive(true);

        List<CardData> rewards =
            rewardSystem.GenerateRewards(
                deckType,
                rewardCardSlots.Length
            );

        for (int i = 0; i < rewardCardSlots.Length; i++)
        {
            if (i < rewards.Count)
            {
                rewardCardSlots[i].gameObject.SetActive(true);
                rewardCardSlots[i].SetCard(rewards[i]);
            }
            else
            {
                rewardCardSlots[i].gameObject.SetActive(false);
            }
        }
    }
}