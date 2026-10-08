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
    public string text;         // 이벤트일 때 화면에 나오는 문구
}

[CreateAssetMenu(fileName = "SearchTable", menuName = "Soul/Search Table")]
public class SearchTable : ScriptableObject
{
    public List<SearchOutcome> entries = CreateDefaultEntries();

    // 비중대로 결과 하나를 뽑는다. 뽑을 수 있는 것이 없으면 null
    public SearchOutcome Pick()
    {
        int total = 0;
        foreach (SearchOutcome e in entries)
            if (e != null) total += Mathf.Max(0, e.weight);
        if (total <= 0) return null;

        int roll = Random.Range(0, total);
        foreach (SearchOutcome e in entries)
        {
            if (e == null) continue;
            roll -= Mathf.Max(0, e.weight);
            if (roll < 0) return e;
        }
        return null;
    }

    private static SearchOutcome E(SearchOutcomeKind k, string label, int weight, string text)
    {
        return new SearchOutcome { kind = k, label = label, weight = weight, text = text };
    }

    private static List<SearchOutcome> CreateDefaultEntries()
    {
        return new List<SearchOutcome>
        {
            E(SearchOutcomeKind.Resource, "자원 발견", 76, ""),
            E(SearchOutcomeKind.Event, "아무것도 없음", 4, "아무것도 발견하지 못했다..."),
            E(SearchOutcomeKind.Monster, "몬스터 조우", 20, ""),
        };
    }

    [ContextMenu("기본값으로 되돌리기")]
    public void ResetToDefaults()
    {
        entries = CreateDefaultEntries();
    }
}
