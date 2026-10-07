using UnityEngine;
using TMPro;

public class PlayerUI : MonoBehaviour
{
    public static PlayerUI Instance;

    public TextMeshProUGUI statText;
    public TextMeshProUGUI SPText;

    void Awake()
    {
        // 1. 어디서든 PlayerUI.Instance 로 접근할 수 있게 할당
        Instance = this;
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

    public void UpdateStatText()
    {
        int level = GameManager.Level;
        int exp = GameManager.Exp;
        int hp = GameManager.Hp;
        float hpmax = GameManager.HpMax;
        int at = GameManager.At;
        int df = GameManager.Df;
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

        //statText.text = $"<b><size=120%>LV: {level:N0}</size> / {exp:N0} \n HP: {hp:N0} / {hpmax:N0} </b> 마나: {mana:N0} / {manamax:N0} \n AT: {at:N0} DF: {df:N0} \n 고정 데미지: {fixat:N0} 방어 관통: {breakdf:N0}% \n 체력 퍼뎀: {hprateat:N0}% 체력 흡수: {abs:N0}% 회피율: {avoid:N0}% \n 치명타 확률: {criticalrate:N0}% 치명타 데미지 {critical:N0}% ";

        //1. statText가 연결되어 있을 때만 갱신 (비어있어도 에러 안 남!)
        if (statText != null)
        {
            statText.text = $"<b><size=120%>LV: {level:N0}</size> / {exp:N0} \n HP: {hp:N0} / {hpmax:N0} </b>   \n SP: {sp:N0} / {spmax:N0} \n 마나: {mana:N0} / {manamax:N0} \n AT: {at:N0}    DF: {df:N0} \n 고정 데미지: {fixat:N0} 방어 관통: {breakdf:N0}% \n 체력 퍼뎀: {hprateat:N0}% 체력 흡수: {abs:N0}% 회피율: {avoid:N0}% \n 치명타 확률: {criticalrate:N0}% 치명타 데미지 {critical:N0}% ";
        }
    }

    public void UpdateSP()
    {
        int sp = GameManager.SP;
        int spmax = GameManager.SPMax;
        Debug.Log("맵 SP 텍스트 갱신 정상");
        SPText.text = $"SP: {sp:N0} / {spmax:N0}";
        Debug.Log(GameManager.SP);
    }
}