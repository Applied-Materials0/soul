using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 마을 화면 오른쪽 위의 날짜/시간대/날씨 글자와 [휴식] 버튼을 이어 준다.
// 글자와 버튼 위치/색은 LoopTown 씬의 TimePanel 오브젝트(DateText, WeatherText)와 Rest_Button에서 직접 고치면 된다.
public class TimeHUD : MonoBehaviour
{
    public TextMeshProUGUI dateText;     // "1일 오전"
    public TextMeshProUGUI weatherText;  // "맑음"
    public Button restButton;            // 휴식 버튼 (누르면 다음 시간대로)

    void OnEnable()
    {
        GameTime.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        GameTime.Changed -= Refresh;
    }

    void Start()
    {
        if (restButton != null) restButton.onClick.AddListener(OnClickRest);
        Refresh();
    }

    // R 키로도 휴식한다 (가방/지도가 열려 있거나 연출 중에는 쓰지 않음)
    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.R)) return;
        if (InventoryManager.Instance != null && InventoryManager.Instance.IsInventoryOpen) return;
        if (MapManager.Instance != null && MapManager.Instance.IsMapOpen) return;
        OnClickRest();
    }

    // 화면이 검게 변하는 동안 시간이 흐르고 회복한다 (RestSequence)
    private void OnClickRest()
    {
        if (RestSequence.Playing || FaintSequence.Playing) return;
        SoundManager.Instance?.PlaySlotClickSound();
        RestSequence.Play(GameTime.Rest);
    }

    private void Refresh()
    {
        if (dateText != null) dateText.text = GameTime.Day + "일 " + GameTime.PeriodName(GameTime.Period);
        if (weatherText != null) weatherText.text = GameTime.WeatherName(GameTime.Weather);
    }
}
