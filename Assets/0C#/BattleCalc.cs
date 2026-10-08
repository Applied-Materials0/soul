using UnityEngine;

// 전투 계산식 모음. 데미지 공식을 고치고 싶으면 이 파일만 보면 된다.
// (턴 진행, 문구, 보상 지급은 BattleSystem이 맡는다.)
//
// 용어: "증감률"은 [%] 값이다. 0이면 변화 없음, 50이면 +50%, -20이면 -20%.
public static class BattleCalc
{
    // 몬스터가 [방어]했을 때 방어력 증가 [%] (플레이어의 GameManager.DefendBonus와 같은 개념)
    public const float MonsterDefendBonus = 50f;

    // [%] 확률 판정: percent가 30이면 30% 확률로 true
    public static bool Roll(float percent)
    {
        return Random.value * 100f < percent;
    }

    // 증감률(%)을 곱하는 값으로 바꾼다: 0 -> 1.0, 50 -> 1.5, -20 -> 0.8
    public static float Mult(float ratePercent)
    {
        return Mathf.Max(0f, 1f + ratePercent / 100f);
    }

    // 플레이어의 최대 체력 = 최대 체력 x 체력 증감률
    public static int PlayerMaxHp()
    {
        return Mathf.Max(1, Mathf.RoundToInt((GameManager.HpMax + GameManager.EquipHp) * Mult(GameManager.HpRate)));
    }

    // =========================================================
    //  [공격] 플레이어 -> 몬스터
    // =========================================================
    public struct AttackResult
    {
        public int damage;     // 준 피해 (고정 데미지 포함)
        public int fixedDamage; // 그중 방어력과 상관없는 고정 피해
        public int heal;       // 흡혈로 회복한 체력
        public bool crit;      // 치명타 여부
        public bool dodged;    // 몬스터가 회피함
        public bool noEffect;  // 피해가 0 ("효과가 없는 것 같다...")
    }

    // 계산 순서:
    //  1) 몬스터 회피율로 회피 판정. 회피하면 피해 0.
    //  2) 공격력 = 플레이어 공격력 + 도구/장비 공격력 + 특수 공격력(적 최대체력 비례 데미지 = 적 최대체력 x 체력비례공격%)
    //  3) 치명타 확률에 걸리면 공격력 x (1 + 치명타 피해 증가%)
    //  4) 공격력 x 공격력 증감률
    //  5) 적 방어력 = 적 방어력 x (1 - 방어 관통%). 몬스터가 [방어] 중이면 방어력 x (1 + 몬스터 방어 보너스%)
    //  6) 피해 = 공격력 - 적 방어력 (0 미만이면 0), 여기에 방어력과 상관없는 고정 데미지를 더함
    //  7) 흡혈 = 준 피해 x 체력 흡수%
    public static AttackResult PlayerAttack(Monster m, bool monsterDefending)
    {
        AttackResult r = new AttackResult();

        if (Roll(m.avoid))
        {
            r.dodged = true;
            return r;
        }

        float special = m.hpMax * GameManager.HpRateAt / 100f;
        float atk = GameManager.At + GameManager.EquipAt + special;

        r.crit = Roll(GameManager.CriticalRate);
        if (r.crit) atk *= Mult(GameManager.Critical);

        atk *= Mult(GameManager.AtRate);

        float def = m.df * (1f - Mathf.Clamp(GameManager.BreakDf, 0f, 100f) / 100f);
        if (monsterDefending) def *= Mult(MonsterDefendBonus);

        int main = Mathf.Max(0, Mathf.FloorToInt(atk - def));
        int total = main + Mathf.Max(0, GameManager.FixAt);

        r.fixedDamage = Mathf.Max(0, GameManager.FixAt);
        r.damage = total;
        r.noEffect = total <= 0;
        r.heal = total > 0 ? Mathf.FloorToInt(total * Mathf.Max(0f, GameManager.Abs) / 100f) : 0;
        return r;
    }

    // =========================================================
    //  몬스터 -> 플레이어
    // =========================================================
    public struct HitResult
    {
        public int damage;     // 받은 피해
        public bool dodged;    // 플레이어가 회피함
        public bool noEffect;  // 피해가 0 ("효과가 없는 것 같다...")
    }

    // 계산 순서:
    //  1) 플레이어 회피율로 회피 판정. 회피하면 피해 0.
    //  2) 플레이어 방어력 = 방어력 x 방어력 증감률. [방어] 중이면 방어력 x (1 + 방어 보너스%) (기본 +50%, 장비로 추가)
    //  3) 피해 = 몬스터 공격력 - 플레이어 방어력 (0 미만이면 0)
    public static HitResult MonsterAttack(Monster m, bool playerDefending)
    {
        HitResult h = new HitResult();

        if (Roll(GameManager.Avoid))
        {
            h.dodged = true;
            return h;
        }

        float def = (GameManager.Df + GameManager.EquipDf) * Mult(GameManager.DfRate);
        if (playerDefending) def *= Mult(GameManager.DefendBonus);

        h.damage = Mathf.Max(0, Mathf.FloorToInt(m.at - def));
        h.noEffect = h.damage <= 0;
        return h;
    }
}
