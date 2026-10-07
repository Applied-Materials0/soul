using System.Collections.Generic;

// 숙련도의 현재 상태(레벨/경험치). 게임을 켜 둔 동안 유지된다. 표(ProficiencyTable)는 필요 경험치와 보너스만 정한다.
public static class Proficiency
{
    private static readonly Dictionary<ProficiencyKind, int> levels = new Dictionary<ProficiencyKind, int>();
    private static readonly Dictionary<ProficiencyKind, int> exps = new Dictionary<ProficiencyKind, int>();

    public static int Level(ProficiencyKind kind)
    {
        return levels.TryGetValue(kind, out int v) ? v : 1;
    }

    public static int Exp(ProficiencyKind kind)
    {
        return exps.TryGetValue(kind, out int v) ? v : 0;
    }

    // 현재 숙련도 레벨의 수확 보너스 [%]
    public static float Bonus(ProficiencyKind kind)
    {
        ProficiencyDef def = GameTables.Proficiency.Get(kind);
        if (def == null) return 0f;
        int lv = Level(kind);
        return lv >= 1 && lv <= def.levels.Count ? def.levels[lv - 1].bonusPercent : 0f;
    }

    // 숙련도 경험치를 더하고, 레벨이 올랐으면 true (newLevel에 새 레벨)
    public static bool AddExp(ProficiencyKind kind, int amount, out int newLevel)
    {
        newLevel = Level(kind);
        ProficiencyDef def = GameTables.Proficiency.Get(kind);
        if (def == null || amount <= 0) return false;

        int exp = Exp(kind) + amount;
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

        levels[kind] = lv;
        exps[kind] = exp;
        newLevel = lv;
        return up;
    }
}
