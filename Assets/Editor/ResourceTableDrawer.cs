using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ResourceSpawnTable을 "한 줄 = 자원 한 종류"인 표로 그리는 공용 도구.
// ResourceSpawnTable 인스펙터와 "Soul > 데이터 표" 창의 [자원] 탭이 같이 쓴다.
// 엑셀처럼 머리글 줄과 왼쪽의 [줄 조작 버튼 + 이름] 열이 고정되고, 나머지는 스크롤된다.
public static class ResourceTableDrawer
{
    // 열 정의: 제목, 설명(툴팁), 속성 이름(@로 시작하면 계산해서 보여 주는 열), 너비
    private struct Col
    {
        public string title, tip, prop;
        public float width;
        public Col(string title, string tip, string prop, float width)
        {
            this.title = title; this.tip = tip; this.prop = prop; this.width = width;
        }
    }

    private const string PercentCol = "@percent";
    private const string ItemNameCol = "@itemName";

    private const float RowHeight = 26f;
    private const float HeaderHeight = 34f;
    private const float Gap = 4f;
    private const float ButtonWidth = 24f;   // 줄 조작 버튼 하나의 너비 (위, 아래, 아래에 줄 추가, 삭제)
    private const int FrozenCols = 1;        // 고정할 열 수 (이름)

    private static readonly Col[] Cols =
    {
        new Col("이름", "목록에서 구분하기 위한 이름 (게임에는 영향 없음)", "label", 100),
        new Col("비중", "표 안의 비중 합계 중 내 몫이 확률. 0이면 안 나옴", "weight", 50),
        new Col("확률", "계산된 실제 확률 (자원이 나왔을 때 이 자원일 확률)", PercentCol, 55),
        new Col("발견 문구", "상황 텍스트 (예: 나뭇가지를 발견했다.)", "foundText", 170),
        new Col("버튼", "채집 버튼 이름 (채집, 벌목, 채광...)", "gatherButtonText", 55),
        new Col("아이템ID", "채집하면 얻는 기본 아이템 ID", "itemId", 60),
        new Col("아이템명", "ID에 해당하는 아이템 이름 (자동 표시)", ItemNameCol, 80),
        new Col("도구", "필요한 도구 (None이면 맨손)", "toolType", 80),
        new Col("티어", "필요 도구 티어", "tier", 40),
        new Col("HP최소", "자원 체력(채집 횟수) 최솟값", "hpMin", 50),
        new Col("HP최대", "자원 체력(채집 횟수) 최댓값", "hpMax", 50),
        new Col("소리", "채집 소리", "sound", 80),
    };

    private const string ExtraHeader = "추가 획득 (아이템 ID, x 채집한 양의 배수, @확률%)";

    private static Dictionary<int, string> itemNames = new Dictionary<int, string>();

