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

        StatUI.SetActive(false);

        // 게임 시작 시 정보창을 인벤토리 UI 밑에 미리 하나 생성해 둠
        if (infoPanelPrefab != null && inventoryUI != null)
        {
            Instantiate(infoPanelPrefab, inventoryUI.transform);
        }
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
        inventoryUI.SetActive(false);
    }

    public void OpenStat()
    {
        if (BtnAudio != null) BtnAudio.Play();
        bool willStatBeActive = !StatUI.activeSelf;
        StatUI.SetActive(willStatBeActive);

        if (ItemInfoPanel.Instance != null)
        {
            ItemInfoPanel.Instance.gameObject.SetActive(!willStatBeActive);
        }
    }

    public void CloseStat()
    {
        if (BtnAudio != null) BtnAudio.Play();
        StatUI.SetActive(false);
    }

    // 인벤토리 정렬 등 전체 새로고침이 필요할 때만 호출
    public void RefreshInventoryUI()
    {
        // 1. 기존 UI 및 딕셔너리 비우기
        foreach (Transform child in contentTransform)
        {
            Destroy(child.gameObject);
        }
        dynamicSlots.Clear();

        // 2. 리스트 순서대로 슬롯 재생성
        foreach (ItemStack stack in itemList)
        {
            GameObject newSlotObj = Instantiate(slotPrefab, contentTransform);
            ItemSlot slotScript = newSlotObj.GetComponent<ItemSlot>();

            if (slotScript != null)
            {
                slotScript.SetupSlot(stack.itemData);
                slotScript.UpdateCountUI(stack.amount); // UI에 수량 표시
                dynamicSlots.Add(stack, slotScript);
            }
        }
    }

    /// <summary>
    /// 인벤토리 내 적절한 도구를 자동으로 찾아 내구도를 1 차감합니다.
    /// </summary>
    public Item ConsumeToolDurability(ToolType requiredType, int requiredTier, out ToolCheckResult checkResult, out bool isBroken)
    {
        isBroken = false;

        var ownedTools = itemList
            .Where(stack => stack.itemData.toolType == requiredType && stack.itemData.durability > 0)
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
        isBroken = targetStack.itemData.UseDurability(1);
        Debug.Log("내구도 차감");
        return targetStack.itemData;
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
            int hasCount = GetItemCount(ing.item.id);
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

        // 1. 재료 소모
        foreach (var ing in recipe.ingredients)
        {
            RemoveItem(ing.item, ing.amount * craftCount);
        }

        // 2. 완성품 추가
        AddItem(recipe.resultItem, recipe.resultAmount * craftCount);
        return true;
    }

    /// <summary>
    /// 아이템 획득 시 호출
    /// </summary>
    public void AddItem(Item newItem, int amount = 1)
    {
        Debug.Log($"[AddItem] 아이템 추가 시도 - ID: {newItem.itemName}, 개수: {amount}");

        ItemStack existingStack = itemList.Find(x => x.itemData == newItem);

        if (existingStack != null)
        {
            existingStack.amount += amount;

            // 기존에 생성되어 있는 UI의 숫자만 변경
            if (dynamicSlots.TryGetValue(existingStack, out ItemSlot slotScript))
            {
                slotScript.UpdateCountUI(existingStack.amount);
            }
            Debug.Log($"[AddItem 성공] 기존 아이템 수량 증가: {newItem.itemName} ({existingStack.amount}개)");
        }
        else
        {
            // 신규 아이템 (리스트 추가 및 새 슬롯 UI 생성)
            ItemStack newStack = new ItemStack(newItem, amount);
            itemList.Add(newStack);
            Debug.Log($"[AddItem 성공] 새 아이템 목록 추가 완료: {newItem.itemName}");

            GameObject slotObj = Instantiate(slotPrefab, contentTransform);
            ItemSlot slotScript = slotObj.GetComponent<ItemSlot>();

            slotScript.SetupSlot(newStack.itemData);
            slotScript.UpdateCountUI(newStack.amount);
            dynamicSlots.Add(newStack, slotScript);
        }
    }

    /// <summary>
    /// 크래프팅/사용 등으로 아이템 삭제 시 호출
    /// </summary>
    public void RemoveItem(Item targetItem, int amount = 1)
    {
        ItemStack existingStack = itemList.Find(x => x.itemData == targetItem);
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
    public Item itemData; // ScriptableObject 기반 아이템 데이터
    public int amount;    // 소지 수량

    public ItemStack(Item data, int amount)
    {
        this.itemData = data;
        this.amount = amount;
    }
}