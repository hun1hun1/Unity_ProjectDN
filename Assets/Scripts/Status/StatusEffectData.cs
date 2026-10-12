using UnityEngine;

[CreateAssetMenu(fileName = "StatusEffect", menuName = "Status/Effect Definition")]
public class StatusEffectData : ScriptableObject
{
    [SerializeField] private StatusEffectType statusType;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    public StatusEffectType Type => statusType;
    public Sprite Icon => icon;
    public string DisplayName => !string.IsNullOrEmpty(displayName) ? displayName :
        statusType == StatusEffectType.WeaknessExposure ? "약점 노출" : "녹슬음";
}
