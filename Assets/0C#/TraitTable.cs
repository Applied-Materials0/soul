using System.Collections.Generic;
using UnityEngine;

// 특성 표. 장비에 특성 ID를 적어 두면, 그 장비를 "장착"했을 때 특성의 효과가 적용된다 (Soul > 데이터 표 > [특성] 탭).
// 한 줄 = 특성 하나. 값이 0(또는 꺼짐)인 칸은 그 효과가 없다는 뜻이라, 필요한 칸만 채우면 된다.
// 에셋은 Assets/Resources/Tables/TraitTable.asset.
//
// 효과는 크게 네 갈래다 (칸 이름 옆 설명은 표의 칸 이름에 마우스를 올리면 나온다):
//  1) 능력치: 장착하면 계속 적용 (공격/방어/체력 증가량, 방어 관통, 고정 데미지, 체력 퍼뎀, 치명타, 흡수, 최대 마나/SP 증가)
//  2) 회복: 전투 시작 때 한 번(회복량), 턴마다(턴당 회복, 마나 회복)
//  3) 상태이상: 공격으로 적에게 붙이는 지속 피해 (독, 화염 ...). 몬스터가 플레이어에게 붙일 수도 있다 (몬스터 표의 [특성 공격])
//  4) 전투 효과: 처형, 반사, 완전 방어, 부활, 필중, 선제공격 ... (횟수 칸은 "한 전투에 몇 번"이고, 선제공격만 "하루에 몇 번")
// 체력 퍼뎀과 턴당 체력 퍼뎀은 대상의 "현재 체력"의 비율이다 (최대 체력이 아님).
[System.Serializable]
public class TraitDef
{
    [Header("기본")]
    public int id;                 // 특성 ID (장비 표의 "특성ID"에 적음)
    public string label;           // 이름 (버프 창에 보임)
    public string description;     // 설명
    public bool debuff;            // 디버프 특성: 상대가 이 특성을 걸 때, 이미 같은 특성을 가진 쪽에는 걸리지 않는다
    public WeaponType weaponType;  // 무기 종류: 정해 두면 그 종류의 무기(장비 표의 [무기 종류])에 붙어 있을 때만 적용 (None = 어떤 장비든)

    [Header("능력치 (장착 중 항상)")]
    public float atRate;           // 공격 증가량 [%] (마이너스면 감소)
    public float dfRate;           // 방어 증가량 [%] (마이너스면 디버프)
    public float hpRate;           // 체력 증가량 [%] (최대 체력)
    public float resistPercent;    // 내성 적용 [%]: 이 특성의 공격(방어력 감소, 지속 피해)이 독 내성에 의해 줄어드는 정도. 100이면 내성만큼 그대로, 0이면 내성이 안 통함 (내성은 회복 숙련도로 오름)
    public float breakDf;          // 방어 관통 [%]
    public int fixAt;              // 고정 데미지 (방어력과 상관없이 더해지는 피해)
    public float hpRateAt;         // 체력 퍼뎀 [%]: 적의 현재 체력의 이 비율만큼 추가 피해
    public float criticalRate;     // 치명타 확률 [%]
    public float critical;         // 치명타 데미지 [%]
    public float abs;              // 흡수 [%]: 준 피해의 이 비율만큼 체력 회복
    public float manaRate;         // 최대 마나 증가 [%]
    public float skillManaReduce;  // 스킬 마나 감소 [%] (스킬이 생기면 적용)
    public float spRate;           // 최대 SP 증가 [%]

    [Header("회복")]
    public int healAmount;         // 회복량: 전투를 시작할 때 한 번 회복하는 체력
    public float healPercent;      // 회복량 [%]: 전투를 시작할 때 한 번 회복하는 체력 (최대 체력의 %)
    public int regenPerTurn;       // 턴당 회복: 전투 중 턴마다 회복하는 체력
    public float regenHpPercent;   // 턴당 회복 [%]: 턴마다 회복하는 체력 (최대 체력의 %)
    public int manaPerTurn;        // 마나 회복: 전투 중 턴마다 회복하는 마나
    public float manaPercentPerTurn; // 마나 회복 [%]: 턴마다 회복하는 마나 (최대 마나의 %)

    [Header("상태이상 (공격으로 적에게 붙임. 몬스터가 붙일 수도 있음)")]
    public float inflictChance;    // 부여 확률 [%]: 공격이 들어갔을 때 이 특성의 상태이상을 붙일 확률
    public float dotDamage;        // 턴당 지속 데미지 (첫 턴)
    public float dotGrowth;        // 턴당 지속 데미지 증가 (턴이 지날 때마다 늘어남)
    public float dotHpPercent;     // 턴당 체력 퍼뎀 [%] (대상의 현재 체력 비율)
    public float dotHpGrowth;      // 턴당 체력 퍼뎀 증가 [%p] (턴이 지날 때마다 늘어남)
    public float healReducePercent; // 적 회복 감소 [%]: 상태이상에 걸린 동안 그 대상의 회복량이 줄어듦
    [Min(0)] public int dotTurns;  // 지속 턴 수

