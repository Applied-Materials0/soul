using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text; // <- StringBuilder 사용을 위해 필요!

public class ItemInfoPanel : MonoBehaviour
{

    public static ItemInfoPanel Instance;

    [Header("UI 요소 연결")]
    public Image infoIcon;
    public TextMeshProUGUI infoName;
    public TextMeshProUGUI infoDescription;
    public GameObject iteminfopanel; 
    [SerializeField] private TextMeshProUGUI recipeText;
    [SerializeField] private GameObject craftButton;
    [SerializeField] private CraftQuantityPopup craftPopup; // 수량 선택 팝업



    void Awake()
    {
        Instance = this;
        // 시작할 때는 꺼둠
        gameObject.SetActive(false);
    }

    // 핵심 함수: 아이템 데이터를 받아 정보창을 세팅하고 켬
    public void OpenInfoPanel(Item item)
    {
        SoundManager.Instance.PlaySlotClickSound();
        //1. 슬롯에서 데이터가 잘 넘어왔는지 체크
        if (item == null)
        {
            Debug.LogError("넘어온 아이템 데이터(item)가 null입니다!");
            return;
        }
        //2. 인스펙터 연결 체크
        Debug.Log($"[데이터 확인] 이름: {item.itemName} / 아이콘 유무: {item.icon != null} / 갯수: {item.count}");
        gameObject.SetActive(true);

        // 이름 세팅
        if (infoName != null) infoName.text = item.itemName;
        else Debug.LogError("infoName (Text)이 인스펙터에 연결되지 않았습니다!");

        // 설명 세팅
        if (infoDescription != null) infoDescription.text = BuildItemStatText(item);
        else Debug.LogError("infoDescription (Text)이 인스펙터에 연결되지 않았습니다!");

        // 1. 데이터 연동
        if (item.icon != null)
        {
            infoIcon.sprite = item.icon;
            infoIcon.gameObject.SetActive(true);
        }
        else
        {
            infoIcon.gameObject.SetActive(false);
        }
    }

    public void CloseInfoPanel()
    {
        SoundManager.Instance.PlaySlotClickSound();
        gameObject.SetActive(false);
    }

    // 핵심 함수: 아이템 종류에 따라 텍스트 조합하기
    private string BuildItemStatText(Item item)
    {
        StringBuilder sb = new StringBuilder();

        // 1) 아이템 기본 스토리/설명이 있다면 상단에 출력
        if (!string.IsNullOrEmpty(item.description))
        {
            sb.AppendLine(item.description);
            sb.AppendLine(); // 한 줄 공백 띄우기
        }
        // 2. 수치가 0보다 큰 항목만 알아서 줄바꿈 출력!
        //if (item.count > 1)
        sb.AppendLine($" 수   량 : {item.count} 개");
        if (item.weight > 0) sb.AppendLine($" 중   량 : {item.weight} kg");
        if (item.durability > 0) sb.AppendLine($" 내구도 : {item.durability} / {item.durabilitymax}");
        if (item.at > 0) sb.AppendLine($" 공 격 력 : {item.at}");
        if (item.df > 0) sb.AppendLine($" 방 어 력 : {item.df}");
        if (item.hp > 0) sb.AppendLine($" 체    력 : {item.hp}");
        if (item.atrate > 0) sb.AppendLine($" 공격력 배율 : {item.atrate}");
        if (item.dfrate > 0) sb.AppendLine($" 방어력 배율 : {item.dfrate}");
        if (item.hprate > 0) sb.AppendLine($" 체력 배율 : {item.hprate}");
        if (item.heal > 0) sb.AppendLine($" 회 복 량 : {item.heal}");
        if (item.hprateat > 0) sb.AppendLine($" 체력 공격력 : {item.hprateat}");
        if (item.fixat > 0) sb.AppendLine($" 고정 공격력 : {item.fixat}");
        if (item.breakdf > 0) sb.AppendLine($" 관 통 률 : {item.breakdf}");
        if (item.abs > 0) sb.AppendLine($" 흡 수 율 : {item.abs}");
        if (item.avoid > 0) sb.AppendLine($" 회 피 율 : {item.avoid}");
        if (item.critical > 0) sb.AppendLine($" 치명타 배율 : {item.critical}");
        if (item.criticalrate > 0) sb.AppendLine($" 치명타 확률 : {item.criticalrate}");
        if (item.manamax > 0) sb.AppendLine($" 최대 마나 : {item.manamax}");
        if (item.manaheal > 0) sb.AppendLine($" 마나 회복량 : {item.manaheal}");
        if (item.healrate > 0) sb.AppendLine($" 회복량 배율 : {item.healrate}");
        if (item.goldrate > 0) sb.AppendLine($" 골드 배율 : {item.goldrate}");
        if (item.exprate > 0) sb.AppendLine($" 경험치 배율 : {item.exprate}");
        if (item.spmax > 0) sb.AppendLine($" 최대 스태미나 : {item.spmax}");
        if (item.spheal > 0) sb.AppendLine($" 스태미나 회복량 : {item.spheal}");
        if (item.weightmax > 0) sb.AppendLine($" 가방 무게 증량 : {item.weightmax}");

        // 3. 특수 기믹/효과가 있다면 황금색(TMP 태그)으로 강조 출력!
        if (!string.IsNullOrEmpty(item.specialEffect))
        {
            sb.AppendLine();
            sb.AppendLine($"<color=#FFD700> {item.specialEffect}</color>"); // TMP 텍스트 컬러 태그
        }

        return sb.ToString();
    }

