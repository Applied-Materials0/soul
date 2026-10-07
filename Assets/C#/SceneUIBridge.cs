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
        BtnAudio.Play();
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
        BtnAudio.Play();
        Debug.Log("맵 SP 텍스트 갱신 요청 정상");
        if (MapManager.Instance != null)
        {
            MapManager.Instance.ToggleMap();
            PlayerUI.Instance.UpdateSP();
        }
        else
        {
            Debug.LogWarning("MapManager를 찾을 수 없습니다!");
        }
    }
}