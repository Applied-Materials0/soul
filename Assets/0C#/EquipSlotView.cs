using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 장비 칸 하나 (장비창과 필드 화면의 장비 표시가 같이 쓴다). 칸의 위치/크기/색은 하이어라키에서 직접 고친다.
// 구조: 이 오브젝트(테두리 Image) - Inner(어두운 바탕) / Icon(아이템 이미지) / Label(비어 있을 때 부위 이름)
// 테두리 색: 보통은 회색, 내구도를 50% 이상 썼으면 주황, 90% 이상 썼으면(또는 파괴됐으면) 빨강.
public class EquipSlotView : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public EquipSlot slot;                 // 장착 부위
    public int index;                      // 같은 부위의 몇 번째 칸인가 (무기만 0, 1)
    public Image border;                   // 테두리 (이 오브젝트의 Image)
    public Image inner;                    // 바탕
    public Image icon;                     // 아이템 이미지
    public TextMeshProUGUI label;          // 비어 있을 때 보이는 부위 이름

    public static readonly Color EmptyBorder = new Color(0.45f, 0.45f, 0.45f, 1f);
    public static readonly Color WornBorder = new Color(1f, 0.55f, 0.1f, 1f);     // 내구도 50% 이상 소모
    public static readonly Color DangerBorder = new Color(0.9f, 0.1f, 0.1f, 1f);  // 내구도 90% 이상 소모 / 파괴
    private static readonly Color InnerNormal = new Color(0.12f, 0.12f, 0.12f, 0.85f);
    private static readonly Color InnerSelected = new Color(0.55f, 0.5f, 0.2f, 0.95f);

    private EquipmentSlots owner;
    private bool interactive;

    public ItemStack Stack { get; private set; }
    public RectTransform Rect { get { return (RectTransform)transform; } }

    public void Init(EquipmentSlots owner, bool interactive)
    {
        this.owner = owner;
        this.interactive = interactive;
        if (border != null) border.raycastTarget = interactive; // 필드의 표시용 칸은 클릭/호버를 받지 않는다
    }

    public void Apply(ItemStack stack, bool selected)
    {
        Stack = stack;
        Item it = stack != null ? stack.itemData : null;

        if (icon != null)
        {
            icon.sprite = it != null ? it.icon : null;
            icon.enabled = it != null && it.icon != null;
            icon.raycastTarget = false;
        }
        if (label != null)
        {
            label.gameObject.SetActive(it == null || it.icon == null);
            label.text = it != null ? it.itemName : EquipSlotInfo.Name(slot);
            label.raycastTarget = false;
        }
        if (inner != null)
        {
            inner.color = selected ? InnerSelected : InnerNormal;
            inner.raycastTarget = false;
        }
        if (border != null) border.color = BorderColor(stack);
    }

    // 내구도를 얼마나 썼는가에 따른 테두리 색
    public static Color BorderColor(ItemStack s)
    {
        Color? c = InventoryManager.WearColor(s); // 인벤토리와 같은 기준 (남은 내구도 50% 이하 주황, 10% 이하 빨강)
        return c.HasValue ? c.Value : EmptyBorder;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (interactive && owner != null) owner.Select(this);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!interactive || owner == null) return;
        owner.Select(this);
        owner.ActivateSelected();
    }
}
