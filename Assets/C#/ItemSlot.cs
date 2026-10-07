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

    /// <summary>
    /// 슬롯 UI 갱신 함수 (아이콘 및 기본 데이터 연동만 담당)
    /// </summary>
    public void SetupSlot(Item item)
    {
        if (item == null || item.icon == null)
        {
            SetEmpty();
            return;
        }

        myItemData = item;

        // 1. 아이콘 연동
        iconImage.sprite = item.icon;
        iconImage.gameObject.SetActive(true);
        if (iconImage.type == Image.Type.Simple) iconImage.preserveAspect = true;

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
        Debug.Log("<color=yellow>1. 슬롯 물리적 클릭 감지됨!</color>");

        // 1. 좌클릭이 아니거나 데이터가 없으면 무시
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            Debug.Log("<color=red>2. 실패: 좌클릭이 아님</color>");
            return;
        }

        if (myItemData == null)
        {
            Debug.Log("<color=red>2. 실패: myItemData가 비어있음 (빈 슬롯으로 인식됨)</color>");
            return;
        }

        Debug.Log($"<color=green>3. 통과! 아이템 데이터: {myItemData.itemName}</color>");

        // 2. 유효한 아이템 슬롯을 좌클릭했을 때만 효과음 재생
        SoundManager.Instance?.PlaySlotClickSound();

        // 3. 정보 패널 열기 및 데이터 전달
        if (ItemInfoPanel.Instance != null)
        {
            ItemInfoPanel.Instance.OpenInfoPanel(myItemData);
            ItemInfoPanel.Instance.ShowInfo(myItemData);
        }
        else
        {
            Debug.Log("<color=red>4. 실패: ItemInfoPanel.Instance가 NULL임! (싱글톤 없음)</color>");
        }

        // 4. 스탯 창 닫기 (Null 조건부 연산자 '?.' 추가로 안전성 확보)
        InventoryManager.Instance?.CloseStat();
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