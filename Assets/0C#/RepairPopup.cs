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
        CraftQuantityPopup.CreateButton(panel, "CancelButton", "취소", font,
            new Color(0.5f, 0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(130f, 40f), new Vector2(220f, 80f), popup.Close);

        popup.Refresh();
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

        if (onDone != null) onDone();
        Close();
    }

    public void Close()
    {
        Destroy(gameObject);
    }
}
