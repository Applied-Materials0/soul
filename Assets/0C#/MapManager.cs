using System.Collections.Generic;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    //정적(static) 인스턴스 변수 선언
    public static MapManager Instance;
    public AudioSource BtnAudio;       // 버튼 음원

    [Header("UI 연결")]
    public GameObject MapUI;     // 지도 UI Panel/Canvas
    public Transform contentTransform;  // Scroll View -> Viewport -> Content

    //맵 UI가 켜져 있는지 여부를 반환하는 읽기 전용 프로퍼티
    public bool IsMapOpen => MapUI != null && MapUI.activeSelf;

    void Awake()
    {
        //싱글톤 초기화 (어디서든 MapManager.Instance로 접근 가능하게 만듦)
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 게임 시작 시 지도는 꺼둔 채로 시작
        if (MapUI != null)
        {
            MapUI.SetActive(false);
        }
    }

    public void ToggleMap()
    {
        // Toggle에서 직접 SetActive를 하지 않고 Open/Close로 넘겨줌
        if (MapUI.activeSelf)
        {
            CloseMap();
        }
        else
        {
            OpenMap();
        }
    }

    public void OpenMap()
    {
        // 1. 지도를 열 때 인벤토리가 열려있다면 닫기
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.CloseInventoryQuiet(); // 소리는 아래에서 한 번만
        }

        // 2. 소리 재생
        if (BtnAudio != null) BtnAudio.Play();

        // 3. UI 활성화
        MapUI.SetActive(true);
    }

    // 지도 닫기 (X 버튼이 인스펙터에서 이 함수를 이름으로 부르므로, 매개변수를 추가하면 안 됨!)
    public void CloseMap()
    {
        CloseMapInternal(true);
    }

    // 소리 없이 닫기: 씬 이동처럼 다른 소리가 이미 나는 경우에 사용
    public void CloseMapQuiet()
    {
        CloseMapInternal(false);
    }

    // 이미 닫혀 있으면 아무 소리도 내지 않음
    private void CloseMapInternal(bool playSound)
    {
        if (MapUI == null || !MapUI.activeSelf) return;

        // 1. 소리 재생
        if (playSound && BtnAudio != null) BtnAudio.Play();

        // 2. UI 비활성화
        MapUI.SetActive(false);
    }

}
