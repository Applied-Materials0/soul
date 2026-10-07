using System.Linq;
using UnityEditor;
using UnityEngine;

// 아이템/몬스터 도감(ItemDatabase, MonsterDatabase)에 에셋을 자동으로 등록한다.
// 새 Item/Monster 에셋이 생기면(데이터 표의 [+ 새 ...] 버튼이든 Create 메뉴든) 도감에 저절로 들어가므로,
// "Collect All" 버튼을 누르는 것을 잊어서 제작 창/채집/드랍에 안 나오는 일이 없다.
public static class DatabaseSync
{
    private static T[] FindAll<T>() where T : Object
    {
        return AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(a => a != null)
            .ToArray();
    }

    // 프로젝트의 모든 Item/Monster를 도감에 맞춘다. 바뀐 것이 있으면 true
    public static bool SyncAll()
    {
        bool a = SyncItems();
        bool b = SyncMonsters();
        if (a || b) AssetDatabase.SaveAssets();
        return a || b;
    }

    // 도감 에셋이 하나도 없으면 에셋이 있는 폴더(없으면 fallbackFolder)에 만든다
    private static T FindOrCreateDatabase<T>(string assetName, string anyAssetFolder) where T : ScriptableObject
    {
        string guid = AssetDatabase.FindAssets("t:" + typeof(T).Name).FirstOrDefault();
        if (guid != null) return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        if (string.IsNullOrEmpty(anyAssetFolder)) return null; // 등록할 에셋이 아직 없으면 만들지 않음

        T db = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(db, $"{anyAssetFolder}/{assetName}.asset");
        return db;
    }

    private static string FolderOfFirst<T>() where T : Object
    {
        string guid = AssetDatabase.FindAssets("t:" + typeof(T).Name).FirstOrDefault();
        if (guid == null) return null;
        return System.IO.Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guid)).Replace('\\', '/');
    }

    // =========================================================
    //  아이템 에셋 파일 이름: 항상 "번호_이름" (번호는 3자리, 예: ID 14, 이름 물통 -> 014_물통.asset)
    // =========================================================
    public static string ItemFileName(Item item)
    {
        string name = string.IsNullOrWhiteSpace(item.itemName) ? "이름없음" : item.itemName.Trim();

        // 파일 이름에 쓸 수 없는 글자는 뺀다
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "");
        return $"{item.id:000}_{name}";
    }

    // 모든 아이템 에셋의 파일 이름을 "번호_이름"에 맞춘다. 바꾼 것이 있으면 true
    // (GUID는 그대로라서 레시피, 도감, 씬의 연결은 끊기지 않는다)
    public static bool NormalizeItemFileNames()
    {
        bool renamed = false;
        foreach (string guid in AssetDatabase.FindAssets("t:Item"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Item item = AssetDatabase.LoadAssetAtPath<Item>(path);
            if (item == null) continue;

            string expected = ItemFileName(item);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == expected) continue;

            string error = AssetDatabase.RenameAsset(path, expected);
            if (string.IsNullOrEmpty(error)) renamed = true;
            else Debug.LogWarning($"[DatabaseSync] '{path}' 파일 이름을 '{expected}'로 바꾸지 못했습니다: {error}");
        }
        return renamed;
    }

    // =========================================================
    //  스프라이트: 아이템 ID에 맞는 4자리 이름의 스프라이트를 스프라이트 폴더에서 찾는다 (ID 14 -> 0014)
    // =========================================================
    private const string SpriteFolder = "Assets/2Sprites";

    public static Sprite FindSpriteForId(int id)
    {
        if (!AssetDatabase.IsValidFolder(SpriteFolder)) return null;

        string wanted = id.ToString("0000");
        foreach (string guid in AssetDatabase.FindAssets(wanted, new[] { SpriteFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != wanted) continue;

            // 이미지 파일 안의 첫 번째 스프라이트 (여러 개로 잘린 이미지여도 첫 번째를 쓴다)
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite != null) return sprite;
        }
        return null;
    }

    public static bool SyncItems()
    {
        Item[] all = FindAll<Item>();
        ItemDatabase db = FindOrCreateDatabase<ItemDatabase>("ItemDatabase", FolderOfFirst<Item>());
        if (db == null) return false;

        bool changed = db.items.RemoveAll(i => i == null) > 0;
        foreach (Item item in all)
        {
            if (db.items.Contains(item)) continue;
            db.items.Add(item);
            changed = true;
        }
        if (changed)
        {
            db.items = db.items.OrderBy(i => i.id).ToList();
            EditorUtility.SetDirty(db);
        }
        return changed;
    }

    public static bool SyncMonsters()
    {
        Monster[] all = FindAll<Monster>();
        MonsterDatabase db = FindOrCreateDatabase<MonsterDatabase>("MonsterDatabase", FolderOfFirst<Monster>());
        if (db == null) return false;

        bool changed = db.monsters.RemoveAll(m => m == null) > 0;
        foreach (Monster m in all)
        {
            if (db.monsters.Contains(m)) continue;
            db.monsters.Add(m);
            changed = true;
        }
        if (changed)
        {
            db.monsters = db.monsters.OrderBy(m => m.id).ToList();
            EditorUtility.SetDirty(db);
        }
        return changed;
    }
}

// 에셋이 새로 생기거나 지워질 때마다 도감을 맞춘다
public class DatabaseSyncPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        bool relevant = imported.Any(p => p.EndsWith(".asset")) || deleted.Any(p => p.EndsWith(".asset"));
        if (!relevant) return;

        // 가져오기가 끝난 뒤에 처리한다 (여기서 바로 에셋을 저장하면 가져오기와 겹침)
        EditorApplication.delayCall -= Run;
        EditorApplication.delayCall += Run;
    }

    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        // 아이템 파일 이름을 "번호_이름"으로 맞추고, 도감에 등록한다. 바뀐 것이 없으면 아무 일도 안 하므로 반복되지 않음
        DatabaseSync.NormalizeItemFileNames();
        DatabaseSync.SyncAll();
    }
}
