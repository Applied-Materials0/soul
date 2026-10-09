using UnityEngine;
using System.Collections.Generic;

// 장착 부위. 장비 아이템은 이 중 한 곳에 장착한다 (None이면 장착 불가)
public enum EquipSlot
{
    None,       // 장비 아님
    Weapon,     // 무기 / 도구
    Head,       // 머리
    Body,       // 몸
    Legs,       // 다리
    Accessory,  // 장신구
    Shield,      // 방패
    Glove,  // 장갑
}

public static class EquipSlotInfo
{
    public static string Name(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Weapon: return "무기";
            case EquipSlot.Head: return "머리";
            case EquipSlot.Body: return "몸";
            case EquipSlot.Legs: return "다리";
            case EquipSlot.Accessory: return "장신구";
            case EquipSlot.Shield: return "방패";
            case EquipSlot.Glove: return "장갑";
            default: return "-";
        }
    }
}

// 무기 종류 (장비 표의 [무기 종류]). 스킬/특성이 "이 종류의 무기를 장착했을 때만" 쓰이게 하는 기준이다.
// 배틀 엑스는 도끼 도구(도구 종류 Axe)로도 설정할 수 있다 (제작/수리/벌목 가능). 도구 종류는 따로 정하므로 둘 다 쓸 수 있다.
public enum WeaponType
{
    None,       // 무기 종류 없음
    Mace,       // 메이스
    BattleAxe,  // 배틀 엑스
    Bow,        // 활
    Sword       // 검
}

public static class WeaponTypeInfo
{
    public static string Name(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Mace: return "메이스";
            case WeaponType.BattleAxe: return "배틀 엑스";
            case WeaponType.Bow: return "활";
            case WeaponType.Sword: return "검";
            default: return "";
        }
    }
}

public enum ToolType
{
    None,       // 도구 아님/자원
    Axe,        // 도끼: 벌목
    Pickaxe,    // 곡괭이: 채광
    Hammer,     // 망치
    Sickle,     // 낫: 풀, 작물
    Bottle,     // 병: 물
    Knife,  // 칼: 도축 (가죽, 고기)
    Mortar      // 절구: 자원 분쇄
}

// 아이템 정의 데이터. 아이템 하나 = .asset 파일 하나 (Create > Soul > Item).
// 플레이 중 바뀌는 값(수량, 현재 내구도)은 여기에 두지 않고 ItemStack에 둔다.
// 주의: 필드 이름을 바꿀 때는 반드시 [FormerlySerializedAs("옛이름")]을 붙일 것. 안 붙이면 그 필드 값이 초기화된다.
[CreateAssetMenu(fileName = "Item_", menuName = "Soul/Item")]
public class Item : ScriptableObject
{
    [Header("기본 정보")]
    public int id;             // 고유 ID (ItemDatabase 안에서 중복 금지)
    public string itemName;    // 아이템 이름
    public int weight;         // 무게
    public int countmax;       // 한 슬롯에 쌓이는 최대 수량 (도구/장비는 1, 0이면 제한 없음). 넘치면 다음 슬롯에 쌓임
    public int durabilitymax;  // 최대 내구도 (도구가 아니면 0)

    [Header("도구 분류")]
    public ToolType toolType;  // 도구 종류
    public int tier;           // 도구 티어
    public int toolattack;     // 자원 채집 시 채집량

    [Header("설명")]
    [TextArea(2, 5)]
    public string description;
    [Header("특수 효과 (예: 착용 시 체력 회복)")]
    public string specialEffect;

    [Header("아이템 능력치")]
    public int at;             // 공격력
    public int df;             // 방어력
    public int hp;             // 체력
    public float hprateat;     // 체력 비례 공격력
    public int fixat;          // 고정 공격력
    public float breakdf;      // 방어 무시
    public float abs;          // 흡혈
    public float avoid;        // 회피
    public float criticalrate; // 치명타 확률
    public float critical;     // 치명타 피해
    public int manamax;        // 최대 마나 증가
    public float atrate;       // 공격력 비율
    public float dfrate;       // 방어력 비율
    public float hprate;       // 체력 비율
    public float healrate;     // 회복량 비율
    public float goldrate;     // 골드 비율
    public float exprate;      // 경험치 비율
    public int spmax;          // 최대 스태미나 증가
    public int weightmax;      // 소지 무게 증가
    public int slotmax;        // 소지 슬롯 증가 (장비)

