using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 메뉴 Soul > 데이터 표: 아이템 / 자원 / 몬스터 데이터를 한 창의 표에서 보고 바로 수정한다.
// 칸을 고치면 해당 에셋에 그대로 저장된다 (저장 버튼 / 창을 닫거나 다른 창으로 이동할 때 파일에 기록).
public class DataTableWindow : EditorWindow
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

    private enum Mode { Item, Resource, Monster, Level, SPCost, Proficiency, PlayerBase, Sound }
    private static readonly string[] ModeNames = { "아이템", "자원", "몬스터", "레벨", "SP 소모", "숙련도", "기본 능력치", "효과음" };

    private static readonly string[] ItemTabNames = { "기본", "도구", "장비 스탯", "레시피" };
    private static readonly string[] MonsterTabNames = { "기본", "능력치", "보상", "출현 지역", "도망/도려내기" };

    // ===== 아이템 열 =====
    private static readonly Col[] ItemLead =
    {
        C("ID", "id", 45, "고유 번호 (겹치면 빨간색)"),
        C("이름", "itemName", 120, null, true),
    };

    private static readonly Col[] ItemBasic = ItemLead.Concat(new[]
    {
        C("무게", "weight", 60, "1개당 무게"),
        C("슬롯당 최대", "countmax", 75, "한 슬롯에 쌓이는 최대 수량 (도구/장비는 1, 0이면 제한 없음)"),
        C("아이콘", "icon", 110),
        C("설명", "description", 300, null, true),
    }).ToArray();

    private static readonly Col[] ItemTool = ItemLead.Concat(new[]
    {
        C("도구 종류", "toolType", 90),
        C("티어", "tier", 50, "필요/보유 도구 티어"),
        C("채집량", "toolattack", 60, "한 번 채집할 때 얻는 양"),
        C("최대 내구도", "durabilitymax", 80, "도구가 아니면 0"),
    }).ToArray();

    private static readonly Col[] ItemStats = ItemLead.Concat(new[]
    {
        C("공격력", "at", 60), C("방어력", "df", 60), C("체력", "hp", 60), C("회복량", "heal", 60),
        C("체력 공격력", "hprateat", 70, "체력 비례 공격력"), C("고정 공격력", "fixat", 70),
        C("관통률", "breakdf", 60, "방어 무시"), C("흡수율", "abs", 60), C("회피율", "avoid", 60),
        C("치명타 확률", "criticalrate", 70), C("치명타 배율", "critical", 70),
        C("최대 마나", "manamax", 65), C("마나 회복", "manaheal", 65),
        C("공격력 배율", "atrate", 70), C("방어력 배율", "dfrate", 70), C("체력 배율", "hprate", 65),
        C("회복량 배율", "healrate", 70), C("골드 배율", "goldrate", 65), C("경험치 배율", "exprate", 70),
        C("최대 스태미나", "spmax", 75), C("스태미나 회복", "spheal", 75), C("가방 증량", "weightmax", 65, "소지 최대 무게 증가량 (장비)"),
        C("슬롯 증가", "slotmax", 65, "가방 슬롯 개수 증가량 (장비)"),
        C("특수 효과", "specialEffect", 200, null, true),
    }).ToArray();

    private static readonly Col[] ItemRecipe = ItemLead.Concat(new[]
    {
        C("1회 생산", "recipe.resultAmount", 65, "제작 1회에 만들어지는 수량"),
    }).ToArray();

    // ===== 몬스터 열 =====
    private static readonly Col[] MonsterLead =
    {
        C("ID", "id", 45, "고유 번호 (겹치면 빨간색)"),
        C("이름", "monsterName", 120, null, true),
    };

    private static readonly Col[] MonsterBasic = MonsterLead.Concat(new[]
    {
        C("레벨", "level", 50),
        C("이미지", "sprite", 110),
        C("설명", "description", 300, null, true),
    }).ToArray();

    private static readonly Col[] MonsterStats = MonsterLead.Concat(new[]
    {
        C("체력", "hpMax", 60, "최대 체력"), C("공격력", "at", 60), C("방어력", "df", 60),
        C("속도", "speed", 55, "높을수록 먼저 행동"), C("회피율", "avoid", 60, "%"),
        C("치명타 확률", "criticalrate", 70, "%"), C("치명타 피해", "critical", 70, "% 증가"),
        C("방어 확률", "defendChance", 65, "자기 턴에 방어를 고를 확률 [%]"),
        C("저체력 방어", "defendChanceLowHp", 75, "체력 30% 이하일 때 방어 확률 [%]"),
        C("제압 확률", "suppressChance", 70, "턴마다 플레이어를 제압할 확률 [%]"),
    }).ToArray();

    private static readonly Col[] MonsterReward = MonsterLead.Concat(new[]
    {
        C("경험치", "exp", 60), C("골드", "gold", 60),
    }).ToArray();

    private static readonly Col[] MonsterSpawnCols = MonsterLead.ToArray();

    private static readonly Col[] MonsterFleeCarve = MonsterLead.Concat(new[]
    {
        C("도망 체력%", "fleeHpPercent", 80, "체력이 이 비율[%] 이하가 되면 도망칠 수 있음 (0이면 도망치지 않음)"),
        C("도망 확률", "fleeChance", 70, "그 상태에서 자기 턴마다 도망칠 확률 [%]"),
        C("도려내기 횟수", "carveCount", 85, "쓰러뜨린 뒤 도려낼 수 있는 횟수 (0이면 불가)"),
        C("필요 칼 티어", "carveToolTier", 85, "도려내는 데 필요한 칼의 티어"),
    }).ToArray();

    // ===== 상태 =====
    private Mode mode;
    private int itemTab, monsterTab;
    private List<SerializedObject> rows = new List<SerializedObject>();
    private Dictionary<int, string> itemNames = new Dictionary<int, string>();
    private SerializedObject resourceTable; // [자원] 탭이 보여 주는 ResourceSpawnTable 에셋
    private SerializedObject ruleTable;     // [레벨] / [SP 소모] / [숙련도] 탭이 보여 주는 규칙 표 에셋
    private Vector2 scroll;
    private string search = "";
    private bool dirty;

    [MenuItem("Soul/데이터 표")]
    private static void Open()
    {
        GetWindow<DataTableWindow>("데이터 표").Show();
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

    // 현재 모드의 에셋을 id 순서로 불러온다
    private void Refresh()
    {
        // 아이템 이름은 재료/드랍/자원표의 ID 옆에 보여 주려고 항상 읽어 둔다
        Item[] items = AssetDatabase.FindAssets("t:Item")
            .Select(g => AssetDatabase.LoadAssetAtPath<Item>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(i => i != null).OrderBy(i => i.id).ToArray();
        itemNames = new Dictionary<int, string>();
        foreach (Item item in items)
            if (!itemNames.ContainsKey(item.id)) itemNames[item.id] = item.itemName;
        ResourceTableDrawer.RefreshNames();

        rows = new List<SerializedObject>();
        resourceTable = null;
        ruleTable = null;

        switch (mode)
        {
            case Mode.Level:
                ruleTable = LoadRuleTable("t:LevelTable");
                break;
            case Mode.SPCost:
                ruleTable = LoadRuleTable("t:SPCostTable");
                break;
            case Mode.Proficiency:
                ruleTable = LoadRuleTable("t:ProficiencyTable");
                break;
            case Mode.PlayerBase:
                ruleTable = LoadRuleTable("t:PlayerBaseTable");
                break;
            case Mode.Sound:
                ruleTable = LoadRuleTable("t:SoundTable");
                break;
            case Mode.Item:
                rows = items.Select(i => new SerializedObject(i)).ToList();
                break;
            case Mode.Monster:
                rows = AssetDatabase.FindAssets("t:Monster")
                    .Select(g => AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(m => m != null).OrderBy(m => m.id)
                    .Select(m => new SerializedObject(m)).ToList();
                break;
            case Mode.Resource:
                string guid = AssetDatabase.FindAssets("t:ResourceSpawnTable").FirstOrDefault();
                if (guid != null)
                {
                    var table = AssetDatabase.LoadAssetAtPath<ResourceSpawnTable>(AssetDatabase.GUIDToAssetPath(guid));
                    if (table != null) resourceTable = new SerializedObject(table);
                }
                break;
        }
    }

    // ===== [+ 새 아이템] / [+ 새 몬스터]: 에셋을 만들고 도감에 자동 등록 =====
    // 에셋이 이미 있는 폴더에 만들고(없으면 fallback 폴더를 만듦), 파일 이름은 "번호_이름.asset"
    private static string FolderOf<T>(string fallback) where T : Object
    {
        string guid = AssetDatabase.FindAssets("t:" + typeof(T).Name).FirstOrDefault();
        if (guid != null)
            return Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guid)).Replace("\\", "/");

        if (!AssetDatabase.IsValidFolder(fallback))
            AssetDatabase.CreateFolder("Assets", fallback.Substring("Assets/".Length));
        return fallback;
    }

    private void CreateNewItem()
    {
        Save();
        int newId = AssetDatabase.FindAssets("t:Item")
            .Select(g => AssetDatabase.LoadAssetAtPath<Item>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(i => i != null).Select(i => i.id).DefaultIfEmpty(-1).Max() + 1;

        Item item = ScriptableObject.CreateInstance<Item>();
        item.id = newId;
        item.itemName = "새 아이템";
        item.countmax = 99;

        // 아이콘: 2Sprites 폴더에서 ID에 맞는 4자리 이름의 스프라이트를 찾아 넣는다 (ID 14 -> 0014)
        item.icon = DatabaseSync.FindSpriteForId(newId);
        if (item.icon == null)
            Debug.LogWarning($"[데이터 표] 2Sprites 폴더에서 '{newId:0000}' 스프라이트를 찾지 못해 아이콘을 비워 두었습니다.");

        // 파일 이름은 "번호_이름" (번호는 3자리)
        string path = AssetDatabase.GenerateUniqueAssetPath($"{FolderOf<Item>("Assets/1Item")}/{DatabaseSync.ItemFileName(item)}.asset");
        AssetDatabase.CreateAsset(item, path);
        AssetDatabase.SaveAssets();
        FinishCreate();
    }

    private void CreateNewMonster()
    {
        Save();
        int newId = AssetDatabase.FindAssets("t:Monster")
            .Select(g => AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(m => m != null).Select(m => m.id).DefaultIfEmpty(-1).Max() + 1;

        Monster monster = ScriptableObject.CreateInstance<Monster>();
        monster.id = newId;
        monster.monsterName = "새 몬스터";

        string path = AssetDatabase.GenerateUniqueAssetPath($"{FolderOf<Monster>("Assets/6Monsters")}/{newId:000}_새 몬스터.asset");
        AssetDatabase.CreateAsset(monster, path);
        AssetDatabase.SaveAssets();
        FinishCreate();
    }

    // 도감에 등록하고 표를 새로 읽은 뒤, 새 줄이 보이도록 맨 아래로 스크롤
    private void FinishCreate()
    {
        DatabaseSync.SyncAll();
        Refresh();
        scroll = new Vector2(scroll.x, float.MaxValue);
        Repaint();
    }

    // 프로젝트에서 규칙 표 에셋 하나를 찾아 편집용으로 연다 (없으면 null)
    private static SerializedObject LoadRuleTable(string filter)
    {
        string guid = AssetDatabase.FindAssets(filter).FirstOrDefault();
        if (guid == null) return null;
        Object asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
        return asset != null ? new SerializedObject(asset) : null;
    }

    private void Save()
    {
        if (!dirty) return;
        AssetDatabase.SaveAssets();
        dirty = false;

        // 표에서 아이템의 번호나 이름을 바꿨다면 파일 이름도 "번호_이름"에 맞춘다
        if (mode == Mode.Item) DatabaseSync.NormalizeItemFileNames();
    }

    private void OnGUI()
    {
        DrawToolbar();

        switch (mode)
        {
            case Mode.Resource: DrawResourceMode(); break;
            case Mode.Level:
            case Mode.SPCost:
            case Mode.Proficiency:
            case Mode.PlayerBase:
            case Mode.Sound: DrawRuleMode(); break;
            default: DrawRowsMode(); break;
        }
    }

    // ===== [레벨] / [SP 소모] / [숙련도] 탭: 에셋 하나가 표 하나 =====
    private void DrawRuleMode()
    {
        if (ruleTable == null || ruleTable.targetObject == null)
        {
            EditorGUILayout.HelpBox(
                "표 에셋이 아직 없습니다. 메뉴 Soul > 규칙 표 에셋 만들기 (없는 것만) 를 누르거나 에디터를 다시 열면 자동으로 만들어집니다.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"에셋: {AssetDatabase.GetAssetPath(ruleTable.targetObject)}", EditorStyles.miniLabel);

        bool changed = false;
        switch (mode)
        {
            case Mode.Level: changed = RuleTableDrawers.DrawLevelTable(ruleTable, ref scroll); break;
            case Mode.SPCost: changed = RuleTableDrawers.DrawSPCostTable(ruleTable); break;
            case Mode.Proficiency: changed = RuleTableDrawers.DrawProficiencyTable(ruleTable); break;
            case Mode.PlayerBase: changed = RuleTableDrawers.DrawPlayerBaseTable(ruleTable); break;
            case Mode.Sound: changed = RuleTableDrawers.DrawSoundTable(ruleTable); break;
        }

        if (changed)
        {
            EditorUtility.SetDirty(ruleTable.targetObject);
            dirty = true;
        }
    }

    // 위쪽 줄: 모드, 탭, 검색, 새로고침, 저장
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        int newMode = GUILayout.Toolbar((int)mode, ModeNames, EditorStyles.toolbarButton, GUILayout.Width(560));
        if (newMode != (int)mode)
        {
            Save();
            mode = (Mode)newMode;
            scroll = Vector2.zero;
            Refresh();
        }

        GUILayout.Space(12);
        if (mode == Mode.Item)
            itemTab = GUILayout.Toolbar(itemTab, ItemTabNames, EditorStyles.toolbarButton, GUILayout.Width(380));
        else if (mode == Mode.Monster)
            monsterTab = GUILayout.Toolbar(monsterTab, MonsterTabNames, EditorStyles.toolbarButton, GUILayout.Width(480));

        if (mode == Mode.Item || mode == Mode.Monster)
        {
            GUILayout.Space(12);
            GUILayout.Label("검색", GUILayout.Width(30));
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(160));

            // 새 항목 추가: 에셋을 만들고 도감에 자동 등록
            GUILayout.Space(8);
            if (GUILayout.Button(mode == Mode.Item ? "+ 새 아이템" : "+ 새 몬스터", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                if (mode == Mode.Item) CreateNewItem();
                else CreateNewMonster();
            }
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(60))) Refresh();
        if (GUILayout.Button(dirty ? "저장 *" : "저장", EditorStyles.toolbarButton, GUILayout.Width(55))) Save();
        EditorGUILayout.EndHorizontal();
    }

    // ===== [자원] 탭 =====
    private void DrawResourceMode()
    {
        if (resourceTable == null || resourceTable.targetObject == null)
        {
            EditorGUILayout.HelpBox(
                "ResourceSpawnTable 에셋이 없습니다. Project 창에서 Create > Soul > Resource Spawn Table 로 만드세요.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"에셋: {AssetDatabase.GetAssetPath(resourceTable.targetObject)}", EditorStyles.miniLabel);
        if (ResourceTableDrawer.Draw(resourceTable, ref scroll))
        {
            EditorUtility.SetDirty(resourceTable.targetObject);
            dirty = true;
        }
    }

    // ===== [아이템] / [몬스터] 탭: 에셋 하나가 한 줄 =====
    private Col[] ColsForTab()
    {
        if (mode == Mode.Item)
        {
            switch (itemTab)
            {
                case 1: return ItemTool;
                case 2: return ItemStats;
                case 3: return ItemRecipe;
                default: return ItemBasic;
            }
        }
        switch (monsterTab)
        {
            case 1: return MonsterStats;
            case 2: return MonsterReward;
            case 3: return MonsterSpawnCols;
            case 4: return MonsterFleeCarve;
            default: return MonsterBasic;
        }
    }

    // 표 오른쪽에 붙는 "목록형" 칸(재료/드랍/출현 지역)의 머리글. 없으면 null
    private string ExtraHeader()
    {
        if (mode == Mode.Item && itemTab == 3) return "재료 (아이템 ID x 수량)   |   필요 도구 (종류, T=티어, 내구도 소모)";
        if (mode == Mode.Monster && monsterTab == 2) return "드랍 (아이템 ID x 최소~최대 @확률%)";
        if (mode == Mode.Monster && monsterTab == 3) return "출현 (지역 ID / 비중)";
        if (mode == Mode.Monster && monsterTab == 4) return "도려내서 얻는 것 (아이템 ID x 최소~최대 @확률%)";
        return null;
    }

    private string NamePath() { return mode == Mode.Item ? "itemName" : "monsterName"; }

    private void DrawRowsMode()
    {
        if (rows.Count == 0)
        {
            EditorGUILayout.HelpBox(mode == Mode.Item
                ? "Item 에셋이 없습니다. Project 창에서 Create > Soul > Item 으로 만드세요."
                : "Monster 에셋이 없습니다. Project 창에서 Create > Soul > Monster 로 만드세요.", MessageType.Info);
            return;
        }

        Col[] cols = ColsForTab();
        string extraHeader = ExtraHeader();

        // 겹치는 id 찾기 (빨간색으로 표시)
        HashSet<int> duplicates = new HashSet<int>(
            rows.Where(r => r.targetObject != null)
                .GroupBy(r => r.FindProperty("id").intValue).Where(g => g.Count() > 1).Select(g => g.Key));

        scroll = EditorGUILayout.BeginScrollView(scroll);

        // 머리글
        GUIStyle header = new GUIStyle(EditorStyles.miniBoldLabel)
        { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        EditorGUILayout.BeginHorizontal();
        foreach (Col c in cols)
            GUILayout.Label(new GUIContent(c.title, c.tip), header, GUILayout.Width(c.width), GUILayout.Height(30));
        if (extraHeader != null) GUILayout.Label(extraHeader, header, GUILayout.Width(extraHeader.Length > 40 ? 760 : 480), GUILayout.Height(30));
        EditorGUILayout.EndHorizontal();

        // 줄
        foreach (SerializedObject so in rows)
        {
            if (so.targetObject == null) continue;
            so.Update();

            int id = so.FindProperty("id").intValue;
            string rowName = so.FindProperty(NamePath()).stringValue;
            if (!MatchesSearch(id, rowName)) continue;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();

            foreach (Col c in cols)
            {
                Color prev = GUI.backgroundColor;
                if (c.path == "id" && duplicates.Contains(id)) GUI.backgroundColor = Color.red;
                DrawCell(so, c);
                GUI.backgroundColor = prev;
            }

            if (mode == Mode.Item && itemTab == 3)
            {
                DrawIngredients(so);
                DrawRecipeTools(so);
            }
            else if (mode == Mode.Monster && monsterTab == 2)
                DrawDrops(so, "drops");
            else if (mode == Mode.Monster && monsterTab == 3)
                DrawSpawns(so);
            else if (mode == Mode.Monster && monsterTab == 4)
                DrawDrops(so, "carveDrops");

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
            EditorGUILayout.HelpBox("ID가 겹치는 항목이 있습니다 (빨간색). 겹치면 도감에서 하나만 쓰입니다.", MessageType.Warning);
    }

    private bool MatchesSearch(int id, string rowName)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        string s = search.Trim();
        return (rowName != null && rowName.Contains(s)) || id.ToString() == s;
    }

    private static void DrawCell(SerializedObject so, Col c)
    {
        // PropertyField 대신 TableField: [Header] 제목이 같이 그려져 칸이 한 줄 아래로 밀리는 것을 막음
        TableField.Draw(so.FindProperty(c.path), c.width);
    }

    private string ItemNameOf(int id)
    {
        return itemNames.TryGetValue(id, out string found) ? found : "(없음)";
    }

    // 아이템 레시피: 재료를 [아이템ID (이름) x 수량 -] 로 한 줄에 나열하고 [+]로 추가
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
            GUILayout.Label(ItemNameOf(idProp.intValue), EditorStyles.miniLabel, GUILayout.Width(62));
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

    // 몬스터 드랍: [아이템ID (이름) x 최소~최대 @확률% -]
    private void DrawDrops(SerializedObject so, string listPath)
    {
        SerializedProperty list = so.FindProperty(listPath);
        if (list == null) return;

        int removeAt = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty d = list.GetArrayElementAtIndex(i);
            SerializedProperty idProp = d.FindPropertyRelative("itemId");
            EditorGUILayout.PropertyField(idProp, GUIContent.none, GUILayout.Width(34));
            GUILayout.Label(ItemNameOf(idProp.intValue), EditorStyles.miniLabel, GUILayout.Width(56));
            GUILayout.Label("x", GUILayout.Width(10));
            EditorGUILayout.PropertyField(d.FindPropertyRelative("amountMin"), GUIContent.none, GUILayout.Width(28));
            GUILayout.Label("~", GUILayout.Width(10));
            EditorGUILayout.PropertyField(d.FindPropertyRelative("amountMax"), GUIContent.none, GUILayout.Width(28));
            GUILayout.Label("@", GUILayout.Width(12));
            EditorGUILayout.PropertyField(d.FindPropertyRelative("chance"), GUIContent.none, GUILayout.Width(40));
            GUILayout.Label("%", GUILayout.Width(14));
            if (GUILayout.Button("-", GUILayout.Width(20))) removeAt = i;
            GUILayout.Space(6);
        }
        if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);

        if (GUILayout.Button("+", GUILayout.Width(24)))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("itemId").intValue = 0;
            added.FindPropertyRelative("amountMin").intValue = 1;
            added.FindPropertyRelative("amountMax").intValue = 1;
            added.FindPropertyRelative("chance").floatValue = 100f;
        }
    }

    // 아이템 레시피의 필요 도구: [종류 T티어 내구도소모 -] [+]. 제작하면 도구의 내구도만 깎인다.
    private void DrawRecipeTools(SerializedObject so)
    {
        SerializedProperty list = so.FindProperty("recipe.tools");
        if (list == null) return;

        GUILayout.Space(14);
        GUILayout.Label("도구", EditorStyles.miniBoldLabel, GUILayout.Width(28));

        int removeAt = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty t = list.GetArrayElementAtIndex(i);
            TableField.Draw(t.FindPropertyRelative("toolType"), 72);
            GUILayout.Label("T", EditorStyles.miniLabel, GUILayout.Width(10));
            TableField.Draw(t.FindPropertyRelative("tier"), 28);
            GUILayout.Label("-", EditorStyles.miniLabel, GUILayout.Width(8));
            SerializedProperty cost = t.FindPropertyRelative("durabilityCost");
            TableField.Draw(cost, 30);
            cost.intValue = Mathf.Max(1, cost.intValue);
            if (GUILayout.Button("x", GUILayout.Width(20))) removeAt = i;
            GUILayout.Space(8);
        }
        if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);

        if (GUILayout.Button("+", GUILayout.Width(24)))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("tier").intValue = 0;
            added.FindPropertyRelative("durabilityCost").intValue = 1;
        }
    }

    // 몬스터 출현 지역: [지역 ID / 비중 -]
    private void DrawSpawns(SerializedObject so)
    {
        SerializedProperty list = so.FindProperty("spawns");
        if (list == null) return;

        int removeAt = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty s = list.GetArrayElementAtIndex(i);
            GUILayout.Label("지역", EditorStyles.miniLabel, GUILayout.Width(26));
            EditorGUILayout.PropertyField(s.FindPropertyRelative("regionId"), GUIContent.none, GUILayout.Width(34));
            GUILayout.Label("비중", EditorStyles.miniLabel, GUILayout.Width(26));
            EditorGUILayout.PropertyField(s.FindPropertyRelative("weight"), GUIContent.none, GUILayout.Width(34));
            if (GUILayout.Button("-", GUILayout.Width(20))) removeAt = i;
            GUILayout.Space(8);
        }
        if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);

        if (GUILayout.Button("+", GUILayout.Width(24)))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("regionId").intValue = 1;
            added.FindPropertyRelative("weight").intValue = 1;
        }
    }
}
