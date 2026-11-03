using TMPro;
using UnityEngine;

public class BattleRunInfoUI : MonoBehaviour
{
    [SerializeField] private TMP_Text floorNode;
    [SerializeField] private TMP_Text goldValue;
    [SerializeField] private BattleRewardSystem rewards;

    private void OnEnable()
    {
        RunProgress progress = RunSession.Instance != null ? RunSession.Instance.Progress : null;
        floorNode.text = progress != null && (progress.Phase == RunPhase.Battle || progress.Phase == RunPhase.Rewards)
            ? $"{progress.FloorIndex + 1}층 · {progress.NodeIndex + 1} / {progress.Nodes.Count}"
            : "—";

        rewards.Changed += RefreshGold;
        RefreshGold();
    }

    private void OnDisable() => rewards.Changed -= RefreshGold;

    private void RefreshGold() => goldValue.text = RunGoldInventory.Gold.ToString();
}