    [Header("장착 (장비 장착창에 끼는 아이템. 장착해야 능력치와 특성이 적용됨)")]
    public EquipSlot equipSlot;   // 장착 부위 (None이면 장착 불가)
    public WeaponType weaponType; // 무기 종류 (메이스/배틀 엑스/활/검. 무기가 아니면 None)
    public int traitId;           // 특성 ID (특성 표 참고. 0이면 없음. 예: 독)

    [Header("수리 / 등급 (장비/도구의 내구도가 0이 되면 정보창의 [수리]로 고침)")]
    public List<Ingredient> repairIngredients = new List<Ingredient>(); // 수리에 필요한 재료 (제작 레시피처럼 여러 개. 비어 있으면 재료 없이 수리)
    [HideInInspector] public int repairItemId;   // (옛 값: 재료 하나짜리. 에디터가 repairIngredients로 옮겨 줌)
    [HideInInspector] public int repairAmount;
    public int repairRestore;     // 수리하면 차는 내구도 (0 이하면 가득 참)
    [Min(0)] public int repairSpCost;  // 수리할 때 드는 SP
    public int wearExp = 1;       // 내구도를 1 소모할 때마다 쌓이는 장비 경험치 (등급 표의 경험치로 등급이 오름)

    [Header("사용 효과 (소모품: 하나라도 0이 아니면 인벤토리에서 [사용] 버튼이 생김)")]
    public int useHp;          // 사용 시 체력 회복
    public int useSp;          // 사용 시 SP 회복
    public int useMana;        // 사용 시 마나 회복
    public int useResultItemId; // 사용 후 남는 아이템 ID (0이면 없음. 내구도를 이어받음)


    [Header("표시 / 제작")]
    public Sprite icon;        // 아이템 이미지
    public Recipe recipe;      // 제작법 (재료가 비어 있으면 제작 불가)

    // 닳거나 부서질 수 있는 아이템인가: 최대 내구도가 있고 장신구가 아니어야 한다 (장신구는 깨지지 않음)
    public bool CanBreak { get { return durabilitymax > 0 && equipSlot != EquipSlot.Accessory; } }

    // 수리에 드는 재료 목록 (옛 단일 재료 값도 인정)
    public List<Ingredient> RepairMaterials()
    {
        List<Ingredient> list = new List<Ingredient>();
        foreach (Ingredient ing in repairIngredients)
            if (ing != null && ing.itemId >= 0 && ing.amount > 0) list.Add(ing);
        if (list.Count == 0 && repairItemId > 0 && repairAmount > 0)
            list.Add(new Ingredient { itemId = repairItemId, amount = repairAmount });
        return list;
    }

    // 제작법은 "이 아이템을 만드는 방법"이므로 결과 아이템 id는 항상 자기 자신으로 맞춘다
    private void OnValidate()
    {
        if (recipe != null) recipe.resultItemId = id;
    }
}

[System.Serializable]
public class Ingredient
{
    public int itemId;   // 재료 아이템 ID (ItemDatabase에서 조회)
    public int amount;   // 필요 수량
}

[System.Serializable]
public class Recipe
{
    public List<Ingredient> ingredients = new List<Ingredient>(); // 필요 재료 목록

    // 필요한 도구: 제작할 때 내구도만 깎이고 사라지지는 않는다 (예: 간이 절구로 분쇄, 망치로 금속판 제작)
    public List<RecipeTool> tools = new List<RecipeTool>();

    [HideInInspector]
    public int resultItemId;      // 완성품 아이템 ID (Item.OnValidate가 자동으로 채움)
    public int resultAmount = 1;  // 1회 제작 시 생산 수량
    [Min(0)] public int spCost;   // 1회 제작에 드는 SP (제작 숙련도의 SP 감소가 적용됨)
}

// 레시피에 필요한 도구 한 줄
[System.Serializable]
public class RecipeTool
{
    public ToolType toolType;                // 필요한 도구 종류
    public int tier;                         // 필요한 도구 티어 (이 티어 이상이면 사용 가능)
    [Min(1)] public int durabilityCost = 1;  // 1회 제작에 깎이는 도구 내구도
}

// 도구 종류의 한글 이름
public static class ToolTypeInfo
{
    public static string Name(ToolType type)
    {
        switch (type)
        {
            case ToolType.Axe: return "도끼";
            case ToolType.Pickaxe: return "곡괭이";
            case ToolType.Hammer: return "망치";
            case ToolType.Sickle: return "낫";
            case ToolType.Bottle: return "수통";
            case ToolType.Knife: return "칼";
            case ToolType.Mortar: return "절구";
            default: return "도구";
        }
    }
}
