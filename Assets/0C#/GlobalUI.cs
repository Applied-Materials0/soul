using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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
        if (Input.GetKeyDown(KeyCode.Tab) && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ToggleInventory();
        }

        // 지도 단축키
        if (Input.GetKeyDown(KeyCode.M) && MapManager.Instance != null)
        {
            MapManager.Instance.ToggleMap();
        }

        // ESC 키 (공통 닫기)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapeKey();
        }
    }

    // 마우스로 누른 버튼이 계속 "선택된 버튼"으로 남으면, 나중에 Space/Enter(제출 키)가 그 버튼을 또 눌러 버린다
    // (예: 필드에서 누른 [도망] 버튼이 가방에서 Space로 아이템을 쓸 때 같이 눌림). 그래서 선택을 계속 비워 둔다.
    // 글자 입력칸(수량 입력)은 선택이 있어야 입력되므로 건드리지 않는다.
    void LateUpdate()
    {
        EventSystem es = EventSystem.current;
        if (es == null || es.currentSelectedGameObject == null) return;
        if (es.currentSelectedGameObject.GetComponent<TMP_InputField>() != null) return;
        es.SetSelectedGameObject(null);
    }

    // ESC: 가장 위에 있는 창부터 한 겹씩 닫음 (제작 팝업 -> 레시피 창 -> 가방 -> 지도)
    // 인스펙터 연결(inventoryManager)이 비어 있어도 동작하도록 InventoryManager.Instance를 직접 사용함
    private void HandleEscapeKey()
    {
        InventoryManager inv = InventoryManager.Instance;
        MapManager map = MapManager.Instance;

        if (inv != null && inv.CloseTopCraftLayer()) return;

        if (inv != null && inv.IsInventoryOpen)
        {
            inv.CloseInventory();
        }
        else if (map != null && map.IsMapOpen)
        {
            map.CloseMap();
        }
    }

    //FieldSearch.cs에서 RegisterUI(this)를 부를 때 실행될 함수
    public void RegisterUI(FieldSearch newFieldSearch)
    {
        fieldSearch = newFieldSearch;
    }

}