    [Header("전투 효과 (전투 중에만)")]
    public float executeHpPercent; // 처형 체력 [%]: 적의 현재 체력이 최대 체력의 이 비율 미만이면 공격 때 바로 처형
    public float reflectPercent;   // 반사 [%]: 적에게 받은 데미지의 이 비율을 적에게 되돌림
    public int perfectDefendCount; // 완전 방어 횟수: 한 전투에서 [방어]가 공격을 완전히 막는 횟수
    public bool stealBuff;         // 상대 버프 탈취 (아직 구현 안 됨. 표에만 있음)
    public bool cleanse;           // 나의 디버프 제거 (매 턴 내게 걸린 상태이상을 지우고 디버프 효과를 받지 않음)
    public int reviveCount;        // 부활 횟수: 한 전투에서 체력이 0이 되었을 때 되살아나는 횟수
    public float reviveHpPercent = 50f; // 부활 체력 [%]: 부활할 때 회복하는 체력 (최대 체력의 %)
    public int sureHitCount;       // 필중 횟수: 한 전투에서 적의 회피와 방어 태세를 무시하고 반드시 맞히는 공격 횟수
    public int firstStrikeCount;   // 선제공격 횟수: 하루(휴식으로 다음 날이 되면 초기화) 동안 전투 시작 때 내가 먼저 움직이는 횟수
}

[CreateAssetMenu(fileName = "TraitTable", menuName = "Soul/Trait Table")]
public class TraitTable : ScriptableObject
{
    public List<TraitDef> traits = CreateDefaultTraits();

    public TraitDef Get(int id)
    {
        if (id <= 0) return null;
        foreach (TraitDef t in traits)
            if (t != null && t.id == id) return t;
        return null;
    }

    // 기본으로 있어야 하는 특성들 (에디터가 정해진 때 한 번 표에 반영해 줌. GameTablesBootstrap)
    public static List<TraitDef> CreateDefaultTraits()
    {
        return new List<TraitDef>
        {
            new TraitDef
            {
                id = 1, label = "독", debuff = true, atRate = 30f, dfRate = -30f, resistPercent = 100f,
                inflictChance = 30f, dotDamage = 2f, dotGrowth = 1f, dotHpPercent = 1f, dotTurns = 5,
                description = "독을 머금어 공격력이 오른다. 독 내성이 없으면 방어력이 줄어든다. 공격하면 적에게 독이 퍼진다."
            },
            new TraitDef { id = 2, label = "마나 도둑", manaPerTurn = 2, description = "턴마다 마나를 회복한다." },
            new TraitDef { id = 3, label = "처형", executeHpPercent = 15f, description = "체력이 15% 미만인 적을 공격하면 바로 처형한다." },
            new TraitDef { id = 4, label = "거인", hpRate = 30f, dfRate = 20f, description = "거인의 힘으로 체력과 방어력이 오른다." },
            new TraitDef { id = 5, label = "가시", reflectPercent = 30f, description = "받은 피해의 30%를 적에게 되돌린다." },
            new TraitDef { id = 6, label = "굳히기", perfectDefendCount = 1, description = "한 전투에 한 번, [방어]하면 적의 공격을 완전히 막는다." },
            new TraitDef { id = 7, label = "속임수", stealBuff = true, description = "상대의 버프를 빼앗는다. (아직 구현되지 않음)" },
            new TraitDef { id = 8, label = "정화", cleanse = true, description = "매 턴 내게 걸린 디버프를 지운다." },
            new TraitDef { id = 9, label = "부활", reviveCount = 1, reviveHpPercent = 50f, description = "체력이 0이 되면 체력의 50%로 부활한다. (한 전투에 1번)" },
            new TraitDef
            {
                id = 10, label = "화염", debuff = true, resistPercent = 100f, inflictChance = 30f, dotDamage = 3f, dotHpPercent = 2f,
                healReducePercent = 50f, dotTurns = 4,
                description = "공격하면 적이 불타 지속 피해를 입고 회복량이 줄어든다."
            },
            new TraitDef { id = 11, label = "회복", healAmount = 5, regenPerTurn = 2, regenHpPercent = 1f, description = "전투 시작 때 체력을 회복하고, 턴마다 체력이 조금씩 회복된다." },
            new TraitDef { id = 12, label = "명사수", sureHitCount = 3, description = "한 전투에 3번, 적의 회피와 방어 태세를 무시하고 반드시 맞힌다." },
            new TraitDef { id = 13, label = "퀵턴", firstStrikeCount = 1, description = "하루에 한 번, 전투를 시작할 때 반드시 먼저 공격한다." },
            new TraitDef { id = 14, label = "격파", breakDf = 20f, fixAt = 3, hpRateAt = 3f, description = "방어 관통, 고정 데미지, 체력 퍼뎀이 오른다." },
            new TraitDef { id = 15, label = "마법", manaRate = 30f, skillManaReduce = 20f, description = "최대 마나가 늘고 스킬의 마나 소모가 줄어든다." },
            new TraitDef { id = 16, label = "치명", atRate = 10f, criticalRate = 10f, critical = 30f, description = "공격력, 치명타 확률, 치명타 데미지가 오른다." },
            new TraitDef { id = 17, label = "흡수", abs = 15f, description = "피해를 줄 때 체력을 흡수한다." },
            new TraitDef { id = 18, label = "다크나이트", weaponType = WeaponType.Mace, atRate = 20f, hpRateAt = 3f, description = "메이스의 공격력과 체력 퍼뎀이 오른다." },
            new TraitDef { id = 19, label = "레인저", weaponType = WeaponType.Bow, atRate = 20f, fixAt = 3, description = "활의 공격력과 고정 데미지가 오른다." },
            new TraitDef { id = 20, label = "검성", weaponType = WeaponType.Sword, atRate = 20f, critical = 30f, criticalRate = 10f, description = "검의 공격력, 치명타 배율, 치명타 확률이 오른다." },
            new TraitDef { id = 21, label = "도살자", weaponType = WeaponType.BattleAxe, atRate = 20f, abs = 10f, description = "배틀 엑스의 공격력과 흡수가 오른다." },
        };
    }

    [ContextMenu("기본값으로 되돌리기")]
    public void ResetToDefaults()
    {
        traits = CreateDefaultTraits();
    }
}
