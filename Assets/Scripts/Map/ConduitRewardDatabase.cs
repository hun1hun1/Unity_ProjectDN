using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ConduitRewardDatabase", menuName = "Conduit/Reward Database")]
public sealed class ConduitRewardDatabase : ScriptableObject
{
    public ConduitData[] conduits = Array.Empty<ConduitData>();
}
