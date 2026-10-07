using UnityEngine;
using TMPro;

public class FieldManager : MonoBehaviour
{
    [Header("UI & 연출 요소")]
    public TextMeshProUGUI regionNameText; // 맵 이름 표시 UI
    //public SpriteRenderer backgroundRenderer; // 배경 이미지 출력용

    //[Header("지역별 배경 리소스")]
    //public Sprite forestSprite;
    //public Sprite dungeonSprite;
    //public Sprite waterfallSprite;

    void Start()
    {
        // 씬이 열리자마자 저장된 ID 값에 따라 화면 출력 설정
        InitField(GameManager.selectedRegionID);
    }

    void InitField(int regionID)
    {
        switch (regionID)
        {
            case 1:
                regionNameText.text = "포레스트";
                //backgroundRenderer.sprite = forestSprite;
                // 포레스트 전용 몬스터 스폰 로직 실행
                break;

            case 2:
                regionNameText.text = "폭포";
                //backgroundRenderer.sprite = dungeonSprite;
                // 던전 전용 몬스터 스폰 로직 실행
                break;

            case 3:
                regionNameText.text = "그라운드";
                //backgroundRenderer.sprite = waterfallSprite;
                // 폭포 전용 몬스터 스폰 로직 실행
                break;

            case 4:
                regionNameText.text = "광산";
                //backgroundRenderer.sprite = waterfallSprite;
                // 폭포 전용 몬스터 스폰 로직 실행
                break;

            case 5:
                regionNameText.text = "채석장";
                //backgroundRenderer.sprite = waterfallSprite;
                // 폭포 전용 몬스터 스폰 로직 실행
                break;

            case 6:
                regionNameText.text = "유적";
                //backgroundRenderer.sprite = waterfallSprite;
                // 폭포 전용 몬스터 스폰 로직 실행
                break;

            default:
                Debug.LogError("잘못된 지역 ID입니다!");
                break;
        }
    }
}