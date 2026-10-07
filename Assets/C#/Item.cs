using UnityEngine;
using System.Collections.Generic; // 리스트 사용

public enum ToolType
{
    None,       // 도구 아님/자원
    Axe,        // 도끼: 나무
    Pickaxe,    // 곡괭이: 광물
    Hammer,     // 망치: 돌
    Sickle,     // 낫: 풀, 작물
    Bottle   // 물통
}

[System.Serializable]
public class Item
{
    [Header("기본 정보")]
    public int id;             // ID
    public string itemName;    // 아이템 이름
    public int weight;         // 무게
    public int countmax;       // 최대 개수
    public int durability;     // 내구도
    public int durabilitymax;  // 최대 내구도

    [Header("도구 분류")]
    public ToolType toolType;  // 도구 종류 설정
    public int tier;           //아이템 티어
    public int toolattack;     //자원 채집 전용 공격력

    [Header("기본 설명")]
    [TextArea(2, 5)]
    public string description;
    [Header("특수 기믹 설명 (예: 공격 시 마나 회복)")]
    public string specialEffect; // 텍스트로 기믹 적는 칸!

    [Header("아이템 능력치")]  //**소문자**===========
    public int at;             // 공격력
    public int df;             // 방어력
    public int hp;             // 체력
    public int heal;           // 회복량
    public float hprateat;     //체력 공격력 (체력 비례 데미지)
    public int fixat;          //고정 공격력 (고정뎀)
    public float breakdf;      //관통률      (방어 관통률
    public float abs;          //흡수율      (체력 흡수율
    public float avoid;        //회피율
    public float criticalrate; //치명타 확률
    public float critical;     //치명타 배율
    public int manamax;        //최대 마나 증가
    public int manaheal;       //마나 회복량
    public float atrate;       //공격력 배율
    public float dfrate;       //방어력 배율
    public float hprate;       //체력 배율
    public float healrate;     // 추가 회복량 배율
    public float goldrate;     //골드 배율
    public float exprate;      //경험치 배율
    public int spmax;          //최대 스태미나 증가
    public int spheal;         //스태미나 회복
    public int weightmax;      //가방 무게 증량
    public Sprite icon;        //아이콘 이미지
    public Recipe recipe;      //레시피 함수 변수


    // 내구도 감소 메서드 (부서졌으면 true 반환)
    public bool UseDurability(int amount)
    {
        durability -= amount;
        if (durability <= 0)
        {
            durability = 0;
            return true; // 파괴됨
        }
        return false; // 아직 사용 가능
    }
}

[System.Serializable]
public class Ingredient
{
    // 기존의 int itemIndex 대신 Item 객체를 직접 받습니다.
    public Item item;
    public int amount;
}

[System.Serializable]
public class Recipe
{
    public List<Ingredient> ingredients = new List<Ingredient>(); // 필요 재료 목록

    // 기존의 int resultItemIndex 대신 Item 객체를 직접 받습니다.
    public Item resultItem;
    public int resultAmount = 1;  // 1회 제작 시 생성 개수
}