using UnityEngine;

public abstract class BattleCharacter : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] protected int maxHP = 80;
    [SerializeField] protected int currentHP;

    [Header("Block")]
    [SerializeField] protected int block;

    [Header("UI")]
    [SerializeField] private CharacterInfoUI characterInfoUI;

    private DelayedEffectSystem boundDelayedEffects;

    public void BindCharacterInfoUI(CharacterInfoUI ui)
    {
        if (characterInfoUI == ui) return;
        if (characterInfoUI != null) characterInfoUI.BindStatusEffects(null);
        characterInfoUI = ui;
        if (characterInfoUI != null) characterInfoUI.BindStatusEffects(StatusEffects, ContinuousCards, boundDelayedEffects);
        UpdateUI();
    }

    public event System.Action<BattleCharacter> Died;
    public event System.Action VitalsChanged;
    public void BindDelayedEffects(DelayedEffectSystem delayed)
    {
        boundDelayedEffects = delayed;
        if (characterInfoUI != null) characterInfoUI.BindStatusEffects(StatusEffects, ContinuousCards, delayed);
    }

    public StatusEffectSystem StatusEffects { get; } = new StatusEffectSystem();
    public ContinuousCardSystem ContinuousCards { get; } = new ContinuousCardSystem();

    protected virtual void Awake()
    {
        currentHP = maxHP;
        if (characterInfoUI != null) characterInfoUI.BindStatusEffects(StatusEffects, ContinuousCards);

        UpdateUI();
    }

    // Apply persistent run HP after Awake, without healing or carrying battle block.
    public void RestoreHealth(int health, int maximum)
    {
        if (maximum < 1) throw new System.ArgumentOutOfRangeException(nameof(maximum));
        maxHP = maximum;
        currentHP = Mathf.Clamp(health, 0, maximum);
        block = 0;
        UpdateUI();
    }

    public virtual void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    public virtual void TakeDamage(int damage, BattleCharacter attacker)
    {
        bool isAttack = damage > 0 && currentHP > 0 && attacker != null && attacker != this && attacker.currentHP > 0;
        int thorns = isAttack ? StatusEffects.GetBuffPower(StatusEffectType.Thorns) : 0;
        damage = isAttack && StatusEffects.HasStatus(StatusEffectType.Ward)
            ? 0 : DamageCalculator.Calculate(damage, attacker, this);
        // 방어도가 먼저 피해를 흡수
        int remainingDamage = damage - block;

        if (remainingDamage < 0)
            remainingDamage = 0;

        block -= damage;

        if (block < 0)
            block = 0;

        ApplyDamageToHealth(damage, remainingDamage);
        if (!isAttack) return;
        StatusEffects.ConsumeTriggeredBuffs(BuffDuration.NextIncomingAttack);
        if (attacker != null) attacker.StatusEffects.ConsumeTriggeredBuffs(BuffDuration.NextAttack);
        // Fixed retaliation does not count as attacking and cannot trigger another reaction.
        if (thorns > 0 && currentHP > 0 && attacker != null && attacker.currentHP > 0)
            attacker.TakeFixedDamage(thorns);
    }

    internal void TakeFixedDamage(int damage) => ApplyDamageToHealth(damage, damage);

    private void ApplyDamageToHealth(int damage, int remainingDamage)
    {
        bool wasAlive = currentHP > 0;
        currentHP -= remainingDamage;

        if (currentHP < 0)
            currentHP = 0;

        Debug.Log($"{gameObject.name} 피해 {damage} / HP {currentHP} / Block {block}");

        UpdateUI();

        if (wasAlive && currentHP <= 0)
        {
            Died?.Invoke(this);
            Die();
        }
    }

    public virtual void AddBlock(int amount)
    {
        amount = BlockCalculator.Calculate(amount, this);
        block += amount;

        Debug.Log($"{gameObject.name} 방어도 +{amount} / 현재 방어도 {block}");

        UpdateUI();
        if (Application.isPlaying && amount > 0 && TryGetComponent(out BattleCharacterPresentation presentation))
            presentation.PlayBlockGain(amount);
    }

    public virtual void ResetBlock()
    {
        block = 0;

        Debug.Log($"{gameObject.name} 방어도 초기화 / 현재 방어도 {block}");

        UpdateUI();
    }

    public virtual void Heal(int amount)
    {
        currentHP += amount;
        if (currentHP > maxHP) currentHP = maxHP;

        Debug.Log($"{gameObject.name} 회복 +{amount} / 현재 HP {currentHP}");

        UpdateUI();
    }

    protected void UpdateUI()
    {
        if (characterInfoUI != null)
        {
            characterInfoUI.SetHP(currentHP, maxHP);
            characterInfoUI.SetBlock(block);
        }
        // Intent previews also depend on HP and block, even when no character HUD is present.
        VitalsChanged?.Invoke();
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} 사망");
    }

    public int GetCurrentHP()
    {
        return currentHP;
    }

    public int GetMaxHP()
    {
        return maxHP;
    }

    public int GetBlock()
    {
        return block;
    }
}
