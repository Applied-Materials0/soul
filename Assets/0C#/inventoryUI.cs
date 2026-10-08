using System.Collections;
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

        // 정렬 버튼 (제작 버튼 왼쪽)
        SetupSortButton();
        SetupEquipPanels();
        RecalcEquipment();

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
    private string bagStatusShown = "";

    // 무게 게이지 색: 80% 이상 주황색, 90% 이상 빨간색, 그 아래는 초록색
    private static readonly Color GaugeGreen = new Color(0.20f, 0.75f, 0.25f, 1f);
    private static readonly Color GaugeOrange = new Color(1.00f, 0.60f, 0.10f, 1f);
    private static readonly Color GaugeRed = new Color(0.85f, 0.15f, 0.15f, 1f);

    // 우측 하단: 흰색 바탕 위에 현재 무게 비율만큼 색이 차는 막대와 숫자.
    // 위치/크기/색은 BagUI 프리팹 안의 BagWeightGauge(Fill, Text), BagSlotText, BagMessage 오브젝트를 직접 고치면 된다.
    // (프리팹에 없으면 아래에서 같은 모양으로 임시로 만든다)
    private void SetupBagStatusText()
    {
        if (inventoryUI == null) return;
        Transform root = inventoryUI.transform;

        Transform gauge = root.Find("BagWeightGauge");
        if (gauge != null)
        {
            bagGaugeFill = gauge.Find("Fill") as RectTransform;
            bagGaugeFillImage = bagGaugeFill != null ? bagGaugeFill.GetComponent<Image>() : null;
            Transform t = gauge.Find("Text");
            bagWeightText = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }
        Transform slot = root.Find("BagSlotText");
        if (slot != null) bagSlotText = slot.GetComponent<TextMeshProUGUI>();
        Transform msg = root.Find("BagMessage");
        if (msg != null)
        {
            bagMessage = msg.gameObject;
            bagMessageGroup = msg.GetComponent<CanvasGroup>();
            bagMessageText = msg.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (bagGaugeFill != null && bagGaugeFillImage != null && bagWeightText != null && bagSlotText != null) return;

        // ---- 프리팹에 없을 때의 대체 생성 (가방 UI 안 맨 위에) ----
        TMP_FontAsset font = FindUIFont();
        Vector2 corner = new Vector2(1f, 0f);

        RectTransform slotRt = CraftQuantityPopup.NewRect("BagSlotText", root, corner, new Vector2(-30f, 62f), new Vector2(360f, 30f));
        bagSlotText = CraftQuantityPopup.AddText(slotRt, 22, TextAlignmentOptions.Right, font);
        bagSlotText.color = Color.black;
        bagSlotText.fontStyle = FontStyles.Bold;

        RectTransform bar = CraftQuantityPopup.NewRect("BagWeightGauge", root, corner, new Vector2(-30f, 28f), new Vector2(360f, 30f));
        Image back = bar.gameObject.AddComponent<Image>();
        back.color = Color.white;
        back.raycastTarget = false;

        bagGaugeFill = CraftQuantityPopup.NewRect("Fill", bar, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        bagGaugeFill.anchorMin = Vector2.zero;
        bagGaugeFill.anchorMax = new Vector2(0f, 1f);
        bagGaugeFill.offsetMin = Vector2.zero;
        bagGaugeFill.offsetMax = Vector2.zero;
        bagGaugeFillImage = bagGaugeFill.gameObject.AddComponent<Image>();
        bagGaugeFillImage.color = GaugeGreen;
        bagGaugeFillImage.raycastTarget = false;

        RectTransform textRt = CraftQuantityPopup.NewRect("Text", bar, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        bagWeightText = CraftQuantityPopup.AddText(textRt, 20, TextAlignmentOptions.Center, font);
        bagWeightText.color = Color.black;
        bagWeightText.fontStyle = FontStyles.Bold;

        slotRt.SetAsLastSibling();
        bar.SetAsLastSibling();
    }

    // =========================================================
    //  가방 키보드 조작: W/A/S/D(또는 방향키)로 슬롯 이동, Space = 사용/장착/수리, Q = 버리기(두 번 눌러야 버림)
    // =========================================================
    private ItemStack kbSelected;

    private void HandleBagKeys()
    {
        // 수리 창이 떠 있으면 그것만 조작: Space/Enter = 수리, Q = 취소
        Transform rp = inventoryUI.transform.Find("RepairPopup");
        if (rp != null)
        {
            RepairPopup popup = rp.GetComponent<RepairPopup>();
            if (popup != null)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) popup.Confirm();
                else if (Input.GetKeyDown(KeyCode.Q)) popup.Close();
            }
            return;
        }

        // 제작 레시피 창이 열려 있으면 슬롯 이동 키는 쓰지 않는다
        if (recipePanel != null && recipePanel.IsOpen) return;

        int dx = 0, dy = 0;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) dx = -1;
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dx = 1;
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) dy = 1;
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) dy = -1;
        if (dx != 0 || dy != 0) MoveKeyboardSelection(dx, dy);

        ItemInfoPanel panel = ItemInfoPanel.Instance;
        if (panel == null) return;
        if (Input.GetKeyDown(KeyCode.Space)) panel.PrimaryAction();
        if (Input.GetKeyDown(KeyCode.Q)) panel.DiscardKey();
    }

    // 마우스로 슬롯을 눌렀을 때도 키보드 선택이 거기서 이어지게 한다
    public void MarkKeyboardSelection(ItemStack stack)
    {
        SetKeyboardHighlight(kbSelected, false);
        kbSelected = stack;
        SetKeyboardHighlight(kbSelected, true);
    }

    private void SetKeyboardHighlight(ItemStack stack, bool on)
    {
        if (stack != null && dynamicSlots.TryGetValue(stack, out ItemSlot slot) && slot != null) slot.SetSelected(on);
    }

    private void MoveKeyboardSelection(int dx, int dy)
    {
        List<ItemStack> shown = new List<ItemStack>();
        foreach (ItemStack s in itemList)
            if (dynamicSlots.TryGetValue(s, out ItemSlot sl) && sl != null && sl.gameObject.activeInHierarchy) shown.Add(s);
        if (shown.Count == 0) return;

        // 처음이거나 선택한 것이 사라졌으면 첫 슬롯부터
        if (kbSelected == null || !shown.Contains(kbSelected)) { SelectByKeyboard(shown[0]); return; }

        // 누른 방향으로 가장 가까운 슬롯 (방향에서 벗어난 만큼 불리하게 계산)
        Vector3 from = dynamicSlots[kbSelected].transform.position;
        ItemStack best = null;
        float bestScore = float.MaxValue;
        foreach (ItemStack s in shown)
        {
            if (s == kbSelected) continue;
            Vector3 d = dynamicSlots[s].transform.position - from;
            float along = d.x * dx + d.y * dy;
            if (along <= 0.001f) continue;
            float perp = Mathf.Abs(dx != 0 ? d.y : d.x);
            float score = along + perp * 2f;
            if (score < bestScore) { bestScore = score; best = s; }
        }
        if (best != null) SelectByKeyboard(best);
    }

    // 슬롯을 고르면 정보창이 열린다 (마우스로 누른 것과 같음)
    private void SelectByKeyboard(ItemStack stack)
    {
        MarkKeyboardSelection(stack);
        CloseStat();
        ItemInfoPanel panel = ItemInfoPanel.Instance;
        if (panel != null) panel.ShowItem(stack.itemData, stack);
    }

    // 가방이 열려 있는 동안 값이 바뀌면(획득, 소모, 장비로 한도 변경 등) 게이지와 숫자를 갱신한다
    void Update()
    {
        if (inventoryUI != null && inventoryUI.activeInHierarchy)
        {
            if (Input.GetKeyDown(KeyCode.C)) ToggleRecipePanel();
            if (Input.GetKeyDown(KeyCode.X)) OnClickSort();
            if (Input.GetKeyDown(KeyCode.Z)) OpenStat();
            HandleBagKeys();
        }

        if (bagGaugeFill == null || inventoryUI == null || !inventoryUI.activeInHierarchy) return;

        int weight = CurrentWeight, weightLimit = WeightLimit, used = UsedSlots, slotLimit = SlotLimit;

        string key = $"{weight}/{weightLimit}/{used}/{slotLimit}";
        if (key == bagStatusShown) return; // 바뀐 것이 없으면 그대로 둔다
        bagStatusShown = key;

        float ratio = weightLimit > 0 ? Mathf.Clamp01(weight / (float)weightLimit) : 1f;
        bagGaugeFill.anchorMax = new Vector2(ratio, 1f);
        bagGaugeFillImage.color = ratio >= 0.9f ? GaugeRed : ratio >= 0.8f ? GaugeOrange : GaugeGreen;
        bagWeightText.text = $"무게 {weight:N0} / {weightLimit:N0}";

        string slots = $"슬롯 {used} / {slotLimit}";
        bagSlotText.text = used >= slotLimit ? $"<color=#CC0000>{slots}</color>" : slots; // 슬롯이 가득 차면 빨간색
    }

    // =========================================================
    //  가방 안 알림: 가방이 열려 있으면 BagMessage 오브젝트에 보여 준다 (가방 UI가 다른 알림을 덮기 때문)
    // =========================================================
    private GameObject bagMessage;
    private CanvasGroup bagMessageGroup;
    private TextMeshProUGUI bagMessageText;
    private Coroutine bagMessageRoutine;

    // 가방이 열려 있고 BagMessage가 있으면 거기에 보여 주고 true. 아니면 false (다른 곳에 보여 줘야 함)
    // 가방이 열려 있으면 가방 안 알림에, 아니면 화면 중앙 알림에 보여 준다
    public void ShowBagMessageOrToast(string text)
    {
        if (!ShowBagMessage(text)) ItemGainToast.ShowMessage(text);
    }

    public bool ShowBagMessage(string text)
    {
        if (bagMessage == null || bagMessageText == null || !IsInventoryOpen) return false;

        bagMessageText.text = text;
        bagMessage.SetActive(true);
        bagMessage.transform.SetAsLastSibling();
        if (bagMessageGroup != null) bagMessageGroup.alpha = 1f;
        if (bagMessageRoutine != null) StopCoroutine(bagMessageRoutine);
        bagMessageRoutine = StartCoroutine(HideBagMessage());
        return true;
    }

    private IEnumerator HideBagMessage()
    {
        float t = 0f;
        while (t < 2.5f) { t += Time.unscaledDeltaTime; yield return null; }

        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime;
            if (bagMessageGroup != null) bagMessageGroup.alpha = 1f - Mathf.Clamp01(t);
            yield return null;
        }
        bagMessage.SetActive(false);
        bagMessageRoutine = null;
    }

    // 가방을 닫으면 알림도 치운다
    private void HideBagMessageNow()
    {
        if (bagMessageRoutine != null) { StopCoroutine(bagMessageRoutine); bagMessageRoutine = null; }
        if (bagMessage != null) bagMessage.SetActive(false);
    }

    // =========================================================
    //  정렬 버튼: 누를 때마다 [ID순] / [무게순]이 번갈아 적용된다
    // =========================================================
    private bool nextSortByWeight;
    private TextMeshProUGUI sortLabel;

    private void SetupSortButton()
    {
        if (inventoryUI == null) return;

        RectTransform craft = craftRecipeButton != null
            ? craftRecipeButton.GetComponent<RectTransform>()
            : inventoryUI.transform.Find("CraftRecipeButton") as RectTransform;

        Vector2 anchor = new Vector2(1f, 1f);
        Vector2 pos = new Vector2(-420f, -100f);
        Vector2 size = new Vector2(150f, 100f);
        Vector2 pivot = Vector2.zero;
        if (craft != null)
        {
            anchor = craft.anchorMax;
            pivot = craft.pivot;
            size = craft.sizeDelta;
            pos = craft.anchoredPosition;
        }

        // 제작 버튼 왼쪽으로 [정렬] [장비] [버프] 순서로 나란히
        GameObject sort = MakeBarButton("SortButton", "ID순 정렬", anchor, pivot, pos - new Vector2((size.x + 10f) * 1f, 0f), size, new Color(0.25f, 0.4f, 0.65f), OnClickSort);
        sortLabel = sort.GetComponentInChildren<TextMeshProUGUI>();
        MakeBarButton("EquipButton", "장비창", anchor, pivot, pos - new Vector2((size.x + 10f) * 2f, 0f), size, new Color(0.5f, 0.4f, 0.2f), ToggleEquipPanel);
        MakeBarButton("BuffButton", "버프창", anchor, pivot, pos - new Vector2((size.x + 10f) * 3f, 0f), size, new Color(0.45f, 0.25f, 0.55f), ToggleBuffPanel);
        MakeBarButton("ProficiencyButton", "숙련도창", anchor, pivot, pos - new Vector2((size.x + 10f) * 4f, 0f), size, new Color(0.2f, 0.5f, 0.45f), ToggleProficiencyPanel);
    }

    private GameObject MakeBarButton(string name, string label, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = CraftQuantityPopup.CreateButton(inventoryUI.transform, name, label, FindUIFont(), color, anchor, pos, size, onClick);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        TextMeshProUGUI t = go.GetComponentInChildren<TextMeshProUGUI>();
        if (t != null) t.fontSize = 26;
        return go;
    }

    private void OnClickSort()
    {
        SoundManager.Instance?.PlaySlotClickSound();
        SortItems(nextSortByWeight);
        nextSortByWeight = !nextSortByWeight;
        if (sortLabel != null) sortLabel.text = nextSortByWeight ? "무게순 정렬" : "ID순 정렬";
    }

    // 슬롯 순서를 정렬한다. byWeight: 슬롯 전체 무게(개당 무게 x 수량)가 무거운 순, 아니면 ID 순.
    // 정렬 뒤에 얻는 아이템은 여전히 맨 뒤(빈 곳)에 차례로 들어간다.
    public void SortItems(bool byWeight)
    {
        List<ItemStack> sorted = byWeight
            ? itemList.OrderByDescending(s => s.itemData.weight * s.amount).ThenBy(s => s.itemData.id).ToList()
            : itemList.OrderBy(s => s.itemData.id).ThenByDescending(s => s.amount).ToList();
        itemList.Clear();
        itemList.AddRange(sorted);
        RefreshInventoryUI();
    }

    // =========================================================
    //  슬롯 끌어다 놓기: 같은 아이템이면 합치고, 다른 아이템이면 자리를 바꾼다
    // =========================================================
    public void DropOnto(ItemStack from, ItemStack to)
    {
        if (from == null || to == null || from == to) return;

        bool mergeable = from.itemData.id == to.itemData.id
            && from.itemData.durabilitymax <= 0 // 내구도가 따로인 도구/장비는 합치지 않음
            && !IsEquipped(from) && !IsEquipped(to)
            && to.amount < StackLimit(to.itemData);

        if (mergeable)
        {
            int move = Mathf.Min(from.amount, StackLimit(to.itemData) - to.amount);
            to.amount += move;
            from.amount -= move;
            if (from.amount <= 0)
            {
                itemList.Remove(from);
            }
            RefreshInventoryUI();
            GameManager.Weight = CurrentWeight;
        RecalcEquipment();
            PlayerUI.RefreshAll();
            return;
        }

        // 합칠 수 없으면 자리 바꾸기
        int a = itemList.IndexOf(from), b = itemList.IndexOf(to);
        if (a < 0 || b < 0) return;
        itemList[a] = to;
        itemList[b] = from;
        RefreshInventoryUI();
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
        ItemGainToast.ClearAll(); // 필드에서 얻고 남아 있던 획득 표시는 가방을 열면 바로 치운다
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
        HideBagMessageNow();
        MarkKeyboardSelection(null);
        if (inventoryUI != null)
        {
            Transform rp = inventoryUI.transform.Find("RepairPopup");
            if (rp != null) Destroy(rp.gameObject);
        }
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
        CloseSidePanels(); // 장비창/버프창과 같은 자리
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
        slot.SetEquippedMark(IsEquipped(stack));
        if (stack == kbSelected) slot.SetSelected(true);
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
    private ItemStack lastToolStack;

    // 방금 쓴 도구 1개를 없앤다 (도구가 결과 아이템으로 바뀔 때: 빈 물통 -> 물을 담은 물통)
    public void RemoveLastUsedTool()
    {
        if (lastToolStack != null && itemList.Contains(lastToolStack)) RemoveFromStack(lastToolStack, 1);
        lastToolStack = null;
    }

    public Item ConsumeToolDurability(ToolType requiredType, int requiredTier, out ToolCheckResult checkResult, out bool isBroken)
    {
        isBroken = false;
        LastToolDurability = 0;
        lastToolStack = null;

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
        lastToolStack = targetStack;
        if (IsUnbreakable(targetStack)) { LastToolDurability = UnbreakableDurability; return usedTool; } // 닳지 않는 도구는 내구도를 깎지 않는다
        targetStack.durability -= 1;
        LastToolDurability = Mathf.Max(0, targetStack.durability);
        GainWearExp(targetStack, 1);
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
        // (복사본으로 바꾸면 장착 기록과 등급/경험치가 끊기므로, 원래 스택의 값만 기억해 둔다)
        var saved = itemList.Select(s => new { stack = s, amount = s.amount, durability = s.durability }).ToList();

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
            itemList = saved.Select(x => { x.stack.amount = x.amount; x.stack.durability = x.durability; return x.stack; }).ToList();
            RefreshInventoryUI();
            GameManager.Weight = CurrentWeight;
        RecalcEquipment();
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
                    GainWearExp(s, take);
                    remaining -= take;
                    if (remaining <= 0) break;
                }
            }
        }

        // 4. 완성품 추가
        AddItem(result, total);

        // 제작했으니 제작 숙련도와 플레이어 레벨 경험치를 얻는다
        string craftReward = Proficiency.Reward(ProficiencyKind.Crafting);
        if (craftReward.Length > 0) ShowBagMessageOrToast(craftReward);
        return true;
    }

    // =========================================================
    //  장비 장착 / 특성 / 버프 / 등급 / 수리
    //  장착한 아이템은 가방(itemList)에 그대로 있고, 어느 부위에 끼웠는지만 따로 기억한다.
    //  장착해야 능력치와 특성이 적용된다. 가방에 든 도구(낫, 도끼 등)는 장착하지 않아도 가장 높은 공격력 하나가 적용된다.
    //  무기는 두 개까지, 나머지 부위(머리/몸/다리/장신구/방패)는 하나씩 장착할 수 있다.
    // =========================================================
    private readonly Dictionary<EquipSlot, List<ItemStack>> equipped = new Dictionary<EquipSlot, List<ItemStack>>();
    private int appliedSpBonus, appliedManaBonus;   // 지금 GameManager에 더해 둔 장비 SP/마나 보너스
    private TextMeshProUGUI equipBody, buffBody;     // 장비창 / 버프창 본문 글자 (BagUI 프리팹의 EquipPanel, BuffPanel)
    private GameObject equipPanel, buffPanel;
    private GameObject profPanel;                    // BagUI 프리팹의 ProficiencyPanel
    private TextMeshProUGUI profBody;

    public static bool CanEquip(Item item) { return item != null && item.equipSlot != EquipSlot.None; }

    // 부위별로 한 번에 낄 수 있는 개수 (무기만 2개)
    public static int SlotCapacity(EquipSlot slot) { return slot == EquipSlot.Weapon ? 2 : 1; }

    public bool IsEquipped(ItemStack s)
    {
        if (s == null) return false;
        foreach (List<ItemStack> list in equipped.Values) if (list.Contains(s)) return true;
        return false;
    }

    // 그 부위에 장착한 스택들 (없으면 빈 목록)
    public List<ItemStack> EquippedIn(EquipSlot slot)
    {
        return equipped.TryGetValue(slot, out List<ItemStack> list) ? list : new List<ItemStack>();
    }

    private IEnumerable<ItemStack> AllEquipped()
    {
        foreach (List<ItemStack> list in equipped.Values)
            foreach (ItemStack s in list) yield return s;
    }

    // 부서진(내구도 0인) 장비는 능력치가 적용되지 않는다
    private static bool IsWorking(ItemStack s) { return s != null && (s.itemData.durabilitymax <= 0 || s.durability > 0); }

    public bool Equip(ItemStack stack, out string message)
    {
        message = "";
        if (stack == null || !CanEquip(stack.itemData)) { message = "장착할 수 없는 아이템입니다."; return false; }
        if (!IsWorking(stack)) { message = "파괴되어 장착할 수 없습니다. 먼저 수리하세요."; return false; }
        if (IsEquipped(stack)) return false;

        EquipSlot slot = stack.itemData.equipSlot;
        if (!equipped.TryGetValue(slot, out List<ItemStack> list)) { list = new List<ItemStack>(); equipped[slot] = list; }

        // 자리가 없으면 가장 먼저 낀 것을 벗긴다
        ItemStack old = null;
        if (list.Count >= SlotCapacity(slot)) { old = list[0]; list.RemoveAt(0); }
        list.Add(stack);
        RecalcEquipment();

        message = old != null
            ? $"{Josa.WithEul(stack.itemData.itemName)} 장착했다. ({old.itemData.itemName} 해제)"
            : $"{Josa.WithEul(stack.itemData.itemName)} 장착했다.";
        return true;
    }

    public bool Unequip(ItemStack stack, out string message)
    {
        message = "";
        if (!IsEquipped(stack)) return false;
        RemoveEquippedRecord(stack);
        RecalcEquipment();
        message = $"{Josa.WithEul(stack.itemData.itemName)} 해제했다.";
        return true;
    }

    // 스택이 가방에서 사라질 때 장착 기록도 지운다
    private void RemoveEquippedRecord(ItemStack stack)
    {
        foreach (List<ItemStack> list in equipped.Values) list.Remove(stack);
    }

    // ---- 등급 ----
    public static int GradeOf(ItemStack s) { return s != null ? Mathf.Max(1, s.grade) : 1; }

    // 등급에 따른 능력치 배율 (등급 표의 능력치 보너스 %)
    private static float GradeMult(ItemStack s)
    {
        GradeDef g = GameTables.Grades.Get(GradeOf(s));
        return g != null ? 1f + g.statBonusPercent / 100f : 1f;
    }

    // 내구도를 lost만큼 쓴 만큼 장비 경험치를 쌓고, 경험치가 차면 등급이 오른다. 올랐으면 알림 문구를 돌려준다.
    public string GainWearExp(ItemStack s, int lost)
    {
        if (s == null || lost <= 0 || s.itemData.wearExp <= 0) return "";
        GradeTable table = GameTables.Grades;

        s.grade = GradeOf(s);
        s.exp += s.itemData.wearExp * lost;

        string up = "";
        while (s.grade < table.MaxTier)
        {
            GradeDef g = table.Get(s.grade);
            if (g == null || g.expToNext <= 0 || s.exp < g.expToNext) break;
            s.exp -= g.expToNext;
            s.grade++;
            up = $"{Josa.WithEun(s.itemData.itemName)} {table.NameOf(s.grade)} 등급이 되었다!";
        }
        if (up.Length > 0)
        {
            RecalcEquipment();
            ShowBagMessageOrToast(up);
        }
        return up;
    }

    // 정보창에 쓰는 한 줄: "Rare (Tier 2)   EXP 12 / 300"
    public string GradeLine(ItemStack s)
    {
        if (s == null) return "";
        GradeTable table = GameTables.Grades;
        int tier = GradeOf(s);
        GradeDef g = table.Get(tier);
        string name = $"{table.NameOf(tier)} (Tier {tier})";
        if (g == null || g.expToNext <= 0 || tier >= table.MaxTier) return $"{name}   EXP {s.exp} (MAX)";
        return $"{name}   EXP {s.exp} / {g.expToNext}";
    }

    // 독 같은 디버프에 대한 내성 [%] = 회복 숙련도 레벨의 보너스
    public static float PoisonResist()
    {
        return Mathf.Clamp(Proficiency.Bonus(ProficiencyKind.Recovery), 0f, 100f);
    }

    // 특성의 방어력 증감 [%]. 내성이 있으면 디버프(마이너스)가 그만큼 줄어든다
    private static float EffectiveDfRate(TraitDef t)
    {
        if (t.dfRate < 0f && t.resistable) return t.dfRate * (1f - PoisonResist() / 100f);
        return t.dfRate;
    }

    // 지금 적용 중인 특성들: 장착한 (멀쩡한) 장비의 특성 + 기본 능력치 표의 고유 특성
    public struct TraitSource
    {
        public TraitDef trait;
        public string source;   // 어디서 온 특성인가 (장비 이름 / 고유)
    }

    public List<TraitSource> ActiveTraits()
    {
        List<TraitSource> result = new List<TraitSource>();
        foreach (ItemStack s in AllEquipped())
        {
            if (!IsWorking(s)) continue;
            TraitDef t = GameTables.Traits.Get(s.itemData.traitId);
            if (t != null) result.Add(new TraitSource { trait = t, source = s.itemData.itemName });
        }
        TraitDef innate = GameTables.Traits.Get(GameTables.PlayerBase.innateTraitId);
        if (innate != null) result.Add(new TraitSource { trait = innate, source = "고유 특성" });
        return result;
    }

    // 장착한 장비와 가방의 도구를 모아 플레이어의 장비 능력치를 다시 계산한다 (가방/장착이 바뀔 때마다 부름)
    public void RecalcEquipment()
    {
        int at = 0, df = 0, hp = 0, fixAt = 0, slotB = 0, weightB = 0, sp = 0, mana = 0;
        float atRate = 0f, dfRate = 0f, hpRate = 0f, hpRateAt = 0f, breakDf = 0f, abs = 0f, avoid = 0f;
        float crit = 0f, critRate = 0f, goldRate = 0f, expRate = 0f;

        foreach (ItemStack s in AllEquipped())
        {
            if (!IsWorking(s)) continue;
            Item it = s.itemData;
            float m = GradeMult(s); // 등급이 높을수록 기본 능력치가 늘어남
            at += Mathf.RoundToInt(it.at * m); df += Mathf.RoundToInt(it.df * m); hp += Mathf.RoundToInt(it.hp * m);
            fixAt += Mathf.RoundToInt(it.fixat * m);
            slotB += it.slotmax; weightB += it.weightmax;
            sp += it.spmax; mana += it.manamax;
            atRate += it.atrate; dfRate += it.dfrate; hpRate += it.hprate; hpRateAt += it.hprateat;
            breakDf += it.breakdf; abs += it.abs; avoid += it.avoid; crit += it.critical; critRate += it.criticalrate;
            goldRate += it.goldrate; expRate += it.exprate;
        }

        foreach (TraitSource ts in ActiveTraits())
        {
            atRate += ts.trait.atRate;
            dfRate += EffectiveDfRate(ts.trait);
        }

        // 가방의 도구 중 가장 높은 공격력 하나 (장착한 것은 위에서 이미 더함)
        int toolAt = 0;
        foreach (ItemStack s in itemList)
            if (s.itemData.toolType != ToolType.None && !IsEquipped(s) && IsUsableTool(s))
                toolAt = Mathf.Max(toolAt, Mathf.RoundToInt(s.itemData.at * GradeMult(s)));

        GameManager.EquipAt = at + toolAt;
        GameManager.EquipDf = df;
        GameManager.EquipHp = hp;
        GameManager.FixAt = fixAt;
        GameManager.SlotBonus = slotB;
        GameManager.WeightBonus = weightB;
        GameManager.AtRate = atRate;
        GameManager.DfRate = dfRate;
        GameManager.HpRate = hpRate;
        GameManager.HpRateAt = hpRateAt;
        GameManager.BreakDf = breakDf;
        GameManager.Abs = abs;
        GameManager.Avoid = avoid;
        GameManager.Critical = crit;
        GameManager.CriticalRate = critRate;
        GameManager.GoldR = goldRate;
        GameManager.ExpR = Mathf.RoundToInt(expRate);

        // 최대 SP/마나는 기본값 위에 장비 보너스를 얹는다 (바뀐 만큼만 더하고 뺌)
        GameManager.SPMax += sp - appliedSpBonus;
        appliedSpBonus = sp;
        GameManager.ManaMax += mana - appliedManaBonus;
        appliedManaBonus = mana;
        if (GameManager.SP > GameManager.SPMax) GameManager.SP = GameManager.SPMax;
        if (GameManager.Mana > GameManager.ManaMax) GameManager.Mana = GameManager.ManaMax;
        int maxHp = BattleCalc.PlayerMaxHp();
        if (GameManager.Hp > maxHp) GameManager.Hp = maxHp;

        RefreshEquipPanels();
        RefreshAllSlotMarks();
        PlayerUI.RefreshAll();
    }

    // ---- 장비창 / 버프창 (ItemInfoPanel, StatPanel과 같은 크기와 자리) ----
    private void SetupEquipPanels()
    {
        if (inventoryUI == null) return;
        Transform e = inventoryUI.transform.Find("EquipPanel");
        Transform b = inventoryUI.transform.Find("BuffPanel");
        if (e != null) { equipPanel = e.gameObject; Transform body = e.Find("Body"); equipBody = body != null ? body.GetComponent<TextMeshProUGUI>() : null; }
        if (b != null) { buffPanel = b.gameObject; Transform body = b.Find("Body"); buffBody = body != null ? body.GetComponent<TextMeshProUGUI>() : null; }
        Transform p = inventoryUI.transform.Find("ProficiencyPanel");
        if (p != null) { profPanel = p.gameObject; Transform body = p.Find("Body"); profBody = body != null ? body.GetComponent<TextMeshProUGUI>() : null; }
        if (equipPanel != null) equipPanel.SetActive(false);
        if (buffPanel != null) buffPanel.SetActive(false);
        if (profPanel != null) profPanel.SetActive(false);
    }

    // 정보창/스탯창과 같은 자리를 쓰므로, 하나가 열리면 나머지는 닫는다
    public void CloseSidePanels()
    {
        if (equipPanel != null) equipPanel.SetActive(false);
        if (buffPanel != null) buffPanel.SetActive(false);
        if (profPanel != null) profPanel.SetActive(false);
    }

    public void ToggleEquipPanel()
    {
        if (equipPanel == null) return;
        SoundManager.Instance?.PlaySlotClickSound();
        bool on = !equipPanel.activeSelf;
        CloseSidePanels();
        if (!on) return;
        HideInfoPanel();
        if (StatUI != null) StatUI.SetActive(false);
        equipPanel.SetActive(true);
        equipPanel.transform.SetAsLastSibling();
        RefreshEquipPanels();
    }

    public void ToggleBuffPanel()
    {
        if (buffPanel == null) return;
        SoundManager.Instance?.PlaySlotClickSound();
        bool on = !buffPanel.activeSelf;
        CloseSidePanels();
        if (!on) return;
        HideInfoPanel();
        if (StatUI != null) StatUI.SetActive(false);
        buffPanel.SetActive(true);
        buffPanel.transform.SetAsLastSibling();
        RefreshEquipPanels();
    }

    public void ToggleProficiencyPanel()
    {
        if (profPanel == null) return;
        SoundManager.Instance?.PlaySlotClickSound();
        bool on = !profPanel.activeSelf;
        CloseSidePanels();
        if (!on) return;
        HideInfoPanel();
        if (StatUI != null) StatUI.SetActive(false);
        profPanel.SetActive(true);
        profPanel.transform.SetAsLastSibling();
        RefreshEquipPanels();
    }

    // 숙련도 목록: 이름, 레벨, 경험치, 그 레벨의 보너스
    public string ProficiencyText()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int need = LevelSystem.ExpToNext(GameManager.Level);
        sb.AppendLine($"<b>플레이어 Lv.{GameManager.Level}</b>   EXP {GameManager.Exp}" + (need > 0 ? $" / {need}" : " (MAX)"));
        sb.AppendLine();

        foreach (ProficiencyDef def in GameTables.Proficiency.defs)
        {
            if (def == null) continue;
            int lv = Proficiency.Level(def);
            int next = lv >= 1 && lv <= def.levels.Count ? def.levels[lv - 1].expToNext : 0;
            string exp = next > 0 ? $"{Proficiency.Exp(def)} / {next}" : $"{Proficiency.Exp(def)} (MAX)";
            sb.AppendLine($"<b>{def.label}</b>  Lv.{lv}   EXP {exp}");

            ProficiencyLevel l = Proficiency.Current(def);
            if (l == null) continue;
            List<string> bonus = new List<string>();
            if (l.bonusPercent != 0f) bonus.Add(def.kind == ProficiencyKind.Recovery ? $"독 내성 {l.bonusPercent:0.#}%" : $"수확 +{l.bonusPercent:0.#}%");
            if (l.extraMax > 0) bonus.Add($"수량 +{Mathf.Min(l.extraMin, l.extraMax)}~{Mathf.Max(l.extraMin, l.extraMax)}");
            if (l.expBonusPercent != 0f) bonus.Add($"레벨 경험치 +{l.expBonusPercent:0.#}%");
            if (l.spReducePercent != 0f) bonus.Add($"SP -{l.spReducePercent:0.#}%");
            if (bonus.Count > 0) sb.AppendLine("   <size=75%>" + string.Join(" / ", bonus) + "</size>");
        }
        return sb.ToString();
    }

    private static readonly EquipSlot[] SlotOrder =
        { EquipSlot.Weapon, EquipSlot.Shield, EquipSlot.Head, EquipSlot.Body, EquipSlot.Legs, EquipSlot.Accessory };

    public void RefreshEquipPanels()
    {
        if (equipBody != null && equipPanel != null && equipPanel.activeSelf)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (EquipSlot slot in SlotOrder)
            {
                List<ItemStack> list = EquippedIn(slot);
                int cap = SlotCapacity(slot);
                for (int i = 0; i < cap; i++)
                {
                    string label = cap > 1 ? $"{EquipSlotInfo.Name(slot)} {i + 1}" : EquipSlotInfo.Name(slot);
                    if (i >= list.Count) { sb.AppendLine($"{label} : -"); continue; }

                    ItemStack s = list[i];
                    string dur = s.itemData.durabilitymax > 0
                        ? (s.durability > 0 ? $"  ({s.durability}/{s.itemData.durabilitymax})" : "  <color=#C00000>(파괴됨)</color>")
                        : "";
                    TraitDef t = GameTables.Traits.Get(s.itemData.traitId);
                    string trait = t != null ? $"  [{t.label}]" : "";
                    sb.AppendLine($"{label} : {s.itemData.itemName}{dur}{trait}");
                    sb.AppendLine($"   <size=75%>{GameTables.Grades.NameOf(GradeOf(s))}</size>");
                }
            }
            sb.AppendLine();
            sb.AppendLine($"공격력 +{GameManager.EquipAt}");
            sb.AppendLine($"방어력 +{GameManager.EquipDf}");
            sb.AppendLine($"체력 +{GameManager.EquipHp}");
            sb.AppendLine($"고정 공격력 +{GameManager.FixAt}");
            sb.AppendLine();
            sb.AppendLine("<size=75%>가방에서 아이템을 눌러 [장착] / [해제]</size>");
            equipBody.text = sb.ToString();
        }

        if (buffBody != null && buffPanel != null && buffPanel.activeSelf)
            buffBody.text = BuffText();

        if (profBody != null && profPanel != null && profPanel.activeSelf)
            profBody.text = ProficiencyText();
    }

    // 버프/디버프 내역: 적용 중인 특성과 독 내성
    public string BuffText()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        const string good = "<color=#1B7F2A>", bad = "<color=#C00000>", end = "</color>";
        float resist = PoisonResist();
        int lines = 0;

        foreach (TraitSource ts in ActiveTraits())
        {
            TraitDef t = ts.trait;
            sb.AppendLine($"<b>{t.label}</b> ({ts.source})");
            if (t.atRate != 0f) { sb.AppendLine($"  {(t.atRate > 0 ? good : bad)}공격력 {t.atRate:+0.#;-0.#}%{end}"); lines++; }
            float df = EffectiveDfRate(t);
            if (t.dfRate != 0f)
            {
                string note = t.resistable && t.dfRate < 0f ? $" (내성 {resist:0.#}%로 {t.dfRate:+0.#;-0.#}% -> {df:+0.#;-0.#}%)" : "";
                sb.AppendLine($"  {(df > 0 ? good : bad)}방어력 {df:+0.#;-0.#}%{end}{note}");
                lines++;
            }
            if (t.inflictChance > 0f && t.dotDamage > 0f)
            {
                sb.AppendLine($"  공격 시 {t.inflictChance:0.#}% 확률로 {t.label} 부여 (매 턴 {t.dotDamage:0.#} +{t.dotGrowth:0.#}씩, {t.dotTurns}턴)");
                lines++;
            }
            if (!string.IsNullOrEmpty(t.description)) sb.AppendLine($"  <size=75%>{t.description}</size>");
        }
        if (lines == 0) sb.AppendLine("적용 중인 버프/디버프가 없습니다.");

        ProficiencyDef rec = GameTables.Proficiency.Get(ProficiencyKind.Recovery);
        if (rec != null)
        {
            sb.AppendLine();
            sb.AppendLine($"{good}독 내성 {resist:0.#}%{end}  ({rec.label} 숙련도 Lv.{Proficiency.Level(rec)})");
        }
        return sb.ToString();
    }

    // 내구도를 1 깎는다. 공격하면 장착한 무기 전부, 맞으면 장착한 방어구(머리/몸/다리/방패) 전부가 닳는다.
    // 0이 되면 파괴되어 능력치가 사라지고, 정보창의 [수리]로 고칠 때까지 쓸 수 없다. 닳을 때마다 장비 경험치가 쌓인다.
    public string WearEquipment(bool attacked)
    {
        List<ItemStack> targets = new List<ItemStack>();
        if (attacked) targets.AddRange(EquippedIn(EquipSlot.Weapon));
        else
        {
            targets.AddRange(EquippedIn(EquipSlot.Head));
            targets.AddRange(EquippedIn(EquipSlot.Body));
            targets.AddRange(EquippedIn(EquipSlot.Legs));
            targets.AddRange(EquippedIn(EquipSlot.Shield));
        }

        bool changed = false;
        List<string> worn = new List<string>();
        foreach (ItemStack s in targets.ToArray())
        {
            if (s.itemData.durabilitymax <= 0 || s.durability <= 0) continue;
            s.durability -= 1;
            changed = true;
            worn.Add($"{s.itemData.itemName} {s.durability}/{s.itemData.durabilitymax}");
            GainWearExp(s, 1);
            if (s.durability <= 0)
            {
                s.durability = 0;
                ItemGainToast.ShowBroken(s.itemData, "장비가 파괴되었다!");
            }
        }
        if (changed) RecalcEquipment();
        return worn.Count > 0 ? "\n<size=75%>내구도: " + string.Join(", ", worn) + "</size>" : "";
    }

    // ---- 수리 ----
    public static bool NeedsRepair(ItemStack s)
    {
        return s != null && s.itemData.durabilitymax > 0 && s.durability <= 0;
    }

    // 수리 후 내구도
    public static int RepairedDurability(Item it)
    {
        return it.repairRestore > 0 ? Mathf.Min(it.durabilitymax, it.repairRestore) : it.durabilitymax;
    }

    // 재료를 내고 내구도를 채운다. 재료가 모자라면 실패
    public bool TryRepair(ItemStack s, out string message)
    {
        message = "";
        if (!NeedsRepair(s)) return false;
        Item it = s.itemData;

        List<Ingredient> materials = it.RepairMaterials();
        foreach (Ingredient ing in materials)
        {
            if (GetItemCount(ing.itemId) < ing.amount) { message = "수리 재료가 부족합니다."; return false; }
        }
        foreach (Ingredient ing in materials) RemoveItem(ing.itemId, ing.amount);

        s.durability = RepairedDurability(it);
        RecalcEquipment();
        message = $"{Josa.WithEul(it.itemName)} 수리했다. (내구도 {s.durability}/{it.durabilitymax})";
        return true;
    }

    // 슬롯에 "장착" 표시를 다시 맞춘다
    private void RefreshAllSlotMarks()
    {
        foreach (KeyValuePair<ItemStack, ItemSlot> kv in dynamicSlots)
            if (kv.Value != null) kv.Value.SetEquippedMark(IsEquipped(kv.Key));
    }

    // 마을처럼 필드가 없는 곳에서 기절했을 때: 체력을 채우고 페널티를 적용한다
    public string RecoverFromFaint()
    {
        GameManager.Hp = BattleCalc.PlayerMaxHp();
        GameManager.isfaint = false;
        string penalty = ApplyFaintPenalty();
        PlayerUI.RefreshAll();
        return string.IsNullOrEmpty(penalty) ? "병원에서 치료를 받았다." : "병원에서 치료를 받았다. " + penalty + ".";
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
    // 방금 사용으로 체력이 0이 되어 쓰러졌는가 (호출한 쪽이 기절 처리를 한다)
    public bool LastUseFainted { get; private set; }

    public bool UseItem(ItemStack stack, out string message)
    {
        message = "";
        LastUseFainted = false;
        if (stack == null || stack.itemData == null || !CanUse(stack.itemData)) return false;
        Item item = stack.itemData;

        int hpBefore = GameManager.Hp;
        int spBefore = GameManager.SP;
        int manaBefore = GameManager.Mana;

        GameManager.Hp = Mathf.Clamp(GameManager.Hp + item.useHp, 0, Mathf.Max(BattleCalc.PlayerMaxHp(), GameManager.Hp)); // 체력을 깎는 아이템은 쓰러질 수도 있다
        GameManager.SP = Mathf.Clamp(GameManager.SP + item.useSp, 0, Mathf.Max(GameManager.SPMax, GameManager.SP));
        GameManager.Mana = Mathf.Clamp(GameManager.Mana + item.useMana, 0, Mathf.Max(GameManager.ManaMax, GameManager.Mana));

        int hp = GameManager.Hp - hpBefore;
        int sp = GameManager.SP - spBefore;
        int mana = GameManager.Mana - manaBefore;

        // 이미 전부 가득 차서 아무 효과가 없으면 소모하지 않는다 (마이너스 효과가 있는 아이템은 항상 먹을 수 있음)
        bool harmful = item.useHp < 0 || item.useSp < 0 || item.useMana < 0;
        if (harmful) ScreenFlash.Shared.Play(new Color(0.85f, 0f, 0f), 0.45f, 0.8f); // 위험한 것을 먹으면 맞았을 때처럼 화면이 붉어짐
        if (!harmful && hp == 0 && sp == 0 && mana == 0)
        {
            message = "이미 가득 차서 사용할 필요가 없습니다.";
            return false;
        }

        // 사용 후 남는 아이템(예: 가득찬 물통 -> 물통)은 내구도를 이어받는다. 내구도가 0이면 파손되어 남지 않는다.
        Item leftover = item.useResultItemId > 0 ? GetItemData(item.useResultItemId) : null;
        // 이어받을 내구도가 있으면(물을 담을 때 물려받은 값) 그대로 넘기고, 0이면 파손, 아예 없으면 새 것
        int carry = -1;
        bool broken = false;
        if (leftover != null)
        {
            if (stack.durability > 0) carry = stack.durability;
            else if (item.durabilitymax > 0) broken = true;
            else if (leftover.durabilitymax > 0) carry = leftover.durabilitymax;
        }

        RemoveFromStack(stack, 1); // 1개 소모 (0개가 되면 슬롯이 사라짐)

        List<string> effects = new List<string>();
        if (hp != 0) effects.Add($"체력 {hp:+#;-#}");
        if (sp != 0) effects.Add($"SP {sp:+#;-#}");
        if (mana != 0) effects.Add($"마나 {mana:+#;-#}");
        message = $"{item.itemName} 사용" + (effects.Count > 0 ? $": {string.Join(", ", effects)}" : "");

        // 아이템을 쓰면 [회복] 숙련도와 플레이어 레벨 경험치를 함께 얻는다 (독 같은 위험한 것도 마찬가지).
        // 회복 숙련도 레벨의 보너스(%)가 독 내성이다.
        float resistBefore = PoisonResist();
        string recoveryReward = Proficiency.Reward(ProficiencyKind.Recovery);
        if (recoveryReward.Length > 0) message += "\n" + recoveryReward;
        if (!Mathf.Approximately(resistBefore, PoisonResist()))
        {
            message += $" (독 내성 {PoisonResist():0.#}%)";
            RecalcEquipment(); // 내성이 바뀌면 장비 특성의 디버프도 달라짐
        }
        if (GameManager.Hp <= 0)
        {
            LastUseFainted = true;
            message += "\n눈앞이 캄캄해졌다...";
        }

        if (leftover != null)
        {
            if (broken)
                message += $"\n{Josa.WithIga(leftover.itemName)} 파괴되었습니다!";
            else if (AddItem(leftover, 1, carry) < 1)
                message += $"\n가방이 가득 차서 {Josa.WithEul(leftover.itemName)} 버렸다.";
        }
        else if (item.useResultItemId > 0)
        {
            message += $"\n(사용 후 남는 아이템 ID {item.useResultItemId}를 도감에서 찾지 못했다)";
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
        part.grade = stack.grade;
        part.exp = stack.exp;
        itemList.Insert(itemList.IndexOf(stack) + 1, part);

        RefreshInventoryUI(); // 순서대로 슬롯을 다시 만든다
        GameManager.Weight = CurrentWeight;
        RecalcEquipment();
        PlayerUI.RefreshAll();
        message = $"{Josa.WithEul(stack.itemData.itemName)} {stack.amount}개와 {half}개로 나눴다.";
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
                if (IsEquipped(s)) continue;

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
        bool keepSeparate = durability >= 0;

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
            if (keepSeparate) stack.durability = newItem.durabilitymax > 0 ? Mathf.Min(durability, newItem.durabilitymax) : durability;
            itemList.Add(stack);
            CreateSlot(stack);
            amount -= add;
        }

        GameManager.Weight = CurrentWeight;
        RecalcEquipment();
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
            RemoveEquippedRecord(stack);
        }
        else
        {
            RefreshSlot(stack);
        }

        GameManager.Weight = CurrentWeight;
        RecalcEquipment();
        PlayerUI.RefreshAll();
    }
}
[System.Serializable]
public class ItemStack
{
    public Item itemData;  // ScriptableObject 기반 아이템 데이터 (읽기만 할 것, 수정 금지)
    public int amount;     // 소지 수량
    public int durability; // 현재 내구도 (도구 스택 전체가 공유)
    public int grade = 1;  // 장비 등급 번호 (Tier). 등급 표 참고
    public int exp;        // 현재 등급에서 쌓은 장비 경험치

    public ItemStack(Item data, int amount)
    {
        this.itemData = data;
        this.amount = amount;
        this.durability = data != null ? data.durabilitymax : 0;
    }
}