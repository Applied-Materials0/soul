using UnityEngine;
using UnityEngine.UI;

public enum FieldButtonType { Search, Gather, Run, Defence, Attack, Skill, Map, Bag }

public class FieldActionButton : MonoBehaviour
{
    [SerializeField] private FieldButtonType buttonType;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void Start()
    {
        //Listeners가 버튼 인스펙터에서 On Click () 참조 개체
        button.onClick.RemoveAllListeners();
        // 실행 시 코드로 자동 연결되므로 On Click ()에 자기 자신을 넣지 않아도 됨
        button.onClick.AddListener(OnButtonClick);
    }

    // 키보드 단축키용: 이 종류의 버튼이 지금 화면에 켜져 있고 누를 수 있으면 누른다. 눌렀으면 true
    public static bool Press(FieldButtonType type)
    {
        foreach (FieldActionButton b in FindObjectsByType<FieldActionButton>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (b.buttonType != type || b.button == null || !b.button.IsActive() || !b.button.interactable) continue;
            b.button.onClick.Invoke();
            return true;
        }
        return false;
    }

    private void OnButtonClick()
    {
        if (FieldSearch.Instance == null) return;

        switch (buttonType)
        {
            case FieldButtonType.Search:
                FieldSearch.Instance.SearchBtnOn();
                break;
            case FieldButtonType.Gather:
                FieldSearch.Instance.GatherBtnOn();
                break;
            case FieldButtonType.Run:
                FieldSearch.Instance.RunBtnOn();
                break;
            case FieldButtonType.Defence:
                FieldSearch.Instance.DefenceBtnOn();
                break;
            case FieldButtonType.Attack:
                FieldSearch.Instance.AttackBtnOn();
                break;
            case FieldButtonType.Skill:
                FieldSearch.Instance.SkillBtnOn();
                break;
            case FieldButtonType.Map:
                MapManager.Instance.OpenMap();
                break;
            case FieldButtonType.Bag:
                InventoryManager.Instance.OpenInventory();
                break;
        }
    }
}