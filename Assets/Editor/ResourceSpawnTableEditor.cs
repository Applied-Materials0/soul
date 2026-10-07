using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ResourceSpawnTable 인스펙터를 "한 줄 = 자원 한 종류"인 표로 보여 준다.
// 열 이름이 맨 위에 한 번만 나오고, 비중을 고치면 확률(%)이 바로 바뀐다.
[CustomEditor(typeof(ResourceSpawnTable))]
public class ResourceSpawnTableEditor : Editor
{
    // 열 정의: 제목, 설명(툴팁), 속성 이름(null이면 계산해서 보여 주는 열), 너비
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

    private static readonly Col[] Cols =
    {
        new Col("이름", "목록에서 구분하기 위한 이름 (게임에는 영향 없음)", "label", 90),
        new Col("비중", "표 안의 비중 합계 중 내 몫이 확률. 0이면 안 나옴", "weight", 50),
        new Col("확률", "계산된 실제 확률 (자원이 나왔을 때 이 자원일 확률)", PercentCol, 55),
        new Col("발견 문구", "상황 텍스트 (예: 나뭇가지를 발견했다.)", "foundText", 170),
        new Col("버튼", "채집 버튼 이름 (채집, 벌목, 채광...)", "gatherButtonText", 55),
        new Col("아이템ID", "얻는 아이템 ID", "itemId", 60),
        new Col("아이템명", "ID에 해당하는 아이템 이름 (자동 표시)", ItemNameCol, 80),
        new Col("도구", "필요한 도구 (None이면 맨손)", "toolType", 80),
        new Col("티어", "필요 도구 티어", "tier", 40),
        new Col("HP최소", "자원 체력(채집 횟수) 최솟값", "hpMin", 50),
        new Col("HP최대", "자원 체력(채집 횟수) 최댓값", "hpMax", 50),
        new Col("소리", "채집 소리", "sound", 80),
    };

    private const float ButtonsWidth = 84f;
    private Vector2 scroll;
    private Dictionary<int, string> itemNames;

    private void OnEnable()
    {
        RefreshItemNames();
    }

    // 아이템 ID -> 이름 표 (아이템명 열에 쓰임)
    private void RefreshItemNames()
    {
        itemNames = new Dictionary<int, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Item"))
        {
            Item item = AssetDatabase.LoadAssetAtPath<Item>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && !itemNames.ContainsKey(item.id)) itemNames[item.id] = item.itemName;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty entries = serializedObject.FindProperty("entries");

        // 비중 합계
        int total = 0;
        for (int i = 0; i < entries.arraySize; i++)
            total += Mathf.Max(0, entries.GetArrayElementAtIndex(i).FindPropertyRelative("weight").intValue);

        EditorGUILayout.HelpBox(
            $"비중 합계: {total}   (확률 = 내 비중 / 합계. 합이 100일 필요는 없음)", MessageType.None);

        float tableWidth = ButtonsWidth + 12f;
        foreach (Col c in Cols) tableWidth += c.width + 4f;

        // 가로로만 스크롤. 높이는 줄 수에 맞춰 고정
        float rowHeight = EditorGUIUtility.singleLineHeight + 4f;
        float height = rowHeight * (entries.arraySize + 1) + 20f;
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(height));

        // 머리글
        EditorGUILayout.BeginHorizontal(GUILayout.Width(tableWidth));
        GUIStyle header = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
        foreach (Col c in Cols)
            GUILayout.Label(new GUIContent(c.title, c.tip), header, GUILayout.Width(c.width));
        GUILayout.Label("", GUILayout.Width(ButtonsWidth));
        EditorGUILayout.EndHorizontal();

        // 줄
        int removeAt = -1, moveFrom = -1, moveTo = -1;
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty e = entries.GetArrayElementAtIndex(i);
            int weight = Mathf.Max(0, e.FindPropertyRelative("weight").intValue);

            EditorGUILayout.BeginHorizontal(GUILayout.Width(tableWidth));
            foreach (Col c in Cols)
            {
                if (c.prop == PercentCol)
                {
                    float percent = total > 0 ? weight * 100f / total : 0f;
                    GUILayout.Label($"{percent:0.#}%", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(c.width));
                }
                else if (c.prop == ItemNameCol)
                {
                    int id = e.FindPropertyRelative("itemId").intValue;
                    string n = itemNames.TryGetValue(id, out string found) ? found : "(없음)";
                    GUILayout.Label(n, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(c.width));
                }
                else
                {
                    DrawField(e.FindPropertyRelative(c.prop), c.width);
                }
            }

            if (GUILayout.Button("▲", GUILayout.Width(26)) && i > 0) { moveFrom = i; moveTo = i - 1; }
            if (GUILayout.Button("▼", GUILayout.Width(26)) && i < entries.arraySize - 1) { moveFrom = i; moveTo = i + 1; }
            if (GUILayout.Button("X", GUILayout.Width(26))) removeAt = i;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        // 줄 이동/삭제는 반복문이 끝난 뒤에 처리
        if (moveFrom >= 0) entries.MoveArrayElement(moveFrom, moveTo);
        if (removeAt >= 0) entries.DeleteArrayElementAtIndex(removeAt);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 줄 추가", GUILayout.Height(24)))
        {
            // 마지막 줄을 복사해서 새 줄을 만든다 (비어 있으면 기본값)
            entries.InsertArrayElementAtIndex(entries.arraySize);
            SerializedProperty added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            added.FindPropertyRelative("label").stringValue = "새 자원";
        }
        if (GUILayout.Button("아이템 이름 새로고침", GUILayout.Height(24), GUILayout.Width(150)))
            RefreshItemNames();
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("기본값으로 되돌리기"))
        {
            if (EditorUtility.DisplayDialog("기본값으로 되돌리기", "표의 모든 값이 처음 기본값으로 바뀝니다. 계속할까요?", "되돌리기", "취소"))
            {
                Undo.RecordObject(target, "Reset Resource Spawn Table");
                ((ResourceSpawnTable)target).ResetToDefaults();
                EditorUtility.SetDirty(target);
            }
        }
    }

    // 속성 하나를 지정한 너비로 그린다 (문자열은 한 줄 입력칸)
    private static void DrawField(SerializedProperty p, float width)
    {
        if (p == null)
        {
            GUILayout.Label("?", GUILayout.Width(width));
            return;
        }
        if (p.propertyType == SerializedPropertyType.String)
            p.stringValue = EditorGUILayout.TextField(p.stringValue, GUILayout.Width(width));
        else
            EditorGUILayout.PropertyField(p, GUIContent.none, GUILayout.Width(width));
    }
}
