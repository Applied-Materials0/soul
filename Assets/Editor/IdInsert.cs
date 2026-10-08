using System.Linq;
using UnityEditor;
using UnityEngine;

// 표의 중간에 새 항목을 끼워 넣는다 (엑셀에서 줄을 삽입하는 것과 같음).
// 끼워 넣은 자리의 번호(ID)와 그 뒤 항목들의 번호가 1씩 밀리고, 그 번호를 가리키던 곳도 같이 바뀐다.
//  - 아이템: 레시피 재료, 사용 후 남는 아이템, 몬스터의 드랍/도려내기 얻는 것, 자원 표의 아이템(기본 + 추가 획득)
//  - 몬스터: 다른 곳에서 번호로 가리키지 않아 몬스터 번호만 밀린다
public static class IdInsert
{
    private static T[] FindAll<T>() where T : Object
    {
        return AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(a => a != null)
            .ToArray();
    }

    // 번호가 insertId 이상인 모든 아이템의 번호와, 그 번호를 가리키는 모든 곳을 1씩 민다.
    // 이 함수가 끝나면 insertId 번호가 비어 있으니, 호출한 쪽이 새 아이템을 만들면 된다.
    public static void ShiftItemIds(int insertId)
    {
        Item[] items = FindAll<Item>();

        // 1) 아이템 번호 밀기 (큰 번호부터 밀어서 중간에 번호가 겹치지 않게)
        foreach (Item item in items.Where(i => i.id >= insertId).OrderByDescending(i => i.id))
        {
            Undo.RecordObject(item, "Insert Item");
            item.id++;
            EditorUtility.SetDirty(item);
        }

        // 2) 레시피: 재료의 번호, 완성품 번호(= 자기 자신의 번호)
        foreach (Item item in items)
        {
            if (item.useResultItemId >= insertId) { Undo.RecordObject(item, "Insert Item"); item.useResultItemId++; EditorUtility.SetDirty(item); }
            if (item.repairItemId >= insertId) { Undo.RecordObject(item, "Insert Item"); item.repairItemId++; EditorUtility.SetDirty(item); }
            if (item.repairIngredients != null)
                foreach (Ingredient ri in item.repairIngredients)
                    if (ri != null && ri.itemId >= insertId) { Undo.RecordObject(item, "Insert Item"); ri.itemId++; EditorUtility.SetDirty(item); }
            if (item.recipe == null) continue;
            Undo.RecordObject(item, "Insert Item");
            foreach (Ingredient ing in item.recipe.ingredients)
                if (ing != null && ing.itemId >= insertId) ing.itemId++;
            item.recipe.resultItemId = item.id;
            EditorUtility.SetDirty(item);
        }

        // 3) 몬스터: 드랍과 도려내기로 얻는 아이템
        foreach (Monster monster in FindAll<Monster>())
        {
            Undo.RecordObject(monster, "Insert Item");
            foreach (MonsterDrop d in monster.drops) if (d != null && d.itemId >= insertId) d.itemId++;
            foreach (MonsterDrop d in monster.carveDrops) if (d != null && d.itemId >= insertId) d.itemId++;
            EditorUtility.SetDirty(monster);
        }

        // 4) 자원 표: 기본 아이템과 추가 획득 아이템
        foreach (ResourceSpawnTable table in FindAll<ResourceSpawnTable>())
        {
            Undo.RecordObject(table, "Insert Item");
            foreach (ResourceSpawn e in table.entries)
            {
                if (e == null) continue;
                if (e.itemId >= insertId) e.itemId++;
                foreach (ResourceExtraYield y in e.extraYields) if (y != null && y.itemId >= insertId) y.itemId++;
            }
            EditorUtility.SetDirty(table);
        }

        AssetDatabase.SaveAssets();
    }

    // 번호가 insertId 이상인 모든 몬스터의 번호를 1씩 민다
    public static void ShiftMonsterIds(int insertId)
    {
        foreach (Monster monster in FindAll<Monster>().Where(m => m.id >= insertId).OrderByDescending(m => m.id))
        {
            Undo.RecordObject(monster, "Insert Monster");
            monster.id++;
            EditorUtility.SetDirty(monster);
        }
        AssetDatabase.SaveAssets();
    }

    // 뒤에서 밀릴 항목 수 (확인 창에 보여 주기 위함)
    public static int CountItemsFrom(int insertId) { return FindAll<Item>().Count(i => i.id >= insertId); }
    public static int CountMonstersFrom(int insertId) { return FindAll<Monster>().Count(m => m.id >= insertId); }
}
