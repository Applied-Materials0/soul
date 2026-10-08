using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ItemSlot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
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

    // =========================================================
    //  끌어다 놓기: 다른 슬롯 위에 놓으면 같은 아이템은 합쳐지고, 다른 아이템은 자리가 바뀐다
    // =========================================================
    private GameObject dragGhost;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || myItemData == null || iconImage == null || iconImage.sprite == null) return;

        // 마우스를 따라다니는 아이콘 (가방 UI의 맨 위 캔버스에 그림)
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        ClearGhost();
        dragGhost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        dragGhost.transform.SetParent(canvas.rootCanvas.transform, false);
        dragGhost.GetComponent<CanvasGroup>().blocksRaycasts = false;
        Image img = dragGhost.GetComponent<Image>();
        img.sprite = iconImage.sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0.8f);
        ((RectTransform)dragGhost.transform).sizeDelta = ((RectTransform)iconImage.transform).rect.size;
        dragGhost.transform.SetAsLastSibling();
        dragGhost.transform.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragGhost != null) dragGhost.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ClearGhost();
    }

    // 놓은 뒤 슬롯들이 다시 만들어져 이 슬롯이 사라지거나, 가방이 꺼져도, 따라다니던 아이콘이 남지 않도록 치운다
    private void ClearGhost()
    {
        if (dragGhost != null) Destroy(dragGhost);
        dragGhost = null;
    }

    void OnDisable() { ClearGhost(); }
    void OnDestroy() { ClearGhost(); }

    public void OnDrop(PointerEventData eventData)
    {
        ItemSlot from = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<ItemSlot>() : null;
        if (from != null) from.ClearGhost(); // 놓는 순간 슬롯들이 다시 만들어지므로 먼저 아이콘을 치움
        if (from == null || from == this || InventoryManager.Instance == null) return;
        InventoryManager.Instance.DropOnto(from.Stack, Stack);
    }

    // =========================================================
    //  장착 표시: 장착 중인 아이템은 슬롯 아래쪽에 "장착" 글자가 붙는다
    // =========================================================
    private TextMeshProUGUI equippedMark;

    public void SetEquippedMark(bool on)
    {
        if (equippedMark == null)
        {
            if (!on) return;
            GameObject go = new GameObject("EquippedMark", typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 30f);
            equippedMark = go.AddComponent<TextMeshProUGUI>();
            if (countText != null) equippedMark.font = countText.font;
            equippedMark.fontSize = 22;
            equippedMark.alignment = TextAlignmentOptions.Left;
            equippedMark.color = new Color(0.1f, 0.55f, 0.15f);
            equippedMark.fontStyle = FontStyles.Bold;
            equippedMark.raycastTarget = false;
            equippedMark.text = "장착";
        }
        equippedMark.gameObject.SetActive(on);
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
