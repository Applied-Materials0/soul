using System.Collections.Generic;
using UnityEngine;

// 숙련도 표. 숙련도 종류마다 가로가 레벨, 세로가 세부 항목이다 (Soul > 데이터 표 > [숙련도] 탭).
// 에셋은 Assets/Resources/Tables/ProficiencyTable.asset.
public enum ProficiencyKind
{
    Carving,  // 도려내기 (칼로 가죽/고기 얻기)
}

[System.Serializable]
public class ProficiencyLevel
{
    public int level;                   // 숙련도 레벨 (표가 자동으로 맞춤)
    [Min(0)] public int expToNext;      // 다음 숙련도 레벨까지 필요한 경험치 (마지막 레벨은 0)
    public float bonusPercent;          // 이 레벨의 수확 보너스 [%] (얻는 수량이 이만큼 늘어남)
}

[System.Serializable]
public class ProficiencyDef
{
    public ProficiencyKind kind;
    public string label;                // 표에 보이는 이름
    [Min(0)] public int expPerUse = 1;  // 한 번 사용할 때 오르는 숙련도 경험치
    public List<ProficiencyLevel> levels = new List<ProficiencyLevel>();
}

[CreateAssetMenu(fileName = "ProficiencyTable", menuName = "Soul/Proficiency Table")]
public class ProficiencyTable : ScriptableObject
{
    public List<ProficiencyDef> defs = CreateDefaultDefs();

    public ProficiencyDef Get(ProficiencyKind kind)
    {
        foreach (ProficiencyDef d in defs)
            if (d != null && d.kind == kind) return d;
        return null;
    }

    private static List<ProficiencyDef> CreateDefaultDefs()
    {
        ProficiencyDef carving = new ProficiencyDef { kind = ProficiencyKind.Carving, label = "도려내기", expPerUse = 1 };
        int[] exp = { 5, 10, 20, 35, 55, 80, 110, 150, 200, 0 };
        float[] bonus = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 100 };
        for (int i = 0; i < exp.Length; i++)
            carving.levels.Add(new ProficiencyLevel { level = i + 1, expToNext = exp[i], bonusPercent = bonus[i] });
        return new List<ProficiencyDef> { carving };
    }

    [ContextMenu("기본값으로 되돌리기")]
    public void ResetToDefaults()
    {
        defs = CreateDefaultDefs();
    }
}
