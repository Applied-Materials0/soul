using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 몬스터와 마주쳤을 때 화면 중앙 상단에 보이는 몬스터 이름과 체력 게이지.
// 게이지: 빈 부분은 빨간색, 남은 체력은 초록색이고, 게이지 위에 "현재 체력 / 최대 체력" 수치가 표시된다.
// 인스펙터 작업 없이 FieldSearch가 처음 필요할 때 Create()로 만든다.
public class MonsterHUD : MonoBehaviour
{
    private static readonly Color EmptyColor = new Color(0.78f, 0.12f, 0.12f, 1f); // 빈 게이지: 빨간색
    private static readonly Color FillColor = new Color(0.20f, 0.75f, 0.25f, 1f);  // 현재 체력: 초록색

    private TextMeshProUGUI nameText;
    private TextMeshProUGUI hpText;
    private RectTransform fill;

    // =========================================================
    //  생성
    // =========================================================
    public static MonsterHUD Create(Transform context, TMP_FontAsset font)
    {
        Canvas canvas = context.GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.rootCanvas.transform : context;

        // 가로 760, 세로 110의 틀을 화면 위쪽 가운데에 붙인다
        // (맨 위 50px는 지역 이름 텍스트가 쓰고 있어서 그 아래에 둔다)
        RectTransform root = CraftQuantityPopup.NewRect("MonsterHUD", parent, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(760f, 110f));
        MonsterHUD hud = root.gameObject.AddComponent<MonsterHUD>();
        hud.Build(root, font);
        root.gameObject.SetActive(false);
        return hud;
    }

    private void Build(RectTransform root, TMP_FontAsset font)
    {
        Vector2 top = new Vector2(0.5f, 1f);

        // 몬스터 이름
        nameText = CraftQuantityPopup.AddText(
            CraftQuantityPopup.NewRect("Name", root, top, Vector2.zero, new Vector2(760f, 56f)),
            44, TextAlignmentOptions.Center, font);
        nameText.fontStyle = FontStyles.Bold;

        // 게이지 바탕 = 빈 게이지(빨간색)
        RectTransform bar = CraftQuantityPopup.NewRect("Bar", root, top, new Vector2(0f, -62f), new Vector2(640f, 40f));
        bar.gameObject.AddComponent<Image>().color = EmptyColor;

        // 현재 체력 = 왼쪽부터 비율만큼 차는 초록색 막대
        fill = CraftQuantityPopup.NewRect("Fill", bar, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = FillColor;

        // 체력 수치 (막대 위에 그려지도록 마지막에 만든다)
        RectTransform hpRt = CraftQuantityPopup.NewRect("HpText", bar, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        hpRt.anchorMin = Vector2.zero;
        hpRt.anchorMax = Vector2.one;
        hpRt.offsetMin = Vector2.zero;
        hpRt.offsetMax = Vector2.zero;
        hpText = CraftQuantityPopup.AddText(hpRt, 26, TextAlignmentOptions.Center, font);
        hpText.fontStyle = FontStyles.Bold;
    }

    // =========================================================
    //  표시
    // =========================================================
    // 몬스터를 만났을 때: 이름과 체력을 표시하고 화면에 켠다
    public void Show(string monsterName, int hp, int hpMax)
    {
        nameText.text = monsterName;
        SetHp(hp, hpMax);
        gameObject.SetActive(true);
    }

    // 체력이 바뀔 때(전투에서 피해를 입을 때) 호출: 게이지와 수치를 갱신
    public void SetHp(int hp, int hpMax)
    {
        hpMax = Mathf.Max(hpMax, 1);
        hp = Mathf.Clamp(hp, 0, hpMax);

        // 초록색 막대의 오른쪽 끝을 비율에 맞춰 줄인다
        fill.anchorMax = new Vector2(hp / (float)hpMax, 1f);
        hpText.text = $"{hp} / {hpMax}";
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
