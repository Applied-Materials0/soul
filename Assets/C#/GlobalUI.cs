using UnityEngine;

public class GlobalUI : MonoBehaviour
{
    public static GlobalUI Instance;

    [Header("관리할 매니저 연결")]
    public InventoryManager inventoryManager; 

    //새로 진입한 FieldSearch의 참조를 담아둘 변수
    public FieldSearch fieldSearch { get; private set; }

    void Awake()
    {
        // 1. 최상위 부모로 분리 (DontDestroyOnLoad 정상 작동 보장)
        transform.SetParent(null);

        // 2. 씬 이동 시 중복 생성 방지 (싱글톤 패턴)
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 부모가 보존되므로 자식(Canvas, 매니저) 전체가 보존됨!

        }
        else
        {
            // 이미 다른 씬에서 넘어온 Global_UI가 있다면 새로 생긴 건 파괴
            Destroy(gameObject);
        }
    }
    void Update()
    {
        // 인벤토리 단축키
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            InventoryManager.Instance.ToggleInventory();
        }

        // 지도 단축키
        if (Input.GetKeyDown(KeyCode.M))
        {
            MapManager.Instance.ToggleMap();
        }

        // ESC 키 (공통 닫기)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (InventoryManager.Instance != null && InventoryManager.Instance.IsInventoryOpen)
            {
                HandleEscapeKey();
            }
                
        }
    }
    private void HandleEscapeKey()
    {
        // 우선순위에 따라 원하는 창부터 순서대로 닫기
        if (inventoryManager != null && inventoryManager.IsInventoryOpen)
        {
            inventoryManager.CloseInventory();
        }
        else if (MapManager.Instance != null && MapManager.Instance.IsMapOpen)
        {
            MapManager.Instance.CloseMap();
        }
    }

    //FieldSearch.cs에서 RegisterUI(this)를 부를 때 실행될 함수
    public void RegisterUI(FieldSearch newFieldSearch)
    {
        fieldSearch = newFieldSearch;
        Debug.Log("FieldSearch가 GlobalUI에 성공적으로 등록되었습니다.");
    }

}