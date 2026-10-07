using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public enum GatherSoundType
{
    Default,    // 기본 사운드
    Bush,       //  수풀 사운드
    Logging,    //  벌목 사운드
    Mining,     //  채광 사운드
    Water       //  물 사운드
}

public class FieldSearch : MonoBehaviour
{
    public static FieldSearch Instance;

    [Header("Audio Sources")]
    public AudioSource BtnAudio;       // 버튼 음원
    public AudioSource SearchAudio;    // 탐색 음원
    public AudioSource RunAudio;       // 도망 음원
    public AudioSource GatherAudio;    // 채집 음원
    public AudioSource BushAudio;      // 수풀 음원
    public AudioSource LoggingAudio;   // 벌목 음원
    public AudioSource TreefallAudio;  // 나무 쓰러짐 음원
    public AudioSource MiningAudio;    // 채광 음원
    public AudioSource StonefallAudio; // 돌 부서짐 음원
    public AudioSource WaterAudio;     // 물 음원

    [Header("UI References")]
    public TextMeshProUGUI SearchText; // 필드 상황 텍스트
    public TextMeshProUGUI GatherText; // 채집 버튼 이름 변경 텍스트
    public GameObject GatherBtn;       // 채집 버튼
    public GameObject SearchBtn;       // 탐색 버튼
    public GameObject RunBtn;          // 도망 버튼
    public GameObject MapBtn;          // 맵 버튼

    // 내부 탐색/채집 상태 변수
    private int result;   // 랜덤 정수
    private int events;   // 탐색 이벤트 번호
    private int mob;      // 마주친 몬스터 번호
    private int sourceHP; // 자원의 현재 체력

    // 현재 선택된 자원 정보 (공통 채집 로직용)
    private int currentItemIndex;
    private ToolType currentRequiredTool;
    private int currentRequiredTier;
    private GatherSoundType currentSoundType;

    [Header("자원 아이템 데이터베이스 (인스펙터 등록 필수)")]
    public List<Item> resourceDB = new List<Item>();

