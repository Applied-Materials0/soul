using UnityEditor;
using UnityEngine;

// "규칙 표"(SP 소모, 레벨, 숙련도)를 그리는 도구. Soul > 데이터 표 창의 [레벨], [SP 소모], [숙련도] 탭이 쓴다.
// 레벨/숙련도는 "가로 = 레벨, 세로 = 세부 항목" 모양이다.
public static class RuleTableDrawers
{
    // =========================================================
    //  가로 = 레벨, 세로 = 항목 표 (레벨 표와 숙련도 표가 같이 씀)
    // =========================================================
    public struct Row
    {
        public string title, path, tip;
        public Row(string title, string path, string tip = null)
        {
            this.title = title; this.path = path; this.tip = tip;
        }
    }

    private const float LabelWidth = 150f;
    private const float CellWidth = 58f;

    // list: 레벨마다 항목 하나씩 들어 있는 배열 속성. expPath: "다음 레벨까지 경험치" 속성 이름(없으면 null)
    // 값이 바뀌었으면 true
    private static bool DrawLevels(SerializedProperty list, Row[] rows, string expPath, string expTitle, ref Vector2 scroll)
    {
        EditorGUI.BeginChangeCheck();

        int n = list.arraySize;
        for (int i = 0; i < n; i++)
            list.GetArrayElementAtIndex(i).FindPropertyRelative("level").intValue = i + 1; // 레벨 번호는 항상 순서대로

        bool hasCumulative = expPath != null;
        int lineCount = rows.Length + 1 + (hasCumulative ? 1 : 0);
        float width = LabelWidth + (CellWidth + 4f) * n + 24f;
        float height = lineCount * (EditorGUIUtility.singleLineHeight + 4f) + 26f;

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(height));

        // 머리글: Lv.1 Lv.2 ...
        GUIStyle header = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.BeginHorizontal(GUILayout.Width(width));
        GUILayout.Label("", GUILayout.Width(LabelWidth));
        for (int i = 0; i < n; i++)
            GUILayout.Label($"Lv.{i + 1}", header, GUILayout.Width(CellWidth));
        EditorGUILayout.EndHorizontal();

