using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 메뉴 Soul > 아이템 표: 프로젝트의 모든 Item 에셋을 한 표에서 보고 바로 수정한다.
// 칸을 고치면 해당 아이템 에셋에 그대로 저장된다 (저장 버튼 / 창을 닫거나 다른 창으로 이동할 때 파일에 기록).
public class ItemTableWindow : EditorWindow
{
    private class Col
    {
        public string title, tip, path;
        public float width;
        public bool text;
    }

    private static Col C(string title, string path, float width, string tip = null, bool text = false)
    {
        return new Col { title = title, path = path, width = width, tip = tip, text = text };
    }

    private static readonly string[] TabNames = { "기본", "도구", "장비 스탯", "레시피" };

    // 모든 탭의 맨 앞에 나오는 열
    private static readonly Col[] Lead =
    {
        C("ID", "id", 45, "고유 번호 (겹치면 빨간색)"),
        C("이름", "itemName", 120, null, true),
    };

    private static readonly Col[] BasicCols = Lead.Concat(new[]
    {
        C("무게", "weight", 60, "1개당 무게"),
        C("슬롯당 최대", "countmax", 75, "한 슬롯에 쌓이는 최대 수량 (도구/장비는 1, 0이면 제한 없음)"),
        C("아이콘", "icon", 110),
        C("설명", "description", 300, null, true),
    }).ToArray();

    private static readonly Col[] ToolCols = Lead.Concat(new[]
    {
        C("도구 종류", "toolType", 90),
        C("티어", "tier", 50, "필요/보유 도구 티어"),
        C("채집량", "toolattack", 60, "한 번 채집할 때 얻는 양"),
        C("최대 내구도", "durabilitymax", 80, "도구가 아니면 0"),
    }).ToArray();

    private static readonly Col[] StatCols = Lead.Concat(new[]
    {
        C("공격력", "at", 60), C("방어력", "df", 60), C("체력", "hp", 60), C("회복량", "heal", 60),
        C("체력 공격력", "hprateat", 70, "체력 비례 공격력"), C("고정 공격력", "fixat", 70),
        C("관통률", "breakdf", 60, "방어 무시"), C("흡수율", "abs", 60), C("회피율", "avoid", 60),
        C("치명타 확률", "criticalrate", 70), C("치명타 배율", "critical", 70),
        C("최대 마나", "manamax", 65), C("마나 회복", "manaheal", 65),
        C("공격력 배율", "atrate", 70), C("방어력 배율", "dfrate", 70), C("체력 배율", "hprate", 65),
        C("회복량 배율", "healrate", 70), C("골드 배율", "goldrate", 65), C("경험치 배율", "exprate", 70),
        C("최대 스태미나", "spmax", 75), C("스태미나 회복", "spheal", 75), C("가방 증량", "weightmax", 65, "소지 무게 증가량"),
        C("특수 효과", "specialEffect", 200, null, true),
    }).ToArray();

    private static readonly Col[] RecipeCols = Lead.Concat(new[]
    {
        C("1회 생산", "recipe.resultAmount", 65, "제작 1회에 만들어지는 수량"),
    }).ToArray();

    private List<SerializedObject> rows = new List<SerializedObject>();
    private Dictionary<int, string> names = new Dictionary<int, string>();
    private Vector2 scroll;
    private int tab;
    private string search = "";
    private bool dirty;

    [MenuItem("Soul/아이템 표")]
    private static void Open()
    {
        GetWindow<ItemTableWindow>("아이템 표").Show();
    }

    private void OnEnable()
    {
        Refresh();
        Undo.undoRedoPerformed += Repaint;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= Repaint;
        Save();
    }

    private void OnLostFocus() { Save(); }
    private void OnProjectChange() { Refresh(); Repaint(); }

