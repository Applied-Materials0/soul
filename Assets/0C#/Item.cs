using UnityEngine;
using System.Collections.Generic;

public enum ToolType
{
    None,       // 도구 아님/자원
    Axe,        // 도끼: 벌목
    Pickaxe,    // 곡괭이: 채광
    Hammer,     // 망치
    Sickle,     // 낫: 풀, 작물
    Bottle      // 병: 물
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
    public int heal;           // 회복량
    public float hprateat;     // 체력 비례 공격력
    public int fixat;          // 고정 공격력
    public float breakdf;      // 방어 무시
    public float abs;          // 흡혈
    public float avoid;        // 회피
    public float criticalrate; // 치명타 확률
    public float critical;     // 치명타 피해
    public int manamax;        // 최대 마나 증가
    public int manaheal;       // 마나 회복량
    public float atrate;       // 공격력 비율
    public float dfrate;       // 방어력 비율
    public float hprate;       // 체력 비율
    public float healrate;     // 회복량 비율
    public float goldrate;     // 골드 비율
    public float exprate;      // 경험치 비율
    public int spmax;          // 최대 스태미나 증가
    public int spheal;         // 스태미나 회복
    public int weightmax;      // 소지 무게 증가

    [Header("표시 / 제작")]
    public Sprite icon;        // 아이템 이미지
    public Recipe recipe;      // 제작법 (재료가 비어 있으면 제작 불가)

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

    [HideInInspector]
    public int resultItemId;      // 완성품 아이템 ID (Item.OnValidate가 자동으로 채움)
    public int resultAmount = 1;  // 1회 제작 시 생산 수량
}