        // 항목 줄
        foreach (Row row in rows)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(width));
            GUILayout.Label(new GUIContent(row.title, row.tip), GUILayout.Width(LabelWidth));
            for (int i = 0; i < n; i++)
                TableField.Draw(list.GetArrayElementAtIndex(i).FindPropertyRelative(row.path), CellWidth);
            EditorGUILayout.EndHorizontal();
        }

        // 누적 경험치 (읽기 전용): 그 레벨에 도달하기까지 모은 경험치 총합
        if (hasCumulative)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(width));
            GUILayout.Label(new GUIContent("누적 " + expTitle, "처음부터 그 레벨에 도달하기까지 필요한 경험치 총합 (자동 계산)"), EditorStyles.miniLabel, GUILayout.Width(LabelWidth));
            long sum = 0;
            for (int i = 0; i < n; i++)
            {
                GUILayout.Label(sum.ToString("N0"), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(CellWidth));
                sum += Mathf.Max(0, list.GetArrayElementAtIndex(i).FindPropertyRelative(expPath).intValue);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        // 레벨 추가 / 삭제
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 레벨 추가", GUILayout.Height(24)))
        {
            list.InsertArrayElementAtIndex(n); // 마지막 레벨을 복사해서 새 마지막 레벨을 만든다
            if (hasCumulative && n >= 1)
            {
                // 예전 마지막 레벨은 이제 다음 레벨이 생겼으니 필요 경험치가 있어야 한다. 새 마지막 레벨은 0(더 안 오름)
                SerializedProperty oldLast = list.GetArrayElementAtIndex(n - 1).FindPropertyRelative(expPath);
                if (oldLast.intValue <= 0)
                    oldLast.intValue = n >= 2 ? Mathf.Max(1, list.GetArrayElementAtIndex(n - 2).FindPropertyRelative(expPath).intValue) : 10;
                list.GetArrayElementAtIndex(n).FindPropertyRelative(expPath).intValue = 0;
            }
        }
        using (new EditorGUI.DisabledScope(n <= 1))
        {
            if (GUILayout.Button("- 마지막 레벨 삭제", GUILayout.Height(24)))
            {
                list.DeleteArrayElementAtIndex(n - 1);
                if (hasCumulative)
                    list.GetArrayElementAtIndex(n - 2).FindPropertyRelative(expPath).intValue = 0; // 새 마지막 레벨
            }
        }
        EditorGUILayout.EndHorizontal();

        return EditorGUI.EndChangeCheck();
    }

    // =========================================================
    //  레벨 표
    // =========================================================
    private static readonly Row[] LevelRows =
    {
        new Row("다음 레벨까지 경험치", "expToNext", "이 레벨에서 다음 레벨이 되기까지 필요한 경험치. 마지막 레벨은 0"),
        new Row("체력 증가", "hpMaxGain", "이 레벨에 도달했을 때 최대 체력 증가 (1레벨은 시작 상태라 적용 안 됨)"),
        new Row("공격력 증가", "atGain", "도달 시 공격력 증가"),
        new Row("방어력 증가", "dfGain", "도달 시 방어력 증가"),
        new Row("최대 SP 증가", "spMaxGain", "도달 시 최대 SP 증가"),
        new Row("속도 증가", "speedGain", "도달 시 속도 증가"),
    };

    public static bool DrawLevelTable(SerializedObject so, ref Vector2 scroll)
    {
        so.Update();
        EditorGUILayout.HelpBox(
            "가로가 레벨, 세로가 항목입니다. '다음 레벨까지 경험치'는 그 레벨에서 다음 레벨이 되기까지 필요한 경험치이고, " +
            "나머지 항목은 그 레벨에 도달했을 때 늘어나는 능력치입니다 (1레벨 칸은 사용되지 않음). 마지막 레벨의 경험치는 0으로 둡니다.",
            MessageType.None);

        bool changed = DrawLevels(so.FindProperty("levels"), LevelRows, "expToNext", "경험치", ref scroll);
        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("기본값으로 되돌리기") &&
            EditorUtility.DisplayDialog("기본값으로 되돌리기", "레벨 표의 모든 값이 처음 기본값으로 바뀝니다. 계속할까요?", "되돌리기", "취소"))
        {
            Undo.RecordObject(so.targetObject, "Reset Level Table");
            ((LevelTable)so.targetObject).ResetToDefaults();
            EditorUtility.SetDirty(so.targetObject);
            changed = true;
        }
        return changed;
    }

    // =========================================================
    //  숙련도 표: 숙련도 종류마다 한 덩어리
    // =========================================================
    private static readonly Row[] ProficiencyRows =
    {
        new Row("다음 레벨까지 경험치", "expToNext", "이 숙련도 레벨에서 다음 레벨이 되기까지 필요한 숙련도 경험치. 마지막 레벨은 0"),
        new Row("수확 보너스", "bonus", "이 레벨일 때 한 번에 얻는 수량이 이만큼 늘어남 (+1이면 1개 더). [회복] 숙련도에서는 독 내성: 방어력 감소를 이만큼 [%p] 줄이고 독 지속 피해도 이만큼 줄임"),
        new Row("수량 추가 최소", "extraMin", "채집/도려내기 때 같은 아이템을 최소 이만큼 더 얻음"),
        new Row("수량 추가 최대", "extraMax", "최대 이만큼 더 얻음 (최소~최대 중에서 뽑음)"),
        new Row("레벨 경험치 보너스", "expBonus", "이 숙련도 행동으로 얻는 플레이어 레벨 경험치가 이만큼 늘어남 (+1이면 1 증가)"),
        new Row("행동 SP 감소", "spReduce", "이 숙련도 행동(공격/방어/채집/도려내기/탐색)에 드는 SP가 이만큼 줄어듦 (-1이면 1 감소, 0 밑으로는 안 내려감)"),
    };

    private static Vector2[] profScrolls = new Vector2[0];

    public static bool DrawProficiencyTable(SerializedObject so)
    {
        so.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.HelpBox(
            "행동을 하면 숙련도 경험치와 플레이어 레벨 경험치를 함께 얻습니다. 종류: 전투(공격/방어), 벌목(도끼), 채광(곡괭이), 채석(Quarrying: 자원 표의 숙련도 칸에서 지정), 풀 베기(낫), 제작, 회복(아이템 사용, 레벨 보너스가 독 내성), 도려내기. " +
            "레벨 보너스는 모두 정량입니다: 수확 보너스(+n개), 수량 추가(최소~최대), 레벨 경험치 보너스(+n), 행동 SP 감소(-n). [Gather]는 예전 방식(아래 '도구'로 채집할 때)입니다. " +
            "레벨 보너스는 누적되지 않습니다: 지금 레벨 줄의 값 하나만 적용됩니다 (2레벨 +1, 3레벨 +1이면 3레벨은 +1, +2로 만들려면 3레벨에 2를 적습니다). " +
            "같은 종류의 숙련도를 여러 개 만들 수 있고, 번호(ID)가 겹치지 않아야 진행도가 따로 쌓입니다.",
            MessageType.None);

        SerializedProperty defs = so.FindProperty("defs");
        if (profScrolls.Length != defs.arraySize) profScrolls = new Vector2[defs.arraySize];

        int removeAt = -1;
        for (int d = 0; d < defs.arraySize; d++)
        {
            SerializedProperty def = defs.GetArrayElementAtIndex(d);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("ID", GUILayout.Width(18));
            TableField.Draw(def.FindPropertyRelative("id"), 40);
            GUILayout.Label("숙련도", GUILayout.Width(44));
            TableField.Draw(def.FindPropertyRelative("label"), 120);
            GUILayout.Label("종류", GUILayout.Width(30));
            SerializedProperty kind = def.FindPropertyRelative("kind");
            TableField.Draw(kind, 110);
            if (kind.enumValueIndex == (int)ProficiencyKind.Gather)
            {
                GUILayout.Label(new GUIContent("도구", "이 도구 종류로 채집하면 경험치가 오르고 보너스가 적용됨"), GUILayout.Width(30));
                TableField.Draw(def.FindPropertyRelative("tool"), 90);
            }
            GUILayout.Label(new GUIContent("1회 경험치", "한 번 사용할 때 오르는 숙련도 경험치"), GUILayout.Width(70));
            TableField.Draw(def.FindPropertyRelative("expPerUse"), 50);
            GUILayout.Label(new GUIContent("레벨 경험치/회", "한 번 행동할 때 얻는 플레이어 레벨 경험치 (아래 '레벨 경험치 보너스'가 곱해짐)"), GUILayout.Width(90));
            TableField.Draw(def.FindPropertyRelative("playerExpPerUse"), 50);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("숙련도 삭제", GUILayout.Width(80))) removeAt = d;
            EditorGUILayout.EndHorizontal();

            DrawLevels(def.FindPropertyRelative("levels"), ProficiencyRows, "expToNext", "경험치", ref profScrolls[d]);
            EditorGUILayout.EndVertical();
        }
        if (removeAt >= 0 && EditorUtility.DisplayDialog("숙련도 삭제", "이 숙련도를 표에서 지울까요?", "삭제", "취소"))
            defs.DeleteArrayElementAtIndex(removeAt);

        if (GUILayout.Button("+ 숙련도 추가 (마지막 숙련도를 복사)", GUILayout.Height(24)))
        {
            int maxId = 0;
            for (int i = 0; i < defs.arraySize; i++)
                maxId = Mathf.Max(maxId, defs.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue);

            defs.InsertArrayElementAtIndex(defs.arraySize);
            SerializedProperty added = defs.GetArrayElementAtIndex(defs.arraySize - 1);
            added.FindPropertyRelative("id").intValue = maxId + 1;
            added.FindPropertyRelative("label").stringValue = "새 숙련도";
            added.FindPropertyRelative("kind").enumValueIndex = (int)ProficiencyKind.Gather;
        }

        bool changed = EditorGUI.EndChangeCheck();
        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("기본값으로 되돌리기") &&
            EditorUtility.DisplayDialog("기본값으로 되돌리기", "숙련도 표의 모든 값이 처음 기본값으로 바뀝니다. 계속할까요?", "되돌리기", "취소"))
        {
            Undo.RecordObject(so.targetObject, "Reset Proficiency Table");
            ((ProficiencyTable)so.targetObject).ResetToDefaults();
            EditorUtility.SetDirty(so.targetObject);
            changed = true;
        }
        return changed;
    }

    // =========================================================
    //  등급 표: 한 줄 = 등급 하나 (위에서부터 Tier 1, 2, 3 ...)
    // =========================================================
    private static readonly string[][] GradeCols =
    {
        new[] { "Tier", "tier", "40", "등급 번호 (1부터)" },
        new[] { "등급 이름", "name", "110", "" },
        new[] { "다음 등급까지 경험치", "expToNext", "110", "이 경험치가 차면 다음 등급으로 오름 (마지막 등급은 0)" },
        new[] { "공격력 %", "atBonus", "80", "이 등급일 때 장비의 공격력이 늘어나는 비율 (100이면 2배)" },
        new[] { "방어력 %", "dfBonus", "80", "장비의 방어력" },
        new[] { "체력 %", "hpBonus", "80", "장비의 체력" },
        new[] { "고정 데미지 %", "fixBonus", "85", "장비의 고정 데미지" },
        new[] { "방어 관통 %", "breakDfBonus", "85", "장비의 방어 관통" },
        new[] { "체력 퍼뎀 %", "hpRateAtBonus", "85", "장비의 체력 퍼뎀" },
        new[] { "치명타 배율 %", "critBonus", "85", "장비의 치명타 데미지" },
        new[] { "치명타 확률 %", "critRateBonus", "85", "장비의 치명타 확률" },
        new[] { "흡수 %", "absBonus", "80", "장비의 체력 흡수" },
        new[] { "회복 증가율 %", "healRateBonus", "85", "장비의 회복 증가율 (회복 아이템을 쓸 때 회복량이 늘어나는 비율)" },
    };

    private static Vector2 gradeScroll;

    public static bool DrawGradeTable(SerializedObject so)
    {
        so.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.HelpBox(
            "장비/도구는 내구도를 1 쓸 때마다 경험치(아이템 표 [수리/등급] 탭의 '경험치/내구도')를 얻고, " +
            "'다음 등급까지 경험치'가 차면 다음 등급으로 오릅니다 (남은 경험치는 이어짐). 마지막 등급의 경험치는 0으로 둡니다. " +
            "공격력 % ~ 회복 증가율 %는 그 등급일 때 장비의 해당 능력치가 늘어나는 비율이며, 능력치마다 따로 정합니다. " +
            "장비의 기본 능력치에 곱해집니다 (100이면 2배, 0이면 그대로). 값을 고친 뒤에는 가방을 열면 반영됩니다.",
            MessageType.None);

        SerializedProperty list = so.FindProperty("grades");

        const float rowH = 20f, headH = 34f, gap = 2f, delW = 28f;
        float[] widths = new float[GradeCols.Length];
        float total = delW;
        for (int i = 0; i < GradeCols.Length; i++) { widths[i] = float.Parse(GradeCols[i][2]); total += widths[i] + gap; }

        GUIStyle header = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        gradeScroll = EditorGUILayout.BeginScrollView(gradeScroll, true, false);

        Rect hr = GUILayoutUtility.GetRect(total, headH);
        float x = hr.x;
        for (int i = 0; i < GradeCols.Length; i++)
        {
            GUI.Label(new Rect(x, hr.y, widths[i], headH), new GUIContent(GradeCols[i][0], GradeCols[i][3]), header);
            x += widths[i] + gap;
        }

        int removeAt = -1;
        for (int r = 0; r < list.arraySize; r++)
        {
            SerializedProperty g = list.GetArrayElementAtIndex(r);
            Rect rr = GUILayoutUtility.GetRect(total, rowH);
            x = rr.x;
            for (int i = 0; i < GradeCols.Length; i++)
            {
                TableField.Draw(new Rect(x, rr.y, widths[i], rowH), g.FindPropertyRelative(GradeCols[i][1]));
                x += widths[i] + gap;
            }
            if (GUI.Button(new Rect(x, rr.y, delW - 2f, rowH), "X")) removeAt = r;
        }
        if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("+ 등급 추가", GUILayout.Height(24)))
        {
            int maxTier = 0;
            for (int i = 0; i < list.arraySize; i++)
                maxTier = Mathf.Max(maxTier, list.GetArrayElementAtIndex(i).FindPropertyRelative("tier").intValue);

            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            foreach (string[] c in GradeCols)
            {
                SerializedProperty p = added.FindPropertyRelative(c[1]);
                if (p == null) continue;
                if (p.propertyType == SerializedPropertyType.Float) p.floatValue = 0f;
                else if (p.propertyType == SerializedPropertyType.Integer) p.intValue = 0;
            }
            added.FindPropertyRelative("tier").intValue = maxTier + 1;
            added.FindPropertyRelative("name").stringValue = "New Grade";
        }

        bool changed = EditorGUI.EndChangeCheck();
        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("기본값으로 되돌리기") &&
            EditorUtility.DisplayDialog("기본값으로 되돌리기", "등급 표의 모든 값이 처음 기본값으로 바뀝니다. 계속할까요?", "되돌리기", "취소"))
        {
            Undo.RecordObject(so.targetObject, "Reset Grade Table");
            ((GradeTable)so.targetObject).ResetToDefaults();
            EditorUtility.SetDirty(so.targetObject);
            changed = true;
        }
        return changed;
    }

    // =========================================================
    //  특성 표: 한 줄 = 특성 하나 (장비의 "특성ID"가 이 표를 가리킴)
    // =========================================================
    private static readonly string[][] TraitCols =
    {
        // 제목, 변수, 너비, 설명(칸 이름에 마우스를 올리면 보임)
        new[] { "ID", "id", "40", "특성 번호 (장비의 '특성ID'에 적음)" },
        new[] { "이름", "label", "90", "버프 창에 보이는 이름" },
        new[] { "디버프", "debuff", "50", "켜면 디버프 특성: 상대가 걸 때, 이미 같은 특성을 가진 쪽에는 걸리지 않음" },
        new[] { "무기 종류", "weaponType", "80", "정해 두면 그 종류의 무기(메이스/배틀 엑스/활/검)에 이 특성이 붙어 있을 때만 적용. None이면 어떤 장비든" },
        new[] { "공격 증가량 %", "atRate", "80", "공격력 증감 [%] (마이너스면 감소)" },
        new[] { "방어 증가량 %", "dfRate", "80", "방어력 증감 [%] (마이너스면 디버프)" },
        new[] { "체력 증가량 %", "hpRate", "80", "최대 체력 증감 [%]" },
        new[] { "내성 적용 %", "resistPercent", "75", "이 특성의 공격(방어력 감소, 지속 피해)이 독 내성에 의해 줄어드는 정도. 100이면 내성만큼 그대로, 0이면 내성이 안 통함. 내성은 회복 숙련도 레벨이 오를수록 늘어남" },
        new[] { "방어 관통 %", "breakDf", "75", "상대 방어력을 무시하는 비율" },
        new[] { "고정 데미지", "fixAt", "75", "방어력과 상관없이 더해지는 피해" },
        new[] { "체력 퍼뎀 %", "hpRateAt", "75", "적의 현재 체력의 이 비율만큼 추가 피해" },
        new[] { "치명타 확률 %", "criticalRate", "85", "" },
        new[] { "치명타 데미지 %", "critical", "90", "" },
        new[] { "흡수 %", "abs", "60", "준 피해의 이 비율만큼 체력 회복" },
        new[] { "회복량", "healAmount", "65", "전투를 시작할 때 한 번 회복하는 체력" },
        new[] { "회복량 %", "healPercent", "70", "전투를 시작할 때 한 번 회복하는 체력 (최대 체력의 %)" },
        new[] { "턴당 회복", "regenPerTurn", "75", "전투 중 턴마다 회복하는 체력" },
        new[] { "턴당 회복 %", "regenHpPercent", "85", "전투 중 턴마다 회복하는 체력 (최대 체력의 %)" },
        new[] { "턴당 체력 퍼뎀 %", "dotHpPercent", "100", "상태이상: 턴마다 대상의 현재 체력의 이 비율만큼 피해" },
        new[] { "턴당 체력 퍼뎀 증가", "dotHpGrowth", "110", "상태이상: 턴이 지날 때마다 체력 퍼뎀 %가 이만큼 늘어남" },
        new[] { "부여 확률 %", "inflictChance", "80", "공격이 들어갔을 때 이 특성의 상태이상을 붙일 확률" },
        new[] { "턴당 지속 데미지", "dotDamage", "100", "상태이상: 첫 턴의 지속 데미지" },
        new[] { "턴당 지속 데미지 증가", "dotGrowth", "125", "상태이상: 턴이 지날 때마다 지속 데미지가 이만큼 늘어남" },
        new[] { "마나 회복", "manaPerTurn", "70", "전투 중 턴마다 회복하는 마나" },
        new[] { "마나 회복 %", "manaPercentPerTurn", "80", "전투 중 턴마다 회복하는 마나 (최대 마나의 %)" },
        new[] { "최대 마나 증가 %", "manaRate", "100", "" },
        new[] { "스킬 마나 감소 %", "skillManaReduce", "100", "스킬 마나 소모 감소 (스킬이 생기면 적용)" },
        new[] { "최대 SP 증가 %", "spRate", "95", "" },
        new[] { "적 회복 감소 %", "healReducePercent", "90", "상태이상에 걸린 대상의 회복량이 줄어드는 비율" },
        new[] { "지속 턴 수", "dotTurns", "70", "상태이상이 지속되는 턴 수" },
        new[] { "처형 체력 %", "executeHpPercent", "85", "적의 현재 체력이 최대 체력의 이 비율 미만이면 공격 때 바로 처형" },
        new[] { "반사 %", "reflectPercent", "60", "적에게 받은 데미지의 이 비율을 적에게 되돌림" },
        new[] { "완전 방어 횟수", "perfectDefendCount", "100", "한 전투에서 [방어]가 공격을 완전히 막는 횟수" },
        new[] { "상대 버프 탈취 (미구현)", "stealBuff", "125", "아직 구현되지 않았습니다. 표에만 있음" },
        new[] { "나의 디버프 제거", "cleanse", "100", "매 턴 내게 걸린 상태이상을 지우고 디버프 효과를 받지 않음" },
        new[] { "부활 횟수", "reviveCount", "70", "한 전투에서 체력이 0이 되었을 때 되살아나는 횟수" },
        new[] { "부활 체력 %", "reviveHpPercent", "80", "부활할 때 회복하는 체력 (최대 체력의 %)" },
        new[] { "필중 횟수", "sureHitCount", "70", "한 전투에서 적의 회피와 방어 태세를 무시하고 반드시 맞히는 횟수" },
        new[] { "선제공격 횟수", "firstStrikeCount", "100", "하루(다음 날이 되면 초기화) 동안 전투 시작 때 내가 먼저 움직이는 횟수" },
        new[] { "설명", "description", "380", "" },
    };

    private static Vector2 traitScroll;

    // 칸마다 정해진 자리(Rect)에 그려서 머리글과 칸이 어긋나지 않게 한다
    public static bool DrawTraitTable(SerializedObject so)
    {
        so.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.HelpBox(
            "장비를 [장착]하면 그 장비에 적힌 특성ID의 효과가 적용됩니다 (장비 스탯 탭의 '특성ID'). 값이 0(또는 꺼짐)인 칸은 그 효과가 없다는 뜻이니 필요한 칸만 채우세요. " +
            "표가 넓으니 아래 스크롤바로 옆으로 움직입니다. 칸 이름에 마우스를 올리면 설명이 나옵니다.\n" +
            "구분: 공격 증가량~선제공격 횟수 중 [능력치] 공격/방어/체력 증가량~최대 SP 증가량, [회복] 회복량~마나 회복 %, [상태이상] 턴당 체력 퍼뎀~지속 턴 수 (공격으로 적에게 붙이는 독/화염, 몬스터가 플레이어에게 걸 수도 있음), " +
            "[전투 효과] 처형 체력~선제공격 횟수. 체력 퍼뎀은 대상의 현재 체력 비율입니다. '횟수'는 한 전투에 몇 번이고, 선제공격만 하루에 몇 번입니다.",
            MessageType.None);

        SerializedProperty list = so.FindProperty("traits");

        const float rowH = 20f, headH = 34f, gap = 2f, delW = 28f;
        float[] widths = new float[TraitCols.Length];
        float total = delW;
        for (int i = 0; i < TraitCols.Length; i++) { widths[i] = float.Parse(TraitCols[i][2]); total += widths[i] + gap; }

        GUIStyle header = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        traitScroll = EditorGUILayout.BeginScrollView(traitScroll, true, false);

        // 머리글
        Rect hr = GUILayoutUtility.GetRect(total, headH);
        float x = hr.x;
        for (int i = 0; i < TraitCols.Length; i++)
        {
            GUI.Label(new Rect(x, hr.y, widths[i], headH), new GUIContent(TraitCols[i][0], TraitCols[i][3]), header);
            x += widths[i] + gap;
        }

        // 줄
        int removeAt = -1;
        for (int r = 0; r < list.arraySize; r++)
        {
            SerializedProperty t = list.GetArrayElementAtIndex(r);
            Rect rr = GUILayoutUtility.GetRect(total, rowH);
            x = rr.x;
            for (int i = 0; i < TraitCols.Length; i++)
            {
                SerializedProperty p = t.FindPropertyRelative(TraitCols[i][1]);
                Rect cell = new Rect(x, rr.y, widths[i], rowH);
                if (p != null && p.propertyType == SerializedPropertyType.Boolean)
                    p.boolValue = EditorGUI.Toggle(new Rect(cell.x + (cell.width - 14f) / 2f, cell.y, 14f, cell.height), p.boolValue); // 체크 칸은 가운데
                else
                    TableField.Draw(cell, p);
                x += widths[i] + gap;
            }
            if (GUI.Button(new Rect(x, rr.y, delW - 2f, rowH), "X")) removeAt = r;
        }
        if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("+ 특성 추가", GUILayout.Height(24)))
        {
            int maxId = 0;
            for (int i = 0; i < list.arraySize; i++)
                maxId = Mathf.Max(maxId, list.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue);

            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            // 마지막 줄이 복사되므로 모든 칸을 비운다
            foreach (string[] c in TraitCols)
            {
                SerializedProperty p = added.FindPropertyRelative(c[1]);
                if (p == null) continue;
                switch (p.propertyType)
                {
                    case SerializedPropertyType.Integer: p.intValue = 0; break;
                    case SerializedPropertyType.Float: p.floatValue = 0f; break;
                    case SerializedPropertyType.Boolean: p.boolValue = false; break;
                    case SerializedPropertyType.String: p.stringValue = ""; break;
                }
            }
            added.FindPropertyRelative("id").intValue = maxId + 1;
            added.FindPropertyRelative("label").stringValue = "새 특성";
            added.FindPropertyRelative("reviveHpPercent").floatValue = 50f;
        }

        bool changed = EditorGUI.EndChangeCheck();
        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("기본값으로 되돌리기") &&
            EditorUtility.DisplayDialog("기본값으로 되돌리기", "특성 표의 모든 값이 처음 기본값으로 바뀝니다. 계속할까요?", "되돌리기", "취소"))
        {
            Undo.RecordObject(so.targetObject, "Reset Trait Table");
            ((TraitTable)so.targetObject).ResetToDefaults();
            EditorUtility.SetDirty(so.targetObject);
            changed = true;
        }
        return changed;
    }

    // =========================================================
    //  탐색 결과 표: 한 줄 = 탐색했을 때 나올 수 있는 결과 하나
    // =========================================================
    // 지역 번호 칸: 번호와 지역 이름을 같이 보여 준다 (0 = 모든 지역. 이름은 FieldManager의 지역과 같음)
    private static readonly string[] RegionNames = { "전체", "포레스트", "폭포", "그라운드", "광산", "채석장", "유적" };

    private static void DrawRegionField(SerializedProperty p)
    {
        EditorGUILayout.BeginHorizontal(GUILayout.Width(95));
        p.intValue = Mathf.Max(0, EditorGUILayout.IntField(p.intValue, GUILayout.Width(28)));
        int id = p.intValue;
        GUILayout.Label(id < RegionNames.Length ? RegionNames[id] : "?", EditorStyles.miniLabel, GUILayout.Width(62));
        EditorGUILayout.EndHorizontal();
    }

    public static bool DrawSearchTable(SerializedObject so)
    {
        so.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.HelpBox(
            "탐색 버튼을 눌렀을 때 무엇이 나오는지의 비율입니다. 확률 = 내 비중 / 비중 합계 (합이 100일 필요는 없음, 0이면 안 나옴). " +
            "[자원]이 나오면 어떤 자원인지는 [자원] 탭의 비중으로, [몬스터]가 나오면 어떤 몬스터인지는 몬스터의 출현 지역/비중으로 정해집니다. " +
            "[이벤트]는 문구만 화면에 나옵니다 (줄을 추가해 이벤트 문구를 여러 개 둘 수 있음). " +
            "오전/오후/밤 칸은 시간대별 배율(%)로, 비중에 곱해집니다 (100 = 그대로, 0 = 그 시간대에는 안 나옴). 확률 칸은 시간대 보정 전의 값입니다.",
            MessageType.None);

        SerializedProperty entries = so.FindProperty("entries");
        int total = 0;
        for (int i = 0; i < entries.arraySize; i++)
            total += Mathf.Max(0, entries.GetArrayElementAtIndex(i).FindPropertyRelative("weight").intValue);

        GUIStyle header = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("지역", header, GUILayout.Width(95));
        GUILayout.Label("종류", header, GUILayout.Width(90));
        GUILayout.Label("이름", header, GUILayout.Width(130));
        GUILayout.Label("비중", header, GUILayout.Width(50));
        GUILayout.Label("확률", header, GUILayout.Width(55));
        GUILayout.Label("오전 %", header, GUILayout.Width(50));
        GUILayout.Label("오후 %", header, GUILayout.Width(50));
        GUILayout.Label("밤 %", header, GUILayout.Width(50));
        GUILayout.Label("이벤트 문구", header, GUILayout.Width(320));
        EditorGUILayout.EndHorizontal();

        int removeAt = -1;
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty e = entries.GetArrayElementAtIndex(i);
            SerializedProperty weight = e.FindPropertyRelative("weight");
            int w = Mathf.Max(0, weight.intValue);

            EditorGUILayout.BeginHorizontal();
            DrawRegionField(e.FindPropertyRelative("regionId"));
            TableField.Draw(e.FindPropertyRelative("kind"), 90);
            TableField.Draw(e.FindPropertyRelative("label"), 130);
            TableField.Draw(weight, 50);
            weight.intValue = Mathf.Max(0, weight.intValue);
            GUILayout.Label(total > 0 ? $"{w * 100f / total:0.#}%" : "-", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(55));
            TableField.Draw(e.FindPropertyRelative("morningPercent"), 50);
            TableField.Draw(e.FindPropertyRelative("afternoonPercent"), 50);
            TableField.Draw(e.FindPropertyRelative("nightPercent"), 50);
            if (e.FindPropertyRelative("kind").enumValueIndex == (int)SearchOutcomeKind.Event)
                TableField.Draw(e.FindPropertyRelative("text"), 320);
            else
                GUILayout.Space(324);
            if (GUILayout.Button("X", GUILayout.Width(26))) removeAt = i;
            EditorGUILayout.EndHorizontal();
        }
        if (removeAt >= 0) entries.DeleteArrayElementAtIndex(removeAt);

        if (GUILayout.Button("+ 줄 추가", GUILayout.Height(24)))
        {
            entries.InsertArrayElementAtIndex(entries.arraySize);
            SerializedProperty added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            added.FindPropertyRelative("label").stringValue = "새 결과";
            added.FindPropertyRelative("kind").enumValueIndex = (int)SearchOutcomeKind.Event;
            added.FindPropertyRelative("weight").intValue = 1;
            added.FindPropertyRelative("text").stringValue = "";
            added.FindPropertyRelative("morningPercent").floatValue = 100f;
            added.FindPropertyRelative("afternoonPercent").floatValue = 100f;
            added.FindPropertyRelative("nightPercent").floatValue = 100f;
        }

        bool changed = EditorGUI.EndChangeCheck();
        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("기본값으로 되돌리기") &&
            EditorUtility.DisplayDialog("기본값으로 되돌리기", "탐색 결과 표의 모든 값이 처음 기본값으로 바뀝니다. 계속할까요?", "되돌리기", "취소"))
        {
            Undo.RecordObject(so.targetObject, "Reset Search Table");
            ((SearchTable)so.targetObject).ResetToDefaults();
            EditorUtility.SetDirty(so.targetObject);
            changed = true;
        }
        return changed;
    }

    // =========================================================
    //  SP 소모 표: 한 줄 = 행동 하나
    // =========================================================
    public static bool DrawSPCostTable(SerializedObject so)
    {
        so.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.HelpBox(
            "행동마다 쓰는 SP입니다. SP가 모자라면 그 행동을 할 수 없고, 전투 중에는 행동하지 못한 채 턴이 넘어갑니다.",
            MessageType.None);

        SerializedProperty entries = so.FindProperty("entries");

        GUIStyle header = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("행동", header, GUILayout.Width(100));
        GUILayout.Label("이름", header, GUILayout.Width(100));
        GUILayout.Label("SP 소모", header, GUILayout.Width(70));
        GUILayout.Label("메모", header, GUILayout.Width(380));
        EditorGUILayout.EndHorizontal();

        int removeAt = -1;
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty e = entries.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();
            TableField.Draw(e.FindPropertyRelative("action"), 100);
            TableField.Draw(e.FindPropertyRelative("label"), 100);
            SerializedProperty cost = e.FindPropertyRelative("cost");
            TableField.Draw(cost, 70);
            cost.intValue = Mathf.Max(0, cost.intValue);
            TableField.Draw(e.FindPropertyRelative("note"), 380);
            if (GUILayout.Button("X", GUILayout.Width(26))) removeAt = i;
            EditorGUILayout.EndHorizontal();
        }
        if (removeAt >= 0) entries.DeleteArrayElementAtIndex(removeAt);

        if (GUILayout.Button("+ 줄 추가", GUILayout.Height(24)))
        {
            entries.InsertArrayElementAtIndex(entries.arraySize);
            entries.GetArrayElementAtIndex(entries.arraySize - 1).FindPropertyRelative("label").stringValue = "새 행동";
        }

        bool changed = EditorGUI.EndChangeCheck();
        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("기본값으로 되돌리기") &&
            EditorUtility.DisplayDialog("기본값으로 되돌리기", "SP 소모 표의 모든 값이 처음 기본값으로 바뀝니다. 계속할까요?", "되돌리기", "취소"))
        {
            Undo.RecordObject(so.targetObject, "Reset SP Cost Table");
            ((SPCostTable)so.targetObject).ResetToDefaults();
            EditorUtility.SetDirty(so.targetObject);
            changed = true;
        }
        return changed;
    }

    // =========================================================
    //  기본 능력치 표: 한 줄 = 항목 하나
    // =========================================================
    private struct BaseRow
    {
        public string title, path, tip;
        public int min;
        public BaseRow(string title, string path, string tip, int min = int.MinValue)
        {
            this.title = title; this.path = path; this.tip = tip; this.min = min;
        }
    }

    private static readonly BaseRow[] BaseRows =
    {
        new BaseRow("최대 체력", "hpMax", "게임을 시작할 때의 최대 체력", 1),
        new BaseRow("공격력", "at", "시작 공격력"),
        new BaseRow("방어력", "df", "시작 방어력"),
        new BaseRow("최대 SP", "spMax", "시작 최대 SP(행동력)", 1),
        new BaseRow("마나", "mana", "시작 최대 마나"),
        new BaseRow("속도", "speed", "높을수록 전투에서 먼저 행동"),
        new BaseRow("슬롯 최대 개수", "slotMax", "가방의 기본 슬롯 개수. 실제 한도 = 이 값 + 장비 보너스(GameManager.SlotBonus)", 1),
        new BaseRow("최대 소지 무게", "weightMax", "가방의 기본 최대 무게. 실제 한도 = 이 값 + 장비 보너스(GameManager.WeightBonus)", 1),
        new BaseRow("방어 보너스 (%)", "defendBonus", "[방어]할 때 방어력이 늘어나는 비율. 기본 50, 장비로 더 늘 수 있음"),
        new BaseRow("병원비 (골드)", "hospitalFee", "기절하면 내는 병원비. 골드가 모자라면 가진 만큼만 냄", 0),
        new BaseRow("기절 시 아이템 손실 (%)", "itemLossPercent", "기절하면 잃는 아이템 비율 (도구 제외)", 0),
        new BaseRow("오후 SP 회복 (%)", "restAfternoonSpPercent", "[휴식]으로 오후가 될 때 회복하는 SP (최대 SP의 %)"),
        new BaseRow("밤 SP 회복 (%)", "restNightSpPercent", "[휴식]으로 밤이 될 때 회복하는 SP (최대 SP의 %). 다음 날 오전은 체력과 SP가 가득 찬다"),
        new BaseRow("탈진 스택 수", "exhaustStacks", "SP가 0에 닿을 때마다 쌓이는 스택. 이만큼 쌓이면 건강이 나빠져 최대 SP가 줄어듦 (0이면 탈진 없음). 푹 자고 다음 날이 되면 풀림", 0),
        new BaseRow("건강 악화 최대 SP 감소 (%)", "exhaustSpMaxPercent", "건강이 나빠졌을 때 줄어드는 최대 SP [%]"),
        new BaseRow("고유 특성 ID", "innateTraitId", "장비 없이도 가지는 특성 (특성 표의 ID, 0 = 없음). 예: 1 = 독", 0),
        new BaseRow("위험 1단계 체력 (%)", "lowHpPercent1", "체력이 이 비율 이하면 화면 가장자리가 붉어지기 시작"),
        new BaseRow("위험 1단계 세기", "lowHpAlpha1", "붉은 정도 (0~100)"),
        new BaseRow("위험 2단계 체력 (%)", "lowHpPercent2", "더 위험한 단계의 체력 비율"),
        new BaseRow("위험 2단계 세기", "lowHpAlpha2", "붉은 정도 (0~100)"),
        new BaseRow("위험 3단계 체력 (%)", "lowHpPercent3", "위독 단계의 체력 비율"),
        new BaseRow("위험 3단계 세기", "lowHpAlpha3", "붉은 정도 (0~100)"),
    };

    public static bool DrawPlayerBaseTable(SerializedObject so)
    {
        so.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.HelpBox(
            "게임을 처음 켰을 때 한 번 적용되는 시작 능력치와 가방 한도입니다. " +
            "이미 진행 중인 게임에는 반영되지 않으니, 바꾼 뒤에는 플레이를 껐다 켜서 확인하세요.",
            MessageType.None);

        foreach (BaseRow row in BaseRows)
        {
            SerializedProperty p = so.FindProperty(row.path);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(row.title, row.tip), GUILayout.Width(150));
            TableField.Draw(p, 100);
            if (p != null && p.propertyType == SerializedPropertyType.Integer && row.min != int.MinValue)
                p.intValue = Mathf.Max(row.min, p.intValue);
            GUILayout.Label(row.tip, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        bool changed = EditorGUI.EndChangeCheck();
        so.ApplyModifiedProperties();
        return changed;
    }

    // =========================================================
    //  효과음 표: 한 줄 = 효과음 하나
    // =========================================================
    public static bool DrawSoundTable(SerializedObject so)
    {
        so.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.HelpBox(
            "상황마다 재생할 소리입니다. 소리 파일 칸에 오디오 파일(Project 창의 4Sound 등)을 끌어다 놓으면 바뀝니다.",
            MessageType.None);

        SerializedProperty entries = so.FindProperty("entries");

        GUIStyle header = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("상황", header, GUILayout.Width(110));
        GUILayout.Label("이름", header, GUILayout.Width(100));
        GUILayout.Label("소리 파일", header, GUILayout.Width(220));
        GUILayout.Label("볼륨", header, GUILayout.Width(60));
        GUILayout.Label("메모", header, GUILayout.Width(300));
        EditorGUILayout.EndHorizontal();

        int removeAt = -1;
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty e = entries.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();
            TableField.Draw(e.FindPropertyRelative("sound"), 110);
            TableField.Draw(e.FindPropertyRelative("label"), 100);
            TableField.Draw(e.FindPropertyRelative("clip"), 220);
            SerializedProperty volume = e.FindPropertyRelative("volume");
            TableField.Draw(volume, 60);
            volume.floatValue = Mathf.Clamp01(volume.floatValue);
            TableField.Draw(e.FindPropertyRelative("note"), 300);
            if (GUILayout.Button("X", GUILayout.Width(26))) removeAt = i;
            EditorGUILayout.EndHorizontal();
        }
        if (removeAt >= 0) entries.DeleteArrayElementAtIndex(removeAt);

        if (GUILayout.Button("+ 줄 추가", GUILayout.Height(24)))
        {
            entries.InsertArrayElementAtIndex(entries.arraySize);
            entries.GetArrayElementAtIndex(entries.arraySize - 1).FindPropertyRelative("label").stringValue = "새 효과음";
        }

        bool changed = EditorGUI.EndChangeCheck();
        so.ApplyModifiedProperties();
        return changed;
    }

    private static readonly string[][] SkillCols =
    {
        // 제목, 변수, 너비, 설명(칸 이름에 마우스를 올리면 보임)
        new[] { "ID", "id", "40", "스킬 번호" },
        new[] { "이름", "label", "100", "스킬 창에 보이는 이름" },
        new[] { "종류", "kind", "80", "공격: 적을 공격 / 회복: 내 체력을 회복" },
        new[] { "필요 레벨", "unlockLevel", "70", "플레이어가 이 레벨이 되면 쓸 수 있음" },
        new[] { "무기 종류", "weaponType", "80", "정해 두면 그 종류의 무기를 장착해야 쓸 수 있음 (None = 어떤 무기든)" },
        new[] { "마나 소모", "manaCost", "70", "쓸 때 드는 마나 (특성 '스킬 마나 감소 %'만큼 줄어듦). 마나는 다음 날이 되면 가득 참" },
        new[] { "SP 소모", "spCost", "65", "쓸 때 드는 SP" },
        new[] { "쿨다운 턴", "cooldown", "70", "쓴 뒤 다시 쓰기까지 기다리는 턴 수 (한 전투 안에서)" },
        new[] { "타수", "hits", "50", "한 번에 연속으로 때리는 횟수 (공격 스킬)" },
        new[] { "위력 %", "powerPercent", "70", "한 번당 공격력. 100이면 일반 공격과 같음 (공격 스킬)" },
        new[] { "고정 데미지", "fixedDamage", "75", "한 번당 더해지는 고정 데미지 (공격 스킬)" },
        new[] { "방어 관통 %", "breakDf", "75", "이 스킬에서만 더해지는 방어 관통 (공격 스킬)" },
        new[] { "치명타 확률 %", "criticalRate", "85", "이 스킬에서만 더해지는 치명타 확률 (공격 스킬)" },
        new[] { "필중", "sureHit", "45", "켜면 적의 회피와 방어 태세를 무시 (공격 스킬)" },
        new[] { "상태이상 ID", "inflictTraitId", "80", "맞췄을 때 적에게 거는 상태이상 (특성 표의 ID, 0 = 없음. 예: 1 = 독)" },
        new[] { "부여 확률 %", "inflictChance", "80", "상태이상을 거는 확률" },
        new[] { "회복량", "healAmount", "65", "회복 스킬: 고정 회복량" },
        new[] { "회복량 %", "healPercent", "70", "회복 스킬: 최대 체력의 이 비율만큼 회복" },
        new[] { "상태이상 치료", "cleanse", "85", "회복 스킬: 내게 걸린 상태이상을 지움" },
        new[] { "설명", "description", "380", "스킬 창에 보이는 설명" },
    };

    private static Vector2 skillScroll;

    public static bool DrawSkillTable(SerializedObject so)
    {
        so.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.HelpBox(
            "전투에서 [스킬] 버튼(S 키)을 누르면 스킬 창이 뜨고, 고른 스킬을 쓰면 이번 턴을 씁니다. 값이 0(또는 꺼짐)인 칸은 그 효과가 없다는 뜻이니 필요한 칸만 채우세요. " +
            "마나는 최대 마나(기본 능력치 표 + 장비 특성) 안에서 쓰고, 다음 날이 되면 가득 찹니다. " +
            "구분: 타수~상태이상 부여 확률은 공격 스킬, 회복량~상태이상 치료는 회복 스킬 칸입니다. 표가 넓으니 아래 스크롤바로 옆으로 움직입니다.",
            MessageType.None);

        SerializedProperty list = so.FindProperty("skills");

        const float rowH = 20f, headH = 34f, gap = 2f, delW = 28f;
        float[] widths = new float[SkillCols.Length];
        float total = delW;
        for (int i = 0; i < SkillCols.Length; i++) { widths[i] = float.Parse(SkillCols[i][2]); total += widths[i] + gap; }

        GUIStyle header = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        skillScroll = EditorGUILayout.BeginScrollView(skillScroll, true, false);

        Rect hr = GUILayoutUtility.GetRect(total, headH);
        float x = hr.x;
        for (int i = 0; i < SkillCols.Length; i++)
        {
            GUI.Label(new Rect(x, hr.y, widths[i], headH), new GUIContent(SkillCols[i][0], SkillCols[i][3]), header);
            x += widths[i] + gap;
        }

        int removeAt = -1;
        for (int r = 0; r < list.arraySize; r++)
        {
            SerializedProperty t = list.GetArrayElementAtIndex(r);
            Rect rr = GUILayoutUtility.GetRect(total, rowH);
            x = rr.x;
            for (int i = 0; i < SkillCols.Length; i++)
            {
                SerializedProperty p = t.FindPropertyRelative(SkillCols[i][1]);
                Rect cell = new Rect(x, rr.y, widths[i], rowH);
                if (p != null && p.propertyType == SerializedPropertyType.Boolean)
                    p.boolValue = EditorGUI.Toggle(new Rect(cell.x + (cell.width - 14f) / 2f, cell.y, 14f, cell.height), p.boolValue);
                else
                    TableField.Draw(cell, p);
                x += widths[i] + gap;
            }
            if (GUI.Button(new Rect(x, rr.y, delW - 2f, rowH), "X")) removeAt = r;
        }
        if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("+ 스킬 추가", GUILayout.Height(24)))
        {
            int maxId = 0;
            for (int i = 0; i < list.arraySize; i++)
                maxId = Mathf.Max(maxId, list.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue);

            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            foreach (string[] c in SkillCols)
            {
                SerializedProperty p = added.FindPropertyRelative(c[1]);
                if (p == null) continue;
                switch (p.propertyType)
                {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.Enum: p.intValue = 0; break;
                    case SerializedPropertyType.Float: p.floatValue = 0f; break;
                    case SerializedPropertyType.Boolean: p.boolValue = false; break;
                    case SerializedPropertyType.String: p.stringValue = ""; break;
                }
            }
            added.FindPropertyRelative("id").intValue = maxId + 1;
            added.FindPropertyRelative("label").stringValue = "새 스킬";
            added.FindPropertyRelative("unlockLevel").intValue = 1;
            added.FindPropertyRelative("hits").intValue = 1;
            added.FindPropertyRelative("powerPercent").floatValue = 100f;
        }

        bool changed = EditorGUI.EndChangeCheck();
        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("기본값으로 되돌리기") &&
            EditorUtility.DisplayDialog("기본값으로 되돌리기", "스킬 표의 모든 값이 처음 기본값으로 바뀝니다. 계속할까요?", "되돌리기", "취소"))
        {
            Undo.RecordObject(so.targetObject, "Reset Skill Table");
            ((SkillTable)so.targetObject).ResetToDefaults();
            EditorUtility.SetDirty(so.targetObject);
            changed = true;
        }
        return changed;
    }
}