    // 프로젝트의 모든 Item 에셋을 id 순서로 불러온다
    private void Refresh()
    {
        rows = AssetDatabase.FindAssets("t:Item")
            .Select(g => AssetDatabase.LoadAssetAtPath<Item>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(i => i != null)
            .OrderBy(i => i.id)
            .Select(i => new SerializedObject(i))
            .ToList();

        names = new Dictionary<int, string>();
        foreach (SerializedObject so in rows)
        {
            int id = so.FindProperty("id").intValue;
            if (!names.ContainsKey(id)) names[id] = so.FindProperty("itemName").stringValue;
        }
    }

    private void Save()
    {
        if (!dirty) return;
        AssetDatabase.SaveAssets();
        dirty = false;
    }

    private Col[] ColsForTab()
    {
        switch (tab)
        {
            case 1: return ToolCols;
            case 2: return StatCols;
            case 3: return RecipeCols;
            default: return BasicCols;
        }
    }

    private void OnGUI()
    {
        // 위쪽 줄: 탭, 검색, 새로고침, 저장
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        tab = GUILayout.Toolbar(tab, TabNames, EditorStyles.toolbarButton, GUILayout.Width(380));
        GUILayout.Space(12);
        GUILayout.Label("검색", GUILayout.Width(30));
        search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(160));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(60))) Refresh();
        if (GUILayout.Button(dirty ? "저장 *" : "저장", EditorStyles.toolbarButton, GUILayout.Width(55))) Save();
        EditorGUILayout.EndHorizontal();

        if (rows.Count == 0)
        {
            EditorGUILayout.HelpBox("Item 에셋이 없습니다. Project 창에서 Create > Soul > Item 으로 만드세요.", MessageType.Info);
            return;
        }

        Col[] cols = ColsForTab();

        // 겹치는 id 찾기 (빨간색으로 표시)
        HashSet<int> duplicates = new HashSet<int>(
            rows.GroupBy(r => r.FindProperty("id").intValue).Where(g => g.Count() > 1).Select(g => g.Key));

        scroll = EditorGUILayout.BeginScrollView(scroll);

        // 머리글
        GUIStyle header = new GUIStyle(EditorStyles.miniBoldLabel)
        { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        EditorGUILayout.BeginHorizontal();
        foreach (Col c in cols)
            GUILayout.Label(new GUIContent(c.title, c.tip), header, GUILayout.Width(c.width), GUILayout.Height(30));
        if (tab == 3) GUILayout.Label("재료 (아이템 ID x 수량)", header, GUILayout.Width(420), GUILayout.Height(30));
        EditorGUILayout.EndHorizontal();

        // 줄
        foreach (SerializedObject so in rows)
        {
            if (so.targetObject == null) continue;
            so.Update();

            int id = so.FindProperty("id").intValue;
            string itemName = so.FindProperty("itemName").stringValue;
            if (!MatchesSearch(id, itemName)) continue;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();

            foreach (Col c in cols)
            {
                Color prev = GUI.backgroundColor;
                if (c.path == "id" && duplicates.Contains(id)) GUI.backgroundColor = Color.red;
                DrawCell(so, c);
                GUI.backgroundColor = prev;
            }
            if (tab == 3) DrawIngredients(so);

            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(so.targetObject);
                dirty = true;
            }
        }

        EditorGUILayout.EndScrollView();

        if (duplicates.Count > 0)
            EditorGUILayout.HelpBox("ID가 겹치는 아이템이 있습니다 (빨간색). 겹치면 도감에서 하나만 쓰입니다.", MessageType.Warning);
    }

    private bool MatchesSearch(int id, string itemName)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        string s = search.Trim();
        return itemName.Contains(s) || id.ToString() == s;
    }

    private static void DrawCell(SerializedObject so, Col c)
    {
        SerializedProperty p = so.FindProperty(c.path);
        if (p == null)
        {
            GUILayout.Label("?", GUILayout.Width(c.width));
            return;
        }
        if (c.text && p.propertyType == SerializedPropertyType.String)
            p.stringValue = EditorGUILayout.TextField(p.stringValue, GUILayout.Width(c.width));
        else
            EditorGUILayout.PropertyField(p, GUIContent.none, GUILayout.Width(c.width));
    }

    // 레시피 탭: 재료를 [아이템ID (이름) x 수량 -] 로 한 줄에 나열하고 [+]로 추가
    private void DrawIngredients(SerializedObject so)
    {
        SerializedProperty list = so.FindProperty("recipe.ingredients");
        if (list == null) return;

        int removeAt = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty ing = list.GetArrayElementAtIndex(i);
            SerializedProperty idProp = ing.FindPropertyRelative("itemId");
            EditorGUILayout.PropertyField(idProp, GUIContent.none, GUILayout.Width(36));

            string n = names.TryGetValue(idProp.intValue, out string found) ? found : "(없음)";
            GUILayout.Label(n, EditorStyles.miniLabel, GUILayout.Width(62));
            GUILayout.Label("x", GUILayout.Width(10));
            EditorGUILayout.PropertyField(ing.FindPropertyRelative("amount"), GUIContent.none, GUILayout.Width(34));
            if (GUILayout.Button("-", GUILayout.Width(20))) removeAt = i;
            GUILayout.Space(6);
        }
        if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);

        if (GUILayout.Button("+", GUILayout.Width(24)))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("itemId").intValue = 0;
            added.FindPropertyRelative("amount").intValue = 1;
        }
    }
}
