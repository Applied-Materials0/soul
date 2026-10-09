using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 전투 중 [스킬] 버튼(S 키)을 누르면 뜨는 스킬 창. 모양은 Field 씬 하이어라키의 SkillPanel 아래 오브젝트를 직접 옮기고 고치면 된다.
//  - Window: 창 전체 (평소에는 꺼져 있고, 코드가 켠다)
//  - RowTemplate: 스킬 한 줄의 견본 (Label = 이름, Cost = 비용). 코드가 스킬 수만큼 복제해서 아래로 쌓는다. 견본의 위치가 첫 줄 위치.
//  - ResourceText: 현재 마나/SP,  DetailText: 고른 스킬의 설명과 쓸 수 없는 이유
// 키: W/S(위/아래) 고르기, Space/Enter 사용, ESC나 한 번 더 [스킬] 닫기. 마우스로 줄을 누르면 바로 쓴다.
// 스킬은 스킬 표(Soul > 데이터 표 > 스킬)에서 정한다.
public class SkillPanel : MonoBehaviour
{
    public static SkillPanel Instance { get; private set; }
    public static bool IsOpen { get { return Instance != null && Instance.window != null && Instance.window.activeSelf; } }

    public GameObject window;
    public RectTransform rowTemplate;
    public TextMeshProUGUI resourceText;
    public TextMeshProUGUI detailText;
    [Min(1)] public int visibleRows = 6;
    public float rowHeight = 72f;
    public float rowSpacing = 6f;

    public Color normalColor = new Color(0.22f, 0.22f, 0.26f, 0.95f);
    public Color selectedColor = new Color(0.55f, 0.45f, 0.15f, 0.95f);
    public Color blockedTextColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private readonly List<SkillDef> skills = new List<SkillDef>();
    private readonly List<GameObject> rows = new List<GameObject>();
    private int selected;
    private int scroll;
    private int openedFrame = -1;

