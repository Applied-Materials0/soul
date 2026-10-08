using System.Collections.Generic;
using UnityEngine;

// 숙련도 표. 숙련도 종류마다 가로가 레벨, 세로가 세부 항목이다 (Soul > 데이터 표 > [숙련도] 탭).
// 행동을 하면 그 종류의 숙련도 경험치와 플레이어 레벨 경험치를 함께 얻는다.
// 에셋은 Assets/Resources/Tables/ProficiencyTable.asset.
//
// 번호(숫자)는 저장된 에셋에 그대로 들어 있으므로 바꾸거나 순서를 옮기지 말고, 새 종류는 맨 뒤에 추가할 것.
public enum ProficiencyKind
{
    Carving,    // 0 도려내기 (칼로 가죽/고기 얻기)
    Gather,     // 1 채집 (예전 방식: 아래 "도구" 종류로 채집할 때. 자원 표의 숙련도 칸에서 [Gather]는 "도구 종류로 자동 선택"이라는 뜻)
    Logging,    // 2 벌목 (도끼)
    Mining,     // 3 채광 (곡괭이로 광석)
    Mowing,     // 4 풀 베기 (낫)
    Recovery,   // 5 회복 (아이템 사용 시). 레벨의 [수확 보너스] 칸이 독 내성(%)이 됨
    Combat,     // 6 전투 (공격, 방어 시)
    Quarrying,  // 7 채석 (곡괭이로 돌)
    Crafting,   // 8 제작
}

[System.Serializable]
public class ProficiencyLevel
{
    public int level;                   // 숙련도 레벨 (표가 자동으로 맞춤)
    [Min(0)] public int expToNext;      // 다음 숙련도 레벨까지 필요한 경험치 (마지막 레벨은 0)
    public float bonusPercent;          // 수확 보너스 [%] (얻는 수량이 이만큼 늘어남). 회복 숙련도에서는 독 내성 [%]
    [Min(0)] public int extraMin;       // 채집/도려내기 때 같은 아이템을 최소 이만큼 더 얻음
    [Min(0)] public int extraMax;       // 최대 이만큼 더 얻음 (최소~최대 중에서 뽑음)
    public float expBonusPercent;       // 이 숙련도 행동으로 얻는 플레이어 레벨 경험치가 늘어나는 비율 [%]
    public float spReducePercent;       // 이 숙련도 행동에 드는 SP가 줄어드는 비율 [%]
}

[System.Serializable]
public class ProficiencyDef
{
    public int id;                      // 숙련도 번호 (겹치지 않게. 표의 [+ 숙련도 추가]가 자동으로 매김). 진행도(레벨/경험치)를 이 번호로 구분함
    public ProficiencyKind kind;
    public ToolType tool;               // 종류가 [Gather]일 때, 이 도구 종류로 채집하면 경험치가 오름 (None = 맨손)
    public string label;                // 표에 보이는 이름
    [Min(0)] public int expPerUse = 1;  // 한 번 행동할 때 오르는 숙련도 경험치
    [Min(0)] public int playerExpPerUse = 1; // 한 번 행동할 때 얻는 플레이어 레벨 경험치 (레벨 경험치 보너스가 곱해짐)
    public List<ProficiencyLevel> levels = new List<ProficiencyLevel>();
}

[CreateAssetMenu(fileName = "ProficiencyTable", menuName = "Soul/Proficiency Table")]
public class ProficiencyTable : ScriptableObject
{
    public List<ProficiencyDef> defs = CreateDefaultDefs();

    // 기본으로 있어야 하는 숙련도 종류들 (표에 없으면 에디터가 기본값으로 추가하고, 그 전에는 코드가 기본값으로 동작함)
    public static readonly ProficiencyKind[] DefaultKinds =
    {
        ProficiencyKind.Carving, ProficiencyKind.Logging, ProficiencyKind.Mining, ProficiencyKind.Quarrying,
        ProficiencyKind.Mowing, ProficiencyKind.Crafting, ProficiencyKind.Combat, ProficiencyKind.Recovery,
    };

    // 채집 숙련도 중 해당 도구 종류에 맞는 것 (예전 방식의 [Gather] 숙련도)
    public ProficiencyDef GetGather(ToolType tool)
    {
        foreach (ProficiencyDef d in defs)
            if (d != null && d.kind == ProficiencyKind.Gather && d.tool == tool) return d;
        return null;
    }

