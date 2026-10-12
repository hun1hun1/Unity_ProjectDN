using UnityEngine;

public enum CardType
{
    Attack,
    Skill,
    Continuous
}

public enum CardRarity
{
    Common,
    Uncommon,
    Rare
}

[CreateAssetMenu(fileName = "CardData", menuName = "Card/CardData")]
public class CardData : ScriptableObject
{
    public string cardName;
    public Cost cost;
    public Sprite artwork;

    [TextArea]
    public string description;

    public CardDeckType deckType;
    public CardType type;
    public CardRarity rarity;
    public CardEffect[] effects;

    [Header("Ammo Requirement")]
    [Min(0)] public int requiredAmmo;
    [Tooltip("Consume the required ammo when this card is used.")]
    public bool consumeRequiredAmmo;
}