using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 장비 칸 묶음: 아래에 있는 EquipSlotView들을 지금 장착한 장비로 채운다. 같은 스크립트를 두 곳에 쓴다.
//  - 장비창(가방): interactive = true. W/A/S/D(또는 방향키)나 마우스로 칸을 고르면 설명 팝업이 뜨고, 눌러서(Space) 해제한다.
//  - 필드 화면: interactive = false. 선택은 안 되고 보여 주기만 한다 (테두리 색으로 내구도를 알려 줌).
// 칸의 모양과 위치는 하이어라키의 각 칸(Slot_...)을 직접 옮기고 고치면 된다. 칸 사이 이동은 칸의 실제 위치로 계산한다.
public class EquipmentSlots : MonoBehaviour
{
    public bool interactive;

    [Header("설명 팝업 (interactive일 때만. 약간 투명한 검은색 바탕에 흰 글씨)")]
    public GameObject tooltipRoot;
    public Image tooltipIcon;
    public TextMeshProUGUI tooltipTitle;
    public TextMeshProUGUI tooltipBody;

    private EquipSlotView[] views;
    private EquipSlotView selected;

    void Awake()
    {
        views = GetComponentsInChildren<EquipSlotView>(true);
        foreach (EquipSlotView v in views) v.Init(this, interactive);
        if (tooltipRoot != null) tooltipRoot.SetActive(false);
    }

    void OnEnable()
    {
        InventoryManager.EquipmentChanged += Refresh;
        if (views == null) views = GetComponentsInChildren<EquipSlotView>(true);
        if (interactive && selected == null && views.Length > 0) selected = FindFirstEquipped() ?? views[0];
        Refresh();
    }

    void OnDisable()
    {
        InventoryManager.EquipmentChanged -= Refresh;
    }

    private EquipSlotView FindFirstEquipped()
    {
        foreach (EquipSlotView v in views) if (v.Stack != null) return v;
        return null;
    }

    public void Refresh()
    {
        if (views == null) return;
        InventoryManager inv = InventoryManager.Instance;
        foreach (EquipSlotView v in views)
        {
            ItemStack s = inv != null ? inv.EquippedAt(v.slot, v.index) : null;
            v.Apply(s, interactive && v == selected);
        }
        UpdateTooltip();
    }

    public void Select(EquipSlotView v)
    {
        if (!interactive || v == null || v == selected) return;
        selected = v;
        Refresh();
    }

    // 키보드 이동: 칸의 실제 위치를 보고 그 방향에서 가장 가까운 칸으로 간다
    public void Move(int dx, int dy)
    {
        if (!interactive || views == null || views.Length == 0) return;
        if (selected == null) { selected = views[0]; Refresh(); return; }

        Vector2 from = selected.Rect.anchoredPosition;
        Vector2 dir = new Vector2(dx, dy);
        EquipSlotView best = null;
        float bestScore = float.MaxValue;
        foreach (EquipSlotView v in views)
        {
            if (v == selected || !v.gameObject.activeInHierarchy) continue;
            Vector2 d = v.Rect.anchoredPosition - from;
            float along = Vector2.Dot(d, dir);
            if (along <= 0.01f) continue;                       // 그 방향에 있는 칸만
            float across = Mathf.Abs(d.x * dir.y - d.y * dir.x); // 방향에서 벗어난 정도
            float score = along + across * 2f;
            if (score < bestScore) { bestScore = score; best = v; }
        }
        if (best != null) Select(best);
    }

    // 선택한 칸의 장비를 해제한다 (전투 중이면 이번 턴을 씀)
    public void ActivateSelected()
    {
        if (!interactive || selected == null || selected.Stack == null || InventoryManager.Instance == null) return;
        SoundManager.Instance?.PlaySlotClickSound();
        InventoryManager.Instance.ToggleEquipFromUI(selected.Stack);
    }

    private void UpdateTooltip()
    {
        if (tooltipRoot == null) return;
        ItemStack s = selected != null ? selected.Stack : null;
        if (!interactive || s == null || InventoryManager.Instance == null)
        {
            tooltipRoot.SetActive(false);
            return;
        }

        string title, body;
        InventoryManager.Instance.BuildEquipTooltip(s, out title, out body);
        tooltipRoot.SetActive(true);
        if (tooltipTitle != null) tooltipTitle.text = title;
        if (tooltipBody != null) tooltipBody.text = body;
        if (tooltipIcon != null)
        {
            tooltipIcon.sprite = s.itemData.icon;
            tooltipIcon.enabled = s.itemData.icon != null;
        }
    }
}