    private static readonly Dictionary<ProficiencyKind, ProficiencyDef> fallbacks = new Dictionary<ProficiencyKind, ProficiencyDef>();

    public ProficiencyDef Get(ProficiencyKind kind)
    {
        foreach (ProficiencyDef d in defs)
            if (d != null && d.kind == kind) return d;

        // 표에 그 종류가 없으면 기본값으로 대신 동작한다 (에디터를 열면 표에 자동으로 추가됨)
        if (kind == ProficiencyKind.Gather) return null;
        if (!fallbacks.TryGetValue(kind, out ProficiencyDef def))
        {
            def = MakeDefault(kind);
            fallbacks[kind] = def;
        }
        return def;
    }

    public static string DefaultLabel(ProficiencyKind kind)
    {
        switch (kind)
        {
            case ProficiencyKind.Carving: return "도려내기";
            case ProficiencyKind.Logging: return "벌목";
            case ProficiencyKind.Mining: return "채광";
            case ProficiencyKind.Quarrying: return "채석";
            case ProficiencyKind.Mowing: return "풀 베기";
            case ProficiencyKind.Crafting: return "제작";
            case ProficiencyKind.Combat: return "전투";
            case ProficiencyKind.Recovery: return "회복";
            default: return kind.ToString();
        }
    }

    // 종류별 기본 숙련도 (레벨 10단계). 레벨이 오를수록 수확 보너스/수량 추가/레벨 경험치 보너스/SP 감소가 커진다.
    public static ProficiencyDef MakeDefault(ProficiencyKind kind)
    {
        int id;
        switch (kind)
        {
            case ProficiencyKind.Carving: id = 0; break;
            case ProficiencyKind.Logging: id = 1; break;
            case ProficiencyKind.Mining: id = 2; break;
            case ProficiencyKind.Quarrying: id = 3; break;
            case ProficiencyKind.Mowing: id = 4; break;
            case ProficiencyKind.Crafting: id = 5; break;
            case ProficiencyKind.Combat: id = 6; break;
            case ProficiencyKind.Recovery: id = 1000; break;
            default: id = 2000 + (int)kind; break;
        }

        bool recovery = kind == ProficiencyKind.Recovery;
        bool combat = kind == ProficiencyKind.Combat;
        bool gatherLike = kind == ProficiencyKind.Logging || kind == ProficiencyKind.Mining
            || kind == ProficiencyKind.Quarrying || kind == ProficiencyKind.Mowing;

        ProficiencyDef def = new ProficiencyDef
        {
            id = id, kind = kind, label = DefaultLabel(kind),
            expPerUse = recovery ? 10 : 1,   // 회복은 위험한 아이템도 먹게 되므로 한 번에 많이 오른다
            playerExpPerUse = 1,
        };

        int[] exp = recovery ? new[] { 20, 40, 80, 140, 220, 320, 440, 600, 800, 0 }
                  : combat ? new[] { 10, 20, 40, 70, 110, 160, 220, 300, 400, 0 }
                  : new[] { 5, 10, 20, 35, 55, 80, 110, 150, 200, 0 };

        for (int i = 0; i < exp.Length; i++)
        {
            int lv = i + 1;
            ProficiencyLevel l = new ProficiencyLevel { level = lv, expToNext = exp[i] };
            l.bonusPercent = (recovery || kind == ProficiencyKind.Carving || gatherLike) ? i * 10f : 0f;
            if (gatherLike || kind == ProficiencyKind.Carving)
            {
                l.extraMin = lv >= 7 ? 1 : 0;
                l.extraMax = lv >= 7 ? 2 : (lv >= 4 ? 1 : 0);
            }
            l.expBonusPercent = i * 5f;
            l.spReducePercent = (gatherLike || combat || kind == ProficiencyKind.Carving) ? i * 3f : 0f;
            def.levels.Add(l);
        }
        return def;
    }

    private static List<ProficiencyDef> CreateDefaultDefs()
    {
        List<ProficiencyDef> list = new List<ProficiencyDef>();
        foreach (ProficiencyKind k in DefaultKinds) list.Add(MakeDefault(k));
        return list;
    }

    // 예전 코드 호환: 회복 숙련도 기본값
    public static ProficiencyDef CreateRecoveryDef() { return MakeDefault(ProficiencyKind.Recovery); }

    [ContextMenu("기본값으로 되돌리기")]
    public void ResetToDefaults()
    {
        defs = CreateDefaultDefs();
    }
}
