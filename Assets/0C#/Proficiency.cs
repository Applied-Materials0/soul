using System.Collections.Generic;

// 숙련도의 현재 상태(레벨/경험치). 게임을 켜 둔 동안 유지된다. 표(ProficiencyTable)는 필요 경험치와 보너스만 정한다.
// 숙련도마다 번호(id)가 있어서, 같은 종류(예: 채집)의 숙련도가 여러 개여도 진행도가 따로 쌓인다.
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

    // 현재 숙련도 레벨의 수확 보너스 [%]
    public static float Bonus(ProficiencyDef def)
    {
        if (def == null) return 0f;
        int lv = Level(def);
        return lv >= 1 && lv <= def.levels.Count ? def.levels[lv - 1].bonusPercent : 0f;
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

    // 종류로 찾는 편의 함수 (도려내기처럼 종류마다 숙련도가 하나인 경우)
    public static int Level(ProficiencyKind kind) { return Level(GameTables.Proficiency.Get(kind)); }
    public static int Exp(ProficiencyKind kind) { return Exp(GameTables.Proficiency.Get(kind)); }
    public static float Bonus(ProficiencyKind kind) { return Bonus(GameTables.Proficiency.Get(kind)); }
    public static bool AddExp(ProficiencyKind kind, int amount, out int newLevel)
    {
        return AddExp(GameTables.Proficiency.Get(kind), amount, out newLevel);
    }
}
