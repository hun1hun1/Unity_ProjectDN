using UnityEngine;

public class Player : BattleCharacter
{
    [SerializeField]
    private CostSystem costSystem;

    public CostSystem GetCostSystem()
    {
        if (costSystem == null) costSystem = GetComponent<CostSystem>();
        return costSystem;
    }

    public AmmoSystem GetAmmoSystem()
    {
        return GetComponent<AmmoSystem>();
    }

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Die()
    {
        Debug.Log("플레이어가 사망했습니다.");

        // 게임 오버 처리
    }
}
