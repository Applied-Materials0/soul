using System.Collections.Generic;
using UnityEngine;

// 행동마다 SP(행동력)를 얼마나 쓰는지 정하는 표. 행 하나 = 행동 하나.
// 에셋은 Assets/Resources/Tables/SPCostTable.asset 이고, Soul > 데이터 표 > [SP 소모] 탭에서 고친다.
public enum SPAction
{
    Search,   // 탐색
    Gather,   // 채집 (벌목, 채광 등 자원 채집 한 번)
    Carve,    // 도려내기 (칼로 가죽/고기 얻기 한 번)
    Attack,   // 전투: 공격
    Defend,   // 전투: 방어
    Run,      // 전투: 도망
    Travel,   // 다른 지역으로 이동 (루프 타운으로 돌아가는 이동은 제외)
}

[System.Serializable]
public class SPCostEntry
{
    public SPAction action;
    public string label;       // 표에 보이는 이름 (게임에는 영향 없음)
    [Min(0)] public int cost;  // 소모하는 SP
    public string note;        // 메모
}

[CreateAssetMenu(fileName = "SPCostTable", menuName = "Soul/SP Cost Table")]
public class SPCostTable : ScriptableObject
{
    public List<SPCostEntry> entries = CreateDefaultEntries();

    // 해당 행동의 SP 소모량 (표에 없으면 0)
    public int Get(SPAction action)
    {
        foreach (SPCostEntry e in entries)
            if (e != null && e.action == action) return Mathf.Max(0, e.cost);
        return 0;
    }

    private static SPCostEntry E(SPAction a, string label, int cost, string note)
    {
        return new SPCostEntry { action = a, label = label, cost = cost, note = note };
    }

    private static List<SPCostEntry> CreateDefaultEntries()
    {
        return new List<SPCostEntry>
        {
            E(SPAction.Search, "탐색", 1, "탐색 버튼을 한 번 누를 때"),
            E(SPAction.Gather, "채집", 1, "자원을 한 번 채집할 때"),
            E(SPAction.Carve, "도려내기", 1, "칼로 한 번 도려낼 때"),
            E(SPAction.Attack, "공격", 1, "전투에서 공격할 때"),
            E(SPAction.Defend, "방어", 1, "전투에서 방어할 때"),
            E(SPAction.Run, "도망", 1, "전투에서 도망을 시도할 때"),
            E(SPAction.Travel, "지역 이동", 5, "다른 지역으로 이동할 때 (루프 타운으로 돌아갈 때는 소모 안 함)"),
        };
    }

    [ContextMenu("기본값으로 되돌리기")]
    public void ResetToDefaults()
    {
        entries = CreateDefaultEntries();
    }
}
