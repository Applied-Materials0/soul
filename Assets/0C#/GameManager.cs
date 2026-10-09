using UnityEngine;

public class GameManager : MonoBehaviour
{


    void Awake()
    {
        // 기본 스탯은 게임을 처음 켰을 때 한 번만 설정함 (씬을 다시 불러와도 초기화하지 않음)
        if (!Once)
        {
            Once = true;
            Level = 1;
            ExpNext = LevelSystem.ExpToNext(1); // 레벨 표(LevelTable)의 1레벨 필요 경험치

            // 시작 능력치와 가방 한도는 기본 능력치 표(PlayerBaseTable)에서 읽음
            PlayerBaseTable b = GameTables.PlayerBase;
            SPMax = b.spMax;
            Mana = b.mana;
            ManaMax = b.mana;
            Hp = b.hpMax;
            HpMax = b.hpMax;
            At = b.at;
            Df = b.df;
            Speed = b.speed;
            SlotMax = b.slotMax;
            WeightMax = b.weightMax;
            DefendBonus = b.defendBonus;

            SP = SPMax; // 시작할 때만 가득. 이후 SP는 마을의 [휴식]으로 회복한다 (GameTime)
            GameTime.StartNewGame();
        }
    }
    //플레이어 누적 능력치 변수
    static public int Gold;             //돈
    static public int Level;            //레벨
    static public int Exp;              //경험치
    static public int ExpNext;          //레벨업까지 경험치
    static public int ExpR;             //경험치 증감율
    static public int SP;               //현재 행동력 Stamina Point
    static public int SPMax;            //최대 행동력
    static public int Mana;             //마나량
    static public int ManaMax;          //최대 마나량
    static public int Weight;           //가방 무게
    //Status
    static public int At;               //공격력
    static public int Df;               //방어력
    static public int Hp;               //체력
    static public float HpMax;          //최대 체력
    static public float AtRate;         //공격 증감률
    static public float DfRate;         //방어 증감률
    static public float HpRate;         //체력 증감률
    static public int Speed;            //속도 (첫 조우에 높을 수록 선 턴)
    static public float HpRateAt;       //체력 비례 공격[%]
    static public int FixAt;            //고정 공격력
    static public int SlotMax;          //슬롯 최대 개수 기본값 (기본 능력치 표)
    static public int SlotBonus;        //장비로 늘어난 슬롯 개수 (장비 시스템이 채움). 실제 한도 = SlotMax + SlotBonus
    static public int WeightBonus;      //장비로 늘어난 최대 무게 (장비 시스템이 채움). 실제 한도 = WeightMax + WeightBonus
    static public int EquipAt;         //도구 및 장비 공격력 합 (장비 시스템이 채움)
    static public int EquipDf;         // 장비 방어력 합 (장비 시스템이 채움)
    static public int EquipHp;         // 장비로 늘어난 최대 체력 (장비 시스템이 채움)
    static public float DefendBonus;    //[방어] 시 방어력 증가 [%], 기본 50 (장비로 추가될 수 있음)
    static public float BreakDf;        //관통률 [%] 방관
    static public float Abs;            //체력 흡수[%] 공격력 비율만큼 흡수 Absorption
    static public float Avoid;          //회피율 값의 확률로 데미지 0으로 만듦
    static public float Critical;       //치명타 데미지 배율 [%]
    static public float CriticalRate;   //치명타 확률 [%]
    static public int Heal;             //회복량
    static public int HealRate;         //회복 배율 [%]
    static public int SPHeal;           //스태미나 회복량
    static public int MPHeal;           //마나 회복량
    static public float GoldR;          //골드 보너스
    static public int WeightMax;        //가방 최대 무게

    //게임 시스템
    static public bool Once;            //첫 기본 스탯 초기화 할 때 최초만 실행
    public static int selectedRegionID = 0; //씬 변수, 씬 로딩에 쓰임
    static public int SceneNum;         //가방, 맵 등 씬에서 뒤로가면 이전의 씬으로 돌아가기 위해 이전의 씬을 저장해 둘 변수
    static public int Field;            //필드에서 지역을 클릭하면 그 지역에 맞게 이동하기 위함
    static public bool isfaint;         //자신의 기절 상태
    static public int equippedToolIndex;//장착 도구 인덱스
}
