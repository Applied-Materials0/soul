using System.Collections.Generic;
using UnityEngine;

// 탐색 버튼을 눌렀을 때 무엇이 나오는지를 정하는 표. 한 줄 = 나올 수 있는 결과 하나.
// 확률 = 내 비중 / 비중 합계 (합이 100일 필요는 없음, 0이면 절대 안 나옴).
// 에셋은 Assets/Resources/Tables/SearchTable.asset 이고, Soul > 데이터 표 > [탐색 결과] 탭에서 고친다.
public enum SearchOutcomeKind
{
    Resource,  // 자원 발견 (어떤 자원인지는 [자원] 표의 비중으로 정함)
    Event,     // 이벤트 (아래 문구가 화면에 나옴)
    Monster,   // 몬스터 조우 (어떤 몬스터인지는 몬스터의 출현 지역/비중으로 정함)
}

[System.Serializable]
public class SearchOutcome
{
    public SearchOutcomeKind kind;
    public string label;        // 표에 보이는 이름 (게임에는 영향 없음)
    [Min(0)] public int weight; // 비중
    public int regionId;        // 이 결과가 나오는 지역 ID (0 = 모든 지역. 1 포레스트, 2 폭포, 3 그라운드, 4 광산, 5 채석장, 6 유적)
    public string text;         // 이벤트일 때 화면에 나오는 문구

    // 시간대별 배율 [%]: 비중에 곱해진다 (100 = 그대로, 0 = 그 시간대에는 안 나옴). 밤에는 몬스터가 늘어나는 식으로 쓴다
    [Min(0)] public float morningPercent = 100f;
    [Min(0)] public float afternoonPercent = 100f;
    [Min(0)] public float nightPercent = 100f;

    // 이 지역에서 나올 수 있는 결과인가 (지역 0이거나 같은 지역)
    public bool AppliesTo(int region)
    {
        return regionId == 0 || regionId == region;
    }

    // 지금 시간대의 배율을 곱한 비중
    public float TimedWeight(DayPeriod period)
    {
        float pct = period == DayPeriod.Morning ? morningPercent : period == DayPeriod.Afternoon ? afternoonPercent : nightPercent;
        return Mathf.Max(0, weight) * Mathf.Max(0f, pct) / 100f;
    }
}

[CreateAssetMenu(fileName = "SearchTable", menuName = "Soul/Search Table")]
public class SearchTable : ScriptableObject
{
    public List<SearchOutcome> entries = CreateDefaultEntries();

    // 비중대로 결과 하나를 뽑는다. 뽑을 수 있는 것이 없으면 null
    public SearchOutcome Pick(int regionId = 0)
    {
        DayPeriod period = GameTime.Period; // 시간대마다 몬스터/이벤트가 나올 확률이 다르다
        float total = 0f;
        foreach (SearchOutcome e in entries)
            if (e != null && e.AppliesTo(regionId)) total += e.TimedWeight(period);
        if (total <= 0f) return null;

        float roll = Random.Range(0f, total);
        foreach (SearchOutcome e in entries)
        {
            if (e == null) continue;
            if (!e.AppliesTo(regionId)) continue;
            roll -= e.TimedWeight(period);
            if (roll < 0f) return e;
        }
        return null;
    }

    private static SearchOutcome E(SearchOutcomeKind k, string label, int weight, string text, float morning = 100f, float afternoon = 100f, float night = 100f)
    {
        return new SearchOutcome { kind = k, label = label, weight = weight, text = text, morningPercent = morning, afternoonPercent = afternoon, nightPercent = night };
    }

    private static List<SearchOutcome> CreateDefaultEntries()
    {
        return new List<SearchOutcome>
        {
            E(SearchOutcomeKind.Resource, "자원 발견", 76, "", 110f, 100f, 80f),
            E(SearchOutcomeKind.Event, "아무것도 없음", 4, "아무것도 발견하지 못했다...", 100f, 100f, 200f),
            E(SearchOutcomeKind.Monster, "몬스터 조우", 20, "", 60f, 100f, 160f),
        };
    }

    [ContextMenu("기본값으로 되돌리기")]
    public void ResetToDefaults()
    {
        entries = CreateDefaultEntries();
    }
}
