using System.Collections.Generic;
using UnityEngine;

public enum SkillKind
{
    Attack,   // 공격 스킬: 적을 공격한다 (위력, 연타, 방어 관통, 상태이상 ...)
    Heal,     // 회복 스킬: 내 체력을 회복한다
}

// 스킬 표. 한 줄 = 스킬 하나 (Soul > 데이터 표 > [스킬] 탭). 에셋은 Assets/Resources/Tables/SkillTable.asset.
// 전투에서 [스킬] 버튼(또는 S 키)을 누르면 스킬 창이 뜨고, 고른 스킬을 쓰면 이번 턴을 쓴다.
// 비용은 마나(최대 마나는 기본 능력치 표 + 장비 특성)와 SP. 마나는 다음 날이 되면 가득 찬다.
// 값이 0인 칸은 그 효과가 없다는 뜻이라, 필요한 칸만 채우면 된다.
[System.Serializable]
public class SkillDef
{
    [Header("기본")]
    public int id;                 // 스킬 번호
    public string label;           // 이름
    public string description;     // 설명 (스킬 창에 보임)
    public SkillKind kind;         // 공격 / 회복

    [Header("사용 조건과 비용")]
    public int unlockLevel = 1;    // 플레이어가 이 레벨이 되면 쓸 수 있다
    public WeaponType weaponType;  // 이 종류의 무기를 장착해야 쓸 수 있다 (None = 어떤 무기든)
    public int manaCost;           // 마나 소모 (특성 "스킬 마나 감소 %"만큼 줄어듦)
    public int spCost;             // SP 소모
    [Min(0)] public int cooldown;  // 쓴 뒤 다시 쓰기까지 기다리는 턴 수 (한 전투 안에서)

    [Header("공격 스킬")]
    [Min(1)] public int hits = 1;  // 때리는 횟수 (한 번에 연속해서)
    public float powerPercent = 100f; // 위력 %: 일반 공격 대비 한 번당 공격력 (100 = 일반 공격과 같음)
    public int fixedDamage;        // 한 번당 더해지는 고정 데미지
    public float breakDf;          // 추가 방어 관통 %
    public float criticalRate;     // 추가 치명타 확률 %
    public bool sureHit;           // 필중: 회피와 방어 태세를 무시
    public int inflictTraitId;     // 맞추면 적에게 거는 상태이상 (특성 표의 ID, 0 = 없음. 예: 1 = 독)
    public float inflictChance;    // 상태이상 부여 확률 %

    [Header("회복 스킬")]
    public int healAmount;         // 회복량
    public float healPercent;      // 회복량 % (최대 체력 비율)
    public bool cleanse;           // 내게 걸린 상태이상을 지움
}

[CreateAssetMenu(fileName = "SkillTable", menuName = "Soul/Skill Table")]
public class SkillTable : ScriptableObject
{
    public List<SkillDef> skills = CreateDefaultSkills();

    public SkillDef Get(int id)
    {
        foreach (SkillDef s in skills)
            if (s != null && s.id == id) return s;
        return null;
    }

    public static List<SkillDef> CreateDefaultSkills()
    {
        return new List<SkillDef>
        {
            new SkillDef { id = 1, label = "강타", unlockLevel = 1, manaCost = 3, powerPercent = 150f, cooldown = 1,
                description = "온 힘을 실어 내리친다. 일반 공격보다 강하다." },
            new SkillDef { id = 2, label = "관통", unlockLevel = 2, manaCost = 3, powerPercent = 100f, breakDf = 50f,
                description = "적의 방어력을 절반 무시하고 찌른다." },
            new SkillDef { id = 3, label = "응급 처치", kind = SkillKind.Heal, unlockLevel = 2, manaCost = 4, cooldown = 3, healPercent = 25f, cleanse = true,
                description = "체력을 회복하고 걸린 상태이상을 치료한다." },
            new SkillDef { id = 4, label = "연속 베기", unlockLevel = 3, manaCost = 4, hits = 2, powerPercent = 70f,
                description = "두 번 연달아 벤다." },
            new SkillDef { id = 5, label = "독침", unlockLevel = 4, manaCost = 3, powerPercent = 60f, inflictTraitId = 1, inflictChance = 100f,
                description = "약하게 찌르고 적에게 독을 퍼뜨린다." },
            new SkillDef { id = 6, label = "조준 사격", unlockLevel = 5, manaCost = 5, powerPercent = 120f, sureHit = true, cooldown = 2,
                description = "적의 회피와 방어 태세를 무시하고 반드시 맞힌다." },
            // 무기 종류별 스킬 (해당 종류의 무기를 장착해야 쓸 수 있음)
            new SkillDef { id = 7, label = "분쇄", weaponType = WeaponType.Mace, unlockLevel = 2, manaCost = 3, powerPercent = 120f, breakDf = 30f,
                description = "메이스로 방어를 부수듯 내리친다. 적의 방어력을 일부 무시한다." },
            new SkillDef { id = 8, label = "회전 베기", weaponType = WeaponType.BattleAxe, unlockLevel = 3, manaCost = 4, hits = 2, powerPercent = 80f, cooldown = 1,
                description = "배틀 엑스를 휘둘러 두 번 벤다." },
            new SkillDef { id = 9, label = "연사", weaponType = WeaponType.Bow, unlockLevel = 3, manaCost = 5, hits = 3, powerPercent = 50f, cooldown = 2,
                description = "화살을 세 번 연달아 쏜다." },
            new SkillDef { id = 10, label = "검기", weaponType = WeaponType.Sword, unlockLevel = 2, manaCost = 3, powerPercent = 110f, criticalRate = 30f,
                description = "검에 기운을 실어 벤다. 치명타가 잘 터진다." },
        };
    }

    [ContextMenu("기본값으로 되돌리기")]
    public void ResetToDefaults()
    {
        skills = CreateDefaultSkills();
    }
}
