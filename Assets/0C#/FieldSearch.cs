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

    [Tooltip("자원이 나왔을 때 어떤 자원인지를 정하는 표. Create > Soul > Resource Spawn Table 로 만들어 연결. 비워 두면 기본 비율 사용")]
    public ResourceSpawnTable resourceTable;

    [Tooltip("몬스터 도감. 연결하면 현재 지역에 출현하는 몬스터가 비중대로 뽑힘. Create > Soul > Monster Database 로 만들어 연결")]
    public MonsterDatabase monsterDB;
    private Monster currentMonster; // 지금 마주친 몬스터 (전투 구현 때 사용)
    private BattleSystem battle;    // 턴제 전투 진행 (Awake에서 같은 오브젝트에 붙임)
    private CarveSystem carve;      // 쓰러뜨린 몬스터를 칼로 도려내기 (Awake에서 같은 오브젝트에 붙임)

    // 지금 몬스터와 전투 중인가 (전투 중에는 아이템 사용 등을 막는 데 쓰임)
    public bool InBattle { get { return battle != null && battle.Active; } }

    // 전투 중 내 차례인가 (아이템 사용 등)
    public bool CanActInBattle { get { return battle != null && battle.CanAct; } }

    // 전투 중 아이템을 썼다: 한 턴을 쓰고 몬스터의 차례
    public void BattleItemUsed(string message)
    {
        if (battle != null) battle.OnItemUsed(message);
    }

    // 아이템(독 등)을 먹고 쓰러졌다: 전투 중이면 전투의 기절 처리, 아니면 병원비를 내고 마을로
    public void FaintFromItem()
    {
        if (battle != null && battle.Active) { battle.FaintByItem(); return; }

        string text = InventoryManager.Instance != null ? InventoryManager.Instance.RecoverFromFaint() : "병원에서 치료를 받았다.";
        StartCoroutine(FaintOutRoutine(text));
    }

    // 알림 글자가 완전히 사라진 뒤에 화면이 어두워지며 마을로 간다
    private System.Collections.IEnumerator FaintOutRoutine(string text)
    {
        ItemGainToast.ShowMessage(text);
        yield return new WaitForSeconds(ItemGainToast.MessageDuration);
        ReturnToTown();
    }

    // 내부 탐색/채집 상태 변수
    private int events;   // 탐색 이벤트 번호
    private int mob;      // 마주친 몬스터 번호
    private int sourceHP; // 자원의 현재 체력

    // 현재 선택된 자원 정보 (공통 채집 로직용)
    private int currentItemIndex;
    private ToolType currentRequiredTool;
    private int currentRequiredTier;
    private GatherSoundType currentSoundType;
    private ProficiencyKind currentSkill = ProficiencyKind.Gather; // 지금 자원의 채집 숙련도 종류 (자원 표)

    // FieldSearch는 이 씬의 UI(텍스트, 버튼, 소리)를 직접 들고 있으므로 씬마다 새로 만들어져야 한다.
    // DontDestroyOnLoad로 살려 두면 두 번째 진입 때 새 FieldSearch가 파괴되고, 이미 사라진 UI를 가리키는
    // 옛 인스턴스가 쓰여서 버튼이 먹통이 되었음.
    private void Awake()
    {
        Instance = this;

        // 턴제 전투 담당을 같은 오브젝트에 붙인다 (인스펙터 작업 불필요)
        battle = GetComponent<BattleSystem>();
        if (battle == null) battle = gameObject.AddComponent<BattleSystem>();
        battle.Init(this);

        // 몬스터를 쓰러뜨린 뒤 칼로 도려내는 시스템도 같은 오브젝트에 붙인다
        carve = GetComponent<CarveSystem>();
        if (carve == null) carve = gameObject.AddComponent<CarveSystem>();
        carve.Init(this);
    }

    // 키보드 단축키: Space 탐색, E 채집, A 공격, S 스킬, D 방어, R 도망 (가방/지도가 열려 있으면 쓰지 않음)
    private void Update()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.IsInventoryOpen) return;
        if (MapManager.Instance != null && MapManager.Instance.IsMapOpen) return;

        if (Input.GetKeyDown(KeyCode.Space)) FieldActionButton.Press(FieldButtonType.Search);
        else if (Input.GetKeyDown(KeyCode.E)) FieldActionButton.Press(FieldButtonType.Gather);
        else if (Input.GetKeyDown(KeyCode.A)) FieldActionButton.Press(FieldButtonType.Attack);
        else if (Input.GetKeyDown(KeyCode.S)) FieldActionButton.Press(FieldButtonType.Skill);
        else if (Input.GetKeyDown(KeyCode.D)) FieldActionButton.Press(FieldButtonType.Defence);
        else if (Input.GetKeyDown(KeyCode.R)) FieldActionButton.Press(FieldButtonType.Run);
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
            ToolType.Knife => "칼",
            ToolType.Mortar => "절구",
            _ => "도구"
        };
    }


    // =========================================================
    // 1. [탐색 버튼] - 필드 수색 및 이벤트/자원 발생
    // =========================================================
    public void SearchBtnOn()
    {
        // 새로 탐색하면 이전에 도려내던 사체는 두고 떠난다
        if (carve != null) carve.End();

        // SP 소모량은 SP 소모 표(Soul > 데이터 표 > SP 소모)에서 정함
        int searchCost = GameTables.SPCosts.Get(SPAction.Search);
        if (GameManager.SP < searchCost)
        {
            BtnAudio.Play();
            SearchText.SetText("SP가 부족합니다!");
            return;
        }

        GameManager.SP -= searchCost;
        if (PlayerUI.Instance != null) PlayerUI.Instance.UpdateStatText();

        SearchAudio.Stop();
        RunAudio.Stop();
        SearchAudio.Play();

        // 1단계: 무엇이 나올지를 탐색 결과 표(SearchTable)의 비중대로 정함
        SearchOutcome outcome = GameTables.Search.Pick();
        if (outcome == null)
        {
            Debug.LogWarning("[FieldSearch] 탐색 결과 표의 비중이 모두 0입니다. Soul > 데이터 표 > [탐색 결과] 탭을 확인하세요.");
            SearchEvent(null);
            return;
        }

        switch (outcome.kind)
        {
            case SearchOutcomeKind.Resource: SearchResource(); break;
            case SearchOutcomeKind.Monster: SearchMonster(); break;
            default: SearchEvent(outcome.text); break;
        }
    }

    // 자원 발견: 어떤 자원이 나올지는 resourceTable(ResourceSpawnTable 에셋)의 비중대로 정함
    private void SearchResource()
    {
        ResourceSpawnTable table = GetResourceTable();
        ResourceSpawn spawn = table != null ? table.Pick() : null;
        if (spawn == null)
        {
            Debug.LogWarning("[FieldSearch] 뽑을 수 있는 자원이 없습니다. ResourceSpawnTable의 비중을 확인하세요.");
            SearchEvent(null);
            return;
        }

        int lo = Mathf.Max(1, Mathf.Min(spawn.hpMin, spawn.hpMax));
        int hi = Mathf.Max(1, Mathf.Max(spawn.hpMin, spawn.hpMax));
        int hp = Random.Range(lo, hi + 1);

        SetSearchTarget(spawn.foundText, spawn.gatherButtonText, 0, spawn.itemId, spawn.toolType, spawn.tier, hp, spawn.sound);
        currentSkill = spawn.skill;
        currentExtraYields = spawn.extraYields; // 채집할 때 함께 얻는 아이템 (자원 표의 "추가 획득")
    }

    // 지금 찾은 자원을 채집할 때 기본 아이템과 함께 추가로 얻는 아이템들
    private List<ResourceExtraYield> currentExtraYields;

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
    private void SearchEvent(string text)
    {
        SearchText.SetText(string.IsNullOrEmpty(text) ? "아무것도 발견하지 못했다..." : text);
        GatherBtn.SetActive(false);
    }

    // 몬스터 조우: 지금 있는 지역(GameManager.selectedRegionID)에 출현하는 몬스터 중 비중(weight)대로 뽑는다
    private void SearchMonster()
    {
        Monster picked = monsterDB != null ? monsterDB.PickForRegion(GameManager.selectedRegionID) : null;
        currentMonster = picked;

        if (picked == null)
        {
            // 도감이 연결되지 않았거나 이 지역에 출현하는 몬스터가 없을 때: 예전처럼 번호만 임시로 뽑는다
            if (monsterDB == null)
            {
                Debug.LogWarning("[FieldSearch] monsterDB가 연결되지 않았습니다. Field 씬의 FieldManager 오브젝트에 있는 FieldSearch에 " +
                    "MonsterDatabase 에셋을 연결하고 씬을 저장하세요. 임시 몬스터로 대체합니다.");
            }
            else
            {
                Debug.LogWarning($"[FieldSearch] 지역 ID {GameManager.selectedRegionID}에 출현하는 몬스터가 없습니다. " +
                    "몬스터 에셋의 출현 지역(spawns)과 MonsterDatabase 목록(Collect All Monsters)을 확인하세요. 임시 몬스터로 대체합니다.");
            }
            SetMonsterEncounter("적과 마주쳤다!", Random.Range(1, 21));
            return;
        }

        SetMonsterEncounter($"{picked.monsterName}{HasJongseong(picked.monsterName, "이", "가")} 나타났다!", picked.id);

        // 턴제 전투 시작: 이름/체력 게이지 표시, Speed가 높은 쪽이 먼저 행동
        battle.Begin(picked);
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
        // 몬스터를 쓰러뜨린 뒤라면 같은 버튼이 [도려내기]로 동작함
        if (carve != null && carve.Active)
        {
            carve.OnCarve();
            return;
        }
        ProcessGathering();
    }

    // 몬스터를 쓰러뜨렸을 때 BattleSystem이 호출: 칼로 도려낼 수 있는 몬스터면 [도려내기]를 켠다
    public void StartCarving(Monster m)
    {
        if (carve != null) carve.Begin(m);
    }

    private void ProcessGathering()
    {
        // 1. SP(행동력) 체크 (SP 소모 표의 채집 소모량)
        ProficiencyDef gatherProf = Proficiency.ForGathering(currentSkill, currentRequiredTool); // 이 자원을 캘 때 오르는 숙련도
        int gatherCost = Proficiency.ReducedSp(gatherProf, GameTables.SPCosts.Get(SPAction.Gather)); // 숙련도가 SP를 줄여 줌
        if (GameManager.SP < gatherCost)
        {
            BtnAudio.Play();
            SearchText.text = "행동력(SP)이 부족합니다!";
            return;
        }

        // 아이템 고유 번호로 ItemDatabase(InventoryManager.itemDB)에서 원본 데이터를 찾습니다.
        Item targetResource = InventoryManager.Instance.GetItemData(currentItemIndex);
        if (targetResource == null) return; // 원인은 GetItemData가 로그로 알려줌

        // 가방이 가득 찼거나 너무 무거우면 도구 내구도와 SP를 쓰기 전에 막는다
        InventoryManager.AddBlock bagBlock;
        if (InventoryManager.Instance.GetAddableAmount(targetResource, 1, out bagBlock) < 1)
        {
            BtnAudio.Play();
            SearchText.text = InventoryManager.BlockMessage(bagBlock);
            return;
        }

        string resourceName = targetResource.itemName;
        // 병으로 물을 담는 자원은 아이템 이름(가득찬 ...)이 아니라 "물"을 담는다고 안내한다 (특정 아이템 이름에 묶지 않음)
        bool fillsWater = currentRequiredTool == ToolType.Bottle;
        string needName = fillsWater ? "물" : resourceName;

        // 2. 도구 조건 판별 및 내구도 자동 차감
        Item usedTool = null;
        int toolDurabilityLeft = -1; // 쓴 도구의 남은 내구도 (결과 아이템이 내구도를 이어받을 때 사용)
        bool isBroken = false;
        int damage = 1; // 맨손 기본 채집력

        if (currentRequiredTool != ToolType.None)
        {
            ToolCheckResult toolResult;
            usedTool = InventoryManager.Instance.ConsumeToolDurability(currentRequiredTool, currentRequiredTier, out toolResult, out isBroken);

            // 자원 이름 및 도구 이름 가져오기
            string toolName = GetToolTypeName(currentRequiredTool);
            string eulLuel = HasJongseong(needName, "을", "를"); // 덤불 -> 을 / 딸기 -> 를
            string iGa = HasJongseong(toolName, "이", "가");         // 낫 -> 이 / 도끼 -> 가

            if (toolResult == ToolCheckResult.NoTool)
            {
                string purpose = fillsWater ? "담으려면" : "채집하려면";
                string lack = fillsWater ? "" : "채집에 필요한 도구가 없습니다! ";
                SearchText.text = $"{lack}{needName}{eulLuel} {purpose} {toolName}{iGa} 필요합니다!";
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
            toolDurabilityLeft = InventoryManager.Instance.LastToolDurability;
        }

        // 채집 숙련도: 이 도구 종류에 맞는 숙련도가 있으면 레벨의 보너스만큼 한 번에 채집하는 양이 늘어난다
        if (gatherProf != null)
            damage = Mathf.Max(damage, Mathf.RoundToInt(damage * BattleCalc.Mult(Proficiency.Bonus(gatherProf))));

        // 3. 자원 차감 및 인벤토리 추가
        int wanted = Mathf.Min(sourceHP, damage);
        // 결과 아이템이 내구도를 가지면(물을 담은 물통) 쓴 도구의 남은 내구도를 이어받는다. 도구 1개가 결과 아이템 1개로 바뀐다 (복제되지 않음).
        // (결과 아이템의 최대 내구도를 표에 안 적었어도 병으로 물을 담을 때는 항상 변환한다)
        bool transforms = usedTool != null && (targetResource.durabilitymax > 0 || currentRequiredTool == ToolType.Bottle);
        int carry = -1;
        if (transforms)
        {
            carry = toolDurabilityLeft;
            if (usedTool.durabilitymax > 0) carry = Mathf.Min(carry, usedTool.durabilitymax); // 닳지 않는 도구는 도구의 최대 내구도로 맞춤
            else carry = Mathf.Min(carry, 100);
        }
        if (carry >= 0) wanted = 1; // 도구 1개가 결과 아이템 1개로 바뀐다
        int gainedAmount = InventoryManager.Instance.AddItem(targetResource, wanted, carry);
        if (carry >= 0 && gainedAmount > 0) InventoryManager.Instance.RemoveLastUsedTool(); // 도구는 사라지고 결과 아이템이 내구도를 이어받는다
        bool bagLimited = gainedAmount < wanted; // 가방 때문에 일부만 얻음
        sourceHP -= gainedAmount;
        GameManager.SP -= gatherCost;
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
        if (bagLimited) SearchText.text += "\n" + InventoryManager.BlockMessage(InventoryManager.Instance.LastAddBlock);

        // 추가로 얻는 아이템 (예: 벌집 -> 꿀 + 밀랍). 채집한 양에 배수를 곱해서, 각 줄의 확률대로 얻는다
        if (gainedAmount > 0 && currentExtraYields != null)
        {
            List<string> extraGot = new List<string>();
            bool extraBagFull = false;
            foreach (ResourceExtraYield y in currentExtraYields)
            {
                if (y == null || !BattleCalc.Roll(y.chance)) continue;
                Item extra = InventoryManager.Instance.GetItemData(y.itemId);
                if (extra == null) continue;

                int want = gainedAmount * Mathf.Max(1, y.multiplier);
                int got = InventoryManager.Instance.AddItem(extra, want);
                if (got < want) extraBagFull = true;
                if (got > 0) extraGot.Add($"{extra.itemName} {got:N0}개");
            }
            if (extraGot.Count > 0) SearchText.text += "\n추가 획득: " + string.Join(", ", extraGot);
            if (extraBagFull) SearchText.text += "\n" + InventoryManager.BlockMessage(InventoryManager.Instance.LastAddBlock);
        }

        // 채집 숙련도 경험치
        // 숙련도 보너스: 같은 아이템을 최소~최대 중에서 더 얻는다 (자원 체력은 줄지 않음)
        if (gainedAmount > 0 && !transforms && gatherProf != null)
        {
            int bonusWanted = Proficiency.ExtraAmount(gatherProf);
            if (bonusWanted > 0)
            {
                int bonusGot = InventoryManager.Instance.AddItem(targetResource, bonusWanted);
                if (bonusGot > 0) SearchText.text += $"\n{gatherProf.label} 숙련도 보너스! {targetResource.itemName} +{bonusGot}";
            }
        }

        // 행동했으니 숙련도 경험치와 플레이어 레벨 경험치를 함께 얻는다
        if (gainedAmount > 0 && gatherProf != null)
        {
            string rewardText = Proficiency.Reward(gatherProf);
            if (rewardText.Length > 0) SearchText.text += "\n" + rewardText;
        }

        if (isBroken && usedTool != null)
        {
            SearchText.text += $"\n{Josa.WithIga(usedTool.itemName)} 파괴되었습니다!";
            ItemGainToast.ShowBroken(usedTool, "도구가 파괴되었다!");
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
        // 전투 중이면 제압당했는지 등을 BattleSystem이 판단하고, 성공하면 Flee()를 부른다
        if (battle != null && battle.Active)
        {
            BtnAudio.Play();
            battle.OnRun();
            return;
        }
        Flee();
    }

    // 도망 성공 처리: 소리, 문구, 버튼을 탐색 상태로 되돌린다
    public void Flee()
    {
        SearchAudio.Stop();
        RunAudio.Play();
        SearchText.text = "무사히 도망쳤다.";
        RestoreExploreButtons();
    }

    // 전투/조우가 끝나면 탐색 상태의 버튼으로 되돌린다
    public void RestoreExploreButtons()
    {
        RunBtn.SetActive(false);
        GatherBtn.SetActive(false);
        SearchBtn.SetActive(true);
        MapBtn.SetActive(true);
    }

    // 기절하면 루프 타운으로 돌아간다 (임시 규칙)
    public void ReturnToTown()
    {
        GameManager.selectedRegionID = 0; // 루프 타운 지역 ID
        if (FadeManager.Instance != null) FadeManager.Instance.LoadSceneWithFade("LoopTown");
        else UnityEngine.SceneManagement.SceneManager.LoadScene("LoopTown");
    }

    // =========================================================
    //  [공격 버튼]
    // =========================================================
    public void AttackBtnOn()
    {
        SearchAudio.Stop();
        BtnAudio.Play();

        if (battle != null && battle.Active) battle.OnAttack();
        else SearchText.text = "공격할 상대가 없다.";
    }

    // =========================================================
    //  [방어 버튼]
    // =========================================================
    public void DefenceBtnOn()
    {
        SearchAudio.Stop();
        BtnAudio.Play();

        if (battle != null && battle.Active) battle.OnDefend();
        else SearchText.text = "방어할 일이 없다.";
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