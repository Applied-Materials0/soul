using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text; // <- StringBuilder 사용을 위해 필요!

public class ItemInfoPanel : MonoBehaviour
{

    // 정보창은 처음에 꺼져 있어 Awake가 늦게 실행되므로, 비활성 상태여도 InventoryManager를 통해 찾아 온다.
    // (BagUI 안에 ItemInfoPanel이 여러 개 있어도 스탯창에 붙은 것은 제외하고 올바른 하나만 반환)
    private static ItemInfoPanel instance;
    public static ItemInfoPanel Instance
    {
        get
        {
            if (instance == null && InventoryManager.Instance != null)
                instance = InventoryManager.Instance.FindInfoPanel();
            return instance;
        }
    }

    [Header("UI 요소 연결")]
    public Image infoIcon;
    public TextMeshProUGUI infoName;
    public TextMeshProUGUI infoDescription;
    public GameObject iteminfopanel;
    [Header("제작 UI (비워 두면 코드가 기본 UI를 자동 생성)")]
    [SerializeField] private TextMeshProUGUI recipeText;
    [SerializeField] private GameObject craftButton;
    [SerializeField] private CraftQuantityPopup craftPopup; // 수량 선택 팝업

    void Awake()
    {
        // 인벤토리가 이 패널을 찾지 못하는 구조일 때만 스스로를 등록
        if (instance == null && (InventoryManager.Instance == null || InventoryManager.Instance.FindInfoPanel() == null))
            instance = this;
    }

    // 슬롯 클릭 시 호출: 정보창을 열고 내용과 제작 버튼 상태를 한 번에 세팅
    public void ShowItem(Item item)
    {
        if (item == null) return;
        OpenInfoPanel(item);
        ShowInfo(item);
    }

    // 정보창과 제작 팝업을 닫음 (효과음 없음)
    public void HidePanel()
    {
        if (craftPopup != null) craftPopup.Close();
        gameObject.SetActive(false);
    }

    // 제작 버튼과 수량 팝업이 인스펙터에 없으면 기본 UI를 만들어 붙임
    private void EnsureCraftUI()
    {
        TMP_FontAsset font = infoName != null ? infoName.font : null;

        if (craftButton == null)
        {
            craftButton = CraftQuantityPopup.CreateButton(transform, "CraftButton", "제작", font,
                new Color(0.2f, 0.55f, 0.3f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(260f, 80f), OnClickCraftButton);
        }
        if (craftPopup == null)
        {
            craftPopup = CraftQuantityPopup.Create(transform, font);
        }
    }

    // 핵심 함수: 아이템 데이터를 받아 정보창을 세팅하고 켬
    public void OpenInfoPanel(Item item)
    {
        SoundManager.Instance?.PlaySlotClickSound();
        //1. 슬롯에서 데이터가 잘 넘어왔는지 체크
        if (item == null)
        {
            Debug.LogError("넘어온 아이템 데이터(item)가 null입니다!");
            return;
        }
        Display(item);
    }

    // 효과음 없이 아이템 정보(이름, 설명, 아이콘)를 화면에 표시
    private void Display(Item item)
    {
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
        SoundManager.Instance?.PlaySlotClickSound();
        HidePanel();
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
        sb.AppendLine($" 수   량 : {InventoryManager.Instance.GetItemCount(item.id)} 개");
        if (item.weight > 0) sb.AppendLine($" 중   량 : {item.weight} kg");
        if (item.durabilitymax > 0)
        {
            ItemStack stack = InventoryManager.Instance.FindStack(item.id);
            int currentDurability = stack != null ? stack.durability : item.durabilitymax;
            sb.AppendLine($" 내구도 : {currentDurability} / {item.durabilitymax}");
        }
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
        EnsureCraftUI();

        currentItem = item;
        currentRecipe = item.recipe; // 아이템에 설정된 레시피 가동

        // 레시피에 재료가 하나라도 있어야 제작 가능한 아이템
        bool craftable = currentRecipe != null && currentRecipe.ingredients != null && currentRecipe.ingredients.Count > 0;

        // 제작 가능하면 제작 버튼을 켜고, 제작할 수 없는 아이템이면 끔
        craftButton.SetActive(craftable);

        if (recipeText != null)
        {
            recipeText.SetText(craftable ? "<b>[제작 가능]</b>" : "제작 불가능한 아이템입니다.");
        }
    }

    // '제작' 버튼 클릭 시 조합법과 수량 입력 팝업 열기
    public void OnClickCraftButton()
    {
        SoundManager.Instance?.PlaySlotClickSound();
        if (currentRecipe == null || currentItem == null)
        {
            Debug.LogError("[CraftError] 선택된 아이템/레시피가 없습니다! ShowInfo()가 먼저 호출되어야 합니다.");
            return;
        }

        EnsureCraftUI();
        craftPopup.Open(currentRecipe, OnCrafted);
    }

    // 제작이 끝난 뒤 호출: 보유 수량이 바뀌었으므로 정보창 내용을 갱신
    private void OnCrafted()
    {
        if (currentItem != null)
        {
            Display(currentItem);
            ShowInfo(currentItem);
        }
    }

}