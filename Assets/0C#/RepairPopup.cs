using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 장비/도구 수리 창. 정보창의 [수리]를 누르면 가방 UI 위에 뜨고, 필요한 재료와 수리 후 내구도를 보여 준다.
// [수리]를 누르면 재료가 차감되고 내구도가 찬다. 재료는 [아이템] 표의 [수리/등급] 탭에서 정한다.
public class RepairPopup : MonoBehaviour
{
    private ItemStack stack;
    private Action onDone;
    private TextMeshProUGUI bodyText;
    private Button repairButton;
    private Image repairImage;
    private Image cancelImage;
    private int cursor;            // 키보드 커서: 0 = [수리], 1 = [취소]
    private int openedFrame;

    public static void Open(ItemStack stack, Action onDone)
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null || inv.inventoryUI == null || stack == null) return;

        TMP_FontAsset font = inv.UIFont;
        Transform parent = inv.inventoryUI.transform;

        // 뒤의 가방을 눌러지지 않게 막는 반투명 배경
        RectTransform dim = CraftQuantityPopup.NewRect("RepairPopup", parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        dim.offsetMin = Vector2.zero;
        dim.offsetMax = Vector2.zero;
        Image dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.5f);
        dim.SetAsLastSibling();

        RepairPopup popup = dim.gameObject.AddComponent<RepairPopup>();
        popup.stack = stack;
        popup.onDone = onDone;

        // 가운데 창
        RectTransform panel = CraftQuantityPopup.NewRect("Panel", dim, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 560f));
        Image bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0.96f, 0.96f, 0.96f, 1f);

        TextMeshProUGUI title = CraftQuantityPopup.AddText(
            CraftQuantityPopup.NewRect("Title", panel, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(780f, 70f)),
            48, TextAlignmentOptions.Center, font);
        title.color = Color.black;
        title.fontStyle = FontStyles.Bold;
        title.text = "수리";

        popup.bodyText = CraftQuantityPopup.AddText(
            CraftQuantityPopup.NewRect("Body", panel, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(740f, 340f)),
            34, TextAlignmentOptions.TopLeft, font);
        popup.bodyText.color = Color.black;
        ((RectTransform)popup.bodyText.transform).pivot = new Vector2(0.5f, 1f);

        GameObject repair = CraftQuantityPopup.CreateButton(panel, "RepairButton", "수리", font,
            new Color(0.2f, 0.55f, 0.3f), new Vector2(0.5f, 0f), new Vector2(-130f, 40f), new Vector2(220f, 80f), popup.OnRepair);
        popup.repairButton = repair.GetComponent<Button>();
        popup.repairImage = repair.GetComponent<Image>();
        popup.cancelImage = CraftQuantityPopup.CreateButton(panel, "CancelButton", "취소", font,
            new Color(0.5f, 0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(130f, 40f), new Vector2(220f, 80f), popup.Close).GetComponent<Image>();

        popup.openedFrame = Time.frameCount;
        popup.Refresh();
        popup.UpdateCursor();
    }

    // A/D = 커서를 [수리]/[취소]로 옮김, Space/Enter = 커서가 있는 버튼 누르기. (ESC는 GlobalUI가 CloseIfOpen으로 이 창만 닫음)
    void Update()
    {
        if (openedFrame == Time.frameCount) return; // 이 창을 연 Space가 곧바로 [수리]를 누르지 않게

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
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null || inv.inventoryUI == null) return false;
        Transform rp = inv.inventoryUI.transform.Find("RepairPopup");
        if (rp == null) return false;
        Destroy(rp.gameObject);
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

        bodyText.text = text;
        repairButton.interactable = enough;
    }

    public void Confirm() { if (repairButton != null && repairButton.interactable) OnRepair(); }

    private void OnRepair()
    {
        SoundManager.Instance?.PlaySlotClickSound();
        InventoryManager inv = InventoryManager.Instance;
        string message;
        bool ok = inv.TryRepair(stack, out message);
        inv.ShowBagMessageOrToast(message);
        if (!ok) { Refresh(); return; }
        SoundManager.Instance?.PlayEvent(SoundEvent.Repair); // 수리 효과음 (효과음 표의 [수리], 기본: workshop)

        if (onDone != null) onDone();
        Close();
    }

    public void Close()
    {
        Destroy(gameObject);
    }
}
