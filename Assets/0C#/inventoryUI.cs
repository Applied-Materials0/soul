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

        if (willStatBeActive) HideInfoPanel();
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
    public Item ConsumeToolDurability(ToolType requiredType, int requiredTier, out ToolCheckResult checkResult, out bool isBroken)
    {
        isBroken = false;

        var ownedTools = itemList
            .Where(stack => stack.itemData.toolType == requiredType && stack.durability > 0)
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
        targetStack.durability -= 1;
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

    // 해당 레시피로 최대 몇 개까지 만들 수 있는지 계산
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
        return maxCraft;
    }

    // 실제 제작 실행 (재료 소모 + 아이템 추가)
    public bool TryCraftItem(Recipe recipe, int craftCount)
    {
        return TryCraftItem(GetItemData(recipe.resultItemId), recipe, craftCount);
    }

    // result: 이 레시피로 만들어지는 아이템 (아이템 에셋에 들어 있는 레시피는 그 아이템을 만든다)
    public bool TryCraftItem(Item result, Recipe recipe, int craftCount)
    {
        if (result == null || recipe == null) return false;
        if (craftCount <= 0 || GetMaxCraftableAmount(recipe) < craftCount)
            return false;

        // 1. 재료 소모
        foreach (var ing in recipe.ingredients)
        {
            RemoveItem(ing.itemId, ing.amount * craftCount);
        }

        // 2. 완성품 추가
        AddItem(result, recipe.resultAmount * craftCount);
        return true;
    }

    // 한 슬롯에 쌓을 수 있는 최대 수량 (countmax가 0 이하면 제한 없음)
    private static int StackLimit(Item item)
    {
        return item.countmax > 0 ? item.countmax : int.MaxValue;
    }

    /// <summary>
    /// 아이템 획득 시 호출.
    /// 같은 아이템의 빈자리가 있는 스택(앞쪽 슬롯부터)을 먼저 채우고, 남으면 새 슬롯을 만든다.
    /// 도구/장비(countmax = 1)는 하나마다 슬롯이 따로 생기고 내구도도 각자 가진다.
    /// </summary>
    public void AddItem(Item newItem, int amount = 1)
    {
        if (newItem == null || amount <= 0) return;
        int limit = StackLimit(newItem);

        // 1. 자리가 남은 기존 스택 채우기
        foreach (ItemStack s in itemList)
        {
            if (amount <= 0) break;
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
            itemList.Add(stack);
            CreateSlot(stack);
            amount -= add;
        }

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