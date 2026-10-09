using System.Collections.Generic;
using UnityEngine;

// 장비 등급 표. 장비/도구는 내구도를 쓸 때마다 경험치를 얻고, 경험치가 차면 다음 등급으로 오른다 (Soul > 데이터 표 > [등급] 탭).
// 한 줄 = 등급 하나. 등급 번호(Tier 1, 2, 3...)는 위에서부터 차례로 대응한다.
// 아이템마다 내구도 1당 경험치(wearExp)와 수리 재료는 [아이템] 표의 [수리/등급] 탭에서 정한다.
// 에셋은 Assets/Resources/Tables/GradeTable.asset.
[System.Serializable]
public class GradeDef
{
    public int tier;                  // 등급 번호 (1부터)
    public string name;               // 등급 이름 (Common, Rare ...)
    [Min(0)] public int expToNext;    // 다음 등급까지 필요한 경험치 (마지막 등급은 0)
    // 등급일 때 장비의 각 능력치가 늘어나는 비율 [%]. 능력치마다 따로 정하며, 그 장비의 기본 능력치에 곱해진다 (100이면 2배, 0이면 그대로).
    public float atBonus;             // 공격력
    public float dfBonus;             // 방어력
    public float hpBonus;             // 체력
    public float fixBonus;            // 고정 데미지
    public float breakDfBonus;        // 방어 관통
    public float hpRateAtBonus;       // 체력 퍼뎀
    public float critBonus;           // 치명타 배율(치명타 데미지)
    public float critRateBonus;       // 치명타 확률
    public float absBonus;            // 흡수
    public float healRateBonus;       // 회복 증가율
    [HideInInspector] public float statBonusPercent; // (옛 값: 한 칸으로 묶었던 능력치 %. 에디터가 위의 칸들로 옮겨 줌)
}

[CreateAssetMenu(fileName = "GradeTable", menuName = "Soul/Grade Table")]
public class GradeTable : ScriptableObject
{
    public List<GradeDef> grades = CreateDefaultGrades();

    public GradeDef Get(int tier)
    {
        foreach (GradeDef g in grades)
            if (g != null && g.tier == tier) return g;
        return null;
    }

    public int MaxTier
    {
        get
        {
            int max = 1;
            foreach (GradeDef g in grades) if (g != null) max = Mathf.Max(max, g.tier);
            return max;
        }
    }

    public string NameOf(int tier)
    {
        GradeDef g = Get(tier);
        return g != null && !string.IsNullOrEmpty(g.name) ? g.name : $"Tier {tier}";
    }

    private static GradeDef G(int tier, string name, int exp, float bonus)
    {
        return new GradeDef { tier = tier, name = name, expToNext = exp, atBonus = bonus, dfBonus = bonus, hpBonus = bonus, fixBonus = bonus };
    }

    private static List<GradeDef> CreateDefaultGrades()
    {
        return new List<GradeDef>
        {
            G(1, "Common", 100, 0f),
            G(2, "Rare", 300, 10f),
            G(3, "Epic", 600, 25f),
            G(4, "Legends", 1000, 50f),
            G(5, "Eternal", 0, 100f),
        };
    }

    [ContextMenu("기본값으로 되돌리기")]
    public void ResetToDefaults()
    {
        grades = CreateDefaultGrades();
    }
}
