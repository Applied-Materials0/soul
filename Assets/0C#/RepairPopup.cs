using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 장비/도구 수리 창. 정보창의 [수리]를 누르면 가방 UI 위에 뜨고, 필요한 재료와 수리 후 내구도를 보여 준다.
// [수리]를 누르면 재료가 차감되고 내구도가 찬다. 재료는 [아이템] 표의 [수리/등급] 탭에서 정한다.
// 모양은 BagUI 프리팹의 RepairPopup 오브젝트(평소에는 꺼져 있음)를 직접 옮기고 고치면 된다:
//   RepairPopup(어두운 배경) - Panel - Title / Body(재료 목록 글자) / RepairButton / CancelButton
// 키: A/D 커서를 [수리]/[취소]로 옮김, Space/Enter 누르기, ESC 닫기(수리 창만).
public class RepairPopup : MonoBehaviour
{
    public TextMeshProUGUI bodyText;
    public Button repairButton;
    public Button cancelButton;
    public Image repairImage;
    public Image cancelImage;

    private ItemStack stack;
    private Action onDone;
    private int cursor;            // 키보드 커서: 0 = [수리], 1 = [취소]
    private int openedFrame;
    private bool wired;

    public static bool IsOpen
    {
        get
        {
            RepairPopup p = Find();
            return p != null && p.gameObject.activeSelf;
        }
    }

    private static RepairPopup Find()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null || inv.inventoryUI == null) return null;
        Transform t = inv.inventoryUI.transform.Find("RepairPopup"); // 꺼져 있어도 찾는다
        return t != null ? t.GetComponent<RepairPopup>() : null;
    }

    public static void Open(ItemStack stack, Action onDone)
    {
        if (stack == null) return;
        RepairPopup popup = Find();
        if (popup == null)
        {
            Debug.LogError("[RepairPopup] BagUI에 RepairPopup 오브젝트가 없습니다.");
            return;
        }

        popup.stack = stack;
        popup.onDone = onDone;
        popup.cursor = 0;
        popup.openedFrame = Time.frameCount; // 이 창을 연 Space가 곧바로 [수리]를 누르지 않게
        popup.Wire();
        popup.transform.SetAsLastSibling();
        popup.gameObject.SetActive(true);
        popup.Refresh();
        popup.UpdateCursor();
    }

    // 버튼 동작은 처음 열 때 한 번만 연결한다
    private void Wire()
    {
        if (wired) return;
        wired = true;
        if (repairButton != null) repairButton.onClick.AddListener(OnRepair);
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
    }

    // A/D = 커서를 [수리]/[취소]로 옮김, Space/Enter = 커서가 있는 버튼 누르기. (ESC는 GlobalUI가 CloseIfOpen으로 이 창만 닫음)
    void Update()
    {
        if (openedFrame == Time.frameCount) return;

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) { cursor = 1; UpdateCursor(); }
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) { cursor = 0; UpdateCursor(); }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            if (cursor == 0) Confirm();
            else Close();
        }
    }

    // 커서가 있는 버튼을 밝게 키워 보여 준다
    private void UpdateCursor()
    {
        if (repairImage != null)
        {
            repairImage.color = cursor == 0 ? new Color(0.3f, 0.75f, 0.42f) : new Color(0.2f, 0.55f, 0.3f);
            repairImage.transform.localScale = cursor == 0 ? Vector3.one * 1.08f : Vector3.one;
        }
        if (cancelImage != null)
        {
            cancelImage.color = cursor == 1 ? new Color(0.8f, 0.38f, 0.38f) : new Color(0.55f, 0.25f, 0.25f);
            cancelImage.transform.localScale = cursor == 1 ? Vector3.one * 1.08f : Vector3.one;
        }
    }

    // ESC용: 수리 창이 떠 있으면 이 창만 닫고 true
    public static bool CloseIfOpen()
    {
        RepairPopup p = Find();
        if (p == null || !p.gameObject.activeSelf) return false;
        p.Close();
        return true;
    }

    // 필요한 재료와 수리 후 내구도를 보여 준다 (모자란 재료는 빨간색)
    private void Refresh()
    {
        InventoryManager inv = InventoryManager.Instance;
        Item it = stack.itemData;

        string text = $"{it.itemName}\n내구도  {stack.durability} -> {InventoryManager.RepairedDurability(it)} / {it.durabilitymax}\n\n필요한 재료\n";
        bool enough = true;

        List<Ingredient> materials = it.RepairMaterials();
        foreach (Ingredient ing in materials)
        {
            Item mat = inv.GetItemData(ing.itemId);
            int have = inv.GetItemCount(ing.itemId);
            bool ok = have >= ing.amount;
            if (!ok) enough = false;
            string name = mat != null ? mat.itemName : $"ID {ing.itemId}";
            string color = ok ? "#1B7F2A" : "#C00000";
            text += $"  {name} x{ing.amount}   <color={color}>(보유 {have})</color>\n";
        }
        if (materials.Count == 0) text += "  없음";

        if (it.repairSpCost > 0)
        {
            bool spOk = GameManager.SP >= it.repairSpCost;
            if (!spOk) enough = false;
            string spColor = spOk ? "#1B7F2A" : "#C00000";
            text += $"\n\nSP 소모  <color={spColor}>{it.repairSpCost} (보유 {GameManager.SP})</color>";
        }

        if (bodyText != null) bodyText.text = text;
        if (repairButton != null) repairButton.interactable = enough;
    }

    public void Confirm() { if (repairButton != null && repairButton.interactable) OnRepair(); }

    private void OnRepair()
    {
        if (stack == null) return;
        SoundManager.Instance?.PlaySlotClickSound();
        InventoryManager inv = InventoryManager.Instance;
        string message;
        bool ok = inv.TryRepair(stack, out message);
        inv.ShowBagMessageOrToast(message);
        if (!ok) { Refresh(); return; }
        SoundManager.Instance?.PlayEvent(SoundEvent.Repair); // 수리 효과음 (효과음 표의 [수리], 기본: workshop)

        Action done = onDone;
        Close();
        if (done != null) done();
    }

    public void Close()
    {
        onDone = null;
        gameObject.SetActive(false);
    }
}
