using UnityEngine;

[CreateAssetMenu(fileName = "StatusEffect", menuName = "Status/Effect Definition")]
public class StatusEffectData : ScriptableObject
{
    [SerializeField] private StatusEffectType statusType;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField, TextArea(2, 5)] private string description;

    public StatusEffectType Type => statusType;
    public Sprite Icon => icon;
    public string Description => description;
    internal string FormatAmount(int amount) => $"{amount}{(Type == StatusEffectType.Burn ? "중첩" : "턴")}";
    public string DisplayName => !string.IsNullOrEmpty(displayName) ? displayName :
        statusType switch
        {
            StatusEffectType.WeaknessExposure => "약점 노출",
            StatusEffectType.DefenseBreak => "방어 파괴",
            StatusEffectType.Burn => "화상",
            _ => "녹슬음"
        };
}
