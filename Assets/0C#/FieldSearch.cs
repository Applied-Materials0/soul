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

    [Header("탐색 결과 비율 (탐색 버튼을 눌렀을 때 무엇이 나올지)")]
    [Tooltip("비율끼리 비교하는 값이라 합이 100일 필요는 없음. 확률 = 내 값 / 세 값의 합. 0이면 절대 안 나옴")]
    [Min(0)] public int resourceWeight = 76; // 자원 발견 (나뭇가지, 덤불, 광석, 연못 등. 어떤 자원인지의 비율은 아래 resourceTable 에셋에서 조절)
    [Min(0)] public int eventWeight = 4;     // 이벤트 (지금은 "아무것도 발견하지 못했다"만 있음)
    [Min(0)] public int monsterWeight = 20;  // 몬스터 조우

    [Tooltip("자원이 나왔을 때 어떤 자원인지를 정하는 표. Create > Soul > Resource Spawn Table 로 만들어 연결. 비워 두면 기본 비율 사용")]
    public ResourceSpawnTable resourceTable;

    // 내부 탐색/채집 상태 변수
    private int events;   // 탐색 이벤트 번호
    private int mob;      // 마주친 몬스터 번호
    private int sourceHP; // 자원의 현재 체력

    // 현재 선택된 자원 정보 (공통 채집 로직용)
    private int currentItemIndex;
    private ToolType currentRequiredTool;
    private int currentRequiredTier;
    private GatherSoundType currentSoundType;

    // FieldSearch는 이 씬의 UI(텍스트, 버튼, 소리)를 직접 들고 있으므로 씬마다 새로 만들어져야 한다.
    // DontDestroyOnLoad로 살려 두면 두 번째 진입 때 새 FieldSearch가 파괴되고, 이미 사라진 UI를 가리키는
    // 옛 인스턴스가 쓰여서 버튼이 먹통이 되었음.
    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
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

        // 1단계: 자원 / 이벤트 / 몬스터 중 무엇이 나올지를 인스펙터의 비율대로 정함
        int resW = Mathf.Max(0, resourceWeight);
        int evW = Mathf.Max(0, eventWeight);
        int monW = Mathf.Max(0, monsterWeight);
        int total = resW + evW + monW;

        if (total <= 0)
        {
            Debug.LogWarning("[FieldSearch] 탐색 결과 비율(resourceWeight/eventWeight/monsterWeight)이 모두 0입니다.");
            SearchEvent();
            return;
        }

        int roll = Random.Range(0, total); // 0 ~ total-1
        if (roll < resW) SearchResource();
        else if (roll < resW + evW) SearchEvent();
        else SearchMonster();
    }

    // 자원 발견: 어떤 자원이 나올지는 resourceTable(ResourceSpawnTable 에셋)의 비중대로 정함
    private void SearchResource()
    {
        ResourceSpawnTable table = GetResourceTable();
        ResourceSpawn spawn = table != null ? table.Pick() : null;
        if (spawn == null)
        {
            Debug.LogWarning("[FieldSearch] 뽑을 수 있는 자원이 없습니다. ResourceSpawnTable의 비중을 확인하세요.");
            SearchEvent();
            return;
        }

        int lo = Mathf.Max(1, Mathf.Min(spawn.hpMin, spawn.hpMax));
        int hi = Mathf.Max(1, Mathf.Max(spawn.hpMin, spawn.hpMax));
        int hp = Random.Range(lo, hi + 1);

        SetSearchTarget(spawn.foundText, spawn.gatherButtonText, 0, spawn.itemId, spawn.toolType, spawn.tier, hp, spawn.sound);
    }

    // 에셋을 아직 연결하지 않았을 때를 위한 기본 표 (코드에 들어 있는 기본값과 같음)
    private ResourceSpawnTable fallbackTable;
    private ResourceSpawnTable GetResourceTable()
    {
        if (resourceTable != null) return resourceTable;

        if (fallbackTable == null)
        {
            fallbackTable = ScriptableObject.CreateInstance<ResourceSpawnTable>();
            Debug.LogWarning("[FieldSearch] resourceTable이 연결되지 않아 기본 비율을 사용합니다. Create > Soul > Resource Spawn Table 에셋을 만들어 연결하세요.");
        }
        return fallbackTable;
    }

    // 이벤트: 지금은 아무 일도 없는 경우만 있음. 새 이벤트는 여기에 추가
    private void SearchEvent()
    {
        SearchText.SetText("아무것도 발견하지 못했다...");
        GatherBtn.SetActive(false);
    }

    // 몬스터 조우 (몬스터 번호는 임시로 1~20 중 무작위. 몬스터 도감을 만들면 지역별 출현 가중치로 대체)
    private void SearchMonster()
    {
        SetMonsterEncounter("적과 마주쳤다!", Random.Range(1, 21));
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

        // 아이템 고유 번호로 ItemDatabase(InventoryManager.itemDB)에서 원본 데이터를 찾습니다.
        Item targetResource = InventoryManager.Instance.GetItemData(currentItemIndex);
        if (targetResource == null) return; // 원인은 GetItemData가 로그로 알려줌

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
        InventoryManager.Instance.AddItem(targetResource, gainedAmount);
        sourceHP -= gainedAmount;
        GameManager.SP -= 1;
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