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
        new Row("수확 보너스 (%)", "bonusPercent", "이 레벨일 때 얻는 수량이 늘어나는 비율 [%]"),
    };

    private static Vector2[] profScrolls = new Vector2[0];

    public static bool DrawProficiencyTable(SerializedObject so)
    {
        so.Update();
        EditorGUI.BeginChangeCheck();

        SerializedProperty defs = so.FindProperty("defs");
        if (profScrolls.Length != defs.arraySize) profScrolls = new Vector2[defs.arraySize];

        for (int d = 0; d < defs.arraySize; d++)
        {
            SerializedProperty def = defs.GetArrayElementAtIndex(d);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("숙련도", GUILayout.Width(44));
            TableField.Draw(def.FindPropertyRelative("label"), 120);
            GUILayout.Label("종류", GUILayout.Width(30));
            TableField.Draw(def.FindPropertyRelative("kind"), 110);
            GUILayout.Label(new GUIContent("1회 경험치", "한 번 사용할 때 오르는 숙련도 경험치"), GUILayout.Width(70));
            TableField.Draw(def.FindPropertyRelative("expPerUse"), 50);
            EditorGUILayout.EndHorizontal();

            DrawLevels(def.FindPropertyRelative("levels"), ProficiencyRows, "expToNext", "경험치", ref profScrolls[d]);
            EditorGUILayout.EndVertical();
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
}
