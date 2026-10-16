using System.Collections;
using UnityEngine;

public enum MulliganHandScope { Dream, Nightmare, Both }
public enum MulliganCountMode { All, Fixed }
public enum MulliganSelectionMode { Manual, Random }

[CreateAssetMenu(fileName = "MulliganEffect", menuName = "Card/Effect/Mulligan")]
public class MulliganEffect : CardEffect
{
    [SerializeField] private MulliganHandScope handScope;
    [SerializeField] private MulliganCountMode countMode = MulliganCountMode.Fixed;
    [SerializeField, Min(0)] private int count = 1;
    [SerializeField] private MulliganSelectionMode selectionMode;

    public MulliganHandScope HandScope => handScope;
    public MulliganCountMode CountMode => countMode;
    public int Count => Mathf.Max(0, count);
    public MulliganSelectionMode SelectionMode => selectionMode;
    public bool IsValid => (handScope == MulliganHandScope.Dream || handScope == MulliganHandScope.Nightmare || handScope == MulliganHandScope.Both) &&
        (countMode == MulliganCountMode.All || countMode == MulliganCountMode.Fixed) &&
        (countMode == MulliganCountMode.All || selectionMode == MulliganSelectionMode.Manual || selectionMode == MulliganSelectionMode.Random);

    public override void Execute(BattleManager battle)
    {
        if (battle != null) battle.RunEffectRoutine(ExecuteRoutine(battle));
    }

    public override IEnumerator ExecuteRoutine(BattleManager battle) =>
        battle != null ? battle.Mulligans.Resolve(this, battle) : null;

    public override IEnumerator ActivateContinuousRoutine(BattleManager battle, ActiveContinuousCard source) =>
        ExecuteRoutine(battle);
    public override string GetEffectSummary(System.Collections.Generic.HashSet<CardEffect> visiting)
    {
        return $"{(handScope == MulliganHandScope.Dream ? "꿈" : handScope == MulliganHandScope.Nightmare ? "악몽" : "양쪽")} 손패 {(countMode == MulliganCountMode.All ? "전체" : Count.ToString() + "장")} 교체";
    }
}
