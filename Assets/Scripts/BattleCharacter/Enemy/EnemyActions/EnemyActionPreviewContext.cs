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
        int amount = DamageCalculator.CalculateWithModifiers(damage,
            enemyStatuses.Contains(StatusEffectType.Rust), playerStatuses.Contains(StatusEffectType.WeaknessExposure));
        if (hasPlayer)
        {
            playerHP = Math.Max(0, playerHP - Math.Max(0, amount - playerBlock));
            playerBlock = Math.Max(0, playerBlock - amount);
        }
        return amount;
    }

    public int Block(int block) => BlockCalculator.CalculateWithModifiers(block,
        enemyStatuses.Contains(StatusEffectType.DefenseBreak));

    public void Apply(StatusEffectData status, EnemyTargetType target, int turns)
    {
        if (status == null || turns <= 0) return;
        (target == EnemyTargetType.Player ? playerStatuses : enemyStatuses).Add(status.Type);
    }
}
