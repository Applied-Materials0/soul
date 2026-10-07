using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MapRegionButton : MonoBehaviour
{
    public AudioSource BtnAudio;       // 버튼 음원
    [Header("구역 정보 데이터")]
    public Region regionData;

    [Header("정보창 연결")]
    public MapInfoPanel infoPanel;

    //  인스펙터 버튼(OnClick)에 연결할 매개변수 없는 함수 추가!
    public void ClickRegion()
    {
        BtnAudio.Play();
        if (regionData == null)
        {
            Debug.LogError($"[에러] {gameObject.name}에 regionData가 설정되지 않았습니다!");
            return;
        }

        if (infoPanel == null)
        {
            Debug.LogError($"[에러] {gameObject.name}에 infoPanel 연결이 빠졌습니다!");
            return;
        }

        // 정보창 열기 실행
        infoPanel.OpenMapInfo(regionData);
    }
}