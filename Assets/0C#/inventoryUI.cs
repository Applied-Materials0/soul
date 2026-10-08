using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;



// 도구 검사 결과 상태
public enum ToolCheckResult
{
    Success, // 도구 존재 및 티어 충족
    NoTool,  // 해당 종류의 도구가 아예 없음 (또는 내구도 고갈)
    LowTier  // 도구는 있으나 티어가 부족함
}

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    public AudioSource BtnAudio;       // 버튼 음원

    [Header("UI 연결")]
    public GameObject inventoryUI;     // 가방 UI Panel/Canvas
    public GameObject StatUI;          // 스탯 UI
    public Transform contentTransform; // Scroll View -> Viewport -> Content
    public GameObject slotPrefab;      // ItemSlot 프리팹 (ItemSlot 스크립트가 붙어있어야 함)
    public GameObject infoPanelPrefab; // 정보창 프리팹

    // 인벤토리 UI가 켜져 있는지 여부를 반환하는 읽기 전용 프로퍼티
    public bool IsInventoryOpen => inventoryUI != null && inventoryUI.activeSelf;

    [Header("아이템 도감 (모든 Item 에셋이 등록된 ItemDatabase)")]
    public ItemDatabase itemDB;

    [Header("제작 (비워 두면 가방 UI 상단, 스탯 버튼 옆에 제작 버튼을 자동 생성)")]
    public Button craftRecipeButton;
    [Tooltip("제작 UI에 쓸 한글 폰트. 비워 두면 가방 UI의 텍스트 중 한글이 들어 있는 폰트를 자동으로 찾음")]
    public TMP_FontAsset uiFont;
    private CraftRecipePanel recipePanel;

    [Header("아이템 데이터 목록")]
    public List<ItemStack> itemList = new List<ItemStack>(); // 플레이어가 소지한 아이템 리스트

    // 데이터와 생성된 UI 매핑
    private Dictionary<ItemStack, ItemSlot> dynamicSlots = new Dictionary<ItemStack, ItemSlot>();

    void Awake()
    {
        // 싱글톤 초기화 
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 수량 0인 스택은 "보유 중"이 아니므로 제거 (인스펙터에 아이템만 끌어다 놓은 경우 대비)
        itemList.RemoveAll(s => s == null || s.itemData == null || s.amount <= 0);

        // 인스펙터에서 넣어 둔 스택은 생성자를 거치지 않으므로 도구 내구도를 여기서 채움
        foreach (ItemStack stack in itemList)
        {
            if (stack.durability <= 0)
                stack.durability = stack.itemData.durabilitymax;
        }

        if (StatUI != null) StatUI.SetActive(false);

        // 정보창이 BagUI 안에 이미 있으면 새로 만들지 않음 (여러 개면 어느 것이 쓰일지 알 수 없음)
        if (FindInfoPanel() == null && infoPanelPrefab != null && inventoryUI != null)
        {
            Instantiate(infoPanelPrefab, inventoryUI.transform);
        }

        // 시작 아이템이 있다면 그 슬롯을 생성
        RefreshInventoryUI();

        // 상시 표시되는 제작 버튼 준비
        SetupCraftButton();

        // 우측 하단에 무게와 슬롯 한도를 작게 보여 주는 글자 준비
        SetupBagStatusText();
    }

    // =========================================================
    //  가방 우측 하단의 무게/슬롯 표시: 스탯 창을 켜지 않아도 가방이 열려 있으면 항상 보인다
    // =========================================================
    private RectTransform bagGaugeFill;      // 무게 게이지의 채워진 부분
    private Image bagGaugeFillImage;
    private TextMeshProUGUI bagWeightText;   // 게이지 위의 "현재 / 최대" 숫자
    private TextMeshProUGUI bagSlotText;     // 게이지 위의 "슬롯 사용 / 최대"
    private GameObject bagGaugeCanvas;
    private string bagStatusShown = "";

    // 무게 게이지 색: 80% 이상 주황색, 90% 이상 빨간색, 그 아래는 초록색
    private static readonly Color GaugeGreen = new Color(0.20f, 0.75f, 0.25f, 1f);
    private static readonly Color GaugeOrange = new Color(1.00f, 0.60f, 0.10f, 1f);
    private static readonly Color GaugeRed = new Color(0.85f, 0.15f, 0.15f, 1f);

    // 우측 하단: 몬스터 체력 게이지처럼 흰색 바탕 위에 현재 무게 비율만큼 색이 차는 막대와 숫자
    private void SetupBagStatusText()
    {
        if (inventoryUI == null) return;
        TMP_FontAsset font = FindUIFont();
        Vector2 corner = new Vector2(1f, 0f); // 우측 하단 모서리 기준

        // 가방 UI 안의 배치나 겹침에 영향받지 않도록 전용 캔버스(화면 맨 위)에 만들고, 가방이 열린 동안만 켠다
        GameObject canvasGo = new GameObject("BagGauge Canvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas gaugeCanvas = canvasGo.GetComponent<Canvas>();
        gaugeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        gaugeCanvas.sortingOrder = 90;
        canvasGo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        DontDestroyOnLoad(canvasGo); // 장면이 바뀌어도 유지 (가방 매니저와 같이 살아남음)
        bagGaugeCanvas = canvasGo;
        Transform host = canvasGo.transform;

        // 슬롯 개수 (게이지 위에 작게)
        RectTransform slotRt = CraftQuantityPopup.NewRect("BagSlotText", host, corner, new Vector2(-30f, 62f), new Vector2(360f, 30f));
        bagSlotText = CraftQuantityPopup.AddText(slotRt, 22, TextAlignmentOptions.Right, font);
        bagSlotText.outlineWidth = 0.25f;
        bagSlotText.outlineColor = new Color32(0, 0, 0, 255);

        // 게이지 바탕 = 흰색
        RectTransform bar = CraftQuantityPopup.NewRect("BagWeightGauge", host, corner, new Vector2(-30f, 28f), new Vector2(360f, 30f));
        Image back = bar.gameObject.AddComponent<Image>();
        back.color = Color.white;
        back.raycastTarget = false;

        // 채워진 부분 = 현재 무게 비율 (왼쪽부터)
        bagGaugeFill = CraftQuantityPopup.NewRect("Fill", bar, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        bagGaugeFill.anchorMin = Vector2.zero;
        bagGaugeFill.anchorMax = new Vector2(0f, 1f);
        bagGaugeFill.offsetMin = Vector2.zero;
        bagGaugeFill.offsetMax = Vector2.zero;
        bagGaugeFillImage = bagGaugeFill.gameObject.AddComponent<Image>();
        bagGaugeFillImage.color = GaugeGreen;
        bagGaugeFillImage.raycastTarget = false;

        // 숫자 (흰 바탕과 색 막대 위에서 읽히도록 검은 글자)
        RectTransform textRt = CraftQuantityPopup.NewRect("Text", bar, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        bagWeightText = CraftQuantityPopup.AddText(textRt, 20, TextAlignmentOptions.Center, font);
        bagWeightText.color = Color.black;
        bagWeightText.fontStyle = FontStyles.Bold;

        canvasGo.SetActive(false);
    }

    void OnDestroy()
    {
        if (bagGaugeCanvas != null) Destroy(bagGaugeCanvas);
    }

    // 가방이 열려 있는 동안 값이 바뀌면(획득, 소모, 장비로 한도 변경 등) 게이지와 숫자를 갱신한다
    void Update()
    {
        if (bagGaugeFill == null || inventoryUI == null) return;
        bool open = inventoryUI.activeInHierarchy;
        if (bagGaugeCanvas.activeSelf != open) bagGaugeCanvas.SetActive(open);
        if (!open) return;

        int weight = CurrentWeight, weightLimit = WeightLimit, used = UsedSlots, slotLimit = SlotLimit;

        string key = $"{weight}/{weightLimit}/{used}/{slotLimit}";
        if (key == bagStatusShown) return; // 바뀐 것이 없으면 그대로 둔다
        bagStatusShown = key;

        float ratio = weightLimit > 0 ? Mathf.Clamp01(weight / (float)weightLimit) : 1f;
        bagGaugeFill.anchorMax = new Vector2(ratio, 1f);
        bagGaugeFillImage.color = ratio >= 0.9f ? GaugeRed : ratio >= 0.8f ? GaugeOrange : GaugeGreen;
        bagWeightText.text = $"무게 {weight:N0} / {weightLimit:N0}";

        string slots = $"슬롯 {used} / {slotLimit}";
        bagSlotText.text = used >= slotLimit ? $"<color=#FF6666>{slots}</color>" : slots; // 슬롯이 가득 차면 빨간색
    }

    // 가방 UI 상단의 [제작] 버튼: 스탯 버튼 바로 왼쪽에 같은 모양으로 만든다
    private void SetupCraftButton()
    {
        if (craftRecipeButton != null)
        {
            craftRecipeButton.onClick.AddListener(ToggleRecipePanel);
            return;
        }
        if (inventoryUI == null) return;

        RectTransform statBtn = inventoryUI.transform.Find("StatButton") as RectTransform;
        TMP_Text statLabel = statBtn != null ? statBtn.GetComponentInChildren<TMP_Text>(true) : null;

        Vector2 anchor = new Vector2(1f, 1f);
        Vector2 pos = new Vector2(-420f, -100f);
        Vector2 size = new Vector2(150f, 100f);
        Vector2 pivot = Vector2.zero;
        if (statBtn != null)
        {
            anchor = statBtn.anchorMax;
            pivot = statBtn.pivot;
            size = statBtn.sizeDelta;
            pos = statBtn.anchoredPosition - new Vector2(size.x + 10f, 0f);
        }

        GameObject go = CraftQuantityPopup.CreateButton(inventoryUI.transform, "CraftRecipeButton", "제작",
            FindUIFont(), new Color(0.2f, 0.55f, 0.3f), anchor, pos, size, ToggleRecipePanel);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;

        // 스탯 버튼과 같은 이미지/글자 모양을 따라 함
        if (statBtn != null)
        {
            Image src = statBtn.GetComponent<Image>();
            Image dst = go.GetComponent<Image>();
            if (src != null)
            {
                dst.sprite = src.sprite;
                dst.type = src.type;
                dst.color = src.color;
            }
            TMP_Text dstLabel = go.GetComponentInChildren<TMP_Text>();
            if (dstLabel != null && statLabel != null)
            {
                dstLabel.fontSize = statLabel.fontSize;
                dstLabel.color = statLabel.color;
            }
        }
    }

    // 제작 UI에 쓸 한글 폰트를 찾음.
    // 첫 번째 텍스트의 폰트를 그냥 쓰면 안 됨: 슬롯의 SlotText처럼 숫자만 쓰는 기본 폰트(LiberationSans)에는 한글이 없어 네모로 나옴.
    // 그래서 uiFont가 지정돼 있으면 그것을, 아니면 가방 UI 텍스트 중 한글 글자('한')가 실제로 들어 있는 폰트를 고른다.
    private TMP_FontAsset FindUIFont()
    {
        if (uiFont != null) return uiFont;
        if (inventoryUI == null) return null;

        TMP_FontAsset fallback = null;
        foreach (TMP_Text label in inventoryUI.GetComponentsInChildren<TMP_Text>(true))
        {
            TMP_FontAsset f = label.font;
            if (f == null) continue;
            if (f.HasCharacter('한')) return f;
            if (fallback == null) fallback = f;
        }

        if (fallback != null)
            Debug.LogWarning("[InventoryManager] 한글이 들어 있는 TMP 폰트를 찾지 못했습니다. uiFont에 NotoSansCJKkr SDF를 지정하세요.");
        return fallback;
    }

    // [제작] 버튼: 제작 레시피 슬라이드 창을 열고 닫음
    public void ToggleRecipePanel()
    {
        if (BtnAudio != null) BtnAudio.Play();
        if (recipePanel == null)
        {
            recipePanel = CraftRecipePanel.Create(inventoryUI.transform, FindUIFont());
        }
        recipePanel.Toggle();
    }

    // 인벤토리 정보창(ItemInfoPanel)을 찾아 반환. 스탯창에 붙은 것은 제외. 비활성 상태여도 찾음.
    private ItemInfoPanel infoPanelCache;
    public ItemInfoPanel FindInfoPanel()
    {
        if (infoPanelCache != null) return infoPanelCache;
        if (inventoryUI == null) return null;

        foreach (ItemInfoPanel p in inventoryUI.GetComponentsInChildren<ItemInfoPanel>(true))
        {
            bool isStatPanel = p.name.Contains("Stat")
                || (StatUI != null && p.transform.IsChildOf(StatUI.transform));
            if (!isStatPanel)
            {
                infoPanelCache = p;
                break;
            }
        }
        return infoPanelCache;
    }

    // 정보창과 제작 팝업을 숨김
    private void HideInfoPanel()
    {
        ItemInfoPanel panel = FindInfoPanel();
        if (panel != null) panel.HidePanel();
    }

    public void ToggleInventory()
    {
        if (inventoryUI.activeSelf) CloseInventory();
        else OpenInventory();
    }

    public void OpenInventory()
    {
        if (MapManager.Instance != null)
        {
            MapManager.Instance.CloseMapQuiet(); // 소리는 아래에서 한 번만
        }

        if (BtnAudio != null) BtnAudio.Play();
        inventoryUI.SetActive(true);
        PlayerUI.RefreshAll(); // 스탯 창 텍스트를 지금 값으로
    }

    // 가방 닫기 (X 버튼이 인스펙터에서 이 함수를 이름으로 부르므로, 매개변수를 추가하면 안 됨!)
    public void CloseInventory()
    {
        CloseInventoryInternal(true);
    }

    // 소리 없이 닫기: 지도를 열면서 가방을 닫는 경우처럼 다른 소리가 이미 나는 경우에 사용
    public void CloseInventoryQuiet()
    {
        CloseInventoryInternal(false);
    }

    // 이미 닫혀 있으면 소리를 내지 않음
    private void CloseInventoryInternal(bool playSound)
    {
        if (playSound && inventoryUI.activeSelf && BtnAudio != null) BtnAudio.Play();
        HideInfoPanel(); // 가방을 닫으면 정보창과 제작창도 같이 닫음
        if (recipePanel != null) recipePanel.Close(true);
        inventoryUI.SetActive(false);
    }

    // ESC 키용: 제작 팝업 → 레시피 창 순서로 한 겹만 닫고, 닫은 것이 있으면 true
    public bool CloseTopCraftLayer()
    {
        return recipePanel != null && recipePanel.CloseTopLayer();
    }

    // 스탯 버튼: 스탯창을 켜면 정보창(ItemInfoPanel)은 숨기고, 한 번 더 누르면 스탯창을 끔
    public void OpenStat()
    {
        if (BtnAudio != null) BtnAudio.Play();
        bool willStatBeActive = !StatUI.activeSelf;
        StatUI.SetActive(willStatBeActive);

        if (willStatBeActive) { HideInfoPanel(); PlayerUI.RefreshAll(); }
    }

    public void CloseStat()
    {
        if (StatUI == null || !StatUI.activeSelf) return;
        if (BtnAudio != null) BtnAudio.Play();
        StatUI.SetActive(false);
    }

    // 인벤토리 정렬 등 전체 새로고침이 필요할 때만 호출
    public void RefreshInventoryUI()
    {
        if (contentTransform == null) return;

        // 1. 기존 UI 및 딕셔너리 비우기
        foreach (Transform child in contentTransform)
        {
            Destroy(child.gameObject);
        }
        dynamicSlots.Clear();

        // 2. 리스트 순서대로 슬롯 재생성
        foreach (ItemStack stack in itemList)
        {
            CreateSlot(stack);
        }
    }

    // 스택 하나의 슬롯 UI를 Content 맨 뒤에 생성 (GridLayoutGroup이 좌상단부터 우측, 아래 순으로 배치)
    private ItemSlot CreateSlot(ItemStack stack)
    {
        if (slotPrefab == null || contentTransform == null)
        {
            Debug.LogError("InventoryManager의 slotPrefab 또는 contentTransform이 연결되지 않았습니다!");
            return null;
        }

        GameObject slotObj = Instantiate(slotPrefab, contentTransform);
        slotObj.SetActive(true); // slotPrefab이 꺼져 있는 템플릿이어도 복제본은 켜야 보임
        slotObj.transform.SetAsLastSibling();

        ItemSlot slot = slotObj.GetComponent<ItemSlot>();
        if (slot == null)
        {
            Debug.LogError("slotPrefab에 ItemSlot 스크립트가 없습니다!");
            Destroy(slotObj);
            return null;
        }

        slot.Stack = stack; // 슬롯이 어느 스택(수량/내구도)을 보여 주는지 연결
        slot.SetupSlot(stack.itemData);
        slot.UpdateCountUI(stack.amount);
        dynamicSlots[stack] = slot;
        return slot;
    }

    // 스택의 슬롯 UI가 있으면 수량만 갱신하고, 없으면 새로 만든다
    private void RefreshSlot(ItemStack stack)
    {
        if (dynamicSlots.TryGetValue(stack, out ItemSlot slot) && slot != null)
            slot.UpdateCountUI(stack.amount);
        else
            CreateSlot(stack);
    }

    /// <summary>
    /// 인벤토리 내 적절한 도구를 자동으로 찾아 내구도를 1 차감합니다.
    /// 소모 순서: 필요 티어 이상인 도구 중 가장 낮은 티어 먼저, 티어가 같으면 앞쪽(좌상단) 슬롯 먼저.
    /// 도구는 슬롯마다 따로 내구도를 가집니다(countmax = 1).
    /// </summary>
    // 방금 ConsumeToolDurability로 쓴 도구의 남은 내구도 (닳지 않는 도구는 UnbreakableDurability)
    public int LastToolDurability { get; private set; }

    public Item ConsumeToolDurability(ToolType requiredType, int requiredTier, out ToolCheckResult checkResult, out bool isBroken)
    {
        isBroken = false;
        LastToolDurability = 0;

        var ownedTools = itemList
            .Where(stack => stack.itemData.toolType == requiredType && IsUsableTool(stack))
            .ToList();

        if (ownedTools.Count == 0)
        {
            checkResult = ToolCheckResult.NoTool;
            return null;
        }

        ItemStack targetStack = ownedTools
            .Where(stack => stack.itemData.tier >= requiredTier)
            .OrderBy(stack => stack.itemData.tier)
            .FirstOrDefault();

        if (targetStack == null)
        {
            checkResult = ToolCheckResult.LowTier;
            return null;
        }

        checkResult = ToolCheckResult.Success;
        Item usedTool = targetStack.itemData;
        if (IsUnbreakable(targetStack)) { LastToolDurability = UnbreakableDurability; return usedTool; } // 닳지 않는 도구는 내구도를 깎지 않는다
        targetStack.durability -= 1;
        LastToolDurability = Mathf.Max(0, targetStack.durability);
        if (targetStack.durability <= 0)
        {
            targetStack.durability = 0;
            isBroken = true;

            // countmax가 2 이상인 도구 스택이면 하나만 부서지고 다음 도구가 새 내구도로 이어짐
            // (countmax = 1이면 해당 슬롯의 도구는 내구도 0인 채로 남음)
            if (targetStack.amount > 1)
            {
                RemoveFromStack(targetStack, 1);
                targetStack.durability = usedTool.durabilitymax;
            }
        }
        return usedTool;
    }

    // id로 아이템 원본 데이터(ScriptableObject) 조회
    public Item GetItemData(int id)
    {
        if (itemDB == null)
        {
            Debug.LogError("InventoryManager에 itemDB(ItemDatabase)가 연결되지 않았습니다!");
            return null;
        }
        Item item = itemDB.Get(id);
        if (item == null) Debug.LogError($"ItemDatabase에 ID {id} 아이템이 없습니다!");
        return item;
    }

    // 특정 아이템의 현재 총 보유 수량 반환
    public int GetItemCount(int itemID)
    {
        int total = 0;
        foreach (var stack in itemList)
        {
            if (stack.itemData.id == itemID)
                total += stack.amount;
        }
        return total;
    }

    // =========================================================
    //  가방 한도: 슬롯 개수와 무게
    // =========================================================
    // 한도 = 기본값(기본 능력치 표) + 장비 보너스. 장비 시스템이 GameManager.SlotBonus / WeightBonus를 채우면 자동으로 반영된다.
    public int SlotLimit { get { return Mathf.Max(0, GameManager.SlotMax + GameManager.SlotBonus); } }
    public int WeightLimit { get { return Mathf.Max(0, GameManager.WeightMax + GameManager.WeightBonus); } }
    public int UsedSlots { get { return itemList.Count; } }

    // 지금 들고 있는 무게 합계 (아이템 1개 무게 x 수량)
    public int CurrentWeight
    {
        get
        {
            long total = 0;
            foreach (ItemStack s in itemList)
                total += (long)s.itemData.weight * s.amount;
            return (int)System.Math.Min(total, int.MaxValue);
        }
    }

    // 아이템을 더 넣을 수 없는 이유
    public enum AddBlock { None, Slots, Weight }

    // 마지막 AddItem이 일부 또는 전부 넣지 못한 이유 (None이면 전부 넣었음)
    public AddBlock LastAddBlock { get; private set; }

    public static string BlockMessage(AddBlock block)
    {
        switch (block)
        {
            case AddBlock.Slots: return "가방이 가득 찼습니다!";
            case AddBlock.Weight: return "너무 무거워서 더 들 수 없습니다!";
            default: return "";
        }
    }

    // 슬롯과 무게 한도 안에서 want개 중 몇 개까지 넣을 수 있는지 계산한다
    public int GetAddableAmount(Item item, int want, out AddBlock block)
    {
        block = AddBlock.None;
        if (item == null || want <= 0) return 0;

        // 슬롯: 같은 아이템 스택의 남은 자리 + 빈 슬롯 수 x 슬롯당 최대 수량
        int limit = StackLimit(item);
        long bySlots = 0;
        foreach (ItemStack s in itemList)
            if (s.itemData.id == item.id) bySlots += Mathf.Max(0, limit - s.amount);
        bySlots += (long)Mathf.Max(0, SlotLimit - itemList.Count) * limit;

        // 무게: 남은 무게 여유 / 아이템 1개 무게
        long byWeight = item.weight > 0 ? (long)Mathf.Max(0, WeightLimit - CurrentWeight) / item.weight : long.MaxValue;

        long addable = System.Math.Min(want, System.Math.Min(bySlots, byWeight));
        if (addable < want) block = bySlots <= byWeight ? AddBlock.Slots : AddBlock.Weight;
        return (int)addable;
    }

    // 한글 폰트 (획득 표시 등 다른 UI가 가방 UI와 같은 폰트를 쓰도록)
    public TMP_FontAsset UIFont { get { return FindUIFont(); } }

    // =========================================================
    //  도구: 필요 티어 이상이고 내구도가 남은 도구 (낮은 티어 먼저, 같으면 앞쪽 슬롯 먼저)
    // =========================================================
    // 내구도(durabilitymax)가 0인 도구는 닳지 않는 도구다 (예: 가죽 물통). 내구도 검사와 소모를 하지 않는다.
    public const int UnbreakableDurability = 1000000; // 닳지 않는 도구의 "남은 내구도"로 취급하는 값 (화면에서는 "제한 없음")
    private static bool IsUnbreakable(ItemStack s) { return s.itemData.durabilitymax <= 0; }
    private static bool IsUsableTool(ItemStack s) { return IsUnbreakable(s) || s.durability > 0; }

    private List<ItemStack> EligibleTools(ToolType type, int tier)
    {
        return itemList
            .Where(s => s.itemData.toolType == type && s.itemData.tier >= tier && IsUsableTool(s))
            .OrderBy(s => s.itemData.tier)
            .ToList();
    }

    // 사용할 수 있는 해당 도구들의 남은 내구도 합계
    public int GetToolDurability(ToolType type, int tier)
    {
        int total = 0;
        foreach (ItemStack s in EligibleTools(type, tier))
        {
            if (IsUnbreakable(s)) return UnbreakableDurability; // 하나라도 닳지 않는 도구가 있으면 제한 없음
            total += s.durability;
        }
        return total;
    }

    // =========================================================
    //  제작
    // =========================================================
    // 마지막 제작이 실패한 이유 (제작 창이 보여 줌)
    public string LastCraftFailReason { get; private set; }

    // 해당 레시피로 최대 몇 개까지 만들 수 있는지 계산 (재료와 필요한 도구의 내구도 모두 고려)
    public int GetMaxCraftableAmount(Recipe recipe)
    {
        if (recipe == null || recipe.ingredients.Count == 0) return 0;

        int maxCraft = int.MaxValue;
        foreach (var ing in recipe.ingredients)
        {
            if (ing.amount <= 0) continue;
            int hasCount = GetItemCount(ing.itemId);
            if (hasCount < ing.amount) return 0; // 재료 부족 시 0개

            int possible = hasCount / ing.amount;
            if (possible < maxCraft) maxCraft = possible;
        }

        // 도구: 1회 제작에 내구도 durabilityCost가 깎이므로 남은 내구도로 몇 번 만들 수 있는지
        if (recipe.tools != null)
        {
            foreach (RecipeTool t in recipe.tools)
            {
                if (t == null) continue;
                int possible = GetToolDurability(t.toolType, t.tier) / Mathf.Max(1, t.durabilityCost);
                if (possible < 1) return 0; // 도구가 없거나 내구도 부족
                if (possible < maxCraft) maxCraft = possible;
            }
        }
        return maxCraft;
    }

    // 실제 제작 실행 (재료 소모 + 도구 내구도 소모 + 아이템 추가)
    public bool TryCraftItem(Recipe recipe, int craftCount)
    {
        return TryCraftItem(GetItemData(recipe.resultItemId), recipe, craftCount);
    }

    // result: 이 레시피로 만들어지는 아이템 (아이템 에셋에 들어 있는 레시피는 그 아이템을 만든다)
    public bool TryCraftItem(Item result, Recipe recipe, int craftCount)
    {
        LastCraftFailReason = "";
        if (result == null || recipe == null) return false;
        if (craftCount <= 0 || GetMaxCraftableAmount(recipe) < craftCount)
        {
            LastCraftFailReason = "재료나 도구가 부족합니다!";
            return false;
        }

        // 완성품이 들어갈 자리와 무게가 있는지는 재료를 뺀 뒤에야 알 수 있다.
        // 그래서 현재 상태를 복사해 두었다가, 안 되면 그대로 되돌린다.
        List<ItemStack> snapshot = itemList
            .Select(s => new ItemStack(s.itemData, s.amount) { durability = s.durability })
            .ToList();

        // 1. 재료 소모
        foreach (var ing in recipe.ingredients)
        {
            RemoveItem(ing.itemId, ing.amount * craftCount);
        }

        // 2. 완성품이 가방에 다 들어가는지 확인
        int total = recipe.resultAmount * craftCount;
        AddBlock block;
        if (GetAddableAmount(result, total, out block) < total)
        {
            itemList = snapshot;
            RefreshInventoryUI();
            GameManager.Weight = CurrentWeight;
            PlayerUI.RefreshAll();
            LastCraftFailReason = BlockMessage(block);
            return false;
        }

        // 3. 도구 내구도 소모 (사라지지는 않고 내구도만 깎임)
        if (recipe.tools != null)
        {
            foreach (RecipeTool t in recipe.tools)
            {
                if (t == null) continue;
                int remaining = Mathf.Max(1, t.durabilityCost) * craftCount;
                foreach (ItemStack s in EligibleTools(t.toolType, t.tier))
                {
                    if (IsUnbreakable(s)) break; // 닳지 않는 도구는 내구도를 깎지 않는다
                    int take = Mathf.Min(s.durability, remaining);
                    s.durability -= take;
                    remaining -= take;
                    if (remaining <= 0) break;
                }
            }
        }

        // 4. 완성품 추가
        AddItem(result, total);
        return true;
    }

    // =========================================================
    //  소모품 사용: 사용 효과(체력/SP/마나 회복)가 하나라도 있는 아이템은 인벤토리에서 [사용]할 수 있다
    // =========================================================
    public static bool CanUse(Item item)
    {
        return item != null && (item.useHp != 0 || item.useSp != 0 || item.useMana != 0);
    }

    // 스택에서 아이템 1개를 사용한다. 성공하면 true. message에는 결과(또는 못 쓰는 이유)가 담긴다.
    // 마이너스 값이 있는 아이템(독 등)도 먹을 수 있고, 체력은 1 밑으로 내려가지 않는다.
    public bool UseItem(ItemStack stack, out string message)
    {
        message = "";
        if (stack == null || stack.itemData == null || !CanUse(stack.itemData)) return false;
        Item item = stack.itemData;

        int hpBefore = GameManager.Hp;
        int spBefore = GameManager.SP;
        int manaBefore = GameManager.Mana;

        GameManager.Hp = Mathf.Clamp(GameManager.Hp + item.useHp, Mathf.Min(1, GameManager.Hp), Mathf.Max(BattleCalc.PlayerMaxHp(), GameManager.Hp));
        GameManager.SP = Mathf.Clamp(GameManager.SP + item.useSp, 0, Mathf.Max(GameManager.SPMax, GameManager.SP));
        GameManager.Mana = Mathf.Clamp(GameManager.Mana + item.useMana, 0, Mathf.Max(GameManager.ManaMax, GameManager.Mana));

        int hp = GameManager.Hp - hpBefore;
        int sp = GameManager.SP - spBefore;
        int mana = GameManager.Mana - manaBefore;

        // 이미 전부 가득 차서 아무 효과가 없으면 소모하지 않는다 (마이너스 효과가 있는 아이템은 항상 먹을 수 있음)
        bool harmful = item.useHp < 0 || item.useSp < 0 || item.useMana < 0;
        if (!harmful && hp == 0 && sp == 0 && mana == 0)
        {
            message = "이미 가득 차서 사용할 필요가 없습니다.";
            return false;
        }

        // 사용 후 남는 아이템(예: 가득찬 물통 -> 물통)은 내구도를 이어받는다. 내구도가 0이면 파손되어 남지 않는다.
        Item leftover = item.useResultItemId > 0 ? GetItemData(item.useResultItemId) : null;
        int carry = -1;
        if (leftover != null && item.durabilitymax > 0) carry = stack.durability;
        if (leftover != null && leftover.durabilitymax > 0 && carry < 0) carry = leftover.durabilitymax;

        RemoveFromStack(stack, 1); // 1개 소모 (0개가 되면 슬롯이 사라짐)

        List<string> effects = new List<string>();
        if (hp != 0) effects.Add($"체력 {hp:+#;-#}");
        if (sp != 0) effects.Add($"SP {sp:+#;-#}");
        if (mana != 0) effects.Add($"마나 {mana:+#;-#}");
        message = $"{item.itemName} 사용" + (effects.Count > 0 ? $": {string.Join(", ", effects)}" : "");

        if (leftover != null)
        {
            if (leftover.durabilitymax > 0 && carry <= 0)
                message += $"\n[{leftover.itemName}]이(가) 파손되었다.";
            else if (AddItem(leftover, 1, carry) < 1)
                message += $"\n가방이 가득 차서 [{leftover.itemName}]을(를) 버렸다.";
        }

        PlayerUI.RefreshAll();
        return true;
    }

    // 스택의 절반(내림)을 새 슬롯으로 나눈다. 슬롯이 없거나 수량이 1개면 실패
    public bool SplitStack(ItemStack stack, out string message)
    {
        message = "";
        if (stack == null || stack.amount < 2) { message = "나눌 수 없습니다. (수량이 1개)"; return false; }
        if (UsedSlots >= SlotLimit) { message = "빈 슬롯이 없어 나눌 수 없습니다."; return false; }

        int half = stack.amount / 2;
        stack.amount -= half;
        ItemStack part = new ItemStack(stack.itemData, half);
        part.durability = stack.durability;
        itemList.Insert(itemList.IndexOf(stack) + 1, part);

        RefreshInventoryUI(); // 순서대로 슬롯을 다시 만든다
        GameManager.Weight = CurrentWeight;
        PlayerUI.RefreshAll();
        message = $"{stack.itemData.itemName}을(를) {stack.amount}개와 {half}개로 나눴다.";
        return true;
    }

    // 스택 전체를 버린다
    public void DiscardStack(ItemStack stack)
    {
        if (stack == null) return;
        RemoveFromStack(stack, stack.amount);
    }

    // =========================================================
    //  기절 페널티 (기본 능력치 표의 [기절] 설정): 병원비를 내고, 아이템 일부를 잃는다
    //  도구/장비(도구 종류가 있거나 내구도가 있는 아이템)는 잃지 않는다.
    // =========================================================
    public string ApplyFaintPenalty()
    {
        PlayerBaseTable b = GameTables.PlayerBase;
        List<string> parts = new List<string>();

        // 병원비: 가진 골드보다 많으면 가진 만큼만 낸다
        int fee = Mathf.Min(GameManager.Gold, Mathf.Max(0, b.hospitalFee));
        GameManager.Gold -= fee;
        if (b.hospitalFee > 0) parts.Add($"병원비 {fee}골드를 냈다");

        // 아이템: 수량의 일정 비율을 잃는다 (소수점 부분은 그 확률로 1개 더 잃음)
        float rate = Mathf.Clamp(b.itemLossPercent, 0f, 100f) / 100f;
        bool lostAny = false;
        if (rate > 0f)
        {
            foreach (ItemStack s in itemList.ToList())
            {
                Item it = s.itemData;
                if (it.toolType != ToolType.None || it.durabilitymax > 0) continue; // 도구/장비는 안전

                float exact = s.amount * rate;
                int lose = Mathf.FloorToInt(exact);
                if (Random.value < exact - lose) lose++;
                lose = Mathf.Min(lose, s.amount);
                if (lose <= 0) continue;

                RemoveFromStack(s, lose);
                lostAny = true;
            }
        }
        if (lostAny) parts.Add("아이템 일부를 잃었다");

        PlayerUI.RefreshAll();
        return parts.Count > 0 ? string.Join(", ", parts) : "";
    }

    // 한 슬롯에 쌓을 수 있는 최대 수량 (countmax가 0 이하면 제한 없음)
    private static int StackLimit(Item item)
    {
        return item.countmax > 0 ? item.countmax : int.MaxValue;
    }

    /// <summary>
    /// 아이템 획득 시 호출. 슬롯 개수와 무게 한도 안에서 넣을 수 있는 만큼만 넣고, 실제로 넣은 개수를 돌려준다.
    /// (못 넣은 이유는 LastAddBlock). 같은 아이템의 빈자리가 있는 스택(앞쪽 슬롯부터)을 먼저 채우고, 남으면 새 슬롯을 만든다.
    /// 도구/장비(countmax = 1)는 하나마다 슬롯이 따로 생기고 내구도도 각자 가진다.
    /// 넣은 아이템은 화면 중앙에 이미지가 잠깐 표시된다.
    /// </summary>
    public int AddItem(Item newItem, int amount = 1, int durability = -1)
    {
        LastAddBlock = AddBlock.None;
        if (newItem == null || amount <= 0) return 0;

        AddBlock block;
        amount = GetAddableAmount(newItem, amount, out block);
        LastAddBlock = block;
        if (amount <= 0) return 0;

        int added = amount;
        int limit = StackLimit(newItem);

        // durability를 지정한 내구도 아이템(예: 물을 담은 가죽 물통)은 내구도가 다르므로 기존 스택에 합치지 않는다
        bool keepSeparate = durability >= 0 && newItem.durabilitymax > 0;

        // 1. 자리가 남은 기존 스택 채우기
        foreach (ItemStack s in itemList)
        {
            if (amount <= 0) break;
            if (keepSeparate) break;
            if (s.itemData.id != newItem.id || s.amount >= limit) continue;

            int add = Mathf.Min(limit - s.amount, amount);
            s.amount += add;
            amount -= add;
            RefreshSlot(s);
        }

        // 2. 남은 수량은 새 스택(= 새 슬롯, 획득 순서대로 뒤에 추가)
        while (amount > 0)
        {
            int add = Mathf.Min(limit, amount);
            ItemStack stack = new ItemStack(newItem, add);
            if (keepSeparate) stack.durability = Mathf.Min(durability, newItem.durabilitymax);
            itemList.Add(stack);
            CreateSlot(stack);
            amount -= add;
        }

        GameManager.Weight = CurrentWeight;
        PlayerUI.RefreshAll();
        ItemGainToast.Show(newItem, added);
        return added;
    }

    /// <summary>
    /// 크래프팅/사용 등으로 아이템 삭제 시 호출 (뒤쪽 스택부터 차감)
    /// </summary>
    public void RemoveItem(int targetItemId, int amount = 1)
    {
        for (int i = itemList.Count - 1; i >= 0 && amount > 0; i--)
        {
            ItemStack s = itemList[i];
            if (s.itemData.id != targetItemId) continue;

            int take = Mathf.Min(s.amount, amount);
            amount -= take;
            RemoveFromStack(s, take);
        }
    }

    // 스택에서 수량을 빼고, 0이 되면 리스트와 슬롯 UI에서 제거
    private void RemoveFromStack(ItemStack stack, int take)
    {
        stack.amount -= take;

        if (stack.amount <= 0)
        {
            // 슬롯 UI 파괴 (GridLayoutGroup이 자동으로 땡겨줌)
            if (dynamicSlots.TryGetValue(stack, out ItemSlot slot))
            {
                if (slot != null) Destroy(slot.gameObject);
                dynamicSlots.Remove(stack);
            }
            itemList.Remove(stack);
        }
        else
        {
            RefreshSlot(stack);
        }

        GameManager.Weight = CurrentWeight;
        PlayerUI.RefreshAll();
    }
}
[System.Serializable]
public class ItemStack
{
    public Item itemData;  // ScriptableObject 기반 아이템 데이터 (읽기만 할 것, 수정 금지)
    public int amount;     // 소지 수량
    public int durability; // 현재 내구도 (도구 스택 전체가 공유)

    public ItemStack(Item data, int amount)
    {
        this.itemData = data;
        this.amount = amount;
        this.durability = data != null ? data.durabilitymax : 0;
    }
}