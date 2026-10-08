using System.Collections.Generic;

// 경험치를 받아 레벨을 올리는 계산. 필요 경험치와 오르는 능력치는 LevelTable(레벨 표)이 정한다.
public static class LevelSystem
{
    public struct LevelUp
    {
        public int newLevel;
        public int hpMax, at, df, spMax, speed; // 이 레벨에 도달하며 늘어난 능력치
    }

    // 현재 레벨에서 다음 레벨까지 필요한 경험치 (최고 레벨이면 0)
    public static int ExpToNext(int level)
    {
        return GameTables.Levels.ExpToNext(level);
    }

    // 레벨업 알림 문구: "레벨이 올랐다! Lv.2 -> Lv.4 (체력 +10 공격력 +3)". 능력치는 오른 레벨 전체의 합계. 오르지 않았으면 빈 문자열
    public static string Describe(int oldLevel, List<LevelUp> ups)
    {
        if (ups == null || ups.Count == 0) return "";
        int hp = 0, at = 0, df = 0, sp = 0, speed = 0;
        foreach (LevelUp up in ups) { hp += up.hpMax; at += up.at; df += up.df; sp += up.spMax; speed += up.speed; }

        string gains = "";
        if (hp != 0) gains += $" 체력 +{hp}";
        if (at != 0) gains += $" 공격력 +{at}";
        if (df != 0) gains += $" 방어력 +{df}";
        if (sp != 0) gains += $" SP +{sp}";
        if (speed != 0) gains += $" 속도 +{speed}";

        string text = $"레벨이 올랐다! Lv.{oldLevel} -> Lv.{GameManager.Level}";
        if (gains.Length > 0) text += $" ({gains.Trim()})";
        return text;
    }

    // 경험치를 더한다. 레벨이 올랐으면 오른 레벨마다 한 항목씩 돌려준다.
    public static List<LevelUp> AddExp(int amount)
    {
        List<LevelUp> result = new List<LevelUp>();
        GameManager.Exp += amount;

        while (true)
        {
            int need = ExpToNext(GameManager.Level);
            if (need <= 0 || GameManager.Exp < need) break;

            GameManager.Exp -= need;
            GameManager.Level++;

            LevelEntry e = GameTables.Levels.Get(GameManager.Level);
            LevelUp up = new LevelUp { newLevel = GameManager.Level };
            if (e != null)
            {
                up.hpMax = e.hpMaxGain; up.at = e.atGain; up.df = e.dfGain; up.spMax = e.spMaxGain; up.speed = e.speedGain;

                GameManager.HpMax += e.hpMaxGain;
                GameManager.Hp += e.hpMaxGain; // 늘어난 최대 체력만큼 현재 체력도 함께 오른다
                GameManager.At += e.atGain;
                GameManager.Df += e.dfGain;
                GameManager.SPMax += e.spMaxGain;
                GameManager.Speed += e.speedGain;
            }
            result.Add(up);
        }

        GameManager.ExpNext = ExpToNext(GameManager.Level);
        return result;
    }
}
