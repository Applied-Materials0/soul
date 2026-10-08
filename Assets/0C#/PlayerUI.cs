using System.Collections.Generic;
using UnityEngine;
using TMPro;

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

    // 모든 PlayerUI의 스탯 텍스트와 SP 텍스트를 지금 값으로 갱신
    public static void RefreshAll()
    {
        for (int i = all.Count - 1; i >= 0; i--)
        {
            if (all[i] == null) { all.RemoveAt(i); continue; }
            all[i].Redraw();
        }
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
        string atText = Mathf.RoundToInt(at * BattleCalc.Mult(GameManager.AtRate)).ToString("N0") + RateNote(GameManager.AtRate);
        string dfText = Mathf.RoundToInt(df * BattleCalc.Mult(GameManager.DfRate)).ToString("N0") + RateNote(GameManager.DfRate);
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
            statText.text = $"<b><size=120%>LV: {level:N0}</size> / EXP {exp:N0}{(GameManager.ExpNext > 0 ? " / " + GameManager.ExpNext.ToString("N0") : " (MAX)")} \n HP: {hp:N0} / {hpmax:N0} </b>   \n SP: {sp:N0} / {spmax:N0}{BagStatusLine()} \n 마나: {mana:N0} / {manamax:N0} \n AT: {atText}    DF: {dfText} \n 고정 데미지: {fixat:N0} 방어 관통: {breakdf:N0}% \n 체력 퍼뎀: {hprateat:N0}% 체력 흡수: {abs:N0}% 회피율: {avoid:N0}% \n 치명타 확률: {criticalrate:N0}% 치명타 데미지 {critical:N0}% ";
        }

        // SP 텍스트(맵 화면)가 연결되어 있으면 함께 갱신
        if (SPText != null)
        {
            SPText.text = $"SP: {sp:N0} / {spmax:N0}";
        }
    }
}