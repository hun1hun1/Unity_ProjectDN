using UnityEngine;

public enum CardType
{
    Attack,
    Skill,
    Continuous
}

[CreateAssetMenu(fileName = "CardData", menuName = "Card/CardData")]
public class CardData : ScriptableObject
{
    public string cardName;
    public int cost;
    public Sprite artwork;

    [TextArea]
    public string description;

    public CardType type;
    public CardEffect[] effects;
}