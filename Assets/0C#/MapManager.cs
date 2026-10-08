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

    // =========================================================
    //  키보드: 방향키로 구역을 고르고 Enter로 입장한다
    // =========================================================
    private MapRegionButton selectedRegion;

    void Update()
    {
        if (!IsMapOpen)
        {
            if (selectedRegion != null) { selectedRegion.SetHighlight(false); selectedRegion = null; }
            return;
        }

        int dx = 0, dy = 0;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) dx = -1;
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dx = 1;
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) dy = 1;
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) dy = -1;
        if (dx != 0 || dy != 0) MoveSelection(dx, dy);

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
        if (enter && selectedRegion != null)
        {
            // 마우스로 눌렀던 버튼이 선택 상태로 남아 있으면 Enter가 그 버튼도 누르므로 선택을 비운다
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

            MapInfoPanel info = selectedRegion.infoPanel;
            if (info != null) info.OnEnterButtonClick();
        }
    }

    private void MoveSelection(int dx, int dy)
    {
        MapRegionButton[] buttons = MapUI.GetComponentsInChildren<MapRegionButton>(false);
        if (buttons.Length == 0) return;
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

        // 처음이거나 사라졌으면 지금 있는 지역(없으면 첫 번째)부터 시작
        bool valid = false;
        foreach (MapRegionButton b in buttons) if (b == selectedRegion) valid = true;
        if (!valid)
        {
            MapRegionButton start = buttons[0];
            foreach (MapRegionButton b in buttons)
                if (b.regionData != null && b.regionData.id == GameManager.selectedRegionID) start = b;
            SelectRegion(start);
            return;
        }

        // 누른 방향으로 가장 가까운 구역 (방향에서 벗어난 만큼 불리하게 계산)
        MapRegionButton best = null;
        float bestScore = float.MaxValue;
        foreach (MapRegionButton b in buttons)
        {
            if (b == selectedRegion) continue;
            Vector3 d = b.transform.position - selectedRegion.transform.position;
            float along = d.x * dx + d.y * dy;
            if (along <= 0.001f) continue;
            float perp = Mathf.Abs(dx != 0 ? d.y : d.x);
            float score = along + perp * 2f;
            if (score < bestScore) { bestScore = score; best = b; }
        }
        if (best != null) SelectRegion(best);
    }

    private void SelectRegion(MapRegionButton b)
    {
        if (selectedRegion != null) selectedRegion.SetHighlight(false);
        selectedRegion = b;
        selectedRegion.SetHighlight(true);
        selectedRegion.Preview();
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

        // 3. 이전에 보던 구역 정보창은 닫고 시작 (이동한 뒤라 현재 위치가 바뀌었을 수 있음)
        CloseRegionInfo();

        ItemGainToast.ClearAll(); // 필드에서 얻고 남아 있던 획득 표시는 지도를 열면 바로 치운다

        // 4. UI 활성화
        MapUI.SetActive(true);
        PlayerUI.RefreshAll(); // 맵의 SP 텍스트를 지금 값으로
    }

    // 열려 있던 구역 정보창(입장 버튼이 있는 창)을 닫는다. 남아 있으면 이동 전의 구역 정보와 입장 버튼이 그대로 보인다.
    private void CloseRegionInfo()
    {
        if (MapUI == null) return;
        MapInfoPanel info = MapUI.GetComponentInChildren<MapInfoPanel>(true);
        if (info != null) info.CloseMapInfo();
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
        CloseRegionInfo();
    }

}
