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
    private string infoBaseText = ""; // 안내 문구 원본 (SP 부족 경고를 덧붙였다 지울 때 사용)
    private bool entering;            // 이동을 시작했는가 (같은 이동이 두 번 처리되는 것을 막음)
    private bool openRequested;       // OpenMapInfo가 이 창을 켜는 중인가

    void Awake()
    {
        // 처음에는 꺼진 상태로 시작한다. 단, 구역을 눌러 OpenMapInfo가 처음으로 이 창을 켜는 순간
        // (이때 Awake가 처음 실행됨)에는 꺼 버리면 안 된다. 꺼 버리면 처음에 두 번 눌러야 창이 떴음.
        if (!openRequested) gameObject.SetActive(false);

        // 진입 버튼에 클릭 이벤트 연결
        // 씬의 버튼 OnClick에 이미 이 함수가 연결돼 있으면 코드로 또 연결하지 않는다.
        // (둘 다 연결하면 한 번 클릭에 이동이 두 번 실행되어 SP가 두 배로 닳고 소리도 두 번 났음)
        if (enterButton != null && enterButton.onClick.GetPersistentEventCount() == 0)
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

        // 지금 있는 지역이면 이동할 필요가 없으므로 안내만 하고 입장 버튼은 숨긴다
        bool isHere = data.id == GameManager.selectedRegionID;
        if (isHere) sb.AppendLine(" 현재 위치한 지역입니다.");

        if (data.recommendedLevel > 0)
            sb.AppendLine($" 권장 레벨 : Lv.{data.recommendedLevel}");

        if (!string.IsNullOrEmpty(data.appearedMonsters))
            sb.AppendLine($" 등장 몬스터 : {data.appearedMonsters}");

        // 이동에 필요한 SP도 함께 안내 (루프 타운으로 돌아가는 이동은 SP를 쓰지 않음)
        int travelCost = TravelCost(data);
        if (travelCost > 0) sb.AppendLine($" 이동 SP : {travelCost}");

        infoBaseText = sb.ToString();
        regionInfoText.text = infoBaseText;

        // 지금 있는 지역에서는 입장 버튼을 숨기고, 잠긴 지역이면 진입 버튼 비활성화
        entering = false;
        if (enterButton != null)
        {
            enterButton.gameObject.SetActive(!isHere);
            enterButton.interactable = data.isUnlocked;
        }

        openRequested = true; // 아직 Awake 전이라면, 이 SetActive로 실행되는 Awake가 창을 다시 끄지 않게 함
        gameObject.SetActive(true);
    }

    //  진입하기 버튼을 눌렀을 때 실행
    public void OnEnterButtonClick()
    {
        // 이미 이동을 시작했거나 지금 있는 지역이면 무시 (SP가 두 번 닳는 것을 막는 안전장치)
        if (entering || currentData == null || currentData.id == GameManager.selectedRegionID) return;
        if (!currentData.isUnlocked) return; // 잠긴 지역은 키보드 Enter로도 들어갈 수 없다

        if (!string.IsNullOrEmpty(currentData.sceneName))
        {
            // 지역 이동 SP (SP 소모 표). 모자라면 이동하지 못함
            int travelCost = TravelCost(currentData);
            if (GameManager.SP < travelCost)
            {
                regionInfoText.text = infoBaseText + $"\n<color=#FF5555>SP가 부족합니다! (필요 {travelCost})</color>";
                return;
            }

            entering = true; // 여기부터는 이동이 시작됨 (한 번만 처리)
            GameManager.SP -= travelCost;
            if (PlayerUI.Instance != null) PlayerUI.Instance.UpdateStatText();

            WalkAudio.Play();

            // 이동할 지역의 ID를 기억해 둔다 (FieldManager의 지역 이름, FieldSearch의 몬스터 출현이 이 값을 사용)
            GameManager.selectedRegionID = currentData.id;
            RegionTitle.Queue(currentData.RegionName); // 도착해서 화면이 밝아지면 지역 이름을 크게 보여 줌

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

    // 이동에 드는 SP. 루프 타운(지역 ID 0)으로 돌아가는 이동은 SP가 모자라도 갈 수 있도록 0으로 둔다
    private static int TravelCost(Region data)
    {
        if (data == null || data.id == 0) return 0;
        return GameTables.SPCosts.Get(SPAction.Travel);
    }

    public void CloseMapInfo()
    {
        gameObject.SetActive(false);
    }
}