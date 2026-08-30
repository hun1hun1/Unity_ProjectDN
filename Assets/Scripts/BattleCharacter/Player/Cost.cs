using UnityEngine;

[System.Serializable]
public struct Cost
{
    public int dreamCost;
    public int nightmareCost;

    public Cost(int dreamCost, int nightmareCost)
    {
        this.dreamCost = dreamCost;
        this.nightmareCost = nightmareCost;
    }
}
