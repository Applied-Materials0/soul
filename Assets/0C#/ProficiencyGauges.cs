using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 필드 화면의 숙련도 게이지: 숙련도마다 한 줄씩, 경험치 수치 없이 게이지만 보여 준다 (수치는 가방의 숙련도창에서 확인).
// 빨간 막대 = 다음 숙련도 레벨까지 필요한 경험치, 초록 = 그중 지금 쌓은 비율.
//
// 모양은 씬의 ProficiencyGauges 오브젝트 안의 견본 줄(GaugeRow)을 고치면 된다. 견본 줄 구조:
//   GaugeRow (RectTransform) - Label (TextMeshProUGUI, "벌목 Lv.2") / Bar (Image, 빨강) - Fill (Image, 초록)
// 실행하면 견본은 숨겨지고, 숙련도 표의 숙련도 수만큼 복사본이 아래로 차례로 만들어진다.
public class ProficiencyGauges : MonoBehaviour
{
    public RectTransform rowTemplate;  // 견본 줄
    public float rowHeight = 30f;      // 줄 사이 간격

    private class Row
    {
        public ProficiencyDef def;
        public TextMeshProUGUI label;
        public RectTransform fill;
    }

    private readonly List<Row> rows = new List<Row>();

    void Start()
    {
        Build();
        PlayerUI.Refreshed += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        PlayerUI.Refreshed -= Refresh;
    }

    private void Build()
    {
        if (rowTemplate == null) return;
        rowTemplate.gameObject.SetActive(false);

        int i = 0;
        foreach (ProficiencyDef def in GameTables.Proficiency.defs)
        {
            if (def == null || def.levels.Count == 0) continue;

            RectTransform rt = Instantiate(rowTemplate, rowTemplate.parent);
            rt.gameObject.SetActive(true);
            rt.name = "Row " + def.label;
            rt.anchoredPosition = rowTemplate.anchoredPosition + new Vector2(0f, -rowHeight * i);

            Row row = new Row { def = def };
            Transform label = rt.Find("Label");
            if (label != null) row.label = label.GetComponent<TextMeshProUGUI>();
            Transform fill = rt.Find("Bar/Fill");
            if (fill != null) row.fill = fill as RectTransform;
            rows.Add(row);
            i++;
        }
    }

    private void Refresh()
    {
        foreach (Row row in rows)
        {
            int level = Proficiency.Level(row.def);
            int need = level >= 1 && level <= row.def.levels.Count ? row.def.levels[level - 1].expToNext : 0;
            float ratio = need > 0 ? Mathf.Clamp01(Proficiency.Exp(row.def) / (float)need) : 1f; // 최고 레벨이면 가득

            if (row.label != null) row.label.text = row.def.label + " Lv." + level;
            if (row.fill != null) row.fill.anchorMax = new Vector2(ratio, 1f);
        }
    }
}
