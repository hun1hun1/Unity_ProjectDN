using UnityEngine;

[CreateAssetMenu(fileName = "StatusEffect", menuName = "Status/Effect Definition")]
public class StatusEffectData : ScriptableObject
{
    [SerializeField] private StatusEffectType statusType;
    [SerializeField] private StatusEffectCategory category;
    [Tooltip("Used by buffs. Next Attack is spent when the owner attacks; Next Incoming Attack is spent when the owner is attacked.")]
    [SerializeField] private BuffDuration buffDuration;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField, TextArea(2, 5)] private string description;

    public StatusEffectType Type => statusType;
    public StatusEffectCategory Category => category;
    public BuffDuration Duration => buffDuration;
    public Sprite Icon => icon;
    public string Description => description;
    internal string FormatAmount(int amount) => $"{amount}{(Type == StatusEffectType.Burn ? "중첩" : "턴")}";
    internal bool CanApply(int turns, int power)
    {
        if (!System.Enum.IsDefined(typeof(StatusEffectType), Type)) return false;
        if (Type == StatusEffectType.Ward)
            return Category == StatusEffectCategory.Buff && Duration == BuffDuration.NextIncomingAttack && power > 0;
        return Category switch
        {
            StatusEffectCategory.Debuff => Type != StatusEffectType.Thorns && turns > 0,
            StatusEffectCategory.Buff => Type != StatusEffectType.Burn && power > 0 &&
                System.Enum.IsDefined(typeof(BuffDuration), Duration) && (Duration != BuffDuration.FixedTurns || turns > 0),
            _ => false
        };
    }

    internal string FormatApplication(int turns, int power)
    {
        if (Category == StatusEffectCategory.Debuff) return FormatAmount(turns);
        string duration = Duration switch
        {
            BuffDuration.UntilBattleEnd => "전투 종료까지",
            BuffDuration.NextTurn => "다음 플레이어 턴에만",
            BuffDuration.NextAttack => "다음 공격 시 소모",
            BuffDuration.NextIncomingAttack => "다음 공격을 받을 때 소모",
            _ => $"{turns}턴"
        };
        if (Type == StatusEffectType.Ward) return $"({duration})";
        return $"{power} ({duration})";
    }
    public string DisplayName => !string.IsNullOrEmpty(displayName) ? displayName :
        statusType switch
        {
            StatusEffectType.WeaknessExposure => "약점 노출",
            StatusEffectType.DefenseBreak => "방어 파괴",
            StatusEffectType.Burn => "화상",
            StatusEffectType.Thorns => "가시",
            StatusEffectType.Ward => "방호막",
            _ => "녹슬음"
        };
}