    // 아이템 ID -> 이름 표를 새로 읽는다 (아이템명 열에 쓰임)
    public static void RefreshNames()
    {
        itemNames = new Dictionary<int, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Item"))
        {
            Item item = AssetDatabase.LoadAssetAtPath<Item>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && !itemNames.ContainsKey(item.id)) itemNames[item.id] = item.itemName;
        }
    }

    private static string NameOf(int id)
    {
        return itemNames.TryGetValue(id, out string found) ? found : "(없음)";
    }

    // 표를 그린다. fillWindow가 true면 창의 남는 공간을 모두 쓰고(데이터 표 창), false면 높이를 줄 수에 맞춘다(인스펙터).
    // 값이 바뀌었으면 true
    public static bool Draw(SerializedObject so, ref Vector2 scroll, bool fillWindow)
    {
        so.Update();
        EditorGUI.BeginChangeCheck();

        SerializedProperty entries = so.FindProperty("entries");
        int count = entries.arraySize;

        // 비중 합계
        int total = 0;
        for (int i = 0; i < count; i++)
            total += Mathf.Max(0, entries.GetArrayElementAtIndex(i).FindPropertyRelative("weight").intValue);

        EditorGUILayout.HelpBox(
            $"비중 합계: {total}   (확률 = 내 비중 / 합계. 합이 100일 필요는 없음)", MessageType.None);

        // 너비: 왼쪽 고정 = [줄 버튼 4개] + 이름, 나머지는 스크롤 (+ 추가 획득 목록)
        float buttonsW = (ButtonWidth + Gap) * 4f;
        float frozenW = buttonsW + 4f;
        for (int i = 0; i < FrozenCols; i++) frozenW += Cols[i].width + Gap;

        float scrollCols = 0f;
        for (int i = FrozenCols; i < Cols.Length; i++) scrollCols += Cols[i].width + Gap;

        float extraW = 260f; // 머리글이 보일 최소 너비
        for (int i = 0; i < count; i++)
        {
            SerializedProperty extras = entries.GetArrayElementAtIndex(i).FindPropertyRelative("extraYields");
            extraW = Mathf.Max(extraW, (extras != null ? extras.arraySize : 0) * 230f + 60f);
        }
        float scrollW = scrollCols + extraW + 24f;

        Rect area = fillWindow
            ? GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true))
            : GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f, GUILayout.ExpandWidth(true),
                GUILayout.Height(Mathf.Min(560f, HeaderHeight + (count + 1) * RowHeight + 20f)));

        GUIStyle header = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };

        // 줄 조작은 반복이 끝난 뒤에 처리한다
        int removeAt = -1, moveFrom = -1, moveTo = -1, addAfter = -1;

        Vector2 s = scroll;
        FrozenGrid.Draw(area, ref s, count, frozenW, scrollW, RowHeight, HeaderHeight,
            // 왼쪽 고정 머리글
            r =>
            {
                float x = buttonsW + 4f;
                for (int i = 0; i < FrozenCols; i++)
                {
                    GUI.Label(new Rect(x, 0f, Cols[i].width, r.height), new GUIContent(Cols[i].title, Cols[i].tip), header);
                    x += Cols[i].width + Gap;
                }
            },
            // 스크롤 머리글
            r =>
            {
                float x = r.x;
                for (int i = FrozenCols; i < Cols.Length; i++)
                {
                    GUI.Label(new Rect(x, 0f, Cols[i].width, r.height), new GUIContent(Cols[i].title, Cols[i].tip), header);
                    x += Cols[i].width + Gap;
                }
                GUI.Label(new Rect(x, 0f, extraW, r.height), ExtraHeader, header);
            },
            // 왼쪽 고정 줄: [위] [아래] [아래에 줄 추가] [삭제] 이름
            (r, i) =>
            {
                SerializedProperty e = entries.GetArrayElementAtIndex(i);
                float x = r.x;
                if (GUI.Button(new Rect(x, r.y + 2f, ButtonWidth, r.height - 4f), new GUIContent("▲", "위로")) && i > 0) { moveFrom = i; moveTo = i - 1; }
                x += ButtonWidth + Gap;
                if (GUI.Button(new Rect(x, r.y + 2f, ButtonWidth, r.height - 4f), new GUIContent("▼", "아래로")) && i < count - 1) { moveFrom = i; moveTo = i + 1; }
                x += ButtonWidth + Gap;
                if (GUI.Button(new Rect(x, r.y + 2f, ButtonWidth, r.height - 4f), new GUIContent("+", "이 줄 아래에 줄을 추가합니다 (이 줄을 복사)"))) addAfter = i;
                x += ButtonWidth + Gap;
                if (GUI.Button(new Rect(x, r.y + 2f, ButtonWidth, r.height - 4f), new GUIContent("X", "이 줄 삭제"))) removeAt = i;
                x += ButtonWidth + Gap + 4f;

                for (int k = 0; k < FrozenCols; k++)
                {
                    DrawField(new Rect(x, r.y + 2f, Cols[k].width, r.height - 4f), e.FindPropertyRelative(Cols[k].prop));
                    x += Cols[k].width + Gap;
                }
            },
            // 스크롤 줄: 나머지 칸 + 추가 획득 목록
            (r, i) =>
            {
                SerializedProperty e = entries.GetArrayElementAtIndex(i);
                int weight = Mathf.Max(0, e.FindPropertyRelative("weight").intValue);
                float x = r.x;

                for (int k = FrozenCols; k < Cols.Length; k++)
                {
                    Rect cell = new Rect(x, r.y + 2f, Cols[k].width, r.height - 4f);
                    if (Cols[k].prop == PercentCol)
                    {
                        float percent = total > 0 ? weight * 100f / total : 0f;
                        EditorGUI.LabelField(cell, $"{percent:0.#}%", EditorStyles.centeredGreyMiniLabel);
                    }
                    else if (Cols[k].prop == ItemNameCol)
                    {
                        EditorGUI.LabelField(cell, NameOf(e.FindPropertyRelative("itemId").intValue), EditorStyles.centeredGreyMiniLabel);
                    }
                    else
                    {
                        DrawField(cell, e.FindPropertyRelative(Cols[k].prop));
                    }
                    x += Cols[k].width + Gap;
                }

                DrawExtraYields(r, x, e.FindPropertyRelative("extraYields"));
            });
        scroll = s;

        // 줄 이동/추가/삭제
        if (moveFrom >= 0) entries.MoveArrayElement(moveFrom, moveTo);
        if (addAfter >= 0) entries.InsertArrayElementAtIndex(addAfter); // 그 줄을 복사해서 바로 아래에 넣는다
        if (removeAt >= 0) entries.DeleteArrayElementAtIndex(removeAt);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 줄 추가 (맨 아래)", GUILayout.Height(24)))
        {
            entries.InsertArrayElementAtIndex(entries.arraySize);
            SerializedProperty added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            added.FindPropertyRelative("label").stringValue = "새 자원";
        }
        if (GUILayout.Button("아이템 이름 새로고침", GUILayout.Height(24), GUILayout.Width(150)))
            RefreshNames();
        EditorGUILayout.EndHorizontal();

        bool changed = EditorGUI.EndChangeCheck();
        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("기본값으로 되돌리기"))
        {
            if (EditorUtility.DisplayDialog("기본값으로 되돌리기", "표의 모든 값이 처음 기본값으로 바뀝니다. 계속할까요?", "되돌리기", "취소"))
            {
                Undo.RecordObject(so.targetObject, "Reset Resource Spawn Table");
                ((ResourceSpawnTable)so.targetObject).ResetToDefaults();
                EditorUtility.SetDirty(so.targetObject);
                changed = true;
            }
        }
        return changed;
    }

    // 추가 획득 목록: [아이템ID (이름) x배수 @확률% -] ... [+]
    private static void DrawExtraYields(Rect row, float startX, SerializedProperty list)
    {
        if (list == null) return;
        float x = startX;
        float y = row.y + 2f;
        float h = row.height - 4f;

        int removeAt = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty yield = list.GetArrayElementAtIndex(i);
            SerializedProperty idProp = yield.FindPropertyRelative("itemId");

            DrawField(new Rect(x, y, 40f, h), idProp); x += 44f;
            EditorGUI.LabelField(new Rect(x, y, 66f, h), NameOf(idProp.intValue), EditorStyles.miniLabel); x += 70f;
            EditorGUI.LabelField(new Rect(x, y, 10f, h), "x", EditorStyles.miniLabel); x += 12f;
            SerializedProperty mult = yield.FindPropertyRelative("multiplier");
            DrawField(new Rect(x, y, 34f, h), mult);
            mult.intValue = Mathf.Max(1, mult.intValue); x += 38f;
            EditorGUI.LabelField(new Rect(x, y, 12f, h), "@", EditorStyles.miniLabel); x += 14f;
            DrawField(new Rect(x, y, 46f, h), yield.FindPropertyRelative("chance")); x += 50f;
            EditorGUI.LabelField(new Rect(x, y, 14f, h), "%", EditorStyles.miniLabel); x += 16f;
            if (GUI.Button(new Rect(x, y, 22f, h), "-")) removeAt = i;
            x += 36f;
        }
        if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);

        if (GUI.Button(new Rect(x, y, 26f, h), new GUIContent("+", "이 자원에서 추가로 얻는 아이템 추가")))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("itemId").intValue = 0;
            added.FindPropertyRelative("multiplier").intValue = 1;
            added.FindPropertyRelative("chance").floatValue = 100f;
        }
    }

    // 속성 하나를 정해진 칸에 그린다 ([Header] 제목 없이 한 줄로)
    private static void DrawField(Rect rect, SerializedProperty p)
    {
        TableField.Draw(rect, p);
        if (p == null || p.propertyType != SerializedPropertyType.Integer) return;

        // 표에서는 [Min] 제한이 적용되지 않으므로 직접 보정
        if (p.name == "weight") p.intValue = Mathf.Max(0, p.intValue);
        else if (p.name == "hpMin" || p.name == "hpMax") p.intValue = Mathf.Max(1, p.intValue);
    }
}
