using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ItemSlot : MonoBehaviour, IPointerClickHandler
{
    [Header("슬롯 UI 요소")]
    public Image iconImage;           // 자기 자신 내부의 Icon Image
    public TextMeshProUGUI countText; // 자기 자신 내부의 Count Text

    private Item myItemData;

    // 이 슬롯이 보여 주는 스택 (수량, 현재 내구도). InventoryManager가 슬롯을 만들 때 연결함
    public ItemStack Stack { get; set; }

    /// <summary>
    /// 슬롯 UI 갱신 함수 (아이콘 및 기본 데이터 연동만 담당)
    /// </summary>
    public void SetupSlot(Item item)
    {
        if (item == null)
        {
            SetEmpty();
            return;
        }

        // 아이콘이 없어도 데이터는 연결해야 슬롯을 눌러 정보창을 열 수 있음
        myItemData = item;

        // 1. 아이콘 연동
        if (item.icon != null)
        {
            iconImage.sprite = item.icon;
            iconImage.gameObject.SetActive(true);
            if (iconImage.type == Image.Type.Simple) iconImage.preserveAspect = true;
        }
        else
        {
            Debug.LogWarning($"[ItemSlot] '{item.itemName}'(ID {item.id}) 아이템 에셋에 icon이 없습니다.");
            iconImage.gameObject.SetActive(false);
        }

        // (수량 텍스트 연동은 이제 UpdateCountUI가 전담하므로 여기서 지웠습니다!)
    }

    /// <summary>
    /// 인벤토리 매니저가 아이템 획득/소모 시 수량 텍스트만 갱신할 때 호출
    /// </summary>
    public void UpdateCountUI(int amount)
    {
        if (amount > 1)
        {
            countText.text = amount.ToString();
            countText.gameObject.SetActive(true);
        }
        else
        {
            // 1개 이하면 숫자를 숨김
            countText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// IPointerClickHandler 인터페이스 구현: 슬롯 클릭 시 실행
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 좌클릭이 아니거나 데이터가 없으면 무시
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (myItemData == null) return;

        // 스탯 창이 켜져 있으면 닫고 정보창을 보여 줌
        InventoryManager.Instance?.CloseStat();

        // 정보 패널 열기 (효과음은 패널이 재생)
        ItemInfoPanel panel = ItemInfoPanel.Instance;
        if (panel != null)
        {
            panel.ShowItem(myItemData, Stack);
        }
        else
        {
            Debug.LogError("[ItemSlot] ItemInfoPanel을 찾을 수 없습니다! BagUI 안에 ItemInfoPanel이 있는지 확인하세요.");
        }
    }

    /// <summary>
    /// 빈 슬롯 상태로 초기화
    /// </summary>
    public void SetEmpty()
    {
        myItemData = null;
        if (iconImage != null) iconImage.gameObject.SetActive(false);
        if (countText != null) countText.gameObject.SetActive(false);
    }
}