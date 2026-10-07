using System.Collections.Generic;
using UnityEngine;
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
            MapManager.Instance.CloseMap();
        }

        if (BtnAudio != null) BtnAudio.Play();
        inventoryUI.SetActive(true);
    }

    public void CloseInventory()
    {
        if (BtnAudio != null) BtnAudio.Play();
        HideInfoPanel(); // 가방을 닫으면 정보창과 제작창도 같이 닫음
        inventoryUI.SetActive(false);
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

        slot.SetupSlot(stack.itemData);
        slot.UpdateCountUI(stack.amount);
        dynamicSlots[stack] = slot;
        return slot;
    }

    /// <summary>
    /// 인벤토리 내 적절한 도구를 자동으로 찾아 내구도를 1 차감합니다.
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
            Debug.Log("해당 도구 없음");
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

            // 같은 도구가 여러 개면 하나만 부서지고 다음 도구가 새 내구도로 이어짐
            if (targetStack.amount > 1)
            {
                RemoveItem(usedTool.id, 1);
                targetStack.durability = usedTool.durabilitymax;
            }
        }
        Debug.Log("내구도 차감");
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

    // 특정 아이템의 스택 반환 (없으면 null)
    public ItemStack FindStack(int itemID)
    {
        return itemList.Find(x => x.itemData.id == itemID);
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
        if (craftCount <= 0 || GetMaxCraftableAmount(recipe) < craftCount)
            return false;

        // 완성품이 도감에 없으면 재료를 쓰기 전에 중단
        Item result = GetItemData(recipe.resultItemId);
        if (result == null) return false;

        // 1. 재료 소모
        foreach (var ing in recipe.ingredients)
        {
            RemoveItem(ing.itemId, ing.amount * craftCount);
        }

        // 2. 완성품 추가
        AddItem(result, recipe.resultAmount * craftCount);
        return true;
    }

    /// <summary>
    /// 아이템 획득 시 호출
    /// </summary>
    public void AddItem(Item newItem, int amount = 1)
    {
        if (newItem == null || amount <= 0) return;

        ItemStack stack = itemList.Find(x => x.itemData.id == newItem.id);

        if (stack != null)
        {
            stack.amount += amount;
        }
        else
        {
            // 신규 아이템: 리스트 맨 뒤에 추가 (= 획득 순서)
            stack = new ItemStack(newItem, amount);
            itemList.Add(stack);
        }

        // 슬롯 UI가 있으면 숫자만 갱신, 없으면(신규이거나 아직 UI가 안 만들어진 스택) 새로 생성
        if (dynamicSlots.TryGetValue(stack, out ItemSlot slot) && slot != null)
        {
            slot.UpdateCountUI(stack.amount);
        }
        else
        {
            CreateSlot(stack);
        }

        Debug.Log($"[AddItem] {newItem.itemName} +{amount} (보유 {stack.amount}개)");
    }

    /// <summary>
    /// 크래프팅/사용 등으로 아이템 삭제 시 호출
    /// </summary>
    public void RemoveItem(int targetItemId, int amount = 1)
    {
        ItemStack existingStack = itemList.Find(x => x.itemData.id == targetItemId);
        if (existingStack == null) return;

        existingStack.amount -= amount;

        if (existingStack.amount <= 0)
        {
            // 리스트에서 제거 및 UI 파괴 (GridLayoutGroup이 자동으로 땡겨줌)
            if (dynamicSlots.TryGetValue(existingStack, out ItemSlot slotScript))
            {
                Destroy(slotScript.gameObject);
                dynamicSlots.Remove(existingStack);
            }
            itemList.Remove(existingStack);
        }
        else
        {
            // 아직 아이템이 남아있다면 깎인 숫자만 UI에 갱신
            if (dynamicSlots.TryGetValue(existingStack, out ItemSlot slotScript))
            {
                slotScript.UpdateCountUI(existingStack.amount);
            }
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