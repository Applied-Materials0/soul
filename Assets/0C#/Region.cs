using UnityEngine;

[System.Serializable]
public class Region
{
    [Header("기본 정보")]
    public int id;             // ID
    public string RegionName;    // 지역 이름
    public string sceneName;    //이동 씬 이름
    public Sprite regionImage;   // 구역 썸네일 이미지
    public int recommendedLevel; // 권장 레벨

    [Header("기본 설명")]
    [TextArea(2, 5)]
    public string description; 
    public string appearedMonsters; // 등장 몬스터
    [Header("레벨 및 난이도 경고")]
    public string Caution; // 경고글 작성
    public bool isUnlocked = true; // 해금 여부 (잠긴 지역 구분용)
}