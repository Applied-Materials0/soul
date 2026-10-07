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
}
