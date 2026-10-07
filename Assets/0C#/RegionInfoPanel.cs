using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; //  씬 이동을 위해 필수!

public class MapInfoPanel : MonoBehaviour
{
    [Header("UI 연결")]
    public Image regionImage;
    public TextMeshProUGUI regionNameText;
    public TextMeshProUGUI regionInfoText;
    public Button enterButton; //  진입(입장) 버튼

    public AudioSource WalkAudio;       // 진입 음원
    private Region currentData;

    void Awake()
    {
        gameObject.SetActive(false);
        
        // 진입 버튼에 클릭 이벤트 연결
        if (enterButton != null)
        {
            enterButton.onClick.AddListener(OnEnterButtonClick);
        }
    }

    // 구역 버튼을 눌렀을 때 실행되는 함수
    public void OpenMapInfo(Region data)
    {
        if (data == null) return;
        currentData = data;

        regionNameText.text = data.RegionName;

        // 이미지 적용
        if (data.regionImage != null)
        {
            regionImage.sprite = data.regionImage;
            regionImage.gameObject.SetActive(true);
        }

        // 텍스트 정보 조합
        StringBuilder sb = new StringBuilder();

        if (!string.IsNullOrEmpty(data.description))
        {
            sb.AppendLine(data.description);
            sb.AppendLine();
        }

        if (data.recommendedLevel > 0)
            sb.AppendLine($" 권장 레벨 : Lv.{data.recommendedLevel}");

        if (!string.IsNullOrEmpty(data.appearedMonsters))
            sb.AppendLine($" 등장 몬스터 : {data.appearedMonsters}");

        regionInfoText.text = sb.ToString();

        // 잠긴 지역이면 진입 버튼 비활성화
        if (enterButton != null)
        {
            enterButton.interactable = data.isUnlocked;
        }

        gameObject.SetActive(true);
    }

    //  진입하기 버튼을 눌렀을 때 실행
    public void OnEnterButtonClick()
    {
        WalkAudio.Play();
        if (currentData != null && !string.IsNullOrEmpty(currentData.sceneName))
        {
            // 이동할 지역의 ID를 기억해 둔다 (FieldManager의 지역 이름, FieldSearch의 몬스터 출현이 이 값을 사용)
            GameManager.selectedRegionID = currentData.id;

            //기존 SceneManager.LoadScene(...) 대신 FadeManager 사용!
            if (FadeManager.Instance != null)
            {
                FadeManager.Instance.LoadSceneWithFade(currentData.sceneName);
            }
            else
            {
                // 혹시 FadeManager가 없으면 비상용으로 직접 이동
                UnityEngine.SceneManagement.SceneManager.LoadScene(currentData.sceneName);
            }
        }
    }

    public void CloseMapInfo()
    {
        gameObject.SetActive(false);
    }
}