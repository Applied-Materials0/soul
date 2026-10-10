using UnityEngine;

// 탈진: SP가 0까지 떨어질 때마다 스택이 하나씩 쌓인다. 스택이 정해진 개수에 닿으면 건강이 나빠져 최대 SP가 줄어든다.
// 푹 자고 다음 날을 맞으면(밤에 휴식, 또는 기절로 하루가 넘어감) 스택과 건강 악화가 모두 풀린다.
// 개수와 줄어드는 정도는 기본 능력치 표의 [탈진] 항목에서 고친다.
public static class Exhaustion
{
    public static int Stacks;       // 지금 쌓인 스택
    private static bool wasZero;    // 직전에 SP가 0이었는가 (0에 "도달"할 때만 한 번 세기 위함)

    // 건강이 나빠진 상태인가
    public static bool Weakened
    {
        get
        {
            int need = GameTables.PlayerBase.exhaustStacks;
            return need > 0 && Stacks >= need;
        }
    }

    // 건강 악화로 줄어드는 최대 SP (baseSpMax = 장비 보너스를 뺀 기본 최대 SP)
    public static int SpMaxPenalty(int baseSpMax)
    {
        if (!Weakened) return 0;
        return BattleCalc.FloorInt(baseSpMax * Mathf.Clamp(GameTables.PlayerBase.exhaustSpMaxPercent, 0f, 100f) / 100f);
    }

    // SP가 바뀐 뒤에 부른다 (PlayerUI가 갱신될 때마다). SP가 0에 도달한 순간 스택이 쌓인다.
    public static void Observe()
    {
        if (GameManager.SP > 0) { wasZero = false; return; }
        if (wasZero) return;
        wasZero = true;

        bool before = Weakened;
        Stacks++;
        if (!before && Weakened)
        {
            ItemGainToast.ShowMessage("무리를 해서 건강이 나빠졌다...\n최대 SP가 줄어든다.");
            if (InventoryManager.Instance != null) InventoryManager.Instance.RecalcEquipment();
        }
        else
        {
            int need = GameTables.PlayerBase.exhaustStacks;
            if (need > 0 && !Weakened) ItemGainToast.ShowMessage($"지쳤다... (탈진 {Stacks}/{need})");
        }
    }

    // 하루가 지나 푹 쉬었다: 스택과 건강 악화가 풀린다
    public static void Reset()
    {
        bool was = Weakened;
        Stacks = 0;
        wasZero = false;
        if (was && InventoryManager.Instance != null) InventoryManager.Instance.RecalcEquipment();
    }
}
