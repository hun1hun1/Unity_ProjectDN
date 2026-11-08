using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Card/Reward Pool Catalog")]
public sealed class CardRewardPoolCatalog : ScriptableObject
{
    [SerializeField] private CardRewardPool[] pools = Array.Empty<CardRewardPool>();

    public IReadOnlyList<CardRewardPool> Pools => pools;
}
