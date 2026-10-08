using System.Collections.Generic;
using UnityEngine;

// 특성 표. 장비에 특성 ID를 적어 두면, 그 장비를 "장착"했을 때 특성의 효과가 적용된다 (Soul > 데이터 표 > [특성] 탭).
// 한 줄 = 특성 하나. 예) 독: 공격력이 오르는 대신, 독 내성이 없으면 방어력이 줄어든다.
// 독 내성은 [회복] 숙련도 레벨의 보너스(%)이고, 위험한 아이템을 먹을수록 오른다.
// 에셋은 Assets/Resources/Tables/TraitTable.asset.
[System.Serializable]
public class TraitDef
{
    public int id;                 // 특성 ID (장비 표의 "특성ID"에 적음)
    public string label;           // 이름 (버프 창에 보임)
    public string description;     // 설명
    public float atRate;           // 공격력 증감 [%] (+면 버프)
    public float dfRate;           // 방어력 증감 [%] (-면 디버프)
    public bool resistable;        // true면 독 내성만큼 방어력 증감(디버프)이 줄어듦
    public float inflictChance;    // 이 특성을 가진 채 적을 공격해 피해를 줄 때, 적에게 상태이상(독 등)을 붙일 확률 [%]
    public float dotDamage;        // 상태이상 첫 턴의 지속 피해
    public float dotGrowth;        // 턴이 지날 때마다 늘어나는 지속 피해
    [Min(0)] public int dotTurns;  // 상태이상이 지속되는 턴 수
}

[CreateAssetMenu(fileName = "TraitTable", menuName = "Soul/Trait Table")]
public class TraitTable : ScriptableObject
{
    public List<TraitDef> traits = CreateDefaultTraits();

    public TraitDef Get(int id)
    {
        if (id <= 0) return null;
        foreach (TraitDef t in traits)
            if (t != null && t.id == id) return t;
        return null;
    }

    private static List<TraitDef> CreateDefaultTraits()
    {
        return new List<TraitDef>
        {
            new TraitDef
            {
                id = 1, label = "독", atRate = 30f, dfRate = -30f, resistable = true,
                inflictChance = 30f, dotDamage = 2f, dotGrowth = 1f, dotTurns = 5,
                description = "독을 머금어 공격력이 오른다. 독 내성이 없으면 방어력이 줄어든다."
            },
        };
    }

    [ContextMenu("기본값으로 되돌리기")]
    public void ResetToDefaults()
    {
        traits = CreateDefaultTraits();
    }
}
