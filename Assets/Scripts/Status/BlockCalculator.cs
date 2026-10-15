public static class BlockCalculator
{
    public static int Calculate(int baseBlock, BattleCharacter recipient)
    {
        return CalculateWithModifiers(baseBlock,
            recipient != null && recipient.StatusEffects.HasStatus(StatusEffectType.DefenseBreak));
    }

    public static int CalculateWithModifiers(int baseBlock, bool defenseBreak)
    {
        // Only gains are reduced. Preserve the existing behavior of non-positive changes.
        return defenseBreak && baseBlock > 0 ? baseBlock / 2 : baseBlock;
    }
}
