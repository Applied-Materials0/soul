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

    private enum Mode { Item, Resource, Monster, Level, SPCost, Proficiency, PlayerBase, Sound, Search, Trait, Grade, Type }
    private static readonly string[] ModeNames = { "아이템", "자원", "몬스터", "레벨", "SP 소모", "숙련도", "기본 능력치", "효과음", "탐색 결과", "특성", "등급", "타입" };

    private static readonly string[] ItemTabNames = { "기본", "도구", "장비 스탯", "레시피", "사용 효과", "수리/등급" };
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
        C("장착 부위", "equipSlot", 80, "장비창에 끼는 부위 (None이면 장착 불가)"), C("특성ID", "traitId", 65, "특성 표의 ID (0 = 없음). 장착해야 적용됨. 예: 1 = 독"),
        C("공격력", "at", 60), C("방어력", "df", 60), C("체력", "hp", 60),
        C("체력 공격력", "hprateat", 70, "체력 비례 공격력"), C("고정 공격력", "fixat", 70),
        C("관통률", "breakdf", 60, "방어 무시"), C("흡수율", "abs", 60), C("회피율", "avoid", 60),
        C("치명타 확률", "criticalrate", 70), C("치명타 배율", "critical", 70),
        C("최대 마나", "manamax", 65),
        C("공격력 배율", "atrate", 70), C("방어력 배율", "dfrate", 70), C("체력 배율", "hprate", 65),
        C("회복량 배율", "healrate", 70), C("골드 배율", "goldrate", 65), C("경험치 배율", "exprate", 70),
        C("최대 스태미나", "spmax", 75), C("가방 증량", "weightmax", 65, "소지 최대 무게 증가량 (장비)"),
        C("슬롯 증가", "slotmax", 65, "가방 슬롯 개수 증가량 (장비)"),
        C("특수 효과", "specialEffect", 200, null, true),
    }).ToArray();

    private static readonly Col[] ItemRecipe = ItemLead.Concat(new[]
    {
        C("1회 생산", "recipe.resultAmount", 65, "제작 1회에 만들어지는 수량"),
    }).ToArray();

    private static readonly Col[] ItemUse = ItemLead.Concat(new[]
    {
        C("체력 회복", "useHp", 75, "사용하면 회복하는 체력 (하나라도 0보다 크면 인벤토리에서 [사용] 가능)"),
        C("SP 회복", "useSp", 75, "사용하면 회복하는 SP"),
        C("마나 회복", "useMana", 75, "사용하면 회복하는 마나"),
        C("사용 후 남는 아이템ID", "useResultItemId", 120, "사용하면 이 아이템 1개가 남음 (예: 가득찬 물통 -> 물통). 내구도는 이어받음. 0이면 없음"),
    }).ToArray();

    private static readonly Col[] ItemRepair = ItemLead.Concat(new[]
    {
        C("차는 내구도", "repairRestore", 85, "수리하면 차는 내구도 (0이면 가득 참)"),
        C("경험치/내구도", "wearExp", 85, "내구도를 1 쓸 때마다 쌓이는 장비 경험치 (등급 표의 경험치로 등급이 오름)"),
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
    private Vector2 ruleScroll; // 규칙 표 화면의 세로 스크롤
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

        if (mode == Mode.Type) TypeListDrawer.Refresh();
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
            case Mode.Search:
                ruleTable = LoadRuleTable("t:SearchTable");
                break;
            case Mode.Trait:
                ruleTable = LoadRuleTable("t:TraitTable");
                break;
            case Mode.Grade:
                ruleTable = LoadRuleTable("t:GradeTable");
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

    // forcedId가 0 이상이면 그 번호로, 아니면 가장 큰 번호 + 1로 만든다
    private void CreateNewItem(int forcedId = -1)
    {
        Save();
        int newId = forcedId >= 0 ? forcedId : AssetDatabase.FindAssets("t:Item")
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

    private void CreateNewMonster(int forcedId = -1)
    {
        Save();
        int newId = forcedId >= 0 ? forcedId : AssetDatabase.FindAssets("t:Monster")
            .Select(g => AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(m => m != null).Select(m => m.id).DefaultIfEmpty(-1).Max() + 1;

        Monster monster = ScriptableObject.CreateInstance<Monster>();
        monster.id = newId;
        monster.monsterName = "새 몬스터";

        // 파일 이름은 "번호_이름" (번호는 3자리)
        string path = AssetDatabase.GenerateUniqueAssetPath($"{FolderOf<Monster>("Assets/6Monsters")}/{DatabaseSync.MonsterFileName(monster)}.asset");
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

        // 표에서 아이템이나 몬스터의 번호나 이름을 바꿨다면 파일 이름도 "번호_이름"에 맞춘다
        if (mode == Mode.Item) DatabaseSync.NormalizeItemFileNames();
        if (mode == Mode.Monster) DatabaseSync.NormalizeMonsterFileNames();
    }

    private void OnGUI()
    {
        DrawToolbar();

        switch (mode)
        {
            case Mode.Resource: DrawResourceMode(); break;
            case Mode.Type: TypeListDrawer.Draw(ref scroll); break;
            case Mode.Level:
            case Mode.SPCost:
            case Mode.Proficiency:
            case Mode.PlayerBase:
            case Mode.Grade:
            case Mode.Trait:
            case Mode.Search:
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

        // 숙련도/특성처럼 표가 길어도 아래로 스크롤되도록 전체를 스크롤 영역에 넣는다 (레벨 표는 자체 스크롤이 있음)
        bool outerScroll = mode != Mode.Level;
        if (outerScroll) ruleScroll = EditorGUILayout.BeginScrollView(ruleScroll);
        switch (mode)
        {
            case Mode.Level: changed = RuleTableDrawers.DrawLevelTable(ruleTable, ref scroll); break;
            case Mode.SPCost: changed = RuleTableDrawers.DrawSPCostTable(ruleTable); break;
            case Mode.Proficiency: changed = RuleTableDrawers.DrawProficiencyTable(ruleTable); break;
            case Mode.PlayerBase: changed = RuleTableDrawers.DrawPlayerBaseTable(ruleTable); break;
            case Mode.Sound: changed = RuleTableDrawers.DrawSoundTable(ruleTable); break;
            case Mode.Search: changed = RuleTableDrawers.DrawSearchTable(ruleTable); break;
            case Mode.Trait: changed = RuleTableDrawers.DrawTraitTable(ruleTable); break;
            case Mode.Grade: changed = RuleTableDrawers.DrawGradeTable(ruleTable); break;
        }

        if (outerScroll) EditorGUILayout.EndScrollView();

        if (changed)
        {
            EditorUtility.SetDirty(ruleTable.targetObject);
            dirty = true;
        }
    }

    // 위쪽 두 줄. 창이 좁아도 버튼이 밀려나지 않도록, 탭은 남는 폭을 나눠 쓰고 버튼은 항상 오른쪽 끝에 고정 폭으로 둔다.
    //  1줄: 모드 탭 .............. [새로고침] [저장]
    //  2줄: 세부 탭 ... [검색칸] [+ 새 항목]   (아이템/몬스터 모드에서만)
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        int newMode = GUILayout.Toolbar((int)mode, ModeNames, EditorStyles.toolbarButton);
        if (newMode != (int)mode)
        {
            Save();
            mode = (Mode)newMode;
            scroll = Vector2.zero;
            Refresh();
        }

        if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(64))) Refresh();
        if (GUILayout.Button(dirty ? "저장 *" : "저장", EditorStyles.toolbarButton, GUILayout.Width(56))) Save();
        EditorGUILayout.EndHorizontal();

        if (mode == Mode.Item || mode == Mode.Monster)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (mode == Mode.Item)
                itemTab = GUILayout.Toolbar(itemTab, ItemTabNames, EditorStyles.toolbarButton);
            else
                monsterTab = GUILayout.Toolbar(monsterTab, MonsterTabNames, EditorStyles.toolbarButton);

            GUILayout.Label("검색", GUILayout.Width(30));
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(70), GUILayout.MaxWidth(170));

            // 새 항목 추가: 에셋을 만들고 도감에 자동 등록
            if (GUILayout.Button(mode == Mode.Item ? "+ 새 아이템" : "+ 새 몬스터", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                if (mode == Mode.Item) CreateNewItem();
                else CreateNewMonster();
            }
            EditorGUILayout.EndHorizontal();
        }
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
        if (ResourceTableDrawer.Draw(resourceTable, ref scroll, true))
        {
            EditorUtility.SetDirty(resourceTable.targetObject);
            dirty = true;
        }
    }

    // ===== [아이템] / [몬스터] 탭: 에셋 하나가 한 줄 (엑셀처럼 머리글과 왼쪽 ID/이름 열 고정) =====
    private const float RowHeight = 26f;
    private const float HeaderHeight = 34f;
    private const float CellGap = 4f;
    private const float InsertButtonWidth = 26f;
    private const int FrozenColumns = 2; // 왼쪽에 고정할 열 수 (ID, 이름)

    private Col[] ColsForTab()
    {
        if (mode == Mode.Item)
        {
            switch (itemTab)
            {
                case 1: return ItemTool;
                case 2: return ItemStats;
                case 3: return ItemRecipe;
                case 4: return ItemUse;
                case 5: return ItemRepair;
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
        if (mode == Mode.Item && itemTab == 5) return "수리 재료 (아이템 ID x 수량)";
        if (mode == Mode.Monster && monsterTab == 2) return "드랍 (아이템 ID x 최소~최대 @확률%)";
        if (mode == Mode.Monster && monsterTab == 3) return "출현 (지역 ID / 비중)";
        if (mode == Mode.Monster && monsterTab == 4) return "도려내서 얻는 것 (아이템 ID x 최소~최대 @확률%)";
        return null;
    }

    private string NamePath() { return mode == Mode.Item ? "itemName" : "monsterName"; }

    private bool MatchesSearch(int id, string rowName)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        string s = search.Trim();
        return (rowName != null && rowName.Contains(s)) || id.ToString() == s;
    }

    private string ItemNameOf(int id)
    {
        return itemNames.TryGetValue(id, out string found) ? found : "(없음)";
    }

    // 한 줄 안에서 칸을 왼쪽부터 차례로 놓는 도우미. 칸마다 정해진 자리에 놓이므로 머리글과 어긋나지 않는다.
    private struct Cursor
    {
        public float x;
        public Rect row;
        public Cursor(Rect row) { this.row = row; x = row.x; }

        public Rect Take(float width)
        {
            Rect r = new Rect(x, row.y + 2f, width, row.height - 4f);
            x += width + CellGap;
            return r;
        }

        public void Skip(float width) { x += width; }
    }

    private int pendingInsertId = -1; // 줄 앞의 [+] 버튼으로 요청된 "끼워 넣기" 번호

    private void DrawRowsMode()
    {
        if (rows.Count == 0)
        {
            EditorGUILayout.HelpBox(mode == Mode.Item
                ? "Item 에셋이 없습니다. 위의 [+ 새 아이템]을 누르거나 Project 창에서 Create > Soul > Item 으로 만드세요."
                : "Monster 에셋이 없습니다. 위의 [+ 새 몬스터]를 누르거나 Project 창에서 Create > Soul > Monster 로 만드세요.", MessageType.Info);
            return;
        }

        Col[] cols = ColsForTab();
        int frozenCount = Mathf.Min(FrozenColumns, cols.Length);

        // 보여 줄 줄 (검색 결과)
        List<SerializedObject> view = new List<SerializedObject>();
        foreach (SerializedObject so in rows)
        {
            if (so.targetObject == null) continue;
            so.Update();
            if (MatchesSearch(so.FindProperty("id").intValue, so.FindProperty(NamePath()).stringValue)) view.Add(so);
        }

        // 겹치는 id 찾기 (빨간색으로 표시)
        HashSet<int> duplicates = new HashSet<int>(
            rows.Where(r => r.targetObject != null)
                .GroupBy(r => r.FindProperty("id").intValue).Where(g => g.Count() > 1).Select(g => g.Key));

        // 너비 계산: 왼쪽 고정 부분 = [삽입 버튼] + ID + 이름, 나머지는 스크롤
        float frozenW = InsertButtonWidth + CellGap + 4f;
        for (int i = 0; i < frozenCount; i++) frozenW += cols[i].width + CellGap;

        float scrollCols = 0f;
        for (int i = frozenCount; i < cols.Length; i++) scrollCols += cols[i].width + CellGap;

        float extraW = 0f;
        foreach (SerializedObject so in view) extraW = Mathf.Max(extraW, ExtraWidth(so));
        string extraHeader = ExtraHeader();
        if (extraHeader != null) extraW = Mathf.Max(extraW, 240f); // 머리글이 보일 최소 너비
        float scrollW = scrollCols + extraW + 24f;

        Rect area = GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUIStyle header = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };

        FrozenGrid.Draw(area, ref scroll, view.Count, frozenW, scrollW, RowHeight, HeaderHeight,
            // 왼쪽 고정 머리글
            r =>
            {
                float x = InsertButtonWidth + CellGap;
                for (int i = 0; i < frozenCount; i++)
                {
                    GUI.Label(new Rect(x, 0f, cols[i].width, r.height), new GUIContent(cols[i].title, cols[i].tip), header);
                    x += cols[i].width + CellGap;
                }
            },
            // 스크롤 머리글 (가로 스크롤을 따라 움직임)
            r =>
            {
                float x = r.x;
                for (int i = frozenCount; i < cols.Length; i++)
                {
                    GUI.Label(new Rect(x, 0f, cols[i].width, r.height), new GUIContent(cols[i].title, cols[i].tip), header);
                    x += cols[i].width + CellGap;
                }
                if (extraHeader != null)
                    GUI.Label(new Rect(x, 0f, extraW, r.height), extraHeader, header);
            },
            // 왼쪽 고정 줄: [끼워 넣기] ID 이름
            (r, i) => DrawFrozenCells(r, view[i], cols, frozenCount, duplicates),
            // 스크롤 줄: 나머지 칸 + 목록형 칸
            (r, i) => DrawScrollCells(r, view[i], cols, frozenCount));

        if (duplicates.Count > 0)
            EditorGUILayout.HelpBox("ID가 겹치는 항목이 있습니다 (빨간색). 겹치면 도감에서 하나만 쓰입니다.", MessageType.Warning);

        HandlePendingInsert();
    }

    private void DrawFrozenCells(Rect row, SerializedObject so, Col[] cols, int frozenCount, HashSet<int> duplicates)
    {
        so.Update();
        int id = so.FindProperty("id").intValue;
        Cursor c = new Cursor(row);

        // 이 줄 위에 새 항목을 끼워 넣는 버튼 (엑셀의 "줄 삽입")
        if (GUI.Button(c.Take(InsertButtonWidth), new GUIContent("+", "이 줄 위에 새 항목을 끼워 넣습니다 (이 번호와 뒤의 번호가 1씩 밀림)")))
            pendingInsertId = id;

        EditorGUI.BeginChangeCheck();
        for (int k = 0; k < frozenCount; k++)
        {
            Color prev = GUI.backgroundColor;
            if (cols[k].path == "id" && duplicates.Contains(id)) GUI.backgroundColor = Color.red;
            TableField.Draw(c.Take(cols[k].width), so.FindProperty(cols[k].path));
            GUI.backgroundColor = prev;
        }
        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
            dirty = true;
        }
    }

    private void DrawScrollCells(Rect row, SerializedObject so, Col[] cols, int frozenCount)
    {
        so.Update();
        Cursor c = new Cursor(row);

        EditorGUI.BeginChangeCheck();
        for (int k = frozenCount; k < cols.Length; k++)
            TableField.Draw(c.Take(cols[k].width), so.FindProperty(cols[k].path));

        DrawExtras(ref c, so); // 재료, 드랍, 출현 지역처럼 개수가 정해져 있지 않은 칸
        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
            dirty = true;
        }
    }

    // ===== 목록형 칸 (재료/도구/드랍/출현 지역). 칸 개수에 따라 오른쪽으로 길어진다 =====
    private void DrawExtras(ref Cursor c, SerializedObject so)
    {
        if (mode == Mode.Item && itemTab == 3)
        {
            DrawIngredients(ref c, so, "recipe.ingredients");
            DrawRecipeTools(ref c, so);
        }
        else if (mode == Mode.Item && itemTab == 5) DrawIngredients(ref c, so, "repairIngredients");
        else if (mode == Mode.Monster && monsterTab == 2) DrawDrops(ref c, so, "drops");
        else if (mode == Mode.Monster && monsterTab == 3) DrawSpawns(ref c, so);
        else if (mode == Mode.Monster && monsterTab == 4) DrawDrops(ref c, so, "carveDrops");
    }

    // 목록형 칸이 차지할 너비 (스크롤 영역의 전체 너비를 정하는 데 씀)
    private float ExtraWidth(SerializedObject so)
    {
        if (mode == Mode.Item && itemTab == 3)
        {
            SerializedProperty ing = so.FindProperty("recipe.ingredients");
            SerializedProperty tools = so.FindProperty("recipe.tools");
            return (ing != null ? ing.arraySize : 0) * 210f + 40f + 40f + (tools != null ? tools.arraySize : 0) * 180f + 40f;
        }
        if (mode == Mode.Item && itemTab == 5)
        {
            SerializedProperty rep = so.FindProperty("repairIngredients");
            return (rep != null ? rep.arraySize : 0) * 210f + 60f;
        }
        if (mode == Mode.Monster && (monsterTab == 2 || monsterTab == 4))
        {
            SerializedProperty drops = so.FindProperty(monsterTab == 2 ? "drops" : "carveDrops");
            return (drops != null ? drops.arraySize : 0) * 240f + 50f;
        }
        if (mode == Mode.Monster && monsterTab == 3)
        {
            SerializedProperty spawns = so.FindProperty("spawns");
            return (spawns != null ? spawns.arraySize : 0) * 160f + 50f;
        }
        return 0f;
    }

    private static void SmallLabel(Rect r, string text)
    {
        EditorGUI.LabelField(r, text, EditorStyles.miniLabel);
    }

    // 아이템 레시피: 재료를 [아이템ID (이름) x 수량 -] 로 나열하고 [+]로 추가
    private void DrawIngredients(ref Cursor c, SerializedObject so, string path)
    {
        SerializedProperty list = so.FindProperty(path);
        if (list == null) return;

        int removeAt = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty ing = list.GetArrayElementAtIndex(i);
            SerializedProperty idProp = ing.FindPropertyRelative("itemId");
            TableField.Draw(c.Take(40), idProp);
            SmallLabel(c.Take(70), ItemNameOf(idProp.intValue));
            SmallLabel(c.Take(10), "x");
            TableField.Draw(c.Take(38), ing.FindPropertyRelative("amount"));
            if (GUI.Button(c.Take(22), "-")) removeAt = i;
            c.Skip(10);
        }
        if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);

        if (GUI.Button(c.Take(26), "+"))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("itemId").intValue = 0;
            added.FindPropertyRelative("amount").intValue = 1;
        }
    }

    // 아이템 레시피의 필요 도구: [종류 T티어 -내구도소모 x] [+]. 제작하면 도구의 내구도만 깎인다.
    private void DrawRecipeTools(ref Cursor c, SerializedObject so)
    {
        SerializedProperty list = so.FindProperty("recipe.tools");
        if (list == null) return;

        c.Skip(14);
        EditorGUI.LabelField(c.Take(30), "도구", EditorStyles.miniBoldLabel);

        int removeAt = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty t = list.GetArrayElementAtIndex(i);
            TableField.Draw(c.Take(76), t.FindPropertyRelative("toolType"));
            SmallLabel(c.Take(10), "T");
            TableField.Draw(c.Take(30), t.FindPropertyRelative("tier"));
            SmallLabel(c.Take(8), "-");
            SerializedProperty cost = t.FindPropertyRelative("durabilityCost");
            TableField.Draw(c.Take(34), cost);
            cost.intValue = Mathf.Max(1, cost.intValue);
            if (GUI.Button(c.Take(22), "x")) removeAt = i;
            c.Skip(8);
        }
        if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);

        if (GUI.Button(c.Take(26), "+"))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("tier").intValue = 0;
            added.FindPropertyRelative("durabilityCost").intValue = 1;
        }
    }

    // 몬스터 드랍/도려내기: [아이템ID (이름) x 최소~최대 @확률% -]
    private void DrawDrops(ref Cursor c, SerializedObject so, string listPath)
    {
        SerializedProperty list = so.FindProperty(listPath);
        if (list == null) return;

        int removeAt = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty d = list.GetArrayElementAtIndex(i);
            SerializedProperty idProp = d.FindPropertyRelative("itemId");
            TableField.Draw(c.Take(38), idProp);
            SmallLabel(c.Take(62), ItemNameOf(idProp.intValue));
            SmallLabel(c.Take(10), "x");
            TableField.Draw(c.Take(32), d.FindPropertyRelative("amountMin"));
            SmallLabel(c.Take(10), "~");
            TableField.Draw(c.Take(32), d.FindPropertyRelative("amountMax"));
            SmallLabel(c.Take(12), "@");
            TableField.Draw(c.Take(44), d.FindPropertyRelative("chance"));
            SmallLabel(c.Take(14), "%");
            if (GUI.Button(c.Take(22), "-")) removeAt = i;
            c.Skip(8);
        }
        if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);

        if (GUI.Button(c.Take(26), "+"))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("itemId").intValue = 0;
            added.FindPropertyRelative("amountMin").intValue = 1;
            added.FindPropertyRelative("amountMax").intValue = 1;
            added.FindPropertyRelative("chance").floatValue = 100f;
        }
    }

    // 몬스터 출현 지역: [지역 ID / 비중 -]
    private void DrawSpawns(ref Cursor c, SerializedObject so)
    {
        SerializedProperty list = so.FindProperty("spawns");
        if (list == null) return;

        int removeAt = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty s = list.GetArrayElementAtIndex(i);
            SmallLabel(c.Take(28), "지역");
            TableField.Draw(c.Take(38), s.FindPropertyRelative("regionId"));
            SmallLabel(c.Take(28), "비중");
            TableField.Draw(c.Take(38), s.FindPropertyRelative("weight"));
            if (GUI.Button(c.Take(22), "-")) removeAt = i;
            c.Skip(10);
        }
        if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);

        if (GUI.Button(c.Take(26), "+"))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("regionId").intValue = 1;
            added.FindPropertyRelative("weight").intValue = 1;
        }
    }

    // ===== 줄 끼워 넣기: 번호를 1씩 밀고 그 자리에 새 항목을 만든다 =====
    private void HandlePendingInsert()
    {
        if (pendingInsertId < 0) return;
        int id = pendingInsertId;
        pendingInsertId = -1;

        if (mode == Mode.Item)
        {
            int moving = IdInsert.CountItemsFrom(id);
            if (!EditorUtility.DisplayDialog("아이템 끼워 넣기",
                $"{id}번 자리에 새 아이템을 끼워 넣습니다.\n\n{id}번부터 뒤의 아이템 {moving}개의 번호가 1씩 밀립니다. " +
                "레시피의 재료, 몬스터 드랍, 자원 표에서 그 번호를 쓰던 곳도 함께 바뀝니다.\n\n" +
                "되돌리기 쉽도록 실행 전에 git 커밋을 해 두는 것을 권장합니다.", "끼워 넣기", "취소"))
                return;

            Save();
            IdInsert.ShiftItemIds(id);
            CreateNewItem(id);
            DatabaseSync.NormalizeItemFileNames(); // 번호가 바뀐 아이템의 파일 이름도 맞춤
        }
        else if (mode == Mode.Monster)
        {
            int moving = IdInsert.CountMonstersFrom(id);
            if (!EditorUtility.DisplayDialog("몬스터 끼워 넣기",
                $"{id}번 자리에 새 몬스터를 끼워 넣습니다.\n\n{id}번부터 뒤의 몬스터 {moving}개의 번호가 1씩 밀립니다.", "끼워 넣기", "취소"))
                return;

            Save();
            IdInsert.ShiftMonsterIds(id);
            CreateNewMonster(id);
            DatabaseSync.NormalizeMonsterFileNames();
        }

        Refresh();
        GUIUtility.ExitGUI(); // 줄 목록이 바뀌었으니 이번 그리기를 여기서 끝낸다
    }
}
