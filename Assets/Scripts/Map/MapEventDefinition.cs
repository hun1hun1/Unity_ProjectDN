using UnityEngine;

[CreateAssetMenu(fileName = "MapEvent", menuName = "Map/Floor Event")]
public sealed class MapEventDefinition : ScriptableObject
{
    public string title;
    [TextArea] public string description;
    [Min(0)] public int goldReward;
}
