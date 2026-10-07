using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CraftQuantityPopup : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Button cancelBtn;

    private Recipe targetRecipe;
    private int currentCount = 1;
    private int maxCount = 1;

    public void OpenPopup(Recipe recipe, int maxCraftable)
    {
        targetRecipe = recipe;
        maxCount = maxCraftable;
        currentCount = 1; // 기본 1개 세팅

        UpdateUI();
        gameObject.SetActive(true);
    }

    public void OnClickPlus()
    {
        if (currentCount < maxCount)
        {
            currentCount++;
            UpdateUI();
        }
    }

    public void OnClickMinus()
    {
        if (currentCount > 1)
        {
            currentCount--;
            UpdateUI();
        }
    }

    public void OnClickMax()
    {
        currentCount = maxCount;
        UpdateUI();
    }

    // [제작 확인] 버튼
    public void OnClickConfirm()
    {
        bool success = InventoryManager.Instance.TryCraftItem(targetRecipe, currentCount);
        if (success)
        {
            // 제작 성공 사운드 재생 및 UI 갱신
            ClosePopup();
        }
    }

    // [취소] 버튼
    public void OnClickCancel()
    {
        ClosePopup();
    }

    private void ClosePopup()
    {
        gameObject.SetActive(false);
    }

    private void UpdateUI()
    {
        countText.SetText($"{currentCount} / {maxCount}");
    }
}