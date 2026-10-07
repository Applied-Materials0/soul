using System.Collections.Generic;
using UnityEngine;

// 몬스터 정의 데이터. 몬스터 하나 = .asset 파일 하나 (Create > Soul > Monster).
// 전투 중 바뀌는 값(현재 HP 등)은 여기에 두지 않는다. 정의만 읽는다.
// 주의: 필드 이름을 바꿀 때는 반드시 [FormerlySerializedAs("옛이름")]을 붙일 것. 안 붙이면 그 필드 값이 초기화된다.
[CreateAssetMenu(fileName = "Monster_", menuName = "Soul/Monster")]
public class Monster : ScriptableObject
{
    [Header("기본 정보")]
    public int id;                 // 고유 ID (MonsterDatabase 안에서 중복 금지)
    public string monsterName;     // 몬스터 이름
    [TextArea(2, 5)]
    public string description;     // 설명
    public Sprite sprite;          // 전투 화면 이미지
    public int level = 1;          // 레벨

    [Header("능력치")]
    public int hpMax = 10;         // 최대 체력
    public int at = 1;             // 공격력
    public int df = 0;             // 방어력
    public int speed = 1;          // 속도 (높을수록 먼저 행동. 플레이어는 GameManager.Speed)
    public float avoid;            // 회피율 [%]
    public float criticalrate;     // 치명타 확률 [%]
    public float critical;         // 치명타 피해 증가 [%]

    [Header("전투 행동")]
    [Range(0f, 100f)] public float defendChance = 10f;       // 자기 턴에 방어를 고를 확률 [%] (체력이 충분할 때)
    [Range(0f, 100f)] public float defendChanceLowHp = 40f;  // 체력이 30% 이하일 때 방어를 고를 확률 [%]
    [Range(0f, 100f)] public float suppressChance = 10f;     // 제압 확률 [%]: 자기 턴마다 이 확률로 플레이어를 제압해 다음 턴에 도망치지 못하게 함

    [Header("처치 보상")]
    public int exp;                // 경험치
    public int gold;               // 골드
    public List<MonsterDrop> drops = new List<MonsterDrop>(); // 아이템 드랍 (여러 개 가능)

    [Header("출현 지역")]
    public List<MonsterSpawn> spawns = new List<MonsterSpawn>(); // 어느 지역에 얼마나 자주 나오는지
}

// 드랍 아이템 한 줄
[System.Serializable]
public class MonsterDrop
{
    public int itemId;                       // 드랍하는 아이템 ID (ItemDatabase)
    [Min(1)] public int amountMin = 1;       // 최소 수량
    [Min(1)] public int amountMax = 1;       // 최대 수량 (둘 다 포함)
    [Range(0f, 100f)] public float chance = 100f; // 드랍 확률 [%]
}

// 출현 지역 한 줄
[System.Serializable]
public class MonsterSpawn
{
    public int regionId;                     // 지역 ID (Region.id. 루프 타운 0, 숲 1 ...)
    [Min(0)] public int weight = 1;          // 그 지역 몬스터끼리의 비중 (0이면 안 나옴)
}
