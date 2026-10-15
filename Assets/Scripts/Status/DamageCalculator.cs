using System;

public static class DamageCalculator
{
    public static int Calculate(int baseDamage, BattleCharacter attacker, BattleCharacter defender)
    {
        return CalculateWithModifiers(baseDamage,
            attacker != null && attacker.StatusEffects.HasStatus(StatusEffectType.Rust),
            defender != null && defender.StatusEffects.HasStatus(StatusEffectType.WeaknessExposure));
    }

    internal static int CalculateWithModifiers(int baseDamage, bool rust, bool weaknessExposure)
    {
        int outgoing = rust ? 3 : 4;
        int incoming = weaknessExposure ? 3 : 2;
        // Apply both multipliers before truncating, then absorb damage with block.
        long damage = (long)Math.Max(0, baseDamage) * outgoing * incoming / 8;
        return (int)Math.Min(int.MaxValue, damage);
    }
}
