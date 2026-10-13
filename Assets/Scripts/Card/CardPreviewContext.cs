using System;
using System.Collections.Generic;
using System.Globalization;

// A disposable snapshot of the values needed for card descriptions, never live combat state.
public sealed class CardPreviewContext
{
    private readonly HashSet<StatusEffectType> playerStatuses;
    private readonly HashSet<StatusEffectType> enemyStatuses;
    private readonly Dictionary<string, string> values;
    private readonly HashSet<CardEffect> visiting;
    private readonly bool hasPlayer;
    private readonly bool hasEnemy;
    private readonly bool hasAmmoSystem;
    private readonly bool hasBattleContext;
    private readonly bool applyStatusModifiers;
    private bool canExecute;
    private int ammo;
    private int maxAmmo;

    internal CardPreviewContext(CardData card, BattleManager battle, bool applyStatusModifiers)
    {
        Player player = battle != null ? battle.GetPlayer() : null;
        Enemy enemy = battle != null ? battle.GetTarget() : null;
        AmmoSystem source = player != null ? player.GetAmmoSystem() : null;
        this.applyStatusModifiers = applyStatusModifiers;
        hasPlayer = player != null;
        hasEnemy = enemy != null;
        hasBattleContext = battle != null && hasPlayer;
        hasAmmoSystem = source != null;
        ammo = source != null ? source.CurrentAmmo : 0;
        maxAmmo = source != null ? source.MaxAmmo : 0;
        playerStatuses = CopyStatuses(player);
        enemyStatuses = CopyStatuses(enemy);
        values = new Dictionary<string, string>();
        visiting = new HashSet<CardEffect>();
        canExecute = hasBattleContext && card.requiredAmmo >= 0 &&
            (card.requiredAmmo == 0 || hasAmmoSystem && ammo >= card.requiredAmmo);
        if (canExecute && card.consumeRequiredAmmo && card.requiredAmmo > 0)
            ammo -= card.requiredAmmo;
    }

    private CardPreviewContext(CardPreviewContext source)
    {
        playerStatuses = new HashSet<StatusEffectType>(source.playerStatuses);
        enemyStatuses = new HashSet<StatusEffectType>(source.enemyStatuses);
        values = source.values;
        visiting = source.visiting;
        applyStatusModifiers = source.applyStatusModifiers;
        hasPlayer = source.hasPlayer;
        hasEnemy = source.hasEnemy;
        hasAmmoSystem = source.hasAmmoSystem;
        hasBattleContext = source.hasBattleContext;
        canExecute = source.canExecute;
        ammo = source.ammo;
        maxAmmo = source.maxAmmo;
    }

    private static HashSet<StatusEffectType> CopyStatuses(BattleCharacter character)
    {
        var copy = new HashSet<StatusEffectType>();
        if (character != null)
            foreach (ActiveStatusEffect effect in character.StatusEffects.ActiveEffects)
                if (effect.RemainingTurns > 0) copy.Add(effect.Type);
        return copy;
    }

    internal void PreviewEffects(CardEffect[] effects, string parentPath = null)
    {
        if (effects == null) return;
        for (int i = 0; i < effects.Length; i++)
        {
            CardEffect effect = effects[i];
            if (effect == null || !visiting.Add(effect)) continue;
            string path = parentPath == null ? i.ToString(CultureInfo.InvariantCulture) : parentPath + "." + i;
            try { effect.Preview(this, path); }
            finally { visiting.Remove(effect); }
        }
    }

    internal bool TryGetValue(string token, out string value) => values.TryGetValue(token, out value);

    public void RecordDamage(string path, int baseDamage, CardTarget target)
    {
        if (target != CardTarget.Player && target != CardTarget.Enemy) return;
        HashSet<StatusEffectType> defender = target == CardTarget.Player ? playerStatuses : enemyStatuses;
        int amount = DamageCalculator.CalculateWithModifiers(baseDamage,
            applyStatusModifiers && playerStatuses.Contains(StatusEffectType.Rust),
            applyStatusModifiers && defender.Contains(StatusEffectType.WeaknessExposure));
        string value = amount.ToString(CultureInfo.InvariantCulture);
        if (amount < baseDamage) value = "<color=#E57373>" + value + "</color>";
        else if (amount > baseDamage) value = "<color=#81C784>" + value + "</color>";
        values["damage:" + path] = value;
    }

    public void ApplyStatus(StatusEffectData status, int turns, CardTarget target)
    {
        if (status == null || turns <= 0 || !Enum.IsDefined(typeof(StatusEffectType), status.Type)) return;
        if (target == CardTarget.Player && hasPlayer) playerStatuses.Add(status.Type);
        else if (target == CardTarget.Enemy && hasEnemy) enemyStatuses.Add(status.Type);
    }

    public void LoadAmmo(int amount)
    {
        if (hasAmmoSystem && amount > 0) ammo += Math.Min(amount, maxAmmo - ammo);
    }

    public void IncreaseMaxAmmo(int amount)
    {
        if (hasAmmoSystem && amount > 0) maxAmmo = (int)Math.Min(int.MaxValue, (long)maxAmmo + amount);
    }

    public void PreviewAmmoBonus(string path, int cost, CardEffect[] effects)
    {
        bool canSpend = hasAmmoSystem && cost > 0 && ammo >= cost;
        bool available = canExecute && canSpend;
        values["ammoCost:" + path] = cost.ToString(CultureInfo.InvariantCulture);
        values["ammoCondition:" + path] = !hasBattleContext ? "" : available
            ? "<color=#81C784>현재 발동 가능</color>"
            : "<color=#808080>현재 총알 부족</color>";

        if (canSpend)
        {
            ammo -= cost;
            PreviewEffects(effects, path);
        }
        else
        {
            // Display the bonus's hypothetical damage without leaking its statuses into later effects.
            var branch = new CardPreviewContext(this);
            branch.canExecute = false;
            branch.PreviewEffects(effects, path);
        }
    }
}
