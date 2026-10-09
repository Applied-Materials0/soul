using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    public static PlayerUI Instance;

    // 씬마다 PlayerUI가 따로 있고(예: 필드 화면의 스탯 텍스트), 마을에서 만든 PlayerUI는 씬이 바뀌어도 남아 있다
    // (가방의 스탯 창, 맵의 SP 텍스트). Instance는 가장 나중에 만들어진 하나뿐이라, 모든 PlayerUI를 따로 기억해 두고
    // 갱신할 때 전부 갱신한다. 그렇지 않으면 필드에서 가방 스탯 창과 맵 SP 텍스트가 갱신되지 않는다.
    private static readonly List<PlayerUI> all = new List<PlayerUI>();

    public TextMeshProUGUI statText;
    public TextMeshProUGUI SPText;

    void Awake()
    {
        // 1. 어디서든 PlayerUI.Instance 로 접근할 수 있게 할당
        Instance = this;
        if (!all.Contains(this)) all.Add(this);
    }

    void OnDestroy()
    {
        all.Remove(this);
        if (Instance == this) Instance = all.Count > 0 ? all[all.Count - 1] : null;
    }

    // 갱신이 끝날 때마다 (필드의 숙련도 게이지 등이 따라서 갱신됨)
    public static event System.Action Refreshed;

    // 모든 PlayerUI의 스탯 텍스트와 SP 텍스트를 지금 값으로 갱신
    public static void RefreshAll()
    {
        Exhaustion.Observe(); // SP가 0에 닿았는지 (탈진 스택)
        for (int i = all.Count - 1; i >= 0; i--)
        {
            if (all[i] == null) { all.RemoveAt(i); continue; }
            all[i].Redraw();
        }
        Refreshed?.Invoke();
    }

    void OnEnable()
    {
        // 2. 인벤토리나 스탯창이 켜질 때마다 알아서 최신 GameManager 스탯으로 갱신!
        UpdateStatText();
    }

    void Start()
    {
        UpdateStatText();
    }

    // 예전부터 쓰던 갱신 함수. 이제 모든 PlayerUI를 갱신한다.
    public void UpdateStatText()
    {
        RefreshAll();
    }

    public void UpdateSP()
    {
        RefreshAll();
    }

    // 가방 상태 한 줄: 소지 무게와 슬롯 사용량 (인벤토리가 없으면 비움)
    private static string BagStatusLine()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return "";
        return $"\n 무게: {inv.CurrentWeight:N0} / {inv.WeightLimit:N0}   슬롯: {inv.UsedSlots} / {inv.SlotLimit}";
    }

    // =========================================================
    //  내 체력 게이지: 스탯 텍스트 바로 위에 붙는다 (필드의 스탯 텍스트, 가방의 스탯 창 모두).
    //  몬스터 체력 게이지와 같은 모양: 빈 부분은 빨간색, 현재 체력만큼 왼쪽부터 초록색
    // =========================================================
    private RectTransform hpBarRoot;
    private RectTransform hpBarFill;
    private bool hpBarFromHierarchy;
    private TextMeshProUGUI hpBarText;
    private static readonly Color HpEmptyColor = new Color(0.75f, 0.15f, 0.15f, 1f);
    private static readonly Color HpFillColor = new Color(0.20f, 0.75f, 0.25f, 1f);

    // 하이어라키에 "PlayerHpBar" 오브젝트(자식: Fill, Text)가 있으면 그것을 쓴다 (스탯 텍스트와 같은 부모 아래).
    // 위치/크기/색은 그 오브젝트에서 직접 고치면 된다. 없을 때만 아래에서 대신 만든다.
    private bool FindHpBar()
    {
        Transform parent = statText.transform.parent;
        Transform bar = parent != null ? parent.Find("PlayerHpBar") : null;
        if (bar == null) return false;
        Transform fill = bar.Find("Fill");
        Transform text = bar.Find("Text");
        if (fill == null) return false;
        hpBarRoot = bar as RectTransform;
        hpBarFill = fill as RectTransform;
        hpBarText = text != null ? text.GetComponent<TextMeshProUGUI>() : null;
        hpBarFromHierarchy = true;
        return true;
    }

    private void BuildHpBar()
    {
        if (FindHpBar()) return;
        hpBarRoot = CraftQuantityPopup.NewRect("PlayerHpBar", statText.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 26f));
        hpBarRoot.pivot = new Vector2(0f, 0f);
        Image back = hpBarRoot.gameObject.AddComponent<Image>();
        back.color = HpEmptyColor;
        back.raycastTarget = false;

        hpBarFill = CraftQuantityPopup.NewRect("Fill", hpBarRoot, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        hpBarFill.anchorMin = Vector2.zero;
        hpBarFill.anchorMax = Vector2.one;
        hpBarFill.offsetMin = Vector2.zero;
        hpBarFill.offsetMax = Vector2.zero;
        Image fill = hpBarFill.gameObject.AddComponent<Image>();
        fill.color = HpFillColor;
        fill.raycastTarget = false;

        RectTransform textRt = CraftQuantityPopup.NewRect("Text", hpBarRoot, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        hpBarText = CraftQuantityPopup.AddText(textRt, 20, TextAlignmentOptions.Center, statText.font);
        hpBarText.fontStyle = FontStyles.Bold;
        hpBarText.outlineWidth = 0.25f;
        hpBarText.outlineColor = new Color32(0, 0, 0, 255);
    }

    private void UpdateHpBar(int hp, int hpMax)
    {
        if (hpBarRoot == null) BuildHpBar();

        float ratio = hpMax > 0 ? Mathf.Clamp01(hp / (float)hpMax) : 0f;
        hpBarFill.anchorMax = new Vector2(ratio, 1f);
        if (hpBarText != null) hpBarText.text = $"HP {hp:N0} / {hpMax:N0}";
        if (hpBarFromHierarchy) return; // 하이어라키 오브젝트의 위치는 건드리지 않는다

        // 스탯 글자 덩어리의 왼쪽 위 모서리 바로 위에 놓는다
        statText.ForceMeshUpdate();
        Bounds b = statText.textBounds;
        hpBarRoot.localPosition = new Vector3(b.min.x, b.max.y + 10f, 0f);
    }

    // =========================================================
    //  경험치 바: 레벨 줄 바로 아래. 빨간 막대 = 다음 레벨까지 필요한 경험치, 초록 = 그중 지금 쌓은 비율.
    //  하이어라키의 "PlayerExpBar"(자식: Fill) 오브젝트를 쓴다 (스탯 텍스트와 같은 부모 아래). 위치/크기/색은 그 오브젝트에서 직접 고친다.
    // =========================================================
    private RectTransform expBarFill;
    private bool expBarSearched;

    private void UpdateExpBar(int exp)
    {
        if (!expBarSearched)
        {
            expBarSearched = true;
            Transform parent = statText.transform.parent;
            Transform bar = parent != null ? parent.Find("PlayerExpBar") : null;
            Transform fill = bar != null ? bar.Find("Fill") : null;
            expBarFill = fill as RectTransform;
        }
        if (expBarFill == null) return;

        int next = GameManager.ExpNext;
        float ratio = next > 0 ? Mathf.Clamp01(exp / (float)next) : 1f; // 최고 레벨이면 가득
        expBarFill.anchorMax = new Vector2(ratio, 1f);
    }

    // 증감률이 0이 아니면 "(+30%)" 같은 꼬리표 (늘면 초록, 줄면 빨강)
    private static string RateNote(float rate)
    {
        if (Mathf.Approximately(rate, 0f)) return "";
        string color = rate > 0f ? "#1B7F2A" : "#C00000";
        return $" <color={color}>({rate:+0.#;-0.#}%)</color>";
    }

    // 이 PlayerUI에 연결된 텍스트를 그린다
    private void Redraw()
    {
        int level = GameManager.Level;
        int exp = GameManager.Exp;
        int hp = GameManager.Hp;
        float hpmax = BattleCalc.PlayerMaxHp();
        int at = GameManager.At + GameManager.EquipAt;
        int df = GameManager.Df + GameManager.EquipDf;
        // 공격력/방어력은 장비와 특성(독 등)의 증감률까지 반영한 실제 값으로 보여 준다
        string atText = Mathf.RoundToInt(at * BattleCalc.Mult(BattleCalc.TotalAtRate())).ToString("N0") + RateNote(BattleCalc.TotalAtRate());
        string dfText = Mathf.RoundToInt(df * BattleCalc.Mult(BattleCalc.TotalDfRate())).ToString("N0") + RateNote(BattleCalc.TotalDfRate());
        int fixat = GameManager.FixAt;
        float breakdf = GameManager.BreakDf;
        float hprateat = GameManager.HpRateAt;
        float abs = GameManager.Abs;
        float avoid = GameManager.Avoid;
        float critical = GameManager.Critical;
        float criticalrate = GameManager.CriticalRate;
        int mana = GameManager.Mana;
        int manamax = GameManager.ManaMax;
        int sp = GameManager.SP;
        int spmax = GameManager.SPMax;

        //statText.text = $"<b><size=120%>LV: {level:N0}</size> / EXP {exp:N0}{(GameManager.ExpNext > 0 ? " / " + GameManager.ExpNext.ToString("N0") : " (MAX)")} \n HP: {hp:N0} / {hpmax:N0} </b> 마나: {mana:N0} / {manamax:N0} \n AT: {at:N0} DF: {df:N0} \n 고정 데미지: {fixat:N0} 방어 관통: {breakdf:N0}% \n 체력 퍼뎀: {hprateat:N0}% 체력 흡수: {abs:N0}% 회피율: {avoid:N0}% \n 치명타 확률: {criticalrate:N0}% 치명타 데미지 {critical:N0}% ";

        //1. statText가 연결되어 있을 때만 갱신 (비어있어도 에러 안 남!)
        if (statText != null)
        {
            statText.text = $"<b><size=120%>LV: {level:N0}</size></b>\n \n \nSP: {sp:N0} / {spmax:N0}{BagStatusLine()} \n 마나: {mana:N0} / {manamax:N0} \n AT: {atText}    DF: {dfText} \n 고정 데미지: {fixat:N0} 방어 관통: {breakdf:N0}% \n 체력 퍼뎀: {hprateat:N0}% 체력 흡수: {abs:N0}% 회피율: {avoid:N0}% \n 치명타 확률: {criticalrate:N0}% 치명타 데미지 {critical:N0}% ";
        }

        if (statText != null) UpdateHpBar(hp, Mathf.RoundToInt(hpmax));
        if (statText != null) UpdateExpBar(exp);

        // SP 텍스트(맵 화면)가 연결되어 있으면 함께 갱신
        if (SPText != null)
        {
            SPText.text = $"SP: {sp:N0} / {spmax:N0}";
        }
    }
}