    //변수 선언
    private Recipe currentRecipe;
    private Item currentItem;

    public void ShowInfo(Item item)
    {
        currentItem = item;
        currentRecipe = item.recipe; // 아이템에 설정된 레시피 가동

        if (currentRecipe != null && currentRecipe.ingredients.Count > 0)
        {
            Debug.Log($"[ShowInfo] 선택된 아이템 레시피 감지됨: {item.itemName}");
            craftButton.SetActive(true);

            // 조합법 텍스트 구성 (예: 나뭇가지: 2/5개)
            string text = "<b>[조합법]</b>\n";
            foreach (var ing in currentRecipe.ingredients)
            {
                string ingName = ing.item.itemName;
                int hasCount = InventoryManager.Instance.GetItemCount(ing.item.id);

                // 재료가 충분하면 흰색, 부족하면 빨간색 표시
                string colorHex = (hasCount >= ing.amount) ? "#FFFFFF" : "#FF5555";
                text += $"<color={colorHex}>- {ingName}: {ing.amount}개 (보유: {hasCount}개)</color>\n";
            }
            recipeText.SetText(text);
        }
        else
        {
            Debug.Log($"[ShowInfo] {item.itemName}은(는) 조합법이 없는 아이템입니다.");
            craftButton.SetActive(false);
            recipeText.SetText("제작 불가능한 아이템입니다.");
        }
    }

    // '제작' 버튼 클릭 시 수량 선택 팝업 열기
    public void OnClickCraftButton()
    {
        SoundManager.Instance.PlaySlotClickSound();
        // 1. currentRecipe가 null인지 검사 (지워도 됨)
        if (currentRecipe == null)
        {
            Debug.LogError("[CraftError] currentRecipe가 null입니다! ShowInfo()가 제대로 호출되었는지 확인하세요.");
            return;
        }
        int maxCraft = InventoryManager.Instance.GetMaxCraftableAmount(currentRecipe); 
        Debug.Log($"[CraftCheck] 계산된 최대 제작 가능 수량: {maxCraft}");
        
        // 재료별 보유 수량 출력 디버그 (지워도 됨)
        foreach (var ing in currentRecipe.ingredients)
        {
            int hasCount = InventoryManager.Instance.GetItemCount(ing.item.id);
            Debug.Log($"[재료 검사] 필요 ID: {ing.item.id} | 필요 개수: {ing.amount} | 현재 보유: {hasCount}");
        }

        if (maxCraft < 1)
        {
            // 재료 부족 알림 메시지 출력
            Debug.LogWarning("재료가 부족합니다!");
            return;
        }
        // 2. 즉시 1개 제작 실행
        bool success = InventoryManager.Instance.TryCraftItem(currentRecipe, 1);
        if (success)
        {
            // (선택) 제작 성공 사운드 재생
            // SoundManager.Instance?.PlayCraftSound();

            // 3. 재료가 소모되고 아이템이 늘어났으므로 정보 패널 UI 즉시 갱신
            ShowInfo(currentItem);
        }

        //craftPopup.OpenPopup(currentRecipe, maxCraft); //수량 선택 팝업
    }

}