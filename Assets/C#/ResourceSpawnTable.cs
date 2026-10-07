using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 필드 탐색에서 "자원을 발견했을 때" 어떤 자원이 나올지를 정하는 표.
// 한 줄 = 자원 한 종류. weight(비중)끼리의 비율대로 뽑힌다. 새 자원은 줄만 추가하면 된다.
[System.Serializable]
public class ResourceSpawn
{
    public string label;                 // 메모용 이름 (목록에서 구분하기 위한 이름이라 게임에는 영향 없음)
    [Min(0)] public int weight = 1;      // 비중. 표 안의 비중 합계 중 내 몫이 확률. 0이면 안 나옴

    [Header("화면에 보이는 문구")]
    public string foundText = "을(를) 발견했다.";  // 상황 텍스트 (예: 나뭇가지를 발견했다.)
    public string gatherButtonText = "채집";       // 채집 버튼 이름 (채집, 벌목, 채광...)

    [Header("얻는 아이템")]
    public int itemId;                   // 얻는 아이템 ID (ItemDatabase)

    [Header("필요 도구 (None이면 맨손)")]
    public ToolType toolType = ToolType.None;
    public int tier;                     // 필요 도구 티어

    [Header("자원 체력 (채집 횟수 범위, 둘 다 포함)")]
    [Min(1)] public int hpMin = 1;
    [Min(1)] public int hpMax = 1;

    [Header("채집 소리")]
    public GatherSoundType sound = GatherSoundType.Default;
}

[CreateAssetMenu(fileName = "ResourceSpawnTable", menuName = "Soul/Resource Spawn Table")]
public class ResourceSpawnTable : ScriptableObject
{
    // 에셋을 새로 만들면 현재 게임의 기본 비율이 미리 채워진다
    public List<ResourceSpawn> entries = CreateDefaultEntries();

    [Header("확률 미리보기 (자동 계산됨, 직접 수정해도 무시)")]
    [TextArea(4, 24)] public string probabilityPreview;

    // 비중대로 자원 한 종류를 뽑음. 뽑을 수 있는 항목이 없으면 null
    public ResourceSpawn Pick()
    {
        int total = 0;
        foreach (ResourceSpawn e in entries)
            if (e != null) total += Mathf.Max(0, e.weight);
        if (total <= 0) return null;

        int roll = Random.Range(0, total);
        foreach (ResourceSpawn e in entries)
        {
            if (e == null) continue;
            int w = Mathf.Max(0, e.weight);
            if (roll < w) return e;
            roll -= w;
        }
        return null;
    }

    // 비중을 고칠 때마다 항목별 실제 확률(%)을 계산해서 보여 줌
    private void OnValidate()
    {
        int total = 0;
        foreach (ResourceSpawn e in entries)
            if (e != null) total += Mathf.Max(0, e.weight);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"비중 합계: {total}");
        foreach (ResourceSpawn e in entries)
        {
            if (e == null) continue;
            float percent = total > 0 ? Mathf.Max(0, e.weight) * 100f / total : 0f;
            string title = string.IsNullOrEmpty(e.label) ? e.foundText : e.label;
            sb.AppendLine($"{title}: {percent:0.#}%  (비중 {e.weight})");
        }
        probabilityPreview = sb.ToString();
    }

    [ContextMenu("기본값으로 되돌리기")]
    private void ResetToDefaults()
    {
        entries = CreateDefaultEntries();
        OnValidate();
    }

    private static ResourceSpawn E(string label, int weight, string foundText, string button, int itemId,
        ToolType tool, int tier, int hpMin, int hpMax, GatherSoundType sound)
    {
        return new ResourceSpawn
        {
            label = label, weight = weight, foundText = foundText, gatherButtonText = button, itemId = itemId,
            toolType = tool, tier = tier, hpMin = hpMin, hpMax = hpMax, sound = sound
        };
    }

    // 지금까지 코드에 박혀 있던 비율 그대로 (합계 76)
    private static List<ResourceSpawn> CreateDefaultEntries()
    {
        return new List<ResourceSpawn>
        {
            E("나뭇가지",   5, "나뭇가지를 발견했다.",   "채집", 0,  ToolType.None,    0, 1, 1,   GatherSoundType.Default),
            E("덤불",       9, "덤불을 발견했다.",       "채집", 13, ToolType.Sickle,  0, 1, 4,   GatherSoundType.Bush),
            E("참나무",    11, "참나무를 발견했다.",     "벌목", 0,  ToolType.Axe,     1, 3, 9,   GatherSoundType.Logging),
            E("구리 조각",  3, "구리 조각을 발견했다.",  "채집", 1,  ToolType.None,    0, 1, 1,   GatherSoundType.Default),
            E("구리 광석",  2, "구리 광석을 발견했다.",  "채광", 1,  ToolType.Pickaxe, 1, 5, 14,  GatherSoundType.Mining),
            E("잡석 조각",  5, "잡석 조각을 발견했다.",  "채집", 2,  ToolType.None,    0, 1, 1,   GatherSoundType.Default),
            E("잡석",       3, "잡석을 발견했다.",       "채석", 2,  ToolType.Pickaxe, 1, 3, 9,   GatherSoundType.Mining),
            E("풀",        10, "풀을 발견했다.",         "채집", 13, ToolType.None,    0, 1, 1,   GatherSoundType.Bush),
            E("딸기",       7, "딸기를 발견했다.",       "채집", 16, ToolType.None,    0, 1, 4,   GatherSoundType.Bush),
            E("옥수수",     2, "옥수수를 발견했다.",     "채집", 17, ToolType.None,    0, 1, 4,   GatherSoundType.Bush),
            E("벌집",       3, "벌집을 발견했다.",       "채집", 18, ToolType.None,    0, 1, 1,   GatherSoundType.Default),
            E("달걀",       1, "달걀을 발견했다.",       "채집", 19, ToolType.None,    0, 1, 4,   GatherSoundType.Default),
            E("주황 버섯",  5, "주황 버섯을 발견했다.",  "채집", 26, ToolType.None,    0, 1, 1,   GatherSoundType.Default),
            E("푸른 버섯",  3, "푸른 버섯을 발견했다.",  "채집", 27, ToolType.None,    0, 1, 1,   GatherSoundType.Default),
            E("붉은 버섯",  1, "붉은 버섯을 발견했다.",  "채집", 28, ToolType.None,    0, 1, 1,   GatherSoundType.Default),
            E("연못",       6, "연못을 발견했다.",       "담기", 15, ToolType.Bottle,  0, 100, 100, GatherSoundType.Water),
        };
    }
}
