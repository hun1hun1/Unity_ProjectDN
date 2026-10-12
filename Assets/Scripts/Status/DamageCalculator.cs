using System;

public static class DamageCalculator
{
    public static int Calculate(int baseDamage, BattleCharacter attacker, BattleCharacter defender)
    {
        int outgoing = attacker != null && attacker.StatusEffects.GetRemainingTurns(StatusEffectType.Rust) > 0 ? 3 : 4;
        int incoming = defender != null && defender.StatusEffects.GetRemainingTurns(StatusEffectType.WeaknessExposure) > 0 ? 3 : 2;
        // Apply both multipliers before truncating, then absorb damage with block.
        long damage = (long)Math.Max(0, baseDamage) * outgoing * incoming / 8;
        return (int)Math.Min(int.MaxValue, damage);
    }
}
