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
    private readonly bool isContinuous;
    private readonly bool openingAvailable;
    private bool canExecute;
    private int ammo;
    private int maxAmmo;
    private int? spentAmmo;
    private bool spentAmmoIsKnown;
    private bool CanResolveSpentAmmo => spentAmmoIsKnown || hasBattleContext && applyStatusModifiers;

    internal CardPreviewContext(CardData card, BattleManager battle, bool applyStatusModifiers, bool forTurnStart = false, int? capturedSpentAmmo = null)
    {
        Player player = battle != null ? battle.GetPlayer() : null;
        Enemy enemy = battle != null ? battle.GetTarget() : null;
        AmmoSystem source = player != null ? player.GetAmmoSystem() : null;
        this.applyStatusModifiers = applyStatusModifiers;
        spentAmmo = capturedSpentAmmo;
        spentAmmoIsKnown = capturedSpentAmmo.HasValue;
        openingAvailable = !forTurnStart && card != null && card.costMode == CardCostMode.Fixed &&
            battle != null && battle.OpeningAvailable;
        isContinuous = card != null && card.type == CardType.Continuous && !forTurnStart;
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
        // Recurring effects are actions of an already active card, not another card use.
        canExecute = capturedSpentAmmo.HasValue || hasBattleContext && (forTurnStart || card.requiredAmmo >= 0 &&
            (card.requiredAmmo == 0 || hasAmmoSystem && ammo >= card.requiredAmmo));
        if (!forTurnStart && canExecute && card.consumeRequiredAmmo && card.requiredAmmo > 0)
            ammo -= card.requiredAmmo;
    }

    private CardPreviewContext(CardPreviewContext source, bool delayed = false)
    {
        playerStatuses = new HashSet<StatusEffectType>(source.playerStatuses);
        enemyStatuses = new HashSet<StatusEffectType>(source.enemyStatuses);
        values = source.values;
        visiting = source.visiting;
        applyStatusModifiers = !delayed && source.applyStatusModifiers;
        isContinuous = !delayed && source.isContinuous;
        openingAvailable = !delayed && source.openingAvailable;
        hasPlayer = source.hasPlayer;
        hasEnemy = source.hasEnemy;
        hasAmmoSystem = source.hasAmmoSystem;
        hasBattleContext = source.hasBattleContext;
        canExecute = source.canExecute;
        ammo = source.ammo;
        maxAmmo = source.maxAmmo;
        spentAmmo = source.spentAmmo;
        spentAmmoIsKnown = source.spentAmmoIsKnown || delayed && source.hasBattleContext &&
            source.applyStatusModifiers && source.spentAmmo.HasValue;
    }

    private static HashSet<StatusEffectType> CopyStatuses(BattleCharacter character)
    {
        var copy = new HashSet<StatusEffectType>();
        if (character != null)
            foreach (ActiveStatusEffect effect in character.StatusEffects.ActiveEffects)
                if (effect.IsActive) copy.Add(effect.Type);
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

    public void PreviewDelayed(string path, CardEffect[] effects)
    {
        var branch = new CardPreviewContext(this, delayed: true);
        branch.PreviewEffects(effects, path);
    }

    public void PreviewOpening(string path, CardEffect[] effects)
    {
        bool available = canExecute && openingAvailable;
        values["openingCondition:" + path] = !hasBattleContext || !applyStatusModifiers ? "" : available
            ? "<color=#81C784>개전 발동 가능</color>"
            : "<color=#808080>개전 발동 불가</color>";
        if (available)
        {
            PreviewEffects(effects, path);
        }
        else
        {
            // Keep the bonus's numbers visible without applying its changes to later effects.
            var branch = new CardPreviewContext(this);
            branch.canExecute = false;
            branch.PreviewEffects(effects, path);
        }
    }

    public void RecordDamage(string path, int baseDamage, CardTarget target, EffectAmountMode mode = EffectAmountMode.Fixed)
    {
        if (target != CardTarget.Player && target != CardTarget.Enemy) return;
        bool symbolic = mode == EffectAmountMode.PerSpentAmmo && !CanResolveSpentAmmo;
        if (!symbolic) baseDamage = EffectAmount.Resolve(baseDamage, mode, spentAmmo.GetValueOrDefault());
        HashSet<StatusEffectType> defender = target == CardTarget.Player ? playerStatuses : enemyStatuses;
        bool warded = applyStatusModifiers && canExecute && hasEnemy && target == CardTarget.Enemy &&
            !symbolic && baseDamage > 0 && defender.Remove(StatusEffectType.Ward);
        int amount = warded ? 0 : DamageCalculator.CalculateWithModifiers(baseDamage,
            applyStatusModifiers && playerStatuses.Contains(StatusEffectType.Rust),
            applyStatusModifiers && defender.Contains(StatusEffectType.WeaknessExposure));
        string value = amount.ToString(CultureInfo.InvariantCulture);
        if (amount < baseDamage) value = "<color=#E57373>" + value + "</color>";
        else if (amount > baseDamage) value = "<color=#81C784>" + value + "</color>";
        values["damage:" + path] = symbolic ? value + " × N" : value;
    }

    public void RecordBlock(string path, int baseBlock, EffectAmountMode mode = EffectAmountMode.Fixed)
    {
        bool symbolic = mode == EffectAmountMode.PerSpentAmmo && !CanResolveSpentAmmo;
        if (!symbolic) baseBlock = EffectAmount.Resolve(baseBlock, mode, spentAmmo.GetValueOrDefault());
        // BlockEffect always grants block to the player, matching its Execute implementation.
        int amount = BlockCalculator.CalculateWithModifiers(baseBlock,
            applyStatusModifiers && playerStatuses.Contains(StatusEffectType.DefenseBreak));
        string value = amount.ToString(CultureInfo.InvariantCulture);
        if (amount < baseBlock) value = "<color=#E57373>" + value + "</color>";
        else if (amount > baseBlock) value = "<color=#81C784>" + value + "</color>";
        values["block:" + path] = symbolic ? value + " × N" : value;
    }

    public void ApplyStatus(StatusEffectData status, int turns, CardTarget target, int buffPower = 1)
    {
        if (status == null) return;
        if (status.Category == StatusEffectCategory.Buff)
        {
            if (!status.CanApply(turns, buffPower) || status.Duration == BuffDuration.NextTurn) return;
        }
        else if (!isContinuous && turns <= 0 || !Enum.IsDefined(typeof(StatusEffectType), status.Type)) return;
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

    public void PreviewSpendAllAmmo(string path, CardEffect[] effects, bool valid = true)
    {
        int count = ammo;
        bool live = hasBattleContext && applyStatusModifiers;
        bool available = valid && canExecute && hasAmmoSystem && count > 0;
        values["spentAmmo:" + path] = live ? count.ToString(CultureInfo.InvariantCulture) : "N";
        values["ammoCondition:" + path] = !live ? "" : available
            ? "<color=#81C784>현재 발동 가능</color>" : "<color=#808080>현재 총알 부족</color>";
        if (available && live)
        {
            int? previous = spentAmmo;
            bool wasKnown = spentAmmoIsKnown;
            ammo = 0;
            spentAmmo = count;
            spentAmmoIsKnown = true;
            try { PreviewEffects(effects, path); }
            finally { spentAmmo = previous; spentAmmoIsKnown = wasKnown; }
        }
        else
        {
            var branch = new CardPreviewContext(this);
            branch.canExecute = false;
            branch.spentAmmo = live ? (int?)count : null;
            branch.spentAmmoIsKnown = live;
            branch.PreviewEffects(effects, path);
        }
    }

    public void PreviewSpentAmmoThreshold(string path, int minimum, CardEffect[] effects, bool valid = true)
    {
        bool available = valid && minimum > 0 && canExecute &&
            spentAmmo.GetValueOrDefault() >= minimum;
        values["spentAmmoCondition:" + path] = !CanResolveSpentAmmo ? "" : available
            ? "<color=#81C784>추가 효과 발동 가능</color>" : "<color=#808080>소모한 총알 부족</color>";
        if (available) PreviewEffects(effects, path);
        else
        {
            var branch = new CardPreviewContext(this);
            branch.canExecute = false;
            branch.PreviewEffects(effects, path);
        }
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