    private void Awake() // ★ 대문자 A로 수정
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        // 씬으로 들어와서 태어나자마자 글로벌 매니저를 찾아가 자기 주소를 갱신함
        if (GlobalUI.Instance != null)
        {
            GlobalUI.Instance.RegisterUI(this);
        }
    }

    //한국어 이름 변환 함수, 자원 채집에 필요한 도구 표시
    private string GetToolTypeName(ToolType toolType)
    {
        return toolType switch
        {
            ToolType.Axe => "도끼",
            ToolType.Pickaxe => "곡괭이",
            ToolType.Sickle => "낫",
            ToolType.Bottle => "수통",
            _ => "도구"
        };
    }


    // =========================================================
    // 1. [탐색 버튼] - 필드 수색 및 이벤트/자원 발생
    // =========================================================
    public void SearchBtnOn()
    {
        if (GameManager.SP <= 0)
        {
            BtnAudio.Play();
            SearchText.SetText("SP가 부족합니다!");
            return;
        }

        GameManager.SP -= 1;
        if (PlayerUI.Instance != null) PlayerUI.Instance.UpdateStatText();

        SearchAudio.Stop();
        RunAudio.Stop();
        SearchAudio.Play();

        result = Random.Range(1, 101); // 1~100까지 균등하게 생성

        if (result >= 1 && result <= 5) // 5% - 나뭇가지 (맨손 줍기)
        {
            //SetSearchTarget(상황 텍스트, 버튼 텍스트, 이벤트, 아이템id, 툴 타입, 티어, 소스 HP);
            SetSearchTarget("나뭇가지를 발견했다.", "채집", 0, 0, ToolType.None, 0, 1, GatherSoundType.Default);
        }
        else if (result >= 6 && result <= 14) // 9% - 덤불
        {
            int hp = Random.Range(1, 5);
            SetSearchTarget("덤불을 발견했다.", "채집", 12, 13, ToolType.Sickle, 0, hp, GatherSoundType.Bush);
        }
        else if (result >= 15 && result <= 25) // 11% - 참나무 (1티어 도끼)
        {
            int hp = Random.Range(3, 10);
            SetSearchTarget("참나무를 발견했다.", "벌목", 3, 0, ToolType.Axe, 1, hp, GatherSoundType.Logging);
        }
        else if (result >= 26 && result <= 28) // 3% - 구리 조각
        {
            SetSearchTarget("구리 조각을 발견했다.", "채집", 1, 1, ToolType.None, 0, 1, GatherSoundType.Default);
        }
        else if (result >= 29 && result <= 30) // 2% - 구리 광석 (2티어 곡괭이)
        {
            int hp = Random.Range(5, 15);
            SetSearchTarget("구리 광석을 발견했다.", "채광", 14, 1, ToolType.Pickaxe, 1, hp, GatherSoundType.Mining);
        }
        else if (result >= 31 && result <= 35) // 5% - 잡석 조각
        {
            SetSearchTarget("잡석 조각을 발견했다.", "채집", 2, 2, ToolType.None, 0, 1, GatherSoundType.Default);
        }
        else if (result >= 36 && result <= 38) // 3% - 잡석/바위 (1티어 곡괭이)
        {
            int hp = Random.Range(3, 10);
            SetSearchTarget("잡석을 발견했다.", "채석", 15, 2, ToolType.Pickaxe, 1, hp, GatherSoundType.Mining);
        }
        else if (result >= 39 && result <= 48) // 10% - 풀
        {
            SetSearchTarget("풀을 발견했다.", "채집", 4, 13, ToolType.None, 0, 1, GatherSoundType.Bush);
        }
        else if (result >= 49 && result <= 55) // 7% - 딸기
        {
            int hp = Random.Range(1, 5);
            SetSearchTarget("딸기를 발견했다.", "채집", 5, 16, ToolType.None, 0, hp, GatherSoundType.Bush);
        }
        else if (result >= 56 && result <= 57) // 2% - 옥수수
        {
            int hp = Random.Range(1, 5);
            SetSearchTarget("옥수수를 발견했다.", "채집", 6, 17, ToolType.None, 0, hp, GatherSoundType.Bush);
        }
        else if (result >= 58 && result <= 60) // 3% - 벌집
        {
            SetSearchTarget("벌집을 발견했다.", "채집", 7, 18, ToolType.None, 0, 1, GatherSoundType.Default);
        }
        else if (result == 61) // 1% - 달걀
        {
            int hp = Random.Range(1, 5);
            SetSearchTarget("달걀을 발견했다.", "채집", 8, 19, ToolType.None, 0, hp, GatherSoundType.Default);
        }
        else if (result >= 62 && result <= 66) // 5% - 주황 버섯
        {
            SetSearchTarget("주황 버섯을 발견했다.", "채집", 9, 26, ToolType.None, 0, 1, GatherSoundType.Default);
        }
        else if (result >= 67 && result <= 69) // 3% - 푸른 버섯
        {
            SetSearchTarget("푸른 버섯을 발견했다.", "채집", 10, 27, ToolType.None, 0, 1, GatherSoundType.Default);
        }
        else if (result == 70) // 1% - 붉은 버섯
        {
            SetSearchTarget("붉은 버섯을 발견했다.", "채집", 11, 28, ToolType.None, 0, 1, GatherSoundType.Default);
        }
        else if (result >= 71 && result <= 90) // 몬스터 조우
        {
            SetMonsterEncounter("적과 마주쳤다!", result - 70);
        }
        else if (result >= 91 && result <= 96) // 6% - 연못
        {
            SetSearchTarget("연못을 발견했다.", "담기", 16, 15, ToolType.Bottle, 0, 100, GatherSoundType.Water);
        }
        else
        {
            SearchText.SetText("아무것도 발견하지 못했다...");
            GatherBtn.SetActive(false);
        }
    }

    // 탐색 결과 세팅 세부 함수
    private void SetSearchTarget(string desc, string btnText, int eventID, int itemIdx, ToolType tool, int tier, int hp, GatherSoundType soundType)
    {
        SearchText.SetText(desc);       //상황 텍스트
        GatherText.SetText(btnText);    //버튼 텍스트
        GatherBtn.SetActive(true);      //채집 버튼 활성화
        RunBtn.SetActive(false);        //도망 버튼 비활성화

        events = eventID;
        currentItemIndex = itemIdx;
        currentRequiredTool = tool;
        currentRequiredTier = tier;
        sourceHP = hp;
        currentSoundType = soundType;
    }

    private void SetMonsterEncounter(string text, int mobID)
    {
        SearchText.SetText(text);
        GatherBtn.SetActive(false);
        RunBtn.SetActive(true);
        SearchBtn.SetActive(false);
        MapBtn.SetActive(false);
        mob = mobID;
    }

    // =========================================================
    // 2. [채집 버튼] - 공통 채집 로직 실행
    // =========================================================
    public void GatherBtnOn()
    {
        Debug.LogError("채집 버튼 입력 정상");
        ProcessGathering();
    }

    private void ProcessGathering()
    {
        // 1. SP(행동력) 체크
        if (GameManager.SP < 1)
        {
            BtnAudio.Play();
            SearchText.text = "행동력(SP)이 부족합니다!";
            return;
        }

        //수정됨: 가방을 뒤지는 대신, 새로 만든 도감(resourceDB)에서 아이템 고유 번호로 원본 데이터를 찾습니다.
        Item targetResource = resourceDB.Find(x => x.id == currentItemIndex);
        if (targetResource == null)
        {
            Debug.LogError($"ID가 {currentItemIndex}인 아이템을 resourceDB에서 찾을 수 없습니다! 유니티 인스펙터를 확인하세요.");
            return;
        }

        string resourceName = targetResource.itemName;
        if (resourceName == "가득찬 물통")
        {
            resourceName = "물";
        }

        // 2. 도구 조건 판별 및 내구도 자동 차감
        Item usedTool = null;
        bool isBroken = false;
        int damage = 1; // 맨손 기본 채집력

        if (currentRequiredTool != ToolType.None)
        {
            ToolCheckResult toolResult;
            usedTool = InventoryManager.Instance.ConsumeToolDurability(currentRequiredTool, currentRequiredTier, out toolResult, out isBroken);

            // 자원 이름 및 도구 이름 가져오기
            string toolName = GetToolTypeName(currentRequiredTool);
            string eulLuel = HasJongseong(resourceName, "을", "를"); // 덤불 -> 을 / 딸기 -> 를
            string iGa = HasJongseong(toolName, "이", "가");         // 낫 -> 이 / 도끼 -> 가

            if (toolResult == ToolCheckResult.NoTool)
            {
                SearchText.text = $"채집에 필요한 도구가 없습니다! {resourceName}{eulLuel} 채집하려면 {toolName}{iGa} 필요합니다!";
                BtnAudio.Play();
                return;
            }
            else if(toolResult == ToolCheckResult.LowTier)
            {
                SearchText.text = $"{currentRequiredTier}티어 이상의 도구가 필요합니다!";
                BtnAudio.Play();
                return;
            }
            // 도구의 공격력(채집력) 적용
            damage = usedTool.toolattack;
        }

        // 3. 자원 차감 및 인벤토리 추가
        int gainedAmount = Mathf.Min(sourceHP, damage);
        InventoryManager.Instance.itemList[currentItemIndex].itemData.count += gainedAmount;
        sourceHP -= gainedAmount;
        GameManager.SP -= 1;
        Debug.Log(InventoryManager.Instance.itemList[currentItemIndex].itemData.count);//현재 아이템 갯수
        if (PlayerUI.Instance != null) PlayerUI.Instance.UpdateStatText(); //스탯창 갱신

        // 4. 사운드 재생
        PlayGatherSound(currentSoundType);

        // 5. 결과 텍스트 및 파손 메시지 처리
        if (sourceHP <= 0)
        {
            PlayFinishSound(currentSoundType);
            GatherBtn.SetActive(false);
        }
        SearchText.text = $"{resourceName} {gainedAmount:N0}개를 획득했다. (남은 체력: {sourceHP:N0})";

        if (isBroken && usedTool != null)
        {
            SearchText.text += $"\n[{usedTool.itemName}]이(가) 파손되었습니다!";
        }
    }

    private void PlayGatherSound(GatherSoundType soundType) //채집 사운드
    {
        switch (soundType)
        {
            case GatherSoundType.Logging: LoggingAudio.Play(); break;
            case GatherSoundType.Mining: MiningAudio.Play(); break;
            case GatherSoundType.Bush: BushAudio.Play(); break;
            case GatherSoundType.Water: WaterAudio.Play(); break;
            default: GatherAudio.Play(); break;
        }
    }

    private void PlayFinishSound(GatherSoundType soundType) //채집 완료 사운드
    {
        switch (soundType)
        {
            case GatherSoundType.Logging: TreefallAudio.Play(); break;
            case GatherSoundType.Mining: StonefallAudio.Play(); break;
            case GatherSoundType.Bush: BushAudio.Play(); break;
            case GatherSoundType.Water: WaterAudio.Play(); break;
            default: GatherAudio.Play(); break;
        }
    }

    // =========================================================
    // 3. [도망 버튼]
    // =========================================================
    public void RunBtnOn()
    {
        SearchAudio.Stop();
        RunAudio.Play();
        SearchText.text = "무사히 도망쳤다.";
        RunBtn.SetActive(false);
        SearchBtn.SetActive(true);
        MapBtn.SetActive(true);
    }

    // =========================================================
    //  [공격 버튼]
    // =========================================================
    public void AttackBtnOn()
    {
        SearchAudio.Stop();
        BtnAudio.Play();
        SearchText.text = "공격!";
    }

    // =========================================================
    //  [방어 버튼]
    // =========================================================
    public void DefenceBtnOn()
    {
        SearchAudio.Stop();
        BtnAudio.Play();
        SearchText.text = "방어!";
    }

    // =========================================================
    //  [스킬 버튼]
    // =========================================================
    public void SkillBtnOn()
    {
        SearchAudio.Stop();
        BtnAudio.Play();
        SearchText.text = "스킬창 오픈!";
    }

    // =========================================================
    //  [필드 상황 텍스트 을/를/이/가 변환]
    // =========================================================
    // 받침이 있으면 firstChar(을/이), 없으면 secondChar(를/가) 반환
    private string HasJongseong(string text, string firstChar, string secondChar)
    {
        if (string.IsNullOrEmpty(text)) return firstChar;
        char lastChar = text[text.Length - 1];

        // 한글 범위 체크
        if (lastChar >= 0xAC00 && lastChar <= 0xD7A3)
        {
            bool hasBatchim = (lastChar - 0xAC00) % 28 != 0;
            return hasBatchim ? firstChar : secondChar;
        }
        return firstChar;
    }
}