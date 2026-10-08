using UnityEngine;

// 플레이어의 시작 능력치와 기본 한도. 게임을 처음 켰을 때 한 번 적용된다 (Soul > 데이터 표 > [기본 능력치] 탭).
// 에셋은 Assets/Resources/Tables/PlayerBaseTable.asset.
//
// 슬롯 최대 개수와 최대 무게는 "기본값 + 장비 보너스"로 계산한다.
//  - 여기의 값이 기본값이고, 장비가 올려 주는 만큼은 GameManager.SlotBonus / GameManager.WeightBonus에 더해진다.
[CreateAssetMenu(fileName = "PlayerBaseTable", menuName = "Soul/Player Base Table")]
public class PlayerBaseTable : ScriptableObject
{
    [Header("시작 능력치")]
    [Min(1)] public int hpMax = 10;     // 최대 체력
    public int at = 10;                 // 공격력
    public int df = 10;                 // 방어력
    [Min(1)] public int spMax = 100;    // 최대 SP (행동력)
    public int mana = 10;               // 최대 마나
    public int speed = 0;               // 속도 (높을수록 전투에서 먼저 행동)

    [Header("가방 한도 (기본값. 장비로 늘어날 수 있음)")]
    [Min(1)] public int slotMax = 28;       // 슬롯 최대 개수
    [Min(1)] public int weightMax = 10000;  // 최대 소지 무게

    [Header("전투")]
    public float defendBonus = 50f;     // [방어] 시 방어력 증가 [%] (장비로 추가될 수 있음)

    [Header("기절 (체력이 0이 되었을 때의 대가)")]
    [Min(0)] public int hospitalFee = 10;                  // 병원비(골드). 가진 골드보다 많으면 가진 만큼만 냄
    [Range(0f, 100f)] public float itemLossPercent = 10f;  // 잃는 아이템 비율 [%] (각 아이템 수량에서). 도구/장비(한 슬롯에 1개인 것, 도구 종류가 있는 것)는 잃지 않음

    [Header("위험 경고 (체력이 낮을 때 화면 가장자리가 붉어짐. 퍼센트는 클수록 먼저, 세기는 0~100)")]
    [Range(0f, 100f)] public float lowHpPercent1 = 50f;  // 1단계: 체력이 이 비율 [%] 이하
    [Range(0f, 100f)] public float lowHpAlpha1 = 15f;    // 1단계 붉은 세기
    [Range(0f, 100f)] public float lowHpPercent2 = 25f;  // 2단계
    [Range(0f, 100f)] public float lowHpAlpha2 = 40f;
    [Range(0f, 100f)] public float lowHpPercent3 = 10f;  // 3단계 (위독)
    [Range(0f, 100f)] public float lowHpAlpha3 = 70f;
}