    void Awake()
    {
        Instance = this;
        if (window != null)
        {
            Image back = window.GetComponent<Image>();
            if (back != null) back.raycastTarget = true; // 창 뒤의 버튼이 눌리지 않게
            window.SetActive(false);
        }
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ESC용: 열려 있으면 닫고 true
    public static bool CloseIfOpen()
    {
        if (!IsOpen) return false;
        Instance.Close();
        return true;
    }

    public void Toggle()
    {
        if (IsOpen) Close(); else Open();
    }

    public void Open()
    {
        if (window == null || rowTemplate == null || GameTables.Skills == null) return;

        skills.Clear();
        foreach (SkillDef s in GameTables.Skills.skills)
            if (s != null) skills.Add(s);
        skills.Sort((a, b) => a.id.CompareTo(b.id));

        foreach (GameObject r in rows) Destroy(r);
        rows.Clear();
        for (int i = 0; i < skills.Count; i++)
        {
            GameObject row = Instantiate(rowTemplate.gameObject, rowTemplate.parent);
            row.name = "Skill_" + skills[i].id;
            row.SetActive(true);
            Image img = row.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            Button b = row.GetComponent<Button>();
            if (b == null) b = row.AddComponent<Button>();
            b.targetGraphic = img;
            int index = i;
            b.onClick.AddListener(() => { selected = index; Refresh(); UseSelected(); });
            rows.Add(row);
        }

        selected = Mathf.Clamp(selected, 0, Mathf.Max(0, skills.Count - 1));
        scroll = 0;
        openedFrame = Time.frameCount;
        window.transform.SetAsLastSibling();
        window.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (window != null) window.SetActive(false);
    }

    void Update()
    {
        if (!IsOpen) return;

        // 전투가 끝났거나 가방/지도가 열리면 닫는다
        FieldSearch field = FieldSearch.Instance;
        bool uiOpen = (InventoryManager.Instance != null && InventoryManager.Instance.IsInventoryOpen)
            || (MapManager.Instance != null && MapManager.Instance.IsMapOpen);
        if (field == null || !field.InBattle || uiOpen) { Close(); return; }

        if (openedFrame == Time.frameCount) return; // 이 창을 연 키가 곧바로 선택을 움직이지 않게

        int move = 0;
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) move = -1;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) move = 1;
        if (move != 0 && skills.Count > 0)
        {
            selected = Mathf.Clamp(selected + move, 0, skills.Count - 1);
            SoundManager.Instance?.PlaySlotClickSound();
            Refresh();
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) UseSelected();
    }

    private void UseSelected()
    {
        FieldSearch field = FieldSearch.Instance;
        if (field == null || selected < 0 || selected >= skills.Count) return;
        if (field.UseSkill(skills[selected])) Close();
        else { SoundManager.Instance?.PlaySlotClickSound(); Refresh(); } // 쓸 수 없으면 이유가 설명에 보임
    }

    // 줄 모양(이름, 비용, 색)과 보이는 범위, 설명을 갱신
    private void Refresh()
    {
        FieldSearch field = FieldSearch.Instance;

        if (selected < scroll) scroll = selected;
        if (selected >= scroll + visibleRows) scroll = selected - visibleRows + 1;

        Vector2 start = rowTemplate.anchoredPosition;
        for (int i = 0; i < rows.Count; i++)
        {
            SkillDef s = skills[i];
            GameObject row = rows[i];
            bool inView = i >= scroll && i < scroll + visibleRows;
            row.SetActive(inView);
            if (!inView) continue;

            RectTransform rt = (RectTransform)row.transform;
            rt.anchoredPosition = new Vector2(start.x, start.y - (i - scroll) * (rowHeight + rowSpacing));

            string reason = field != null ? field.SkillBlockReason(s) : null;
            bool blocked = reason != null;

            Image img = row.GetComponent<Image>();
            if (img != null) img.color = i == selected ? selectedColor : normalColor;

            Transform label = row.transform.Find("Label");
            Transform cost = row.transform.Find("Cost");
            if (label != null)
            {
                TextMeshProUGUI t = label.GetComponent<TextMeshProUGUI>();
                string tag = s.weaponType != WeaponType.None ? $" [{WeaponTypeInfo.Name(s.weaponType)}]" : "";
                t.text = (GameManager.Level < s.unlockLevel ? $"{s.label}  (Lv.{s.unlockLevel})" : s.label) + tag;
                t.color = blocked ? blockedTextColor : Color.white;
            }
            if (cost != null)
            {
                TextMeshProUGUI t = cost.GetComponent<TextMeshProUGUI>();
                t.text = CostText(s);
                t.color = blocked ? blockedTextColor : Color.white;
            }
        }

        if (resourceText != null)
            resourceText.text = $"마나 {GameManager.Mana} / {GameManager.ManaMax}    SP {GameManager.SP} / {GameManager.SPMax}";

        if (detailText != null)
        {
            if (selected < 0 || selected >= skills.Count)
            {
                detailText.text = "배운 스킬이 없다.";
            }
            else
            {
                SkillDef s = skills[selected];
                string reason = field != null ? field.SkillBlockReason(s) : null;
                string text = s.label + "\n" + s.description;
                if (reason != null) text += $"\n<color=#FF6060>{reason}</color>";
                detailText.text = text;
            }
        }
    }

    // "마나 3  SP 2  쿨 1" 처럼 0이 아닌 비용만
    private static string CostText(SkillDef s)
    {
        List<string> parts = new List<string>();
        int mana = BattleSystem.SkillManaCost(s);
        if (mana > 0) parts.Add("마나 " + mana);
        if (s.spCost > 0) parts.Add("SP " + s.spCost);
        if (s.cooldown > 0) parts.Add("쿨 " + s.cooldown);
        return parts.Count > 0 ? string.Join("  ", parts) : "-";
    }
}
