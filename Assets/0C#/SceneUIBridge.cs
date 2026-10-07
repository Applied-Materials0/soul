using UnityEngine;

public class SceneUIBridge : MonoBehaviour
{
    public static SceneUIBridge Instance;
    public AudioSource BtnAudio;       // 버튼 음원


    void Awake()
    {
        // 씬이 이동될 때마다 새 씬의 Bridge로 자동 교체됨
        Instance = this;
    }


    //씬의 가방 버튼이 누를 함수
    public void ClickOpenInventory()
    {
        // 버튼음은 InventoryManager가 재생함 (여기서도 재생하면 소리가 두 번 남)
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ToggleInventory();
        }
        else
        {
            Debug.LogWarning("InventoryManager를 찾을 수 없습니다!");
        }
    }

    //씬의 지도 버튼이 누를 함수
    public void ClickOpenMap()
    {
        // 버튼음은 MapManager가 재생함 (여기서도 재생하면 소리가 두 번 남)
        if (MapManager.Instance != null)
        {
            MapManager.Instance.ToggleMap();
            if (PlayerUI.Instance != null) PlayerUI.Instance.UpdateSP();
        }
        else
        {
            Debug.LogWarning("MapManager를 찾을 수 없습니다!");
        }
    }
}