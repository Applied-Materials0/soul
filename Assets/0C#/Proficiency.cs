using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 숙련도의 현재 상태(레벨/경험치). 게임을 켜 둔 동안 유지된다. 표(ProficiencyTable)는 필요 경험치와 보너스만 정한다.
// 숙련도마다 번호(id)가 있어서, 같은 종류의 숙련도가 여러 개여도 진행도가 따로 쌓인다.
//
// 행동을 하면 Reward()로 숙련도 경험치와 플레이어 레벨 경험치를 함께 얻는다.
// 숙련도 레벨의 보너스: 수확 보너스(%), 수량 추가(최소~최대), 레벨 경험치 보너스(%), 행동 SP 감소(%).
public static class Proficiency
{
    private static readonly Dictionary<int, int> levels = new Dictionary<int, int>();
    private static readonly Dictionary<int, int> exps = new Dictionary<int, int>();

    public static int Level(ProficiencyDef def)
    {
        return def != null && levels.TryGetValue(def.id, out int v) ? v : 1;
    }

    public static int Exp(ProficiencyDef def)
    {
        return def != null && exps.TryGetValue(def.id, out int v) ? v : 0;
    }

    // 지금 레벨의 보너스 줄 (없으면 null)
    public static ProficiencyLevel Current(ProficiencyDef def)
    {
        if (def == null) return null;
        int lv = Level(def);
        return lv >= 1 && lv <= def.levels.Count ? def.levels[lv - 1] : null;
    }

    // 현재 숙련도 레벨의 수확 보너스 (회복 숙련도에서는 독 내성)
    public static int Bonus(ProficiencyDef def)
    {
        ProficiencyLevel l = Current(def);
        return l != null ? l.bonus : 0;
    }

    // 현재 레벨에서 같은 아이템을 더 얻는 수량 (최소~최대 중에서 뽑음)
    public static int ExtraAmount(ProficiencyDef def)
    {
        ProficiencyLevel l = Current(def);
        if (l == null) return 0;
        int lo = Mathf.Max(0, Mathf.Min(l.extraMin, l.extraMax));
        int hi = Mathf.Max(0, Mathf.Max(l.extraMin, l.extraMax));
        return hi <= 0 ? 0 : Random.Range(lo, hi + 1);
    }

    // 행동에 드는 SP에서 숙련도의 SP 감소(정량)를 뺀다 (0 밑으로는 안 내려감)
    public static int ReducedSp(ProficiencyDef def, int baseCost)
    {
        ProficiencyLevel l = Current(def);
        if (l == null || baseCost <= 0 || l.spReduce <= 0) return baseCost;
        return Mathf.Max(0, baseCost - l.spReduce);
    }

    public static int ReducedSp(ProficiencyKind kind, int baseCost)
    {
        return ReducedSp(GameTables.Proficiency.Get(kind), baseCost);
    }

    // 숙련도 경험치를 더하고, 레벨이 올랐으면 true (newLevel에 새 레벨)
    public static bool AddExp(ProficiencyDef def, int amount, out int newLevel)
    {
        newLevel = Level(def);
        if (def == null || amount <= 0) return false;

        int exp = Exp(def) + amount;
        int lv = newLevel;
        bool up = false;

        while (lv < def.levels.Count)
        {
            int need = def.levels[lv - 1].expToNext;
            if (need <= 0 || exp < need) break;
            exp -= need;
            lv++;
            up = true;
        }

        levels[def.id] = lv;
        exps[def.id] = exp;
        newLevel = lv;
        return up;
    }

    // 행동을 했을 때: 숙련도 경험치와 플레이어 레벨 경험치를 함께 얻는다 (레벨 경험치에는 숙련도의 보너스가 곱해짐).
    // 숙련도나 플레이어 레벨이 오르면 그 알림을 화면 한가운데에 띄운다 (어디서 얻든 같은 자리). 돌려주는 글은 항상 빈 문자열이다.
    public static string Reward(ProficiencyDef def)
    {
        if (def == null) return "";
        if (AddExp(def, def.expPerUse, out int profLevel))
            ItemGainToast.ShowCenter($"{def.label} 숙련도가 올랐다! Lv.{profLevel}");

        if (def.playerExpPerUse > 0)
        {
            ProficiencyLevel l = Current(def);
            int playerExp = Mathf.Max(1, def.playerExpPerUse + (l != null ? l.expBonus : 0)); // 레벨의 경험치 보너스(정량)를 더한다
            int oldLevel = GameManager.Level;
            string levelText = LevelSystem.Describe(oldLevel, LevelSystem.AddExp(playerExp));
            if (levelText.Length > 0) ItemGainToast.ShowCenter(levelText);
        }

        PlayerUI.RefreshAll();
        return "";
    }

    public static string Reward(ProficiencyKind kind)
    {
        return Reward(GameTables.Proficiency.Get(kind));
    }

    // 자원을 채집할 때 쓰는 숙련도: 자원 표에서 정한 종류, 없으면 도구 종류로 자동 선택
    // (도끼 = 벌목, 곡괭이 = 채광, 낫 = 풀 베기). 예전 방식의 [Gather] 숙련도가 있으면 그것도 인정한다.
    public static ProficiencyDef ForGathering(ProficiencyKind spawnSkill, ToolType tool)
    {
        ProficiencyTable table = GameTables.Proficiency;

        ProficiencyKind kind;
        switch (spawnSkill)
        {
            case ProficiencyKind.Logging:
            case ProficiencyKind.Mining:
            case ProficiencyKind.Quarrying:
            case ProficiencyKind.Mowing:
                kind = spawnSkill;
                break;
            default:
                switch (tool)
                {
                    case ToolType.Axe: kind = ProficiencyKind.Logging; break;
                    case ToolType.Pickaxe: kind = ProficiencyKind.Mining; break;
                    case ToolType.Sickle: kind = ProficiencyKind.Mowing; break;
                    default: return table.GetGather(tool);
                }
                break;
        }
        return table.Get(kind) ?? table.GetGather(tool);
    }

    // 종류로 찾는 편의 함수 (종류마다 숙련도가 하나인 경우)
    public static int Level(ProficiencyKind kind) { return Level(GameTables.Proficiency.Get(kind)); }
    public static int Exp(ProficiencyKind kind) { return Exp(GameTables.Proficiency.Get(kind)); }
    public static int Bonus(ProficiencyKind kind) { return Bonus(GameTables.Proficiency.Get(kind)); }
    public static bool AddExp(ProficiencyKind kind, int amount, out int newLevel)
    {
        return AddExp(GameTables.Proficiency.Get(kind), amount, out newLevel);
    }
}
