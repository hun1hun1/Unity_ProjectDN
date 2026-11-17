using System;
using System.Collections.Generic;

// One snapshot is shared by all children of a composite. Never execute real actions to render intent.
public sealed class EnemyActionPreviewContext
{
    public BattleManager Battle { get; }
    internal bool SummarizeStatuses { get; }
    private readonly HashSet<StatusEffectType> playerStatuses;
    private readonly HashSet<StatusEffectType> enemyStatuses;
    private readonly bool hasPlayer;
    private readonly bool hasEnemy;
    private int playerHP;
    private int enemyHP;
    private int playerBlock;
    private int playerThorns;
    public bool CanContinue => (!hasPlayer || playerHP > 0) && (!hasEnemy || enemyHP > 0);

    public EnemyActionPreviewContext(BattleManager battle, bool summarizeStatuses = false)
    {
        Battle = battle;
        SummarizeStatuses = summarizeStatuses;
        Player player = battle != null ? battle.GetPlayer() : null;
        Enemy enemy = battle != null ? battle.GetTarget() : null;
        hasPlayer = player != null; hasEnemy = enemy != null;
        playerHP = hasPlayer ? player.GetCurrentHP() : 0;
        enemyHP = hasEnemy ? enemy.GetCurrentHP() : 0;
        playerBlock = hasPlayer ? player.GetBlock() : 0;
        playerThorns = hasPlayer ? player.StatusEffects.GetBuffPower(StatusEffectType.Thorns) : 0;
        playerStatuses = Copy(player); enemyStatuses = Copy(enemy);
    }

    private static HashSet<StatusEffectType> Copy(BattleCharacter character)
    {
        var result = new HashSet<StatusEffectType>();
        if (character != null)
            foreach (ActiveStatusEffect effect in character.StatusEffects.ActiveEffects)
                if (effect.IsActive) result.Add(effect.Type);
        return result;
    }

    public int Attack(int damage)
    {
        bool warded = damage > 0 && hasPlayer && hasEnemy && CanContinue && playerStatuses.Remove(StatusEffectType.Ward);
        int amount = warded ? 0 : DamageCalculator.CalculateWithModifiers(damage,
            enemyStatuses.Contains(StatusEffectType.Rust), playerStatuses.Contains(StatusEffectType.WeaknessExposure));
        if (hasPlayer)
        {
            playerHP = Math.Max(0, playerHP - Math.Max(0, amount - playerBlock));
            playerBlock = Math.Max(0, playerBlock - amount);
            if (damage > 0 && playerHP > 0 && hasEnemy)
                enemyHP = Math.Max(0, enemyHP - playerThorns);
        }
        return amount;
    }

    public int Block(int block) => BlockCalculator.CalculateWithModifiers(block,
        enemyStatuses.Contains(StatusEffectType.DefenseBreak));

    public void Apply(StatusEffectData status, EnemyTargetType target, int turns, int buffPower = 1)
    {
        if (status == null || !status.CanApply(turns, buffPower)) return;
        if (status.Category == StatusEffectCategory.Buff)
        {
            if (status.Duration == BuffDuration.NextTurn) return;
            if (status.Type == StatusEffectType.Thorns && target == EnemyTargetType.Player)
                playerThorns = (int)Math.Min(int.MaxValue, (long)playerThorns + buffPower);
        }
        (target == EnemyTargetType.Player ? playerStatuses : enemyStatuses).Add(status.Type);
    }
}
