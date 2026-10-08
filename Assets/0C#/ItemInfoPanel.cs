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
    // 지금 표시 중인 아이템과 스택 (제작 후 수량 표시를 갱신할 때 사용)
    private Item currentItem;
    private ItemStack currentStack;

    void Awake()
    {
        // 인벤토리가 이 패널을 찾지 못하는 구조일 때만 스스로를 등록
        if (instance == null && (InventoryManager.Instance == null || InventoryManager.Instance.FindInfoPanel() == null))
            instance = this;
    }

    // 슬롯 클릭 시 호출: 정보창을 열고 그 슬롯(스택)의 내용을 표시
    public void ShowItem(Item item, ItemStack stack)
    {
        if (item == null) return;
        SoundManager.Instance?.PlaySlotClickSound();
        Display(item, stack);
    }

    // 정보창을 닫음 (효과음 없음)
    public void HidePanel()
    {
        gameObject.SetActive(false);
    }

    // 정보창이 열려 있으면 내용을 다시 그림 (제작으로 보유 수량이 바뀐 뒤 호출)
    public void RefreshIfOpen()
    {
        if (gameObject.activeSelf && currentItem != null)
            Display(currentItem, currentStack);
    }

    // 효과음 없이 아이템 정보(이름, 설명, 아이콘)를 화면에 표시
    private void Display(Item item, ItemStack stack)
    {
        currentItem = item;
        currentStack = stack;
        gameObject.SetActive(true);
        if (InventoryManager.Instance != null) InventoryManager.Instance.CloseSidePanels(); // 장비창/버프창은 이 정보창과 같은 자리를 쓴다

        // 이름 위 작은 글자: 장비/도구의 등급과 경험치
        EnsureGradeLabel();
        bool isGear = item.durabilitymax > 0 || InventoryManager.CanEquip(item);
        gradeLabel.text = isGear && stack != null && InventoryManager.Instance != null ? InventoryManager.Instance.GradeLine(stack) : "";

        // 이름 세팅
        if (infoName != null) infoName.text = item.itemName;
        else Debug.LogError("infoName (Text)이 인스펙터에 연결되지 않았습니다!");

        // 설명 세팅
        if (infoDescription != null) infoDescription.text = BuildItemStatText(item, stack);
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

        // 사용 효과(체력/SP/마나 회복)가 있는 소모품이면 [사용] 버튼을 보여 준다. 전투 중에도 쓸 수 있다 (한 턴을 씀).
        EnsureUseButton();
        bool inBattle = FieldSearch.Instance != null && FieldSearch.Instance.InBattle;
        useButton.SetActive(InventoryManager.CanUse(item) && stack != null);

        // [장착] / [해제]: 장비 아이템만, 전투 중에는 바꿀 수 없다
        EnsureEquipButton();
        bool broken = InventoryManager.NeedsRepair(stack);
        EnsureRepairButton();
        repairButton.SetActive(broken && !inBattle);
        bool canEquip = InventoryManager.CanEquip(item) && stack != null && !inBattle && !broken;
        equipButton.SetActive(canEquip);
        if (canEquip) equipLabel.text = InventoryManager.Instance.IsEquipped(stack) ? "해제" : "장착";

        // [버리기] [나누기]: 전투 중에는 쓸 수 없다
        EnsureBagButtons();
        discardArmed = false;
        discardLabel.text = "버리기";
        discardButton.SetActive(stack != null && !inBattle);
        splitButton.SetActive(stack != null && !inBattle && stack.amount >= 2);
    }

    // 정보창 아래쪽의 [사용] 버튼 (처음 필요할 때 만든다)
    private GameObject useButton;

    private void EnsureUseButton()
    {
        if (useButton != null) return;
        TMP_FontAsset font = infoName != null ? infoName.font : null;
        useButton = CraftQuantityPopup.CreateButton(transform, "UseButton", "사용", font,
            new Color(0.2f, 0.55f, 0.3f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(260f, 80f), OnClickUse);
    }


    // 정보창 아래쪽 양끝의 [버리기] / [반으로 나누기] 버튼
    private GameObject discardButton, splitButton;
    private TextMeshProUGUI discardLabel;
    private bool discardArmed;       // [버리기]를 한 번 눌러 "한 번 더 누르면 버림" 상태
    private float discardArmedAt;

    private void EnsureBagButtons()
    {
        if (discardButton != null) return;
        TMP_FontAsset font = infoName != null ? infoName.font : null;
        discardButton = CraftQuantityPopup.CreateButton(transform, "DiscardButton", "버리기", font,
            new Color(0.65f, 0.2f, 0.2f), new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(200f, 80f), OnClickDiscard);
        discardLabel = discardButton.GetComponentInChildren<TextMeshProUGUI>();
        splitButton = CraftQuantityPopup.CreateButton(transform, "SplitButton", "반으로 나누기", font,
            new Color(0.25f, 0.4f, 0.65f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(200f, 80f), OnClickSplit);
        // 긴 글자가 버튼 밖으로 나가지 않게 글자 크기를 줄인다
        TextMeshProUGUI splitLabel = splitButton.GetComponentInChildren<TextMeshProUGUI>();
        if (splitLabel != null) splitLabel.fontSize = 24;
    }

    void Update()
    {
        // 한 번 누른 뒤 3초 안에 다시 누르지 않으면 [버리기] 확인 상태를 푼다
        if (discardArmed && Time.unscaledTime - discardArmedAt > 3f)
        {
            discardArmed = false;
            if (discardLabel != null) discardLabel.text = "버리기";
        }
    }

    // [버리기]: 실수로 버리지 않도록 두 번 눌러야 슬롯의 아이템 전부를 버린다 (일부만 버리려면 먼저 나누기)
    private void OnClickDiscard()
    {
        SoundManager.Instance?.PlaySlotClickSound();
        if (currentStack == null || InventoryManager.Instance == null) return;

        if (!discardArmed)
        {
            discardArmed = true;
            discardArmedAt = Time.unscaledTime;
            discardLabel.text = "정말 버림?";
            return;
        }

        string name = currentItem != null ? currentItem.itemName : "";
        int amount = currentStack.amount;
        InventoryManager.Instance.DiscardStack(currentStack);
        ItemGainToast.ShowMessage($"{name} {amount}개를 버렸다.");
        HidePanel();
    }

    // [반으로 나누기]: 이 슬롯의 절반을 새 슬롯으로 나눈다
    private void OnClickSplit()
    {
        SoundManager.Instance?.PlaySlotClickSound();
        if (currentStack == null || InventoryManager.Instance == null) return;

        string message;
        bool ok = InventoryManager.Instance.SplitStack(currentStack, out message);
        ItemGainToast.ShowMessage(message);
        if (ok) Display(currentItem, currentStack);
    }

    // [사용]: 아이템 1개를 쓰고 효과를 적용한다. 다 쓰면 정보창을 닫는다.
    private void OnClickUse()
    {
        SoundManager.Instance?.PlaySlotClickSound();
        if (currentStack == null || InventoryManager.Instance == null) return;

        // 전투 중에는 내 차례일 때만 쓸 수 있고, 쓰면 이번 턴을 쓴 것으로 친다
        FieldSearch field = FieldSearch.Instance;
        bool inBattle = field != null && field.InBattle;
        if (inBattle && !field.CanActInBattle)
        {
            ItemGainToast.ShowMessage("지금은 아이템을 쓸 수 없다.");
            return;
        }

        string message;
        bool used = InventoryManager.Instance.UseItem(currentStack, out message);
        ItemGainToast.ShowMessage(message); // 결과(또는 못 쓰는 이유)를 알림

        if (!used) return;

        // 체력이 0이 되어 쓰러졌다 (독 같은 위험한 아이템)
        if (InventoryManager.Instance.LastUseFainted)
        {
            HidePanel();
            InventoryManager.Instance.CloseInventoryQuiet();
            if (field != null) field.FaintFromItem();
            else ItemGainToast.ShowMessage(InventoryManager.Instance.RecoverFromFaint());
            return;
        }

        // 마지막 1개를 써서 슬롯이 사라졌으면 정보창을 닫고, 남았으면 수량을 갱신
        if (InventoryManager.Instance.itemList.Contains(currentStack)) Display(currentItem, currentStack);
        else HidePanel();

        // 전투 중이면 가방을 닫고 몬스터의 차례로 넘어간다
        if (inBattle)
        {
            HidePanel();
            InventoryManager.Instance.CloseInventoryQuiet();
            field.BattleItemUsed(message);
        }
    }

    // 아이템 이름 위의 작은 글자: 등급과 경험치 (BagUI/ItemInfoPanel 프리팹의 InfoGrade 오브젝트. 없으면 만든다)
    private TextMeshProUGUI gradeLabel;

    private void EnsureGradeLabel()
    {
        if (gradeLabel != null) return;
        Transform t = transform.Find("InfoGrade");
        if (t != null) gradeLabel = t.GetComponent<TextMeshProUGUI>();
        if (gradeLabel != null) return;

        RectTransform rt = CraftQuantityPopup.NewRect("InfoGrade", transform, new Vector2(0f, 1f), new Vector2(200f, -20f), new Vector2(520f, 34f));
        rt.pivot = new Vector2(0f, 0.5f);
        gradeLabel = CraftQuantityPopup.AddText(rt, 26, TextAlignmentOptions.Left, infoName != null ? infoName.font : null);
        gradeLabel.color = new Color(0.25f, 0.25f, 0.25f);
    }

    // [수리] 버튼 (장비/도구의 내구도가 0일 때, [장착] 버튼 자리에 나타남)
    private GameObject repairButton;

    private void EnsureRepairButton()
    {
        if (repairButton != null) return;
        TMP_FontAsset font = infoName != null ? infoName.font : null;
        repairButton = CraftQuantityPopup.CreateButton(transform, "RepairButton", "수리", font,
            new Color(0.2f, 0.5f, 0.6f), new Vector2(0.5f, 0f), new Vector2(0f, 135f), new Vector2(260f, 80f), OnClickRepair);
    }

    private void OnClickRepair()
    {
        SoundManager.Instance?.PlaySlotClickSound();
        if (currentStack == null) return;
        RepairPopup.Open(currentStack, () => { if (currentItem != null) Display(currentItem, currentStack); });
    }

    // 정보창 아래쪽의 [장착] / [해제] 버튼
    private GameObject equipButton;
    private TextMeshProUGUI equipLabel;

    private void EnsureEquipButton()
    {
        if (equipButton != null) return;
        TMP_FontAsset font = infoName != null ? infoName.font : null;
        equipButton = CraftQuantityPopup.CreateButton(transform, "EquipButton", "장착", font,
            new Color(0.6f, 0.45f, 0.15f), new Vector2(0.5f, 0f), new Vector2(0f, 135f), new Vector2(260f, 80f), OnClickEquip);
        equipLabel = equipButton.GetComponentInChildren<TextMeshProUGUI>();
    }

    private void OnClickEquip()
    {
        SoundManager.Instance?.PlaySlotClickSound();
        if (currentStack == null || InventoryManager.Instance == null) return;

        string message;
        if (InventoryManager.Instance.IsEquipped(currentStack)) InventoryManager.Instance.Unequip(currentStack, out message);
        else InventoryManager.Instance.Equip(currentStack, out message);

        InventoryManager.Instance.ShowBagMessageOrToast(message);
        Display(currentItem, currentStack); // 버튼 글자와 능력치 표시를 갱신
    }

    public void CloseInfoPanel()
    {
        SoundManager.Instance?.PlaySlotClickSound();
        HidePanel();
    }

    // 핵심 함수: 아이템 종류에 따라 텍스트 조합하기
    private string BuildItemStatText(Item item, ItemStack stack)
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
        int owned = InventoryManager.Instance.GetItemCount(item.id);
        sb.AppendLine($" 수   량 : {owned} 개");
        // 중량은 보유한 수량 전체의 무게로 표시 (item.weight는 1개당 무게)
        if (item.weight > 0)
        {
            string unit = owned > 1 ? $" (개당 {item.weight} kg)" : "";
            sb.AppendLine($" 중   량 : {item.weight * Mathf.Max(owned, 1)} kg{unit}");
        }
        if (item.durabilitymax <= 0 && stack != null && stack.durability > 0)
            sb.AppendLine($" 내구도 : {stack.durability}"); // 도구에서 이어받은 내구도 (최대 내구도가 표에 없는 아이템)
        if (item.durabilitymax > 0)
        {
            // 도구는 슬롯마다 내구도가 따로이므로 클릭한 슬롯(스택)의 현재 내구도를 표시
            int currentDurability = stack != null ? stack.durability : item.durabilitymax;
            sb.AppendLine($" 내구도 : {currentDurability} / {item.durabilitymax}");
        }
        // 사용 효과: 회복하는 것(플러스 값)만 보여 준다. 마이너스 값은 표시하지 않는다.
        if (item.useHp > 0) sb.AppendLine($" 체력 회복 : +{item.useHp}");
        if (item.useSp > 0) sb.AppendLine($" SP 회복 : +{item.useSp}");
        if (item.useMana > 0) sb.AppendLine($" 마나 회복 : +{item.useMana}");
        if (item.equipSlot != EquipSlot.None) sb.AppendLine($" 장착 부위 : {EquipSlotInfo.Name(item.equipSlot)}");
        TraitDef trait = GameTables.Traits.Get(item.traitId);
        if (trait != null)
        {
            string eff = "";
            if (trait.atRate != 0f) eff += $" 공격력 {trait.atRate:+0.#;-0.#}%";
            if (trait.dfRate != 0f) eff += $" 방어력 {trait.dfRate:+0.#;-0.#}%";
            string effText = eff.Length > 0 ? " (" + eff.Trim() + ")" : "";
            sb.AppendLine($" 특성 : {trait.label}{effText}");
        }
        if (item.at > 0) sb.AppendLine($" 공 격 력 : {item.at}");
        if (item.df > 0) sb.AppendLine($" 방 어 력 : {item.df}");
        if (item.hp > 0) sb.AppendLine($" 체    력 : {item.hp}");
        if (item.atrate > 0) sb.AppendLine($" 공격력 배율 : {item.atrate}");
        if (item.dfrate > 0) sb.AppendLine($" 방어력 배율 : {item.dfrate}");
        if (item.hprate > 0) sb.AppendLine($" 체력 배율 : {item.hprate}");
        if (item.hprateat > 0) sb.AppendLine($" 체력 공격력 : {item.hprateat}");
        if (item.fixat > 0) sb.AppendLine($" 고정 공격력 : {item.fixat}");
        if (item.breakdf > 0) sb.AppendLine($" 관 통 률 : {item.breakdf}");
        if (item.abs > 0) sb.AppendLine($" 흡 수 율 : {item.abs}");
        if (item.avoid > 0) sb.AppendLine($" 회 피 율 : {item.avoid}");
        if (item.critical > 0) sb.AppendLine($" 치명타 배율 : {item.critical}");
        if (item.criticalrate > 0) sb.AppendLine($" 치명타 확률 : {item.criticalrate}");
        if (item.manamax > 0) sb.AppendLine($" 최대 마나 : {item.manamax}");
        if (item.healrate > 0) sb.AppendLine($" 회복량 배율 : {item.healrate}");
        if (item.goldrate > 0) sb.AppendLine($" 골드 배율 : {item.goldrate}");
        if (item.exprate > 0) sb.AppendLine($" 경험치 배율 : {item.exprate}");
        if (item.spmax > 0) sb.AppendLine($" 최대 스태미나 : {item.spmax}");
        if (item.weightmax > 0) sb.AppendLine($" 가방 무게 증량 : {item.weightmax}");
        if (item.slotmax > 0) sb.AppendLine($" 슬롯 증량 : {item.slotmax}");

        // 3. 특수 기믹/효과가 있다면 황금색(TMP 태그)으로 강조 출력!
        if (!string.IsNullOrEmpty(item.specialEffect))
        {
            sb.AppendLine();
            sb.AppendLine($"<color=#FFD700> {item.specialEffect}</color>"); // TMP 텍스트 컬러 태그
        }

        return sb.ToString();
    }

}