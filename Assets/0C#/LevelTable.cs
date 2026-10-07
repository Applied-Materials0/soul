using System.Collections.Generic;
using UnityEngine;

// 레벨 표. 가로가 레벨, 세로가 세부 항목이다 (Soul > 데이터 표 > [레벨] 탭).
// 에셋은 Assets/Resources/Tables/LevelTable.asset.
//
// 한 레벨의 항목:
//  - expToNext: 이 레벨에서 다음 레벨이 되기까지 필요한 경험치 (마지막 레벨은 0 = 더 이상 오르지 않음)
//  - 나머지: 이 레벨에 "도달했을 때" 늘어나는 능력치 (1레벨은 시작 상태라 적용되지 않음)
[System.Serializable]
public class LevelEntry
{
    public int level;                  // 레벨 (표가 자동으로 맞춤)
    [Min(0)] public int expToNext;     // 다음 레벨까지 필요한 경험치
    public int hpMaxGain;              // 도달 시 최대 체력 증가
    public int atGain;                 // 도달 시 공격력 증가
    public int dfGain;                 // 도달 시 방어력 증가
    public int spMaxGain;              // 도달 시 최대 SP 증가
    public int speedGain;              // 도달 시 속도 증가
}

[CreateAssetMenu(fileName = "LevelTable", menuName = "Soul/Level Table")]
public class LevelTable : ScriptableObject
{
    public List<LevelEntry> levels = CreateDefaultLevels();

    public int MaxLevel { get { return levels.Count; } }

    // 레벨의 항목 (범위를 벗어나면 null)
    public LevelEntry Get(int level)
    {
        if (level < 1 || level > levels.Count) return null;
        return levels[level - 1];
    }

    // level에서 다음 레벨까지 필요한 경험치. 최고 레벨이거나 표에 없으면 0
    public int ExpToNext(int level)
    {
        if (level >= MaxLevel) return 0;
        LevelEntry e = Get(level);
        return e != null ? Mathf.Max(0, e.expToNext) : 0;
    }

    private static List<LevelEntry> CreateDefaultLevels()
    {
        List<LevelEntry> list = new List<LevelEntry>();
        const int count = 20;
        for (int lv = 1; lv <= count; lv++)
        {
            list.Add(new LevelEntry
            {
                level = lv,
                expToNext = lv < count ? 5 * lv * (lv + 1) : 0, // 10, 30, 60, 100, 150 ...
                hpMaxGain = 5,
                atGain = 1,
                dfGain = 1,
                spMaxGain = 5,
                speedGain = lv % 5 == 0 ? 1 : 0,
            });
        }
        return list;
    }

    [ContextMenu("기본값으로 되돌리기")]
    public void ResetToDefaults()
    {
        levels = CreateDefaultLevels();
    }
}
